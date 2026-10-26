using System;
using Apex.Core;
using Apex.Environment;
using Apex.Weather;
using NUnit.Framework;

namespace Apex.Tests
{
    public class LightingTests
    {
        [Test]
        public void SunPositionMatchesTheAlmanacForIstanbul()
        {
            // 20 September 2026: solar noon about 12:57 local, sunset about 19:06–19:10 (published tables).
            SolarPosition.Istanbul(2026, 9, 20, 12.95, out float noonEl, out float noonAz);
            Assert.That(noonEl, Is.EqualTo(90f - 41.045f + 1.0f).Within(0.5f), "elevation = 90° − latitude + declination");
            Assert.That(noonAz, Is.EqualTo(180f).Within(2f));
            double sunset = SolarPosition.IstanbulSunset(2026, 9, 20);
            Assert.That(sunset, Is.InRange(19.0, 19.25));
            SolarPosition.Istanbul(2026, 9, 20, sunset, out _, out float setAz);
            Assert.That(setAz, Is.InRange(268f, 275f), "close to due west at the equinox");
            // Summer solstice noon: about 72.4°.
            SolarPosition.Istanbul(2026, 6, 21, 13.0, out float juneEl, out _);
            Assert.That(juneEl, Is.EqualTo(72.4f).Within(0.6f));
            // Morning sun rises in the east.
            SolarPosition.Istanbul(2026, 9, 20, 8.75, out float mEl, out float mAz);
            Assert.That(mAz, Is.InRange(95f, 120f));
            Assert.That(mEl, Is.InRange(15f, 25f));
        }

