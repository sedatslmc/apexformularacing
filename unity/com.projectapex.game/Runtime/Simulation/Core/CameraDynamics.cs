using System;

namespace Apex.Core
{
    /// <summary>
    /// Subtle camera feedback (handling brief 2026-11, section 7): a sense of speed and of the car's weight transfer
    /// without hiding what the car does. Inputs are the car's speed and its longitudinal/lateral acceleration; they go
    /// through a critically damped spring (the camera rides on a soft mount), so kerb hits and noise do not shake the
    /// picture. Off with "Reduzierte Bewegung" (strength 0).
    /// <list type="bullet">
    /// <item>Field of view: up to +6° at 300 km/h (sense of speed).</item>
    /// <item>Braking: the chase camera moves up to 0.45 m closer and every view pitches down up to 1.2° (the car dives);
    /// accelerating: the chase camera drops back up to 0.3 m.</item>
    /// <item>Cornering: the chase camera slides up to 0.25 m to the outside, so the car's yaw angle stays readable.</item>
    /// </list>
    /// The camera must not cover bad physics: the offsets are small next to the car's own motion.
    /// </summary>
    public sealed class CameraDynamics
    {
        public const float MaxFovGain = 6f, FullFovSpeed = 83f;
        public const float BrakeCloser = 0.45f, AccelBack = 0.3f, PitchPerG = 0.25f, MaxPitch = 1.2f, LateralSlide = 0.25f;
        /// <summary>Spring natural frequency (rad/s): ~0.3 s to settle.</summary>
        public const float Omega = 9f;

        float longG, longV, latG, latV;

        /// <summary>0 off .. 1 full.</summary>
        public float strength = 1f;

        /// <summary>Extra field of view (deg).</summary>
        public float FovOffset { get; private set; }
        /// <summary>Chase camera offset along the car (m, + = further behind).</summary>
        public float Back { get; private set; }
        /// <summary>Chase camera offset across (m, + = to the right).</summary>
        public float Side { get; private set; }
        /// <summary>Camera pitch (deg, + = nose down).</summary>
        public float Pitch { get; private set; }

        public void Reset() { longG = longV = latG = latV = 0f; FovOffset = Back = Side = Pitch = 0f; }

        /// <param name="speed">forward speed (m/s)</param>
        /// <param name="longAccel">longitudinal acceleration (m/s², + forward)</param>
        /// <param name="latAccel">lateral acceleration (m/s², + to the right)</param>
        public void Update(float speed, float longAccel, float latAccel, float dt)
        {
            float s = ApexMath.Clamp01(strength);
            if (dt <= 0f) return;
            dt = MathF.Min(dt, 0.05f);   // a frame hitch must not kick the spring
            // Critically damped spring towards the (clamped) acceleration in g.
            Spring(ref longG, ref longV, ApexMath.Clamp(longAccel / 9.81f, -6f, 3f), dt);
            Spring(ref latG, ref latV, ApexMath.Clamp(latAccel / 9.81f, -5f, 5f), dt);
            float v = ApexMath.Clamp01(MathF.Abs(speed) / FullFovSpeed);
            FovOffset = s * MaxFovGain * v * v * (3f - 2f * v);
            float brake = ApexMath.Clamp01(-longG / 5f), accel = ApexMath.Clamp01(longG / 1.5f);
            Back = s * (AccelBack * accel - BrakeCloser * brake);
            Pitch = s * ApexMath.Clamp(-longG * PitchPerG, -MaxPitch * 0.5f, MaxPitch);
            // Outward in a corner: a right turn (lateral acceleration to the right) puts the camera to the left.
            Side = s * -LateralSlide * ApexMath.Clamp(latG / 4f, -1f, 1f);
        }

        static void Spring(ref float x, ref float v, float target, float dt)
        {
            // Semi-implicit critically damped spring, stable for any dt.
            float a = Omega * Omega * (target - x) - 2f * Omega * v;
            v += a * dt;
            x += v * dt;
            if (float.IsNaN(x)) { x = target; v = 0f; }
        }
    }
}
