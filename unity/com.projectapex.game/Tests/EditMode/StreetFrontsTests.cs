using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Apex.AI;
using Apex.Environment;
using Apex.Tracks;
using Apex.Vehicle;
using NUnit.Framework;

namespace Apex.Tests
{
    /// <summary>Shop signs and vertically hung flags of the real city's street fronts (StreetFronts).</summary>
    public class StreetFrontsTests
    {
        static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

        [Test]
        public void SignCatalogIsTurkishUpperCaseAndFitsTheAtlas()
        {
            var cat = StreetFronts.Catalog;
            Assert.That(cat.Length, Is.LessThanOrEqualTo(StreetFronts.AtlasColumns * StreetFronts.AtlasRows));
            Assert.That(cat.Select(s => s.text).Distinct().Count(), Is.EqualTo(cat.Length), "no duplicate signs");
            foreach (var s in cat)
            {
                // Turkish upper case (i -> İ, ı -> I): the text must not change under tr-TR ToUpper.
                Assert.That(s.text.ToUpper(Turkish), Is.EqualTo(s.text), s.text);
                Assert.That(Regex.IsMatch(s.text, "^[A-ZÇĞİÖŞÜ &·]+$"), Is.True, $"only Turkish capitals: {s.text}");
                Assert.That(Regex.IsMatch(s.subtitle, "^[A-Za-zÇĞİÖŞÜçğıöşü ·']*$"), Is.True, $"subtitle: {s.subtitle}");
                // Board and lettering contrast (relative luminance difference).
                Assert.That(Math.Abs(Lum(s.board) - Lum(s.letters)), Is.GreaterThan(0.3f), $"contrast of {s.text}");
            }
            Assert.That(cat.Count(s => s.ortakoy), Is.GreaterThanOrEqualTo(8), "Ortaköy's own mix (kumpir, waffle, silver, souvenirs)");
        }

        static float Lum((byte r, byte g, byte b) c) => (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) / 255f;

