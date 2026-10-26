using System;
using System.Collections.Generic;
using Apex.Environment;
using Apex.Tracks;
using Apex.Weather;
using NUnit.Framework;

namespace Apex.Tests
{
    public class EnvironmentTests
    {
        [Test]
        public void TurkishFlagGeometry()
        {
            // Hoist and fly are red, the crescent ring and the star are white.
            Assert.That(TurkishFlagDesign.IsWhite(0.05f, 0f), Is.False);
            Assert.That(TurkishFlagDesign.IsWhite(1.45f, 0.4f), Is.False);
            Assert.That(TurkishFlagDesign.IsWhite(0.27f, 0f), Is.True, "left limb of the crescent");
            Assert.That(TurkishFlagDesign.IsWhite(0.6f, 0f), Is.False, "inside the inner circle");
            Assert.That(TurkishFlagDesign.IsWhite(TurkishFlagDesign.StarCx, 0f), Is.True, "star centre");
            // Crescent tips at x = 0.71125 G; the star circle touches that line.
            Assert.That(TurkishFlagDesign.TipX, Is.EqualTo(0.71125f).Within(1e-4f));
            Assert.That(TurkishFlagDesign.StarCx, Is.EqualTo(0.83625f).Within(1e-4f));
            // One star point towards the hoist: white just inside it, red just above it.
            Assert.That(TurkishFlagDesign.IsWhite(TurkishFlagDesign.TipX + 0.01f, 0f), Is.True);
            Assert.That(TurkishFlagDesign.IsWhite(TurkishFlagDesign.TipX + 0.01f, 0.03f), Is.False);
            var px = TurkishFlagDesign.Rasterize(64);
            Assert.That(px.Length, Is.EqualTo(96 * 64 * 4));
            // Red share: crescent ~0.071 G² + star ~0.018 G² white of 1.5 G² => ~94 % red.
            int red = 0;
            for (int i = 0; i < px.Length; i += 4) if (px[i + 1] < 128) red++;
            Assert.That(red / (float)(96 * 64), Is.InRange(0.93f, 0.95f));
        }
    
        static (CircuitDefinition def, TrackGeometry g, TerrainField field) Bosphorus()
        {
            var def = TestData.Circuit("bosphorus_gp");
            var g = TrackGeometry.FromDefinition(def);
            return (def, g, new TerrainField(g, def));
        }

        [Test]
        public void LandMassesMatchTheGeography()
        {
            var (def, g, field) = Bosphorus();
            var land = field.land;
            Assert.That(land.HasLand, Is.True);
            // Track and European landmarks on land, Asian landmarks on the Asian mass,
            // the Maiden's Tower islet and the middle of the bridge over water.
            for (int i = 0; i < g.Count; i += 10)
                Assert.That(land.MassAt(g.Positions[i].X, g.Positions[i].Z), Is.EqualTo("europe"), $"track sample {i}");
            CircuitDefinition.Landmark L(string id) => def.FindLandmark(id);
            Assert.That(land.MassAt(L("galata").east, L("galata").north), Is.EqualTo("europe"));
            Assert.That(land.MassAt(L("sultan_ahmed").east, L("sultan_ahmed").north), Is.EqualTo("peninsula"));
            Assert.That(land.MassAt(L("beylerbeyi").east + 150f, L("beylerbeyi").north), Is.EqualTo("asia"));
            Assert.That(land.IsLand(L("maidens_tower").east, L("maidens_tower").north), Is.False);
            var eu = L("bridge_eu"); var asia = L("bridge_as");
            Assert.That(land.IsLand((eu.east + asia.east) / 2f, (eu.north + asia.north) / 2f), Is.False);
            // Golden Horn separates Galata from the historic peninsula.
            Assert.That(land.IsLand((L("karakoy_pier").east + L("eminonu_pier").east) / 2f, (L("karakoy_pier").north + L("eminonu_pier").north) / 2f), Is.False);
            // Hills: the Asian ridge is higher than its shore, the seabed below the water.
            Assert.That(land.Height(2900f, 500f), Is.GreaterThan(land.Height(1840f, 500f) + 40f));
            Assert.That(field.Height(900f, 500f), Is.LessThan(-3f));
        }

