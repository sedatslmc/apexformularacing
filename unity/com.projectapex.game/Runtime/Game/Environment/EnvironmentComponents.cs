using System;
using System.Collections.Generic;
using Apex.Environment;
using UnityEngine;
using UnityEngine.Rendering;

namespace Apex.Game
{
    /// <summary>
    /// Runtime state of the circuit environment: shared wind, night lighting and wet
    /// surfaces for the environment materials, and car proximity for crowd reactions.
    /// </summary>
    public sealed class EnvironmentInstance : MonoBehaviour
    {
        public int quality;
        public WindField wind;
        public LandModel land;
        public float ambienceVolume = 0.5f;
        public readonly List<(Vector3 pos, float length, float pole, int detail)> flagSpots = new List<(Vector3, float, float, int)>();
        public readonly List<Vector3> grandstandCentres = new List<Vector3>();
        /// <summary>Street-lamp heads (world positions) for their reflections on the water.</summary>
        public readonly List<Vector3> lampHeads = new List<Vector3>();
        /// <summary>Current street/window light level 0..1.</summary>
        public float ArtificialLight => Mathf.Max(0f, artificial);

        public float WindSpeed { get; private set; }
        /// <summary>Rain intensity 0..1 (set by WeatherVisuals; roughens the water).</summary>
        public float RainIntensity { get; set; }
        public Vector3 WindDirection
        {
            get
            {
                var d = wind != null ? wind.Direction : new System.Numerics.Vector2(0f, 1f);
                return new Vector3(d.X, 0f, d.Y);
            }
        }

        readonly List<(Material mat, Color full)> lights = new List<(Material, Color)>();
        readonly List<(Material mat, Color baseColor, float smooth)> wettable = new List<(Material, Color, float)>();
        float artificial = -1f, wetness = -1f;
        VehicleController[] cars = new VehicleController[0];
        float carRefresh;

        public void AddLight(Material m, Color full)
        {
            if (m == null) return;
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            lights.Add((m, full));
        }

        public void AddWettable(Material m)
        {
            if (m == null) return;
            var c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.gray;
            float s = m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : 0.2f;
            wettable.Add((m, c, s));
        }

        readonly List<(Light light, float full)> realLights = new List<(Light, float)>();

        /// <summary>A real light that follows the night level (off in daylight).</summary>
        public void AddRealLight(Light l, float fullIntensity)
        {
            if (l == null) return;
            realLights.Add((l, fullIntensity));
            l.intensity = fullIntensity * Mathf.Max(0f, artificial);
            l.enabled = artificial > 0.02f;
        }

        /// <summary>0 = daylight .. 1 = all street lamps, windows, floodlights and bridge lights on.</summary>
        public void SetArtificialLight(float a)
        {
            if (Mathf.Abs(a - artificial) < 0.005f) return;
            artificial = a;
            foreach (var (mat, full) in lights)
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", full * a);
            foreach (var (l, full) in realLights)
            {
                if (l == null) continue;
                l.enabled = a > 0.02f;
                l.intensity = full * a;
            }
        }

        /// <summary>Wet facades, pavements and roofs: darker albedo, higher smoothness.</summary>
        public void SetWetness(float w)
        {
            if (Mathf.Abs(w - wetness) < 0.01f) return;
            wetness = w;
            foreach (var (mat, c, s) in wettable)
            {
                var col = Color.Lerp(c, c * 0.62f, w);
                col.a = c.a;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", col);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", Mathf.Lerp(s, 0.75f, w));
            }
        }

        /// <summary>0..1: how close the nearest car is (crowds stand up and cheer).</summary>
        public float Excitement(Vector3 p)
        {
            float best = float.MaxValue;
            foreach (var c in cars)
                if (c != null) best = Mathf.Min(best, (c.transform.position - p).sqrMagnitude);
            return Mathf.Clamp01(1f - Mathf.Sqrt(best) / 160f);
        }

