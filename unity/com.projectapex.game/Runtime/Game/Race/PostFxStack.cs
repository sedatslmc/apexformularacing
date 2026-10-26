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
    /// with the built-in pipeline it does nothing. Bloom filtering quality follows the graphics
    /// tier; anti-aliasing belongs to <see cref="GraphicsQuality"/> (it stays on when the grade is off).
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
#endif
        bool active;

        /// <summary>Creates the global volume (no-op without URP).</summary>
        public void Init(bool enabledBySettings)
        {
#if APEX_URP
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "ApexPostFx";
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            adjust = profile.Add<ColorAdjustments>(true);
            white = profile.Add<WhiteBalance>(true);
            bloom = profile.Add<Bloom>(true);
            bloom.highQualityFiltering.Override(GraphicsQuality.Current.bloomHighQuality);
            vignette = profile.Add<Vignette>(true);
            vignette.color.Override(Color.black);
            vignette.smoothness.Override(0.45f);
            volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
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
            bloom.highQualityFiltering.Override(GraphicsQuality.Current.bloomHighQuality);
            vignette.intensity.Override(g.vignette);
#endif
        }

        void OnDestroy()
        {
#if APEX_URP
            if (profile != null) Destroy(profile);
#endif
        }
    }
}
