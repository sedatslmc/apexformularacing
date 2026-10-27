using System.Collections.Generic;
using Apex.Environment;
using UnityEngine;
using UnityEngine.Rendering;

namespace Apex.Game
{
    /// <summary>
    /// Draws <see cref="CityLife"/> with GPU instancing: street furniture and parked cars in static spatial
    /// batches, pedestrians (two stride poses) and traffic moving every frame. Only what lies within
    /// <see cref="drawDistance"/> of the camera is drawn; movers advance everywhere (cheap, no physics).
    /// </summary>
    public sealed class CityLifeComponent : MonoBehaviour
    {
        public float drawDistance = 300f;
        public float shadowDistance = 120f;

        /// <summary>Car colours common in Istanbul traffic: white, silver, grey, black, dark blue, dark red.</summary>
        static readonly Color[] CarColours =
        {
            new Color(0.86f, 0.86f, 0.85f), new Color(0.6f, 0.61f, 0.62f), new Color(0.33f, 0.34f, 0.35f),
            new Color(0.05f, 0.05f, 0.06f), new Color(0.1f, 0.15f, 0.3f), new Color(0.4f, 0.07f, 0.08f),
        };
        static readonly Color TaxiYellow = new Color(0.95f, 0.75f, 0.05f);
        /// <summary>Generic city bus livery (no operator branding).</summary>
        static readonly Color BusColour = new Color(0.82f, 0.84f, 0.86f);
        static readonly Color[] ShirtColours =
        {
            new Color(0.85f, 0.85f, 0.84f), new Color(0.12f, 0.16f, 0.28f), new Color(0.5f, 0.12f, 0.12f),
            new Color(0.2f, 0.2f, 0.21f), new Color(0.55f, 0.48f, 0.36f), new Color(0.25f, 0.4f, 0.48f),
        };
        static readonly Color[] TrouserColours =
        {
            new Color(0.12f, 0.14f, 0.2f), new Color(0.08f, 0.08f, 0.09f), new Color(0.42f, 0.38f, 0.3f),
        };

        sealed class Kind
        {
            public Mesh mesh;
            public Material[] mats;
            public Matrix4x4[] buffer = new Matrix4x4[1023];
            public int count;
        }

        sealed class StaticBatch
        {
            public int kind;
            public Vector3 centre;
            public readonly List<Matrix4x4> list = new List<Matrix4x4>();
            public Matrix4x4[] matrices;
        }

        CityLife life;
        readonly List<Kind> kinds = new List<Kind>();
        readonly Dictionary<string, int> kindIndex = new Dictionary<string, int>();
        readonly List<StaticBatch> statics = new List<StaticBatch>();
        int[] pedKinds;
        int[] signalKinds;

        public void Setup(CityLife cityLife)
        {
            life = cityLife;
            pedKinds = new int[ShirtColours.Length * 2];
            for (int i = 0; i < ShirtColours.Length; i++)
                for (int stride = 0; stride < 2; stride++)
                {
                    int shirt = i, trousers = i % TrouserColours.Length, st = stride;
                    pedKinds[i * 2 + stride] = KindFor("Ped" + i + "_" + stride, B => EnvironmentGeometry.Pedestrian(B, "CityShirt" + shirt, "CityTrousers" + trousers, st));
                }
            var open = new Dictionary<(int, int, int), StaticBatch>();
            void AddStatic(int kind, Vector3 pos, float yaw)
            {
                var key = (kind, Mathf.FloorToInt(pos.x / 120f), Mathf.FloorToInt(pos.z / 120f));
                if (!open.TryGetValue(key, out var b) || b.list.Count >= 1023)
                {
                    b = new StaticBatch { kind = kind };
                    statics.Add(b);
                    open[key] = b;
                }
                b.list.Add(Matrix4x4.TRS(pos, Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f), Vector3.one));
            }
            foreach (var p in life.props)
            {
                var prop = (CityLife.Prop)p.kind;
                AddStatic(KindFor("Prop" + p.kind, B => EnvironmentGeometry.StreetProp(B, prop)), V(p.position), p.yaw);
            }
            foreach (var p in life.parked) AddStatic(VehicleKind(p.kind, p.variant, true), V(p.position), p.yaw);
            foreach (var b in statics)
            {
                b.matrices = b.list.ToArray();
                var c = Vector3.zero;
                foreach (var m in b.matrices) c += (Vector3)m.GetColumn(3);
                b.centre = c / Mathf.Max(1, b.matrices.Length);
            }
            foreach (var v in life.vehicles) VehicleKind(v.kind, v.variant);
            signalKinds = new int[3];
            for (int st = 0; st < 3; st++) { int state = st; signalKinds[st] = KindFor("Signal" + st, B => EnvironmentGeometry.TrafficLight(B, state)); }
        }

        static Vector3 V(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);

        static void EnsureColour(string key, Color c)
        {
            var m = EnvironmentMaterials.Solid(c);
            ApexMaterials.Register(key, m);
        }

        /// <summary>Signal lamps glow day and night (fixed emission); the dark lenses are near black glass.</summary>
        static void EnsureSignal(string key, Color lens, Color glow)
        {
            var m = new Material(ApexMaterials.LitShader()) { name = "City_" + key, enableInstancing = true };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", lens);
            if (m.HasProperty("_Color")) m.SetColor("_Color", lens);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.8f);
            if (glow.maxColorComponent > 0f && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", glow);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            ApexMaterials.Register(key, m);
        }

