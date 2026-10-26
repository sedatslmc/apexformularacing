using Apex.Core;
using Apex.Weather;

namespace Apex.Environment
{
    /// <summary>
    /// Post-processing grade per time of day and weather, in the units of URP's volume
    /// overrides (ColorAdjustments, WhiteBalance, Bloom, Vignette) so the runtime only copies
    /// values. Tonemapping is ACES throughout; the grade keeps colours natural (no teal-orange
    /// push, no heavy bloom): golden hour is warm and slightly lower in contrast, overcast and
    /// rain are cooler and flatter, night lifts exposure a little and lets the street lights bloom.
    /// </summary>
    public static class ColorGrading
    {
        public struct Grade
        {
            /// <summary>Exposure offset in EV.</summary>
            public float postExposure;
            /// <summary>-100..100.</summary>
            public float contrast, saturation;
            /// <summary>White balance temperature and tint, -100..100 (+ warmer / + magenta).</summary>
            public float temperature, tint;
            /// <summary>Bloom intensity (>= 0), threshold (linear, >= 0) and scatter (0..1).</summary>
            public float bloomIntensity, bloomThreshold, bloomScatter;
            /// <summary>0..1.</summary>
            public float vignette;
        }

        public static Grade Get(TimeOfDay time, float cloudCover, float rainIntensity)
        {
            Grade g;
            switch (time)
            {
                case TimeOfDay.Morning:
                    g = new Grade { postExposure = 0.1f, contrast = 8f, saturation = 4f, temperature = 6f, tint = 0f, bloomIntensity = 0.25f, bloomThreshold = 1.1f, bloomScatter = 0.6f, vignette = 0.16f };
                    break;
                case TimeOfDay.Day:
                    g = new Grade { postExposure = 0f, contrast = 10f, saturation = 6f, temperature = 0f, tint = 0f, bloomIntensity = 0.2f, bloomThreshold = 1.2f, bloomScatter = 0.6f, vignette = 0.15f };
                    break;
                case TimeOfDay.Dusk:
                    g = new Grade { postExposure = 0.35f, contrast = 6f, saturation = 2f, temperature = 14f, tint = 4f, bloomIntensity = 0.6f, bloomThreshold = 0.95f, bloomScatter = 0.7f, vignette = 0.2f };
                    break;
                case TimeOfDay.Night:
                    g = new Grade { postExposure = 0.6f, contrast = 12f, saturation = -6f, temperature = -8f, tint = 0f, bloomIntensity = 0.9f, bloomThreshold = 0.85f, bloomScatter = 0.7f, vignette = 0.24f };
                    break;
                default: // golden hour
                    g = new Grade { postExposure = 0.15f, contrast = 6f, saturation = 8f, temperature = 18f, tint = 3f, bloomIntensity = 0.4f, bloomThreshold = 1.0f, bloomScatter = 0.65f, vignette = 0.18f };
                    break;
            }
            // Overcast and rain: diffuse light, cooler and flatter, less highlight bloom from the sun.
            float c = ApexMath.Clamp01(cloudCover), r = ApexMath.Clamp01(rainIntensity);
            float flat = ApexMath.Clamp01(0.6f * c + 0.4f * r);
            g.contrast = ApexMath.Lerp(g.contrast, -4f, flat);
            g.saturation = ApexMath.Lerp(g.saturation, -18f, flat);
            g.temperature = ApexMath.Lerp(g.temperature, -6f, flat);
            g.tint *= 1f - flat;
            if (time != TimeOfDay.Night) g.bloomIntensity *= 1f - 0.5f * flat;
            g.postExposure += 0.2f * flat;
            return g;
        }
    }
}
