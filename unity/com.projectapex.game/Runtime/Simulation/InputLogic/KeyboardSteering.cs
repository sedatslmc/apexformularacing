using System;
using Apex.Core;

namespace Apex.InputLogic
{
    /// <summary>
    /// Digital steering (keyboard, touch buttons) to a steering value (handling brief 2026-11,
    /// docs/HANDLING_KEYBOARD.md). A key is either on or off, so the steering is built up in two layers:
    /// <list type="number">
    /// <item>Ramp: an internal travel p moves towards the key at a press rate (slower at speed), returns to the
    /// centre at a release rate and swings through the centre at a reversal rate when the other key is pressed.</item>
    /// <item>Progressive curve: output = sign(p)·|p|^γ. A short tap gives a small correction, holding the key builds
    /// up to full input in the same time as a linear ramp – smooth, not slow.</item>
    /// </list>
    /// What "full input" means is decided by the car: with the front-slip limiter
    /// (<see cref="SlipTarget"/>, VehicleSimulation.steerSlipTarget) full input puts the front tyres at their grip peak,
    /// at 60 km/h as at 300 km/h – large lock in hairpins, small lock at speed, never past the peak.
    /// The ramp runs on the physics clock (FixedUpdate), so it does not depend on the frame rate.
    /// </summary>
    public sealed class KeyboardSteering
    {
        public enum Profile { Direct = 0, Balanced = 1, Smooth = 2, Classic = 3 }

        public struct Rates
        {
            /// <summary>Press rate at standstill and at <see cref="FullSpeed"/> (travel per second).</summary>
            public float pressLow, pressHigh, releaseLow, releaseHigh, reverse;
            /// <summary>Progressive curve exponent (1 = linear).</summary>
            public float gamma;
        }

        /// <summary>Speed (m/s) at which the high-speed rates apply (~290 km/h).</summary>
        public const float FullSpeed = 80f;

        public static Rates RatesFor(Profile p)
        {
            switch (p)
            {
                case Profile.Direct: return new Rates { pressLow = 9f, pressHigh = 5.5f, releaseLow = 12f, releaseHigh = 8f, reverse = 12f, gamma = 1.15f };
                case Profile.Smooth: return new Rates { pressLow = 5.5f, pressHigh = 2.6f, releaseLow = 7.5f, releaseHigh = 4.5f, reverse = 7f, gamma = 1.6f };
                case Profile.Classic:
                    // The October 2026 keyboard (InputTuning ramps: k over 60 m/s, linear), for the A/B comparison.
                    return new Rates { pressLow = 8f, pressHigh = 4.5f, releaseLow = 10f, releaseHigh = 7f, reverse = InputTuning.SteerReverseRate, gamma = 1f };
                default: return new Rates { pressLow = 7.5f, pressHigh = 3.6f, releaseLow = 10f, releaseHigh = 6f, reverse = 9.5f, gamma = 1.35f };
            }
        }

        /// <summary>Front-slip limiter target for digital steering (VehicleSimulation.steerSlipTarget); 0 = none (Classic).</summary>
        public static float SlipTarget(Profile p) => p == Profile.Direct ? 1.1f : p == Profile.Smooth ? 0.95f : p == Profile.Classic ? 0f : 1f;

        public Profile profile = Profile.Balanced;
        /// <summary>Player setting "Lenkempfindlichkeit" (0.5 .. 1.5): scales the ramp rates.</summary>
        public float sensitivity = 1f;
        /// <summary>Player setting "Lenkglättung" (0 .. 1): output low-pass of up to 60 ms on top of the ramp.</summary>
        public float smoothing;

        float travel, output;

        /// <summary>Internal ramp travel (−1..1).</summary>
        public float Travel => travel;
        public float Value => output;

        public void Reset() { travel = 0f; output = 0f; }

        /// <param name="key">−1 left, 0 none, +1 right (both keys = 0)</param>
        /// <param name="speed">car speed (m/s)</param>
        public float Update(float key, float speed, float dt)
        {
            var r = RatesFor(profile);
            bool classic = profile == Profile.Classic;
            float k = ApexMath.Clamp01(MathF.Abs(speed) / (classic ? 60f : FullSpeed));
            float sens = ApexMath.Clamp(sensitivity, 0.5f, 1.5f);
            key = ApexMath.Clamp(key, -1f, 1f);
            float rate;
            if (key == 0f) rate = ApexMath.Lerp(r.releaseLow, r.releaseHigh, k);
            else if (travel != 0f && MathF.Sign(key) != MathF.Sign(travel)) rate = r.reverse;
            else rate = ApexMath.Lerp(r.pressLow, r.pressHigh, k);
            travel = ApexMath.MoveToward(travel, key, rate * sens * dt);
            float shaped = MathF.Sign(travel) * MathF.Pow(MathF.Abs(travel), r.gamma);
            float tau = 0.06f * ApexMath.Clamp01(smoothing);
            output = tau > 1e-4f ? output + (shaped - output) * ApexMath.Clamp01(dt / tau) : shaped;
            return output;
        }
    }
}