        static void EnsureMaterials()
        {
            EnsureSignal(EnvironmentGeometry.SignalOff, new Color(0.05f, 0.05f, 0.05f), Color.black);
            EnsureSignal(EnvironmentGeometry.SignalRed, new Color(0.6f, 0.05f, 0.04f), new Color(1f, 0.08f, 0.05f) * 2.5f);
            EnsureSignal(EnvironmentGeometry.SignalAmber, new Color(0.6f, 0.4f, 0.05f), new Color(1f, 0.6f, 0.08f) * 2.2f);
            EnsureSignal(EnvironmentGeometry.SignalGreen, new Color(0.05f, 0.5f, 0.3f), new Color(0.1f, 1f, 0.55f) * 2.2f);
            for (int i = 0; i < CarColours.Length; i++) EnsureColour("CityCar" + i, CarColours[i]);
            EnsureColour("CityTaxi", TaxiYellow);
            EnsureColour("CityBus", BusColour);
            for (int i = 0; i < ShirtColours.Length; i++) EnsureColour("CityShirt" + i, ShirtColours[i]);
            for (int i = 0; i < TrouserColours.Length; i++) EnsureColour("CityTrousers" + i, TrouserColours[i]);
        }

        /// <summary>Mesh kind of a vehicle; parked ones get unlit lamp lenses so they stay dark at night.</summary>
        int VehicleKind(int kind, int variant, bool parked = false)
        {
            var k = (CityLife.Vehicle)kind;
            string body = k == CityLife.Vehicle.Taxi ? "CityTaxi" : k == CityLife.Vehicle.Bus ? "CityBus" : "CityCar" + (variant % CarColours.Length);
            if (!parked) return KindFor("Veh" + kind + body, B => EnvironmentGeometry.CityVehicle(B, k, body));
            return KindFor("Parked" + kind + body, B => EnvironmentGeometry.CityVehicle(m => B(m == EnvironmentGeometry.Headlight ? EnvironmentGeometry.Glass : m == EnvironmentGeometry.Taillight ? "SeatAccent" : m), k, body));
        }

        int KindFor(string key, System.Action<EnvironmentGeometry.Parts> build)
        {
            if (kindIndex.TryGetValue(key, out int i)) return i;
            if (kinds.Count == 0) EnsureMaterials();
            var parts = new Dictionary<string, MeshBuilder>();
            build(m => parts.TryGetValue(m, out var mb) ? mb : parts[m] = new MeshBuilder());
            var mesh = EnvironmentBuilder.Combine(parts, "City" + key, out var mats);
            foreach (var m in mats) m.enableInstancing = true;
            kinds.Add(new Kind { mesh = mesh, mats = mats });
            kindIndex[key] = kinds.Count - 1;
            return kinds.Count - 1;
        }

        void Update()
        {
            if (life == null) return;
            life.Advance(Time.deltaTime);
            var cam = Camera.main;
            if (cam == null) return;
            var eye = cam.transform.position;
            float far2 = drawDistance * drawDistance, shadow2 = shadowDistance * shadowDistance;
            foreach (var b in statics)
            {
                float d2 = (b.centre - eye).sqrMagnitude;
                // Batches span 120 m cells; test their centre with that margin.
                float reach = drawDistance + 85f;
                if (d2 > reach * reach) continue;
                Draw(kinds[b.kind], b.matrices, b.matrices.Length, d2 < shadow2);
            }
            foreach (var p in life.pedestrians)
            {
                var pos = V(p.position);
                if ((pos - eye).sqrMagnitude > far2 * 0.36f) continue;   // people only within 60 % of the range
                int stride = Mathf.FloorToInt(p.s / 0.7f) & 1;
                Push(pedKinds[(p.variant % ShirtColours.Length) * 2 + stride], Matrix4x4.TRS(pos, Quaternion.Euler(0f, p.yaw * Mathf.Rad2Deg, 0f), Vector3.one * (0.94f + 0.02f * p.kind)));
            }
            foreach (var v in life.vehicles)
            {
                var pos = V(v.position);
                if ((pos - eye).sqrMagnitude > far2) continue;
                Push(VehicleKind(v.kind, v.variant), Matrix4x4.TRS(pos, Quaternion.Euler(0f, v.yaw * Mathf.Rad2Deg, 0f), Vector3.one));
            }
            foreach (var sg in life.signals)
            {
                var pos = V(sg.position);
                if ((pos - eye).sqrMagnitude > far2) continue;
                Push(signalKinds[life.State(sg)], Matrix4x4.TRS(pos, Quaternion.Euler(0f, sg.yaw * Mathf.Rad2Deg, 0f), Vector3.one));
            }
            foreach (var k in kinds) Flush(k);
        }

        void Push(int kind, Matrix4x4 m)
        {
            var k = kinds[kind];
            k.buffer[k.count++] = m;
            if (k.count == k.buffer.Length) Flush(k);
        }

        void Flush(Kind k)
        {
            if (k.count == 0) return;
            Draw(k, k.buffer, k.count, shadowDistance > 0f);
            k.count = 0;
        }

        static void Draw(Kind k, Matrix4x4[] m, int n, bool shadows)
        {
            for (int s = 0; s < k.mats.Length; s++)
                Graphics.DrawMeshInstanced(k.mesh, s, k.mats[s], m, n, null, shadows ? ShadowCastingMode.On : ShadowCastingMode.Off, true);
        }
    }
}