        [Test]
        public void SunlightWarmsAndDimsTowardsTheHorizonWithoutAnOrangeFilter()
        {
            var noon = SkyModel.Transmittance(50f, SkyModel.AerosolDepth);
            var golden = SkyModel.Transmittance(9f, SkyModel.AerosolDepth);
            var dusk = SkyModel.Transmittance(1.5f, SkyModel.AerosolDepth);
            Assert.That(noon.b / noon.r, Is.GreaterThan(0.75f), "midday sun is close to white");
            Assert.That(golden.b / golden.r, Is.LessThan(noon.b / noon.r));
            Assert.That(dusk.b / dusk.r, Is.LessThan(golden.b / golden.r));
            Assert.That(golden.Luminance, Is.LessThan(noon.Luminance * 0.5f));

            var g = SkyModel.Get(TimeOfDay.GoldenHour);
            Assert.That(g.elevation, Is.InRange(6f, 12f));
            Assert.That(g.azimuthTrue, Is.InRange(250f, 270f));
            // Golden, not orange: green stays high, blue well above zero (sRGB).
            Assert.That(g.sunColor.r, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(g.sunColor.g, Is.InRange(0.75f, 0.92f));
            Assert.That(g.sunColor.b, Is.InRange(0.45f, 0.75f));
            var d = SkyModel.Get(TimeOfDay.Day);
            Assert.That(d.sunColor.b, Is.GreaterThan(0.85f));
            Assert.That(d.sunIntensity, Is.EqualTo(SkyModel.ReferenceSunIntensity).Within(0.05f), "reference midday near 50°");
        }

        [Test]
        public void LowSunScenesAreSkyLitAndTheCameraAdapts()
        {
            var day = SkyModel.Get(TimeOfDay.Day);
            var golden = SkyModel.Get(TimeOfDay.GoldenHour);
            var dusk = SkyModel.Get(TimeOfDay.Dusk);
            // Physical: the diffuse share of the ground illuminance rises as the sun sinks.
            float Share(SkyModel.Lighting l) => l.skyKlux / (l.skyKlux + l.directKlux);
            Assert.That(Share(day), Is.InRange(0.12f, 0.3f));
            Assert.That(Share(golden), Is.GreaterThan(0.55f));
            Assert.That(Share(dusk), Is.GreaterThan(0.9f));
            // Exposure compensation grows, but the low-sun scene still ends up darker than midday.
            Assert.That(golden.exposureEv, Is.GreaterThan(1f));
            Assert.That(dusk.exposureEv, Is.GreaterThan(golden.exposureEv));
            Assert.That(golden.ambientSky.Luminance, Is.GreaterThan(day.ambientSky.Luminance * 0.8f), "bright, sky-lit shadows at golden hour");
            Assert.That(dusk.sunIntensity, Is.LessThan(golden.sunIntensity * 0.5f));
            // Golden hour horizon is warmer than the sky overhead; midday horizon is near neutral.
            Assert.That(golden.ambientEquator.r / golden.ambientEquator.b, Is.GreaterThan(golden.ambientSky.r / golden.ambientSky.b + 0.3f));
            Assert.That(golden.fog.r, Is.GreaterThan(golden.fog.b));
            Assert.That(day.fog.b, Is.GreaterThan(day.fog.r));
        }

        [Test]
        public void NightAndCloudDarkenTheAmbientTerm()
        {
            var day = SkyModel.Get(TimeOfDay.Day);
            var night = SkyModel.Get(TimeOfDay.Night);
            var overcast = SkyModel.Get(TimeOfDay.Day, 1f);
            Assert.That(night.ambientSky.Luminance, Is.LessThan(day.ambientSky.Luminance * 0.35f));
            Assert.That(night.ambientEquator.r, Is.GreaterThan(night.ambientEquator.b), "warm city glow on the horizon");
            Assert.That(night.artificial, Is.EqualTo(1f));
            Assert.That(day.artificial, Is.EqualTo(0f));
            Assert.That(overcast.sunIntensity, Is.LessThan(day.sunIntensity * 0.45f));
            Assert.That(overcast.shadowStrength, Is.LessThan(day.shadowStrength));
            Assert.That(Math.Abs(overcast.ambientSky.r - overcast.ambientSky.b), Is.LessThan(Math.Abs(day.ambientSky.r - day.ambientSky.b)), "grey overcast sky");
            foreach (TimeOfDay t in Enum.GetValues(typeof(TimeOfDay)))
                foreach (float c in new[] { 0f, 0.5f, 1f })
                {
                    var l = SkyModel.Get(t, c);
                    Assert.That(l.sunIntensity, Is.InRange(0f, 2.5f), t + " " + c);
                    foreach (var col in new[] { l.ambientSky, l.ambientEquator, l.ambientGround, l.fog, l.sunColor })
                    {
                        Assert.That(col.r, Is.InRange(0f, 1.2f), t + " " + c + " " + col);
                        Assert.That(col.g, Is.InRange(0f, 1.2f), t + " " + c + " " + col);
                        Assert.That(col.b, Is.InRange(0f, 1.2f), t + " " + c + " " + col);
                    }
                }
        }

        [Test]
        public void GraphicsTiersScaleMonotonicallyAndKeepUrpRules()
        {
            GraphicsPreset prev = default;
            for (int i = 0; i < GraphicsPreset.TierCount; i++)
            {
                var p = GraphicsPreset.For((GraphicsTier)i);
                Assert.That(p.tier, Is.EqualTo((GraphicsTier)i));
                if (p.antialiasing == PostAntialiasing.Taa) Assert.That(p.msaa, Is.EqualTo(1), "URP turns TAA off with MSAA");
                Assert.That(new[] { 1, 2, 4, 8 }, Does.Contain(p.msaa));
                Assert.That(new[] { 256, 512, 1024, 2048, 4096 }, Does.Contain(p.shadowResolution));
                Assert.That(p.shadowCascades, Is.InRange(1, 4));
                Assert.That(p.split1, Is.GreaterThan(0f).And.LessThan(p.split2));
                if (p.shadowCascades >= 3) Assert.That(p.split2, Is.LessThan(1f));
                if (p.shadowCascades == 4) Assert.That(p.split3, Is.GreaterThan(p.split2).And.LessThan(1f));
                if (i > 0)
                {
                    Assert.That(p.shadowDistance, Is.GreaterThan(prev.shadowDistance));
                    Assert.That(p.shadowCascades, Is.GreaterThanOrEqualTo(prev.shadowCascades));
                    Assert.That(p.lodBias, Is.GreaterThan(prev.lodBias));
                    Assert.That(p.reflectionResolution, Is.GreaterThan(prev.reflectionResolution));
                    Assert.That(p.environmentDetail, Is.GreaterThanOrEqualTo(prev.environmentDetail));
                }
                prev = p;
            }
            var mobile = GraphicsPreset.For(GraphicsTier.Mobile);
            Assert.That(mobile.ssao, Is.False);
            Assert.That(mobile.ultraTextures, Is.False, "mobile never loads 4K sets");
            Assert.That(mobile.reflectionCapturesWorld, Is.False);
            Assert.That(GraphicsPreset.For(GraphicsTier.Ultra).ultraTextures, Is.True);
            Assert.That(GraphicsPreset.Auto(true, 12000), Is.EqualTo(GraphicsTier.Mobile));
            Assert.That(GraphicsPreset.Auto(false, 8192), Is.EqualTo(GraphicsTier.High));
            Assert.That(GraphicsPreset.Auto(false, 2048), Is.EqualTo(GraphicsTier.Medium));
        }

        [Test]
        public void CascadeSplitsKeepTheFirstCascadeOnTheCar()
        {
            var s = GraphicsPreset.CascadeSplits(4, 1f, 320f, 0.75f);
            Assert.That(s.Length, Is.EqualTo(3));
            // Chase camera to car is ~6 m: the first cascade must cover it with margin but stay tight.
            Assert.That(s[0] * 320f, Is.InRange(12f, 35f));
            Assert.That(s[1], Is.GreaterThan(s[0]));
            Assert.That(s[2], Is.GreaterThan(s[1]).And.LessThan(1f));
            var uniform = GraphicsPreset.CascadeSplits(4, 1f, 320f, 0f);
            Assert.That(uniform[0], Is.EqualTo(0.25f).Within(0.01f));
            Assert.That(GraphicsPreset.CascadeSplits(1, 1f, 100f, 0.5f)[0], Is.EqualTo(1f));
        }

        [Test]
        public void SettingsKeepTheGraphicsTierInRange()
        {
            var s = new Apex.Core.SettingsData { graphicsTier = 9 };
            s.MigrateAndValidate();
            Assert.That(s.graphicsTier, Is.EqualTo(GraphicsPreset.TierCount - 1));
            s.graphicsTier = -5;
            s.MigrateAndValidate();
            Assert.That(s.graphicsTier, Is.EqualTo(-1));
        }
    }
}
