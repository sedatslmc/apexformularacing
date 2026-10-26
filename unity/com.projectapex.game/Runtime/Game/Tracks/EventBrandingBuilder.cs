using System.Collections.Generic;
using Apex.Environment;
using Apex.Race;
using Apex.Tracks;
using UnityEngine;
using UnityEngine.Rendering;

namespace Apex.Game
{
    /// <summary>
    /// Places the Bosphorus GP event identity (circuits whose definition has identity
    /// "bosphorus"): the branded start gantry with working start lights, the pit building with
    /// 20 garage bays, timing tower (live order), race control, podium with the original trophy
    /// on the pit roof, emblem panels and the event flags. Every footprint is checked against
    /// water and other parts of the track; a piece that does not fit is left out rather than
    /// placed on top of something.
    /// </summary>
    public static class EventBrandingBuilder
    {
        public const string Emblem = "EventEmblem";

        public static bool IsEvent(CircuitDefinition def) => def != null && def.identity == EventIdentity.Bosphorus;

        sealed class PartSet
        {
            public readonly Dictionary<string, MeshBuilder> parts = new Dictionary<string, MeshBuilder>();
            public MeshBuilder B(string key)
            {
                if (!parts.TryGetValue(key, out var mb)) parts[key] = mb = new MeshBuilder();
                return mb;
            }
        }

        // --- Start gantry -----------------------------------------------------------------------

        /// <summary>Event gantry over the start line; its lamps are driven by <see cref="StartLightsComponent"/>.</summary>
        public static void BuildGantry(Transform parent, CircuitInstance ci)
        {
            EnsureMaterials();
            var g = ci.Geometry;
            float d = ci.Definition.startLineDistance;
            float w = g.WidthAt(d);
            var pose = ci.PoseAt(d, 0f, 0f);
            var set = new PartSet();
            var lamps = EventGeometry.Gantry(set.B, w);
            var go = EnvironmentBuilder.AddMeshObject(parent, "Gantry", set.parts, true);
            go.transform.SetPositionAndRotation(pose.position, pose.rotation);
            var lights = go.AddComponent<StartLightsComponent>();
            // One material per light pair so the five columns switch on one after another.
            for (int k = 0; k < 5; k++)
            {
                var mat = StartLightsComponent.LampMaterial();
                lights.Add(mat);
                for (int l = 0; l < 2; l++)
                    Quad(go.transform, "Lamp", lamps[k * 2 + l] + Vector3.back * 0.005f, Quaternion.identity, new Vector2(0.4f, 0.4f), mat);
            }
            // Emblem and lettering on both faces of the beam.
            float y = EventGeometry.GantryBeamY + EventGeometry.GantryBeamH * 0.45f;
            var font = CircuitBuilder.BuiltinFont();
            foreach (float face in new[] { -1f, 1f })
            {
                // Quads and TextMeshes are seen from their -Z side.
                var rot = face < 0f ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
                var z = Vector3.forward * face * 0.565f;
                Quad(go.transform, "Emblem", new Vector3(0f, y, 0f) + z * 1.01f, rot, new Vector2(1.3f, 1.3f), ApexMaterials.Get(Emblem));
                if (font == null) continue;
                float off = w * 0.5f * 0.55f + 0.9f;
                Text(go.transform, font, new Vector3(-off * face, y, 0f) + z * 1.02f, rot, "BOSPHORUS", EventGeometry.IvoryC, 0.9f, w * 0.42f);
                Text(go.transform, font, new Vector3(off * face, y, 0f) + z * 1.02f, rot, "GRAND PRIX", EventGeometry.IvoryC, 0.9f, w * 0.42f);
            }
        }

        // --- Pit complex ------------------------------------------------------------------------

