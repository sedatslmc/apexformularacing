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

        static (int spins, int offTrack, double lap) KeyboardLap(DrivingProfile profile, bool wet,
            Apex.InputLogic.KeyboardSteering.Profile keys = Apex.InputLogic.KeyboardSteering.Profile.Balanced, VehicleSetup setup = null)
        {
            var settings = new SettingsData();
            settings.ApplyProfile(profile);
            var (d, lat) = Bed.GridSlot(0);
            var factory = Bed.setupFactory;
            if (setup != null)
            {
                Bed.setupFactory = () => setup.legacy ? VehicleSetup.Legacy() : new VehicleSetup();
                Bed.PlanFor(setup);   // each car's driver plans with that car's grip
            }
            var r = Bed.Drive(d, lat, 0f, 420f, laps: 2, pace: 0.95f, digitalInput: true, keyboardProfile: keys, configure: sim =>
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
            Bed.setupFactory = factory;
            if (setup != null) Bed.PlanFor(new VehicleSetup());
            TestContext.WriteLine($"{profile} {(wet ? "wet" : "dry")} keys {keys}{(setup != null && setup.legacy ? " (previous handling)" : "")}: {r}");
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
        [TestCase(Apex.InputLogic.KeyboardSteering.Profile.Direct)]
        [TestCase(Apex.InputLogic.KeyboardSteering.Profile.Smooth)]
        public void EveryKeyboardProfileLapsCleanly(Apex.InputLogic.KeyboardSteering.Profile keys)
        {
            // Test C on the real circuit: each keyboard steering profile (Balanced is the case above).
            var (spins, off, lap) = KeyboardLap(DrivingProfile.Balanced, false, keys);
            Assert.That(spins, Is.EqualTo(0));
            Assert.That(off, Is.EqualTo(0));
            Assert.That(lap, Is.InRange(118.0, 150.0));
        }

        [Test]
        public void NewHandlingIsFasterOnTheKeyboardThanThePrevious()
        {
            // A/B on the real circuit: the same keyboard driver in the previous (October 2026) and the current car, each
            // planning with its own car's grip. Measured 2026-11-01: previous ~148.5 s, now ~144.7 s for the first lap.
            var (spinsNew, offNew, now) = KeyboardLap(DrivingProfile.Balanced, false);
            var (_, _, before) = KeyboardLap(DrivingProfile.Balanced, false, setup: VehicleSetup.Legacy());
            TestContext.WriteLine($"Bosphorus GP, keyboard, Balanced: previous {before:F1} s, now {now:F1} s");
            Assert.That(spinsNew + offNew, Is.EqualTo(0));
            Assert.That(now, Is.LessThan(before - 1.5), "more usable grip on the keyboard");
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