        void Update()
        {
            if (wind != null) WindSpeed = wind.Speed(Time.timeSinceLevelLoad);
            carRefresh -= Time.deltaTime;
            if (carRefresh <= 0f)
            {
                carRefresh = 2f;
                cars = FindObjectsByType<VehicleController>(FindObjectsSortMode.None);
            }
        }
    }

    /// <summary>
    /// Turkish flag on a pole; cloth animated from the shared wind (see <see cref="FlagCloth"/>).
    /// Detail 0..2 for ordinary flags (fewer vertices and updates with distance); <see cref="HeroDetail"/>
    /// for the monumental flag (fine cloth grid, updated every frame, own pole built by the caller).
    /// </summary>
    public sealed class FlagComponent : MonoBehaviour
    {
        EnvironmentInstance env;
        Transform cloth;
        Mesh mesh;
        Vector3[] verts;
        int nu, nv, detail;
        float length, height, phase, yaw;
        int frame;

        public const int HeroDetail = -1;
        /// <summary>Distance (m) from the pole axis to the hoist (a thick pole would otherwise cut through the cloth).</summary>
        public float hoistOffset;

        public void Setup(EnvironmentInstance environment, Vector3 basePos, float flagLength, float pole, int detailLevel, Material flagMat)
        {
            bool hero = detailLevel == HeroDetail;
            env = environment;
            detail = detailLevel;
            length = flagLength;
            height = flagLength / TurkishFlagDesign.AspectRatio;
            phase = basePos.x * 0.013f + basePos.z * 0.007f;
            transform.position = basePos;
            if (!hero) BuildPole(pole);
            cloth = new GameObject("Cloth").transform;
            cloth.SetParent(transform, false);
            cloth.localPosition = Vector3.up * (pole - 0.05f) + Vector3.right * 0f;
            nu = hero ? 46 : detail == 0 ? 16 : detail == 1 ? 11 : 6;
            nv = hero ? 31 : detail == 0 ? 10 : detail == 1 ? 7 : 4;
            BuildCloth(flagMat, hero || detail == 0);
        }

        void BuildPole(float pole)
        {
            var poleMb = new MeshBuilder();
            float r = Mathf.Clamp(pole * 0.012f, 0.04f, 0.25f);
            poleMb.Tube(Vector3.zero, Vector3.up * (pole + 0.25f), r, detail == 2 ? 4 : 8, true);
            poleMb.Box(Vector3.up * (pole + 0.3f), Vector3.one * r * 2.6f);
            var poleGo = EnvironmentBuilder.AddMeshObject(transform, "Pole", new Dictionary<string, MeshBuilder> { [EnvironmentGeometry.FlagPole] = poleMb }, detail < 2, false);
            poleGo.transform.localPosition = Vector3.zero;
        }

        void BuildCloth(Material flagMat, bool shadows)
        {
            verts = new Vector3[nu * nv];
            var uv = new Vector2[nu * nv];
            var tris = new List<int>();
            for (int j = 0; j < nv; j++)
                for (int i = 0; i < nu; i++)
                {
                    float u = i / (nu - 1f), v = j / (nv - 1f);
                    uv[j * nu + i] = new Vector2(u, 1f - v);
                    if (i < nu - 1 && j < nv - 1)
                    {
                        int a = j * nu + i;
                        tris.Add(a); tris.Add(a + 1); tris.Add(a + nu + 1);
                        tris.Add(a); tris.Add(a + nu + 1); tris.Add(a + nu);
                    }
                }
            mesh = new Mesh { name = "FlagCloth" };
            mesh.MarkDynamic();
            Evaluate(0f, 5f);
            mesh.vertices = verts;
            mesh.uv = uv;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(new Vector3(length * 0.5f, -height * 0.5f, 0f), new Vector3(length * 2.2f, length * 2.2f, length * 1.2f));
            cloth.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = cloth.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = flagMat;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            yaw = TargetYaw();
        }

        float TargetYaw()
        {
            var w = env != null ? env.WindDirection : Vector3.forward;
            // Yaw that maps local +x (hoist to fly) onto the wind direction.
            return Mathf.Atan2(-w.z, w.x) * Mathf.Rad2Deg;
        }

        void Evaluate(float t, float speed)
        {
            for (int j = 0; j < nv; j++)
                for (int i = 0; i < nu; i++)
                {
                    var p = FlagCloth.Evaluate(i / (nu - 1f), j / (nv - 1f), length, height, speed, t, phase);
                    verts[j * nu + i] = new Vector3(p.X + hoistOffset, p.Y, p.Z);
                }
        }

        void LateUpdate()
        {
            if (env == null || mesh == null) return;
            var cam = Camera.main;
            float dist = cam != null ? (cam.transform.position - transform.position).magnitude : 0f;
            // Distant flags update less often; very distant ones keep their last pose.
            int interval = detail == HeroDetail ? (dist < 3000f ? 1 : 2) : dist < 250f ? 1 : dist < 900f ? 3 : 8;
            if (dist > 4000f || (++frame % interval) != 0) return;
            yaw = Mathf.MoveTowardsAngle(yaw, TargetYaw(), 20f * Time.deltaTime * interval);
            cloth.rotation = Quaternion.Euler(0f, yaw, 0f);
            float speed = env.WindSpeed * (1f + 0.08f * Mathf.Sin(Time.time * 0.7f + phase));
            Evaluate(Time.timeSinceLevelLoad, speed);
            mesh.vertices = verts;
            mesh.RecalculateNormals();
        }
    }

    /// <summary>Moves ferries and ships along their <see cref="MarineRoute"/>s with a gentle roll.</summary>
    public sealed class MarineTrafficComponent : MonoBehaviour
    {
        readonly List<(MarineRoute route, Transform vessel, float offset, float waterY)> vessels = new List<(MarineRoute, Transform, float, float)>();

        public void Add(MarineRoute route, Transform vessel, float offset, float waterY) => vessels.Add((route, vessel, offset, waterY));

        void Update()
        {
            double t = Time.timeSinceLevelLoad;
            foreach (var (route, vessel, offset, waterY) in vessels)
            {
                route.Sample(t, offset, out var p, out float heading, out float speed);
                float roll = Mathf.Sin((float)t * 0.6f + offset) * 0.8f;
                float pitch = Mathf.Sin((float)t * 0.45f + offset * 1.3f) * 0.4f;
                vessel.SetPositionAndRotation(new Vector3(p.X, waterY + Mathf.Sin((float)t * 0.5f + offset) * 0.12f, p.Y),
                    Quaternion.Euler(pitch, heading * Mathf.Rad2Deg, roll));
            }
        }
    }

    /// <summary>
    /// GPU-instanced spectators on the grandstands, in batches by shirt colour. They
    /// idle with a small sway and jump up when a car passes close by.
    /// </summary>
    public sealed class CrowdComponent : MonoBehaviour
    {
        public EnvironmentInstance env;
        static readonly Color[] ShirtColours =
        {
            new Color(0.82f, 0.08f, 0.1f), new Color(0.92f, 0.92f, 0.92f), new Color(0.0f, 0.7f, 0.9f),
            new Color(0.07f, 0.12f, 0.3f), new Color(0.95f, 0.75f, 0.1f), new Color(0.12f, 0.12f, 0.13f),
        };

        sealed class Batch
        {
            public int colour;
            public Vector3 centre;
            public readonly List<(Vector3 pos, Quaternion rot, float scale, float phase, bool still)> people = new List<(Vector3, Quaternion, float, float, bool)>();
            public Matrix4x4[] matrices, flagMatrices;
        }

        bool HasFlag(float phase) => Mathf.Repeat(phase * 7.31f, 1f) < handFlagShare;

        readonly List<Batch> batches = new List<Batch>();
        readonly Dictionary<(int, int, int), Batch> open = new Dictionary<(int, int, int), Batch>();
        Mesh mesh, handFlag;
        Material[] shirts;
        Material skin, pole, cloth;
        /// <summary>Share of spectators waving a small Turkish hand flag.</summary>
        public float handFlagShare = 0.22f;

        /// <summary>Standing height (m) and facing (degrees) of a spectator, shared with props attached to them.</summary>
        public static float Height(float rnd) => 1.62f + Mathf.Abs(rnd) * 0.25f;
        public static float Facing(float yawDeg, float rnd) => yawDeg + rnd * 20f;

        /// <param name="still">photographers: no jumping and no hand flag (their camera is a static mesh)</param>
        public void Add(Vector3 pos, float yawDeg, float rnd, bool still = false)
        {
            int colour = Mathf.Abs((int)(rnd * 1000f)) % ShirtColours.Length;
            // Fans in red and white are the majority.
            if (rnd > 0.3f) colour = rnd > 0.65f ? 0 : 1;
            var key = (colour, Mathf.FloorToInt(pos.x / 80f), Mathf.FloorToInt(pos.z / 80f));
            if (!open.TryGetValue(key, out var b) || b.people.Count >= 1023)
            {
                b = new Batch { colour = colour };
                batches.Add(b);
                open[key] = b;
            }
            b.people.Add((pos, Quaternion.Euler(0f, Facing(yawDeg, rnd), 0f), Height(rnd), rnd * 40f, still));
        }

        void Start()
        {
            var shirt = new MeshBuilder();
            var skinMb = new MeshBuilder();
            EnvironmentGeometry.SpectatorMesh(shirt, skinMb);
            mesh = EnvironmentBuilder.Combine(new Dictionary<string, MeshBuilder> { ["Shirt"] = shirt, [EnvironmentGeometry.Skin] = skinMb }, "Spectator", out var mats);
            skin = mats[1];
            skin.enableInstancing = true;
            shirts = new Material[ShirtColours.Length];
            for (int i = 0; i < shirts.Length; i++) shirts[i] = EnvironmentMaterials.Solid(ShirtColours[i]);
            // Hand flag in the spectator's local frame (unit height): stick from the raised right hand,
            // cloth at the top, flying to the side. Instanced like the spectators.
            var stick = new MeshBuilder();
            var flag = new MeshBuilder();
            stick.Tube(new Vector3(0.17f, 0.78f, 0.04f), new Vector3(0.19f, 1.22f, 0.04f), 0.006f, 4, true);
            float fh = 0.13f, fl = fh * TurkishFlagDesign.AspectRatio;
            flag.QuadFacing(new Vector3(0.19f, 1.22f - fh, 0.04f), new Vector3(0.19f + fl, 1.22f - fh, 0.04f), new Vector3(0.19f + fl, 1.22f, 0.04f), new Vector3(0.19f, 1.22f, 0.04f),
                Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector3.back);
            handFlag = EnvironmentBuilder.Combine(new Dictionary<string, MeshBuilder> { [EnvironmentGeometry.FlagPole] = stick, [EnvironmentGeometry.Gull] = flag }, "HandFlag", out var flagMats);
            pole = flagMats[0];
            pole.enableInstancing = true;
            // The flag material is double-sided; an instancing copy keeps the shared one untouched.
            cloth = new Material(EnvironmentMaterials.Flag) { name = "HandFlag", enableInstancing = true };
            foreach (var b in batches)
            {
                int flags = 0;
                foreach (var p in b.people) if (!p.still && HasFlag(p.phase)) flags++;
                b.flagMatrices = new Matrix4x4[flags];
                b.matrices = new Matrix4x4[b.people.Count];
                var c = Vector3.zero;
                foreach (var p in b.people) c += p.pos;
                b.centre = c / Mathf.Max(1, b.people.Count);
            }
        }

        void Update()
        {
            if (mesh == null) return;
            var cam = Camera.main;
            if (cam == null) return;
            float t = Time.timeSinceLevelLoad;
            foreach (var b in batches)
            {
                if ((b.centre - cam.transform.position).sqrMagnitude > 1100f * 1100f) continue;
                float excite = env != null ? env.Excitement(b.centre) : 0f;
                int f = 0;
                for (int i = 0; i < b.people.Count; i++)
                {
                    var (pos, rot, scale, phase, still) = b.people[i];
                    if (still) { b.matrices[i] = Matrix4x4.TRS(pos, rot, Vector3.one * scale); continue; }
                    float idle = Mathf.Sin(t * 1.3f + phase) * 0.02f;
                    float jump = Mathf.Max(0f, Mathf.Sin(t * 7.5f + phase)) * 0.22f * excite;
                    b.matrices[i] = Matrix4x4.TRS(pos + Vector3.up * (idle + jump), rot, Vector3.one * scale);
                    if (f < b.flagMatrices.Length && HasFlag(phase))
                    {
                        // Waving: the flag swings about the hand, faster and wider when the crowd is excited.
                        float wave = Mathf.Sin(t * (3.2f + 4f * excite) + phase) * (14f + 22f * excite);
                        var hand = new Vector3(0.17f, 0.78f, 0.04f);
                        var m = Matrix4x4.Translate(hand) * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, wave)) * Matrix4x4.Translate(-hand);
                        b.flagMatrices[f++] = b.matrices[i] * m;
                    }
                }
                Graphics.DrawMeshInstanced(mesh, 0, shirts[b.colour], b.matrices, b.matrices.Length, null, ShadowCastingMode.Off, false);
                Graphics.DrawMeshInstanced(mesh, 1, skin, b.matrices, b.matrices.Length, null, ShadowCastingMode.Off, false);
                if (f > 0 && handFlag != null)
                {
                    Graphics.DrawMeshInstanced(handFlag, 0, pole, b.flagMatrices, f, null, ShadowCastingMode.Off, false);
                    Graphics.DrawMeshInstanced(handFlag, 1, cloth, b.flagMatrices, f, null, ShadowCastingMode.Off, false);
                }
            }
        }
    }

    /// <summary>Gulls circling over the water (instanced, wings flap by scaling the V).</summary>
    public sealed class GullComponent : MonoBehaviour
    {
        Mesh mesh;
        Material material;
        readonly List<(Vector3 centre, float radius, float angular, float phase)> birds = new List<(Vector3, float, float, float)>();
        Matrix4x4[] matrices;

        public void Setup(Mesh gullMesh, Material mat)
        {
            mesh = gullMesh;
            material = mat;
            material.enableInstancing = true;
        }

        public void Add(Vector3 centre, float radius, float angularSpeed, float phase) => birds.Add((centre, radius, angularSpeed, phase));

        void Update()
        {
            if (mesh == null || birds.Count == 0) return;
            if (matrices == null || matrices.Length != birds.Count) matrices = new Matrix4x4[birds.Count];
            float t = Time.timeSinceLevelLoad;
            for (int i = 0; i < birds.Count; i++)
            {
                var (c, r, w, ph) = birds[i];
                float a = ph + t * w;
                var pos = c + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(t * 0.3f + ph) * 3f, Mathf.Sin(a) * r);
                var dir = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)) * Mathf.Sign(w);
                // Glide most of the time, flap in bursts.
                float flapping = Mathf.Clamp01(Mathf.Sin(t * 0.4f + ph * 3f) * 2f);
                float wing = Mathf.Lerp(1f, 0.8f + 0.7f * Mathf.Sin(t * 11f + ph), flapping);
                var rot = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, 0f, -12f * Mathf.Sign(w));
                matrices[i] = Matrix4x4.TRS(pos, rot, new Vector3(1.2f, 1.2f * Mathf.Max(0.1f, wing), 1.2f));
            }
            Graphics.DrawMeshInstanced(mesh, 0, material, matrices, matrices.Length, null, ShadowCastingMode.Off, false);
        }
    }

    /// <summary>Instanced trees in spatial batches; nearby batches sway with the shared wind.</summary>
    public sealed class TreeSwayComponent : MonoBehaviour
    {
        public EnvironmentInstance env;
        readonly List<(Mesh mesh, Material[] mats)> kinds = new List<(Mesh, Material[])>();

        sealed class Batch
        {
            public int kind;
            public Vector3 centre;
            public readonly List<(Vector3 pos, float height, float yaw)> trees = new List<(Vector3, float, float)>();
            public Matrix4x4[] matrices;
            public bool built;
        }

        readonly List<Batch> batches = new List<Batch>();
        readonly Dictionary<(int, int, int), Batch> open = new Dictionary<(int, int, int), Batch>();

        public void AddKind(Mesh mesh, Material[] mats)
        {
            foreach (var m in mats) m.enableInstancing = true;
            kinds.Add((mesh, mats));
        }

        public void Add(int kind, Vector3 pos, float height, float yawDeg)
        {
            var key = (kind, Mathf.FloorToInt(pos.x / 250f), Mathf.FloorToInt(pos.z / 250f));
            if (!open.TryGetValue(key, out var b) || b.trees.Count >= 1023)
            {
                b = new Batch { kind = kind };
                batches.Add(b);
                open[key] = b;
            }
            b.trees.Add((pos, height, yawDeg));
        }

        void Update()
        {
            var cam = Camera.main;
            if (cam == null) return;
            float t = Time.timeSinceLevelLoad;
            float speed = env != null ? env.WindSpeed : 3f;
            var wind = env != null ? env.WindDirection : Vector3.forward;
            var axis = Vector3.Cross(Vector3.up, wind);
            foreach (var b in batches)
            {
                if (b.matrices == null)
                {
                    b.matrices = new Matrix4x4[b.trees.Count];
                    var c = Vector3.zero;
                    foreach (var tr in b.trees) c += tr.pos;
                    b.centre = c / Mathf.Max(1, b.trees.Count);
                }
                float d2 = (b.centre - cam.transform.position).sqrMagnitude;
                if (d2 > 2600f * 2600f) continue;
                bool animate = d2 < 450f * 450f;
                if (animate || !b.built)
                {
                    for (int i = 0; i < b.trees.Count; i++)
                    {
                        var (pos, h, yaw) = b.trees[i];
                        float lean = 0.06f * speed + (0.4f + 0.1f * speed) * Mathf.Sin(t * (1.1f + 0.2f * Mathf.Sin(yaw)) + pos.x * 0.05f);
                        var rot = Quaternion.AngleAxis(animate ? lean : 0.06f * speed, axis) * Quaternion.Euler(0f, yaw, 0f);
                        b.matrices[i] = Matrix4x4.TRS(pos, rot, Vector3.one * h);
                    }
                    b.built = true;
                }
                var (mesh, mats) = kinds[b.kind];
                for (int s = 0; s < mats.Length; s++)
                    Graphics.DrawMeshInstanced(mesh, s, mats[s], b.matrices, b.matrices.Length, null, d2 < 600f * 600f ? ShadowCastingMode.On : ShadowCastingMode.Off, true);
            }
        }
    }

    /// <summary>
    /// Drives the strait's material. With the Apex/Water shader: three wave layers drifting downwind at
    /// deep-water speeds, Cox–Munk roughness from wind and rain, and the coast-distance texture
    /// (<see cref="WaterOptics"/>). With the Lit fallback: one scrolling normal map.
    /// </summary>
    public sealed class WaterSurfaceComponent : MonoBehaviour
    {
        public EnvironmentInstance env;
        Material mat;
        bool custom;
        Vector2 offset;
        readonly Vector2[] layerOffsets = new Vector2[3];
        Texture2D coast;
        Vector4 coastRect;
        static readonly int[] WaveIds = { Shader.PropertyToID("_WaveA"), Shader.PropertyToID("_WaveB"), Shader.PropertyToID("_WaveC") };
        static readonly int RoughnessId = Shader.PropertyToID("_Roughness");
        static readonly int LampsId = Shader.PropertyToID("_ApexWaterLamps"), LampColorId = Shader.PropertyToID("_ApexWaterLampColor");
        readonly Vector4[] lamps = new Vector4[WaterOptics.MaxLamps];
        float nextLampPick;

        /// <summary>Coast-distance texture and its world rectangle (minX, minZ, 1/sizeX, 1/sizeZ); owned by this component.</summary>
        public void SetCoast(Texture2D tex, Vector4 rect)
        {
            coast = tex;
            coastRect = rect;
            ApplyCoast();
        }

        void Start()
        {
            mat = GetComponent<MeshRenderer>()?.sharedMaterial;
            custom = mat != null && mat.HasProperty("_WaveA");
            ApplyCoast();
        }

        void ApplyCoast()
        {
            if (!custom || coast == null) return;
            mat.SetTexture("_CoastTex", coast);
            mat.SetVector("_CoastRect", coastRect);
        }

        void Update()
        {
            if (mat == null) return;
            var w = env != null ? env.WindDirection : Vector3.forward;
            float speed = env != null ? env.WindSpeed : 4f;
            if (custom)
            {
                var layers = WaterOptics.Layers(w.x, w.z, speed);
                for (int i = 0; i < layers.Length && i < WaveIds.Length; i++)
                {
                    var l = layers[i];
                    // UV offset of a layer moving at its phase speed (wrapped to keep float precision).
                    var o = layerOffsets[i] - new Vector2(l.dirX, l.dirZ) * (l.speed / l.tile * Time.deltaTime);
                    o.x -= Mathf.Floor(o.x);
                    o.y -= Mathf.Floor(o.y);
                    layerOffsets[i] = o;
                    mat.SetVector(WaveIds[i], new Vector4(o.x, o.y, 1f / l.tile, l.strength));
                }
                mat.SetFloat(RoughnessId, WaterOptics.Roughness(speed, env != null ? env.RainIntensity : 0f));
                UpdateLamps();
                return;
            }
            // Wavelets drift downwind at roughly 0.1 x wind speed (uv unit = 24 m).
            offset += new Vector2(w.x, w.z) * (0.1f * speed / 24f) * Time.deltaTime;
            offset.x %= 1f; offset.y %= 1f;
            mat.mainTextureOffset = offset;
            if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(speed / 10f)));
        }

        /// <summary>
        /// The street lamps nearest the camera as point lights for the water's glitter only (globals, so the
        /// material stays SRP-batcher compatible); rough water stretches them into the familiar light paths.
        /// </summary>
        void UpdateLamps()
        {
            float level = env != null ? env.ArtificialLight : 0f;
            var cam = Camera.main;
            if (level < 0.01f || env == null || env.lampHeads.Count == 0 || cam == null)
            {
                Shader.SetGlobalVector(LampColorId, Vector4.zero);
                return;
            }
            var c = WaterOptics.LampColor;
            Shader.SetGlobalVector(LampColorId, new Vector4(c[0], c[1], c[2], 0f) * (level * WaterOptics.LampIntensity));
            if (Time.unscaledTime < nextLampPick) return;
            nextLampPick = Time.unscaledTime + 0.25f;
            var p = cam.transform.position;
            int count = WaterOptics.NearestLamps(env.lampHeads.Count, i => (env.lampHeads[i] - p).sqrMagnitude, picked);
            for (int k = 0; k < lamps.Length; k++)
                lamps[k] = k < count ? (Vector4)env.lampHeads[picked[k]] + new Vector4(0f, 0f, 0f, 1f) : Vector4.zero;
            Shader.SetGlobalVectorArray(LampsId, lamps);
        }

        readonly int[] picked = new int[WaterOptics.MaxLamps];

        void OnDestroy()
        {
            if (coast != null) Destroy(coast);
            Shader.SetGlobalVector(LampColorId, Vector4.zero);
        }
    }

    /// <summary>
    /// Ambience around the camera: water/harbour, crowd, city and wind loops (generated
    /// procedurally, no recorded assets), mixed by <see cref="AmbienceMixer"/>. Street
    /// canyons get reverb zones (placed by the builder) that also colour the engine sound.
    /// </summary>
    public sealed class AmbienceComponent : MonoBehaviour
    {
        public EnvironmentInstance env;
        public Func<Vector3, float> enclosure;
        AudioSource water, crowd, city, windSrc, oneShot;
        AudioClip horn, gull1, gull2;
        float nextHorn, nextGull;
        readonly System.Random rng = new System.Random(1453);

        /// <summary>Distant ship's horn: two low tones (a fifth apart) with harmonics, slow attack, 2.6 s.</summary>
        static float[] Horn(int rate)
        {
            int n = (int)(rate * 2.6f);
            var d = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = Mathf.Clamp01(t / 0.25f) * Mathf.Clamp01((2.6f - t) / 0.6f);
                float s = 0f;
                foreach (float f in new[] { 98f, 147f })
                    for (int h = 1; h <= 4; h++) s += Mathf.Sin(2f * Mathf.PI * f * h * t) / (h * 1.6f);
                d[i] = s * env * 0.22f;
            }
            return d;
        }

        /// <summary>Gull cry: a falling, slightly rough chirp repeated two or three times.</summary>
        static float[] Gull(int rate, int calls, int seed)
        {
            var r = new System.Random(seed);
            float call = 0.28f, gap = 0.12f;
            int n = (int)(rate * calls * (call + gap));
            var d = new float[n];
            for (int c = 0; c < calls; c++)
            {
                float f0 = 2200f + (float)r.NextDouble() * 400f, f1 = 1300f + (float)r.NextDouble() * 300f;
                int start = (int)(rate * c * (call + gap));
                double ph = 0;
                for (int i = 0; i < call * rate && start + i < n; i++)
                {
                    float t = i / (float)rate;
                    float f = Mathf.Lerp(f0, f1, t / call);
                    ph += 2.0 * Mathf.PI * f / rate;
                    float env = Mathf.Sin(Mathf.PI * t / call);
                    float rough = 1f + 0.35f * Mathf.Sin(2f * Mathf.PI * 70f * t);
                    d[start + i] = (float)Math.Sin(ph) * env * rough * 0.18f;
                }
            }
            return d;
        }

        static AudioClip Clip(string name, float[] data, int rate)
        {
            var clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        AudioSource Source(string name, float[] data, int rate)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            var clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            src.clip = clip;
            src.loop = true;
            src.spatialBlend = 0f;
            src.volume = 0f;
            src.Play();
            return src;
        }

        /// <summary>Loopable noise: one-pole filters, amplitude modulation, crossfaded ends.</summary>
        static float[] Noise(int rate, float seconds, float lowpass, float highpass, Func<float, float> envelope, int seed)
        {
            int n = (int)(rate * seconds), fade = rate / 4;
            var raw = new float[n + fade];
            var rng = new System.Random(seed);
            float lp = 0f, hpIn = 0f, hp = 0f;
            float aL = Mathf.Exp(-2f * Mathf.PI * lowpass / rate), aH = Mathf.Exp(-2f * Mathf.PI * highpass / rate);
            for (int i = 0; i < raw.Length; i++)
            {
                float w = (float)rng.NextDouble() * 2f - 1f;
                lp = lp * aL + w * (1f - aL);
                hp = aH * (hp + lp - hpIn);
                hpIn = lp;
                raw[i] = hp * envelope(i / (float)rate);
            }
            var outp = new float[n];
            float peak = 1e-4f;
            for (int i = 0; i < n; i++)
            {
                float v = raw[i];
                if (i < fade) v = Mathf.Lerp(raw[n + i], v, i / (float)fade);
                outp[i] = v;
                peak = Mathf.Max(peak, Mathf.Abs(v));
            }
            for (int i = 0; i < n; i++) outp[i] *= 0.6f / peak;
            return outp;
        }

        void Start()
        {
            int rate = 22050;
            water = Source("Amb_Water", Noise(rate, 6f, 500f, 40f, t => 0.55f + 0.45f * Mathf.Sin(t * 2.1f) * Mathf.Sin(t * 0.7f + 1f), 11), rate);
            crowd = Source("Amb_Crowd", Noise(rate, 6f, 2200f, 250f, t => 0.7f + 0.3f * Mathf.Sin(t * 1.3f) + 0.2f * Mathf.Sin(t * 3.7f), 23), rate);
            city = Source("Amb_City", Noise(rate, 6f, 180f, 25f, t => 0.85f + 0.15f * Mathf.Sin(t * 0.5f), 37), rate);
            windSrc = Source("Amb_Wind", Noise(rate, 6f, 4000f, 900f, t => 0.6f + 0.4f * Mathf.Sin(t * 0.33f), 41), rate);
            var go = new GameObject("Amb_OneShots");
            go.transform.SetParent(transform, false);
            oneShot = go.AddComponent<AudioSource>();
            oneShot.spatialBlend = 0f;
            oneShot.playOnAwake = false;
            horn = Clip("Amb_ShipHorn", Horn(rate), rate);
            gull1 = Clip("Amb_Gull1", Gull(rate, 3, 5), rate);
            gull2 = Clip("Amb_Gull2", Gull(rate, 2, 9), rate);
            nextHorn = Time.time + AmbienceEvents.NextInterval(AmbienceEvents.Kind.ShipHorn, rng.NextDouble());
            nextGull = Time.time + AmbienceEvents.NextInterval(AmbienceEvents.Kind.Gull, rng.NextDouble());
        }

        void Update()
        {
            var cam = Camera.main;
            if (cam == null || env == null || water == null) return;
            var p = cam.transform.position;
            float waterDist = env.land != null && env.land.IsLand(p.x, p.z) ? env.land.CoastDistance(p.x, p.z) : 0f;
            float stand = float.MaxValue;
            foreach (var c in env.grandstandCentres) stand = Mathf.Min(stand, (c - p).magnitude);
            float enc = enclosure != null ? enclosure(p) : 0f;
            var mix = AmbienceMixer.Evaluate(waterDist, stand, enc, Mathf.Max(0f, p.y));
            float k = 1f - Mathf.Exp(-Time.deltaTime * 2f);
            float vol = env.ambienceVolume;
            float crowdBoost = 0.6f + 0.4f * env.Excitement(p);
            water.volume = Mathf.Lerp(water.volume, mix.water * 0.5f * vol, k);
            crowd.volume = Mathf.Lerp(crowd.volume, mix.crowd * 0.6f * crowdBoost * vol, k);
            city.volume = Mathf.Lerp(city.volume, mix.city * 0.3f * vol, k);
            windSrc.volume = Mathf.Lerp(windSrc.volume, mix.wind * Mathf.Clamp01(env.WindSpeed / 10f) * 0.4f * vol, k);
            // Occasional horns and gulls where the water can be heard.
            if (oneShot != null && Time.time >= nextHorn)
            {
                float v = AmbienceEvents.Volume(AmbienceEvents.Kind.ShipHorn, mix.water) * vol;
                if (v > 0.001f) oneShot.PlayOneShot(horn, v);
                nextHorn = Time.time + AmbienceEvents.NextInterval(AmbienceEvents.Kind.ShipHorn, rng.NextDouble());
            }
            if (oneShot != null && Time.time >= nextGull)
            {
                float v = AmbienceEvents.Volume(AmbienceEvents.Kind.Gull, mix.water) * vol;
                if (v > 0.001f) oneShot.PlayOneShot(rng.NextDouble() < 0.5 ? gull1 : gull2, v);
                nextGull = Time.time + AmbienceEvents.NextInterval(AmbienceEvents.Kind.Gull, rng.NextDouble());
            }
        }
    }
}

