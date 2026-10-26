using System;
using System.Collections.Generic;
using System.Numerics;
using Apex.Tracks;

namespace Apex.Environment
{
    /// <summary>
    /// The city's own streets from OSM (<see cref="CityData"/>), draped on the terrain as visual meshes
    /// (no colliders; not drivable): carriageways with centre dashes on the bigger streets, raised
    /// pavements with a kerb face on both sides of the roads, paved footways and pedestrian zones, and
    /// stairs with real steps. Every piece stays outside the racing envelope (<see cref="TrackClearance"/>):
    /// side streets end at the circuit's barriers, as at a real street race. Meshes are split into chunks.
    /// </summary>
    public sealed class StreetNetwork
    {
        public const int Road = 0, Sidewalk = 1, Kerb = 2, Marking = 3, Footway = 4, Steps = 5, Submeshes = 6;
        public const float ChunkSize = 192f;
        public const float SidewalkWidth = 2.2f, KerbHeight = 0.15f;
        /// <summary>Lift above the terrain (the 12 m terrain grid lies between the sampled points).</summary>
        public const float Lift = 0.22f;
        /// <summary>Distance every street vertex keeps from the racing envelope.</summary>
        public const float TrackGap = 1.5f;
        const float Step = 4f;

        public readonly Dictionary<(int, int), SurfaceMesh> chunks = new Dictionary<(int, int), SurfaceMesh>();
        public float roadLength, footLength;
        public int steps;

        readonly TerrainField field;
        readonly TrackClearance clearance;

        public StreetNetwork(CityData data, TerrainField field, TrackClearance clearance, int quality)
        {
            this.field = field;
            this.clearance = clearance;
            foreach (var s in data.streets)
            {
                if (s.bridge || s.highway == "corridor") continue;
                if (quality == 0 && (s.highway == "footway" || s.highway == "path" || s.highway == "service")) continue;
                Build(s);
            }
        }

        SurfaceMesh Chunk(Vector2 p)
        {
            var key = ((int)MathF.Floor(p.X / ChunkSize), (int)MathF.Floor(p.Y / ChunkSize));
            if (!chunks.TryGetValue(key, out var m)) chunks[key] = m = new SurfaceMesh($"Streets_{key.Item1}_{key.Item2}", Submeshes, null);
            return m;
        }

        static void Quad(SurfaceMesh m, int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 facing)
        {
            int s = m.vertices.Count;
            m.vertices.Add(a); m.vertices.Add(b); m.vertices.Add(c); m.vertices.Add(d);
            m.uvs.Add(ua); m.uvs.Add(ub); m.uvs.Add(uc); m.uvs.Add(ud);
            // Unity: front face where cross(b - a, c - a) points to the viewer.
            bool flip = Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0f;
            var t = m.triangles[sub];
            if (!flip) { t.Add(s); t.Add(s + 1); t.Add(s + 2); t.Add(s); t.Add(s + 2); t.Add(s + 3); }
            else { t.Add(s); t.Add(s + 2); t.Add(s + 1); t.Add(s); t.Add(s + 3); t.Add(s + 2); }
        }

        /// <summary>Resampled centreline with per-point clearance from the track.</summary>
        List<Vector2> Resample(List<Vector2> pts, float step)
        {
            var r = new List<Vector2> { pts[0] };
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                float len = Vector2.Distance(a, b);
                int n = Math.Max(1, (int)MathF.Ceiling(len / step));
                for (int k = 1; k <= n; k++) r.Add(Vector2.Lerp(a, b, k / (float)n));
            }
            return r;
        }

        float H(Vector2 p) => field.Height(p.X, p.Y);

