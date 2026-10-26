using System;
using System.Collections.Generic;
using System.Numerics;
using Apex.AI;
using Apex.Environment;
using Apex.Tracks;
using Apex.Vehicle;
using NUnit.Framework;

namespace Apex.Tests
{
    public class CityTests
    {
        static (CircuitDefinition def, TerrainField field, TrackClearance clear, CityData data, OsmCity city) world;

        static (CircuitDefinition def, TerrainField field, TrackClearance clear, CityData data, OsmCity city) World()
        {
            if (world.city != null) return world;
            var def = TestData.Circuit("bosphorus_a");
            var g = TrackGeometry.FromDefinition(def);
            var setup = new VehicleSetup();
            var line = new RacingLine(g, 1.45f, setup.downforceArea * setup.airDensity * 0.5f / setup.mass);
            var plan = new TrackInfrastructurePlan(g, g.CornersFor(def), line.offsets, line.speed);
            var surface = new RoadSurface(g, def, plan.kerbs.left, plan.kerbs.right, plan.kerbs.height);
            var field = new TerrainField(g, def);
            var clear = new TrackClearance(surface);
            var data = TestData.City("bosphorus_a");
            world = (def, field, clear, data, new OsmCity(data, field, clear, def, null, 1));
            return world;
        }

        [Test]
        public void EarClippingCoversConcaveFootprints()
        {
            // L-shaped footprint, given clockwise: area 300 m².
            var l = new List<Vector2> { new Vector2(0, 0), new Vector2(0, 20), new Vector2(10, 20), new Vector2(10, 10), new Vector2(20, 10), new Vector2(20, 0) };
            var tris = Polygon.Triangulate(l);
            Assert.That(tris.Count, Is.EqualTo(12));
            float sum = 0f;
            for (int i = 0; i < tris.Count; i += 3)
            {
                Vector2 a = l[tris[i]], b = l[tris[i + 1]], c = l[tris[i + 2]];
                float cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
                Assert.That(cross, Is.GreaterThan(0f), "counter-clockwise triangles");
                sum += cross * 0.5f;
            }
            Assert.That(sum, Is.EqualTo(300f).Within(1e-3f));
            Assert.That(Polygon.Contains(l, 15f, 15f), Is.False, "the notch is outside");
            Assert.That(Polygon.Contains(l, 5f, 15f), Is.True);
        }

        [Test]
        public void TheRealCityStandsBesideTheTrackNotOnIt()
        {
            var (_, field, clear, data, city) = World();
            TestContext.WriteLine($"{data.buildings.Count} OSM buildings -> {city.buildings.Count} placed ({city.removedTrack} at the track, {city.removedWater} water, {city.removedReserved} landmark areas), {city.trees.Count} trees");
            Assert.That(data.buildings.Count, Is.GreaterThan(3000));
            Assert.That(city.buildings.Count, Is.GreaterThan(data.buildings.Count / 2), "most of the real city stays");
            foreach (var b in city.buildings)
            {
                Assert.That(Polygon.SignedArea(b.outline), Is.GreaterThan(0f), "counter-clockwise");
                foreach (var p in b.outline)
                {
                    Assert.That(clear.Margin(p.X, p.Y), Is.GreaterThanOrEqualTo(OsmCity.BuildingClearance), $"building corner at {p.X:F0},{p.Y:F0}");
                    Assert.That(field.IsWater(p.X, p.Y), Is.False);
                }
                Assert.That(b.height, Is.InRange(3f, 60f));
            }
            int cypress = 0;
            foreach (var t in city.trees)
            {
                Assert.That(clear.Margin(t.x, t.z), Is.GreaterThanOrEqualTo(OsmCity.TreeClearance), $"tree at {t.x:F0},{t.z:F0}");
                if (t.kind == 2) cypress++;
            }
            Assert.That(city.trees.Count, Is.GreaterThan(1500), "parks, woods, cemeteries and street trees");
            Assert.That(cypress, Is.GreaterThan(50), "cypresses in the cemeteries");
        }
    }
}
