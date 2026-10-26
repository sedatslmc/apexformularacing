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
        public void SideStreetsEndAtTheBarriersAndStayOnLand()
        {
            var (_, field, clear, data, _) = World();
            var net = new StreetNetwork(data, field, clear, 1);
            TestContext.WriteLine($"{net.roadLength / 1000f:F1} km roads, {net.footLength / 1000f:F1} km footways, {net.steps} steps, {net.chunks.Count} chunks, {net.TriangleCount} triangles");
            Assert.That(net.roadLength, Is.GreaterThan(30000f), "the side-street network around the circuit");
            Assert.That(net.footLength, Is.GreaterThan(10000f));
            Assert.That(net.steps, Is.GreaterThan(100), "Istanbul's stairs (merdiven)");
            int checkedVerts = 0;
            foreach (var c in net.chunks.Values)
                for (int i = 0; i < c.vertices.Count; i += 7)
                {
                    var v = c.vertices[i];
                    Assert.That(clear.Margin(v.X, v.Z), Is.GreaterThan(0.5f), $"street vertex at {v.X:F0},{v.Z:F0} inside the racing envelope");
                    checkedVerts++;
                }
            Assert.That(checkedVerts, Is.GreaterThan(10000));
        }

        [Test]
        public void CityLifeStaysOffTheRacingSurfaceAndMoves()
        {
            var (_, field, clear, data, city) = World();
            var life = new CityLife(data, field, clear, city, 1, 600, 220);
            TestContext.WriteLine($"{life.props.Count} props, {life.parked.Count} parked cars, {life.pedestrians.Count} pedestrians, {life.vehicles.Count} vehicles");
            Assert.That(life.props.Count, Is.GreaterThan(500));
            Assert.That(life.parked.Count, Is.GreaterThan(300));
            Assert.That(life.pedestrians.Count, Is.InRange(400, 700));
            Assert.That(life.vehicles.Count, Is.InRange(150, 260));
            int taxis = life.vehicles.FindAll(v => v.kind == (int)CityLife.Vehicle.Taxi).Count;
            Assert.That(taxis, Is.GreaterThan(life.vehicles.Count / 5), "yellow taxis");
            var start = life.vehicles.ConvertAll(v => v.position);
            // Two minutes of city life: nobody ever enters the racing envelope; traffic moves.
            float moved = 0f;
            for (int k = 0; k < 120; k++)
            {
                life.Advance(1f);
                foreach (var m in life.pedestrians) Assert.That(clear.Margin(m.position.X, m.position.Z), Is.GreaterThan(1f), "pedestrian on the track");
                foreach (var m in life.vehicles) Assert.That(clear.Margin(m.position.X, m.position.Z), Is.GreaterThan(1f), "city traffic on the track");
            }
            for (int i = 0; i < life.vehicles.Count; i++) moved += Vector3.Distance(start[i], life.vehicles[i].position);
            Assert.That(moved / life.vehicles.Count, Is.GreaterThan(30f), "traffic flows");
            foreach (var p in life.props) Assert.That(clear.Margin(p.position.X, p.position.Z), Is.GreaterThan(CityLife.TrackGap));
            foreach (var p in life.parked) Assert.That(clear.Margin(p.position.X, p.position.Z), Is.GreaterThan(CityLife.TrackGap));
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

        [Test]
        public void ThePalacesStandOnTheirOwnFootprints()
        {
            var (_, _, clear, _, city) = World();
            var wings = city.buildings.FindAll(b => b.classical == OsmCity.PalaceWing);
            var deps = city.buildings.FindAll(b => b.classical == OsmCity.PalaceDependency);
            var towers = city.buildings.FindAll(b => b.classical == OsmCity.ClockTower);
            TestContext.WriteLine($"{wings.Count} palace wings, {deps.Count} service buildings, {towers.Count} clock tower(s)");
            foreach (var key in new[] { "Selamlık", "Harem", "Muayede", "Çırağan" })
                Assert.That(wings.Exists(b => b.name.Contains(key)), Is.True, key + " is built from its footprint");
            Assert.That(wings.Find(b => b.name.Contains("Muayede")).height, Is.EqualTo(26f));
            Assert.That(towers.Count, Is.EqualTo(1), "Dolmabahçe clock tower");
            // 18 houses in the data; the four at the southern end touch the circuit corner and give way to the barriers.
            Assert.That(city.buildings.FindAll(b => b.classical == OsmCity.RowHouse).Count, Is.EqualTo(14), "Akaretler row houses");
            Assert.That(wings.Exists(b => b.name.Contains("Kempinski")), Is.False, "the modern hotel stays generic");
            // Service buildings ("Daire", "Köşk", "Kapı") only next to a palace.
            foreach (var d in deps)
            {
                var c = Centre(d.outline);
                Assert.That(wings.Exists(w => Vector2.Distance(Centre(w.outline), c) < 400f), Is.True, d.name);
            }
            foreach (var b in wings)
                foreach (var p in b.outline)
                    Assert.That(clear.Margin(p.X, p.Y), Is.GreaterThanOrEqualTo(OsmCity.BuildingClearance), b.name);
        }

        static Vector2 Centre(List<Vector2> o)
        {
            var c = Vector2.Zero;
            foreach (var p in o) c += p;
            return c / o.Count;
        }
    }
}
