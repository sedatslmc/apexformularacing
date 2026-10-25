using System;
using System.Collections.Generic;

namespace Apex.Career
{
    public enum RewardKind
    {
        PaintColour,
        HelmetDesign,
        CarNumberStyle,
        RaceSuit,
        GarageTheme,
        CameraFilter,
        Decal,
        WheelRimColour,
        ChallengeSeries,
        Title,
    }

    public readonly struct LevelReward
    {
        public readonly int level;
        public readonly RewardKind kind;
        public readonly string name;
        public readonly bool cosmetic;

        public LevelReward(int level, RewardKind kind, string name)
        {
            this.level = level;
            this.kind = kind;
            this.name = name;
            cosmetic = true;
        }
    }

    /// <summary>
    /// Player levels 1–100 with a data-driven reward on every level. All rewards are
    /// cosmetic or content unlocks; nothing affects performance in normalised modes.
    /// XP requirements grow smoothly so early levels come quickly.
    /// </summary>
    public static class Progression
    {
        public const int MaxLevel = 100;

        static readonly string[] Colours =
        {
            "Bosporus-Blau", "Galata-Rot", "Ortaköy-Weiß", "Kalkstein-Beige", "Zypressen-Grün", "Abendgold",
            "Graphit", "Elektrisch Cyan", "Fährorange", "Möwengrau", "Tulpenrot", "Nachtviolett",
            "Alpen-Eis", "Neon-Tokio-Pink", "Wüstensand", "Dschungelgrün", "Fjordblau", "Stadion-Silber",
            "Toskana-Ocker", "Marina-Türkis",
        };
        static readonly string[] Helmets = { "Streifen", "Chevron", "Halbmond-Schwung", "Karbon-Raster", "Brückenseile", "Wellen", "Sternbild", "Splitter", "Retro-Ring", "Zielflagge" };
        static readonly string[] Suits = { "Klassisch", "Leichtbau", "Nacht", "Regen", "Hitzeschild", "Gold-Edition" };
        static readonly string[] Themes = { "Werkstatt hell", "Mitternacht", "Bosporus-Abend", "Industrial", "Showroom", "Tribüne" };
        static readonly string[] Filters = { "Broadcast neutral", "Filmisch warm", "Kühl", "Schwarzweiß", "Retro-TV", "Goldene Stunde" };
        static readonly string[] Decals = { "Startnummer-Schatten", "Seitenpfeil", "Kalligrafie-Linie", "Geometrie", "Speedlines", "Kachelmuster", "Sternspur", "Wellenkamm" };
        static readonly string[] Rims = { "Schwarz matt", "Bronze", "Weiß", "Titan", "Cyan", "Gold" };
        static readonly string[] Challenges = { "Bosporus-Sprint", "Regenmeister", "Reifenflüsterer", "Startrakete", "Überholkünstler", "Perfekte Runde", "Zeitfahr-Elite", "Ausdauer-Serie" };
        static readonly string[] Titles = { "Rookie", "Talent", "Herausforderer", "Pole-Jäger", "Sieger", "Champion", "Legende", "Apex-Meister" };

        /// <summary>Total XP needed to reach a level (level 1 = 0 XP).</summary>
        public static int XpForLevel(int level)
        {
            if (level <= 1) return 0;
            int n = Math.Min(level, MaxLevel) - 1;
            return (int)Math.Round(500.0 * n + 40.0 * n * n);
        }

        public static int LevelForXp(long xp)
        {
            int level = 1;
            while (level < MaxLevel && xp >= XpForLevel(level + 1)) level++;
            return level;
        }

        public static IReadOnlyList<LevelReward> Rewards => rewards ?? (rewards = BuildRewards());
        static List<LevelReward> rewards;

        static List<LevelReward> BuildRewards()
        {
            var list = new List<LevelReward>(MaxLevel);
            int[] idx = new int[10];
            for (int level = 1; level <= MaxLevel; level++)
            {
                RewardKind kind;
                if (level % 25 == 0) kind = RewardKind.Title;
                else if (level % 10 == 0) kind = RewardKind.ChallengeSeries;
                else if (level % 9 == 0) kind = RewardKind.GarageTheme;
                else if (level % 8 == 0) kind = RewardKind.RaceSuit;
                else if (level % 7 == 0) kind = RewardKind.CameraFilter;
                else if (level % 6 == 0) kind = RewardKind.WheelRimColour;
                else if (level % 5 == 0) kind = RewardKind.HelmetDesign;
                else if (level % 4 == 0) kind = RewardKind.Decal;
                else if (level % 3 == 0) kind = RewardKind.CarNumberStyle;
                else kind = RewardKind.PaintColour;
                string[] pool = PoolFor(kind);
                int k = idx[(int)kind]++;
                string baseName = pool[k % pool.Length];
                int edition = k / pool.Length;
                string name = edition == 0 ? baseName : $"{baseName} {(edition == 1 ? "Metallic" : edition == 2 ? "Perlmutt" : "Edition " + (edition + 1))}";
                if (kind == RewardKind.CarNumberStyle) name = $"Startnummern-Stil {k + 1}";
                if (kind == RewardKind.Title) name = $"Titel „{Titles[Math.Min(Titles.Length - 1, level / 25 + 3)]}“";
                list.Add(new LevelReward(level, kind, name));
            }
            return list;
        }

        static string[] PoolFor(RewardKind kind)
        {
            switch (kind)
            {
                case RewardKind.HelmetDesign: return Helmets;
                case RewardKind.RaceSuit: return Suits;
                case RewardKind.GarageTheme: return Themes;
                case RewardKind.CameraFilter: return Filters;
                case RewardKind.Decal: return Decals;
                case RewardKind.WheelRimColour: return Rims;
                case RewardKind.ChallengeSeries: return Challenges;
                case RewardKind.Title: return Titles;
                default: return Colours;
            }
        }

        /// <summary>XP for a race result: finishing, position, clean laps, fastest lap, progress.</summary>
        public static int RaceXp(int position, int fieldSize, int cleanLaps, bool fastestLap, bool finished, int positionsGained)
        {
            if (!finished) return 50 + cleanLaps * 20;
            int xp = 250;
            xp += Math.Max(0, (fieldSize - position + 1)) * 40;
            if (position == 1) xp += 300;
            else if (position <= 3) xp += 150;
            xp += cleanLaps * 30;
            if (fastestLap) xp += 100;
            xp += Math.Max(0, positionsGained) * 25;
            return xp;
        }
    }
}
