using System;
using Apex.Core;

namespace Apex.Vehicle
{
    /// <summary>
    /// Engine-independent vehicle dynamics for a rear-wheel-drive open-wheel car.
    ///
    /// Per fixed step the engine layer:
    ///   1. calls <see cref="PreStep"/> (gearbox, steering -> wheel steer angles),
    ///   2. raycasts each wheel and fills its contact inputs
    ///      (contact, compression, contact velocities in the steered wheel frame, surface),
    ///   3. calls <see cref="Step"/>, which computes suspension loads (incl. anti-roll bars),
    ///      drivetrain torque through a limited-slip differential, sub-stepped wheel spin,
    ///      combined-slip tire forces and aero loads,
    ///   4. applies wheel loads (along the suspension axis), tire forces (at the contact
    ///      points) and aero forces to its rigid body.
    /// </summary>
    public sealed class VehicleSimulation
    {
        public readonly VehicleSetup setup;
        public readonly WheelState[] wheels = new WheelState[4];
        public readonly TireModel tireFront;
        public readonly TireModel tireRear;
        public readonly EngineModel engine;
        public readonly Gearbox gearbox;

        // Driver inputs (written by player or AI controller every step)
        public float throttle;
        public float brake;
        /// <summary>-1 full left .. +1 full right.</summary>
        public float steer;
        /// <summary>Traction control level: 0 off, 1 low (more slip allowed), 2 medium, 3 high.</summary>
        public int tractionLevel = 2;
        public bool assistAbs = true;
        /// <summary>Steering assist strength 0 (off: raw mechanical lock and rack) .. 1 (full).
        /// The assist never adds grip; it limits lock to what the front can use and adds a
        /// bounded counter-steer correction.</summary>
        public float steeringAssist = 1f;
        public bool autoGearbox = true;
        /// <summary>
        /// Speed-sensitive steering (an input filter, not a driving aid): limits the lock
        /// to a margin above what the front tires can use at the current speed, so full
        /// digital input at 250 km/h does not mean 22° of lock. No counter-steer, no grip.
        /// Off = raw mechanical lock (for analogue wheels / pads).
        /// </summary>
        public bool speedSensitiveSteering = true;
        /// <summary>Lock margin above the grip limit with speed-sensitive steering and no assist.</summary>
        public float speedSensitiveHeadroom = 1.35f;

        public bool assistTraction
        {
            get => tractionLevel > 0;
            set => tractionLevel = value ? Math.Max(tractionLevel, 2) : 0;
        }

        public bool assistSteering
        {
            get => steeringAssist > 0f;
            set => steeringAssist = value ? (steeringAssist > 0f ? steeringAssist : 1f) : 0f;
        }
        /// <summary>Holds the car (grid, pause, calibration): full brakes, no drive and
        /// never engages reverse, unlike holding the brake pedal at a standstill.</summary>
        public bool holdBrake;
        /// <summary>Pit limiter / speed cap in m/s (infinity = off).</summary>
        public float speedLimit = float.PositiveInfinity;

        // Conditions
        /// <summary>Damage from impacts; only has effects when <see cref="damageEnabled"/>.</summary>
        public readonly DamageState damage = new DamageState();
        public bool damageEnabled;
        public readonly TireState[] tires = new TireState[4];
        /// <summary>Simulate tire temperature, wear and compound grip.</summary>
        public bool simulateTires = true;
        /// <summary>Track water level 0..1 (from the weather model).</summary>
        public float trackWetness;
        public float trackTemperature = 34f;

        // Telemetry
        public float EngineRpm { get; private set; }
        public float SteerAngle { get; private set; }
        public float ForwardSpeed { get; private set; }
        public float Speed { get; private set; }
        public float DownforceFront { get; private set; }
        public float DownforceRear { get; private set; }
        public float Drag { get; private set; }
        public bool TcActive { get; private set; }
        public bool AbsActive { get; private set; }
        /// <summary>Stability control is braking/cutting drive this step.</summary>
        public bool EscActive { get; private set; }
        /// <summary>Rear slip (relative to its peak) above which stability control intervenes.</summary>
        public const float EscThreshold = 1.0f;
        /// <summary>Brake torque on the outside front at full intervention, relative to maxBrakeTorque.</summary>
        public const float EscBrakeShare = 0.6f;
        public float TcCut => tcCut;
        public float SimTime { get; private set; }
        /// <summary>Clutch is slipping (launch, shift sync, anti-stall).</summary>
        public bool ClutchSlipping { get; private set; }
        public bool LimiterActive { get; private set; }
        /// <summary>Plank-to-ground height front/rear (m) from suspension compression.</summary>
        public float RideHeightFront { get; private set; }
        public float RideHeightRear { get; private set; }
        /// <summary>Floor downforce relative to its nominal share (ground-effect gain).</summary>
        public float FloorGain { get; private set; } = 1f;
        public float AeroBalance { get; private set; }
        /// <summary>The plank touches the ground (sparks).</summary>
        public bool Bottoming { get; private set; }
        /// <summary>Cockpit steering-wheel rotation in degrees (+ = clockwise).</summary>
        public float SteeringWheelAngleDeg => SteerAngle * ApexMath.Rad2Deg * setup.steeringWheelRatio;

