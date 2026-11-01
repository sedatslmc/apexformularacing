using System;
using Apex.Core;
using NUnit.Framework;

namespace Apex.Tests
{
    /// <summary>Phase 6: the frame-time summary of a performance capture (PerfCapture).</summary>
    public class FrameStatsTests
    {
        [Test]
        public void SummaryFindsPercentilesHitchesAndTheSlowSection()
        {
            var st = new FrameStats();
            // 1000 frames at 16 ms around a 2000 m lap, 22 ms between 1200 and 1400 m, and three 80 ms hitches.
            for (int i = 0; i < 1000; i++)
            {
                float d = i * 2f;
                float ms = d >= 1200f && d < 1400f ? 22f : 16f;
                if (i == 100 || i == 500 || i == 900) ms = 80f;
                st.Add(ms, d, 900f, 200f, 2.5e6f);
            }
            var s = st.Summarize(FrameStats.PcTargetMs, 2000f, 200f);
            Assert.That(s.frames, Is.EqualTo(1000));
            Assert.That(s.p50Ms, Is.EqualTo(16f).Within(1e-4f));
            Assert.That(s.p95Ms, Is.EqualTo(22f).Within(1e-4f));
            Assert.That(s.maxMs, Is.EqualTo(80f));
            Assert.That(s.hitches, Is.EqualTo(3));
            Assert.That(s.withinTarget, Is.EqualTo(0.897f).Within(1e-3f), "100 slow frames and 3 hitches miss 16.7 ms");
            Assert.That(s.low1Fps, Is.LessThan(s.averageFps));
            Assert.That(s.sections.Count, Is.EqualTo(10));
            var slow = FrameStats.Slowest(s, 1)[0];
            Assert.That(slow.from, Is.EqualTo(1200f), "the slow part of the lap");
            Assert.That(s.drawCallsAvg, Is.EqualTo(900f));
            string report = FrameStats.Report("Test", s);
            StringAssert.Contains("1200–1400 m", report);
            StringAssert.Contains("Draw Calls", report);
            Assert.That(st.Csv().Split('\n').Length, Is.EqualTo(1002), "header, 1000 rows, trailing newline");
        }

        [Test]
        public void MissingCountersAndEmptyCapturesAreReportedHonestly()
        {
            var st = new FrameStats();
            Assert.That(st.Summarize(FrameStats.MobileTargetMs).frames, Is.EqualTo(0));
            for (int i = 0; i < 50; i++) st.Add(30f);
            var s = st.Summarize(FrameStats.MobileTargetMs, 1000f);
            Assert.That(s.drawCallsAvg, Is.LessThan(0f));
            Assert.That(s.sections, Is.Empty, "no distance, no sections");
            Assert.That(s.withinTarget, Is.EqualTo(1f));
            StringAssert.Contains("nicht verfügbar", FrameStats.Report("Test", s));
            Assert.That(FrameStats.Percentile(new System.Collections.Generic.List<float> { 1f, 3f }, 50f), Is.EqualTo(2f));
        }
    }
}
