using System;
using System.Collections.Generic;
using Apex.Core;
using Apex.Tracks;
using Apex.Vehicle;
using UnityEngine;
using UnityEngine.Rendering;

namespace Apex.Game
{
    /// <summary>
    /// Runtime circuit: authored definition, baked geometry and helpers for lap
    /// distance and grid placement. Created by <see cref="CircuitBuilder"/>.
    /// </summary>
    public sealed class CircuitInstance : MonoBehaviour
    {
        public CircuitDefinition Definition { get; internal set; }
        public TrackGeometry Geometry { get; internal set; }
        public float[] SectorStarts { get; internal set; }
        public System.Collections.Generic.List<TrackGeometry.Corner> Corners { get; internal set; }
        public CircuitDefinition.PitLane Pit => Definition.pit;

        /// <summary>Lateral offset (+right) of the pit lane centre at a raw distance.</summary>
        public float PitLaneLateral(float rawDistance)
        {
            var p = Definition.pit;
            return p.side * (Geometry.WidthAt(rawDistance) * 0.5f + p.laneCentreFromEdge);
        }

        public float LapDistance(float rawDistance) => ApexMath.PosMod(rawDistance - Definition.startLineDistance, Geometry.Length);
        public float RawDistance(float lapDistance) => Geometry.Wrap(lapDistance + Definition.startLineDistance);

        /// <summary>Grid slot pose (slot 0 = pole), staggered two-wide, 8 m apart.</summary>
        public Pose GridPose(int slot, float heightAboveRoad)
        {
            float back = 12f + slot * 8f;
            float side = slot % 2 == 0 ? -3f : 3f;
            return PoseAt(RawDistance(-back), side, heightAboveRoad);
        }

        public Pose PoseAt(float rawDistance, float lateral, float height)
        {
            Geometry.FrameAt(rawDistance, out var p, out var f, out _, lateral);
            var fwd = f.ToUnity();
            return new Pose(p.ToUnity() + Vector3.up * height, Quaternion.LookRotation(fwd, Vector3.up));
        }
    }

    /// <summary>
    /// Builds a circuit procedurally from its definition: road with edge lines,
    /// kerbs, grass run-off, barriers, terrain, water, start line and gantry, each
    /// surface with a tagged MeshCollider. All geometry is generated, so no
    /// third-party track assets are involved.
    /// </summary>
    public static class CircuitBuilder
    {
        const float KerbWidth = 1.3f;
        const float KerbHeight = 0.045f;
        const float KerbCurvature = 1f / 260f;
        const float LineWidth = 0.3f;
        const float RunoffWidth = 16f;
        const float RunoffDrop = 0.12f;
        const float WallHeight = 1.1f;
        const float SkirtDepth = 8f;
        /// <summary>Pit area width from the track edge to the outer pit wall.</summary>
        const float PitOuter = 13.5f;

        public static CircuitInstance Build(CircuitDefinition def, Transform parent = null)
        {
            var root = new GameObject("Circuit_" + def.id);
            if (parent) root.transform.SetParent(parent, false);
            var ci = root.AddComponent<CircuitInstance>();
            ci.Definition = def;
            ci.Geometry = TrackGeometry.FromDefinition(def);
            var starts = new float[def.sectorFractions.Length];
            for (int i = 0; i < starts.Length; i++) starts[i] = def.sectorFractions[i] * ci.Geometry.Length;
            ci.SectorStarts = starts;

            var g = ci.Geometry;
            var kerbL = new float[g.Count];
            var kerbR = new float[g.Count];
            ComputeKerbs(g, kerbL, kerbR);

            BuildRoad(root.transform, g);
            BuildKerbs(root.transform, g, kerbL, kerbR);
            var pitMask = new bool[g.Count];
            if (def.pit != null)
                for (int i = 0; i < g.Count; i++) pitMask[i] = def.pit.Contains(g.Distances[i], g.Length);
            ci.Corners = g.DetectCorners();
            BuildRunoffAndWalls(root.transform, g, kerbL, kerbR, def.pit, pitMask);
            if (def.pit != null) BuildPitLane(root.transform, ci, pitMask);
            BuildTerrain(root.transform, g, def);
            BuildStartLine(root.transform, ci);
            BuildGrid(root.transform, ci);
            BuildCornerBoards(root.transform, ci);
            return ci;
        }

