using System;
using Apex.Race;
using NUnit.Framework;

namespace Apex.Tests
{
    /// <summary>Stopping on the box, safe release, merge check and pit speed monitor (Apex.Race.PitBoxStop).</summary>
    public class PitBoxStopTests
    {
        [Test]
        public void TheStopWindowAcceptsOnlyACarSquareOnItsMarks()
        {
            Assert.That(PitBoxStop.Judge(0.1f, 0.1f, 2f), Is.EqualTo(PitBoxStop.Fit.Good));
            Assert.That(PitBoxStop.Judge(0.8f, 0f, 0f), Is.EqualTo(PitBoxStop.Fit.TooShort));
            Assert.That(PitBoxStop.Judge(-0.8f, 0f, 0f), Is.EqualTo(PitBoxStop.Fit.TooFar));
            Assert.That(PitBoxStop.Judge(0f, 0.5f, 0f), Is.EqualTo(PitBoxStop.Fit.OffLine));
            Assert.That(PitBoxStop.Judge(0f, 0f, 8f), Is.EqualTo(PitBoxStop.Fit.Skewed));
            // The parking aid settles what is near; without it only an overshoot is pushed back by the crew.
            Assert.That(PitBoxStop.Settleable(0.9f, 0.5f, 9f, true), Is.True);
            Assert.That(PitBoxStop.Settleable(0.9f, 0.5f, 9f, false), Is.False);
            Assert.That(PitBoxStop.Settleable(-1.5f, 0.1f, 1f, false), Is.True, "pushed back");
            Assert.That(PitBoxStop.Settleable(-3f, 0f, 0f, true), Is.False, "box missed");
            Assert.That(PitBoxStop.Settleable(0f, 1.0f, 0f, true), Is.False, "too far off the line");
        }

        [Test]
        public void SettlingIsSmoothAndShort()
        {
            Assert.That(PitBoxStop.SettleShare(0f, 0.5f), Is.EqualTo(0f));
            Assert.That(PitBoxStop.SettleShare(0.5f, 0.5f), Is.EqualTo(1f).Within(1e-5f));
            // No jump in speed at the ends (smootherstep).
            Assert.That(PitBoxStop.SettleShare(0.01f, 1f), Is.LessThan(1e-4f));
            Assert.That(PitBoxStop.SettleTime(0.05f, 0.05f), Is.LessThan(0.5f), "a few centimetres: under half a second");
            Assert.That(PitBoxStop.SettleTime(-2f, 0f), Is.InRange(1.2f, 1.8f), "push back of 2 m");
        }

        [Test]
        public void TheApproachSlowsSmoothlyOntoTheCentre()
        {
            float limit = 80f / 3.6f, prev = float.MaxValue;
            for (float d = 60f; d >= 0f; d -= 0.5f)
            {
                float v = PitBoxStop.ApproachSpeed(d, limit);
                Assert.That(v, Is.LessThanOrEqualTo(prev + 1e-4f), $"monotonic at {d}");
                Assert.That(v, Is.LessThanOrEqualTo(limit));
                prev = v;
            }
            Assert.That(PitBoxStop.ApproachSpeed(0f, limit), Is.EqualTo(0f));
            Assert.That(PitBoxStop.ApproachSpeed(0.3f, limit), Is.GreaterThan(0.3f), "still creeping onto the mark");
            Assert.That(PitBoxStop.BrakeNow(0.03f, 0.5f), Is.True);
            Assert.That(PitBoxStop.BrakeNow(3f, 2f), Is.False);
        }

        [Test]
        public void TheReleaseWaitsForACarInTheFastLane()
        {
            float fast = PitLaneLayout.FastLaneCentre, work = PitLaneLayout.Divider + 2f;
            Assert.That(PitBoxStop.BlocksRelease(20f, 22f, fast), Is.True, "80 km/h, 20 m behind");
            Assert.That(PitBoxStop.BlocksRelease(80f, 22f, fast), Is.False, "far behind");
            Assert.That(PitBoxStop.BlocksRelease(-10f, 22f, fast), Is.False, "already past");
            Assert.That(PitBoxStop.BlocksRelease(3f, 0f, fast), Is.True, "alongside");
            Assert.That(PitBoxStop.BlocksRelease(20f, 5f, work), Is.False, "in the working lane");
        }

        [Test]
        public void TheMergeWaitsForRaceTrafficAlongsideOrClosingFast()
        {
            Assert.That(PitBoxStop.BlocksMerge(3f, 40f, 45f, -1f), Is.True, "alongside");
            Assert.That(PitBoxStop.BlocksMerge(-30f, 40f, 75f, -1.5f), Is.True, "closing at 35 m/s from 30 m");
            Assert.That(PitBoxStop.BlocksMerge(-30f, 40f, 40f, -1.5f), Is.False, "same speed, 30 m behind");
            Assert.That(PitBoxStop.BlocksMerge(-10f, 40f, 75f, -8f), Is.False, "on the far side of the track");
            Assert.That(PitBoxStop.BlocksMerge(30f, 40f, 40f, -1f), Is.False, "well ahead");
        }

        [Test]
        public void SpeedingIsCaughtOncePerPassage()
        {
            var m = new PitSpeedMonitor();
            float limit = 22.22f;
            Assert.That(m.Update(true, limit + 0.2f, limit, 1f), Is.False, "within the tolerance");
            Assert.That(m.Update(true, limit + 2f, limit, 0.1f), Is.False, "grace");
            Assert.That(m.Update(true, limit + 2f, limit, 0.2f), Is.True, "offence");
            Assert.That(m.Update(true, limit + 2f, limit, 1f), Is.False, "only once");
            Assert.That(m.MaxExcess, Is.EqualTo(2f).Within(1e-4f));
            m.Reset();
            Assert.That(m.Update(false, 80f, limit, 1f), Is.False, "outside the limited zone");
        }

        [Test]
        public void TheGuideTellsTheDriverWhatToCorrect()
        {
            Assert.That(PitBoxStop.Guide(2, 120f, 0f, false), Is.EqualTo("BOX 3 · 120 m"));
            Assert.That(PitBoxStop.Guide(2, 3.2f, 0.6f, false), Is.EqualTo("BOX 3 · noch 3,2 m · 0,6 m nach links"));
            Assert.That(PitBoxStop.Guide(2, 0.1f, 0.1f, false), Is.EqualTo("BOX 3 · STOPP · mittig"));
            Assert.That(PitBoxStop.Guide(2, -1.2f, 0f, false), Is.EqualTo("BOX 3 · 1,2 m zu weit"));
            Assert.That(PitBoxStop.Guide(2, 5f, 0f, true), Is.EqualTo("BOX 3 belegt – dahinter warten"));
        }
    }
}
