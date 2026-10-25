using System;
using System.Collections.Generic;
using Apex.Core;

namespace Apex.Race
{
    public enum SessionType
    {
        Practice,
        Qualifying,
        Race,
        TimeTrial,
    }

    /// <summary>Rules chosen for a session (only what is selected is enforced).</summary>
    [Serializable]
    public sealed class RaceRules
    {
        public SessionType type = SessionType.Race;
        public int laps = 5;
        /// <summary>Qualifying/practice duration in seconds (0 = unlimited).</summary>
        public float sessionSeconds = 600f;
        public bool falseStarts = true;
        public float falseStartPenalty = 5f;
        public bool trackLimitPenalties = true;
        /// <summary>Warnings before a track-limits time penalty.</summary>
        public int trackLimitWarnings = 3;
        public float trackLimitPenalty = 5f;
        public bool fastestLapPoint = true;
        public bool mandatoryPitStop;
        public static readonly int[] Points = { 25, 18, 15, 12, 10, 8, 6, 4, 2, 1 };
    }

    /// <summary>
    /// Engine-independent race control: start lights, lap/sector timing per car,
    /// live order and gaps, finish and classification, false starts, track-limit
    /// warnings/penalties, fastest lap and championship points. Fed every fixed
    /// step with each car's lap distance (from the start line).
    /// </summary>
    public sealed class RaceDirector
    {
        public enum Phase { Waiting, Lights, Racing, Finished }

        public sealed class Entry
        {
            public int id;
            public string name;
            public string team;
            public string code;
            public bool isPlayer;
            public int gridSlot;
            public LapTimer timer;
            public float lapDistance;
            public bool finished;
            public double finishTime;
            public double penalties;
            public int trackLimitWarnings;
            public bool falseStart;
            public bool retired;
            public int pitStops;
            public int position;
            public readonly List<double> checkpointTimes = new List<double>();
            public double startTime;
            public float startDistance = -1f;

            public int LapsCompleted => timer.completedLaps.Count;
            public double BestLap => timer.BestLapTime;

            /// <summary>Distance covered since the start line (grid cars start slightly negative).</summary>
            public double Progress(float trackLength)
            {
                if (timer.LapNumber == 0) return lapDistance > trackLength * 0.5f ? lapDistance - trackLength : lapDistance;
                return (timer.LapNumber - 1) * (double)trackLength + lapDistance;
            }
        }

        public const float CheckpointSpacing = 25f;

        public readonly RaceRules rules;
        public readonly float trackLength;
        public readonly float[] sectorStarts;
        public readonly List<Entry> entries = new List<Entry>();

        public Phase CurrentPhase { get; private set; } = Phase.Waiting;
        public int LightsOn { get; private set; }
        public double RaceStartTime { get; private set; } = -1;
        public double Now { get; private set; }
        public Entry Leader { get; private set; }
        public double FastestLap { get; private set; }
        public Entry FastestLapHolder { get; private set; }
        public bool Checkered { get; private set; }

        public event Action<int> LightsChanged;
        public event Action LightsOut;
        public event Action<Entry> EntryFinished;
        public event Action<Entry, double> FastestLapSet;
        public event Action<Entry, double, string> PenaltyIssued;
        public event Action<Entry, string> Warning;
        public event Action SessionEnded;

        double lightsStart;
        double holdTime;
        readonly Random rng;
        readonly List<Entry> order = new List<Entry>();

        public RaceDirector(RaceRules rules, float trackLength, float[] sectorStarts, int seed = 1)
        {
            this.rules = rules;
            this.trackLength = trackLength;
            this.sectorStarts = sectorStarts;
            rng = new Random(seed);
        }

        public Entry AddEntry(int id, string name, string code, string team, bool isPlayer, int gridSlot)
        {
            var e = new Entry { id = id, name = name, code = code, team = team, isPlayer = isPlayer, gridSlot = gridSlot, timer = new LapTimer(trackLength, sectorStarts) };
            e.timer.LapCompleted += (lap, time, valid, pb) => OnLap(e, time, valid);
            entries.Add(e);
            order.Add(e);
            return e;
        }