        public event Action<int> GearChanged;

        float tcCut;
        float engineOmega;
        bool clutchLocked;
        float lastRatio;
        float reverseTimer;
        bool shiftUpRequested;
        bool shiftDownRequested;

        public VehicleSimulation(VehicleSetup setup)
        {
            this.setup = setup;
            tireFront = setup.tireFront;
            tireRear = setup.tireRear;
            engine = setup.engine;
            gearbox = setup.gearbox;
            for (int i = 0; i < 4; i++)
            {
                wheels[i] = new WheelState(i, setup);
                tires[i] = new TireState { coolingScale = i < 2 ? 1f : 1.3f };
            }
            Reset();
        }

        public void Reset()
        {
            foreach (var w in wheels)
            {
                w.compression = w.staticCompression;
                w.ResetDynamics();
            }
            gearbox.Reset(1);
            EngineRpm = engine.idleRpm;
            engineOmega = engine.idleRpm * RpmToRad;
            clutchLocked = false;
            lastRatio = gearbox.TotalRatio;
            DownforceFront = DownforceRear = Drag = 0f;
            FloorGain = 1f;
            tcCut = 0f;
            reverseTimer = 0f;
            SteerAngle = 0f;
            throttle = brake = steer = 0f;
        }

        const float RpmToRad = ApexMath.Tau / 60f;

        public TireModel TireFor(WheelState w) => w.front ? tireFront : tireRear;

        /// <summary>Matches engine speed to the driven wheels with the clutch closed
        /// (for placing a car at speed, e.g. rolling starts and test rigs).</summary>
        public void SyncEngineToWheels()
        {
            float ratio = gearbox.TotalRatio;
            float w = (wheels[2].omega + wheels[3].omega) * 0.5f * ratio;
            engineOmega = MathF.Max(w, engine.idleRpm * RpmToRad);
            EngineRpm = engineOmega / RpmToRad;
            lastRatio = ratio;
            clutchLocked = ratio != 0f && w >= engine.idleRpm * RpmToRad;
        }

        /// <summary>Fits a fresh set of tires (pit stop / session start).</summary>
        public void FitTires(TireCompound compound, float startTemp = float.NaN)
        {
            foreach (var t in tires) t.Fit(compound, startTemp);
        }

        public float TireWearMultiplier
        {
            set { foreach (var t in tires) t.wearMultiplier = value; }
        }

        public float AverageTireWear()
        {
            float s = 0f;
            foreach (var t in tires) s += t.wear;
            return s / 4f;
        }

        public void RequestShiftUp() => shiftUpRequested = true;
        public void RequestShiftDown() => shiftDownRequested = true;

        public float SpeedKmh => Speed * 3.6f;

        /// <summary>Number of wheels that count as inside track limits (airborne wheels count).</summary>
        public int WheelsOnTrack()
        {
            int n = 0;
            foreach (var w in wheels)
                if (!w.contact || SurfaceProperties.For(w.surface).onTrack) n++;
            return n;
        }

        /// <summary>Phase 1: gearbox and steering. Call before raycasting the wheels.</summary>
        public void PreStep(float dt, float forwardSpeed, float speed)
        {
            ForwardSpeed = forwardSpeed;
            Speed = speed;
            UpdateGearbox(dt);
            UpdateSteering(dt);
        }

