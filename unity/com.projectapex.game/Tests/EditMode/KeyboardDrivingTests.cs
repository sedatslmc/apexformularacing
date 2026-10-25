using System;
using System.Collections.Generic;
using Apex.AI;
using Apex.Core;
using Apex.InputLogic;
using Apex.Race;
using Apex.Tracks;
using Apex.Vehicle;
using NUnit.Framework;

namespace Apex.Tests
{
    /// <summary>
    /// Drivability with digital input: an AI decides what it wants, but the car only
    /// receives keys through the game's ramps (tapping to modulate), like a player on
    /// a keyboard or touch buttons. Profiles meant for those inputs must not spin.
    /// </summary>
    public class KeyboardDrivingTests
    {
        const float Dt = 1f / 120f;

        static (int spins, float lapTime) KeyboardLap(string trackId, SettingsData settings, bool wet)
        {
            var def = TestData.Circuit(trackId);
            var g = TrackGeometry.FromDefinition(def);
            var setup = new VehicleSetup();
            var line = new RacingLine(g, 1.45f, setup.downforceArea * setup.airDensity * 0.5f / setup.mass);
            var sim = new VehicleSimulation(setup)
            {
                tractionLevel = settings.tractionLevel,
                assistAbs = settings.abs,
                steeringAssist = settings.SteeringAssistStrength,
                speedSensitiveSteering = settings.speedSensitiveSteering,
            };
            if (wet)
            {
                sim.trackWetness = 0.6f;
                sim.FitTires(TireCompound.Intermediate);
            }
            var rig = new FlatGroundRig(sim);
            g.FrameAt(g.Wrap(def.startLineDistance - 15f), out var p0, out var f0, out _, 0f);
            rig.Place(p0, f0);
            var ai = new AIDriver(line, AIPersonality.Make(0.95f, 0.2f, 1f, 0.5f, 0.5f), 7);
            var steer = new DigitalAxis();
            var thr = new DigitalAxis(InputTuning.ThrottleRate, InputTuning.ThrottleRate, InputTuning.ThrottleRate);
            var brk = new DigitalAxis(InputTuning.BrakeRate, InputTuning.BrakeRate, InputTuning.BrakeRate);
            var timer = new LapTimer(g.Length, Array.ConvertAll(def.sectorFractions, f => f * g.Length));
            var states = new List<CarState>();
            int hint = -1, spins = 0;
            bool spinning = false;
            for (int i = 0; i < 120 * 300 && timer.completedLaps.Count < 2; i++)
            {
                var pr = g.Project(rig.Position, hint);
                hint = pr.index;
                var st = new CarState { id = 0, distance = pr.distance, lateral = pr.lateral, speed = rig.u, position = rig.Position, forward = rig.Forward };
                states.Clear();
                states.Add(st);
                ai.maxSteerRad = sim.CurrentMaxLock();
                float grip = 0f;
                foreach (var t in sim.tires) grip += t.GripMultiplier(sim.trackWetness);
                ai.gripFactor = grip / 4f;
                ai.wet = wet;
                ai.frontSlipUsage = MathF.Max(MathF.Abs(sim.wheels[0].slipAngle), MathF.Abs(sim.wheels[1].slipAngle)) / sim.tireFront.slipAnglePeak;
                var c = ai.Drive(st, states, Dt);
                float k = ApexMath.Clamp01(rig.u / 60f);
                steer.pressRate = InputTuning.SteerPressRate(k);
                steer.releaseRate = InputTuning.SteerReleaseRate(k);
                steer.reverseRate = InputTuning.SteerReverseRate;
                float key = 0f;
                if (MathF.Abs(c.steer) > 0.05f && (c.steer - steer.Value) * MathF.Sign(c.steer) > 0.03f) key = MathF.Sign(c.steer);
                sim.steer = steer.Update(key, Dt);
                sim.throttle = thr.Update(c.throttle > 0.5f ? 1f : 0f, Dt);
                sim.brake = brk.Update(c.brake > 0.25f ? 1f : 0f, Dt);
                rig.Step(Dt);
                timer.Update(g.Wrap(pr.distance - def.startLineDistance), rig.time);
                float beta = MathF.Abs(MathF.Atan2(rig.v, MathF.Abs(rig.u) + 0.1f));
                if (beta > 0.45f && !spinning) { spins++; spinning = true; }
                if (beta < 0.1f) spinning = false;
            }
            float lap = timer.completedLaps.Count > 1 ? (float)timer.completedLaps[1].time : float.NaN;
            return (spins, lap);
        }

        static SettingsData Profile(DrivingProfile p)
        {
            var s = new SettingsData();
            s.ApplyProfile(p);
            return s;
        }

        [Test]
        [TestCase(DrivingProfile.Casual)]
        [TestCase(DrivingProfile.Balanced)]
        [TestCase(DrivingProfile.Expert)]
        public void KeyboardLapWithoutSpinsInTheDry(DrivingProfile profile)
        {
            var (spins, lap) = KeyboardLap("bosphorus_gp", Profile(profile), false);
            Assert.That(spins, Is.EqualTo(0));
            Assert.That(lap, Is.InRange(95f, 130f));
        }

        [Test]
        [TestCase(DrivingProfile.Casual)]
        [TestCase(DrivingProfile.Balanced)]
        [TestCase(DrivingProfile.Expert)]
        public void KeyboardLapWithoutSpinsInTheWet(DrivingProfile profile)
        {
            var (spins, lap) = KeyboardLap("bosphorus_gp", Profile(profile), true);
            Assert.That(spins, Is.EqualTo(0));
            Assert.That(lap, Is.InRange(110f, 150f));
        }
    }
}
