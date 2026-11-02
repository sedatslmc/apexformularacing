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
        public const float BlendRate = 7f, Lift = 0.04f, MaxAlpha = 0.85f;

        RacingLineGuide guide;
        Mesh mesh;
        MeshRenderer meshRenderer;
        Color32[] colours;
        Color[] current, target;
        readonly List<RacingLineGuide.Dash> dashes = new List<RacingLineGuide.Dash>();
        readonly List<int> active = new List<int>();
        readonly HashSet<int> activeSet = new HashSet<int>();

        public void Build(RacingLineGuide g)
        {
            guide = g;
            int n = g.DashCount;
            var verts = new Vector3[n * 4];
            var uvs = new Vector2[n * 4];
            var tris = new int[n * 6];
            float half = RacingLineGuide.DashLength * 0.5f, w = RacingLineGuide.DashHalfWidth;
            for (int i = 0; i < n; i++)
            {
                g.DashFrame(i, out var c, out var f, out var r);
                var centre = c.ToUnity(); var fwd = f.ToUnity().normalized; var right = r.ToUnity().normalized;
                int v = i * 4;
                verts[v] = OnRoad(centre - fwd * half - right * w);
                verts[v + 1] = OnRoad(centre - fwd * half + right * w);
                verts[v + 2] = OnRoad(centre + fwd * half + right * w);
                verts[v + 3] = OnRoad(centre + fwd * half - right * w);
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
            colours = new Color32[verts.Length];
            mesh.colors32 = colours;
            current = new Color[n];
            target = new Color[n];
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.sharedMaterial = LineMaterial();
        }

        static readonly RaycastHit[] hits = new RaycastHit[8];

        /// <summary>Drops a point onto the track collider below it (the real road surface incl. camber), 4 cm above.
        /// Cars (rigidbodies) on the grid are ignored; the highest static surface within ±3 m wins.</summary>
        static Vector3 OnRoad(Vector3 p)
        {
            int n = Physics.RaycastNonAlloc(p + Vector3.up * 3f, Vector3.down, hits, 6f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NegativeInfinity;
            Vector3 point = p, normal = Vector3.up;
            for (int i = 0; i < n; i++)
            {
                if (hits[i].collider.attachedRigidbody != null) continue;
                if (hits[i].point.y > best) { best = hits[i].point.y; point = hits[i].point; normal = hits[i].normal; }
            }
            return point + normal * Lift;
        }

        static Material LineMaterial()
        {
            var shader = Resources.Load<Shader>("Shaders/ApexRacingLine");
            if (shader == null || !shader.isSupported) shader = Shader.Find("Sprites/Default");
            return new Material(shader) { name = "RacingLineGuide", renderQueue = 2970 };
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
            guide.Evaluate(race.Player.raw, sim.ForwardSpeed, Mathf.Clamp(grip, 0.3f, 1f), mode, settings.dynamicBrakeLine, dashes);
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
        }
    }
}
