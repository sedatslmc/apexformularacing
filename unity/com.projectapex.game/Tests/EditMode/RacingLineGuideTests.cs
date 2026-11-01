using System;
using System.Collections.Generic;
using Apex.AI;
using Apex.Tracks;
using Apex.Vehicle;
using NUnit.Framework;

namespace Apex.Tests
{
    /// <summary>Test E of the handling brief: the dynamic racing line on bosphorus_a.</summary>
    public class RacingLineGuideTests
    {
        static RacingLineGuide guide;
        static TrackGeometry geo;

        static RacingLineGuide Guide()
        {
            if (guide != null) return guide;
            geo = TrackGeometry.FromDefinition(TestData.Circuit("bosphorus_a"));
            guide = new RacingLineGuide(CarPerformance.For(new VehicleSetup()).Line(geo));
            return guide;
        }

        /// <summary>
        /// The corner after the longest full-speed stretch (T1 at the end of the Bosphorus straight, 342 → ~60 km/h):
        /// lap distance of its slowest point and the planned speed there.
        /// </summary>
        static (float apex, float speed) SlowestCorner()
        {
            var gd = Guide();
            float runStart = 0f, bestLen = 0f, bestEnd = 0f;
            bool inRun = false;
            for (float d = 0f; d < geo.Length; d += 5f)
            {
                bool full = gd.Allowed(d, 1f) >= RacingLine.MaxSpeed - 0.1f;
                if (full && !inRun) { inRun = true; runStart = d; }
                if (!full && inRun) { inRun = false; if (d - runStart > bestLen) { bestLen = d - runStart; bestEnd = d; } }
            }
            float best = float.MaxValue, at = 0f;
            for (float d = bestEnd; d < bestEnd + 500f; d += 1f)
            {
                float a = gd.Allowed(geo.Wrap(d), 1f);
                if (a < best) { best = a; at = geo.Wrap(d); }
            }
            return (at, best);
        }

        static List<RacingLineGuide.Dash> Look(float carDistance, float kmh)
        {
            var list = new List<RacingLineGuide.Dash>();
            Guide().Evaluate(carDistance, kmh / 3.6f, 1f, RacingLineGuide.Mode.WholeLap, true, list);
            return list;
        }

        /// <summary>Distance before the apex where the first red dash appears for a car <paramref name="before"/> m ahead of it.</summary>
        static float RedStartBeforeApex(float apex, float before, float kmh)
        {
            foreach (var d in Look(geo.Wrap(apex - before), kmh))
                if (d.hint == RacingLineGuide.Hint.Red) return before - d.ahead;
            return float.NaN;
        }

        [Test]
        public void BrakingWarningMovesWithTheApproachSpeed()
        {
            var (apex, vMin) = SlowestCorner();
            TestContext.WriteLine($"corner after the longest straight: apex {apex:F0} m, plan {vMin * 3.6f:F0} km/h");
            Assert.That(vMin * 3.6f, Is.InRange(40f, 110f), "a hairpin at the end of the straight");
            float at300 = RedStartBeforeApex(apex, 360f, 300f);
            float at170 = RedStartBeforeApex(apex, 200f, 170f);   // within the 212 m look-ahead at 170 km/h
            TestContext.WriteLine($"red starts {at300:F0} m before the apex at 300 km/h, {at170:F0} m at 170 km/h");
            Assert.That(at300, Is.GreaterThan(at170 + 40f), "faster approach, earlier braking point");
            Assert.That(at170, Is.GreaterThan(20f), "but still a braking zone at 170 km/h");
            // Slow enough already: no red or orange before the apex.
            foreach (var d in Look(geo.Wrap(apex - 150f), vMin * 3.6f - 8f))
                if (d.ahead < 140f) Assert.That(d.hint, Is.Not.EqualTo(RacingLineGuide.Hint.Red).And.Not.EqualTo(RacingLineGuide.Hint.Orange), $"{d.ahead:F0} m ahead");
            // Yellow before the red: a warning band.
            var dashes = Look(geo.Wrap(apex - 360f), 300f);
            int firstRed = dashes.FindIndex(x => x.hint == RacingLineGuide.Hint.Red);
            Assert.That(firstRed, Is.GreaterThan(0));
            Assert.That(dashes[firstRed - 1].hint, Is.EqualTo(RacingLineGuide.Hint.Yellow).Or.EqualTo(RacingLineGuide.Hint.Orange), "warning before the braking point");
        }

