using Apex.Core;
using NUnit.Framework;

namespace Apex.Tests
{
    /// <summary>Camera feedback (handling brief section 7): subtle, smooth, off with reduced motion.</summary>
    public class CameraDynamicsTests
    {
        static CameraDynamics Settle(float speed, float longA, float latA, float strength = 1f, float seconds = 1.5f)
        {
            var c = new CameraDynamics { strength = strength };
            for (float t = 0f; t < seconds; t += 1f / 60f) c.Update(speed, longA, latA, 1f / 60f);
            return c;
        }

        [Test]
        public void SpeedWidensTheViewAndBrakingPullsTheCameraIn()
        {
            Assert.That(Settle(0f, 0f, 0f).FovOffset, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(Settle(83f, 0f, 0f).FovOffset, Is.EqualTo(CameraDynamics.MaxFovGain).Within(0.01f));
            Assert.That(Settle(40f, 0f, 0f).FovOffset, Is.InRange(1f, 4f));
            var brake = Settle(70f, -5f * 9.81f, 0f);
            Assert.That(brake.Back, Is.LessThan(-0.4f), "closer under hard braking");
            Assert.That(brake.Pitch, Is.GreaterThan(0.9f).And.LessThanOrEqualTo(CameraDynamics.MaxPitch), "nose down, at most 1.2°");
            var accel = Settle(30f, 1.5f * 9.81f, 0f);
            Assert.That(accel.Back, Is.GreaterThan(0.25f), "drops back when accelerating");
            var right = Settle(50f, 0f, 4f * 9.81f);
            Assert.That(right.Side, Is.LessThan(-0.2f), "to the outside of a right-hand corner");
        }

        [Test]
        public void ItIsSmoothAndOffWithReducedMotion()
        {
            // A one-frame kerb spike of 8 g barely moves the camera.
            var c = Settle(60f, 0f, 0f);
            c.Update(60f, -8f * 9.81f, 0f, 1f / 60f);
            Assert.That(c.Back, Is.GreaterThan(-0.05f));
            // A frame hitch does not kick it either.
            c.Update(60f, -3f * 9.81f, 0f, 0.5f);
            Assert.That(float.IsNaN(c.Back), Is.False);
            Assert.That(c.Back, Is.GreaterThan(-CameraDynamics.BrakeCloser - 1e-3f));
            var off = Settle(83f, -5f * 9.81f, 4f * 9.81f, strength: 0f);
            Assert.That(off.FovOffset + System.Math.Abs(off.Back) + System.Math.Abs(off.Pitch) + System.Math.Abs(off.Side), Is.EqualTo(0f));
        }
    }
}
