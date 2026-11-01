using System;

namespace Apex.Environment
{
    /// <summary>
    /// Wind motion of the vertically hung balcony and window flags (docs/ISTANBUL_OVERHAUL.md, Phase 5): the cloth is
    /// held at the hoist (u = 0, the top) and swings out of the facade towards the free end, with a travelling wave
    /// whose amplitude and frequency grow with the wind (WindField). Every flag gets its own phase.
    /// </summary>
    public static class FlagWave
    {
        /// <summary>Largest displacement of the free end (m) at a wind speed (m/s).</summary>
        public static float Amplitude(float wind) => Math.Clamp(0.03f + 0.028f * wind, 0.03f, 0.35f);

        /// <summary>Flutter frequency (Hz).</summary>
        public static float Frequency(float wind) => 0.8f + 0.12f * Math.Clamp(wind, 0f, 15f);

        /// <summary>Displacement out of the facade (m) of a point at u (0 hoist .. 1 free end) at time t.</summary>
        public static float Out(float u, double t, float wind, float phase)
        {
            float a = Amplitude(wind) * MathF.Pow(Math.Clamp(u, 0f, 1f), 1.4f);
            return a * (0.6f + 0.4f * MathF.Sin((float)(2.0 * Math.PI * Frequency(wind) * t) - 4.2f * u + phase));
        }

        /// <summary>Sideways displacement (m), smaller and out of step with <see cref="Out"/>.</summary>
        public static float Side(float u, double t, float wind, float phase)
        {
            float a = 0.35f * Amplitude(wind) * MathF.Pow(Math.Clamp(u, 0f, 1f), 1.6f);
            return a * MathF.Cos((float)(2.0 * Math.PI * Frequency(wind) * 0.7 * t) - 3.1f * u + phase * 1.7f);
        }

        /// <summary>Phase of the flag at a position (each flag swings on its own).</summary>
        public static float Phase(float x, float z) => (LandModel.Hash((int)MathF.Floor(x * 0.5f), (int)MathF.Floor(z * 0.5f)) + 1f) * MathF.PI;
    }
}
