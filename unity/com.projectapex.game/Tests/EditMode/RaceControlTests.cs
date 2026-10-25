using System;
using System.Collections.Generic;
using Apex.AI;
using Apex.Race;
using Apex.Tracks;
using Apex.Vehicle;
using NUnit.Framework;

namespace Apex.Tests
{
    public class RaceControlTests
    {
        const float L = 5000f;
        const double Dt = 0.1;

        static CarReport Car(int id, double progress, float speed, bool onTrack = true) =>
            new CarReport { id = id, progress = progress, distance = (float)(progress % L), speed = speed, onRacingSurface = onTrack };

        [Test]
        public void StoppedCarOnTrackBringsOutTheSafetyCarAndRestarts()
        {
            var rc = new RaceControl(new RaceControlRules(), L);
            var log = new List<TrackStatus>();
            var recovered = new List<int>();
            rc.StatusChanged += (s, m) => log.Add(s);
            rc.CarRecovered += id => recovered.Add(id);
            double t = 0;
            int leaderLaps = 3;
            bool stranded = true;
            for (int i = 0; i < 1200; i++, t += Dt)
            {
                var cars = new List<CarReport>
                {
                    Car(0, 20000 + t * 30, 30f),
                    Car(1, 1200, stranded ? 0f : 0f, true),
                };
                if (!stranded) cars[1] = new CarReport { id = 1, ignore = true };
                rc.Update(t, cars, leaderLaps);
                if (recovered.Count > 0) stranded = false;
                if (t > 60 && leaderLaps == 3) leaderLaps = 4;
                if (rc.Status == TrackStatus.SafetyCarIn) { rc.LeaderCrossedLine(t); break; }
            }
            Assert.That(log, Is.EqualTo(new[] { TrackStatus.Yellow, TrackStatus.SafetyCar, TrackStatus.SafetyCarIn, TrackStatus.Green }));
            Assert.That(recovered, Is.EqualTo(new[] { 1 }));
            Assert.That(rc.SafetyCarDeployments, Is.EqualTo(1));
        }

        [Test]
        public void CarStrandedOffTrackGivesVirtualSafetyCar()
        {
            var rc = new RaceControl(new RaceControlRules(), L);
            var log = new List<TrackStatus>();
            rc.StatusChanged += (s, m) => log.Add(s);
            double t = 0;
            bool stranded = true;
            rc.CarRecovered += id => stranded = false;
            for (int i = 0; i < 600; i++, t += Dt)
            {
                var cars = new List<CarReport> { Car(0, 10000 + t * 30, 30f), stranded ? Car(1, 2500, 0f, false) : new CarReport { id = 1, ignore = true } };
                rc.Update(t, cars, 2);
            }
            Assert.That(log, Is.EqualTo(new[] { TrackStatus.Yellow, TrackStatus.VirtualSafetyCar, TrackStatus.VirtualSafetyCarEnding, TrackStatus.Green }));
        }

        [Test]
        public void CarThatDrivesOnClearsTheYellow()
        {
            var rc = new RaceControl(new RaceControlRules(), L);
            double t = 0;
            for (int i = 0; i < 50; i++, t += Dt) rc.Update(t, new[] { Car(1, 900, 1f) }, 0);
            Assert.That(rc.Status, Is.EqualTo(TrackStatus.Yellow));
            Assert.That(rc.IsYellowAt(800f), Is.True);
            Assert.That(rc.IsYellowAt(2000f), Is.False);
            for (int i = 0; i < 5; i++, t += Dt) rc.Update(t, new[] { Car(1, 900 + i * 2, 25f) }, 0);
            Assert.That(rc.Status, Is.EqualTo(TrackStatus.Green));
        }

        [Test]
        public void NoSafetyCarWhenDisabled()
        {
            var rc = new RaceControl(new RaceControlRules { safetyCar = false, virtualSafetyCar = false }, L);
            double t = 0;
            for (int i = 0; i < 150; i++, t += Dt) rc.Update(t, new[] { Car(1, 900, 0f) }, 0);
            Assert.That(rc.Status, Is.EqualTo(TrackStatus.Yellow));
        }

