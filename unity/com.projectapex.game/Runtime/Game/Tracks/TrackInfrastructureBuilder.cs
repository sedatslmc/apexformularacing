using System.Collections.Generic;
using Apex.Core;
using Apex.Race;
using Apex.Tracks;
using Apex.Vehicle;
using UnityEngine;
using UnityEngine.Rendering;

namespace Apex.Game
{
    /// <summary>
    /// Builds the street-circuit infrastructure from the <see cref="TrackInfrastructurePlan"/>:
    /// visible concrete barriers (collision stays on the simple wall mesh), guardrail on escape
    /// roads, energy modules and tyre stacks, catch fencing with gates, marshal posts with LED
    /// flag panels (driven by <see cref="FlagPanelComponent"/>), sector and pit signs, and road
    /// details (braking rubber, patches, manholes, faded lane markings). Meshes are chunked
    /// (~400 m) so off-screen parts are culled.
    /// </summary>
    public static class TrackInfrastructureBuilder
    {
        const float ChunkLength = 400f;
        const float BaseDy = -0.12f;   // run-off level relative to the road

        sealed class PartSet
        {
            public readonly Dictionary<string, MeshBuilder> parts = new Dictionary<string, MeshBuilder>();
            public MeshBuilder B(string key)
            {
                if (!parts.TryGetValue(key, out var mb)) parts[key] = mb = new MeshBuilder();
                return mb;
            }
        }

        public static void Build(CircuitInstance ci, Transform root, bool[] pitMask)
        {
            var g = ci.Geometry;
            var plan = ci.Infrastructure;
            var def = ci.Definition;
            var infra = new GameObject("Infrastructure").transform;
            infra.SetParent(root, false);
            int n = g.Count;
            int chunk = Mathf.Max(1, Mathf.RoundToInt(ChunkLength / g.Spacing));
            int pitSide = def.pit != null ? def.pit.side : 0;
            int barrierStep = Mathf.Max(1, Mathf.RoundToInt(4f / g.Spacing));
            bool IsGate(float d)
            {
                foreach (float gd in plan.gates)
                    if (ApexMath.PosMod(d - gd, g.Length) < 3.6f) return true;
                return false;
            }
            for (int c0 = 0; c0 < n; c0 += chunk)
            {
                var set = new PartSet();
                int c1 = Mathf.Min(n, c0 + chunk);
                foreach (int side in new[] { -1, 1 })
                {
                    float[] wall = side < 0 ? ci.WallLeft : ci.WallRight;
                    // Runs of the same barrier type inside this chunk.
                    int runStart = c0;
                    int RunType(int i)
                    {
                        if (side == pitSide && pitMask[i]) return 3;           // pit lane boundary: concrete, no fence
                        float r = plan.RunoffAt(i, side);
                        if (r < 1.0f) return 2;                                // median wall: concrete, no fence
                        return r >= 4f ? 1 : 0;                                // escape road: guardrail; else concrete
                    }
                    for (int i = c0; i <= c1; i++)
                    {
                        bool end = i == c1 || RunType(i) != RunType(runStart);
                        if (!end) continue;
                        int type = RunType(runStart);
                        int b = Mathf.Min(i + 1, c1 + 1);
                        if (type == 1) TrackInfrastructureGeometry.Guardrail(set.B(TrackInfrastructureGeometry.Armco), set.B(TrackInfrastructureGeometry.Steel), g, wall, side, runStart, b, BaseDy);
                        else TrackInfrastructureGeometry.ConcreteBarrier(set.B(TrackInfrastructureGeometry.Concrete), g, wall, side, runStart, b, barrierStep, BaseDy);
                        if (type == 0 || type == 1)
                            TrackInfrastructureGeometry.CatchFence(set.B, g, wall, side, runStart, b, BaseDy, IsGate);
                        runStart = i;
                    }
                }
                EnvironmentBuilder.AddMeshObject(infra, $"Barriers_{c0 / chunk:00}", set.parts, true);
            }
            BuildProtection(ci, infra);
            BuildGates(ci, infra, pitMask, IsGateSide);
            BuildRoadDetails(ci, infra);
            BuildPostsAndPanels(ci, infra);
            BuildSigns(ci, infra);

            bool IsGateSide(int i, int side) => !(side == pitSide && pitMask[i]) && plan.RunoffAt(i, side) >= 1.0f;
        }

