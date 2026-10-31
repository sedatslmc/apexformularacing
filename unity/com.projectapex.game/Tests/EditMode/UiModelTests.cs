using System.Collections.Generic;
using Apex.Core;
using Apex.Race;
using Apex.Tracks;
using Apex.UI;
using NUnit.Framework;

namespace Apex.Tests
{
    /// <summary>APEX // RACE CONTROL UI models: design tokens, motion, main menu entries and navigation.</summary>
    public class UiModelTests
    {
        [Test]
        public void TokensKeepTextReadableOnThePanels()
        {
            // Panels render over dark 3D scenes; test against the panel colour composited over the navy base.
            var panel = UiTokens.Over(UiTokens.Carbon, UiTokens.PanelAlpha, UiTokens.Navy);
            var strong = UiTokens.Over(UiTokens.Carbon, UiTokens.PanelStrongAlpha, UiTokens.Navy);
            foreach (var bg in new[] { panel, strong, UiTokens.Navy, UiTokens.Surface })
            {
                Assert.That(UiTokens.Contrast(UiTokens.Ivory, bg), Is.GreaterThanOrEqualTo(7f), "primary text (AAA)");
                Assert.That(UiTokens.Contrast(UiTokens.Steel, bg), Is.GreaterThanOrEqualTo(4.5f), "labels and secondary text (AA)");
                Assert.That(UiTokens.Contrast(UiTokens.Cyan, bg), Is.GreaterThanOrEqualTo(4.5f), "cyan values and focus text");
                Assert.That(UiTokens.Contrast(UiTokens.Amber, bg), Is.GreaterThanOrEqualTo(4.5f), "warnings");
            }
            // Turkish Red is an accent (lines, large display text), not body text: at least 3:1 for large text on navy.
            Assert.That(UiTokens.Contrast(UiTokens.Red, UiTokens.Navy), Is.GreaterThanOrEqualTo(2.2f));
            Assert.That(UiTokens.Contrast(UiTokens.Ivory, UiTokens.Red), Is.GreaterThanOrEqualTo(4.5f), "white text on a red chip");
            // Spacing on the 4 px grid; motion short.
            foreach (var s in new[] { UiTokens.S1, UiTokens.S2, UiTokens.S3, UiTokens.S4, UiTokens.S5, UiTokens.S6, UiTokens.S7, UiTokens.S8, UiTokens.ScreenMargin })
                Assert.That(s % 4f, Is.EqualTo(0f));
            Assert.That(UiTokens.ScreenTime, Is.LessThanOrEqualTo(0.35f), "transitions stay short");
            Assert.That(UiTokens.FocusTime, Is.LessThan(UiTokens.PanelTime));
        }

        [Test]
        public void MotionEasesAndCanBeSkipped()
        {
            Assert.That(UiMotion.OutCubic(0f), Is.EqualTo(0f));
            Assert.That(UiMotion.OutCubic(1f), Is.EqualTo(1f));
            Assert.That(UiMotion.OutCubic(0.5f), Is.GreaterThan(0.5f), "fast start, soft landing");
            Assert.That(UiMotion.InOutCubic(0.5f), Is.EqualTo(0.5f).Within(1e-5f));
            float prev = -1f;
            for (float t = 0f; t <= 1.01f; t += 0.05f) { float v = UiMotion.OutCubic(t); Assert.That(v, Is.GreaterThanOrEqualTo(prev)); prev = v; }
            var tw = Tween.Start(0f, 100f, 0.3f);
            tw.Advance(0.1f);
            Assert.That(tw.Value, Is.InRange(1f, 99f));
            Assert.That(tw.Done, Is.False);
            tw.Skip();
            Assert.That(tw.Done, Is.True);
            Assert.That(tw.Value, Is.EqualTo(100f));
            tw.Retarget(50f, 0.2f);
            Assert.That(tw.Value, Is.EqualTo(100f), "retargeting starts from where the value is (no jump)");
            // Staggered entrance: later items start later, all end at 1.
            Assert.That(UiMotion.Staggered(0.05f, 0, 0.22f, 0.04f), Is.GreaterThan(UiMotion.Staggered(0.05f, 3, 0.22f, 0.04f)));
            Assert.That(UiMotion.Staggered(1f, 6, 0.22f, 0.04f), Is.EqualTo(1f));
            Assert.That(UiMotion.CountUp(42.0, 10f, 0.6f), Is.EqualTo(42.0));
        }

