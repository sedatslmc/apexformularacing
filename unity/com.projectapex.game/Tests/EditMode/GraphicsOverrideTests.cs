using Apex.Core;
using NUnit.Framework;

namespace Apex.Tests
{
    public class GraphicsOverrideTests
    {
        [Test]
        public void MinusOneKeepsTheTier()
        {
            foreach (GraphicsTier t in System.Enum.GetValues(typeof(GraphicsTier)))
            {
                var p = GraphicsPreset.For(t);
                Assert.That(p.WithOverrides(-1, -1, -1), Is.EqualTo(p));
            }
        }

        [Test]
        public void SeparateShadowAntialiasingAndTextureChoices()
        {
            var ultra = GraphicsPreset.For(GraphicsTier.Ultra);
            var low = ultra.WithOverrides(0, -1, -1);
            Assert.That(low.shadowDistance, Is.LessThan(ultra.shadowDistance));
            Assert.That(low.shadowResolution, Is.EqualTo(1024));
            Assert.That(low.antialiasing, Is.EqualTo(ultra.antialiasing), "shadows do not touch AA");
            Assert.That(low.ssao, Is.EqualTo(ultra.ssao));

            var mobile = GraphicsPreset.For(GraphicsTier.Mobile).WithOverrides(3, 3, 1);
            Assert.That(mobile.shadowResolution, Is.EqualTo(4096));
            Assert.That(mobile.antialiasing, Is.EqualTo(PostAntialiasing.Taa));
            Assert.That(mobile.msaa, Is.EqualTo(1), "TAA needs MSAA off");
            Assert.That(mobile.textureMipLimit, Is.EqualTo(1));
            Assert.That(mobile.renderScale, Is.EqualTo(0.85f), "the render scale stays with the tier");

            var msaa = GraphicsPreset.For(GraphicsTier.Ultra).WithOverrides(-1, 4, 0);
            Assert.That(msaa.msaa, Is.EqualTo(4));
            Assert.That(msaa.antialiasing, Is.EqualTo(PostAntialiasing.None));
            Assert.That(GraphicsPreset.For(GraphicsTier.High).WithOverrides(-1, 0, -1).msaa, Is.EqualTo(1));

            for (int s = 0; s < GraphicsPreset.ShadowLevels; s++)
            {
                var p = ultra.WithOverrides(s, -1, -1);
                Assert.That(p.split1, Is.InRange(0f, 1f));
                Assert.That(p.split1, Is.LessThanOrEqualTo(p.split2));
            }
        }
    }
}