        void Build(CityData.Street s)
        {
            bool road = CityData.IsRoad(s.highway);
            bool stair = s.highway == "steps";
            float width = s.width > 0f ? MathF.Min(s.width, 24f) : CityData.DefaultWidth(s.highway);
            if (!road && !stair) width = MathF.Max(width, 2f);
            var pts = Resample(s.points, stair ? 0.6f : Step);
            float half = width * 0.5f + (road && s.highway != "service" ? SidewalkWidth : 0f);
            // Usable runs: every cross-section point clear of the racing envelope and on land.
            var ok = new bool[pts.Count];
            for (int i = 0; i < pts.Count; i++)
            {
                var dir = Dir(pts, i);
                var right = new Vector2(dir.Y, -dir.X);
                var p = pts[i];
                ok[i] = field.InNearArea(p.X, p.Y) && !field.IsWater(p.X, p.Y)
                    && clearance.Margin(p.X, p.Y) > TrackGap + half
                    && clearance.Margin(p.X + right.X * half, p.Y + right.Y * half) > TrackGap
                    && clearance.Margin(p.X - right.X * half, p.Y - right.Y * half) > TrackGap;
            }
            bool markings = road && (s.highway.StartsWith("primary") || s.highway.StartsWith("secondary") || s.highway.StartsWith("tertiary"));
            float along = 0f;
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                float seg = Vector2.Distance(pts[i], pts[i + 1]);
                if (!ok[i] || !ok[i + 1] || seg < 1e-3f) { along += seg; continue; }
                var m = Chunk(pts[i]);
                if (stair) StairStep(m, pts[i], pts[i + 1], width, along);
                else if (road) RoadSegment(m, pts, i, width, along, markings, s.highway != "service");
                else Ribbon(m, Footway, pts, i, width * 0.5f, Lift + 0.03f, along);
                if (road) roadLength += seg; else footLength += seg;
                along += seg;
            }
        }

        static Vector2 Dir(List<Vector2> pts, int i)
        {
            var a = pts[Math.Max(0, i - 1)]; var b = pts[Math.Min(pts.Count - 1, i + 1)];
            var d = b - a;
            return d.LengthSquared() > 1e-8f ? Vector2.Normalize(d) : new Vector2(0f, 1f);
        }

        Vector3 At(Vector2 p, Vector2 right, float lateral, float lift)
        {
            var q = p + right * lateral;
            return new Vector3(q.X, H(q) + lift, q.Y);
        }

        void Ribbon(SurfaceMesh m, int sub, List<Vector2> pts, int i, float halfWidth, float lift, float along, float uvScale = 0.25f)
        {
            var ri = Right(pts, i); var rj = Right(pts, i + 1);
            float seg = Vector2.Distance(pts[i], pts[i + 1]);
            Quad(m, sub, At(pts[i], ri, -halfWidth, lift), At(pts[i + 1], rj, -halfWidth, lift), At(pts[i + 1], rj, halfWidth, lift), At(pts[i], ri, halfWidth, lift),
                new Vector2(-halfWidth * uvScale, along * uvScale), new Vector2(-halfWidth * uvScale, (along + seg) * uvScale),
                new Vector2(halfWidth * uvScale, (along + seg) * uvScale), new Vector2(halfWidth * uvScale, along * uvScale), Vector3.UnitY);
        }

        static Vector2 Right(List<Vector2> pts, int i)
        {
            var d = Dir(pts, i);
            return new Vector2(d.Y, -d.X);
        }

        void RoadSegment(SurfaceMesh m, List<Vector2> pts, int i, float width, float along, bool markings, bool sidewalks)
        {
            float h = width * 0.5f;
            // Metric UVs like the circuit (10 m per unit), so the scanned asphalt keeps its scale.
            Ribbon(m, Road, pts, i, h, Lift, along, 0.1f);
            if (markings && ((int)(along / 3f)) % 3 != 2)
                Ribbon(m, Marking, pts, i, 0.06f, Lift + 0.01f, along);
            if (!sidewalks) return;
            var ri = Right(pts, i); var rj = Right(pts, i + 1);
            float seg = Vector2.Distance(pts[i], pts[i + 1]);
            foreach (float side in new[] { -1f, 1f })
            {
                float a = h * side, b = (h + SidewalkWidth) * side;
                float top = Lift + KerbHeight;
                var p0 = At(pts[i], ri, a, top); var p1 = At(pts[i + 1], rj, a, top);
                var p2 = At(pts[i + 1], rj, b, top); var p3 = At(pts[i], ri, b, top);
                Quad(m, Sidewalk, p0, p1, p2, p3, new Vector2(p0.X, p0.Z) / 3f, new Vector2(p1.X, p1.Z) / 3f, new Vector2(p2.X, p2.Z) / 3f, new Vector2(p3.X, p3.Z) / 3f, Vector3.UnitY);
                // Kerb face towards the carriageway, and the outer edge down to the ground.
                var k0 = At(pts[i], ri, a, Lift - 0.05f); var k1 = At(pts[i + 1], rj, a, Lift - 0.05f);
                var towardsRoad = new Vector3(-ri.X * side, 0f, -ri.Y * side);
                Quad(m, Kerb, k0, k1, p1, p0, new Vector2(along, 0f), new Vector2(along + seg, 0f), new Vector2(along + seg, 0.2f), new Vector2(along, 0.2f), towardsRoad);
                var o0 = At(pts[i], ri, b, -0.3f); var o1 = At(pts[i + 1], rj, b, -0.3f);
                Quad(m, Kerb, o0, o1, p2, p3, new Vector2(along, 0f), new Vector2(along + seg, 0f), new Vector2(along + seg, 0.5f), new Vector2(along, 0.5f), -towardsRoad);
            }
        }

        /// <summary>One flight piece of a stair: tread at the upper height and a riser of the height difference (17 cm steps).</summary>
        void StairStep(SurfaceMesh m, Vector2 a, Vector2 b, float width, float along)
        {
            float ha = H(a), hb = H(b);
            var dir = b - a;
            float len = dir.Length();
            dir /= len;
            var right = new Vector2(dir.Y, -dir.X) * (width * 0.5f);
            // Quantise heights to 17 cm risers so the flight reads as steps, not a ramp.
            float qa = MathF.Round(ha / 0.17f) * 0.17f, qb = MathF.Round(hb / 0.17f) * 0.17f;
            float lift = Lift + 0.05f;
            Vector3 P(Vector2 p, float y) => new Vector3(p.X, y + lift, p.Y);
            float tread = MathF.Max(qa, qb);
            bool up = qb >= qa;
            // Tread on the higher end's level from the riser to the far end.
            var r0 = up ? a : b;
            Quad(m, Steps, P(a - right, tread), P(b - right, tread), P(b + right, tread), P(a + right, tread),
                new Vector2(-0.5f, along), new Vector2(-0.5f, along + len), new Vector2(0.5f, along + len), new Vector2(0.5f, along), Vector3.UnitY);
            if (MathF.Abs(qb - qa) < 0.01f) return;
            float low = MathF.Min(qa, qb);
            var facing = up ? new Vector3(-dir.X, 0f, -dir.Y) : new Vector3(dir.X, 0f, dir.Y);
            Quad(m, Steps, P(r0 - right, low), P(r0 + right, low), P(r0 + right, tread), P(r0 - right, tread),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0.17f), new Vector2(0f, 0.17f), facing);
            steps++;
        }

        public int TriangleCount
        {
            get { int n = 0; foreach (var c in chunks.Values) n += c.TriangleCount; return n; }
        }
    }
}
