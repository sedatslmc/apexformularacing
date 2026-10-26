using Apex.InputLogic;
using Apex.Vehicle;
using NUnit.Framework;

namespace Apex.Tests
{
    /// <summary>
    /// Formula handling targets on the 6-DOF rig (flat pad, in-game force code). Baseline before the
    /// overhaul (Balanced): 1.72 / 2.24 / 3.20 g at 80 / 150 / 250 km/h, front/rear slip use at the limit
    /// 2.0 / 1.8 / 1.8 (strong understeer), 300→80 km/h in 99 m. See docs/HANDLING_OVERHAUL.md.
    /// </summary>
    public class HandlingBenchmarkTests
    {
        [Test]
        [TestCase(80f, 1.85f)]
        [TestCase(150f, 2.55f)]
        [TestCase(250f, 3.9f)]
        public void CorneringGripRisesWithSpeedAndBothAxlesWork(float kmh, float minG)
        {
            var c = new HandlingBenchmark(null, 1).CorneringCapacity(kmh);
            TestContext.WriteLine($"{kmh} km/h: {c.maxLateralG:F2} g, balance {c.Balance:F2}");
            Assert.That(c.maxLateralG, Is.GreaterThan(minG), "downforce makes fast corners feel different");
            // Mild understeer at the limit: stable, but the rear does real work (was 1.8-2.0).
            Assert.That(c.Balance, Is.InRange(1.1f, 1.55f));
        }

        [Test]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void TransientsStayOnTheTyresInEveryProfile(int profile)
        {
            var hb = new HandlingBenchmark(null, profile);
            foreach (float kmh in new[] { 80f, 150f, 250f })
            {
                var l = hb.LimitStep(kmh);
                Assert.That(l.spun, Is.False, $"full steering step at {kmh} km/h");
            }
            foreach (float kmh in new[] { 120f, 200f, 260f })
                Assert.That(hb.TrailBrake(kmh).spun, Is.False, $"trail braking at {kmh} km/h");
            foreach (float kmh in new[] { 60f, 100f })
                Assert.That(hb.PowerExit(kmh).spun, Is.False, $"power exit at {kmh} km/h");
        }

        [Test]
        public void SteeringRespondsWithoutLagOrOvershoot()
        {
            var hb = new HandlingBenchmark(null, 1);
            foreach (float kmh in new[] { 80f, 150f, 250f })
            {
                var r = hb.SteeringResponse(kmh);
                TestContext.WriteLine($"{kmh} km/h: t63 {r.t63ms:F0} ms, overshoot {r.overshootPct:F0} %, 1 Hz lag {r.sineLagMs:F0} ms");
                Assert.That(r.t63ms, Is.LessThan(90f));
                Assert.That(r.sineLagMs, Is.LessThan(60f));
                Assert.That(r.overshootPct, Is.LessThan(25f));
            }
        }

        [Test]
        public void FormulaBrakingFromHighSpeed()
        {
            var b = new HandlingBenchmark(null, 1).Brake(300f, 80f);
            TestContext.WriteLine($"300->80: {b.distance:F0} m, mean {b.meanG:F2} g, peak {b.peakG:F2} g");
            Assert.That(b.distance, Is.LessThan(95f));
            Assert.That(b.peakG, Is.GreaterThan(5.5f));
        }

        [Test]
        public void LegacySetupReproducesThePreOverhaulBaseline()
        {
            // The in-game A/B switch is only useful if "Alt" really is the old car.
            var legacy = VehicleSetup.Legacy();
            Assert.That(legacy.escEnabled, Is.False);
            Assert.That(legacy.legacy, Is.True);
            var oldC = new HandlingBenchmark(legacy, 1).CorneringCapacity(80f);
            var newC = new HandlingBenchmark(null, 1).CorneringCapacity(80f);
            TestContext.WriteLine($"80 km/h: legacy {oldC.maxLateralG:F2} g / {oldC.Balance:F2}, new {newC.maxLateralG:F2} g / {newC.Balance:F2}");
            Assert.That(oldC.maxLateralG, Is.EqualTo(1.72f).Within(0.05f));
            Assert.That(oldC.Balance, Is.GreaterThan(1.8f), "old car pushes at the limit");
            Assert.That(InputTuning.SteerPressRate(1f, true), Is.LessThan(InputTuning.SteerPressRate(1f)));
            Assert.That(InputTuning.StickSteer(0.5f, true), Is.EqualTo(0.5f), "old stick was linear");
        }

        [Test]
        public void StickCurveHasFullLockAndFineCentre()
        {
            Assert.That(InputTuning.StickSteer(0f), Is.EqualTo(0f));
            Assert.That(InputTuning.StickSteer(1f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(InputTuning.StickSteer(-1f), Is.EqualTo(-1f).Within(1e-5f));
            float half = InputTuning.StickSteer(0.5f);
            Assert.That(half, Is.InRange(0.3f, 0.5f), "finer than linear around the centre");
            Assert.That(InputTuning.StickSteer(0.05f), Is.GreaterThan(0f).And.LessThan(0.05f), "no second dead zone on top of the Input System's");
        }
    }
}
