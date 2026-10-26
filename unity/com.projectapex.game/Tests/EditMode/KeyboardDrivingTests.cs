using System;
using System.Collections.Generic;
using Apex.Core;
using Apex.Tracks;
using Apex.Vehicle;
using NUnit.Framework;

namespace Apex.Tests
{
    /// <summary>
    /// Drivability with digital input: an AI decides what it wants, but the car only
    /// receives keys through the game's ramps (tapping to modulate), like a player on
    /// a keyboard or touch buttons. Profiles meant for those inputs must not spin.
    /// Runs on the 6-DOF rig over the circuit's real surface (CircuitTestBed), which uses
    /// the in-game force code incl. the anti-roll bars; the earlier flat rig split the
    /// lateral load transfer with a fixed share and could not represent the roll setup.
    /// </summary>
    public class KeyboardDrivingTests
    {
        static CircuitTestBed bed;
        static CircuitTestBed Bed => bed ??= new CircuitTestBed(TestData.Circuit("bosphorus_a"));

        static (int spins, int offTrack, double lap) KeyboardLap(DrivingProfile profile, bool wet)
        {
            var settings = new SettingsData();
            settings.ApplyProfile(profile);
            var (d, lat) = Bed.GridSlot(0);
            var r = Bed.Drive(d, lat, 0f, 420f, laps: 2, pace: 0.95f, digitalInput: true, configure: sim =>
            {
                sim.tractionLevel = settings.tractionLevel;
                sim.assistAbs = settings.abs;
                sim.steeringAssist = settings.SteeringAssistStrength;
                sim.speedSensitiveSteering = settings.speedSensitiveSteering;
                if (wet)
                {
                    sim.trackWetness = 0.6f;
                    sim.FitTires(TireCompound.Intermediate);
                }
            });
            TestContext.WriteLine($"{profile} {(wet ? "wet" : "dry")}: {r}");
            return (r.Count(CircuitTestBed.EventKind.Spin), r.Count(CircuitTestBed.EventKind.LeftTrack), r.lapTimes.Count > 1 ? r.lapTimes[1] : double.NaN);
        }

        [Test]
        [TestCase(DrivingProfile.Casual)]
        [TestCase(DrivingProfile.Balanced)]
        [TestCase(DrivingProfile.Expert)]
        public void BosphorusGrandPrixKeyboardLapWithoutSpins(DrivingProfile profile)
        {
            var (spins, off, lap) = KeyboardLap(profile, false);
            Assert.That(spins, Is.EqualTo(0));
            Assert.That(off, Is.EqualTo(0));
            Assert.That(lap, Is.InRange(118.0, 150.0));
        }

        [Test]
        [TestCase(DrivingProfile.Casual)]
        [TestCase(DrivingProfile.Balanced)]
        [TestCase(DrivingProfile.Expert)]
        public void BosphorusGrandPrixKeyboardLapWithoutSpinsInTheWet(DrivingProfile profile)
        {
            var (spins, off, lap) = KeyboardLap(profile, true);
            Assert.That(spins, Is.EqualTo(0));
            Assert.That(off, Is.EqualTo(0));
            Assert.That(lap, Is.InRange(135.0, 170.0));
        }
    }
}