        [Test]
        public void SignAtlasManifestMatchesTheCatalog()
        {
            string dir = Path.Combine(TestData.TracksDir(), "..", "Textures", "Signs");
            Assert.That(File.Exists(Path.Combine(dir, "ShopSigns.jpg")), Is.True, "run tools/signs/build_sign_atlas.py");
            string manifest = File.ReadAllText(Path.Combine(dir, "ShopSigns.json"));
            var texts = Regex.Match(manifest, "\"texts\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline).Groups[1].Value;
            var list = Regex.Matches(texts, "\"((?:[^\"\\\\]|\\\\.)*)\"").Select(m => Regex.Unescape(m.Groups[1].Value)).ToArray();
            Assert.That(list, Is.EqualTo(StreetFronts.Catalog.Select(s => s.text).ToArray()), "atlas out of date: re-export the catalog and rebuild the atlas");
            // Atlas cells: v up, inside [0, 1], 4:1 like the sign board.
            for (int i = 0; i < StreetFronts.Catalog.Length; i++)
            {
                var (u0, v0, u1, v1) = StreetFronts.Uv(i);
                Assert.That(u0 >= 0f && v0 >= 0f && u1 <= 1f && v1 <= 1f && u1 > u0 && v1 > v0, Is.True);
                Assert.That((u1 - u0) / (v1 - v0), Is.EqualTo(StreetFronts.BoardWidth / StreetFronts.BoardHeight).Within(0.05f));
            }
            Assert.That(StreetFronts.Uv(0).v1, Is.GreaterThan(0.99f), "sign 0 is the top-left cell");
        }

        [Test]
        public void OrtakoyShopsFollowTheQuarterAndFlagsHangThere()
        {
            int ortakoySigns = 0, n = 0, elsewhereOrtakoy = 0, shopsO = 0, shopsE = 0, flagsO = 0, flagsE = 0;
            for (int seed = 1; seed < 400; seed++)
                for (int edge = 0; edge < 4; edge++)
                {
                    if (StreetFronts.FlagFront(seed * 7919, edge, true)) flagsO++;
                    if (StreetFronts.FlagFront(seed * 7919, edge, false)) flagsE++;
                    for (int bay = 0; bay < 4; bay++)
                    {
                        n++;
                        if (StreetFronts.Catalog[StreetFronts.SignFor(seed * 7919, edge, bay, true)].ortakoy) ortakoySigns++;
                        if (StreetFronts.Catalog[StreetFronts.SignFor(seed * 7919, edge, bay, false)].ortakoy) elsewhereOrtakoy++;
                        if (StreetFronts.IsShopBay(seed * 7919, edge, bay, false, true)) shopsO++;
                        if (StreetFronts.IsShopBay(seed * 7919, edge, bay, false, false)) shopsE++;
                    }
                }
            TestContext.WriteLine($"Ortaköy signs {ortakoySigns * 100 / n} %, shop bays {shopsO * 100 / n} % (elsewhere {shopsE * 100 / n} %), flag fronts {flagsO / 16} / {flagsE / 16} %");
            Assert.That(ortakoySigns, Is.InRange(n * 0.6, n * 0.9), "kumpir, waffle, cafés and silver in Ortaköy");
            Assert.That(elsewhereOrtakoy, Is.EqualTo(0), "the square's specialities stay in Ortaköy");
            Assert.That(shopsO, Is.GreaterThan(shopsE * 2));
            Assert.That(flagsO, Is.GreaterThan(flagsE * 2));
            // Flag spots: above the ground floor, within the storeys and bays.
            for (int seed = 1; seed < 200; seed++)
                for (int floors = 2; floors < 9; floors++)
                {
                    var (bay, floor) = StreetFronts.FlagSpot(seed, 1, 0, 3, floors);
                    Assert.That(bay, Is.InRange(0, 2));
                    Assert.That(floor, Is.InRange(1, floors - 1));
                }
        }

        [Test]
        public void TheOrtakoyQuarterIsMarkedAroundTheMosque()
        {
            var def = TestData.Circuit("bosphorus_a");
            var g = TrackGeometry.FromDefinition(def);
            var setup = new VehicleSetup();
            var line = new RacingLine(g, 1.45f, setup.downforceArea * setup.airDensity * 0.5f / setup.mass);
            var plan = new TrackInfrastructurePlan(g, g.CornersFor(def), line.offsets, line.speed);
            var surface = new RoadSurface(g, def, plan.kerbs.left, plan.kerbs.right, plan.kerbs.height);
            var city = new OsmCity(TestData.City("bosphorus_a"), new TerrainField(g, def), new TrackClearance(surface), def, null, 1);
            var mosque = def.FindLandmark("ortakoy_mosque");
            int quarter = 0, fronts = 0, uphill = 0;
            foreach (var b in city.buildings)
            {
                // Each facade's ground floor starts at its own street (between the footprint's lowest corner and
                // two storeys below the roof).
                for (int i = 0; i < b.outline.Count; i++)
                {
                    Assert.That(b.level[i], Is.GreaterThanOrEqualTo(b.ground + 0.6f - 1e-3f));
                    Assert.That(b.level[i], Is.LessThanOrEqualTo(MathF.Max(b.ground + 0.6f, b.ground + b.height - 2f * CityLayout.FloorHeight) + 1e-3f));
                    if (b.ortakoy && b.front[i] && b.level[i] > b.ground + 0.6f + 1f) uphill++;
                }
                var c = System.Numerics.Vector2.Zero;
                foreach (var p in b.outline) c += p;
                c /= b.outline.Count;
                float d = MathF.Sqrt((c.X - mosque.east) * (c.X - mosque.east) + (c.Y - mosque.north) * (c.Y - mosque.north));
                if (d > StreetFronts.OrtakoyRadius + 1f) Assert.That(b.ortakoy, Is.False);
                if (b.ortakoy)
                {
                    quarter++;
                    if (b.front != null && Array.IndexOf(b.front, true) >= 0) fronts++;
                }
            }
            TestContext.WriteLine($"Ortaköy quarter: {quarter} buildings, {fronts} with street fronts, {uphill} uphill fronts above the lowest corner");
            Assert.That(uphill, Is.GreaterThan(20), "on Ortaköy's slope the uphill shop fronts start at their own street");
            Assert.That(quarter, Is.GreaterThan(80), "the bazaar lanes and Muallim Naci Cd. around the square");
            Assert.That(fronts, Is.GreaterThan(quarter / 2));
        }
    }
}
