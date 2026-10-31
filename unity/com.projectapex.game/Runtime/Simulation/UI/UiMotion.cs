using System;

namespace Apex.UI
{
    /// <summary>Easing curves and skippable tweens for the UI (short, precise, never blocking input).</summary>
    public static class UiMotion
    {
        public static float Clamp01(float t) => t < 0f ? 0f : t > 1f ? 1f : t;
        public static float OutCubic(float t) { t = 1f - Clamp01(t); return 1f - t * t * t; }
        public static float InOutCubic(float t) { t = Clamp01(t); return t < 0.5f ? 4f * t * t * t : 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f; }
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// <summary>Progress (0..1, eased) of item <paramref name="index"/> of a staggered entrance <paramref name="time"/> s after it began.</summary>
        public static float Staggered(float time, int index, float duration, float step) => OutCubic((time - index * step) / MathF.Max(duration, 1e-4f));

        /// <summary>Displayed value of a number counting up to <paramref name="target"/> (count-up animations end at the target).</summary>
        public static double CountUp(double target, float time, float duration) => target * OutCubic(time / MathF.Max(duration, 1e-4f));
    }

    /// <summary>A value animated from <see cref="from"/> to <see cref="to"/>; <see cref="Skip"/> jumps to the end.</summary>
    public struct Tween
    {
        public float from, to, duration, elapsed;

        public static Tween Start(float from, float to, float duration) => new Tween { from = from, to = to, duration = duration };

        public bool Done => elapsed >= duration;
        public float Value => duration <= 0f ? to : UiMotion.Lerp(from, to, UiMotion.OutCubic(elapsed / duration));
        public void Advance(float dt) { elapsed = MathF.Min(duration, elapsed + MathF.Max(0f, dt)); }
        public void Skip() { elapsed = duration; }

        /// <summary>Re-targets from the current value (e.g. focus moving while an animation runs), no jump.</summary>
        public void Retarget(float target, float newDuration)
        {
            from = Value;
            to = target;
            duration = newDuration;
            elapsed = 0f;
        }
    }
}