        /// <summary>Pit building, timing tower, race control, podium and trophy, emblems and event flags.</summary>
        public static void Build(CircuitInstance ci, Transform root)
        {
            EnsureMaterials();
            var layout = new EventLayout(ci.Geometry, ci.Definition, ci.Terrain, ci.WallLeft, ci.WallRight, CircuitBuilder.PitOuter);
            ci.EventLayout = layout;
            var parent = new GameObject("EventIdentity").transform;
            parent.SetParent(root, false);
            var font = CircuitBuilder.BuiltinFont();
            var toTrack = Quaternion.Euler(0f, 90f, 0f);   // quads and text facing local -x (the track)
            float length = layout.pitLength;

            if (layout.pitBuilding.placed)
            {
                var set = new PartSet();
                EventGeometry.PitBuilding(set.B, length, layout.bayZ, ci.Pit.boxSpacing);
                // Plinth down to the terrain.
                set.B(EventGeometry.Navy).Box(new Vector3(EventGeometry.PitDepth * 0.5f, -1.5f, 0f), new Vector3(EventGeometry.PitDepth, 3f, length));
                var building = Place(parent, "PitBuilding", set, layout.pitBuilding);
                var col = building.AddComponent<BoxCollider>();
                col.center = new Vector3(EventGeometry.PitDepth * 0.5f + 0.3f, EventGeometry.PitRoof * 0.5f, 0f);
                col.size = new Vector3(EventGeometry.PitDepth - 0.6f, EventGeometry.PitRoof, length);
                ci.EventFootprints.Add(Footprint(building.transform, new Vector3(EventGeometry.PitDepth * 0.5f, 0f, 0f), length * 0.5f + 4f));
                // Emblems and the event name on the glazed upper floor, facing the pit lane.
                float glassY = EventGeometry.PitGroundH + EventGeometry.PitUpperH * 0.5f;
                for (float z = -length * 0.5f + 20f; z < length * 0.5f - 10f; z += 45f)
                {
                    Quad(building.transform, "Emblem", new Vector3(1.74f, glassY, z), toTrack, new Vector2(2.6f, 2.6f), ApexMaterials.Get(Emblem));
                    if (font != null && z + 45f < length * 0.5f - 10f)
                        Text(building.transform, font, new Vector3(1.74f, glassY, z + 22.5f), toTrack, z < 0f ? "BOSPHORUS GRAND PRIX · ISTANBUL" : EventIdentity.Slogan, EventGeometry.IvoryC, 1.0f, 30f);
                }
                // Podium on the roof terrace at the start line, facing the track, with the trophy on the top step.
                var podium = new PartSet();
                EventGeometry.Podium(podium.B);
                var podiumGo = EnvironmentBuilder.AddMeshObject(building.transform, "Podium", podium.parts, true);
                podiumGo.transform.localPosition = new Vector3(EventGeometry.PitDepth * 0.5f, EventGeometry.PitRoof, layout.podiumZ);
                podiumGo.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
                var trophy = new PartSet();
                EventGeometry.Trophy(trophy.B);
                EnvironmentBuilder.AddMeshObject(podiumGo.transform, "Trophy", trophy.parts, false).transform.localPosition = EventGeometry.TrophyOnPodium;
                Quad(podiumGo.transform, "Emblem", new Vector3(0f, 4.2f, -2.03f), Quaternion.Euler(0f, 180f, 0f), new Vector2(3.2f, 3.2f), ApexMaterials.Get(Emblem));
            }

            if (layout.timingTower.placed)
            {
                var tower = new PartSet();
                EventGeometry.TimingTower(tower.B);
                tower.B(EventGeometry.Navy).Box(new Vector3(0f, -1.5f, 0f), new Vector3(3.2f, 3f, 1.1f));
                var towerGo = Place(parent, "TimingTower", tower, layout.timingTower);
                // Quads and TextMeshes are seen from their -Z side: turn them to the tower's +z face.
                var front = Quaternion.Euler(0f, 180f, 0f);
                Quad(towerGo.transform, "Emblem", new Vector3(0f, 14.6f, 0.47f), front, new Vector2(1.1f, 1.1f), ApexMaterials.Get(Emblem));
                if (font != null)
                {
                    var display = towerGo.AddComponent<TimingTowerComponent>();
                    // After the 180° turn the text's +x is the tower's -x: rows start at local +1.2 (viewer's left).
                    for (int r = 0; r < TimingTowerComponent.Rows; r++)
                        display.Add(Text(towerGo.transform, font, new Vector3(1.2f, 13.2f - r * 1.12f, 0.47f), front, "", EventGeometry.IvoryC, 0.8f, 2.4f, TextAnchor.MiddleLeft));
                }
                ci.EventFootprints.Add(Footprint(towerGo.transform, Vector3.zero, 5f));
            }

            if (layout.raceControl.placed)
            {
                var rc = new PartSet();
                EventGeometry.RaceControl(rc.B);
                rc.B(EventGeometry.Navy).Box(new Vector3(6f, -1.5f, 0f), new Vector3(12f, 3f, 16f));
                var rcGo = Place(parent, "RaceControl", rc, layout.raceControl);
                var box = rcGo.AddComponent<BoxCollider>();
                box.center = new Vector3(6f, 4f, 0f);
                box.size = new Vector3(12f, 8f, 16f);
                if (font != null)
                    Text(rcGo.transform, font, new Vector3(-0.02f, 4.4f, 0f), toTrack, "YARIŞ KONTROL", EventGeometry.NavyC, 0.7f, 9f);
                ci.EventFootprints.Add(Footprint(rcGo.transform, new Vector3(6f, 0f, 0f), 12f));
            }

            foreach (var (pos, len, pole) in layout.flags) ci.EventFlags.Add((pos.ToUnity(), len, pole, 1));
        }

