using System;
using System.Numerics;
using Apex.AI;
using Apex.Environment;
using Apex.Tracks;
using Apex.Vehicle;
using NUnit.Framework;

namespace Apex.Tests
{
    /// <summary>
    /// Nothing the environment places may stand on the circuit: street lamps, pavements, signs,
    /// flags, buildings and trees are checked against the keep-out envelope of every track part
    /// (between the barrier lines, incl. run-off and pit lane) on the real bosphorus_a layout.
    /// </summary>
    public class PlacementValidationTests
    {
        static (CircuitDefinition def, TrackGeometry g, RoadSurface surface, TrackClearance clear, TerrainField field) world;

        static (CircuitDefinition def, TrackGeometry g, RoadSurface surface, TrackClearance clear, TerrainField field) World()
        {
            if (world.g != null) return world;
            var def = TestData.Circuit("bosphorus_a");
            var g = TrackGeometry.FromDefinition(def);
            var setup = new VehicleSetup();
            var line = new RacingLine(g, 1.45f, setup.downforceArea * setup.airDensity * 0.5f / setup.mass);
            var plan = new TrackInfrastructurePlan(g, g.CornersFor(def), line.offsets, line.speed);
            var surface = new RoadSurface(g, def, plan.kerbs.left, plan.kerbs.right, plan.kerbs.height);
            world = (def, g, surface, new TrackClearance(surface), new TerrainField(g, def));
            return world;
        }

        [Test]
        public void TheTerrainBesideTheTrackIsNoLongerAGreyCanyon()
        {
            // Audit of 2026-10-26: 63 % of the side profiles had a "wall" (> 4 m rise, > 30° over 6 m) within
            // 80 m of the barrier, up to 59 m high. The hillside envelope (TerrainField.PlateauWidth,
            // MaxHillGradient) brought that to ~6 %; the rest are places where two parts of the circuit run
            // side by side at different heights (real retaining walls, drawn as masonry by Apex/Terrain).
            var (_, g, surface, _, field) = World();
            int profiles = 0, walls = 0;
            for (float d = 0f; d < g.Length; d += 20f)
            {
                int i = g.IndexAt(d);
                foreach (int side in new[] { -1, 1 })
                {
                    float wall = side < 0 ? surface.wallLeft[i] : surface.wallRight[i];
                    var p0 = g.Positions[i];
                    float rise = 0f, steep = 0f, prev = float.NaN;
                    for (float o = 1f; o <= 80f; o += 6f)
                    {
                        var q = p0 + g.Rights[i] * (side * (wall + o));
                        if (field.IsWater(q.X, q.Z)) break;
                        float h = field.Height(q.X, q.Z) - p0.Y;
                        rise = MathF.Max(rise, h);
                        if (!float.IsNaN(prev)) steep = MathF.Max(steep, MathF.Atan2(MathF.Abs(h - prev), 6f) * 180f / MathF.PI);
                        prev = h;
                    }
                    profiles++;
                    if (rise > 4f && steep > 30f) walls++;
                }
            }
            TestContext.WriteLine($"{walls} of {profiles} side profiles with a wall");
            Assert.That(walls / (float)profiles, Is.LessThan(0.1f));
        }

        [Test]
        public void ClearanceEnvelopeCoversRoadRunoffAndPitLane()
        {
            var (_, g, surface, clear, _) = World();
            for (int i = 0; i < g.Count; i += 25)
            {
                var c = g.Positions[i];
                Assert.That(clear.Margin(c.X, c.Z), Is.LessThan(-g.Widths[i] * 0.5f + 0.01f), $"centreline at {g.Distances[i]:F0} m");
                // Just behind the right barrier of this part: outside, unless another part is there.
                var behind = c + g.Rights[i] * (surface.wallRight[i] + 0.3f);
                Assert.That(clear.Margin(behind.X, behind.Z), Is.LessThan(0.31f));
            }
        }

        [Test]
        public void StreetLampsAndPavementsNeverStandOnTheTrack()
        {
            var (_, g, surface, clear, field) = World();
            var layout = new StreetFurnitureLayout(g, surface.wallLeft, surface.wallRight, clear, field.IsWater, 1);
            TestContext.WriteLine($"{layout.lamps.Count} lamps, {layout.rejectedLamps} removed; {layout.pavements.Count} pavement pieces, {layout.rejectedPavements} removed");
            // The old "behind my own barrier" rule put lamps onto other parts of the circuit.
            Assert.That(layout.rejectedLamps, Is.GreaterThan(0), "the check has something to do on this layout");
            foreach (var l in layout.lamps)
                Assert.That(clear.Margin(l.position.X, l.position.Z), Is.GreaterThanOrEqualTo(StreetFurnitureLayout.LampRadius + StreetFurnitureLayout.LampMargin - 1e-3f), $"lamp at {l.distance:F0} m");
            foreach (var p in layout.pavements)
                foreach (var q in new[] { p.a, p.b, p.c, p.d })
                    Assert.That(clear.Margin(q.X, q.Z), Is.GreaterThan(0f));
            Assert.That(layout.lamps.Count, Is.GreaterThan(200), "the streets stay lit");
        }

