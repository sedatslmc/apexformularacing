using UnityEditor;
using UnityEngine;

namespace Apex.EditorTools
{
    /// <summary>
    /// Import settings for the scanned CC0 sets in Resources/Textures/CC0 and CC0_Ultra (tools/textures):
    /// normal maps as normal maps, data maps linear, trilinear filtering with anisotropy for road
    /// surfaces seen at grazing angles; mobile at most 2048 px ASTC 6x6, standalone at most 4096 px
    /// (the source size caps it). Quality of the compression is checked in the editor, not here.
    /// </summary>
    public sealed class CC0TextureImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            // Committed 1K sets (Textures/CC0) and fetched 4K sets (Textures/CC0_Ultra).
            if (!path.Contains("/Resources/Textures/CC0/") && !path.Contains("/Resources/Textures/CC0_Ultra/")) return;
            var ti = (TextureImporter)assetImporter;
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            ti.mipmapEnabled = true;
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.filterMode = FilterMode.Trilinear;
            ti.anisoLevel = 8;
            switch (file)
            {
                case "Normal":
                    ti.textureType = TextureImporterType.NormalMap;
                    break;
                case "MetallicSmoothness":
                case "Occlusion":
                    ti.textureType = TextureImporterType.Default;
                    ti.sRGBTexture = false;
                    ti.alphaSource = TextureImporterAlphaSource.FromInput;
                    break;
                default:   // BaseColor, PaintBase
                    ti.textureType = TextureImporterType.Default;
                    ti.sRGBTexture = true;
                    break;
            }
            foreach (var platform in new[] { "Android", "iPhone" })
                ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = platform,
                    overridden = true,
                    maxTextureSize = 2048,
                    format = TextureImporterFormat.ASTC_6x6,
                });
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Standalone",
                overridden = true,
                maxTextureSize = 4096,
                format = file == "Normal" ? TextureImporterFormat.BC5 : TextureImporterFormat.BC7,
            });
        }
    }
}
