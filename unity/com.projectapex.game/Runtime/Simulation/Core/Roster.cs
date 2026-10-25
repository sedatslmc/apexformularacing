using System;
using System.Collections.Generic;
using Apex.AI;

namespace Apex.Core
{
    /// <summary>Fictional team (no real-world teams, sponsors or liveries).</summary>
    public readonly struct TeamInfo
    {
        public readonly string name;
        public readonly float r, g, b;      // primary colour
        public readonly float ar, ag, ab;   // accent colour

        public TeamInfo(string name, float r, float g, float b, float ar, float ag, float ab)
        {
            this.name = name;
            this.r = r; this.g = g; this.b = b;
            this.ar = ar; this.ag = ag; this.ab = ab;
        }
    }

    /// <summary>Fictional driver with an AI personality.</summary>
    public readonly struct DriverInfo
    {
        public readonly string name;
        public readonly string code;
        public readonly int team;
        public readonly int number;
        public readonly AIPersonality personality;

        public DriverInfo(string name, string code, int team, int number, AIPersonality personality)
        {
            this.name = name;
            this.code = code;
            this.team = team;
            this.number = number;
            this.personality = personality;
        }
    }

    /// <summary>
    /// The fictional Apex Series field: 10 teams × 2 drivers. Names are invented and
    /// deliberately unlike real drivers or teams. Personalities differ in pace,
    /// aggression, consistency, wet skill and tire saving.
    /// </summary>
    public static class Roster
    {
        public static readonly TeamInfo[] Teams =
        {
            new TeamInfo("Meridian Racing", 0.05f, 0.33f, 0.62f, 0.10f, 0.85f, 0.95f),
            new TeamInfo("Kestrel Motorsport", 0.80f, 0.12f, 0.10f, 0.95f, 0.95f, 0.95f),
            new TeamInfo("Nova Velocity", 0.10f, 0.10f, 0.12f, 0.95f, 0.75f, 0.10f),
            new TeamInfo("Bosporus Racing Team", 0.70f, 0.05f, 0.15f, 0.95f, 0.85f, 0.55f),
            new TeamInfo("Orion Engineering", 0.95f, 0.50f, 0.05f, 0.10f, 0.10f, 0.15f),
            new TeamInfo("Atlas Corse", 0.05f, 0.45f, 0.25f, 0.95f, 0.95f, 0.85f),
            new TeamInfo("Helix Dynamics", 0.55f, 0.20f, 0.70f, 0.20f, 0.95f, 0.70f),
            new TeamInfo("Vanta GP", 0.92f, 0.92f, 0.94f, 0.10f, 0.35f, 0.85f),
            new TeamInfo("Solaris Works", 0.95f, 0.80f, 0.15f, 0.20f, 0.20f, 0.25f),
            new TeamInfo("Nordlys Racing", 0.15f, 0.55f, 0.80f, 0.95f, 0.30f, 0.30f),
        };

        public static readonly DriverInfo[] Drivers =
        {
            new DriverInfo("Teodor Valkenburg", "VAL", 0, 4, AIPersonality.Make(0.955f, 0.55f, 0.90f, 0.60f, 0.50f)),
            new DriverInfo("Ines Marquardt", "MAQ", 0, 17, AIPersonality.Make(0.945f, 0.45f, 0.85f, 0.75f, 0.65f)),
            new DriverInfo("Rafe Okonkwo-Lind", "OKL", 1, 8, AIPersonality.Make(0.950f, 0.80f, 0.70f, 0.50f, 0.30f)),
            new DriverInfo("Sanna Ilvesmaa", "ILV", 1, 23, AIPersonality.Make(0.940f, 0.40f, 0.92f, 0.85f, 0.70f)),
            new DriverInfo("Dorian Castellaro", "CAS", 2, 11, AIPersonality.Make(0.945f, 0.70f, 0.75f, 0.40f, 0.40f)),
            new DriverInfo("Mika Thorvaldsen", "THO", 2, 31, AIPersonality.Make(0.935f, 0.50f, 0.85f, 0.65f, 0.55f)),
            new DriverInfo("Deniz Karaduman", "KAR", 3, 34, AIPersonality.Make(0.950f, 0.65f, 0.80f, 0.70f, 0.50f)),
            new DriverInfo("Elif Sarıyer", "SAR", 3, 61, AIPersonality.Make(0.935f, 0.45f, 0.90f, 0.80f, 0.75f)),
            new DriverInfo("Lucien Abernethy", "ABE", 4, 7, AIPersonality.Make(0.930f, 0.85f, 0.60f, 0.45f, 0.30f)),
            new DriverInfo("Priya Ramaswamy-Holt", "RAH", 4, 19, AIPersonality.Make(0.930f, 0.40f, 0.88f, 0.60f, 0.80f)),
            new DriverInfo("Joaquín Ferreño", "FER", 5, 27, AIPersonality.Make(0.925f, 0.60f, 0.78f, 0.55f, 0.50f)),
            new DriverInfo("Halvard Brekke", "BRE", 5, 44, AIPersonality.Make(0.920f, 0.35f, 0.90f, 0.90f, 0.60f)),
            new DriverInfo("Noor Al-Saadi", "NAS", 6, 9, AIPersonality.Make(0.925f, 0.70f, 0.72f, 0.50f, 0.45f)),
            new DriverInfo("Kasimir Wendt", "WEN", 6, 52, AIPersonality.Make(0.915f, 0.50f, 0.82f, 0.60f, 0.55f)),
            new DriverInfo("Aurelio Benedetti-Ruiz", "BER", 7, 14, AIPersonality.Make(0.915f, 0.75f, 0.65f, 0.45f, 0.35f)),
            new DriverInfo("Tove Lindqvist", "LIN", 7, 28, AIPersonality.Make(0.910f, 0.40f, 0.88f, 0.70f, 0.70f)),
            new DriverInfo("Oyelaran Mensah", "MEN", 8, 3, AIPersonality.Make(0.910f, 0.60f, 0.75f, 0.55f, 0.50f)),
            new DriverInfo("Clémence Darvault", "DAR", 8, 66, AIPersonality.Make(0.905f, 0.45f, 0.85f, 0.65f, 0.60f)),
            new DriverInfo("Yusuf Altıntepe", "ALT", 9, 12, AIPersonality.Make(0.905f, 0.65f, 0.70f, 0.75f, 0.45f)),
            new DriverInfo("Freja Høstmark", "HOS", 9, 77, AIPersonality.Make(0.900f, 0.40f, 0.85f, 0.80f, 0.65f)),
        };

        /// <summary>AI pace multiplier for a difficulty in [0,1] (0 = gentle, 1 = expert).</summary>
        public static float DifficultyScale(float difficulty) => 0.88f + 0.14f * Math.Max(0f, Math.Min(1f, difficulty));

        /// <summary>Picks n AI drivers, skipping the seat the player takes (team/slot).</summary>
        public static List<DriverInfo> PickField(int n, int playerTeam, int playerSlot)
        {
            var list = new List<DriverInfo>();
            for (int i = 0; i < Drivers.Length && list.Count < n; i++)
            {
                if (Drivers[i].team == playerTeam && (i % 2) == playerSlot) continue;
                list.Add(Drivers[i]);
            }
            return list;
        }
    }
}
