using System;
using System.Collections.Generic;
using System.Numerics;
using Apex.Core;
using Apex.Environment;
using Apex.Vehicle;

namespace Apex.Tracks
{
    /// <summary>Triangle mesh with UVs and sub-meshes (engine-independent; converted 1:1 to a Unity Mesh).</summary>
    public sealed class SurfaceMesh
    {
        public readonly string name;
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<Vector2> uvs = new List<Vector2>();
        public readonly List<int>[] triangles;
        /// <summary>Physical surface of the mesh collider; null = visual only (no collider).</summary>
        public readonly SurfaceType? surface;

        public SurfaceMesh(string name, int submeshes, SurfaceType? surface)
        {
            this.name = name;
            this.surface = surface;
            triangles = new List<int>[submeshes];
            for (int i = 0; i < submeshes; i++) triangles[i] = new List<int>();
        }

        public int TriangleCount
        {
            get { int n = 0; foreach (var t in triangles) n += t.Count / 3; return n; }
        }

        /// <summary>Quad i→j along the track; b is right of a. Front face up/inward (Unity: clockwise from the front).</summary>
        public void Quad(int sub, Vector3 ai, Vector3 bi, Vector3 aj, Vector3 bj, Vector2 uai, Vector2 ubi, Vector2 uaj, Vector2 ubj, bool doubleSided = false)
        {
            int s = vertices.Count;
            vertices.Add(ai); vertices.Add(bi); vertices.Add(aj); vertices.Add(bj);
            uvs.Add(uai); uvs.Add(ubi); uvs.Add(uaj); uvs.Add(ubj);
            var t = triangles[sub];
            t.Add(s); t.Add(s + 2); t.Add(s + 3);
            t.Add(s); t.Add(s + 3); t.Add(s + 1);
            if (doubleSided)
            {
                t.Add(s); t.Add(s + 3); t.Add(s + 2);
                t.Add(s); t.Add(s + 1); t.Add(s + 3);
            }
        }
    }

    /// <summary>
    /// The single canonical definition of the circuit's physical surfaces: road (with edge
    /// lines), kerbs, run-off, pit apron and lane, pit wall, collision barriers, the skirt and
    /// the near terrain. The Unity builder turns each <see cref="SurfaceMesh"/> into the visual
    /// mesh <b>and</b> the MeshCollider, and the offline drive rig and the validation ray-cast
    /// against exactly the same triangles, so what is tested is what the car drives on.
    /// The road cross-section is flat (no camber); heights come from the centreline.
    /// </summary>
    public sealed class RoadSurface
    {
        public const float LineWidth = 0.3f;
        public const float RunoffWidth = 16f;
        public const float RunoffDrop = 0.12f;
        public const float WallHeight = 1.1f;
        public const float SkirtDepth = 8f;
        /// <summary>Pit area width from the track edge to the outer pit wall.</summary>
        public const float PitOuter = 13.5f;
        /// <summary>Pit wall offset from the track edge.</summary>
        public const float PitWallFromEdge = 2.1f;

        public readonly TrackGeometry geometry;
        public readonly CircuitDefinition definition;
        public readonly float[] kerbLeft, kerbRight, kerbHeight;
        public readonly bool[] pitMask;
        public readonly float[] wallLeft, wallRight;
        public readonly SurfaceMesh road, kerbs, runoff, barriers, skirt;
        public readonly SurfaceMesh pitApron, pitLane, pitWall;
        public SurfaceMesh terrain;

        public IEnumerable<SurfaceMesh> All
        {
            get
            {
                foreach (var m in new[] { road, kerbs, runoff, barriers, skirt, pitApron, pitLane, pitWall, terrain })
                    if (m != null) yield return m;
            }
        }

        public RoadSurface(TrackGeometry g, CircuitDefinition def, float[] kerbL, float[] kerbR, float[] kerbH)
        {
            geometry = g;
            definition = def;
            kerbLeft = kerbL;
            kerbRight = kerbR;
            kerbHeight = kerbH;
            int n = g.Count;
            pitMask = new bool[n];
            if (def.pit != null)
                for (int i = 0; i < n; i++) pitMask[i] = def.pit.Contains(g.Distances[i], g.Length);
            wallLeft = new float[n];
            wallRight = new float[n];
            for (int i = 0; i < n; i++)
            {
                bool pitHere = def.pit != null && pitMask[i];
                wallLeft[i] = WallOffset(i, pitHere && def.pit.side < 0, -1);
                wallRight[i] = WallOffset(i, pitHere && def.pit.side > 0, 1);
            }
            road = BuildRoad();
            kerbs = BuildKerbs();
            BuildRunoffAndWalls(out runoff, out barriers, out skirt);
            if (def.pit != null) BuildPit(out pitApron, out pitLane, out pitWall);
        }

