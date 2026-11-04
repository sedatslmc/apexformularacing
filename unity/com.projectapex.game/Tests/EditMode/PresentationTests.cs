using System;
using System.Linq;
using System.Numerics;
using Apex.Core;
using Apex.UI;
using NUnit.Framework;

namespace Apex.Tests
{
    /// <summary>Race Control 2.0: loading progress, cinematic scripts and camera poses, race preparation steps, menu.</summary>
    public class PresentationTests
    {
        [Test]
        public void LoadingProgressComesOnlyFromCompletedStages()
        {
            var m = new LoadingModel(LoadContext.Race, LoadingModel.SessionStages(true, true, true));
            Assert.That(m.Progress, Is.EqualTo(0f));
            Assert.That(m.Current, Is.EqualTo(m.stages[0].label));
            float prev = 0f;
            foreach (var st in m.stages.ToList())
            {
                m.Complete(st.id);
                Assert.That(m.Progress, Is.GreaterThan(prev), st.id);
                prev = m.Progress;
            }
            Assert.That(m.Done, Is.True);
            Assert.That(m.Current, Is.EqualTo(""));
            // The city is the biggest single stage; without it the stage does not exist.
            var noCity = LoadingModel.SessionStages(false, true, false);
            Assert.That(noCity.Any(s => s.id == "environment"), Is.False);
            m = new LoadingModel(LoadContext.Race, LoadingModel.SessionStages(true, false, false));
            m.Complete("unknown");
            Assert.That(m.Progress, Is.EqualTo(0f), "unknown ids change nothing");
        }

        [Test]
        public void TipsRotateEverySixSeconds()
        {
            Assert.That(LoadingModel.TipAt(0f, 2), Is.EqualTo(LoadingModel.TipAt(5.9f, 2)));
            Assert.That(LoadingModel.TipAt(6.1f, 2), Is.Not.EqualTo(LoadingModel.TipAt(0f, 2)));
            Assert.That(LoadingModel.Title(LoadContext.Race, "Bosphorus Grand Prix"), Is.EqualTo("BOSPHORUS GRAND PRIX"));
            Assert.That(LoadingModel.Subtitle(LoadContext.Championship, "ISTANBUL", 2), Does.Contain("02"));
        }

        [Test]
        public void IntroAndRaceStartAreShortAndEndOnTheirCards()
        {
            var intro = CinematicScripts.GameIntro(true);
            Assert.That(CinematicScripts.Duration(intro), Is.InRange(12f, 22f), "high quality but not long");
            Assert.That(intro.Last().title, Is.EqualTo("APEX"));
            Assert.That(intro.Last().subtitle, Is.EqualTo(TitleScreen.Tagline));
            Assert.That(intro.Any(s => s.subject == ShotSubject.Bridge) && intro.Any(s => s.subject == ShotSubject.CityFlags), Is.True);
            Assert.That(CinematicScripts.GameIntro(false).Any(s => s.subject == ShotSubject.Bridge), Is.False, "no city: car shots only");

            var race = CinematicScripts.RaceIntro(true, "Bosphorus Grand Prix", 1, true, 6, 20);
            Assert.That(CinematicScripts.Duration(race), Is.InRange(10f, 20f));
            Assert.That(race.Any(s => s.title == "BOSPHORUS GRAND PRIX" && s.subtitle == "ROUND 01"), Is.True);
            Assert.That(race.Last().subject, Is.EqualTo(ShotSubject.Grid));
            Assert.That(race.Last().gridSlot, Is.EqualTo(6), "ends on the player's car");
            var tt = CinematicScripts.RaceIntro(false, "Bosphorus Grand Prix", 0, true, 0, 1);
            Assert.That(CinematicScripts.Duration(tt), Is.LessThan(6f), "time trial / practice: short");
            Assert.That(CinematicScripts.At(race, 1000f).index, Is.EqualTo(race.Count));
        }

        [Test]
        public void ShotPosesAreFiniteAndLookAtTheSubject()
        {
            var subject = new Vector3(100f, 5f, -40f);
            var fwd = new Vector3(0.6f, 0f, 0.8f);
            foreach (var shots in new[] { CinematicScripts.GameIntro(true), CinematicScripts.RaceIntro(true, "X", 1, true, 3, 10) })
                foreach (var s in shots)
                    for (float t = 0f; t <= s.duration; t += s.duration / 8f)
                    {
                        var (eye, target) = CinematicScripts.Pose(s, subject, fwd, t);
                        Assert.That(float.IsFinite(eye.X) && float.IsFinite(eye.Y) && float.IsFinite(eye.Z), Is.True, s.subject.ToString());
                        Assert.That(Vector3.Distance(eye, target), Is.GreaterThan(0.3f), $"{s.subject}: camera not inside its target");
                        Assert.That(eye.Y, Is.GreaterThan(subject.Y), $"{s.subject}: above the ground of the subject");
                    }
            // Moves ease: no jump between neighbouring frames.
            var orbit = CinematicScripts.GameIntro(false)[0];
            var a = CinematicScripts.Pose(orbit, subject, fwd, 1.0f).eye;
            var b = CinematicScripts.Pose(orbit, subject, fwd, 1.0f + 1f / 60f).eye;
            Assert.That(Vector3.Distance(a, b), Is.LessThan(0.2f));
        }