        static void BuildProtection(CircuitInstance ci, Transform infra)
        {
            var g = ci.Geometry;
            var set = new PartSet();
            foreach (var p in ci.Infrastructure.protections)
            {
                float[] wall = p.side < 0 ? ci.WallLeft : ci.WallRight;
                if (p.kind == TrackInfrastructurePlan.ProtectionKind.TyreStack)
                    TrackInfrastructureGeometry.TyreStacks(set.B, g, wall, p.side, p.from, p.to, BaseDy);
                else
                    TrackInfrastructureGeometry.EnergyModules(set.B, g, wall, p.side, p.from, p.to, BaseDy);
            }
            if (set.parts.Count == 0) return;
            var go = EnvironmentBuilder.AddMeshObject(infra, "ImpactProtection", set.parts, true);
            // Cars hit the modules, not the wall behind them.
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            go.AddComponent<TrackSurface>().surface = SurfaceType.Wall;
        }

        static void BuildGates(CircuitInstance ci, Transform infra, bool[] pitMask, System.Func<int, int, bool> allowed)
        {
            var g = ci.Geometry;
            var steel = new MeshBuilder();
            var font = CircuitBuilder.BuiltinFont();
            int k = 0;
            foreach (float d in ci.Infrastructure.gates)
            {
                int side = (k++ % 2 == 0) ? 1 : -1;
                int i = g.IndexAt(d);
                if (!allowed(i, side)) side = -side;
                if (!allowed(i, side)) continue;
                float[] wall = side < 0 ? ci.WallLeft : ci.WallRight;
                TrackInfrastructureGeometry.Gate(steel, g, wall, side, d, BaseDy);
                if (k % 3 == 0 && font != null)
                {
                    // Emergency exit sign on the gate, readable from the track.
                    var pos = TrackInfrastructureGeometry.P(g, g.IndexAt(d + 1.75f), side * (wall[i] + 0.25f), BaseDy + TrackInfrastructureGeometry.BarrierHeight + 0.5f);
                    Label(infra, font, pos, Quaternion.LookRotation(new Vector3(g.Rights[i].X, 0f, g.Rights[i].Z) * side), "ACİL ÇIKIŞ", new Color(0.05f, 0.45f, 0.2f), Color.white, 1.4f, 0.45f);
                }
            }
            EnvironmentBuilder.AddMeshObject(infra, "FenceGates", new Dictionary<string, MeshBuilder> { [TrackInfrastructureGeometry.Steel] = steel }, false);
        }

        static void BuildRoadDetails(CircuitInstance ci, Transform infra)
        {
            var g = ci.Geometry;
            var plan = ci.Infrastructure;
            var opaque = new PartSet();
            foreach (var p in plan.patches)
                TrackInfrastructureGeometry.RoadDecal(opaque.B(TrackInfrastructureGeometry.Patch), g, g.Wrap(p.distance), g.Wrap(p.distance + p.length),
                    p.lateral - p.width * 0.5f, p.lateral + p.width * 0.5f, 0.02f, 0f);
            foreach (var (from, to, lat) in plan.laneDashes)
                TrackInfrastructureGeometry.RoadDecal(opaque.B(TrackInfrastructureGeometry.Dash), g, g.Wrap(from), g.Wrap(to), lat - 0.07f, lat + 0.07f, 0.022f, 0f);
            foreach (var (d, lat) in plan.manholes)
            {
                int i = g.IndexAt(d);
                TrackInfrastructureGeometry.ManholeCover(opaque.B(TrackInfrastructureGeometry.Manhole), TrackInfrastructureGeometry.P(g, i, lat, 0.024f), 0.38f);
            }
            EnvironmentBuilder.AddMeshObject(infra, "RoadDetails", opaque.parts, false);
            var rubber = new MeshBuilder();
            foreach (var z in plan.rubber)
                TrackInfrastructureGeometry.RoadDecal(rubber, g, z.from, z.to, z.lateralFrom, z.lateralTo, 0.03f, 25f);
            EnvironmentBuilder.AddMeshObject(infra, "Rubber", new Dictionary<string, MeshBuilder> { [TrackInfrastructureGeometry.Rubber] = rubber }, false);
        }

