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
        [Test]
        public void HudPresetsAndDefaultLayoutKeepTheRoadClear()
        {
            Assert.That(HudLayout.Shows(HudPreset.Minimal, HudElement.Dash) && HudLayout.Shows(HudPreset.Minimal, HudElement.Info), Is.True);
            Assert.That(HudLayout.Shows(HudPreset.Minimal, HudElement.Tower) || HudLayout.Shows(HudPreset.Minimal, HudElement.Map), Is.False);
            foreach (var e in HudLayout.All)
            {
                Assert.That(HudLayout.Shows(HudPreset.Standard, e), Is.True);
                Assert.That(HudLayout.Shows(HudPreset.Off, e), Is.False);
            }
            Assert.That(HudLayout.ShowsTelemetry(HudPreset.Extended) && !HudLayout.ShowsTelemetry(HudPreset.Standard), Is.True);
            Assert.That(HudLayout.ShowsSafetyInfo(HudPreset.Off), Is.True, "start lights and flags stay");
            Assert.That(HudLayout.Next(HudPreset.Off), Is.EqualTo(HudPreset.Minimal));
            // Default layout: no element covers the road or another element, on 16:9, 21:9 and 16:10.
            foreach (var w in new[] { 1920f, 2520f, 1728f })
                Assert.That(HudLayout.Conflicts(new HudLayoutData(), HudPreset.Standard, w, 1080f), Is.Empty, $"width {w}");
            // Right-anchored elements follow the right edge on ultrawide screens.
            Assert.That(HudLayout.DefaultRect(HudElement.Timing, 2520f, 1080f).XMax, Is.EqualTo(2520f - 40f).Within(0.01f));
            Assert.That(HudLayout.DefaultRect(HudElement.Dash, 2520f, 1080f).x + 260f, Is.EqualTo(1260f).Within(0.01f), "dash centred");
        }

        [Test]
        public void HudEditorMovesScalesAndStaysOnScreen()
        {
            var data = new HudLayoutData();
            var ed = new HudEditor(data);
            ed.SelectElement(HudElement.Timing);
            Assert.That(ed.Current, Is.EqualTo(HudElement.Timing));
            ed.Nudge(-100f, -50f, 1920f, 1080f);
            var r = HudLayout.Rect(HudElement.Timing, data.Get(HudElement.Timing), 1920f, 1080f);
            var d = HudLayout.DefaultRect(HudElement.Timing, 1920f, 1080f);
            Assert.That(r.x, Is.EqualTo(d.x - 100f).Within(0.01f));
            Assert.That(r.y, Is.EqualTo(d.y - 50f).Within(0.01f));
            // Pushing past the edge clamps, and the stored offset does not drift beyond it.
            ed.Nudge(5000f, 0f, 1920f, 1080f);
            Assert.That(HudLayout.Rect(HudElement.Timing, data.Get(HudElement.Timing), 1920f, 1080f).XMax, Is.EqualTo(1920f).Within(0.01f));
            ed.Nudge(-10f, 0f, 1920f, 1080f);
            Assert.That(HudLayout.Rect(HudElement.Timing, data.Get(HudElement.Timing), 1920f, 1080f).XMax, Is.EqualTo(1910f).Within(0.01f));
            for (int i = 0; i < 20; i++) ed.Resize(0.1f);
            Assert.That(data.Get(HudElement.Timing).scale, Is.EqualTo(HudLayoutData.MaxScale));
            for (int i = 0; i < 20; i++) ed.Fade(-0.1f);
            Assert.That(data.Get(HudElement.Timing).opacity, Is.EqualTo(HudLayoutData.MinOpacity));
            ed.Reset();
            Assert.That(data.Get(HudElement.Timing).scale, Is.EqualTo(1f));
            Assert.That(HudLayout.Rect(HudElement.Timing, data.Get(HudElement.Timing), 1920f, 1080f).x, Is.EqualTo(d.x).Within(0.01f));
            // Moving the dash into the middle of the screen is reported.
            ed.SelectElement(HudElement.Dash);
            ed.Nudge(0f, -400f, 1920f, 1080f);
            Assert.That(HudLayout.Conflicts(data, HudPreset.Standard, 1920f, 1080f).Exists(c => c.Contains("Fahrbahn")), Is.True);
            ed.Select(1);
            Assert.That(ed.Current, Is.EqualTo(HudElement.Tyres));
            ed.Select(-2);
            Assert.That(ed.Current, Is.EqualTo(HudElement.Events));
            // Save files with junk are repaired.
            var junk = new HudLayoutData { elements = new[] { null, new HudElementLayout { element = 99 }, new HudElementLayout { element = 1, scale = 9f, opacity = 0f }, new HudElementLayout { element = 1 } } };
            Assert.That(junk.Normalize(), Is.True);
            Assert.That(junk.elements.Length, Is.EqualTo(1));
            Assert.That(junk.elements[0].scale, Is.EqualTo(HudLayoutData.MaxScale));
            var settings = new SettingsData { hudLayout = null, hudPreset = 7 };
            settings.MigrateAndValidate();
            Assert.That(settings.hudLayout, Is.Not.Null);
            Assert.That(settings.hudPreset, Is.EqualTo(3));
        }

        [Test]
        public void ShiftLightsAndTyreBands()
        {
            const float limiter = 12300f;
            Assert.That(ShiftLights.Lit(5000f, limiter), Is.EqualTo(0));
            Assert.That(ShiftLights.Lit(limiter * ShiftLights.ShiftFraction, limiter), Is.EqualTo(ShiftLights.Count));
            int prev = 0;
            for (float rpm = 8000f; rpm <= limiter; rpm += 50f) { int n = ShiftLights.Lit(rpm, limiter); Assert.That(n, Is.GreaterThanOrEqualTo(prev)); prev = n; }
            Assert.That(ShiftLights.Flash(limiter * 0.99f, limiter) && !ShiftLights.Flash(limiter * 0.9f, limiter), Is.True);
            Assert.That(ShiftLights.ColourOf(0), Is.EqualTo(ShiftLights.Colour.Green));
            Assert.That(ShiftLights.ColourOf(7), Is.EqualTo(ShiftLights.Colour.Red));
            Assert.That(ShiftLights.ColourOf(14), Is.EqualTo(ShiftLights.Colour.Blue));
            Assert.That(TyreTemps.Band(60f, 95f, 25f), Is.EqualTo(TyreBand.Cold));
            Assert.That(TyreTemps.Band(100f, 95f, 25f), Is.EqualTo(TyreBand.Optimal));
            Assert.That(TyreTemps.Band(120f, 95f, 25f), Is.EqualTo(TyreBand.Hot));
        }

        [Test]
        public void EventFeedPrioritisesDedupesAndQueues()
        {
            var f = new RaceEventFeed();
            f.Post(HudEventKind.PositionGained, "P5", "+1 Platz", HudTone.Good, 0);
            Assert.That(f.Update(0.1).title, Is.EqualTo("P5"));
            f.Post(HudEventKind.Flag, "SAFETY CAR", "", HudTone.Warning, 0.5);
            Assert.That(f.Update(0.6).title, Is.EqualTo("SAFETY CAR"), "a flag replaces a position change at once");
            Assert.That(f.Post(HudEventKind.Flag, "SAFETY CAR", "", HudTone.Warning, 1.0), Is.Null, "no repeat within seconds");
            f.Post(HudEventKind.FastestLap, "SCHNELLSTE RUNDE", "1:30.000", HudTone.Best, 1.0);
            Assert.That(f.Update(1.1).title, Is.EqualTo("SAFETY CAR"), "lower priority waits");
            Assert.That(f.Queued, Is.EqualTo(1));
            Assert.That(f.Update(4.6).title, Is.EqualTo("SCHNELLSTE RUNDE"));
            Assert.That(f.Update(7.5), Is.Not.Null, "queued message gets its full time");
            Assert.That(f.Update(7.7), Is.Null);
        }

        [Test]
        public void EventDetectorReportsFlagsPositionsFinalLapAndFinish()
        {
            var f = new RaceEventFeed();
            var d = new RaceEventDetector();
            var s = new RaceSnapshot { race = true, racing = true, position = 6, fieldSize = 12, lap = 4, laps = 5, status = TrackStatus.Green };
            double t = 0;
            d.Update(s, t, f);
            // Overtake that is undone at once: no message.
            s.position = 5; d.Update(s, t += 0.5, f);
            s.position = 6; d.Update(s, t += 0.5, f);
            d.Update(s, t += 2.0, f);
            Assert.That(f.Update(t), Is.Null);
            // A held overtake is reported once.
            s.position = 5; d.Update(s, t += 0.1, f);
            d.Update(s, t += 1.6, f);
            Assert.That(f.Update(t).title, Is.EqualTo("P5"));
            Assert.That(f.Current.detail, Is.EqualTo("+1 Platz"));
            // Safety car, then green.
            s.status = TrackStatus.SafetyCar; d.Update(s, t += 0.1, f);
            Assert.That(f.Update(t).title, Is.EqualTo("SAFETY CAR"));
            s.status = TrackStatus.Green; d.Update(s, t += 10, f);
            Assert.That(f.Update(t).title, Is.EqualTo("GRÜNE FLAGGE"));
            // A local yellow elsewhere on track is not shown; at the player's position it is.
            s.status = TrackStatus.Yellow; s.yellowHere = false; d.Update(s, t += 5, f);
            Assert.That(f.Update(t), Is.Null);
            s.yellowHere = true; d.Update(s, t += 1, f);
            Assert.That(f.Update(t).title, Is.EqualTo("GELBE FLAGGE"));
            s.status = TrackStatus.Green; s.yellowHere = false;
            // Final lap and finish.
            s.lap = 5; d.Update(s, t += 5, f);
            Assert.That(f.Update(t).title, Is.EqualTo("LETZTE RUNDE"));
            s.finished = true; s.position = 2; d.Update(s, t += 5, f);
            Assert.That(f.Update(t).title, Is.EqualTo("ZIEL · P2"));
            Assert.That(f.Current.tone, Is.EqualTo(HudTone.Best));
        }

        [Test]
        public void TrackMapProjectsWorldPoints()
        {
            var def = TestData.Circuit("test_loop");
            var g = TrackGeometry.FromDefinition(def);
            var m = TrackMap.Fit(def, g, 250f, 210f, 12f, 200);
            Assert.That(m.MapX(g.Positions[0].X), Is.EqualTo(m.x[0]).Within(0.01f));
            Assert.That(m.MapY(g.Positions[0].Z), Is.EqualTo(m.y[0]).Within(0.01f));
        }
        [Test]
        public void PauseOffersOnlySessionFunctionsAndConfirmsLeavingARace()
        {
            var race = PauseModel.Entries(true, true).ConvertAll(e => e.action);
            Assert.That(race.Contains(PauseAction.ResetCar) || race.Contains(PauseAction.Restart), Is.False, "no reset or restart in a race");
            Assert.That(race[0], Is.EqualTo(PauseAction.Resume));
            Assert.That(race[race.Count - 1], Is.EqualTo(PauseAction.Leave));
            Assert.That(PauseModel.Entries(true, true).Find(e => e.action == PauseAction.Leave).confirm, Is.True);
            Assert.That(PauseModel.Entries(false, true).Find(e => e.action == PauseAction.Leave).confirm, Is.False);
            var practice = PauseModel.Entries(false, false).ConvertAll(e => e.action);
            Assert.That(practice.Contains(PauseAction.ResetCar) && practice.Contains(PauseAction.Restart) && !practice.Contains(PauseAction.HudEditor), Is.True);
            var gate = new ConfirmGate();
            Assert.That(gate.Press(3), Is.False);
            Assert.That(gate.Armed(3), Is.True);
            Assert.That(gate.Press(1), Is.False, "another item re-arms instead of confirming");
            Assert.That(gate.Press(1), Is.True);
            Assert.That(gate.Armed(1), Is.False);
        }

        [Test]
        public void ResultsHeadlineWeekendAndPodium()
        {
            Assert.That(ResultsModel.Headline(true, false, 1, 12, true).title, Is.EqualTo("SIEG"));
            Assert.That(ResultsModel.Headline(true, false, 3, 12, true).title, Is.EqualTo("PODIUM"));
            Assert.That(ResultsModel.Headline(true, false, 7, 12, true), Is.EqualTo(("P7", "P7 von 12")));
            Assert.That(ResultsModel.Headline(true, false, 7, 12, false).title, Is.EqualTo("NICHT IM ZIEL"));
            Assert.That(ResultsModel.Headline(false, true, 1, 12, false).title, Is.EqualTo("POLE POSITION"));
            var w = ResultsModel.Weekend(true, false);
            Assert.That(w.Count, Is.EqualTo(3));
            Assert.That(w[0].done && w[1].current && !w[2].done, Is.True);
            Assert.That(ResultsModel.Weekend(false, true), Is.Empty);
            Assert.That(ResultsModel.Actions(true, false)[0].action, Is.EqualTo(ResultAction.ContinueToRace));
            Assert.That(ResultsModel.Actions(false, true).ConvertAll(a => a.action), Is.EqualTo(new List<ResultAction> { ResultAction.NextRound, ResultAction.MainMenu }));
            Assert.That(ResultsModel.ShowPodium(true, true, 2, true), Is.True);
            Assert.That(ResultsModel.ShowPodium(true, true, 4, true) || ResultsModel.ShowPodium(true, true, 1, false) || ResultsModel.ShowPodium(false, true, 1, true), Is.False);
        }

        [Test]
        public void GarageOrbitAndIntroCamera()
        {
            var o = new GarageOrbit();
            o.Select(GarageView.Rear);
            for (int i = 0; i < 200; i++) o.Update(0.02f, false);
            Assert.That(o.yaw, Is.EqualTo(205f).Within(0.5f));
            o.Drag(0f, -10000f);
            Assert.That(o.TargetPitch, Is.EqualTo(GarageOrbit.MinPitch), "never below the floor");
            o.Zoom(100f);
            Assert.That(o.TargetDistance, Is.EqualTo(GarageOrbit.MinDistance));
            o.Update(0.01f, true);
            Assert.That(o.distance, Is.EqualTo(GarageOrbit.MinDistance), "reduced motion jumps");
            o.Offset(out float x, out float y, out float z);
            Assert.That(System.Math.Sqrt(x * x + y * y + z * z), Is.EqualTo(GarageOrbit.MinDistance).Within(1e-3));
            // Shortest way round: from 350° to 10° goes forward through 0°.
            var w = new GarageOrbit { yaw = 350f };
            w.Drag(80f, 0f);   // target 35 + 20 = 55°
            w.Update(0.05f, false);
            Assert.That(w.yaw, Is.InRange(0f, 55f), "350° → 55° turns the short way, through 0°");
            Assert.That(IntroCamera.Blend(0f), Is.EqualTo(0f));
            Assert.That(IntroCamera.Blend(IntroCamera.Seconds), Is.EqualTo(1f));
            IntroCamera.Pose(0f, out float a0, out _, out _);
            IntroCamera.Pose(IntroCamera.Seconds, out float a1, out _, out _);
            Assert.That(a0, Is.GreaterThan(a1));
        }

        [Test]
        public void ColourVisionPalettesKeepMeaningfulPairsApart()
        {
            foreach (ColourVision v in System.Enum.GetValues(typeof(ColourVision)))
            {
                var p = UiPalette.For(v);
                float Diff(UiTokens.Rgb a, UiTokens.Rgb b) => UiPalette.DeltaE(UiPalette.Simulate(a, v), UiPalette.Simulate(b, v));
                Assert.That(Diff(p.good, p.bad), Is.GreaterThan(30f), $"{v}: faster/slower");
                Assert.That(Diff(p.cold, p.hot), Is.GreaterThan(30f), $"{v}: cold/hot tyres");
                Assert.That(Diff(p.optimal, p.hot), Is.GreaterThan(20f), $"{v}: optimal/hot tyres");
                Assert.That(Diff(p.sector1, p.sector2), Is.GreaterThan(20f), $"{v}: sectors 1/2");
                Assert.That(Diff(p.sector2, p.sector3), Is.GreaterThan(20f), $"{v}: sectors 2/3");
            }
            // Without the adapted palette the standard green/red pair collapses for deuteranopes.
            var std = UiPalette.For(ColourVision.Standard);
            float stdDeut = UiPalette.DeltaE(UiPalette.Simulate(std.good, ColourVision.Deuteranopia), UiPalette.Simulate(std.bad, ColourVision.Deuteranopia));
            float adapted = UiPalette.DeltaE(UiPalette.Simulate(UiPalette.For(ColourVision.Deuteranopia).good, ColourVision.Deuteranopia), UiPalette.Simulate(UiPalette.For(ColourVision.Deuteranopia).bad, ColourVision.Deuteranopia));
            Assert.That(adapted, Is.GreaterThan(stdDeut));
            Assert.That(UiPalette.Simulate(UiTokens.Ivory, ColourVision.Standard).r, Is.EqualTo(UiTokens.Ivory.r));
        }

        [Test]
        public void EnglishCoversAllModelTexts()
        {
            Loc.Missing.Clear();
            Loc.Current = Language.English;
            try
            {
                var c = new SessionConfig();
                foreach (SetupMode m in System.Enum.GetValues(typeof(SetupMode)))
                {
                    SetupModel.Title(m);
                    SetupModel.Summary(m, c);
                    foreach (var o in SetupModel.Options(m, c, Tracks, id => id)) { var _ = o.group + o.label + o.value(); }
                }
                c.format = WeekendFormat.RaceOnly; c.aiCount = 0; c.failures = 1; c.tireWear = 0f;
                SetupModel.Summary(SetupMode.GrandPrix, c);
                foreach (var o in SetupModel.Options(SetupMode.GrandPrix, c, Tracks, id => id)) o.value();
                c.failures = 2; foreach (var o in SetupModel.Options(SetupMode.GrandPrix, c, Tracks, id => id)) o.value();
                foreach (Apex.Weather.TimeOfDay t in System.Enum.GetValues(typeof(Apex.Weather.TimeOfDay))) SetupModel.TimeName(t);
                SetupModel.PointsScheme();
                MainMenuModel.Build(new MenuContext { trackName = "X", laps = 1, bestLap = 90 });
                MainMenuModel.Build(new MenuContext { trackName = "X", laps = 3, championshipActive = true, championshipRound = 1, championshipRounds = 2 });
                foreach (var e in HudLayout.All) HudLayout.Name(e);
                foreach (HudPreset p in System.Enum.GetValues(typeof(HudPreset))) HudLayout.PresetName(p);
                var data = new HudLayoutData();
                var ed = new HudEditor(data); ed.SelectElement(HudElement.Dash); ed.Nudge(0f, -500f, 1920f, 1080f);
                HudLayout.Conflicts(data, HudPreset.Standard, 1920f, 1080f);
                foreach (Apex.Race.TrackStatus s in System.Enum.GetValues(typeof(Apex.Race.TrackStatus))) { RaceEventDetector.StatusTitle(s, true); RaceEventDetector.StatusDetail(s); }
                var feed = new RaceEventFeed(); var det = new RaceEventDetector();
                var snap = new RaceSnapshot { race = true, racing = true, position = 3, fieldSize = 9, lap = 1, laps = 2, status = Apex.Race.TrackStatus.SafetyCar };
                det.Update(snap, 0, feed); snap.status = Apex.Race.TrackStatus.Green; snap.position = 2; snap.lap = 2; det.Update(snap, 1, feed); det.Update(snap, 3, feed);
                snap.position = 4; det.Update(snap, 4, feed); det.Update(snap, 6, feed); snap.finished = true; det.Update(snap, 7, feed);
                snap.finished = false; det.Reset(); det.Update(snap, 8, feed); snap.finished = true; snap.position = 1; det.Update(snap, 9, feed);
                foreach (var b in new[] { true, false }) { PauseModel.Entries(b, true); ResultsModel.Weekend(true, b); ResultsModel.Actions(b, !b); }
                foreach (var (r, q, pos, fin) in new[] { (true, false, 1, true), (true, false, 2, true), (true, false, 5, false), (false, true, 1, false), (false, true, 4, false), (false, false, 2, false) })
                    ResultsModel.Headline(r, q, pos, 10, fin);
                foreach (var cat in SettingsCatalog.All) { SettingsCatalog.Name(cat); SettingsCatalog.Description(cat); }
                foreach (GarageView v in System.Enum.GetValues(typeof(GarageView))) GarageOrbit.Name(v);
                foreach (ColourVision v in System.Enum.GetValues(typeof(ColourVision))) UiPalette.Name(v);
                var f = TrackFacts.From(TestData.Circuit("test_loop"), TestData.Geometry("test_loop"), 0); var dir = f.Direction; f.clockwise = !f.clockwise; dir = f.Direction;
                var pts = new int[Roster.Drivers.Length + 1]; ChampionshipModel.Standings(pts, 3, 0);
                Assert.That(Loc.Missing, Is.Empty, string.Join(" | ", Loc.Missing));
                Assert.That(MainMenuModel.Build(new MenuContext { trackName = "X", laps = 2 })[0].label, Is.EqualTo("Start race"));
            }
            finally { Loc.Current = Language.Deutsch; }
            Assert.That(Loc.T("Rennen starten"), Is.EqualTo("Rennen starten"));
        }
    }
}
