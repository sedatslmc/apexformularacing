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
        [TestCase(0)]
        [TestCase(5)]
        [TestCase(9)]
        public void F_PitLaneIsDrivableEntryToExit(int box)
        {
            // As in the game with the pit assist: arriving at race speed 500 m before the entry, through the last corner, the shared autopilot
            // (PitLaneLayout.Drive: early braking, look-ahead on the path) through the entry lane past the wall's nose,
            // down the fast lane (drive-through), out through the acceleration lane and back onto the track. No wall,
            // barrier or crash cushion may be touched.
            var pit = Bed.def.pit;
            var g = Bed.g;
            float L = g.Length;
            var plan = pit.PlanFor(L);
            float Since(float raw) => -pit.ToEntry(raw, L);
            (float, float, bool) Pilot(float raw, Apex.Vehicle.SurfaceRig rig)
            {
                float since = Since(raw);
                if (since > plan.MergeEnd) return (float.NaN, float.PositiveInfinity, false);
                var cmd = Apex.Race.PitLaneLayout.Drive(plan, since, -1000f, true, pit.laneCentreFromEdge, pit.SpeedLimit, rig.ForwardSpeed);
                // Before the lane: race speed until the braking curve bites, on the pit side of the track.
                return (pit.side * (g.WidthAt(raw) * 0.5f + cmd.fromEdge), cmd.speedCap, false);
            }
            float limit(float raw) => plan.Limited(Since(raw)) ? pit.SpeedLimit : float.PositiveInfinity;
            float mergeSpeed = 0f, laneAt30 = float.NaN, entrySpeed = 0f, minWallGap = float.MaxValue, minBarrierGap = float.MaxValue, minBarrierAt = 0f;
            Bed.trace = (raw, rig) =>
            {
                float since = Since(raw);
                var pr = g.Project(rig.position);
                float fromEdge = pit.side * pr.lateral - g.WidthAt(raw) * 0.5f;
                if (since > -pit.entryTaper && since < plan.WallEnd)
                {
                    // Gap from the car's inner side to the wall / cushion face where they stand.
                    if (since > Apex.Race.PitLaneLayout.WallStart - Apex.Race.PitLaneLayout.AttenuatorLength - 3f)
                        minWallGap = MathF.Min(minWallGap, fromEdge - Apex.Race.PitLaneLayout.CarHalfWidth - Apex.Race.PitLaneLayout.WallOuter);
                }
                if (since > -1f && entrySpeed == 0f) entrySpeed = rig.ForwardSpeed;
                // The outer barrier on the pit side all the way (entry lane, lane, exit lane, merge).
                float barrier = (pit.side < 0 ? Bed.surface.wallLeft : Bed.surface.wallRight)[pr.index];
                float bg = barrier - pit.side * pr.lateral - Apex.Race.PitLaneLayout.CarHalfWidth;
                if (bg < minBarrierGap) { minBarrierGap = bg; minBarrierAt = since; }
                if (since > plan.limitSpan + 30f && float.IsNaN(laneAt30)) laneAt30 = fromEdge;
                if (since > plan.MergeEnd - 10f && mergeSpeed == 0f) mergeSpeed = rig.ForwardSpeed;
            };
            float start = 500f;   // on the straight before the last corner (R ≈ 50 m, 200 m before the entry)
            try
            {
                var r = Bed.Drive(g.Wrap(pit.entry - start), 0f, start + plan.MergeEnd + 100f, 150f,
                    initialSpeed: 62f, autoPilot: Pilot, speedLimitAt: limit);
                AssertClean(r, "pit lane");
                Assert.That(r.distanceDriven, Is.GreaterThan(plan.span + start), r.ToString());
            }
            finally { Bed.trace = null; }
            TestContext.WriteLine($"box {box}: {entrySpeed * 3.6f:F0} km/h at the entry, {minWallGap:F2} m min. gap to the wall, {minBarrierGap:F2} m to the outer barrier (at {minBarrierAt:F0} m), {laneAt30:F1} m beyond the edge 30 m after the exit line, {mergeSpeed * 3.6f:F0} km/h at the merge");
            Assert.That(entrySpeed, Is.LessThanOrEqualTo(pit.SpeedLimit + 0.5f), "at the limit at the entry");
            Assert.That(minWallGap, Is.GreaterThan(0.8f), "clear of the wall and the crash cushion");
            Assert.That(minBarrierGap, Is.GreaterThan(1.0f), "clear of the outer barrier at the entry and the exit");
            Assert.That(laneAt30, Is.GreaterThan(1f), "in the acceleration lane behind the white line");
            Assert.That(mergeSpeed, Is.GreaterThan(pit.SpeedLimit * 1.4f), $"accelerated to {mergeSpeed * 3.6f:F0} km/h before merging");
        }

        [TestCase(0)]
        [TestCase(4)]
        [TestCase(9)]
        public void H_AutomaticStopHaltsSquareOnTheBoxMarks(int box)
        {
            // From the track before the entry lane down the pit lane into the own box, with the shared pit autopilot
            // (PitLaneLayout.Drive): the car must come to rest with its origin on the box centre, on the box line and
            // square to it (PitBoxStop window), so the crew reaches the wheels.
            var pit = Bed.def.pit;
            var g = Bed.g;
            float L = g.Length;
            var plan = pit.PlanFor(L);
            float boxD = g.Wrap(pit.BoxDistance(box));
            float lastRaw = 0f; float lastLat = 0f, lastYaw = 0f, lastSpeed = 0f;
            (float, float, bool) Pilot(float raw, Apex.Vehicle.SurfaceRig rig)
            {
                float since = -pit.ToEntry(raw, L);
                float toBox = Apex.Core.ApexMath.PosMod(boxD - raw, L);
                if (toBox > L * 0.5f) toBox -= L;
                var cmd = Apex.Race.PitLaneLayout.Drive(plan, since, toBox, false, pit.laneCentreFromEdge, pit.SpeedLimit, rig.ForwardSpeed);
                return (pit.side * (g.WidthAt(raw) * 0.5f + cmd.fromEdge), cmd.speedCap, cmd.brake);
            }
            Bed.trace = (raw, rig) =>
            {
                var pr = g.Project(rig.position);
                lastRaw = pr.distance;
                lastLat = pr.lateral;
                var t = g.TangentAt(pr.distance);
                lastYaw = MathF.Atan2(rig.Forward.X * t.Z - rig.Forward.Z * t.X, rig.Forward.X * t.X + rig.Forward.Z * t.Z) * 180f / MathF.PI;
                lastSpeed = rig.ForwardSpeed;
            };
            float start = pit.entryTaper + 60f;
            CircuitTestBed.DriveResult r;
            try
            {
                r = Bed.Drive(g.Wrap(pit.entry - start), 0f, 2000f, 150f, initialSpeed: 30f, autoPilot: Pilot,
                    speedLimitAt: raw => plan.Limited(-pit.ToEntry(raw, L)) ? pit.SpeedLimit : float.PositiveInfinity, stopIsFinish: true);
            }
            finally { Bed.trace = null; }
            float toBoxEnd = Apex.Core.ApexMath.PosMod(boxD - lastRaw + L * 0.5f, L) - L * 0.5f;
            float latErr = lastLat - Bed.def.pit.side * (g.WidthAt(boxD) * 0.5f + pit.laneCentreFromEdge);
            TestContext.WriteLine($"box {box}: stopped {toBoxEnd:F3} m before the centre, {latErr:F3} m off the line, {lastYaw:F2}° skew, {lastSpeed:F2} m/s, {r}");
            Assert.That(r.Count(CircuitTestBed.EventKind.OffRoadSurface) + r.Count(CircuitTestBed.EventKind.LeftTrack) + r.Count(CircuitTestBed.EventKind.Spin), Is.EqualTo(0), r.ToString());
            Assert.That(Apex.Race.PitBoxStop.Judge(toBoxEnd, latErr, lastYaw), Is.EqualTo(Apex.Race.PitBoxStop.Fit.Good),
                $"{toBoxEnd:F2} m / {latErr:F2} m / {lastYaw:F1}°");
            Assert.That(MathF.Abs(lastSpeed), Is.LessThan(0.1f));
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
