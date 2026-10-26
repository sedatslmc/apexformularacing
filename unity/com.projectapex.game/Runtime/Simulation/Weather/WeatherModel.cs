using System;
using System.Collections.Generic;
using Apex.Core;

namespace Apex.Weather
{
    public enum WeatherState
    {
        Clear,
        Overcast,
        LightRain,
        HeavyRain,
    }

    public enum TimeOfDay
    {
        Morning,
        Day,
        GoldenHour,
        Dusk,
        Night,
    }

    /// <summary>
    /// Weather and track water. Rain intensity eases towards the current state's
    /// target, the track wets under rain and dries without it (faster with sun and
    /// heat). Dynamic mode changes state at random intervals from a seeded RNG, so a
    /// session is reproducible; the forecast lists the upcoming planned states.
    /// </summary>
    public sealed class WeatherModel
    {
        public WeatherState State { get; private set; }
        public float RainIntensity { get; private set; }
        public float CloudCover { get; private set; }
        /// <summary>0 dry .. 1 standing water.</summary>
        public float TrackWetness { get; private set; }
        public float AirTemp { get; private set; } = 24f;
        public float TrackTemp { get; private set; } = 34f;
        public bool Dynamic { get; }

        public event Action<WeatherState> StateChanged;

        public readonly struct ForecastItem
        {
            public readonly double time;
            public readonly WeatherState state;

            public ForecastItem(double time, WeatherState state)
            {
                this.time = time;
                this.state = state;
            }
        }

        readonly List<ForecastItem> plan = new List<ForecastItem>();
        readonly Random rng;
        double now;

        public WeatherModel(WeatherState start, bool dynamic, int seed, float initialWetness = -1f)
        {
            State = start;
            Dynamic = dynamic;
            rng = new Random(seed);
            RainIntensity = Target(start).rain;
            CloudCover = Target(start).cloud;
            TrackWetness = initialWetness >= 0f ? initialWetness : (start == WeatherState.HeavyRain ? 0.85f : start == WeatherState.LightRain ? 0.45f : 0f);
            if (dynamic) PlanAhead();
        }

        static (float rain, float cloud) Target(WeatherState s)
        {
            switch (s)
            {
                case WeatherState.Overcast: return (0f, 0.8f);
                case WeatherState.LightRain: return (0.35f, 0.95f);
                case WeatherState.HeavyRain: return (1f, 1f);
                default: return (0f, 0.15f);
            }
        }

        void PlanAhead()
        {
            double t = plan.Count > 0 ? plan[plan.Count - 1].time : now;
            var last = plan.Count > 0 ? plan[plan.Count - 1].state : State;
            while (plan.Count < 4)
            {
                t += 240 + rng.NextDouble() * 480;
                int step = rng.NextDouble() < 0.5 ? -1 : 1;
                last = (WeatherState)ApexMath.Clamp((int)last + step, 0, 3);
                plan.Add(new ForecastItem(t, last));
            }
        }

        public IReadOnlyList<ForecastItem> Forecast => plan;

        public void Update(double time, float dt)
        {
            now = time;
            if (Dynamic && plan.Count > 0 && time >= plan[0].time)
            {
                var next = plan[0].state;
                plan.RemoveAt(0);
                PlanAhead();
                if (next != State)
                {
                    State = next;
                    StateChanged?.Invoke(next);
                }
            }
            var target = Target(State);
            RainIntensity = ApexMath.MoveToward(RainIntensity, target.rain, dt / 90f);
            CloudCover = ApexMath.MoveToward(CloudCover, target.cloud, dt / 120f);
            float wetting = RainIntensity * 0.008f * (1f - TrackWetness * 0.2f);
            float sun = 1f - CloudCover;
            float drying = RainIntensity < 0.05f ? (0.0012f + 0.0018f * sun) * (1f + (TrackTemp - 20f) / 40f) : 0f;
            TrackWetness = ApexMath.Clamp01(TrackWetness + (wetting - drying) * dt);
            float targetTrackTemp = AirTemp + 14f * sun - 10f * TrackWetness;
            TrackTemp = ApexMath.MoveToward(TrackTemp, targetTrackTemp, dt / 60f);
        }

        public static string Name(WeatherState s)
        {
            switch (s)
            {
                case WeatherState.Overcast: return "Bewölkt";
                case WeatherState.LightRain: return "Leichter Regen";
                case WeatherState.HeavyRain: return "Starker Regen";
                default: return "Klar";
            }
        }
    }
}
