using Apex.Environment;
using NUnit.Framework;

namespace Apex.Tests
{
    public class EnvironmentTests
    {
        [Test]
        public void TurkishFlagGeometry()
        {
            // Hoist and fly are red, the crescent ring and the star are white.
            Assert.That(TurkishFlagDesign.IsWhite(0.05f, 0f), Is.False);
            Assert.That(TurkishFlagDesign.IsWhite(1.45f, 0.4f), Is.False);
            Assert.That(TurkishFlagDesign.IsWhite(0.27f, 0f), Is.True, "left limb of the crescent");
            Assert.That(TurkishFlagDesign.IsWhite(0.6f, 0f), Is.False, "inside the inner circle");
            Assert.That(TurkishFlagDesign.IsWhite(TurkishFlagDesign.StarCx, 0f), Is.True, "star centre");
            // Crescent tips at x = 0.71125 G; the star circle touches that line.
            Assert.That(TurkishFlagDesign.TipX, Is.EqualTo(0.71125f).Within(1e-4f));
            Assert.That(TurkishFlagDesign.StarCx, Is.EqualTo(0.83625f).Within(1e-4f));
            // One star point towards the hoist: white just inside it, red just above it.
            Assert.That(TurkishFlagDesign.IsWhite(TurkishFlagDesign.TipX + 0.01f, 0f), Is.True);
            Assert.That(TurkishFlagDesign.IsWhite(TurkishFlagDesign.TipX + 0.01f, 0.03f), Is.False);
            var px = TurkishFlagDesign.Rasterize(64);
            Assert.That(px.Length, Is.EqualTo(96 * 64 * 4));
            // Red share: crescent ~0.071 G² + star ~0.018 G² white of 1.5 G² => ~94 % red.
            int red = 0;
            for (int i = 0; i < px.Length; i += 4) if (px[i + 1] < 128) red++;
            Assert.That(red / (float)(96 * 64), Is.InRange(0.93f, 0.95f));
        }
    }
}
