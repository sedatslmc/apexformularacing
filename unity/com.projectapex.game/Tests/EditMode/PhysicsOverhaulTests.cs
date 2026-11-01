using System;
using System.Numerics;
using Apex.Core;
using Apex.Vehicle;
using NUnit.Framework;

namespace Apex.Tests
{
    /// <summary>Engine inertia, clutch, ground effect, assist levels, kerbs and braking
    /// behaviour of the formula-car model (flat-ground rig).</summary>
    public class PhysicsOverhaulTests
    {
        const float Dt = 1f / 120f;

        static FlatGroundRig Rig(float kmh = 0f, int gear = 1)
        {
            var sim = new VehicleSimulation(new VehicleSetup());
            var rig = new FlatGroundRig(sim);
            rig.Place(Vector3.Zero, new Vector3(0, 0, 1));
            if (kmh > 0f)
            {
                rig.u = kmh / 3.6f;
                foreach (var w in sim.wheels) w.omega = rig.u / w.radius;
                sim.gearbox.gear = gear; sim.SyncEngineToWheels();
            }
            return rig;
        }

        [Test]
        public void EngineRevsWithInertiaInNeutral()
        {
            var rig = Rig();
            rig.sim.autoGearbox = false;
            rig.sim.gearbox.gear = 0;
            rig.sim.throttle = 1f;
            rig.Step(Dt);
            for (int i = 0; i < 5; i++) rig.Step(Dt);
            float early = rig.sim.EngineRpm;
            for (int i = 0; i < 120; i++) rig.Step(Dt);
            Assert.That(early, Is.LessThan(9000f), "revs must build up, not jump");
            Assert.That(rig.sim.EngineRpm, Is.GreaterThan(11500f));
            Assert.That(rig.sim.EngineRpm, Is.LessThanOrEqualTo(rig.sim.engine.maxRpm + 1f));
            rig.sim.throttle = 0f;
            for (int i = 0; i < 240; i++) rig.Step(Dt);
            Assert.That(rig.sim.EngineRpm, Is.EqualTo(rig.sim.engine.idleRpm).Within(400f), "idle control");
        }

        [Test]
        public void DownshiftBlipsAndDoesNotLockTheRear()
        {
            var rig = Rig(160f, 5);
            rig.sim.autoGearbox = false;
            for (int i = 0; i < 30; i++) { rig.sim.throttle = 0.4f; rig.Step(Dt); }
            float before = rig.sim.EngineRpm;
            rig.sim.throttle = 0f;
            rig.sim.RequestShiftDown();
            float minSlip = 0f;
            for (int i = 0; i < 36; i++)
            {
                rig.Step(Dt);
                minSlip = MathF.Min(minSlip, rig.sim.wheels[2].slipRatio);
            }
            float expected = rig.u / rig.sim.wheels[2].radius * rig.sim.gearbox.TotalRatio * 60f / ApexMath.Tau;
            Assert.That(rig.sim.gearbox.gear, Is.EqualTo(4));
            Assert.That(rig.sim.EngineRpm, Is.GreaterThan(before + 1000f));
            Assert.That(rig.sim.EngineRpm, Is.EqualTo(expected).Within(expected * 0.05f));
            Assert.That(minSlip, Is.GreaterThan(-0.1f), "rev-matched downshift must not lock the rear");
        }

        [Test]
        public void UpshiftDropsRevsAndKeepsDriving()
        {
            var rig = Rig(120f, 3);
            rig.sim.autoGearbox = false;
            for (int i = 0; i < 30; i++) { rig.sim.throttle = 1f; rig.Step(Dt); }
            float before = rig.sim.EngineRpm;
            float v0 = rig.u;
            rig.sim.RequestShiftUp();
            for (int i = 0; i < 60; i++) { rig.sim.throttle = 1f; rig.Step(Dt); }
            Assert.That(rig.sim.EngineRpm, Is.LessThan(before));
            Assert.That(rig.sim.ClutchSlipping, Is.False);
            Assert.That(rig.u, Is.GreaterThan(v0 + 3f));
        }

