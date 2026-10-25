using System;
using System.Collections.Generic;
using System.Numerics;
using Apex.Core;

namespace Apex.Tracks
{
    /// <summary>
    /// Baked centreline of a closed circuit. Control points are interpolated with a
    /// closed centripetal Catmull-Rom spline (same as tools/preview_track.py) and
    /// resampled at uniform arc-length spacing. Unity space: x east, y up, z north,
    /// so "right" = Cross(up, forward) = (fwd.z, 0, -fwd.x).
    /// </summary>
    public sealed class TrackGeometry
    {
        const int SamplesPerSegment = 64;

        public float Spacing { get; private set; }
        public float Length { get; private set; }
        public Vector3[] Positions { get; private set; }
        public Vector3[] Tangents { get; private set; }
        /// <summary>Horizontal unit vector to the right of the driving direction.</summary>
        public Vector3[] Rights { get; private set; }
        public float[] Widths { get; private set; }
        /// <summary>Signed curvature (1/m), positive = turning right (clockwise from above).</summary>
        public float[] Curvatures { get; private set; }
        public float[] Distances { get; private set; }
        public int Count => Positions.Length;

        public static TrackGeometry FromDefinition(CircuitDefinition def, float spacing = 2f)
        {
            var g = new TrackGeometry();
            g.Build(def.points, spacing);
            return g;
        }

        void Build(List<(float east, float north, float elevation, float width)> pts, float spacing)
        {
            int n = pts.Count;
            var ctrl = new Vector4[n];
            for (int i = 0; i < n; i++) ctrl[i] = new Vector4(pts[i].east, pts[i].elevation, pts[i].north, pts[i].width);
            var dense = new List<Vector4>(n * SamplesPerSegment);
            for (int i = 0; i < n; i++)
            {
                var p0 = ctrl[(i - 1 + n) % n];
                var p1 = ctrl[i];
                var p2 = ctrl[(i + 1) % n];
                var p3 = ctrl[(i + 2) % n];
                for (int s = 0; s < SamplesPerSegment; s++)
                    dense.Add(CatmullRom(p0, p1, p2, p3, (float)s / SamplesPerSegment));
            }
            int dn = dense.Count;
            var cum = new double[dn + 1];
            for (int i = 0; i < dn; i++)
            {
                var a = dense[i];
                var b = dense[(i + 1) % dn];
                cum[i + 1] = cum[i] + new Vector3(b.X - a.X, b.Y - a.Y, b.Z - a.Z).Length();
            }
            double total = cum[dn];
            int count = Math.Max(16, (int)Math.Round(total / spacing));
            Spacing = (float)(total / count);
            Length = (float)total;
            Positions = new Vector3[count];
            Widths = new float[count];
            Distances = new float[count];
            int j = 0;
            for (int k = 0; k < count; k++)
            {
                double d = k * (total / count);
                while (cum[j + 1] < d) j++;
                float t = (float)((d - cum[j]) / Math.Max(cum[j + 1] - cum[j], 1e-9));
                var v = Vector4.Lerp(dense[j], dense[(j + 1) % dn], t);
                Positions[k] = new Vector3(v.X, v.Y, v.Z);
                Widths[k] = v.W;
                Distances[k] = (float)d;
            }
            Tangents = new Vector3[count];
            Rights = new Vector3[count];
            Curvatures = new float[count];
            var bearings = new float[count];
            for (int k = 0; k < count; k++)
            {
                var tng = Vector3.Normalize(Positions[(k + 1) % count] - Positions[(k - 1 + count) % count]);
                Tangents[k] = tng;
                Rights[k] = Vector3.Normalize(new Vector3(tng.Z, 0f, -tng.X));
                bearings[k] = MathF.Atan2(tng.X, tng.Z);
            }
            for (int k = 0; k < count; k++)
            {
                float db = ApexMath.WrapAngle(bearings[(k + 2) % count] - bearings[(k - 2 + count) % count]);
                Curvatures[k] = db / (4f * Spacing);
            }
        }

        static Vector4 CatmullRom(Vector4 p0, Vector4 p1, Vector4 p2, Vector4 p3, float u)
        {
            // Centripetal parameterisation on the horizontal plane avoids cusps and loops.
            float t0 = 0f;
            float t1 = t0 + MathF.Sqrt(MathF.Max(Flat(p1 - p0), 1e-4f));
            float t2 = t1 + MathF.Sqrt(MathF.Max(Flat(p2 - p1), 1e-4f));
            float t3 = t2 + MathF.Sqrt(MathF.Max(Flat(p3 - p2), 1e-4f));
            float t = t1 + (t2 - t1) * u;
            var a1 = p0 * ((t1 - t) / (t1 - t0)) + p1 * ((t - t0) / (t1 - t0));
            var a2 = p1 * ((t2 - t) / (t2 - t1)) + p2 * ((t - t1) / (t2 - t1));
            var a3 = p2 * ((t3 - t) / (t3 - t2)) + p3 * ((t - t2) / (t3 - t2));
            var b1 = a1 * ((t2 - t) / (t2 - t0)) + a2 * ((t - t0) / (t2 - t0));
            var b2 = a2 * ((t3 - t) / (t3 - t1)) + a3 * ((t - t1) / (t3 - t1));
            return b1 * ((t2 - t) / (t2 - t1)) + b2 * ((t - t1) / (t2 - t1));
        }

        static float Flat(Vector4 d) => MathF.Sqrt(d.X * d.X + d.Z * d.Z);