        [Test]
        public void MainMenuShowsOnlyWorkingModesWithStartRaceFirst()
        {
            var ctx = new MenuContext { trackName = "Bosphorus Grand Prix", laps = 8, timeOfDay = "Goldene Stunde", bestLap = 141.874 };
            var items = MainMenuModel.Build(ctx);
            Assert.That(items[0].action, Is.EqualTo(MenuAction.QuickRace));
            Assert.That(items[0].primary, Is.True);
            Assert.That(items.FindAll(e => e.primary).Count, Is.EqualTo(1), "one highlighted entry");
            Assert.That(items[0].detail, Does.Contain("Bosphorus Grand Prix").And.Contain("8 Runden"));
            var actions = items.ConvertAll(e => e.action);
            foreach (var a in new[] { MenuAction.GrandPrix, MenuAction.Championship, MenuAction.TimeTrial, MenuAction.Practice, MenuAction.Garage, MenuAction.Profile, MenuAction.Quit })
                Assert.That(actions.Contains(a), Is.True, a.ToString());
            Assert.That(items.Find(e => e.action == MenuAction.TimeTrial).detail, Is.EqualTo("Bestzeit 2:21.874"));
            Assert.That(actions.Contains(MenuAction.ContinueChampionship), Is.False);
            // A running championship: "continue" right below the start entry, no second "start a season".
            ctx.championshipActive = true; ctx.championshipRound = 2; ctx.championshipRounds = 2; ctx.championshipTrack = "Apex Testoval"; ctx.championshipPosition = 3;
            items = MainMenuModel.Build(ctx);
            Assert.That(items[1].action, Is.EqualTo(MenuAction.ContinueChampionship));
            Assert.That(items[1].detail, Is.EqualTo("Runde 2 / 2 · Apex Testoval · P3"));
            Assert.That(items.ConvertAll(e => e.action).Contains(MenuAction.Championship), Is.False);
            // Mobile: no quit entry; the profile only where the screen exists.
            ctx.canQuit = false; ctx.profileAvailable = false;
            actions = MainMenuModel.Build(ctx).ConvertAll(e => e.action);
            Assert.That(actions.Contains(MenuAction.Quit), Is.False);
            Assert.That(actions.Contains(MenuAction.Profile), Is.False);
            Assert.That(MainMenuModel.Build(new MenuContext { trackName = "X", laps = 1 })[0].detail, Does.Contain("1 Runde ·"));
        }

        [Test]
        public void NavigatorRemembersFocusAndGoesBack()
        {
            var nav = new UiNavigator(UiScreen.Boot);
            var changes = new List<(UiScreen, UiScreen)>();
            nav.Changed += (a, b) => changes.Add((a, b));
            nav.Reset(UiScreen.MainMenu);
            Assert.That(nav.Current, Is.EqualTo(UiScreen.MainMenu));
            Assert.That(nav.CanGoBack, Is.False);
            Assert.That(nav.MoveFocus(1, 7), Is.EqualTo(1));
            Assert.That(nav.MoveFocus(-2, 7), Is.EqualTo(6), "wraps");
            nav.SetFocus(4);
            nav.Push(UiScreen.Profile);
            Assert.That(nav.Focus(UiScreen.Profile), Is.EqualTo(0));
            nav.MoveFocus(1, 3);
            Assert.That(nav.Back(), Is.True);
            Assert.That(nav.Current, Is.EqualTo(UiScreen.MainMenu));
            Assert.That(nav.Focus(UiScreen.MainMenu), Is.EqualTo(4), "focus remembered");
            Assert.That(nav.Back(), Is.False, "root");
            nav.Push(UiScreen.MainMenu);
            Assert.That(nav.Depth, Is.EqualTo(1), "pushing the current screen does nothing");
            Assert.That(changes, Is.EqualTo(new List<(UiScreen, UiScreen)> { (UiScreen.Boot, UiScreen.MainMenu), (UiScreen.MainMenu, UiScreen.Profile), (UiScreen.Profile, UiScreen.MainMenu) }));
        }
        static readonly string[] Tracks = { "bosphorus_a", "bosphorus_a_classic", "test_loop" };

        static List<string> Keys(List<SetupOption> o) => o.ConvertAll(x => x.key);

