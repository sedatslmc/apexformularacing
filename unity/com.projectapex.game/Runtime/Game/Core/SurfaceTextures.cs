using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// Scanned CC0 texture sets (Poly Haven, ambientCG) imported with tools/textures/import_cc0.py into
    /// Resources/Textures/CC0/&lt;set&gt;/ (BaseColor, Normal, MetallicSmoothness, Occlusion, optional
    /// PaintBase, source.json). <see cref="TryApply"/> puts a set on a URP Lit material, tiled to the
    /// set's real-world size; without the files the caller keeps its procedural fallback.
    /// </summary>
    public static class SurfaceTextures
    {
        public const string Folder = "Textures/CC0";
        /// <summary>Set names the circuit uses (the importer's --name).</summary>
        public const string Asphalt = "asphalt", Runoff = "runoff", Kerb = "kerb";
        /// <summary>Metres per UV unit of the road, kerb and run-off meshes.</summary>
        public const float RoadUvMetres = Apex.Tracks.RoadSurface.UvMetres;

        [System.Serializable]
        public sealed class Info
        {
            public string name;
            public float metres = 1f;
            /// <summary>Mean smoothness of the map (-1 = no map), used for wet roads.</summary>
            public float meanSmoothness = -1f;
            /// <summary>Mean linear albedo of the base colour (0 = unknown), for <see cref="AlbedoTint"/>.</summary>
            public float meanAlbedoLinear;
            public string source, author, licence;
        }

        public static Info Load(string set)
        {
            var json = Resources.Load<TextAsset>(Folder + "/" + set + "/source");
            if (json == null) return null;
            var info = JsonUtility.FromJson<Info>(json.text);
            return info != null && info.metres > 0f ? info : null;
        }

        /// <summary>
        /// Applies set `set` to a URP Lit material: base map (or the paint base when `paint` is true, tinted
        /// by the material colour), normal map, metallic/smoothness map with smoothness taken from its alpha,
        /// occlusion; tiling = uvMetres / the set's metres. Returns false (material unchanged) if the set is
        /// not imported.
        /// </summary>
        public static bool TryApply(Material m, string set, float uvMetres, bool paint = false)
        {
            var info = Load(set);
            if (info == null || m == null) return false;
            string path = Folder + "/" + set + "/";
            var baseMap = Resources.Load<Texture2D>(path + (paint ? "PaintBase" : "BaseColor")) ?? Resources.Load<Texture2D>(path + "BaseColor");
            if (baseMap == null) return false;
            var tiling = Vector2.one * (uvMetres / info.metres);
            SetTexture(m, "_BaseMap", baseMap, tiling);
            SetTexture(m, "_MainTex", baseMap, tiling);
            var normal = Resources.Load<Texture2D>(path + "Normal");
            if (normal != null && m.HasProperty("_BumpMap"))
            {
                m.SetTexture("_BumpMap", normal);
                m.EnableKeyword("_NORMALMAP");
                if (m.HasProperty("_BumpScale")) m.SetFloat("_BumpScale", 1f);
            }
            var gloss = Resources.Load<Texture2D>(path + "MetallicSmoothness");
            if (gloss != null && m.HasProperty("_MetallicGlossMap"))
            {
                m.SetTexture("_MetallicGlossMap", gloss);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                // Smoothness from the metallic map's alpha; _Smoothness then scales it (1 = as scanned).
                if (m.HasProperty("_SmoothnessTextureChannel")) m.SetFloat("_SmoothnessTextureChannel", 0f);
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 1f);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            }
            var ao = Resources.Load<Texture2D>(path + "Occlusion");
            if (ao != null && m.HasProperty("_OcclusionMap"))
            {
                m.SetTexture("_OcclusionMap", ao);
                m.EnableKeyword("_OCCLUSIONMAP");
                if (m.HasProperty("_OcclusionStrength")) m.SetFloat("_OcclusionStrength", 1f);
            }
            return true;
        }

        /// <summary>
        /// Material colour that brings a set's mean linear albedo to `target` (e.g. 0.07 for aged city asphalt;
        /// scans of fresh asphalt are often 0.015). The factor is linear (0.5 .. 6); Color.gamma keeps it linear
        /// after Unity's sRGB-to-linear conversion of material colours in a linear-space project.
        /// </summary>
        public static Color AlbedoTint(string set, float target)
        {
            var info = Load(set);
            if (info == null || info.meanAlbedoLinear <= 0f) return Color.white;
            float f = Mathf.Clamp(target / info.meanAlbedoLinear, 0.5f, 6f);
            return new Color(f, f, f, 1f).gamma;
        }

        /// <summary>Mean linear albedo targets (physically plausible ranges: fresh asphalt 0.04-0.05, aged 0.08-0.12).</summary>
        public const float AsphaltAlbedo = 0.07f, RunoffAlbedo = 0.09f;

        /// <summary>True when a material takes its smoothness from a map (wet-road handling, see WeatherVisuals).</summary>
        public static bool HasGlossMap(Material m) => m != null && m.IsKeywordEnabled("_METALLICSPECGLOSSMAP");

        static void SetTexture(Material m, string prop, Texture2D tex, Vector2 tiling)
        {
            if (!m.HasProperty(prop)) return;
            m.SetTexture(prop, tex);
            m.SetTextureScale(prop, tiling);
        }
    }
}
