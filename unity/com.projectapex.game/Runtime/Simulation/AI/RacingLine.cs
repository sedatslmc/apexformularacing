using System;
using System.Numerics;
using Apex.Core;
using Apex.Tracks;

namespace Apex.AI
{
    /// <summary>
    /// Racing line as a lateral offset from the centreline per track sample,
    /// computed by iterative minimum-curvature smoothing inside the usable width
    /// (outside-inside-outside emerges naturally), plus a speed profile limited by
    /// grip incl. downforce with braking and traction passes. Shared by every AI
    /// car and by the optional braking-line overlay.
    /// </summary>
    public sealed class RacingLine
    {
        public readonly TrackGeometry geometry;
        /// <summary>Lateral offset (+ right) per centreline sample.</summary>
        public readonly float[] offsets;
        /// <summary>Signed curvature of the line per sample (+ right).</summary>
        public readonly float[] curvature;
        /// <summary>Base speed profile (m/s) at grip usage 1.0.</summary>
        public readonly float[] speed;
        /// <summary>Grip-limited part of the profile (m/s): cornering and braking only, without the
        /// top-speed cap and the acceleration limit. Only this part scales with the driver's grip.</summary>
        public readonly float[] gripSpeed;
        /// <summary>Top speed of the profile (m/s); straights run at this cap.</summary>
        public const float MaxSpeed = 95f;

        public float Mu { get; }
        public float DownforcePerMass { get; }

        /// <param name="edgeMargin">Distance kept from the track edge (car half width + safety).</param>
        /// <summary>Longitudinal share of the friction budget used for braking (planner). 0.85 is the conservative
        /// layout reference; CarPerformance.brakeShare plans with the car's measured braking.</summary>
        public readonly float BrakeShare;

        public RacingLine(TrackGeometry geometry, float mu, float downforcePerMass, float edgeMargin = 1.6f, int iterations = 1800, float brakeShare = 0.85f)
        {
            this.geometry = geometry;
            Mu = mu;
            DownforcePerMass = downforcePerMass;
            BrakeShare = brakeShare;
            int n = geometry.Count;
            offsets = new float[n];
            curvature = new float[n];
            speed = new float[n];
            gripSpeed = new float[n];
            var limit = new float[n];
            for (int i = 0; i < n; i++) limit[i] = MathF.Max(0f, geometry.Widths[i] * 0.5f - edgeMargin);
            var next = new float[n];
            // Minimum-curvature smoothing: move each point towards the biharmonic
            // estimate (-p[i-2s] + 4p[i-s] + 4p[i+s] - p[i+2s]) / 6, which removes
            // second differences (curvature) rather than length. Plain midpoint
            // averaging would give the shortest path, which hugs the inside of
            // hairpins and is tighter than the centreline. Coarse strides first let
            // the line move across whole corners; fine strides remove kinks.
            int perStride = Math.Max(20, iterations / 6);
            foreach (int stride in new[] { 32, 16, 8, 4, 2, 1 })
            for (int it = 0; it < perStride; it++)
            {
                for (int i = 0; i < n; i++)
                {
                    var pm2 = Point(((i - 2 * stride) % n + n) % n);
                    var pm1 = Point(((i - stride) % n + n) % n);
                    var pp1 = Point((i + stride) % n);
                    var pp2 = Point((i + 2 * stride) % n);
                    var est = (-pm2 + 4f * pm1 + 4f * pp1 - pp2) / 6f;
                    float desired = Vector3.Dot(est - geometry.Positions[i], geometry.Rights[i]);
                    next[i] = ApexMath.Clamp(ApexMath.Lerp(offsets[i], desired, 0.5f), -limit[i], limit[i]);
                }
                Array.Copy(next, offsets, n);
            }
            ComputeCurvature();
            BuildSpeedProfile();
        }

        public Vector3 Point(int i) => geometry.Positions[i] + geometry.Rights[i] * offsets[i];

        public float OffsetAt(float distance)
        {
            float x = geometry.Wrap(distance) / geometry.Spacing;
            int i = (int)x % geometry.Count;
            int j = (i + 1) % geometry.Count;
            return ApexMath.Lerp(offsets[i], offsets[j], x - MathF.Floor(x));
        }

        public float SpeedAt(float distance) => speed[geometry.IndexAt(distance)];

        public float GripSpeedAt(float distance) => gripSpeed[geometry.IndexAt(distance)];

        void ComputeCurvature()
        {
            int n = geometry.Count;
            var bearing = new float[n];
            var seg = new float[n];
            for (int i = 0; i < n; i++)
            {
                var d = Point((i + 1) % n) - Point((i - 1 + n) % n);
                bearing[i] = MathF.Atan2(d.X, d.Z);
                seg[i] = MathF.Max(new Vector2(d.X, d.Z).Length() * 0.5f, 0.1f);
            }
            for (int i = 0; i < n; i++)
            {
                int a = (i - 2 + n) % n, b = (i + 2) % n;
                float db = ApexMath.WrapAngle(bearing[b] - bearing[a]);
                curvature[i] = db / (seg[a] + seg[i] * 2f + seg[b]);
            }
        }

