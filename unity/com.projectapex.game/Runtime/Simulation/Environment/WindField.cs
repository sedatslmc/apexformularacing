using System;
using System.Numerics;
using Apex.Core;

namespace Apex.Environment
{
    /// <summary>
    /// One wind for the whole circuit, shared by flags, trees, water and smoke so they
    /// all agree. Default: the prevailing north-north-easterly "poyraz" of the
    /// Bosphorus (from true bearing ~25°), rotated into the game frame with the
    /// circuit's true-north bearing. Speed varies slowly with gusts.
    /// </summary>
    public sealed class WindField
    {
        /// <summary>Game-frame bearing the wind blows towards (deg, 0 = +z, clockwise).</summary>
        public float towardsDeg;
        public float meanSpeed = 5.5f;
        public float gustiness = 0.3f;

        public static WindField ForCircuit(float trueNorthBearing, float meanSpeed = 5.5f)
        {
            const float fromTrue = 25f;
            float north = float.IsNaN(trueNorthBearing) ? 0f : trueNorthBearing;
            return new WindField { towardsDeg = ApexMath.PosMod(fromTrue + 180f + north, 360f), meanSpeed = meanSpeed };
        }

        /// <summary>Unit direction the wind blows towards (x, z).</summary>
        public Vector2 Direction
        {
            get
            {
                float a = towardsDeg * MathF.PI / 180f;
                return new Vector2(MathF.Sin(a), MathF.Cos(a));
            }
        }

        /// <summary>Wind speed (m/s) at time t, with slow variation and gusts (deterministic).</summary>
        public float Speed(double t)
        {
            float slow = LandModel.ValueNoise((float)(t * 0.05), 3.1f);
            float gust = MathF.Max(0f, LandModel.ValueNoise((float)(t * 0.35), 11.7f));
            return MathF.Max(0f, meanSpeed * (1f + 0.25f * slow + gustiness * 1.6f * gust));
        }
    }

    /// <summary>
    /// Analytic flag cloth: the flag droops at low wind and streams out downwind as the
    /// wind rises; travelling waves run from hoist to fly with amplitude growing
    /// towards the free edge and frequency rising with wind speed. Cheap enough to
    /// evaluate per vertex on the CPU for a handful of flags.
    /// </summary>
    public static class FlagCloth
    {
        /// <summary>Angle of the flag below the horizontal (deg) for a wind speed (m/s).</summary>
        public static float DroopDeg(float windSpeed) => ApexMath.Lerp(82f, 5f, 1f - MathF.Exp(-windSpeed / 3.5f));

        /// <summary>
        /// Cloth point for u (0 hoist .. 1 fly) and v (0 top .. 1 bottom of the hoist).
        /// Frame: x downwind (horizontal), y up, z lateral; origin at the top of the hoist.
        /// </summary>
        public static Vector3 Evaluate(float u, float v, float length, float height, float windSpeed, float time, float phase)
        {
            float droop = DroopDeg(windSpeed) * MathF.PI / 180f;
            float k = ApexMath.Clamp01(windSpeed / 10f);
            float s = u * length;
            // Wave amplitude grows towards the fly; ripples also make the flag a little shorter.
            float amp = length * (0.025f + 0.05f * k) * MathF.Pow(u, 1.25f);
            float freq = 0.5f + 0.17f * windSpeed;
            float lambda = length * ApexMath.Lerp(0.9f, 0.5f, k);
            float wave = MathF.Sin(2f * MathF.PI * (s / lambda - freq * time) + phase + v * 0.9f);
            float flutter = 0.25f * MathF.Sin(2f * MathF.PI * (s / (lambda * 0.37f) - freq * 2.3f * time) + phase * 1.7f) * u * u;
            float along = s * (1f - 0.04f * k);
            var dir = new Vector3(MathF.Cos(droop), -MathF.Sin(droop), 0f);
            var p = new Vector3(0f, -v * height, 0f) + dir * along;
            // Lateral wave plus a small vertical component (cloth waves are not planar).
            p.Z += amp * (wave + flutter);
            p.Y += amp * 0.25f * MathF.Cos(2f * MathF.PI * (s / lambda - freq * time) + phase);
            return p;
        }
    }
}
