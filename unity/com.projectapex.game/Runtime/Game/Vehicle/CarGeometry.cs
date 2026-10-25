using System.Collections.Generic;
using Apex.Vehicle;
using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// Pure geometry of the original placeholder formula car (no GameObjects), so it
    /// can be generated, checked and previewed outside the engine. Parts are emitted
    /// per material and per LOD (0 = full detail, 1 = medium, 2 = far). Coordinates:
    /// car space relative to the centre of mass, ground at y = -cogHeight, +z forward.
    ///
    /// This is procedural placeholder art built from lofts, wing sections and tubes.
    /// It is deliberately not photorealistic; docs/VEHICLE_ASSET_SPEC.md specifies the
    /// authored model that will replace it.
    /// </summary>
    public sealed class CarGeometry
    {
        public const int LodCount = 3;

        // Material keys (resolved by the engine layer; Paint/Accent are the livery).
        public const string Paint = "CarPaint";
        public const string Accent = "CarAccent";
        public const string Carbon = "Carbon";
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

        static readonly int[] LoftSegments = { 28, 14, 8 };
        static readonly int[] TubeSides = { 8, 5, 4 };
        static readonly int[] LatheSegments = { 32, 16, 10 };

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

        /// <summary>Calls emit for every LOD up to maxLod.</summary>
        void Part(string mat, int maxLod, System.Action<MeshBuilder, int> emit)
        {
            for (int lod = 0; lod <= maxLod; lod++) emit(B(lod, mat), lod);
        }

        public CarGeometry BuildBody()
        {
            Floor();
            Monocoque();
            EngineCover();
            Sidepods();
            target = frontWing;
            FrontWing();
            target = rearWing;
            RearWing();
            target = body;
            Halo();
            Mirrors();
            CockpitInterior();
            Details();
            return this;
        }

        LoftSection S(float z, float h, float hw, float hh, float e = 3f, float x = 0f) => new LoftSection(z, H(h), hw, hh, e, x);

        void Floor()
        {
            float f = FrontZ, r = RearZ;
            // Plan-view outline: narrow at the front, full width under the sidepods,
            // tapering into the diffuser. Floor edges flick up slightly (separate strip).
            var outline = new List<Vector2>
            {
                new Vector2(0.55f, f - 0.95f), new Vector2(0.9f, f - 1.45f), new Vector2(0.92f, r + 0.35f),
                new Vector2(0.62f, r - 0.05f), new Vector2(-0.62f, r - 0.05f), new Vector2(-0.92f, r + 0.35f),
                new Vector2(-0.9f, f - 1.45f), new Vector2(-0.55f, f - 0.95f),
            };
            outline.Reverse();
            Part(Carbon, 2, (mb, lod) => mb.PlateXZ(outline, H(0.05f), 0.025f));
            // Edge wings along the floor sides.
            Part(Carbon, 1, (mb, lod) =>
            {
                foreach (float s in new[] { -1f, 1f })
                    mb.Airfoil(new Vector3(s * 0.86f, H(0.09f), (f - 1.45f + r + 0.35f) * 0.5f), 0.06f, 0.1f, 0.1f, 0f, 0f, 4);
            });
            // Plank.
            Part(Interior, 1, (mb, lod) => mb.Box(new Vector3(0f, H(0.032f), (f - 0.9f + r) * 0.5f), new Vector3(0.3f, 0.012f, f - 0.9f - r)));
            // Diffuser: ramp plus strakes.
            float dz0 = r + 0.3f, dz1 = r - 0.42f;
            Part(Carbon, 2, (mb, lod) =>
            {
                var ramp = new List<LoftSection>
                {
                    new LoftSection(dz0, H(0.065f), 0.62f, 0.012f, 6f),
                    new LoftSection(r - 0.05f, H(0.15f), 0.6f, 0.012f, 6f),
                    new LoftSection(dz1, H(0.3f), 0.55f, 0.012f, 6f),
                };
                mb.Loft(ramp, Mathf.Max(8, LoftSegments[lod] / 2));
            });
            Part(Carbon, 0, (mb, lod) =>
            {
                foreach (float x in new[] { -0.55f, -0.3f, 0.3f, 0.55f })
                    mb.PlateYZ(new[] { new Vector2(dz0, H(0.06f)), new Vector2(dz1, H(0.3f)), new Vector2(dz1, H(0.06f)) }, x, 0.012f);
            });
        }

        void Monocoque()
        {
            float f = FrontZ;
            // Nose and chassis ahead of the cockpit (closed loft, front to back).
            var nose = new List<LoftSection>
            {
                S(f + 1.18f, 0.2f, 0.05f, 0.04f, 2.5f),
                S(f + 1.05f, 0.21f, 0.09f, 0.06f, 2.6f),
                S(f + 0.8f, 0.24f, 0.13f, 0.09f, 2.8f),
                S(f + 0.4f, 0.29f, 0.17f, 0.13f, 3f),
                S(f, 0.34f, 0.21f, 0.17f, 3.2f),
                S(f - 0.5f, 0.38f, 0.25f, 0.21f, 3.4f),
                S(f - 1.0f, 0.41f, 0.29f, 0.23f, 3.6f),
                S(f - 1.42f, 0.42f, 0.31f, 0.24f, 3.8f),
            };
            Part(Paint, 2, (mb, lod) => mb.Loft(nose, LoftSegments[lod], true, false));
            // Cockpit tub: open on top between the front bulkhead and the roll hoop.
            float c0 = f - 1.42f, c1 = f - 2.25f;
            var tub = new List<LoftSection>
            {
                S(c0, 0.42f, 0.31f, 0.24f, 3.8f),
                S(c0 - 0.3f, 0.42f, 0.33f, 0.24f, 3.8f),
                S(c1, 0.42f, 0.34f, 0.24f, 3.8f),
            };
            // Arc from 70° over the side and bottom round to 110°: leaves a ~40° top opening.
            Part(Paint, 2, (mb, lod) => mb.Loft(tub, LoftSegments[lod], false, false,
                110f * Mathf.Deg2Rad, (360f + 70f) * Mathf.Deg2Rad, lod == 0));
            // Cockpit coaming (padded rim) around the opening.
            Part(Interior, 1, (mb, lod) =>
            {
                float y = H(0.655f);
                var rim = new List<Vector3>
                {
                    new Vector3(0f, y, c0 + 0.02f), new Vector3(0.12f, y, c0 - 0.04f), new Vector3(0.15f, y, c0 - 0.4f),
                    new Vector3(0.15f, y, c1 + 0.05f), new Vector3(0f, y + 0.02f, c1 - 0.02f), new Vector3(-0.15f, y, c1 + 0.05f),
                    new Vector3(-0.15f, y, c0 - 0.4f), new Vector3(-0.12f, y, c0 - 0.04f), new Vector3(0f, y, c0 + 0.02f),
                };
                mb.TubePath(rim, 0.025f, TubeSides[lod], false);
            });
            // Floor of the cockpit (visible from the onboard camera).
            Part(Interior, 0, (mb, lod) => mb.Box(new Vector3(0f, H(0.22f), (c0 + c1) * 0.5f), new Vector3(0.5f, 0.02f, c0 - c1)));
            // Nose underside camera/pitot details.
            Part(Carbon, 0, (mb, lod) => mb.Tube(new Vector3(0f, H(0.19f), f + 1.16f), new Vector3(0f, H(0.19f), f + 1.32f), 0.006f, TubeSides[lod]));
        }

        void EngineCover()
        {
            float c1 = FrontZ - 2.25f;
            var cover = new List<LoftSection>
            {
                S(c1, 0.46f, 0.32f, 0.22f, 3.6f),
                S(c1 - 0.25f, 0.5f, 0.27f, 0.27f, 3f),
                S(c1 - 0.7f, 0.5f, 0.24f, 0.26f, 2.8f),
                S(c1 - 1.3f, 0.43f, 0.19f, 0.2f, 2.6f),
                S(c1 - 1.85f, 0.36f, 0.1f, 0.12f, 2.4f),
                S(c1 - 2.25f, 0.33f, 0.05f, 0.06f, 2.2f),
            };
            Part(Paint, 2, (mb, lod) => mb.Loft(cover, LoftSegments[lod], true, true));
            // Airbox / roll hoop above the driver's head.
            var hoop = new List<LoftSection>
            {
                S(c1 + 0.02f, 0.88f, 0.1f, 0.09f, 2.2f),
                S(c1 - 0.18f, 0.84f, 0.13f, 0.12f, 2.4f),
                S(c1 - 0.6f, 0.72f, 0.12f, 0.1f, 2.4f),
                S(c1 - 1.1f, 0.58f, 0.06f, 0.06f, 2.2f),
            };
            Part(Paint, 2, (mb, lod) => mb.Loft(hoop, LoftSegments[lod] / 2 + 4, true, true));
            Part(Interior, 1, (mb, lod) => mb.Loft(new List<LoftSection>
            {
                S(c1 + 0.03f, 0.88f, 0.075f, 0.065f, 2.2f),
                S(c1 + 0.01f, 0.88f, 0.075f, 0.065f, 2.2f),
            }, 12, true, true));
            // Shark fin.
            Part(Paint, 1, (mb, lod) => mb.PlateYZ(new[]
            {
                new Vector2(c1 - 0.55f, H(0.74f)), new Vector2(c1 - 1.9f, H(0.62f)), new Vector2(c1 - 2.0f, H(0.42f)), new Vector2(c1 - 1.1f, H(0.55f)),
            }, 0f, 0.01f));
            // Rear crash structure.
            float r = RearZ;
            Part(Carbon, 2, (mb, lod) => mb.Loft(new List<LoftSection>
            {
                S(r - 0.35f, 0.33f, 0.09f, 0.08f, 3f),
                S(r - 0.75f, 0.3f, 0.05f, 0.05f, 3f),
            }, LoftSegments[lod] / 2, true, true));
        }

        void Sidepods()
        {
            float f = FrontZ;
            foreach (float s in new[] { -1f, 1f })
            {
                var pod = new List<LoftSection>
                {
                    S(f - 1.25f, 0.3f, 0.17f, 0.12f, 4f, s * 0.58f),
                    S(f - 1.5f, 0.31f, 0.2f, 0.15f, 4f, s * 0.6f),
                    S(f - 2.1f, 0.3f, 0.2f, 0.15f, 3.6f, s * 0.56f),
                    S(f - 2.7f, 0.26f, 0.16f, 0.13f, 3f, s * 0.46f),
                    S(f - 3.2f, 0.2f, 0.1f, 0.09f, 2.6f, s * 0.33f),
                    S(f - 3.55f, 0.17f, 0.05f, 0.05f, 2.4f, s * 0.24f),
                };
                Part(Paint, 2, (mb, lod) => mb.Loft(pod, LoftSegments[lod], false, true));
                // Dark radiator inlet in the open front of the pod.
                Part(Interior, 2, (mb, lod) => mb.Loft(new List<LoftSection>
                {
                    S(f - 1.24f, 0.3f, 0.155f, 0.105f, 4f, s * 0.58f),
                    S(f - 1.27f, 0.3f, 0.155f, 0.105f, 4f, s * 0.58f),
                }, LoftSegments[lod], true, true));
                // Undercut / floor fence ahead of the pod.
                Part(Carbon, 0, (mb, lod) => mb.PlateYZ(new[]
                {
                    new Vector2(f - 0.9f, H(0.07f)), new Vector2(f - 0.95f, H(0.2f)), new Vector2(f - 1.25f, H(0.2f)), new Vector2(f - 1.3f, H(0.07f)),
                }, s * 0.48f, 0.01f));
                // Accent stripe along the top of the pod.
                Part(Accent, 1, (mb, lod) => mb.Box(new Vector3(s * 0.57f, H(0.462f), f - 1.9f), new Vector3(0.12f, 0.006f, 0.9f)));
            }
        }

        void FrontWing()
        {
            float z = FrontZ + 0.95f;
            // Four elements, stacked and increasingly steep (inverted: downforce).
            var elements = new[]
            {
                (dz: 0.12f, h: 0.085f, chord: 0.28f, aoa: 4f, mat: Carbon),
                (dz: -0.07f, h: 0.12f, chord: 0.17f, aoa: 14f, mat: Carbon),
                (dz: -0.18f, h: 0.165f, chord: 0.13f, aoa: 24f, mat: Accent),
                (dz: -0.26f, h: 0.215f, chord: 0.1f, aoa: 34f, mat: Accent),
            };
            for (int i = 0; i < elements.Length; i++)
            {
                var e = elements[i];
                int maxLod = i < 2 ? 2 : 1;
                Part(e.mat, maxLod, (mb, lod) =>
                {
                    int seg = lod == 0 ? 8 : 4;
                    if (i == 0)
                    {
                        // Main plane runs under the nose.
                        mb.Airfoil(new Vector3(0f, H(e.h), z + e.dz), 1.92f, e.chord, 0.1f, -0.06f, e.aoa, seg);
                    }
                    else
                    {
                        // Flaps stop at the nose and rise towards the end plates.
                        foreach (float s in new[] { -1f, 1f })
                        {
                            mb.Airfoil(new Vector3(s * 0.31f, H(e.h), z + e.dz), 0.5f, e.chord, 0.09f, -0.06f, e.aoa, seg);
                            mb.Airfoil(new Vector3(s * 0.79f, H(e.h + 0.02f), z + e.dz + 0.02f), 0.38f, e.chord, 0.09f, -0.06f, e.aoa - 3f, seg);
                        }
                    }
                });
            }
            Part(Carbon, 2, (mb, lod) =>
            {
                foreach (float s in new[] { -1f, 1f })
                    mb.PlateYZ(new[]
                    {
                        new Vector2(z + 0.28f, H(0.05f)), new Vector2(z + 0.24f, H(0.2f)), new Vector2(z - 0.18f, H(0.3f)),
                        new Vector2(z - 0.36f, H(0.24f)), new Vector2(z - 0.32f, H(0.05f)),
                    }, s * 0.975f, 0.012f);
            });
            // Nose pylons to the main plane.
            Part(Carbon, 1, (mb, lod) =>
            {
                foreach (float s in new[] { -1f, 1f })
                    mb.PlateYZ(new[]
                    {
                        new Vector2(z + 0.18f, H(0.1f)), new Vector2(z + 0.12f, H(0.2f)), new Vector2(z - 0.08f, H(0.22f)), new Vector2(z - 0.06f, H(0.1f)),
                    }, s * 0.06f, 0.012f);
            });
        }

        void RearWing()
        {
            float z = RearZ - 0.82f;
            Part(Carbon, 2, (mb, lod) => mb.Airfoil(new Vector3(0f, H(0.83f), z + 0.06f), 1.0f, 0.36f, 0.11f, -0.07f, 8f, lod == 0 ? 10 : 4));
            Part(Accent, 2, (mb, lod) => mb.Airfoil(new Vector3(0f, H(0.97f), z - 0.15f), 1.0f, 0.2f, 0.09f, -0.06f, 30f, lod == 0 ? 8 : 4));
            // Beam wing (two small elements low at the rear).
            Part(Carbon, 1, (mb, lod) =>
            {
                mb.Airfoil(new Vector3(0f, H(0.36f), z + 0.12f), 0.9f, 0.14f, 0.1f, -0.05f, 6f, 4);
                mb.Airfoil(new Vector3(0f, H(0.42f), z + 0.01f), 0.9f, 0.1f, 0.1f, -0.05f, 18f, 4);
            });
            // End plates curving into the wing tips.
            Part(Paint, 2, (mb, lod) =>
            {
                foreach (float s in new[] { -1f, 1f })
                    mb.PlateYZ(new[]
                    {
                        new Vector2(z + 0.3f, H(0.62f)), new Vector2(z + 0.32f, H(0.9f)), new Vector2(z + 0.18f, H(1.05f)),
                        new Vector2(z - 0.28f, H(1.08f)), new Vector2(z - 0.34f, H(0.95f)), new Vector2(z - 0.25f, H(0.62f)),
                    }, s * 0.505f, 0.012f);
            });
            // Swan-neck pylon from the crash structure to the main plane.
            Part(Carbon, 2, (mb, lod) => mb.PlateYZ(new[]
            {
                new Vector2(z + 0.48f, H(0.36f)), new Vector2(z + 0.3f, H(0.62f)), new Vector2(z + 0.12f, H(0.85f)),
                new Vector2(z - 0.0f, H(0.85f)), new Vector2(z + 0.18f, H(0.58f)), new Vector2(z + 0.32f, H(0.34f)),
            }, 0f, 0.02f));
        }

        void Halo()
        {
            float c0 = FrontZ - 1.42f;
            float top = H(0.84f);
            Part(Titanium, 2, (mb, lod) =>
            {
                int sides = TubeSides[lod];
                // Central pylon from the chassis to the front of the hoop.
                mb.TubePath(new[] { new Vector3(0f, H(0.62f), c0 + 0.12f), new Vector3(0f, top - 0.02f, c0 - 0.04f) }, 0.022f, sides);
                foreach (float s in new[] { -1f, 1f })
                {
                    var path = new List<Vector3>();
                    for (int i = 0; i <= 10; i++)
                    {
                        float t = i / 10f;
                        float ang = t * Mathf.PI * 0.5f;
                        // Plan view: from the front tip around the head to the rear mounts.
                        float x = s * 0.28f * Mathf.Sin(ang);
                        float zz = (c0 - 0.04f) - 0.5f * (1f - Mathf.Cos(ang)) - 0.12f * t;
                        float y = top - 0.03f * t * t;
                        path.Add(new Vector3(x, y, zz));
                    }
                    // Rear legs down to the chassis.
                    path.Add(new Vector3(s * 0.3f, H(0.7f), c0 - 0.72f));
                    path.Add(new Vector3(s * 0.31f, H(0.6f), c0 - 0.74f));
                    mb.TubePath(path, 0.024f, sides);
                }
            });
        }

        void Mirrors()
        {
            float zc = FrontZ - 1.45f;
            foreach (float s in new[] { -1f, 1f })
            {
                Part(Carbon, 1, (mb, lod) =>
                {
                    mb.Tube(new Vector3(s * 0.3f, H(0.56f), zc), new Vector3(s * 0.48f, H(0.66f), zc + 0.04f), 0.012f, TubeSides[lod]);
                    mb.Box(new Vector3(s * 0.52f, H(0.67f), zc + 0.05f), new Vector3(0.15f, 0.065f, 0.06f));
                });
                Part(Mirror, 1, (mb, lod) => mb.Box(new Vector3(s * 0.52f, H(0.67f), zc + 0.018f), new Vector3(0.135f, 0.05f, 0.004f)));
            }
        }

        void CockpitInterior()
        {
            float c0 = FrontZ - 1.42f, c1 = FrontZ - 2.25f;
            // Seat shell and headrest pads.
            Part(Interior, 0, (mb, lod) =>
            {
                mb.Box(new Vector3(0f, H(0.32f), c1 + 0.12f), new Vector3(0.42f, 0.18f, 0.24f), Quaternion.Euler(-25f, 0f, 0f));
                foreach (float s in new[] { -1f, 1f })
                    mb.Box(new Vector3(s * 0.19f, H(0.7f), c1 + 0.05f), new Vector3(0.08f, 0.14f, 0.2f));
            });
            // Six-point harness: shoulder straps and lap belts.
            Part(Belt, 0, (mb, lod) =>
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    mb.Box(new Vector3(s * 0.09f, H(0.6f), c1 + 0.2f), new Vector3(0.045f, 0.2f, 0.008f), Quaternion.Euler(-35f, 0f, 0f));
                    mb.Box(new Vector3(s * 0.12f, H(0.43f), c1 + 0.32f), new Vector3(0.045f, 0.008f, 0.16f), Quaternion.Euler(0f, s * 25f, 0f));
                }
            });
            // Driver: shoulders, HANS and helmet are separate (helmet hides in first person).
            Part(Interior, 1, (mb, lod) =>
            {
                mb.Loft(new List<LoftSection>
                {
                    S(c1 + 0.32f, 0.5f, 0.18f, 0.1f, 3f),
                    S(c1 + 0.12f, 0.52f, 0.22f, 0.12f, 3f),
                    S(c1 - 0.02f, 0.52f, 0.2f, 0.1f, 3f),
                }, LoftSegments[lod] / 2);
            });
            // Dash panel behind the steering wheel.
            Part(Carbon, 0, (mb, lod) => mb.Box(new Vector3(0f, H(0.58f), c0 - 0.06f), new Vector3(0.36f, 0.06f, 0.1f), Quaternion.Euler(20f, 0f, 0f)));
        }

        void Details()
        {
            float c1 = FrontZ - 2.25f;
            // T-camera on top of the roll hoop, onboard cameras on the nose.
            Part(Accent, 1, (mb, lod) => mb.Box(new Vector3(0f, H(0.99f), c1 - 0.12f), new Vector3(0.16f, 0.04f, 0.08f)));
            Part(Carbon, 0, (mb, lod) =>
            {
                foreach (float s in new[] { -1f, 1f })
                    mb.Loft(new List<LoftSection>
                    {
                        S(FrontZ + 0.3f, 0.43f, 0.016f, 0.016f, 2f, s * 0.13f),
                        S(FrontZ + 0.15f, 0.43f, 0.022f, 0.022f, 2f, s * 0.13f),
                        S(FrontZ + 0.05f, 0.42f, 0.012f, 0.012f, 2f, s * 0.13f),
                    }, 8);
            });
            // Bargeboard-style deflectors behind the front wheels.
            Part(Carbon, 0, (mb, lod) =>
            {
                foreach (float s in new[] { -1f, 1f })
                    mb.PlateYZ(new[]
                    {
                        new Vector2(FrontZ - 0.55f, H(0.07f)), new Vector2(FrontZ - 0.6f, H(0.3f)), new Vector2(FrontZ - 0.85f, H(0.32f)), new Vector2(FrontZ - 0.85f, H(0.07f)),
                    }, s * 0.72f, 0.01f);
            });
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
            // Body: flat-bottomed rectangle typical of formula wheels.
            carbon.Loft(new List<LoftSection>
            {
                new LoftSection(0.02f, 0f, 0.13f, 0.075f, 5f),
                new LoftSection(-0.02f, 0f, 0.13f, 0.075f, 5f),
            }, 24);
            // Grips.
            foreach (float s in new[] { -1f, 1f })
                grips.TubePath(new[] { new Vector3(s * 0.135f, -0.07f, -0.01f), new Vector3(s * 0.15f, 0f, -0.012f), new Vector3(s * 0.135f, 0.07f, -0.01f) }, 0.022f, 8);
            // Paddles behind the wheel.
            foreach (float s in new[] { -1f, 1f })
                carbon.Box(new Vector3(s * 0.07f, 0.02f, 0.04f), new Vector3(0.08f, 0.1f, 0.006f));
            // Display face (facing the driver: -z side).
            display.Box(new Vector3(0f, 0.005f, -0.0215f), new Vector3(0.11f, 0.065f, 0.002f));
        }

        public static void GloveMesh(MeshBuilder mb)
        {
            mb.Loft(new List<LoftSection>
            {
                new LoftSection(0.06f, 0f, 0.012f, 0.012f, 2f),
                new LoftSection(0.03f, 0f, 0.04f, 0.03f, 2.4f),
                new LoftSection(-0.03f, 0f, 0.045f, 0.033f, 2.4f),
                new LoftSection(-0.07f, 0f, 0.03f, 0.03f, 2f),
            }, 10);
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