        static VehicleSimulation SimAtRideHeight(float deltaCompression, float kmh)
        {
            var sim = new VehicleSimulation(new VehicleSetup());
            foreach (var w in sim.wheels)
            {
                w.contact = true;
                w.compression = w.staticCompression + deltaCompression;
                w.longitudinalVelocity = kmh / 3.6f;
            }
            sim.PreStep(Dt, kmh / 3.6f, kmh / 3.6f);
            sim.Step(Dt);
            return sim;
        }

        [Test]
        public void GroundEffectGrowsAsTheCarRunsLower()
        {
            var high = SimAtRideHeight(0f, 250f);
            var low = SimAtRideHeight(0.025f, 250f);
            float dfHigh = high.DownforceFront + high.DownforceRear;
            float dfLow = low.DownforceFront + low.DownforceRear;
            Assert.That(dfLow, Is.GreaterThan(dfHigh * 1.08f));
            Assert.That(low.FloorGain, Is.GreaterThan(1f));
            Assert.That(low.Drag, Is.GreaterThan(high.Drag), "induced drag");
        }

        [Test]
        public void FloorStallsWhenThePlankHitsTheGround()
        {
            var nearGround = SimAtRideHeight(0.045f, 250f);
            var grounded = SimAtRideHeight(0.06f, 250f);
            Assert.That(grounded.Bottoming, Is.True);
            Assert.That(grounded.FloorGain, Is.LessThan(nearGround.FloorGain));
        }

        [Test]
        public void BrakingDiveMovesAeroBalanceForward()
        {
            var setup = new VehicleSetup();
            var sim = new VehicleSimulation(setup);
            foreach (var w in sim.wheels)
            {
                w.contact = true;
                w.compression = w.staticCompression + (w.front ? 0.015f : -0.005f);
            }
            sim.PreStep(Dt, 70f, 70f);
            sim.Step(Dt);
            // Weaker since 2026-11 (aeroRakeSensitivity 0.3): the direction stays, the size is a third.
            Assert.That(sim.AeroBalance, Is.GreaterThan(setup.aeroBalanceFront + 0.004f));
        }

        [Test]
        public void TopSpeedStaysPlausibleWithGroundEffect()
        {
            var rig = Rig();
            for (int i = 0; i < 120 * 40; i++) { rig.sim.throttle = 1f; rig.Step(Dt); }
            Assert.That(rig.u * 3.6f, Is.InRange(310f, 350f));
        }

        [Test]
        public void ProfilesSetAssistLevels()
        {
            var s = new SettingsData();
            Assert.That(s.drivingProfile, Is.EqualTo(DrivingProfile.Balanced));
            Assert.That(s.tractionLevel, Is.EqualTo(2));
            Assert.That(s.SteeringAssistStrength, Is.EqualTo(0.5f));
            s.ApplyProfile(DrivingProfile.Expert);
            Assert.That(s.tractionLevel, Is.EqualTo(1));
            Assert.That(s.abs, Is.True, "digital pedals need ABS (keyboard lap test)");
            Assert.That(s.SteeringAssistStrength, Is.EqualTo(0f));
            Assert.That(s.speedSensitiveSteering, Is.True);
            s.ApplyProfile(DrivingProfile.Casual);
            Assert.That(s.tractionLevel, Is.EqualTo(3));
            Assert.That(s.SteeringAssistStrength, Is.EqualTo(1f));
            s.tractionLevel = 1;
            s.ApplyProfile(DrivingProfile.Custom);
            Assert.That(s.tractionLevel, Is.EqualTo(1), "custom keeps the player's values");
        }

        [Test]
        public void OldSettingsMigrateToBalanced()
        {
            var s = new SettingsData { version = 1, tractionLevel = 0, steeringAssistLevel = 2, brakeBiasFront = 0.9f };
            Assert.That(s.MigrateAndValidate(), Is.True);
            Assert.That(s.version, Is.EqualTo(SettingsData.CurrentVersion));
            Assert.That(s.drivingProfile, Is.EqualTo(DrivingProfile.Balanced));
            Assert.That(s.tractionLevel, Is.EqualTo(2));
            Assert.That(s.brakeBiasFront, Is.EqualTo(0.65f));
        }

        static float SteadyLock(float strength, float kmh)
        {
            var rig = Rig(kmh, 5);
            rig.sim.steeringAssist = strength;
            for (int i = 0; i < 120; i++)
            {
                rig.u = kmh / 3.6f;
                rig.sim.steer = 1f;
                rig.Step(Dt);
            }
            return MathF.Abs(rig.sim.SteerAngle);
        }

