using System;
using System.Numerics;
using Apex.Vehicle;
using NUnit.Framework;

namespace Apex.Tests
{
    /// <summary>Slipstream (tow) and dirty air (downforce loss) behind another car.</summary>
    public class SlipstreamTests
    {
        const float Dt = 1f / 120f;

        [Test]
        public void TheWakeIsAConeBehindTheLeaderThatFadesWithDistance()
        {
            var at10 = Slipstream.Wake(10f, 0f, 80f);
            var at20 = Slipstream.Wake(20f, 0f, 80f);
            var at60 = Slipstream.Wake(60f, 0f, 80f);
            Assert.That(at20.tow, Is.InRange(0.08f, 0.15f), "about a tenth less drag at 20 m");
            Assert.That(at10.tow, Is.GreaterThan(at20.tow));
            Assert.That(at60.tow, Is.GreaterThan(0.01f).And.LessThan(at20.tow * 0.3f), "a little tow a second behind");
            Assert.That(Slipstream.Wake(130f, 0f, 80f).tow, Is.EqualTo(0f));
            // Dirty air: front wing hit harder than the rear; fades faster than the tow.
            Assert.That(at20.frontLoss, Is.InRange(0.1f, 0.25f), "current-generation cars: roughly 15-20 % front loss at 20 m");
            Assert.That(at20.frontLoss, Is.GreaterThan(at20.rearLoss));
            Assert.That(at60.frontLoss / at20.frontLoss, Is.LessThan(at60.tow / at20.tow), "dirty air fades faster than the tow");
            // Out of the cone: one car width to the side at 15 m there is clean air.
            Assert.That(Slipstream.Wake(15f, 3f, 80f).tow, Is.LessThan(Slipstream.Wake(15f, 0f, 80f).tow * 0.2f));
            // Ahead of the leader, or behind a slow car: nothing.
            Assert.That(Slipstream.Wake(-10f, 0f, 80f).tow, Is.EqualTo(0f));
            Assert.That(Slipstream.Wake(15f, 0f, 15f).tow, Is.EqualTo(0f), "no wake at walking pace / pit speed");
        }

        [Test]
        public void CarsAheadCombineAndOncomingCarsGiveNoTow()
        {
            var fwd = new Vector3(0f, 0f, 1f);
            // Follower (0) at z = 0, leaders at z = 20 and z = 40, an oncoming car at z = 15.
            var pos = new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 20f), new Vector3(0f, 0f, 40f), new Vector3(0f, 0f, 15f) };
            var dirs = new[] { fwd, fwd, fwd, -fwd };
            var spd = new[] { 80f, 80f, 80f, 80f };
            var both = Slipstream.For(0, pos, dirs, spd);
            var one = Slipstream.Wake(20f, 0f, 80f);
            Assert.That(both.tow, Is.GreaterThan(one.tow), "two cars ahead tow more than one");
            Assert.That(both.tow, Is.LessThan(one.tow + Slipstream.Wake(40f, 0f, 80f).tow + 1e-4f), "but not more than the sum");
            Assert.That(both.nearest, Is.EqualTo(20f).Within(0.01f));
            // The leader itself gets nothing from the follower behind it.
            Assert.That(Slipstream.For(2, pos, dirs, spd).tow, Is.EqualTo(0f));
            // Without the oncoming car the result is the same.
            var three = Slipstream.For(0, new[] { pos[0], pos[1], pos[2] }, new[] { fwd, fwd, fwd }, new[] { 80f, 80f, 80f });
            Assert.That(both.tow, Is.EqualTo(three.tow).Within(1e-6f));
        }

        [Test]
        public void TheFollowerClosesUpOnAStraightAndLosesFrontDownforce()
        {
            // Two cars flat out on a straight, 25 m apart at 200 km/h: the follower in the tow closes the gap. (Measured
            // while accelerating: at 330 km/h the cars reach the limiter in top gear and the tow no longer adds speed.)
            var leader = new FlatGroundRig(new VehicleSimulation(new VehicleSetup()));
            var follower = new FlatGroundRig(new VehicleSimulation(new VehicleSetup()));
            var solo = new FlatGroundRig(new VehicleSimulation(new VehicleSetup()));
            leader.Place(new Vector3(0f, 0f, 25f), Vector3.UnitZ);
            follower.Place(Vector3.Zero, Vector3.UnitZ);
            solo.Place(new Vector3(50f, 0f, 0f), Vector3.UnitZ);
            foreach (var r in new[] { leader, follower, solo }) { r.SetSpeed(200f / 3.6f, 5); r.sim.throttle = 1f; r.sim.brake = 0f; }
            float maxFrontLoss = 0f;
            for (int k = 0; k < 120 * 6; k++)
            {
                var pos = new[] { leader.Position, follower.Position };
                var fwd = new[] { leader.Forward, follower.Forward };
                var spd = new[] { leader.u, follower.u };
                leader.sim.wake = Slipstream.For(0, pos, fwd, spd);
                follower.sim.wake = Slipstream.For(1, pos, fwd, spd);
                maxFrontLoss = MathF.Max(maxFrontLoss, follower.sim.wake.frontLoss);
                leader.Step(Dt); follower.Step(Dt); solo.Step(Dt);
            }
            float gap = leader.positionXZ.Y - follower.positionXZ.Y;
            TestContext.WriteLine($"gap after 6 s: {gap:F1} m (from 25), speeds leader {leader.u * 3.6f:F0} follower {follower.u * 3.6f:F0} solo {solo.u * 3.6f:F0} km/h");
            Assert.That(gap, Is.LessThan(22f), "the tow pulls the follower up");
            Assert.That(follower.u, Is.GreaterThan(solo.u + 0.8f), "faster than a car in clean air");
            Assert.That(leader.sim.wake.tow, Is.EqualTo(0f));
            Assert.That(maxFrontLoss, Is.GreaterThan(0.1f));
            // With the wake the follower's downforce sits further back: balance shifts towards understeer.
            float share = follower.sim.DownforceFront / (follower.sim.DownforceFront + follower.sim.DownforceRear);
            float clean = solo.sim.DownforceFront / (solo.sim.DownforceFront + solo.sim.DownforceRear);
            Assert.That(share, Is.LessThan(clean - 0.01f));
        }
    }
}
