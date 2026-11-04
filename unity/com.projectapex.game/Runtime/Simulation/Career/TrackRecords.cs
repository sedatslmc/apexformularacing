using System;
using System.Collections.Generic;

namespace Apex.Career
{
    /// <summary>One completed lap as stored in the save game.</summary>
    [Serializable]
    public sealed class StoredLap
    {
        public double time;
        public bool valid;
        public double[] sectors = new double[0];
        public string session = "";
    }

    /// <summary>Per-circuit records: best lap with its sectors, the best single sectors and the most recent laps.</summary>
    [Serializable]
    public sealed class TrackRecord
    {
        public string track = "";
        public double bestLap;
        public double[] bestLapSectors = new double[0];
        public double[] bestSectors = new double[0];
        public List<StoredLap> recent = new List<StoredLap>();

        /// <summary>Sum of the best single sectors (0 while a sector has no valid time yet).</summary>
        public double TheoreticalBest
        {
            get
            {
                if (bestSectors.Length == 0) return 0.0;
                double sum = 0.0;
                foreach (var s in bestSectors) { if (s <= 0.0) return 0.0; sum += s; }
                return sum;
            }
        }
    }

    /// <summary>
    /// Lap records of all circuits (time trial menu, docs/UI_UX_OVERHAUL.md 13.4): every completed lap of the player goes
    /// in; valid laps update the best lap and the best sectors, the last <see cref="RecentLaps"/> laps are kept
    /// (newest first, invalid ones marked). Serialised with the save game (JsonUtility-compatible fields).
    /// </summary>
    [Serializable]
    public sealed class TrackRecordBook
    {
        public const int RecentLaps = 10;
        public List<TrackRecord> tracks = new List<TrackRecord>();

        public TrackRecord Find(string trackId)
        {
            foreach (var t in tracks) if (t.track == trackId) return t;
            return null;
        }

        TrackRecord Get(string trackId)
        {
            var t = Find(trackId);
            if (t != null) return t;
            t = new TrackRecord { track = trackId };
            tracks.Add(t);
            return t;
        }

        /// <summary>Adds a completed lap; returns true when it is a new best lap for the circuit.</summary>
        public bool Submit(string trackId, double time, bool valid, double[] sectors, string session)
        {
            if (string.IsNullOrEmpty(trackId) || !(time > 0.0) || double.IsInfinity(time)) return false;
            var t = Get(trackId);
            var sec = sectors != null ? (double[])sectors.Clone() : new double[0];
            t.recent.Insert(0, new StoredLap { time = time, valid = valid, sectors = sec, session = session ?? "" });
            if (t.recent.Count > RecentLaps) t.recent.RemoveRange(RecentLaps, t.recent.Count - RecentLaps);
            if (!valid) return false;
            if (t.bestSectors.Length < sec.Length) Array.Resize(ref t.bestSectors, sec.Length);
            for (int i = 0; i < sec.Length; i++)
                if (sec[i] > 0.0 && (t.bestSectors[i] <= 0.0 || sec[i] < t.bestSectors[i])) t.bestSectors[i] = sec[i];
            if (t.bestLap > 0.0 && time >= t.bestLap) return false;
            t.bestLap = time;
            t.bestLapSectors = sec;
            return true;
        }
    }
}
