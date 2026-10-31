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
        MotionBlur motionBlur;
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
            // Camera motion blur (High and above, the player can switch it off): blur from the camera's own motion,
            // so the track and city streak at speed while the car in front of the chase camera stays sharp.
            motionBlur = profile.Add<MotionBlur>(true);
            motionBlur.mode.Override(MotionBlurMode.CameraOnly);
            motionBlur.clamp.Override(0.05f);
            ApplyMotionBlur();
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
            ApplyMotionBlur();
#endif
        }

        /// <summary>Player's motion blur switch (Settings.motionBlur); the strength comes from the graphics tier.</summary>
        public static bool MotionBlurEnabled = true;

        /// <summary>Re-applies the tier's motion blur (after a tier or settings change).</summary>
        public void ApplyMotionBlur()
        {
#if APEX_URP
            if (motionBlur == null) return;
            var p = GraphicsQuality.Current;
            float k = MotionBlurEnabled ? p.motionBlur : 0f;
            motionBlur.active = k > 0f;
            motionBlur.intensity.Override(k);
            motionBlur.quality.Override((MotionBlurQuality)Mathf.Clamp(p.motionBlurQuality, 0, 2));
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
