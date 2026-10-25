using System;
using System.Collections.Generic;
using Apex.AI;
using Apex.Core;
using Apex.Race;
using Apex.Tracks;
using Apex.Vehicle;
using Apex.Weather;
using UnityEngine;

namespace Apex.Game
{
    /// <summary>Runtime state of one car in a session (player or AI).</summary>
    public sealed class RaceCar
    {
        public int index;
        public bool isPlayer;
        public int driverIndex = -1;
        public string name;
        public string code;
        public int teamIndex;
        public int number;
        public VehicleController car;
        /// <summary>AI driver; for the player only used by the pit assist.</summary>
        public AIDriver ai;
        public RaceDirector.Entry entry;
        public readonly PitState pit = new PitState();
        public int hint = -1;
        public CarState state;
        public float raw;
        public float lateral;
        public float offTrackTime;
        public bool offTrackCounted;
        public int boxIndex;
        public bool mandatoryStopDone;
        public float startHold;
        public float lastStopTime;

        public VehicleSimulation Sim => car.Sim;
        public bool AutoDriven => !isPlayer || pitAssistActive;
        public bool pitAssistActive;
    }

    /// <summary>
    /// Runs a session with any number of cars: start lights, AI driving and traffic,
    /// timing and classification via <see cref="RaceDirector"/>, track limits, pit
    /// requests and stops, tire wear and weather. Executes before the vehicles in
    /// FixedUpdate so inputs apply to the coming physics step.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class RaceController : MonoBehaviour
    {
        public const float TrackLimitGrace = 0.25f;

        public CircuitInstance Circuit { get; private set; }
        public SessionConfig Config { get; private set; }
        public SessionType Type { get; private set; }
        public RaceDirector Director { get; private set; }
        public WeatherModel Weather { get; private set; }
        public RacingLine Line { get; private set; }
        public readonly List<RaceCar> Cars = new List<RaceCar>();
        public RaceCar Player { get; private set; }
        public InputRouter Input { get; set; }
        public bool PitAssist { get; set; } = true;
        public bool Started { get; private set; }
        /// <summary>False while paused for calibration, menus etc. Holds the player car.</summary>
        public bool PlayerInputEnabled { get; set; } = true;
        public double SimTime { get; private set; }
        public bool PlayerFinished => Player != null && Player.entry.finished;

        /// <summary>(text, colour, priority: 0 info, 1 race, 2 penalty/flag)</summary>
        public event Action<string, Color, int> Message;
        public event Action SessionComplete;

        PitCrew crew;
        readonly List<CarState> states = new List<CarState>();
        double playerFinishedAt = -1;
        bool completeRaised;
        float strategyTimer;
        float flagTimer;

        public void Setup(CircuitInstance circuit, SessionConfig config, SessionType type, IList<int> gridDrivers, Color playerPaint, int playerNumber, string playerName)
        {
            Circuit = circuit;
            Config = config;
            Type = type;
            var g = circuit.Geometry;
            var setup = new VehicleSetup();
            Line = new RacingLine(g, 1.45f, setup.downforceArea * setup.airDensity * 0.5f / setup.mass);
            Weather = new WeatherModel(config.weather, config.dynamicWeather, config.seed);
            Weather.StateChanged += s => Message?.Invoke("Wetter: " + WeatherModel.Name(s), ApexStyle.Caution, 1);
            Director = new RaceDirector(config.MakeRules(type), g.Length, circuit.SectorStarts, config.seed);
            crew = new PitCrew(config.seed + 11);
            Director.PenaltyIssued += (e, s, r) => { if (e.isPlayer) Message?.Invoke($"Strafe +{s:0} s: {r}", ApexStyle.Urgent, 2); };
            Director.Warning += (e, r) => { if (e.isPlayer) Message?.Invoke(r, ApexStyle.Caution, 2); };
            Director.FastestLapSet += (e, t) => Message?.Invoke($"Schnellste Runde: {e.code} {ApexStyle.FormatTime(t)}", ApexStyle.Best, 1);
            Director.EntryFinished += OnEntryFinished;

            var compound = CompoundForConditions(Weather.TrackWetness, config.startCompound);
            int slot = 0;
            foreach (int di in gridDrivers)
            {
                var rc = new RaceCar { index = Cars.Count, isPlayer = di < 0, driverIndex = di };
                TeamInfo team;
                if (rc.isPlayer)
                {
                    rc.teamIndex = config.playerTeam;
                    team = Roster.Teams[rc.teamIndex];
                    rc.name = string.IsNullOrEmpty(playerName) ? "Du" : playerName;
                    rc.code = "DU";
                    rc.number = playerNumber;
                }
                else
                {
                    var d = Roster.Drivers[di];
                    rc.teamIndex = d.team;
                    team = Roster.Teams[d.team];
                    rc.name = d.name;
                    rc.code = d.code;
                    rc.number = d.number;
                }
                var primary = rc.isPlayer ? playerPaint : new Color(team.r, team.g, team.b);
                var accent = new Color(team.ar, team.ag, team.ab);
                rc.car = CarBuilder.Build((rc.isPlayer ? "Player_" : "AI_") + rc.code, new VehicleSetup(), primary, accent);
                rc.car.transform.SetParent(transform, false);
                var pose = circuit.GridPose(slot, rc.car.setup.cogHeight + 0.05f);
                rc.car.ResetTo(pose.position, pose.rotation);
                var sim = rc.Sim;
                sim.FitTires(compound);
                sim.TireWearMultiplier = config.tireWear;
                sim.holdBrake = true;
                if (!rc.isPlayer)
                {
                    sim.assistSteering = true;
                    sim.assistTraction = true;
                    sim.assistAbs = true;
                    sim.autoGearbox = true;
                }
                var personality = rc.isPlayer ? AIPersonality.Make(0.85f, 0.2f, 1f, 0.5f, 0.5f) : Roster.Drivers[di].personality;
                personality.pace *= rc.isPlayer ? 1f : Roster.DifficultyScale(config.difficulty);
                rc.ai = new AIDriver(Line, personality, config.seed * 31 + rc.index);
                rc.entry = Director.AddEntry(rc.index, rc.name, rc.code, team.name, rc.isPlayer, slot);
                rc.boxIndex = Mathf.Clamp(rc.teamIndex * 2 + (rc.isPlayer ? 0 : (di % 2)), 0, 19);
                // Practice/qualifying: AI cars leave the grid one after another.
                rc.startHold = type == SessionType.Race || rc.isPlayer ? 0f : 2f + slot * 2.5f;
                if (rc.isPlayer) Player = rc;
                Cars.Add(rc);
                slot++;
            }
            ApplyConditions();
        }

        /// <summary>Starts lights (race) or the session clock.</summary>
        public void Begin()
        {
            if (Started) return;
            Started = true;
            Director.Start(SimTime);
            if (Type == SessionType.Race)
                Director.LightsOut += () => Message?.Invoke("LIGHTS OUT", ApexStyle.Faster, 1);
        }

        static TireCompound CompoundForConditions(float wetness, TireCompound dry)
        {
            if (wetness > 0.65f) return TireCompound.Wet;
            if (wetness > 0.25f) return TireCompound.Intermediate;
            return dry == TireCompound.Intermediate || dry == TireCompound.Wet ? TireCompound.Medium : dry;
        }

        void ApplyConditions()
        {
            foreach (var rc in Cars)
            {
                rc.Sim.trackWetness = Weather.TrackWetness;
                rc.Sim.trackTemperature = Weather.TrackTemp;
            }
        }

        public void RequestPlayerPit(TireCompound compound)
        {
            if (Player == null || Circuit.Pit == null) return;
            if (Player.pit.stage == PitState.Stage.Requested && Player.pit.nextCompound == compound)
            {
                Player.pit.Cancel();
                Message?.Invoke("Boxenstopp abgesagt", Color.white, 1);
                return;
            }
            Player.pit.Request(compound);
            Message?.Invoke($"Box, Box – diese Runde ({CompoundSpec.For(compound).name})", ApexStyle.Accent, 1);
        }

        /// <summary>Practice/time trial only: put the player car back on track.</summary>
        public void ResetPlayerToTrack()
        {
            if (Player == null || Type == SessionType.Race) return;
            var proj = Circuit.Geometry.Project(Player.car.transform.position.ToNum(), Player.hint);
            var pose = Circuit.PoseAt(proj.distance - 5f, 0f, Player.car.setup.cogHeight + 0.08f);
            var compoundTires = Player.Sim.tires;
            var temps = new float[4];
            var wear = new float[4];
            for (int i = 0; i < 4; i++) { temps[i] = compoundTires[i].temperature; wear[i] = compoundTires[i].wear; }
            Player.car.ResetTo(pose.position, pose.rotation);
            for (int i = 0; i < 4; i++) { compoundTires[i].temperature = temps[i]; compoundTires[i].wear = wear[i]; }
            Player.entry.timer.ResetPosition();
            Player.hint = -1;
            Player.ai.ResetTracking();
            Message?.Invoke("Auto auf die Strecke gesetzt", Color.white, 0);
        }

        void FixedUpdate()
        {
            if (Circuit == null) return;
            float dt = Time.fixedDeltaTime;
            var g = Circuit.Geometry;
            if (Started) SimTime += dt;
            Weather.Update(SimTime, dt);
            ApplyConditions();

            states.Clear();
            foreach (var rc in Cars)
            {
                var pos = rc.car.transform.position;
                var proj = g.Project(pos.ToNum(), rc.hint);
                rc.hint = proj.index;
                rc.raw = proj.distance;
                rc.lateral = proj.lateral;
                rc.state = new CarState
                {
                    id = rc.index,
                    distance = proj.distance,
                    lateral = proj.lateral,
                    speed = rc.Sim.ForwardSpeed,
                    position = pos.ToNum(),
                    forward = rc.car.transform.forward.ToNum(),
                    inPit = rc.pit.InPitLane,
                };
                states.Add(rc.state);
            }

            foreach (var rc in Cars)
            {
                if (Started) Director.UpdateEntry(rc.entry, Circuit.LapDistance(rc.raw), SimTime);
                CheckTrackLimits(rc, dt);
                UpdatePit(rc, dt);
                DriveCar(rc, dt);
            }
            if (Started) Director.Tick(SimTime);
            UpdateFlags(dt);
            UpdateStrategy(dt);
            CheckSessionEnd();
        }

        void DriveCar(RaceCar rc, float dt)
        {
            var sim = rc.Sim;
            bool moveAllowed = Started && Director.CarsMayMove && SimTime >= rc.startHold && rc.pit.stage != PitState.Stage.Stopped;
            if (rc.isPlayer && !PlayerInputEnabled) moveAllowed = false;
            sim.holdBrake = !moveAllowed;
            if (!moveAllowed)
            {
                sim.throttle = sim.brake = sim.steer = 0f;
                return;
            }
            bool auto = !rc.isPlayer || rc.pitAssistActive;
            if (auto)
            {
                float grip = 0f;
                foreach (var t in sim.tires) grip += t.GripMultiplier(sim.trackWetness) / 4f;
                rc.ai.gripFactor = grip;
                rc.ai.wet = sim.trackWetness > 0.25f;
                rc.ai.maxSteerRad = rc.isPlayer && !sim.assistSteering ? sim.setup.maxSteerAngleDeg * ApexMath.Deg2Rad : sim.CurrentMaxLock();
                rc.ai.frontSlipUsage = Mathf.Max(Mathf.Abs(sim.wheels[0].slipAngle), Mathf.Abs(sim.wheels[1].slipAngle)) / sim.tireFront.slipAnglePeak;
                // After the flag AI cars cruise back.
                if (rc.entry.finished && !rc.isPlayer) rc.ai.speedCap = Mathf.Min(rc.ai.speedCap, 45f);
                var c = rc.ai.Drive(rc.state, states, dt);
                sim.throttle = c.throttle;
                sim.brake = c.brake;
                sim.steer = c.steer;
            }
            else if (Input != null)
            {
                sim.throttle = Input.Throttle;
                sim.brake = Input.Brake;
                sim.steer = Input.Steer;
            }
        }

        void CheckTrackLimits(RaceCar rc, float dt)
        {
            if (rc.pit.InPitLane) return;
            if (rc.Sim.WheelsOnTrack() == 0)
            {
                rc.offTrackTime += dt;
                if (rc.offTrackTime > TrackLimitGrace && !rc.offTrackCounted)
                {
                    rc.offTrackCounted = true;
                    rc.entry.timer.Invalidate("Tracklimits");
                    Director.TrackLimitViolation(rc.entry);
                    if (rc.isPlayer && Type != SessionType.Race) Message?.Invoke("Runde ungültig: Tracklimits", ApexStyle.Urgent, 2);
                }
            }
            else
            {
                rc.offTrackTime = 0f;
                rc.offTrackCounted = false;
            }
        }

        // --- Pit lane -------------------------------------------------------------------

        void UpdatePit(RaceCar rc, float dt)
        {
            var pit = Circuit.Pit;
            if (pit == null) return;
            var g = Circuit.Geometry;
            var sim = rc.Sim;
            var ps = rc.pit;
            float L = g.Length;
            float sinceEntry = ApexMath.PosMod(rc.raw - pit.entry, L);
            float toEntry = ApexMath.PosMod(pit.entry - rc.raw, L);
            float span = ApexMath.PosMod(pit.exit - pit.entry, L);
            float laneLat = Circuit.PitLaneLateral(rc.raw);
            float edge = g.WidthAt(rc.raw) * 0.5f;
            switch (ps.stage)
            {
                case PitState.Stage.Requested:
                {
                    bool auto = !rc.isPlayer || PitAssist;
                    if (toEntry < 160f && auto)
                    {
                        // Line up on the pit side of the track before the entry.
                        rc.pitAssistActive = rc.isPlayer;
                        rc.ai.forcedLateral = pit.side * (edge - 1.6f);
                    }
                    bool entered = auto ? sinceEntry < 20f : (sinceEntry < 80f && pit.side * rc.lateral > edge + 1f);
                    if (entered)
                    {
                        ps.stage = PitState.Stage.InLane;
                        if (rc.isPlayer) Message?.Invoke($"Boxengasse – Begrenzer {pit.speedLimitKmh:0} km/h", ApexStyle.Accent, 1);
                    }
                    else if (!auto && sinceEntry >= 80f && sinceEntry < 120f && rc.isPlayer)
                    {
                        // Missed the entry: keep the request for the next lap.
                    }
                    break;
                }
                case PitState.Stage.InLane:
                case PitState.Stage.Leaving:
                {
                    sim.speedLimit = pit.SpeedLimit;
                    bool auto = !rc.isPlayer || rc.pitAssistActive;
                    if (auto)
                    {
                        rc.ai.forcedLateral = sinceEntry > span - 25f ? float.NaN : laneLat;
                        rc.ai.speedCap = pit.SpeedLimit;
                    }
                    if (ps.stage == PitState.Stage.InLane)
                    {
                        float box = g.Wrap(pit.BoxDistance(rc.boxIndex));
                        float toBox = ApexMath.PosMod(box - rc.raw, L);
                        if (toBox > L * 0.5f) toBox -= L;
                        if (auto && toBox < 40f) rc.ai.speedCap = Mathf.Max(0f, toBox * 0.6f);
                        bool inBox = Mathf.Abs(toBox) < (auto ? 2.5f : 4f) && Mathf.Abs(rc.lateral - laneLat) < 3f;
                        if (inBox && Mathf.Abs(sim.ForwardSpeed) < 0.8f)
                        {
                            var (time, mistake) = crew.ServiceTime(ps.changeTires);
                            ps.serviceTotal = time;
                            ps.serviceRemaining = time;
                            ps.lastMistake = mistake;
                            ps.stage = PitState.Stage.Stopped;
                        }
                        else if (toBox < -6f)
                        {
                            ps.stage = PitState.Stage.Leaving;
                            if (rc.isPlayer) Message?.Invoke("Box verpasst – kein Service", ApexStyle.Caution, 2);
                        }
                    }
                    if (sinceEntry > span || sinceEntry > L * 0.9f)
                    {
                        ps.stage = PitState.Stage.None;
                        sim.speedLimit = float.PositiveInfinity;
                        rc.ai.forcedLateral = float.NaN;
                        rc.ai.speedCap = float.PositiveInfinity;
                        rc.pitAssistActive = false;
                        if (rc.isPlayer) Message?.Invoke("Boxenausfahrt", Color.white, 0);
                    }
                    break;
                }
                case PitState.Stage.Stopped:
                {
                    ps.serviceRemaining -= dt;
                    if (ps.serviceRemaining <= 0f)
                    {
                        if (ps.changeTires) sim.FitTires(ps.nextCompound);
                        rc.entry.pitStops++;
                        rc.mandatoryStopDone = true;
                        rc.lastStopTime = ps.serviceTotal;
                        ps.stage = PitState.Stage.Leaving;
                        if (rc.isPlayer)
                            Message?.Invoke($"Stopp {ps.serviceTotal:0.0} s – {CompoundSpec.For(ps.nextCompound).name}" + (ps.lastMistake ? " (Radmutter klemmte)" : ""), ApexStyle.Faster, 1);
                    }
                    break;
                }
            }
        }

        /// <summary>AI strategy: worn tires, wrong tires for the conditions, mandatory stop.</summary>
        void UpdateStrategy(float dt)
        {
            strategyTimer += dt;
            if (strategyTimer < 1f || Type != SessionType.Race || Circuit.Pit == null || !Started) return;
            strategyTimer = 0f;
            float wet = Weather.TrackWetness;
            foreach (var rc in Cars)
            {
                if (rc.isPlayer || rc.pit.stage != PitState.Stage.None || rc.entry.finished) continue;
                int lapsLeft = Director.rules.laps - rc.entry.LapsCompleted;
                if (lapsLeft <= 1) continue;
                var current = rc.Sim.tires[0].compound;
                var ideal = CompoundForConditions(wet, rc.Sim.AverageTireWear() > 0.4f ? TireCompound.Hard : TireCompound.Medium);
                bool wrongTires = CompoundSpec.For(current).wetTire != CompoundSpec.For(ideal).wetTire || (current == TireCompound.Wet) != (ideal == TireCompound.Wet);
                bool worn = rc.Sim.AverageTireWear() > 0.62f;
                bool mandatory = Director.rules.mandatoryPitStop && !rc.mandatoryStopDone && lapsLeft <= 2;
                if (wrongTires || worn || mandatory) rc.pit.Request(wrongTires ? ideal : (lapsLeft > 8 ? TireCompound.Hard : TireCompound.Medium));
            }
        }

        // --- Flags ------------------------------------------------------------------------

        void UpdateFlags(float dt)
        {
            if (Player == null || !Started || Director.CurrentPhase != RaceDirector.Phase.Racing) return;
            flagTimer -= dt;
            if (flagTimer > 0f) return;
            var g = Circuit.Geometry;
            foreach (var rc in Cars)
            {
                if (rc == Player || rc.pit.InPitLane || rc.entry.finished) continue;
                float ahead = ApexMath.PosMod(rc.raw - Player.raw, g.Length);
                bool stoppedOrOff = (rc.Sim.Speed < 8f && SimTime > rc.startHold + 10f) || Mathf.Abs(rc.lateral) > g.WidthAt(rc.raw) * 0.5f + 3f;
                if (stoppedOrOff && ahead < 350f)
                {
                    Message?.Invoke("Gelbe Flagge – Vorsicht", ApexStyle.Caution, 2);
                    flagTimer = 4f;
                    return;
                }
                if (Type == SessionType.Race)
                {
                    float behind = ApexMath.PosMod(Player.raw - rc.raw, g.Length);
                    double progressDiff = rc.entry.Progress(g.Length) - Player.entry.Progress(g.Length);
                    if (behind < 45f && progressDiff > g.Length * 0.5)
                    {
                        Message?.Invoke("Blaue Flagge – Platz machen", new Color(0.3f, 0.55f, 1f), 2);
                        flagTimer = 5f;
                        return;
                    }
                }
            }
        }

        // --- Finish -----------------------------------------------------------------------

        void OnEntryFinished(RaceDirector.Entry e)
        {
            var rc = Cars.Find(c => c.entry == e);
            if (rc != null && Director.rules.mandatoryPitStop && !rc.mandatoryStopDone)
                Director.AddPenalty(e, 20, "Pflicht-Boxenstopp fehlt");
            if (e.isPlayer)
            {
                playerFinishedAt = SimTime;
                Message?.Invoke(Type == SessionType.Race ? $"ZIELFLAGGE – P{e.position}" : "Session beendet", Color.white, 2);
            }
        }

        void CheckSessionEnd()
        {
            if (completeRaised || !Started) return;
            bool done = Director.CurrentPhase == RaceDirector.Phase.Finished;
            if (!done && playerFinishedAt >= 0 && SimTime - playerFinishedAt > 6.0)
            {
                // Classify the rest in their current running order.
                foreach (var e in new List<RaceDirector.Entry>(Director.Standings))
                    if (!e.finished && !e.retired) Director.ForceFinish(e);
                done = true;
            }
            if (done)
            {
                completeRaised = true;
                SessionComplete?.Invoke();
            }
        }

        /// <summary>Grid order for a following race from this session's standings (driver indices, -1 = player).</summary>
        public List<int> ResultOrder()
        {
            var list = new List<int>();
            foreach (var e in Director.Standings)
            {
                var rc = Cars.Find(c => c.entry == e);
                list.Add(rc.isPlayer ? -1 : rc.driverIndex);
            }
            return list;
        }
    }
}