        [Test]
        public void LightSteeringAssistLeavesMoreLockThanFull()
        {
            float full = SteadyLock(1f, 150f);
            float light = SteadyLock(0.5f, 150f);
            float off = SteadyLock(0f, 150f);
            Assert.That(light, Is.GreaterThan(full * 1.2f));
            Assert.That(off, Is.GreaterThan(light));
        }

        static float LaunchPeakSlip(int level)
        {
            var rig = Rig();
            rig.sim.tractionLevel = level;
            float peak = 0f;
            for (int i = 0; i < 360; i++)
            {
                rig.sim.throttle = 1f;
                rig.Step(Dt);
                if (rig.u > 5f) peak = MathF.Max(peak, rig.sim.wheels[2].slipRatio);
            }
            return peak;
        }

        [Test]
        public void TractionLevelsTradeSlipForControl()
        {
            float low = LaunchPeakSlip(1);
            float high = LaunchPeakSlip(3);
            float off = LaunchPeakSlip(0);
            Assert.That(high, Is.LessThan(low));
            Assert.That(low, Is.LessThan(off));
            Assert.That(high, Is.LessThan(0.15f));
        }

        [Test]
        public void KerbRipplesExciteTheSuspension()
        {
            float Spread(SurfaceType surface)
            {
                var sim = new VehicleSimulation(new VehicleSetup());
                float min = float.MaxValue, max = 0f;
                for (int i = 0; i < 120; i++)
                {
                    foreach (var w in sim.wheels)
                    {
                        w.contact = true;
                        w.compression = w.staticCompression;
                        w.longitudinalVelocity = 30f;
                        w.surface = surface;
                    }
                    sim.PreStep(Dt, 30f, 30f);
                    sim.Step(Dt);
                    min = MathF.Min(min, sim.wheels[0].load);
                    max = MathF.Max(max, sim.wheels[0].load);
                }
                return max - min;
            }
            Assert.That(Spread(SurfaceType.Asphalt), Is.LessThan(50f));
            Assert.That(Spread(SurfaceType.Kerb), Is.GreaterThan(500f));
        }

        static FlatGroundRig CorneringRig(float kmh)
        {
            var rig = Rig(kmh, 5);
            rig.sim.steeringAssist = 0f;
            for (int i = 0; i < 120; i++)
            {
                rig.sim.steer = 0.18f;
                rig.sim.throttle = Math.Clamp((kmh / 3.6f - rig.u) * 0.4f + 0.3f, 0f, 1f);
                rig.Step(Dt);
            }
            return rig;
        }

        [Test]
        public void TrailBrakingWithAbsStaysStable()
        {
            var rig = CorneringRig(180f);
            rig.sim.assistAbs = true;
            float maxBeta = 0f;
            float v0 = rig.u;
            for (int i = 0; i < 90; i++)
            {
                // Release the brake as the steering is held: classic trail braking.
                rig.sim.brake = 1f - i / 90f;
                rig.sim.throttle = 0f;
                rig.sim.steer = 0.18f;
                rig.Step(Dt);
                maxBeta = MathF.Max(maxBeta, MathF.Abs(MathF.Atan2(rig.v, rig.u)));
            }
            Assert.That(maxBeta, Is.LessThan(0.12f));
            Assert.That(v0 - rig.u, Is.GreaterThan(10f));
        }

        /// <summary>Time (s) at which the front and rear wheels first lock under full braking without ABS.</summary>
        static (float front, float rear) FirstLock(float bias)
        {
            var rig = Rig(250f, 7);
            rig.sim.assistAbs = false;
            rig.sim.setup.brakeBiasFront = bias;
            float tf = float.PositiveInfinity, tr = float.PositiveInfinity;
            for (int i = 0; i < 480 && rig.u > 3f; i++)
            {
                rig.sim.brake = 0.75f;
                rig.Step(Dt);
                if (float.IsPositiveInfinity(tf) && rig.sim.wheels[0].slipRatio < -0.5f) tf = rig.time;
                if (float.IsPositiveInfinity(tr) && rig.sim.wheels[2].slipRatio < -0.5f) tr = rig.time;
            }
            return (tf, tr);
        }