        static void BuildPostsAndPanels(CircuitInstance ci, Transform infra)
        {
            var g = ci.Geometry;
            var posts = new GameObject("MarshalPosts").transform;
            posts.SetParent(infra, false);
            var shelter = new PartSet();
            TrackInfrastructureGeometry.MarshalShelter(shelter.B);
            var shelterMesh = EnvironmentBuilder.Combine(shelter.parts, "MarshalShelter", out var shelterMats);
            var housing = new MeshBuilder();
            TrackInfrastructureGeometry.PanelHousing(housing);
            var housingMesh = EnvironmentBuilder.Combine(new Dictionary<string, MeshBuilder> { [TrackInfrastructureGeometry.Housing] = housing }, "PanelHousing", out var housingMats);
            var shirt = new MeshBuilder();
            var skin = new MeshBuilder();
            EnvironmentGeometry.SpectatorMesh(shirt, skin);
            var marshalMesh = EnvironmentBuilder.Combine(new Dictionary<string, MeshBuilder> { ["MarshalOrange"] = shirt, [EnvironmentGeometry.Skin] = skin }, "Marshal", out var marshalMats);
            var controller = infra.gameObject.AddComponent<FlagPanelComponent>();
            controller.trackLength = g.Length;
            var font = CircuitBuilder.BuiltinFont();
            int idx = 0;
            foreach (var p in ci.Infrastructure.posts)
            {
                int i = g.IndexAt(p.distance);
                float[] wall = p.side < 0 ? ci.WallLeft : ci.WallRight;
                var fwd = new Vector3(g.Tangents[i].X, 0f, g.Tangents[i].Z).normalized;
                var right = new Vector3(g.Rights[i].X, 0f, g.Rights[i].Z).normalized * p.side;
                var basePos = TrackInfrastructureGeometry.P(g, i, p.side * (wall[i] + 2.3f), BaseDy);
                // Shelter faces the approaching cars (its open side towards -forward / the track).
                var rot = Quaternion.LookRotation(-fwd, Vector3.up);
                var post = new GameObject($"Post_{idx:00}");
                post.transform.SetParent(posts, false);
                post.transform.SetPositionAndRotation(basePos, rot);
                Static(post.transform, "Shelter", shelterMesh, shelterMats, Vector3.zero, Quaternion.identity, Vector3.one);
                // Two marshals beside the shelter, behind the barrier.
                Static(post.transform, "Marshal_A", marshalMesh, marshalMats, new Vector3(0.6f, 0f, 1.1f), Quaternion.Euler(0f, 160f, 0f), Vector3.one * 1.75f);
                Static(post.transform, "Marshal_B", marshalMesh, marshalMats, new Vector3(-0.4f, 0f, 1.3f), Quaternion.Euler(0f, 200f, 0f), Vector3.one * 1.7f);
                // LED panel on a post at the barrier, facing the approaching cars.
                var panelPos = TrackInfrastructureGeometry.P(g, i, p.side * (wall[i] + 0.75f), BaseDy);
                var panel = new GameObject("FlagPanel");
                panel.transform.SetParent(post.transform, false);
                panel.transform.SetPositionAndRotation(panelPos, Quaternion.LookRotation(-fwd, Vector3.up));
                Static(panel.transform, "Housing", housingMesh, housingMats, Vector3.zero, Quaternion.identity, Vector3.one);
                var top = Face(panel.transform, new Vector3(0f, 2.98f, 0.1f), new Vector2(1.18f, 0.4f));
                var bottom = Face(panel.transform, new Vector3(0f, 2.53f, 0.1f), new Vector2(1.18f, 0.4f));
                TextMesh label = null;
                if (font != null)
                {
                    var go = new GameObject("Text");
                    go.transform.SetParent(panel.transform, false);
                    // TextMesh reads from its -Z side; the panel's +Z points at the cars, so flip it.
                    go.transform.localPosition = new Vector3(0f, 2.75f, 0.12f);
                    go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    label = go.AddComponent<TextMesh>();
                    label.font = font;
                    label.fontSize = 64;
                    label.characterSize = 0.05f;
                    label.anchor = TextAnchor.MiddleCenter;
                    label.alignment = TextAlignment.Center;
                    label.color = Color.black;
                    label.text = "";
                    go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                }
                controller.Add(p.distance, top, bottom, label);
                idx++;
            }
        }

