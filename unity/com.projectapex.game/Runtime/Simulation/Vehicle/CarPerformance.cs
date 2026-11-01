using System;

namespace Apex.Vehicle
{
    /// <summary>
    /// The one place where the car's grip becomes planning numbers (handling brief 2026-11, section 16 "Gemeinsame
    /// Physikbasis"): the AI's racing line speeds, the dynamic racing-line guide's target speeds and braking points
    /// and the test drivers all use these values, so more tyre grip or more downforce moves corner speeds and braking
    /// points for everyone at once. Before, the planner used a fixed friction of 1.45 regardless of the car.
    /// </summary>
    public readonly struct CarPerformance
    {
        /// <summary>
        /// Planning friction over the tyres' peak friction: 1.45 / 1.8, the value the AI line was calibrated with
        /// (October 2026). It covers what the tyre peak does not deliver in practice: load sensitivity, the combined
        /// slip of trail braking and power, the kerbs and a small margin to the limit.
        /// </summary>
        public const float PlanningShare = 1.45f / 1.8f;

        /// <summary>Planning friction coefficient (RacingLine mu).</summary>
        public readonly float mu;
        /// <summary>Downforce per unit mass per (m/s)² (RacingLine downforcePerMass).</summary>
        public readonly float downforcePerMass;

        public CarPerformance(float mu, float downforcePerMass)
        {
            this.mu = mu;
            this.downforcePerMass = downforcePerMass;
        }

        public static CarPerformance For(VehicleSetup s)
        {
            float tyreMu = 0.5f * (s.tireFront.muPeak + s.tireRear.muPeak);
            return new CarPerformance(PlanningShare * tyreMu, s.downforceArea * s.airDensity * 0.5f / s.mass);
        }

        /// <summary>The fixed reference the circuit layout (kerbs, rubber, brake boards) was built with; kept so the
        /// track furniture does not move when the car is tuned.</summary>
        public static CarPerformance LayoutReference(VehicleSetup s) => new CarPerformance(1.45f, s.downforceArea * s.airDensity * 0.5f / s.mass);
    }
}
