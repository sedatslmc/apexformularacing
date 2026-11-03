using System;
using Apex.Core;

namespace Apex.Race
{
    /// <summary>
    /// Lanes of the pit lane (docs/PIT_LANE.md), measured from the track edge outwards (towards the garages):
    /// apron 0–2 m, fast lane 2–5.5 m, working lane from 5.5 m with the boxes (car centre at
    /// CircuitDefinition.PitLane.laneCentreFromEdge, 7.5 m) and the crews up to the garage fronts. Cars drive in the
    /// fast lane and only turn into the working lane for their own box – before, they drove the whole lane on the box
    /// line, straight through every other team's box.
    /// </summary>
    public static class PitLaneLayout
    {
        public const float ApronWidth = 2f, FastLaneCentre = 3.75f, Divider = 5.5f, BoxWidth = 3.6f, BoxLength = 6f;
        /// <summary>Distance before the box over which a car turns in from the fast lane, and after it over which it pulls out (m).</summary>
        public const float TurnIn = 28f, PullOut = 24f;
        /// <summary>Half width of a car (m) for clearance checks.</summary>
        public const float CarHalfWidth = 0.95f;

        /// <summary>
        /// Lateral offset from the track edge (m, outwards) for a car in the lane. <paramref name="toBox"/>: metres to its
        /// box centre (negative once past it); <paramref name="served"/>: the stop is done (pull out to the fast lane).
        /// </summary>
        public static float FromEdge(float toBox, bool served, float boxCentre)
        {
            if (!served)
            {
                if (toBox > TurnIn) return FastLaneCentre;
                float k = 1f - ApexMath.Clamp01(toBox / TurnIn);
                return ApexMath.Lerp(FastLaneCentre, boxCentre, k * k * (3f - 2f * k));
            }
            float after = ApexMath.Clamp01(-toBox / PullOut);
            return ApexMath.Lerp(boxCentre, FastLaneCentre, after * after * (3f - 2f * after));
        }
    }
}