        static (int penalties, int giveBacks) PassUnderYellow(bool giveBack)
        {
            var rc = new RaceControl(new RaceControlRules(), L);
            int penalties = 0, requests = 0;
            rc.PenaltyIssued += (id, s, r) => { if (id == 2) penalties++; };
            rc.GiveBackRequested += (a, b) => { if (a == 2 && b == 3) requests++; };
            double t = 0;
            // Car 1 stranded at 1000 m: yellow zone 750..1060 m.
            for (int i = 0; i < 40; i++, t += Dt) rc.Update(t, new[] { Car(1, 1000, 0f), Car(2, 800, 40f), Car(3, 820, 38f) }, 0);
            // Car 2 passes car 3 inside the zone.
            rc.Update(t, new[] { Car(1, 1000, 0f), Car(2, 830, 40f), Car(3, 825, 38f) }, 0);
            for (int i = 0; i < 150; i++)
            {
                t += Dt;
                double p2 = giveBack && i > 20 ? 1100 + i : 1105 + i;
                rc.Update(t, new[] { Car(1, 1000, 0f), Car(2, p2, 40f), Car(3, 1104 + i, 40f) }, 0);
            }
            return (penalties, requests);
        }

        [Test]
        public void PassingUnderYellowMustBeGivenBack()
        {
            Assert.That(PassUnderYellow(false), Is.EqualTo((1, 1)));
            Assert.That(PassUnderYellow(true), Is.EqualTo((0, 1)));
        }

        [Test]
        public void PassingTheStrandedCarIsLegal()
        {
            var rc = new RaceControl(new RaceControlRules(), L);
            int requests = 0;
            rc.GiveBackRequested += (a, b) => requests++;
            double t = 0;
            for (int i = 0; i < 40; i++, t += Dt) rc.Update(t, new[] { Car(1, 1000, 0f), Car(2, 900, 30f) }, 0);
            for (int i = 0; i < 20; i++, t += Dt) rc.Update(t, new[] { Car(1, 1000, 0f), Car(2, 990 + i * 3, 30f) }, 0);
            Assert.That(requests, Is.EqualTo(0));
        }

        [Test]
        public void AiQueuesBehindSlowCarWithoutPassing()
        {
            // Safety-car queue on the test loop: car 0 is capped at 30 m/s, car 1 may
            // not pass and must settle behind it at roughly the follow gap.
            var def = TestData.Circuit("test_loop");
            var g = TrackGeometry.FromDefinition(def);
            var setup = new VehicleSetup();
            var line = new RacingLine(g, 1.45f, setup.downforceArea * setup.airDensity * 0.5f / setup.mass);
            var rigs = new FlatGroundRig[2];
            var ais = new AIDriver[2];
            var hints = new[] { -1, -1 };
            for (int i = 0; i < 2; i++)
            {
                rigs[i] = new FlatGroundRig(new VehicleSimulation(new VehicleSetup()) { assistSteering = true });
                g.FrameAt(g.Wrap(def.startLineDistance + (i == 0 ? 60f : 0f)), out var p, out var f, out _, 0f);
                rigs[i].Place(p, f);
                ais[i] = new AIDriver(line, AIPersonality.Make(0.95f, 1f, 1f, 0.5f, 0.5f), 3 + i);
            }
            ais[0].controlSpeedCap = 30f;
            ais[1].noOvertaking = true;
            ais[1].followGap = 22f;
            var states = new List<CarState>();
            float minGap = float.MaxValue, lastGap = 0f;
            const float dt = 1f / 120f;
            for (int step = 0; step < 120 * 60; step++)
            {
                states.Clear();
                var proj = new TrackGeometry.Projection[2];
                for (int i = 0; i < 2; i++)
                {
                    proj[i] = g.Project(rigs[i].Position, hints[i]);
                    hints[i] = proj[i].index;
                    states.Add(new CarState { id = i, distance = proj[i].distance, lateral = proj[i].lateral, speed = rigs[i].u, position = rigs[i].Position, forward = rigs[i].Forward });
                }
                for (int i = 0; i < 2; i++)
                {
                    ais[i].maxSteerRad = rigs[i].sim.CurrentMaxLock();
                    var c = ais[i].Drive(states[i], states, dt);
                    rigs[i].sim.throttle = c.throttle;
                    rigs[i].sim.brake = c.brake;
                    rigs[i].sim.steer = c.steer;
                    rigs[i].Step(dt);
                }
                float gap = g.Wrap(proj[0].distance - proj[1].distance);
                if (step > 120 * 20) minGap = Math.Min(minGap, gap);
                lastGap = gap;
            }
            Assert.That(minGap, Is.GreaterThan(8f), "never alongside / past");
            Assert.That(lastGap, Is.InRange(12f, 45f));
        }
    }
}
