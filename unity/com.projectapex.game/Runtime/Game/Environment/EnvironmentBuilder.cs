using System;
using System.Collections.Generic;
using Apex.Core;
using Apex.Environment;
using Apex.Race;
using Apex.Tracks;
using UnityEngine;
using UnityEngine.Rendering;

namespace Apex.Game
{
    /// <summary>
    /// Dresses a geo-referenced circuit (Bosphorus GP): far landscape and strait,
    /// quay walls, landmark silhouettes at their mapped positions, procedural city
    /// blocks, the detailed start/finish waterfront (grandstands, hospitality,
    /// pavements, catch fencing, lamps, Turkish street signs, fictional sponsors),
    /// trees, flags, ferries and ships, spectators, gulls and ambience zones.
    /// Everything is generated placeholder art; nothing here is photorealistic.
    /// Existing race systems (road, walls, pit, timing) are not touched.
    /// </summary>
    public static class EnvironmentBuilder
    {
        /// <summary>0 = low (mobile), 1 = medium, 2 = high, 3 = extreme (PC: more life, further); set from the settings
        /// before building. Geometry detail uses 0..2; 3 raises budgets and draw distances.</summary>
        public static int Quality = 1;

        const float WaterY = -0.4f;
        /// <summary>Tower wash spot: illuminance intensity / d² gives ~0.6 on the legs 60 m up (night exposure).</summary>
        const float BridgeWashIntensity = 2200f;
        /// <summary>Façade floodlights (same units as the bridge wash: about 4 at the façade, ten times a lit road).</summary>
        const float MosqueWashIntensity = 1100f, EsmaWashIntensity = 600f;

        /// <summary>A night-only spot light from `from` aimed at `to` (no shadows), switched with the city lights.</summary>
        static void Floodlight(Transform parent, EnvironmentInstance env, Vector3 from, Vector3 to, Color colour, float intensity, float angle)
        {
            var go = new GameObject("Floodlight");
            go.transform.SetParent(parent, false);
            go.transform.position = from;
            go.transform.rotation = Quaternion.LookRotation((to - from).normalized);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.spotAngle = angle;
            l.range = Vector3.Distance(from, to) * 2.5f + 10f;
            l.color = colour;
            l.shadows = LightShadows.None;
            env.AddRealLight(l, intensity);
        }
        const float ChunkSize = 384f;

        public static EnvironmentInstance Build(CircuitInstance ci, Transform parent)
        {
            var def = ci.Definition;
            var g = ci.Geometry;
            var field = ci.Terrain;
            int q = Mathf.Clamp(Quality, 0, 2);
            var root = new GameObject("Environment");
            root.transform.SetParent(parent, false);
            var env = root.AddComponent<EnvironmentInstance>();
            ci.Environment = env;
            env.quality = q;
            env.wind = WindField.ForCircuit(def.trueNorthBearing);
            env.land = field.land;
            Safe("materials", () => EnvironmentMaterials.Ensure(env));

            float cx = (field.MinX + field.MaxX) * 0.5f, cz = (field.MinZ + field.MaxZ) * 0.5f;
            Safe("water and far land", () => BuildWaterAndFarLand(root.transform, env, field, def, cx, cz));
            var reserved = new List<CityLayout.Reserved>();
            // Event buildings (pit building, timing tower, race control) keep stands and city away.
            foreach (var (x, z, r) in ci.EventFootprints) reserved.Add(new CityLayout.Reserved(x, z, r));
            // TV camera towers behind the barrier (TrackInfrastructureBuilder.BuildTvTowers): no tree or building there.
            foreach (var t in ci.Infrastructure.tvTowers)
            {
                var tp = ci.Infrastructure.TvTowerPosition(t, ci.WallLeft, ci.WallRight);
                reserved.Add(new CityLayout.Reserved(tp.X, tp.Y, TrackInfrastructurePlan.TvTowerClearance));
            }
            Safe("waterfront", () => BuildWaterfront(root.transform, env, ci, reserved));
            // Monumental Turkish flag (site from the circuit file, re-checked against the built circuit): keep the city away.
            bool monumental = false;
            if (def.monumentalFlag.HasValue)
            {
                var (fx, fz) = def.monumentalFlag.Value;
                monumental = MonumentalFlagSite.Validate(field, g, ci.Clearance, fx, fz, out _, out _);
                if (monumental) reserved.Add(new CityLayout.Reserved(fx, fz, MonumentalFlagSite.ReservedRadius));
                else Debug.LogWarning($"Monumental flag site {fx:F0},{fz:F0} is not clear of the circuit; not placed.");
            }
            // The real city (OSM footprints, streets, green) where the circuit has city data; generic blocks beyond it.
            var cityText = Resources.Load<TextAsset>("City/" + def.cityId);
            CityData cityData = null;
            if (cityText != null)
            {
                try { cityData = CityData.FromJson(cityText.text); }
                catch (System.Exception e) { Debug.LogError("[Apex] Invalid city data " + def.cityId + ": " + e.Message); }
            }
            OsmCity osm = null;
            if (cityData != null) Safe("OSM city data", () => osm = new OsmCity(cityData, field, ci.Clearance, def, reserved, q));
            var opts = new CityLayout.Options { quality = q, realCityRadius = osm != null ? cityData.radius : 0f };
            opts.reserved.AddRange(reserved);
            var city = new CityLayout(field, def, opts);
            var allLots = new List<CityLayout.Lot>(city.lots);
            if (osm != null)
            {
                Safe("OSM buildings", () => BuildOsmCity(root.transform, osm, env));
                Safe("streets", () => BuildStreets(root.transform, new StreetNetwork(cityData, field, ci.Clearance, q)));
                CityLife life = null;
                Safe("city life", () =>
                {
                    // Extreme (PC, Quality 3): more people and traffic, drawn further.
                    bool extreme = Quality >= 3;
                    life = new CityLife(cityData, field, ci.Clearance, osm, q, extreme ? 2600 : q == 0 ? 300 : q == 1 ? 900 : 1600, extreme ? 360 : q == 0 ? 80 : q == 1 ? 160 : 240);
                    BuildCityLife(root.transform, life, extreme ? 3 : q);
                });
                if (osm.OrtakoyCentre.HasValue) Safe("trackside crowd", () => BuildTracksideCrowd(root.transform, env, ci, osm, life, Mathf.Clamp(Quality, 0, 3), reserved));
                allLots.AddRange(osm.AsLots());
                city.trees.AddRange(osm.trees);
                Debug.Log($"[Apex] Real city: {osm.buildings.Count} buildings, {osm.trees.Count} trees (OSM).");
            }
            Safe("waterfront edge", () => BuildWaterEdge(root.transform, env, ci, osm));
            Safe("generic city", () => BuildCity(root.transform, city));
            Safe("trees", () => BuildTrees(root.transform, env, city));
            // Turkish flags on selected rooftops near the circuit, gathered around the focal points.
            foreach (var f in new RooftopFlagLayout(allLots, field, def, g, q).flags)
                env.flagSpots.Add((V(f.position), f.length, f.pole, 1));
            bool realPalaces = osm != null && osm.buildings.Exists(b => b.classical == OsmCity.PalaceWing);
            Safe("landmarks", () => BuildLandmarks(root.transform, env, ci, realPalaces));
            if (monumental) Safe("monumental flag", () => MonumentalFlagBuilder.Build(root.transform, env, field, def.monumentalFlag.Value.east, def.monumentalFlag.Value.north));
            Safe("trackside", () => BuildTrackside(root.transform, env, ci));
            Safe("ships", () => BuildShips(root.transform, env, def, field.land));
            Safe("gulls", () => BuildGulls(root.transform, env, ci));
            Safe("ambience", () => BuildAmbience(root.transform, env, ci, allLots));
            return env;
        }

        // --- Helpers ------------------------------------------------------------------------

        /// <summary>
        /// Runs one part of the environment build; an exception is logged and the rest still builds (a failing
        /// texture or model must not leave the circuit without its city, water and landmarks).
        /// </summary>
        static void Safe(string part, System.Action build)
        {
            try { build(); }
            catch (System.Exception e)
            {
                Debug.LogError($"[Apex] Environment part '{part}' failed and was skipped: {e.Message}");
                Debug.LogException(e);
            }
        }

