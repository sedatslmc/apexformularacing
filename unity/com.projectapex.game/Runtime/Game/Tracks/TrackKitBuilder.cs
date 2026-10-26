using System.Collections.Generic;
using Apex.Tracks;
using Apex.Vehicle;
using UnityEngine;
using UnityEngine.Rendering;

namespace Apex.Game
{
    /// <summary>
    /// Builds the modular circuit kit from <see cref="TrackKitLayout"/>: per ~60 m of lap one object
    /// with a cross-fading LODGroup (three detail levels merged per material, so a chunk is a few draw
    /// calls), plus the impact-protection collider (simple boxes per module). Barrier collision stays
    /// on the road surface's walls. An authored prefab at Resources/TrackKit/&lt;Module&gt; (e.g.
    /// "BarrierBlock"), modelled at the module's nominal length in the kit frame (+z along, +x away
    /// from the track), replaces the procedural mesh; a LODGroup on it supplies the detail levels.
    /// </summary>
    public static class TrackKitBuilder
    {
        public const float ChunkLength = 60f;
        public const string ResourceFolder = "TrackKit";
        /// <summary>Distance (m, from the chunk centre) up to which each LOD shows; beyond the last the chunk is culled.</summary>
        public static readonly float[] LodDistances = { 90f, 260f, 900f };
        /// <summary>Reference size for the LODGroup thresholds (see <see cref="LodHeight"/>).</summary>
        const float LodSize = 10f;

        sealed class Authored
        {
            public readonly List<(Mesh mesh, int sub, Material mat, Matrix4x4 local)>[] lods = new List<(Mesh, int, Material, Matrix4x4)>[3];
        }

        /// <summary>LODGroup transition height for a distance, with a 60° vertical field of view (QualitySettings.lodBias scales it).</summary>
        public static float LodHeight(float distance) => LodSize / (2f * distance * Mathf.Tan(30f * Mathf.Deg2Rad));

        public static void Build(CircuitInstance ci, Transform parent, TrackKitLayout kit, int quality)
        {
            var g = ci.Geometry;
            var root = new GameObject("CircuitKit").transform;
            root.SetParent(parent, false);
            int chunks = Mathf.Max(1, Mathf.CeilToInt(g.Length / ChunkLength));
            var byChunk = new List<TrackKitLayout.Placement>[chunks];
            foreach (var p in kit.placements)
            {
                int c = Mathf.Min(chunks - 1, (int)(g.Wrap(p.distance) / ChunkLength));
                (byChunk[c] ??= new List<TrackKitLayout.Placement>()).Add(p);
            }
            var procedural = new Dictionary<(TrackKitLayout.Module, int, int), Dictionary<string, MeshBuilder>>();
            var authored = new Dictionary<TrackKitLayout.Module, Authored>();
            foreach (TrackKitLayout.Module m in System.Enum.GetValues(typeof(TrackKitLayout.Module)))
            {
                var a = LoadAuthored(m);
                if (a != null) authored[m] = a;
            }
            Dictionary<string, MeshBuilder> Module(TrackKitLayout.Module m, int lod, int variant)
            {
                var key = (m, lod, variant);
                if (procedural.TryGetValue(key, out var parts)) return parts;
                parts = new Dictionary<string, MeshBuilder>();
                var local = parts;
                TrackKitGeometry.Module(m, k => { if (!local.TryGetValue(k, out var mb)) local[k] = mb = new MeshBuilder(); return mb; }, lod, variant);
                procedural[key] = parts;
                return parts;
            }

            for (int c = 0; c < chunks; c++)
            {
                var list = byChunk[c];
                if (list == null) continue;
                var centre = Vector3.zero;
                foreach (var p in list) centre += new Vector3(p.position.X, p.position.Y, p.position.Z);
                centre /= list.Count;
                var go = new GameObject($"Kit_{c:000}");
                go.transform.SetParent(root, false);
                go.transform.localPosition = centre;
                go.isStatic = true;
                var lods = new LOD[3];
                for (int lod = 0; lod < 3; lod++)
                {
                    // Low environment detail drops the near level (the mid level is used up close).
                    int geo = quality <= 0 ? Mathf.Max(1, lod) : lod;
                    var parts = new Dictionary<string, MeshBuilder>();
                    var combine = new Dictionary<Material, List<CombineInstance>>();
                    foreach (var p in list)
                    {
                        var f = TrackKitGeometry.Transform(p);
                        if (authored.TryGetValue(p.module, out var au))
                        {
                            var world = PlacementMatrix(p, centre);
                            foreach (var (mesh, sub, mat, local) in au.lods[Mathf.Min(geo, au.lods.Length - 1)] ?? au.lods[0])
                            {
                                if (!combine.TryGetValue(mat, out var ci2)) combine[mat] = ci2 = new List<CombineInstance>();
                                ci2.Add(new CombineInstance { mesh = mesh, subMeshIndex = sub, transform = world * local });
                            }
                            continue;
                        }
                        foreach (var kv in Module(p.module, geo, p.variant))
                        {
                            if (!parts.TryGetValue(kv.Key, out var mb)) parts[kv.Key] = mb = new MeshBuilder();
                            mb.Append(kv.Value, v => f(v) - centre);
                        }
                    }
                    var renderers = new List<Renderer>();
                    if (parts.Count > 0)
                    {
                        var lodGo = EnvironmentBuilder.AddMeshObject(go.transform, "LOD" + lod, parts, lod < 2);
                        renderers.Add(lodGo.GetComponent<MeshRenderer>());
                    }
                    if (combine.Count > 0) renderers.Add(CombineAuthored(go.transform, "Authored_LOD" + lod, combine, lod < 2));
                    lods[lod] = new LOD(LodHeight(LodDistances[lod]), renderers.ToArray());
                }
                var group = go.AddComponent<LODGroup>();
                group.fadeMode = LODFadeMode.CrossFade;
                group.animateCrossFading = true;
                group.localReferencePoint = Vector3.zero;
                group.SetLODs(lods);
                group.size = LodSize;
            }
            BuildProtectionCollider(root, kit);
        }