        [Test]
        public void SetupShowsOnlyTheOptionsThatMatterForTheMode()
        {
            var c = new SessionConfig();
            var gp = Keys(SetupModel.Options(SetupMode.GrandPrix, c, Tracks, id => id));
            foreach (var k in new[] { "track", "time", "weather", "dynamicWeather", "format", "laps", "qualifying", "compound", "wear", "ai", "difficulty", "team", "falseStarts", "trackLimits", "pitStop", "safetyCar", "damage", "failures" })
                Assert.That(gp.Contains(k), Is.True, k);
            Assert.That(gp[0], Is.EqualTo("track"));
            // Race only: no qualifying length.
            c.format = WeekendFormat.RaceOnly;
            Assert.That(Keys(SetupModel.Options(SetupMode.GrandPrix, c, Tracks, id => id)).Contains("qualifying"), Is.False);
            // Time trial: alone on track, no weather, tyres, field or rules.
            var tt = Keys(SetupModel.Options(SetupMode.TimeTrial, c, Tracks, id => id));
            Assert.That(tt, Is.EqualTo(new List<string> { "track", "time", "team" }));
            var pr = Keys(SetupModel.Options(SetupMode.Practice, c, Tracks, id => id));
            Assert.That(pr.Contains("weather") && pr.Contains("compound") && !pr.Contains("ai") && !pr.Contains("format"), Is.True);
            var ch = Keys(SetupModel.Options(SetupMode.Championship, c, Tracks, id => id));
            Assert.That(ch, Is.EqualTo(new List<string> { "laps", "ai", "difficulty", "team" }));
            // Every option belongs to a group and has a value.
            foreach (var o in SetupModel.Options(SetupMode.GrandPrix, new SessionConfig(), Tracks, id => id))
                Assert.That(!string.IsNullOrEmpty(o.group) && !string.IsNullOrEmpty(o.value()), Is.True, o.key);
        }

        [Test]
        public void SetupOptionsChangeTheRealConfig()
        {
            var c = new SessionConfig { trackId = "test_loop", laps = 5, format = WeekendFormat.QualifyingAndRace };
            var o = SetupModel.Options(SetupMode.GrandPrix, c, Tracks, id => id.ToUpperInvariant());
            SetupOption Get(string k) => o.Find(x => x.key == k);
            Get("track").change(1);
            Assert.That(c.trackId, Is.EqualTo("bosphorus_a"), "wraps to the first track");
            Assert.That(Get("track").value(), Is.EqualTo("BOSPHORUS_A"));
            Get("track").change(-1);
            Assert.That(c.trackId, Is.EqualTo("test_loop"));
            Get("laps").change(1); Get("laps").change(1);
            Assert.That(c.laps, Is.EqualTo(7));
            Get("format").change(1);
            Assert.That(c.format, Is.EqualTo(WeekendFormat.RaceOnly));
            Assert.That(SetupModel.FirstSession(SetupMode.GrandPrix, c), Is.EqualTo(SessionType.Race));
            Assert.That(SetupModel.Summary(SetupMode.GrandPrix, c), Is.EqualTo("Rennen 7 Runden · 11 KI-Gegner"));
            c.format = WeekendFormat.QualifyingAndRace;
            Assert.That(SetupModel.FirstSession(SetupMode.GrandPrix, c), Is.EqualTo(SessionType.Qualifying));
            Assert.That(SetupModel.Summary(SetupMode.GrandPrix, c), Does.StartWith("Qualifying 8 min + Rennen 7 Runden"));
            Get("safetyCar").change(1);
            Assert.That(c.safetyCar, Is.False);
            Get("wear").change(-1); Get("wear").change(-1); Get("wear").change(-1); Get("wear").change(-1);
            Assert.That(c.tireWear, Is.EqualTo(0f));
            Assert.That(Get("wear").value(), Is.EqualTo("Aus"));
            Assert.That(SetupModel.FirstSession(SetupMode.TimeTrial, c), Is.EqualTo(SessionType.Practice));
            Assert.That(SetupModel.PointsScheme(), Does.StartWith("25-18-15-12-10-8-6-4-2-1"));
        }

