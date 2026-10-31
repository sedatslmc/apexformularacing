using System;
using System.Collections.Generic;
using Apex.Core;
using Apex.Race;
using Apex.Vehicle;
using Apex.Weather;

namespace Apex.UI
{
    public enum SetupMode { GrandPrix, Practice, TimeTrial, Championship }

    /// <summary>One adjustable setting on a setup screen: it reads and changes the real <see cref="SessionConfig"/>.</summary>
    public sealed class SetupOption
    {
        public string key = "", group = "", label = "";
        public Func<string> value;
        /// <summary>Step the value: -1 previous, +1 next (confirm = +1).</summary>
        public Action<int> change;
    }

    /// <summary>
    /// Settings of the Grand Prix, time trial, practice and championship setup screens (docs/UI_UX_OVERHAUL.md,
    /// Phase 3), grouped as Strecke / Rennformat / Feld / Regeln. Every option changes an existing field of
    /// <see cref="SessionConfig"/>; options that do not matter for the mode are not shown (time trial has no
    /// weather or tyres; the qualifying length only with a qualifying session).
    /// </summary>
    public static class SetupModel
    {
        public const string GroupTrack = "Strecke & Bedingungen", GroupFormat = "Rennformat", GroupField = "Feld", GroupRules = "Regeln";

        /// <summary>Championship calendar (fixed until custom championships exist).</summary>
        public static readonly string[] ChampionshipCalendar = { "bosphorus_a", "test_loop" };

        public static string Title(SetupMode m)
        {
            switch (m)
            {
                case SetupMode.GrandPrix: return "Grand Prix";
                case SetupMode.TimeTrial: return "Zeitfahren";
                case SetupMode.Practice: return "Freies Training";
                default: return "Meisterschaft";
            }
        }

        public static string TimeName(TimeOfDay t)
        {
            switch (t)
            {
                case TimeOfDay.Morning: return "Morgen";
                case TimeOfDay.Day: return "Mittag";
                case TimeOfDay.Dusk: return "Sonnenuntergang";
                case TimeOfDay.Night: return "Nacht";
                default: return "Goldene Stunde";
            }
        }

        static int Wrap(int v, int n) => ((v % n) + n) % n;
        static int Step(int d) => d < 0 ? -1 : 1;
        static string OnOff(bool b) => b ? "An" : "Aus";

