using System;
using System.Collections.Generic;
using UnityEngine;

namespace Apex.Game
{
    /// <summary>Cross-section of a lofted body: superellipse centred at (x, y) at station z.</summary>
    public struct LoftSection
    {
        public float z, x, y, halfWidth, halfHeight, exponent;

        public LoftSection(float z, float y, float halfWidth, float halfHeight, float exponent = 3f, float x = 0f)
        {
            this.z = z;
            this.x = x;
            this.y = y;
            this.halfWidth = halfWidth;
            this.halfHeight = halfHeight;
            this.exponent = exponent;
        }
    }

    /// <summary>
    /// Small procedural modelling kit for the placeholder vehicle: lofted
    /// superellipse bodies, cambered wing elements, tubes, lathed parts and
    /// extruded plates, accumulated into one mesh. Shared vertices give smooth
    /// shading on lofts/lathes; plates and wings use split faces for hard edges.
    /// </summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> tris = new List<int>();
        public Matrix4x4 matrix = Matrix4x4.identity;

        public int VertexCount => verts.Count;
        public int TriangleCount => tris.Count / 3;

        int V(Vector3 p, Vector2 uv)
        {
            verts.Add(matrix.MultiplyPoint3x4(p));
            uvs.Add(uv);
            return verts.Count - 1;
        }

        // Unity treats a triangle as front-facing when cross(b - a, c - a) points at the
        // viewer; the generators below are written in the opposite order, hence the swap.
        void Tri(int a, int b, int c, bool flip = false)
        {
            if (flip) { tris.Add(a); tris.Add(b); tris.Add(c); }
            else { tris.Add(a); tris.Add(c); tris.Add(b); }
        }

        void Quad(int a, int b, int c, int d, bool flip = false)
        {
            Tri(a, b, c, flip);
            Tri(a, c, d, flip);
        }

