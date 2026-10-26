using System;
using System.Collections.Generic;
using System.Numerics;
using Apex.Core;

namespace Apex.Tracks
{
    /// <summary>
    /// The circuit's keep-out envelope: everything between the left and right barrier lines of every
    /// part of the lap (road, kerbs, run-off, pit lane), plus a margin. Street furniture, signs, flags
    /// and decoration placed relative to one part of the track must also stay out of every other part
    /// (hairpins, parallel stretches, the pit lane), which a "behind my own barrier" rule does not
    /// guarantee. Spatial bins over the centreline samples keep queries cheap.
    /// </summary>
    public sealed class TrackClearance
    {
        public const float BinSize = 32f;
        readonly TrackGeometry g;
        readonly float[] wallLeft, wallRight;
        readonly Dictionary<long, List<int>> bins = new Dictionary<long, List<int>>();
        readonly float maxWall;

        public TrackClearance(TrackGeometry geometry, float[] wallLeft, float[] wallRight)
        {
            g = geometry;
            this.wallLeft = wallLeft;
            this.wallRight = wallRight;
            for (int i = 0; i < g.Count; i++) maxWall = MathF.Max(maxWall, MathF.Max(wallLeft[i], wallRight[i]));
            for (int i = 0; i < g.Count; i++)
            {
                var p = g.Positions[i];
                long key = Key((int)MathF.Floor(p.X / BinSize), (int)MathF.Floor(p.Z / BinSize));
                if (!bins.TryGetValue(key, out var list)) bins[key] = list = new List<int>();
                list.Add(i);
            }
        }

        public TrackClearance(RoadSurface surface) : this(surface.geometry, surface.wallLeft, surface.wallRight) { }

        static long Key(int bx, int bz) => ((long)bx << 32) ^ (uint)bz;

        /// <summary>
        /// How far (m) the point lies outside the envelope of the nearest track part: negative inside
        /// (between the barrier lines), 0 on a barrier line. Points farther than the search range return +inf.
        /// </summary>
        public float Margin(float x, float z)
        {
            float best = float.PositiveInfinity;
            float range = maxWall + 8f;
            int r = (int)MathF.Ceiling(range / BinSize);
            int cx = (int)MathF.Floor(x / BinSize), cz = (int)MathF.Floor(z / BinSize);
            var q = new Vector2(x, z);
            for (int bz = cz - r; bz <= cz + r; bz++)
                for (int bx = cx - r; bx <= cx + r; bx++)
                {
                    if (!bins.TryGetValue(Key(bx, bz), out var list)) continue;
                    foreach (int i in list)
                    {
                        int j = (i + 1) % g.Count;
                        var a = new Vector2(g.Positions[i].X, g.Positions[i].Z);
                        var b = new Vector2(g.Positions[j].X, g.Positions[j].Z);
                        var ab = b - a;
                        float len2 = ab.LengthSquared();
                        if (len2 < 1e-6f) continue;
                        float raw = Vector2.Dot(q - a, ab) / len2;
                        float t = ApexMath.Clamp01(raw);
                        var foot = a + ab * t;
                        float len = MathF.Sqrt(len2);
                        float lat = (q.X - foot.X) * (ab.Y / len) - (q.Y - foot.Y) * (ab.X / len);
                        // Right of the direction of travel is +lateral (game frame, x right / z forward).
                        float wall = lat >= 0f ? ApexMath.Lerp(wallRight[i], wallRight[j], t) : ApexMath.Lerp(wallLeft[i], wallLeft[j], t);
                        // Beyond a segment end (outside of a curve) the distance to the end point counts.
                        float d = raw >= 0f && raw <= 1f ? MathF.Abs(lat) : Vector2.Distance(q, foot);
                        best = MathF.Min(best, d - wall);
                    }
                }
            return best;
        }

        /// <summary>True when a footprint of the given radius (m) at (x, z) keeps `margin` (m) from every track part's barrier line.</summary>
        public bool IsClear(float x, float z, float radius, float margin = 0f) => Margin(x, z) >= radius + margin;
    }
}
