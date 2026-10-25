using System;
using Apex.Core;

namespace Apex.InputLogic
{
    /// <summary>
    /// Turns digital (key / touch button) input into a progressive analog value with
    /// separate press, release and reversal rates, like a driver's hands on a wheel.
    /// </summary>
    [Serializable]
    public sealed class DigitalAxis
    {
        public float pressRate = 3.2f;
        public float releaseRate = 5f;
        /// <summary>Rate when pushing against the current direction (counter-steer).</summary>
        public float reverseRate = 7f;

        public float Value { get; private set; }

        public DigitalAxis() { }

        public DigitalAxis(float press, float release, float reverse)
        {
            pressRate = press;
            releaseRate = release;
            reverseRate = reverse;
        }

        public float Update(float target, float dt)
        {
            target = ApexMath.Clamp(target, -1f, 1f);
            float rate;
            if (target == 0f) rate = releaseRate;
            else if (Value != 0f && MathF.Sign(target) != MathF.Sign(Value)) rate = reverseRate;
            else rate = pressRate;
            Value = ApexMath.MoveToward(Value, target, rate * dt);
            return Value;
        }

        public void Reset() => Value = 0f;
    }
}
