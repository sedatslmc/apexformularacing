using System;
using System.Collections.Generic;
using Apex.Core;
using Apex.Environment;
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
        /// <summary>Shared ground height (terrain, buildings, props).</summary>
        public TerrainField Terrain { get; internal set; }
        /// <summary>Lateral distance of the barrier from the centreline per sample (left / right side).</summary>
        /// <summary>Kerbs, protection, marshal posts, gates and surface details of this circuit.</summary>
        public TrackInfrastructurePlan Infrastructure { get; internal set; }
        /// <summary>The canonical drivable surfaces (road, kerbs, run-off, pit, walls, terrain) this circuit was built from.</summary>
        public RoadSurface Surface { get; internal set; }
        public float[] WallLeft { get; internal set; }
        public float[] WallRight { get; internal set; }
        /// <summary>Keep-out envelope of all track parts: every placed object checks against it.</summary>
        public TrackClearance Clearance { get; internal set; }
        /// <summary>Environment dressing (city, landmarks, water), if the circuit has geo data.</summary>
        public EnvironmentInstance Environment { get; internal set; }
        /// <summary>Event flags placed by the circuit builder (base position, flag length, pole height, detail).</summary>
        public readonly List<(Vector3 pos, float length, float pole, int detail)> EventFlags = new List<(Vector3, float, float, int)>();
        /// <summary>Placement of the event structures (event circuits only).</summary>
        public EventLayout EventLayout { get; internal set; }
        /// <summary>Pit wall team stands (position on the lane, rotation with local +x away from the track, team).</summary>
        public readonly List<(Vector3 pos, Quaternion rot, int team)> PitWallStands = new List<(Vector3, Quaternion, int)>();
        /// <summary>Ground kept free of city buildings for event structures (centre x/z, radius).</summary>
        public readonly List<(float x, float z, float radius)> EventFootprints = new List<(float, float, float)>();

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
        /// <summary>Pit area width from the track edge to the outer pit wall.</summary>
        internal const float PitOuter = RoadSurface.PitOuter;

        /// <summary>Builds a circuit in one go (menu backdrop, tools).</summary>
        public static CircuitInstance Build(CircuitDefinition def, Transform parent = null)
        {
            CircuitInstance result = null;
            var it = BuildSteps(def, parent, ci => result = ci, null);
            while (it.MoveNext()) { }
            return result;
        }

        /// <summary>
        /// Builds a circuit in stages, yielding after each so a loading screen can draw its real progress:
        /// <paramref name="stageDone"/> gets the stage id (Apex.UI.LoadingModel.SessionStages: geometry, surface, pit,
        /// terrain, event, infrastructure, environment).
        /// </summary>
        public static System.Collections.IEnumerator BuildSteps(CircuitDefinition def, Transform parent, Action<CircuitInstance> done, Action<string> stageDone)
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
            ci.Corners = g.CornersFor(def);
            // Racing line (for braking rubber and protection placement) and the infrastructure layout.
            // Fixed layout reference (CarPerformance.LayoutReference), not the car's grip: tuning the car must not move
            // the kerbs, rubber and brake boards.
            var refPerf = Apex.Vehicle.CarPerformance.LayoutReference(new VehicleSetup());
            var line = new Apex.AI.RacingLine(g, refPerf.mu, refPerf.downforcePerMass);
            var plan = new TrackInfrastructurePlan(g, ci.Corners, line.offsets, line.speed);
            ci.Infrastructure = plan;
            TrackInfrastructureBuilder.EnsureMaterials();
            stageDone?.Invoke("geometry");
            yield return null;

            // All drivable surfaces and their colliders come from the one canonical RoadSurface, the same
            // triangles the offline drive tests and the road validation use.
            var surface = new RoadSurface(g, def, plan.kerbs.left, plan.kerbs.right, plan.kerbs.height);
            ci.Surface = surface;
            ci.Clearance = new TrackClearance(surface);
            var pitMask = surface.pitMask;
            AddSurface(root.transform, surface.road, "Asphalt", "Line");
            AddSurface(root.transform, surface.kerbs, "KerbRed", "KerbWhite");
            AddSurface(root.transform, surface.runoff, g.RunoffLeft != null ? "RunoffAsphalt" : "Grass");
            // Collision walls; the visible barriers come from TrackInfrastructureBuilder.
            AddSurface(root.transform, surface.barriers, "Wall", "WallStripe").GetComponent<MeshRenderer>().enabled = false;
            TerrainSurface.Register();
            AddSurface(root.transform, surface.skirt, "Ground");
            ci.WallLeft = surface.wallLeft;
            ci.WallRight = surface.wallRight;
            stageDone?.Invoke("surface");
            yield return null;
            if (def.pit != null)
            {
                AddSurface(root.transform, surface.pitApron, "Asphalt");
                AddSurface(root.transform, surface.pitLane, "PitLane", "PitWorkLane");
                // Collision solid only: the visible wall, catch fence, crash cushion and team stands come from PitWallBuilder.
                AddSurface(root.transform, surface.pitWall, "Wall").GetComponent<MeshRenderer>().enabled = false;
                BuildPitLane(root.transform, ci);
                stageDone?.Invoke("pit");
                yield return null;
            }
            ci.Terrain = new TerrainField(g, def);
            AddSurface(root.transform, surface.BuildTerrain(ci.Terrain), ci.Terrain.land.HasLand ? "FarGround" : "Ground");
            BuildWater(root.transform, ci.Terrain, def);
            BuildStartLine(root.transform, ci);
            stageDone?.Invoke("terrain");
            yield return null;
            if (EventBrandingBuilder.IsEvent(def))
            {
                Dressing("event identity", () => EventBrandingBuilder.Build(ci, root.transform));
                stageDone?.Invoke("event");
                yield return null;
            }
            BuildGrid(root.transform, ci);
            BuildCornerBoards(root.transform, ci);
            Dressing("infrastructure", () => TrackInfrastructureBuilder.Build(ci, root.transform, pitMask));
            stageDone?.Invoke("infrastructure");
            yield return null;
            if (def.landMasses.Count > 0)
            {
                Dressing("environment", () => EnvironmentBuilder.Build(ci, root.transform));
                stageDone?.Invoke("environment");
            }
            done?.Invoke(ci);
        }

        /// <summary>
        /// Builds optional dressing; an exception there is logged instead of aborting the circuit,
        /// so road, walls, pit lane and grid (everything needed to race) are always there.
        /// </summary>
        static void Dressing(string what, Action build)
        {
            try { build(); }
            catch (Exception e)
            {
                Debug.LogError($"[Apex] Circuit {what} failed, continuing without it: {e.Message}");
                Debug.LogException(e);
            }
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

        /// <summary>Unity mesh (visual) and MeshCollider (when the surface has a physical type) from a SurfaceMesh, vertex for vertex.</summary>
        internal static GameObject AddSurface(Transform parent, SurfaceMesh sm, params string[] materials)
        {
            var m = new Mesh { name = sm.name, indexFormat = IndexFormat.UInt32 };
            var v = new List<Vector3>(sm.vertices.Count);
            foreach (var p in sm.vertices) v.Add(p.ToUnity());
            var uv = new List<Vector2>(sm.uvs.Count);
            foreach (var t in sm.uvs) uv.Add(new Vector2(t.X, t.Y));
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.subMeshCount = sm.triangles.Length;
            for (int i = 0; i < sm.triangles.Length; i++) m.SetTriangles(sm.triangles[i], i);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return AddMesh(parent, sm.name, m, materials, sm.surface);
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

        /// <summary>Box markings, lane markings, pit wall and garage block (apron, lane and the wall's collider come from RoadSurface).</summary>
        static void BuildPitLane(Transform parent, CircuitInstance ci)
        {
            var g = ci.Geometry;
            var pit = ci.Pit;
            float side = pit.side;
            BuildBoxMarkings(parent, ci);
            BuildPitLaneMarkings(parent, ci);
            PitWallBuilder.Build(parent, ci);
            // Garage block behind the outer pit wall (event circuits get the full pit building instead).
            if (EventBrandingBuilder.IsEvent(ci.Definition)) return;
            float start = pit.BoxDistance(0) - 15f, end = pit.LastBox + 15f;
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

        /// <summary>
        /// The exact stop position of each box, built to the car (VehicleSetup axles and tracks, the same numbers the
        /// crew works to): yellow box outline, white marks where the four tyres stand, a stop bar in front of each front
        /// tyre, an alignment line in the team colour leading in, the box number painted ahead of the car and the team
        /// stripe at the garage end.
        /// </summary>
        static void BuildBoxMarkings(Transform parent, CircuitInstance ci)
        {
            var g = ci.Geometry;
            var pit = ci.Pit;
            float side = pit.side;
            var setup = new VehicleSetup();
            float fz = setup.FrontAxleZ, rz = setup.RearAxleZ, tf = setup.trackFront * 0.5f, tr = setup.trackRear * 0.5f;
            var yellow = new MeshData(1);
            var white = new MeshData(1);
            var teams = new MeshData(Roster.Teams.Length);
            var keys = TeamMaterials();
            for (int b = 0; b < pit.boxCount; b++)
            {
                float d = g.Wrap(pit.BoxDistance(b));
                float lat = ci.PitLaneLateral(d);
                int team = pit.TeamOfBox(b, keys.Length);
                Frame(yellow, g, d, lat, 7.2f, Apex.Race.PitLaneLayout.BoxWidth, 0.15f);
                // Tyre marks (x = right of the car, + is towards the garage when the pit is on the right).
                foreach (var (z, x) in new[] { (fz, tf), (fz, -tf), (rz, tr), (rz, -tr) })
                {
                    float l = lat + x;
                    Box(white, g, d + z - 0.36f, d + z + 0.36f, l - 0.22f, l + 0.22f, 0.06f);
                    if (z == fz) PatchOrdered(white, g, d + z + 0.42f, d + z + 0.56f, l - 0.3f, l + 0.3f);   // stop bar
                }
                // Alignment line in the team colour leading in to the box centre, dashed.
                for (float z = -11f; z < -3.4f; z += 1.6f)
                    PatchOrdered(teams, g, d + z, d + z + 0.9f, lat - 0.06f, lat + 0.06f, team);
                // Team stripe at the garage end.
                float inner = lat + side * (Apex.Race.PitLaneLayout.BoxWidth * 0.5f + 0.15f), outer = inner + side * 0.6f;
                PatchOrdered(teams, g, d - 2.8f, d + 2.8f, Mathf.Min(inner, outer), Mathf.Max(inner, outer), team);
                // Box number ahead of the car, read by the driver coming in.
                PaintText(white, g, (b + 1).ToString(), d + 4.4f, lat, 1.3f);
            }
            AddMesh(parent, "PitBoxes", yellow.ToMesh("PitBoxes"), new[] { "Marker" }, null);
            AddMesh(parent, "PitBoxMarks", white.ToMesh("PitBoxMarks"), new[] { "Line" }, null);
            AddMesh(parent, "PitTeamStripes", teams.ToMesh("PitTeamStripes"), keys, null);
        }

        /// <summary>Team colour materials PitTeam0..n (registered once).</summary>
        internal static string[] TeamMaterials()
        {
            var keys = new string[Roster.Teams.Length];
            for (int t = 0; t < keys.Length; t++)
            {
                var team = Roster.Teams[t];
                keys[t] = "PitTeam" + t;
                ApexMaterials.Register(keys[t], new Material(EnvironmentMaterials.Solid(new Color(team.r, team.g, team.b))) { name = keys[t] });
            }
            return keys;
        }

        /// <summary>
        /// Lane markings (docs/PIT_LANE.md, Apex.Race.PitLaneLayout):
        /// <list type="bullet">
        /// <item>Entry: solid white line along the track edge from where the entry lane branches off to the wall's nose,
        /// the lane's own boundary line and white chevrons in the gore between them; "PIT" painted in the lane.</item>
        /// <item>Speed limit lines across the lane, the limit painted after the first and "END" after the second.</item>
        /// <item>The divider between fast lane and working lane along the pit wall.</item>
        /// <item>Exit: the lane boundary and chevrons from the end of the wall to the merge point, then a dashed edge
        /// line along the merge.</item>
        /// </list>
        /// </summary>
        static void BuildPitLaneMarkings(Transform parent, CircuitInstance ci)
        {
            var g = ci.Geometry;
            var pit = ci.Pit;
            float side = pit.side;
            var plan = pit.PlanFor(g.Length);
            var lines = new MeshData(1);
            float Edge(float s) => g.WidthAt(g.Wrap(pit.entry + s)) * 0.5f;
            float D(float s) => g.Wrap(pit.entry + s);
            // Lateral (signed) of a distance from the edge.
            float Lat(float s, float fromEdge) => side * (Edge(s) + fromEdge);
            void Strip(float s0, float s1, float e0, float e1)
            {
                float a = Lat(s0, e0), b = Lat(s0, e1);
                PatchOrdered(lines, g, D(s0), D(s1), Mathf.Min(a, b), Mathf.Max(a, b));
            }
            // Divider between fast lane and working lane, in 4 m pieces (follows the curve).
            const float div = Apex.Race.PitLaneLayout.Divider;
            for (float s = 0f; s < plan.WallEnd; s += 4f) Strip(s, Mathf.Min(s + 4f, plan.WallEnd), div - 0.075f, div + 0.075f);
            // Speed limit lines across the lane (wall to the outer edge): limit on after the entry, off at the exit line.
            float wallStart = Apex.Race.PitLaneLayout.WallStart;
            foreach (float at in new[] { (float)Apex.Race.PitLaneLayout.LimiterStart, plan.limitSpan })
                Strip(at, at + 0.5f, Apex.Race.PitLaneLayout.WallOuter, PitOuter - 0.3f);
            PaintText(lines, g, pit.speedLimitKmh.ToString("0"), D(Apex.Race.PitLaneLayout.LimiterStart + 3f), Lat(0f, Apex.Race.PitLaneLayout.FastLaneCentre), 1.5f);
            PaintText(lines, g, "END", D(plan.limitSpan + 3f), Lat(plan.limitSpan, Apex.Race.PitLaneLayout.FastLaneCentre), 1.2f);
            // Solid edge line where the lane runs beside the track without the wall.
            void EdgeLine(float s0, float s1, bool dashed = false)
            {
                for (float s = s0; s < s1; s += dashed ? 6f : 4f)
                    Strip(s, Mathf.Min(s + (dashed ? 3f : 4f), s1), 0.1f, 0.3f);
            }
            // Entry: edge line from where the lane branches off to the crash cushion at the wall's nose.
            float entryFrom = -pit.entryTaper, nose = wallStart - Apex.Race.PitLaneLayout.AttenuatorLength;
            EdgeLine(entryFrom, nose);
            // The car's path in the entry lane and its inner boundary (1.6 m inside it); chevrons in the gore.
            float Path(float s) => Apex.Race.PitLaneLayout.LaneTarget(1f + s / pit.entryTaper);
            Gore(lines, g, pit, entryFrom, nose, Path, +1);
            PaintText(lines, g, "PIT", D(entryFrom + pit.entryTaper * 0.55f), Lat(entryFrom + pit.entryTaper * 0.55f, Path(-pit.entryTaper * 0.45f)), 1.5f);
            // Exit: from the end of the wall the lane runs beside the track behind its boundary line to the merge point,
            // then the edge line is dashed along the merge.
            float ExitPath(float s) => s <= plan.MergeStart ? Apex.Race.PitLaneLayout.FastLaneCentre : Apex.Race.PitLaneLayout.Route(plan, s, -100f, true, pit.laneCentreFromEdge);
            EdgeLine(plan.WallEnd, plan.MergeStart);
            Gore(lines, g, pit, plan.WallEnd, plan.MergeStart, ExitPath, -1);
            EdgeLine(plan.MergeStart, plan.MergeEnd, true);
            AddMesh(parent, "PitLaneLines", lines.ToMesh("PitLaneLines"), new[] { "Line" }, null);
        }

        /// <summary>
        /// Gore between the track edge line and a lane's inner boundary (path − 1.6 m) between two distances after the
        /// entry: the boundary as a solid line and white chevrons pointing the way the cars go (<paramref name="dir"/>
        /// +1 entry: the lane leaves the track; −1 exit: it joins).
        /// </summary>
        static void Gore(MeshData md, TrackGeometry g, CircuitDefinition.PitLane pit, float s0, float s1, Func<float, float> path, int dir)
        {
            float side = pit.side;
            float Edge(float s) => g.WidthAt(g.Wrap(pit.entry + s)) * 0.5f;
            Vector3 Pt(float s, float fromEdge) =>
                (g.PositionAt(g.Wrap(pit.entry + s)) + g.RightAt(g.Wrap(pit.entry + s)) * (side * (Edge(s) + fromEdge))).ToUnity() + Vector3.up * 0.013f;
            float Bound(float s) => path(s) - 1.6f;
            // Boundary line where the gore is at least 0.4 m wide.
            for (float s = s0; s < s1; s += 2f)
            {
                float e = Mathf.Min(s + 2f, s1);
                if (Bound(s) < 0.7f || Bound(e) < 0.7f) continue;
                QuadUp(md, Pt(s, Bound(s) - 0.1f), Pt(s, Bound(s) + 0.1f), Pt(e, Bound(e) - 0.1f), Pt(e, Bound(e) + 0.1f));
            }
            // Chevrons every 5 m: a 0.35 m wide diagonal bar from the edge line to the boundary.
            for (float s = s0 + 2f; s < s1 - 2f; s += 5f)
            {
                float b = Bound(s);
                if (b < 0.9f) continue;
                float run = Mathf.Min(3f, b) * dir;   // the bar leans along the driving direction
                QuadUp(md, Pt(s, 0.35f), Pt(s + 0.35f, 0.35f), Pt(s + run, b - 0.15f), Pt(s + run + 0.35f, b - 0.15f));
            }
        }

        /// <summary>Quad on the road from two edges (a–b first, c–e second), turned to face up (MeshData.Quad wants b right
        /// of a and c ahead of a, i.e. cross(b − a, c − a) pointing down).</summary>
        static void QuadUp(MeshData md, Vector3 a, Vector3 b, Vector3 c, Vector3 e)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), Vector3.up) > 0f) { var t = b; b = c; c = t; }
            md.Quad(0, a, b, c, e, Vector2.zero, Vector2.right, Vector2.up, Vector2.one);
        }

        /// <summary>
        /// Lettering painted on the road (Apex.UI.PixelFont), centred on (<paramref name="d"/>, <paramref name="lat"/>),
        /// <paramref name="height"/> m tall, reading for a driver coming along the track (top of the letters ahead).
        /// </summary>
        static void PaintText(MeshData md, TrackGeometry g, string text, float d, float lat, float height)
        {
            float px = height / Apex.UI.PixelFont.H;
            float width = Apex.UI.PixelFont.Width(text) * px;
            foreach (var (x, y, len) in Apex.UI.PixelFont.Runs(text))
            {
                // Row 0 is the top of the letters: furthest ahead along the track; columns run left to right across it.
                float s1 = d + height * 0.5f - y * px, s0 = s1 - px;
                float l0 = lat - width * 0.5f + x * px, l1 = l0 + len * px;
                PatchOrdered(md, g, s0, s1, l0, l1);
            }
        }

        /// <summary>A filled rectangle with an outline gap (tyre mark): outline of <paramref name="lw"/> m.</summary>
        static void Box(MeshData md, TrackGeometry g, float d0, float d1, float l0, float l1, float lw)
        {
            PatchOrdered(md, g, d0, d0 + lw, l0, l1);
            PatchOrdered(md, g, d1 - lw, d1, l0, l1);
            PatchOrdered(md, g, d0, d1, l0, l0 + lw);
            PatchOrdered(md, g, d0, d1, l1 - lw, l1);
        }

        /// <summary><see cref="Patch"/> with raw (unwrapped) distances.</summary>
        static void PatchOrdered(MeshData md, TrackGeometry g, float d0, float d1, float l0, float l1, int sub = 0) =>
            Patch(md, g, g.Wrap(d0), g.Wrap(d1), l0, l1, sub);

        /// <summary>Flat quad on the road between two distances and two lateral offsets, 1.2 cm up.</summary>
        static void Patch(MeshData md, TrackGeometry g, float d0, float d1, float l0, float l1, int sub = 0)
        {
            const float lift = 0.013f;
            var a = (g.PositionAt(d0) + g.RightAt(d0) * l0).ToUnity() + Vector3.up * lift;
            var b = (g.PositionAt(d0) + g.RightAt(d0) * l1).ToUnity() + Vector3.up * lift;
            var c = (g.PositionAt(d1) + g.RightAt(d1) * l0).ToUnity() + Vector3.up * lift;
            var e = (g.PositionAt(d1) + g.RightAt(d1) * l1).ToUnity() + Vector3.up * lift;
            md.Quad(sub, a, b, c, e, Vector2.zero, Vector2.right, Vector2.up, Vector2.one);
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

        public static Font BuiltinFont()
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
                // Boards stand just behind the barrier on the outside.
                float[] wallSide = outside < 0 ? ci.WallLeft : ci.WallRight;
                float lat = outside * (wallSide[g.IndexAt(d - 40f)] + 1.2f);
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
                    g.FrameAt(m, out var bp, out var bf, out _, outside * (wallSide[g.IndexAt(m)] + 0.9f));
                    Sign(parent, bp.ToUnity(), Quaternion.LookRotation(bf.ToUnity(), Vector3.up), metres.ToString(), new Color(0.95f, 0.95f, 0.95f), new Color(0.05f, 0.05f, 0.05f), 1.0f);
                }
            }
        }
        /// <summary>
        /// Coarse terrain from the shared <see cref="TerrainField"/> (follows the track's
        /// elevation, stays below the road), plus water east of the shoreline. With geo
        /// land masses the far landscape and the strait are built by the environment.
        /// </summary>
        /// <summary>Water plane beyond a schematic shoreline (circuits without DEM land).</summary>
        static void BuildWater(Transform parent, TerrainField field, CircuitDefinition def)
        {
            float maxX = field.MaxX;
            bool far = field.land.HasLand;
            var shore = def.shoreline;
            if (!far && shore != null && shore.Count > 1)
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
            // Gantry over the line with working start lights.
            if (EventBrandingBuilder.IsEvent(ci.Definition))
            {
                EventBrandingBuilder.BuildGantry(parent, ci);
                return;
            }
            var gantry = new GameObject("Gantry").transform;
            gantry.SetParent(parent, false);
            gantry.SetPositionAndRotation(pose.position, pose.rotation);
            foreach (float side in new[] { -1f, 1f })
                Prop(gantry, PrimitiveType.Cube, "Steel", new Vector3(side * (w * 0.5f + 2.5f), 3.5f, 0f), new Vector3(0.5f, 7f, 0.5f));
            Prop(gantry, PrimitiveType.Cube, "Carbon", new Vector3(0f, 7f, 0f), new Vector3(w + 5.5f, 1.2f, 0.6f));
            var lights = gantry.gameObject.AddComponent<StartLightsComponent>();
            for (int k = 0; k < 5; k++)
            {
                var lamp = Prop(gantry, PrimitiveType.Sphere, "KerbRed", new Vector3((k - 2) * 0.9f, 7f, -0.32f), Vector3.one * 0.44f);
                var lampMat = StartLightsComponent.LampMaterial();
                lamp.GetComponent<MeshRenderer>().sharedMaterial = lampMat;
                lights.Add(lampMat);
            }
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
