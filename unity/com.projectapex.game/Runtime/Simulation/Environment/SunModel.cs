using System;
using Apex.Core;
using Apex.Weather;

namespace Apex.Environment
{
    /// <summary>
    /// Key light per time of day for Istanbul in early autumn, as true azimuth/elevation,
    /// rotated into the game frame with the circuit's true-north bearing. Derived from
    /// <see cref="SkyModel"/> (real sun position on the race weekend, clear-sky atmosphere):
    /// golden hour and sunset put the sun in the west, so it lights the west-facing Asian
    /// shore across the water and back-lights the European slopes; at night a dim, cool
    /// moon replaces it and artificial light takes over.
    /// </summary>
    public static class SunModel
    {
        public struct Light
        {
            /// <summary>True azimuth of the light source (deg from north, clockwise).</summary>
            public float azimuthTrue;
            public float elevation;
            public float r, g, b;
            public float intensity;
            /// <summary>Ambient brightness relative to midday and whether street/window lights are on (0..1).</summary>
            public float ambient;
            public float artificial;
        }

        public static Light Get(TimeOfDay time)
        {
            var sky = SkyModel.Get(time);
            var noon = SkyModel.Get(TimeOfDay.Day);
            return new Light
            {
                azimuthTrue = sky.azimuthTrue,
                elevation = sky.elevation,
                r = sky.sunColor.r, g = sky.sunColor.g, b = sky.sunColor.b,
                intensity = sky.sunIntensity,
                ambient = sky.ambientSky.Luminance / Math.Max(1e-4f, noon.ambientSky.Luminance),
                artificial = sky.artificial,
            };
        }

        /// <summary>Game-frame azimuth (deg, 0 = +z, clockwise) of the light source.</summary>
        public static float GameAzimuth(Light l, float trueNorthBearing) => GameAzimuth(l.azimuthTrue, trueNorthBearing);

        public static float GameAzimuth(float azimuthTrue, float trueNorthBearing) =>
            ApexMath.PosMod(azimuthTrue + (float.IsNaN(trueNorthBearing) ? 0f : trueNorthBearing), 360f);
    }

    /// <summary>
    /// Ambience mix around the listener: layer volumes for water/harbour, crowd and
    /// city plus a reverb amount from how enclosed the spot is (narrow streets between
    /// buildings, stone walls) — engine audio passes through the same reverb.
    /// </summary>
    public static class AmbienceMixer
    {
        public struct Mix
        {
            public float water, crowd, city, wind;
            /// <summary>0 open .. 1 narrow street canyon.</summary>
            public float reverb;
        }

        /// <param name="waterDistance">metres to open water</param>
        /// <param name="grandstandDistance">metres to the nearest grandstand</param>
        /// <param name="enclosure">0..1 building enclosure around the listener</param>
        /// <param name="elevationAboveWater">metres (panoramic, windier sections)</param>
        public static Mix Evaluate(float waterDistance, float grandstandDistance, float enclosure, float elevationAboveWater)
        {
            float open = 1f - ApexMath.Clamp01(enclosure);
            return new Mix
            {
                water = ApexMath.Clamp01(1f - waterDistance / 260f) * (0.4f + 0.6f * open),
                crowd = ApexMath.Clamp01(1f - grandstandDistance / 220f),
                city = 0.25f + 0.55f * ApexMath.Clamp01(enclosure),
                wind = ApexMath.Clamp01(elevationAboveWater / 60f) * open,
                reverb = ApexMath.Clamp01((enclosure - 0.25f) / 0.6f),
            };
        }
    }
}
