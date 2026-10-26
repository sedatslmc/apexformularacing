// API stubs (signatures only) of com.unity.render-pipelines.core / .universal 17.x.
namespace UnityEngine.Rendering
{
    public class VolumeParameter<T>
    {
        public bool overrideState;
        public virtual T value { get; set; }
        public void Override(T x) { overrideState = true; value = x; }
    }
    public class FloatParameter : VolumeParameter<float> { public FloatParameter(float value, bool overrideState = false) { } }
    public class MinFloatParameter : FloatParameter { public MinFloatParameter(float value, float min, bool overrideState = false) : base(value, overrideState) { } }
    public class ClampedFloatParameter : FloatParameter { public ClampedFloatParameter(float value, float min, float max, bool overrideState = false) : base(value, overrideState) { } }
    public class BoolParameter : VolumeParameter<bool> { public BoolParameter(bool value, bool overrideState = false) { } }
    public class ColorParameter : VolumeParameter<Color> { public ColorParameter(Color value, bool overrideState = false) { } }

    public class VolumeComponent : ScriptableObject
    {
        public bool active = true;
    }

    public sealed class VolumeProfile : ScriptableObject
    {
        public T Add<T>(bool overrides = false) where T : VolumeComponent => null;
        public bool TryGet<T>(out T component) where T : VolumeComponent { component = null; return false; }
    }

    public class Volume : MonoBehaviour
    {
        public bool isGlobal = true;
        public float priority;
        public float weight = 1f;
        public float blendDistance;
        public VolumeProfile sharedProfile;
        public VolumeProfile profile { get; set; }
    }
}

namespace UnityEngine.Rendering.Universal
{
    public enum TonemappingMode { None, Neutral, ACES }
    public sealed class TonemappingModeParameter : VolumeParameter<TonemappingMode> { public TonemappingModeParameter(TonemappingMode value, bool overrideState = false) { } }
    public sealed class Tonemapping : VolumeComponent { public TonemappingModeParameter mode; }
    public sealed class Bloom : VolumeComponent
    {
        public MinFloatParameter threshold, intensity;
        public ClampedFloatParameter scatter;
        public ColorParameter tint;
        public BoolParameter highQualityFiltering;
    }
    public sealed class ColorAdjustments : VolumeComponent
    {
        public FloatParameter postExposure;
        public ClampedFloatParameter contrast, hueShift, saturation;
        public ColorParameter colorFilter;
    }
    public sealed class WhiteBalance : VolumeComponent { public ClampedFloatParameter temperature, tint; }
    public sealed class Vignette : VolumeComponent
    {
        public ColorParameter color;
        public ClampedFloatParameter intensity, smoothness;
        public BoolParameter rounded;
    }

    public enum AntialiasingMode { None, FastApproximateAntialiasing, SubpixelMorphologicalAntiAliasing, TemporalAntiAliasing }
    public enum AntialiasingQuality { Low, Medium, High }

    public class UniversalAdditionalCameraData : MonoBehaviour
    {
        public bool renderPostProcessing { get; set; }
        public AntialiasingMode antialiasing { get; set; }
        public AntialiasingQuality antialiasingQuality { get; set; }
    }

    public static class CameraExtensions
    {
        public static UniversalAdditionalCameraData GetUniversalAdditionalCameraData(this Camera camera) => null;
    }
}
