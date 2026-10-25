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
        public float maxSteerAngleDeg = 22f;
        /// <summary>Lock retained at high speed when the steering assist is on.</summary>
        public float highSpeedSteerDeg = 4.5f;
        public float steerAssistSpeed = 75f;
        /// <summary>Steering rack speed limit at standstill (deg/s at the wheels).</summary>
        public float steerRateDeg = 140f;
        /// <summary>Rack speed limit at high speed when the steering assist is on (deg/s).</summary>
        public float steerRateHighSpeedDeg = 35f;
        /// <summary>Counter-steer assist gain (rad of wheel angle per rad of excess rear slip).</summary>
        public float counterSteerGain = 1.2f;
        public float ackermann = 0.12f;
        /// <summary>Steering-wheel rotation per unit of road-wheel angle (cockpit wheel and hands).</summary>
        public float steeringWheelRatio = 5f;
        public float tireRelaxationLength = 0.35f;
        public int wheelSubsteps = 6;

        // Brakes
        public float maxBrakeTorque = 4000f;
        public float brakeBiasFront = 0.57f;

        // Aero
        public float dragArea = 1.25f;
        public float downforceArea = 3.6f;
        public float aeroBalanceFront = 0.43f;
        public float airDensity = 1.225f;

        // Ground effect (approximation, see docs/PHYSICS_MODEL.md)
        /// <summary>Share of <see cref="downforceArea"/> produced by the floor at the reference ride height.</summary>
        public float floorShare = 0.5f;
        /// <summary>Plank-to-ground height at static load, front and rear (rake) in metres.</summary>
        public float rideHeightFront = 0.035f;
        public float rideHeightRear = 0.075f;
        /// <summary>Mean ride height at which the floor delivers its nominal share
        /// (typical at ~150 km/h, the speed the racing line is planned around); lower is stronger, higher is weaker.</summary>
        public float floorReferenceHeight = 0.045f;
        /// <summary>Floor downforce scales with (reference / ride height)^exponent.</summary>
        public float groundEffectExponent = 0.3f;
        public float floorMaxGain = 1.6f;
        /// <summary>Below this mean height the floor flow starts to stall.</summary>
        public float floorStallHeight = 0.01f;
        /// <summary>Fraction of floor downforce lost when the plank is on the ground.</summary>
        public float floorStallLoss = 0.3f;
        /// <summary>Front aero balance shift per metre of rake change (dive moves it forward).</summary>
        public float aeroRakeSensitivity = 1.0f;
        /// <summary>Extra drag per unit of floor gain above nominal (induced drag).</summary>
        public float inducedDragFactor = 0.05f;

        // Drivetrain
        public float diffLockRate = 220f;
        public float diffPreload = 120f;
        public float diffLockRatio = 0.35f;
        public float launchRpm = 7500f;
        /// <summary>Torque capacity of the fully closed clutch (N·m).</summary>
        public float clutchMaxTorque = 1400f;

        // Assists
        /// <summary>TC acts when a driven tire's combined normalised slip exceeds this (1 = grip peak).</summary>
        public float tcCombinedThreshold = 1.0f;
        public float absSlipThreshold = 0.12f;

        /// <summary>Front tires (narrower).</summary>
        public TireModel tireFront = new TireModel { nominalLoad = 2800f };
        /// <summary>Rear tires: wider, so a higher nominal load and slightly more grip.
        /// This keeps the rear from being the weaker axle despite carrying 55 % of the
        /// weight, which is what makes real open-wheelers stable on turn-in.</summary>
        public TireModel tireRear = new TireModel { nominalLoad = 3900f, muPeak = 1.8f, slipAnglePeak = 0.11f };
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
