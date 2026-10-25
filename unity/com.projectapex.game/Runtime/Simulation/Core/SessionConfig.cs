using System;
using Apex.Race;
using Apex.Vehicle;
using Apex.Weather;

namespace Apex.Core
{
    public enum WeekendFormat
    {
        RaceOnly,
        QualifyingAndRace,
    }

    /// <summary>Everything chosen in the Grand Prix setup; persisted between launches.</summary>
    [Serializable]
    public sealed class SessionConfig
    {
        public string trackId = "bosphorus_gp";
        public SessionType sessionType = SessionType.Race;
        public WeekendFormat format = WeekendFormat.QualifyingAndRace;
        public int laps = 5;
        public float qualifyingMinutes = 8f;
        public int aiCount = 11;
        /// <summary>0 gentle .. 1 expert.</summary>
        public float difficulty = 0.5f;
        public WeatherState weather = WeatherState.Clear;
        public bool dynamicWeather;
        public TimeOfDay timeOfDay = TimeOfDay.GoldenHour;
        public float tireWear = 3f;
        public bool falseStarts = true;
        public bool trackLimitPenalties = true;
        public bool mandatoryPitStop;
        public TireCompound startCompound = TireCompound.Medium;
        public int playerTeam;
        public int seed = 1;

        public RaceRules MakeRules(SessionType type) => new RaceRules
        {
            type = type,
            laps = laps,
            sessionSeconds = type == SessionType.Race ? 0f : qualifyingMinutes * 60f,
            falseStarts = falseStarts,
            trackLimitPenalties = trackLimitPenalties,
            mandatoryPitStop = mandatoryPitStop && type == SessionType.Race,
        };

        public void Validate()
        {
            laps = Math.Max(1, Math.Min(60, laps));
            aiCount = Math.Max(0, Math.Min(19, aiCount));
            difficulty = Math.Max(0f, Math.Min(1f, difficulty));
            qualifyingMinutes = Math.Max(3f, Math.Min(30f, qualifyingMinutes));
            tireWear = Math.Max(0f, Math.Min(10f, tireWear));
            playerTeam = Math.Max(0, Math.Min(Roster.Teams.Length - 1, playerTeam));
        }
    }
}
