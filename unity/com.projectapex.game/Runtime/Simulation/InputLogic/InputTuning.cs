using Apex.Core;

namespace Apex.InputLogic
{
    /// <summary>
    /// Ramps for digital steering (keys / touch buttons), shared by the game and the
    /// tuning tools. k = speed / 60 m/s clamped to 0..1. Rates are in full-scale
    /// steering units per second.
    /// </summary>
    public static class InputTuning
    {
        // Faster than before (5.5→3 press, 8→5.5 release): full input at speed took up to 0.33 s, which
        // read as a delay. Full input at speed is bounded by the speed-sensitive lock, so this stays safe.
        public static float SteerPressRate(float k, bool legacy = false) => legacy ? ApexMath.Lerp(5.5f, 3f, k) : ApexMath.Lerp(8f, 4.5f, k);
        public static float SteerReleaseRate(float k, bool legacy = false) => legacy ? ApexMath.Lerp(8f, 5.5f, k) : ApexMath.Lerp(10f, 7f, k);
        public const float SteerReverseRate = 10f;
        public const float ThrottleRate = 7f;
        public const float BrakeRate = 9f;

        /// <summary>Stick response exponent: &gt; 1 gives finer control around the centre, full lock unchanged.</summary>
        public const float StickExponent = 1.35f;

        /// <summary>Analogue stick to steering: response curve only (sign kept). The Input System's
        /// stickDeadzone processor on Gamepad.leftStick already removes the centre (0.125) and
        /// rescales, so a second dead zone here would stack.</summary>
        public static float StickSteer(float x, bool legacy = false)
        {
            if (legacy) return ApexMath.Clamp(x, -1f, 1f);
            float a = ApexMath.Clamp01(System.MathF.Abs(x));
            return System.MathF.Sign(x) * System.MathF.Pow(a, StickExponent);
        }

        /// <summary>Stick curve with the player's "Lenkempfindlichkeit" (0.5 .. 1.5): less sensitive = a softer centre
        /// (exponent up to 1.7), more sensitive = closer to linear (down to 1.0). Full lock stays at full deflection.</summary>
        public static float StickSteer(float x, float sensitivity)
        {
            float s = ApexMath.Clamp(sensitivity, 0.5f, 1.5f);
            float exponent = s <= 1f ? ApexMath.Lerp(1.7f, StickExponent, (s - 0.5f) * 2f) : ApexMath.Lerp(StickExponent, 1f, s - 1f);
            float a = ApexMath.Clamp01(System.MathF.Abs(x));
            return System.MathF.Sign(x) * System.MathF.Pow(a, exponent);
        }
    }
}
