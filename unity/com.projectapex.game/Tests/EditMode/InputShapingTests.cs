using Apex.InputLogic;
using NUnit.Framework;

namespace Apex.Tests
{
    public class InputShapingTests
    {
        [Test]
        public void PedalDeadzoneAndCurve()
        {
            Assert.That(InputTuning.Pedal(0.02f, 0.05f, 1f), Is.EqualTo(0f));
            Assert.That(InputTuning.Pedal(1f, 0.2f, 2f), Is.EqualTo(1f).Within(1e-6f), "full travel stays full");
            Assert.That(InputTuning.Pedal(0.5f, 0f, 1f), Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(InputTuning.Pedal(0.5f, 0f, 2f), Is.LessThan(0.3f), "curve > 1 is finer at the start");
            Assert.That(InputTuning.Pedal(0.5f, 0f, 0.5f), Is.GreaterThan(0.7f));
            float last = 0f;
            for (float v = 0f; v <= 1f; v += 0.01f)
            {
                float o = InputTuning.Pedal(v, 0.1f, 1.6f);
                Assert.That(o, Is.GreaterThanOrEqualTo(last - 1e-6f), "monotonic");
                last = o;
            }
        }

        [Test]
        public void StickDeadzoneKeepsSignAndFullLock()
        {
            Assert.That(InputTuning.StickDeadzone(0.05f, 0.1f), Is.EqualTo(0f));
            Assert.That(InputTuning.StickDeadzone(-1f, 0.1f), Is.EqualTo(-1f).Within(1e-6f));
            Assert.That(InputTuning.StickDeadzone(0.55f, 0.1f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(InputTuning.StickDeadzone(0.3f, 0f), Is.EqualTo(0.3f).Within(1e-6f), "0 = unchanged");
        }
    }
}
