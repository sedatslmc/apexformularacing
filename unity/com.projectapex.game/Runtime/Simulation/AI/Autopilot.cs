using System;
using System.Numerics;
using Apex.Core;
using Apex.Tracks;
using Apex.Vehicle;

namespace Apex.AI
{
    /// <summary>
    /// Racing-line follower used by automated tests, attract mode and as the seed of
    /// the AI driver. Pure pursuit on the centreline (plus lateral offset) and a
    /// curvature-based speed profile limited by estimated grip incl. downforce, with
    /// backward (braking) and forward (traction) passes. Uses the same physics as
    /// the player: it only produces pedal and steering inputs.
    /// </summary>
    public sealed class Autopilot
    {
        public readonly TrackGeometry geometry;
        public float aggression;
        public float muEstimate = 1.6f;
        public float downforcePerMass;
        public float lateralOffset;

        float[] profile;
        int hint = -1;

        public Autopilot(TrackGeometry geometry, VehicleSetup setup, float skill = 0.8f)
        {
            this.geometry = geometry;
            aggression = skill;
            downforcePerMass = setup.downforceArea * setup.airDensity * 0.5f / setup.mass;
            BuildProfile();
        }

        void BuildProfile()
        {
            int n = geometry.Count;
            float mu = muEstimate * aggression;
            const float g = 9.81f;
            profile = new float[n];
            for (int i = 0; i < n; i++)
            {
                // Tightest curvature nearby, so the whole corner is covered.
                float k = 0f;
                for (int o = -3; o <= 3; o++) k = MathF.Max(k, MathF.Abs(geometry.Curvatures[((i + o) % n + n) % n]));
                float denom = k - mu * downforcePerMass;
                profile[i] = denom <= 1e-5f ? 95f : MathF.Min(95f, MathF.Sqrt(mu * g / denom));
            }
            for (int pass = 0; pass < 2; pass++)
                for (int step = 0; step < n; step++)
                {
                    int i = n - 1 - step;
                    float v = profile[(i + 1) % n];
                    // Friction circle (as RacingLine): braking gets what the cornering here leaves of the grip.
                    float aTotal = mu * (g + downforcePerMass * v * v);
                    float aLat = v * v * MathF.Abs(geometry.Curvatures[i]);
                    float decel = MathF.Max(1f, MathF.Sqrt(MathF.Max(0f, aTotal * aTotal - aLat * aLat)) * 0.85f);
                    profile[i] = MathF.Min(profile[i], MathF.Sqrt(v * v + 2f * decel * geometry.Spacing));
                }
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    float v = profile[i];
                    float accel = ApexMath.Clamp(14f - v * 0.09f, 3f, 14f);
                    profile[j] = MathF.Min(profile[j], MathF.Sqrt(v * v + 2f * accel * geometry.Spacing));
                }
        }

        public float TargetSpeed(float distance) => profile[geometry.IndexAt(distance)];

        public void ResetTracking() => hint = -1;

        /// <summary>
        /// Computes inputs. position/forward are world space (x east, y up, z north);
        /// returns (throttle, brake, steer[-1..1, +right]) for a car whose maximum
        /// wheel angle is maxSteerRad (steering assist should be off).
        /// </summary>
        public (float throttle, float brake, float steer) Drive(Vector3 position, Vector3 forward, float forwardSpeed, float wheelbase, float maxSteerRad)
        {
            var proj = geometry.Project(position, hint);
            hint = proj.index;
            float d = proj.distance;
            float v = MathF.Max(forwardSpeed, 0f);
            float lookahead = 7f + v * 0.42f;
            var target = geometry.PositionAt(d + lookahead) + geometry.RightAt(d + lookahead) * lateralOffset;
            var fwd = Vector3.Normalize(new Vector3(forward.X, 0f, forward.Z));
            var right = new Vector3(fwd.Z, 0f, -fwd.X);
            var to = target - position;
            float lx = Vector3.Dot(to, right);
            float lz = Vector3.Dot(to, fwd);
            // Pure pursuit: curvature = 2 * lateral / distance^2.
            float dist2 = MathF.Max(lx * lx + lz * lz, 1f);
            float curvature = 2f * lx / dist2;
            float wheelAngle = MathF.Atan(curvature * wheelbase);
            float steer = ApexMath.Clamp(wheelAngle / maxSteerRad, -1f, 1f);
            float want = MathF.Min(TargetSpeed(d + v * 0.25f), TargetSpeed(d + 4f));
            float err = want - v;
            float throttle = ApexMath.Clamp01(err * 0.35f + 0.15f);
            float brake = ApexMath.Clamp01(-err * 0.18f);
            if (brake > 0.05f) throttle = 0f;
            return (throttle, brake, steer);
        }
    }
}