        public bool IsRace => rules.type == SessionType.Race;
        public bool CarsMayMove => CurrentPhase == Phase.Racing || CurrentPhase == Phase.Finished || !IsRace;

        /// <summary>Starts the lights sequence (race) or the session clock (others).</summary>
        public void Start(double now)
        {
            Now = now;
            if (IsRace)
            {
                CurrentPhase = Phase.Lights;
                lightsStart = now + 1.0;
                holdTime = 0.2 + rng.NextDouble() * 2.8;
                LightsOn = 0;
            }
            else
            {
                CurrentPhase = Phase.Racing;
                RaceStartTime = now;
            }
        }

        public double SessionTime => RaceStartTime < 0 ? 0 : Now - RaceStartTime;
        public double SessionRemaining => rules.sessionSeconds <= 0 ? double.PositiveInfinity : Math.Max(0, rules.sessionSeconds - SessionTime);

        /// <summary>Feed one car sample. lapDistance is measured from the start line.</summary>
        public void UpdateEntry(Entry e, float lapDistance, double time)
        {
            if (e.retired) return;
            if (CurrentPhase == Phase.Lights && rules.falseStarts)
            {
                if (e.startDistance < 0f) e.startDistance = lapDistance;
                float moved = ApexMath.PosMod(lapDistance - e.startDistance + trackLength * 0.5f, trackLength) - trackLength * 0.5f;
                if (!e.falseStart && moved > 1.0f)
                {
                    e.falseStart = true;
                    AddPenalty(e, rules.falseStartPenalty, "Frühstart");
                }
            }
            if (e.finished)
            {
                e.lapDistance = lapDistance;
                return;
            }
            e.timer.Update(lapDistance, time);
            e.lapDistance = lapDistance;
            // Checkpoint times for gaps.
            double progress = e.Progress(trackLength);
            if (progress >= 0)
            {
                int k = (int)(progress / CheckpointSpacing);
                while (e.checkpointTimes.Count <= k) e.checkpointTimes.Add(time);
            }
        }

        public void TrackLimitViolation(Entry e)
        {
            if (!rules.trackLimitPenalties || CurrentPhase != Phase.Racing || !IsRace) return;
            e.trackLimitWarnings++;
            if (e.trackLimitWarnings > rules.trackLimitWarnings)
            {
                e.trackLimitWarnings = 0;
                AddPenalty(e, rules.trackLimitPenalty, "Tracklimits");
            }
            else
            {
                Warning?.Invoke(e, $"Tracklimits – Verwarnung {e.trackLimitWarnings}/{rules.trackLimitWarnings}");
            }
        }

        public void AddPenalty(Entry e, double seconds, string reason)
        {
            e.penalties += seconds;
            PenaltyIssued?.Invoke(e, seconds, reason);
        }

        public void Retire(Entry e) => e.retired = true;

        /// <summary>Advance session state; call once per step after all UpdateEntry calls.</summary>
        public void Tick(double now)
        {
            Now = now;
            if (CurrentPhase == Phase.Lights)
            {
                int lights = (int)Math.Floor(now - lightsStart) + 1;
                lights = Math.Max(0, Math.Min(5, lights));
                if (lights != LightsOn)
                {
                    LightsOn = lights;
                    LightsChanged?.Invoke(lights);
                }
                if (now >= lightsStart + 4.0 + holdTime)
                {
                    LightsOn = 0;
                    LightsChanged?.Invoke(0);
                    CurrentPhase = Phase.Racing;
                    RaceStartTime = now;
                    LightsOut?.Invoke();
                }
            }
            else if (CurrentPhase == Phase.Racing && !IsRace && SessionRemaining <= 0 && !Checkered)
            {
                Checkered = true;
            }
            SortOrder();
            if (CurrentPhase == Phase.Racing && Checkered && AllDone())
            {
                CurrentPhase = Phase.Finished;
                SessionEnded?.Invoke();
            }
        }

        bool AllDone()
        {
            foreach (var e in entries)
                if (!e.finished && !e.retired) return false;
            return true;
        }

