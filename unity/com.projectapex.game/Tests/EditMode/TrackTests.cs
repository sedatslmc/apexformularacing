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

        [Test]
        public void BosphorusGeographyIsConsistent()
        {
            var def = TestData.Circuit("bosphorus_gp");
            var g = TrackGeometry.FromDefinition(def);
            Assert.That(def.landmarks.Count, Is.GreaterThanOrEqualTo(10));
            var eu = def.FindLandmark("bridge_eu");
            var asia = def.FindLandmark("bridge_as");
            var mosque = def.FindLandmark("ortakoy_mosque");
            var palace = def.FindLandmark("dolmabahce");
            float Dist(float ax, float az, float bx, float bz) => (float)Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));
            // Real distances are preserved by the rigid placement (scale 1:1).
            Assert.That(Dist(eu.east, eu.north, asia.east, asia.north), Is.InRange(1500f, 1620f), "bridge length");
            Assert.That(Dist(eu.east, eu.north, mosque.east, mosque.north), Is.InRange(380f, 520f), "mosque just south of the bridge");
            Assert.That(mosque.verified && eu.verified && palace.verified, Is.True);
            // Track keeps clear of the water and of the big landmarks.
            float ShoreX(float z)
            {
                var sh = def.shoreline;
                if (z <= sh[0].north) return sh[0].east;
                for (int i = 0; i < sh.Count - 1; i++)
                    if (z <= sh[i + 1].north)
                        return sh[i].east + (sh[i + 1].east - sh[i].east) * (z - sh[i].north) / (sh[i + 1].north - sh[i].north);
                return sh[sh.Count - 1].east;
            }
            float minPalace = float.MaxValue, minMosque = float.MaxValue;
            for (int i = 0; i < g.Count; i++)
            {
                var p = g.Positions[i];
                Assert.That(p.X, Is.LessThan(ShoreX(p.Z) - 10f), $"track in the water at {i}");
                minPalace = Math.Min(minPalace, Dist(p.X, p.Z, palace.east, palace.north));
                minMosque = Math.Min(minMosque, Dist(p.X, p.Z, mosque.east, mosque.north));
            }
            Assert.That(minPalace, Is.GreaterThan(150f));
            Assert.That(minMosque, Is.InRange(200f, 900f), "mosque visible but not on the circuit");
            // The Asian shore is across the water, east of the European one.
            Assert.That(def.farShoreline.Count, Is.GreaterThan(3));
            foreach (var (east, north) in def.farShoreline)
                if (north > def.shoreline[0].north && north < def.shoreline[def.shoreline.Count - 1].north)
                    Assert.That(east, Is.GreaterThan(ShoreX(north) + 600f));
        }
    }
}
