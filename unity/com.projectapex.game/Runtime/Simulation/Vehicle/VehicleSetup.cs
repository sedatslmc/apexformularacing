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
        // Stiffer than before (140/125 kN/m) in proportion to the higher downforce, so the aero
        // compression and the body clearance at speed stay about the same (HandlingBenchmark, limits).
        public float springFront = 190000f;
        public float springRear = 170000f;
        public float damperBump = 6000f;
        public float damperRebound = 8500f;
        // Roll stiffness (handling brief 2026-11, docs/HANDLING_KEYBOARD.md): the October 25/80 kN/m put most of the
        // load transfer on the rear to hide the keyboard understeer – which really came from held keys steering the
        // front past its grip peak. With the front-slip limiter fixing that at its source, a rear-heavy roll
        // stiffness only made the rear the weak axle in direction changes and under trail braking (spins in the
        // keyboard slalom at 200 km/h, rear slip ~2 × peak when braking into fast corners). 50/50 kN/m: the front takes
        // keyboard slalom at 200 km/h, rear slip ~2 × peak when braking into fast corners). 30/70 kN/m with the aero
        // balance moved back (0.44 → 0.41): slow corners are set by the mechanical balance and keep the rear
        // working (rotation), fast corners by the aero balance, which now holds the rear down (stability).
        // Measured with KeyboardHandlingProbe; see docs/HANDLING_KEYBOARD.md.
        public float antiRollFront = 30000f;
        public float antiRollRear = 70000f;
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
        public float steerRateDeg = 160f;
        /// <summary>Rack speed limit at high speed when the steering assist is on (deg/s).</summary>
        public float steerRateHighSpeedDeg = 70f;
        /// <summary>Counter-steer assist gain (rad of wheel angle per rad of excess rear slip).</summary>
        public float counterSteerGain = 1.2f;
        /// <summary>Ackermann share (0 = parallel steer). Pro-Ackermann over-steered the unloaded inside
        /// front past its peak at the limit; open-wheelers run close to parallel.</summary>
        public float ackermann = 0f;
        /// <summary>Steering-wheel rotation per unit of road-wheel angle (cockpit wheel and hands).</summary>
        public float steeringWheelRatio = 5f;
        public float tireRelaxationLength = 0.35f;
        public int wheelSubsteps = 6;

        // Brakes
        public float maxBrakeTorque = 4000f;
        public float brakeBiasFront = 0.57f;

        // Aero
        public float dragArea = 1.25f;
        // Downforce raised from 3.6 m² (1.25 × weight at 240 km/h) towards a modern formula car:
        // ~1.6 × weight at 240 km/h, 4.4 g in fast corners. Balance slightly forward with it.
        public float downforceArea = 4.6f;
        /// <summary>Front share of the downforce. 0.44 → 0.41 (handling brief 2026-11): more rear downforce for a stable
        /// rear at 200–300 km/h; slow corners are unaffected (little downforce there).</summary>
        public float aeroBalanceFront = 0.41f;
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
        /// <summary>Front aero balance shift per metre of rake change (dive moves it forward). Was 1.0: under hard
        /// braking at 250–300 km/h the dive moved up to 6 % of the downforce to the front and lightened the rear
        /// right where the driver turns in (entry oversteer in fast corners). 0.3 keeps the effect, a third as strong.</summary>
        public float aeroRakeSensitivity = 0.3f;
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
        /// <remarks>muPeak 1.8 → 1.95 (handling brief 2026-11): more usable grip, generous for a simcade game, still a
        /// clear limit. Together with the keyboard fix: 1.84 instead of 1.47 g in a 60 km/h hairpin.</remarks>
        public TireModel tireFront = new TireModel { nominalLoad = 2800f, muPeak = 1.95f, loadSensitivity = 0.08f, slideFloor = 0.82f };
        /// <summary>Rear tires: wider, so a higher nominal load and slightly more grip.
        /// This keeps the rear from being the weaker axle despite carrying 55 % of the
        /// weight, which is what makes real open-wheelers stable on turn-in.</summary>
        /// <remarks>Slide floor 0.78 → 0.84: past its peak the rear lets go more gently (a recoverable slide, not a snap).</remarks>
        public TireModel tireRear = new TireModel { nominalLoad = 3900f, muPeak = 1.95f, slipAnglePeak = 0.11f, loadSensitivity = 0.08f, slideFloor = 0.84f };
        public EngineModel engine = new EngineModel();
        public Gearbox gearbox = new Gearbox();

        /// <summary>Stability control with the steering assist (new in the October 2026 handling).</summary>
        public bool escEnabled = true;
        /// <summary>True for <see cref="Legacy"/>: the input layer also uses the previous keyboard ramps
        /// (KeyboardSteering.Profile.Classic) and no front-slip limiter.</summary>
        public bool legacy;

        /// <summary>
        /// The handling before the November 2026 overhaul (the October 2026 car, PR #10 – what the player drove until
        /// now), unchanged, for a direct in-game comparison (settings → "Fahrverhalten: Bisherig").
        /// See docs/HANDLING_KEYBOARD.md.
        /// </summary>
        public static VehicleSetup Legacy() => new VehicleSetup
        {
            antiRollFront = 25000f,
            antiRollRear = 80000f,
            aeroRakeSensitivity = 1.0f,
            aeroBalanceFront = 0.44f,
            tireFront = new TireModel { nominalLoad = 2800f, muPeak = 1.8f, loadSensitivity = 0.08f, slideFloor = 0.82f },
            tireRear = new TireModel { nominalLoad = 3900f, muPeak = 1.8f, slipAnglePeak = 0.11f, loadSensitivity = 0.08f, slideFloor = 0.78f },
            legacy = true,
        };

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
