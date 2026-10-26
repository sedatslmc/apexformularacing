using System;
using Apex.Core;
using Apex.Weather;

namespace Apex.Environment
{
    /// <summary>
    /// Key light per time of day for Istanbul (41° N) in early autumn, as true
    /// azimuth/elevation, rotated into the game frame with the circuit's true-north
    /// bearing. Golden hour and sunset put the sun in the west-south-west, so it lights
    /// the west-facing Asian shore across the water and back-lights the European
    /// slopes; at night a dim, cool moon replaces it and artificial light takes over.
    /// Colours are kept moderate (no neon, no extreme bloom).
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
            /// <summary>Ambient multiplier and whether street/window lights are on (0..1).</summary>
            public float ambient;
            public float artificial;
        }

        public static Light Get(TimeOfDay time)
        {
            switch (time)
            {
                case TimeOfDay.Morning: return new Light { azimuthTrue = 105f, elevation = 20f, r = 1f, g = 0.9f, b = 0.8f, intensity = 1.1f, ambient = 0.9f };
                case TimeOfDay.Day: return new Light { azimuthTrue = 185f, elevation = 47f, r = 1f, g = 0.97f, b = 0.92f, intensity = 1.35f, ambient = 1f };
                case TimeOfDay.Dusk: return new Light { azimuthTrue = 262f, elevation = 2.5f, r = 1f, g = 0.55f, b = 0.36f, intensity = 0.7f, ambient = 0.6f, artificial = 0.6f };
                case TimeOfDay.Night: return new Light { azimuthTrue = 140f, elevation = 38f, r = 0.55f, g = 0.65f, b = 0.9f, intensity = 0.16f, ambient = 0.28f, artificial = 1f };
                default: return new Light { azimuthTrue = 252f, elevation = 11f, r = 1f, g = 0.78f, b = 0.55f, intensity = 1.15f, ambient = 0.8f, artificial = 0.15f };
            }
        }

        /// <summary>Game-frame azimuth (deg, 0 = +z, clockwise) of the light source.</summary>
        public static float GameAzimuth(Light l, float trueNorthBearing) =>
            ApexMath.PosMod(l.azimuthTrue + (float.IsNaN(trueNorthBearing) ? 0f : trueNorthBearing), 360f);
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
