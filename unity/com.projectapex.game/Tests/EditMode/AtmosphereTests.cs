using System;
using Apex.Environment;
using NUnit.Framework;

namespace Apex.Tests
{
    /// <summary>Phase 5 atmosphere (docs/ISTANBUL_OVERHAUL.md): flag wind and occasional ambience sounds.</summary>
    public class AtmosphereTests
    {
        [Test]
        public void FlagsHangFromTheHoistAndSwingMoreInStrongerWind()
        {
            for (double t = 0; t < 5; t += 0.05)
            {
                Assert.That(FlagWave.Out(0f, t, 8f, 1f), Is.EqualTo(0f).Within(1e-6f), "the hoist stays on the facade");
                Assert.That(FlagWave.Side(0f, t, 8f, 1f), Is.EqualTo(0f).Within(1e-6f));
                for (float u = 0f; u <= 1f; u += 0.1f)
                {
                    Assert.That(MathF.Abs(FlagWave.Out(u, t, 30f, 2f)), Is.LessThanOrEqualTo(FlagWave.Amplitude(30f) + 1e-5f));
                    Assert.That(FlagWave.Out(u, t, 4f, 2f), Is.GreaterThanOrEqualTo(0f), "the cloth swings out of the facade, never into it");
                }
            }
            Assert.That(FlagWave.Amplitude(10f), Is.GreaterThan(FlagWave.Amplitude(2f)));
            Assert.That(FlagWave.Frequency(10f), Is.GreaterThan(FlagWave.Frequency(2f)));
            Assert.That(FlagWave.Amplitude(100f), Is.LessThanOrEqualTo(0.35f), "bounded in a gale");
            Assert.That(FlagWave.Amplitude(0f), Is.GreaterThan(0f), "a breeze even on a calm day");
            // Neighbouring flags do not swing in step.
            int differ = 0;
            for (int i = 0; i < 20; i++)
                if (MathF.Abs(FlagWave.Phase(i * 3.7f, 10f) - FlagWave.Phase(i * 3.7f + 3f, 10f)) > 0.2f) differ++;
            Assert.That(differ, Is.GreaterThan(12));
        }

        [Test]
        public void ShipHornsAndGullsAreOccasionalAndOnlyByTheWater()
        {
            foreach (double r in new[] { 0.0, 0.3, 0.999 })
            {
                Assert.That(AmbienceEvents.NextInterval(AmbienceEvents.Kind.ShipHorn, r), Is.InRange(35f, 90f));
                Assert.That(AmbienceEvents.NextInterval(AmbienceEvents.Kind.Gull, r), Is.InRange(6f, 18f));
            }
            foreach (AmbienceEvents.Kind k in Enum.GetValues(typeof(AmbienceEvents.Kind)))
            {
                Assert.That(AmbienceEvents.Volume(k, 0f), Is.EqualTo(0f), "silent inland");
                Assert.That(AmbienceEvents.Volume(k, 0.1f), Is.EqualTo(0f));
                Assert.That(AmbienceEvents.Volume(k, 1f), Is.GreaterThan(AmbienceEvents.Volume(k, 0.4f)));
                Assert.That(AmbienceEvents.Volume(k, 1f), Is.LessThanOrEqualTo(1f));
            }
        }
    }
}