        /// <summary>Lateral distance of the barrier from the centreline; side -1 = left, +1 = right.</summary>
        public float WallOffset(int i, bool pit, int side)
        {
            var g = geometry;
            if (pit) return g.Widths[i] * 0.5f + PitOuter;
            float[] r = side < 0 ? g.RunoffLeft : g.RunoffRight;
            float[] kerb = side < 0 ? kerbLeft : kerbRight;
            // Run-off is measured from the track edge; a kerb lies inside it.
            return g.Widths[i] * 0.5f + MathF.Max(r != null ? r[i] : RunoffWidth, kerb[i] + 0.2f);
        }

        public static Vector3 P(TrackGeometry g, int i, float lateral, float height) =>
            g.Positions[i] + g.Rights[i] * lateral + new Vector3(0f, height, 0f);

        /// <summary>Ribbon between two lateral offsets (x,y = left offset/height, z,w = right) along the track.</summary>
        public static void Ribbon(SurfaceMesh md, int sub, TrackGeometry g, Func<int, Vector4> offsets, float uvScale = 0.1f, Func<int, int> subFn = null)
        {
            int n = g.Count;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                var oi = offsets(i);
                var oj = offsets(j);
                bool ei = MathF.Abs(oi.Z - oi.X) < 0.01f, ej = MathF.Abs(oj.Z - oj.X) < 0.01f;
                if (ei && ej) continue;
                // Where a strip starts or ends, the empty end collapses onto the other end's inner edge.
                // (An empty end reported as (0,0,0,0) would otherwise pull a wedge across the road from
                // the centreline to the strip.)
                if (ei) oi = InnerEdge(oj);
                if (ej) oj = InnerEdge(oi);
                float vi = g.Distances[i] * uvScale;
                float vj = vi + g.Spacing * uvScale;
                md.Quad(subFn != null ? subFn(i) : sub,
                    P(g, i, oi.X, oi.Y), P(g, i, oi.Z, oi.W), P(g, j, oj.X, oj.Y), P(g, j, oj.Z, oj.W),
                    new Vector2(oi.X * uvScale, vi), new Vector2(oi.Z * uvScale, vi),
                    new Vector2(oj.X * uvScale, vj), new Vector2(oj.Z * uvScale, vj));
            }
        }

        /// <summary>The edge nearer the centreline of a strip (x,y left; z,w right), as a zero-width strip.</summary>
        static Vector4 InnerEdge(Vector4 o) =>
            MathF.Abs(o.X) <= MathF.Abs(o.Z) ? new Vector4(o.X, o.Y, o.X, o.Y) : new Vector4(o.Z, o.W, o.Z, o.W);

        SurfaceMesh BuildRoad()
        {
            var g = geometry;
            var md = new SurfaceMesh("Road", 2, SurfaceType.Asphalt);
            Ribbon(md, 1, g, i => { float h = g.Widths[i] * 0.5f; return new Vector4(-h, 0f, -h + LineWidth, 0f); });
            Ribbon(md, 0, g, i => { float h = g.Widths[i] * 0.5f; return new Vector4(-h + LineWidth, 0f, h - LineWidth, 0f); });
            Ribbon(md, 1, g, i => { float h = g.Widths[i] * 0.5f; return new Vector4(h - LineWidth, 0f, h, 0f); });
            return md;
        }