        static void Static(Transform parent, string name, Mesh mesh, Material[] mats, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Emissive LED face (its own material instance, driven by the panel controller).</summary>
        static Material Face(Transform parent, Vector3 pos, Vector2 size)
        {
            var go = new GameObject("LedFace");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            // Quad's visible side is -Z in Unity's built-in quad: rotate so it faces +Z (the cars).
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = CarBuilder.PrimitiveMesh(PrimitiveType.Quad);
            var mr = go.AddComponent<MeshRenderer>();
            var mat = new Material(ApexMaterials.LitShader()) { name = "LedFace" };
            SetColor(mat, new Color(0.02f, 0.02f, 0.02f));
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.6f);
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            return mat;
        }

        static void BuildSigns(CircuitInstance ci, Transform infra)
        {
            var g = ci.Geometry;
            var def = ci.Definition;
            var font = CircuitBuilder.BuiltinFont();
            if (font == null) return;
            var signs = new GameObject("Signs").transform;
            signs.SetParent(infra, false);
            var navy = new Color(0.114f, 0.157f, 0.208f);
            var ivory = new Color(0.94f, 0.914f, 0.851f);
            // Sector boards at the sector lines (right side, behind the barrier).
            for (int s = 0; s < ci.SectorStarts.Length; s++)
            {
                float d = ci.RawDistance(ci.SectorStarts[s]);
                Board(ci, signs, font, d - 15f, 1, $"SEKTÖR {s + 1}", navy, ivory, 2.2f);
            }
            if (def.pit != null)
            {
                var pit = def.pit;
                Board(ci, signs, font, pit.entry - 150f, pit.side, "PİT GİRİŞİ ▸", new Color(0.78f, 0.06f, 0.18f), Color.white, 2.4f);
                Board(ci, signs, font, pit.entry + 10f, pit.side, $"PİT {pit.speedLimitKmh:0} km/h", Color.white, new Color(0.78f, 0.06f, 0.18f), 2.0f);
                Board(ci, signs, font, pit.exit - 40f, pit.side, "PİT ÇIKIŞI", navy, ivory, 2.2f);
            }
        }

        static void Board(CircuitInstance ci, Transform parent, Font font, float d, int side, string text, Color bg, Color fg, float width)
        {
            var g = ci.Geometry;
            d = g.Wrap(d);
            int i = g.IndexAt(d);
            float[] wall = side < 0 ? ci.WallLeft : ci.WallRight;
            var pos = TrackInfrastructureGeometry.P(g, i, side * (wall[i] + 1.0f), BaseDy + 2.6f);
            var fwd = new Vector3(g.Tangents[i].X, 0f, g.Tangents[i].Z);
            // TextMesh reads from its -Z side: +Z along the driving direction faces the cars.
            Label(parent, font, pos, Quaternion.LookRotation(fwd, Vector3.up), text, bg, fg, width, 0.7f, true);
        }

        static void Label(Transform parent, Font font, Vector3 pos, Quaternion rot, string text, Color bg, Color fg, float width, float height, bool post = false)
        {
            var root = new GameObject("Sign_" + text);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, rot);
            var panel = new GameObject("Panel");
            panel.transform.SetParent(root.transform, false);
            panel.transform.localScale = new Vector3(width, height, 0.06f);
            panel.AddComponent<MeshFilter>().sharedMesh = CarBuilder.PrimitiveMesh(PrimitiveType.Cube);
            panel.AddComponent<MeshRenderer>().sharedMaterial = EnvironmentMaterials.Solid(bg);
            if (post)
            {
                var p = new GameObject("Post");
                p.transform.SetParent(root.transform, false);
                p.transform.localPosition = new Vector3(0f, -1.3f - height * 0.5f, 0.05f);
                p.transform.localScale = new Vector3(0.1f, 2.6f, 0.1f);
                p.AddComponent<MeshFilter>().sharedMesh = CarBuilder.PrimitiveMesh(PrimitiveType.Cube);
                p.AddComponent<MeshRenderer>().sharedMaterial = ApexMaterials.Get("Steel");
            }
            var label = new GameObject("Text");
            label.transform.SetParent(root.transform, false);
            label.transform.localPosition = new Vector3(0f, 0f, -0.04f);
            var tm = label.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = text;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 64;
            tm.characterSize = Mathf.Min(height * 0.1f, width / Mathf.Max(4, text.Length) * 0.2f);
            tm.color = fg;
            label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        // --- Materials --------------------------------------------------------------------------

        static bool materialsReady;