        void OnLap(Entry e, double lapTime, bool valid)
        {
            if (valid && (FastestLap <= 0 || lapTime < FastestLap))
            {
                FastestLap = lapTime;
                FastestLapHolder = e;
                FastestLapSet?.Invoke(e, lapTime);
            }
            if (IsRace)
            {
                if (!Checkered && e.LapsCompleted >= rules.laps) Checkered = true;
                if (Checkered && !e.finished)
                {
                    e.finished = true;
                    e.finishTime = Now - RaceStartTime;
                    EntryFinished?.Invoke(e);
                }
            }
            else if (Checkered && !e.finished)
            {
                // Practice/qualifying: the lap in progress when time ran out still counts.
                e.finished = true;
                EntryFinished?.Invoke(e);
            }
        }

        /// <summary>Called by the session for cars that cannot complete a lap after the flag (e.g. AI in pits).</summary>
        public void ForceFinish(Entry e)
        {
            if (e.finished) return;
            e.finished = true;
            e.finishTime = Now - RaceStartTime;
            EntryFinished?.Invoke(e);
        }

        void SortOrder()
        {
            if (IsRace)
            {
                order.Sort((a, b) =>
                {
                    if (a.retired != b.retired) return a.retired ? 1 : -1;
                    if (CurrentPhase == Phase.Lights || RaceStartTime < 0) return a.gridSlot.CompareTo(b.gridSlot);
                    // Cars that took the flag are classified ahead of cars still running.
                    if (a.finished != b.finished) return a.finished ? -1 : 1;
                    if (a.finished)
                    {
                        int laps = b.LapsCompleted.CompareTo(a.LapsCompleted);
                        return laps != 0 ? laps : (a.finishTime + a.penalties).CompareTo(b.finishTime + b.penalties);
                    }
                    return b.Progress(trackLength).CompareTo(a.Progress(trackLength));
                });
            }
            else
            {
                order.Sort((a, b) =>
                {
                    double la = a.BestLap > 0 ? a.BestLap : double.MaxValue;
                    double lb = b.BestLap > 0 ? b.BestLap : double.MaxValue;
                    int c = la.CompareTo(lb);
                    return c != 0 ? c : a.gridSlot.CompareTo(b.gridSlot);
                });
            }
            for (int i = 0; i < order.Count; i++) order[i].position = i + 1;
            Leader = order.Count > 0 ? order[0] : null;
        }

        public IReadOnlyList<Entry> Standings => order;

        /// <summary>Time gap (s) of e behind the leader; NaN if unknown. For laps down use LapsBehind.</summary>
        public double GapToLeader(Entry e) => Gap(e, Leader);

        public double GapToAhead(Entry e)
        {
            int i = order.IndexOf(e);
            return i <= 0 ? 0 : Gap(e, order[i - 1]);
        }

        public double Gap(Entry e, Entry reference)
        {
            if (reference == null || e == reference) return 0;
            if (!IsRace)
            {
                if (e.BestLap <= 0 || reference.BestLap <= 0) return double.NaN;
                return e.BestLap - reference.BestLap;
            }
            if (e.finished && reference.finished) return (e.finishTime + e.penalties) - (reference.finishTime + reference.penalties);
            int k = e.checkpointTimes.Count - 1;
            if (k < 0 || reference.checkpointTimes.Count <= k) return double.NaN;
            return e.checkpointTimes[k] - reference.checkpointTimes[k];
        }

        public int LapsBehind(Entry e)
        {
            if (Leader == null || e == Leader) return 0;
            double diff = Leader.Progress(trackLength) - e.Progress(trackLength);
            return (int)(diff / trackLength);
        }

        /// <summary>Championship points for a classified position (1-based), incl. fastest-lap bonus.</summary>
        public int PointsFor(Entry e)
        {
            if (!IsRace || e.retired) return 0;
            int pos = e.position;
            int pts = pos >= 1 && pos <= RaceRules.Points.Length ? RaceRules.Points[pos - 1] : 0;
            if (rules.fastestLapPoint && FastestLapHolder == e && pos <= 10) pts += 1;
            return pts;
        }
    }
}
