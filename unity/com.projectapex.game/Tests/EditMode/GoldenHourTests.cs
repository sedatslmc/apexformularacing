using System;
using System.Numerics;
using Apex.Environment;
using Apex.Weather;
using NUnit.Framework;

namespace Apex.Tests
{
    /// <summary>Golden-hour effects (GoldenHour): window glint, directional haze, haze range.</summary>
    public class GoldenHourTests
    {
        // Game frame with true north = +z (bearing 0): west is -x.
        static readonly Vector3 West = new Vector3(-1f, 0f, 0f), East = new Vector3(1f, 0f, 0f);

        [Test]
        public void TheAsianShoreWindowsBurnAtGoldenHourSeenFromEurope()
        {
            var golden = SkyModel.Get(TimeOfDay.GoldenHour);
            var sun = GoldenHour.SunDirection(golden.azimuthTrue, golden.elevation, 0f);
            Assert.That(sun.X, Is.LessThan(-0.9f), "the evening sun stands in the west");
            // A west-facing facade on the Asian shore, seen from the European shore 1.5 km to the west, slightly below.
            var toViewer = Vector3.Normalize(new Vector3(-1500f, -40f, -150f));
            float g = GoldenHour.Glint(West, toViewer, sun, golden.elevation, 0f);
            Assert.That(g, Is.GreaterThan(0.5f), "strong glint at golden hour");
            Assert.That(GoldenHour.Glint(East, toViewer, sun, golden.elevation, 0f), Is.EqualTo(0f), "east-facing walls are in shade");
            Assert.That(GoldenHour.Glint(West, -toViewer, sun, golden.elevation, 0f), Is.EqualTo(0f), "seen from behind: nothing");
            // Seen from the side (along the shore) the reflection misses the viewer.
            Assert.That(GoldenHour.Glint(West, Vector3.Normalize(new Vector3(-0.2f, 0f, 1f)), sun, golden.elevation, 0f), Is.LessThan(g * 0.2f));
            // Midday: no glint; overcast: much weaker; dusk: still burning.
            var day = SkyModel.Get(TimeOfDay.Day);
            Assert.That(GoldenHour.Glint(West, toViewer, GoldenHour.SunDirection(day.azimuthTrue, day.elevation, 0f), day.elevation, 0f), Is.EqualTo(0f));
            Assert.That(GoldenHour.Glint(West, toViewer, sun, golden.elevation, 1f), Is.LessThan(g * 0.2f));
            var dusk = SkyModel.Get(TimeOfDay.Dusk);
            Assert.That(GoldenHour.Glint(West, toViewer, GoldenHour.SunDirection(dusk.azimuthTrue, dusk.elevation, 0f), dusk.elevation, 0f), Is.GreaterThan(0.3f));
            // Colour: golden (red > green > blue), HDR above 1 at full strength.
            var c = GoldenHour.GlintColour(golden, 1f);
            Assert.That(c.r, Is.GreaterThan(c.g));
            Assert.That(c.g, Is.GreaterThan(c.b));
            Assert.That(c.r, Is.GreaterThan(1f));
        }

        [Test]
        public void HazeGlowsTowardsTheLowSunAndStaysBlueAwayFromIt()
        {
            var golden = SkyModel.Get(TimeOfDay.GoldenHour);
            var toward = GoldenHour.Haze(golden, 0.98f, 0f);
            var side = GoldenHour.Haze(golden, 0f, 0f);
            var away = GoldenHour.Haze(golden, -0.95f, 0f);
            Assert.That(toward.Luminance, Is.GreaterThan(side.Luminance), "forward scattering is brighter");
            Assert.That(toward.r - toward.b, Is.GreaterThan(side.r - side.b), "and warmer");
            Assert.That(away.b / away.r, Is.GreaterThan(side.b / side.r), "the side away from the sun is cooler");
            Assert.That(toward.r, Is.LessThanOrEqualTo(1f));
            Assert.That(GoldenHour.Phase(1f), Is.GreaterThan(20f));
            Assert.That(GoldenHour.Phase(0f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(GoldenHour.Phase(-1f), Is.LessThan(1f));
            // Midday and night: the haze stays the sky model's colour in every direction.
            var day = SkyModel.Get(TimeOfDay.Day);
            Assert.That(GoldenHour.Haze(day, 1f, 0f).r, Is.EqualTo(day.fog.r));
            var night = SkyModel.Get(TimeOfDay.Night);
            Assert.That(GoldenHour.Haze(night, 1f, 0f).g, Is.EqualTo(night.fog.g));
            // Overcast: barely directional.
            var grey = SkyModel.Get(TimeOfDay.GoldenHour, 1f);
            var gt = GoldenHour.Haze(grey, 0.98f, 1f);
            Assert.That(gt.Luminance - grey.fog.Luminance, Is.LessThan((toward.Luminance - golden.fog.Luminance) * 0.3f));
        }

        [Test]
        public void HazeClosesInAtLowSunAndInRain()
        {
            var day = GoldenHour.HazeRange(50f, 0f, 0f, true);
            var golden = GoldenHour.HazeRange(SkyModel.Get(TimeOfDay.GoldenHour).elevation, 0f, 0f, true);
            var rain = GoldenHour.HazeRange(50f, 1f, 1f, true);
            Assert.That(day.start, Is.EqualTo(600f));
            Assert.That(day.end, Is.EqualTo(8000f), "midday as before");
            Assert.That(golden.end, Is.LessThan(day.end));
            Assert.That(golden.end, Is.GreaterThan(4000f), "the Asian shore (1–2 km) stays visible");
            Assert.That(rain.end, Is.EqualTo(900f).Within(1f));
            Assert.That(GoldenHour.LowSun(50f), Is.EqualTo(0f));
            Assert.That(GoldenHour.LowSun(5f), Is.EqualTo(1f));
            Assert.That(GoldenHour.LowSun(-5f), Is.EqualTo(0f));
        }
    }
}
