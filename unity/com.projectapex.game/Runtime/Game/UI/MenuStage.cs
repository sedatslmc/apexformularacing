using System.Collections.Generic;
using Apex.UI;
using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// The main-menu showroom stage (docs/UI_UX_OVERHAUL.md, mockup ui_mock_main_menu.jpg): a dark glossy disc with a
    /// thin cyan LED ring, two LED strips and a short Turkish-red accent line, lit by a soft key and two cool rim
    /// lights; the car stands in the middle. With the Istanbul backdrop the stage floats on the Bosphorus off
    /// Ortaköy (<see cref="BosphorusStage"/>), facing the mosque and the bridge. Emission stays restrained (no neon).
    /// </summary>
    public static class MenuStage
    {
        /// <summary>Stage position on the water off Ortaköy (game frame) and the heading (deg) the camera looks along:
        /// north-north-east, so the mosque and the 15 July Martyrs Bridge stand behind the car.</summary>
        public static readonly Vector3 BosphorusStage = new Vector3(2075f, 1.1f, 815f);
        public const float BosphorusViewHeading = 24f;
        public const float Radius = 6.5f;

        public static GameObject Build(Transform parent)
        {
            var root = new GameObject("MenuStage");
            root.transform.SetParent(parent, false);
            var parts = new Dictionary<string, MeshBuilder>();
            MeshBuilder B(string k) { if (!parts.TryGetValue(k, out var m)) parts[k] = m = new MeshBuilder(); return m; }
            // Disc 0.12 m thick (Unity's cylinder is 2 m tall: scale y 0.06), top at y = 0.
            var floor = new GameObject("StageFloor");
            floor.transform.SetParent(root.transform, false);
            floor.transform.localScale = new Vector3(Radius * 2f, 0.06f, Radius * 2f);
            floor.transform.localPosition = new Vector3(0f, -0.06f, 0f);
            floor.AddComponent<MeshFilter>().sharedMesh = CarBuilder.PrimitiveMesh(PrimitiveType.Cylinder);
            floor.AddComponent<MeshRenderer>().sharedMaterial = Material("StageFloor");
            var ring = new List<Vector3>();
            for (int i = 0; i <= 160; i++)
            {
                float a = i * Mathf.PI * 2f / 160f;
                ring.Add(new Vector3(Mathf.Cos(a) * (Radius - 0.22f), 0.012f, Mathf.Sin(a) * (Radius - 0.22f)));
            }
            B("StageLed").TubePath(ring, 0.022f, 6, false);
            foreach (float s in new[] { -1f, 1f }) B("StageLed").Box(new Vector3(s * 2.2f, 0.008f, 0f), new Vector3(0.035f, 0.012f, 5.2f));
            B("StageRed").Box(new Vector3(0f, 0.008f, Radius - 0.6f), new Vector3(2.8f, 0.012f, 0.04f));
            foreach (var kv in parts)
            {
                var go = new GameObject(kv.Key);
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = kv.Value.ToMesh(kv.Key);
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = Material(kv.Key);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            // Lights: soft key from above-front, cool rims from behind (no shadows on the rims).
            Spot(root.transform, "StageKey", new Vector3(1.5f, 7.5f, -3f), Vector3.zero, new Color(0.92f, 0.95f, 1f), 9f, 48f, true);
            Spot(root.transform, "StageRimL", new Vector3(-5.5f, 2.6f, 4.5f), new Vector3(0f, 0.6f, 0f), ApexUi.C(UiTokens.Cyan) * 0.9f + Color.white * 0.1f, 4f, 40f, false);
            Spot(root.transform, "StageRimR", new Vector3(5.5f, 2.2f, 5f), new Vector3(0f, 0.6f, 0f), ApexUi.C(UiTokens.Cyan) * 0.9f + Color.white * 0.1f, 3f, 40f, false);
            return root;
        }

        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        static Material Material(string key)
        {
            if (Materials.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(ApexMaterials.LitShader()) { name = "Menu_" + key };
            Color baseC, emit = Color.black;
            float smooth = 0.3f, metal = 0f;
            switch (key)
            {
                case "StageFloor": baseC = new Color(0.05f, 0.075f, 0.1f); smooth = 0.78f; metal = 0.3f; break;
                case "StageRed": baseC = ApexUi.C(UiTokens.Red); emit = ApexUi.C(UiTokens.Red) * 1.4f; break;
                default: baseC = ApexUi.C(UiTokens.Cyan); emit = ApexUi.C(UiTokens.Cyan) * 1.6f; break;
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseC);
            if (m.HasProperty("_Color")) m.SetColor("_Color", baseC);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            if (emit.maxColorComponent > 0f && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emit);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            Materials[key] = m;
            return m;
        }

        static void Spot(Transform parent, string name, Vector3 pos, Vector3 target, Color colour, float intensity, float angle, bool shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.LookRotation((target - pos).normalized);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.spotAngle = angle;
            l.range = 18f;
            l.color = colour;
            l.intensity = intensity;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        }

        /// <summary>
        /// Cinematic menu camera around a stage (local to it): looks along <paramref name="viewHeading"/> with the car in
        /// the right third, swaying ±10° over 48 s with a slight dolly; reduced motion holds it still.
        /// </summary>
        public static void MenuCamera(Camera cam, Transform stage, float viewHeading, float time, bool reducedMotion)
        {
            float sway = reducedMotion ? 0f : Mathf.Sin(time * 2f * Mathf.PI / 48f) * 10f;
            float dolly = reducedMotion ? 0f : Mathf.Sin(time * 2f * Mathf.PI / 31f) * 0.35f;
            float heading = (viewHeading + sway) * Mathf.Deg2Rad;
            var fwd = new Vector3(Mathf.Sin(heading), 0f, Mathf.Cos(heading));
            var right = new Vector3(fwd.z, 0f, -fwd.x);
            var carPos = stage.position + Vector3.up * 0.45f;
            // Camera behind-left of the car, aiming left of it so the car sits in the right third.
            var eye = carPos - fwd * (7.6f + dolly) - right * 1.2f + Vector3.up * 1.05f;
            var aim = carPos - right * 2.3f + fwd * 6f + Vector3.up * 0.4f;
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(aim - eye));
            cam.fieldOfView = 34f;
        }
    }
}