        public float Wrap(float d) => ApexMath.PosMod(d, Length);

        public int IndexAt(float d) => (int)(Wrap(d) / Spacing) % Count;

        public Vector3 PositionAt(float d)
        {
            float x = Wrap(d) / Spacing;
            int i = (int)x % Count;
            return Vector3.Lerp(Positions[i], Positions[(i + 1) % Count], x - MathF.Floor(x));
        }

        public Vector3 TangentAt(float d)
        {
            float x = Wrap(d) / Spacing;
            int i = (int)x % Count;
            return Vector3.Normalize(Vector3.Lerp(Tangents[i], Tangents[(i + 1) % Count], x - MathF.Floor(x)));
        }

        public Vector3 RightAt(float d)
        {
            var t = TangentAt(d);
            return Vector3.Normalize(new Vector3(t.Z, 0f, -t.X));
        }

        public float CurvatureAt(float d) => Curvatures[IndexAt(d)];
        public float WidthAt(float d) => Widths[IndexAt(d)];

        /// <summary>Projection of a world point onto the centreline.</summary>
        public struct Projection
        {
            public float distance;
            /// <summary>Signed lateral offset, positive = right of the centreline.</summary>
            public float lateral;
            public int index;
        }

        /// <summary>
        /// Projects a world position onto the centreline. With <paramref name="hint"/> >= 0
        /// only a window around it is searched (O(1) per-frame tracking).
        /// </summary>
        public Projection Project(Vector3 world, int hint = -1, int window = 40)
        {
            int bestI = 0;
            float bestD2 = float.MaxValue;
            if (hint < 0)
            {
                for (int i = 0; i < Count; i++)
                {
                    float d2 = FlatDist2(Positions[i], world);
                    if (d2 < bestD2) { bestD2 = d2; bestI = i; }
                }
            }
            else
            {
                for (int o = -window; o <= window; o++)
                {
                    int i = ((hint + o) % Count + Count) % Count;
                    float d2 = FlatDist2(Positions[i], world);
                    if (d2 < bestD2) { bestD2 = d2; bestI = i; }
                }
            }
            var best = new Projection { distance = Distances[bestI], lateral = 0f, index = bestI };
            float bestSeg = float.MaxValue;
            for (int seg = bestI - 1; seg <= bestI; seg++)
            {
                int a = (seg % Count + Count) % Count;
                int b = (a + 1) % Count;
                var pa = new Vector2(Positions[a].X, Positions[a].Z);
                var pb = new Vector2(Positions[b].X, Positions[b].Z);
                var p = new Vector2(world.X, world.Z);
                var ab = pb - pa;
                float t = ApexMath.Clamp01(Vector2.Dot(p - pa, ab) / MathF.Max(ab.LengthSquared(), 1e-9f));
                var c = pa + ab * t;
                float d2 = Vector2.DistanceSquared(p, c);
                if (d2 < bestSeg)
                {
                    bestSeg = d2;
                    var r = new Vector2(Rights[a].X, Rights[a].Z);
                    best.distance = Wrap(Distances[a] + t * Spacing);
                    best.lateral = Vector2.Dot(p - c, r);
                }
            }
            return best;
        }

        /// <summary>Position, forward and right for placing objects on the track.</summary>
        public void FrameAt(float d, out Vector3 position, out Vector3 forward, out Vector3 right, float lateral = 0f)
        {
            forward = TangentAt(d);
            right = Vector3.Normalize(new Vector3(forward.Z, 0f, -forward.X));
            position = PositionAt(d) + right * lateral;
        }

        public static float FlatDist2(Vector3 a, Vector3 b)
        {
            float dx = a.X - b.X, dz = a.Z - b.Z;
            return dx * dx + dz * dz;
        }

        /// <summary>Corner detected from curvature runs.</summary>
        public struct Corner
        {
            public int index;
            public bool right;
            public float angleDeg;
            public float minRadius;
        }

        /// <summary>Detects corners as same-sign curvature runs above a threshold
        /// (mirrors tools/circuit_report.py).</summary>
        public List<Corner> DetectCorners(float minAngleDeg = 12f, float curvatureThreshold = 1f / 450f)
        {
            var result = new List<Corner>();
            int start = 0;
            while (start < Count && MathF.Abs(Curvatures[start]) >= curvatureThreshold) start++;
            int visited = 0;
            int i = start;
            while (visited < Count)
            {
                float k = Curvatures[i % Count];
                if (MathF.Abs(k) >= curvatureThreshold)
                {
                    int sign = MathF.Sign(k);
                    int j = i;
                    float angle = 0f, kmax = 0f;
                    while (MathF.Abs(Curvatures[j % Count]) >= curvatureThreshold && MathF.Sign(Curvatures[j % Count]) == sign)
                    {
                        angle += Curvatures[j % Count] * Spacing;
                        kmax = MathF.Max(kmax, MathF.Abs(Curvatures[j % Count]));
                        j++;
                    }
                    if (MathF.Abs(angle) * ApexMath.Rad2Deg >= minAngleDeg)
                        result.Add(new Corner { index = (i + j) / 2 % Count, right = sign > 0, angleDeg = MathF.Abs(angle) * ApexMath.Rad2Deg, minRadius = 1f / kmax });
                    visited += j - i;
                    i = j;
                }
                else
                {
                    i++;
                    visited++;
                }
            }
            return result;
        }
    }
}
