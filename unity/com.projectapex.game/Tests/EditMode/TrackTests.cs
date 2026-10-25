using System;
using System.Numerics;
using Apex.Core;
using Apex.Tracks;
using NUnit.Framework;

namespace Apex.Tests
{
    public class TrackTests
    {
        static readonly string[] Tracks = { "test_loop", "bosphorus_gp" };

        [TestCaseSource(nameof(Tracks))]
        public void LayoutLoadsAndIsContinuous(string id)
        {
            var g = TestData.Geometry(id);
            Assert.That(g.Count, Is.GreaterThan(500));
            for (int i = 0; i < g.Count; i++)
            {
                float step = Vector3.Distance(g.Positions[i], g.Positions[(i + 1) % g.Count]);
                Assert.That(step, Is.EqualTo(g.Spacing).Within(0.3f), $"spacing at {i}");
            }
        }

        [TestCaseSource(nameof(Tracks))]
        public void RunsCounterClockwise(string id)
        {
            var g = TestData.Geometry(id);
            float total = 0f;
            foreach (float k in g.Curvatures) total += k * g.Spacing;
            // Positive curvature = right turn, so a counter-clockwise lap sums to -360°.
            Assert.That(total * ApexMath.Rad2Deg, Is.EqualTo(-360f).Within(5f));
        }

        [TestCaseSource(nameof(Tracks))]
        public void SectionsKeepClearOfEachOther(string id)
        {
            var g = TestData.Geometry(id);
            float minClear = float.MaxValue;
            for (int i = 0; i < g.Count; i += 5)
            for (int j = i + 5; j < g.Count; j += 5)
            {
                float along = Math.Min(j - i, g.Count - (j - i)) * g.Spacing;
                if (along < 200f) continue;
                minClear = Math.Min(minClear, MathF.Sqrt(TrackGeometry.FlatDist2(g.Positions[i], g.Positions[j])));
            }
            // Two half-widths plus kerbs and run-off on both sides.
            Assert.That(minClear, Is.GreaterThan(60f));
        }

        [Test]
        public void BosphorusMatchesDesignTargets()
        {
            var def = TestData.Circuit("bosphorus_gp");
            var g = TrackGeometry.FromDefinition(def);
            Assert.That(g.Length, Is.InRange(4800f, 5400f), "length ~5.1 km");
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in g.Positions) { minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y); }
            Assert.That(maxY - minY, Is.InRange(35f, 55f), "elevation range");
            Assert.That(def.sectorFractions.Length, Is.EqualTo(3));
            Assert.That(def.direction, Is.EqualTo("counter_clockwise"));
            var corners = g.DetectCorners();
            Assert.That(corners.Count, Is.InRange(19, 21), "corner count");
        }

        [Test]
        public void ProjectionRoundTrip()
        {
            var g = TestData.Geometry("test_loop");
            foreach (float d in new[] { 0f, 123.4f, 1500f, g.Length - 3f })
            {
                var p = g.PositionAt(d) + g.RightAt(d) * 3f;
                var proj = g.Project(p);
                float err = Math.Abs(proj.distance - d);
                err = Math.Min(err, g.Length - err);
                Assert.That(err, Is.LessThan(1f), $"distance at {d}");
                Assert.That(proj.lateral, Is.EqualTo(3f).Within(0.3f), $"lateral at {d}");
            }
        }

        [Test]
        public void MiniJsonParsesNestedData()
        {
            var o = (System.Collections.Generic.Dictionary<string, object>)MiniJson.Parse("{\"a\":[1,2.5,-3e2],\"b\":{\"c\":\"x\\\"y\"},\"d\":true,\"e\":null}");
            var a = (System.Collections.Generic.List<object>)o["a"];
            Assert.That((double)a[2], Is.EqualTo(-300.0));
            Assert.That(((System.Collections.Generic.Dictionary<string, object>)o["b"])["c"], Is.EqualTo("x\"y"));
            Assert.That(o["d"], Is.EqualTo(true));
            Assert.That(o["e"], Is.Null);
        }
    }
}
