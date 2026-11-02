using System;
using System.Collections.Generic;
using Apex.Core;
using Apex.Race;
using Apex.UI;
using Apex.Vehicle;
using Apex.Weather;
using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// Feeds the UI Toolkit race HUD (<see cref="RaceHudView"/>) from the simulation: tower, race status and flags,
    /// dash and shift lights, tyres, minimap, timing, start lights and the event banner (<see cref="RaceEventFeed"/>,
    /// <see cref="RaceEventDetector"/>). The interim IMGUI HUD keeps the radio, pit timer, telemetry, touch buttons
    /// and the layout check; without UI Toolkit it draws everything as before.
    /// </summary>
    public sealed partial class ApexGame
    {
        RaceHudView hudView;
        readonly RaceHudView.HudFrame hudFrame = new RaceHudView.HudFrame();
        readonly RaceEventFeed eventFeed = new RaceEventFeed();
        readonly RaceEventDetector eventDetector = new RaceEventDetector();
        string hudConfigKey = "";

        HudPreset CurrentHudPreset => (HudPreset)Settings.hudPreset;

        void CreateHudView()
        {
            try
            {
                hudView = new RaceHudView();
                hudView.RadioClicked += i => { if (radioOpen && Race != null) RadioAction(i); };
                hudView.EditorClosed += () =>
                {
                    // The key that closed the editor must not also trigger the pause menu.
                    navConfirm = false;
                    swallowClickFrame = Time.frameCount;
                    Settings.hudLayout.Normalize();
                    SaveService.SaveSettings(Settings);
                };
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                hudView = null;
            }
        }

        /// <summary>New session: minimap track, empty feed, fastest-lap events.</summary>
        void BindHudToSession()
        {
            if (hudView == null || Race == null) return;
            hudView.SetTrack(Race.Circuit.Definition, Race.Circuit.Geometry);
            eventFeed.Clear();
            eventDetector.Reset();
            hudConfigKey = "";
            Race.Director.FastestLapSet += (e, t) =>
                eventFeed.Post(HudEventKind.FastestLap, "SCHNELLSTE RUNDE", $"{e.code}  {ApexStyle.FormatTime(t)}", e.isPlayer ? HudTone.Best : HudTone.Neutral, Time.unscaledTime);
        }

        /// <summary>Messages from the game (penalties, radio replies, warnings) go to the event banner.</summary>
        bool PostToHud(string text, Color color, float duration, bool high)
        {
            if (hudView == null || Race == null) return false;
            var tone = color == ApexStyle.Best ? HudTone.Best : color == ApexStyle.Faster ? HudTone.Good
                : color == ApexStyle.Urgent ? HudTone.Danger : color == ApexStyle.Caution ? HudTone.Warning : HudTone.Neutral;
            eventFeed.Post(high ? HudEventKind.Notice : HudEventKind.Info, text, "", tone, Time.unscaledTime, duration);
            return true;
        }

        void CycleHudPreset()
        {
            Settings.hudPreset = (int)HudLayout.Next(CurrentHudPreset);
            SaveService.SaveSettings(Settings);
            ShowMessage("HUD: " + HudLayout.PresetName(CurrentHudPreset), Color.white, 1.2f, false);
        }

        bool InRaceState =>
            sm.Current == GameState.Grid || sm.Current == GameState.Driving || sm.Current == GameState.RadioOverlay
            || sm.Current == GameState.PitApproach || sm.Current == GameState.PitStop || sm.Current == GameState.Pause;

        void UpdateHudView()
        {
            if (hudView == null) return;
            bool on = Race != null && InRaceState;
            hudView.SetVisible(on);
            if (!on)
            {
                if (hudView.Editing) hudView.CloseEditor();
                return;
            }
            string key = $"{Settings.hudPreset}|{Settings.hudScale}|{Settings.reducedMotion}|{Screen.width}x{Screen.height}";
            if (key != hudConfigKey)
            {
                hudConfigKey = key;
                hudView.Configure(CurrentHudPreset, Settings.hudLayout, Settings.hudScale, Settings.reducedMotion);
            }
            FillHudFrame();
            hudView.SetRadio(radioOpen && Settings.controlPreset == ControlPreset.Keyboard, RadioLabels(), radioFocus);
            hudView.Tick(hudFrame, eventFeed, Time.unscaledTime, Time.unscaledDeltaTime);
        }

        readonly List<string> radioLabels = new List<string>();

        List<string> RadioLabels()
        {
            radioLabels.Clear();
            for (int i = 0; i < RadioItems.Length; i++)
                radioLabels.Add(i == 1 ? Loc.Format("Reifen: {0}", CompoundSpec.For(radioCompound).name) : Loc.T(RadioItems[i]));
            return radioLabels;
        }

        static Color SectorTokenColour(Color c)
        {
            if (c == ApexStyle.Best) return ApexUi.C(UiPalette.Current.best);
            if (c == ApexStyle.Faster) return ApexUi.C(UiPalette.Current.good);
            if (c == ApexStyle.Caution) return ApexUi.C(UiTokens.Amber);
            if (c == ApexStyle.Urgent) return ApexUi.C(UiPalette.Current.bad);
            return ApexUi.C(UiTokens.Surface);
        }

        void FillHudFrame()
        {
            var f = hudFrame;
            var d = Race.Director;
            var p = Race.Player;
            var sim = p.Sim;
            var timer = p.entry.timer;
            bool race = Race.Type == SessionType.Race;
            var st = d.Standings;

            // Tower (same rows and gaps as the interim HUD).
            if (race) f.towerHeader = d.CurrentPhase == RaceDirector.Phase.Racing || d.CurrentPhase == RaceDirector.Phase.Finished
                ? $"RUNDE {Mathf.Min(d.Leader.LapsCompleted + 1, d.rules.laps)}/{d.rules.laps}" : "START";
            else if (d.rules.sessionSeconds > 0) f.towerHeader = (Race.Type == SessionType.Qualifying ? "QUALIFYING " : "TRAINING ") + ApexStyle.FormatClock(d.SessionRemaining);
            else f.towerHeader = kind == SessionKind.TimeTrial ? "ZEITFAHREN" : "TRAINING";
            f.checkered = d.Checkered;
            f.tower.Clear();
            bool all = towerExpanded || input.TowerHeld;
            int rows = all ? st.Count : Mathf.Min(st.Count, 6);
            int pi = p.entry.position - 1;
            int first = all ? 0 : Mathf.Clamp(pi - rows / 2, 0, Mathf.Max(0, st.Count - rows));
            for (int k = 0; k < rows && first + k < st.Count; k++)
            {
                var e = st[first + k];
                var rc = Race.Cars[e.id];
                string gap;
                if (race)
                {
                    if (e.position == 1) gap = e.finished ? "ZIEL" : "Führend";
                    else
                    {
                        int laps = d.LapsBehind(e);
                        double g = towerExpanded ? d.GapToLeader(e) : d.GapToAhead(e);
                        gap = laps > 0 ? $"+{laps} Rd." : double.IsNaN(g) ? "" : $"+{g:0.0}";
                    }
                }
                else gap = e.BestLap > 0 ? (e.position == 1 ? ApexStyle.FormatTime(e.BestLap) : ApexStyle.FormatDelta(d.GapToLeader(e))) : "–";
                var team = Roster.Teams[rc.teamIndex];
                var tc = rc.Sim.tires[0].compound;
                f.tower.Add(new RaceHudView.TowerRow
                {
                    position = e.position, code = rc.code, gap = gap, compound = CompoundSpec.For(tc).shortName,
                    team = new Color(team.r, team.g, team.b), compoundColour = CompoundColour(tc), player = e.isPlayer, pit = race && rc.pit.InPitLane,
                });
            }

            // Race status, conditions, flag.
            f.position = $"P{p.entry.position}";
            f.lap = (race ? $"RUNDE {Mathf.Min(p.entry.LapsCompleted + 1, d.rules.laps)}/{d.rules.laps}" : $"RUNDE {Mathf.Max(1, timer.LapNumber)}") + $"  ·  {d.entries.Count} AUTOS";
            var w = Race.Weather;
            f.conditions = $"{WeatherModel.Name(w.State)} · Strecke {w.TrackTemp:0}°C" + (w.TrackWetness > 0.05f ? $" · nass {w.TrackWetness * 100f:0} %" : "");
            f.wet = w.TrackWetness > 0.25f;
            f.flag = null;
            var ctl = Race.Control;
            if (ctl != null && race)
            {
                switch (ctl.Status)
                {
                    case TrackStatus.Yellow: if (ctl.IsYellowAt(p.raw)) { f.flag = "GELB · NICHT ÜBERHOLEN"; f.flagColour = ApexUi.C(UiTokens.FlagYellow); } break;
                    case TrackStatus.VirtualSafetyCar: f.flag = "VSC · TEMPO BEGRENZT"; f.flagColour = ApexUi.C(UiTokens.FlagYellow); break;
                    case TrackStatus.VirtualSafetyCarEnding: f.flag = "VSC ENDET"; f.flagColour = ApexUi.C(UiTokens.FlagGreen); break;
                    case TrackStatus.SafetyCar: f.flag = "SAFETY CAR"; f.flagColour = ApexUi.C(UiTokens.Amber); break;
                    case TrackStatus.SafetyCarIn: f.flag = "SC IN DIESER RUNDE"; f.flagColour = ApexUi.C(UiTokens.Amber); break;
                }
            }

            // Dash.
            int gear = sim.gearbox.gear;
            f.gear = gear < 0 ? "R" : gear == 0 ? "N" : gear.ToString();
            f.speed = Mathf.RoundToInt(Mathf.Abs(sim.ForwardSpeed) * (Settings.speedInMph ? 2.23694f : 3.6f));
            f.unit = Settings.speedInMph ? "MPH" : "KM/H";
            float limiter = sim.engine.limiterRpm;
            f.rpmFraction = Mathf.InverseLerp(4000f, limiter, sim.EngineRpm);
            f.shiftLit = ShiftLights.Lit(sim.EngineRpm, limiter);
            f.shiftFlash = ShiftLights.Flash(sim.EngineRpm, limiter) && gear > 0;
            // Shown from 4 % less drag: about 30 m behind at speed.
            f.slipstream = sim.wake.tow > 0.04f ? Apex.UI.Loc.T("WINDSCHATTEN") : "";
            f.assists = ((sim.TcActive ? "TC  " : "") + (sim.AbsActive ? "ABS  " : "") + (Mathf.Abs(sim.CounterSteer) > 0.01f ? "STAB" : "")).Trim();
            f.pitNote = !float.IsInfinity(sim.speedLimit) ? "PIT LIMIT"
                : p.pit.stage == PitState.Stage.Requested ? "BOX · " + CompoundSpec.For(p.pit.nextCompound).name.ToUpperInvariant() : "";

            // Tyres.
            var tires = sim.tires;
            f.compound = CompoundSpec.For(tires[0].compound).shortName;
            f.compoundColour = CompoundColour(tires[0].compound);
            for (int i = 0; i < 4; i++)
            {
                var t = tires[i];
                f.tyreWear[i] = t.wear;
                f.tyreTemp[i] = t.temperature;
                f.tyreBand[i] = TyreTemps.Band(t.temperature, t.Spec.optimalTemp, t.Spec.window);
            }

            // Timing.
            f.lapTime = ApexStyle.FormatTime(timer.CurrentLapTime());
            f.lapValid = timer.LapValid || !timer.LapActive;
            f.delta = timer.LiveDelta(Circuit != null ? Circuit.LapDistance(p.raw) : 0f);
            f.last = timer.LastLapTime > 0 ? ApexStyle.FormatTime(timer.LastLapTime) : "–";
            f.best = timer.BestLapTime > 0 ? ApexStyle.FormatTime(timer.BestLapTime) : "–";
            f.record = sessionRecord > 0 ? ApexStyle.FormatTime(sessionRecord) : "–";
            for (int i = 0; i < 3; i++)
            {
                f.sectors[i] = sectorText[i];
                f.sectorColours[i] = SectorTokenColour(sectorColor[i]);
            }

            // Start lights, minimap, notes.
            f.lights = d.CurrentPhase == RaceDirector.Phase.Lights;
            f.lightsOn = d.LightsOn;
            f.cars.Clear();
            foreach (var rc in Race.Cars)
            {
                var pos = rc.car.transform.position;
                var team = Roster.Teams[rc.teamIndex];
                f.cars.Add((pos.x, pos.z, new Color(team.r, team.g, team.b), rc.isPlayer));
            }
            f.legacy = p.car.setup.legacy;

            // Events from state changes.
            eventDetector.Update(new RaceSnapshot
            {
                race = race,
                racing = d.CurrentPhase == RaceDirector.Phase.Racing,
                position = p.entry.position,
                fieldSize = d.entries.Count,
                lap = Mathf.Min(p.entry.LapsCompleted + 1, d.rules.laps),
                laps = race ? d.rules.laps : 0,
                finished = p.entry.finished,
                status = ctl != null && race ? ctl.Status : TrackStatus.Green,
                yellowHere = ctl != null && race && ctl.IsYellowAt(p.raw),
            }, Time.unscaledTime, eventFeed);
        }
    }
}
