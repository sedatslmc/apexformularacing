using System;
using System.Collections.Generic;
using System.Numerics;
using Apex.Core;

namespace Apex.Environment
{
    /// <summary>
    /// Wakes of the Bosphorus traffic (docs/ISTANBUL_OVERHAUL.md, wakes section). The routes are pure kinematics
    /// (<see cref="MarineRoute.Sample"/>), so the wake is read from the vessel's own past instead of a history buffer:
    /// for every age a the vessel's position, heading and speed at t − a give
    /// <list type="bullet">
    /// <item>the turbulent centre wake behind the stern: white water as wide as the beam, spreading slowly, fading
    /// over the vessel's wake lifetime;</item>
    /// <item>the two arms of the Kelvin pattern from the bow: in deep water the wave crests trail at
    /// 19.47° (asin 1/3) from the course, independent of speed, so a crest emitted a seconds ago lies
    /// v·a·tan 19.47° to the side of the course.</item>
    /// </list>
    /// Foam strength grows with the length Froude number (a planing motorboat throws much more white water than a
    /// ferry at the same speed). Stopped vessels leave nothing new; old wake fades out.
    /// </summary>
    public static class ShipWake
    {
        public const float KelvinHalfAngleDeg = 19.47f;
        public static readonly float KelvinTan = MathF.Tan(KelvinHalfAngleDeg * MathF.PI / 180f);
        public const float Gravity = 9.81f;
        /// <summary>Spreading of the turbulent wake (m of half width per second of age).</summary>
        public const float Spread = 0.35f;
        /// <summary>Kelvin arms are drawn out to this lateral distance (m); beyond it the crests are too low to see.</summary>
        public const float MaxArmOffset = 180f;

        public struct Sample
        {
            /// <summary>Where the stern was at this age (centre wake) and the bow (start of the Kelvin arms).</summary>
            public Vector2 stern, bow;
            /// <summary>Unit vector to the vessel's right at this age.</summary>
            public Vector2 right;
            public float age, halfWidth, armOffset;
            /// <summary>Foam opacity 0..1 of the centre wake and of the Kelvin arms.</summary>
            public float foam, armFoam;
        }

        /// <summary>Hull length and beam (m) of a vessel kind (LandmarkGeometry hulls).</summary>
        public static (float length, float beam) Hull(int kind)
        {
            switch (kind)
            {
                case MarineRoute.VesselFreighter: return (120f, 18f);
                case MarineRoute.VesselTourBoat: return (28f, 6.5f);
                case MarineRoute.VesselMotorBoat: return (9f, 2.8f);
                default: return (70f, 13.5f);
            }
        }

        /// <summary>Seconds the wake stays visible.</summary>
        public static float Lifetime(int kind)
        {
            switch (kind)
            {
                case MarineRoute.VesselFreighter: return 70f;
                case MarineRoute.VesselTourBoat: return 40f;
                case MarineRoute.VesselMotorBoat: return 25f;
                default: return 55f;
            }
        }

        public static float Froude(float speed, float length) => speed / MathF.Sqrt(Gravity * MathF.Max(length, 1f));

        /// <summary>Foam strength of fresh wake at a speed: none when stopped, more for faster, shorter hulls.</summary>
        public static float Strength(float speed, float length)
        {
            if (speed < 0.5f) return 0f;
            float fr = Froude(speed, length);
            return ApexMath.Clamp(0.25f + 0.9f * fr, 0f, 1f) * ApexMath.Clamp01((speed - 0.5f) / 2f);
        }

        /// <summary>
        /// Wake samples from age 0 to the lifetime in steps of <paramref name="step"/> seconds. The list ends early
        /// where the route jumps (a transit re-entering at the other end of the strait).
        /// </summary>
        public static void Trail(MarineRoute route, double time, float offset, float step, List<Sample> samples)
        {
            samples.Clear();
            var (length, beam) = Hull(route.vesselKind);
            float life = Lifetime(route.vesselKind);
            Vector2 prev = default;
            for (float a = 0f; a <= life + 1e-3f; a += step)
            {
                route.Sample(time - a, offset, out var p, out float heading, out float v);
                if (a > 0f && Vector2.Distance(p, prev) > MathF.Max(route.speed, 1f) * step * 3f + 5f) break;
                prev = p;
                var fwd = new Vector2(MathF.Sin(heading), MathF.Cos(heading));
                var right = new Vector2(fwd.Y, -fwd.X);
                float fade = MathF.Exp(-3f * a / life) * ApexMath.Clamp01((life - a) / (0.15f * life));
                float s = Strength(v, length);
                samples.Add(new Sample
                {
                    stern = p - fwd * (0.5f * length),
                    bow = p + fwd * (0.45f * length),
                    right = right,
                    age = a,
                    halfWidth = 0.5f * beam + Spread * a,
                    armOffset = MathF.Min(MaxArmOffset, v * a * KelvinTan),
                    foam = s * fade,
                    armFoam = 0.45f * s * fade * ApexMath.Clamp01(Froude(v, length) / 0.25f),
                });
            }
        }
    }
}
