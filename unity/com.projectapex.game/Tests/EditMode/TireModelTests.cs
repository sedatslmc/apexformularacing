using Apex.Vehicle;
using NUnit.Framework;

namespace Apex.Tests
{
    public class TireModelTests
    {
        [Test]
        public void ZeroSlipGivesZeroForce()
        {
            new TireModel().Compute(3000f, 0f, 0f, 1f, out float fx, out float fy);
            Assert.That(fx, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(fy, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void LongitudinalPeakAtConfiguredSlip()
        {
            var t = new TireModel();
            float fz = t.nominalLoad;
            t.Compute(fz, t.slipRatioPeak, 0f, 1f, out float peak, out _);
            Assert.That(peak, Is.EqualTo(t.muPeak * fz).Within(1f));
            t.Compute(fz, t.slipRatioPeak * 0.5f, 0f, 1f, out float below, out _);
            t.Compute(fz, t.slipRatioPeak * 3f, 0f, 1f, out float above, out _);
            t.Compute(fz, t.slipRatioPeak * 20f, 0f, 1f, out float sliding, out _);
            Assert.That(below, Is.LessThan(peak));
            Assert.That(above, Is.LessThan(peak));
            Assert.That(sliding, Is.GreaterThan(peak * t.slideFloor * 0.98f));
        }

        [Test]
        public void LateralForceOpposesSlip()
        {
            var t = new TireModel();
            t.Compute(3000f, 0f, 0.05f, 1f, out _, out float fyPos);
            t.Compute(3000f, 0f, -0.05f, 1f, out _, out float fyNeg);
            Assert.That(fyPos, Is.LessThan(0f));
            Assert.That(fyNeg, Is.GreaterThan(0f));
        }

        [Test]
        public void CombinedForceStaysInsideFrictionCircle()
        {
            var t = new TireModel();
            float fz = 4000f;
            float limit = t.MuAtLoad(fz) * fz;
            foreach (float sr in new[] { 0f, 0.05f, 0.1f, 0.3f, -0.2f })
            foreach (float sa in new[] { 0f, 0.05f, 0.12f, 0.4f, -0.3f })
            {
                t.Compute(fz, sr, sa, 1f, out float fx, out float fy);
                Assert.That(System.MathF.Sqrt(fx * fx + fy * fy), Is.LessThanOrEqualTo(limit * 1.0001f), $"sr={sr} sa={sa}");
            }
        }

        [Test]
        public void LoadSensitivityReducesMu()
        {
            var t = new TireModel();
            Assert.That(t.MuAtLoad(8000f), Is.LessThan(t.MuAtLoad(2000f)));
        }

        [Test]
        public void SurfaceGripScalesForceLinearly()
        {
            var t = new TireModel();
            t.Compute(3000f, 0.1f, 0f, 1f, out float a, out _);
            t.Compute(3000f, 0.1f, 0f, 0.5f, out float b, out _);
            Assert.That(b, Is.EqualTo(a * 0.5f).Within(1f));
        }
    }
}
