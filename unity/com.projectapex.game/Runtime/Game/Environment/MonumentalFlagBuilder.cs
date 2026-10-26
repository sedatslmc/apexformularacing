using System.Collections.Generic;
using Apex.Environment;
using UnityEngine;
using UnityEngine.Rendering;

namespace Apex.Game
{
    /// <summary>
    /// The monumental Turkish flag (site from the circuit file, checked by <see cref="MonumentalFlagSite"/>):
    /// paved plaza, stepped octagonal stone foundation sunk into the slope, a tapered 60 m steel pole with
    /// a gilded finial, and an 18 x 12 m flag (<see cref="FlagComponent.HeroDetail"/> cloth). Pole and base
    /// have three detail levels. Procedural art with realistic proportions, not a scanned asset.
    /// </summary>
    public static class MonumentalFlagBuilder
    {
        public static void Build(Transform parent, EnvironmentInstance env, TerrainField field, float x, float z)
        {
            EventBrandingBuilder.EnsureMaterials();
            // A stone terrace (retaining wall all round, paved top just above the highest ground) carries the
            // stepped foundation, so nothing floats on the downhill side and no ground pokes through.
            MonumentalFlagGeometry.Levels(field.Height, x, z, out float baseY, out float terrace, out float deck);
            var root = new GameObject("MonumentalTurkishFlag");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(x, baseY, z);
            float poleTop = MonumentalFlagGeometry.PoleTop(deck);

            var levels = new LOD[3];
            for (int lod = 0; lod < 3; lod++)
            {
                var parts = new Dictionary<string, MeshBuilder>();
                MonumentalFlagGeometry.Build(k => { if (!parts.TryGetValue(k, out var mb)) parts[k] = mb = new MeshBuilder(); return mb; }, lod, terrace, deck);
                var go = EnvironmentBuilder.AddMeshObject(root.transform, "LOD" + lod, parts, lod < 2);
                levels[lod] = new LOD(lod == 0 ? 0.25f : lod == 1 ? 0.06f : 0.004f, new Renderer[] { go.GetComponent<MeshRenderer>() });
            }
            var group = root.AddComponent<LODGroup>();
            group.fadeMode = LODFadeMode.CrossFade;
            group.animateCrossFading = true;
            group.SetLODs(levels);
            group.RecalculateBounds();

            // The flag: hoist at the pole's surface, just below the finial.
            var flagGo = new GameObject("Flag");
            flagGo.transform.SetParent(root.transform, false);
            var flag = flagGo.AddComponent<FlagComponent>();
            flag.hoistOffset = MonumentalFlagGeometry.TopRadius + 0.05f;
            flag.Setup(env, root.transform.position, MonumentalFlagSite.FlagLength, poleTop - 0.4f, FlagComponent.HeroDetail, EnvironmentMaterials.Flag);
        }
    }
}
