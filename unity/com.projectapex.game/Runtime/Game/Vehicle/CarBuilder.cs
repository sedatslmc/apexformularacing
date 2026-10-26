using System;
using System.Collections.Generic;
using Apex.Vehicle;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Apex.Game
{
    /// <summary>
    /// Builds the original placeholder formula car (procedural geometry from
    /// <see cref="CarGeometry"/>, three LODs) plus its physics colliders and
    /// presentation (<see cref="CarVisuals"/>). Proportions follow the design
    /// reference (~5.4 m long, ~1.9 m wide). It is placeholder art, not a
    /// photorealistic model; it will be replaced by an authored asset
    /// (docs/VEHICLE_ASSET_SPEC.md) without touching physics. Coordinates are
    /// relative to the centre of mass; ground is at y = -cogHeight; forward is +z.
    /// </summary>
    public static class CarBuilder
    {
        static readonly Dictionary<PrimitiveType, Mesh> Meshes = new Dictionary<PrimitiveType, Mesh>();

        public static Mesh PrimitiveMesh(PrimitiveType type)
        {
            if (Meshes.TryGetValue(type, out var m) && m != null) return m;
            var go = GameObject.CreatePrimitive(type);
            m = go.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
            Meshes[type] = m;
            return m;
        }

        static readonly Dictionary<string, Material> TeamMaterials = new Dictionary<string, Material>();

        /// <summary>Material instance for a livery colour (cached per colour).</summary>
        public static Material Paint(Color c, string baseKey = "CarPaint")
        {
            string key = baseKey + ColorUtility.ToHtmlStringRGB(c);
            if (TeamMaterials.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(ApexMaterials.Get(baseKey)) { name = "Livery_" + key };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            TeamMaterials[key] = m;
            return m;
        }

        /// <summary>Builds a car with a livery.</summary>
        public static VehicleController Build(string name, VehicleSetup setup, Color primary, Color accent)
        {
            return Build(name, setup, Paint(primary), Paint(accent, "CarAccent"));
        }

        /// <summary>Shared, setup-independent meshes are cached (body geometry depends on the setup
        /// only through axle positions, so one cache entry per wheelbase/weight split).</summary>
        static readonly Dictionary<string, Mesh> MeshCache = new Dictionary<string, Mesh>();

        static Mesh Cached(string key, System.Func<Mesh> make)
        {
            if (MeshCache.TryGetValue(key, out var m) && m != null) return m;
            m = make();
            MeshCache[key] = m;
            return m;
        }

        static string SetupKey(VehicleSetup s) => $"{s.wheelbase:F3}_{s.weightFront:F3}_{s.cogHeight:F3}_{s.trackFront:F3}_{s.trackRear:F3}";

        static Material Mat(string key, Material paint, Material accent)
        {
            if (key == CarGeometry.Paint) return paint;
            if (key == CarGeometry.Accent) return accent;
            return ApexMaterials.Get(key);
        }

        static Renderer AddMesh(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos = default, Quaternion? rot = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            return mr;
        }

        static Mesh Make(System.Action<MeshBuilder> build, string name)
        {
            var mb = new MeshBuilder();
            build(mb);
            return mb.ToMesh(name);
        }

        public static VehicleController Build(string name, VehicleSetup setup)
        {
            return Build(name, setup, ApexMaterials.Get("CarPaint"), ApexMaterials.Get("CarAccent"));
        }

        public static VehicleController Build(string name, VehicleSetup setup, Material paint, Material accent)
        {
            var root = new GameObject(name);
            // Inactive while assembling so VehicleController.Awake sees the final setup.
            root.SetActive(false);
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = setup.mass;
            var mat = RigidbodyCompat.MakeMaterial("CarBody", 0.3f, 0.1f);
            // Body collider: floor box on the plank line and a nose box (see BodyCollider).
            int bi = 0;
            foreach (var b in new BodyCollider(setup).boxes)
            {
                var bodyGo = new GameObject("BodyCollider" + bi++);
                bodyGo.transform.SetParent(root.transform, false);
                bodyGo.transform.localPosition = b.centre.ToUnity();
                bodyGo.transform.localRotation = Quaternion.Euler(b.PitchDegrees, 0f, 0f);
                var body = bodyGo.AddComponent<BoxCollider>();
                body.size = b.size.ToUnity();
                body.sharedMaterial = mat;
            }
            for (int i = 0; i < 4; i++)
            {
                var mount = setup.MountPosition(i, out float sc);
                bool front = i < 2;
                float r = front ? setup.wheelRadiusFront : setup.wheelRadiusRear;
                var s = root.AddComponent<SphereCollider>();
                s.radius = 0.28f;
                s.center = new Vector3(mount.X, r - setup.cogHeight, mount.Z);
                s.sharedMaterial = mat;
            }
            var vc = root.AddComponent<VehicleController>();
            vc.setup = setup;
            BuildVisual(root.transform, vc, setup, paint, accent);
            root.SetActive(true);
            return vc;
        }

        static void BuildVisual(Transform root, VehicleController vc, VehicleSetup setup, Material paint, Material accent)
        {
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root, false);
            var vis = root.gameObject.AddComponent<CarVisuals>();
            vis.vehicle = vc;
            string key = SetupKey(setup);
            var lodRenderers = new List<Renderer>[CarGeometry.LodCount];
            for (int i = 0; i < CarGeometry.LodCount; i++) lodRenderers[i] = new List<Renderer>();

            // Body and the two wings: one mesh per material and LOD. The wings hang off
            // pivots near their mounts so damage can droop or remove them.
            CarGeometry geo = null;
            float g0 = -setup.cogHeight;
            var groups = new (string name, System.Func<CarGeometry, Dictionary<string, MeshBuilder>[]> select, Vector3 pivot)[]
            {
                ("Body", c => c.body, Vector3.zero),
                ("FrontWing", c => c.frontWing, new Vector3(0f, g0 + 0.15f, setup.FrontAxleZ + 1.05f)),
                ("RearWing", c => c.rearWing, new Vector3(0f, g0 + 0.5f, setup.RearAxleZ - 0.5f)),
            };
            foreach (var grp in groups)
            {
                var pivot = new GameObject(grp.name).transform;
                pivot.SetParent(visual, false);
                pivot.localPosition = grp.pivot;
                var groupRenderers = new List<Renderer>();
                for (int lod = 0; lod < CarGeometry.LodCount; lod++)
                {
                    var lodRoot = new GameObject(grp.name + "LOD" + lod).transform;
                    lodRoot.SetParent(pivot, false);
                    lodRoot.localPosition = -grp.pivot;
                    foreach (var matKey in BodyMaterials)
                    {
                        string meshKey = $"{grp.name}_{key}_{lod}_{matKey}";
                        if (!MeshCache.TryGetValue(meshKey, out var mesh) || mesh == null)
                        {
                            if (geo == null) geo = new CarGeometry(setup).BuildBody();
                            if (!grp.select(geo)[lod].TryGetValue(matKey, out var mb)) continue;
                            mesh = mb.ToMesh(meshKey);
                            MeshCache[meshKey] = mesh;
                        }
                        var r = AddMesh(lodRoot, matKey, mesh, Mat(matKey, paint, accent));
                        lodRenderers[lod].Add(r);
                        groupRenderers.Add(r);
                    }
                }
                if (grp.name == "FrontWing") { vis.frontWing = pivot; vis.frontWingRenderers = groupRenderers.ToArray(); }
                if (grp.name == "RearWing") { vis.rearWing = pivot; vis.rearWingRenderers = groupRenderers.ToArray(); }
            }

            // Wheels: pivot (position + steering) -> spinner (tire, rim, disc) + upright (static).
            var bands = new List<Renderer>();
            var bandWheel = new List<int>();
            var links = new List<CarVisuals.Link>();
            var linkMesh = Cached("unit_tube", () => Make(mb => mb.Tube(Vector3.zero, Vector3.forward, 1f, 6), "UnitTube"));
            vc.WheelSpinners = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                bool left = i % 2 == 0;
                string label = (front ? "F" : "R") + (left ? "L" : "R");
                var mount = setup.MountPosition(i, out _);
                float r = front ? setup.wheelRadiusFront : setup.wheelRadiusRear;
                float width = front ? 0.3f : 0.405f;
                const float RimRadius = 0.229f;
                var pivot = new GameObject("Wheel" + label).transform;
                pivot.SetParent(visual, false);
                pivot.localPosition = new Vector3(mount.X, r - setup.cogHeight, mount.Z);
                var spinner = new GameObject("Spin").transform;
                spinner.SetParent(pivot, false);
                vc.WheelVisuals[i] = pivot;
                vc.WheelSpinners[i] = spinner;
                string wk = $"{r:F3}_{width:F3}";
                // LOD0 and LOD1 get their own tire/rim/compound band; LOD2 reuses LOD1.
                for (int lod = 0; lod < 2; lod++)
                {
                    int detail = lod;
                    var tire = Cached($"tire_{wk}_{detail}", () => Make(mb => CarGeometry.TireMesh(mb, r, width, RimRadius, detail), "Tire"));
                    var rim = Cached($"rim_{wk}_{detail}", () => Make(mb => CarGeometry.RimMesh(mb, width, RimRadius, detail), "Rim"));
                    var band = Cached($"band_{wk}_{detail}", () => Make(mb => CarGeometry.CompoundBand(mb, r, width, RimRadius, detail), "Band"));
                    lodRenderers[lod].Add(AddMesh(spinner, "TireLOD" + lod, tire, ApexMaterials.Get(CarGeometry.Tire)));
                    lodRenderers[lod].Add(AddMesh(spinner, "RimLOD" + lod, rim, ApexMaterials.Get(CarGeometry.Rim)));
                    var br = AddMesh(spinner, "CompoundLOD" + lod, band, ApexMaterials.Get("Compound"));
                    lodRenderers[lod].Add(br);
                    bands.Add(br);
                    bandWheel.Add(i);
                }
                // LOD2 reuses the low-detail tire and rim.
                lodRenderers[2].Add(spinner.Find("TireLOD1").GetComponent<Renderer>());
                lodRenderers[2].Add(spinner.Find("RimLOD1").GetComponent<Renderer>());
                // Wheel-cover ring and centre nut on the outer face (left wheels face -x).
                var hub = Cached($"hub_{wk}_{left}", () => Make(mb => CarGeometry.HubMesh(mb, width, RimRadius, !left, 0), "Hub"));
                var hubR = AddMesh(spinner, "Hub", hub, accent);
                lodRenderers[0].Add(hubR);
                var disc = Cached("disc", () => Make(mb => CarGeometry.BrakeDisc(mb, 0.16f, 0), "BrakeDisc"));
                float discX = (left ? 1f : -1f) * (width * 0.5f - 0.1f);
                var discR = AddMesh(spinner, "BrakeDisc", disc, ApexMaterials.Get("BrakeDisc"), new Vector3(discX, 0f, 0f));
                lodRenderers[0].Add(discR);
                vis.brakeDiscs[i] = discR;
                var upright = Cached($"upright_{wk}_{left}_{front}", () => Make(mb => CarGeometry.Upright(mb, r, width, left, front, 0), "Upright"));
                var ur = AddMesh(pivot, "Upright", upright, ApexMaterials.Get(CarGeometry.Carbon));
                lodRenderers[0].Add(ur);
                lodRenderers[1].Add(ur);
                // Suspension links (LOD0 only): inner end on the chassis, outer end on the upright.
                var linkRoot = new GameObject("Links" + label).transform;
                linkRoot.SetParent(visual, false);
                foreach (var (inner, outer, radius) in CarGeometry.Links(setup, i))
                {
                    var lr = AddMesh(linkRoot, "Link", linkMesh, ApexMaterials.Get(CarGeometry.Carbon));
                    lodRenderers[0].Add(lr);
                    links.Add(new CarVisuals.Link { transform = lr.transform, inner = inner, outerSpace = pivot, outer = outer, radius = radius });
                }
            }
            vis.links = links.ToArray();
            vis.compoundBands = bands.ToArray();
            vis.compoundBandWheel = bandWheel.ToArray();

            BuildCockpit(visual, vis, setup, paint, accent, lodRenderers);

            // Rain light on the rear crash structure.
            var rain = Cached("rainlight", () => Make(mb => mb.Box(Vector3.zero, new Vector3(0.08f, 0.05f, 0.02f)), "RainLight"));
            vis.rainLight = AddMesh(visual, "RainLight", rain, ApexMaterials.Get("RainLight"),
                new Vector3(0f, -setup.cogHeight + 0.33f, setup.RearAxleZ - 0.76f));
            lodRenderers[0].Add(vis.rainLight);
            lodRenderers[1].Add(vis.rainLight);

            var lodGroup = visual.gameObject.AddComponent<LODGroup>();
            lodGroup.SetLODs(new[]
            {
                new LOD(0.22f, lodRenderers[0].ToArray()),
                new LOD(0.07f, lodRenderers[1].ToArray()),
                new LOD(0.012f, lodRenderers[2].ToArray()),
            });
            lodGroup.RecalculateBounds();
        }

        static readonly string[] BodyMaterials =
        {
            CarGeometry.Paint, CarGeometry.Accent, CarGeometry.Carbon, CarGeometry.CarbonSatin, CarGeometry.Titanium,
            CarGeometry.Mirror, CarGeometry.Interior, CarGeometry.Belt, CarGeometry.Exhaust,
        };

        static void BuildCockpit(Transform visual, CarVisuals vis, VehicleSetup setup, Material paint, Material accent, List<Renderer>[] lods)
        {
            float g = -setup.cogHeight;
            // Steering wheel: tilted so the face looks up at the driver; rotates about its column (local z).
            var wheel = new GameObject("SteeringWheel").transform;
            wheel.SetParent(visual, false);
            wheel.localPosition = CarGeometry.SteeringWheelPosition(setup);
            vis.steeringWheel = wheel;
            vis.steeringWheelBase = Quaternion.Euler(20f, 0f, 0f);
            wheel.localRotation = vis.steeringWheelBase;
            var carbonMb = new MeshBuilder();
            var gripMb = new MeshBuilder();
            var dispMb = new MeshBuilder();
            CarGeometry.SteeringWheel(carbonMb, gripMb, dispMb);
            lods[0].Add(AddMesh(wheel, "Body", Cached("sw_body", () => carbonMb.ToMesh("SteeringWheel")), ApexMaterials.Get(CarGeometry.Carbon)));
            lods[0].Add(AddMesh(wheel, "Grips", Cached("sw_grips", () => gripMb.ToMesh("Grips")), ApexMaterials.Get(CarGeometry.Interior)));
            lods[0].Add(AddMesh(wheel, "Display", Cached("sw_display", () => dispMb.ToMesh("Display")), ApexMaterials.Get(CarGeometry.Display)));
            // Shift lights above the display.
            var ledMesh = Cached("led", () => Make(mb => mb.Box(Vector3.zero, new Vector3(0.008f, 0.006f, 0.003f)), "Led"));
            var leds = new List<Renderer>();
            for (int i = 0; i < 11; i++)
            {
                var led = AddMesh(wheel, "Led" + i, ledMesh, ApexMaterials.Get(CarGeometry.Display), new Vector3(-0.05f + i * 0.01f, 0.055f, -0.024f));
                leds.Add(led);
                lods[0].Add(led);
            }
            vis.shiftLights = leds.ToArray();
            vis.gearText = DisplayText(wheel, "Gear", new Vector3(0f, 0.008f, -0.0235f), 0.0045f, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.2f));
            vis.speedText = DisplayText(wheel, "Speed", new Vector3(-0.045f, -0.018f, -0.0235f), 0.0018f, TextAnchor.MiddleLeft, Color.white);

            // Gloves on the grips (children of the wheel, so they turn with it).
            for (int s = 0; s < 2; s++)
            {
                float side = s == 0 ? -1f : 1f;
                var gloveMesh = Cached("glove" + s, () => Make(mb => CarGeometry.GloveMesh(mb, side), "Glove"));
                var glove = AddMesh(wheel, s == 0 ? "GloveL" : "GloveR", gloveMesh, ApexMaterials.Get(CarGeometry.Glove),
                    new Vector3(side * 0.14f, 0f, -0.01f));
                lods[0].Add(glove);
                vis.gloves[s] = glove.transform;
                var armMesh = Cached("unit_tube", () => Make(mb => mb.Tube(Vector3.zero, Vector3.forward, 1f, 6), "UnitTube"));
                var upper = AddMesh(visual, "UpperArm" + s, armMesh, accent);
                var fore = AddMesh(visual, "ForeArm" + s, armMesh, accent);
                lods[0].Add(upper);
                lods[0].Add(fore);
                vis.upperArms[s] = upper.transform;
                vis.foreArms[s] = fore.transform;
                vis.shoulders[s] = new Vector3(side * 0.19f, g + 0.6f, setup.FrontAxleZ - 2.12f);
            }

            // Helmet (hidden for the onboard camera, which sits inside it).
            var helmet = new GameObject("Helmet").transform;
            helmet.SetParent(visual, false);
            helmet.localPosition = new Vector3(0f, g + 0.76f, setup.FrontAxleZ - 2.05f);
            var shellMb = new MeshBuilder();
            var visorMb = new MeshBuilder();
            CarGeometry.HelmetMesh(shellMb, visorMb);
            var shell = AddMesh(helmet, "Shell", Cached("helmet_shell", () => shellMb.ToMesh("Helmet")), ApexMaterials.Get(CarGeometry.Helmet));
            var visor = AddMesh(helmet, "Visor", Cached("helmet_visor", () => visorMb.ToMesh("Visor")), ApexMaterials.Get(CarGeometry.Visor));
            foreach (var l in new[] { 0, 1, 2 }) { lods[l].Add(shell); }
            lods[0].Add(visor);
            lods[1].Add(visor);
            vis.helmet = helmet;
            vis.helmetRenderers = new Renderer[] { shell, visor };
        }

        static TextMesh DisplayText(Transform parent, string name, Vector3 pos, float size, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var tm = go.AddComponent<TextMesh>();
            var font = CircuitBuilder.BuiltinFont();
            if (font != null)
            {
                tm.font = font;
                go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            }
            tm.fontSize = 64;
            tm.characterSize = size;
            tm.anchor = anchor;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            tm.text = "";
            return tm;
        }
    }
}
