using System;
using System.Numerics;
using Apex.Core;

namespace Apex.Vehicle
{
    /// <summary>
    /// Optional damage model. Impacts (car-space contact point and velocity change)
    /// damage the part they hit; damage has measurable consequences in the
    /// simulation (aero loss, pulling to one side, punctures) and is repaired in the
    /// pit. No random failures: damage only comes from contact.
    /// </summary>
    [Serializable]
    public sealed class DamageState
    {
        /// <summary>Velocity change (m/s) below which a hit leaves no damage.</summary>
        public const float Threshold = 3f;

        /// <summary>0 intact .. 1 destroyed / lost.</summary>
        public float frontWing;
        public float rearWing;
        public float floor;
        /// <summary>Static toe error per wheel (rad, + = toe to the right) from bent suspension.</summary>
        public readonly float[] toe = new float[4];
        public readonly bool[] puncture = new bool[4];

        public bool Any
        {
            get
            {
                if (frontWing > 0.02f || rearWing > 0.02f || floor > 0.02f) return true;
                for (int i = 0; i < 4; i++) if (MathF.Abs(toe[i]) > 0.001f || puncture[i]) return true;
                return false;
            }
        }

        public bool NeedsNewNose => frontWing > 0.25f;

        /// <summary>Front downforce multiplier (a lost wing keeps a little from the nose/floor).</summary>
        public float FrontAeroFactor => 1f - 0.65f * frontWing;
        public float RearAeroFactor => 1f - 0.5f * rearWing;
        public float FloorFactor => 1f - 0.35f * floor;
        public float DragFactor => 1f + 0.06f * frontWing + 0.04f * rearWing;

        public void Repair(bool newTires)
        {
            frontWing = rearWing = floor = 0f;
            for (int i = 0; i < 4; i++)
            {
                toe[i] = 0f;
                if (newTires) puncture[i] = false;
            }
        }

        /// <summary>
        /// Applies an impact. point: contact in car space (x right, z forward);
        /// deltaV: velocity change of the car from the impulse (m/s); direction:
        /// impulse direction in car space (unit). Returns a short description or null.
        /// </summary>
        public string ApplyImpact(VehicleSetup setup, Vector3 point, float deltaV, Vector3 direction)
        {
            if (deltaV < Threshold) return null;
            float severity = ApexMath.Clamp01((deltaV - Threshold) / 9f);
            string hit = null;
            float zf = setup.FrontAxleZ, zr = setup.RearAxleZ;
            // Wheels: closest corner within reach of the contact.
            int wheel = -1;
            float best = 0.55f;
            for (int i = 0; i < 4; i++)
            {
                var m = setup.MountPosition(i, out _);
                float d = MathF.Sqrt((m.X - point.X) * (m.X - point.X) + (m.Z - point.Z) * (m.Z - point.Z));
                if (d < best) { best = d; wheel = i; }
            }
            if (wheel >= 0)
            {
                // A sideways blow bends the suspension; toe error grows with severity.
                float side = MathF.Abs(direction.X) > 0.3f ? MathF.Sign(direction.X) : (wheel % 2 == 0 ? 1f : -1f);
                toe[wheel] = ApexMath.Clamp(toe[wheel] + side * severity * 0.035f, -0.05f, 0.05f);
                if (deltaV > 9f) puncture[wheel] = true;
                hit = deltaV > 9f ? "Reifenschaden" : "Radaufhängung beschädigt";
            }
            else if (point.Z > zf + 0.25f)
            {
                frontWing = ApexMath.Clamp01(frontWing + severity * 0.9f);
                hit = frontWing >= 0.99f ? "Frontflügel verloren" : "Frontflügel beschädigt";
            }
            else if (point.Z < zr - 0.35f)
            {
                rearWing = ApexMath.Clamp01(rearWing + severity * 0.7f);
                hit = "Heckflügel beschädigt";
            }
            else
            {
                floor = ApexMath.Clamp01(floor + severity * 0.5f);
                hit = "Unterboden beschädigt";
            }
            return hit;
        }
    }
}
