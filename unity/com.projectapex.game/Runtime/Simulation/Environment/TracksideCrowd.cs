using System;
using System.Collections.Generic;
using System.Numerics;
using Apex.Tracks;

namespace Apex.Environment
{
    /// <summary>
    /// Standing spectators behind the barriers of a street section (the Ortaköy reference section: Muallim Naci Cd.,
    /// the square and the Palanga climb): rows from 1.2 m to 8 m behind the barrier line, thinning out towards the
    /// back, facing the track. Spots are on land, outside buildings, clear of the café tables and street furniture,
    /// outside grandstands and event buildings (reserved areas), and outside every part of the racing envelope (between two parallel carriageways there is no room, the median
    /// lies inside the envelope). Deterministic.
    /// </summary>
    public sealed class TracksideCrowd
    {
        public struct Fan
        {
            public Vector3 position;
            /// <summary>Heading (rad) the fan faces: towards the nearest point of the track.</summary>
            public float yaw;
            /// <summary>Per-fan random value in [-1, 1] (shirt colour, height, hand flag).</summary>
            public float rnd;
        }

        public const float MinBehind = 1.2f, MaxBehind = 8f, Spacing = 0.75f;
        public readonly List<Fan> fans = new List<Fan>();

        public TracksideCrowd(TrackGeometry g, TrackClearance clearance, TerrainField field, OsmCity city, IReadOnlyList<CityLife.Placed> furniture,
            Vector2 centre, float radius, int quality, IReadOnlyList<CityLayout.Reserved> reserved = null)
        {
            if (quality <= 0) return;
            float fill = quality == 1 ? 0.5f : quality == 2 ? 0.85f : 1f;
            var taken = new HashSet<long>();
            var blocked = new HashSet<long>();
            const float cell = 0.7f;
            long Key(float x, float z) => ((long)MathF.Floor(x / cell) << 32) ^ (uint)(int)MathF.Floor(z / cell);
            if (furniture != null)
                foreach (var p in furniture)
                    for (float dx = -1.4f; dx <= 1.4f; dx += cell)
                        for (float dz = -1.4f; dz <= 1.4f; dz += cell)
                            blocked.Add(Key(p.position.X + dx, p.position.Z + dz));
            int n = g.Count;
            float along = Spacing / MathF.Max(g.Spacing, 0.1f);
            int stride = Math.Max(1, (int)MathF.Round(along));
            for (int i = 0; i < n; i += stride)
            {
                var c = g.Positions[i];
                if (Vector2.Distance(new Vector2(c.X, c.Z), centre) > radius) continue;
                var right = g.Rights[i];
                var fwd = new Vector3(right.Z, 0f, -right.X);
                foreach (int side in new[] { -1, 1 })
                {
                    float edge = g.Widths[i] * 0.5f;
                    for (float off = edge; off < edge + 3f + MaxBehind + 4f; off += Spacing)
                    {
                        int h = i * 7919 + side * 104729 + (int)(off * 100f);
                        float jx = LandModel.Hash(h, 1) * 0.2f, jz = LandModel.Hash(h, 2) * 0.2f;
                        var p = c + right * (side * off) + fwd * jz + right * jx;
                        float m = clearance.Margin(p.X, p.Z);
                        if (m < MinBehind || m > MaxBehind) continue;
                        // Fewer people towards the back rows.
                        float keep = fill * (1f - 0.6f * (m - MinBehind) / (MaxBehind - MinBehind));
                        if (LandModel.Hash(h, 3) * 0.5f + 0.5f > keep) continue;
                        if (field.IsWater(p.X, p.Z)) continue;
                        // Grandstands and event buildings have their own people (or none).
                        bool free = true;
                        if (reserved != null)
                            foreach (var r in reserved)
                                if ((p.X - r.x) * (p.X - r.x) + (p.Z - r.z) * (p.Z - r.z) < r.radius * r.radius) { free = false; break; }
                        if (!free) continue;
                        if (city != null && city.InsideBuilding(p.X, p.Z, 0.4f)) continue;
                        long k = Key(p.X, p.Z);
                        if (blocked.Contains(k) || !taken.Add(k)) continue;
                        var toTrack = new Vector2(c.X - p.X, c.Z - p.Z);
                        fans.Add(new Fan
                        {
                            position = new Vector3(p.X, field.Height(p.X, p.Z) + StreetNetwork.Lift + 0.02f, p.Z),
                            yaw = MathF.Atan2(toTrack.X, toTrack.Y),
                            rnd = LandModel.Hash(h, 4),
                        });
                    }
                }
            }
        }
    }
}