        static GameObject Place(Transform parent, string name, PartSet set, EventLayout.Placement at)
        {
            var go = EnvironmentBuilder.AddMeshObject(parent, name, set.parts, true);
            go.transform.SetPositionAndRotation(at.position.ToUnity(), Quaternion.Euler(0f, at.yaw, 0f));
            return go;
        }

        static (float x, float z, float radius) Footprint(Transform t, Vector3 local, float radius)
        {
            var p = t.TransformPoint(local);
            return (p.x, p.z, radius);
        }

        static void Quad(Transform parent, string name, Vector3 pos, Quaternion rot, Vector2 size, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = CarBuilder.PrimitiveMesh(PrimitiveType.Quad);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        static TextMesh Text(Transform parent, Font font, Vector3 pos, Quaternion rot, string text, Color colour, float height, float maxWidth, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = text;
            tm.anchor = anchor;
            tm.alignment = TextAlignment.Left;
            tm.fontSize = 64;
            // Built-in font at fontSize 64: line height ≈ 6.4 × characterSize, capitals ≈ 0.7 of that,
            // average glyph width ≈ 0.6 of the line height.
            float byHeight = height / (6.4f * 0.7f);
            float byWidth = text.Length > 0 ? maxWidth / (text.Length * 0.6f * 6.4f) : byHeight;
            tm.characterSize = Mathf.Min(byHeight, byWidth);
            tm.color = colour;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return tm;
        }

        // --- Materials --------------------------------------------------------------------------

        static bool materialsReady;

        static Material Lit(string name, Color c, float smooth, float metallic = 0f, Texture2D tex = null, Vector2? tiling = null)
        {
            var m = new Material(ApexMaterials.LitShader()) { name = "Event_" + name, enableInstancing = true };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (tex != null)
            {
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
                if (tiling.HasValue) m.mainTextureScale = tiling.Value;
            }
            return m;
        }

        static Texture2D Texture(string name, float[] rgba, int n, TextureWrapMode wrap)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = name, wrapMode = wrap, anisoLevel = 4 };
            var px = new Color[n * n];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2], rgba[i * 4 + 3]);
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        /// <summary>Event palette, İznik tiles, the emblem and the grandstand seat colours (registered once).</summary>
        public static void EnsureMaterials()
        {
            if (materialsReady) return;
            materialsReady = true;
            ApexMaterials.Register(EventGeometry.Red, Lit("Red", EventGeometry.RedC, 0.45f));
            ApexMaterials.Register(EventGeometry.Turquoise, Lit("Turquoise", EventGeometry.TurquoiseC, 0.45f));
            ApexMaterials.Register(EventGeometry.Gold, Lit("Gold", EventGeometry.GoldC, 0.62f, 0.75f));
            ApexMaterials.Register(EventGeometry.Navy, Lit("Navy", EventGeometry.NavyC, 0.35f));
            ApexMaterials.Register(EventGeometry.Ivory, Lit("Ivory", EventGeometry.IvoryC, 0.25f));
            ApexMaterials.Register(EventGeometry.Dark, Lit("GarageDark", new Color(0.05f, 0.055f, 0.06f), 0.3f));
            ApexMaterials.Register(EventGeometry.Tile, Lit("IznikTile", Color.white, 0.7f, 0f, Texture("IznikTile", EventArt.IznikTile(128), 128, TextureWrapMode.Repeat), new Vector2(6f, 4f)));
            var emblem = Lit("Emblem", Color.white, 0.35f, 0f, Texture("EventEmblem", EventArt.Emblem(256), 256, TextureWrapMode.Clamp));
            if (emblem.HasProperty("_AlphaClip")) emblem.SetFloat("_AlphaClip", 1f);
            if (emblem.HasProperty("_Cutoff")) emblem.SetFloat("_Cutoff", 0.5f);
            emblem.EnableKeyword("_ALPHATEST_ON");
            emblem.renderQueue = (int)RenderQueue.AlphaTest;
            ApexMaterials.Register(Emblem, emblem);
            // Grandstands in the event colours: turquoise seats, red accent rows, ivory canopies.
            ApexMaterials.Register(EnvironmentGeometry.Seat, Lit("Seat", EventGeometry.TurquoiseC, 0.35f));
            ApexMaterials.Register(EnvironmentGeometry.SeatAccent, Lit("SeatAccent", EventGeometry.RedC, 0.35f));
            ApexMaterials.Register(EnvironmentGeometry.Canopy, Lit("Canopy", EventGeometry.IvoryC, 0.2f));
        }
    }
}