        /// <summary>Kit frame of a placement as a matrix, relative to the chunk origin.</summary>
        static Matrix4x4 PlacementMatrix(TrackKitLayout.Placement p, Vector3 origin)
        {
            var f = TrackKitGeometry.Transform(p);
            var o = f(Vector3.zero) - origin;
            var x = f(Vector3.right) - f(Vector3.zero);
            var y = f(Vector3.up) - f(Vector3.zero);
            var z = f(Vector3.forward) - f(Vector3.zero);
            return new Matrix4x4(new Vector4(x.x, x.y, x.z, 0f), new Vector4(y.x, y.y, y.z, 0f), new Vector4(z.x, z.y, z.z, 0f), new Vector4(o.x, o.y, o.z, 1f));
        }

        static Authored LoadAuthored(TrackKitLayout.Module m)
        {
            var prefab = Resources.Load<GameObject>(ResourceFolder + "/" + m);
            if (prefab == null) return null;
            var a = new Authored();
            var toRoot = prefab.transform.worldToLocalMatrix;
            List<(Mesh, int, Material, Matrix4x4)> Collect(IEnumerable<Renderer> renderers)
            {
                var list = new List<(Mesh, int, Material, Matrix4x4)>();
                foreach (var r in renderers)
                {
                    var mf = r != null ? r.GetComponent<MeshFilter>() : null;
                    if (mf == null || mf.sharedMesh == null) continue;
                    var mats = r.sharedMaterials;
                    for (int s = 0; s < mf.sharedMesh.subMeshCount; s++)
                        list.Add((mf.sharedMesh, s, mats[Mathf.Min(s, mats.Length - 1)], toRoot * r.transform.localToWorldMatrix));
                }
                return list;
            }
            var group = prefab.GetComponent<LODGroup>();
            if (group != null)
            {
                var src = group.GetLODs();
                for (int k = 0; k < 3; k++) a.lods[k] = Collect(src[Mathf.Min(k, src.Length - 1)].renderers);
            }
            else
            {
                var all = Collect(prefab.GetComponentsInChildren<Renderer>());
                for (int k = 0; k < 3; k++) a.lods[k] = all;
            }
            return a;
        }

        static Renderer CombineAuthored(Transform parent, string name, Dictionary<Material, List<CombineInstance>> byMaterial, bool shadows)
        {
            var perMaterial = new List<CombineInstance>();
            var mats = new List<Material>();
            foreach (var kv in byMaterial)
            {
                var m = new Mesh { name = name + "_" + kv.Key.name, indexFormat = IndexFormat.UInt32 };
                m.CombineMeshes(kv.Value.ToArray(), true, true);
                perMaterial.Add(new CombineInstance { mesh = m, transform = Matrix4x4.identity });
                mats.Add(kv.Key);
            }
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(perMaterial.ToArray(), false, false);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats.ToArray();
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            go.isStatic = true;
            return mr;
        }

        /// <summary>One invisible collider for all impact-protection modules: a box per module (what a car hits first).</summary>
        static void BuildProtectionCollider(Transform root, TrackKitLayout kit)
        {
            var mb = new MeshBuilder();
            foreach (var p in kit.placements)
            {
                if (p.module != TrackKitLayout.Module.EnergyModule && p.module != TrackKitLayout.Module.TyreStack) continue;
                float depth = p.module == TrackKitLayout.Module.TyreStack ? TrackKitLayout.TyreStackDepth : TrackKitLayout.EnergyModuleDepth;
                var box = new MeshBuilder();
                box.Box(new Vector3(0f, 0.5f, 0f), new Vector3(depth, 1f, TrackKitGeometry.NominalLength(p.module)));
                mb.Append(box, TrackKitGeometry.Transform(p));
            }
            if (mb.TriangleCount == 0) return;
            var go = new GameObject("ImpactProtectionCollider");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshCollider>().sharedMesh = mb.ToMesh("ImpactProtectionCollider");
            go.AddComponent<TrackSurface>().surface = SurfaceType.Wall;
            go.isStatic = true;
        }
    }
}
