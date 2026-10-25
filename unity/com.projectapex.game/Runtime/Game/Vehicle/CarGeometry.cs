using System.Collections.Generic;
using Apex.Vehicle;
using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// Procedural geometry of the original APEX A01 formula car (no GameObjects), so it
    /// can be generated, checked and rendered outside the engine. Parts are emitted per
    /// material and per LOD (0 full, 1 medium, 2 far). Car space: relative to the
    /// centre of mass, ground at y = -cogHeight, +z forward, +x right.
    ///
    /// The body is one continuous surface lofted through hand-designed cross-section
    /// keyframes (nose, cockpit with opening, sidepods with undercut, coke-bottle
    /// rear) with overshoot-free cubic interpolation; wings are swept/curled span
    /// surfaces with airfoil sections. It is still code-generated art without baked
    /// textures or hand-tuned topology: see docs/VEHICLE_ASSET_SPEC.md for limits.
    /// </summary>
    public sealed class CarGeometry
    {
        public const int LodCount = 3;

        // Material keys (resolved by the engine layer; Paint/Accent are the livery).
        public const string Paint = "CarPaint";
        public const string Accent = "CarAccent";
        public const string Carbon = "Carbon";
        public const string CarbonSatin = "CarbonSatin";
        public const string Titanium = "Titanium";
        public const string Tire = "Tire";
        public const string Rim = "Rim";
        public const string Mirror = "Mirror";
        public const string Interior = "Interior";
        public const string Belt = "Belt";
        public const string Helmet = "Helmet";
        public const string Visor = "Visor";
        public const string Glove = "Glove";
        public const string Display = "Display";
        public const string Exhaust = "Exhaust";

        static readonly int[] TubeSides = { 10, 6, 4 };
        static readonly int[] LatheSegments = { 48, 20, 12 };
        /// <summary>Samples per body control segment (18 segments per ring).</summary>
        static readonly int[] RingSub = { 4, 2, 1 };
        static readonly float[] StationStep = { 0.045f, 0.11f, 0.24f };
        static readonly int[] WingStations = { 26, 10, 6 };
        static readonly int[] ChordSegs = { 12, 6, 4 };

        readonly VehicleSetup setup;
        readonly float g;
        /// <summary>Body meshes: [lod][material].</summary>
        public readonly Dictionary<string, MeshBuilder>[] body = new Dictionary<string, MeshBuilder>[LodCount];
        /// <summary>Front and rear wing as separate meshes so damage can droop or remove them.</summary>
        public readonly Dictionary<string, MeshBuilder>[] frontWing = new Dictionary<string, MeshBuilder>[LodCount];
        public readonly Dictionary<string, MeshBuilder>[] rearWing = new Dictionary<string, MeshBuilder>[LodCount];
        Dictionary<string, MeshBuilder>[] target;

        public CarGeometry(VehicleSetup setup)
        {
            this.setup = setup;
            g = -setup.cogHeight;
            for (int i = 0; i < LodCount; i++)
            {
                body[i] = new Dictionary<string, MeshBuilder>();
                frontWing[i] = new Dictionary<string, MeshBuilder>();
                rearWing[i] = new Dictionary<string, MeshBuilder>();
            }
            target = body;
        }

        public float FrontZ => setup.FrontAxleZ;
        public float RearZ => setup.RearAxleZ;
        /// <summary>Height above the ground in car space.</summary>
        public float H(float h) => g + h;

        MeshBuilder B(int lod, string mat)
        {
            if (!target[lod].TryGetValue(mat, out var mb)) target[lod][mat] = mb = new MeshBuilder();
            return mb;
        }

        void Part(string mat, int maxLod, System.Action<MeshBuilder, int> emit)
        {
            for (int lod = 0; lod <= maxLod; lod++) emit(B(lod, mat), lod);
        }

        public CarGeometry BuildBody()
        {
            Body();
            Floor();
            EngineCover();
            Halo();
            Mirrors();
            CockpitInterior();
            Details();
            target = frontWing;
            FrontWing();
            target = rearWing;
            RearWing();
            target = body;
            foreach (var group in new[] { body, frontWing, rearWing })
                foreach (var kv in group[0])
                    if (kv.Key == Carbon || kv.Key == CarbonSatin) kv.Value.MetricUVs();
            return this;
        }

        // --- Body: continuous lofted surface ------------------------------------------------

        /// <summary>Half cross-section: 10 control points (x >= 0, height above ground) from bottom centre to top centre.</summary>
        struct HalfProfile
        {
            public float z;
            public Vector2[] p;
        }

        static HalfProfile Chassis(float z, float bottom, float top, float w)
        {
            float h = top - bottom;
            return new HalfProfile
            {
                z = z,
                p = new[]
                {
                    new Vector2(0f, bottom), new Vector2(0.55f * w, bottom + 0.02f * h), new Vector2(0.9f * w, bottom + 0.12f * h),
                    new Vector2(w, bottom + 0.3f * h), new Vector2(1.02f * w, bottom + 0.5f * h), new Vector2(w, bottom + 0.68f * h),
                    new Vector2(0.9f * w, bottom + 0.84f * h), new Vector2(0.65f * w, bottom + 0.95f * h), new Vector2(0.35f * w, bottom + 0.995f * h),
                    new Vector2(0f, top),
                },
            };
        }

        /// <summary>
        /// Section through the sidepods: chassis bottom half width wc, pod outer x xo,
        /// pod underside hb (undercut), shoulder height ht, inner top (rimX, rimH) and
        /// centre top (seat height inside the cockpit opening, else the cover top).
        /// </summary>
        static HalfProfile Pod(float z, float wc, float xo, float hb, float ht, float rimX, float rimH, float centre)
        {
            return new HalfProfile
            {
                z = z,
                p = new[]
                {
                    new Vector2(0f, 0.09f), new Vector2(wc, 0.095f), new Vector2(wc + 0.07f, hb - 0.05f),
                    new Vector2((wc + xo) * 0.5f + 0.04f, hb), new Vector2(xo, hb + 0.08f), new Vector2(xo + 0.02f, (hb + ht) * 0.5f + 0.04f),
                    new Vector2(xo - 0.07f, ht), new Vector2(rimX + 0.05f, rimH - 0.012f), new Vector2(rimX, rimH),
                    new Vector2(0f, centre),
                },
            };
        }

        List<HalfProfile> BodyKeys()
        {
            float f = FrontZ;
            return new List<HalfProfile>
            {
                Chassis(f + 1.15f, 0.17f, 0.245f, 0.045f),
                Chassis(f + 0.85f, 0.185f, 0.295f, 0.095f),
                Chassis(f + 0.45f, 0.2f, 0.36f, 0.14f),
                Chassis(f, 0.21f, 0.43f, 0.175f),
                Chassis(f - 0.5f, 0.205f, 0.51f, 0.21f),
                Chassis(f - 0.95f, 0.2f, 0.585f, 0.245f),
                // Cockpit opening begins; pods still on the chassis wall.
                Pod(f - 1.36f, 0.2f, 0.27f, 0.3f, 0.5f, 0.225f, 0.62f, 0.56f),
                // Sidepod inlet face: pods grow out quickly.
                Pod(f - 1.46f, 0.21f, 0.62f, 0.27f, 0.49f, 0.23f, 0.628f, 0.4f),
                Pod(f - 1.58f, 0.22f, 0.78f, 0.25f, 0.52f, 0.24f, 0.635f, 0.33f),
                Pod(f - 1.98f, 0.22f, 0.8f, 0.235f, 0.53f, 0.25f, 0.635f, 0.29f),
                Pod(f - 2.26f, 0.22f, 0.78f, 0.23f, 0.54f, 0.25f, 0.66f, 0.31f),
                // Behind the cockpit: closed engine cover.
                Pod(f - 2.4f, 0.22f, 0.75f, 0.225f, 0.53f, 0.14f, 0.7f, 0.71f),
                Pod(f - 2.88f, 0.21f, 0.66f, 0.2f, 0.49f, 0.13f, 0.645f, 0.655f),
                Pod(f - 3.28f, 0.2f, 0.52f, 0.18f, 0.45f, 0.12f, 0.555f, 0.565f),
                Pod(f - 3.6f, 0.19f, 0.38f, 0.17f, 0.42f, 0.11f, 0.485f, 0.495f),
                Pod(f - 3.93f, 0.17f, 0.24f, 0.18f, 0.38f, 0.09f, 0.42f, 0.425f),
                Chassis(f - 4.18f, 0.24f, 0.38f, 0.1f),
                Chassis(f - 4.36f, 0.28f, 0.355f, 0.05f),
            };
        }

        /// <summary>Monotone cubic (PCHIP) interpolation of a sampled function: smooth, no overshoot.</summary>
        static float Pchip(float[] xs, float[] ys, float x)
        {
            int n = xs.Length;
            // xs descending (front to back) is handled by flipping the sign.
            bool desc = xs[0] > xs[n - 1];
            float X(int i) => desc ? -xs[i] : xs[i];
            float xv = desc ? -x : x;
            if (xv <= X(0)) return ys[0];
            if (xv >= X(n - 1)) return ys[n - 1];
            int k = 0;
            while (k < n - 2 && xv > X(k + 1)) k++;
            float Slope(int i) => (ys[i + 1] - ys[i]) / (X(i + 1) - X(i));
            float D(int i)
            {
                if (i == 0) return Slope(0);
                if (i == n - 1) return Slope(n - 2);
                float a = Slope(i - 1), b = Slope(i);
                if (a * b <= 0f) return 0f;
                return 2f / (1f / a + 1f / b);
            }
            float h = X(k + 1) - X(k), t = (xv - X(k)) / h;
            float t2 = t * t, t3 = t2 * t;
            return (2 * t3 - 3 * t2 + 1) * ys[k] + (t3 - 2 * t2 + t) * h * D(k) + (-2 * t3 + 3 * t2) * ys[k + 1] + (t3 - t2) * h * D(k + 1);
        }

        /// <summary>Closed body ring at z: mirrored half profile, Catmull-Rom with a fixed sample count per segment.</summary>
        Vector3[] BodyRing(List<HalfProfile> keys, float[] zs, float z, int sub)
        {
            var half = new Vector2[10];
            for (int c = 0; c < 10; c++)
            {
                var xs = new float[keys.Count];
                var ys = new float[keys.Count];
                for (int k = 0; k < keys.Count; k++) { xs[k] = keys[k].p[c].x; ys[k] = keys[k].p[c].y; }
                half[c] = new Vector2(Pchip(zs, xs, z), Pchip(zs, ys, z));
            }
            // Full polygon: right side bottom->top, then left side top->bottom (18 points).
            var poly = new List<Vector2>();
            for (int c = 0; c < 10; c++) poly.Add(half[c]);
            for (int c = 8; c >= 1; c--) poly.Add(new Vector2(-half[c].x, half[c].y));
            int n = poly.Count;
            var ring = new Vector3[n * sub];
            for (int i = 0; i < n; i++)
            {
                Vector2 p0 = poly[(i - 1 + n) % n], p1 = poly[i], p2 = poly[(i + 1) % n], p3 = poly[(i + 2) % n];
                for (int s = 0; s < sub; s++)
                {
                    float t = (float)s / sub;
                    float t2 = t * t, t3 = t2 * t;
                    Vector2 q = 0.5f * new Vector2(
                        2 * p1.x + (-p0.x + p2.x) * t + (2 * p0.x - 5 * p1.x + 4 * p2.x - p3.x) * t2 + (-p0.x + 3 * p1.x - 3 * p2.x + p3.x) * t3,
                        2 * p1.y + (-p0.y + p2.y) * t + (2 * p0.y - 5 * p1.y + 4 * p2.y - p3.y) * t2 + (-p0.y + 3 * p1.y - 3 * p2.y + p3.y) * t3);
                    ring[i * sub + s] = new Vector3(q.x, H(q.y), z);
                }
            }
            return ring;
        }

        void Body()
        {
            var keys = BodyKeys();
            var zs = new float[keys.Count];
            for (int k = 0; k < keys.Count; k++) zs[k] = keys[k].z;
            float zFront = zs[0], zBack = zs[keys.Count - 1];
            float cockpitFront = FrontZ - 1.36f, cockpitBack = FrontZ - 2.3f;
            for (int lod = 0; lod < LodCount; lod++)
            {
                int sub = RingSub[lod];
                // Denser stations where the shape changes fast (nose, inlet, cockpit ends).
                var stations = new List<float>();
                float zc = zFront;
                while (zc > zBack)
                {
                    stations.Add(zc);
                    float local = StationStep[lod];
                    bool busy = zc > FrontZ + 0.7f || (zc < cockpitFront + 0.15f && zc > cockpitFront - 0.3f) || (zc < cockpitBack + 0.12f && zc > cockpitBack - 0.15f);
                    zc -= busy ? local * 0.5f : local;
                }
                stations.Add(zBack);
                var rings = new List<Vector3[]>();
                foreach (float z in stations) rings.Add(BodyRing(keys, zs, z, sub));
                int n = rings[0].Length;
                for (int i = 0; i < rings.Count - 1; i++)
                {
                    float zMid = (stations[i] + stations[i + 1]) * 0.5f;
                    for (int j = 0; j < n; j++)
                    {
                        int seg = j / sub;
                        string mat = BodyMaterial(seg, j % sub, sub, zMid, cockpitFront, cockpitBack);
                        var mb = B(lod, mat);
                        int j1 = (j + 1) % n;
                        long Key(int ii, int jj) => ((long)ii << 20) | (uint)jj;
                        int a = mb.SharedVertex(Key(i, j), rings[i][j], new Vector2((float)j / n, stations[i]));
                        int b = mb.SharedVertex(Key(i, j1), rings[i][j1], new Vector2((float)j1 / n, stations[i]));
                        int c = mb.SharedVertex(Key(i + 1, j1), rings[i + 1][j1], new Vector2((float)j1 / n, stations[i + 1]));
                        int d = mb.SharedVertex(Key(i + 1, j), rings[i + 1][j], new Vector2((float)j / n, stations[i + 1]));
                        // Rings run counter-clockwise seen from the front; stations go backwards
                        // (-z), so (a, d, c) faces outward in Unity's convention.
                        mb.Triangle(a, c, b);
                        mb.Triangle(a, d, c);
                    }
                }
                // Nose tip and tail caps.
                CapRing(B(lod, Paint), rings[0], true);
                CapRing(B(lod, Carbon), rings[rings.Count - 1], false);
            }
            // Radiator inlets in the open front of the sidepods.
            foreach (float s in new[] { -1f, 1f })
                Part(Interior, 2, (mb, lod) => mb.Loft(new List<LoftSection>
                {
                    new LoftSection(FrontZ - 1.44f, H(0.39f), 0.13f, 0.075f, 4f, s * 0.47f),
                    new LoftSection(FrontZ - 1.47f, H(0.39f), 0.13f, 0.075f, 4f, s * 0.47f),
                }, lod == 0 ? 24 : 10));
        }

        /// <summary>
        /// Material per body face: control segments 0-2 = carbon underside, 3-5 =
        /// sidepod flanks, 6 = shoulder (cyan accent line on the pods), 7-10 = top /
        /// cockpit opening (interior inside the opening, accent stripe on the nose).
        /// </summary>
        static string BodyMaterial(int seg, int subIndex, int sub, float z, float cockpitFront, float cockpitBack)
        {
            // Mirror: segments 10..17 are the left side of 7..0.
            int s = seg <= 8 ? seg : 17 - seg;
            if (s <= 1) return CarbonSatin;
            bool inCockpit = z < cockpitFront - 0.06f && z > cockpitBack + 0.02f;
            if (inCockpit && s >= 8) return Interior;
            if (s == 6 && z < cockpitFront - 0.1f && z > cockpitBack - 1.2f && (sub == 1 || subIndex == sub / 2)) return Accent;
            if (s == 8 && z > cockpitFront + 0.25f && (sub == 1 || subIndex >= sub - 1)) return Accent;
            return Paint;
        }

        static void CapRing(MeshBuilder mb, Vector3[] ring, bool front)
        {
            Vector3 c = Vector3.zero;
            foreach (var p in ring) c += p;
            c /= ring.Length;
            int ci = mb.SharedVertex(-1 - (front ? 0 : 1), c, Vector2.zero);
            var idx = new int[ring.Length];
            for (int j = 0; j < ring.Length; j++) idx[j] = mb.SharedVertex((front ? 3L : 4L) << 40 | (uint)j, ring[j], Vector2.zero);
            for (int j = 0; j < ring.Length; j++)
            {
                int a = idx[j], b = idx[(j + 1) % ring.Length];
                // The ring winds counter-clockwise about +z: the fan faces forward as is.
                if (front) mb.Triangle(ci, a, b); else mb.Triangle(ci, b, a);
            }
        }

        // --- Floor, diffuser --------------------------------------------------------------

        void Floor()
        {
            float f = FrontZ, r = RearZ;
            var outline = new List<Vector2>
            {
                new Vector2(0.42f, f - 0.8f), new Vector2(0.62f, f - 1.05f), new Vector2(0.86f, f - 1.5f), new Vector2(0.9f, f - 2.4f),
                new Vector2(0.88f, r + 0.5f), new Vector2(0.7f, r + 0.05f), new Vector2(0.6f, r - 0.1f),
                new Vector2(-0.6f, r - 0.1f), new Vector2(-0.7f, r + 0.05f), new Vector2(-0.88f, r + 0.5f), new Vector2(-0.9f, f - 2.4f),
                new Vector2(-0.86f, f - 1.5f), new Vector2(-0.62f, f - 1.05f), new Vector2(-0.42f, f - 0.8f),
            };
            Part(CarbonSatin, 2, (mb, lod) => mb.PlateXZ(outline, H(0.055f), 0.022f));
            // Floor edge wing: curled lip along the outer edge.
            Part(Carbon, 1, (mb, lod) =>
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    var path = new List<Vector2>();
                    var bot = new List<float>();
                    var top = new List<float>();
                    for (int i = 0; i <= 8; i++)
                    {
                        float t = i / 8f;
                        float z = Mathf.Lerp(f - 1.45f, r + 0.55f, t);
                        path.Add(new Vector2(s * (0.89f + 0.015f * Mathf.Sin(t * Mathf.PI)), z));
                        bot.Add(H(0.045f));
                        top.Add(H(0.09f + 0.035f * Mathf.Sin(t * Mathf.PI)));
                    }
                    mb.Fin(path, bot, top, 0.012f);
                }
            });
            // Venturi inlet fences under the sidepod fronts (curved, as on current cars).
            Part(CarbonSatin, 0, (mb, lod) =>
            {
                foreach (float s in new[] { -1f, 1f })
                    for (int k = 0; k < 2; k++)
                    {
                        float x0 = 0.32f + k * 0.11f;
                        var path = new List<Vector2> { new Vector2(s * x0, f - 0.8f - k * 0.08f), new Vector2(s * (x0 + 0.05f), f - 1.05f - k * 0.08f), new Vector2(s * (x0 + 0.12f), f - 1.4f) };
                        mb.Fin(path, new[] { H(0.045f), H(0.045f), H(0.05f) }, new[] { H(0.1f), H(0.13f), H(0.15f) }, 0.008f);
                    }
            });
            // Plank.
            Part(Interior, 1, (mb, lod) => mb.Box(new Vector3(0f, H(0.034f), (f - 0.85f + r) * 0.5f), new Vector3(0.3f, 0.012f, f - 0.85f - r)));
            // Diffuser ramp and strakes.
            float dz0 = r + 0.35f, dz1 = r - 0.42f;
            Part(Carbon, 2, (mb, lod) =>
            {
                var rings = new List<Vector3[]>();
                int steps = lod == 0 ? 8 : 3;
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    float z = Mathf.Lerp(dz0, dz1, t);
                    float y = H(0.06f + 0.26f * t * t);
                    float w = Mathf.Lerp(0.62f, 0.56f, t);
                    rings.Add(new[] { new Vector3(-w, y - 0.012f, z), new Vector3(-w, y + 0.012f, z), new Vector3(w, y + 0.012f, z), new Vector3(w, y - 0.012f, z) });
                }
                mb.RingLoft(rings, true, true);
            });
            Part(Carbon, 1, (mb, lod) =>
            {
                foreach (float x in new[] { -0.56f, -0.3f, -0.1f, 0.1f, 0.3f, 0.56f })
                {
                    var path = new List<Vector2>();
                    var bot = new List<float>();
                    var top = new List<float>();
                    for (int i = 0; i <= 4; i++)
                    {
                        float t = i / 4f;
                        path.Add(new Vector2(x, Mathf.Lerp(dz0, dz1, t)));
                        bot.Add(H(0.04f + 0.1f * t * t));
                        top.Add(H(0.06f + 0.26f * t * t) - 0.01f);
                    }
                    mb.Fin(path, bot, top, 0.008f);
                }
            });
        }

        // --- Engine cover details, airbox, rear crash structure ------------------------------

        void EngineCover()
        {
            float c1 = FrontZ - 2.3f;
            // Roll hoop / airbox rising from the engine cover.
            var hoop = new List<Vector3[]>();
            int segs = 20;
            float[] zs = { c1 + 0.02f, c1 - 0.12f, c1 - 0.45f, c1 - 0.85f, c1 - 1.25f };
            float[] ys = { 0.9f, 0.88f, 0.8f, 0.7f, 0.62f };
            float[] ws = { 0.11f, 0.13f, 0.12f, 0.08f, 0.04f };
            float[] hs = { 0.1f, 0.12f, 0.12f, 0.1f, 0.06f };
            for (int lod = 0; lod < LodCount; lod++)
            {
                int n = lod == 0 ? segs : 10;
                var rings = new List<Vector3[]>();
                for (int i = 0; i < zs.Length; i++)
                {
                    var ring = new Vector3[n];
                    for (int j = 0; j < n; j++)
                    {
                        float t = Mathf.PI * 2f * j / n;
                        float c = Mathf.Cos(t), sn = Mathf.Sin(t);
                        // Teardrop: flat bottom blending into the cover, rounded top.
                        float yy = sn > 0f ? sn * hs[i] : sn * hs[i] * 1.8f;
                        ring[j] = new Vector3(c * ws[i], H(ys[i]) + yy, zs[i]);
                    }
                    rings.Add(ring);
                }
                B(lod, Paint).RingLoft(rings, true, true);
            }
            Part(Interior, 1, (mb, lod) => mb.Loft(new List<LoftSection>
            {
                new LoftSection(c1 + 0.035f, H(0.905f), 0.075f, 0.06f, 2.4f),
                new LoftSection(c1 + 0.015f, H(0.905f), 0.075f, 0.06f, 2.4f),
            }, 16));
            // Shark fin.
            Part(Paint, 1, (mb, lod) =>
            {
                var path = new List<Vector2>();
                var bot = new List<float>();
                var top = new List<float>();
                for (int i = 0; i <= 6; i++)
                {
                    float t = i / 6f;
                    float z = Mathf.Lerp(c1 - 0.7f, c1 - 1.95f, t);
                    path.Add(new Vector2(0f, z));
                    bot.Add(H(Mathf.Lerp(0.66f, 0.42f, t)));
                    top.Add(H(Mathf.Lerp(0.74f, 0.62f, t * t)));
                }
                mb.Fin(path, bot, top, 0.008f);
            });
            // Rear crash structure and exhaust.
            float r = RearZ;
            Part(Carbon, 2, (mb, lod) => mb.Loft(new List<LoftSection>
            {
                new LoftSection(r - 0.5f, H(0.3f), 0.08f, 0.07f, 3f),
                new LoftSection(r - 0.78f, H(0.29f), 0.05f, 0.05f, 3f),
            }, lod == 0 ? 16 : 8, true, true));
            Part(Exhaust, 1, (mb, lod) =>
            {
                mb.Lathe(new List<Vector2>
                {
                    new Vector2(-0.12f, 0.001f), new Vector2(-0.12f, 0.045f), new Vector2(0.1f, 0.05f), new Vector2(0.12f, 0.046f), new Vector2(0.12f, 0.035f), new Vector2(0.05f, 0.034f), new Vector2(0.05f, 0.001f),
                }, lod == 0 ? 20 : 10);
            });
        }

        // --- Halo, mirrors, cockpit, details --------------------------------------------------

        void Halo()
        {
            float c0 = FrontZ - 1.36f;
            float top = H(0.86f);
            Part(Carbon, 2, (mb, lod) =>
            {
                int sides = TubeSides[lod] + 2;
                // Central pillar: faired, wider fore-aft than across.
                mb.FairedTube(new[] { new Vector3(0f, H(0.6f), c0 + 0.1f), new Vector3(0f, H(0.74f), c0 + 0.03f), new Vector3(0f, top - 0.01f, c0 - 0.05f) }, 0.045f, 0.03f, sides, Vector3.forward);
                var hoop = new List<Vector3>();
                for (int i = -12; i <= 12; i++)
                {
                    float t = i / 12f;
                    float ang = t * Mathf.PI * 0.5f;
                    float x = 0.28f * Mathf.Sin(ang);
                    float zz = (c0 - 0.05f) - 0.52f * (1f - Mathf.Cos(ang)) - 0.1f * Mathf.Abs(t);
                    hoop.Add(new Vector3(x, top - 0.035f * t * t, zz));
                }
                // Rear legs down to the chassis.
                hoop.Insert(0, new Vector3(-0.29f, H(0.7f), c0 - 0.76f));
                hoop.Insert(0, new Vector3(-0.3f, H(0.62f), c0 - 0.78f));
                hoop.Add(new Vector3(0.29f, H(0.7f), c0 - 0.76f));
                hoop.Add(new Vector3(0.3f, H(0.62f), c0 - 0.78f));
                mb.FairedTube(hoop, 0.06f, 0.035f, sides, Vector3.up);
            });
        }

        void Mirrors()
        {
            float zc = FrontZ - 1.6f;
            foreach (float s in new[] { -1f, 1f })
            {
                Part(Carbon, 1, (mb, lod) =>
                {
                    mb.FairedTube(new[] { new Vector3(s * 0.44f, H(0.53f), zc - 0.02f), new Vector3(s * 0.5f, H(0.6f), zc + 0.02f), new Vector3(s * 0.53f, H(0.64f), zc + 0.04f) }, 0.03f, 0.012f, 6, Vector3.forward);
                    // Aero housing: rounded front, open (mirror) back.
                    mb.Loft(new List<LoftSection>
                    {
                        new LoftSection(zc + 0.12f, H(0.665f), 0.03f, 0.012f, 2f, s * 0.56f),
                        new LoftSection(zc + 0.09f, H(0.665f), 0.08f, 0.032f, 2.6f, s * 0.56f),
                        new LoftSection(zc + 0.04f, H(0.665f), 0.085f, 0.035f, 3f, s * 0.56f),
                    }, lod == 0 ? 16 : 8);
                });
                Part(Mirror, 1, (mb, lod) => mb.Box(new Vector3(s * 0.56f, H(0.665f), zc + 0.035f), new Vector3(0.155f, 0.06f, 0.004f)));
            }
        }

        void CockpitInterior()
        {
            float c1 = FrontZ - 2.3f;
            Part(Interior, 0, (mb, lod) =>
            {
                // Seat shell and headrest pads.
                mb.Box(new Vector3(0f, H(0.34f), c1 + 0.14f), new Vector3(0.38f, 0.16f, 0.22f), Quaternion.Euler(-25f, 0f, 0f));
                foreach (float s in new[] { -1f, 1f })
                    mb.Loft(new List<LoftSection>
                    {
                        new LoftSection(c1 + 0.22f, H(0.68f), 0.035f, 0.07f, 3f, s * 0.19f),
                        new LoftSection(c1 + 0.02f, H(0.68f), 0.04f, 0.075f, 3f, s * 0.19f),
                    }, 12);
                // Dash with a small display above the driver's legs.
                mb.Box(new Vector3(0f, H(0.58f), FrontZ - 1.42f), new Vector3(0.36f, 0.05f, 0.1f), Quaternion.Euler(20f, 0f, 0f));
            });
            // Six-point harness.
            Part(Belt, 0, (mb, lod) =>
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    mb.Box(new Vector3(s * 0.09f, H(0.6f), c1 + 0.22f), new Vector3(0.045f, 0.2f, 0.008f), Quaternion.Euler(-35f, 0f, 0f));
                    mb.Box(new Vector3(s * 0.12f, H(0.44f), c1 + 0.34f), new Vector3(0.045f, 0.008f, 0.16f), Quaternion.Euler(0f, s * 25f, 0f));
                }
            });
            // Driver: race suit torso and shoulders (helmet, arms, gloves are animated parts).
            Part(Accent, 1, (mb, lod) =>
            {
                mb.Loft(new List<LoftSection>
                {
                    new LoftSection(c1 + 0.36f, H(0.5f), 0.17f, 0.09f, 3f),
                    new LoftSection(c1 + 0.16f, H(0.53f), 0.21f, 0.12f, 3f),
                    new LoftSection(c1 + 0.02f, H(0.54f), 0.19f, 0.1f, 3f),
                }, lod == 0 ? 20 : 10);
            });
            // HANS device behind the helmet.
            Part(Interior, 0, (mb, lod) => mb.Box(new Vector3(0f, H(0.66f), c1 + 0.12f), new Vector3(0.26f, 0.05f, 0.14f), Quaternion.Euler(-15f, 0f, 0f)));
        }

        void Details()
        {
            float c1 = FrontZ - 2.3f;
            // T-camera on top of the roll hoop and onboard camera pods on the nose.
            Part(Accent, 1, (mb, lod) => mb.Loft(new List<LoftSection>
            {
                new LoftSection(c1 - 0.08f, H(1.0f), 0.08f, 0.02f, 3f),
                new LoftSection(c1 - 0.18f, H(1.0f), 0.08f, 0.02f, 3f),
            }, 12));
            Part(Carbon, 0, (mb, lod) =>
            {
                foreach (float s in new[] { -1f, 1f })
                    mb.Loft(new List<LoftSection>
                    {
                        new LoftSection(FrontZ + 0.32f, H(0.42f), 0.012f, 0.012f, 2f, s * 0.12f),
                        new LoftSection(FrontZ + 0.18f, H(0.425f), 0.02f, 0.02f, 2f, s * 0.12f),
                        new LoftSection(FrontZ + 0.08f, H(0.42f), 0.012f, 0.012f, 2f, s * 0.12f),
                    }, 10);
            });
            // Pitot tube.
            Part(Titanium, 0, (mb, lod) => mb.Tube(new Vector3(0f, H(0.22f), FrontZ + 1.12f), new Vector3(0f, H(0.22f), FrontZ + 1.3f), 0.005f, 6));
        }

        // --- Wings ---------------------------------------------------------------------------

        void FrontWing()
        {
            float le = FrontZ + 1.1f;
            var elements = new[]
            {
                (h0: 0.085f, rise: 0.02f, dz: 0f, chord: 0.3f, aoa0: 3f, aoa1: 7f, mat: Carbon, inner: 0f),
                (h0: 0.115f, rise: 0.06f, dz: -0.21f, chord: 0.19f, aoa0: 12f, aoa1: 20f, mat: Carbon, inner: 0.09f),
                (h0: 0.15f, rise: 0.09f, dz: -0.33f, chord: 0.15f, aoa0: 22f, aoa1: 31f, mat: Paint, inner: 0.09f),
                (h0: 0.185f, rise: 0.11f, dz: -0.42f, chord: 0.12f, aoa0: 32f, aoa1: 42f, mat: Accent, inner: 0.09f),
            };
            for (int e = 0; e < elements.Length; e++)
            {
                var el = elements[e];
                int maxLod = e < 2 ? 2 : 1;
                Part(el.mat, maxLod, (mb, lod) =>
                {
                    int n = WingStations[lod];
                    foreach (float side in new[] { -1f, 1f })
                    {
                        if (el.inner <= 0f && side < 0f) continue;
                        var st = new List<MeshBuilder.WingStation>();
                        for (int i = 0; i <= n; i++)
                        {
                            float t = i / (float)n;
                            float x = el.inner <= 0f ? Mathf.Lerp(-0.96f, 0.96f, t) : side * Mathf.Lerp(el.inner, 0.96f, t);
                            float span = Mathf.Abs(x) / 0.96f;
                            float rise = el.rise * Mathf.Pow(span, 1.6f);
                            // Leading edge sweeps back towards the tips; inner flaps dip under the nose.
                            float z = le + el.dz - 0.1f * span * span - el.chord * 0.25f;
                            st.Add(new MeshBuilder.WingStation
                            {
                                position = new Vector3(x, H(el.h0 + rise), z),
                                chord = el.chord * Mathf.Lerp(1f, 0.85f, span),
                                thickness = 0.09f,
                                camber = -0.06f,
                                aoaDeg = Mathf.Lerp(el.aoa0, el.aoa1, span),
                            });
                        }
                        mb.SpanWing(st, ChordSegs[lod]);
                    }
                });
            }
            // Curved end plates with a small canard on the outside.
            Part(Carbon, 2, (mb, lod) =>
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    var path = new List<Vector2>();
                    var bot = new List<float>();
                    var top = new List<float>();
                    int n = lod == 0 ? 10 : 4;
                    for (int i = 0; i <= n; i++)
                    {
                        float t = i / (float)n;
                        float z = Mathf.Lerp(le + 0.05f, le - 0.62f, t);
                        path.Add(new Vector2(s * (0.975f - 0.04f * (1f - t) * (1f - t)), z));
                        bot.Add(H(0.045f + 0.02f * t));
                        top.Add(H(0.16f + 0.17f * Mathf.Sin(t * Mathf.PI * 0.6f)));
                    }
                    mb.Fin(path, bot, top, 0.01f);
                }
            });
            // Nose pylons down to the main plane.
            Part(Carbon, 1, (mb, lod) =>
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    var path = new List<Vector2> { new Vector2(s * 0.04f, le - 0.02f), new Vector2(s * 0.05f, le - 0.15f), new Vector2(s * 0.06f, le - 0.3f) };
                    mb.Fin(path, new[] { H(0.1f), H(0.1f), H(0.11f) }, new[] { H(0.19f), H(0.205f), H(0.22f) }, 0.01f);
                }
            });
        }

        /// <summary>Rear wing main plane height above ground: the car's highest aero surface.</summary>
        const float MainPlaneH = 0.9f;

        void RearWing()
        {
            float zq = RearZ - 0.85f;
            const float halfSpan = 0.5f, tipR = 0.11f;
            // Main plane: slight central dip ("spoon"), tips curl down into the end plates.
            Part(Carbon, 2, (mb, lod) => mb.SpanWing(CurledSpan(zq + 0.05f, MainPlaneH, 0.36f, 9f, halfSpan, tipR, WingStations[lod], true), ChordSegs[lod] + 2));
            // DRS flap.
            Part(Accent, 2, (mb, lod) =>
            {
                var st = new List<MeshBuilder.WingStation>();
                int n = WingStations[lod];
                for (int i = 0; i <= n; i++)
                {
                    float t = i / (float)n;
                    float x = Mathf.Lerp(-(halfSpan - 0.035f), halfSpan - 0.035f, t);
                    st.Add(new MeshBuilder.WingStation { position = new Vector3(x, H(MainPlaneH + 0.065f + 0.02f * (x / halfSpan) * (x / halfSpan)), zq - 0.21f), chord = 0.21f, thickness = 0.08f, camber = -0.06f, aoaDeg = 32f });
                }
                mb.SpanWing(st, ChordSegs[lod]);
            });
            // Beam wing.
            Part(Carbon, 1, (mb, lod) =>
            {
                foreach (var (h, dz, chord, aoa) in new[] { (0.34f, 0.12f, 0.15f, 6f), (0.4f, 0f, 0.11f, 18f) })
                {
                    var st = new List<MeshBuilder.WingStation>();
                    int n = WingStations[lod] / 2;
                    for (int i = 0; i <= n; i++)
                    {
                        float x = Mathf.Lerp(-0.46f, 0.46f, i / (float)n);
                        st.Add(new MeshBuilder.WingStation { position = new Vector3(x, H(h - 0.02f * (1f - (x / 0.46f) * (x / 0.46f))), zq + 0.15f + dz), chord = chord, thickness = 0.1f, camber = -0.05f, aoaDeg = aoa });
                    }
                    mb.SpanWing(st, ChordSegs[lod]);
                }
            });
            // End plates below the curled tips.
            Part(Carbon, 2, (mb, lod) =>
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    var path = new List<Vector2>();
                    var bot = new List<float>();
                    var top = new List<float>();
                    int n = lod == 0 ? 8 : 3;
                    for (int i = 0; i <= n; i++)
                    {
                        float t = i / (float)n;
                        float z = Mathf.Lerp(zq + 0.27f, zq - 0.32f, t);
                        path.Add(new Vector2(s * (halfSpan + tipR * 0.9f), z));
                        // Swept leading edge: tall at the trailing edge, cut away low at the front.
                        bot.Add(H(Mathf.Lerp(0.66f, 0.4f, Mathf.Sqrt(t))));
                        top.Add(H(MainPlaneH - tipR + 0.01f) + 0.05f * Mathf.Sin(t * Mathf.PI));
                    }
                    mb.Fin(path, bot, top, 0.012f);
                }
            });
            // Swan-neck pylon from the crash structure to the main plane.
            Part(Carbon, 2, (mb, lod) =>
            {
                var path = new List<Vector2>();
                var bot = new List<float>();
                var top = new List<float>();
                for (int i = 0; i <= 5; i++)
                {
                    float t = i / 5f;
                    path.Add(new Vector2(0f, Mathf.Lerp(RearZ - 0.55f, zq + 0.08f, t)));
                    bot.Add(H(Mathf.Lerp(0.3f, MainPlaneH - 0.05f, t * t)));
                    top.Add(H(Mathf.Lerp(0.38f, MainPlaneH + 0.01f, Mathf.Sqrt(t))));
                }
                mb.Fin(path, bot, top, 0.02f);
            });
        }

        /// <summary>Span stations for a wing with a central dip whose tips roll 90° down (end-plate blend).</summary>
        List<MeshBuilder.WingStation> CurledSpan(float z, float h, float chord, float aoa, float halfSpan, float tipR, int n, bool curl)
        {
            var st = new List<MeshBuilder.WingStation>();
            int tipN = Mathf.Max(2, n / 4);
            for (int side = -1; side <= 1; side += 2)
            {
                var half = new List<MeshBuilder.WingStation>();
                for (int i = 0; i <= n / 2; i++)
                {
                    float x = halfSpan * i / (n / 2f);
                    float y = H(h - 0.025f * (1f - (x / halfSpan) * (x / halfSpan)));
                    half.Add(new MeshBuilder.WingStation { position = new Vector3(side * x, y, z), chord = chord, thickness = 0.11f, camber = -0.07f, aoaDeg = aoa });
                }
                if (curl)
                    for (int i = 1; i <= tipN; i++)
                    {
                        float a = i / (float)tipN * Mathf.PI * 0.5f;
                        var c = new Vector3(side * halfSpan, H(h) - tipR, z);
                        half.Add(new MeshBuilder.WingStation
                        {
                            position = c + new Vector3(side * Mathf.Sin(a) * tipR, Mathf.Cos(a) * tipR, 0f),
                            chord = chord,
                            thickness = 0.11f,
                            camber = -0.07f,
                            aoaDeg = aoa,
                            rollDeg = -side * a * Mathf.Rad2Deg,
                        });
                    }
                if (side < 0) { half.Reverse(); st.AddRange(half); }
                else st.AddRange(half.GetRange(1, half.Count - 1));
            }
            return st;
        }

        // --- Wheels ------------------------------------------------------------------------------

        /// <summary>Tire: lathe with rounded shoulders, sidewall bulge and a rim recess. Axis = local x.</summary>
        public static void TireMesh(MeshBuilder mb, float radius, float width, float rimRadius, int lod)
        {
            float hw = width * 0.5f;
            float shoulder = Mathf.Min(0.05f, (radius - rimRadius) * 0.35f);
            var profile = new List<Vector2>
            {
                new Vector2(-hw + 0.012f, rimRadius - 0.005f),
                new Vector2(-hw, rimRadius + 0.02f),
                new Vector2(-hw - 0.006f, (rimRadius + radius) * 0.5f),
                new Vector2(-hw, radius - shoulder),
                new Vector2(-hw + shoulder * 0.45f, radius - shoulder * 0.2f),
                new Vector2(-hw + shoulder, radius),
                new Vector2(hw - shoulder, radius),
                new Vector2(hw - shoulder * 0.45f, radius - shoulder * 0.2f),
                new Vector2(hw, radius - shoulder),
                new Vector2(hw + 0.006f, (rimRadius + radius) * 0.5f),
                new Vector2(hw, rimRadius + 0.02f),
                new Vector2(hw - 0.012f, rimRadius - 0.005f),
            };
            mb.Lathe(profile, LatheSegments[lod]);
        }

        /// <summary>Rim with lip and a flat aero wheel cover (current-regulation style).</summary>
        public static void RimMesh(MeshBuilder mb, float width, float rimRadius, int lod)
        {
            float hw = width * 0.5f - 0.01f;
            mb.Lathe(new List<Vector2>
            {
                new Vector2(-hw, rimRadius), new Vector2(-hw + 0.015f, rimRadius - 0.012f), new Vector2(hw - 0.015f, rimRadius - 0.012f), new Vector2(hw, rimRadius),
            }, LatheSegments[lod]);
            foreach (float s in new[] { -1f, 1f })
            {
                var cover = new List<Vector2>
                {
                    new Vector2(s * (hw - 0.004f), rimRadius - 0.004f),
                    new Vector2(s * (hw - 0.012f), rimRadius * 0.55f),
                    new Vector2(s * (hw - 0.02f), 0.05f),
                    new Vector2(s * (hw - 0.02f), 0.001f),
                };
                // Mirror image: reverse so the outside stays to the left of the profile.
                if (s < 0f) cover.Reverse();
                mb.Lathe(cover, LatheSegments[lod]);
            }
        }

        /// <summary>Compound band on the tire sidewall (coloured by the fitted compound).</summary>
        public static void CompoundBand(MeshBuilder mb, float radius, float width, float rimRadius, int lod)
        {
            float hw = width * 0.5f + 0.008f;
            float r0 = rimRadius + (radius - rimRadius) * 0.45f, r1 = r0 + 0.018f;
            foreach (float s in new[] { -1f, 1f })
                mb.Lathe(s < 0f
                    ? new List<Vector2> { new Vector2(s * hw, r0), new Vector2(s * hw, r1) }
                    : new List<Vector2> { new Vector2(s * hw, r1), new Vector2(s * hw, r0) }, LatheSegments[lod]);
        }

        /// <summary>Wheel-cover details: inner ring and a centre nut (spin with the wheel).</summary>
        public static void HubMesh(MeshBuilder mb, float width, float rimRadius, bool outerIsPositive, int lod)
        {
            float x = (outerIsPositive ? 1f : -1f) * (width * 0.5f - 0.015f);
            var nut = new List<Vector2> { new Vector2(0f, 0.001f), new Vector2(0f, 0.045f), new Vector2(0.025f, 0.042f), new Vector2(0.032f, 0.02f), new Vector2(0.034f, 0.001f) };
            var ring = new List<Vector2> { new Vector2(0f, rimRadius * 0.62f), new Vector2(0.006f, rimRadius * 0.62f), new Vector2(0.006f, rimRadius * 0.7f), new Vector2(0f, rimRadius * 0.7f) };
            foreach (var prof in new[] { nut, ring })
            {
                var p2 = new List<Vector2>();
                foreach (var p in prof) p2.Add(new Vector2(x + (outerIsPositive ? p.x : -p.x), p.y));
                if (!outerIsPositive) p2.Reverse();
                mb.Lathe(p2, LatheSegments[lod]);
            }
        }

        public static void BrakeDisc(MeshBuilder mb, float radius, int lod)
        {
            mb.Lathe(new List<Vector2>
            {
                new Vector2(-0.016f, radius * 0.55f), new Vector2(-0.016f, radius), new Vector2(0.016f, radius), new Vector2(0.016f, radius * 0.55f), new Vector2(-0.016f, radius * 0.55f),
            }, LatheSegments[lod]);
        }

        /// <summary>Upright, caliper and brake duct (steer with the wheel, do not spin).</summary>
        public static void Upright(MeshBuilder mb, float radius, float width, bool left, bool front, int lod)
        {
            float inward = left ? 1f : -1f;
            float x = inward * (width * 0.5f - 0.06f);
            mb.Box(new Vector3(x, 0f, 0f), new Vector3(0.05f, radius * 0.9f, 0.09f));
            // Caliper at the trailing side of the disc.
            mb.Box(new Vector3(x - inward * 0.04f, radius * 0.38f, -radius * 0.2f), new Vector3(0.05f, 0.11f, 0.07f), Quaternion.Euler(30f, 0f, 0f));
            // Brake duct drum (current-regulation style cooling inlet).
            mb.Loft(new List<LoftSection>
            {
                new LoftSection(radius * 0.55f, -0.02f, 0.05f, 0.07f, 3f, x + inward * 0.03f),
                new LoftSection(radius * 0.2f, -0.02f, 0.06f, 0.09f, 3f, x + inward * 0.03f),
            }, lod == 0 ? 10 : 6);
            if (front)
            {
                // Wheel deflector ("eyebrow") above the front tire, mounted on the upright.
                var path = new List<Vector2>();
                var bot = new List<float>();
                var top = new List<float>();
                for (int i = 0; i <= 6; i++)
                {
                    float a = Mathf.Lerp(-0.6f, 0.9f, i / 6f);
                    path.Add(new Vector2(x + inward * 0.02f, Mathf.Sin(a) * (radius + 0.03f)));
                    float h = Mathf.Cos(a) * (radius + 0.03f);
                    bot.Add(h);
                    top.Add(h + 0.012f);
                }
                mb.Fin(path, bot, top, 0.05f);
            }
        }

        // --- Suspension anchor points (car space) ---------------------------------------------

        /// <summary>
        /// Suspension links per corner: inner chassis point (car space) and outer point
        /// relative to the wheel's steering pivot (pivot space, moves with the wheel and
        /// with steering). Upper/lower wishbone legs, steering/toe link and push/pull rod.
        /// </summary>
        public static List<(Vector3 inner, Vector3 outer, float radius)> Links(VehicleSetup setup, int index)
        {
            bool front = index < 2;
            bool left = index % 2 == 0;
            float s = left ? -1f : 1f;
            float g = -setup.cogHeight;
            float z = front ? setup.FrontAxleZ : setup.RearAxleZ;
            float r = front ? setup.wheelRadiusFront : setup.wheelRadiusRear;
            float width = front ? 0.3f : 0.405f;
            float inX = front ? 0.17f : 0.22f;
            // Upright pickups in pivot space (pivot = wheel centre).
            float ox = -s * (width * 0.5f - 0.06f);
            var list = new List<(Vector3, Vector3, float)>();
            Vector3 upper = new Vector3(ox, r * 0.42f, 0f), lower = new Vector3(ox, -r * 0.45f, 0f);
            list.Add((new Vector3(s * inX, g + 0.46f, z + 0.28f), upper, 0.014f));
            list.Add((new Vector3(s * inX, g + 0.44f, z - 0.2f), upper, 0.014f));
            list.Add((new Vector3(s * (inX - 0.03f), g + 0.17f, z + 0.35f), lower, 0.014f));
            list.Add((new Vector3(s * (inX - 0.03f), g + 0.16f, z - 0.25f), lower, 0.014f));
            // Steering track rod (front) or toe link (rear), behind the axle.
            list.Add((new Vector3(s * (inX + 0.02f), g + 0.3f, z - 0.1f), new Vector3(ox, 0.02f, -0.12f), 0.011f));
            // Push rod (front) / pull rod (rear).
            if (front) list.Add((new Vector3(s * (inX - 0.02f), g + 0.52f, z - 0.05f), new Vector3(ox, -r * 0.4f, 0.02f), 0.012f));
            else list.Add((new Vector3(s * (inX - 0.02f), g + 0.12f, z + 0.1f), new Vector3(ox, r * 0.38f, 0.02f), 0.012f));
            return list;
        }

        // --- Steering wheel and hands (steering-wheel space: x right, y up, z towards the driver's view forward) ---

        public static Vector3 SteeringWheelPosition(VehicleSetup setup) => new Vector3(0f, -setup.cogHeight + 0.55f, setup.FrontAxleZ - 1.55f);

        public static void SteeringWheel(MeshBuilder carbon, MeshBuilder grips, MeshBuilder display)
        {
            // Body: wide, flat-bottomed formula wheel with grip horns (rings in the x-y plane,
            // extruded along z), smooth outline from a mirrored half shape.
            var half = new[]
            {
                new Vector2(0f, -0.07f), new Vector2(0.07f, -0.07f), new Vector2(0.115f, -0.06f), new Vector2(0.13f, -0.035f),
                new Vector2(0.125f, 0.02f), new Vector2(0.115f, 0.055f), new Vector2(0.08f, 0.07f), new Vector2(0f, 0.072f),
            };
            var outline = new List<Vector2>(half);
            for (int i = half.Length - 2; i >= 1; i--) outline.Add(new Vector2(-half[i].x, half[i].y));
            var rings = new List<Vector3[]>();
            foreach (var (z, scale) in new[] { (0.026f, 0.96f), (0.022f, 1f), (-0.02f, 1f), (-0.024f, 0.97f) })
            {
                var ring = new Vector3[outline.Count];
                for (int i = 0; i < outline.Count; i++) ring[i] = new Vector3(outline[i].x * scale, outline[i].y * scale, z);
                rings.Add(ring);
            }
            carbon.RingLoft(rings, true, true);
            // Grips: rubber, slightly barrel shaped, angled like real wheels.
            foreach (float s in new[] { -1f, 1f })
                grips.FairedTube(new[]
                {
                    new Vector3(s * 0.125f, -0.075f, -0.008f), new Vector3(s * 0.142f, -0.03f, -0.012f),
                    new Vector3(s * 0.145f, 0.02f, -0.012f), new Vector3(s * 0.132f, 0.065f, -0.008f),
                }, 0.04f, 0.048f, 12, Vector3.forward);
            // Shift paddles behind the wheel and clutch paddles below them.
            foreach (float s in new[] { -1f, 1f })
            {
                carbon.Box(new Vector3(s * 0.075f, 0.025f, 0.04f), new Vector3(0.075f, 0.09f, 0.006f), Quaternion.Euler(0f, 0f, -s * 10f));
                carbon.Box(new Vector3(s * 0.06f, -0.04f, 0.036f), new Vector3(0.06f, 0.035f, 0.005f));
            }
            // Display face (towards the driver: -z side) with a raised bezel.
            display.Box(new Vector3(0f, 0.008f, -0.0245f), new Vector3(0.105f, 0.062f, 0.002f));
            carbon.Box(new Vector3(0f, 0.008f, -0.0235f), new Vector3(0.118f, 0.074f, 0.002f));
            // Buttons and rotary switches around the display.
            for (int i = 0; i < 4; i++)
                foreach (float s in new[] { -1f, 1f })
                {
                    float y = 0.035f - i * 0.022f;
                    grips.Lathe(new List<Vector2> { new Vector2(-0.0035f, 0.001f), new Vector2(-0.0035f, 0.0065f), new Vector2(0.0035f, 0.0065f), new Vector2(0.0035f, 0.001f) }, 10);
                    // Lathe axis is x: tip the button towards the driver by building it in place.
                    MoveLast(grips, new Vector3(s * 0.078f, y, -0.026f), 10 * 4);
                }
            foreach (float s in new[] { -1f, 1f })
                foreach (float y in new[] { -0.045f, -0.02f })
                    display.Box(new Vector3(s * 0.035f + s * (y < -0.03f ? 0.02f : 0f), y, -0.027f), new Vector3(0.016f, 0.016f, 0.008f));
        }

        /// <summary>Rotates the last vertexCount vertices from the lathe's x axis to -z and moves them to position.</summary>
        static void MoveLast(MeshBuilder mb, Vector3 position, int vertexCount)
        {
            mb.TransformLast(vertexCount, v => position + new Vector3(v.y, v.z, -v.x));
        }

        /// <summary>Gloved hand wrapped around a vertical grip (local y along the grip, -z towards the driver).</summary>
        public static void GloveMesh(MeshBuilder mb, float side = 1f)
        {
            // Back of the hand / palm around the grip.
            var rings = new List<Vector3[]>();
            foreach (var (y, w, d) in new[] { (-0.045f, 0.03f, 0.028f), (-0.03f, 0.036f, 0.034f), (0.01f, 0.038f, 0.036f), (0.04f, 0.034f, 0.032f), (0.055f, 0.022f, 0.022f) })
            {
                var ring = new Vector3[14];
                for (int j = 0; j < 14; j++)
                {
                    float t = Mathf.PI * 2f * j / 14;
                    ring[j] = new Vector3(side * Mathf.Cos(t) * w, y, Mathf.Sin(t) * d - 0.008f);
                }
                rings.Add(ring);
            }
            mb.RingLoft(rings, true, true);
            // Thumb resting on the front face of the wheel.
            mb.FairedTube(new[] { new Vector3(side * 0.01f, 0.03f, -0.03f), new Vector3(-side * 0.012f, 0.045f, -0.042f), new Vector3(-side * 0.03f, 0.05f, -0.045f) }, 0.022f, 0.02f, 8, Vector3.up);
            // Wrist cuff.
            mb.FairedTube(new[] { new Vector3(0f, -0.045f, 0f), new Vector3(side * 0.01f, -0.09f, 0.03f) }, 0.05f, 0.045f, 12, Vector3.forward);
        }

        public static void HelmetMesh(MeshBuilder shell, MeshBuilder visor)
        {
            shell.Loft(new List<LoftSection>
            {
                new LoftSection(0.15f, 0.0f, 0.03f, 0.03f, 2f),
                new LoftSection(0.12f, 0.01f, 0.1f, 0.11f, 2.2f),
                new LoftSection(0.02f, 0.02f, 0.135f, 0.14f, 2.2f),
                new LoftSection(-0.1f, 0.0f, 0.12f, 0.13f, 2.2f),
                new LoftSection(-0.15f, -0.02f, 0.05f, 0.06f, 2f),
            }, 16);
            visor.Loft(new List<LoftSection>
            {
                new LoftSection(0.128f, 0.035f, 0.1f, 0.03f, 4f),
                new LoftSection(0.07f, 0.04f, 0.135f, 0.032f, 4f),
            }, 16, true, true, -10f * Mathf.Deg2Rad, 190f * Mathf.Deg2Rad);
        }
    }
}
