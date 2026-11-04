using System;
using Apex.Core;

namespace Apex.Race
{
    /// <summary>
    /// Lanes of the pit lane (docs/PIT_LANE.md), measured from the track edge outwards (towards the garages):
    /// track-side strip 0–0.45 m, pit wall (concrete with catch fence) 0.45–0.85 m, pit wall deck with the team stands
    /// 0.85–2.25 m, fast lane 2.6–6.0 m (car centre 4.3 m: more than a metre of air to the stands), working lane from
    /// 6.0 m with the boxes (car centre at CircuitDefinition.PitLane.laneCentreFromEdge, 8.3 m) and the crews up to the
    /// garage fronts. Cars drive in the fast lane and only turn into the working lane for their own box.
    /// </summary>
    public static class PitLaneLayout
    {
        public const float ApronWidth = 2f, FastLaneCentre = 4.3f, Divider = 6.0f, BoxWidth = 3.6f, BoxLength = 6f;
        /// <summary>Distance before the box over which a car turns in from the fast lane, and after it over which it pulls out (m).</summary>
        public const float TurnIn = 28f, PullOut = 30f;
        /// <summary>Half width of a car (m) for clearance checks.</summary>
        public const float CarHalfWidth = 0.95f;
        /// <summary>Pit wall cross-section from the track edge (m): concrete wall, deck for the team stands, fast lane edge.</summary>
        public const float WallInner = 0.45f, WallOuter = 0.85f, DeckOuter = 2.25f, FastLaneInner = 2.6f;
        /// <summary>Collision face of the pit wall (its track-side face).</summary>
        public const float WallFromEdge = WallInner;
        /// <summary>Crash cushion in front of the wall's nose in the entry lane (m).</summary>
        public const float AttenuatorLength = 5f;

        /// <summary>Speed limit line after the entry (m): the limiter runs from here to the exit line.</summary>
        public const float LimiterStart = 15f;
        /// <summary>
        /// The pit wall's nose lies this far after the entry (negative: in the entry lane, where the lane has moved far
        /// enough off the track that the wall separates it from the race traffic), and it ends this far after the exit line (m).
        /// </summary>
        public const float WallStart = -30f, WallAfterLimit = 10f;
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
            /// <summary>Latest start of the merge when race traffic holds a car back (the lane is still wide enough to finish it).</summary>
            public float LatestMergeStart => MergeStart + 30f;
            /// <summary>Length of the lane change back onto the track.</summary>
            public float MergeSpan => MathF.Max(MergeEnd - MergeStart, 30f);
            /// <summary>Is the speed limiter on <paramref name="since"/> metres after the entry?</summary>
            public bool Limited(float since) => since >= LimiterStart && since <= limitSpan;
        }

        /// <summary>Where a car on its way to or from the pit lane drives on the track (m from the edge, negative = on the track).</summary>
        public const float TrackLine = -1.6f;
        /// <summary>The automatic car is at the pit limit this far before the entry: braked well before the wall's nose.</summary>
        public const float BrakedBy = -40f;
        /// <summary>Deceleration (m/s²) of the automatic car's braking towards the pit entry (firm, not a full stop).</summary>
        public const float EntryDecel = 7f;

        /// <summary>Lateral target from the edge between the track (share 0: 1.6 m inside the edge) and the fast lane (1).</summary>
        public static float LaneTarget(float share)
        {
            share = ApexMath.Clamp01(share);
            return ApexMath.Lerp(TrackLine, FastLaneCentre, share * share * (3f - 2f * share));
        }

        /// <summary>
        /// Lateral target from the edge (m, outwards) for an automatic car on a pit stop, as RaceController drives it:
        /// along the entry lane as it opens (<paramref name="since"/> &lt; 0: metres before the entry), the fast lane,
        /// into the own box and out (<see cref="FromEdge"/>), then the acceleration lane after the exit line, merging
        /// onto the track between <see cref="Plan.MergeStart"/> and <see cref="Plan.MergeEnd"/>.
        /// </summary>
        public static float Route(in Plan plan, float since, float toBox, bool served, float boxCentre, float mergeFrom = float.NaN)
        {
            // mergeFrom: where the merge began (NaN = at MergeStart); a car held back by traffic passes its own position
            // (stays in the lane) until it may go.
            float m = float.IsNaN(mergeFrom) ? plan.MergeStart : mergeFrom;
            if (since > plan.MergeStart)
                return LaneTarget(1f - (since - m) / plan.MergeSpan);
            // Into the box straight on the box line for the last metres (PitBoxStop.StraightIn): parked square to the crew.
            float f = FromEdge(served ? toBox : toBox - PitBoxStop.StraightIn, served, boxCentre);
            if (since < 0f) f = MathF.Min(f, LaneTarget(1f + since / MathF.Max(plan.entryTaper, 1f)));
            return f;
        }

        /// <summary>What an automatic car on a pit stop is told to do (RaceController and the drive tests).</summary>
        public struct Command
        {
            /// <summary>Lateral target from the track edge (m, outwards).</summary>
            public float fromEdge;
            /// <summary>Speed cap (m/s).</summary>
            public float speedCap;
            /// <summary>Stand on the brakes now (on the box mark).</summary>
            public bool brake;
        }

        /// <summary>
        /// The whole pit stop for an automatic car: brake down to the limit by the limit line, follow the lanes, slow
        /// down onto the box and stop with the car's origin on the box centre (PitBoxStop.ApproachSpeed / BrakeNow), then
        /// out and through the exit lane. <paramref name="toBox"/>: metres to the own box centre; <paramref name="served"/>:
        /// the stop is done.
        /// </summary>
        public static Command Drive(in Plan plan, float since, float toBox, bool served, float boxCentre, float limit, float speed, float mergeFrom = float.NaN)
        {
            // The lateral target is given for the point the driver steers at (AIDriver.Lookahead): the car follows the
            // entry lane, the box turn-in and the merge on the path instead of lagging behind it and cutting into the
            // wall's nose at speed. On the last metres into the box the look-ahead shrinks so the stop stays square.
            float lead = AI.AIDriver.Lookahead(speed);
            if (!served) lead *= ApexMath.Clamp01((toBox - PitBoxStop.StraightIn) / 20f);
            // Pulling away: straight out of the box first (the crew stands beside it), the look-ahead grows after 6 m.
            else if (toBox > -40f) lead *= ApexMath.Clamp01((-toBox - 6f) / 20f);
            var c = new Command { fromEdge = Route(plan, since + lead, toBox - lead, served, boxCentre, mergeFrom), speedCap = float.PositiveInfinity };
            if (since < BrakedBy)
                c.speedCap = MathF.Sqrt(limit * limit + 2f * EntryDecel * (BrakedBy - since));   // at the limit 40 m before the entry
            else if (since < plan.limitSpan - 2f)
                c.speedCap = limit;
            if (since >= BrakedBy && since < LimiterStart) c.speedCap = limit;
            if (!served && toBox < 60f && toBox > -PitBoxStop.PushBackLimit)
            {
                c.speedCap = MathF.Min(c.speedCap, PitBoxStop.ApproachSpeed(toBox, limit));
                c.brake = PitBoxStop.BrakeNow(toBox, speed);
            }
            return c;
        }

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