        /// <summary>Phase 2: forces. Wheel contact inputs must be filled in.</summary>
        public void Step(float dt)
        {
            SimTime += dt;
            float throttleIn = throttle;
            float brakeIn = brake;
            if (holdBrake)
            {
                throttleIn = 0f;
                brakeIn = 1f;
            }
            else if (gearbox.gear < 0)
            {
                // In reverse the pedals swap so "brake" backs the car up.
                throttleIn = brake;
                brakeIn = throttle;
            }

            ComputeLoads(dt);

            // --- Lateral slip with relaxation length -------------------------------
            foreach (var w in wheels)
            {
                float vx = w.longitudinalVelocity;
                float target = MathF.Atan2(w.lateralVelocity, MathF.Max(MathF.Abs(vx), 2f));
                float k = ApexMath.Clamp(MathF.Abs(vx) * dt / setup.tireRelaxationLength, 0.05f, 1f);
                w.slipAngle = ApexMath.Lerp(w.slipAngle, target, k);
            }

            // --- Drivetrain: engine inertia + auto-clutch ---------------------------
            var rl = wheels[2];
            var rr = wheels[3];
            float ratio = gearbox.TotalRatio;
            float eff = gearbox.efficiency;
            float effThrottle = throttleIn;
            if (ForwardSpeed > speedLimit)
            {
                // Limiter: cut drive and brake gently back to the limit.
                effThrottle = 0f;
                brakeIn = MathF.Max(brakeIn, ApexMath.Clamp01((ForwardSpeed - speedLimit) * 0.15f));
            }
            else if (ForwardSpeed > speedLimit - 1.5f)
            {
                effThrottle *= ApexMath.Clamp01((speedLimit - ForwardSpeed) / 1.5f);
            }
            float inputOmega = (rl.omega + rr.omega) * 0.5f * ratio;
            float inputRpm = inputOmega / RpmToRad;
            float idleW = engine.idleRpm * RpmToRad;
            bool shifting = gearbox.IsShifting;
            // Auto-clutch clutchCap: open in neutral, while shifting and below idle
            // (anti-stall); a launch clutchCap holds the engine near launch rpm and
            // closes progressively as the car picks up speed.
            float clutchCap;
            bool launching = false;
            if (gearbox.gear == 0 || shifting) clutchCap = 0f;
            else if (throttleIn > 0.05f && inputRpm < setup.launchRpm)
            {
                launching = true;
                float launchT = engine.peakTorque * engine.CurveAt(setup.launchRpm) * throttleIn;
                float closing = ApexMath.SmoothStep01(ApexMath.Clamp01((inputRpm - (setup.launchRpm - 2000f)) / 2000f));
                clutchCap = ApexMath.Lerp(launchT, setup.clutchMaxTorque, closing);
            }
            else if (inputOmega < idleW * 0.95f) clutchCap = 0f;
            else clutchCap = setup.clutchMaxTorque;
            // The clutch sticks while it can carry the engine torque; it slips when the
            // engine overpowers it, when it opens, or right after a ratio change.
            float engineTorqueNow = MathF.Abs(engine.Torque(engineOmega / RpmToRad, effThrottle));
            if (ratio != lastRatio || clutchCap <= 0f || engineTorqueNow > clutchCap) clutchLocked = false;
            lastRatio = ratio;
            if (!clutchLocked && clutchCap > 0f && engineTorqueNow <= clutchCap && MathF.Abs(engineOmega - inputOmega) < 40f * RpmToRad)
                clutchLocked = true;
            ClutchSlipping = !clutchLocked && clutchCap > 0f;
            // Engine-side target used by the governor when the clutch slips: launch rpm,
            // idle, or (during a seamless shift) the new gear's input speed.
            float governTarget = launching
                ? ApexMath.Lerp(engine.idleRpm, setup.launchRpm, ApexMath.Clamp01(throttleIn * 1.4f)) * RpmToRad
                : idleW;
            if (shifting && gearbox.gear != 0) governTarget = MathF.Max(MathF.Abs(inputOmega), idleW);
            // Traction control limits the torque reaching the axle (ignition cut).
            float requested = clutchLocked
                ? engine.Torque(engineOmega / RpmToRad, effThrottle) * ratio * eff
                : clutchCap * MathF.Abs(ratio) * eff;
            // Stability control (part of the steering assist, off at strength 0): when the rear slides
            // past its grip peak and works harder than the front, brake the outside front wheel (a yaw
            // moment against the rotation) and cut drive, both scaled with the assist strength.
            float escExcess = 0f;
            int escWheel = -1;
            if (steeringAssist > 0f && Speed > 10f && !holdBrake && gearbox.gear > 0)
            {
                float ar = 0.5f * (wheels[2].slipAngle + wheels[3].slipAngle);
                float af = 0.5f * (wheels[0].slipAngle + wheels[1].slipAngle);
                float rearUse = MathF.Abs(ar) / tireRear.slipAnglePeak;
                float frontUse = MathF.Abs(af) / tireFront.slipAnglePeak;
                if (rearUse > EscThreshold && rearUse > frontUse)
                {
                    escExcess = ApexMath.Clamp01((rearUse - EscThreshold) / 0.5f) * ApexMath.Clamp01(steeringAssist);
                    // Right turn: the rear slides left (negative slip), the outside is the left side.
                    escWheel = ar < 0f ? 0 : 1;
                    effThrottle *= 1f - escExcess;
                }
            }
            EscActive = escExcess > 0f;
            float limited = ApplyTractionControl(requested * (1f - escExcess), throttleIn, dt);
            float tcLimit = limited < requested ? limited : float.PositiveInfinity;
            float engineReflected = engine.inertia * ratio * ratio * 0.5f;
            LimiterActive = false;
            // --- Brakes with optional ABS ----------------------------------------
            // ABS limits each wheel's brake torque to what its tire can transmit at a
            // slip just below the peak (friction-circle share left after cornering),
            // and backs off further if the wheel still starts to lock.
            AbsActive = false;
            foreach (var w in wheels)
            {
                var tire = TireFor(w);
                float tanPeakAbs = MathF.Tan(tire.slipAnglePeak);
                float share = w.front ? setup.brakeBiasFront : 1f - setup.brakeBiasFront;
                float tb = brakeIn * setup.maxBrakeTorque * share * 2f;
                if (assistAbs && brakeIn > 0.05f && Speed > 3f && w.contact)
                {
                    float target = 0.95f;
                    float sy = MathF.Tan(MathF.Min(MathF.Abs(w.slipAngle), 1.4f)) / tanPeakAbs;
                    float longShare = target > sy ? MathF.Sqrt(1f - (sy * sy) / (target * target)) : 0.2f;
                    float capacity = tire.MuAtLoad(w.load) * w.grip * w.load * tire.Curve(target) * longShare * w.radius;
                    // Engine braking already uses part of the rear tires' budget.
                    capacity -= MathF.Max(0f, -w.driveTorque);
                    // Wheel starting to lock: release pressure so it can spin back up
                    // (past the peak the tire transmits only the sliding floor).
                    if (w.slipRatio < -setup.absSlipThreshold) capacity *= 0.4f;
                    // Cornering brake control: a rear tire already past its lateral peak
                    // gets no brake pressure, so braking cannot pull the rear round.
                    if (!w.front && sy > 0.95f) capacity *= ApexMath.Clamp01((1.25f - sy) / 0.3f);
                    capacity = MathF.Max(capacity, 0f);
                    if (tb > capacity)
                    {
                        tb = capacity;
                        AbsActive = true;
                    }
                }
                if (w.index == escWheel) tb = MathF.Max(tb, escExcess * setup.maxBrakeTorque * EscBrakeShare);
                w.brakeTorque = tb;
            }

            // --- Sub-stepped wheel spin and tire forces ----------------------------
            int subN = Math.Max(1, setup.wheelSubsteps);
            float h = dt / subN;
            Span<float> fxSum = stackalloc float[4];
            Span<float> fySum = stackalloc float[4];
            float engineSum = 0f;
            for (int s = 0; s < subN; s++)
            {
                float axleTorque;
                float rpm = engineOmega / RpmToRad;
                if (clutchLocked)
                {
                    float te = engine.Torque(rpm, effThrottle);
                    LimiterActive |= rpm >= engine.limiterRpm && effThrottle > 0f;
                    axleTorque = te * ratio * eff;
                    if (axleTorque > tcLimit) axleTorque = tcLimit;
                }
                else
                {
                    // Slipping clutch: Coulomb friction between engine and gearbox input.
                    float wIn = (rl.omega + rr.omega) * 0.5f * ratio;
                    float slip = engineOmega - wIn;
                    float tc = clutchCap * MathF.Sign(slip);
                    if (ratio != 0f && MathF.Abs(tc * ratio * eff) > tcLimit) tc = MathF.Sign(tc) * tcLimit / MathF.Abs(ratio * eff);
                    axleTorque = tc * ratio * eff;
                    float drive;
                    if (shifting)
                        drive = ApexMath.Clamp(engine.inertia * 400f * (governTarget - engineOmega), -engine.shiftSyncTorque, engine.shiftSyncTorque);
                    else
                    {
                        float te = engine.Torque(rpm, effThrottle);
                        float govern = tc + engine.inertia * 30f * (governTarget - engineOmega);
                        // Launch governor / idle control hold the target rpm.
                        if (launching) drive = MathF.Min(te, govern);
                        else if (effThrottle < 0.05f) drive = MathF.Max(te, MathF.Min(govern, engine.peakTorque * 0.3f));
                        else drive = te;
                        LimiterActive |= rpm >= engine.limiterRpm && effThrottle > 0f;
                    }
                    float newEngine = engineOmega + (drive - tc) / engine.inertia * h;
                    newEngine = ApexMath.Clamp(newEngine, idleW * 0.9f, engine.maxRpm * RpmToRad);
                    // Lock-up once the slip changes sign and the clutch can hold the engine.
                    if (clutchCap > 0f && MathF.Sign(newEngine - wIn) != MathF.Sign(slip)
                        && MathF.Abs(engine.Torque(newEngine / RpmToRad, effThrottle)) <= clutchCap)
                    {
                        clutchLocked = true;
                        newEngine = wIn;
                    }
                    engineOmega = newEngine;
                }
                // Open-diff split plus a clutch-type locking torque (LSD).
                float dw = rl.omega - rr.omega;
                float lockCap = setup.diffPreload + setup.diffLockRatio * MathF.Abs(axleTorque);
                float lockT = ApexMath.Clamp(dw * setup.diffLockRate, -lockCap, lockCap);
                rl.driveTorque = axleTorque * 0.5f - lockT * 0.5f;
                rr.driveTorque = axleTorque * 0.5f + lockT * 0.5f;
                wheels[0].driveTorque = 0f;
                wheels[1].driveTorque = 0f;
                for (int i = 0; i < 4; i++)
                {
                    var w = wheels[i];
                    float vx = w.longitudinalVelocity;
                    float fx = 0f, fy = 0f;
                    if (w.contact && w.load > 0f)
                    {
                        w.slipRatio = (w.omega * w.radius - vx) / MathF.Max(MathF.Abs(vx), 3f);
                        TireFor(w).Compute(w.load, w.slipRatio, w.slipAngle, w.grip, out fx, out fy);
                    }
                    float rolling = SurfaceProperties.For(w.surface).rollingDrag * w.load * w.radius * MathF.Sign(w.omega);
                    if (damageEnabled && damage.puncture[i]) rolling *= 6f;
                    float torque = w.driveTorque - fx * w.radius - rolling;
                    // A locked clutch couples the engine's inertia to the driven wheels.
                    float inertia = w.inertia + (clutchLocked && !w.front ? engineReflected : 0f);
                    float newOmega = w.omega + torque / inertia * h;
                    // Brakes oppose rotation but never reverse it.
                    float brakeDw = w.brakeTorque / inertia * h;
                    if (MathF.Abs(newOmega) <= brakeDw) newOmega = 0f;
                    else newOmega -= MathF.Sign(newOmega) * brakeDw;
                    w.omega = newOmega;
                    fxSum[i] += fx;
                    fySum[i] += fy;
                }
                if (clutchLocked) engineOmega = MathF.Max((rl.omega + rr.omega) * 0.5f * ratio, idleW * 0.9f);
                engineSum += engineOmega;
            }
            EngineRpm = MathF.Max(engineSum / subN / RpmToRad, 0f);
            for (int i = 0; i < 4; i++)
            {
                var w = wheels[i];
                w.fx = fxSum[i] / subN;
                w.fy = fySum[i] / subN;
                w.spinAngle = ApexMath.PosMod(w.spinAngle + w.omega * dt, ApexMath.Tau);
                if (simulateTires)
                {
                    // Frictional power at the contact patch heats and wears the tire.
                    float slipPower = w.contact
                        ? MathF.Abs(w.fx * (w.omega * w.radius - w.longitudinalVelocity)) + MathF.Abs(w.fy * w.lateralVelocity)
                        : 0f;
                    tires[i].Update(dt, slipPower, w.load, Speed, trackTemperature, trackWetness);
                }
            }

            UpdateAero();
        }

