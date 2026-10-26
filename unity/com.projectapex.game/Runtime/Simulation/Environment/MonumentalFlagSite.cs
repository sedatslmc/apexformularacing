using System;
using System.Numerics;
using Apex.Tracks;

namespace Apex.Environment
{
    /// <summary>
    /// Site of the monumental Turkish flag: a giant flag on a tall pole, as at prominent places in
    /// Istanbul. Searched on the Yıldız hills (around the `yildiz_park` landmark of the circuit file;
    /// otherwise the highest ground near the lap). The site must be on land, keep the
    /// flag's swept circle plus a margin clear of every track part, and is scored by how many points
    /// of the lap see the flag (terrain line of sight from a driver's eye height to mid-flag) and by
    /// its elevation; coarse 40 m grid, then 10 m around the best three. Steep hillsides (more than
    /// <see cref="MaxSlopeDrop"/> under the terrace) are left out. Buildings are kept away by a reserved area (<see cref="ReservedRadius"/>).
    /// </summary>
    public sealed class MonumentalFlagSite
    {
        /// <summary>Pole height above its foundation and flag length (fly) in metres; height = length / 1.5.</summary>
        public const float PoleHeight = 60f, FlagLength = 18f;
        public const float SearchRadius = 450f;
        /// <summary>Clearance of the swept circle (flag length) from every barrier line.</summary>
        public const float TrackMargin = 12f;
        /// <summary>City buildings stay outside this radius (foundation, plaza, the flag's sweep).</summary>
        public const float ReservedRadius = 28f;
        /// <summary>Largest height difference (m) of the ground under the 15 m terrace.</summary>
        public const float MaxSlopeDrop = 3.5f;
        public const float EyeHeight = 1.2f;

        public readonly bool found;
        public readonly Vector3 position;
        /// <summary>Share of lap sample points (every 50 m) from which mid-flag is visible over the terrain.</summary>
        public readonly float visibleShare;
        /// <summary>Distance (m) from the nearest barrier line.</summary>
        public readonly float trackMargin;

        public MonumentalFlagSite(TerrainField field, TrackGeometry g, TrackClearance clearance, CircuitDefinition def)
        {
            var centre = def.FindLandmark("yildiz_park");
            float cx, cz;
            if (centre != null) { cx = centre.east; cz = centre.north; }
            else
            {
                // Highest ground within 400 m of the lap.
                cx = g.Positions[0].X; cz = g.Positions[0].Z;
                float best = float.MinValue;
                for (int i = 0; i < g.Count; i += 10)
                {
                    var p = g.Positions[i] + g.Rights[i] * 200f;
                    float h = field.Height(p.X, p.Z);
                    if (h > best && !field.IsWater(p.X, p.Z)) { best = h; cx = p.X; cz = p.Z; }
                }
            }
            // Coarse pass (40 m grid, eyes every 100 m), then a fine pass around the three best sites
            // (10 m grid, eyes every 50 m): a few seconds at most, deterministic.
            var coarseEyes = Eyes(g, 100f);
            var fineEyes = Eyes(g, 50f);
            var top = new System.Collections.Generic.List<(float score, float x, float z)>();
            for (float dz = -SearchRadius; dz <= SearchRadius; dz += 40f)
                for (float dx = -SearchRadius; dx <= SearchRadius; dx += 40f)
                {
                    if (dx * dx + dz * dz > SearchRadius * SearchRadius) continue;
                    if (Score(field, clearance, coarseEyes, cx + dx, cz + dz, 25f, out float sc, out _, out _))
                        top.Add((sc, cx + dx, cz + dz));
                }
            top.Sort((a, b) => b.score.CompareTo(a.score));
            float bestScore = float.MinValue;
            for (int k = 0; k < Math.Min(3, top.Count); k++)
                for (float dz = -30f; dz <= 30f; dz += 10f)
                    for (float dx = -30f; dx <= 30f; dx += 10f)
                    {
                        float x = top[k].x + dx, z = top[k].z + dz;
                        if (!Score(field, clearance, fineEyes, x, z, 15f, out float sc, out float share, out float margin) || sc <= bestScore) continue;
                        bestScore = sc;
                        found = true;
                        position = new Vector3(x, field.Height(x, z), z);
                        visibleShare = share;
                        trackMargin = margin;
                    }
        }

        /// <summary>Checks a given site (e.g. stored in the circuit file): on land, clear of the circuit by the flag's sweep
        /// plus margin; returns the visible share of the lap (eyes every 50 m). Fast enough for loading.</summary>
        public static bool Validate(TerrainField field, TrackGeometry g, TrackClearance clearance, float x, float z, out float visibleShare, out float trackMargin)
        {
            bool ok = Score(field, clearance, Eyes(g, 50f), x, z, 15f, out _, out visibleShare, out trackMargin);
            return ok;
        }

        static System.Collections.Generic.List<Vector3> Eyes(TrackGeometry g, float step)
        {
            var eyes = new System.Collections.Generic.List<Vector3>();
            for (float d = 0f; d < g.Length; d += step)
            {
                var p = g.PositionAt(d);
                eyes.Add(new Vector3(p.X, p.Y + EyeHeight, p.Z));
            }
            return eyes;
        }

        /// <summary>Site score: visibility from the lap first, then a little for higher ground and for being near the lap.</summary>
        static bool Score(TerrainField field, TrackClearance clearance, System.Collections.Generic.List<Vector3> eyes, float x, float z, float sightStep,
            out float score, out float share, out float margin)
        {
            score = share = 0f;
            margin = 0f;
            if (!field.InNearArea(x, z) || field.IsWater(x, z) || field.land.CoastDistance(x, z) < 30f) return false;
            margin = clearance.Margin(x, z);
            if (margin < FlagLength + TrackMargin) return false;
            float ground = field.Height(x, z);
            // Ground under the 15 m terrace: at most MaxSlopeDrop of height difference (no towering podium on a steep hillside).
            float lo = ground, hi = ground;
            for (int k = 0; k < 12; k++)
            {
                float a = k * MathF.PI / 6f;
                float h = field.Height(x + MathF.Cos(a) * 15f, z + MathF.Sin(a) * 15f);
                lo = MathF.Min(lo, h);
                hi = MathF.Max(hi, h);
            }
            if (hi - lo > MaxSlopeDrop) return false;
            var flagMid = new Vector3(x, ground + PoleHeight - FlagLength / TurkishFlagDesign.AspectRatio * 0.5f, z);
            int seen = 0;
            foreach (var e in eyes) if (Visible(field, e, flagMid, sightStep)) seen++;
            share = seen / (float)eyes.Count;
            score = share * 100f + ground * 0.1f - MathF.Min(margin, 400f) * 0.02f;
            return true;
        }

        /// <summary>Terrain line of sight from eye to target (buildings are not considered).</summary>
        public static bool Visible(TerrainField field, Vector3 eye, Vector3 target, float sightStep = 10f)
        {
            var d = target - eye;
            float len = new Vector2(d.X, d.Z).Length();
            if (len > 2500f) return false;
            int steps = Math.Max(1, (int)(len / sightStep));
            for (int k = 1; k < steps; k++)
            {
                float t = k / (float)steps;
                var p = eye + d * t;
                if (field.Height(p.X, p.Z) > p.Y) return false;
            }
            return true;
        }

        public float FlagHeight => FlagLength / TurkishFlagDesign.AspectRatio;
    }
}