        void BuildSpeedProfile()
        {
            int n = geometry.Count;
            float ds = geometry.Spacing;
            GripProfile(Mu, gripSpeed);
            for (int i = 0; i < n; i++) speed[i] = MathF.Min(MaxSpeed, gripSpeed[i]);
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < n; i++)
                {
                    float v = speed[i];
                    float accel = AccelAt(v, geometry.Distances[i]);
                    int j = (i + 1) % n;
                    speed[j] = MathF.Min(speed[j], MathF.Sqrt(v * v + 2f * accel * ds));
                }
        }

        /// <summary>Planner acceleration (m/s²) at a speed and lap distance: traction and power limit, minus the grade.</summary>
        public float AccelAt(float v, float distance) =>
            MathF.Max(0.5f, ApexMath.Clamp(14f - v * 0.09f, 3f, 14f) - 9.81f * geometry.GradeAt(distance));

        /// <summary>Planner braking deceleration (m/s²) available at a speed and lap distance in a straight line.</summary>
        public float BrakeDecelAt(float v, float distance) =>
            MathF.Max(1f, Mu * (9.81f + DownforcePerMass * v * v) * BrakeShare + 9.81f * geometry.GradeAt(distance));

        /// <summary>Line curvature (1/m, + right) at a lap distance.</summary>
        public float CurvatureAt(float distance) => curvature[geometry.IndexAt(distance)];

        /// <summary>Cornering and braking limit (m/s) per sample for a friction coefficient mu.</summary>
        void GripProfile(float mu, float[] profile)
        {
            int n = geometry.Count;
            const float g = 9.81f;
            for (int i = 0; i < n; i++)
            {
                float k = 0f;
                for (int o = -2; o <= 2; o++) k = MathF.Max(k, MathF.Abs(curvature[((i + o) % n + n) % n]));
                float denom = k - mu * DownforcePerMass;
                profile[i] = denom <= 1e-5f ? 400f : MathF.Min(400f, MathF.Sqrt(mu * g / denom));
            }
            BrakingPass(profile, mu);
        }

        readonly System.Collections.Generic.Dictionary<int, float[]> scaledGrip = new System.Collections.Generic.Dictionary<int, float[]>();

        /// <summary>
        /// Grip-limited speed (m/s) with the tyre grip scaled by <paramref name="gripScale"/> (wet track), from
        /// a profile recomputed with the lower friction (quantised to 1/40, cached). Unlike scaling the dry
        /// profile by sqrt(grip), this keeps the downforce physics: at the lower corner speed there is less
        /// downforce too, so aero-loaded corners and corners in a braking zone lose more than sqrt(grip).
        /// </summary>
        public float GripSpeedAt(float distance, float gripScale)
        {
            int key = (int)MathF.Round(ApexMath.Clamp(gripScale, 0.1f, 1f) * 40f);
            if (key >= 40) return GripSpeedAt(distance);
            if (!scaledGrip.TryGetValue(key, out var prof))
            {
                prof = new float[geometry.Count];
                GripProfile(Mu * key / 40f, prof);
                scaledGrip[key] = prof;
            }
            return prof[geometry.IndexAt(distance)];
        }

        void BrakingPass(float[] profile, float mu)
        {
            int n = geometry.Count;
            const float g = 9.81f;
            float ds = geometry.Spacing;
            // Longitudinal grade (+ uphill): gravity helps braking uphill and hurts it downhill, and the
            // other way round when accelerating.
            for (int pass = 0; pass < 2; pass++)
                for (int step = 0; step < n; step++)
                {
                    int i = n - 1 - step;
                    float v = profile[(i + 1) % n];
                    // Friction circle: braking gets what the cornering at this point leaves of the grip.
                    float aTotal = mu * (g + DownforcePerMass * v * v);
                    float aLat = v * v * MathF.Abs(curvature[i]);
                    float aLong = MathF.Sqrt(MathF.Max(0f, aTotal * aTotal - aLat * aLat));
                    float decel = MathF.Max(1f, aLong * BrakeShare + g * geometry.GradeAt(geometry.Distances[i]));
                    profile[i] = MathF.Min(profile[i], MathF.Sqrt(v * v + 2f * decel * ds));
                }
        }

        /// <summary>Total line length (m), shorter than the centreline when corners are cut.</summary>
        public float LineLength()
        {
            float len = 0f;
            for (int i = 0; i < geometry.Count; i++) len += Vector3.Distance(Point(i), Point((i + 1) % geometry.Count));
            return len;
        }
    }
}
