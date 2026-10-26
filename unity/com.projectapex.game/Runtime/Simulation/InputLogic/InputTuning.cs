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
        public static float SteerPressRate(float k) => ApexMath.Lerp(8f, 4.5f, k);
        public static float SteerReleaseRate(float k) => ApexMath.Lerp(10f, 7f, k);
        public const float SteerReverseRate = 10f;
        public const float ThrottleRate = 7f;
        public const float BrakeRate = 9f;

        /// <summary>Gamepad stick dead zone (fraction of travel), rescaled so output starts at 0 just outside it.</summary>
        public const float StickDeadZone = 0.06f;
        /// <summary>Stick response exponent: &gt; 1 gives finer control around the centre, full lock unchanged.</summary>
        public const float StickExponent = 1.35f;

        /// <summary>Analogue stick to steering: dead zone with rescale, then the response curve (sign kept).</summary>
        public static float StickSteer(float x)
        {
            float a = System.MathF.Abs(x);
            if (a <= StickDeadZone) return 0f;
            float t = ApexMath.Clamp01((a - StickDeadZone) / (1f - StickDeadZone));
            return System.MathF.Sign(x) * System.MathF.Pow(t, StickExponent);
        }
    }
}
