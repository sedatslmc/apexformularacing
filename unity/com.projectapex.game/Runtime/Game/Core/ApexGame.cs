using System;
using System.Collections;
using System.Collections.Generic;
using Apex.Career;
using Apex.Core;
using Apex.Race;
using Apex.Tracks;
using Apex.Vehicle;
using UnityEngine;
using UnityEngine.Rendering;

namespace Apex.Game
{
    public enum SessionKind
    {
        Practice,
        TimeTrial,
        GrandPrix,
        Championship,
    }

    /// <summary>
    /// Boot entry point and owner of the authoritative <see cref="GameStateMachine"/>.
    /// Loads sessions (practice, time trial, Grand Prix weekend, championship rounds),
    /// routes pause/back/radio, persists settings, career and the last Grand Prix
    /// configuration, and draws the interim IMGUI menus and broadcast HUD (partial
    /// files ApexGame.Menus.cs and ApexGame.Hud.cs). Every menu entry leads to a
    /// working feature; nothing decorative.
    /// </summary>
    public sealed partial class ApexGame : MonoBehaviour
    {
        public static readonly string[] TrackIds = { "bosphorus_a", "test_loop" };
        const string CareerFile = "career.json";
        const string ConfigFile = "session.json";

        readonly GameStateMachine sm = new GameStateMachine();
        public GameStateMachine State => sm;
        public SettingsData Settings { get; private set; }
        public CareerData Career { get; private set; }
        public SessionConfig Config { get; private set; }
        public RaceController Race { get; private set; }

        InputRouter input;
        Camera cam;
        RaceCamera raceCam;
        Light sun;
        GameObject showroom;
        VehicleController showroomCar;
        GameObject world;
        WeatherVisuals weatherVisuals;

        SessionKind kind;
        SessionType currentType;
        string trackId = "";
        bool calibrating;
        double sessionRecord;
        int startPosition;
        List<int> pendingRaceGrid;
        ResultsData lastResults;
        readonly Dictionary<string, CircuitDefinition> definitions = new Dictionary<string, CircuitDefinition>();

        // Messages: one high-priority and one low-priority toast at a time.
        struct Toast { public string text; public Color color; public float time; }
        Toast toastHigh, toastLow;

        float fps = 60f;
        float restartArmed;

        void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Time.fixedDeltaTime = 1f / 120f;
            Time.maximumDeltaTime = 1f / 15f;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;

            Settings = SaveService.LoadSettings();
            Career = SaveService.Load(CareerFile, () => new CareerData());
            if (Career.MigrateAndValidate()) SaveService.Save(CareerFile, Career);
            Config = SaveService.Load(ConfigFile, () => new SessionConfig());
            Config.Validate();
            showTelemetry = Settings.showTelemetry;
            SetupCameraAndLighting();
            input = gameObject.AddComponent<InputRouter>();
            input.ApplySettings(Settings);
            sm.Changed += OnStateChanged;
            sm.TryTransition(GameState.MainMenu);
        }

        // --- Data --------------------------------------------------------------------------

        public CircuitDefinition Definition(string id)
        {
            if (definitions.TryGetValue(id, out var d)) return d;
            var text = Resources.Load<TextAsset>("Tracks/" + id);
            if (text == null) return null;
            try
            {
                d = CircuitDefinition.FromJson(text.text);
            }
            catch (Exception e)
            {
                Debug.LogError("[Apex] Invalid track data " + id + ": " + e.Message);
                return null;
            }
            definitions[id] = d;
            return d;
        }

        public void SaveCareer() => SaveService.Save(CareerFile, Career);
        public void SaveConfig() => SaveService.Save(ConfigFile, Config);

        // --- Scene setup -------------------------------------------------------------------

