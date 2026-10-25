using System;
using Apex.Core;

namespace Apex.Vehicle
{
    /// <summary>
    /// Combined-slip tire model (normalised-slip "magic formula" variant).
    /// Slip ratio and slip angle are normalised by their peak values and combined
    /// into one slip magnitude that runs through a single Pacejka-style curve. That
    /// yields a friction circle, a clear peak and a gentle fall-off past the limit.
    /// See docs/PHYSICS_MODEL.md.
    /// </summary>
    [Serializable]
    public class TireModel
    {
        /// <summary>Friction coefficient at nominal load.</summary>
        public float muPeak = 1.75f;
        public float slipRatioPeak = 0.10f;
        /// <summary>Slip angle (radians) of peak lateral force.</summary>
        public float slipAnglePeak = 0.12f;
        /// <summary>Pacejka C shape factor; ~1.9 gives a moderate post-peak drop.</summary>
        public float shape = 1.9f;
        /// <summary>Fraction of peak grip retained when fully sliding.</summary>
        public float slideFloor = 0.72f;
        /// <summary>Load (N) at which muPeak applies.</summary>
        public float nominalLoad = 3000f;
        /// <summary>Grip loss per unit relative load increase.</summary>
        public float loadSensitivity = 0.12f;

        float StiffnessB => MathF.Tan(ApexMath.Pi / (2f * shape));

        public float MuAtLoad(float fz)
        {
            float rel = (fz - nominalLoad) / nominalLoad;
            return muPeak * ApexMath.Clamp(1f - loadSensitivity * rel, 0.6f, 1.25f);
        }

        /// <summary>Normalised force curve: 0 at s=0, 1 at s=1, tends to slideFloor.</summary>
        public float Curve(float s)
        {
            if (s <= 1f)
                return MathF.Sin(shape * MathF.Atan(StiffnessB * s));
            float t = 1f - MathF.Exp(-(s - 1f) * 0.9f);
            return ApexMath.Lerp(1f, slideFloor, t);
        }

        /// <summary>Normalised combined slip magnitude (1 = at the grip peak).</summary>
        public float CombinedSlip(float slipRatio, float slipAngle)
        {
            float sx = slipRatio / slipRatioPeak;
            float sy = MathF.Tan(ApexMath.Clamp(slipAngle, -1.4f, 1.4f)) / MathF.Tan(slipAnglePeak);
            return MathF.Sqrt(sx * sx + sy * sy);
        }

        /// <summary>
        /// Tire forces in newtons. Positive slip ratio (wheel faster than ground)
        /// gives positive Fx; positive slip angle (sliding to the right) gives
        /// negative Fy (force to the left).
        /// </summary>
        public void Compute(float fz, float slipRatio, float slipAngle, float surfaceGrip, out float fx, out float fy)
        {
            fx = 0f;
            fy = 0f;
            if (fz <= 0f) return;
            float sx = slipRatio / slipRatioPeak;
            float sy = MathF.Tan(ApexMath.Clamp(slipAngle, -1.4f, 1.4f)) / MathF.Tan(slipAnglePeak);
            float s = MathF.Sqrt(sx * sx + sy * sy);
            if (s < 1e-6f) return;
            float f = MuAtLoad(fz) * surfaceGrip * fz * Curve(s);
            fx = f * sx / s;
            fy = -f * sy / s;
        }
    }
}
