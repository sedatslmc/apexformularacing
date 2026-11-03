using System;
using Apex.Core;

namespace Apex.InputLogic
{
    /// <summary>
    /// Virtual steering for touch screens (handling brief 2026-11, section 4 "optional virtuelle Lenksteuerung"):
    /// the thumb lands anywhere in the steering zone, and its sideways travel from that point is the steering –
    /// analogue, unlike the ◄ ► buttons. Positions are in screen widths (0..1), so it feels the same on every
    /// phone and tablet.
    /// <list type="bullet">
    /// <item>Range: <see cref="range"/> screen widths for full lock (default 0.12: about a thumb's comfortable swipe).</item>
    /// <item>A small dead zone around the landing point, then a mild curve (finer near the centre).</item>
    /// <item>Floating centre: once the thumb passes full lock, the centre follows it, so moving back steers the other
    /// way at once instead of first travelling back over the overshoot.</item>
    /// <item>Lifting the thumb returns the steering to the centre within ~80 ms.</item>
    /// </list>
    /// The result goes to the car like a stick (front-slip limiter 1.0), so it uses the same physics as every input.
    /// </summary>
    public sealed class TouchWheel
    {
        /// <summary>Screen widths of thumb travel for full lock.</summary>
        public float range = 0.12f;
        public const float DeadZone = 0.06f, Exponent = 1.25f, ReturnRate = 12f;

        float origin;
        bool held;

        public float Value { get; private set; }
        public bool Held => held;

        /// <summary>Thumb lands at <paramref name="x"/> (screen widths).</summary>
        public void Begin(float x)
        {
            origin = x;
            held = true;
        }

        /// <summary>Thumb at <paramref name="x"/> while held, or released (<paramref name="down"/> false).</summary>
        public float Update(bool down, float x, float dt)
        {
            if (!down)
            {
                held = false;
                Value = ApexMath.MoveToward(Value, 0f, ReturnRate * dt);
                return Value;
            }
            if (!held) Begin(x);
            float r = MathF.Max(range, 0.02f);
            float offset = (x - origin) / r;
            // Floating centre: never further than full lock from the thumb.
            if (offset > 1f) { origin = x - r; offset = 1f; }
            else if (offset < -1f) { origin = x + r; offset = -1f; }
            float a = MathF.Abs(offset);
            float shaped = a <= DeadZone ? 0f : MathF.Pow((a - DeadZone) / (1f - DeadZone), Exponent);
            Value = MathF.Sign(offset) * ApexMath.Clamp01(shaped);
            return Value;
        }

        public void Reset() { held = false; Value = 0f; }
    }
}
