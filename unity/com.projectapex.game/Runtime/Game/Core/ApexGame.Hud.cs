using System.Collections.Generic;
using Apex.Core;
using Apex.Race;
using Apex.Vehicle;
using Apex.Weather;
using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// Broadcast-style HUD (interim IMGUI): slim timing tower (top-left), race info
    /// micro panel (top-right), gear/speed/RPM dash (bottom-centre), start lights,
    /// prioritised toasts, team radio strip, optional minimap and telemetry. All
    /// values come from the simulation. Panels register their rectangles so the
    /// layout check (F2) can report overlaps with touch controls and safe areas.
    /// </summary>
    public sealed partial class ApexGame
    {
        bool showTelemetry;
        bool towerExpanded;
        bool showMap;
        int hudPreset; // 0 full, 1 compact, 2 minimal
        bool layoutDebug;

        bool radioOpen;
        int radioFocus;
        float radioIdle;
        TireCompound radioCompound = TireCompound.Medium;
        readonly List<(string name, Rect rect)> hudZones = new List<(string, Rect)>();

        static readonly string[] RadioItems = { "Box diese Runde", "Reifen wählen", "Wetter", "Abstand", "Strategie", "Schaden", "Schließen" };

        // --- Radio ------------------------------------------------------------------------

        void OpenRadio()
        {
            radioOpen = true;
            input.radioOpen = true;
            radioFocus = 0;
            radioIdle = 0f;
            if (Race != null)
            {
                var current = Race.Player.Sim.tires[0].compound;
                if (Race.Player.pit.stage == PitState.Stage.None) radioCompound = SuggestCompound(current);
            }
        }

        void CloseRadio()
        {
            radioOpen = false;
            input.radioOpen = false;
        }

        TireCompound SuggestCompound(TireCompound current)
        {
            float wet = Race.Weather.TrackWetness;
            if (wet > 0.65f) return TireCompound.Wet;
            if (wet > 0.25f) return TireCompound.Intermediate;
            return current == TireCompound.Hard ? TireCompound.Medium : current == TireCompound.Soft ? TireCompound.Medium : TireCompound.Hard;
        }

        void UpdateRadio()
        {
            radioIdle += Time.unscaledDeltaTime;
            if (input.NavDown) { radioFocus = (radioFocus + 1) % RadioItems.Length; radioIdle = 0f; }
            if (input.NavUp) { radioFocus = (radioFocus - 1 + RadioItems.Length) % RadioItems.Length; radioIdle = 0f; }
            int choice = input.NumberPressed;
            if (input.ConfirmPressed) choice = radioFocus + 1;
            if (choice > 0) RadioAction(choice - 1);
            // Collapses after a few seconds without input (except while choosing tires).
            if (radioIdle > 6f && radioFocus != 1) CloseRadio();
        }

        void RadioAction(int item)
        {
            radioIdle = 0f;
            radioFocus = item;
            var p = Race.Player;
            var d = Race.Director;
            switch (item)
            {
                case 0:
                    Race.RequestPlayerPit(radioCompound);
                    CloseRadio();
                    break;
                case 1:
                    radioCompound = (TireCompound)(((int)radioCompound + 1) % 5);
                    ShowMessage("Funk: Reifen für den Stopp – " + CompoundSpec.For(radioCompound).name, ApexStyle.Accent, 2f, false);
                    break;
                case 2:
                {
                    var w = Race.Weather;
                    string forecast = w.Dynamic && w.Forecast.Count > 0
                        ? $"in {Mathf.Max(0f, (float)(w.Forecast[0].time - Race.SimTime)) / 60f:0} min {WeatherModel.Name(w.Forecast[0].state)}"
                        : "stabil";
                    ShowMessage($"Funk: {WeatherModel.Name(w.State)}, Strecke {w.TrackTemp:0}°C, Nässe {w.TrackWetness * 100f:0} %, Prognose {forecast}", Color.white, 4f, true);
                    CloseRadio();
                    break;
                }
                case 3:
                {
                    var st = d.Standings;
                    int i = p.entry.position - 1;
                    string ahead = i > 0 ? $"vor dir {st[i - 1].code} {d.Gap(p.entry, st[i - 1]):+0.0;-0.0} s" : "du führst";
                    string behind = i + 1 < st.Count ? $", hinter dir {st[i + 1].code} {d.Gap(st[i + 1], p.entry):0.0} s" : "";
                    ShowMessage("Funk: P" + p.entry.position + ", " + ahead + behind, Color.white, 4f, true);
                    CloseRadio();
                    break;
                }
                case 4:
                {
                    var t = p.Sim.tires;
                    float wear = p.Sim.AverageTireWear();
                    int lapsLeft = d.rules.laps - p.entry.LapsCompleted;
                    string advice = wear > 0.6f ? "Reifen am Limit – Box empfohlen" : Race.Type == SessionType.Race && d.rules.mandatoryPitStop && !p.mandatoryStopDone ? "Pflichtstopp noch offen" : "Pace halten";
                    ShowMessage($"Funk: {CompoundSpec.For(t[0].compound).name}, Verschleiß {wear * 100f:0} %, {lapsLeft} Rd. übrig – {advice}", Color.white, 4f, true);
                    CloseRadio();
                    break;
                }
                case 5:
                {
                    ShowMessage("Funk: " + DamageReport(p.Sim), Color.white, 4f, true);
                    CloseRadio();
                    break;
                }
                default:
                    CloseRadio();
                    break;
            }
        }

        static string DamageReport(VehicleSimulation sim)
        {
            if (!sim.damageEnabled) return "Schadensmodell ist aus.";
            var d = sim.damage;
            if (!d.Any) return "Keine Schäden, Auto in Ordnung.";
            var parts = new List<string>();
            if (d.frontWing > 0.02f) parts.Add($"Frontflügel {d.frontWing * 100f:0} %");
            if (d.rearWing > 0.02f) parts.Add($"Heckflügel {d.rearWing * 100f:0} %");
            if (d.floor > 0.02f) parts.Add($"Unterboden {d.floor * 100f:0} %");
            string[] names = { "VL", "VR", "HL", "HR" };
            for (int i = 0; i < 4; i++)
            {
                if (d.puncture[i]) parts.Add($"Reifenschaden {names[i]}");
                else if (Mathf.Abs(d.toe[i]) > 0.001f) parts.Add($"Spur {names[i]} {d.toe[i] * Mathf.Rad2Deg:+0.0;-0.0}°");
            }
            return string.Join(", ", parts) + (d.NeedsNewNose || d.puncture[0] || d.puncture[1] || d.puncture[2] || d.puncture[3] ? " – Box empfohlen" : "");
        }

        // --- HUD --------------------------------------------------------------------------

        void Zone(string name, Rect r)
        {
            if (layoutDebug) hudZones.Add((name, r));
        }

        void DrawHud()
        {
            if (Race == null) return;
            hudZones.Clear();
            var safe = SafeRect();
            var d = Race.Director;
            var p = Race.Player;
            // With UI Toolkit the tower, status, dash, lights, events, map and timing are RaceHudView; IMGUI keeps
            // the radio, pit timer, telemetry, touch buttons, the pause/radio buttons and the layout check.
            bool newHud = hudView != null;
            if (!newHud)
            {
                if (hudPreset < 2) DrawTower(safe, d, p);
                DrawInfoPanel(safe, d, p);
                DrawDash(safe, p);
                if (d.CurrentPhase == RaceDirector.Phase.Lights) DrawLights(safe, d);
                DrawToasts(safe);
                if (showMap && hudPreset == 0) DrawMiniMap(safe);
                if (p.car.setup.legacy)
                    ApexStyle.Text(new Rect(safe.center.x - 160f, safe.y + 8f, 320f, 26f), "ALTES FAHRVERHALTEN (VERGLEICH)", 16, ApexStyle.Caution, FontStyle.Bold, TextAnchor.MiddleCenter);
            }
            else if (!hudView.Editing) DrawHudButtons(safe);
            if (showTelemetry || (newHud && Apex.UI.HudLayout.ShowsTelemetry(CurrentHudPreset))) DrawTelemetry(safe, p);
            // Keyboard/mouse: the radio is part of the UI Toolkit HUD; touch keeps its IMGUI layout (touch buttons).
            if (radioOpen && !(hudView != null && Settings.controlPreset == ControlPreset.Keyboard)) DrawRadio(safe);
            if (p.pit.stage == PitState.Stage.Stopped) DrawPitTimer(safe, p);
            DrawTouchButtons();
            AddHudZones();
            if (layoutDebug) DrawLayoutDebug(safe);
        }

        void DrawTower(Rect safe, RaceDirector d, RaceCar p)
        {
            var st = d.Standings;
            bool race = Race.Type == SessionType.Race;
            int rows = towerExpanded || input.TowerHeld ? st.Count : Mathf.Min(st.Count, hudPreset == 1 ? 3 : 6);
            int pi = p.entry.position - 1;
            int first = towerExpanded || input.TowerHeld ? 0 : Mathf.Clamp(pi - rows / 2, 0, Mathf.Max(0, st.Count - rows));
            float rowH = 30f, w = 300f;
            float x = safe.x + 20f, y = safe.y + 20f;
            string header;
            if (race) header = d.CurrentPhase == RaceDirector.Phase.Racing || d.CurrentPhase == RaceDirector.Phase.Finished
                ? $"RUNDE {Mathf.Min(d.Leader.LapsCompleted + 1, d.rules.laps)}/{d.rules.laps}" : "START";
            else if (d.rules.sessionSeconds > 0) header = (Race.Type == SessionType.Qualifying ? "QUALI " : "TRAINING ") + ApexStyle.FormatClock(d.SessionRemaining);
            else header = kind == SessionKind.TimeTrial ? "ZEITFAHREN" : "TRAINING";
            var box = new Rect(x, y, w, rowH * (rows + 1) + 8f);
            ApexStyle.Box(box, ApexStyle.Panel);
            Zone("Tower", box);
            ApexStyle.Text(new Rect(x + 12f, y + 2f, w, rowH), header, 18, ApexStyle.Accent, FontStyle.Bold);
            if (d.Checkered) ApexStyle.Text(new Rect(x + w - 60f, y + 2f, 50f, rowH), "ZIEL", 18, Color.white, FontStyle.Bold, TextAnchor.MiddleRight);
            for (int k = 0; k < rows && first + k < st.Count; k++)
            {
                var e = st[first + k];
                var rc = Race.Cars[e.id];
                float ry = y + rowH * (k + 1) + 4f;
                if (e.isPlayer) ApexStyle.Box(new Rect(x + 2f, ry, w - 4f, rowH - 2f), new Color(0.12f, 0.28f, 0.33f, 0.9f));
                var team = Roster.Teams[rc.teamIndex];
                ApexStyle.Box(new Rect(x + 44f, ry + 5f, 4f, rowH - 12f), new Color(team.r, team.g, team.b));
                ApexStyle.Text(new Rect(x + 8f, ry, 34f, rowH), e.position.ToString(), 17, ApexStyle.Fg, FontStyle.Bold, TextAnchor.MiddleRight);
                ApexStyle.Text(new Rect(x + 56f, ry, 70f, rowH), rc.code, 17, e.isPlayer ? ApexStyle.Accent : ApexStyle.Fg, FontStyle.Bold);
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
                    if (rc.pit.InPitLane) gap = "BOX";
                }
                else
                {
                    gap = e.BestLap > 0 ? (e.position == 1 ? ApexStyle.FormatTime(e.BestLap) : ApexStyle.FormatDelta(d.GapToLeader(e))) : "–";
                }
                ApexStyle.Text(new Rect(x + 120f, ry, 120f, rowH), gap, 16, ApexStyle.TextDim, FontStyle.Normal, TextAnchor.MiddleRight);
                var tire = rc.Sim.tires[0];
                ApexStyle.Text(new Rect(x + 250f, ry, 40f, rowH), CompoundSpec.For(tire.compound).shortName, 16, CompoundColour(tire.compound), FontStyle.Bold, TextAnchor.MiddleCenter);
            }
        }

        static Color CompoundColour(TireCompound c)
        {
            switch (c)
            {
                case TireCompound.Soft: return new Color(1f, 0.3f, 0.3f);
                case TireCompound.Medium: return new Color(1f, 0.85f, 0.25f);
                case TireCompound.Hard: return new Color(0.95f, 0.95f, 0.95f);
                case TireCompound.Intermediate: return new Color(0.35f, 0.85f, 0.35f);
                default: return new Color(0.3f, 0.55f, 1f);
            }
        }

        void DrawInfoPanel(Rect safe, RaceDirector d, RaceCar p)
        {
            float w = 340f, h = 92f;
            // Leave room for the keyboard pause button / touch pause + radio column.
            float x = safe.xMax - w - 130f, y = safe.y + 20f;
            var r = new Rect(x, y, w, h);
            ApexStyle.Box(r, ApexStyle.Panel);
            Zone("Info", r);
            bool race = Race.Type == SessionType.Race;
            string pos = $"P{p.entry.position}/{d.entries.Count}";
            string lap = race ? $"Runde {Mathf.Min(p.entry.LapsCompleted + 1, d.rules.laps)}/{d.rules.laps}" : $"Runde {Mathf.Max(1, p.entry.timer.LapNumber)}";
            ApexStyle.Text(new Rect(x + 14f, y + 6f, w, 40f), pos, 30, ApexStyle.Fg, FontStyle.Bold);
            ApexStyle.Text(new Rect(x + 140f, y + 10f, w - 150f, 34f), lap, 20, ApexStyle.TextDim, FontStyle.Normal, TextAnchor.MiddleRight);
            var w8 = Race.Weather;
            string wx = $"{WeatherModel.Name(w8.State)} · {w8.TrackTemp:0}°C" + (w8.TrackWetness > 0.05f ? $" · nass {w8.TrackWetness * 100f:0}%" : "");
            ApexStyle.Text(new Rect(x + 14f, y + 50f, w - 20f, 30f), wx, 17, w8.TrackWetness > 0.25f ? ApexStyle.Caution : ApexStyle.TextDim);
            DrawTrackStatus(new Rect(x, y + h + 30f, w, 30f), p);
            float fx = safe.xMax - 120f;
            ApexStyle.Text(new Rect(fx - 160f, y + h + 4f, 270f, 24f), $"{fps:0} FPS", 16, ApexStyle.TextDim, FontStyle.Normal, TextAnchor.MiddleRight);
            DrawHudButtons(safe);
        }

        /// <summary>Pause and radio buttons in the top-right corner (mouse; touch has its own buttons).</summary>
        void DrawHudButtons(Rect safe)
        {
            float y = safe.y + 20f;
            if (Settings.controlPreset == ControlPreset.Keyboard)
            {
                if (ApexStyle.Button(new Rect(safe.xMax - 110f, y, 90f, 60f), "II", false, 28)) sm.TryTransition(GameState.Pause);
                if (ApexStyle.Button(new Rect(safe.xMax - 110f, y + 68f, 90f, 44f), "FUNK", radioOpen, 16))
                {
                    if (radioOpen) CloseRadio();
                    else OpenRadio();
                }
            }
        }

        /// <summary>Persistent flag strip under the info panel (race control status).</summary>
        void DrawTrackStatus(Rect r, RaceCar p)
        {
            var rc = Race.Control;
            if (rc == null || Race.Type != SessionType.Race) return;
            string text = null;
            Color c = ApexStyle.Caution;
            switch (rc.Status)
            {
                case TrackStatus.Yellow:
                    if (rc.IsYellowAt(p.raw)) text = "GELB – nicht überholen";
                    break;
                case TrackStatus.VirtualSafetyCar: text = "VSC – Tempo begrenzt"; break;
                case TrackStatus.VirtualSafetyCarEnding: text = "VSC ENDET"; c = ApexStyle.Faster; break;
                case TrackStatus.SafetyCar: text = "SAFETY CAR"; c = new Color(1f, 0.6f, 0.1f); break;
                case TrackStatus.SafetyCarIn: text = "SC IN DIESER RUNDE"; c = new Color(1f, 0.6f, 0.1f); break;
            }
            if (text == null) return;
            ApexStyle.Box(r, new Color(c.r * 0.25f, c.g * 0.25f, c.b * 0.1f, 0.85f));
            ApexStyle.Box(new Rect(r.x, r.y, 6f, r.height), c);
            ApexStyle.Text(new Rect(r.x + 16f, r.y, r.width - 20f, r.height), text, 18, c, FontStyle.Bold);
            Zone("TrackStatus", r);
        }

        void DrawDash(Rect safe, RaceCar p)
        {
            var sim = p.Sim;
            var timer = p.entry.timer;
            float dw = 440f, dx = (virtualSize.x - dw) * 0.5f, dy = safe.yMax - 200f;
            var r = new Rect(dx, dy, dw, 184f);
            ApexStyle.Box(r, ApexStyle.Panel);
            Zone("Dash", r);
            float rpmFrac = Mathf.InverseLerp(4000f, 12500f, sim.EngineRpm);
            const int segments = 15;
            float segW = (dw - 40f) / segments;
            for (int i = 0; i < segments; i++)
            {
                bool lit = rpmFrac * segments > i;
                Color c = i < 9 ? ApexStyle.Faster : (i < 12 ? ApexStyle.Caution : ApexStyle.Urgent);
                ApexStyle.Box(new Rect(dx + 20f + i * segW, dy + 10f, segW - 4f, 12f), lit ? c : new Color(1f, 1f, 1f, 0.08f));
            }
            int gear = sim.gearbox.gear;
            ApexStyle.Text(new Rect(dx + 16f, dy + 26f, 110f, 90f), gear < 0 ? "R" : gear == 0 ? "N" : gear.ToString(), 80, ApexStyle.Accent, FontStyle.Bold, TextAnchor.MiddleCenter);
            float spd = Mathf.Abs(sim.ForwardSpeed) * (Settings.speedInMph ? 2.23694f : 3.6f);
            ApexStyle.Text(new Rect(dx + 120f, dy + 28f, 160f, 64f), spd.ToString("0"), 56, ApexStyle.Fg, FontStyle.Bold, TextAnchor.MiddleRight);
            ApexStyle.Text(new Rect(dx + 120f, dy + 88f, 160f, 24f), Settings.speedInMph ? "mph" : "km/h", 17, ApexStyle.TextDim, FontStyle.Normal, TextAnchor.MiddleRight);
            // Lap time and delta on the right of the dash.
            ApexStyle.Text(new Rect(dx + 290f, dy + 30f, 140f, 34f), ApexStyle.FormatTime(timer.CurrentLapTime()), 24, timer.LapValid || !timer.LapActive ? ApexStyle.Fg : ApexStyle.Urgent, FontStyle.Bold, TextAnchor.MiddleRight);
            double delta = timer.LiveDelta(Circuit != null ? Circuit.LapDistance(p.raw) : 0f);
            ApexStyle.Text(new Rect(dx + 290f, dy + 62f, 140f, 28f), ApexStyle.FormatDelta(delta), 20, delta < 0 ? ApexStyle.Faster : ApexStyle.Urgent, FontStyle.Bold, TextAnchor.MiddleRight);
            ApexStyle.Text(new Rect(dx + 290f, dy + 90f, 140f, 24f), "Beste " + ApexStyle.FormatTime(timer.BestLapTime), 15, ApexStyle.Best, FontStyle.Normal, TextAnchor.MiddleRight);
            for (int i = 0; i < 3; i++)
                ApexStyle.Text(new Rect(dx + 20f + i * 140f, dy + 120f, 140f, 24f), sectorText[i], 16, sectorColor[i]);
            // Tires: compound, temperature colour, wear fill.
            var tires = sim.tires;
            ApexStyle.Text(new Rect(dx + 20f, dy + 148f, 40f, 28f), CompoundSpec.For(tires[0].compound).shortName, 20, CompoundColour(tires[0].compound), FontStyle.Bold);
            for (int i = 0; i < 4; i++)
            {
                float tx = dx + 56f + (i % 2) * 24f, ty = dy + 146f + (i / 2) * 16f;
                var t = tires[i];
                var spec = t.Spec;
                float rel = (t.temperature - spec.optimalTemp) / spec.window;
                Color c = rel < -0.6f ? new Color(0.3f, 0.55f, 1f) : rel > 0.6f ? ApexStyle.Urgent : ApexStyle.Faster;
                ApexStyle.Box(new Rect(tx, ty, 20f, 13f), new Color(c.r, c.g, c.b, 0.35f));
                ApexStyle.Box(new Rect(tx, ty, 20f * (1f - t.wear), 13f), c);
            }
            string flags = (sim.TcActive ? "TC  " : "") + (sim.AbsActive ? "ABS  " : "") + (Mathf.Abs(sim.CounterSteer) > 0.01f ? "STAB  " : "") + (float.IsInfinity(sim.speedLimit) ? "" : "PIT LIMIT");
            ApexStyle.Text(new Rect(dx + 120f, dy + 148f, 310f, 28f), flags, 17, ApexStyle.Caution, FontStyle.Bold, TextAnchor.MiddleRight);
            if (p.pit.stage == PitState.Stage.Requested)
                ApexStyle.Text(new Rect(dx, dy - 30f, dw, 26f), $"BOX ANGEFORDERT – {CompoundSpec.For(p.pit.nextCompound).name}", 18, ApexStyle.Accent, FontStyle.Bold, TextAnchor.MiddleCenter);
            double record = sessionRecord;
            if (record > 0 && hudPreset == 0)
                ApexStyle.Text(new Rect(dx + dw + 12f, dy + 150f, 220f, 26f), "Rekord " + ApexStyle.FormatTime(record), 15, ApexStyle.TextDim);
        }

        CircuitInstance Circuit => Race?.Circuit;

        void DrawLights(Rect safe, RaceDirector d)
        {
            float size = 56f, gap = 18f;
            float w = 5 * size + 4 * gap + 40f;
            float x = (virtualSize.x - w) * 0.5f, y = safe.y + 110f;
            var r = new Rect(x, y, w, size + 30f);
            ApexStyle.Box(r, ApexStyle.PanelSolid);
            Zone("Lights", r);
            for (int i = 0; i < 5; i++)
            {
                bool on = d.LightsOn > i;
                ApexStyle.Box(new Rect(x + 20f + i * (size + gap), y + 15f, size, size), on ? new Color(0.95f, 0.08f, 0.05f) : new Color(0.2f, 0.05f, 0.05f));
            }
        }

        void DrawToasts(Rect safe)
        {
            if (toastHigh.time > 0f)
            {
                var r = new Rect(virtualSize.x * 0.25f, safe.y + 210f, virtualSize.x * 0.5f, 48f);
                ApexStyle.Box(r, new Color(0.04f, 0.05f, 0.07f, 0.6f * Mathf.Clamp01(toastHigh.time * 3f)));
                ApexStyle.Text(r, toastHigh.text, 26, toastHigh.color, FontStyle.Bold, TextAnchor.MiddleCenter);
                Zone("Toast", r);
            }
            if (toastLow.time > 0f)
            {
                var r = new Rect(virtualSize.x * 0.3f, safe.y + 264f, virtualSize.x * 0.4f, 36f);
                ApexStyle.Text(r, toastLow.text, 20, toastLow.color, FontStyle.Bold, TextAnchor.MiddleCenter);
            }
        }

        void DrawMiniMap(Rect safe)
        {
            var r = new Rect(safe.x + 20f, safe.y + 300f, 260f, 220f);
            ApexStyle.Box(r, ApexStyle.Panel);
            Zone("Minimap", r);
            DrawTrackMap(new Rect(r.x + 8f, r.y + 8f, r.width - 16f, r.height - 16f), Race.Circuit.Geometry, Race.Circuit.Definition, Race, 2.5f, false);
        }

        void DrawRadio(Rect safe)
        {
            float w = 330f, rowH = 42f;
            // Below the race status box (and its flag strip) of the UI Toolkit HUD.
            float x = safe.xMax - w - 130f, y = safe.y + (hudView != null ? 240f : 130f);
            var r = new Rect(x, y, w, rowH * RadioItems.Length + 50f);
            ApexStyle.Box(r, ApexStyle.PanelSolid);
            Zone("Radio", r);
            ApexStyle.Text(new Rect(x + 14f, y + 6f, w, 34f), "TEAMFUNK", 18, ApexStyle.Accent, FontStyle.Bold);
            for (int i = 0; i < RadioItems.Length; i++)
            {
                var rr = new Rect(x + 8f, y + 44f + i * rowH, w - 16f, rowH - 4f);
                string label = $"{i + 1}  {RadioItems[i]}" + (i == 1 ? $": {CompoundSpec.For(radioCompound).name}" : "");
                if (Settings.controlPreset == ControlPreset.Keyboard)
                {
                    if (ApexStyle.Button(rr, label, i == radioFocus, 18)) RadioAction(i);
                }
                else
                {
                    ApexStyle.Box(rr, i == radioFocus ? new Color(0.12f, 0.2f, 0.24f, 0.95f) : new Color(1f, 1f, 1f, 0.04f));
                    ApexStyle.Text(new Rect(rr.x + 12f, rr.y, rr.width, rr.height), label, 18, ApexStyle.Fg);
                }
            }
        }

        void DrawPitTimer(Rect safe, RaceCar p)
        {
            var ps = p.pit;
            float elapsed = ps.serviceTotal - ps.serviceRemaining;
            var r = new Rect((virtualSize.x - 360f) * 0.5f, safe.y + 120f, 360f, 70f);
            ApexStyle.Box(r, ApexStyle.PanelSolid);
            Zone("PitTimer", r);
            ApexStyle.Text(new Rect(r.x, r.y + 4f, r.width, 34f), $"BOXENSTOPP {elapsed:0.0} s", 26, ApexStyle.Fg, FontStyle.Bold, TextAnchor.MiddleCenter);
            ApexStyle.Text(new Rect(r.x, r.y + 38f, r.width, 28f), $"Neue Reifen: {CompoundSpec.For(ps.nextCompound).name}", 18, CompoundColour(ps.nextCompound), FontStyle.Normal, TextAnchor.MiddleCenter);
        }

        void DrawTelemetry(Rect safe, RaceCar car)
        {
            var sim = car.Sim;
            var r = new Rect(safe.xMax - 600f, safe.y + 240f, 580f, 386f);
            ApexStyle.Box(r, ApexStyle.Panel);
            Zone("Telemetry", r);
            float y = r.y + 10f;
            void Line(string s)
            {
                ApexStyle.Text(new Rect(r.x + 14f, y, r.width - 28f, 26f), s, 17, ApexStyle.Fg);
                y += 26f;
            }
            Line($"rpm {sim.EngineRpm:00000}  Gas {sim.throttle:0.00}  Bremse {sim.brake:0.00}  Lenk {sim.steer:+0.00;-0.00}");
            Line($"Lenkwinkel {sim.SteerAngle * ApexMath.Rad2Deg:+0.0;-0.0}°  Gegenlenk {sim.CounterSteer * ApexMath.Rad2Deg:+0.0;-0.0}°  TC {sim.TcCut:0.00}");
            Line($"Abtrieb V {sim.DownforceFront:0} N  H {sim.DownforceRear:0} N  Luftwid. {sim.Drag:0} N");
            Line($"Fahrhöhe V {sim.RideHeightFront * 1000f:0} mm  H {sim.RideHeightRear * 1000f:0} mm  Boden x{sim.FloorGain:0.00}  Bal. {sim.AeroBalance * 100f:0.0} %{(sim.Bottoming ? "  AUFSETZEN" : "")}");
            Line($"Kupplung {(sim.ClutchSlipping ? "schleift" : "geschlossen")}{(sim.LimiterActive ? "  BEGRENZER" : "")}  Frame {Time.smoothDeltaTime * 1000f:0.0} ms ({1f / Mathf.Max(Time.smoothDeltaTime, 1e-4f):0} fps)");
            for (int i = 0; i < 4; i++)
            {
                var w = sim.wheels[i];
                var t = sim.tires[i];
                Line($"{w.label} {w.load,5:0}N sr {w.slipRatio:+0.00;-0.00} sa {w.slipAngle * ApexMath.Rad2Deg:+0.0;-0.0}° {w.compression * 1000f:0}mm {t.temperature:0}°C {t.wear * 100f:0}% g{w.grip:0.00} {w.surface}");
            }
            // Handling validation: slip use per axle (> 1 = past the grip peak), lateral g, yaw rate, body slip.
            var body = car.car.Body;
            var lv = car.car.transform.InverseTransformDirection(RigidbodyCompat.GetVelocity(body));
            float yaw = body.angularVelocity.y;
            float useF = Mathf.Max(Mathf.Abs(sim.wheels[0].slipAngle), Mathf.Abs(sim.wheels[1].slipAngle)) / sim.tireFront.slipAnglePeak;
            float useR = Mathf.Max(Mathf.Abs(sim.wheels[2].slipAngle), Mathf.Abs(sim.wheels[3].slipAngle)) / sim.tireRear.slipAnglePeak;
            float beta = lv.z > 1f ? Mathf.Atan2(lv.x, lv.z) * Mathf.Rad2Deg : 0f;
            Line($"{(sim.setup.legacy ? "ALT" : "NEU")}  Schlupf V {useF:0.00} H {useR:0.00}  quer {Mathf.Abs(yaw * lv.z) / 9.81f:0.00} g  Gier {yaw * Mathf.Rad2Deg:+0;-0}°/s  β {beta:+0.0;-0.0}°{(sim.EscActive ? "  ESC" : "")}{(sim.AbsActive ? "  ABS" : "")}");
            Line($"Strecke nass {sim.trackWetness * 100f:0} %  {sim.trackTemperature:0}°C  Physik {1f / Time.fixedDeltaTime:0} Hz");
            Line($"Neigung {input.RollDeg:+0;-0}°  Zuverl. {input.TiltReliability:0.00}");
        }

        void DrawTouchButtons()
        {
            if (Settings.controlPreset == ControlPreset.Keyboard) return;
            float h = Screen.height;
            foreach (var b in input.Buttons)
            {
                var r = new Rect(b.rect.x / uiScale, (h - b.rect.yMax) / uiScale, b.rect.width / uiScale, b.rect.height / uiScale);
                Zone("Touch:" + b.label, r);
                if (b.id >= TouchLayout.RadioEntry0) continue; // drawn by the radio panel layout below
                bool held = input.touch.IsHeld(b.id);
                float a = Settings.touchButtonOpacity * (held ? 1.6f : 1f);
                Color fill = b.id == TouchLayout.Throttle ? new Color(0.3f, 0.9f, 0.45f, a * 0.6f)
                    : b.id == TouchLayout.Brake ? new Color(1f, 0.32f, 0.27f, a * 0.6f)
                    : new Color(1f, 1f, 1f, a * 0.35f);
                ApexStyle.Box(r, fill);
                ApexStyle.Text(r, b.label, b.id == TouchLayout.Radio ? 16 : 24, new Color(1f, 1f, 1f, Mathf.Clamp01(a * 2f)), FontStyle.Bold, TextAnchor.MiddleCenter);
            }
            if (radioOpen)
            {
                foreach (var b in input.Buttons)
                {
                    if (b.id < TouchLayout.RadioEntry0) continue;
                    var r = new Rect(b.rect.x / uiScale, (h - b.rect.yMax) / uiScale, b.rect.width / uiScale, b.rect.height / uiScale);
                    int k = b.id - TouchLayout.RadioEntry0;
                    ApexStyle.Box(r, k == radioFocus ? new Color(0.12f, 0.3f, 0.36f, 0.92f) : new Color(0.07f, 0.08f, 0.1f, 0.85f));
                    string label = k == 1 ? $"2 Reifen: {CompoundSpec.For(radioCompound).shortName}" : b.label;
                    ApexStyle.Text(r, label, 18, ApexStyle.Fg, FontStyle.Bold, TextAnchor.MiddleCenter);
                }
            }
        }

        /// <summary>F2: outlines HUD zones and touch controls, lists overlaps and safe-area violations.</summary>
        void DrawLayoutDebug(Rect safe)
        {
            var outline = new Color(1f, 0f, 1f, 0.8f);
            void Outline(Rect r, Color c)
            {
                ApexStyle.Box(new Rect(r.x, r.y, r.width, 2f), c);
                ApexStyle.Box(new Rect(r.x, r.yMax - 2f, r.width, 2f), c);
                ApexStyle.Box(new Rect(r.x, r.y, 2f, r.height), c);
                ApexStyle.Box(new Rect(r.xMax - 2f, r.y, 2f, r.height), c);
            }
            Outline(safe, Color.yellow);
            var issues = new List<string>();
            for (int i = 0; i < hudZones.Count; i++)
            {
                Outline(hudZones[i].rect, outline);
                var a = hudZones[i];
                if (a.rect.xMin < safe.xMin - 1f || a.rect.xMax > safe.xMax + 1f || a.rect.yMin < safe.yMin - 1f || a.rect.yMax > safe.yMax + 1f)
                    issues.Add($"{a.name} außerhalb der Safe Area");
                for (int j = i + 1; j < hudZones.Count; j++)
                {
                    var b = hudZones[j];
                    bool aTouch = a.name.StartsWith("Touch:"), bTouch = b.name.StartsWith("Touch:");
                    if (aTouch && bTouch) continue;
                    if (a.rect.Overlaps(b.rect)) issues.Add($"Überlappung: {a.name} × {b.name}");
                }
            }
            var box = new Rect(virtualSize.x * 0.3f, safe.y + 320f, virtualSize.x * 0.4f, 30f + Mathf.Max(1, issues.Count) * 24f);
            ApexStyle.Box(box, new Color(0f, 0f, 0f, 0.8f));
            ApexStyle.Text(new Rect(box.x + 10f, box.y + 4f, box.width, 24f), issues.Count == 0 ? "Layout-Prüfung: keine Konflikte" : $"Layout-Prüfung: {issues.Count} Konflikt(e)", 18, issues.Count == 0 ? ApexStyle.Faster : ApexStyle.Urgent, FontStyle.Bold);
            for (int i = 0; i < issues.Count; i++)
                ApexStyle.Text(new Rect(box.x + 10f, box.y + 28f + i * 24f, box.width, 24f), issues[i], 16, ApexStyle.Fg);
        }
    }
}
