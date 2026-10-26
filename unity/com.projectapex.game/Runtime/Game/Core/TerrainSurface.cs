using UnityEngine;
using UnityEngine.Rendering;

namespace Apex.Game
{
    /// <summary>
    /// Ground material of the circuit surroundings (near terrain and far land): the Apex/Terrain shader
    /// under URP with the scanned grass, soil and rubble-wall sets chosen by slope (docs/TERRAIN.md), else
    /// URP/standard Lit with the grass set. Replaces the flat grey "FarGround" colour. Wetness makes it glossier.
    /// </summary>
    public static class TerrainSurface
    {
        public const string Grass = "terrain_grass", Soil = "terrain_soil", Wall = "terrain_wall";
        /// <summary>Target mean linear albedo of each layer (grass and soil ~0.12, limestone rubble ~0.22).</summary>
        public const float GrassAlbedo = 0.12f, SoilAlbedo = 0.12f, WallAlbedo = 0.22f;
        /// <summary>Metres per UV unit of the terrain meshes (RoadSurface.BuildTerrain: x/40, z/40).</summary>
        const float TerrainUvMetres = 40f;

        static Material material;
        static bool custom;
        static Vector4 smoothness;

        /// <summary>Registers the material under the ground keys the circuit and environment builders use.</summary>
        public static void Register()
        {
            if (material == null) material = Create();
            if (material == null) return;
            ApexMaterials.Register(EnvironmentGeometry.FarGround, material);
            ApexMaterials.Register("Ground", material);
        }

        static Material Create()
        {
            var shader = Resources.Load<Shader>("Shaders/ApexTerrain");
            var grass = SurfaceTextures.Load(Grass, out string gp);
            var soil = SurfaceTextures.Load(Soil, out string sp);
            var wall = SurfaceTextures.Load(Wall, out string wp);
            if (shader != null && shader.isSupported && GraphicsSettings.currentRenderPipeline != null && grass != null && soil != null && wall != null)
            {
                var m = new Material(shader) { name = "Env_Terrain" };
                m.SetTexture("_GrassMap", Resources.Load<Texture2D>(gp + "BaseColor"));
                m.SetTexture("_GrassNormal", Resources.Load<Texture2D>(gp + "Normal"));
                m.SetTexture("_SoilMap", Resources.Load<Texture2D>(sp + "BaseColor"));
                m.SetTexture("_SoilNormal", Resources.Load<Texture2D>(sp + "Normal"));
                m.SetTexture("_WallMap", Resources.Load<Texture2D>(wp + "BaseColor"));
                m.SetTexture("_WallNormal", Resources.Load<Texture2D>(wp + "Normal"));
                m.SetVector("_Tiling", new Vector4(1f / grass.metres, 1f / soil.metres, 1f / wall.metres, 1f / 37f));
                m.SetVector("_AlbedoScale", new Vector4(Scale(grass, GrassAlbedo), Scale(soil, SoilAlbedo), Scale(wall, WallAlbedo), 0f));
                smoothness = new Vector4(Smooth(grass, 0.25f), Smooth(soil, 0.12f), Smooth(wall, 0.1f), 0f);
                m.SetVector("_Smoothness", smoothness);
                custom = true;
                return m;
            }
            // Fallback: Lit with the grass set (or the old ground colour when no set is imported).
            var lit = new Material(ApexMaterials.LitShader()) { name = "Env_Terrain_Lit" };
            if (SurfaceTextures.TryApply(lit, Grass, TerrainUvMetres))
            {
                var tint = SurfaceTextures.AlbedoTint(Grass, GrassAlbedo);
                if (lit.HasProperty("_BaseColor")) lit.SetColor("_BaseColor", tint);
                if (lit.HasProperty("_Color")) lit.SetColor("_Color", tint);
                return lit;
            }
            return null;
        }

        static float Scale(SurfaceTextures.Info info, float target) =>
            info.meanAlbedoLinear > 0f ? Mathf.Clamp(target / info.meanAlbedoLinear, 0.3f, 4f) : 1f;

        static float Smooth(SurfaceTextures.Info info, float fallback) => info.meanSmoothness >= 0f ? info.meanSmoothness : fallback;

        /// <summary>0 dry .. 1 soaked (from WeatherVisuals).</summary>
        public static void SetWetness(float wet)
        {
            if (!custom || material == null) return;
            material.SetVector("_Smoothness", new Vector4(smoothness.x, smoothness.y, smoothness.z, Mathf.Clamp01(wet)));
        }
    }
}
