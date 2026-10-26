using System;
using System.Collections.Generic;
using System.Numerics;
using Apex.Tracks;

namespace Apex.Environment
{
    /// <summary>
    /// Roadside dressing behind the barriers: the pavement strip (kerbstone to 4.3 m behind the
    /// barrier face), street lamps every 45 m (70 m at low detail) and spots for signs. Every piece is
    /// checked against the whole circuit's keep-out envelope (<see cref="TrackClearance"/>), not only
    /// against the part it was laid out from: where a hairpin, a parallel stretch or the pit lane runs
    /// behind the barrier, the lamp or pavement piece is left out instead of standing on the track.
    /// </summary>
    public sealed class StreetFurnitureLayout
    {
        public struct Lamp
        {
            public Vector3 position;
            /// <summary>Horizontal direction the arm points to (towards the own track part).</summary>
            public Vector3 towards;
            public float distance;
            public int side;
        }

        public struct PavementQuad
        {
            public Vector3 a, b, c, d;
            public Vector3 outward;
        }

        /// <summary>Lamp post footprint radius and the free space kept to any barrier line (m).</summary>
        public const float LampRadius = 0.25f, LampMargin = 0.6f;
        public const float PavementFrom = 0.65f, PavementTo = 4.3f, LampOffset = 3.6f;
        public const float SurfaceDy = -0.1f;

        public readonly List<Lamp> lamps = new List<Lamp>();
        public readonly List<PavementQuad> pavements = new List<PavementQuad>();
        /// <summary>Pieces the clearance check removed (they would have stood on another part of the track).</summary>
        public int rejectedLamps, rejectedPavements;

        readonly TrackGeometry g;
        readonly TrackClearance clearance;
        readonly float[] wallLeft, wallRight;

        public StreetFurnitureLayout(TrackGeometry geometry, float[] wallLeft, float[] wallRight, TrackClearance clearance, Func<float, float, bool> isWater, int quality)
        {
            g = geometry;
            this.clearance = clearance;
            this.wallLeft = wallLeft;
            this.wallRight = wallRight;
            int n = g.Count;
            int step = Math.Max(1, (int)MathF.Round(8f / g.Spacing));
            float lampSpacing = quality == 0 ? 70f : 45f;
            foreach (int side in new[] { -1, 1 })
            {
                float[] wall = side < 0 ? wallLeft : wallRight;
                float nextLamp = side < 0 ? 0f : lampSpacing * 0.5f;
                for (int i = 0; i < n; i += step)
                {
                    int j = (i + step) % n;
                    var a = Point(i, side * (wall[i] + PavementFrom));
                    var b = Point(i, side * (wall[i] + PavementTo));
                    var c = Point(j, side * (wall[j] + PavementTo));
                    var d = Point(j, side * (wall[j] + PavementFrom));
                    if (!isWater(b.X, b.Z))
                    {
                        // All corners and the middle must lie outside every track part.
                        var mid = (a + b + c + d) * 0.25f;
                        bool clear = true;
                        foreach (var p in new[] { a, b, c, d, mid })
                            if (!clearance.IsClear(p.X, p.Z, 0f, 0.05f)) { clear = false; break; }
                        if (clear) pavements.Add(new PavementQuad { a = a, b = b, c = c, d = d, outward = Vector3.Normalize(b - a) });
                        else rejectedPavements++;
                    }
                    if (g.Distances[i] >= nextLamp)
                    {
                        nextLamp += lampSpacing;
                        var basePt = Point(i, side * (wall[i] + LampOffset));
                        if (isWater(basePt.X, basePt.Z)) continue;
                        if (clearance.IsClear(basePt.X, basePt.Z, LampRadius, LampMargin))
                            lamps.Add(new Lamp { position = basePt, towards = g.Rights[i] * -side, distance = g.Distances[i], side = side });
                        else rejectedLamps++;
                    }
                }
            }
        }

        Vector3 Point(int i, float lateral) => g.Positions[i] + g.Rights[i] * lateral + new Vector3(0f, SurfaceDy, 0f);

        /// <summary>
        /// A free spot for a sign or flag pole behind the barrier at sample i: `offset` m behind the barrier
        /// on the preferred side, else on the other side; false if neither is clear of the whole circuit.
        /// </summary>
        public bool SignSpot(int i, int preferredSide, float offset, float radius, out Vector3 position, out int side)
        {
            foreach (int s in new[] { preferredSide, -preferredSide })
            {
                float wall = s < 0 ? wallLeft[i] : wallRight[i];
                var p = Point(i, s * (wall + offset));
                if (clearance.IsClear(p.X, p.Z, radius, 0.3f)) { position = p; side = s; return true; }
            }
            position = default;
            side = preferredSide;
            return false;
        }
    }
}