        [Test]
        public void BuildingsTreesAndEventFlagsStayOffTheTrack()
        {
            var (def, g, surface, clear, field) = World();
            var city = new CityLayout(field, def, new CityLayout.Options { quality = 0 });
            int lots = 0;
            foreach (var lot in city.lots)
            {
                if (lot.band > 0) continue;
                lots++;
                float s = MathF.Sin(lot.yaw), c = MathF.Cos(lot.yaw);
                foreach (var (u, v) in new[] { (-0.5f, -0.5f), (0.5f, -0.5f), (0.5f, 0.5f), (-0.5f, 0.5f) })
                {
                    float x = lot.x + c * u * lot.w + s * v * lot.d, z = lot.z - s * u * lot.w + c * v * lot.d;
                    Assert.That(clear.Margin(x, z), Is.GreaterThan(1f), $"building at {lot.x:F0},{lot.z:F0}");
                }
            }
            foreach (var t in city.trees)
                Assert.That(clear.Margin(t.x, t.z), Is.GreaterThan(0.5f), $"tree at {t.x:F0},{t.z:F0}");
            var ev = new EventLayout(g, def, field, surface.wallLeft, surface.wallRight, RoadSurface.PitOuter);
            foreach (var (pos, _, _) in ev.flags)
                Assert.That(clear.Margin(pos.X, pos.Z), Is.GreaterThan(0.3f), $"event flag at {pos.X:F0},{pos.Z:F0}");
            TestContext.WriteLine($"{lots} near buildings, {city.trees.Count} trees, {ev.flags.Count} event flags checked");
        }

        [Test]
        public void MonumentalFlagStandsOnTheYildizSlopeClearOfTheTrackAndIsSeenFromTheLap()
        {
            var (def, g, _, clear, field) = World();
            Assert.That(def.monumentalFlag.HasValue, "bosphorus_a has a monumental flag site");
            var (x, z) = def.monumentalFlag.Value;
            Assert.That(MonumentalFlagSite.Validate(field, g, clear, x, z, out float share, out float margin), "on land and clear of every track part");
            TestContext.WriteLine($"site {x:F0},{z:F0}: {margin:F0} m from the nearest barrier line, mid-flag visible from {share:P0} of the lap");
            Assert.That(margin, Is.GreaterThan(MonumentalFlagSite.FlagLength + MonumentalFlagSite.TrackMargin), "the flag's sweep never reaches the circuit");
            Assert.That(share, Is.GreaterThan(0.4f), "a signature view from much of the lap");
            var park = def.FindLandmark("yildiz_park");
            Assert.That(MathF.Sqrt((x - park.east) * (x - park.east) + (z - park.north) * (z - park.north)), Is.LessThan(MonumentalFlagSite.SearchRadius + 1f), "on the Yıldız hills");
        }

        [Test]
        public void RooftopFlagsSitOnFlatRoofsNearTheTrackAndAreSpacedOut()
        {
            var (def, g, _, clear, field) = World();
            var city = new CityLayout(field, def, new CityLayout.Options { quality = 1 });
            var layout = new RooftopFlagLayout(city, field, def, g, 1);
            TestContext.WriteLine($"{layout.flags.Count} rooftop flags");
            Assert.That(layout.flags.Count, Is.InRange(20, 32));
            for (int a = 0; a < layout.flags.Count; a++)
            {
                var p = layout.flags[a].position;
                Assert.That(clear.Margin(p.X, p.Z), Is.GreaterThan(3f), "on a building, not over the circuit");
                Assert.That(field.TrackDistance(p.X, p.Z, RooftopFlagLayout.MaxTrackDistance + 1f, out _), Is.LessThan(RooftopFlagLayout.MaxTrackDistance + 0.5f));
                Assert.That(city.lots.Exists(l => l.band == 0 && l.roof != 1 && MathF.Abs(l.ground + l.height - p.Y) < 0.01f
                    && MathF.Sqrt((l.x - p.X) * (l.x - p.X) + (l.z - p.Z) * (l.z - p.Z)) < 0.5f * MathF.Sqrt(l.w * l.w + l.d * l.d)), "on top of a flat roof or tower");
                for (int b = a + 1; b < layout.flags.Count; b++)
                    Assert.That(Vector2.Distance(new Vector2(p.X, p.Z), new Vector2(layout.flags[b].position.X, layout.flags[b].position.Z)), Is.GreaterThanOrEqualTo(RooftopFlagLayout.MinSpacing));
            }
        }

        [Test]
        public void RoadKerbAndRunoffUvsAreMetricSoScannedTexturesKeepTheirSize()
        {
            var (_, g, surface, _, _) = World();
            foreach (var sm in new[] { surface.road, surface.kerbs, surface.runoff })
            {
                int checkedQuads = 0;
                for (int t = 0; t + 6 <= sm.triangles[0].Count && checkedQuads < 200; t += 6, checkedQuads++)
                {
                    // Each ribbon quad: vertices i (a, b) and j (a, b); UV distance = world distance / UvMetres.
                    int s0 = sm.triangles[0][t];
                    var a = sm.vertices[s0]; var b = sm.vertices[s0 + 1]; var aj = sm.vertices[s0 + 2];
                    var ua = sm.uvs[s0]; var ub = sm.uvs[s0 + 1]; var uaj = sm.uvs[s0 + 2];
                    // Planar mapping: the UVs follow the horizontal width (the run-off edge drops 12 cm).
                    float across = new Vector2(a.X - b.X, a.Z - b.Z).Length(), uvAcross = MathF.Abs(ub.X - ua.X);
                    Assert.That(uvAcross * RoadSurface.UvMetres, Is.EqualTo(across).Within(0.02f + across * 0.02f), sm.name + " across");
                    Assert.That(MathF.Abs(uaj.Y - ua.Y) * RoadSurface.UvMetres, Is.EqualTo(g.Spacing).Within(0.01f), sm.name + " along");
                }
                Assert.That(checkedQuads, Is.GreaterThan(0), sm.name);
            }
        }
    }
}
