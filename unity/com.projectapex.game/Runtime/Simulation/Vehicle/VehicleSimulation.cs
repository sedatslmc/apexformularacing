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
        public readonly TireModel tire;
        public readonly EngineModel engine;
        public readonly Gearbox gearbox;

        // Driver inputs (written by player or AI controller every step)
        public float throttle;
        public float brake;
        /// <summary>-1 full left .. +1 full right.</summary>
        public float steer;
        public bool assistTraction = true;
        public bool assistAbs = true;
        public bool assistSteering = true;
        public bool autoGearbox = true;

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
        public float TcCut => tcCut;
        public float SimTime { get; private set; }

        public event Action<int> GearChanged;

        float tcCut;
        float reverseTimer;
        bool shiftUpRequested;
        bool shiftDownRequested;

        public VehicleSimulation(VehicleSetup setup)
        {
            this.setup = setup;
            tire = setup.tire;
            engine = setup.engine;
            gearbox = setup.gearbox;
            for (int i = 0; i < 4; i++) wheels[i] = new WheelState(i, setup);
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
            tcCut = 0f;
            reverseTimer = 0f;
            SteerAngle = 0f;
            throttle = brake = steer = 0f;
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
            if (gearbox.gear < 0)
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

            // --- Drivetrain -----------------------------------------------------
            var rl = wheels[2];
            var rr = wheels[3];
            float wheelRpm = (rl.omega + rr.omega) * 0.5f * 60f / ApexMath.Tau;
            float ratio = gearbox.TotalRatio;
            float engagedRpm = MathF.Abs(wheelRpm * ratio);
            bool clutchSlipping = gearbox.gear == 0 || engagedRpm < engine.idleRpm
                                  || (throttleIn > 0.05f && engagedRpm < setup.launchRpm);
            float effThrottle = throttleIn;
            if (gearbox.IsShifting) effThrottle = 0f;
            float crankTorque = 0f;
            if (clutchSlipping)
            {
                // Simcade auto-clutch: engine revs to a launch rpm and feeds torque.
                float target = ApexMath.Lerp(engine.idleRpm, setup.launchRpm, ApexMath.Clamp01(throttleIn * 1.4f));
                EngineRpm = ApexMath.MoveToward(EngineRpm, MathF.Max(target, engagedRpm), 25000f * dt);
                if (gearbox.gear != 0 && effThrottle > 0f) crankTorque = engine.Torque(EngineRpm, effThrottle);
            }
            else
            {
                EngineRpm = MathF.Min(engagedRpm, engine.maxRpm);
                crankTorque = engine.Torque(engagedRpm, effThrottle);
            }
            float axleTorque = crankTorque * ratio * gearbox.efficiency;
            axleTorque = ApplyTractionControl(axleTorque, throttleIn, dt);

            // --- Brakes with optional ABS ----------------------------------------
            // ABS limits each wheel's brake torque to what its tire can transmit at a
            // slip just below the peak (friction-circle share left after cornering),
            // and backs off further if the wheel still starts to lock.
            AbsActive = false;
            float tanPeakAbs = MathF.Tan(tire.slipAnglePeak);
            foreach (var w in wheels)
            {
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
                    capacity = MathF.Max(capacity, 0f);
                    if (tb > capacity)
                    {
                        tb = capacity;
                        AbsActive = true;
                    }
                }
                w.brakeTorque = tb;
            }

            // --- Sub-stepped wheel spin and tire forces ----------------------------
            int subN = Math.Max(1, setup.wheelSubsteps);
            float h = dt / subN;
            Span<float> fxSum = stackalloc float[4];
            Span<float> fySum = stackalloc float[4];
            for (int s = 0; s < subN; s++)
            {
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
                        tire.Compute(w.load, w.slipRatio, w.slipAngle, w.grip, out fx, out fy);
                    }
                    float rolling = SurfaceProperties.For(w.surface).rollingDrag * w.load * w.radius * MathF.Sign(w.omega);
                    float torque = w.driveTorque - fx * w.radius - rolling;
                    float newOmega = w.omega + torque / w.inertia * h;
                    // Brakes oppose rotation but never reverse it.
                    float brakeDw = w.brakeTorque / w.inertia * h;
                    if (MathF.Abs(newOmega) <= brakeDw) newOmega = 0f;
                    else newOmega -= MathF.Sign(newOmega) * brakeDw;
                    w.omega = newOmega;
                    fxSum[i] += fx;
                    fySum[i] += fy;
                }
            }
            for (int i = 0; i < 4; i++)
            {
                var w = wheels[i];
                w.fx = fxSum[i] / subN;
                w.fy = fySum[i] / subN;
                w.spinAngle = ApexMath.PosMod(w.spinAngle + w.omega * dt, ApexMath.Tau);
            }

            // --- Aerodynamics ---------------------------------------------------------
            float q = 0.5f * setup.airDensity * ForwardSpeed * ForwardSpeed;
            float downforce = setup.downforceArea * q;
            DownforceFront = downforce * setup.aeroBalanceFront;
            DownforceRear = downforce * (1f - setup.aeroBalanceFront);
            Drag = setup.dragArea * 0.5f * setup.airDensity * Speed * Speed;
        }

        void ComputeLoads(float dt)
        {
            // Anti-roll bars couple left/right compression on each axle.
            float arbFront = (wheels[0].compression - wheels[1].compression) * setup.antiRollFront;
            float arbRear = (wheels[2].compression - wheels[3].compression) * setup.antiRollRear;
            foreach (var w in wheels)
            {
                var props = SurfaceProperties.For(w.surface);
                w.grip = props.grip * w.conditionGrip;
                if (w.loadOverride >= 0f)
                {
                    w.load = w.loadOverride;
                    w.prevCompression = w.compression;
                    continue;
                }
                if (!w.contact)
                {
                    w.load = 0f;
                    w.prevCompression = w.compression;
                    continue;
                }
                float compVel = (w.compression - w.prevCompression) / dt;
                w.prevCompression = w.compression;
                float force = w.spring * w.compression;
                force += (compVel > 0f ? setup.damperBump : setup.damperRebound) * compVel;
                if (w.compression > setup.suspensionTravel)
                    force += setup.bumpStopRate * (w.compression - setup.suspensionTravel);
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
                if (MathF.Abs(ForwardSpeed) < 0.8f && brake > 0.5f && throttle < 0.05f && gearbox.gear > 0)
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
                if (gearbox.gear < 0)
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

        /// <summary>Maximum steering lock (rad) at the current speed.</summary>
        public float CurrentMaxLock()
        {
            float maxLock = setup.maxSteerAngleDeg * ApexMath.Deg2Rad;
            if (!assistSteering) return maxLock;
            float t = ApexMath.Clamp01(MathF.Abs(ForwardSpeed) / setup.steerAssistSpeed);
            return ApexMath.Lerp(maxLock, setup.highSpeedSteerDeg * ApexMath.Deg2Rad, ApexMath.SmoothStep01(t));
        }

        void UpdateSteering(float dt)
        {
            float target = ApexMath.Clamp(steer, -1f, 1f) * CurrentMaxLock();
            // Rack speed limit keeps the front from snapping on digital input.
            SteerAngle = ApexMath.MoveToward(SteerAngle, target, setup.steerRateDeg * ApexMath.Deg2Rad * dt);
            float ack = setup.ackermann * SteerAngle;
            for (int i = 0; i < 2; i++)
            {
                var w = wheels[i];
                bool inside = (SteerAngle > 0f && !w.left) || (SteerAngle < 0f && w.left);
                w.steer = SteerAngle + (inside ? ack : -ack * 0.5f);
            }
            wheels[2].steer = 0f;
            wheels[3].steer = 0f;
        }

        /// <summary>
        /// Traction control: feed-forward limit of drive torque to what the driven
        /// tires can transmit at their current load and slip angle (friction-circle
        /// budget at a target slip slightly below the peak), plus a gentle feedback
        /// trim if measured slip still exceeds the threshold. Catches launch
        /// wheelspin and mid-corner power oversteer without bang-bang oscillation.
        /// </summary>
        float ApplyTractionControl(float axleTorque, float throttleIn, float dt)
        {
            TcActive = false;
            if (!assistTraction || throttleIn < 0.05f || axleTorque <= 0f)
            {
                tcCut = ApexMath.MoveToward(tcCut, 0f, 6f * dt);
                return axleTorque;
            }
            float target = setup.tcCombinedThreshold * 0.92f;
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
            if (measured > setup.tcCombinedThreshold && Speed > 3f)
                tcCut = ApexMath.MoveToward(tcCut, 0.6f, 3f * dt);
            else
                tcCut = ApexMath.MoveToward(tcCut, 0f, 4f * dt);
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
