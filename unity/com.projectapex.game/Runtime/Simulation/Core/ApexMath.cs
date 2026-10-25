using System;

namespace Apex.Core
{
    /// <summary>Small float helpers so the simulation needs no engine math library.</summary>
    public static class ApexMath
    {
        public const float Pi = MathF.PI;
        public const float Tau = MathF.PI * 2f;
        public const float Deg2Rad = MathF.PI / 180f;
        public const float Rad2Deg = 180f / MathF.PI;

        public static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;

        public static float MoveToward(float current, float target, float maxDelta)
        {
            if (MathF.Abs(target - current) <= maxDelta) return target;
            return current + MathF.Sign(target - current) * maxDelta;
        }

        public static float SmoothStep01(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Positive modulo (result in [0, m)).</summary>
        public static float PosMod(float v, float m)
        {
            float r = v % m;
            return r < 0f ? r + m : r;
        }

        /// <summary>Wraps an angle to (-pi, pi].</summary>
        public static float WrapAngle(float a)
        {
            a = PosMod(a + Pi, Tau) - Pi;
            return a <= -Pi ? a + Tau : a;
        }
    }
}
