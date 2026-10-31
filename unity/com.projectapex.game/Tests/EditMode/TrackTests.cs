using System;
using System.Numerics;
using Apex.Core;
using Apex.Tracks;
using NUnit.Framework;

namespace Apex.Tests
{
    public class TrackTests
    {
        static readonly string[] Tracks = { "test_loop", "bosphorus_gp", "bosphorus_a", "bosphorus_a_classic" };
        static readonly string[] OpenTracks = { "test_loop", "bosphorus_gp" };

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

        [TestCaseSource(nameof(OpenTracks))]
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

        /// <summary>
        /// Variant A with the Ortaköy R20 routing (approved 2026-10-31): Muallim Naci Cd. past Ortaköy Square, a 20 m
        /// left-hand hairpin on the square's open north part, back on a parallel carriageway into the Palanga climb.
        /// </summary>
        [Test]
        public void BosphorusVariantAMatchesTheApprovedPlan()
        {
            var def = TestData.Circuit("bosphorus_a");
            var g = TrackGeometry.FromDefinition(def);
            Assert.That(def.name, Is.EqualTo("Bosphorus Grand Prix"));
            Assert.That(g.Length, Is.InRange(8000f, 8300f), "approved R20 length ~8.15 km");
            Assert.That(def.cornerNames.Count, Is.EqualTo(16));
            var corners = g.CornersFor(def);
            Assert.That(corners.Count, Is.EqualTo(16), "approved corner count (R20)");
            float Lap(int k) => g.Wrap(g.Distances[corners[k].index] - def.startLineDistance);
            int Find(string name) => def.cornerNames.IndexOf(name);
            // Ortaköy: Muallim Naci kink, right onto the square, the hairpin (left), the exit (left).
            Assert.That(corners[Find("Muallim Naci")].right, Is.True);
            Assert.That(corners[Find("Meydan Girişi")].right, Is.True);
            Assert.That(corners[Find("Ortaköy Meydan Virajı")].right, Is.False);
            Assert.That(Lap(Find("Ortaköy Meydan Virajı")), Is.InRange(2300f, 2500f));
            Assert.That(corners[Find("Barbaros İnişi")].right, Is.True);
            // The hairpin turns round on Ortaköy Square: 40-80 m from the mosque, never on it.
            var mosque = def.FindLandmark("ortakoy_mosque");
            float minMosque = float.MaxValue;
            foreach (var p in g.Positions) minMosque = Math.Min(minMosque, (float)Math.Sqrt((p.X - mosque.east) * (p.X - mosque.east) + (p.Z - mosque.north) * (p.Z - mosque.north)));
            Assert.That(minMosque, Is.InRange(40f, 80f), "hairpin beside the mosque, not on it");
            AssertCommonVariantA(def, g);
        }

        /// <summary>The earlier layout (Ortaköy hairpin at the end of Çırağan Cd.), kept as bosphorus_a_classic.</summary>
        [Test]
        public void BosphorusClassicLayoutIsTheEarlierApprovedPlan()
        {
            var def = TestData.Circuit("bosphorus_a_classic");
            var g = TrackGeometry.FromDefinition(def);
            Assert.That(def.cityId, Is.EqualTo("bosphorus_a"), "shares the real-city data");
            Assert.That(g.Length, Is.InRange(7500f, 7800f), "approved length ~7.64 km");
            Assert.That(def.cornerNames.Count, Is.EqualTo(13));
            var corners = g.CornersFor(def);
            Assert.That(corners.Count, Is.EqualTo(13), "approved corner count");
            // T1 Ortaköy is a left hairpin at ~2.15 km, T6 the right-hander at the bottom of Barbaros.
            Assert.That(corners[0].right, Is.False);
            Assert.That(g.Wrap(g.Distances[corners[0].index] - def.startLineDistance), Is.InRange(2050f, 2250f));
            Assert.That(corners[5].right, Is.True);
            AssertCommonVariantA(def, g);
        }

        static void AssertCommonVariantA(CircuitDefinition def, TrackGeometry g)
        {
            Assert.That(def.direction, Is.EqualTo("counter_clockwise"));
            Assert.That(def.sectorFractions.Length, Is.EqualTo(3));
            var corners = g.CornersFor(def);
            for (int i = 1; i < corners.Count; i++)
                Assert.That(g.Distances[corners[i].index], Is.Not.EqualTo(g.Distances[corners[i - 1].index]));
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in g.Positions) { minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y); }
            Assert.That(maxY - minY, Is.InRange(80f, 115f), "Palanga/Yıldız climb");
            // Game gradient stays below 12.5 % over 50 m everywhere.
            int w = (int)(50f / g.Spacing);
            for (int i = 0; i < g.Count; i++)
            {
                var a = g.Positions[i];
                var b = g.Positions[(i + w) % g.Count];
                Assert.That(Math.Abs(b.Y - a.Y) / (w * g.Spacing), Is.LessThan(0.125f), $"grade at {g.Distances[i]:F0} m");
            }
            // Start/finish on Dolmabahçe Caddesi (OSM axis, game frame = true north up).
            var sf = g.PositionAt(def.startLineDistance);
            Assert.That(Math.Sqrt((sf.X + 53f) * (sf.X + 53f) + (sf.Z - 155f) * (sf.Z - 155f)), Is.LessThan(15.0));
            // 20 grid slots fit on the straight inside the track.
            for (int slot = 0; slot < 20; slot++)
            {
                float d = g.Wrap(def.startLineDistance - 12f - slot * 8f);
                Assert.That(g.WidthAt(d) * 0.5f, Is.GreaterThan(3f + 1.2f), $"grid slot {slot}");
            }
            Assert.That(def.pit, Is.Not.Null);
            Assert.That(def.pit.Contains(def.startLineDistance, g.Length), Is.True, "pit lane alongside the line");
            Assert.That(def.pit.BoxDistance(19) - def.pit.BoxDistance(0), Is.LessThan(def.pit.exit - def.pit.entry - 60f), "20 boxes fit");
        }

        [Test]
        public void BosphorusBarriersNeverCrossAnotherPartOfTheTrack()
        {
            // Street circuit: walls 0.5–12 m from the edge. Where two parts of the lap run side
            // by side (Beşiktaş Cd. carriageways) both must fit with their walls.
            var def = TestData.Circuit("bosphorus_a");
            var g = TrackGeometry.FromDefinition(def);
            Assert.That(g.RunoffLeft, Is.Not.Null);
            int step = 3;
            for (int i = 0; i < g.Count; i += step)
            for (int j = i + step; j < g.Count; j += step)
            {
                float along = Math.Min(j - i, g.Count - (j - i)) * g.Spacing;
                if (along < 300f) continue;
                float d = MathF.Sqrt(TrackGeometry.FlatDist2(g.Positions[i], g.Positions[j]));
                if (d > 60f) continue;
                float need = g.Widths[i] * 0.5f + g.Widths[j] * 0.5f + 1.0f;
                Assert.That(d, Is.GreaterThan(need), $"parts at {g.Distances[i]:F0} m and {g.Distances[j]:F0} m overlap");
                float wallI = Math.Min(g.RunoffLeft[i], g.RunoffRight[i]) + g.Widths[i] * 0.5f;
                Assert.That(wallI, Is.LessThan(d - g.Widths[j] * 0.5f + 0.01f), $"wall of {g.Distances[i]:F0} m inside the other carriageway");
            }
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
