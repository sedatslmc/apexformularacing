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
            // Barriers, guardrail, catch fence with gates and banners, impact protection: the modular kit.
            var kit = new TrackKitLayout(g, plan, ci.Surface);
            TrackKitBuilder.Build(ci, infra, kit, EnvironmentBuilder.Quality);
            BuildGateSigns(infra, kit);
            BuildRoadDetails(ci, infra);
            BuildPostsAndPanels(ci, infra);
            BuildSigns(ci, infra);
        }

        /// <summary>Emergency-exit sign on every third fence gate, readable from the track.</summary>
        static void BuildGateSigns(Transform infra, TrackKitLayout kit)
        {
            var font = CircuitBuilder.BuiltinFont();
            if (font == null) return;
            int k = 0;
            foreach (var p in kit.placements)
            {
                if (p.module != TrackKitLayout.Module.FenceGate || k++ % 3 != 0) continue;
                var outward = new Vector3(p.outward.X, 0f, p.outward.Z);
                float y0 = p.variant == 1 ? 0f : TrackKitGeometry.BarrierHeight;
                var pos = new Vector3(p.position.X, p.position.Y, p.position.Z) - outward * 0.1f + Vector3.up * (y0 + 1.6f);
                Label(infra, font, pos, Quaternion.LookRotation(outward), "ACİL ÇIKIŞ", new Color(0.05f, 0.45f, 0.2f), Color.white, 1.4f, 0.45f);
            }
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
            // Groove under the tyre tracks, tracks above it, lock-up streaks on top (separate lifts avoid z-fighting).
            foreach (var z in plan.rubber)
                TrackInfrastructureGeometry.RubberStrip(rubber, g, z, z.brakeMark ? 0.034f : z.width > 1.5f ? 0.028f : 0.031f);
            EnvironmentBuilder.AddMeshObject(infra, "Rubber", new Dictionary<string, MeshBuilder> { [TrackInfrastructureGeometry.Rubber] = rubber }, false);
        }

        static void BuildPostsAndPanels(CircuitInstance ci, Transform infra)
        {
            var g = ci.Geometry;
            var posts = new GameObject("MarshalPosts").transform;
            posts.SetParent(infra, false);
            var shelter = LodMeshes("MarshalPost", (B, lod) => TrackKitGeometry.MarshalPost(B, lod));
            var housing = LodMeshes("FlagPanelHousing", (B, lod) => TrackKitGeometry.FlagPanelHousing(B, lod));
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
                WithLods(post.transform, "Shelter", shelter, 40f);
                // Two marshals beside the shelter, behind the barrier.
                Static(post.transform, "Marshal_A", marshalMesh, marshalMats, new Vector3(0.6f, 0f, 1.1f), Quaternion.Euler(0f, 160f, 0f), Vector3.one * 1.75f);
                Static(post.transform, "Marshal_B", marshalMesh, marshalMats, new Vector3(-0.4f, 0f, 1.3f), Quaternion.Euler(0f, 200f, 0f), Vector3.one * 1.7f);
                // LED panel on a post at the barrier, facing the approaching cars.
                var panelPos = TrackInfrastructureGeometry.P(g, i, p.side * (wall[i] + 0.75f), BaseDy);
                var panel = new GameObject("FlagPanel");
                panel.transform.SetParent(post.transform, false);
                panel.transform.SetPositionAndRotation(panelPos, Quaternion.LookRotation(-fwd, Vector3.up));
                WithLods(panel.transform, "Housing", housing, 25f);
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

        /// <summary>Three detail levels of a kit piece as meshes with their materials.</summary>
        static (Mesh mesh, Material[] mats)[] LodMeshes(string name, System.Action<TrackKitGeometry.Parts, int> build)
        {
            var result = new (Mesh, Material[])[3];
            for (int lod = 0; lod < 3; lod++)
            {
                var set = new PartSet();
                build(k => set.B(k), lod);
                result[lod] = (EnvironmentBuilder.Combine(set.parts, name + "_LOD" + lod, out var mats), mats);
            }
            return result;
        }

        /// <summary>Static object with a cross-fading LODGroup: LOD0 up to `lod0Distance`, LOD1 to 3x, LOD2 to 10x, then culled.</summary>
        static void WithLods(Transform parent, string name, (Mesh mesh, Material[] mats)[] lods, float lod0Distance)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.isStatic = true;
            var levels = new LOD[lods.Length];
            for (int k = 0; k < lods.Length; k++)
            {
                var child = new GameObject("LOD" + k);
                child.transform.SetParent(go.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = lods[k].mesh;
                var mr = child.AddComponent<MeshRenderer>();
                mr.sharedMaterials = lods[k].mats;
                mr.shadowCastingMode = k < 2 ? ShadowCastingMode.On : ShadowCastingMode.Off;
                levels[k] = new LOD(TrackKitBuilder.LodHeight(lod0Distance * (k == 0 ? 1f : k == 1 ? 3f : 10f)), new Renderer[] { mr });
            }
            var group = go.AddComponent<LODGroup>();
            group.fadeMode = LODFadeMode.CrossFade;
            group.animateCrossFading = true;
            group.SetLODs(levels);
            group.size = 10f;
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

        static Texture2D asphaltAlbedo, asphaltNormal;
        static bool? surfaceUltra;

        /// <summary>Creates and registers the infrastructure materials (procedural textures) once; the road
        /// surface materials again when the texture tier (<see cref="SurfaceTextures.UseUltra"/>) changed.</summary>
        public static void EnsureMaterials()
        {
            if (materialsReady)
            {
                if (surfaceUltra != SurfaceTextures.UseUltra) RegisterSurfaceMaterials();
                return;
            }
            materialsReady = true;
            asphaltAlbedo = AsphaltTexture(out asphaltNormal);
            var asphaltA = asphaltAlbedo;
            var asphaltN = asphaltNormal;
            RegisterSurfaceMaterials();
            ApexMaterials.Register(TrackInfrastructureGeometry.Patch, Lit("Patch", new Color(0.48f, 0.48f, 0.5f), 0.2f, asphaltA, asphaltN, new Vector2(1f, 1f)));
            var concrete = Lit("BarrierConcrete", Color.white, 0.12f, ConcreteTexture());
            // Scanned concrete grain on top of the block texture (UV: one 3.8 m block along u, 1.6 m of profile along v).
            SurfaceTextures.TryApplyDetail(concrete, SurfaceTextures.Barrier, new Vector2(Apex.Tracks.TrackKitLayout.BlockLength, 1.6f), 0.8f);
            ApexMaterials.Register(TrackInfrastructureGeometry.Concrete, concrete);
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
            // Circuit kit details and the event banners (procedural art, EventArt).
            ApexMaterials.Register(TrackKitGeometry.Dark, Lit("KitDark", new Color(0.14f, 0.14f, 0.15f), 0.1f));
            ApexMaterials.Register(TrackKitGeometry.Strap, Lit("KitStrap", new Color(0.05f, 0.05f, 0.055f), 0.35f));
            var reflector = Lit("KitReflector", new Color(0.95f, 0.95f, 0.95f), 0.85f);
            reflector.EnableKeyword("_EMISSION");
            if (reflector.HasProperty("_EmissionColor")) reflector.SetColor("_EmissionColor", new Color(0.12f, 0.12f, 0.12f));
            ApexMaterials.Register(TrackKitGeometry.Reflector, reflector);
            for (int v = 0; v < TrackKitGeometry.BannerVariants; v++)
                ApexMaterials.Register(TrackKitGeometry.Banner(v), Lit("KitBanner" + v, Color.white, 0.3f, BannerTexture(v)));
            ApexMaterials.Register(TrackKitGeometry.BannerBack, Lit("KitBannerBack", new Color(0.9f, 0.88f, 0.84f), 0.25f));
        }

        /// <summary>Road, run-off and kerb materials from the CC0 sets of the current tier (procedural fallback).</summary>
        static void RegisterSurfaceMaterials()
        {
            surfaceUltra = SurfaceTextures.UseUltra;
            var asphaltA = asphaltAlbedo;
            var asphaltN = asphaltNormal;
            // Road and run-off: the scanned CC0 sets when imported (tools/textures/import_cc0.py, see docs/CC0_MATERIALS.md), else procedural.
            var road = Lit("Asphalt", new Color(0.62f, 0.62f, 0.64f), 0.22f, asphaltA, asphaltN, new Vector2(3.3f, 3.3f));
            // Calibrated to aged city asphalt (the track scan is fresh, very dark asphalt).
            if (SurfaceTextures.TryApply(road, SurfaceTextures.Asphalt, SurfaceTextures.RoadUvMetres))
                SetColor(road, SurfaceTextures.AlbedoTint(SurfaceTextures.Asphalt, SurfaceTextures.AsphaltAlbedo));
            ApexMaterials.Register("Asphalt", road);
            var runoff = Lit("RunoffAsphalt", new Color(0.78f, 0.77f, 0.75f), 0.18f, asphaltA, asphaltN, new Vector2(6.25f, 6.25f));
            // The run-off is older, paler asphalt: its own scan, or the road scan with a lighter tint.
            if (SurfaceTextures.TryApply(runoff, SurfaceTextures.Runoff, SurfaceTextures.RoadUvMetres))
                SetColor(runoff, SurfaceTextures.AlbedoTint(SurfaceTextures.Runoff, SurfaceTextures.RunoffAlbedo));
            else if (SurfaceTextures.TryApply(runoff, SurfaceTextures.Asphalt, SurfaceTextures.RoadUvMetres))
                SetColor(runoff, SurfaceTextures.AlbedoTint(SurfaceTextures.Asphalt, SurfaceTextures.RunoffAlbedo));
            ApexMaterials.Register("RunoffAsphalt", runoff);
            // Kerbs: red and white paint on the scanned kerb set (paint base tinted by the kerb colour); without it the plain palette colours stay.
            if (SurfaceTextures.Load(SurfaceTextures.Kerb) != null)
                foreach (var key in new[] { "KerbRed", "KerbWhite" })
                {
                    var kerb = Lit(key, ApexMaterials.Palette[key], 0.35f);
                    SurfaceTextures.TryApply(kerb, SurfaceTextures.Kerb, SurfaceTextures.RoadUvMetres, paint: true);
                    ApexMaterials.Register(key, kerb);
                }
        }

        static Texture2D BannerTexture(int variant)
        {
            const int w = 768, h = 256;
            var rgba = Apex.Environment.EventArt.Banner(variant, w, h);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "KitBanner" + variant, wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2], 1f);
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
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

        static Texture2D catchFenceTexture;

        /// <summary>
        /// Chain-link mesh at its real scale: 256 px per 2 m UV unit (the fence panels map 0.5 UV per metre),
        /// 20 diamonds = 100 mm mesh, galvanised wire about 4 % of a cell wide, antialiased. Mipmaps average the
        /// wire coverage (~25 %), so a distant fence reads as a light grey veil instead of a dark wall.
        /// </summary>
        public static Texture2D CatchFenceTexture()
        {
            if (catchFenceTexture != null) return catchFenceTexture;
            const int n = 256;
            const float cells = 20f, wire = 0.6f;   // half width in pixels
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "CatchFenceMesh", wrapMode = TextureWrapMode.Repeat, anisoLevel = 8 };
            var px = new Color[n * n];
            float c = n / cells, spacing = c / Mathf.Sqrt(2f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // Distance (px) to the nearest wire of each diagonal family.
                    float a = (x + y + 1f) / c, b = (x - y + n) / c;
                    float da = Mathf.Abs(a - Mathf.Round(a)) * spacing, db = Mathf.Abs(b - Mathf.Round(b)) * spacing;
                    float cover = Mathf.Max(Mathf.Clamp01(wire + 0.5f - da), Mathf.Clamp01(wire + 0.5f - db));
                    px[y * n + x] = new Color(0.62f, 0.64f, 0.66f, cover);
                }
            tex.SetPixels(px);
            tex.Apply(true);
            return catchFenceTexture = tex;
        }

        /// <summary>
        /// Catch-fence mesh, alpha-blended (not alpha-tested): thin wires stay continuous at every distance and
        /// fade to a veil; it casts no shadow (URP transparents do not), which the thin mesh hardly does anyway.
        /// </summary>
        public static Material CatchFenceMaterial(string name)
        {
            var m = Transparent(Lit(name, Color.white, 0.45f, CatchFenceTexture()));
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.6f);
            return m;
        }

        static Material FenceMaterial() => CatchFenceMaterial("CatchFence");

        /// <summary>
        /// Rubber atlas: <see cref="TrackInfrastructureGeometry.RubberLevels"/> bands across u, one per opacity
        /// level (up to 55 %); inside a band soft across the strip, streaky along it (v). Alpha-blended.
        /// </summary>
        static Texture2D RubberTexture()
        {
            int levels = TrackInfrastructureGeometry.RubberLevels;
            const int band = 32, h = 64;
            int w = band * levels;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "Rubber", wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            tex.wrapModeU = TextureWrapMode.Clamp;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int level = x / band;
                    float u = ((x % band) + 0.5f) / band * 2f - 1f;
                    float across = Mathf.Exp(-u * u * 3f);
                    float streak = 0.6f + 0.4f * TileNoise((x % band) * 4, y, band * 4, 8);
                    px[y * w + x] = new Color(0f, 0f, 0f, 0.55f * level / (levels - 1f) * across * streak);
                }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }
    }
}