        /// <summary>
        /// Kerbs with a rounded crown (height per sample from the infrastructure plan) and a short
        /// outer edge down to the run-off; red/white blocks every 2.5 m (sub-meshes 0/1 of the crown).
        /// </summary>
        SurfaceMesh BuildKerbs()
        {
            var g = geometry;
            var md = new SurfaceMesh("Kerbs", 2, SurfaceType.Kerb);
            Func<int, int> stripe = i => ((int)(g.Distances[i] / 2.5f)) % 2;
            // Cross-section from the track edge outwards: (fraction of width, fraction of crown height).
            var prof = new[] { new Vector2(0f, 0f), new Vector2(0.25f, 0.7f), new Vector2(0.5f, 1f), new Vector2(0.78f, 0.85f), new Vector2(1f, 0.25f) };
            foreach (float side in new[] { -1f, 1f })
            {
                float[] k = side < 0 ? kerbLeft : kerbRight;
                for (int s = 0; s < prof.Length - 1; s++)
                {
                    var a = prof[s];
                    var b = prof[s + 1];
                    Ribbon(md, 0, g, i =>
                    {
                        float h = g.Widths[i] * 0.5f, w = k[i], y = kerbHeight[i];
                        if (w <= 0f) return Vector4.Zero;
                        return side < 0 ? new Vector4(-(h + w * b.X), y * b.Y, -(h + w * a.X), y * a.Y)
                                        : new Vector4(h + w * a.X, y * a.Y, h + w * b.X, y * b.Y);
                    }, 0.1f, stripe);
                }
                int n = g.Count;
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    if (k[i] <= 0f && k[j] <= 0f) continue;
                    float oi = side * (g.Widths[i] * 0.5f + k[i]), oj = side * (g.Widths[j] * 0.5f + k[j]);
                    md.Quad(1, P(g, i, oi, -RunoffDrop), P(g, i, oi, kerbHeight[i] * 0.25f), P(g, j, oj, -RunoffDrop), P(g, j, oj, kerbHeight[j] * 0.25f),
                        Vector2.Zero, Vector2.UnitY, Vector2.UnitX, Vector2.One, true);
                }
            }
            return md;
        }

        void BuildRunoffAndWalls(out SurfaceMesh runoffMesh, out SurfaceMesh walls, out SurfaceMesh skirtMesh)
        {
            var g = geometry;
            var pit = definition.pit;
            bool street = g.RunoffLeft != null;
            runoffMesh = new SurfaceMesh("Runoff", 1, street ? SurfaceType.Asphalt : SurfaceType.Grass);
            walls = new SurfaceMesh("Barriers", 2, SurfaceType.Wall);
            skirtMesh = new SurfaceMesh("Skirt", 1, null);
            int n = g.Count;
            foreach (int side in new[] { -1, 1 })
            {
                float[] k = side < 0 ? kerbLeft : kerbRight;
                bool pitSide = pit != null && side == pit.side;
                Ribbon(runoffMesh, 0, g, i =>
                {
                    float inner = g.Widths[i] * 0.5f + k[i];
                    // The pit lane replaces the run-off on its side.
                    float outer = pitSide && pitMask[i] ? inner : WallOffset(i, false, side);
                    return side < 0 ? new Vector4(-outer, -RunoffDrop, -inner, 0f) : new Vector4(inner, 0f, outer, -RunoffDrop);
                }, 0.25f);
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    bool pi = pitSide && pitMask[i], pj = pitSide && pitMask[j];
                    float oi = side * WallOffset(i, pi, side), oj = side * WallOffset(j, pj, side);
                    float yi = pi ? 0f : -RunoffDrop, yj = pj ? 0f : -RunoffDrop;
                    float v = g.Distances[i] * 0.25f;
                    int sub = ((int)(g.Distances[i] / 6f)) % 2;
                    var bi = P(g, i, oi, yi - 0.4f);
                    var ti = P(g, i, oi, yi + WallHeight);
                    var bj = P(g, j, oj, yj - 0.4f);
                    var tj = P(g, j, oj, yj + WallHeight);
                    walls.Quad(sub, bi, ti, bj, tj, new Vector2(0, v), new Vector2(1, v), new Vector2(0, v + 0.5f), new Vector2(1, v + 0.5f), true);
                    // Skirt hides gaps between run-off and terrain (visual only).
                    var si = P(g, i, oi, yi - SkirtDepth);
                    var sj = P(g, j, oj, yj - SkirtDepth);
                    skirtMesh.Quad(0, si, bi, sj, bj, Vector2.Zero, Vector2.UnitX, Vector2.UnitY, Vector2.One, true);
                }
            }
        }

        void BuildPit(out SurfaceMesh apron, out SurfaceMesh lane, out SurfaceMesh wall)
        {
            var g = geometry;
            var pit = definition.pit;
            float side = pit.side;
            apron = new SurfaceMesh("PitApron", 1, SurfaceType.Asphalt);
            lane = new SurfaceMesh("PitLane", 1, SurfaceType.PitLane);
            Ribbon(apron, 0, g, i =>
            {
                if (!pitMask[i]) return Vector4.Zero;
                float e = g.Widths[i] * 0.5f;
                return side < 0 ? new Vector4(-(e + 2f), 0f, -e, 0f) : new Vector4(e, 0f, e + 2f, 0f);
            });
            Ribbon(lane, 0, g, i =>
            {
                if (!pitMask[i]) return Vector4.Zero;
                float e = g.Widths[i] * 0.5f;
                return side < 0 ? new Vector4(-(e + PitOuter), 0f, -(e + 2f), 0f) : new Vector4(e + 2f, 0f, e + PitOuter, 0f);
            });
            // Pit wall between the merge zones.
            wall = new SurfaceMesh("PitWall", 1, SurfaceType.Wall);
            int n = g.Count;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                float di = ApexMath.PosMod(g.Distances[i] - pit.entry, g.Length);
                float span = ApexMath.PosMod(pit.exit - pit.entry, g.Length);
                if (!pitMask[i] || !pitMask[j] || di < 60f || di > span - 60f) continue;
                float oi = side * (g.Widths[i] * 0.5f + PitWallFromEdge), oj = side * (g.Widths[j] * 0.5f + PitWallFromEdge);
                wall.Quad(0, P(g, i, oi, -0.2f), P(g, i, oi, 1.0f), P(g, j, oj, -0.2f), P(g, j, oj, 1.0f),
                    Vector2.Zero, Vector2.UnitY, Vector2.UnitX, Vector2.One, true);
            }
        }

        /// <summary>
        /// Near terrain on the <see cref="TerrainField.NearCell"/> grid (two triangles per cell), plus
        /// a skirt along the border when far land exists. Physical surface: grass.
        /// </summary>
        public SurfaceMesh BuildTerrain(TerrainField field)
        {
            const float cell = TerrainField.NearCell;
            float minX = field.MinX, minZ = field.MinZ, maxX = field.MaxX, maxZ = field.MaxZ;
            int nx = (int)MathF.Round((maxX - minX) / cell) + 1;
            int nz = (int)MathF.Round((maxZ - minZ) / cell) + 1;
            var md = new SurfaceMesh("Terrain", 1, SurfaceType.Grass);
            var verts = md.vertices;
            var uvs = md.uvs;
            var tris = md.triangles[0];
            for (int zi = 0; zi < nz; zi++)
                for (int xi = 0; xi < nx; xi++)
                {
                    float x = minX + xi * cell, z = minZ + zi * cell;
                    verts.Add(new Vector3(x, field.Height(x, z), z));
                    uvs.Add(new Vector2(x / 40f, z / 40f));
                }
            for (int zi = 0; zi < nz - 1; zi++)
                for (int xi = 0; xi < nx - 1; xi++)
                {
                    int a = zi * nx + xi, b = a + 1, c = a + nx, d2 = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(d2);
                    tris.Add(a); tris.Add(d2); tris.Add(b);
                }
            if (field.land.HasLand)
            {
                // Skirt along the border hides hairline cracks against the coarser far grid.
                void Skirt(int i0, int i1)
                {
                    var a = verts[i0]; var b = verts[i1];
                    int s = verts.Count;
                    verts.Add(a); verts.Add(b); verts.Add(a - new Vector3(0f, 12f, 0f)); verts.Add(b - new Vector3(0f, 12f, 0f));
                    uvs.Add(uvs[i0]); uvs.Add(uvs[i1]); uvs.Add(uvs[i0]); uvs.Add(uvs[i1]);
                    tris.Add(s); tris.Add(s + 1); tris.Add(s + 3);
                    tris.Add(s); tris.Add(s + 3); tris.Add(s + 2);
                    tris.Add(s); tris.Add(s + 3); tris.Add(s + 1);
                    tris.Add(s); tris.Add(s + 2); tris.Add(s + 3);
                }
                for (int xi = 0; xi < nx - 1; xi++) { Skirt(xi, xi + 1); Skirt((nz - 1) * nx + xi, (nz - 1) * nx + xi + 1); }
                for (int zi = 0; zi < nz - 1; zi++) { Skirt(zi * nx, (zi + 1) * nx); Skirt(zi * nx + nx - 1, (zi + 1) * nx + nx - 1); }
            }
            terrain = md;
            return md;
        }
    }
}
