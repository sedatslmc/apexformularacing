using System.Numerics;

namespace Apex.Vehicle
{
    /// <summary>Per-wheel configuration, contact inputs and simulation outputs.</summary>
    public sealed class WheelState
    {
        // Configuration
        public readonly int index;
        public readonly string label;
        public readonly bool front;
        public readonly bool left;
        public readonly float radius;
        public readonly float inertia;
        public readonly float spring;
        /// <summary>Top of suspension travel, car space (x right, y up, z forward).</summary>
        public readonly Vector3 mount;
        public readonly float staticCompression;

        // Contact inputs (written by the engine layer each step)
        public bool contact;
        /// <summary>Suspension compression from full extension (m).</summary>
        public float compression;
        /// <summary>Contact-point velocity along the steered wheel's forward axis (m/s).</summary>
        public float longitudinalVelocity;
        /// <summary>Contact-point velocity along the wheel's right axis (m/s).</summary>
        public float lateralVelocity;
        /// <summary>Cosine between contact normal and suspension axis (1 = flat).</summary>
        public float normalAlignment = 1f;
        public SurfaceType surface = SurfaceType.Asphalt;
        /// <summary>Extra grip multiplier from weather/track state (1 = dry, clean).</summary>
        public float conditionGrip = 1f;
        /// <summary>When >= 0, used as normal load instead of the spring model (test rigs).</summary>
        public float loadOverride = -1f;

        // Outputs / state
        public float prevCompression;
        public float load;
        public float omega;
        public float spinAngle;
        /// <summary>Steering angle in radians, positive = right.</summary>
        public float steer;
        public float slipRatio;
        public float slipAngle;
        public float fx;
        public float fy;
        public float driveTorque;
        public float brakeTorque;
        public float grip = 1f;
        /// <summary>Extra suspension compression from surface texture (kerb ripple), m.</summary>
        public float roadOffset;
        /// <summary>Road-texture phase (rad) advanced by rolling distance.</summary>
        public float roadPhase;

        public float SpringLength(float travel) => travel - compression;

        public WheelState(int index, VehicleSetup setup)
        {
            this.index = index;
            front = index < 2;
            left = index % 2 == 0;
            label = (front ? "F" : "R") + (left ? "L" : "R");
            radius = front ? setup.wheelRadiusFront : setup.wheelRadiusRear;
            inertia = front ? setup.wheelInertiaFront : setup.wheelInertiaRear;
            spring = front ? setup.springFront : setup.springRear;
            mount = setup.MountPosition(index, out staticCompression);
            compression = staticCompression;
            prevCompression = staticCompression;
        }

        public void ResetDynamics()
        {
            omega = 0f;
            slipRatio = 0f;
            slipAngle = 0f;
            fx = 0f;
            fy = 0f;
            roadOffset = 0f;
            prevCompression = compression;
        }
    }
}
