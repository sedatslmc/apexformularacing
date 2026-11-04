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
        /// <summary>Speed limit line after the entry (m): the limiter runs from here to the exit line.</summary>
        public const float LimiterStart = 15f;
        /// <summary>The pit wall starts this far after the entry and ends this far after the exit line (m).</summary>
        public const float WallStart = 10f, WallAfterLimit = 10f;
        /// <summary>Share of the exit taper after the end of the pit area by which an automatic car is back on the track.</summary>
        public const float MergeShare = 0.65f;
        /// <summary>Longest lane change back onto the track (m): ~1.5 s at 190 km/h.</summary>
        public const float MergeLength = 80f;

        /// <summary>
        /// The lane along the lap, in metres after the entry: entry lane (−entryTaper … 0) beside the track behind the
        /// white entry line, speed limit from <see cref="LimiterStart"/> to the exit line (<see cref="limitSpan"/>),
        /// pit wall until <see cref="WallEnd"/>, then the acceleration lane behind the white exit line to the merge
        /// (<see cref="MergeEnd"/>), within the pit area (<see cref="span"/>) and its exit taper.
        /// </summary>
        public readonly struct Plan
        {
            public readonly float span, limitSpan, entryTaper, exitTaper;
            public Plan(float span, float limitSpan, float entryTaper, float exitTaper)
            {
                this.span = span;
                this.limitSpan = MathF.Min(limitSpan, span);
                this.entryTaper = entryTaper;
                this.exitTaper = exitTaper;
            }
            public float WallEnd => MathF.Min(span, limitSpan + WallAfterLimit);
            public float MergeEnd => span + exitTaper * MergeShare;
            public float MergeStart => MathF.Max(WallEnd, MergeEnd - MergeLength);
            /// <summary>Is the speed limiter on <paramref name="since"/> metres after the entry?</summary>
            public bool Limited(float since) => since >= LimiterStart && since <= limitSpan;
        }

        /// <summary>Lateral target from the edge between the track (share 0: 1.6 m inside the edge) and the fast lane (1).</summary>
        public static float LaneTarget(float share)
        {
            share = ApexMath.Clamp01(share);
            return ApexMath.Lerp(-1.6f, FastLaneCentre, share * share * (3f - 2f * share));
        }

        /// <summary>
        /// Lateral target from the edge (m, outwards) for an automatic car on a pit stop, as RaceController drives it:
        /// along the entry lane as it opens (<paramref name="since"/> &lt; 0: metres before the entry), the fast lane,
        /// into the own box and out (<see cref="FromEdge"/>), then the acceleration lane after the exit line, merging
        /// onto the track between <see cref="Plan.MergeStart"/> and <see cref="Plan.MergeEnd"/>.
        /// </summary>
        public static float Route(in Plan plan, float since, float toBox, bool served, float boxCentre)
        {
            if (since > plan.MergeStart)
                return LaneTarget(1f - (since - plan.MergeStart) / MathF.Max(plan.MergeEnd - plan.MergeStart, 1f));
            float f = FromEdge(toBox, served, boxCentre);
            if (since < 0f) f = MathF.Min(f, LaneTarget(1f + since / MathF.Max(plan.entryTaper, 1f)));
            return f;
        }

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
