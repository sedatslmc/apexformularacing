using System;
using System.Linq;
using Apex.Tracks;
using NUnit.Framework;

namespace Apex.Tests
{
    /// <summary>
    /// Drivability of the Bosphorus GP on its real collider surface: the vehicle model driven by the
    /// AI on the offline 6-DOF rig (<see cref="Apex.Vehicle.SurfaceRig"/>), with the car's body and
    /// wheel colliders checked against the road every step. Not PhysX: these tests catch geometry
    /// problems (steps, wedges, abrupt profile changes, foreign surfaces), the Unity run confirms them.
    /// </summary>
    public class RoadSurfaceDriveTests
    {
        static CircuitTestBed bed;
        static CircuitTestBed Bed => bed ??= new CircuitTestBed(TestData.Circuit("bosphorus_a"));

        /// <summary>A body touch that matters: deeper than 3 mm (PhysX contact offset is 10 mm).</summary>
        static int HardGroundings(CircuitTestBed.DriveResult r) =>
            r.events.Count(e => e.kind == CircuitTestBed.EventKind.Grounding && e.value > 0.003f);

        static void AssertClean(CircuitTestBed.DriveResult r, string what)
        {
            string ev = string.Join("\n", r.events.Where(e => e.kind != CircuitTestBed.EventKind.Grounding || e.value > 0.003f).Take(12));
            Assert.That(r.Count(CircuitTestBed.EventKind.Stuck), Is.EqualTo(0), $"{what}: stuck\n{r}\n{ev}");
            Assert.That(r.Count(CircuitTestBed.EventKind.LeftTrack), Is.EqualTo(0), $"{what}: left the circuit\n{r}\n{ev}");
            Assert.That(r.Count(CircuitTestBed.EventKind.Airborne), Is.EqualTo(0), $"{what}: airborne\n{r}\n{ev}");
            Assert.That(r.Count(CircuitTestBed.EventKind.VerticalSpike), Is.EqualTo(0), $"{what}: vertical impulse > 2.5 g\n{r}\n{ev}");
            Assert.That(r.Count(CircuitTestBed.EventKind.OffRoadSurface), Is.EqualTo(0), $"{what}: wheel on terrain/wall\n{r}\n{ev}");
            Assert.That(HardGroundings(r), Is.EqualTo(0), $"{what}: body grounding\n{r}\n{ev}");
        }

        [Test]
        public void A_EveryGridSlotStartsCleanly()
        {
            for (int slot = 0; slot < 20; slot++)
            {
                var (d, lat) = Bed.GridSlot(slot);
                var r = Bed.Drive(d, lat, 400f, 30f);
                AssertClean(r, $"grid slot {slot}");
                Assert.That(r.distanceDriven, Is.GreaterThanOrEqualTo(400f), $"grid slot {slot}: {r}");
                Assert.That(r.maxSpeed * 3.6f, Is.GreaterThan(150f), $"grid slot {slot} accelerates normally: {r}");
            }
        }

        [Test]
        public void B_FullLapsAtRacingSpeed()
        {
            // Fastest AI the game fields: personality pace 0.955 × expert difficulty 1.02.
            float pace = 0.955f * Apex.Core.Roster.DifficultyScale(1f);
            var (d, lat) = Bed.GridSlot(0);
            var r = Bed.Drive(d, lat, 0f, 400f, laps: 2, pace: pace);
            AssertClean(r, "two laps");
            Assert.That(r.lapTimes.Count, Is.EqualTo(2), r.ToString());
            TestContext.WriteLine($"laps {string.Join(", ", r.lapTimes.Select(t => t.ToString("F2")))} s, {r}");
        }