        // --- Mesh helper ----------------------------------------------------------

        sealed class MeshData
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int>[] tris;

            public MeshData(int submeshes)
            {
                tris = new List<int>[submeshes];
                for (int i = 0; i < submeshes; i++) tris[i] = new List<int>();
            }

            /// <summary>Quad i->j along the track; b is right of a. Front face up/inward.</summary>
            public void Quad(int sub, Vector3 ai, Vector3 bi, Vector3 aj, Vector3 bj, Vector2 uai, Vector2 ubi, Vector2 uaj, Vector2 ubj, bool doubleSided = false)
            {
                int s = v.Count;
                v.Add(ai); v.Add(bi); v.Add(aj); v.Add(bj);
                uv.Add(uai); uv.Add(ubi); uv.Add(uaj); uv.Add(ubj);
                // Unity front faces are clockwise seen from the front.
                var t = tris[sub];
                t.Add(s); t.Add(s + 2); t.Add(s + 3);
                t.Add(s); t.Add(s + 3); t.Add(s + 1);
                if (doubleSided)
                {
                    t.Add(s); t.Add(s + 3); t.Add(s + 2);
                    t.Add(s); t.Add(s + 1); t.Add(s + 3);
                }
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                m.SetVertices(v);
                m.SetUVs(0, uv);
                m.subMeshCount = tris.Length;
                for (int i = 0; i < tris.Length; i++) m.SetTriangles(tris[i], i);
                m.RecalculateNormals();
                m.RecalculateBounds();
                return m;
            }
        }

        static GameObject AddMesh(Transform parent, string name, Mesh mesh, string[] materials, SurfaceType? surface, bool shadows = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var mats = new Material[materials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = ApexMaterials.Get(materials[i]);
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            go.isStatic = true;
            if (surface.HasValue)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                go.AddComponent<TrackSurface>().surface = surface.Value;
            }
            return go;
        }

        static Vector3 P(TrackGeometry g, int i, float lateral, float height) =>
            (g.Positions[i] + g.Rights[i] * lateral).ToUnity() + Vector3.up * height;