        /// <summary>
        /// Wings plus a ground-effect floor: floor downforce rises as the mean ride
        /// height drops (power law around a reference height), stalls when the plank
        /// nears the ground, and the aero balance moves forward when the car dives
        /// (rake increase). Ride heights come from the suspension compression.
        /// </summary>
        void UpdateAero()
        {
            float cf = 0f, cr = 0f;
            int nf = 0, nr = 0;
            foreach (var w in wheels)
            {
                // An airborne wheel means the corner of the car is high: full extension.
                float dc = (w.contact ? w.compression + w.roadOffset : 0f) - w.staticCompression;
                if (w.front) { cf += dc; nf++; } else { cr += dc; nr++; }
            }
            RideHeightFront = setup.rideHeightFront - cf / nf;
            RideHeightRear = setup.rideHeightRear - cr / nr;
            float hMean = 0.5f * (RideHeightFront + RideHeightRear);
            float gain = MathF.Pow(setup.floorReferenceHeight / MathF.Max(hMean, 0.004f), setup.groundEffectExponent);
            gain = MathF.Min(gain, setup.floorMaxGain);
            if (hMean < setup.floorStallHeight)
                gain *= 1f - setup.floorStallLoss * ApexMath.Clamp01(1f - MathF.Max(hMean, 0f) / setup.floorStallHeight);
            FloorGain = gain;
            Bottoming = MathF.Min(RideHeightFront, RideHeightRear) < 0f;
            float rakeChange = (RideHeightRear - RideHeightFront) - (setup.rideHeightRear - setup.rideHeightFront);
            AeroBalance = ApexMath.Clamp(setup.aeroBalanceFront + setup.aeroRakeSensitivity * rakeChange, setup.aeroBalanceFront - 0.06f, setup.aeroBalanceFront + 0.06f);
            float q = 0.5f * setup.airDensity * ForwardSpeed * ForwardSpeed;
            bool dmg = damageEnabled;
            float floorPart = setup.floorShare * gain * (dmg ? damage.FloorFactor : 1f);
            float cla = setup.downforceArea * (1f - setup.floorShare + floorPart);
            float downforce = cla * q;
            DownforceFront = downforce * AeroBalance * (dmg ? damage.FrontAeroFactor : 1f);
            DownforceRear = downforce * (1f - AeroBalance) * (dmg ? damage.RearAeroFactor : 1f);
            float induced = 1f + setup.inducedDragFactor * MathF.Max(0f, gain - 1f);
            Drag = setup.dragArea * induced * (dmg ? damage.DragFactor : 1f) * 0.5f * setup.airDensity * Speed * Speed;
        }

