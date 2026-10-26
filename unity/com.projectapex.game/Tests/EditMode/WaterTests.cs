using System;
using Apex.Environment;
using Apex.Tracks;
using NUnit.Framework;

namespace Apex.Tests
{
    public class WaterTests
    {
        [Test]
        public void FresnelOfSeaWaterRisesFromTwoPercentToFullAtGrazingAngles()
        {
            Assert.That(WaterOptics.F0, Is.InRange(0.020f, 0.022f));
            Assert.That(WaterOptics.Fresnel(1f), Is.EqualTo(WaterOptics.F0).Within(1e-6f));
            Assert.That(WaterOptics.Fresnel(0f), Is.EqualTo(1f).Within(1e-6f));
            // Looking along the strait from a car (about 3° above the surface): mostly mirror.
            Assert.That(WaterOptics.Fresnel((float)Math.Sin(3 * Math.PI / 180)), Is.GreaterThan(0.6f));
            Assert.That(WaterOptics.Fresnel(0.5f), Is.LessThan(0.1f));
        }

        [Test]
        public void WindRoughensTheSurfaceAndWavesDriftWithDeepWaterSpeed()
        {
            // Cox–Munk: σ² = 0.003 + 0.00512 U; at 5 m/s about 0.029 (rms slope 0.17).
            Assert.That(WaterOptics.SlopeVariance(5f), Is.EqualTo(0.0286f).Within(1e-4f));
            Assert.That(WaterOptics.Roughness(10f), Is.GreaterThan(WaterOptics.Roughness(2f)));
            Assert.That(WaterOptics.Roughness(2f, 1f), Is.GreaterThan(WaterOptics.Roughness(2f)), "rain rings");
            // c = √(gλ/2π): a 10 m wave runs at about 3.95 m/s.
            Assert.That(WaterOptics.PhaseSpeed(10f), Is.EqualTo(3.95f).Within(0.01f));
            var calm = WaterOptics.Layers(1f, 0f, 1f);
            var fresh = WaterOptics.Layers(1f, 0f, 9f);
            Assert.That(calm.Length, Is.EqualTo(3));
            for (int i = 0; i < 3; i++)
            {
                Assert.That(fresh[i].strength, Is.GreaterThan(calm[i].strength));
                float len = (float)Math.Sqrt(fresh[i].dirX * fresh[i].dirX + fresh[i].dirZ * fresh[i].dirZ);
                Assert.That(len, Is.EqualTo(1f).Within(1e-4f));
                Assert.That(fresh[i].dirX, Is.GreaterThan(0.8f), "layers drift roughly downwind");
                if (i > 0)
                {
                    Assert.That(fresh[i].tile, Is.LessThan(fresh[i - 1].tile));
                    Assert.That(fresh[i].speed, Is.LessThan(fresh[i - 1].speed), "shorter waves are slower");
                }
            }
            Assert.That(WaterOptics.Layers(0f, 0f, 3f)[0].dirZ, Is.EqualTo(1f).Within(1e-4f), "no wind direction: north");
        }

        [Test]
        public void TheNearestLampsAreReflected()
        {
            var d = new float[40];
            for (int i = 0; i < d.Length; i++) d[i] = (i * 37 % 40) * 10f;   // a permutation of 0..390
            var picked = new int[WaterOptics.MaxLamps];
            int n = WaterOptics.NearestLamps(d.Length, i => d[i], picked);
            Assert.That(n, Is.EqualTo(WaterOptics.MaxLamps));
            var got = new System.Collections.Generic.List<float>();
            for (int k = 0; k < n; k++) got.Add(d[picked[k]]);
            got.Sort();
            for (int k = 0; k < n; k++) Assert.That(got[k], Is.EqualTo(k * 10f), "the 16 smallest distances");
            Assert.That(WaterOptics.NearestLamps(3, i => i, picked), Is.EqualTo(3), "fewer lamps than slots");
        }

        [Test]
        public void CoastDistanceFieldIsExactForACircularIsland()
        {
            // Land: disc of 500 m radius at the origin; grid 2 km, 8 m cells.
            var f = CoastDistanceField.Build(-1000f, -1000f, 2000f, 250, (mask, res, minX, minZ, cell) =>
            {
                for (int z = 0; z < res; z++)
                    for (int x = 0; x < res; x++)
                    {
                        float px = minX + (x + 0.5f) * cell, pz = minZ + (z + 0.5f) * cell;
                        mask[z * res + x] = px * px + pz * pz <= 500f * 500f;
                    }
            });
            Assert.That(f.Sample(0f, 0f), Is.EqualTo(0f));
            Assert.That(f.Sample(800f, 0f), Is.EqualTo(300f).Within(8f));
            Assert.That(f.Sample(0f, -650f), Is.EqualTo(150f).Within(8f));
            Assert.That(f.Sample(500f, 500f), Is.EqualTo(Math.Sqrt(2) * 500 - 500).Within(8f));
            var bytes = f.Encode(64f);
            Assert.That(bytes[0], Is.EqualTo(255), "far corner beyond the encoded range");
            Assert.That(bytes[125 * 250 + 125], Is.EqualTo(0), "land");
        }

        [Test]
        public void ScanlineLandMaskMatchesThePointTestOnTheBosphorus()
        {
            var def = TestData.Circuit("bosphorus_gp");
            var land = new LandModel(def);
            Assume.That(land.HasLand, Is.True);
            const int res = 200;
            const float minX = -4000f, minZ = -4000f, cell = 40f;
            var mask = new bool[res * res];
            land.RasterizeLand(mask, res, minX, minZ, cell);
            int differ = 0, landCells = 0;
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                {
                    bool point = land.IsLand(minX + (x + 0.5f) * cell, minZ + (z + 0.5f) * cell);
                    if (point) landCells++;
                    if (point != mask[z * res + x]) differ++;
                }
            Assert.That(landCells, Is.GreaterThan(res * res / 10), "the area holds both shores");
            Assert.That(landCells, Is.LessThan(res * res * 9 / 10), "and water");
            // Cells exactly on an outline may differ by floating-point rounding.
            Assert.That(differ, Is.LessThan(res * res / 2000));
        }
    }
}
