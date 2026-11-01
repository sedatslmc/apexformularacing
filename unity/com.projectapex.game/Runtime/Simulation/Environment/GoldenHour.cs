using System;
using System.Numerics;
using Apex.Core;

namespace Apex.Environment
{
    /// <summary>
    /// The low-sun effects that make Istanbul's golden hour (docs/ISTANBUL_OVERHAUL.md, golden hour section), on top
    /// of the physical sun and sky (<see cref="SkyModel"/>):
    /// <list type="bullet">
    /// <item>Window glint: the west-facing windows of Üsküdar and the Asian shore mirror the setting sun and burn
    /// golden for a viewer on the European side. A window reflects the sun towards r = 2(n·s)n − s; the panes of
    /// a city are not all in one plane, so the reflection is a broad lobe around r, not a point.</item>
    /// <item>Directional haze: looking towards a low sun the aerosol over the strait scatters it forward (Mie,
    /// Henyey–Greenstein), so the haze glows golden there and stays blue-grey on the other side.</item>
    /// <item>Haze range: at low sun the light crosses more air; the far shore and the hills sink into the haze sooner.</item>
    /// </list>
    /// Directions are in the game frame (x east, y up, z north, rotated by the circuit's true-north bearing).
    /// </summary>
    public static class GoldenHour
    {
        /// <summary>Glint fades in below this sun elevation (deg) and is full below <see cref="GlintFull"/>.</summary>
        public const float GlintStart = 22f, GlintFull = 12f;
        /// <summary>Lobe exponent of the reflection (spread of window orientations, about ±25°).</summary>
        public const float GlintLobe = 6f;
        /// <summary>Peak emission of a fully glinting window (HDR, the reflected sun in sRGB intensity units).</summary>
        public const float GlintPeak = 3.2f;
        /// <summary>Henyey–Greenstein asymmetry of urban aerosol.</summary>
        public const float HazeAsymmetry = 0.72f;

        /// <summary>Unit vector from the scene towards the sun (or moon) in the game frame.</summary>
        public static Vector3 SunDirection(float azimuthTrue, float elevation, float trueNorthBearing)
        {
            float az = SunModel.GameAzimuth(azimuthTrue, trueNorthBearing) * MathF.PI / 180f;
            float el = elevation * MathF.PI / 180f;
            return new Vector3(MathF.Sin(az) * MathF.Cos(el), MathF.Sin(el), MathF.Cos(az) * MathF.Cos(el));
        }

        /// <summary>0..1 weight of the low-sun effects at a sun elevation (0 by day and at night).</summary>
        public static float LowSun(float elevation) =>
            elevation < -1f ? 0f : ApexMath.Clamp01((GlintStart - elevation) / (GlintStart - GlintFull)) * ApexMath.Clamp01((elevation + 1f) / 2f);

        /// <summary>
        /// Glint strength 0..1 of windows on a wall with outward normal <paramref name="normal"/> (horizontal unit
        /// vector) seen from direction <paramref name="toViewer"/> (unit vector from the wall to the camera).
        /// </summary>
        public static float Glint(Vector3 normal, Vector3 toViewer, Vector3 sun, float elevation, float cloudCover)
        {
            float ns = Vector3.Dot(normal, sun);
            if (ns <= 0f) return 0f;                          // sun behind the wall
            if (Vector3.Dot(normal, toViewer) <= 0f) return 0f; // viewer behind the wall
            var r = 2f * ns * normal - sun;
            float lobe = MathF.Pow(MathF.Max(0f, Vector3.Dot(r, toViewer)), GlintLobe);
            // A grazing sun lights the pane less (projected area), cloud dims the direct beam.
            return lobe * MathF.Sqrt(ns) * LowSun(elevation) * (1f - 0.9f * ApexMath.Clamp01(cloudCover));
        }

        /// <summary>Emission colour (sRGB, HDR scale) of fully glinting windows under the given light.</summary>
        public static SkyModel.Rgb GlintColour(SkyModel.Lighting l, float strength) => l.sunColor * (GlintPeak * ApexMath.Clamp01(strength));

        /// <summary>Henyey–Greenstein phase normalised to 1 at 90° (side-on).</summary>
        public static float Phase(float cosViewSun)
        {
            float g = HazeAsymmetry, g2 = g * g;
            float Hg(float c) => (1f - g2) / MathF.Pow(1f + g2 - 2f * g * c, 1.5f);
            return Hg(ApexMath.Clamp(cosViewSun, -1f, 1f)) / Hg(0f);
        }

        /// <summary>
        /// Haze (fog) colour for a view direction: the sky model's haze, brightened and warmed towards a low sun by
        /// forward scattering. <paramref name="cosViewSun"/> is the cosine between the view and the sun direction.
        /// </summary>
        public static SkyModel.Rgb Haze(SkyModel.Lighting l, float cosViewSun, float cloudCover)
        {
            float low = LowSun(l.elevation) * (1f - 0.85f * ApexMath.Clamp01(cloudCover));
            if (low <= 0f) return l.fog;
            // Forward peak (towards the sun) adds the sun's colour; the back side loses a little warmth.
            float p = Phase(cosViewSun);                      // 1 side-on, ~30 towards the sun, ~0.3 away
            float toward = ApexMath.Clamp01((p - 1f) / 12f);
            float away = ApexMath.Clamp01(1f - p) * 0.35f;
            var sun = l.sunColor;
            var cool = new SkyModel.Rgb(l.fog.r * 0.9f, l.fog.g * 0.95f, l.fog.b * 1.04f);
            var warm = new SkyModel.Rgb(
                Math.Min(1f, l.fog.r * 0.55f + sun.r * 0.62f),
                Math.Min(1f, l.fog.g * 0.55f + sun.g * 0.55f),
                Math.Min(1f, l.fog.b * 0.55f + sun.b * 0.4f));
            var c = SkyModel.Rgb.Lerp(l.fog, warm, toward * low);
            return SkyModel.Rgb.Lerp(c, cool, away * low);
        }

        /// <summary>Linear fog start and end (m): longer air path at low sun, rain closes in.</summary>
        public static (float start, float end) HazeRange(float elevation, float cloudCover, float rain, bool environment)
        {
            float low = LowSun(elevation);
            float start = ApexMath.Lerp(600f, 320f, low);
            float end = ApexMath.Lerp(environment ? 8000f : 3500f, environment ? 5200f : 2600f, low);
            end *= 1f - 0.12f * ApexMath.Clamp01(cloudCover);
            float r = ApexMath.Clamp01(rain);
            return (ApexMath.Lerp(start, 60f, r), ApexMath.Lerp(end, 900f, r));
        }
    }
}
