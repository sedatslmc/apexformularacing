using System.Collections.Generic;
using System.Numerics;
using Apex.Race;
using NUnit.Framework;

namespace Apex.Tests
{
    public class ReplayTests
    {
        static List<(Vector3, Quaternion, float)> Pose(float x, float speed) =>
            new List<(Vector3, Quaternion, float)> { (new Vector3(x, 0f, 0f), Quaternion.Identity, speed), (new Vector3(0f, 0f, x), Quaternion.Identity, speed * 2f) };

        [Test]
        public void SampleInterpolatesBetweenFrames()
        {
            var clip = new ReplayClip(2);
            clip.Add(0f, Pose(0f, 10f));
            clip.Add(0.05f, Pose(1f, 20f));
            var (p, _, v) = clip.Sample(0, 0.025f);
            Assert.That(p.X, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(v, Is.EqualTo(15f).Within(1e-4f));
            var (p1, _, _) = clip.Sample(1, 0.05f);
            Assert.That(p1.Z, Is.EqualTo(1f).Within(1e-4f));
            // Clamped outside the clip.
            Assert.That(clip.Sample(0, 9f).pos.X, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(clip.Sample(0, -1f).pos.X, Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void RecorderRateAndBoundedMemory()
        {
            var clip = new ReplayClip(2);
            float dt = 1f / 120f;
            for (int step = 0; step < 120 * 60; step++)
            {
                float t = step * dt;
                if (clip.Due(t)) clip.Add(t, Pose(t, 1f));
            }
            Assert.That(clip.FrameCount, Is.InRange(1190, 1210), "20 Hz over 60 s");
            Assert.That(clip.Duration, Is.LessThanOrEqualTo(ReplayClip.MaxSeconds));
        }

        [Test]
        public void TvSitesStandOutsideTheRoadAndCutForward()
        {
            var g = TestData.Geometry("bosphorus_a");
            var sites = TvDirector.Sites(g, null, null);
            Assert.That(sites.Count, Is.GreaterThan(g.Length / TvDirector.Spacing * 0.9f));
            foreach (var s in sites)
            {
                Assert.That(TvDirector.ClearOfRoad(g, s.position, 1.9f), Is.True, $"site at {s.distance:F0} m is on the road");
            }
            // Driving a lap: the chosen camera is never far behind the car and cuts move forward.
            int cam = -1, cuts = 0;
            for (float d = 0f; d < g.Length; d += 5f)
            {
                int next = TvDirector.Choose(sites, g.Length, d, g.PositionAt(d), cam);
                Assert.That(next, Is.GreaterThanOrEqualTo(0));
                if (next != cam) { cuts++; cam = next; }
                float past = Apex.Core.ApexMath.PosMod(d - sites[cam].distance, g.Length);
                if (past > g.Length * 0.5f) past -= g.Length;
                Assert.That(past, Is.LessThan(50f));
                Assert.That(System.Numerics.Vector3.Distance(sites[cam].position, g.PositionAt(d)), Is.LessThan(TvDirector.Spacing * 2f + 50f));
            }
            Assert.That(cuts, Is.InRange(sites.Count - 2, sites.Count + 2));
            Assert.That(TvDirector.Fov(10f), Is.GreaterThan(TvDirector.Fov(150f)));
        }
    
        [Test]
        public void TransportPlaysSkipsAndStopsAtTheEnd()
        {
            var tr = new ReplayTransport(10f, 70f, 20, 3);
            tr.Step(1f);
            Assert.That(tr.time, Is.EqualTo(11f).Within(1e-4f));
            tr.Faster();
            tr.Step(1f);
            Assert.That(tr.time, Is.EqualTo(13f).Within(1e-4f));
            tr.Skip(-100f);
            Assert.That(tr.time, Is.EqualTo(10f));
            tr.Skip(1000f);
            tr.Step(0.1f);
            Assert.That(tr.paused && tr.AtEnd, Is.True);
            tr.TogglePause();
            Assert.That(tr.time, Is.EqualTo(10f), "play again from the start");
            tr.NextCar(-4);
            Assert.That(tr.car, Is.EqualTo(19));
            for (int i = 0; i < 5; i++) tr.NextCamera();
            Assert.That(tr.camera, Is.EqualTo(ReplayCamera.Tv));
            Assert.That(ReplayTransport.CameraNames.Length, Is.EqualTo(System.Enum.GetValues(typeof(ReplayCamera)).Length));
            Assert.That(ReplayTransport.Clock(125.7f), Is.EqualTo("2:05"));
        }
    }
}
