using System;
using Apex.InputLogic;
using NUnit.Framework;

namespace Apex.Tests
{
    /// <summary>Virtual steering zone for touch screens.</summary>
    public class TouchWheelTests
    {
        const float Dt = 1f / 60f;

        [Test]
        public void ThumbTravelSteersAnalogueFromWhereItLands()
        {
            var w = new TouchWheel { range = 0.12f };
            Assert.That(w.Update(true, 0.30f, Dt), Is.EqualTo(0f), "landing point = straight ahead, wherever it is");
            Assert.That(w.Update(true, 0.305f, Dt), Is.EqualTo(0f), "small jitter inside the dead zone");
            float half = w.Update(true, 0.36f, Dt);
            Assert.That(half, Is.InRange(0.3f, 0.5f), "half the travel: less than half the lock (finer centre)");
            Assert.That(w.Update(true, 0.42f, Dt), Is.EqualTo(1f).Within(1e-4f), "full travel: full lock");
            Assert.That(w.Update(true, 0.18f, Dt), Is.EqualTo(-1f).Within(1e-4f), "other side");
        }

        [Test]
        public void TheCentreFollowsAnOvershootingThumb()
        {
            var w = new TouchWheel { range = 0.12f };
            w.Update(true, 0.30f, Dt);
            w.Update(true, 0.60f, Dt);   // far past full lock
            // Coming back by a little steers less at once (no dead travel back over the overshoot).
            Assert.That(w.Update(true, 0.54f, Dt), Is.LessThan(0.6f));
            Assert.That(w.Update(true, 0.48f, Dt), Is.LessThan(0.1f), "back at the (moved) centre");
        }

        [Test]
        public void LiftingTheThumbCentresTheSteeringQuickly()
        {
            var w = new TouchWheel { range = 0.12f };
            w.Update(true, 0.3f, Dt);
            w.Update(true, 0.45f, Dt);
            float t = 0f;
            while (Math.Abs(w.Update(false, 0f, Dt)) > 1e-4f) t += Dt;
            Assert.That(t, Is.LessThan(0.1f));
            // A new touch starts from its own landing point again.
            Assert.That(w.Update(true, 0.7f, Dt), Is.EqualTo(0f));
        }
    }
}