        [Test]
        public void TheRacePreparationSortsEveryOptionIntoAStep()
        {
            var c = new SessionConfig();
            var all = SetupModel.Options(SetupMode.GrandPrix, c, new[] { "bosphorus_a", "test_loop" }, id => id);
            int total = 0;
            foreach (var st in RaceSetupFlow.Steps) total += RaceSetupFlow.OptionsFor(st, all).Count;
            Assert.That(total, Is.EqualTo(all.Count), "no option lost, none twice");
            Assert.That(RaceSetupFlow.OptionsFor(SetupStep.Track, all).Single().key, Is.EqualTo("track"));
            Assert.That(RaceSetupFlow.OptionsFor(SetupStep.Car, all).Select(o => o.key), Does.Contain("team").And.Contain("compound"));
            foreach (var o in all) Assert.That(RaceSetupFlow.Explain(o.key), Is.Not.Empty, o.key);
            var summary = RaceSetupFlow.Summary(SetupMode.GrandPrix, c, "Bosphorus Grand Prix", "Istanbul", "Apex A01");
            Assert.That(summary.Select(r => r.label), Does.Contain("Runden").And.Contain("Gegner").And.Contain("Wetter").And.Contain("Tageszeit").And.Contain("Fahrzeug"));
            Assert.That(RaceSetupFlow.Summary(SetupMode.TimeTrial, c, "X", "", "A01").Any(r => r.label == "Wetter"), Is.False);
        }

        [Test]
        public void TheMainMenuHasSettingsDescriptionsAndNoCareerYet()
        {
            var entries = MainMenuModel.Build(new MenuContext { trackName = "X", laps = 5 });
            Assert.That(entries.Any(e => e.action == MenuAction.Settings), Is.True);
            Assert.That(entries.Any(e => e.action == MenuAction.Career), Is.False, "not shown until it works");
            foreach (var e in entries) Assert.That(e.description, Is.Not.Empty, e.action.ToString());
            var withCareer = MainMenuModel.Build(new MenuContext { trackName = "X", laps = 5, careerAvailable = true });
            Assert.That(withCareer[1].action, Is.EqualTo(MenuAction.Career), "prepared: right below the race entry");
        }
    
        [Test]
        public void HudElementsCanBeSwitchedOffOnTopOfThePreset()
        {
            try
            {
                HudLayout.UserHidden = 1 << (int)HudElement.Map;
                Assert.That(HudLayout.Shows(HudPreset.Standard, HudElement.Map), Is.False);
                Assert.That(HudLayout.Shows(HudPreset.Standard, HudElement.Tyres), Is.True);
            }
            finally { HudLayout.UserHidden = 0; }
        }

        [Test]
        public void TeamStandingsSumTheDriversPointsAndProfileTimesAreReadable()
        {
            var pts = new int[Roster.Drivers.Length + 1];
            pts[0] = 25; pts[1] = 18;
            var teams = ChampionshipModel.TeamStandings(pts, 3);
            int total = 0; foreach (var (_, p) in teams) total += p;
            Assert.That(total, Is.EqualTo(43));
            Assert.That(teams.Count, Is.EqualTo(Roster.Teams.Length));
            Assert.That(Apex.Career.CareerData.FormatDrivingTime(3725), Is.EqualTo("1:02 h"));
            Assert.That(Apex.Career.CareerData.FormatDrivingTime(300), Is.EqualTo("5 min"));
            var c = new Apex.Career.CareerData();
            c.RecordDrive(100, true); c.RecordDrive(50, false);
            c.RecordChampionship(2, 61, 2);
            Assert.That(c.fastestLaps, Is.EqualTo(1));
            Assert.That(c.drivingSeconds, Is.EqualTo(150));
            Assert.That(c.championshipResults, Is.EqualTo(new[] { "2|61|2" }));
        }

        [Test]
        public void TheGarageCockpitViewLeavesTheOrbit()
        {
            var o = new GarageOrbit();
            o.Select(GarageView.Cockpit);
            Assert.That(o.Cockpit, Is.True);
            o.Drag(10f, 0f);
            Assert.That(o.Cockpit, Is.False, "dragging returns to the free orbit");
            o.Select(GarageView.Side);
            Assert.That(o.Cockpit, Is.False);
        }
    }
}
