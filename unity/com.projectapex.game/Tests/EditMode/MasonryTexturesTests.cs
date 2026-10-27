using System;
using Apex.Environment;
using NUnit.Framework;

namespace Apex.Tests
{
    public class MasonryTexturesTests
    {
        static float Mean(byte[] b, int channel)
        {
            double s = 0;
            for (int i = channel; i < b.Length; i += 4) s += b[i];
            return (float)(s / (b.Length / 4) / 255.0);
        }

        /// <summary>Largest jump across the wrap seam compared with the largest jump inside the texture (same axis).</summary>
        static float SeamRatio(byte[] b, int n, bool horizontal)
        {
            float seam = 0f, inner = 0f;
            for (int k = 0; k < n; k++)
            {
                int a = horizontal ? (k * n + n - 1) : ((n - 1) * n + k);
                int c = horizontal ? (k * n) : k;
                seam = MathF.Max(seam, MathF.Abs(b[a * 4 + 1] - b[c * 4 + 1]));
                int a2 = horizontal ? (k * n + n / 2 - 1) : ((n / 2 - 1) * n + k);
                int c2 = horizontal ? (k * n + n / 2) : ((n / 2) * n + k);
                inner = MathF.Max(inner, MathF.Abs(b[a2 * 4 + 1] - b[c2 * 4 + 1]));
            }
            return seam / MathF.Max(1f, inner);
        }

        [Test]
        public void MarbleAshlarTilesAndShowsItsJoints()
        {
            const int n = 256;
            var (albedo, normal) = MasonryTextures.Marble(n);
            Assert.That(albedo.Length, Is.EqualTo(n * n * 4));
            float mean = Mean(albedo, 1);
            TestContext.WriteLine($"marble mean {mean:F3}");
            Assert.That(mean, Is.InRange(0.75f, 0.95f), "light stone, multiplied with the palette colour");
            // Mortar joints: a few per cent of the pixels are clearly darker than the stone.
            int dark = 0;
            for (int i = 1; i < albedo.Length; i += 4) if (albedo[i] < mean * 255f * 0.82f) dark++;
            float share = dark / (float)(n * n);
            Assert.That(share, Is.InRange(0.01f, 0.12f), "joints");
            Assert.That(SeamRatio(albedo, n, true), Is.LessThan(1.5f), "seamless in u");
            Assert.That(SeamRatio(albedo, n, false), Is.LessThan(1.5f), "seamless in v");
            // Normals point out of the wall on average (z channel > 0.5).
            Assert.That(Mean(normal, 2), Is.GreaterThan(0.9f));
        }

        [Test]
        public void LimePlasterIsJointlessAndTiles()
        {
            const int n = 128;
            var (albedo, normal) = MasonryTextures.Plaster(n);
            float mean = Mean(albedo, 1);
            Assert.That(mean, Is.InRange(0.8f, 0.98f));
            Assert.That(SeamRatio(albedo, n, true), Is.LessThan(1.5f));
            Assert.That(SeamRatio(albedo, n, false), Is.LessThan(1.5f));
            Assert.That(Mean(normal, 2), Is.GreaterThan(0.95f));
        }
    }
}
