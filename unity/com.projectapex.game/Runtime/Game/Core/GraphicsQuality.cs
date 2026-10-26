using Apex.Core;
using UnityEngine;
using UnityEngine.Rendering;
using GraphicsTier = Apex.Core.GraphicsTier;
#if APEX_URP
using UnityEngine.Rendering.Universal;
#endif

namespace Apex.Game
{
    /// <summary>
    /// Applies a <see cref="GraphicsPreset"/> live: QualitySettings (LOD bias, anisotropy and, for the
    /// built-in pipeline, shadows and MSAA), and with URP a runtime copy of the active pipeline asset
    /// (render scale, MSAA, shadow distance/cascades/resolution, per-object lights), the camera's
    /// post-process anti-aliasing, the sun's soft-shadow quality and the SSAO renderer feature.
    /// <para>The pipeline asset is copied so play mode never edits the project's asset. The SSAO feature
    /// and QualitySettings belong to the project, so their previous values are restored on quit.</para>
    /// SSAO needs the feature on the renderer (Universal 3D template on PC, or "Project Apex → 4. Grafik
    /// einrichten"); without it the tier runs without SSAO and <see cref="SsaoAvailable"/> is false.
    /// </summary>
    public static class GraphicsQuality
    {
        public static GraphicsPreset Current { get; private set; } = GraphicsPreset.For(GraphicsTier.Medium);
        public static bool SsaoAvailable { get; private set; }

        /// <summary>Settings value (-1 = auto) to a tier for this device.</summary>
        public static GraphicsTier Resolve(int setting) =>
            setting < 0 ? GraphicsPreset.Auto(Application.isMobilePlatform, SystemInfo.graphicsMemorySize) : (GraphicsTier)Mathf.Clamp(setting, 0, GraphicsPreset.TierCount - 1);

        struct Snapshot
        {
            public float lodBias, shadowDistance;
            public AnisotropicFiltering aniso;
            public int pixelLights, cascades, msaa;
            public ShadowResolution shadowRes;
            public RenderPipelineAsset pipeline;
        }
        static Snapshot? before;
#if APEX_URP
        static UniversalRenderPipelineAsset runtimeAsset;
        static ScreenSpaceAmbientOcclusion ssao;
        static bool ssaoBefore;
#endif

        public static void Apply(GraphicsPreset p, Camera cam, Light sun)
        {
            Current = p;
            if (before == null)
            {
                before = new Snapshot
                {
                    lodBias = QualitySettings.lodBias, shadowDistance = QualitySettings.shadowDistance, aniso = QualitySettings.anisotropicFiltering,
                    pixelLights = QualitySettings.pixelLightCount, cascades = QualitySettings.shadowCascades, msaa = QualitySettings.antiAliasing,
                    shadowRes = QualitySettings.shadowResolution, pipeline = QualitySettings.renderPipeline,
                };
                Application.quitting += Restore;
            }
            QualitySettings.lodBias = p.lodBias;
            QualitySettings.anisotropicFiltering = p.forceAnisotropic ? AnisotropicFiltering.ForceEnable : AnisotropicFiltering.Enable;
            // Built-in pipeline values (URP reads its own asset below).
            QualitySettings.pixelLightCount = p.maxAdditionalLights;
            QualitySettings.shadowDistance = p.shadowDistance;
            QualitySettings.shadowCascades = p.shadowCascades >= 3 ? 4 : p.shadowCascades == 2 ? 2 : 1;
            QualitySettings.shadowCascade2Split = p.split1;
            QualitySettings.shadowCascade4Split = new Vector3(p.split1, p.split2, p.split3);
            QualitySettings.shadowResolution = p.shadowResolution >= 4096 ? ShadowResolution.VeryHigh : p.shadowResolution >= 2048 ? ShadowResolution.High : ShadowResolution.Medium;
            if (GraphicsSettings.currentRenderPipeline == null) QualitySettings.antiAliasing = p.msaa > 1 ? p.msaa : 0;
            if (sun != null && sun.shadows != LightShadows.None) sun.shadows = LightShadows.Soft;
#if APEX_URP
            var asset = RuntimeAsset();
            if (asset != null)
            {
                asset.renderScale = p.renderScale;
                asset.msaaSampleCount = p.msaa;
                asset.shadowDistance = p.shadowDistance;
                asset.shadowCascadeCount = p.shadowCascades;
                asset.cascade2Split = p.split1;
                asset.cascade3Split = new Vector2(p.split1, p.split2);
                asset.cascade4Split = new Vector3(p.split1, p.split2, p.split3);
                asset.mainLightShadowmapResolution = p.shadowResolution;
                asset.maxAdditionalLightsCount = p.maxAdditionalLights;
                FindSsao(asset);
                if (ssao != null) ssao.SetActive(p.ssao);
            }
            if (cam != null)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null)
                {
                    // URP runs FXAA/SMAA/TAA inside the post-processing pass, so it stays on; the colour grade is a separate volume.
                    data.renderPostProcessing = true;
                    data.antialiasing = p.antialiasing == PostAntialiasing.Fxaa ? AntialiasingMode.FastApproximateAntialiasing
                        : p.antialiasing == PostAntialiasing.Smaa ? AntialiasingMode.SubpixelMorphologicalAntiAliasing
                        : p.antialiasing == PostAntialiasing.Taa ? AntialiasingMode.TemporalAntiAliasing : AntialiasingMode.None;
                    data.antialiasingQuality = (AntialiasingQuality)Mathf.Clamp(p.antialiasingQuality, 0, 2);
                }
            }
            if (sun != null)
            {
                var light = sun.GetUniversalAdditionalLightData();
                if (light != null) light.softShadowQuality = (SoftShadowQuality)(Mathf.Clamp(p.softShadowQuality, 0, 2) + 1);
            }
#endif
        }

#if APEX_URP
        static UniversalRenderPipelineAsset RuntimeAsset()
        {
            var current = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (current == null) return null;
            if (current == runtimeAsset) return runtimeAsset;
            if (runtimeAsset != null) Object.Destroy(runtimeAsset);
            runtimeAsset = Object.Instantiate(current);
            runtimeAsset.name = current.name + " (Apex runtime)";
            QualitySettings.renderPipeline = runtimeAsset;
            ssao = null;
            return runtimeAsset;
        }

        static void FindSsao(UniversalRenderPipelineAsset asset)
        {
            if (ssao != null) return;
            foreach (var data in asset.rendererDataList)
            {
                if (data == null || !data.TryGetRendererFeature(out ScreenSpaceAmbientOcclusion f)) continue;
                ssao = f;
                ssaoBefore = f.isActive;
                break;
            }
            SsaoAvailable = ssao != null;
        }
#endif

        static void Restore()
        {
            if (before == null) return;
            var b = before.Value;
#if APEX_URP
            if (ssao != null) ssao.SetActive(ssaoBefore);
#endif
            QualitySettings.renderPipeline = b.pipeline;
            QualitySettings.lodBias = b.lodBias;
            QualitySettings.shadowDistance = b.shadowDistance;
            QualitySettings.anisotropicFiltering = b.aniso;
            QualitySettings.pixelLightCount = b.pixelLights;
            QualitySettings.shadowCascades = b.cascades;
            QualitySettings.antiAliasing = b.msaa;
            QualitySettings.shadowResolution = b.shadowRes;
            before = null;
        }
    }
}