namespace Apex.Game
{
    /// <summary>
    /// Balcony and window flags of one city chunk swinging in the shared wind (FlagWave). The vertices are moved on
    /// the CPU, only while the chunk is visible and within <see cref="Range"/> of the camera, at up to 20 Hz.
    /// </summary>
    public sealed class FlagWindComponent : UnityEngine.MonoBehaviour
    {
        public EnvironmentInstance env;
        public const float Range = 220f;
        UnityEngine.Mesh mesh;
        UnityEngine.Vector3[] basePos, normals, work;
        float[] u, phase;
        UnityEngine.Renderer rend;
        float next;

        void Start()
        {
            var mf = GetComponent<UnityEngine.MeshFilter>();
            rend = GetComponent<UnityEngine.Renderer>();
            if (mf == null || mf.sharedMesh == null) { enabled = false; return; }
            mesh = UnityEngine.Object.Instantiate(mf.sharedMesh);
            mesh.MarkDynamic();
            mf.sharedMesh = mesh;
            basePos = mesh.vertices;
            normals = mesh.normals;
            var uv = mesh.uv;
            work = new UnityEngine.Vector3[basePos.Length];
            u = new float[basePos.Length];
            phase = new float[basePos.Length];
            for (int i = 0; i < basePos.Length; i++)
            {
                u[i] = uv.Length > i ? uv[i].x : 0f;
                var w = transform.TransformPoint(basePos[i]);
                phase[i] = Apex.Environment.FlagWave.Phase(w.x, w.z);
            }
        }