        static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        static Material Lit(string name, Color c, float smooth, Texture2D albedo = null, Texture2D normal = null, Vector2? tiling = null)
        {
            var m = new Material(ApexMaterials.LitShader()) { name = "Infra_" + name, enableInstancing = true };
            SetColor(m, c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (albedo != null)
            {
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", albedo);
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", albedo);
            }
            if (normal != null && m.HasProperty("_BumpMap"))
            {
                m.SetTexture("_BumpMap", normal);
                m.EnableKeyword("_NORMALMAP");
                if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", 0.7f);
            }
            if (tiling.HasValue) m.mainTextureScale = tiling.Value;
            return m;
        }

        static Material Transparent(Material m)
        {
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        /// <summary>Creates and registers the infrastructure materials (procedural textures) once.</summary>
        public static void EnsureMaterials()
        {
            if (materialsReady) return;
            materialsReady = true;
            var asphaltA = AsphaltTexture(out var asphaltN);
            ApexMaterials.Register("Asphalt", Lit("Asphalt", new Color(0.62f, 0.62f, 0.64f), 0.22f, asphaltA, asphaltN, new Vector2(3.3f, 3.3f)));
            ApexMaterials.Register("RunoffAsphalt", Lit("RunoffAsphalt", new Color(0.78f, 0.77f, 0.75f), 0.18f, asphaltA, asphaltN, new Vector2(2.5f, 2.5f)));
            ApexMaterials.Register(TrackInfrastructureGeometry.Patch, Lit("Patch", new Color(0.48f, 0.48f, 0.5f), 0.2f, asphaltA, asphaltN, new Vector2(1f, 1f)));
            ApexMaterials.Register(TrackInfrastructureGeometry.Concrete, Lit("BarrierConcrete", Color.white, 0.12f, ConcreteTexture()));
            ApexMaterials.Register(TrackInfrastructureGeometry.Armco, Lit("Armco", new Color(0.72f, 0.74f, 0.76f), 0.55f));
            ApexMaterials.Register(TrackInfrastructureGeometry.ModuleA, Lit("ModuleNavy", new Color(0.114f, 0.157f, 0.208f), 0.35f));
            ApexMaterials.Register(TrackInfrastructureGeometry.ModuleB, Lit("ModuleIvory", new Color(0.94f, 0.914f, 0.851f), 0.35f));
            ApexMaterials.Register(TrackInfrastructureGeometry.ModuleC, Lit("ModuleTurquoise", new Color(0.031f, 0.498f, 0.549f), 0.35f));
            ApexMaterials.Register(TrackInfrastructureGeometry.Belt, Lit("ConveyorBelt", new Color(0.08f, 0.08f, 0.09f), 0.3f));
            ApexMaterials.Register(TrackInfrastructureGeometry.Roof, Lit("ShelterRoof", new Color(0.031f, 0.498f, 0.549f), 0.3f));
            ApexMaterials.Register(TrackInfrastructureGeometry.Extinguisher, Lit("Extinguisher", new Color(0.75f, 0.05f, 0.06f), 0.6f));
            ApexMaterials.Register(TrackInfrastructureGeometry.Housing, Lit("PanelHousing", new Color(0.08f, 0.09f, 0.1f), 0.4f));
            ApexMaterials.Register(TrackInfrastructureGeometry.Manhole, Lit("Manhole", new Color(0.16f, 0.16f, 0.17f), 0.45f));
            ApexMaterials.Register(TrackInfrastructureGeometry.Dash, Lit("LaneDash", new Color(0.62f, 0.62f, 0.6f), 0.25f));
            ApexMaterials.Register("MarshalOrange", Lit("MarshalOrange", new Color(0.95f, 0.42f, 0.05f), 0.3f));
            ApexMaterials.Register(TrackInfrastructureGeometry.Rubber, Transparent(Lit("Rubber", new Color(0.03f, 0.03f, 0.03f, 1f), 0.3f, RubberTexture())));
            ApexMaterials.Register(TrackInfrastructureGeometry.Fence, FenceMaterial());
        }

        /// <summary>Tileable asphalt: grey with multi-scale noise and light aggregate, plus a matching normal map.</summary>
        static Texture2D AsphaltTexture(out Texture2D normal)
        {
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "Asphalt", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            normal = new Texture2D(n, n, TextureFormat.RGBA32, true, true) { name = "AsphaltNormal", wrapMode = TextureWrapMode.Repeat };
            var h = new float[n * n];
            var rng = new System.Random(77);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // Periodic noise: sample on a torus by wrapping the lattice (period 16 and 64 cells).
                    float v = 0.5f * TileNoise(x, y, n, 16) + 0.3f * TileNoise(x, y, n, 64) + 0.2f * (float)rng.NextDouble();
                    h[y * n + x] = v;
                }
            var px = new Color[n * n];
            var nx = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float v = h[y * n + x];
                    float g = 0.3f + 0.18f * v;
                    if (v > 0.82f) g += 0.18f;               // light aggregate
                    px[y * n + x] = new Color(g, g, g * 1.02f, 1f);
                    float dx = h[y * n + (x + 1) % n] - h[y * n + (x - 1 + n) % n];
                    float dy = h[((y + 1) % n) * n + x] - h[((y - 1 + n) % n) * n + x];
                    var nn = new Vector3(-dx * 2f, -dy * 2f, 1f).normalized;
                    nx[y * n + x] = new Color(nn.x * 0.5f + 0.5f, nn.y * 0.5f + 0.5f, nn.z * 0.5f + 0.5f, 1f);
                }
            tex.SetPixels(px);
            tex.Apply(true);
            normal.SetPixels(nx);
            normal.Apply(true);
            return tex;
        }