        static Vector2 Superellipse(float theta, float hw, float hh, float e)
        {
            float c = Mathf.Cos(theta), s = Mathf.Sin(theta);
            float p = 2f / e;
            return new Vector2(hw * Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), p), hh * Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), p));
        }

        /// <summary>
        /// Lofts superellipse sections along z (sections ordered front to back or back
        /// to front). arcStart/arcEnd (radians, 0 = right, π/2 = top) allow open shells
        /// such as a cockpit tub; doubleSided adds inner faces.
        /// </summary>
        public void Loft(IList<LoftSection> sections, int segments, bool capStart = true, bool capEnd = true,
            float arcStart = 0f, float arcEnd = Mathf.PI * 2f, bool doubleSided = false)
        {
            bool closed = Mathf.Abs(arcEnd - arcStart - Mathf.PI * 2f) < 1e-3f;
            int ring = closed ? segments : segments + 1;
            int start = verts.Count;
            bool reversed = sections.Count > 1 && sections[1].z > sections[0].z;
            for (int i = 0; i < sections.Count; i++)
            {
                var s = sections[i];
                for (int j = 0; j < ring; j++)
                {
                    float t = arcStart + (arcEnd - arcStart) * j / segments;
                    var p = Superellipse(t, s.halfWidth, s.halfHeight, s.exponent);
                    V(new Vector3(s.x + p.x, s.y + p.y, s.z), new Vector2((float)j / segments, (float)i / (sections.Count - 1)));
                }
            }
            for (int i = 0; i < sections.Count - 1; i++)
            {
                for (int j = 0; j < segments; j++)
                {
                    int j1 = closed ? (j + 1) % ring : j + 1;
                    int a = start + i * ring + j, b = start + i * ring + j1;
                    int c = start + (i + 1) * ring + j1, d = start + (i + 1) * ring + j;
                    Quad(a, b, c, d, reversed);
                    if (doubleSided) Quad(a, b, c, d, !reversed);
                }
            }
            if (closed && capStart) Cap(sections[0], segments, reversed);
            if (closed && capEnd) Cap(sections[sections.Count - 1], segments, !reversed);
        }

        void Cap(LoftSection s, int segments, bool flip)
        {
            int centre = V(new Vector3(s.x, s.y, s.z), new Vector2(0.5f, 0.5f));
            int first = verts.Count;
            for (int j = 0; j < segments; j++)
            {
                var p = Superellipse(Mathf.PI * 2f * j / segments, s.halfWidth, s.halfHeight, s.exponent);
                V(new Vector3(s.x + p.x, s.y + p.y, s.z), new Vector2(0.5f + p.x, 0.5f + p.y));
            }
            for (int j = 0; j < segments; j++)
                Tri(centre, first + (j + 1) % segments, first + j, flip);
        }

        /// <summary>
        /// Wing element spanning x from -span/2 to +span/2. Chord runs along z with the
        /// leading edge forward (+z). Negative camber = downforce (inverted airfoil).
        /// aoaDeg rotates the trailing edge up for positive values.
        /// </summary>
        public void Airfoil(Vector3 centre, float span, float chord, float thickness, float camber, float aoaDeg, int chordSegments = 8, float xOffset = 0f)
        {
            var upper = new Vector2[chordSegments + 1];
            var lower = new Vector2[chordSegments + 1];
            float rad = aoaDeg * Mathf.Deg2Rad;
            float ca = Mathf.Cos(rad), sa = Mathf.Sin(rad);
            for (int i = 0; i <= chordSegments; i++)
            {
                // Cosine spacing for a rounder leading edge.
                float x = 0.5f * (1f - Mathf.Cos(Mathf.PI * i / chordSegments));
                float yt = 5f * thickness * (0.2969f * Mathf.Sqrt(x) - 0.126f * x - 0.3516f * x * x + 0.2843f * x * x * x - 0.1036f * x * x * x * x);
                float yc = camber * 4f * x * (1f - x);
                // Chord coordinate: leading edge at +chord/2 (front).
                float zc = chord * (0.5f - x);
                Vector2 Rot(float z, float y) => new Vector2(z * ca + y * sa, -z * sa + y * ca);
                upper[i] = Rot(zc, (yc + yt) * chord);
                lower[i] = Rot(zc, (yc - yt) * chord);
            }
            float x0 = centre.x - span * 0.5f + xOffset, x1 = centre.x + span * 0.5f + xOffset;
            void Surface(Vector2[] pts, bool flip)
            {
                int b = verts.Count;
                for (int i = 0; i <= chordSegments; i++)
                {
                    V(new Vector3(x0, centre.y + pts[i].y, centre.z + pts[i].x), new Vector2(0f, (float)i / chordSegments));
                    V(new Vector3(x1, centre.y + pts[i].y, centre.z + pts[i].x), new Vector2(1f, (float)i / chordSegments));
                }
                for (int i = 0; i < chordSegments; i++)
                    Quad(b + i * 2, b + i * 2 + 1, b + i * 2 + 3, b + i * 2 + 2, flip);
            }
            Surface(upper, true);
            Surface(lower, false);
            // Tip faces.
            foreach (float x in new[] { x0, x1 })
            {
                int b = verts.Count;
                for (int i = 0; i <= chordSegments; i++)
                {
                    V(new Vector3(x, centre.y + upper[i].y, centre.z + upper[i].x), Vector2.zero);
                    V(new Vector3(x, centre.y + lower[i].y, centre.z + lower[i].x), Vector2.one);
                }
                for (int i = 0; i < chordSegments; i++)
                    Quad(b + i * 2, b + i * 2 + 1, b + i * 2 + 3, b + i * 2 + 2, x == x1);
            }
        }

        /// <summary>Cylinder from a to b.</summary>
        public void Tube(Vector3 a, Vector3 b, float radius, int sides = 6, bool caps = true)
        {
            TubePath(new[] { a, b }, radius, sides, caps);
        }

        /// <summary>Tube along a polyline (parallel-transported frames).</summary>
        public void TubePath(IList<Vector3> path, float radius, int sides = 8, bool caps = true)
        {
            int start = verts.Count;
            Vector3 dir0 = (path[1] - path[0]).normalized;
            Vector3 normal = Vector3.Cross(dir0, Mathf.Abs(dir0.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 dir = i == 0 ? dir0
                    : i == path.Count - 1 ? (path[i] - path[i - 1]).normalized
                    : (path[i + 1] - path[i - 1]).normalized;
                normal = Vector3.ProjectOnPlane(normal, dir).normalized;
                Vector3 bin = Vector3.Cross(dir, normal);
                for (int j = 0; j < sides; j++)
                {
                    float t = Mathf.PI * 2f * j / sides;
                    V(path[i] + (normal * Mathf.Cos(t) + bin * Mathf.Sin(t)) * radius, new Vector2((float)j / sides, i));
                }
            }
            for (int i = 0; i < path.Count - 1; i++)
                for (int j = 0; j < sides; j++)
                {
                    int j1 = (j + 1) % sides;
                    Quad(start + i * sides + j, start + i * sides + j1, start + (i + 1) * sides + j1, start + (i + 1) * sides + j, true);
                }
            if (!caps) return;
            for (int end = 0; end < 2; end++)
            {
                int ringStart = start + (end == 0 ? 0 : (path.Count - 1) * sides);
                int c = V(path[end == 0 ? 0 : path.Count - 1], Vector2.zero);
                for (int j = 0; j < sides; j++)
                    Tri(c, ringStart + j, ringStart + (j + 1) % sides, end == 1);
            }
        }

        /// <summary>Solid of revolution around the local x axis; profile = (x, radius) pairs.
        /// The outside is to the left of the profile direction: walking along +x at a
        /// constant radius faces outward (normal = (-dr, dx) in the (x, r) plane).</summary>
        public void Lathe(IList<Vector2> profile, int segments)
        {
            int start = verts.Count;
            for (int i = 0; i < profile.Count; i++)
                for (int j = 0; j < segments; j++)
                {
                    float t = Mathf.PI * 2f * j / segments;
                    V(new Vector3(profile[i].x, Mathf.Cos(t) * profile[i].y, Mathf.Sin(t) * profile[i].y), new Vector2((float)j / segments, (float)i / (profile.Count - 1)));
                }
            for (int i = 0; i < profile.Count - 1; i++)
                for (int j = 0; j < segments; j++)
                {
                    int j1 = (j + 1) % segments;
                    Quad(start + i * segments + j, start + i * segments + j1, start + (i + 1) * segments + j1, start + (i + 1) * segments + j, true);
                }
        }

        /// <summary>Axis-aligned (optionally rotated) box with split faces.</summary>
        public void Box(Vector3 centre, Vector3 size, Quaternion rotation)
        {
            Vector3 h = size * 0.5f;
            Vector3[] n = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var f in n)
            {
                Vector3 u = Mathf.Abs(f.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 w = Vector3.Cross(f, u);
                Vector3 c = Vector3.Scale(f, h);
                Vector3 uu = Vector3.Scale(u, h), ww = Vector3.Scale(w, h);
                int a = V(centre + rotation * (c - uu - ww), new Vector2(0, 0));
                int b = V(centre + rotation * (c + uu - ww), new Vector2(1, 0));
                int cc = V(centre + rotation * (c + uu + ww), new Vector2(1, 1));
                int d = V(centre + rotation * (c - uu + ww), new Vector2(0, 1));
                Quad(a, b, cc, d, true);
            }
        }

        public void Box(Vector3 centre, Vector3 size) => Box(centre, size, Quaternion.identity);

        /// <summary>
        /// Extruded plate from a star-shaped polygon given in the (z, y) plane, centred
        /// at x with the given thickness (end plates, fins, strakes).
        /// </summary>
        public void PlateYZ(IList<Vector2> polyZY, float x, float thickness)
        {
            Plate(polyZY, p => new Vector3(x, p.y, p.x), Vector3.right * thickness * 0.5f);
        }

        /// <summary>Extruded plate from a polygon in the (x, z) plane at height y (floor, deflectors).</summary>
        public void PlateXZ(IList<Vector2> polyXZ, float y, float thickness)
        {
            Plate(polyXZ, p => new Vector3(p.x, y, p.y), Vector3.up * thickness * 0.5f);
        }

        void Plate(IList<Vector2> poly, Func<Vector2, Vector3> map, Vector3 halfExtrude)
        {
            int n = poly.Count;
            Vector2 centroid = Vector2.zero;
            foreach (var p in poly) centroid += p;
            centroid /= n;
            Vector3 c3 = map(centroid);
            // Every face is oriented against its known outward direction, so the
            // polygon's winding and the mapping's handedness do not matter.
            for (int side = 0; side < 2; side++)
            {
                Vector3 off = side == 0 ? halfExtrude : -halfExtrude;
                int c = V(c3 + off, new Vector2(0.5f, 0.5f));
                int first = verts.Count;
                for (int i = 0; i < n; i++) V(map(poly[i]) + off, poly[i]);
                for (int i = 0; i < n; i++)
                    TriFacing(c, first + i, first + (i + 1) % n, off);
            }
            for (int i = 0; i < n; i++)
            {
                Vector3 a = map(poly[i]), b = map(poly[(i + 1) % n]);
                Vector3 outward = Vector3.ProjectOnPlane((a + b) * 0.5f - c3, halfExtrude);
                int v0 = V(a + halfExtrude, Vector2.zero), v1 = V(b + halfExtrude, Vector2.right);
                int v2 = V(b - halfExtrude, Vector2.one), v3 = V(a - halfExtrude, Vector2.up);
                TriFacing(v0, v1, v2, outward);
                TriFacing(v0, v2, v3, outward);
            }
        }

        /// <summary>Adds a triangle whose front face (Unity convention) points along outward.</summary>
        void TriFacing(int a, int b, int c, Vector3 outward)
        {
            Vector3 normal = Vector3.Cross(verts[b] - verts[a], verts[c] - verts[a]);
            if (Vector3.Dot(normal, outward) >= 0f) { tris.Add(a); tris.Add(b); tris.Add(c); }
            else { tris.Add(a); tris.Add(c); tris.Add(b); }
        }

        // --- Smooth sculpting tools (APEX A01) -------------------------------------------

        /// <summary>
        /// Connects closed rings with the same point count into a smooth surface.
        /// Rings must be ordered along the body; outward faces are chosen from the
        /// ring centroids, so ring direction and station order do not matter.
        /// </summary>
        public void RingLoft(IList<Vector3[]> rings, bool capStart, bool capEnd)
        {
            int n = rings[0].Length;
            int start = verts.Count;
            for (int i = 0; i < rings.Count; i++)
                for (int j = 0; j < n; j++)
                    V(rings[i][j], new Vector2((float)j / n, i));
            // Orientation from topology, not from local geometry: a ring that winds
            // counter-clockwise about the loft direction gives outward quads (a, b, c).
            // This stays correct for concave sections (cockpit notch, undercuts).
            bool lastSign = true;
            for (int i = 0; i < rings.Count - 1; i++)
            {
                Vector3 dir = Centroid(rings[i + 1]) - Centroid(rings[i]);
                float dot = Vector3.Dot(Newell(rings[i]), dir);
                bool sign = Mathf.Abs(dot) < 1e-9f ? lastSign : dot > 0f;
                lastSign = sign;
                for (int j = 0; j < n; j++)
                {
                    int j1 = (j + 1) % n;
                    int a = start + i * n + j, b = start + i * n + j1, c = start + (i + 1) * n + j1, d = start + (i + 1) * n + j;
                    if (sign) { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(a); tris.Add(c); tris.Add(d); }
                    else { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(a); tris.Add(d); tris.Add(c); }
                }
            }
            for (int end = 0; end < 2; end++)
            {
                if ((end == 0 && !capStart) || (end == 1 && !capEnd)) continue;
                int ri = end == 0 ? 0 : rings.Count - 1;
                int other = end == 0 ? Mathf.Min(1, rings.Count - 1) : Mathf.Max(0, rings.Count - 2);
                Vector3 c = Centroid(rings[ri]);
                Vector3 outward = c - Centroid(rings[other]);
                int ci = V(c, new Vector2(0.5f, 0.5f));
                int first = verts.Count;
                for (int j = 0; j < n; j++) V(rings[ri][j], Vector2.zero);
                for (int j = 0; j < n; j++) TriFacing(ci, first + j, first + (j + 1) % n, outward);
            }
        }

        /// <summary>Newell normal of a closed polygon (right-handed sense of its winding).</summary>
        static Vector3 Newell(Vector3[] ring)
        {
            Vector3 n = Vector3.zero;
            for (int i = 0; i < ring.Length; i++)
            {
                Vector3 p = ring[i], q = ring[(i + 1) % ring.Length];
                n += new Vector3((p.y - q.y) * (p.z + q.z), (p.z - q.z) * (p.x + q.x), (p.x - q.x) * (p.y + q.y));
            }
            return n;
        }

        static Vector3 Centroid(Vector3[] ring)
        {
            Vector3 c = Vector3.zero;
            foreach (var p in ring) c += p;
            return c / ring.Length;
        }

        /// <summary>Airfoil ring (closed) in the (z, y) plane: leading edge at +chord/2, points ordered upper then lower.</summary>
        public static Vector2[] AirfoilRing(float chord, float thickness, float camber, float aoaDeg, int chordSegments)
        {
            var ring = new Vector2[chordSegments * 2];
            float rad = aoaDeg * Mathf.Deg2Rad;
            float ca = Mathf.Cos(rad), sa = Mathf.Sin(rad);
            for (int k = 0; k < chordSegments * 2; k++)
            {
                bool upper = k < chordSegments;
                int i = upper ? k : chordSegments * 2 - k;
                float x = 0.5f * (1f - Mathf.Cos(Mathf.PI * i / chordSegments));
                float yt = 5f * thickness * (0.2969f * Mathf.Sqrt(x) - 0.126f * x - 0.3516f * x * x + 0.2843f * x * x * x - 0.1036f * x * x * x * x);
                float yc = camber * 4f * x * (1f - x);
                float zc = chord * (0.5f - x);
                float yy = (upper ? yc + yt : yc - yt) * chord;
                ring[k] = new Vector2(zc * ca + yy * sa, -zc * sa + yy * ca);
            }
            return ring;
        }

        /// <summary>One spanwise station of a wing: position of the quarter chord and section shape.</summary>
        public struct WingStation
        {
            public Vector3 position;
            public float chord, thickness, camber, aoaDeg;
            /// <summary>Roll of the section about the chord line (deg): curls tips down into end plates.</summary>
            public float rollDeg;
        }

        /// <summary>Smooth wing surface through spanwise stations (curved, twisted, tapered elements).</summary>
        public void SpanWing(IList<WingStation> stations, int chordSegments, bool capEnds = true)
        {
            var rings = new List<Vector3[]>();
            foreach (var st in stations)
            {
                var prof = AirfoilRing(st.chord, st.thickness, st.camber, st.aoaDeg, chordSegments);
                float rr = st.rollDeg * Mathf.Deg2Rad;
                float cr = Mathf.Cos(rr), sr = Mathf.Sin(rr);
                var ring = new Vector3[prof.Length];
                for (int k = 0; k < prof.Length; k++)
                {
                    // Section plane spanned by z (chord) and the rolled "up" vector (x, y).
                    float up = prof[k].y;
                    ring[k] = st.position + new Vector3(-sr * up, cr * up, prof[k].x);
                }
                rings.Add(ring);
            }
            RingLoft(rings, capEnds, capEnds);
        }

        /// <summary>
        /// Curved thin wall (end plates, fences, strakes): a path in plan view (x, z)
        /// with bottom/top heights per point; thickness across the path.
        /// </summary>
        public void Fin(IList<Vector2> pathXZ, IList<float> bottom, IList<float> top, float thickness)
        {
            int n = pathXZ.Count;
            var rings = new List<Vector3[]>();
            for (int i = 0; i < n; i++)
            {
                Vector2 dir = i == 0 ? pathXZ[1] - pathXZ[0] : i == n - 1 ? pathXZ[n - 1] - pathXZ[n - 2] : pathXZ[i + 1] - pathXZ[i - 1];
                float len = Mathf.Sqrt(dir.x * dir.x + dir.y * dir.y);
                Vector3 side = new Vector3(dir.y / len, 0f, -dir.x / len) * (thickness * 0.5f);
                Vector3 b = new Vector3(pathXZ[i].x, bottom[i], pathXZ[i].y), t = new Vector3(pathXZ[i].x, top[i], pathXZ[i].y);
                rings.Add(new[] { b - side, t - side, t + side, b + side });
            }
            RingLoft(rings, true, true);
        }

        /// <summary>Tube with an elliptical, flattened section along a path (halo, faired links).</summary>
        public void FairedTube(IList<Vector3> path, float width, float height, int sides, Vector3 upHint)
        {
            var rings = new List<Vector3[]>();
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 dir = i == 0 ? path[1] - path[0] : i == path.Count - 1 ? path[i] - path[i - 1] : path[i + 1] - path[i - 1];
                dir = dir.normalized;
                Vector3 side = Vector3.Cross(upHint, dir).normalized;
                if (side.magnitude < 0.5f) side = Vector3.Cross(Vector3.forward, dir).normalized;
                Vector3 up = Vector3.Cross(dir, side);
                var ring = new Vector3[sides];
                for (int j = 0; j < sides; j++)
                {
                    float t = Mathf.PI * 2f * j / sides;
                    ring[j] = path[i] + side * (Mathf.Cos(t) * width * 0.5f) + up * (Mathf.Sin(t) * height * 0.5f);
                }
                rings.Add(ring);
            }
            RingLoft(rings, true, true);
        }

        /// <summary>Per-vertex normals (area-weighted face normals).</summary>
        public Vector3[] ComputeNormals()
        {
            var normals = new Vector3[verts.Count];
            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector3 a = verts[tris[i]], b = verts[tris[i + 1]], c = verts[tris[i + 2]];
                Vector3 n = Vector3.Cross(b - a, c - a);
                normals[tris[i]] += n;
                normals[tris[i + 1]] += n;
                normals[tris[i + 2]] += n;
            }
            for (int i = 0; i < normals.Length; i++) normals[i] = normals[i].normalized;
            return normals;
        }

        /// <summary>Replaces UVs with metric box projection (1 unit = 1 m) for tiling detail textures (carbon weave).</summary>
        public void MetricUVs()
        {
            var normals = ComputeNormals();
            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 n = normals[i], p = verts[i];
                float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
                uvs[i] = ax >= ay && ax >= az ? new Vector2(p.z, p.y) : ay >= az ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y);
            }
        }

        readonly Dictionary<long, int> shared = new Dictionary<long, int>();

        /// <summary>Vertex shared by key within this builder (smooth shading across quads).</summary>
        public int SharedVertex(long key, Vector3 p, Vector2 uv)
        {
            if (shared.TryGetValue(key, out int i)) return i;
            i = V(p, uv);
            shared[key] = i;
            return i;
        }

        /// <summary>Raw triangle by vertex indices (caller guarantees orientation).</summary>
        public void Triangle(int a, int b, int c)
        {
            tris.Add(a);
            tris.Add(b);
            tris.Add(c);
        }

        /// <summary>Applies f to the last count vertices in builder-local space (the matrix's translation is kept).</summary>
        public void TransformLast(int count, Func<Vector3, Vector3> f)
        {
            Vector3 offset = matrix.MultiplyPoint3x4(Vector3.zero);
            for (int i = verts.Count - count; i < verts.Count; i++) verts[i] = offset + f(verts[i] - offset);
        }

        public IReadOnlyList<Vector3> Vertices => verts;
        public IReadOnlyList<Vector2> UVs => uvs;
        public IReadOnlyList<int> Triangles => tris;

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