        /// <param name="trackIds">Selectable tracks.</param>
        /// <param name="trackName">Display name of a track id.</param>
        public static List<SetupOption> Options(SetupMode mode, SessionConfig c, IList<string> trackIds, Func<string, string> trackName)
        {
            var o = new List<SetupOption>();
            void Add(string key, string group, string label, Func<string> value, Action<int> change) =>
                o.Add(new SetupOption { key = key, group = group, label = label, value = value, change = change });

            if (mode == SetupMode.Championship)
            {
                // Starting a season: the calendar is fixed; laps, field and team apply to every round.
                Add("laps", GroupFormat, "Runden pro Rennen", () => c.laps.ToString(), d => c.laps += Step(d));
                Add("ai", GroupField, "KI-Gegner", () => c.aiCount.ToString(), d => c.aiCount += Step(d));
                Add("difficulty", GroupField, "KI-Stärke", () => $"{c.difficulty * 100f:0} %", d => c.difficulty += Step(d) * 0.1f);
                Add("team", GroupField, "Team", () => Roster.Teams[c.playerTeam].name, d => c.playerTeam = Wrap(c.playerTeam + Step(d), Roster.Teams.Length));
                return o;
            }

            Add("track", GroupTrack, "Strecke", () => trackName(c.trackId), d =>
            {
                int i = trackIds.IndexOf(c.trackId);
                c.trackId = trackIds[Wrap((i < 0 ? 0 : i) + Step(d), trackIds.Count)];
            });
            Add("time", GroupTrack, "Tageszeit", () => TimeName(c.timeOfDay), d => c.timeOfDay = (TimeOfDay)Wrap((int)c.timeOfDay + Step(d), 5));
            if (mode != SetupMode.TimeTrial)
            {
                Add("weather", GroupTrack, "Wetter", () => WeatherModel.Name(c.weather), d => c.weather = (WeatherState)Wrap((int)c.weather + Step(d), 4));
                if (mode == SetupMode.GrandPrix)
                    Add("dynamicWeather", GroupTrack, "Wetterwechsel", () => OnOff(c.dynamicWeather), d => c.dynamicWeather = !c.dynamicWeather);
            }
            if (mode == SetupMode.GrandPrix)
            {
                Add("format", GroupFormat, "Format", () => c.format == WeekendFormat.RaceOnly ? "Nur Rennen" : "Qualifying + Rennen",
                    d => c.format = c.format == WeekendFormat.RaceOnly ? WeekendFormat.QualifyingAndRace : WeekendFormat.RaceOnly);
                Add("laps", GroupFormat, "Runden", () => c.laps.ToString(), d => c.laps += Step(d));
                if (c.format == WeekendFormat.QualifyingAndRace)
                    Add("qualifying", GroupFormat, "Qualifying-Dauer", () => $"{c.qualifyingMinutes:0} min", d => c.qualifyingMinutes += Step(d));
            }
            if (mode != SetupMode.TimeTrial)
            {
                Add("compound", GroupFormat, "Startreifen", () => CompoundSpec.For(c.startCompound).name, d => c.startCompound = (TireCompound)Wrap((int)c.startCompound + Step(d), 3));
                Add("wear", GroupFormat, "Reifenverschleiß", () => c.tireWear <= 0f ? "Aus" : $"{c.tireWear:0}×", d => c.tireWear = Math.Max(0f, Math.Min(10f, c.tireWear + Step(d))));
            }
            if (mode == SetupMode.GrandPrix)
            {
                Add("ai", GroupField, "KI-Gegner", () => c.aiCount.ToString(), d => c.aiCount += Step(d));
                Add("difficulty", GroupField, "KI-Stärke", () => $"{c.difficulty * 100f:0} %", d => c.difficulty += Step(d) * 0.1f);
            }
            Add("team", GroupField, "Team", () => Roster.Teams[c.playerTeam].name, d => c.playerTeam = Wrap(c.playerTeam + Step(d), Roster.Teams.Length));
            if (mode == SetupMode.GrandPrix)
            {
                Add("falseStarts", GroupRules, "Frühstart-Strafe", () => OnOff(c.falseStarts), d => c.falseStarts = !c.falseStarts);
                Add("trackLimits", GroupRules, "Tracklimit-Strafen", () => OnOff(c.trackLimitPenalties), d => c.trackLimitPenalties = !c.trackLimitPenalties);
                Add("pitStop", GroupRules, "Pflicht-Boxenstopp", () => OnOff(c.mandatoryPitStop), d => c.mandatoryPitStop = !c.mandatoryPitStop);
                Add("safetyCar", GroupRules, "Safety Car / VSC", () => OnOff(c.safetyCar), d => c.safetyCar = !c.safetyCar);
                Add("damage", GroupRules, "Schäden", () => OnOff(c.damage), d => c.damage = !c.damage);
                Add("failures", GroupRules, "Technische Defekte (KI)", () => c.failures == 0 ? "Aus" : c.failures == 1 ? "Selten" : "Häufig",
                    d => c.failures = Wrap(c.failures + Step(d), 3));
            }
            return o;
        }

        /// <summary>One line describing what "Start" will do, from the real settings.</summary>
        public static string Summary(SetupMode mode, SessionConfig c)
        {
            switch (mode)
            {
                case SetupMode.GrandPrix:
                    string race = $"Rennen {c.laps} {(c.laps == 1 ? "Runde" : "Runden")}";
                    string field = c.aiCount == 0 ? "ohne Gegner" : $"{c.aiCount} KI-Gegner";
                    return (c.format == WeekendFormat.QualifyingAndRace ? $"Qualifying {c.qualifyingMinutes:0} min + {race}" : race) + " · " + field;
                case SetupMode.TimeTrial: return "Allein auf der Strecke · Bestzeit und Sektoren";
                case SetupMode.Practice: return "Freie Fahrt ohne Zeitlimit";
                default: return $"{ChampionshipCalendar.Length} Rennen · {c.laps} {(c.laps == 1 ? "Runde" : "Runden")} · {c.aiCount} KI-Gegner";
            }
        }

        /// <summary>The first session that "Start" opens for a mode (Grand Prix with qualifying starts with it).</summary>
        public static SessionType FirstSession(SetupMode mode, SessionConfig c) =>
            mode == SetupMode.GrandPrix ? (c.format == WeekendFormat.QualifyingAndRace ? SessionType.Qualifying : SessionType.Race)
            : mode == SetupMode.Championship ? SessionType.Race : SessionType.Practice;

        /// <summary>The championship points scheme as shown on the season screen (from <see cref="RaceRules.Points"/>).</summary>
        public static string PointsScheme()
        {
            return string.Join("-", Array.ConvertAll(RaceRules.Points, p => p.ToString())) + ", +1 für die schnellste Runde (Top 10)";
        }
    }
}
