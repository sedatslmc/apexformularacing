using Apex.Career;
using Apex.UI;
using NUnit.Framework;

namespace Apex.Tests
{
    public class TrackRecordsTests
    {
        [Test]
        public void BestLapBestSectorsAndRecentLaps()
        {
            var b = new TrackRecordBook();
            Assert.That(b.Submit("t", 90.0, true, new[] { 30.0, 31.0, 29.0 }, "Zeitfahren"), Is.True);
            Assert.That(b.Submit("t", 91.0, true, new[] { 29.5, 32.0, 29.5 }, "Zeitfahren"), Is.False, "slower lap");
            Assert.That(b.Submit("t", 85.0, false, new[] { 28.0, 29.0, 28.0 }, "Zeitfahren"), Is.False, "invalid laps never count");
            var r = b.Find("t");
            Assert.That(r.bestLap, Is.EqualTo(90.0));
            Assert.That(r.bestLapSectors, Is.EqualTo(new[] { 30.0, 31.0, 29.0 }));
            Assert.That(r.bestSectors, Is.EqualTo(new[] { 29.5, 31.0, 29.0 }), "best single sectors over valid laps");
            Assert.That(r.TheoreticalBest, Is.EqualTo(89.5).Within(1e-9));
            Assert.That(r.recent.Count, Is.EqualTo(3));
            Assert.That(r.recent[0].valid, Is.False, "newest first");
            for (int i = 0; i < 20; i++) b.Submit("t", 95.0 + i, true, new[] { 31.0, 32.0, 32.0 }, "Training");
            Assert.That(r.recent.Count, Is.EqualTo(TrackRecordBook.RecentLaps));
            Assert.That(b.Find("other"), Is.Null);
            Assert.That(b.Submit("t", double.PositiveInfinity, true, null, ""), Is.False);
        }

        [Test]
        public void RecordsPanelRows()
        {
            Assert.That(TimeTrialRecords.Sectors(null, 3), Is.Empty);
            Assert.That(TimeTrialRecords.Recent(null, 6), Is.Empty);
            var b = new TrackRecordBook();
            b.Submit("t", 90.0, true, new[] { 30.0, 31.0, 29.0 }, "Zeitfahren");
            b.Submit("t", 91.0, true, new[] { 29.5, 32.0, 29.5 }, "Zeitfahren");
            b.Submit("t", 88.0, false, new[] { 29.0, 30.0, 29.0 }, "Zeitfahren");
            var r = b.Find("t");
            var s = TimeTrialRecords.Sectors(r, 3);
            Assert.That(s.Count, Is.EqualTo(4), "three sectors and the ideal lap");
            Assert.That(s[0].value, Is.EqualTo("29.500"));
            Assert.That(s[0].note, Does.Contain("30.000"), "the best lap was slower in S1");
            Assert.That(s[1].note, Is.Empty);
            Assert.That(s[3].note, Is.EqualTo("-0.500"));
            var recent = TimeTrialRecords.Recent(r, 6);
            Assert.That(recent[0].tone, Is.EqualTo(2), "invalid");
            Assert.That(recent[1].note, Is.EqualTo("+1.000"));
            Assert.That(recent[2].tone, Is.EqualTo(1), "best lap");
        }
    }
}
