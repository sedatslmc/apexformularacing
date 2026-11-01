using System;
using System.Collections.Generic;
using System.Numerics;
using Apex.Tracks;

namespace Apex.Environment
{
    /// <summary>
    /// Sightlines from the cockpit (docs/ISTANBUL_OVERHAUL.md, Phase 6): how far ahead the driver sees the road, and
    /// whether the brake boards show in time. Occluders are the road itself over crests, the terrain and the OSM
    /// buildings beyond the barriers. Not modelled: trees, the barrier and fence (the eye sits below the 1.1 m wall,
    /// as in a real street circuit, so the road inside a walled corner is never in view), the car's own bodywork.
    /// </summary>
    public sealed class DriverSight
    {
        /// <summary>Eye height of a single-seater driver above the road (m) and the target height (a car, m).</summary>
        public const float EyeHeight = 0.95f, TargetHeight = 0.5f;
        public const float Step = 10f, MaxRange = 300f, SampleSpacing = 3f;
        /// <summary>A brake board must show from this long before the car reaches it (s).</summary>
        public const float BoardWarning = 1.5f;
        /// <summary>Board top above the road (m) and offset beyond the road edge (m): TrackInfrastructureBuilder.Board.</summary>
        public const float BoardTop = 3.1f, BoardOutside = 2.5f;

        public enum Blocker { None, Crest, Terrain, Building }

        readonly TrackGeometry g;
        readonly TrackClearance clearance;
        readonly TerrainField field;
        readonly OsmCity city;

        /// <summary>Sight distance (m, up to <see cref="MaxRange"/>) every <see cref="Step"/> m of the lap and what ends it.</summary>
        public readonly List<(float distance, float sight, Blocker by)> profile = new List<(float, float, Blocker)>();

        public DriverSight(TrackGeometry g, TrackClearance clearance, TerrainField field, OsmCity city)
        {
            this.g = g; this.clearance = clearance; this.field = field; this.city = city;
        }

        Vector3 Eye(float s) => g.PositionAt(g.Wrap(s)) + Vector3.UnitY * EyeHeight;

        /// <summary>What blocks the straight line from <paramref name="eye"/> (at lap distance s0) to the target at lap distance s1, if anything.</summary>
        public Blocker Blocked(Vector3 eye, Vector3 target, float s0, float s1)
        {
            float len = Vector3.Distance(eye, target);
            int n = Math.Max(2, (int)(len / SampleSpacing));
            float along = s1 - s0;
            for (int k = 1; k < n; k++)
            {
                float t = k / (float)n;
                var p = Vector3.Lerp(eye, target, t);
                if (len * (1f - t) < 3f) break; // the target's own footing (board post, kerb)
                float m = clearance.Margin(p.X, p.Z);
                if (m <= 0f)
                {
                    // Over the racing envelope: the road surface itself (crests). The sight line follows the lap
                    // closely enough on the approaches to take the road height at the matching lap distance.
                    float road = g.PositionAt(g.Wrap(s0 + along * t)).Y;
                    if (p.Y < road + 0.05f) return Blocker.Crest;
                    continue;
                }
                if (m < 1.5f) continue; // the barrier strip: the wall is below the eye line by design
                if (p.Y < field.Height(p.X, p.Z)) return Blocker.Terrain;
                if (city != null && p.Y < city.BuildingTop(p.X, p.Z)) return Blocker.Building;
            }
            return Blocker.None;
        }

        /// <summary>Sight distance from lap distance s along the centre line.</summary>
        public (float sight, Blocker by) SightFrom(float s)
        {
            var eye = Eye(s);
            for (float d = Step; d <= MaxRange; d += Step)
            {
                var target = g.PositionAt(g.Wrap(s + d)) + Vector3.UnitY * TargetHeight;
                var b = Blocked(eye, target, s, s + d);
                if (b != Blocker.None) return (d - Step, b);
            }
            return (MaxRange, Blocker.None);
        }

        public void Survey()
        {
            profile.Clear();
            for (float s = 0f; s < g.Length; s += Step)
            {
                var (sight, by) = SightFrom(s);
                profile.Add((s, sight, by));
            }
        }

        /// <summary>Lap distance before the board from which its top is in view (m), up to <paramref name="needed"/> + 50 m.</summary>
        public float BoardVisibleFrom(TrackInfrastructurePlan.BrakeBoard b, float needed)
        {
            int i = g.IndexAt(g.Wrap(b.distance));
            var p = g.PositionAt(g.Wrap(b.distance)) + g.Rights[i] * (b.side * (g.Widths[i] * 0.5f + BoardOutside)) + Vector3.UnitY * BoardTop;
            float seen = 0f;
            for (float d = 5f; d <= needed + 50f; d += 5f)
            {
                var eye = Eye(b.distance - d);
                if (Blocked(eye, p, b.distance - d, b.distance) != Blocker.None) break;
                seen = d;
            }
            return seen;
        }
    }
}