        [Test]
        public void FrontsLockFirstWithoutAbsUnderStandardBias()
        {
            var (front, rear) = FirstLock(0.57f);
            Assert.That(front, Is.LessThan(rear));
        }

        [Test]
        public void RearwardBiasLocksTheRearFirst()
        {
            var (front, rear) = FirstLock(0.5f);
            Assert.That(rear, Is.LessThan(front));
        }

        [Test]
        public void SpeedSensitiveSteeringLimitsLockWithoutAssist()
        {
            var rig = Rig(200f, 6);
            rig.sim.steeringAssist = 0f;
            rig.sim.PreStep(Dt, rig.u, rig.u);
            float limited = rig.sim.CurrentMaxLock();
            rig.sim.speedSensitiveSteering = false;
            float raw = rig.sim.CurrentMaxLock();
            rig.sim.steeringAssist = 1f;
            float assisted = rig.sim.CurrentMaxLock();
            Assert.That(raw, Is.EqualTo(rig.sim.setup.maxSteerAngleDeg * ApexMath.Deg2Rad).Within(1e-4f));
            Assert.That(limited, Is.LessThan(raw * 0.6f));
            Assert.That(limited, Is.GreaterThan(assisted), "more lock than the assist: the front can be overdriven");
        }

        [Test]
        public void SteeringWheelFollowsTheRack()
        {
            var rig = Rig(60f, 2);
            for (int i = 0; i < 60; i++) { rig.sim.steer = -0.5f; rig.Step(Dt); }
            Assert.That(rig.sim.SteeringWheelAngleDeg, Is.EqualTo(rig.sim.SteerAngle * ApexMath.Rad2Deg * rig.sim.setup.steeringWheelRatio).Within(1e-3f));
            Assert.That(rig.sim.SteeringWheelAngleDeg, Is.LessThan(-10f));
        }

        [Test]
        public void FrontWingDamageCostsFrontDownforceAndIsRepaired()
        {
            var setup = new VehicleSetup();
            var sim = SimAtRideHeight(0f, 250f);
            float intactFront = sim.DownforceFront;
            sim.damageEnabled = true;
            string hit = sim.damage.ApplyImpact(setup, new System.Numerics.Vector3(0.3f, -0.2f, setup.FrontAxleZ + 0.9f), 12f, new System.Numerics.Vector3(0f, 0f, -1f));
            Assert.That(hit, Does.Contain("Frontflügel"));
            sim.PreStep(Dt, 250f / 3.6f, 250f / 3.6f);
            sim.Step(Dt);
            Assert.That(sim.DownforceFront, Is.LessThan(intactFront * 0.5f));
            sim.damage.Repair(true);
            Assert.That(sim.damage.Any, Is.False);
        }

        [Test]
        public void SmallTapsLeaveNoDamage()
        {
            var d = new DamageState();
            Assert.That(d.ApplyImpact(new VehicleSetup(), System.Numerics.Vector3.Zero, 2f, new System.Numerics.Vector3(1f, 0f, 0f)), Is.Null);
            Assert.That(d.Any, Is.False);
        }

        [Test]
        public void BentSuspensionPullsTheCar()
        {
            var setup = new VehicleSetup();
            var rig = Rig(150f, 5);
            rig.sim.damageEnabled = true;
            var fr = setup.MountPosition(1, out _);
            rig.sim.damage.ApplyImpact(setup, fr, 8f, new System.Numerics.Vector3(-1f, 0f, 0f));
            for (int i = 0; i < 240; i++)
            {
                rig.sim.steer = 0f;
                rig.sim.throttle = 0.3f;
                rig.Step(Dt);
            }
            Assert.That(MathF.Abs(rig.positionXZ.X), Is.GreaterThan(1f), "drifts off line without steering");
        }

        [Test]
        public void PunctureCostsGripAndSpeed()
        {
            var a = Rig(200f, 6);
            var b = Rig(200f, 6);
            b.sim.damageEnabled = true;
            b.sim.damage.puncture[2] = true;
            for (int i = 0; i < 600; i++)
            {
                a.sim.throttle = b.sim.throttle = 1f;
                a.sim.steer = b.sim.steer = 0f;
                a.Step(Dt);
                b.Step(Dt);
            }
            Assert.That(b.u, Is.LessThan(a.u - 2f));
        }
    }
}