        /// <summary>Ribbon between two lateral offsets (a left of b) along the track.</summary>
        static void Ribbon(MeshData md, int sub, TrackGeometry g, Func<int, Vector4> offsets, float uvScale = 0.1f, Func<int, int> subFn = null)
        {
            int n = g.Count;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                var oi = offsets(i);
                var oj = offsets(j);
                if (Mathf.Abs(oi.z - oi.x) < 0.01f && Mathf.Abs(oj.z - oj.x) < 0.01f) continue;
                float vi = g.Distances[i] * uvScale;
                float vj = vi + g.Spacing * uvScale;
                md.Quad(subFn != null ? subFn(i) : sub,
                    P(g, i, oi.x, oi.y), P(g, i, oi.z, oi.w), P(g, j, oj.x, oj.y), P(g, j, oj.z, oj.w),
                    new Vector2(oi.x * uvScale, vi), new Vector2(oi.z * uvScale, vi),
                    new Vector2(oj.x * uvScale, vj), new Vector2(oj.z * uvScale, vj));
            }
        }

        // --- Parts ------------------------------------------------------------------

        static void ComputeKerbs(TrackGeometry g, float[] left, float[] right)
        {
            int n = g.Count;
            var raw = new float[n];
            for (int i = 0; i < n; i++) raw[i] = Mathf.Abs(g.Curvatures[i]) > KerbCurvature ? 1f : 0f;
            int dilate = Mathf.RoundToInt(16f / g.Spacing);
            const int taper = 4;
            for (int i = 0; i < n; i++)
            {
                float k = 0f;
                for (int o = -dilate - taper; o <= dilate + taper; o++)
                {
                    float w = Mathf.Abs(o) > dilate ? 1f - (float)(Mathf.Abs(o) - dilate) / (taper + 1) : 1f;
                    k = Mathf.Max(k, raw[((i + o) % n + n) % n] * w);
                }
                left[i] = k;
                right[i] = k;
            }
        }

        static void BuildRoad(Transform parent, TrackGeometry g)
        {
            var md = new MeshData(2);
            Ribbon(md, 1, g, i => { float h = g.Widths[i] * 0.5f; return new Vector4(-h, 0f, -h + LineWidth, 0f); });
            Ribbon(md, 0, g, i => { float h = g.Widths[i] * 0.5f; return new Vector4(-h + LineWidth, 0f, h - LineWidth, 0f); });
            Ribbon(md, 1, g, i => { float h = g.Widths[i] * 0.5f; return new Vector4(h - LineWidth, 0f, h, 0f); });
            AddMesh(parent, "Road", md.ToMesh("Road"), new[] { "Asphalt", "Line" }, SurfaceType.Asphalt);
        }

        static void BuildKerbs(Transform parent, TrackGeometry g, float[] kerbL, float[] kerbR)
        {
            var md = new MeshData(2);
            Func<int, int> stripe = i => ((int)(g.Distances[i] / 3f)) % 2;
            foreach (float side in new[] { -1f, 1f })
            {
                float[] k = side < 0 ? kerbL : kerbR;
                // Rising half then falling half: a shallow ridge.
                Ribbon(md, 0, g, i =>
                {
                    float h = g.Widths[i] * 0.5f, w = KerbWidth * k[i];
                    float y = KerbHeight * Mathf.Min(k[i] * 2f, 1f);
                    return side < 0 ? new Vector4(-(h + w * 0.5f), y, -h, 0f) : new Vector4(h, 0f, h + w * 0.5f, y);
                }, 0.1f, stripe);
                Ribbon(md, 0, g, i =>
                {
                    float h = g.Widths[i] * 0.5f, w = KerbWidth * k[i];
                    float y = KerbHeight * Mathf.Min(k[i] * 2f, 1f);
                    return side < 0 ? new Vector4(-(h + w), 0f, -(h + w * 0.5f), y) : new Vector4(h + w * 0.5f, y, h + w, 0f);
                }, 0.1f, stripe);
            }
            AddMesh(parent, "Kerbs", md.ToMesh("Kerbs"), new[] { "KerbRed", "KerbWhite" }, SurfaceType.Kerb);
        }

        static float WallOffset(TrackGeometry g, float[] kerb, int i, bool pit) =>
            pit ? g.Widths[i] * 0.5f + PitOuter : g.Widths[i] * 0.5f + KerbWidth * kerb[i] + RunoffWidth;

        static void BuildRunoffAndWalls(Transform parent, TrackGeometry g, float[] kerbL, float[] kerbR, CircuitDefinition.PitLane pit, bool[] pitMask)
        {
            var grass = new MeshData(1);
            var walls = new MeshData(2);
            var skirt = new MeshData(1);
            int n = g.Count;
            foreach (float side in new[] { -1f, 1f })
            {
                float[] k = side < 0 ? kerbL : kerbR;
                bool pitSide = pit != null && (int)side == pit.side;
                Ribbon(grass, 0, g, i =>
                {
                    float inner = g.Widths[i] * 0.5f + KerbWidth * k[i];
                    // The pit lane replaces the run-off on its side.
                    float outer = pitSide && pitMask[i] ? inner : WallOffset(g, k, i, false);
                    return side < 0 ? new Vector4(-outer, -RunoffDrop, -inner, 0f) : new Vector4(inner, 0f, outer, -RunoffDrop);
                }, 0.25f);
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    bool pi_ = pitSide && pitMask[i], pj = pitSide && pitMask[j];
                    float oi = side * WallOffset(g, k, i, pi_), oj = side * WallOffset(g, k, j, pj);
                    float yi = pi_ ? 0f : -RunoffDrop, yj = pj ? 0f : -RunoffDrop;
                    float v = g.Distances[i] * 0.25f;
                    int sub = ((int)(g.Distances[i] / 6f)) % 2;
                    var bi = P(g, i, oi, yi - 0.4f);
                    var ti = P(g, i, oi, yi + WallHeight);
                    var bj = P(g, j, oj, yj - 0.4f);
                    var tj = P(g, j, oj, yj + WallHeight);
                    walls.Quad(sub, bi, ti, bj, tj, new Vector2(0, v), new Vector2(1, v), new Vector2(0, v + 0.5f), new Vector2(1, v + 0.5f), true);
                    // Skirt hides gaps between run-off and terrain.
                    var si = P(g, i, oi, yi - SkirtDepth);
                    var sj = P(g, j, oj, yj - SkirtDepth);
                    skirt.Quad(0, si, bi, sj, bj, Vector2.zero, Vector2.right, Vector2.up, Vector2.one, true);
                }
            }
            AddMesh(parent, "Runoff", grass.ToMesh("Runoff"), new[] { "Grass" }, SurfaceType.Grass);
            AddMesh(parent, "Barriers", walls.ToMesh("Barriers"), new[] { "Wall", "WallStripe" }, SurfaceType.Wall, true);
            AddMesh(parent, "Skirt", skirt.ToMesh("Skirt"), new[] { "Ground" }, null);
        }

        /// <summary>Pit apron, pit lane, pit wall, box markings and garage block.</summary>
        static void BuildPitLane(Transform parent, CircuitInstance ci, bool[] pitMask)
        {
            var g = ci.Geometry;
            var pit = ci.Pit;
            float side = pit.side;
            var apron = new MeshData(1);
            var lane = new MeshData(1);
            Ribbon(apron, 0, g, i =>
            {
                if (!pitMask[i]) return Vector4.zero;
                float e = g.Widths[i] * 0.5f;
                return side < 0 ? new Vector4(-(e + 2f), 0f, -e, 0f) : new Vector4(e, 0f, e + 2f, 0f);
            });
            Ribbon(lane, 0, g, i =>
            {
                if (!pitMask[i]) return Vector4.zero;
                float e = g.Widths[i] * 0.5f;
                return side < 0 ? new Vector4(-(e + PitOuter), 0f, -(e + 2f), 0f) : new Vector4(e + 2f, 0f, e + PitOuter, 0f);
            });
            AddMesh(parent, "PitApron", apron.ToMesh("PitApron"), new[] { "Asphalt" }, SurfaceType.Asphalt);
            AddMesh(parent, "PitLane", lane.ToMesh("PitLane"), new[] { "PitLane" }, SurfaceType.PitLane);
            // Pit wall between the merge zones.
            var wall = new MeshData(1);
            int n = g.Count;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                float di = ApexMath.PosMod(g.Distances[i] - pit.entry, g.Length);
                float span = ApexMath.PosMod(pit.exit - pit.entry, g.Length);
                if (!pitMask[i] || !pitMask[j] || di < 60f || di > span - 60f) continue;
                float oi = side * (g.Widths[i] * 0.5f + 2.1f), oj = side * (g.Widths[j] * 0.5f + 2.1f);
                wall.Quad(0, P(g, i, oi, -0.2f), P(g, i, oi, 1.0f), P(g, j, oj, -0.2f), P(g, j, oj, 1.0f),
                    Vector2.zero, Vector2.up, Vector2.right, Vector2.one, true);
            }
            AddMesh(parent, "PitWall", wall.ToMesh("PitWall"), new[] { "Wall" }, SurfaceType.Wall, true);
            // Box markings: a yellow frame per box on the lane.
            var marks = new MeshData(1);
            for (int b = 0; b < 20; b++)
            {
                float d = g.Wrap(pit.BoxDistance(b));
                float lat = ci.PitLaneLateral(d);
                Frame(marks, g, d, lat, 6f, 3.6f, 0.15f);
            }
            AddMesh(parent, "PitBoxes", marks.ToMesh("PitBoxes"), new[] { "Marker" }, null);
            // Garage block behind the outer pit wall.
            float start = pit.BoxDistance(0) - 15f, end = pit.BoxDistance(19) + 15f;
            float mid = g.Wrap((start + end) * 0.5f);
            var pose = ci.PoseAt(mid, side * (g.WidthAt(mid) * 0.5f + PitOuter + 6f), 3.5f);
            var garage = new GameObject("Garages");
            garage.transform.SetParent(parent, false);
            garage.transform.SetPositionAndRotation(pose.position, pose.rotation);
            garage.transform.localScale = new Vector3(10f, 7f, end - start);
            garage.AddComponent<MeshFilter>().sharedMesh = CarBuilder.PrimitiveMesh(PrimitiveType.Cube);
            garage.AddComponent<MeshRenderer>().sharedMaterial = ApexMaterials.Get("Building");
            garage.AddComponent<BoxCollider>();
        }

        /// <summary>Rectangular outline on the road surface (centre distance, lateral, length, width, line width).</summary>
        static void Frame(MeshData md, TrackGeometry g, float d, float lat, float length, float width, float lw)
        {
            const float lift = 0.012f;
            void Seg(float d0, float d1, float l0, float l1)
            {
                int i0 = g.IndexAt(d0), i1 = g.IndexAt(d1);
                var a = (g.PositionAt(d0) + g.RightAt(d0) * l0).ToUnity() + Vector3.up * lift;
                var b = (g.PositionAt(d0) + g.RightAt(d0) * l1).ToUnity() + Vector3.up * lift;
                var c = (g.PositionAt(d1) + g.RightAt(d1) * l0).ToUnity() + Vector3.up * lift;
                var e = (g.PositionAt(d1) + g.RightAt(d1) * l1).ToUnity() + Vector3.up * lift;
                md.Quad(0, a, b, c, e, Vector2.zero, Vector2.right, Vector2.up, Vector2.one);
            }
            float h = length * 0.5f, w = width * 0.5f;
            Seg(d - h, d - h + lw, lat - w, lat + w);
            Seg(d + h - lw, d + h, lat - w, lat + w);
            Seg(d - h, d + h, lat - w, lat - w + lw);
            Seg(d - h, d + h, lat + w - lw, lat + w);
        }

        /// <summary>Painted grid slots (staggered two-wide, 8 m apart).</summary>
        static void BuildGrid(Transform parent, CircuitInstance ci)
        {
            var md = new MeshData(1);
            var g = ci.Geometry;
            for (int slot = 0; slot < 20; slot++)
            {
                float back = 12f + slot * 8f;
                float d = ci.RawDistance(-back);
                float side = slot % 2 == 0 ? -3f : 3f;
                // Front bar and two short side lines, like a real grid box.
                Frame(md, g, d + 2.6f, side, 0.25f, 2.6f, 0.25f);
                Frame(md, g, d + 1.6f, side - 1.3f, 2.2f, 0.2f, 0.2f);
                Frame(md, g, d + 1.6f, side + 1.3f, 2.2f, 0.2f, 0.2f);
            }
            AddMesh(parent, "GridSlots", md.ToMesh("GridSlots"), new[] { "Line" }, null);
        }

        static Font builtinFont;

        internal static Font BuiltinFont()
        {
            if (builtinFont != null) return builtinFont;
            foreach (var name in new[] { "LegacyRuntime.ttf", "Arial.ttf" })
            {
                try
                {
                    builtinFont = Resources.GetBuiltinResource<Font>(name);
                    if (builtinFont != null) break;
                }
                catch (Exception)
                {
                    // Name differs between Unity versions; try the next one.
                }
            }
            return builtinFont;
        }

        static void Sign(Transform parent, Vector3 pos, Quaternion rot, string text, Color board, Color textColor, float size)
        {
            var root = new GameObject("Sign_" + text);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, rot);
            Prop(root.transform, PrimitiveType.Cube, "Steel", new Vector3(0f, 1.2f, 0f), new Vector3(0.12f, 2.4f, 0.12f));
            var panel = Prop(root.transform, PrimitiveType.Cube, "Carbon", new Vector3(0f, 2.4f + size * 0.5f, 0f), new Vector3(size * 1.4f, size, 0.08f));
            var font = BuiltinFont();
            if (font == null) return;
            var label = new GameObject("Text");
            label.transform.SetParent(root.transform, false);
            label.transform.localPosition = new Vector3(0f, 2.4f + size * 0.5f, -0.05f);
            var tm = label.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = text;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.characterSize = size * 0.09f;
            tm.fontSize = 64;
            tm.color = textColor;
            label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            var mr = panel.GetComponent<MeshRenderer>();
            var mat = new Material(mr.sharedMaterial);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", board);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", board);
            mr.sharedMaterial = mat;
        }

        /// <summary>Numbered corner boards and 150/100/50 braking boards before heavy-braking corners.</summary>
        static void BuildCornerBoards(Transform parent, CircuitInstance ci)
        {
            var g = ci.Geometry;
            var names = ci.Definition.cornerNames;
            var corners = new System.Collections.Generic.List<TrackGeometry.Corner>(ci.Corners);
            float start = ci.Definition.startLineDistance;
            corners.Sort((a, b) => ApexMath.PosMod(g.Distances[a.index] - start, g.Length).CompareTo(ApexMath.PosMod(g.Distances[b.index] - start, g.Length)));
            for (int c = 0; c < corners.Count; c++)
            {
                var corner = corners[c];
                float d = g.Distances[corner.index];
                // Board on the outside of the corner, facing the approaching cars.
                float outside = corner.right ? -1f : 1f;
                float lat = outside * (g.WidthAt(d) * 0.5f + KerbWidth + 5f);
                float bd = d - 40f;
                g.FrameAt(bd, out var p, out var f, out _, lat);
                // TextMesh reads from its -Z side: +Z along the driving direction faces the cars.
                var rot = Quaternion.LookRotation(f.ToUnity(), Vector3.up);
                string label = "T" + (c + 1) + (c < names.Count && !string.IsNullOrEmpty(names[c]) ? "\n" + names[c] : "");
                Sign(parent, p.ToUnity(), rot, label, new Color(0.06f, 0.07f, 0.09f), new Color(0.15f, 0.85f, 0.95f), 1.4f);
                if (corner.angleDeg < 70f || corner.minRadius > 60f) continue;
                foreach (int metres in new[] { 150, 100, 50 })
                {
                    float m = d - 30f - metres;
                    g.FrameAt(m, out var bp, out var bf, out _, outside * (g.WidthAt(m) * 0.5f + KerbWidth + 3.5f));
                    Sign(parent, bp.ToUnity(), Quaternion.LookRotation(bf.ToUnity(), Vector3.up), metres.ToString(), new Color(0.95f, 0.95f, 0.95f), new Color(0.05f, 0.05f, 0.05f), 1.0f);
                }
            }
        }
        /// <summary>
        /// Coarse terrain that follows the track's elevation (inverse-distance
        /// weighted) and stays below the road, plus water east of the shoreline.
        /// </summary>
        static void BuildTerrain(Transform parent, TrackGeometry g, CircuitDefinition def)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue, minY = float.MaxValue;
            foreach (var p in g.Positions)
            {
                minX = Mathf.Min(minX, p.X); maxX = Mathf.Max(maxX, p.X);
                minZ = Mathf.Min(minZ, p.Z); maxZ = Mathf.Max(maxZ, p.Z);
                minY = Mathf.Min(minY, p.Y);
            }
            const float margin = 400f, cell = 12f;
            minX -= margin; maxX += margin; minZ -= margin; maxZ += margin;
            int nx = Mathf.CeilToInt((maxX - minX) / cell) + 1;
            int nz = Mathf.CeilToInt((maxZ - minZ) / cell) + 1;
            int step = Mathf.Max(1, Mathf.RoundToInt(10f / g.Spacing));
            var samples = new List<System.Numerics.Vector3>();
            for (int i = 0; i < g.Count; i += step) samples.Add(g.Positions[i]);
            var shore = def.shoreline;
            float baseLevel = minY - 0.8f;
            var verts = new Vector3[nx * nz];
            var uvs = new Vector2[nx * nz];
            for (int zi = 0; zi < nz; zi++)
            for (int xi = 0; xi < nx; xi++)
            {
                float x = minX + xi * cell, z = minZ + zi * cell;
                float wsum = 0f, hsum = 0f, nearest = float.MaxValue, nearestY = 0f;
                foreach (var s in samples)
                {
                    float dx = s.X - x, dz = s.Z - z;
                    float d2 = dx * dx + dz * dz;
                    float w = 1f / (d2 + 400f);
                    wsum += w;
                    hsum += w * s.Y;
                    if (d2 < nearest) { nearest = d2; nearestY = s.Y; }
                }
                float h = hsum / wsum - 1.2f;
                float d = Mathf.Sqrt(nearest);
                // Never poke through the road corridor.
                if (d < 40f) h = Mathf.Min(h, nearestY - 1.0f);
                h = Mathf.Max(h, baseLevel);
                if (shore != null && shore.Count > 1 && x > ShoreX(shore, z)) h = Mathf.Min(h, -4f);
                verts[zi * nx + xi] = new Vector3(x, h, z);
                uvs[zi * nx + xi] = new Vector2(x / 40f, z / 40f);
            }
            var tris = new int[(nx - 1) * (nz - 1) * 6];
            int t = 0;
            for (int zi = 0; zi < nz - 1; zi++)
            for (int xi = 0; xi < nx - 1; xi++)
            {
                int a = zi * nx + xi, b = a + 1, c = a + nx, d2 = c + 1;
                tris[t++] = a; tris[t++] = c; tris[t++] = d2;
                tris[t++] = a; tris[t++] = d2; tris[t++] = b;
            }
            var mesh = new Mesh { name = "Terrain", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AddMesh(parent, "Terrain", mesh, new[] { "Ground" }, SurfaceType.Grass);
            if (shore != null && shore.Count > 1)
            {
                var water = new MeshData(1);
                for (int i = 0; i < shore.Count - 1; i++)
                {
                    var a = new Vector3(shore[i].east, -0.4f, shore[i].north);
                    var b = new Vector3(shore[i + 1].east, -0.4f, shore[i + 1].north);
                    var a2 = new Vector3(maxX + 3000f, -0.4f, shore[i].north);
                    var b2 = new Vector3(maxX + 3000f, -0.4f, shore[i + 1].north);
                    water.Quad(0, a, a2, b, b2, Vector2.zero, Vector2.right, Vector2.up, Vector2.one);
                }
                AddMesh(parent, "Water", water.ToMesh("Water"), new[] { "Water" }, null);
            }
        }

        static float ShoreX(List<(float east, float north)> shore, float z)
        {
            if (z <= shore[0].north) return shore[0].east;
            for (int i = 0; i < shore.Count - 1; i++)
            {
                if (z <= shore[i + 1].north)
                {
                    float t = (z - shore[i].north) / Mathf.Max(shore[i + 1].north - shore[i].north, 1e-3f);
                    return Mathf.Lerp(shore[i].east, shore[i + 1].east, t);
                }
            }
            return shore[shore.Count - 1].east;
        }

        static void BuildStartLine(Transform parent, CircuitInstance ci)
        {
            var g = ci.Geometry;
            float d = ci.Definition.startLineDistance;
            var pose = ci.PoseAt(d, 0f, 0.012f);
            float w = g.WidthAt(d);
            var tex = new Texture2D(16, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            for (int x = 0; x < 16; x++)
            for (int y = 0; y < 2; y++)
                tex.SetPixel(x, y, (x + y) % 2 == 0 ? Color.white : new Color(0.08f, 0.08f, 0.08f));
            tex.Apply();
            var mat = new Material(ApexMaterials.LitShader()) { name = "StartLine" };
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            var line = new GameObject("StartLine");
            line.transform.SetParent(parent, false);
            line.transform.SetPositionAndRotation(pose.position, pose.rotation * Quaternion.Euler(90f, 0f, 0f));
            line.transform.localScale = new Vector3(w, 1f, 1f);
            line.AddComponent<MeshFilter>().sharedMesh = CarBuilder.PrimitiveMesh(PrimitiveType.Quad);
            var mr = line.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            // Gantry over the line.
            var gantry = new GameObject("Gantry").transform;
            gantry.SetParent(parent, false);
            gantry.SetPositionAndRotation(pose.position, pose.rotation);
            foreach (float side in new[] { -1f, 1f })
                Prop(gantry, PrimitiveType.Cube, "Steel", new Vector3(side * (w * 0.5f + 2.5f), 3.5f, 0f), new Vector3(0.5f, 7f, 0.5f));
            Prop(gantry, PrimitiveType.Cube, "Carbon", new Vector3(0f, 7f, 0f), new Vector3(w + 5.5f, 1.2f, 0.6f));
            for (int k = 0; k < 5; k++)
                Prop(gantry, PrimitiveType.Sphere, "KerbRed", new Vector3((k - 2) * 0.9f, 7f, -0.32f), Vector3.one * 0.44f);
        }

        static Transform Prop(Transform parent, PrimitiveType type, string mat, Vector3 pos, Vector3 scale)
        {
            var go = new GameObject(type.ToString());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = CarBuilder.PrimitiveMesh(type);
            go.AddComponent<MeshRenderer>().sharedMaterial = ApexMaterials.Get(mat);
            return go.transform;
        }
    }
}
