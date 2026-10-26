using System;
using System.Collections.Generic;

namespace Apex.Race
{
    /// <summary>
    /// Original Bosphorus Grand Prix identity (no real series, teams or sponsors): event name,
    /// slogan, Turkish trackside messages, and the logic behind the start lights, LED screens and
    /// the timing tower. Pure data, used by the runtime builders and tested in EditMode.
    /// </summary>
    public static class EventIdentity
    {
        public const string Bosphorus = "bosphorus";
        public const string EventName = "BOSPHORUS GRAND PRIX";
        public const string EventCity = "ISTANBUL";
        public const string Slogan = "WHERE CONTINENTS MEET SPEED";

        /// <summary>Turkish banner messages along the start straight and in the city.</summary>
        public static readonly string[] Banners =
        {
            "İSTANBUL'A HOŞ GELDİNİZ", "BOĞAZ GRAND PRIX", "HIZIN BULUŞMA NOKTASI", "YARIŞ BAŞLIYOR",
        };

        /// <summary>Messages the LED screens rotate through while nothing race-relevant is shown.</summary>
        public static readonly string[] IdleMessages = { "BOĞAZ GRAND PRIX", "İSTANBUL'A HOŞ GELDİNİZ", "HIZIN BULUŞMA NOKTASI" };

        /// <summary>Seconds each rotating message stays on the screens.</summary>
        public const double MessageSeconds = 6.0;

        /// <summary>Number of lit red lamp pairs on the start gantry (0 outside the start sequence).</summary>
        public static int LitStartLamps(RaceDirector.Phase phase, int lightsOn) =>
            phase == RaceDirector.Phase.Lights ? Math.Max(0, Math.Min(5, lightsOn)) : 0;

        /// <summary>
        /// Text for the trackside LED screens. Track status has priority, then the start sequence and
        /// the finish; otherwise the screens alternate between the event messages and the race state.
        /// </summary>
        public static string ScreenMessage(RaceDirector.Phase phase, TrackStatus status, double now, string leaderCode = null, int lap = 0, int laps = 0)
        {
            if (phase == RaceDirector.Phase.Racing)
            {
                switch (status)
                {
                    case TrackStatus.SafetyCar:
                    case TrackStatus.SafetyCarIn:
                        return "GÜVENLİK ARACI";
                    case TrackStatus.VirtualSafetyCar:
                    case TrackStatus.VirtualSafetyCarEnding:
                        return "SANAL GÜVENLİK ARACI";
                }
            }
            if (phase == RaceDirector.Phase.Lights) return "YARIŞ BAŞLIYOR";
            if (phase == RaceDirector.Phase.Finished) return "DAMALI BAYRAK";
            int slot = (int)Math.Floor(Math.Max(0.0, now) / MessageSeconds);
            if (phase == RaceDirector.Phase.Racing && !string.IsNullOrEmpty(leaderCode) && slot % 2 == 1)
                return laps > 0 ? $"LİDER {leaderCode} · TUR {Math.Min(lap, laps)}/{laps}" : $"LİDER {leaderCode}";
            return IdleMessages[(slot / (phase == RaceDirector.Phase.Racing ? 2 : 1)) % IdleMessages.Length];
        }

        /// <summary>Rows for the timing tower: "position code", in race order, at most `rows` entries.</summary>
        public static List<string> TowerRows(IReadOnlyList<RaceDirector.Entry> standings, int rows)
        {
            var list = new List<string>();
            if (standings == null) return list;
            for (int i = 0; i < standings.Count && list.Count < rows; i++)
            {
                var e = standings[i];
                string code = string.IsNullOrEmpty(e.code) ? (e.name ?? "").ToUpperInvariant() : e.code;
                if (code.Length > 3) code = code.Substring(0, 3);
                list.Add($"{i + 1,2} {code}");
            }
            return list;
        }
    }
}