        void Update()
        {
            if (mesh == null || rend == null || !rend.isVisible || UnityEngine.Time.time < next) return;
            var cam = UnityEngine.Camera.main;
            if (cam == null || (rend.bounds.ClosestPoint(cam.transform.position) - cam.transform.position).sqrMagnitude > Range * Range) return;
            next = UnityEngine.Time.time + 0.05f;
            float wind = env != null ? env.WindSpeed : 5f;
            double t = UnityEngine.Time.timeSinceLevelLoad;
            for (int i = 0; i < basePos.Length; i++)
            {
                var n = normals[i];
                var side = new UnityEngine.Vector3(n.z, 0f, -n.x);
                work[i] = basePos[i] + n * Apex.Environment.FlagWave.Out(u[i], t, wind, phase[i]) + side * Apex.Environment.FlagWave.Side(u[i], t, wind, phase[i]);
            }
            mesh.vertices = work;
            mesh.RecalculateBounds();
        }
    }
}

namespace Apex.Game
{
    /// <summary>
    /// Drives the golden-hour window glint (EnvironmentBuilder.BuildSunGlint): per group of walls of one direction
    /// and tile, the glint strength from GoldenHour.Glint for the current sun (WeatherVisuals.CurrentLight) and the
    /// direction to the camera. Groups without glint are disabled, so the layer costs nothing by day and at night.
    /// </summary>
    public sealed class SunGlintComponent : MonoBehaviour
    {
        public float trueNorth = float.NaN;
        readonly System.Collections.Generic.List<(MeshRenderer r, Vector3 normal, Vector3 centre)> groups = new System.Collections.Generic.List<(MeshRenderer, Vector3, Vector3)>();
        MaterialPropertyBlock block;
        static readonly int GlintId = Shader.PropertyToID("_GlintColor");
        float next;