        void SetupCameraAndLighting()
        {
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 9000f;
            raceCam = cam.GetComponent<RaceCamera>();
            if (raceCam == null) raceCam = cam.gameObject.AddComponent<RaceCamera>();
            raceCam.enabled = false;

            sun = FindAnyObjectByType<Light>();
            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.shadows = LightShadows.Soft;
            }
            ResetMenuLighting();
            var sky = Resources.Load<Material>(ApexMaterials.ResourceFolder + "/Sky");
            if (sky == null)
            {
                var shader = Shader.Find("Skybox/Procedural");
                if (shader != null) sky = new Material(shader);
            }
            if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.55f, 0.65f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.55f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.24f, 0.2f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
        }

        void ResetMenuLighting()
        {
            sun.intensity = 1.25f;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.shadowStrength = 0.9f;
            sun.transform.rotation = Quaternion.Euler(38f, 35f, 0f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fogColor = new Color(0.72f, 0.78f, 0.84f);
            RenderSettings.fogStartDistance = 300f;
            RenderSettings.fogEndDistance = 3500f;
            var sky = RenderSettings.skybox;
            if (sky != null && sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", 1.3f);
        }

        public static Color PaintColour(int index)
        {
            // Same order as the colour rewards in Progression.
            Color[] table =
            {
                new Color(0.047f, 0.12f, 0.28f), new Color(0.75f, 0.1f, 0.12f), new Color(0.93f, 0.93f, 0.95f), new Color(0.85f, 0.78f, 0.64f),
                new Color(0.12f, 0.38f, 0.22f), new Color(0.9f, 0.68f, 0.2f), new Color(0.16f, 0.17f, 0.19f), new Color(0.1f, 0.85f, 0.95f),
                new Color(0.95f, 0.5f, 0.1f), new Color(0.58f, 0.6f, 0.63f), new Color(0.85f, 0.15f, 0.3f), new Color(0.35f, 0.15f, 0.55f),
                new Color(0.75f, 0.88f, 0.95f), new Color(0.95f, 0.3f, 0.65f), new Color(0.82f, 0.7f, 0.48f), new Color(0.2f, 0.55f, 0.25f),
                new Color(0.15f, 0.35f, 0.6f), new Color(0.75f, 0.76f, 0.78f), new Color(0.75f, 0.55f, 0.2f), new Color(0.1f, 0.7f, 0.7f),
            };
            return table[((index % table.Length) + table.Length) % table.Length];
        }

        public static string PaintName(int index)
        {
            int k = 0;
            foreach (var r in Progression.Rewards)
            {
                if (r.kind != RewardKind.PaintColour) continue;
                if (k == index) return r.name;
                k++;
            }
            return "Lackierung " + (index + 1);
        }

        /// <summary>Paint colours unlocked so far (the first two are always available).</summary>
        public List<int> UnlockedPaints()
        {
            var list = new List<int>();
            int colourIndex = 0;
            foreach (var r in Progression.Rewards)
            {
                if (r.kind != RewardKind.PaintColour) continue;
                if (colourIndex < 2 || r.level <= Career.Level) list.Add(colourIndex);
                colourIndex++;
            }
            return list;
        }

        Color PlayerPaint() => PaintColour(Career.paintIndex);

        void BuildShowroom()
        {
            ResetMenuLighting();
            if (showroom != null) return;
            showroom = new GameObject("Showroom");
            var floor = new GameObject("Floor");
            floor.transform.SetParent(showroom.transform, false);
            floor.transform.localScale = new Vector3(14f, 0.05f, 14f);
            floor.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            floor.AddComponent<MeshFilter>().sharedMesh = CarBuilder.PrimitiveMesh(PrimitiveType.Cylinder);
            floor.AddComponent<MeshRenderer>().sharedMaterial = ApexMaterials.Get("Asphalt");
            RebuildShowroomCar();
        }

        void RebuildShowroomCar()
        {
            if (showroom == null) return;
            if (showroomCar != null) Destroy(showroomCar.gameObject);
            var team = Roster.Teams[Config.playerTeam];
            showroomCar = CarBuilder.Build("ShowroomCar", new VehicleSetup(), PlayerPaint(), new Color(team.ar, team.ag, team.ab));
            showroomCar.GetComponent<Rigidbody>().isKinematic = true;
            showroomCar.enabled = false;
            showroomCar.transform.SetParent(showroom.transform, false);
            showroomCar.transform.localPosition = new Vector3(0f, showroomCar.setup.cogHeight, 0f);
            for (int i = 0; i < 4; i++)
            {
                var m = showroomCar.setup.MountPosition(i, out float sc);
                showroomCar.WheelVisuals[i].localPosition = new Vector3(m.X, (i < 2 ? showroomCar.setup.wheelRadiusFront : showroomCar.setup.wheelRadiusRear) - showroomCar.setup.cogHeight, m.Z);
            }
        }

        void UpdateShowroomCamera()
        {
            float t = Time.unscaledTime * 0.12f;
            float dist = sm.Current == GameState.Garage ? 6f : 7.5f;
            var pos = new Vector3(Mathf.Sin(t) * dist, 2.0f, Mathf.Cos(t) * dist);
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(new Vector3(0f, 0.3f, 0f) - pos));
            cam.fieldOfView = 45f;
        }

        // --- Sessions -----------------------------------------------------------------------

        public void StartSession(SessionKind k, SessionType type, List<int> grid = null)
        {
            if (!sm.TryTransition(GameState.Loading)) return;
            kind = k;
            currentType = type;
            StartCoroutine(LoadSession(grid));
        }

        List<int> DefaultGrid(int aiCount, int seed)
        {
            var field = Roster.PickField(aiCount, Config.playerTeam, 0);
            var order = new List<int>();
            foreach (var d in field) order.Add(Array.IndexOf(Roster.Drivers, d));
            var rng = new System.Random(seed);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            // Without qualifying the player starts mid-pack.
            order.Insert(Mathf.Clamp(order.Count / 2, 0, order.Count), -1);
            return order;
        }

        SessionConfig ConfigForSession()
        {
            if (kind == SessionKind.Championship)
            {
                return new SessionConfig
                {
                    trackId = Career.calendar[Career.nextRound],
                    laps = Career.championshipLaps,
                    aiCount = Career.championshipAi,
                    difficulty = Career.championshipDifficulty,
                    format = WeekendFormat.RaceOnly,
                    weather = Config.weather,
                    dynamicWeather = Config.dynamicWeather,
                    timeOfDay = Config.timeOfDay,
                    tireWear = Config.tireWear,
                    falseStarts = Config.falseStarts,
                    trackLimitPenalties = Config.trackLimitPenalties,
                    playerTeam = Config.playerTeam,
                    seed = 100 + Career.nextRound,
                };
            }
            if (kind == SessionKind.Practice || kind == SessionKind.TimeTrial)
            {
                return new SessionConfig
                {
                    trackId = Config.trackId,
                    aiCount = 0,
                    weather = kind == SessionKind.TimeTrial ? Apex.Weather.WeatherState.Clear : Config.weather,
                    timeOfDay = Config.timeOfDay,
                    tireWear = kind == SessionKind.TimeTrial ? 0f : Config.tireWear,
                    startCompound = kind == SessionKind.TimeTrial ? TireCompound.Soft : Config.startCompound,
                    playerTeam = Config.playerTeam,
                    seed = Config.seed,
                };
            }
            return Config;
        }

        IEnumerator LoadSession(List<int> grid)
        {
            yield return null;
            var cfg = ConfigForSession();
            cfg.Validate();
            var def = Definition(cfg.trackId);
            if (def == null)
            {
                sm.TryTransition(GameState.MainMenu);
                yield break;
            }
            if (showroom != null) Destroy(showroom);
            showroom = null;
            showroomCar = null;
            world = new GameObject("World");
            EnvironmentBuilder.Quality = Settings.environmentDetail;
            var circuit = CircuitBuilder.Build(def, world.transform);
            yield return null;

            if (grid == null) grid = DefaultGrid(cfg.aiCount, cfg.seed);
            startPosition = grid.IndexOf(-1) + 1;
            var type = kind == SessionKind.Practice || kind == SessionKind.TimeTrial ? SessionType.Practice : currentType;
            var raceGo = new GameObject("Race");
            raceGo.transform.SetParent(world.transform, false);
            Race = raceGo.AddComponent<RaceController>();
            Race.Setup(circuit, cfg, type, grid, PlayerPaint(), Career.carNumber, "Du");
            if (kind == SessionKind.Practice || kind == SessionKind.TimeTrial) Race.Director.rules.sessionSeconds = 0f;
            Race.Input = input;
            Race.Message += (t, c, p) => ShowMessage(t, c, p >= 2 ? 3f : 2.2f, p >= 1);
            Race.SessionComplete += OnSessionComplete;
            Race.Player.entry.timer.LapCompleted += OnPlayerLap;
            Race.Player.entry.timer.SectorCompleted += OnPlayerSector;
            Race.Player.entry.timer.LapStarted += _ => ResetSectorColours();
            var playerCar = Race.Player.car;
            var audioSrc = playerCar.gameObject.AddComponent<AudioSource>();
            audioSrc.playOnAwake = false;
            var engineAudio = playerCar.gameObject.AddComponent<EngineAudio>();
            engineAudio.car = playerCar;
            engineAudio.volume = Settings.masterVolume * 0.75f;
            weatherVisuals = world.AddComponent<WeatherVisuals>();
            weatherVisuals.weather = Race.Weather;
            weatherVisuals.timeOfDay = cfg.timeOfDay;
            weatherVisuals.sun = sun;
            weatherVisuals.trueNorthBearing = def.trueNorthBearing;
            weatherVisuals.environment = circuit.Environment;
            if (circuit.Environment != null) circuit.Environment.ambienceVolume = Settings.masterVolume * 0.6f;
            weatherVisuals.cameraTransform = cam.transform;
            weatherVisuals.postProcessing = Settings.postProcessing;
            weatherVisuals.Bind(circuit.gameObject);

            trackId = def.id;
            sessionRecord = SaveService.BestLap(Settings, trackId);
            ResetSectorColours();
            raceCam.target = playerCar.transform;
            raceCam.SetMode(Settings.cameraMode);
            raceCam.enabled = true;
            input.ReleaseAll();
            ApplyAssists();
            calibrating = Settings.controlPreset == ControlPreset.Tilt;
            Race.PlayerInputEnabled = !calibrating;
            sm.TryTransition(GameState.Grid);
            if (!calibrating) BeginSession();
            string label = type == SessionType.Race ? "Rennen" : type == SessionType.Qualifying ? "Qualifying" : kind == SessionKind.TimeTrial ? "Zeitfahren" : "Freies Training";
            ShowMessage($"{def.name} – {label}", Color.white, 3f, true);
        }

        void BeginSession()
        {
            Race.PlayerInputEnabled = true;
            Race.Begin();
            if (sm.Current == GameState.Grid) sm.TryTransition(GameState.Driving);
        }

        void EndSession()
        {
            if (world != null) Destroy(world);
            world = null;
            Race = null;
            weatherVisuals = null;
            raceCam.enabled = false;
            raceCam.target = null;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            radioOpen = false;
            input.radioOpen = false;
        }

        void ApplyAssists()
        {
            if (Race == null) return;
            var sim = Race.Player.Sim;
            sim.tractionLevel = Settings.tractionLevel;
            sim.assistAbs = Settings.abs;
            sim.steeringAssist = Settings.SteeringAssistStrength;
            sim.speedSensitiveSteering = Settings.speedSensitiveSteering;
            sim.setup.brakeBiasFront = Settings.brakeBiasFront;
            sim.autoGearbox = Settings.autoGearbox;
            input.manualGears = !sim.autoGearbox;
            Race.PitAssist = Settings.pitAssist;
        }

        void OnStateChanged(GameState from, GameState to)
        {
            bool paused = to == GameState.Pause;
            Time.timeScale = paused ? 0f : 1f;
            AudioListener.pause = paused;
            if (from != GameState.Pause || to == GameState.MainMenu) focus = 0;
            settingsOpen = false;
            helpOpen = false;
            if (paused) input.ReleaseAll();
            input.drivingControls = to == GameState.Driving || to == GameState.Grid || to == GameState.RadioOverlay || to == GameState.PitApproach || to == GameState.PitStop;
            if (to == GameState.MainMenu || to == GameState.Garage || to == GameState.TrackSelect)
            {
                if (world != null) EndSession();
                BuildShowroom();
            }
            if (to == GameState.Garage) RebuildShowroomCar();
        }

        // --- Timing events ----------------------------------------------------------------

        readonly string[] sectorText = { "S1 --.---", "S2 --.---", "S3 --.---" };
        readonly Color[] sectorColor = new Color[3];

        void ResetSectorColours()
        {
            for (int i = 0; i < 3; i++)
            {
                sectorColor[i] = ApexStyle.TextDim;
                if (Race == null || Race.Player.entry.LapsCompleted == 0) sectorText[i] = $"S{i + 1} --.---";
            }
        }

        void OnPlayerSector(int index, double time, double delta)
        {
            if (index >= sectorText.Length) return;
            sectorText[index] = $"S{index + 1} {time:0.000}";
            var t = Race.Player.entry.timer;
            if (!t.LapValid) sectorColor[index] = ApexStyle.Urgent;
            else if (double.IsNaN(delta) || Math.Abs(t.bestSectorTimes[index] - time) < 1e-9) sectorColor[index] = ApexStyle.Best;
            else sectorColor[index] = delta <= 0 ? ApexStyle.Faster : ApexStyle.Caution;
        }

        void OnPlayerLap(int lap, double time, bool valid, bool personalBest)
        {
            if (valid && SaveService.SubmitLap(Settings, trackId, time))
            {
                sessionRecord = time;
                ShowMessage("Neuer Rekord  " + ApexStyle.FormatTime(time), ApexStyle.Best, 3f, true);
            }
            else if (personalBest)
            {
                ShowMessage("Persönliche Bestzeit  " + ApexStyle.FormatTime(time), ApexStyle.Faster, 2.5f, false);
            }
        }

        // --- Results ------------------------------------------------------------------------

        public sealed class ResultsData
        {
            public SessionType type;
            public SessionKind kind;
            public string trackName;
            public readonly List<string[]> rows = new List<string[]>();
            public int playerRow = -1;
            public int playerPosition;
            public int xpGained;
            public int levelBefore;
            public int levelAfter;
            public List<int> order;
            public string[] championshipLines;
            public bool championshipFinished;
        }

        void OnSessionComplete()
        {
            var d = Race.Director;
            var res = new ResultsData { type = Race.Type, kind = kind, trackName = Race.Circuit.Definition.name, order = Race.ResultOrder() };
            foreach (var e in d.Standings)
            {
                var rc = Race.Cars.Find(c => c.entry == e);
                string gap;
                if (Race.Type == SessionType.Race)
                {
                    int lapsDown = d.Leader.LapsCompleted - e.LapsCompleted;
                    gap = e.position == 1 ? ApexStyle.FormatTime(e.finishTime + e.penalties) : lapsDown > 0 ? $"+{lapsDown} Rd." : ApexStyle.FormatDelta(d.GapToLeader(e));
                }
                else
                {
                    gap = e.position == 1 ? ApexStyle.FormatTime(e.BestLap) : ApexStyle.FormatDelta(d.GapToLeader(e));
                }
                res.rows.Add(new[]
                {
                    e.position.ToString(), rc.code, e.name, Roster.Teams[rc.teamIndex].name, gap,
                    ApexStyle.FormatTime(e.BestLap), e.pitStops.ToString(), e.penalties > 0 ? $"+{e.penalties:0}s" : "",
                    Race.Type == SessionType.Race ? d.PointsFor(e).ToString() : "",
                });
                if (e.isPlayer)
                {
                    res.playerRow = res.rows.Count - 1;
                    res.playerPosition = e.position;
                }
            }
            res.levelBefore = Career.Level;
            var pe = Race.Player.entry;
            int clean = 0;
            foreach (var lap in pe.timer.completedLaps) if (lap.valid) clean++;
            if (Race.Type == SessionType.Race)
            {
                res.xpGained = Progression.RaceXp(pe.position, d.entries.Count, clean, d.FastestLapHolder == pe, pe.finished, startPosition - pe.position);
                Career.racesStarted++;
                if (pe.position == 1) Career.wins++;
                if (pe.position <= 3) Career.podiums++;
            }
            else
            {
                res.xpGained = 60 + clean * 25 + (pe.position == 1 && Race.Type == SessionType.Qualifying ? 150 : 0);
                if (Race.Type == SessionType.Qualifying && pe.position == 1) Career.poles++;
            }
            Career.xp += res.xpGained;
            res.levelAfter = Career.Level;
            if (kind == SessionKind.Championship && Race.Type == SessionType.Race)
            {
                foreach (var e in d.Standings)
                {
                    var rc = Race.Cars.Find(c => c.entry == e);
                    int idx = rc.isPlayer ? 0 : rc.driverIndex + 1;
                    if (idx >= 0 && idx < Career.points.Length) Career.points[idx] += d.PointsFor(e);
                }
                Career.nextRound++;
                res.championshipLines = ChampionshipTable(10);
                res.championshipFinished = !Career.ChampionshipActive;
            }
            SaveCareer();
            lastResults = res;
            pendingRaceGrid = Race.Type == SessionType.Qualifying ? res.order : null;
            sm.TryTransition(GameState.Results);
        }

        public string[] ChampionshipTable(int rows)
        {
            var idx = new List<int>();
            for (int i = 0; i < Career.points.Length; i++) idx.Add(i);
            idx.Sort((a, b) => Career.points[b].CompareTo(Career.points[a]));
            var lines = new List<string>();
            for (int k = 0; k < Mathf.Min(rows, idx.Count); k++)
            {
                int i = idx[k];
                string name = i == 0 ? "Du" : Roster.Drivers[i - 1].name;
                lines.Add($"{k + 1,2}.  {name}   –   {Career.points[i]} Pkt.");
            }
            return lines.ToArray();
        }

        // --- Update -------------------------------------------------------------------------

        void ShowMessage(string text, Color color, float duration, bool high)
        {
            var t = new Toast { text = text, color = color, time = duration };
            if (high) toastHigh = t;
            else toastLow = t;
        }

        void Update()
        {
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-4f), 0.05f);
            float udt = Time.unscaledDeltaTime;
            toastHigh.time -= udt;
            toastLow.time -= udt;
            restartArmed -= udt;
            ReadNavigation();
            if (input.ScreenshotPressed)
            {
                string path = System.IO.Path.Combine(Application.persistentDataPath, $"apex_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                ScreenCapture.CaptureScreenshot(path);
                ShowMessage("Screenshot gespeichert", Color.white, 1.5f, false);
            }
            if (input.HelpPressed) helpOpen = !helpOpen;

            switch (sm.Current)
            {
                case GameState.MainMenu:
                case GameState.TrackSelect:
                case GameState.Garage:
                    UpdateShowroomCamera();
                    if (input.PausePressed) MenuBack();
                    break;
                case GameState.Results:
                    if (input.PausePressed) sm.TryTransition(GameState.MainMenu);
                    break;
                case GameState.Grid:
                case GameState.Driving:
                case GameState.RadioOverlay:
                case GameState.PitApproach:
                case GameState.PitStop:
                    UpdateInRace();
                    break;
                case GameState.Pause:
                    if (input.PausePressed)
                    {
                        if (settingsOpen) settingsOpen = false;
                        else sm.Resume();
                    }
                    break;
            }
            if (Race != null) input.vehicleSpeed = Mathf.Abs(Race.Player.Sim.ForwardSpeed);
        }

        void MenuBack()
        {
            if (settingsOpen) { settingsOpen = false; focus = 0; return; }
            if (helpOpen) { helpOpen = false; return; }
            if (sm.Current != GameState.MainMenu) sm.TryTransition(GameState.MainMenu);
        }

        void UpdateInRace()
        {
            if (Race == null) return;
            if (input.PausePressed)
            {
                if (radioOpen) CloseRadio();
                else sm.TryTransition(GameState.Pause);
                return;
            }
            if (calibrating && (input.ConfirmPressed || navConfirm)) FinishCalibration();
            if (input.CameraPressed) ShowMessage(RaceCamera.ModeNames[Settings.cameraMode = raceCam.Cycle()] + "-Kamera", Color.white, 1f, false);
            if (input.ResetPressed) Race.ResetPlayerToTrack();
            if (input.TelemetryPressed) showTelemetry = !showTelemetry;
            if (input.TowerPressed) towerExpanded = !towerExpanded;
            if (input.MapPressed) showMap = !showMap;
            if (input.HudPresetPressed) hudPreset = (hudPreset + 1) % 3;
            if (input.LayoutDebugPressed) layoutDebug = !layoutDebug;
            if (input.ShiftUpPressed) Race.Player.Sim.RequestShiftUp();
            if (input.ShiftDownPressed) Race.Player.Sim.RequestShiftDown();
            if (input.PitPressed) Race.RequestPlayerPit(radioCompound);
            if (input.RadioPressed)
            {
                if (radioOpen) CloseRadio();
                else OpenRadio();
            }
            if (radioOpen) UpdateRadio();
            if (input.RestartPressed && Race.Type != SessionType.Race)
            {
                if (restartArmed > 0f)
                {
                    restartArmed = 0f;
                    var k = kind;
                    var t = currentType;
                    EndSession();
                    StartSessionDirect(k, t);
                    return;
                }
                restartArmed = 2f;
                ShowMessage("F5 erneut drücken: Session neu starten", ApexStyle.Caution, 2f, true);
            }
            SyncPitState();
        }

        void StartSessionDirect(SessionKind k, SessionType t)
        {
            // Restart from inside a session: Driving -> Loading is an allowed transition.
            if (!sm.TryTransition(GameState.Loading)) return;
            kind = k;
            currentType = t;
            StartCoroutine(LoadSession(null));
        }

        /// <summary>Keeps the state machine in step with the player's pit stage and radio.</summary>
        void SyncPitState()
        {
            if (sm.Current == GameState.Grid || sm.Current == GameState.Pause) return;
            var stage = Race.Player.pit.stage;
            GameState want = stage == PitState.Stage.Stopped ? GameState.PitStop
                : stage == PitState.Stage.InLane ? GameState.PitApproach
                : radioOpen ? GameState.RadioOverlay : GameState.Driving;
            if (sm.Current != want && sm.CanTransition(want)) sm.TryTransition(want);
        }

        void FinishCalibration()
        {
            input.CalibrateTilt();
            calibrating = false;
            BeginSession();
            ShowMessage("Lenkung zentriert", ApexStyle.Accent, 1.5f, false);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && Race != null && sm.Current != GameState.Pause && sm.Current != GameState.Results && sm.Current != GameState.Loading)
                sm.TryTransition(GameState.Pause);
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused && Race != null && sm.Current != GameState.Pause && sm.Current != GameState.Results && sm.Current != GameState.Loading)
                sm.TryTransition(GameState.Pause);
        }
    }
}