        void ComputeLoads(float dt)
        {
            // Anti-roll bars couple left/right compression on each axle.
            float arbFront = (wheels[0].compression - wheels[1].compression) * setup.antiRollFront;
            float arbRear = (wheels[2].compression - wheels[3].compression) * setup.antiRollRear;
            foreach (var w in wheels)
            {
                var props = SurfaceProperties.For(w.surface);
                float tireGrip = simulateTires ? tires[w.index].GripMultiplier(trackWetness) : 1f;
                w.grip = props.grip * w.conditionGrip * tireGrip;
                if (damageEnabled && damage.puncture[w.index]) w.grip *= 0.3f;
                // Surface texture (kerb ripples, grass, gravel) excites the suspension.
                // Its frequency is capped well below the 120 Hz step to avoid aliasing.
                if (props.textureAmplitude > 0f && w.contact)
                {
                    float freq = MathF.Min(MathF.Abs(w.longitudinalVelocity) / props.textureWavelength, 24f);
                    w.roadPhase = ApexMath.PosMod(w.roadPhase + freq * ApexMath.Tau * dt, ApexMath.Tau);
                    float speedFade = ApexMath.Clamp01(MathF.Abs(w.longitudinalVelocity) / 4f);
                    w.roadOffset = props.textureAmplitude * speedFade * 0.5f * (1f + MathF.Sin(w.roadPhase));
                }
                else w.roadOffset = 0f;
                if (w.loadOverride >= 0f)
                {
                    // Test rigs prescribe the load; the texture still adds spring force.
                    w.load = MathF.Max(0f, w.loadOverride + w.spring * w.roadOffset);
                    w.prevCompression = w.compression;
                    continue;
                }
                if (!w.contact)
                {
                    w.load = 0f;
                    w.prevCompression = w.compression;
                    continue;
                }
                float comp = w.compression + w.roadOffset;
                float compVel = (comp - w.prevCompression) / dt;
                w.prevCompression = comp;
                float force = w.spring * comp;
                force += (compVel > 0f ? setup.damperBump : setup.damperRebound) * compVel;
                if (comp > setup.suspensionTravel)
                    force += setup.bumpStopRate * (comp - setup.suspensionTravel);
                float arb = w.front ? arbFront : arbRear;
                force += w.left ? arb : -arb;
                w.load = MathF.Max(0f, force) * MathF.Max(w.normalAlignment, 0f);
            }
        }

