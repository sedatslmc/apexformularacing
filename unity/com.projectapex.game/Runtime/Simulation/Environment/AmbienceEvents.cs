using System;

namespace Apex.Environment
{
    /// <summary>
    /// Occasional sounds on top of the ambience loops (docs/ISTANBUL_OVERHAUL.md, Phase 5): a distant ship's horn
    /// every 35–90 s and gull cries every 6–18 s, both only where the water can be heard (ambience water mix), and
    /// fainter the less of it there is.
    /// </summary>
    public static class AmbienceEvents
    {
        public enum Kind { ShipHorn, Gull }

        /// <summary>Seconds until the next event of a kind, from a random value in [0, 1).</summary>
        public static float NextInterval(Kind k, double r) => k == Kind.ShipHorn ? 35f + (float)r * 55f : 6f + (float)r * 12f;

        /// <summary>Volume (0..1) of an event at a water-mix level (AmbienceMixer.water, 0..1); silent inland.</summary>
        public static float Volume(Kind k, float waterMix)
        {
            if (waterMix < 0.15f) return 0f;
            float v = Math.Clamp((waterMix - 0.15f) / 0.85f, 0f, 1f);
            return k == Kind.ShipHorn ? 0.25f + 0.45f * v : 0.15f + 0.35f * v;
        }
    }
}
