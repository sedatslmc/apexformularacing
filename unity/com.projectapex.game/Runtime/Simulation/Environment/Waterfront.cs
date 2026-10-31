using System;
using System.Collections.Generic;
using System.Numerics;
using Apex.Tracks;

namespace Apex.Environment
{
    /// <summary>
    /// The built edge of the Bosphorus along the real (OSM) coastline: a stone quay wall on every coast edge in the
    /// near area, and on the European side a paved promenade behind it with an iron railing, mooring bollards and
    /// lamps, as along the Beşiktaş and Ortaköy waterfront. The promenade gives way to the circuit, buildings and
    /// landmark terraces (`blocked`). Quay tops follow the ground behind the wall, at least <see cref="MinTop"/>
    /// above the water. Visual only; the water plane, terrain (water below -4 m) and this edge share the coastline.
    /// </summary>
    public sealed class Waterfront
    {
        public struct Segment
        {
            public Vector2 a, b;
            /// <summary>Unit vector from the waterline towards the land.</summary>
            public Vector2 inland;
            /// <summary>Height of the quay top (and the promenade) above the water datum.</summary>
            public float top;
            public bool promenade;
            public bool europe;
        }

        public const float PromenadeWidth = 8f, MinTop = 1.2f, MaxTop = 6f, Piece = 6f;
        public const float BollardSpacing = 18f, LampSpacing = 32f;

        public readonly List<Segment> segments = new List<Segment>();
        public readonly List<Vector3> bollards = new List<Vector3>();
        /// <summary>Lamp foot positions and the direction they face (towards the water).</summary>
        public readonly List<(Vector3 foot, Vector2 seaward)> lamps = new List<(Vector3, Vector2)>();
        public float promenadeLength, quayLength;

        Waterfront() { }

        /// <summary>An empty container (the builder sorts segments into chunks).</summary>
        public static Waterfront Empty() => new Waterfront();

        public Waterfront(TerrainField field, TrackClearance clearance, Func<float, float, bool> blocked)
        {
            if (!field.land.HasLand) return;
            var edges = new List<(float ax, float az, float bx, float bz, string mass)>();
            field.land.CoastEdges(field.MinX, field.MaxX, field.MinZ, field.MaxZ, edges);
            float nextBollard = 0f, nextLamp = LampSpacing * 0.5f;
            foreach (var e in edges)
            {
                var a0 = new Vector2(e.ax, e.az);
                var b0 = new Vector2(e.bx, e.bz);
                float len = Vector2.Distance(a0, b0);
                if (len < 0.3f) continue;
                int n = Math.Max(1, (int)MathF.Ceiling(len / Piece));
                var dir = (b0 - a0) / len;
                var normal = new Vector2(dir.Y, -dir.X);
                for (int k = 0; k < n; k++)
                {
                    var a = Vector2.Lerp(a0, b0, k / (float)n);
                    var b = Vector2.Lerp(a0, b0, (k + 1) / (float)n);
                    var mid = (a + b) * 0.5f;
                    if (!field.InNearArea(mid.X, mid.Y)) continue;
                    // Inland side: where the land mass is.
                    var inland = field.land.IsLand(mid.X + normal.X * 1.5f, mid.Y + normal.Y * 1.5f) ? normal : -normal;
                    if (!field.land.IsLand(mid.X + inland.X * 1.5f, mid.Y + inland.Y * 1.5f)) continue;   // spit thinner than 3 m
                    var behind = mid + inland * 4f;
                    float top = Math.Clamp(field.Height(behind.X, behind.Y), MinTop, MaxTop);
                    bool europe = e.mass == "europe";
                    bool prom = europe;
                    if (prom)
                        foreach (float d in new[] { 1f, PromenadeWidth * 0.5f, PromenadeWidth })
                        {
                            var q = mid + inland * d;
                            if (!field.land.IsLand(q.X, q.Y) || clearance.Margin(q.X, q.Y) < 2f || (blocked != null && blocked(q.X, q.Y))) { prom = false; break; }
                        }
                    float seg = Vector2.Distance(a, b);
                    segments.Add(new Segment { a = a, b = b, inland = inland, top = top, promenade = prom, europe = europe });
                    quayLength += seg;
                    if (!prom) continue;
                    promenadeLength += seg;
                    nextBollard -= seg;
                    nextLamp -= seg;
                    if (nextBollard <= 0f)
                    {
                        var p = mid + inland * 0.45f;
                        bollards.Add(new Vector3(p.X, top, p.Y));
                        nextBollard += BollardSpacing;
                    }
                    if (nextLamp <= 0f)
                    {
                        var p = mid + inland * (PromenadeWidth - 1f);
                        lamps.Add((new Vector3(p.X, top, p.Y), -inland));
                        nextLamp += LampSpacing;
                    }
                }
            }
        }
    }
}
