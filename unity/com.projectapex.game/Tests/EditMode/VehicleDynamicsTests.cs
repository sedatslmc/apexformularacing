using System;
using System.Numerics;
using Apex.AI;
using Apex.Race;
using Apex.Tracks;
using Apex.Vehicle;
using NUnit.Framework;

namespace Apex.Tests
{
    /// <summary>Behavioural tests on the engine-independent flat-ground rig. They
    /// guard the handling targets; in-engine play-mode tests complement them.</summary>
    public class VehicleDynamicsTests
    {
        const float Dt = 1f / 120f;

        static FlatGroundRig NewRig(bool assists = true)
        {
            var sim = new VehicleSimulation(new VehicleSetup())
            {
                assistTraction = assists,
                assistAbs = assists,
                assistSteering = false,
            };
            var rig = new FlatGroundRig(sim);
            rig.Place(Vector3.Zero, new Vector3(0, 0, 1));
            return rig;
        }

        [Test]
        public void SpringModelCarriesWeightAtStaticCompression()
        {
            var setup = new VehicleSetup();
            var sim = new VehicleSimulation(setup);
            foreach (var w in sim.wheels)
            {
                w.contact = true;
                w.compression = w.staticCompression;
            }
            sim.PreStep(Dt, 0f, 0f);
            sim.Step(Dt);
            float total = 0f, front = 0f;
            foreach (var w in sim.wheels)
            {
                total += w.load;
                if (w.front) front += w.load;
            }
            Assert.That(total, Is.EqualTo(setup.mass * 9.81f).Within(setup.mass * 9.81f * 0.01f));
            Assert.That(front / total, Is.EqualTo(setup.weightFront).Within(0.01f));
        }

        [Test]
        public void StandingStartAcceleration()
        {
            var rig = NewRig();
            float t100 = -1f, t200 = -1f;
            for (int i = 0; i < 120 * 40; i++)
            {
                rig.sim.throttle = 1f;
                rig.Step(Dt);
                float kmh = rig.u * 3.6f;
                if (t100 < 0f && kmh >= 100f) t100 = rig.time;
                if (t200 < 0f && kmh >= 200f) t200 = rig.time;
            }
            TestContext.WriteLine($"0-100 {t100:F2}s, 0-200 {t200:F2}s, after 40s {rig.u * 3.6f:F0} km/h");
            Assert.That(t100, Is.InRange(2.0f, 3.6f), "0-100 km/h");
            Assert.That(t200, Is.InRange(4.0f, 7.5f), "0-200 km/h");
            Assert.That(rig.u * 3.6f, Is.InRange(300f, 365f), "top speed");
            Assert.That(Math.Abs(rig.heading), Is.LessThan(0.02f), "drives straight");
        }

        [Test]
        public void BrakingFrom200WithAbsIsShortAndStable()
        {
            var rig = NewRig();
            rig.u = 200f / 3.6f;
            foreach (var w in rig.sim.wheels) w.omega = rig.u / w.radius;
            rig.sim.gearbox.gear = 6;
            float start = rig.positionXZ.Y;
            float minSlip = 0f;
            int steps = 0;
            while (rig.u > 0.5f && steps++ < 120 * 10)
            {
                rig.sim.brake = 1f;
                rig.Step(Dt);
                // ABS works above ~3 m/s; check lock-up only where it is active.
                if (rig.u > 5f)
                    foreach (var w in rig.sim.wheels) minSlip = Math.Min(minSlip, w.slipRatio);
            }
            float distance = rig.positionXZ.Y - start;
            TestContext.WriteLine($"200-0: {distance:F1} m in {rig.time:F2} s, min slip {minSlip:F2}");
            Assert.That(distance, Is.InRange(45f, 110f));
            Assert.That(minSlip, Is.GreaterThan(-0.6f), "ABS prevents lock-up");
            Assert.That(Math.Abs(rig.heading), Is.LessThan(0.05f), "stays straight");
        }

        [Test]
        public void SteadyCorneringConvergesWithoutSpinning()
        {
            var rig = NewRig();
            rig.u = 150f / 3.6f;
            foreach (var w in rig.sim.wheels) w.omega = rig.u / w.radius;
            rig.sim.gearbox.gear = 5;
            float lastYaw = 0f;
            for (int i = 0; i < 120 * 8; i++)
            {
                rig.sim.steer = 0.15f;
                // Hold speed with a simple throttle controller.
                rig.sim.throttle = Math.Clamp((150f / 3.6f - rig.u) * 0.5f + 0.3f, 0f, 1f);
                rig.Step(Dt);
                lastYaw = rig.yawRate;
            }
            float beta = MathF.Atan2(rig.v, rig.u);
            TestContext.WriteLine($"yaw rate {lastYaw:F3} rad/s, sideslip {beta * 57.3f:F2} deg, lat g {rig.u * lastYaw / 9.81f:F2}");
            Assert.That(lastYaw, Is.GreaterThan(0.05f), "turns right for positive steer");
            Assert.That(Math.Abs(beta), Is.LessThan(0.1f), "body slip angle stays small");
            Assert.That(rig.u * 3.6f, Is.InRange(130f, 170f));
        }

        [TestCase("test_loop", 40.0, 75.0)]
        [TestCase("bosphorus_gp", 85.0, 140.0)]
        public void AutopilotCompletesValidLap(string trackId, double minLap, double maxLap)
        {
            var def = TestData.Circuit(trackId);
            var g = TrackGeometry.FromDefinition(def);
            var rig = NewRig();
            g.FrameAt(g.Wrap(def.startLineDistance - 15f), out var pos, out var fwd, out _, -3f);
            rig.Place(pos, fwd);
            var ap = new Autopilot(g, rig.sim.setup);
            var sectors = new float[def.sectorFractions.Length];
            for (int i = 0; i < sectors.Length; i++) sectors[i] = def.sectorFractions[i] * g.Length;
            var timer = new LapTimer(g.Length, sectors);
            int hint = -1;
            float maxLateral = 0f;
            int steps = 0;
            while (timer.completedLaps.Count < 2 && steps++ < 120 * 400)
            {
                var c = ap.Drive(rig.Position, rig.Forward, rig.u, rig.sim.setup.wheelbase, rig.sim.setup.maxSteerAngleDeg * MathF.PI / 180f);
                rig.sim.throttle = c.throttle;
                rig.sim.brake = c.brake;
                rig.sim.steer = c.steer;
                rig.Step(Dt);
                var proj = g.Project(rig.Position, hint);
                hint = proj.index;
                if (timer.LapActive) maxLateral = Math.Max(maxLateral, Math.Abs(proj.lateral) - g.Widths[proj.index] * 0.5f);
                timer.Update(g.Wrap(proj.distance - def.startLineDistance), rig.time);
            }
            Assert.That(timer.completedLaps.Count, Is.GreaterThanOrEqualTo(2), "two laps completed");
            var flying = timer.completedLaps[1];
            TestContext.WriteLine($"{trackId}: flying lap {flying.time:F3} s, max beyond edge {maxLateral:F2} m");
            Assert.That(flying.time, Is.InRange(minLap, maxLap));
            Assert.That(maxLateral, Is.LessThan(1.5f), "stays within track limits (incl. kerbs)");
        }
    }
}