        void UpdateGearbox(float dt)
        {
            gearbox.Update(dt);
            bool shifted = false;
            if (shiftUpRequested) shifted = gearbox.ShiftUp();
            else if (shiftDownRequested) shifted = gearbox.ShiftDown();
            shiftUpRequested = shiftDownRequested = false;
            if (autoGearbox && !shifted)
            {
                float wheelRpm = ForwardSpeed / setup.wheelRadiusRear * 60f / ApexMath.Tau;
                // Simcade convenience: hold brake at a standstill to engage reverse.
                if (!holdBrake && MathF.Abs(ForwardSpeed) < 0.8f && brake > 0.5f && throttle < 0.05f && gearbox.gear > 0)
                {
                    reverseTimer += dt;
                    if (reverseTimer > 0.6f)
                    {
                        gearbox.gear = -1;
                        shifted = true;
                    }
                }
                else
                {
                    reverseTimer = 0f;
                }
                if (holdBrake && gearbox.gear < 0)
                {
                    gearbox.gear = 1;
                    shifted = true;
                }
                else if (gearbox.gear < 0)
                {
                    if (throttle > 0.3f && ForwardSpeed > -1f)
                    {
                        gearbox.gear = 1;
                        shifted = true;
                    }
                }
                else
                {
                    shifted = gearbox.AutoShift(wheelRpm, throttle, brake, ForwardSpeed) || shifted;
                }
            }
            if (shifted) GearChanged?.Invoke(gearbox.gear);
        }

