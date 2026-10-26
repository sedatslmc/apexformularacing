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

        public float WindSpeed { get; private set; }
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

        /// <summary>0 = daylight .. 1 = all street lamps, windows, floodlights and bridge lights on.</summary>
        public void SetArtificialLight(float a)
        {
            if (Mathf.Abs(a - artificial) < 0.005f) return;
            artificial = a;
            foreach (var (mat, full) in lights)
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", full * a);
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

    /// <summary>Turkish flag on a pole; cloth animated from the shared wind (see <see cref="FlagCloth"/>).</summary>
    public sealed class FlagComponent : MonoBehaviour
    {
        EnvironmentInstance env;
        Transform cloth;
        Mesh mesh;
        Vector3[] verts;
        int nu, nv, detail;
        float length, height, phase, yaw;
        int frame;

        public void Setup(EnvironmentInstance environment, Vector3 basePos, float flagLength, float pole, int detailLevel, Material flagMat)
        {
            env = environment;
            detail = detailLevel;
            length = flagLength;
            height = flagLength / TurkishFlagDesign.AspectRatio;
            phase = basePos.x * 0.013f + basePos.z * 0.007f;
            transform.position = basePos;
            var poleMb = new MeshBuilder();
            float r = Mathf.Clamp(pole * 0.012f, 0.04f, 0.25f);
            poleMb.Tube(Vector3.zero, Vector3.up * (pole + 0.25f), r, detail == 2 ? 4 : 8, true);
            poleMb.Box(Vector3.up * (pole + 0.3f), Vector3.one * r * 2.6f);
            var poleGo = EnvironmentBuilder.AddMeshObject(transform, "Pole", new Dictionary<string, MeshBuilder> { [EnvironmentGeometry.FlagPole] = poleMb }, detail < 2, false);
            poleGo.transform.localPosition = Vector3.zero;
            cloth = new GameObject("Cloth").transform;
            cloth.SetParent(transform, false);
            cloth.localPosition = Vector3.up * (pole - 0.05f) + Vector3.right * 0f;
            nu = detail == 0 ? 16 : detail == 1 ? 11 : 6;
            nv = detail == 0 ? 10 : detail == 1 ? 7 : 4;
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
            mr.shadowCastingMode = detail == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off;
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
                    verts[j * nu + i] = new Vector3(p.X, p.Y, p.Z);
                }
        }

        void LateUpdate()
        {
            if (env == null || mesh == null) return;
            var cam = Camera.main;
            float dist = cam != null ? (cam.transform.position - transform.position).magnitude : 0f;
            // Distant flags update less often; very distant ones keep their last pose.
            int interval = dist < 250f ? 1 : dist < 900f ? 3 : 8;
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
            public readonly List<(Vector3 pos, Quaternion rot, float scale, float phase)> people = new List<(Vector3, Quaternion, float, float)>();
            public Matrix4x4[] matrices;
        }

        readonly List<Batch> batches = new List<Batch>();
        readonly Dictionary<(int, int, int), Batch> open = new Dictionary<(int, int, int), Batch>();
        Mesh mesh;
        Material[] shirts;
        Material skin;

        public void Add(Vector3 pos, float yawDeg, float rnd)
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
            b.people.Add((pos, Quaternion.Euler(0f, yawDeg + rnd * 20f, 0f), 1.62f + Mathf.Abs(rnd) * 0.25f, rnd * 40f));
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
            foreach (var b in batches)
            {
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
                for (int i = 0; i < b.people.Count; i++)
                {
                    var (pos, rot, scale, phase) = b.people[i];
                    float idle = Mathf.Sin(t * 1.3f + phase) * 0.02f;
                    float jump = Mathf.Max(0f, Mathf.Sin(t * 7.5f + phase)) * 0.22f * excite;
                    b.matrices[i] = Matrix4x4.TRS(pos + Vector3.up * (idle + jump), rot, Vector3.one * scale);
                }
                Graphics.DrawMeshInstanced(mesh, 0, shirts[b.colour], b.matrices, b.matrices.Length, null, ShadowCastingMode.Off, false);
                Graphics.DrawMeshInstanced(mesh, 1, skin, b.matrices, b.matrices.Length, null, ShadowCastingMode.Off, false);
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

    /// <summary>Scrolls the strait's normal map with the wind (cheap animated water, no planar reflections).</summary>
    public sealed class WaterSurfaceComponent : MonoBehaviour
    {
        public EnvironmentInstance env;
        Material mat;
        Vector2 offset;

        void Start() => mat = GetComponent<MeshRenderer>()?.sharedMaterial;

        void Update()
        {
            if (mat == null) return;
            var w = env != null ? env.WindDirection : Vector3.forward;
            float speed = env != null ? env.WindSpeed : 4f;
            // Wavelets drift downwind at roughly 0.1 x wind speed (uv unit = 24 m).
            offset += new Vector2(w.x, w.z) * (0.1f * speed / 24f) * Time.deltaTime;
            offset.x %= 1f; offset.y %= 1f;
            mat.mainTextureOffset = offset;
            if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(speed / 10f)));
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
        AudioSource water, crowd, city, windSrc;

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
        }
    }
}
