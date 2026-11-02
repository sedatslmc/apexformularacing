using System.Collections.Generic;
using Apex.AI;
using Apex.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Apex.Game
{
    /// <summary>
    /// Renders the dynamic racing line (Apex.AI.RacingLineGuide; docs/RACING_LINE_GUIDE.md): one mesh with a quad per
    /// dash for the whole lap, laid on the road (raycast against the track colliders at build time, 4 cm above),
    /// whose vertex colours are updated each frame for the dashes ahead of the player's car. Colours blend towards
    /// their hint over ~0.15 s, so a change never flickers; dashes outside the window fade out. Physics and the
    /// guide's maths run in Apex.Simulation; this only draws. No allocation per frame once the lists have grown.
    /// </summary>
    public sealed class RacingLineGuideComponent : MonoBehaviour
    {
        public RaceController race;
        public SettingsData settings;

        /// <summary>Hint colours (linear-ish sRGB): calm green, yellow, orange, red.</summary>
        public static readonly Color Green = new Color(0.22f, 0.95f, 0.45f), Yellow = new Color(1f, 0.86f, 0.18f),
            Orange = new Color(1f, 0.52f, 0.1f), Red = new Color(1f, 0.16f, 0.12f);
        public const float BlendRate = 7f, Lift = 0.05f, MaxAlpha = 0.9f;

        RacingLineGuide guide;
        Mesh mesh;
        MeshRenderer meshRenderer;
        Color32[] colours;
        Color[] current, target;
        readonly List<RacingLineGuide.Dash> dashes = new List<RacingLineGuide.Dash>();
        readonly List<int> active = new List<int>();
        readonly HashSet<int> activeSet = new HashSet<int>();

        /// <summary>What the line is doing (F3 telemetry, console): shader, dashes on the road, dashes visible now.</summary>
        public string Diagnostics { get; private set; } = "";
        string shaderName = "";
        int cornersOnRoad, totalCorners, visibleNow;
        float noDashesFor;
        bool warned;

        /// <param name="road">the track's road triangles (CircuitInstance.Surface); the dashes are laid on them</param>
        public void Build(RacingLineGuide g, Apex.Tracks.RoadSurface road)
        {
            guide = g;
            int n = g.DashCount;
            // On the RoadSurface triangles (same as the road mesh and its collider), not via Unity physics: the track
            // colliders may not be in the physics scene yet when the session is built.
            var world = road != null ? new Apex.Tracks.CollisionWorld(road.All) : null;
            var quads = g.DashQuads(world, Lift, out cornersOnRoad);
            totalCorners = quads.Length;
            var verts = new Vector3[quads.Length];
            var uvs = new Vector2[quads.Length];
            var tris = new int[n * 6];
            for (int i = 0; i < n; i++)
            {
                int v = i * 4;
                for (int k = 0; k < 4; k++) verts[v + k] = quads[v + k].ToUnity();
                uvs[v] = new Vector2(-1f, 0f); uvs[v + 1] = new Vector2(1f, 0f); uvs[v + 2] = new Vector2(1f, 1f); uvs[v + 3] = new Vector2(-1f, 1f);
                int t = i * 6;
                tris[t] = v; tris[t + 1] = v + 2; tris[t + 2] = v + 1;
                tris[t + 3] = v; tris[t + 4] = v + 3; tris[t + 5] = v + 2;
            }
            mesh = new Mesh { name = "RacingLineGuide", indexFormat = verts.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.MarkDynamic();
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            // A little margin so the whole-lap mesh is never culled by a too-tight box.
            var b = mesh.bounds; b.Expand(10f); mesh.bounds = b;
            colours = new Color32[verts.Length];
            mesh.colors32 = colours;
            current = new Color[n];
            target = new Color[n];
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.sharedMaterial = LineMaterial(out shaderName);
            Debug.Log($"[Apex] Racing line guide: {n} dashes, {cornersOnRoad}/{totalCorners} corners on the road surface, shader {shaderName}.");
        }

        /// <summary>
        /// Material for the dashes: the glow shader (Apex/RacingLine); if it is missing or does not compile for this
        /// platform, URP's particle unlit shader set to alpha blending (it multiplies the vertex colour); last resort
        /// the built-in sprite shader. Each one shows the vertex colours.
        /// </summary>
        static Material LineMaterial(out string used)
        {
            var shader = Resources.Load<Shader>("Shaders/ApexRacingLine");
            if (shader != null && shader.isSupported)
            {
                var m = new Material(shader) { name = "RacingLineGuide" };
                if (m.passCount > 0) { m.renderQueue = 2970; used = shader.name; return m; }
            }
            var particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (particles != null && particles.isSupported)
            {
                var m = new Material(particles) { name = "RacingLineGuide" };
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_Cull", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white * 1.4f);
                m.renderQueue = 2970;
                used = particles.name + " (fallback)";
                return m;
            }
            var sprite = Shader.Find("Sprites/Default");
            used = sprite != null ? sprite.name + " (fallback)" : "none";
            return sprite != null ? new Material(sprite) { name = "RacingLineGuide", renderQueue = 2970 } : null;
        }

        static Color HintColour(RacingLineGuide.Hint h) =>
            h == RacingLineGuide.Hint.Red ? Red : h == RacingLineGuide.Hint.Orange ? Orange : h == RacingLineGuide.Hint.Yellow ? Yellow : Green;

        void LateUpdate()
        {
            if (guide == null || race == null || race.Player == null || settings == null) return;
            var mode = (RacingLineGuide.Mode)Mathf.Clamp(settings.racingLineMode, 0, 2);
            bool show = mode != RacingLineGuide.Mode.Off;
            if (meshRenderer.enabled != (show || active.Count > 0)) meshRenderer.enabled = show || active.Count > 0;
            var sim = race.Player.Sim;
            // The same grip the AI plans with: tyre temperature, wear and the wet track.
            float grip = 0f;
            foreach (var t in sim.tires) grip += t.GripMultiplier(sim.trackWetness) / 4f;
            guide.Evaluate(race.Player.raw, sim.ForwardSpeed, Mathf.Clamp(grip, 0.3f, 1f), mode, settings.dynamicBrakeLine, dashes,
                RacingLineGuide.AdviceScale(settings.brakeAdvice));
            // Targets: visible dashes get their hint colour and fade; everything that was visible fades out.
            for (int k = 0; k < active.Count; k++) target[active[k]].a = 0f;
            foreach (var d in dashes)
            {
                var c = HintColour(d.hint);
                c.a = d.alpha * MaxAlpha;
                target[d.index] = c;
                if (activeSet.Add(d.index)) { active.Add(d.index); if (current[d.index].a <= 0f) { current[d.index] = c; current[d.index].a = 0f; } }
            }
            float k01 = 1f - Mathf.Exp(-BlendRate * Time.deltaTime);
            bool dirty = false;
            for (int k = active.Count - 1; k >= 0; k--)
            {
                int i = active[k];
                var c = Color.Lerp(current[i], target[i], k01);
                if (target[i].a <= 0f && c.a < 0.01f) { c.a = 0f; active.RemoveAt(k); activeSet.Remove(i); }
                current[i] = c;
                var c32 = (Color32)c;
                int v = i * 4;
                if (!colours[v].Equals(c32)) { colours[v] = colours[v + 1] = colours[v + 2] = colours[v + 3] = c32; dirty = true; }
            }
            if (dirty) mesh.colors32 = colours;
            visibleNow = dashes.Count;
            Diagnostics = $"Ideallinie: {mode}, {visibleNow} Segmente sichtbar, {cornersOnRoad}/{totalCorners} Ecken auf der Fahrbahn, Shader {shaderName}";
            // A shown line with nothing to show for 3 s of driving is a fault worth one console line.
            if (show && visibleNow == 0 && sim.ForwardSpeed > 5f) noDashesFor += Time.deltaTime; else noDashesFor = 0f;
            if (noDashesFor > 3f && !warned) { warned = true; Debug.LogWarning("[Apex] Racing line guide shows no dashes: " + Diagnostics); }
        }
    }
}