        /// <summary>
        /// Maximum steering lock (rad) at the current speed. Without assist this is the
        /// mechanical lock. With the steering assist it is the smaller of a speed-faded
        /// lock and a grip-based limit: the kinematic angle for the car's maximum
        /// lateral acceleration (incl. downforce) plus the front tire's peak slip angle.
        /// Beyond that the front can only scrub or provoke the rear.
        /// </summary>
        public float CurrentMaxLock()
        {
            float maxLock = setup.maxSteerAngleDeg * ApexMath.Deg2Rad;
            float strength = ApexMath.Clamp01(steeringAssist);
            if (strength <= 0f && !speedSensitiveSteering) return maxLock;
            float v = MathF.Abs(ForwardSpeed);
            float t = ApexMath.Clamp01(v / setup.steerAssistSpeed);
            float faded = strength > 0f ? ApexMath.Lerp(maxLock, setup.highSpeedSteerDeg * ApexMath.Deg2Rad, ApexMath.SmoothStep01(t)) : maxLock;
            if (v < 5f) return faded;
            float downforcePerMass = 0.5f * setup.airDensity * v * v * setup.downforceArea / setup.mass;
            float aMax = tireFront.muPeak * 0.9f * (9.81f + downforcePerMass);
            float gripLimit = MathF.Atan(setup.wheelbase * aMax / (v * v)) + tireFront.slipAnglePeak * 0.9f;
            // Lighter assist leaves headroom past the grip limit: the driver can
            // overdrive the front (understeer) or load up the rear.
            if (strength <= 0f) return MathF.Min(maxLock, gripLimit * speedSensitiveHeadroom);
            float headroom = 1f + AssistHeadroom * (1f - strength);
            return MathF.Min(maxLock, MathF.Min(faded, gripLimit) * headroom);
        }

        /// <summary>Extra lock above the grip limit per unit of missing assist strength.</summary>
        public const float AssistHeadroom = 0.5f;

        /// <summary>Rack speed (rad/s): slower at speed with the assist so digital input cannot snap the car.</summary>
        float CurrentRackRate()
        {
            float fast = setup.steerRateDeg * ApexMath.Deg2Rad;
            float strength = ApexMath.Clamp01(steeringAssist);
            if (strength <= 0f) return fast;
            float t = ApexMath.Clamp01(MathF.Abs(ForwardSpeed) / 55f);
            float slow = ApexMath.Lerp(fast, setup.steerRateHighSpeedDeg * ApexMath.Deg2Rad, strength);
            return ApexMath.Lerp(fast, slow, ApexMath.SmoothStep01(t));
        }

        /// <summary>Counter-steer added by the assist when the rear slides past its limit (rad, + = right).</summary>
        public float CounterSteer { get; private set; }