        public void Add(MeshRenderer r, Vector3 normal, Vector3 centre) => groups.Add((r, normal, centre));

        void Update()
        {
            // 10 Hz is enough: the glint follows the camera's position, not its rotation.
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.1f;
            var cam = Camera.main;
            if (cam == null || !WeatherVisuals.HasLight) { SetAll(false); return; }
            var l = WeatherVisuals.CurrentLight;
            float low = Apex.Environment.GoldenHour.LowSun(l.elevation);
            if (low <= 0f) { SetAll(false); return; }
            if (block == null) block = new MaterialPropertyBlock();
            var s = Apex.Environment.GoldenHour.SunDirection(l.azimuthTrue, l.elevation, trueNorth);
            var p = cam.transform.position;
            foreach (var (r, n, c) in groups)
            {
                if (r == null) continue;
                var to = p - c;
                float dist = to.magnitude;
                if (dist < 1f) { r.enabled = false; continue; }
                to /= dist;
                float g = Apex.Environment.GoldenHour.Glint(new System.Numerics.Vector3(n.x, n.y, n.z), new System.Numerics.Vector3(to.x, to.y, to.z), s, l.elevation, WeatherVisuals.CurrentCloud);
                if (g < 0.004f) { r.enabled = false; continue; }
                var col = Apex.Environment.GoldenHour.GlintColour(l, g);
                // The shader takes linear colour (Vector property, no gamma conversion).
                block.SetVector(GlintId, new Vector4(Mathf.GammaToLinearSpace(col.r), Mathf.GammaToLinearSpace(col.g), Mathf.GammaToLinearSpace(col.b), 0f));
                r.SetPropertyBlock(block);
                r.enabled = true;
            }
        }

        void SetAll(bool on)
        {
            foreach (var (r, _, _) in groups) if (r != null && r.enabled != on) r.enabled = on;
        }
    }
}
