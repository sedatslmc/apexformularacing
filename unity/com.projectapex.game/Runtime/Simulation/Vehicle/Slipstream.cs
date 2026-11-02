using System;
using System.Collections.Generic;
using System.Numerics;
using Apex.Core;

namespace Apex.Vehicle
{
    /// <summary>
    /// Slipstream and dirty air (docs/PHYSICS_MODEL.md, "Windschatten und Dirty Air"). A car leaves a wake of slow,
    /// turbulent air behind it:
    /// <list type="bullet">
    /// <item>Tow: the follower meets slower air, so its drag drops — about 11 % at 20 m (worth roughly 10 km/h
    /// at the end of a long straight), still a few per cent at 40–60 m, gone beyond ~100 m.</item>
    /// <item>Dirty air: the wings and floor work in turbulent, slower flow and lose downforce, the front wing more
    /// than the floor and rear wing, so the follower understeers in fast corners. The loss is strongest close
    /// behind and fades faster with distance than the tow (current-generation ground-effect cars: roughly 15–20 %
    /// at 20 m, not the 35–50 % of older designs).</item>
    /// </list>
    /// The wake is a cone behind the leader: it widens with distance, so sitting a car-width to the side of it leaves
    /// clean air (the classic move out of the tow to overtake). The wake grows with the leader's speed (none below
    /// ~70 km/h). Several cars ahead combine as 1 − Π(1 − x).
    /// </summary>
    public static class Slipstream
    {
        /// <summary>Largest drag reduction right behind a car, and its decay length (m).</summary>
        public const float MaxTow = 0.25f, TowLength = 25f;
        /// <summary>Largest total downforce loss right behind a car, and its decay length (m).</summary>
        public const float MaxDirtyAir = 0.3f, DirtyAirLength = 22f;
        /// <summary>Share of the loss on the front axle relative to the rear (front wing hit hardest).</summary>
        public const float FrontShare = 1.35f, RearShare = 0.7f;
        /// <summary>Wake half-width at the car (m) and its growth per metre behind.</summary>
        public const float WakeHalfWidth = 0.9f, WakeSpread = 0.045f;
        public const float MaxRange = 120f;
        /// <summary>Leader speed (m/s) below which there is no useful wake, and above which it is full.</summary>
        public const float WakeSpeedMin = 20f, WakeSpeedFull = 45f;

        public struct Effect
        {
            /// <summary>Drag reduction 0..1 (fraction of the car's drag).</summary>
            public float tow;
            /// <summary>Downforce loss 0..1 on the front and rear axle.</summary>
            public float frontLoss, rearLoss;
            /// <summary>Distance to the car whose wake matters most (m), or infinity.</summary>
            public float nearest;

            public static readonly Effect None = new Effect { nearest = float.PositiveInfinity };
        }

        /// <summary>
        /// Wake of one leader at the follower's position: behind distance <paramref name="behind"/> (m, along the
        /// leader's heading; positive = behind it) and lateral offset <paramref name="lateral"/> (m).
        /// </summary>
        public static Effect Wake(float behind, float lateral, float leaderSpeed)
        {
            if (behind <= 0.5f || behind > MaxRange) return Effect.None;
            float strength = ApexMath.Clamp01((leaderSpeed - WakeSpeedMin) / (WakeSpeedFull - WakeSpeedMin));
            if (strength <= 0f) return Effect.None;
            float half = WakeHalfWidth + WakeSpread * behind;
            float side = MathF.Exp(-(lateral * lateral) / (half * half));
            // Very close behind (under a car length) the follower's nose sits in the leader's recirculation: the
            // tow saturates rather than growing further.
            float d = MathF.Max(behind, 3f);
            float tow = MaxTow * MathF.Exp(-d / TowLength) * side * strength;
            float loss = MaxDirtyAir * MathF.Exp(-d / DirtyAirLength) * side * strength;
            return new Effect
            {
                tow = tow,
                frontLoss = ApexMath.Clamp01(loss * FrontShare),
                rearLoss = ApexMath.Clamp01(loss * RearShare),
                nearest = behind,
            };
        }

        /// <summary>
        /// Combined effect on a follower from all other cars (positions and forward directions in the ground plane;
        /// y is ignored). <paramref name="self"/> is the follower's index in the lists.
        /// </summary>
        public static Effect For(int self, IReadOnlyList<Vector3> positions, IReadOnlyList<Vector3> forwards, IReadOnlyList<float> speeds)
        {
            float keepTow = 1f, keepFront = 1f, keepRear = 1f, nearest = float.PositiveInfinity;
            var me = positions[self];
            var myFwd = Flat(forwards[self]);
            for (int i = 0; i < positions.Count; i++)
            {
                if (i == self) continue;
                var lp = positions[i];
                float dx = me.X - lp.X, dz = me.Z - lp.Z;
                if (dx * dx + dz * dz > MaxRange * MaxRange) continue;
                var lf = Flat(forwards[i]);
                // Only a car driving the same way: a car coming the other way (pit lane, spin) leaves no tow.
                if (Vector2.Dot(lf, myFwd) < 0.5f) continue;
                var rel = new Vector2(dx, dz);
                float behind = -Vector2.Dot(rel, lf);
                float lateral = lf.X * rel.Y - lf.Y * rel.X;
                var e = Wake(behind, lateral, speeds[i]);
                if (e.tow <= 0f && e.frontLoss <= 0f) continue;
                keepTow *= 1f - e.tow;
                keepFront *= 1f - e.frontLoss;
                keepRear *= 1f - e.rearLoss;
                nearest = MathF.Min(nearest, e.nearest);
            }
            return new Effect { tow = 1f - keepTow, frontLoss = 1f - keepFront, rearLoss = 1f - keepRear, nearest = nearest };
        }

        static Vector2 Flat(Vector3 v)
        {
            var f = new Vector2(v.X, v.Z);
            float l = f.Length();
            return l > 1e-5f ? f / l : new Vector2(0f, 1f);
        }
    }
}
