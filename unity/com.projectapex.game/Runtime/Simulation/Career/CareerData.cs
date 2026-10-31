using System;
using System.Collections.Generic;
using Apex.Core;

namespace Apex.Career
{
    /// <summary>Persistent player progress and championship state (versioned).</summary>
    [Serializable]
    public sealed class CareerData
    {
        public const int CurrentVersion = 1;
        public int version = CurrentVersion;
        public long xp;
        public int paintIndex;
        public int carNumber = 27;
        public int playerTeam;
        public int racesStarted;
        public int wins;
        public int podiums;
        public int poles;
        /// <summary>Races finished (for the average finishing position) and the sum of those positions.</summary>
        public int racesFinished, finishPositionSum;
        /// <summary>Distance driven in completed laps of all sessions (km).</summary>
        public double distanceKm;
        /// <summary>Last session for the main menu card ("P4 · Grand Prix"); empty before the first one.</summary>
        public string lastTrack = "", lastSession = "";
        public int lastPosition, lastFieldSize, lastPositionsGained;
        public double lastBestLap;

        // Championship in progress (empty calendar = none).
        public string[] calendar = new string[0];
        public int nextRound;
        /// <summary>Points per entrant: player is index 0, then Roster.Drivers order.</summary>
        public int[] points = new int[0];
        public int championshipLaps = 5;
        public int championshipAi = 11;
        public float championshipDifficulty = 0.5f;

        public int Level => Progression.LevelForXp(xp);
        /// <summary>Average finishing position of finished races; 0 before the first.</summary>
        public float AverageFinish => racesFinished > 0 ? finishPositionSum / (float)racesFinished : 0f;

        /// <summary>Records a completed session for the statistics and the "last result" card.</summary>
        public void RecordSession(string trackName, string session, int position, int fieldSize, int positionsGained, double bestLap, double lapsKm, bool raceFinished)
        {
            lastTrack = trackName ?? "";
            lastSession = session ?? "";
            lastPosition = position;
            lastFieldSize = fieldSize;
            lastPositionsGained = positionsGained;
            lastBestLap = bestLap;
            distanceKm += Math.Max(0.0, lapsKm);
            if (raceFinished && position > 0)
            {
                racesFinished++;
                finishPositionSum += position;
            }
        }
        public bool ChampionshipActive => calendar != null && calendar.Length > 0 && nextRound < calendar.Length;

        public void StartChampionship(string[] tracks, int laps, int ai, float difficulty)
        {
            calendar = tracks;
            nextRound = 0;
            points = new int[Roster.Drivers.Length + 1];
            championshipLaps = laps;
            championshipAi = ai;
            championshipDifficulty = difficulty;
        }

        /// <summary>Unlocked rewards up to the current level.</summary>
        public List<LevelReward> Unlocked()
        {
            var list = new List<LevelReward>();
            int lvl = Level;
            foreach (var r in Progression.Rewards)
                if (r.level <= lvl) list.Add(r);
            return list;
        }

        public bool MigrateAndValidate()
        {
            bool changed = false;
            if (version < CurrentVersion) { version = CurrentVersion; changed = true; }
            if (calendar == null) { calendar = new string[0]; changed = true; }
            if (lastTrack == null) { lastTrack = ""; changed = true; }
            if (lastSession == null) { lastSession = ""; changed = true; }
            // The Bosphorus GP moved to the geographic layout (Variant A, id "bosphorus_a").
            for (int i = 0; i < calendar.Length; i++)
                if (calendar[i] == "bosphorus_gp") { calendar[i] = "bosphorus_a"; changed = true; }
            if (points == null || (calendar.Length > 0 && points.Length != Roster.Drivers.Length + 1))
            {
                points = new int[Roster.Drivers.Length + 1];
                changed = true;
            }
            if (xp < 0) { xp = 0; changed = true; }
            return changed;
        }
    }
}
