using System;
using System.Collections.Generic;
using Apex.Vehicle;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Apex.Game
{
    /// <summary>
    /// Builds the original placeholder open-wheel car from primitive meshes plus its
    /// physics colliders. Proportions follow the design reference (~5.4 m long,
    /// ~1.9 m wide); it will be replaced by an authored model without touching
    /// physics. Coordinates are relative to the centre of mass; ground is at
    /// y = -cogHeight; forward is +z.
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

        /// <summary>Builds a car with a livery; body parts are merged per material for cheap rendering.</summary>
        public static VehicleController Build(string name, VehicleSetup setup, Color primary, Color accent)
        {
            var vc = Build(name, setup);
            var visual = vc.transform.Find("Visual");
            var paint = Paint(primary);
            var acc = Paint(accent, "CarAccent");
            foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>())
            {
                var shared = mr.sharedMaterial;
                if (shared == ApexMaterials.Get("CarPaint")) mr.sharedMaterial = paint;
                else if (shared == ApexMaterials.Get("CarAccent")) mr.sharedMaterial = acc;
            }
            CombineBody(visual, vc.WheelVisuals);
            return vc;
        }

        /// <summary>Merges the static body parts into one mesh per material (wheels stay separate).</summary>
        public static void CombineBody(Transform visual, Transform[] wheels)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var toDestroy = new List<GameObject>();
            var toLocal = visual.worldToLocalMatrix;
            foreach (Transform child in visual)
            {
                if (Array.IndexOf(wheels, child) >= 0) continue;
                var mf = child.GetComponent<MeshFilter>();
                var mr = child.GetComponent<MeshRenderer>();
                if (mf == null || mr == null) continue;
                if (!groups.TryGetValue(mr.sharedMaterial, out var list)) groups[mr.sharedMaterial] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mf.sharedMesh, transform = toLocal * child.localToWorldMatrix });
                toDestroy.Add(child.gameObject);
            }
            foreach (var kv in groups)
            {
                var mesh = new Mesh { name = "Body_" + kv.Key.name };
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                var go = new GameObject("Body_" + kv.Key.name);
                go.transform.SetParent(visual, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = kv.Key;
            }
            foreach (var go in toDestroy)
            {
                if (Application.isPlaying) Object.Destroy(go);
                else Object.DestroyImmediate(go);
            }
        }

        public static VehicleController Build(string name, VehicleSetup setup, string paintKey = "CarPaint")
        {
            var root = new GameObject(name);
            // Inactive while assembling so VehicleController.Awake sees the final setup.
            root.SetActive(false);
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = setup.mass;
            var mat = RigidbodyCompat.MakeMaterial("CarBody", 0.3f, 0.1f);
            float g = -setup.cogHeight;
            var body = root.AddComponent<BoxCollider>();
            body.center = new Vector3(0f, g + 0.29f, 0f);
            body.size = new Vector3(1.0f, 0.5f, 5.2f);
            body.sharedMaterial = mat;
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
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            BuildBody(visual, g, paintKey);
            for (int i = 0; i < 4; i++) vc.WheelVisuals[i] = BuildWheel(visual, i, setup);
            root.SetActive(true);
            return vc;
        }

        static Transform Part(Transform parent, PrimitiveType type, string mat, Vector3 pos, Vector3 scale, Vector3 euler = default)
        {
            var go = new GameObject(type.ToString());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = PrimitiveMesh(type);
            go.AddComponent<MeshRenderer>().sharedMaterial = ApexMaterials.Get(mat);
            return go.transform;
        }

        static void BuildBody(Transform v, float g, string paint)
        {
            const PrimitiveType Box = PrimitiveType.Cube;
            const PrimitiveType Cyl = PrimitiveType.Cylinder;
            // Floor with venturi edges and plank.
            Part(v, Box, "Carbon", new Vector3(0, g + 0.06f, -0.25f), new Vector3(1.45f, 0.04f, 3.3f));
            // Monocoque and engine cover.
            Part(v, Box, paint, new Vector3(0, g + 0.3f, -0.1f), new Vector3(0.62f, 0.42f, 2.6f));
            Part(v, Box, paint, new Vector3(0, g + 0.36f, -1.6f), new Vector3(0.5f, 0.3f, 1.2f));
            // Tapered nose (cylinder lying along z; Unity cylinder is 2 units tall).
            Part(v, Cyl, paint, new Vector3(0, g + 0.28f, 2.05f), new Vector3(0.36f, 0.95f, 0.26f), new Vector3(90f, 0f, 0f));
            // Front wing: main plane, flap, end plates.
            Part(v, Box, "Carbon", new Vector3(0, g + 0.1f, 2.95f), new Vector3(1.95f, 0.03f, 0.42f));
            Part(v, Box, "CarAccent", new Vector3(0, g + 0.17f, 2.82f), new Vector3(1.85f, 0.03f, 0.22f), new Vector3(-20f, 0f, 0f));
            foreach (float side in new[] { -1f, 1f })
                Part(v, Box, "Carbon", new Vector3(side * 0.98f, g + 0.16f, 2.9f), new Vector3(0.025f, 0.24f, 0.6f));
            // Sidepods with undercut and accent stripe.
            foreach (float side in new[] { -1f, 1f })
            {
                Part(v, Box, paint, new Vector3(side * 0.5f, g + 0.27f, -0.15f), new Vector3(0.42f, 0.34f, 1.5f));
                Part(v, Box, paint, new Vector3(side * 0.5f, g + 0.2f, 0.75f), new Vector3(0.4f, 0.2f, 0.45f), new Vector3(-18f, 0f, 0f));
                Part(v, Box, "CarAccent", new Vector3(side * 0.5f, g + 0.41f, -0.15f), new Vector3(0.43f, 0.03f, 1.5f));
            }
            // Cockpit, airbox, halo, helmet.
            Part(v, Box, "Carbon", new Vector3(0, g + 0.52f, 0.55f), new Vector3(0.44f, 0.06f, 0.7f));
            Part(v, Box, paint, new Vector3(0, g + 0.72f, -0.1f), new Vector3(0.36f, 0.42f, 0.5f));
            Part(v, Box, "Carbon", new Vector3(0, g + 0.85f, 0.17f), new Vector3(0.22f, 0.18f, 0.12f));
            Part(v, PrimitiveType.Sphere, "Helmet", new Vector3(0, g + 0.68f, 0.42f), new Vector3(0.28f, 0.28f, 0.28f));
            Part(v, Box, "Carbon", new Vector3(0, g + 0.82f, 0.6f), new Vector3(0.05f, 0.05f, 0.75f), new Vector3(-10f, 0f, 0f));
            foreach (float side in new[] { -1f, 1f })
                Part(v, Box, "Carbon", new Vector3(side * 0.24f, g + 0.7f, 0.5f), new Vector3(0.04f, 0.04f, 0.6f), new Vector3(-6f, side * -7f, 0f));
            // Rear wing on a central pylon, beam wing, diffuser.
            Part(v, Box, "Carbon", new Vector3(0, g + 0.65f, -2.35f), new Vector3(0.05f, 0.6f, 0.15f));
            Part(v, Box, "Carbon", new Vector3(0, g + 0.95f, -2.4f), new Vector3(1.0f, 0.03f, 0.38f), new Vector3(7f, 0f, 0f));
            Part(v, Box, "CarAccent", new Vector3(0, g + 1.04f, -2.52f), new Vector3(1.0f, 0.03f, 0.22f), new Vector3(26f, 0f, 0f));
            foreach (float side in new[] { -1f, 1f })
                Part(v, Box, paint, new Vector3(side * 0.51f, g + 0.8f, -2.45f), new Vector3(0.025f, 0.55f, 0.7f));
            Part(v, Box, "Carbon", new Vector3(0, g + 0.4f, -2.45f), new Vector3(0.9f, 0.03f, 0.2f));
            Part(v, Box, "Carbon", new Vector3(0, g + 0.15f, -2.15f), new Vector3(1.0f, 0.18f, 0.4f), new Vector3(23f, 0f, 0f));
        }

        static Transform BuildWheel(Transform v, int index, VehicleSetup setup)
        {
            bool front = index < 2;
            string label = (front ? "F" : "R") + (index % 2 == 0 ? "L" : "R");
            var pivot = new GameObject("Wheel" + label).transform;
            pivot.SetParent(v, false);
            var mount = setup.MountPosition(index, out float sc);
            float r = front ? setup.wheelRadiusFront : setup.wheelRadiusRear;
            float width = front ? 0.3f : 0.4f;
            pivot.localPosition = new Vector3(mount.X, r - setup.cogHeight, mount.Z);
            // Cylinder axis along local x: rotate 90° about z. Cylinder height is 2 units.
            Part(pivot, PrimitiveType.Cylinder, "Tire", Vector3.zero, new Vector3(r * 2f, width * 0.5f, r * 2f), new Vector3(0f, 0f, 90f));
            Part(pivot, PrimitiveType.Cylinder, "Rim", Vector3.zero, new Vector3(r * 1.25f, width * 0.5f + 0.005f, r * 1.25f), new Vector3(0f, 0f, 90f));
            // Spoke marker so wheel rotation is visible.
            Part(pivot, PrimitiveType.Cube, "CarAccent", Vector3.zero, new Vector3(width + 0.02f, r * 1.1f, 0.05f));
            return pivot;
        }
    }
}