        [Test]
        public void TheLineTurnsGreenAfterTheApexAndIsGreenOnTheStraights()
        {
            var (apex, vMin) = SlowestCorner();
            // At the apex at the corner's speed: the dashes after the apex (plan rising) are green within ~40 m.
            var after = Look(apex, vMin * 3.6f);
            int green = after.FindIndex(x => x.ahead > 8f && x.hint == RacingLineGuide.Hint.Green);
            Assert.That(green, Is.GreaterThanOrEqualTo(0));
            // (T1 is an S: the second, quicker part follows the apex, so green comes after ~65 m.)
            Assert.That(after[green].ahead, Is.LessThan(90f), "green soon after the apex");
            // On the long straight at moderate speed: mostly green.
            int greens = 0, all = 0;
            foreach (var d in Look(geo.Wrap(apex - 1200f), 150f)) { all++; if (d.hint == RacingLineGuide.Hint.Green) greens++; }
            Assert.That(greens, Is.GreaterThan(all / 2), "mostly green on a straight at moderate speed");
        }

        [Test]
        public void TheLineStaysOnTheAsphaltEverywhere()
        {
            var gd = Guide();
            for (int i = 0; i < gd.DashCount; i++)
            {
                var (offset, half) = gd.DashLateral(i);
                Assert.That(Math.Abs(offset) + RacingLineGuide.DashHalfWidth, Is.LessThanOrEqualTo(half - 1.0f), $"dash {i} at {gd.dashDistance[i]:F0} m");
            }
        }

        [Test]
        public void TheBrakingPointDoesNotJumpAsTheCarApproaches()
        {
            var (apex, _) = SlowestCorner();
            // A car at 300 km/h coming closer: the lap position where red starts stays put (no flicker).
            float first = float.NaN;
            for (float before = 370f; before > 250f; before -= 3f)
            {
                float red = RedStartBeforeApex(apex, before, 300f);
                if (float.IsNaN(red)) continue;
                if (float.IsNaN(first)) first = red;
                Assert.That(Math.Abs(red - first), Is.LessThan(RacingLineGuide.DashSpacing + 0.5f), $"{before:F0} m before");
            }
            Assert.That(first, Is.Not.NaN);
        }

        [Test]
        public void TheRedZoneLeavesEnoughRoomForTheRealBrakes()
        {
            // Physics check: full braking from 300 km/h where red starts, on the 6-DOF pad (ABS), reaches the corner's
            // speed before the apex – the guide matches the car, with margin.
            var (apex, vMin) = SlowestCorner();
            float red = RedStartBeforeApex(apex, 360f, 300f);
            var b = new HandlingBenchmark(null, 1).Brake(300f, vMin * 3.6f);
            TestContext.WriteLine($"red {red:F0} m before the apex, the car needs {b.distance:F0} m from 300 to {vMin * 3.6f:F0} km/h");
            Assert.That(b.distance, Is.LessThan(red));
            // Not absurdly early either. The plan is conservative here: T1's braking zone lies in a fast kink
            // (R ≈ 270 m) whose cornering takes part of the grip, and the planner keeps a margin for keys and ABS.
            Assert.That(b.distance, Is.GreaterThan(red * 0.3f), "not absurdly early either");
        }

        [Test]
        public void WetTrackAndModesChangeTheGuide()
        {
            var (apex, _) = SlowestCorner();
            var gd = Guide();
            var dry = new List<RacingLineGuide.Dash>();
            var wet = new List<RacingLineGuide.Dash>();
            gd.Evaluate(geo.Wrap(apex - 420f), 300f / 3.6f, 1f, RacingLineGuide.Mode.WholeLap, true, dry);
            gd.Evaluate(geo.Wrap(apex - 420f), 300f / 3.6f, 0.75f, RacingLineGuide.Mode.WholeLap, true, wet);
            float FirstRed(List<RacingLineGuide.Dash> l) => l.Find(x => x.hint == RacingLineGuide.Hint.Red).ahead;
            Assert.That(dry.Exists(x => x.hint == RacingLineGuide.Hint.Red), Is.True);
            Assert.That(FirstRed(wet), Is.LessThan(FirstRed(dry)), "earlier braking in the wet");
            var off = new List<RacingLineGuide.Dash>();
            gd.Evaluate(100f, 50f, 1f, RacingLineGuide.Mode.Off, true, off);
            Assert.That(off, Is.Empty);
            var plain = new List<RacingLineGuide.Dash>();
            gd.Evaluate(geo.Wrap(apex - 360f), 300f / 3.6f, 1f, RacingLineGuide.Mode.WholeLap, false, plain);
            Assert.That(plain.TrueForAll(x => x.hint == RacingLineGuide.Hint.Green), "braking colours off: line only");
            var corners = new List<RacingLineGuide.Dash>();
            gd.Evaluate(geo.Wrap(apex - 1200f), 250f / 3.6f, 1f, RacingLineGuide.Mode.CornersOnly, true, corners);
            var whole = new List<RacingLineGuide.Dash>();
            gd.Evaluate(geo.Wrap(apex - 1200f), 250f / 3.6f, 1f, RacingLineGuide.Mode.WholeLap, true, whole);
            Assert.That(corners.Count, Is.LessThan(whole.Count), "corners only hides the straights");
            // Look-ahead grows with speed.
            Assert.That(RacingLineGuide.LookAhead(80f), Is.GreaterThan(RacingLineGuide.LookAhead(30f)));
        }
    }
}