        [Test]
        public void C_LongStraightAllowsFullThrottleAcceleration()
        {
            // Dolmabahçe/Çırağan straight from the start line towards Ortaköy (lap 0 to ~1900 m), held on its line.
            var g = Bed.g;
            float start = Bed.def.startLineDistance;
            var fromStand = Bed.Drive(start, -2f, 1700f, 60f, throttleOnly: true);
            AssertClean(fromStand, "0 → full speed");
            Assert.That(fromStand.maxSpeed * 3.6f, Is.GreaterThan(315f), $"same as on a flat road (330 km/h after ~1.6 km): {fromStand}");
            var rolling = Bed.Drive(start, -2f, 1700f, 60f, initialSpeed: 100f / 3.6f, throttleOnly: true);
            AssertClean(rolling, "100 km/h → full speed");
            Assert.That(rolling.maxSpeed * 3.6f, Is.GreaterThan(320f), rolling.ToString());
        }

        [Test]
        public void D_NoUnexpectedImpulsesOnAnotherDriverLine()
        {
            // A slower, different driver (other line through traffic-free corners, other braking points).
            var (d, lat) = Bed.GridSlot(7);
            var r = Bed.Drive(d, lat, 0f, 420f, laps: 1, pace: 0.88f, seed: 11);
            AssertClean(r, "lap at 88 % pace");
            Assert.That(r.lapTimes.Count, Is.EqualTo(1), r.ToString());
            Assert.That(r.maxCompression, Is.LessThan(new Apex.Vehicle.VehicleSetup().suspensionTravel), "no bump-stop hits: " + r);
        }

        [Test]
        public void E_AiCompletesLapsFromTheBackOfTheGrid()
        {
            var (d, lat) = Bed.GridSlot(19);
            var r = Bed.Drive(d, lat, 0f, 420f, laps: 1, pace: 0.95f, seed: 5);
            AssertClean(r, "AI from slot 20");
            Assert.That(r.lapTimes.Count, Is.EqualTo(1), r.ToString());
        }

        [Test]
        public void F_PitLaneIsDrivableEntryToExit()
        {
            var pit = Bed.def.pit;
            var g = Bed.g;
            float lane(float raw) => pit.side * (g.WidthAt(raw) * 0.5f + pit.laneCentreFromEdge);
            // As RaceController drives a pit stop: line up on the pit side 160 m before the entry, follow the
            // lane after the entry, merge back where the pit wall ends (PitMergeLength before the exit).
            float target(float raw)
            {
                float into = Apex.Core.ApexMath.PosMod(raw - pit.entry, g.Length);
                float toEntry = Apex.Core.ApexMath.PosMod(pit.entry - raw, g.Length);
                float span = Apex.Core.ApexMath.PosMod(pit.exit - pit.entry, g.Length);
                if (toEntry < 160f) return pit.side * (g.WidthAt(raw) * 0.5f - 1.6f);
                if (into < span - 60f) return lane(raw);
                return float.NaN;
            }
            var r = Bed.Drive(g.Wrap(pit.entry - 150f), 0f, 150f + Apex.Core.ApexMath.PosMod(pit.exit - pit.entry, g.Length) + 150f, 90f,
                initialSpeed: 22f, lateralAt: target, speedLimit: pit.SpeedLimit);
            AssertClean(r, "pit lane");
            Assert.That(r.distanceDriven, Is.GreaterThan(500f), r.ToString());
        }

        [Test]
        public void G_RoadSurfaceValidationHasNoCriticalFindings()
        {
            var v = new RoadSurfaceValidator(Bed);
            TestContext.WriteLine(v.Report(30));
            Assert.That(v.Critical, Is.EqualTo(0), v.Report(30));
            Assert.That(v.summary.gridSlotsOk, Is.EqualTo(20));
            Assert.That(v.summary.maxSurfaceOffset, Is.LessThan(0.002f), "collider = designed surface");
            // The dramatic elevation stays: Yıldız summit ~100 m, waterfront ~5 m.
            Assert.That(v.summary.heightMax - v.summary.heightMin, Is.GreaterThan(85f));
        }
    }
}