        [Test]
        public void TerrainStaysBelowTheRoadAndMatchesTheFarGrid()
        {
            var (def, g, field) = Bosphorus();
            for (int i = 0; i < g.Count; i += 7)
            {
                var p = g.Positions[i];
                Assert.That(field.Height(p.X, p.Z), Is.LessThan(p.Y - 0.5f), $"terrain through the road at {i}");
            }
            // On the border of the near area the near terrain equals the far landscape.
            for (float x = field.MinX; x <= field.MaxX; x += 96f)
                Assert.That(field.Height(x, field.MinZ), Is.EqualTo(field.land.Height(x, field.MinZ)).Within(0.05f));
            Assert.That((field.MaxX - field.MinX) % TerrainField.FarCell, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void CityKeepsClearOfTrackWaterAndLandmarks()
        {
            var (def, g, field) = Bosphorus();
            var opts = new CityLayout.Options { quality = 0 };
            opts.reserved.Add(new CityLayout.Reserved(-60f, 250f, 80f));
            var city = new CityLayout(field, def, opts);
            var again = new CityLayout(field, def, new CityLayout.Options { quality = 0, reserved = { new CityLayout.Reserved(-60f, 250f, 80f) } });
            Assert.That(city.lots.Count, Is.EqualTo(again.lots.Count), "deterministic");
            Assert.That(city.lots.Count, Is.InRange(1500, 12000));
            int near = 0, onAsia = 0, onPeninsula = 0;
            foreach (var lot in city.lots)
            {
                Assert.That(field.IsWater(lot.x, lot.z), Is.False);
                if (lot.band == 0)
                {
                    near++;
                    float td = field.TrackDistance(lot.x, lot.z, 80f, out _);
                    Assert.That(td, Is.GreaterThan(7.5f + opts.trackClearance), "building in the track corridor");
                }
                string m = field.land.MassAt(lot.x, lot.z);
                if (m == "asia") onAsia++;
                if (m == "peninsula") onPeninsula++;
                float dx = lot.x + 60f, dz = lot.z - 250f;
                Assert.That(dx * dx + dz * dz, Is.GreaterThan(80f * 80f), "reserved area");
                var ortakoy = def.FindLandmark("ortakoy_mosque");
                float mx = lot.x - ortakoy.east, mz = lot.z - ortakoy.north;
                Assert.That(mx * mx + mz * mz, Is.GreaterThan(55f * 55f), "mosque forecourt");
                Assert.That(lot.height, Is.InRange(5f, 220f));
            }
            Assert.That(near, Is.GreaterThan(800));
            // Signature views stay open: nothing right in front of a viewpoint, only low blocks further out.
            Assert.That(def.sightlines.Count, Is.GreaterThanOrEqualTo(3));
            foreach (var s in def.sightlines)
            {
                float a = s.bearing * (float)Math.PI / 180f;
                foreach (var lot in city.lots)
                {
                    if (lot.band != 0) continue;
                    float dx = lot.x - s.east, dz = lot.z - s.north;
                    float along = dx * (float)Math.Sin(a) + dz * (float)Math.Cos(a);
                    float lateral = Math.Abs(dx * (float)Math.Cos(a) - dz * (float)Math.Sin(a));
                    if (along < 0f || along > 700f || lateral > 30f + 0.18f * along) continue;
                    Assert.That(along, Is.GreaterThanOrEqualTo(120f), s.label);
                    Assert.That(lot.floors, Is.LessThanOrEqualTo(2), s.label);
                }
            }
            Assert.That(onAsia, Is.GreaterThan(200), "Asian shore skyline");
            Assert.That(onPeninsula, Is.GreaterThan(30), "historic peninsula");
            Assert.That(city.trees.Count, Is.GreaterThan(100));
        }

        [Test]
        public void FlagsDroopInCalmAndStreamInWind()
        {
            float L = 3f, H = 2f;
            var calm = FlagCloth.Evaluate(1f, 0.5f, L, H, 0f, 1.3f, 0f);
            var windy = FlagCloth.Evaluate(1f, 0.5f, L, H, 11f, 1.3f, 0f);
            Assert.That(calm.Y, Is.LessThan(-1f - 0.8f * L), "hangs down");
            Assert.That(calm.X, Is.LessThan(0.3f * L));
            Assert.That(windy.X, Is.GreaterThan(0.85f * L), "streams downwind");
            Assert.That(Math.Abs(windy.Y + 1f), Is.LessThan(0.4f * L));
            // The hoist edge stays on the pole at any wind.
            for (float t = 0f; t < 3f; t += 0.37f)
            {
                var hoist = FlagCloth.Evaluate(0f, 0.3f, L, H, 8f, t, 1f);
                Assert.That(Math.Abs(hoist.X) + Math.Abs(hoist.Z), Is.LessThan(1e-4f));
                Assert.That(hoist.Y, Is.EqualTo(-0.6f).Within(1e-4f));
            }
            // Same wind direction for everyone, rotated with true north (poyraz from NNE).
            var wind = WindField.ForCircuit(308f);
            Assert.That(wind.towardsDeg, Is.EqualTo((25f + 180f + 308f) % 360f).Within(1e-3f));
            for (double t = 0; t < 600; t += 13) Assert.That(wind.Speed(t), Is.InRange(2f, 14f));
        }

        [Test]
        public void FerriesAndShipsStayOnTheWater()
        {
            var (def, g, field) = Bosphorus();
            var routes = MarineRoute.ForCircuit(def, field.land);
            Assert.That(routes.Count, Is.GreaterThanOrEqualTo(4));
            Assert.That(routes.Exists(r => r.name.StartsWith("Beşiktaş")), Is.True);
            foreach (var r in routes)
            {
                for (double t = 0; t < r.CycleTime; t += 2.0)
                {
                    r.Sample(t, 0f, out var p, out _, out _);
                    Assert.That(field.land.IsLand(p.X, p.Y), Is.False, $"{r.name} on land at t={t}");
                }
                if (r.shuttle)
                {
                    // Docked at both ends for the dwell time.
                    r.Sample(r.Length / r.speed + r.dwell * 0.5f, 0f, out _, out _, out float v);
                    Assert.That(v, Is.EqualTo(0f));
                }
            }
        }

        [Test]
        public void GoldenHourSunIsInTheWest()
        {
            var golden = SunModel.Get(TimeOfDay.GoldenHour);
            Assert.That(golden.azimuthTrue, Is.InRange(235f, 275f));
            Assert.That(golden.elevation, Is.InRange(5f, 15f));
            Assert.That(SunModel.Get(TimeOfDay.Night).artificial, Is.EqualTo(1f));
            Assert.That(SunModel.Get(TimeOfDay.Day).artificial, Is.EqualTo(0f));
            // Game frame: true north at 308° => the western sun sits at 200° (towards -z/-x).
            Assert.That(SunModel.GameAzimuth(golden, 308f), Is.EqualTo((252f + 308f) % 360f).Within(1e-3f));
            var street = AmbienceMixer.Evaluate(400f, 600f, 0.9f, 20f);
            var quay = AmbienceMixer.Evaluate(20f, 60f, 0.1f, 2f);
            Assert.That(street.reverb, Is.GreaterThan(0.8f));
            Assert.That(quay.reverb, Is.EqualTo(0f));
            Assert.That(quay.water, Is.GreaterThan(street.water));
            Assert.That(quay.crowd, Is.GreaterThan(0.5f));
        }
    
        [Test]
        public void BosphorusGrandPrixGeographyAndCity()
        {
            var def = TestData.Circuit("bosphorus_a");
            var g = TrackGeometry.FromDefinition(def);
            var field = new TerrainField(g, def);
            var land = field.land;
            Assert.That(land.HasLand && land.HasHeightGrid, Is.True);
            Assert.That(def.trueNorthBearing, Is.EqualTo(0f), "unrotated frame: true north = +z");
            for (int i = 0; i < g.Count; i += 10)
            {
                var p = g.Positions[i];
                Assert.That(land.MassAt(p.X, p.Z), Is.EqualTo("europe"), $"track on the European side at {g.Distances[i]:F0} m");
                Assert.That(field.Height(p.X, p.Z), Is.LessThan(p.Y - 0.5f), "terrain below the road");
            }
            CircuitDefinition.Landmark L(string id) => def.FindLandmark(id);
            Assert.That(land.IsLand(L("maidens_tower").east, L("maidens_tower").north), Is.False, "Maiden's Tower islet in the water");
            Assert.That(land.MassAt(L("bridge_as").east + 150f, L("bridge_as").north), Is.EqualTo("asia"));
            Assert.That(land.IsLand((L("bridge_eu").east + L("bridge_as").east) / 2f, (L("bridge_eu").north + L("bridge_as").north) / 2f), Is.False);
            Assert.That(land.MassAt(L("sultan_ahmed").east, L("sultan_ahmed").north), Is.EqualTo("europe"), "peninsula joins Europe beyond the Golden Horn");
            Assert.That(land.Height(L("galata").east, L("galata").north), Is.InRange(25f, 80f), "Galata hill from the DEM");
            var city = new CityLayout(field, def, new CityLayout.Options { quality = 0 });
            Assert.That(city.lots.Count, Is.GreaterThan(1500));
            foreach (var lot in city.lots)
            {
                Assert.That(field.IsWater(lot.x, lot.z), Is.False);
                if (lot.band == 0)
                    Assert.That(field.TrackDistance(lot.x, lot.z, 80f, out _), Is.GreaterThan(7.5f + 26f));
            }
            var routes = MarineRoute.ForCircuit(def, land);
            Assert.That(routes.Count, Is.GreaterThanOrEqualTo(4));
            foreach (var r in routes)
                for (double t = 0; t < r.CycleTime; t += 3.0)
                {
                    r.Sample(t, 0f, out var p, out _, out _);
                    Assert.That(land.IsLand(p.X, p.Y), Is.False, $"{r.name} on land at t={t} ({p.X:F0}, {p.Y:F0})");
                }
        }
    }
}
