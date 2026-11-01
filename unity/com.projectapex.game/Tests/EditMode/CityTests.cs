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
        public void SpectatorZonesStandInGroupsAtTheBrakingCorners()
        {
            var (def, field, clear, data, city) = World();
            var g = TrackGeometry.FromDefinition(def);
            var setup = new VehicleSetup();
            var line = new RacingLine(g, 1.45f, setup.downforceArea * setup.airDensity * 0.5f / setup.mass);
            var plan = new TrackInfrastructurePlan(g, g.CornersFor(def), line.offsets, line.speed);
            var centre = city.OrtakoyCentre.Value;
            Assert.That(SpectatorZones.Centres(g, plan, centre, StreetFronts.OrtakoyRadius, 0), Is.Empty, "no zones on mobile");
            var zones = SpectatorZones.Centres(g, plan, centre, StreetFronts.OrtakoyRadius, 2);
            TestContext.WriteLine($"{plan.viewingSpots.Count} viewing spots, {zones.Count} zones at quality 2");
            Assert.That(zones.Count, Is.InRange(3, SpectatorZones.MaxZones(2)));
            for (int i = 0; i < zones.Count; i++)
            {
                Assert.That(Vector2.Distance(zones[i], centre), Is.GreaterThan(StreetFronts.OrtakoyRadius + SpectatorZones.Radius - 0.01f), "zone inside the Ortaköy crowd");
                for (int j = i + 1; j < zones.Count; j++)
                    Assert.That(Vector2.Distance(zones[i], zones[j]), Is.GreaterThanOrEqualTo(2f * SpectatorZones.Radius - 0.01f), "zones overlap");
            }
            int even = 0, grouped = 0, photographers = 0;
            foreach (var z in zones)
            {
                even += new TracksideCrowd(g, clear, field, city, null, z, SpectatorZones.Radius, 2).fans.Count;
                var crowd = new TracksideCrowd(g, clear, field, city, null, z, SpectatorZones.Radius, 2, null, SpectatorZones.GroupScale);
                grouped += crowd.fans.Count;
                foreach (var f in crowd.fans)
                {
                    if (f.photographer) photographers++;
                    Assert.That(clear.Margin(f.position.X, f.position.Z), Is.InRange(TracksideCrowd.MinBehind - 0.01f, TracksideCrowd.MaxBehind + 0.01f));
                    Assert.That(field.IsWater(f.position.X, f.position.Z), Is.False);
                    Assert.That(city.InsideBuilding(f.position.X, f.position.Z, 0.3f), Is.False, "fan inside a house");
                }
            }
            TestContext.WriteLine($"zone fans: {grouped} in groups (evenly spread: {even}), {photographers} photographers");
            Assert.That(grouped, Is.GreaterThan(100), "spectators at the braking zones");
            Assert.That(grouped, Is.LessThan(even), "groups leave gaps along the barrier");
            Assert.That(photographers, Is.GreaterThan(0));
            Assert.That(photographers, Is.LessThan(grouped * 0.1), "photographers are a few among the fans");
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
            TestContext.WriteLine($"{life.connectedEnds} connected street ends, {life.junctionTurns} turns at junctions in two minutes");
            Assert.That(life.connectedEnds, Is.GreaterThan(100), "street network with junctions");
            Assert.That(life.junctionTurns, Is.GreaterThan(life.vehicles.Count / 2), "vehicles turn into other streets instead of only turning round");
            TestContext.WriteLine($"{life.signals.Count} traffic lights, up to {life.maxWaiting} vehicles waiting at red");
            Assert.That(life.signals.Count, Is.GreaterThan(20), "signalised junctions on the main streets");
            Assert.That(life.maxWaiting, Is.GreaterThan(0), "traffic stops at red lights");
            foreach (var sg in life.signals)
                Assert.That(clear.Margin(sg.position.X, sg.position.Z), Is.GreaterThan(CityLife.TrackGap), "traffic light on the track");
            foreach (var p in life.props) Assert.That(clear.Margin(p.position.X, p.position.Z), Is.GreaterThan(CityLife.TrackGap));
            // Ortaköy's bazaar lanes: café tables and simit carts, in the quarter only and never inside a building.
            var centre = city.OrtakoyCentre.Value;
            int cafes = 0, carts = 0;
            foreach (var p in life.props)
            {
                if (p.kind != (int)CityLife.Prop.CafeSet && p.kind != (int)CityLife.Prop.SimitCart) continue;
                if (p.kind == (int)CityLife.Prop.CafeSet) cafes++; else carts++;
                Assert.That(Vector2.Distance(new Vector2(p.position.X, p.position.Z), centre), Is.LessThan(StreetFronts.OrtakoyRadius));
                Assert.That(city.InsideBuilding(p.position.X, p.position.Z, 0.8f), Is.False, "café table inside a house");
                Assert.That(clear.Margin(p.position.X, p.position.Z), Is.GreaterThan(CityLife.TrackGap + 1f));
            }
            TestContext.WriteLine($"Ortaköy: {cafes} café tables, {carts} simit carts, {life.guests.Count} guests, {life.cats.Count} cats");
            Assert.That(cafes, Is.GreaterThan(60), "café tables in the bazaar lanes");
            Assert.That(carts, Is.GreaterThan(2));
            Assert.That(life.guests.Count, Is.InRange(cafes * 0.6, cafes * 1.6), "about one guest per table");
            foreach (var gst in life.guests)
            {
                Assert.That(Vector2.Distance(new Vector2(gst.position.X, gst.position.Z), centre), Is.LessThan(StreetFronts.OrtakoyRadius + 1f));
                Assert.That(clear.Margin(gst.position.X, gst.position.Z), Is.GreaterThan(CityLife.TrackGap));
            }
            Assert.That(life.cats.Count, Is.GreaterThan(10), "Istanbul's street cats");
            foreach (var cat in life.cats)
            {
                Assert.That(city.InsideBuilding(cat.position.X, cat.position.Z, 0f), Is.False, "cat inside a house");
                Assert.That(clear.Margin(cat.position.X, cat.position.Z), Is.GreaterThan(CityLife.TrackGap));
            }
            // Standing fans behind the barriers of the Ortaköy section.
            var crowd = new TracksideCrowd(TrackGeometry.FromDefinition(TestData.Circuit("bosphorus_a")), clear, field, city, life.props, centre, StreetFronts.OrtakoyRadius, 2);
            TestContext.WriteLine($"Ortaköy trackside crowd: {crowd.fans.Count} fans");
            Assert.That(crowd.fans.Count, Is.GreaterThan(400), "spectators along Muallim Naci Cd., the square and the Palanga climb");
            foreach (var f in crowd.fans)
            {
                float m = clear.Margin(f.position.X, f.position.Z);
                Assert.That(m, Is.InRange(TracksideCrowd.MinBehind - 0.01f, TracksideCrowd.MaxBehind + 0.01f), "fans stand 1.2-8 m behind the barrier");
                Assert.That(field.IsWater(f.position.X, f.position.Z), Is.False);
                Assert.That(city.InsideBuilding(f.position.X, f.position.Z, 0.3f), Is.False, "fan inside a house");
                foreach (var p in life.props)
                    if (p.kind == (int)CityLife.Prop.CafeSet)
                        Assert.That(Vector2.Distance(new Vector2(p.position.X, p.position.Z), new Vector2(f.position.X, f.position.Z)), Is.GreaterThan(0.9f), "fan on a café table");
            }
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
            // Street fronts (balconies, bay windows, awnings): most buildings have one, and none is blocked by a neighbour.
            int withFront = 0;
            foreach (var b in city.buildings)
            {
                Assert.That(b.front, Is.Not.Null);
                Assert.That(b.front.Length, Is.EqualTo(b.outline.Count));
                bool any = false;
                for (int i = 0; i < b.front.Length; i++)
                {
                    if (!b.front[i]) continue;
                    any = true;
                    var a = b.outline[i]; var c = b.outline[(i + 1) % b.outline.Count];
                    var e = c - a;
                    var outward = new Vector2(e.Y, -e.X) / e.Length();
                    var q = (a + c) * 0.5f + outward * 1.5f;
                    Assert.That(city.InsideBuilding(q.X, q.Y, 0f), Is.False, "street front faces another building");
                }
                if (any) withFront++;
            }
            TestContext.WriteLine($"{withFront} buildings with a street front");
            Assert.That(withFront, Is.GreaterThan(city.buildings.Count * 2 / 5));
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
            var gates = city.buildings.FindAll(b => b.classical == OsmCity.Gate);
            Assert.That(gates.Count, Is.EqualTo(2), "Saltanat and Hazine gates");
            Assert.That(gates.Exists(b => b.name.Contains("Saltanat")) && gates.Exists(b => b.name.Contains("Hazine")), Is.True);
            Assert.That(deps.Exists(b => b.name.Contains("Koltuk Kapısı")), Is.True, "a square gate house stays a service building");
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
            // Ortaköy: the Esma Sultan Yalısı is its own model (brick shell), 15 m; houses of worship never get shop
            // fronts (the Etz Ahayim Synagogue beside the circuit carried shop signs while every amenity counted as a shop).
            var esma = city.buildings.FindAll(b => b.classical == OsmCity.BrickShell);
            Assert.That(esma.Count, Is.EqualTo(1));
            Assert.That(esma[0].name, Does.Contain("Esma Sultan"));
            Assert.That(esma[0].height, Is.EqualTo(15f));
            var synagogue = city.buildings.Find(b => b.name != null && b.name.Contains("Sinagog"));
            Assert.That(synagogue.outline, Is.Not.Null, "the Etz Ahayim Synagogue stands");
            Assert.That(synagogue.worship && !synagogue.shop && synagogue.historic, Is.True);
            foreach (var b in city.buildings) if (b.worship) Assert.That(b.shop, Is.False, b.name);
        }

        /// <summary>
        /// The land/water edge is the OSM coastline (tools/geo/osm_land.py), not the DEM 0 m contour that ran up to
        /// 450 m out into the Bosphorus: the waterfront landmarks stand at the water, quays follow the coast, and the
        /// promenade never reaches the circuit.
        /// </summary>
        [Test]
        public void TheWaterfrontLandmarksStandAtTheRealCoast()
        {
            var (def, field, clear, _, city) = World();
            var land = field.land;
            foreach (var (id, maxCoast) in new[] { ("ortakoy_mosque", 40f), ("ciragan", 60f) })
            {
                var l = def.FindLandmark(id);
                float d = land.CoastDistance(l.east, l.north);
                TestContext.WriteLine($"{id}: {d:F0} m from the coast");
                Assert.That(land.IsLand(l.east, l.north), Is.True, id + " on land");
                Assert.That(d, Is.LessThan(maxCoast), id + " at the waterfront");
            }
            // Dolmabahçe: the palace wings stand within 100 m of the quay.
            foreach (var b in city.buildings.FindAll(x => x.name.Contains("Selamlık") || x.name.Contains("Harem")))
            {
                var c = Centre(b.outline);
                Assert.That(land.CoastDistance(c.X, c.Y), Is.LessThan(110f), b.name);
            }
            var wf = new Waterfront(field, clear, (x, z) => city.InsideBuilding(x, z, 0f));
            TestContext.WriteLine($"{wf.quayLength / 1000f:F1} km quay, {wf.promenadeLength / 1000f:F1} km promenade, {wf.bollards.Count} bollards, {wf.lamps.Count} lamps");
            Assert.That(wf.quayLength, Is.GreaterThan(4000f));
            Assert.That(wf.promenadeLength, Is.GreaterThan(1500f));
            foreach (var sg in wf.segments)
            {
                Assert.That(sg.top, Is.InRange(Waterfront.MinTop, Waterfront.MaxTop));
                if (!sg.promenade) continue;
                var q = (sg.a + sg.b) * 0.5f + sg.inland * Waterfront.PromenadeWidth;
                Assert.That(clear.Margin(q.X, q.Y), Is.GreaterThan(1.9f), "promenade into the circuit");
                Assert.That(land.IsLand(q.X, q.Y), Is.True, "promenade over the water");
            }
            // Nothing planted on the water.
            foreach (var t in city.trees) Assert.That(field.IsWater(t.x, t.z), Is.False, $"tree on the water at {t.x:F0},{t.z:F0}");
        }

        [Test]
        public void TheBosphorusShoreIsAQuayNotACliff()
        {
            // docs/BOSPHORUS_COAST_HEIGHTS.md: the surface model put 10-56 m of ground at the water's edge (84 % of the
            // shore above 4 m), and the Dolmabahçe wings stood 27-35 m above the Bosphorus on a cliff.
            var (_, field, _, data, city) = World();
            const float c = 6f;
            int coast = 0, high = 0;
            float worst = 0f;
            for (float z = field.MinZ + c; z < field.MaxZ - c; z += c)
                for (float x = field.MinX + c; x < field.MaxX - c; x += c)
                {
                    if (field.IsWater(x, z)) continue;
                    if (!(field.IsWater(x + c, z) || field.IsWater(x - c, z) || field.IsWater(x, z + c) || field.IsWater(x, z - c))) continue;
                    coast++;
                    float h = field.Height(x, z);
                    worst = MathF.Max(worst, h);
                    if (h > CoastalProfile.QuayLevel + 2.5f) high++;
                }
            TestContext.WriteLine($"{coast} shore cells, highest {worst:F1} m, {high} above quay + 2.5 m");
            Assert.That(coast, Is.GreaterThan(500));
            Assert.That(high, Is.LessThanOrEqualTo(coast / 100), "the shore is quay level, not a cliff");
            // The waterfront palaces and the mosque stand on the coastal flat: low and nearly level.
            foreach (var b in data.buildings)
            {
                if (b.name == null || !(b.name.StartsWith("Dolmabahçe Sarayı Selamlık") || b.name.StartsWith("Dolmabahçe Sarayı Harem") || b.name.StartsWith("Dolmabahçe Sarayı Muayede")
                    || b.name == "Çırağan Sarayı" || b.name == "Büyük Mecidiye Camii" || b.name == "Esma Sultan Yalısı")) continue;
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (var p in b.outline) { float h = field.Height(p.X, p.Y); lo = MathF.Min(lo, h); hi = MathF.Max(hi, h); }
                TestContext.WriteLine($"{b.name}: ground {lo:F1}..{hi:F1} m");
                Assert.That(lo, Is.InRange(1f, 4f), b.name + " at quay level");
                Assert.That(hi, Is.LessThan(8f), b.name + " not on a hillside");
            }
            // Quays now match the ground behind them: no exposed earth above the wall.
            var wf = new Waterfront(field, World().clear, (x, z) => city.InsideBuilding(x, z, 0f));
            int steps = 0;
            foreach (var sg in wf.segments)
            {
                var m = (sg.a + sg.b) * 0.5f;
                var behind = m + sg.inland * 12f;
                if (field.Height(behind.X, behind.Y) - sg.top > 3f) steps++;
            }
            TestContext.WriteLine($"{steps} of {wf.segments.Count} quay pieces with more than 3 m of ground above the wall 12 m behind");
            Assert.That(steps, Is.LessThanOrEqualTo(wf.segments.Count / 50));
            // The profile itself.
            Assert.That(CoastalProfile.Bound(0f), Is.EqualTo(CoastalProfile.QuayLevel));
            Assert.That(CoastalProfile.Ramp(4f, 80f, 10f), Is.EqualTo(CoastalProfile.QuayLevel), "level promenade");
            Assert.That(CoastalProfile.Ramp(200f, 30f, 10f), Is.EqualTo(9f).Within(0.01f), "meets the street band");
            Assert.That(CoastalProfile.Limit(30f, 50f, 100f, 8f, 140f), Is.LessThan(8f), "seaward of the coastal road: coastal flat");
            Assert.That(CoastalProfile.Limit(30f, 200f, 100f, 8f, 50f), Is.EqualTo(30f), "inland of the road: the real hill");
        }

        [Test]
        public void DolmabahceHasItsPalaceQuayFiveSeaGatesAndAnOpenForecourt()
        {
            var (def, field, clear, data, city) = World();
            var front = city.PalaceFront;
            Assert.That(front.HasPalace, Is.True);
            var wf = new Waterfront(field, clear, (x, z) => city.InsideBuilding(x, z, 0f), front.OnFrontage);
            front.PlaceGates(wf);
            TestContext.WriteLine($"palace quay {wf.palaceLength:F0} m, gates {front.gates.Count}");
            // Walled palace quays: Dolmabahçe (the real one ~600 m incl. the gardens) and Çırağan with its hotel wing.
            float dolma = 0f, ciragan = 0f;
            foreach (var sg in wf.segments)
            {
                if (!sg.palace) continue;
                var m = (sg.a + sg.b) * 0.5f;
                if (m.X < 600f) dolma += Vector2.Distance(sg.a, sg.b); else ciragan += Vector2.Distance(sg.a, sg.b);
            }
            TestContext.WriteLine($"Dolmabahçe quay {dolma:F0} m, Çırağan quay {ciragan:F0} m");
            Assert.That(dolma, Is.InRange(250f, 700f));
            Assert.That(ciragan, Is.InRange(200f, 700f));
            foreach (var sg in wf.segments)
                if (sg.palace) Assert.That(sg.promenade, Is.False, "no public promenade on the palace quay");
            foreach (var l in wf.lamps) Assert.That(front.OnFrontage(l.foot.X, l.foot.Z), Is.False, "no promenade lamps on the palace quay");
            // Five sea gates; the main one at the OSM node on the ceremonial hall's axis.
            Assert.That(front.gates.Count, Is.EqualTo(5));
            var main = front.gates.FindAll(g => g.main);
            Assert.That(main.Count, Is.EqualTo(1));
            Assert.That(Vector2.Distance(main[0].position, PalaceWaterfront.MainSeaGate), Is.LessThan(25f), "main gate at the OSM sea gate");
            for (int i = 0; i < front.gates.Count; i++)
            {
                var g = front.gates[i];
                TestContext.WriteLine($"{g.name} at {g.position.X:F0},{g.position.Y:F0} {(g.surveyed ? "(OSM)" : "(approx.)")}");
                Assert.That(field.land.IsLand(g.position.X, g.position.Y), Is.True, "gate on the quay");
                var sea = g.position + g.seaward * 6f;
                Assert.That(field.IsWater(sea.X, sea.Y), Is.True, "gate faces the water");
                for (int j = i + 1; j < front.gates.Count; j++)
                    Assert.That(Vector2.Distance(g.position, front.gates[j].position), Is.GreaterThan(PalaceWaterfront.MinGateSpacing - 0.1f));
            }
            Assert.That(wf.openings.Count, Is.EqualTo(5));
            // The railing is cut at the gates.
            var cut = new Waterfront.Segment { a = new Vector2(-10f, 0f), b = new Vector2(10f, 0f) };
            var parts = Waterfront.OpenParts(cut, new List<(Vector2, float)> { (new Vector2(0f, 1f), 3f) });
            Assert.That(parts.Count, Is.EqualTo(2));
            Assert.That(parts[0].t1, Is.EqualTo(0.35f).Within(1e-4f));
            // The forecourt between the facade and the water is open: no trees there.
            int blocked = 0;
            foreach (var t in city.trees)
                if (front.InForecourt(t.x, t.z, field.land.CoastDistance(t.x, t.z))) blocked++;
            Assert.That(blocked, Is.EqualTo(0));
        }

        [Test]
        public void FacadesAlongTheCircuitAreFestiveWithFlagsOfDifferentSizes()
        {
            var (_, _, _, _, city) = World();
            int race = 0, raceFlags = 0, other = 0, otherFlags = 0;
            foreach (var b in city.buildings)
            {
                if (b.front == null || b.worship || b.classical != OsmCity.NotClassical) continue;
                int seed = StreetFronts.Hash((int)(b.outline[0].X * 7.1f), (int)(b.outline[0].Y * 3.3f));
                for (int i = 0; i < b.front.Length; i++)
                {
                    if (!b.front[i] || b.ortakoy) continue;
                    bool flag = StreetFronts.FlagFront(seed, i, false, b.raceFront[i]);
                    if (b.raceFront[i]) { race++; if (flag) raceFlags++; }
                    else { other++; if (flag) otherFlags++; }
                }
            }
            TestContext.WriteLine($"race fronts {race} ({100f * raceFlags / race:F0} % with flags), other fronts {other} ({100f * otherFlags / other:F0} %)");
            Assert.That(race, Is.GreaterThan(100), "fronts that look onto the circuit");
            Assert.That(raceFlags / (float)race, Is.InRange(0.3f, 0.5f));
            Assert.That(otherFlags / (float)other, Is.InRange(0.05f, 0.15f));
            float lo = float.MaxValue, hi = 0f;
            for (int k = 0; k < 400; k++) { float sc = StreetFronts.FlagScale(k * 7, k % 5, k % 2); lo = MathF.Min(lo, sc); hi = MathF.Max(hi, sc); }
            Assert.That(lo, Is.GreaterThanOrEqualTo(0.8f));
            Assert.That(hi, Is.LessThanOrEqualTo(1.75f));
            Assert.That(hi - lo, Is.GreaterThan(0.6f), "flags of different sizes");
        }

        [Test]
        public void TvTowersStandClearOfTreesAndBuildings()
        {
            var def = TestData.Circuit("bosphorus_a");
            var bed = new CircuitTestBed(def, false);
            var field = new TerrainField(bed.g, def);
            var towers = new List<CityLayout.Reserved>();
            var spots = new List<Vector2>();
            foreach (var t in bed.plan.tvTowers)
            {
                var p = bed.plan.TvTowerPosition(t, bed.surface.wallLeft, bed.surface.wallRight);
                spots.Add(p);
                towers.Add(new CityLayout.Reserved(p.X, p.Y, TrackInfrastructurePlan.TvTowerClearance));
            }
            var city = new OsmCity(TestData.City("bosphorus_a"), field, new TrackClearance(bed.surface), def, towers, 1);
            foreach (var p in spots)
            {
                foreach (var tr in city.trees)
                    Assert.That(Vector2.Distance(new Vector2(tr.x, tr.z), p), Is.GreaterThan(TrackInfrastructurePlan.TvTowerClearance - 0.01f), "tree in a TV tower");
                Assert.That(city.InsideBuilding(p.X, p.Y, 0f), Is.False, "TV tower inside a building");
                Assert.That(field.IsWater(p.X, p.Y), Is.False, "TV tower in the water");
            }
        }

        [Test]
        public void TheCityIsBuiltFromSeveralBuildingFamilies()
        {
            var (_, _, _, _, city) = World();
            var count = new Dictionary<BuildingFamilies.Family, int>();
            int generic = 0;
            foreach (var b in city.buildings)
            {
                if (b.classical != OsmCity.NotClassical) continue;
                generic++;
                count.TryGetValue(b.family, out int c);
                count[b.family] = c + 1;
                if (b.family == BuildingFamilies.Family.OttomanTimber)
                {
                    Assert.That(b.height, Is.LessThanOrEqualTo(11f), "timber houses are low");
                    Assert.That(b.style, Is.InRange(BuildingFamilies.TimberStyle0, BuildingFamilies.TimberStyle0 + BuildingFamilies.TimberStyles - 1), "painted timber colours");
                }
                if (b.family == BuildingFamilies.Family.Apartment1970s) Assert.That(b.roof, Is.EqualTo(0), "post-war blocks have flat roofs");
                if (b.roof == 1) Assert.That(b.outline.Count, Is.EqualTo(4));
                Assert.That(b.style, Is.InRange(0, BuildingFamilies.FacadeStyles - 1));
            }
            foreach (BuildingFamilies.Family f in System.Enum.GetValues(typeof(BuildingFamilies.Family)))
            {
                count.TryGetValue(f, out int c);
                TestContext.WriteLine($"{f}: {c} ({100f * c / generic:F0} %)");
                Assert.That(c, Is.GreaterThan(generic / 100), $"{f} present");
            }
            // The post-war apartment blocks make up most of the district; no family dominates completely.
            Assert.That(count[BuildingFamilies.Family.Apartment1970s], Is.GreaterThan(generic / 3));
            foreach (var kv in count) Assert.That(kv.Value, Is.LessThan(generic * 0.8f), $"{kv.Key} does not dominate");
            Assert.That(BuildingFamilies.Classify("office", 30f, 900f, false, false, 1), Is.Not.EqualTo(BuildingFamilies.Family.OttomanTimber));
            Assert.That(BuildingFamilies.Classify("house", 8f, 120f, false, false, 7), Is.EqualTo(BuildingFamilies.Family.OttomanTimber));
            Assert.That(BuildingFamilies.Classify("yes", 18f, 400f, true, false, 3), Is.EqualTo(BuildingFamilies.Family.Masonry19th));
        }

        static Vector2 Centre(List<Vector2> o)
        {
            var c = Vector2.Zero;
            foreach (var p in o) c += p;
            return c / o.Count;
        }
    }
}