        internal static Mesh Combine(Dictionary<string, MeshBuilder> parts, string name, out Material[] materials)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var subs = new List<List<int>>();
            var mats = new List<Material>();
            foreach (var kv in parts)
            {
                var mb = kv.Value;
                if (mb.TriangleCount == 0) continue;
                int b = verts.Count;
                verts.AddRange(mb.Vertices);
                uvs.AddRange(mb.UVs);
                var t = new List<int>(mb.Triangles.Count);
                foreach (int i in mb.Triangles) t.Add(i + b);
                subs.Add(t);
                mats.Add(ApexMaterials.Get(kv.Key));
            }
            var mesh = new Mesh { name = name };
            if (verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = subs.Count;
            for (int i = 0; i < subs.Count; i++) mesh.SetTriangles(subs[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            materials = mats.ToArray();
            return mesh;
        }

        internal static GameObject AddMeshObject(Transform parent, string name, Dictionary<string, MeshBuilder> parts, bool shadows, bool isStatic = true)
        {
            var mesh = Combine(parts, name, out var mats);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            go.isStatic = isStatic;
            return go;
        }

        sealed class PartSet
        {
            public readonly Dictionary<string, MeshBuilder> parts = new Dictionary<string, MeshBuilder>();
            public MeshBuilder B(string key)
            {
                if (!parts.TryGetValue(key, out var mb)) parts[key] = mb = new MeshBuilder();
                return mb;
            }
            public int Triangles
            {
                get { int n = 0; foreach (var p in parts.Values) n += p.TriangleCount; return n; }
            }
        }

        static Vector3 V(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);

        static Vector3 TrackPoint(TrackGeometry g, int i, float lateral, float dy) => V(g.Positions[i] + g.Rights[i] * lateral) + Vector3.up * dy;

        // --- Water, far land, quays -----------------------------------------------------------

        static void BuildWaterAndFarLand(Transform root, EnvironmentInstance env, TerrainField field, CircuitDefinition def, float cx, float cz)
        {
            float extent = Quality >= 3 ? 9000f : Quality >= 2 ? 7200f : Quality == 1 ? 6400f : 5600f;
            var far = new MeshBuilder();
            EnvironmentGeometry.FarTerrain(far, field, cx, cz, extent);
            var parts = new Dictionary<string, MeshBuilder> { [EnvironmentGeometry.FarGround] = far };
            AddMeshObject(root, "FarLand", parts, false);
            // One water plane for the strait, the Golden Horn and the Sea of Marmara.
            var water = new MeshBuilder();
            float e = extent + 2000f;
            water.QuadFacing(new Vector3(cx - e, WaterY, cz - e), new Vector3(cx + e, WaterY, cz - e), new Vector3(cx + e, WaterY, cz + e), new Vector3(cx - e, WaterY, cz + e),
                new Vector2(cx - e, cz - e) / 24f, new Vector2(cx + e, cz - e) / 24f, new Vector2(cx + e, cz + e) / 24f, new Vector2(cx - e, cz + e) / 24f, Vector3.up);
            var wgo = AddMeshObject(root, "Bosphorus", new Dictionary<string, MeshBuilder> { ["EnvWater"] = water }, false);
            var wc = wgo.AddComponent<WaterSurfaceComponent>();
            wc.env = env;
            // Shore distance for shallow tint and foam: 6 km around the circuit, 5.9 m (Low: 11.7 m) cells.
            const float coastSize = 6000f;
            int res = Quality == 0 ? 512 : 1024;
            float minX = cx - coastSize * 0.5f, minZ = cz - coastSize * 0.5f;
            var coast = CoastDistanceField.Build(minX, minZ, coastSize, res, field.RasterizeLand);
            var coastTex = new Texture2D(res, res, TextureFormat.R8, false, true)
            {
                name = "CoastDistance", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            coastTex.SetPixelData(coast.Encode(WaterOptics.CoastMaxDistance), 0);
            coastTex.Apply(false, true);
            wc.SetCoast(coastTex, new Vector4(minX, minZ, 1f / coastSize, 1f / coastSize));
            // Without land outlines the simple quay along the shorelines; with them BuildWaterEdge follows the coastline.
            if (field.land.HasLand) return;
            var quay = new MeshBuilder();
            EnvironmentGeometry.QuayWall(quay, def.shoreline, true);
            if (def.farShoreline.Count > 1) EnvironmentGeometry.QuayWall(quay, def.farShoreline, false);
            AddMeshObject(root, "QuayWalls", new Dictionary<string, MeshBuilder> { [EnvironmentGeometry.Quay] = quay }, false);
        }

        /// <summary>
        /// Quays and promenade along the real coastline (Waterfront), chunked so distant pieces cull; the promenade
        /// keeps clear of buildings and of the landmark terraces (Ortaköy Mosque).
        /// </summary>
        static void BuildWaterEdge(Transform root, EnvironmentInstance env, CircuitInstance ci, OsmCity osm)
        {
            var def = ci.Definition;
            var mosque = def.FindLandmark("ortakoy_mosque");
            bool Blocked(float x, float z)
            {
                if (osm != null && osm.InsideBuilding(x, z, 0f)) return true;
                if (mosque != null && (x - mosque.east) * (x - mosque.east) + (z - mosque.north) * (z - mosque.north) < 45f * 45f) return true;
                return false;
            }
            System.Func<float, float, bool> palaceFront = null;
            if (osm != null && osm.PalaceFront.HasPalace) palaceFront = osm.PalaceFront.OnFrontage;
            var wf = new Waterfront(ci.Terrain, ci.Clearance, Blocked, palaceFront);
            if (palaceFront != null) osm.PalaceFront.PlaceGates(wf);
            var edgeRoot = new GameObject("Waterfront").transform;
            edgeRoot.SetParent(root, false);
            const float size = 256f;
            var chunks = new Dictionary<(int, int), Waterfront>();
            // Split by chunk: one Waterfront-shaped bucket per cell (segments, bollards, lamps).
            Waterfront Bucket(float x, float z)
            {
                var key = (Mathf.FloorToInt(x / size), Mathf.FloorToInt(z / size));
                if (!chunks.TryGetValue(key, out var b)) { chunks[key] = b = Waterfront.Empty(); b.openings.AddRange(wf.openings); }
                return b;
            }
            foreach (var sgm in wf.segments) Bucket(sgm.a.X, sgm.a.Y).segments.Add(sgm);
            foreach (var p in wf.bollards) Bucket(p.X, p.Z).bollards.Add(p);
            foreach (var l in wf.lamps) Bucket(l.foot.X, l.foot.Z).lamps.Add(l);
            foreach (var kv in chunks)
            {
                var parts = new PartSet();
                EnvironmentGeometry.WaterfrontEdge(parts.B, kv.Value, Mathf.Clamp(Quality, 0, 2));
                var go = AddMeshObject(edgeRoot, $"Waterfront_{kv.Key.Item1}_{kv.Key.Item2}", parts.parts, true);
                var lg = go.AddComponent<LODGroup>();
                lg.SetLODs(new[] { new LOD(0.01f, new Renderer[] { go.GetComponent<Renderer>() }) });
                lg.RecalculateBounds();
            }
            // Dolmabahçe's five sea gates on the palace quay, lit warm from the quay at night (High and above).
            if (palaceFront != null)
                for (int i = 0; i < osm.PalaceFront.gates.Count; i++)
                {
                    var g = osm.PalaceFront.gates[i];
                    var pos = new Vector3(g.position.X, ci.Terrain.Height(g.position.X, g.position.Y), g.position.Y);
                    float yaw = Mathf.Atan2(g.seaward.X, g.seaward.Y) * Mathf.Rad2Deg;
                    bool main = g.main;
                    PlaceDetailed(edgeRoot, "dolmabahce_sea_gate_" + i, "Dolmabahçe " + g.name, main ? 11f : 8.5f, lg => lg.SeaGate(main), pos, yaw, PalaceLods);
                    if (Quality >= 2)
                    {
                        var sea = new Vector3(g.seaward.X, 0f, g.seaward.Y);
                        Floodlight(edgeRoot, env, pos + sea * 6f + Vector3.up * 0.4f, pos + Vector3.up * (main ? 6f : 4.5f), new Color(1f, 0.82f, 0.6f), EsmaWashIntensity, 50f);
                    }
                }
            Debug.Log($"[Apex] Waterfront: {wf.quayLength / 1000f:F1} km quay, {wf.promenadeLength / 1000f:F1} km promenade, {wf.palaceLength / 1000f:F2} km palace quay, " +
                $"{(palaceFront != null ? osm.PalaceFront.gates.Count : 0)} sea gates, {wf.bollards.Count} bollards, {wf.lamps.Count} lamps.");
        }

        // --- City -------------------------------------------------------------------------------

        static void BuildCity(Transform root, CityLayout city)
        {
            var cityRoot = new GameObject("City").transform;
            cityRoot.SetParent(root, false);
            // Chunk lots so distant chunks cull and near chunks switch to simple boxes.
            var chunks = new Dictionary<(int, int, int), List<CityLayout.Lot>>();
            foreach (var lot in city.lots)
            {
                int band = lot.band == 0 ? 0 : 1;
                float size = band == 0 ? ChunkSize : ChunkSize * 3f;
                var key = (Mathf.FloorToInt(lot.x / size), Mathf.FloorToInt(lot.z / size), band);
                if (!chunks.TryGetValue(key, out var list)) chunks[key] = list = new List<CityLayout.Lot>();
                list.Add(lot);
            }
            foreach (var kv in chunks)
            {
                string name = $"Block_{kv.Key.Item1}_{kv.Key.Item2}_{kv.Key.Item3}";
                var detailed = new PartSet();
                foreach (var lot in kv.Value) EnvironmentGeometry.Building(detailed.B, lot, kv.Key.Item3 == 0);
                if (kv.Key.Item3 != 0)
                {
                    AddMeshObject(cityRoot, name, detailed.parts, false);
                    continue;
                }
                var simple = new PartSet();
                foreach (var lot in kv.Value)
                {
                    var flat = lot;
                    flat.roof = lot.roof == 2 ? 2 : 0;
                    EnvironmentGeometry.Building(simple.B, flat, false);
                }
                var group = new GameObject(name);
                group.transform.SetParent(cityRoot, false);
                var lod0 = AddMeshObject(group.transform, "LOD0", detailed.parts, true);
                var lod1 = AddMeshObject(group.transform, "LOD1", simple.parts, false);
                var lg = group.AddComponent<LODGroup>();
                lg.SetLODs(new[]
                {
                    new LOD(0.18f, new Renderer[] { lod0.GetComponent<Renderer>() }),
                    new LOD(0.004f, new Renderer[] { lod1.GetComponent<Renderer>() }),
                });
                lg.RecalculateBounds();
            }
        }

        /// <summary>
        /// The city's side streets (StreetNetwork): visual only, no colliders; older city asphalt (the scanned
        /// run-off set), paver pavements and footways, concrete kerbs and steps, centre dashes; each chunk is
        /// culled when it covers less than 1.5 % of the screen height.
        /// </summary>
        static void BuildStreets(Transform root, StreetNetwork net)
        {
            var streetRoot = new GameObject("Streets").transform;
            streetRoot.SetParent(root, false);
            foreach (var chunk in net.chunks.Values)
            {
                if (chunk.TriangleCount == 0) continue;
                var go = CircuitBuilder.AddSurface(streetRoot, chunk, "RunoffAsphalt", EnvironmentGeometry.Pavement, EnvironmentGeometry.Concrete, "Line", EnvironmentGeometry.Pavement, EnvironmentGeometry.Concrete);
                var lg = go.AddComponent<LODGroup>();
                lg.SetLODs(new[] { new LOD(0.015f, new Renderer[] { go.GetComponent<Renderer>() }) });
                lg.RecalculateBounds();
            }
            Debug.Log($"[Apex] Streets: {net.roadLength / 1000f:F1} km roads, {net.footLength / 1000f:F1} km footways, {net.steps} steps.");
        }

        /// <summary>Street furniture, parked cars, pedestrians and traffic (CityLife), drawn instanced near the camera.</summary>
        static void BuildCityLife(Transform root, CityLife life, int q)
        {
            var go = new GameObject("CityLife");
            go.transform.SetParent(root, false);
            var c = go.AddComponent<CityLifeComponent>();
            c.drawDistance = q == 0 ? 150f : q == 1 ? 250f : q == 2 ? 350f : 520f;
            c.shadowDistance = q == 0 ? 0f : q == 1 ? 80f : q == 2 ? 140f : 220f;
            c.Setup(life);
            Debug.Log($"[Apex] City life: {life.props.Count} props, {life.parked.Count} parked cars, {life.pedestrians.Count} pedestrians, {life.vehicles.Count} vehicles.");
        }

        /// <summary>
        /// Standing fans behind the barriers of the Ortaköy section (TracksideCrowd), drawn like the grandstand crowd;
        /// many wave Turkish hand flags and they cheer when cars pass.
        /// </summary>
        static void BuildTracksideCrowd(Transform root, EnvironmentInstance env, CircuitInstance ci, OsmCity osm, CityLife life, int q, List<CityLayout.Reserved> reserved)
        {
            var fans = new List<TracksideCrowd.Fan>();
            var props = life != null ? life.props : null;
            var crowd = new TracksideCrowd(ci.Geometry, ci.Clearance, ci.Terrain, osm, props, osm.OrtakoyCentre.Value, StreetFronts.OrtakoyRadius, q, reserved);
            fans.AddRange(crowd.fans);
            int ortakoy = fans.Count;
            // Viewing zones at the heavily braked corners elsewhere on the lap: standing fans in groups.
            foreach (var z in SpectatorZones.Centres(ci.Geometry, ci.Infrastructure, osm.OrtakoyCentre.Value, StreetFronts.OrtakoyRadius, q))
                fans.AddRange(new TracksideCrowd(ci.Geometry, ci.Clearance, ci.Terrain, osm, props, z, SpectatorZones.Radius, q, reserved, SpectatorZones.GroupScale).fans);
            if (fans.Count == 0) return;
            var go = new GameObject("TracksideCrowd");
            go.transform.SetParent(root, false);
            var c = go.AddComponent<CrowdComponent>();
            c.env = env;
            c.handFlagShare = 0.45f;
            // Photographers: no cheering, a camera in front of the face.
            var cameras = new MeshBuilder();
            int photographers = 0;
            foreach (var f in fans)
            {
                if (f.photographer)
                {
                    // Same height and facing as the instanced figure: body and lens at eye level, in front of the face.
                    var p = V(f.position);
                    float h = CrowdComponent.Height(f.rnd), yaw = CrowdComponent.Facing(f.yaw * Mathf.Rad2Deg, f.rnd) * Mathf.Deg2Rad;
                    var fwd = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
                    var eye = p + Vector3.up * (0.88f * h);
                    cameras.Box(eye + fwd * (0.075f * h + 0.06f), new Vector3(0.14f, 0.1f, 0.12f), Quaternion.LookRotation(fwd));
                    cameras.Box(eye + fwd * (0.075f * h + 0.18f), new Vector3(0.08f, 0.08f, 0.14f), Quaternion.LookRotation(fwd));
                    photographers++;
                }
                c.Add(V(f.position), f.yaw * Mathf.Rad2Deg, f.rnd, f.photographer);
            }
            if (photographers > 0) AddMeshObject(go.transform, "PhotographerCameras", new Dictionary<string, MeshBuilder> { [EnvironmentGeometry.DarkSteel] = cameras }, false);
            Debug.Log($"[Apex] Trackside crowd: {fans.Count} fans ({ortakoy} at Ortaköy, {photographers} photographers).");
        }

        /// <summary>LOD screen heights of the palace buildings (per building, they are up to 140 m long).</summary>
        static readonly float[] PalaceLods = { 0.3f, 0.08f, 0.01f };
        static readonly float[] RowLods = { 0.12f, 0.03f, 0.004f };

        /// <summary>
        /// The palaces on their OSM footprints with modelled classical facades (LandmarkGeometry.ClassicalBuilding),
        /// the dome over Dolmabahçe's ceremonial hall, and the clock tower; each building with three LODs.
        /// </summary>
        static void BuildClassical(Transform root, OsmCity osm, EnvironmentInstance env)
        {
            var palaceRoot = new GameObject("Palaces").transform;
            palaceRoot.SetParent(root, false);
            int seed = 0, count = 0;
            foreach (var b in osm.buildings)
            {
                if (b.classical == OsmCity.NotClassical) continue;
                var c = System.Numerics.Vector2.Zero;
                foreach (var p in b.outline) c += p;
                c /= b.outline.Count;
                var local = new List<Vector2>(b.outline.Count);
                foreach (var p in b.outline) local.Add(new Vector2(p.X - c.X, p.Y - c.Y));
                var pos = new Vector3(c.X, b.ground, c.Y);
                string name = string.IsNullOrEmpty(b.name) ? "Palace building" : b.name;
                int s = seed++;
                if (b.classical == OsmCity.RowHouse)
                {
                    // Street front: the edge whose midpoint lies nearest to a street.
                    int front = 0;
                    float near = float.MaxValue;
                    for (int i = 0; i < b.outline.Count; i++)
                    {
                        var m = (b.outline[i] + b.outline[(i + 1) % b.outline.Count]) * 0.5f;
                        float d = osm.StreetDistance(m.X, m.Y);
                        if (d < near) { near = d; front = i; }
                    }
                    float h = b.height;
                    PlaceDetailed(palaceRoot, "osm_classical_" + s, "Akaretler row house", h, g => g.RowHouse(local, h, front, s), pos, 0f, RowLods);
                }
                else if (b.classical == OsmCity.Gate)
                {
                    // Long axis of the footprint along the model's x; width and depth from its extents.
                    var (yaw, width, depth) = FootprintAxes(local);
                    float h = b.height;
                    PlaceDetailed(palaceRoot, "osm_classical_" + s, name, h, g => g.GateMonument(width, depth, h), pos, yaw, PalaceLods);
                }
                else if (b.classical == OsmCity.BrickShell)
                {
                    float h = b.height;
                    PlaceDetailed(palaceRoot, "osm_classical_" + s, name, h, g => g.BrickShellYali(local, h, s), pos, 0f, PalaceLods);
                    // Night: warm uplights on the brick from the quay and street side (High and above).
                    if (Quality >= 2)
                        for (int i = 0; i < b.outline.Count; i++)
                        {
                            var a0 = b.outline[i]; var a1 = b.outline[(i + 1) % b.outline.Count];
                            var e = a1 - a0;
                            if (e.Length() < 12f) continue;
                            var m = (a0 + a1) * 0.5f;
                            var outward = new Vector3(e.Y, 0f, -e.X) / e.Length();
                            var at = new Vector3(m.X, b.ground + 0.6f + h * 0.55f, m.Y);
                            Floodlight(palaceRoot, env, at + outward * 7f - Vector3.up * (h * 0.55f - 0.5f), at, new Color(1f, 0.8f, 0.55f), EsmaWashIntensity, 55f);
                        }
                }
                else if (b.classical == OsmCity.ClockTower)
                {
                    // Faces square to the longest footprint edge.
                    float best = 0f, yaw = 0f;
                    for (int i = 0; i < local.Count; i++)
                    {
                        var d = local[(i + 1) % local.Count] - local[i];
                        if (d.magnitude > best) { best = d.magnitude; yaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg; }
                    }
                    PlaceDetailed(palaceRoot, LandmarkAssets.DolmabahceClockTowerId, name, 27f, g => g.ClockTowerDetailed(), pos + Vector3.up * 0.6f, yaw, PalaceLods);
                }
                else
                {
                    bool palace = b.classical == OsmCity.PalaceWing;
                    bool hall = b.name != null && b.name.Contains("Muayede");
                    float h = b.height;
                    PlaceDetailed(palaceRoot, "osm_classical_" + s, name, h, g =>
                    {
                        g.ClassicalBuilding(local, h, s, palace);
                        if (hall) g.HallDome(h, 11f);
                    }, pos, 0f, PalaceLods);
                }
                count++;
            }
            Debug.Log($"[Apex] Palaces: {count} classical buildings on their OSM footprints.");
        }

        /// <summary>Yaw (deg) that turns model +x onto the footprint's longest edge, and the extents along and across it.</summary>
        static (float yaw, float width, float depth) FootprintAxes(List<Vector2> o)
        {
            float best = 0f; Vector2 dir = Vector2.right;
            for (int i = 0; i < o.Count; i++)
            {
                var d = o[(i + 1) % o.Count] - o[i];
                if (d.magnitude > best) { best = d.magnitude; dir = d.normalized; }
            }
            float minU = float.MaxValue, maxU = float.MinValue, minV = float.MaxValue, maxV = float.MinValue;
            foreach (var p in o)
            {
                float u = Vector2.Dot(p, dir), v = -p.x * dir.y + p.y * dir.x;
                minU = Mathf.Min(minU, u); maxU = Mathf.Max(maxU, u); minV = Mathf.Min(minV, v); maxV = Mathf.Max(maxV, v);
            }
            // Model x maps to (cos yaw, -sin yaw) in (east, north).
            return (Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg, maxU - minU, maxV - minV);
        }

        /// <summary>The OSM buildings in chunks with two LODs (detailed: cornices, parapets, roof tanks).</summary>
        static void BuildOsmCity(Transform root, OsmCity osm, EnvironmentInstance env)
        {
            var cityRoot = new GameObject("RealCity").transform;
            cityRoot.SetParent(root, false);
            var chunks = new Dictionary<(int, int), List<OsmCity.Building>>();
            const float size = 192f;
            BuildClassical(root, osm, env);
            foreach (var b in osm.buildings)
            {
                if (b.classical != OsmCity.NotClassical) continue;
                var p = b.outline[0];
                var key = (Mathf.FloorToInt(p.X / size), Mathf.FloorToInt(p.Y / size));
                if (!chunks.TryGetValue(key, out var list)) chunks[key] = list = new List<OsmCity.Building>();
                list.Add(b);
            }
            foreach (var kv in chunks)
            {
                var detailed = new PartSet();
                var simple = new PartSet();
                var fronts = new PartSet();
                foreach (var b in kv.Value)
                {
                    EnvironmentGeometry.FootprintBuilding(detailed.B, b, true);
                    EnvironmentGeometry.FacadeOrnaments(fronts.B, b, Mathf.Clamp(Quality, 0, 2));
                    EnvironmentGeometry.FootprintBuilding(simple.B, b, false);
                }
                var group = new GameObject($"Real_{kv.Key.Item1}_{kv.Key.Item2}");
                group.transform.SetParent(cityRoot, false);
                var lod0 = AddMeshObject(group.transform, "LOD0", detailed.parts, true);
                var lod1 = AddMeshObject(group.transform, "LOD1", simple.parts, true);
                var lg = group.AddComponent<LODGroup>();
                var r0 = lod0.GetComponent<Renderer>();
                // Hanging flags in their own mesh, moved by the wind (FlagWindComponent).
                Renderer flagRenderer = null;
                if (fronts.parts.TryGetValue(EnvironmentGeometry.BalconyFlag, out var flagParts) && flagParts.TriangleCount > 0)
                {
                    fronts.parts.Remove(EnvironmentGeometry.BalconyFlag);
                    var fg = AddMeshObject(group.transform, "Flags", new Dictionary<string, MeshBuilder> { [EnvironmentGeometry.BalconyFlag] = flagParts }, false, false);
                    fg.AddComponent<FlagWindComponent>().env = env;
                    flagRenderer = fg.GetComponent<Renderer>();
                }
                if (fronts.Triangles > 0)
                {
                    // Street-front relief (balconies, bay windows, awnings) only on the near level: it reads up to
                    // about 400 m; beyond, the facade texture alone carries the windows.
                    var rel = AddMeshObject(group.transform, "Fronts", fronts.parts, true);
                    lg.SetLODs(new[]
                    {
                        new LOD(0.35f, flagRenderer != null ? new Renderer[] { r0, rel.GetComponent<Renderer>(), flagRenderer } : new Renderer[] { r0, rel.GetComponent<Renderer>() }),
                        new LOD(0.12f, new Renderer[] { r0 }),
                        new LOD(0.003f, new Renderer[] { lod1.GetComponent<Renderer>() }),
                    });
                }
                else
                    lg.SetLODs(new[]
                    {
                        new LOD(0.12f, new Renderer[] { r0 }),
                        new LOD(0.003f, new Renderer[] { lod1.GetComponent<Renderer>() }),
                    });
                lg.RecalculateBounds();
            }
        }

        static void BuildTrees(Transform root, EnvironmentInstance env, CityLayout city)
        {
            var go = new GameObject("Trees");
            go.transform.SetParent(root, false);
            var trees = go.AddComponent<TreeSwayComponent>();
            trees.env = env;
            var crowns = new[] { EnvironmentGeometry.Foliage, EnvironmentGeometry.Pine, EnvironmentGeometry.Cypress };
            for (int kind = 0; kind < 3; kind++)
            {
                var trunk = new MeshBuilder();
                var crown = new MeshBuilder();
                EnvironmentGeometry.TreeMesh(kind, trunk, crown);
                var mesh = Combine(new Dictionary<string, MeshBuilder> { [EnvironmentGeometry.Bark] = trunk, [crowns[kind]] = crown }, "Tree" + kind, out var mats);
                trees.AddKind(mesh, mats);
            }
            int stride = Quality == 0 ? 2 : 1;
            for (int i = 0; i < city.trees.Count; i += stride)
            {
                var t = city.trees[i];
                trees.Add(t.kind, new Vector3(t.x, t.ground - 0.2f, t.z), t.height, LandModel.Hash(i, 7) * 180f);
            }
        }

        // --- Landmarks --------------------------------------------------------------------------

        /// <summary>Moves a point inland until it has the given clearance from the coast.</summary>
        static Vector3 OnLand(TerrainField field, Vector3 p, Vector3 inland, float clearance)
        {
            for (float s = 0f; s < 400f; s += 5f)
            {
                var x = p + inland * s;
                if (!field.IsWater(x.x, x.z) && field.land.CoastDistance(x.x, x.z) >= clearance) return x;
            }
            return p;
        }

        static void Place(Transform root, CircuitDefinition.Landmark l, LandmarkGeometry lg, Vector3 pos, float yawDeg, bool shadows) =>
            Place(root, l.id, l.name, l.height, lg, pos, yawDeg, shadows);

        /// <summary>Places the authored model Resources/Landmarks/&lt;id&gt; if there is one, else the procedural placeholder.</summary>
        static void Place(Transform root, string id, string name, float refHeight, LandmarkGeometry lg, Vector3 pos, float yawDeg, bool shadows)
        {
            if (PlaceAuthored(root, id, name, refHeight, pos, yawDeg)) return;
            var go = AddMeshObject(root, name, lg.parts, shadows);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yawDeg, 0f));
        }

        /// <summary>LOD screen heights of the detailed mosque (40 m tall: LOD0 to about 140 m, LOD1 to about 580 m at 60° FOV).</summary>
        static readonly float[] OrtakoyLods = { 0.25f, 0.06f, 0.004f };

        /// <summary>
        /// Places an in-house detailed landmark (LandmarkModels) with three LODs built from the same code at
        /// detail 0, 1 and 2, unless an authored model exists under Resources/Landmarks/&lt;id&gt;.
        /// </summary>
        static void PlaceDetailed(Transform root, string id, string name, float refHeight, System.Action<LandmarkGeometry> build, Vector3 pos, float yawDeg, float[] screenHeights)
        {
            if (PlaceAuthored(root, id, name, refHeight, pos, yawDeg)) return;
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yawDeg, 0f));
            go.isStatic = true;
            var lods = new LOD[3];
            for (int d = 0; d < 3; d++)
            {
                var lg = new LandmarkGeometry(d);
                build(lg);
                lg.MasonryUVs();
                var child = AddMeshObject(go.transform, name + "_LOD" + d, lg.parts, d < 2);
                lods[d] = new LOD(screenHeights[d], new Renderer[] { child.GetComponent<Renderer>() });
            }
            var group = go.AddComponent<LODGroup>();
            group.SetLODs(lods);
            group.RecalculateBounds();
        }

