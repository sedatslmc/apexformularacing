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

        // Championship in progress (empty calendar = none).
        public string[] calendar = new string[0];
        public int nextRound;
        /// <summary>Points per entrant: player is index 0, then Roster.Drivers order.</summary>
        public int[] points = new int[0];
        public int championshipLaps = 5;
        public int championshipAi = 11;
        public float championshipDifficulty = 0.5f;

        public int Level => Progression.LevelForXp(xp);
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