        static float TileNoise(int x, int y, int size, int cells)
        {
            float fx = x * cells / (float)size, fy = y * cells / (float)size;
            int x0 = (int)fx, y0 = (int)fy;
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            float H(int a, int b) => Apex.Environment.LandModel.Hash(a % cells, b % cells) * 0.5f + 0.5f;
            float a0 = H(x0, y0), a1 = H(x0 + 1, y0), b0 = H(x0, y0 + 1), b1 = H(x0 + 1, y0 + 1);
            return Mathf.Lerp(Mathf.Lerp(a0, a1, tx), Mathf.Lerp(b0, b1, tx), ty);
        }

        /// <summary>Barrier concrete: one 3.8 m block per texture repeat (joint at the edge), scuffs low down.</summary>
        static Texture2D ConcreteTexture()
        {
            const int w = 256, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "BarrierConcrete", wrapMode = TextureWrapMode.Repeat, anisoLevel = 2 };
            var px = new Color[w * h];
            var rng = new System.Random(5);
            var scuffs = new List<(int x0, int x1, int y, float k)>();
            for (int s = 0; s < 7; s++) scuffs.Add((rng.Next(w), rng.Next(w), rng.Next(4, 20), 0.25f + (float)rng.NextDouble() * 0.35f));
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float v = 0.68f + 0.06f * (TileNoise(x, y, w, 32) - 0.5f) + 0.03f * ((float)rng.NextDouble() - 0.5f);
                    if (x < 2 || x > w - 3) v *= 0.45f;                    // block joint
                    if (y == 18 || y == 40) v *= 0.9f;                      // profile break lines
                    foreach (var (x0, x1, sy, k) in scuffs)
                        if (System.Math.Abs(y - sy) < 2 && ((x0 < x1 && x >= x0 && x <= x1) || (x0 > x1 && (x >= x0 || x <= x1)))) v *= 1f - k;
                    if (y < 3) v *= 0.8f;                                   // grime at the foot
                    px[y * w + x] = new Color(v, v, v * 0.98f, 1f);
                }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>Diamond-mesh catch fence (alpha-tested, no blending overdraw).</summary>
        static Material FenceMaterial()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "CatchFenceMesh", wrapMode = TextureWrapMode.Repeat };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int d1 = (x + y) % 16, d2 = (x - y + 64) % 16;
                    bool wire = d1 < 2 || d2 < 2 || y < 2;
                    px[y * n + x] = wire ? new Color(0.62f, 0.64f, 0.66f, 1f) : new Color(0f, 0f, 0f, 0f);
                }
            tex.SetPixels(px);
            tex.Apply(true);
            var m = Lit("CatchFence", Color.white, 0.4f, tex);
            if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1f);
            if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)RenderQueue.AlphaTest;
            return m;
        }

        /// <summary>Rubber deposit: soft across the strip (u), streaky along it (v), alpha-blended.</summary>
        static Texture2D RubberTexture()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "Rubber", wrapMode = TextureWrapMode.Repeat };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f;
                    float across = Mathf.Exp(-u * u * 3f);
                    float streak = 0.6f + 0.4f * TileNoise(x * 4, y, n * 4, 8);
                    px[y * n + x] = new Color(0f, 0f, 0f, 0.5f * across * streak);
                }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }
    }
}