        static bool PlaceAuthored(Transform root, string id, string name, float refHeight, Vector3 pos, float yawDeg)
        {
            var prefab = Resources.Load<GameObject>(LandmarkAssets.ResourceFolder + "/" + id);
            if (prefab == null) return false;
            var go = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.Euler(0f, yawDeg, 0f), root);
            go.name = name;
            float top = float.NegativeInfinity;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) top = Mathf.Max(top, r.bounds.max.y);
            if (!float.IsNegativeInfinity(top))
            {
                string warn = LandmarkAssets.CheckHeight(id, refHeight, top - pos.y);
                if (warn != null) Debug.LogWarning(warn);
            }
            return true;
        }

        /// <summary>Game-frame yaw (deg) of a true bearing.</summary>
        static float TrueToGame(CircuitDefinition def, float trueBearing) =>
            ApexMath.PosMod(trueBearing + (float.IsNaN(def.trueNorthBearing) ? 0f : def.trueNorthBearing), 360f);

        static float ShoreYaw(List<(float east, float north)> shore, float z)
        {
            float a = TerrainField.ShoreX(shore, z - 60f), b = TerrainField.ShoreX(shore, z + 60f);
            return Mathf.Atan2(b - a, 120f) * Mathf.Rad2Deg;
        }

        static void BuildLandmarks(Transform root, EnvironmentInstance env, CircuitInstance ci, bool realCity)
        {
            var def = ci.Definition;
            var field = ci.Terrain;
            var lmRoot = new GameObject("Landmarks").transform;
            lmRoot.SetParent(root, false);
            Vector3 P(CircuitDefinition.Landmark l) => new Vector3(l.east, field.Height(l.east, l.north), l.north);
            // Qibla from Istanbul is about 151° true; mosque axes follow it.
            float qibla = TrueToGame(def, 151f);
            var eu = def.FindLandmark("bridge_eu");
            var asia = def.FindLandmark("bridge_as");
            if (eu != null && asia != null)
            {
                var a = P(eu); var b = P(asia);
                var mid = new Vector3((a.x + b.x) * 0.5f, WaterY, (a.z + b.z) * 0.5f);
                float bridgeYaw = Mathf.Atan2(b.x - a.x, b.z - a.z) * Mathf.Rad2Deg;
                if (!PlaceAuthored(lmRoot, LandmarkAssets.BridgeId, "Bridge_15Temmuz", LandmarkAssets.BridgeTowerHeight, mid, bridgeYaw))
                {
                    var lg = new LandmarkGeometry(0);
                    lg.Bridge(a, b, WaterY);
                    AddMeshObject(lmRoot, "Bridge_15Temmuz", lg.parts, true);
                }
                // Night lighting in red and white (LandmarkGeometry.BridgeIllumination; also with an authored model,
                // which shares the bridge's real dimensions): red cables, white hangers, tower bands and deck dots.
                var lights = new MeshBuilder();
                var red = new MeshBuilder();
                var white = new MeshBuilder();
                foreach (float lat in new[] { -17.2f, 17.2f })
                    foreach (var p in LandmarkGeometry.BridgeLightPath(a, b, WaterY, lat))
                        lights.Box(p + Vector3.up * 1.2f, new Vector3(0.9f, 0.5f, 0.9f));
                var wash = LandmarkGeometry.BridgeIllumination(a, b, WaterY, red, white);
                AddMeshObject(lmRoot, "BridgeLights", new Dictionary<string, MeshBuilder>
                {
                    [EnvironmentGeometry.BridgeLight] = lights, [EnvironmentGeometry.BridgeRed] = red, [EnvironmentGeometry.BridgeWhite] = white,
                }, false);
                // Real lights: four white spots wash the tower legs from deck level (no shadows).
                foreach (var w in wash)
                {
                    var go = new GameObject("BridgeTowerWash");
                    go.transform.SetParent(lmRoot, false);
                    go.transform.position = w;
                    go.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
                    var l = go.AddComponent<Light>();
                    l.type = LightType.Spot;
                    l.spotAngle = 24f;
                    l.range = 120f;
                    l.color = new Color(1f, 0.97f, 0.92f);
                    l.shadows = LightShadows.None;
                    env.AddRealLight(l, BridgeWashIntensity);
                }
            }
            foreach (var l in def.landmarks)
            {
                var pos = P(l);
                var lg = new LandmarkGeometry(l.kind == "mosque" || l.kind == "palace" ? 0 : 1);
                switch (l.id)
                {
                    case "ortakoy_mosque":
                    {
                        // In-house detailed model on its own quay terrace, axis and plan from the OSM footprint.
                        float yaw = LandmarkGeometry.OrtakoyAxis;
                        pos += Quaternion.Euler(0f, yaw, 0f) * LandmarkGeometry.OrtakoyHallOffset;
                        pos.y = Mathf.Max(field.Height(pos.x, pos.z), 1.6f);
                        PlaceDetailed(lmRoot, l.id, l.name, l.height, g => g.OrtakoyMosqueDetailed(), pos, yaw, OrtakoyLods);
                        // Night: the mosque floodlit warm white from the terrace (three free hall facades, the
                        // pavilion front and the dome), as on the Bosphorus at night. High and above.
                        if (Quality >= 2)
                        {
                            var rot = Quaternion.Euler(0f, yaw, 0f);
                            var warm = new Color(1f, 0.88f, 0.7f);
                            foreach (var (from, to) in new[]
                            {
                                (new Vector3(0f, 0.5f, 19f), new Vector3(0f, 11f, 10.6f)), (new Vector3(19f, 0.5f, 0f), new Vector3(10.6f, 11f, 0f)),
                                (new Vector3(-19f, 0.5f, 0f), new Vector3(-10.6f, 11f, 0f)), (new Vector3(0f, 0.5f, -33f), new Vector3(0f, 7f, -19.8f)),
                                (new Vector3(13f, 0.5f, 13f), new Vector3(0f, 26f, 0f)),
                            })
                                Floodlight(lmRoot, env, pos + rot * from, pos + rot * to, warm, MosqueWashIntensity, 70f);
                        }
                        break;
                    }
                    case "dolmabahce":
                    {
                        // The palace and its clock tower stand on their OSM footprints (BuildClassical); with the
                        // procedural city (no OSM data) the simple massing remains.
                        float yaw = ShoreYaw(def.shoreline, l.north);
                        pos = OnLand(field, pos, Vector3.left, 40f);
                        pos.y = field.Height(pos.x, pos.z);
                        if (!realCity)
                        {
                            lg.Palace(284f, 48f, 18f, true);
                            Place(lmRoot, l, lg, pos, yaw, true);
                            var tower = new LandmarkGeometry(0);
                            tower.ClockTower(27f);
                            var tp = OnLand(field, new Vector3(pos.x, 0f, pos.z - 170f), Vector3.left, 25f);
                            tp.y = field.Height(tp.x, tp.z);
                            Place(lmRoot, LandmarkAssets.DolmabahceClockTowerId, "Dolmabahçe-Uhrturm", 27f, tower, tp, yaw, true);
                        }
                        env.flagSpots.Add((pos + Quaternion.Euler(0f, yaw, 0f) * new Vector3(30f, 0f, 0f), 5f, 24f, 0));
                        break;
                    }
                    case "ciragan":
                        if (realCity) break;
                        lg.Palace(160f, 30f, 14f, false);
                        pos = OnLand(field, pos, Vector3.left, 28f);
                        pos.y = field.Height(pos.x, pos.z);
                        Place(lmRoot, l, lg, pos, ShoreYaw(def.shoreline, l.north), true);
                        break;
                    case "beylerbeyi":
                        lg.Palace(65f, 40f, 15f, false);
                        pos = OnLand(field, pos, Vector3.right, 30f);
                        pos.y = field.Height(pos.x, pos.z);
                        Place(lmRoot, l, lg, pos, ShoreYaw(def.farShoreline, l.north), false);
                        break;
                    case "galata":
                        lg.GalataTower(l.height);
                        Place(lmRoot, l, lg, pos, 0f, false);
                        env.flagSpots.Add((pos + Vector3.up * (l.height + 0.5f), 2.4f, 4f, 2));
                        break;
                    case "maidens_tower":
                        lg.MaidensTower();
                        Place(lmRoot, l, lg, new Vector3(pos.x, WaterY, pos.z), TrueToGame(def, 90f), false);
                        env.flagSpots.Add((new Vector3(pos.x + 4f, WaterY + 20f, pos.z + 2f), 1.8f, 4f, 2));
                        break;
                    case "sultan_ahmed":
                        lg.ImperialMosque(11.7f, 26f, 6, 64f, LandmarkGeometry.Stone);
                        Place(lmRoot, l, lg, pos, qibla, false);
                        break;
                    case "hagia_sophia":
                        lg.ImperialMosque(15.5f, 34f, 4, 60f, LandmarkGeometry.HagiaPlaster);
                        Place(lmRoot, l, lg, pos, TrueToGame(def, 122f), false);
                        break;
                }
            }
        }

        // --- Start/finish waterfront prototype ----------------------------------------------------

        /// <summary>
        /// Detailed 500 m around the start line: grandstands between the barrier and the
        /// quay (spectators, LED boards, large flags), paved paddock with hospitality
        /// units behind the garages, plus grandstands outside the heavy-braking corners
        /// where there is room.
        /// </summary>
        static void BuildWaterfront(Transform root, EnvironmentInstance env, CircuitInstance ci, List<CityLayout.Reserved> reserved)
        {
            var g = ci.Geometry;
            var field = ci.Terrain;
            var def = ci.Definition;
            var wf = new GameObject("Waterfront").transform;
            wf.SetParent(root, false);
            var stands = new PartSet();
            var seats = new List<(Vector3 pos, float yaw)>();
            int rows = Quality == 0 ? 9 : 12;
            // Main stands on the side opposite the pit, along the start straight.
            int standSide = def.pit != null ? -def.pit.side : 1;
            float start = ci.Definition.startLineDistance;
            var standEnds = new List<Vector3>();
            for (float d = start - 170f; d < start + 330f; d += 68f)
                TryStand(ci, stands, seats, standEnds, reserved, g.Wrap(d), 60f, standSide, rows, true);
            // Spectator stands outside heavy-braking corners.
            foreach (var c in ci.Corners)
            {
                if (c.angleDeg < 70f) continue;
                float d = g.Distances[c.index];
                TryStand(ci, stands, seats, standEnds, reserved, g.Wrap(d - 90f), 48f, c.right ? -1 : 1, rows - 3, true);
            }
            AddMeshObject(wf, "Grandstands", stands.parts, true);
            env.grandstandCentres.AddRange(standEnds);
            // Large flags at the ends of the first and last main stand.
            if (standEnds.Count >= 2)
            {
                env.flagSpots.Add((standEnds[0] + Vector3.up * 0.5f, 6f, 22f, 0));
                env.flagSpots.Add((standEnds[Mathf.Min(standEnds.Count - 1, 5)] + Vector3.up * 0.5f, 6f, 22f, 0));
            }
            // Spectators.
            var crowdGo = new GameObject("Crowd");
            crowdGo.transform.SetParent(wf, false);
            var crowd = crowdGo.AddComponent<CrowdComponent>();
            crowd.env = env;
            float fill = Quality == 0 ? 0.35f : Quality == 1 ? 0.6f : Quality == 2 ? 0.9f : 1f;
            for (int i = 0; i < seats.Count; i++)
                if ((LandModel.Hash(i, 3) * 0.5f + 0.5f) < fill) crowd.Add(seats[i].pos, seats[i].yaw, LandModel.Hash(i, 9));
            // Paddock and hospitality behind the garages.
            if (def.pit != null) BuildPaddock(wf, env, ci, reserved);
        }

        static void TryStand(CircuitInstance ci, PartSet stands, List<(Vector3 pos, float yaw)> seats, List<Vector3> ends, List<CityLayout.Reserved> reserved,
            float d, float length, int side, int rows, bool roof)
        {
            var g = ci.Geometry;
            var field = ci.Terrain;
            int i0 = g.IndexAt(d), i1 = g.IndexAt(d + length);
            float wall = Mathf.Max(side < 0 ? ci.WallLeft[i0] : ci.WallRight[i0], side < 0 ? ci.WallLeft[i1] : ci.WallRight[i1]);
            float front = wall + 2.5f, depthTotal = rows * 0.85f + 0.6f;
            // Local frame: +x along the track, +z away from it.
            var a = TrackPoint(g, i0, side * front, 0f);
            var b = TrackPoint(g, i1, side * front, 0f);
            var along = b - a; along.y = 0f;
            float len = along.magnitude;
            if (len < length * 0.85f) return; // too curved here
            var ax = along / len;
            var away = Vector3.Cross(Vector3.up, ax) * side;
            // Every footprint corner must be on land, off the quay edge and clear of other track parts.
            foreach (var p in new[] { a, b, a + away * depthTotal, b + away * depthTotal, (a + b) * 0.5f + away * depthTotal })
            {
                if (field.IsWater(p.x, p.z) || field.land.CoastDistance(p.x, p.z) < 4f) return;
                if (field.TrackDistance(p.x, p.z, 60f, out _) < g.Widths[i0] * 0.5f + 18f) return;
            }
            var mid = (a + b) * 0.5f + away * depthTotal * 0.5f;
            foreach (var r in reserved)
                if ((new Vector2(mid.x - r.x, mid.z - r.z)).magnitude < r.radius + length * 0.5f) return;
            float baseY = Mathf.Min(a.y, b.y) - 0.15f;
            // LookRotation(away) maps local +z to away and local +x to cross(up, away),
            // which is -along on the right side and +along on the left side.
            var origin = side > 0 ? new Vector3(b.x, baseY, b.z) : new Vector3(a.x, baseY, a.z);
            var rot = Quaternion.LookRotation(away, Vector3.up);
            var local = new PartSet();
            var seatLocal = EnvironmentGeometry.Grandstand(local.B, len, rows, roof);
            foreach (var kv in local.parts)
            {
                var mb = kv.Value;
                mb.TransformLast(mb.VertexCount, v => origin + rot * v);
                Append(stands.B(kv.Key), mb);
            }
            float facing = Quaternion.LookRotation(-away, Vector3.up).eulerAngles.y;
            foreach (var s in seatLocal) seats.Add((origin + rot * s, facing));
            ends.Add(origin + rot * new Vector3(-2.5f, 0f, depthTotal * 0.5f));
            ends.Add(origin + rot * new Vector3(len + 2.5f, 0f, depthTotal * 0.5f));
            reserved.Add(new CityLayout.Reserved(mid.x, mid.z, length * 0.5f + 8f));
        }

        /// <summary>Appends the geometry of src to dst (same material).</summary>
        static void Append(MeshBuilder dst, MeshBuilder src)
        {
            var v = src.Vertices; var uv = src.UVs; var t = src.Triangles;
            for (int i = 0; i < t.Count; i += 3)
                dst.TriangleFacing(v[t[i]], v[t[i + 1]], v[t[i + 2]], uv[t[i]], uv[t[i + 1]], uv[t[i + 2]],
                    Vector3.Cross(v[t[i + 2]] - v[t[i]], v[t[i + 1]] - v[t[i]]));
        }

        static void BuildPaddock(Transform wf, EnvironmentInstance env, CircuitInstance ci, List<CityLayout.Reserved> reserved)
        {
            var g = ci.Geometry;
            var pit = ci.Pit;
            float side = pit.side;
            var parts = new PartSet();
            float d0 = pit.BoxDistance(0) - 20f, d1 = pit.BoxDistance(19) + 20f;
            // Paved paddock strip behind the garages, with a low skirt down to the terrain.
            for (float d = d0; d < d1; d += 4f)
            {
                int i = g.IndexAt(d), j = g.IndexAt(d + 4f);
                float hw = g.Widths[i] * 0.5f;
                float inner = hw + 13.5f + 11f, outer = hw + 13.5f + 46f;
                var a = TrackPoint(g, i, side * inner, -0.05f); var b = TrackPoint(g, i, side * outer, -0.05f);
                var c = TrackPoint(g, j, side * outer, -0.05f); var e = TrackPoint(g, j, side * inner, -0.05f);
                parts.B(EnvironmentGeometry.Pavement).QuadFacing(a, b, c, e, new Vector2(a.x, a.z) / 3f, new Vector2(b.x, b.z) / 3f, new Vector2(c.x, c.z) / 3f, new Vector2(e.x, e.z) / 3f, Vector3.up);
                var outward = (b - a).normalized;
                parts.B(EnvironmentGeometry.Concrete).QuadFacing(b, c, c + Vector3.down * 4f, b + Vector3.down * 4f, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, outward);
            }
            // Three hospitality units, 3 storeys, facing the garages.
            int units = 3;
            float span = d1 - d0;
            for (int u = 0; u < units; u++)
            {
                float dm = d0 + span * (u + 0.5f) / units;
                int i = g.IndexAt(dm);
                float hw = g.Widths[i] * 0.5f;
                float lat = side * (hw + 13.5f + 22f);
                g.FrameAt(g.Wrap(dm), out var p, out var f, out _, lat);
                var fwd = V(f); fwd.y = 0f; fwd.Normalize();
                var away = Vector3.Cross(Vector3.up, fwd) * side;
                float len = Mathf.Min(34f, span / units - 8f);
                var rot = Quaternion.LookRotation(away, Vector3.up);
                var origin = V(p) + Vector3.down * 0.05f - rot * new Vector3(len * 0.5f, 0f, 0f);
                var local = new PartSet();
                EnvironmentGeometry.Hospitality(local.B, len, 15f, 3);
                foreach (var kv in local.parts)
                {
                    kv.Value.TransformLast(kv.Value.VertexCount, v => origin + rot * v);
                    Append(parts.B(kv.Key), kv.Value);
                }
                var mid = origin + rot * new Vector3(len * 0.5f, 0f, 7.5f);
                reserved.Add(new CityLayout.Reserved(mid.x, mid.z, len * 0.5f + 14f));
                env.flagSpots.Add((origin + rot * new Vector3(1f, 3 * 3.6f + 0.2f, -0.6f), 2.4f, 6f, 1));
            }
            // Keep the city off the whole paddock strip.
            for (float d = d0; d < d1; d += 30f)
            {
                g.FrameAt(g.Wrap(d), out var p, out _, out _, side * (g.WidthAt(d) * 0.5f + 13.5f + 28f));
                reserved.Add(new CityLayout.Reserved(p.X, p.Z, 26f));
            }
            AddMeshObject(wf, "Paddock", parts.parts, true);
        }

        // --- Trackside: pavements, catch fences, lamps, signs, sponsors, flags ---------------------

        static readonly string[] Sponsors =
        {
            // Fictional brands created for Project Apex (no real companies).
            "ALTINBOĞAZ ENERJİ", "MAVİ HAT LOJİSTİK", "KUZEY RÜZGARI", "LALEZAR AIR", "SİMİTÇİ & CO", "APEX GP", "MERİDYEN TELEKOM", "YALI SİGORTA",
        };

        static readonly (string text, float x, float z)[] StreetSigns =
        {
            // Real street names at their approximate places along the circuit.
            ("Dolmabahçe Cd.", 10f, 60f),
            ("Beşiktaş Meydanı", -10f, 300f),
            ("Çırağan Cd.", 220f, 830f),
            ("Muallim Naci Cd.", 380f, 1420f),
            ("Yıldız Posta Cd.", -360f, 520f),
            ("Barbaros Blv.", -150f, 40f),
        };

        static void BuildTrackside(Transform root, EnvironmentInstance env, CircuitInstance ci)
        {
            var g = ci.Geometry;
            var field = ci.Terrain;
            var ts = new GameObject("Trackside").transform;
            ts.SetParent(root, false);
            var parts = new PartSet();
            // Pavements and lamps behind the barriers, checked against every part of the circuit.
            var furniture = new StreetFurnitureLayout(g, ci.WallLeft, ci.WallRight, ci.Clearance, field.IsWater, Quality);
            foreach (var q in furniture.pavements)
            {
                Vector3 a = V(q.a), b = V(q.b), c = V(q.c), e = V(q.d);
                parts.B(EnvironmentGeometry.Pavement).QuadFacing(a, b, c, e, new Vector2(a.x, a.z) / 3f, new Vector2(b.x, b.z) / 3f, new Vector2(c.x, c.z) / 3f, new Vector2(e.x, e.z) / 3f, Vector3.up);
                parts.B(EnvironmentGeometry.Concrete).QuadFacing(b, c, c + Vector3.down * 2.5f, b + Vector3.down * 2.5f, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, V(q.outward));
            }
            foreach (var l in furniture.lamps)
            {
                EnvironmentGeometry.Lamp(parts.B, V(l.position), V(l.towards), 9f);
                env.lampHeads.Add(EnvironmentGeometry.LampHeadPosition(V(l.position), V(l.towards), 9f));
            }
            float start = ci.Definition.startLineDistance;
            AddMeshObject(ts, "Pavements_Lamps", parts.parts, false);
            var font = CircuitBuilder.BuiltinFont();
            bool eventDress = EventBrandingBuilder.IsEvent(ci.Definition);
            // Turkish street-name signs (white on blue, as in Istanbul).
            foreach (var (text, x, z) in StreetSigns)
            {
                var pr = g.Project(new System.Numerics.Vector3(x, 0f, z));
                int i = pr.index;
                // Behind the barrier on the side of the named street, or the other side; never on another track part.
                if (!furniture.SignSpot(i, pr.lateral >= 0f ? 1 : -1, 2.2f, 1.5f, out var spot, out _)) continue;
                var p = V(spot);
                var fwd = V(g.Tangents[i]);
                Board(ts, font, p + Vector3.up * 2.6f, Quaternion.LookRotation(fwd, Vector3.up), text, new Color(0.05f, 0.24f, 0.55f), Color.white, 2.6f, 0.55f, true);
            }
            // Sponsor banners on the barriers of the start straight and the waterfront.
            int sponsor = 0;
            for (float d = start - 200f; d < start + 900f; d += 55f)
            {
                int i = g.IndexAt(d);
                foreach (int side in new[] { -1, 1 })
                {
                    float wall = side < 0 ? ci.WallLeft[i] : ci.WallRight[i];
                    // On the barrier's upper face (safety-shape profile leans back 0.17 m at the top).
                    var p = TrackPoint(g, i, side * (wall + 0.12f), -0.12f + 0.62f);
                    // TextMesh reads from its -Z side: face the board towards the track.
                    var facing = V(g.Rights[i]) * side;
                    string text = Sponsors[sponsor++ % Sponsors.Length];
                    Color bg = sponsor % 3 == 0 ? new Color(0.05f, 0.08f, 0.16f) : sponsor % 3 == 1 ? new Color(0.92f, 0.92f, 0.92f) : new Color(0.0f, 0.55f, 0.7f);
                    Color fg = sponsor % 3 == 1 ? new Color(0.06f, 0.1f, 0.2f) : Color.white;
                    // Event circuits: every third board carries a Turkish welcome message in the event colours.
                    if (eventDress && sponsor % 3 == 0)
                    {
                        text = EventIdentity.Banners[(sponsor / 3) % EventIdentity.Banners.Length];
                        bool red = (sponsor / 3) % 2 == 0;
                        bg = red ? EventGeometry.RedC : EventGeometry.NavyC;
                        fg = red ? EventGeometry.IvoryC : EventGeometry.GoldC;
                    }
                    Board(ts, font, p, Quaternion.LookRotation(facing, Vector3.up), text, bg, fg, 11f, 0.95f, false);
                }
            }
            // LED screens behind the main grandstands (event circuits: live messages, see EventScreenComponent).
            var screens = eventDress ? ts.gameObject.AddComponent<EventScreenComponent>() : null;
            for (int k = 1; k < env.grandstandCentres.Count; k += 4)
            {
                var c = env.grandstandCentres[k];
                var pr = g.Project(new System.Numerics.Vector3(c.x, 0f, c.z));
                var toTrack = (TrackPoint(g, pr.index, 0f, 0f) - c); toTrack.y = 0f;
                var screen = Board(ts, font, c + Vector3.up * 13f, Quaternion.LookRotation(-toTrack.normalized, Vector3.up), "BOĞAZ GRAND PRIX", new Color(0.02f, 0.03f, 0.05f), new Color(0.2f, 0.85f, 1f), 9f, 4.5f, true);
                if (screens != null && screen != null)
                {
                    // Long messages ("SANAL GÜVENLİK ARACI") must fit the 9 m screen.
                    screen.characterSize = Mathf.Min(4.5f * 0.11f, 9f / 22f * 0.21f);
                    screens.Add(screen);
                }
            }
            // Flags: shared wind, sizes by place, distant ones cheap.
            var flags = new GameObject("Flags").transform;
            flags.SetParent(ts, false);
            // Small flags on the start gantry posts (event circuits place their own flags).
            env.flagSpots.AddRange(ci.EventFlags);
            if (!eventDress)
            {
                var pose = ci.PoseAt(start, 0f, 0f);
                float w = g.WidthAt(start);
                foreach (float s in new[] { -1f, 1f })
                    env.flagSpots.Add((pose.position + pose.rotation * new Vector3(s * (w * 0.5f + 2.5f), 7f, 0f), 1.5f, 2.6f, 1));
            }
            var flagMat = EnvironmentMaterials.Flag;
            foreach (var (pos, length, pole, detail) in env.flagSpots)
            {
                // A flag pole standing on the ground (not on a gantry, roof or tower) must not stand on any part of the circuit.
                bool onGround = pos.y < field.Height(pos.x, pos.z) + 2f;
                if (onGround && !ci.Clearance.IsClear(pos.x, pos.z, 0.3f, 0.3f)) continue;
                var go = new GameObject("TurkishFlag");
                go.transform.SetParent(flags, false);
                var flag = go.AddComponent<FlagComponent>();
                flag.Setup(env, pos, length, pole, detail, flagMat);
            }
        }

        static TextMesh Board(Transform parent, Font font, Vector3 pos, Quaternion rot, string text, Color bg, Color fg, float width, float height, bool posts)
        {
            var root = new GameObject("Board_" + text);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, rot);
            Cube(root.transform, Vector3.zero, new Vector3(width, height, 0.08f), EnvironmentMaterials.Solid(bg));
            if (posts)
                foreach (float s in new[] { -0.42f, 0.42f })
                    Cube(root.transform, new Vector3(s * width, -height * 0.5f - 1.3f, 0.06f), new Vector3(0.1f, 2.6f, 0.1f), ApexMaterials.Get("Steel"));
            if (font == null) return null;
            var label = new GameObject("Text");
            label.transform.SetParent(root.transform, false);
            label.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            var tm = label.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = text;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 64;
            tm.characterSize = Mathf.Min(height * 0.11f, width / Mathf.Max(4, text.Length) * 0.21f);
            tm.color = fg;
            label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return tm;
        }

        static void Cube(Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = new GameObject("Panel");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = CarBuilder.PrimitiveMesh(PrimitiveType.Cube);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        // --- Ships, gulls, ambience -------------------------------------------------------------------

        static void BuildShips(Transform root, EnvironmentInstance env, CircuitDefinition def, LandModel land)
        {
            var routes = MarineRoute.ForCircuit(def, land);
            if (routes.Count == 0) return;
            var go = new GameObject("MarineTraffic");
            go.transform.SetParent(root, false);
            var mt = go.AddComponent<MarineTrafficComponent>();
            var ferry = new LandmarkGeometry(1);
            ferry.Ferry();
            var freighter = new LandmarkGeometry(1);
            freighter.Freighter();
            var tour = new LandmarkGeometry(1);
            tour.TourBoat();
            var motor = new LandmarkGeometry(1);
            motor.MotorBoat();
            foreach (var r in routes)
            {
                // Moderate traffic: two Beşiktaş ferries, one per other ferry line, one tour boat, one motor boat per
                // loop (none on mobile), two transit ships per direction (one on mobile).
                int count = r.vesselKind == MarineRoute.VesselMotorBoat ? (Quality == 0 ? 0 : 1)
                    : r.shuttle ? (r.name.StartsWith("Beşiktaş") ? 2 : 1) : (Quality == 0 ? 1 : 2);
                for (int k = 0; k < count; k++)
                {
                    var lg = r.vesselKind == MarineRoute.VesselFerry ? ferry : r.vesselKind == MarineRoute.VesselTourBoat ? tour
                        : r.vesselKind == MarineRoute.VesselMotorBoat ? motor : freighter;
                    var v = AddMeshObject(go.transform, r.name + "_" + k, lg.parts, true, false);
                    mt.Add(r, v.transform, r.CycleTime * k / count + LandModel.Hash(k, r.points.Count) * 20f, WaterY);
                }
            }
        }

        static void BuildGulls(Transform root, EnvironmentInstance env, CircuitInstance ci)
        {
            var go = new GameObject("Gulls");
            go.transform.SetParent(root, false);
            var gulls = go.AddComponent<GullComponent>();
            var mb = new MeshBuilder();
            EnvironmentGeometry.GullMesh(mb);
            gulls.Setup(Combine(new Dictionary<string, MeshBuilder> { [EnvironmentGeometry.Gull] = mb }, "Gull", out var mats), mats[0]);
            var def = ci.Definition;
            var field = ci.Terrain;
            int count = Quality == 0 ? 10 : Quality >= 3 ? 36 : 22;
            // Over the water off the start/finish and Ortaköy.
            var centres = new List<Vector3>();
            foreach (var id in new[] { "besiktas_pier", "ortakoy_mosque", "ciragan" })
            {
                var l = def.FindLandmark(id);
                if (l != null) centres.Add(new Vector3(l.east + 90f, 0f, l.north));
            }
            if (centres.Count == 0) return;
            for (int i = 0; i < count; i++)
            {
                var c = centres[i % centres.Count];
                gulls.Add(c + new Vector3(LandModel.Hash(i, 1) * 80f, 14f + (LandModel.Hash(i, 2) * 0.5f + 0.5f) * 30f, LandModel.Hash(i, 4) * 120f),
                    35f + (LandModel.Hash(i, 5) * 0.5f + 0.5f) * 60f, 0.25f + LandModel.Hash(i, 6) * 0.08f, LandModel.Hash(i, 8) * 3.14f);
            }
        }

        static void BuildAmbience(Transform root, EnvironmentInstance env, CircuitInstance ci, List<CityLayout.Lot> lots)
        {
            var g = ci.Geometry;
            var field = ci.Terrain;
            // Enclosure per track sample: tall blocks within 45 m on both sides make a street canyon.
            int step = Mathf.Max(1, Mathf.RoundToInt(20f / g.Spacing));
            var grid = new Dictionary<(int, int), int>();
            foreach (var lot in lots)
            {
                if (lot.band != 0 || lot.height < 9f) continue;
                var key = (Mathf.FloorToInt(lot.x / 30f), Mathf.FloorToInt(lot.z / 30f));
                grid.TryGetValue(key, out int c);
                grid[key] = c + 1;
            }
            float Enclosure(Vector3 p)
            {
                int cx = Mathf.FloorToInt(p.x / 30f), cz = Mathf.FloorToInt(p.z / 30f), count = 0;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                        if (grid.TryGetValue((cx + dx, cz + dz), out int c)) count += c;
                return Mathf.Clamp01(count / 9f);
            }
            var go = new GameObject("Ambience");
            go.transform.SetParent(root, false);
            var amb = go.AddComponent<AmbienceComponent>();
            amb.env = env;
            amb.enclosure = Enclosure;
            Vector3 lastZone = new Vector3(1e9f, 0f, 0f);
            int zones = 0;
            for (int i = 0; i < g.Count; i += step)
            {
                var p = V(g.Positions[i]);
                if (Enclosure(p) < 0.6f || (p - lastZone).magnitude < 70f || zones >= 24) continue;
                var z = new GameObject("ReverbZone");
                z.transform.SetParent(go.transform, false);
                z.transform.position = p + Vector3.up * 2f;
                var rz = z.AddComponent<AudioReverbZone>();
                rz.reverbPreset = AudioReverbPreset.StoneCorridor;
                rz.minDistance = 25f;
                rz.maxDistance = 60f;
                lastZone = p;
                zones++;
            }
        }
    }

    /// <summary>
    /// Environment materials with procedural textures (facade windows with night
    /// emission, water normals, catch-fence mesh, the Turkish flag) and registration
    /// of night lights and wet surfaces with the <see cref="EnvironmentInstance"/>.
    /// </summary>
    public static class EnvironmentMaterials
    {
        /// <summary>
        /// Facade colours (BuildingFamilies slots): Istanbul plaster – cream, ochre, salmon, light grey, pale yellow,
        /// dusty pink –, painted timber – oxblood, dark ochre, grey-blue – and the cool grey of contemporary buildings.
        /// </summary>
        static readonly Color[] FacadeColours =
        {
            new Color(0.9f, 0.85f, 0.73f), new Color(0.85f, 0.68f, 0.46f), new Color(0.86f, 0.62f, 0.52f),
            new Color(0.78f, 0.78f, 0.76f), new Color(0.92f, 0.86f, 0.62f), new Color(0.85f, 0.71f, 0.69f),
            new Color(0.56f, 0.24f, 0.19f), new Color(0.76f, 0.56f, 0.26f), new Color(0.5f, 0.57f, 0.62f),
            new Color(0.62f, 0.64f, 0.66f),
        };

        static readonly Dictionary<Color, Material> solids = new Dictionary<Color, Material>();
        static Texture2D facadeAlbedo, facadeEmission, waterNormal, flagTex;
        public static Material Flag { get; private set; }

        public static Material Solid(Color c)
        {
            if (solids.TryGetValue(c, out var m) && m != null) return m;
            m = new Material(ApexMaterials.LitShader()) { name = "Env_Solid", enableInstancing = true };
            SetColour(m, c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.3f);
            solids[c] = m;
            return m;
        }

        static void SetColour(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        static void SetTexture(Material m, string urp, string builtin, Texture t)
        {
            if (m.HasProperty(urp)) m.SetTexture(urp, t);
            if (builtin != null && m.HasProperty(builtin)) m.SetTexture(builtin, t);
        }

        /// <summary>8 bays x 8 floors: windows with frames and sills, shopfronts on the ground floor.</summary>
        static void BuildFacadeTextures()
        {
            const int n = 256, cell = 32;
            facadeAlbedo = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "FacadeAlbedo", wrapMode = TextureWrapMode.Repeat, anisoLevel = 2 };
            facadeEmission = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "FacadeEmission", wrapMode = TextureWrapMode.Repeat };
            var a = new Color[n * n];
            var e = new Color[n * n];
            var rng = new System.Random(1071);
            var wall = new Color(0.95f, 0.95f, 0.95f);
            var glass = new Color(0.16f, 0.18f, 0.22f);
            var frame = new Color(0.98f, 0.98f, 0.97f);
            for (int fy = 0; fy < 8; fy++)
                for (int bx = 0; bx < 8; bx++)
                {
                    bool shop = fy == 0;
                    bool lit = rng.NextDouble() < (shop ? 0.65 : 0.3);
                    var warm = shop ? new Color(1f, 0.9f, 0.75f) : Color.Lerp(new Color(1f, 0.78f, 0.5f), new Color(0.9f, 0.92f, 1f), (float)rng.NextDouble() * 0.4f);
                    bool shutters = !shop && rng.NextDouble() < 0.18;
                    var awning = rng.NextDouble() < 0.5 ? new Color(0.62f, 0.12f, 0.1f) : new Color(0.15f, 0.35f, 0.25f);
                    for (int y = 0; y < cell; y++)
                        for (int x = 0; x < cell; x++)
                        {
                            Color c = wall, em = Color.black;
                            if (shop)
                            {
                                if (x >= 2 && x < 30 && y >= 3 && y < 23) { c = glass * 0.8f; if (lit) em = warm * 0.9f; }
                                else if (x >= 1 && x < 31 && y >= 23 && y < 28) c = awning;
                            }
                            else
                            {
                                bool inWin = x >= 10 && x < 22 && y >= 9 && y < 27;
                                bool inFrame = x >= 9 && x < 23 && y >= 8 && y < 28;
                                if (inWin) { c = shutters ? new Color(0.45f, 0.38f, 0.3f) : glass; if (lit && !shutters) em = warm; }
                                else if (inFrame) c = frame;
                                else if (y >= 6 && y < 8 && x >= 8 && x < 24) c = new Color(0.82f, 0.8f, 0.76f);
                                if (y == cell - 1) c *= 0.85f;
                            }
                            c.a = 1f;
                            int px = bx * cell + x, py = fy * cell + y;
                            a[py * n + px] = c;
                            e[py * n + px] = em;
                        }
                }
            facadeAlbedo.SetPixels(a);
            facadeAlbedo.Apply(true);
            facadeEmission.SetPixels(e);
            facadeEmission.Apply(true);
        }

        static void BuildWaterNormal()
        {
            const int n = 128;
            waterNormal = new Texture2D(n, n, TextureFormat.RGBA32, true, true) { name = "WaterNormal", wrapMode = TextureWrapMode.Repeat };
            var px = new Color[n * n];
            // Sum of tiling waves (integer wave numbers keep the texture seamless).
            var waves = new[] { (3, 1, 0.5f), (-2, 4, 0.35f), (5, -3, 0.25f), (1, 7, 0.18f), (-7, -2, 0.15f), (9, 5, 0.1f) };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = 0f, dy = 0f;
                    foreach (var (kx, ky, amp) in waves)
                    {
                        float ph = 2f * Mathf.PI * (kx * x + ky * y) / n + kx * 1.3f;
                        float c = Mathf.Cos(ph) * amp;
                        dx += c * kx * 0.12f;
                        dy += c * ky * 0.12f;
                    }
                    var nrm = new Vector3(-dx, -dy, 1f).normalized;
                    px[y * n + x] = new Color(nrm.x * 0.5f + 0.5f, nrm.y * 0.5f + 0.5f, nrm.z * 0.5f + 0.5f, 1f);
                }
            waterNormal.SetPixels(px);
            waterNormal.Apply(true);
        }

        /// <summary>
        /// The Bosphorus surface: the Apex/Water shader under URP (Fresnel, probe reflections, sun glitter,
        /// shore foam; values from <see cref="WaterOptics"/>), otherwise a glossy Lit material with the same normals.
        /// </summary>
        static Material WaterMaterial()
        {
            var shader = Resources.Load<Shader>("Shaders/ApexWater");
            if (shader != null && shader.isSupported && GraphicsSettings.currentRenderPipeline != null)
            {
                var m = new Material(shader) { name = "Env_Water" };
                m.SetTexture("_NormalMap", waterNormal);
                m.SetFloat("_F0", WaterOptics.F0);
                m.SetVector("_DeepColor", new Vector4(WaterOptics.DeepColor[0], WaterOptics.DeepColor[1], WaterOptics.DeepColor[2], 0f));
                m.SetVector("_ShallowColor", new Vector4(WaterOptics.ShallowColor[0], WaterOptics.ShallowColor[1], WaterOptics.ShallowColor[2], 0f));
                m.SetFloat("_FoamAlbedo", WaterOptics.FoamAlbedo);
                m.SetFloat("_FoamWidth", WaterOptics.FoamWidth);
                m.SetFloat("_ShallowWidth", WaterOptics.ShallowWidth);
                m.SetFloat("_CoastMaxDistance", WaterOptics.CoastMaxDistance);
                m.SetFloat("_Roughness", WaterOptics.Roughness(5.5f));
                return m;
            }
            var water = new Material(ApexMaterials.LitShader()) { name = "Env_Water" };
            SetColour(water, new Color(0.07f, 0.18f, 0.23f));
            if (water.HasProperty("_Smoothness")) water.SetFloat("_Smoothness", 0.93f);
            if (water.HasProperty("_Glossiness")) water.SetFloat("_Glossiness", 0.93f);
            if (water.HasProperty("_BumpMap"))
            {
                water.SetTexture("_BumpMap", waterNormal);
                water.EnableKeyword("_NORMALMAP");
                if (water.HasProperty("_BumpScale")) water.SetFloat("_BumpScale", 0.6f);
            }
            return water;
        }

        static void BuildFlag()
        {
            const int h = 128;
            var bytes = TurkishFlagDesign.Rasterize(h);
            int w = bytes.Length / 4 / h;
            flagTex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "TurkishFlag", wrapMode = TextureWrapMode.Clamp, anisoLevel = 2 };
            // Only mip level 0 is provided (LoadRawTextureData would expect the whole mip chain);
            // Apply(true) generates the smaller levels.
            flagTex.SetPixelData(bytes, 0);
            flagTex.Apply(true);
            Flag = new Material(ApexMaterials.LitShader()) { name = "Env_Flag" };
            SetTexture(Flag, "_BaseMap", "_MainTex", flagTex);
            SetColour(Flag, Color.white);
            if (Flag.HasProperty("_Cull")) Flag.SetFloat("_Cull", 0f);
            if (Flag.HasProperty("_Smoothness")) Flag.SetFloat("_Smoothness", 0.25f);
            Flag.doubleSidedGI = true;
        }

        /// <summary>
        /// Lit material with a procedural masonry texture set (MasonryTextures) on the landmark models, whose meshes
        /// carry metric UVs (1 unit = 1 m), so the texture repeats every `tile` metres. Emission stays available
        /// for the night floodlighting (driven by EnvironmentInstance like the other night lights).
        /// </summary>
        static Material MasonryMaterial(string name, string paletteKey, (byte[] albedo, byte[] normal) set, float tile, float smoothness, float bump)
        {
            int n = (int)Mathf.Sqrt(set.albedo.Length / 4);
            var albedo = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = name + "_Albedo", wrapMode = TextureWrapMode.Repeat, anisoLevel = 8 };
            // Only mip level 0 is provided: SetPixelData + Apply(true) builds the chain (LoadRawTextureData would
            // expect every level and throws "not enough data provided").
            albedo.SetPixelData(set.albedo, 0);
            albedo.Apply(true);
            var normal = new Texture2D(n, n, TextureFormat.RGBA32, true, true) { name = name + "_Normal", wrapMode = TextureWrapMode.Repeat, anisoLevel = 8 };
            normal.SetPixelData(set.normal, 0);
            normal.Apply(true);
            var m = new Material(ApexMaterials.LitShader()) { name = name, enableInstancing = true };
            SetColour(m, ApexMaterials.Palette[paletteKey]);
            SetTexture(m, "_BaseMap", "_MainTex", albedo);
            if (m.HasProperty("_BumpMap"))
            {
                m.SetTexture("_BumpMap", normal);
                m.EnableKeyword("_NORMALMAP");
                if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", bump);
            }
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            m.mainTextureScale = Vector2.one / tile;
            if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black); }
            return m;
        }

        public static void Ensure(EnvironmentInstance env)
        {
            if (facadeAlbedo == null) BuildFacadeTextures();
            if (waterNormal == null) BuildWaterNormal();
            if (flagTex == null) BuildFlag();
            for (int i = 0; i < BuildingFamilies.FacadeStyles; i++)
            {
                var m = new Material(ApexMaterials.LitShader()) { name = "Env_Facade" + i, enableInstancing = true };
                SetTexture(m, "_BaseMap", "_MainTex", facadeAlbedo);
                SetColour(m, FacadeColours[i]);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.18f);
                SetTexture(m, "_EmissionMap", null, facadeEmission);
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
                ApexMaterials.Register(EnvironmentGeometry.Facade(i), m);
                env.AddLight(m, new Color(1f, 1f, 1f) * 1.1f);
                env.AddWettable(m);
            }
            ApexMaterials.Register("EnvWater", WaterMaterial());
            // Street fronts: plaster in the facade colours (bay windows, parapets), canvas awnings, balcony flags.
            for (int i = 0; i < BuildingFamilies.FacadeStyles; i++) ApexMaterials.Register(EnvironmentGeometry.FacadeSolid(i), Solid(FacadeColours[i % FacadeColours.Length] * 0.97f));
            var awnings = new[] { new Color(0.55f, 0.1f, 0.09f), new Color(0.13f, 0.32f, 0.22f), new Color(0.12f, 0.17f, 0.3f), new Color(0.85f, 0.8f, 0.66f) };
            for (int i = 0; i < EnvironmentGeometry.Awnings; i++)
            {
                var m = Solid(awnings[i]);
                if (m.HasProperty("_Cull")) { m = new Material(m) { name = "Env_Awning" + i }; m.SetFloat("_Cull", 0f); m.doubleSidedGI = true; }
                ApexMaterials.Register(EnvironmentGeometry.Awning(i), m);
            }
            ApexMaterials.Register(EnvironmentGeometry.BalconyFlag, Flag);
            // Shop signs: the Turkish trade-name atlas (tools/signs/build_sign_atlas.py), softly lit at night.
            var signTex = Resources.Load<Texture2D>("Textures/Signs/ShopSigns");
            var sign = new Material(ApexMaterials.LitShader()) { name = "Env_ShopSign", enableInstancing = true };
            if (signTex != null)
            {
                SetTexture(sign, "_BaseMap", "_MainTex", signTex);
                SetTexture(sign, "_EmissionMap", null, signTex);
            }
            else Debug.LogWarning("Shop sign atlas Textures/Signs/ShopSigns missing: signs are plain boards.");
            SetColour(sign, signTex != null ? Color.white : new Color(0.7f, 0.1f, 0.08f));
            if (sign.HasProperty("_Smoothness")) sign.SetFloat("_Smoothness", 0.45f);
            if (sign.HasProperty("_EmissionColor")) sign.SetColor("_EmissionColor", Color.black);
            ApexMaterials.Register(EnvironmentGeometry.ShopSignAtlas, sign);
            env.AddLight(sign, new Color(1f, 0.97f, 0.9f) * 0.8f);
            ApexMaterials.Register(EnvironmentGeometry.Door, Solid(new Color(0.3f, 0.19f, 0.11f)));
            var fence = TrackInfrastructureBuilder.CatchFenceMaterial("Env_CatchFence");
            ApexMaterials.Register(EnvironmentGeometry.CatchFence, fence);
            // Night lights (moderate intensities: no neon, no blown-out bloom).
            env.AddLight(ApexMaterials.Get(EnvironmentGeometry.LampHead), new Color(1f, 0.82f, 0.58f) * 2.2f);
            // City traffic: headlights and tail lights (the instanced vehicles share these two materials).
            env.AddLight(ApexMaterials.Get(EnvironmentGeometry.Headlight), new Color(1f, 0.95f, 0.85f) * 3f);
            env.AddLight(ApexMaterials.Get(EnvironmentGeometry.Taillight), new Color(0.9f, 0.05f, 0.04f) * 1.6f);
            env.AddLight(ApexMaterials.Get(EnvironmentGeometry.BridgeLight), new Color(0.85f, 0.9f, 1f) * 2f);
            // Turkish red (#E30A17) and white. Red stays at 1.3: brighter saturated red bleaches to pink under ACES.
            env.AddLight(ApexMaterials.Get(EnvironmentGeometry.BridgeRed), new Color(0.89f, 0.04f, 0.09f) * 1.3f);
            env.AddLight(ApexMaterials.Get(EnvironmentGeometry.BridgeWhite), new Color(1f, 0.97f, 0.92f) * 2.4f);
            env.AddLight(ApexMaterials.Get(EnvironmentGeometry.TowerGlass), new Color(0.32f, 0.33f, 0.3f));
            // The Ortaköy Mosque's windows glow warm from the lit prayer hall at night (chandeliers behind the glass).
            env.AddLight(ApexMaterials.Get(LandmarkGeometry.MosqueGlass), new Color(1f, 0.74f, 0.4f) * 1.3f);
            // Textured stone for the landmark models; on failure the plain palette materials stay in use.
            try
            {
                ApexMaterials.Register(LandmarkGeometry.Marble, MasonryMaterial("Env_MarbleAshlar", LandmarkGeometry.Marble, MasonryTextures.Marble(512), MasonryTextures.MarbleTile, 0.42f, 0.8f));
                ApexMaterials.Register(LandmarkGeometry.RowPlaster, MasonryMaterial("Env_LimePlaster", LandmarkGeometry.RowPlaster, MasonryTextures.Plaster(256), MasonryTextures.PlasterTile, 0.15f, 0.5f));
                // Brick carries its full colour in the texture (mortar and burnt bricks), so the material colour is white.
                var brick = MasonryMaterial("Env_Brick", LandmarkGeometry.Brick, MasonryTextures.Brick(512), MasonryTextures.BrickTile, 0.12f, 1f);
                SetColour(brick, Color.white);
                ApexMaterials.Register(LandmarkGeometry.Brick, brick);
            }
            catch (System.Exception e) { Debug.LogException(e); }
            foreach (var key in new[] { LandmarkGeometry.Stone, LandmarkGeometry.Marble, LandmarkGeometry.HagiaPlaster })
                env.AddLight(ApexMaterials.Get(key), new Color(0.32f, 0.27f, 0.2f));
            // Grey interlocking concrete pavers on the pavements (UV = world x/z per 3 m along the circuit);
            // the colour stays white so the scan's own albedo applies (mean linear 0.21).
            var pavement = ApexMaterials.Get(EnvironmentGeometry.Pavement);
            if (pavement != null)
            {
                pavement = new Material(pavement) { name = "Env_Pavement" };
                if (SurfaceTextures.TryApply(pavement, SurfaceTextures.Pavement, 3f))
                {
                    SetColour(pavement, Color.white);
                    ApexMaterials.Register(EnvironmentGeometry.Pavement, pavement);
                }
            }
            foreach (var key in new[] { EnvironmentGeometry.Pavement, EnvironmentGeometry.Concrete, EnvironmentGeometry.RoofTile, EnvironmentGeometry.RoofFlat, EnvironmentGeometry.Quay, LandmarkGeometry.Stone })
                env.AddWettable(ApexMaterials.Get(key));
        }
    }
}