        void UpdateSteering(float dt)
        {
            float maxLock = CurrentMaxLock();
            float target = ApexMath.Clamp(steer, -1f, 1f) * maxLock;
            // Stability assist: when the rear slip angle exceeds ~75 % of its peak and
            // is larger than the front's (oversteer), steer into the slide. The rear
            // slip angle has the sign of the needed correction (right turn -> rear
            // slides left -> negative slip -> steer left). The driver can still
            // override with more lock; the assist only adds a bounded correction.
            float cs = 0f;
            // Only for power oversteer (on throttle, off the brake). Reacting to rear slip
            // while trail braking fought the driver and caused spins (keyboard lap test).
            if (assistSteering && Speed > 8f && throttle > 0.2f && brake < 0.05f)
            {
                float ar = 0.5f * (wheels[2].slipAngle + wheels[3].slipAngle);
                float af = 0.5f * (wheels[0].slipAngle + wheels[1].slipAngle);
                float threshold = tireRear.slipAnglePeak * 0.75f;
                float excess = MathF.Abs(ar) - threshold;
                // Rear beyond its peak is a slide regardless of the front; between 75 % and
                // 100 % only count it when the rear is working harder than the front.
                bool sliding = MathF.Abs(ar) > tireRear.slipAnglePeak || MathF.Abs(ar) > MathF.Abs(af) * 0.9f;
                if (excess > 0f && sliding)
                    cs = MathF.Sign(ar) * MathF.Min(excess * setup.counterSteerGain * ApexMath.Clamp01(steeringAssist), setup.maxSteerAngleDeg * ApexMath.Deg2Rad);
            }
            CounterSteer = ApexMath.MoveToward(CounterSteer, cs, 6f * dt);
            target = ApexMath.Clamp(target + CounterSteer, -setup.maxSteerAngleDeg * ApexMath.Deg2Rad, setup.maxSteerAngleDeg * ApexMath.Deg2Rad);
            // Rack speed limit keeps the front from snapping on digital input.
            SteerAngle = ApexMath.MoveToward(SteerAngle, target, CurrentRackRate() * dt);
            float ack = setup.ackermann * SteerAngle;
            for (int i = 0; i < 2; i++)
            {
                var w = wheels[i];
                bool inside = (SteerAngle > 0f && !w.left) || (SteerAngle < 0f && w.left);
                w.steer = SteerAngle + (inside ? ack : -ack * 0.5f);
            }
            wheels[2].steer = 0f;
            wheels[3].steer = 0f;
            if (damageEnabled)
                for (int i = 0; i < 4; i++) wheels[i].steer += damage.toe[i];
        }

        /// <summary>
        /// Traction control: feed-forward limit of drive torque to what the driven
        /// tires can transmit at their current load and slip angle (friction-circle
        /// budget at a target slip slightly below the peak), plus a gentle feedback
        /// trim if measured slip still exceeds the threshold. Catches launch
        /// wheelspin and mid-corner power oversteer without bang-bang oscillation.
        /// </summary>
        /// <summary>Allowed combined slip relative to the setup threshold per TC level.</summary>
        public static float TcLevelScale(int level) => level <= 1 ? 1.15f : level == 2 ? 1f : 0.88f;

        float ApplyTractionControl(float axleTorque, float throttleIn, float dt)
        {
            TcActive = false;
            if (!assistTraction || throttleIn < 0.05f || axleTorque <= 0f)
            {
                tcCut = ApexMath.MoveToward(tcCut, 0f, 6f * dt);
                return axleTorque;
            }
            float threshold = setup.tcCombinedThreshold * TcLevelScale(tractionLevel);
            float target = threshold * 0.92f;
            var tire = tireRear;
            float tanPeak = MathF.Tan(tire.slipAnglePeak);
            float capacity = 0f;
            float measured = 0f;
            for (int i = 2; i < 4; i++)
            {
                var w = wheels[i];
                if (!w.contact || w.load <= 0f) continue;
                float sy = MathF.Tan(MathF.Min(MathF.Abs(w.slipAngle), 1.4f)) / tanPeak;
                float longShare = target > sy ? MathF.Sqrt(1f - (sy * sy) / (target * target)) : 0f;
                capacity += tire.MuAtLoad(w.load) * w.grip * w.load * tire.Curve(target) * longShare * w.radius;
                if (w.slipRatio > 0.02f) measured = MathF.Max(measured, tire.CombinedSlip(w.slipRatio, w.slipAngle));
            }
            // Feedback trim on top of the feed-forward limit.
            // (Below ~3 m/s slip ratio is ill-defined, so only the feed-forward acts.)
            if (measured > threshold && Speed > 3f)
                tcCut = ApexMath.MoveToward(tcCut, 0.6f, 3f * dt);
            else
                tcCut = ApexMath.MoveToward(tcCut, 0f, 4f * dt);
            // A wheel already past the peak only transmits the sliding force; limit to
            // less than that so it spins back down (also works below 3 m/s).
            if (measured > threshold)
                capacity *= 0.85f * tire.Curve(measured) / tire.Curve(target);
            float limit = capacity * (1f - tcCut);
            if (axleTorque > limit)
            {
                TcActive = true;
                return MathF.Max(limit, 0f);
            }
            return axleTorque;
        }
    }
}
