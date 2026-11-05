using System;
using System.Numerics;
using Apex.Core;

namespace Apex.Race
{
    /// <summary>
    /// Podium ceremony (docs/UI_UX_OVERHAUL.md 13.4): the top three stand on their steps in their team suits, the winner
    /// lifts the trophy, team banners wave behind them and confetti comes down from two cannons on the back wall.
    /// Everything in the podium frame of EventGeometry.Podium (origin on the ground at the centre, +y up, the front
    /// facing -z towards the track). Pure maths, tested without Unity; PodiumCeremonyComponent draws it.
    /// </summary>
    public static class PodiumCeremony
    {
        /// <summary>Feet of P1, P2, P3 on the step tops (EventGeometry.Podium: gold 2.1 m, ivory 1.5 m and 1.1 m).</summary>
        public static readonly Vector3[] Steps = { new Vector3(0f, 2.1f, 0.45f), new Vector3(-2.6f, 1.5f, 0.45f), new Vector3(2.6f, 1.1f, 0.45f) };
        /// <summary>The figures face the track (-z); yaw as in PitCrewChoreography (0 = +z, clockwise).</summary>
        public const float Yaw = MathF.PI;
        /// <summary>Banner poles in front of the back wall (P2, P1, P3 from the viewer's left), pole height and banner size.</summary>
        public static readonly float[] BannerX = { -4.6f, 0f, 4.6f };
        public const float BannerZ = -1.75f, PoleHeight = 6.2f, BannerWidth = 1.7f, BannerHeight = 1.05f;
        public const int ConfettiCount = 360;
        public const float ConfettiSeconds = 9f;
        const float Floor = 0.62f;

        /// <summary>
        /// The winner's hands in the figure frame at <paramref name="t"/> s into the ceremony: the trophy at the chest,
        /// then lifted over the head (from 0.8 s, 0.9 s long) and pumped gently. With reduced motion it is up at once and still.
        /// </summary>
        public static Vector3 WinnerHands(float t, bool reducedMotion)
        {
            var low = new Vector3(0f, 1.12f, 0.32f);
            var high = new Vector3(0f, 2.0f, 0.12f);
            if (reducedMotion) return high;
            float k = ApexMath.Clamp01((t - 0.8f) / 0.9f);
            k = k * k * (3f - 2f * k);
            var h = Vector3.Lerp(low, high, k);
            if (k >= 1f) h.Y -= 0.06f * (0.5f - 0.5f * MathF.Cos((t - 1.7f) * 2.4f));
            return h;
        }

        /// <summary>Trophy base below the hands (the hands hold it at the pylons, 0.12 m above the base).</summary>
        public static Vector3 TrophyBase(Vector3 handsPodium) => handsPodium - new Vector3(0f, 0.12f, 0f);

        /// <summary>Figure-frame point to the podium frame for step <paramref name="place"/> (0 = P1).</summary>
        public static Vector3 ToPodium(int place, Vector3 figurePoint) => Steps[place] + PitCrewChoreography.Rotate(figurePoint, Yaw);

        static float Hash(int i, int k)
        {
            uint h = (uint)(i * 73856093) ^ (uint)(k * 19349663) ^ 0x9E3779B9u;
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return (h & 0xFFFFFF) / (float)0x1000000;
        }

        /// <summary>Colour slot of a confetti piece (0..3: gold, red, ivory, turquoise).</summary>
        public static int ConfettiColour(int i) => (int)(Hash(i, 7) * 4f) & 3;

        /// <summary>
        /// Confetti piece <paramref name="i"/> at <paramref name="t"/> s: shot from a cannon at the top corners of the back
        /// wall towards the centre with drag, then fluttering down at about 1 m/s; it lies on the podium floor once down.
        /// Returns false before its launch. <paramref name="spin"/> is a rotation angle (rad) for the tumbling.
        /// </summary>
        public static bool Confetti(int i, float t, out Vector3 position, out float spin)
        {
            float launch = Hash(i, 1) * 2.4f + (i % 2) * 0.15f;
            float a = t - launch;
            position = Vector3.Zero;
            spin = 0f;
            if (a < 0f) return false;
            float side = i % 2 == 0 ? -1f : 1f;
            var p0 = new Vector3(side * 6.4f, 6.6f, -1.9f);
            // Out of the cannon: towards the centre and the front, upward.
            var v0 = new Vector3(-side * (2.5f + 3.5f * Hash(i, 2)), 2f + 3f * Hash(i, 3), 1.2f + 3.2f * Hash(i, 4));
            const float drag = 2.2f, fall = 1.05f;
            float e = (1f - MathF.Exp(-drag * a)) / drag;
            position = p0 + v0 * e;
            position.Y -= fall * (a - e);
            float phase = Hash(i, 5) * 6.283f, f = 1.6f + 1.4f * Hash(i, 6);
            position.X += 0.35f * MathF.Sin(a * f + phase) * ApexMath.Clamp01(a);
            position.Z += 0.25f * MathF.Cos(a * f * 0.8f + phase) * ApexMath.Clamp01(a);
            if (position.Y <= Floor)
            {
                position.Y = Floor + 0.004f * Hash(i, 8);
                spin = phase;
                return true;
            }
            spin = a * f * 2.2f + phase;
            return true;
        }

        /// <summary>Banner wave: offset along z of a cloth point (u 0 at the pole .. 1 free end, v 0 bottom .. 1 top).</summary>
        public static float BannerWave(float u, float v, float t, int banner) =>
            u * (0.12f * MathF.Sin(t * 3.1f - u * 5.5f + banner * 1.3f) + 0.04f * MathF.Sin(t * 5.3f - u * 9f + v * 2f));
    }
}
