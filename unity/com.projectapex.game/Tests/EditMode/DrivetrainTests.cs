using Apex.Core;
using Apex.Vehicle;
using NUnit.Framework;

namespace Apex.Tests
{
    public class DrivetrainTests
    {
        [Test]
        public void EngineCurveLimiterAndBraking()
        {
            var e = new EngineModel();
            Assert.That(e.CurveAt(10000f), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(e.Torque(e.limiterRpm + 10f, 1f), Is.LessThanOrEqualTo(0f));
            Assert.That(e.Torque(9000f, 0f), Is.LessThan(0f));
            Assert.That(e.PowerKw(11000f), Is.InRange(600f, 760f));
        }

        [Test]
        public void GearboxShiftsAndLimits()
        {
            var g = new Gearbox();
            g.Reset(1);
            Assert.That(g.ShiftUp(), Is.True);
            Assert.That(g.IsShifting, Is.True);
            g.Update(1f);
            Assert.That(g.IsShifting, Is.False);
            g.gear = g.GearCount;
            Assert.That(g.ShiftUp(), Is.False);
            Assert.That(g.TotalRatio, Is.GreaterThan(0f));
            g.gear = -1;
            Assert.That(g.TotalRatio, Is.LessThan(0f));
        }

        [Test]
        public void RatiosDescendAndTopGearSpeedPlausible()
        {
            var g = new Gearbox();
            for (int i = 1; i < g.GearCount; i++) Assert.That(g.RatioFor(i), Is.GreaterThan(g.RatioFor(i + 1)));
            float top = 12300f / g.RatioFor(g.GearCount) * ApexMath.Tau / 60f * 0.36f * 3.6f;
            Assert.That(top, Is.InRange(320f, 380f));
        }

        [Test]
        public void AutoShiftUpThenDownWithoutHunting()
        {
            var g = new Gearbox();
            g.Reset(2);
            float wheelRpm = 12100f / g.RatioFor(2);
            Assert.That(g.AutoShift(wheelRpm, 1f, 0f, 40f), Is.True);
            Assert.That(g.gear, Is.EqualTo(3));
            // Immediately after a shift no further auto shift may happen (anti-hunting).
            Assert.That(g.AutoShift(5000f / g.RatioFor(3), 0f, 1f, 10f), Is.False);
            g.Update(1f);
            Assert.That(g.AutoShift(5000f / g.RatioFor(3), 0f, 1f, 10f), Is.True);
            Assert.That(g.gear, Is.EqualTo(2));
        }
    }
}
