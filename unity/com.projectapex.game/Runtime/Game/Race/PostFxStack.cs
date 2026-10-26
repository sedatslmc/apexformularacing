using Apex.Environment;
using UnityEngine;
#if APEX_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace Apex.Game
{
    /// <summary>
    /// Global URP post-processing volume created at runtime: ACES tonemapping, colour
    /// adjustments, white balance, bloom and vignette from <see cref="ColorGrading"/>.
    /// Only active with the URP package (APEX_URP, see Apex.Runtime.asmdef versionDefines);
    /// with the built-in pipeline it does nothing. Bloom runs in low-quality filtering and
    /// anti-aliasing is FXAA, both chosen for mobile cost (not measured on devices yet).
    /// </summary>
    public sealed class PostFxStack : MonoBehaviour
    {
        public static bool Supported
        {
            get
            {
#if APEX_URP
                return true;
#else
                return false;
#endif
            }
        }

#if APEX_URP
        VolumeProfile profile;
        ColorAdjustments adjust;
        WhiteBalance white;
        Bloom bloom;
        Vignette vignette;
        Volume volume;
        UniversalAdditionalCameraData cameraData;
#endif
        bool active;

        /// <summary>Creates the volume and turns post-processing on for the camera (no-op without URP).</summary>
        public void Init(Camera cam, bool enabledBySettings)
        {
#if APEX_URP
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "ApexPostFx";
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            adjust = profile.Add<ColorAdjustments>(true);
            white = profile.Add<WhiteBalance>(true);
            bloom = profile.Add<Bloom>(true);
            bloom.highQualityFiltering.Override(false);
            vignette = profile.Add<Vignette>(true);
            vignette.color.Override(Color.black);
            vignette.smoothness.Override(0.45f);
            volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
            if (cam != null) cameraData = cam.GetUniversalAdditionalCameraData();
#endif
            active = !enabledBySettings;
            SetEnabled(enabledBySettings);
        }

        public void SetEnabled(bool on)
        {
            if (on == active) return;
            active = on;
#if APEX_URP
            if (volume != null) volume.enabled = on;
            if (cameraData != null)
            {
                cameraData.renderPostProcessing = on;
                cameraData.antialiasing = on ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;
            }
#endif
        }

        public void Apply(ColorGrading.Grade g)
        {
#if APEX_URP
            if (profile == null) return;
            adjust.postExposure.Override(g.postExposure);
            adjust.contrast.Override(g.contrast);
            adjust.saturation.Override(g.saturation);
            white.temperature.Override(g.temperature);
            white.tint.Override(g.tint);
            bloom.intensity.Override(g.bloomIntensity);
            bloom.threshold.Override(g.bloomThreshold);
            bloom.scatter.Override(g.bloomScatter);
            vignette.intensity.Override(g.vignette);
#endif
        }

        void OnDestroy()
        {
            // The camera outlives the race world: leave it without post-processing.
            SetEnabled(false);
#if APEX_URP
            if (profile != null) Destroy(profile);
#endif
        }
    }
}
