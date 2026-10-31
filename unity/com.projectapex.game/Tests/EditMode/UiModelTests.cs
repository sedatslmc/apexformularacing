using System.Collections.Generic;
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
    }
}
