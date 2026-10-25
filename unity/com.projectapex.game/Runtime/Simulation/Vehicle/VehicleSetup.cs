using System;
using System.Numerics;

namespace Apex.Vehicle
{
    /// <summary>
    /// Authored configuration of a car. Coordinates follow Unity's convention:
    /// x = right, y = up, z = forward, origin at the centre of mass.
    /// </summary>
    [Serializable]
    public class VehicleSetup
    {
        public string carName = "Apex FA-1";

        // Chassis
        public float mass = 800f;
        /// <summary>Principal inertia (x = pitch, y = yaw, z = roll) in kg·m².</summary>
        public float inertiaPitch = 1100f;
        public float inertiaYaw = 1250f;
        public float inertiaRoll = 320f;
        public float wheelbase = 3.6f;
        /// <summary>Fraction of static weight on the front axle.</summary>
        public float weightFront = 0.45f;
        public float trackFront = 1.62f;
        public float trackRear = 1.56f;
        /// <summary>Centre-of-mass height above ground at static ride height.</summary>
        public float cogHeight = 0.30f;

        // Suspension
        public float suspensionTravel = 0.09f;
        public float springFront = 140000f;
        public float springRear = 125000f;
        public float damperBump = 6000f;
        public float damperRebound = 8500f;
        public float antiRollFront = 60000f;
        public float antiRollRear = 40000f;
        public float bumpStopRate = 900000f;

        // Wheels and steering
        public float wheelRadiusFront = 0.355f;
        public float wheelRadiusRear = 0.36f;
        public float wheelInertiaFront = 1.1f;
        public float wheelInertiaRear = 1.6f;
        public float maxSteerAngleDeg = 19f;
        /// <summary>Lock retained at high speed when the steering assist is on.</summary>
        public float highSpeedSteerDeg = 4.5f;
        public float steerAssistSpeed = 75f;
        /// <summary>Steering rack speed limit (deg/s at the wheels).</summary>
        public float steerRateDeg = 120f;
        public float ackermann = 0.12f;
        public float tireRelaxationLength = 0.35f;
        public int wheelSubsteps = 6;

        // Brakes
        public float maxBrakeTorque = 5200f;
        public float brakeBiasFront = 0.57f;

        // Aero
        public float dragArea = 1.25f;
        public float downforceArea = 3.6f;
        public float aeroBalanceFront = 0.43f;
        public float airDensity = 1.225f;

        // Drivetrain
        public float diffLockRate = 220f;
        public float diffPreload = 120f;
        public float diffLockRatio = 0.35f;
        public float launchRpm = 7500f;

        // Assists
        /// <summary>TC acts when a driven tire's combined normalised slip exceeds this (1 = grip peak).</summary>
        public float tcCombinedThreshold = 1.0f;
        public float absSlipThreshold = 0.12f;

        public TireModel tire = new TireModel();
        public EngineModel engine = new EngineModel();
        public Gearbox gearbox = new Gearbox();

        public float FrontAxleZ => wheelbase * (1f - weightFront);
        public float RearAxleZ => -wheelbase * weightFront;

        /// <summary>Wheel mount point (top of suspension travel) in car space.
        /// Index order: 0 FL, 1 FR, 2 RL, 3 RR.</summary>
        public Vector3 MountPosition(int index, out float staticCompression)
        {
            bool front = index < 2;
            bool left = index % 2 == 0;
            float radius = front ? wheelRadiusFront : wheelRadiusRear;
            float spring = front ? springFront : springRear;
            float axleShare = front ? weightFront : 1f - weightFront;
            staticCompression = mass * 9.81f * axleShare * 0.5f / spring;
            float wheelCentreY = radius - cogHeight;
            float mountY = wheelCentreY + (suspensionTravel - staticCompression);
            float halfTrack = (front ? trackFront : trackRear) * 0.5f;
            return new Vector3(left ? -halfTrack : halfTrack, mountY, front ? FrontAxleZ : RearAxleZ);
        }
    }
}
