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
        public static float SteerPressRate(float k) => ApexMath.Lerp(5.5f, 3f, k);
        public static float SteerReleaseRate(float k) => ApexMath.Lerp(8f, 5.5f, k);
        public const float SteerReverseRate = 10f;
        public const float ThrottleRate = 7f;
        public const float BrakeRate = 9f;
    }
}