        [Test]
        public void TrackFactsComeFromTheTrackData()
        {
            var def = TestData.Circuit("bosphorus_a");
            var g = TrackGeometry.FromDefinition(def);
            var f = TrackFacts.From(def, g, 0);
            Assert.That(f.istanbul, Is.True);
            Assert.That(f.lengthKm, Is.InRange(8.0f, 8.3f));
            Assert.That(f.corners, Is.EqualTo(16));
            Assert.That(f.clockwise, Is.False, "the Bosphorus GP runs counter-clockwise");
            Assert.That(f.cornerNames.Count, Is.EqualTo(def.corners.Count));
            Assert.That(f.elevationRange, Is.GreaterThan(1f));
            Assert.That(TrackFacts.From(TestData.Circuit("test_loop"), TestData.Geometry("test_loop"), 0).istanbul, Is.False);
        }

        [Test]
        public void TrackMapFitsNorthUpWithSectors()
        {
            var def = TestData.Circuit("bosphorus_a");
            var g = TrackGeometry.FromDefinition(def);
            var m = TrackMap.Fit(def, g, 800f, 500f, 20f, 300);
            Assert.That(m.Count, Is.InRange(150, 300));
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            int northmost = 0;
            for (int i = 0; i < m.Count; i++)
            {
                minX = System.Math.Min(minX, m.x[i]); maxX = System.Math.Max(maxX, m.x[i]);
                minY = System.Math.Min(minY, m.y[i]); maxY = System.Math.Max(maxY, m.y[i]);
                if (m.y[i] < m.y[northmost]) northmost = i;
            }
            Assert.That(minX, Is.GreaterThanOrEqualTo(19.9f)); Assert.That(maxX, Is.LessThanOrEqualTo(780.1f));
            Assert.That(minY, Is.GreaterThanOrEqualTo(19.9f)); Assert.That(maxY, Is.LessThanOrEqualTo(480.1f));
            Assert.That(System.Math.Max(maxX - minX - 760f, maxY - minY - 460f), Is.GreaterThan(-0.5f), "fills one dimension");
            // North up: the top-most map point is the northernmost track point.
            float north = float.MinValue;
            foreach (var p in g.Positions) north = System.Math.Max(north, p.Z);
            int step = (g.Count + 299) / 300;
            Assert.That(g.Positions[northmost * step].Z, Is.EqualTo(north).Within(2f * step + 1f));
            var sectors = new HashSet<int>(m.sector);
            Assert.That(sectors.Count, Is.EqualTo(def.sectorFractions.Length));
            Assert.That(m.cornerX.Length, Is.EqualTo(def.corners.Count));
            Assert.That(TrackMap.SectorAt(0.1f, new[] { 0f, 1f / 3f, 2f / 3f }), Is.EqualTo(0));
            Assert.That(TrackMap.SectorAt(0.5f, new[] { 0f, 1f / 3f, 2f / 3f }), Is.EqualTo(1));
            Assert.That(TrackMap.SectorAt(0.9f, new[] { 0f, 1f / 3f, 2f / 3f }), Is.EqualTo(2));
        }

        [Test]
        public void ChampionshipShowsCalendarAndStandingsWithThePlayer()
        {
            var cal = ChampionshipModel.Calendar(new[] { "a", "b", "c" }, 1, id => id.ToUpperInvariant());
            Assert.That(cal.ConvertAll(r => r.state), Is.EqualTo(new List<RoundState> { RoundState.Done, RoundState.Next, RoundState.Upcoming }));
            Assert.That(cal[2].number, Is.EqualTo(3));
            Assert.That(cal[2].name, Is.EqualTo("C"));
            var points = new int[Roster.Drivers.Length + 1];
            for (int i = 1; i < points.Length; i++) points[i] = 100 - i;   // every AI driver ahead of the player
            var rows = ChampionshipModel.Standings(points, 10, 2);
            Assert.That(rows.Count, Is.EqualTo(10));
            Assert.That(rows[0].points, Is.EqualTo(99));
            Assert.That(rows[9].player, Is.True, "the player is always listed");
            Assert.That(rows[9].position, Is.EqualTo(points.Length));
            Assert.That(rows[9].team, Is.EqualTo(2));
            Assert.That(ChampionshipModel.PlayerPosition(points), Is.EqualTo(points.Length));
            points[0] = 99;   // tie with the leader: the player ranks first
            Assert.That(ChampionshipModel.PlayerPosition(points), Is.EqualTo(1));
            Assert.That(ChampionshipModel.Standings(points, 10, 0)[0].player, Is.True);
        }
    }
}
