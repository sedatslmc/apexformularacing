using System;
using System.Collections.Generic;
using Apex.Career;
using Apex.Core;
using Apex.Race;
using Apex.Tracks;
using Apex.Vehicle;
using Apex.Weather;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Apex.Game
{
    /// <summary>Menus, setup screens, results, pause and settings (interim IMGUI layer).</summary>
    public sealed partial class ApexGame
    {
        enum SetupMode { GrandPrix, Practice, TimeTrial, Championship }

        int focus;
        bool settingsOpen;
        bool helpOpen;
        bool navUp, navDown, navLeft, navRight, navConfirm;
        float uiScale = 1f;
        Vector2 virtualSize;
        SetupMode setupMode;

        struct Row
        {
            public string label;
            public Func<string> value;
            public Action<int> change;
        }

        void ReadNavigation()
        {
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            navUp = input.NavUp;
            navDown = input.NavDown;
            navLeft = (kb != null && kb.leftArrowKey.wasPressedThisFrame) || (pad != null && pad.dpad.left.wasPressedThisFrame);
            navRight = (kb != null && kb.rightArrowKey.wasPressedThisFrame) || (pad != null && pad.dpad.right.wasPressedThisFrame);
            navConfirm = input.ConfirmPressed;
        }

        void OnGUI()
        {
            uiScale = Screen.height / 1080f * Settings.hudScale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
            virtualSize = new Vector2(Screen.width / uiScale, Screen.height / uiScale);
            switch (sm.Current)
            {
                case GameState.MainMenu:
                    if (settingsOpen) DrawSettings();
                    else DrawMainMenu();
                    break;
                case GameState.TrackSelect:
                    if (setupMode == SetupMode.Championship) DrawChampionship();
                    else DrawSetup();
                    break;
                case GameState.Garage:
                    DrawGarage();
                    break;
                case GameState.Loading:
                    DrawLoading();
                    break;
                case GameState.Grid:
                case GameState.Driving:
                case GameState.RadioOverlay:
                case GameState.PitApproach:
                case GameState.PitStop:
                    DrawHud();
                    if (calibrating) DrawCalibration();
                    break;
                case GameState.Pause:
                    DrawHud();
                    if (settingsOpen) DrawSettings();
                    else DrawPause();
                    break;
                case GameState.Results:
                    DrawResults();
                    break;
            }
            if (helpOpen) DrawHelp();
            if (toastLow.time > 0f && sm.Current != GameState.Driving && sm.Current != GameState.Grid && sm.Current != GameState.Pause)
                ApexStyle.Text(new Rect(0, virtualSize.y - 80f, virtualSize.x, 40f), toastLow.text, 24, toastLow.color, FontStyle.Bold, TextAnchor.MiddleCenter);
        }

        Rect SafeRect()
        {
            var s = Screen.safeArea;
            return new Rect(s.x / uiScale, (Screen.height - s.yMax) / uiScale, s.width / uiScale, s.height / uiScale);
        }

        int MenuNav(int count)
        {
            if (count <= 0) return 0;
            if (focus >= count) focus = count - 1;
            if (navDown) focus = (focus + 1) % count;
            if (navUp) focus = (focus - 1 + count) % count;
            return focus;
        }

        /// <summary>Vertical menu; returns true if an item was activated this frame.</summary>
        bool MenuList(float x, float y, float w, float rowH, List<(string label, Action action)> items, int fontSize = 30)
        {
            int f = MenuNav(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                var r = new Rect(x, y + i * (rowH + 10f), w, rowH);
                if (ApexStyle.Button(r, items[i].label, i == f, fontSize) || (navConfirm && i == f))
                {
                    focus = i;
                    navConfirm = false;
                    items[i].action();
                    return true;
                }
            }
            return false;
        }

        /// <summary>Settings-style rows with −/+; the last focus index is the extra button(s).</summary>
        void RowList(float x, float y, float w, float rowH, List<Row> rows, int extraButtons, int fontSize = 24)
        {
            int f = MenuNav(rows.Count + extraButtons);
            for (int i = 0; i < rows.Count; i++)
            {
                var r = new Rect(x, y + i * rowH, w, rowH - 6f);
                bool focused = i == f;
                ApexStyle.Box(r, focused ? new Color(0.12f, 0.2f, 0.24f, 0.95f) : new Color(1f, 1f, 1f, 0.03f));
                if (focused) ApexStyle.Box(new Rect(r.x, r.y, 5f, r.height), ApexStyle.Accent);
                ApexStyle.Text(new Rect(r.x + 20f, r.y, w * 0.5f, r.height), rows[i].label, fontSize, ApexStyle.Fg);
                var minus = new Rect(r.xMax - 360f, r.y + 3f, 64f, r.height - 6f);
                var plus = new Rect(r.xMax - 74f, r.y + 3f, 64f, r.height - 6f);
                ApexStyle.Text(new Rect(r.xMax - 290f, r.y, 210f, r.height), rows[i].value(), fontSize, ApexStyle.Accent, FontStyle.Bold, TextAnchor.MiddleCenter);
                if (ApexStyle.Button(minus, "−", false, fontSize + 2)) { focus = i; rows[i].change(-1); OnRowChanged(); }
                if (ApexStyle.Button(plus, "+", false, fontSize + 2)) { focus = i; rows[i].change(1); OnRowChanged(); }
                if (focused && navLeft) { rows[i].change(-1); OnRowChanged(); }
                if (focused && (navRight || navConfirm)) { rows[i].change(1); OnRowChanged(); navConfirm = false; }
            }
        }

        void OnRowChanged()
        {
            Config.Validate();
            SaveConfig();
        }

        static int Wrap(int v, int count) => ((v % count) + count) % count;
        static string OnOff(bool b) => b ? "An" : "Aus";
        static string TcName(int level) => level <= 0 ? "Aus" : level == 1 ? "Niedrig" : level == 2 ? "Mittel" : "Hoch";

        // --- Main menu ----------------------------------------------------------------------

        void DrawMainMenu()
        {
            var safe = SafeRect();
            float x = safe.x + 70f, y = safe.y + 50f;
            ApexStyle.Text(new Rect(x, y, 900f, 100f), "PROJECT APEX", 88, ApexStyle.Fg, FontStyle.Bold);
            ApexStyle.Box(new Rect(x, y + 100f, 160f, 5f), ApexStyle.Accent);
            int lvl = Career.Level;
            int next = Progression.XpForLevel(Mathf.Min(lvl + 1, Progression.MaxLevel));
            ApexStyle.Text(new Rect(x, y + 112f, 1200f, 36f), $"Level {lvl}  ·  {Career.xp} / {next} XP  ·  Siege {Career.wins}  ·  Podien {Career.podiums}  ·  Poles {Career.poles}", 24, ApexStyle.TextDim);
            var items = new List<(string, Action)>();
            if (Career.ChampionshipActive)
            {
                string tn = Definition(Career.calendar[Career.nextRound])?.name ?? Career.calendar[Career.nextRound];
                items.Add(($"Weiter: Meisterschaft – Runde {Career.nextRound + 1}/{Career.calendar.Length} ({tn})", () => StartSession(SessionKind.Championship, SessionType.Race)));
            }
            items.Add(("Grand Prix", () => OpenSetup(SetupMode.GrandPrix)));
            items.Add(("Meisterschaft", () => OpenSetup(SetupMode.Championship)));
            items.Add(("Zeitfahren", () => OpenSetup(SetupMode.TimeTrial)));
            items.Add(("Freies Training", () => OpenSetup(SetupMode.Practice)));
            items.Add(("Garage", () => sm.TryTransition(GameState.Garage)));
            items.Add(("Einstellungen", () => { settingsOpen = true; focus = 0; }));
            items.Add(("Steuerung & Hilfe", () => helpOpen = true));
            items.Add(("Beenden", Application.Quit));
            float rowH = Mathf.Min(70f, (safe.height - 260f) / items.Count - 10f);
            MenuList(x, y + 170f, 820f, rowH, items, 28);
        }

        void OpenSetup(SetupMode mode)
        {
            setupMode = mode;
            focus = 0;
            sm.TryTransition(GameState.TrackSelect);
        }

        // --- Grand Prix / practice / time trial setup ------------------------------------

        List<Row> SetupRows()
        {
            var c = Config;
            var rows = new List<Row>
            {
                new Row { label = "Strecke", value = () => Definition(c.trackId)?.name ?? c.trackId,
                    change = d => c.trackId = TrackIds[Wrap(Array.IndexOf(TrackIds, c.trackId) + (d < 0 ? -1 : 1), TrackIds.Length)] },
            };
            if (setupMode == SetupMode.GrandPrix)
            {
                rows.Add(new Row { label = "Format", value = () => c.format == WeekendFormat.RaceOnly ? "Nur Rennen" : "Qualifying + Rennen", change = d => c.format = c.format == WeekendFormat.RaceOnly ? WeekendFormat.QualifyingAndRace : WeekendFormat.RaceOnly });
                rows.Add(new Row { label = "Runden", value = () => c.laps.ToString(), change = d => c.laps += d == 0 ? 1 : d });
                rows.Add(new Row { label = "Qualifying-Dauer", value = () => $"{c.qualifyingMinutes:0} min", change = d => c.qualifyingMinutes += d == 0 ? 1 : d });
                rows.Add(new Row { label = "KI-Gegner", value = () => c.aiCount.ToString(), change = d => c.aiCount += d == 0 ? 1 : d });
                rows.Add(new Row { label = "KI-Stärke", value = () => $"{c.difficulty * 100f:0} %", change = d => c.difficulty += (d == 0 ? 1 : d) * 0.1f });
            }
            if (setupMode != SetupMode.TimeTrial)
            {
                rows.Add(new Row { label = "Wetter", value = () => WeatherModel.Name(c.weather), change = d => c.weather = (WeatherState)Wrap((int)c.weather + (d < 0 ? -1 : 1), 4) });
                if (setupMode == SetupMode.GrandPrix)
                    rows.Add(new Row { label = "Wetterwechsel", value = () => OnOff(c.dynamicWeather), change = d => c.dynamicWeather = !c.dynamicWeather });
                rows.Add(new Row { label = "Reifenverschleiß", value = () => c.tireWear <= 0f ? "Aus" : $"{c.tireWear:0}×", change = d => c.tireWear = Mathf.Clamp(c.tireWear + (d < 0 ? -1 : 1), 0f, 10f) });
                rows.Add(new Row { label = "Startreifen", value = () => CompoundSpec.For(c.startCompound).name, change = d => c.startCompound = (TireCompound)Wrap((int)c.startCompound + (d < 0 ? -1 : 1), 3) });
            }
            rows.Add(new Row { label = "Tageszeit", value = () => TimeName(c.timeOfDay), change = d => c.timeOfDay = (TimeOfDay)Wrap((int)c.timeOfDay + (d < 0 ? -1 : 1), 5) });
            if (setupMode == SetupMode.GrandPrix)
            {
                rows.Add(new Row { label = "Frühstart-Strafe", value = () => OnOff(c.falseStarts), change = d => c.falseStarts = !c.falseStarts });
                rows.Add(new Row { label = "Tracklimit-Strafen", value = () => OnOff(c.trackLimitPenalties), change = d => c.trackLimitPenalties = !c.trackLimitPenalties });
                rows.Add(new Row { label = "Pflicht-Boxenstopp", value = () => OnOff(c.mandatoryPitStop), change = d => c.mandatoryPitStop = !c.mandatoryPitStop });
                rows.Add(new Row { label = "Safety Car / VSC", value = () => OnOff(c.safetyCar), change = d => c.safetyCar = !c.safetyCar });
                rows.Add(new Row { label = "Schäden", value = () => OnOff(c.damage), change = d => c.damage = !c.damage });
                rows.Add(new Row { label = "Technische Defekte (KI)", value = () => c.failures == 0 ? "Aus" : c.failures == 1 ? "Selten" : "Häufig",
                    change = d => c.failures = Wrap(c.failures + (d < 0 ? -1 : 1), 3) });
            }
            rows.Add(new Row { label = "Team", value = () => Roster.Teams[c.playerTeam].name, change = d => c.playerTeam = Wrap(c.playerTeam + (d < 0 ? -1 : 1), Roster.Teams.Length) });
            return rows;
        }

        static string TimeName(TimeOfDay t)
        {
            switch (t)
            {
                case TimeOfDay.Morning: return "Morgen";
                case TimeOfDay.Day: return "Mittag";
                case TimeOfDay.Dusk: return "Sonnenuntergang";
                case TimeOfDay.Night: return "Nacht";
                default: return "Goldene Stunde";
            }
        }

        void DrawSetup()
        {
            var safe = SafeRect();
            float lw = Mathf.Min(1000f, safe.width * 0.58f);
            float x = safe.x + 40f, y = safe.y + 30f;
            ApexStyle.Box(new Rect(x - 20f, y - 16f, lw + 40f, safe.height - 30f), ApexStyle.PanelSolid);
            string title = setupMode == SetupMode.GrandPrix ? "GRAND PRIX" : setupMode == SetupMode.TimeTrial ? "ZEITFAHREN" : "FREIES TRAINING";
            ApexStyle.Text(new Rect(x, y, lw, 56f), title, 42, ApexStyle.Fg, FontStyle.Bold);
            var rows = SetupRows();
            float rowH = Mathf.Min(54f, (safe.height - 230f) / (rows.Count + 2));
            RowList(x, y + 70f, lw, rowH, rows, 2, 22);
            float by = y + 76f + rows.Count * rowH;
            int f = focus;
            if (ApexStyle.Button(new Rect(x, by, 420f, 64f), "Start", f == rows.Count, 30) || (navConfirm && f == rows.Count))
            {
                navConfirm = false;
                SaveConfig();
                if (setupMode == SetupMode.GrandPrix)
                    StartSession(SessionKind.GrandPrix, Config.format == WeekendFormat.QualifyingAndRace ? SessionType.Qualifying : SessionType.Race);
                else
                    StartSession(setupMode == SetupMode.TimeTrial ? SessionKind.TimeTrial : SessionKind.Practice, SessionType.Practice);
                return;
            }
            if (ApexStyle.Button(new Rect(x + 440f, by, 300f, 64f), "Zurück", f == rows.Count + 1, 28) || (navConfirm && f == rows.Count + 1))
            {
                sm.TryTransition(GameState.MainMenu);
                return;
            }
            DrawTrackPreview(new Rect(x + lw + 50f, y - 16f, safe.xMax - (x + lw + 50f) - 20f, safe.height - 30f), Config.trackId);
        }

        void DrawTrackPreview(Rect r, string id)
        {
            if (r.width < 200f) return;
            ApexStyle.Box(r, ApexStyle.Panel);
            var def = Definition(id);
            if (def == null) return;
            var g = Geometry(id);
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in g.Positions) { minY = Mathf.Min(minY, p.Y); maxY = Mathf.Max(maxY, p.Y); }
            int corners = cornerCounts[id];
            ApexStyle.Text(new Rect(r.x + 20f, r.y + 14f, r.width - 40f, 40f), def.name, 28, ApexStyle.Fg, FontStyle.Bold);
            ApexStyle.Text(new Rect(r.x + 20f, r.y + 52f, r.width - 40f, 30f), $"{g.Length / 1000f:0.00} km · {corners} Kurven · 3 Sektoren · Höhenunterschied {maxY - minY:0} m", 20, ApexStyle.TextDim);
            bool named = def.corners.Count > 0;
            if (def.identity == EventIdentity.Bosphorus)
                ApexStyle.Text(new Rect(r.x + 20f, r.y + 78f, r.width - 40f, 24f), EventIdentity.Slogan, 16, ApexStyle.Accent, FontStyle.Bold);
            var mapRect = new Rect(r.x + 20f, r.y + 104f, r.width - 40f, r.height - (named ? 300f : 228f));
            DrawTrackMap(mapRect, g, def, null, 4f, true, named);
            if (named)
            {
                // Corner names in lap order, numbered like the labels on the map.
                var order = CornersInLapOrder(def, g.Length);
                var sb = new System.Text.StringBuilder();
                for (int k = 0; k < order.Count; k++) sb.Append(k == 0 ? "" : " · ").Append(k + 1).Append(' ').Append(order[k].name);
                ApexStyle.Text(new Rect(r.x + 20f, r.yMax - 192f, r.width - 40f, 76f), WordWrap(sb.ToString(), 78), 15, ApexStyle.Fg, FontStyle.Normal, TextAnchor.UpperLeft);
            }
            ApexStyle.Text(new Rect(r.x + 20f, r.yMax - 116f, r.width - 40f, 100f), WordWrap(def.description, 70), 18, ApexStyle.TextDim, FontStyle.Normal, TextAnchor.UpperLeft);
            double best = SaveService.BestLap(Settings, id);
            if (best > 0) ApexStyle.Text(new Rect(r.x + 20f, r.yMax - 40f, r.width - 40f, 30f), "Dein Rekord: " + ApexStyle.FormatTime(best), 20, ApexStyle.Best);
        }

        readonly Dictionary<string, TrackGeometry> geometries = new Dictionary<string, TrackGeometry>();
        readonly Dictionary<string, int> cornerCounts = new Dictionary<string, int>();

        TrackGeometry Geometry(string id)
        {
            if (geometries.TryGetValue(id, out var g)) return g;
            g = TrackGeometry.FromDefinition(Definition(id));
            geometries[id] = g;
            cornerCounts[id] = g.CornersFor(Definition(id)).Count;
            return g;
        }

        static string WordWrap(string text, int width)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var words = text.Split(' ');
            var sb = new System.Text.StringBuilder();
            int line = 0;
            foreach (var w in words)
            {
                if (line + w.Length > width) { sb.Append('\n'); line = 0; }
                sb.Append(w).Append(' ');
                line += w.Length + 1;
            }
            return sb.ToString();
        }

        /// <summary>Top-down map from the same spline data (north up). Optionally shows cars.</summary>
        static List<CircuitDefinition.CornerMark> CornersInLapOrder(CircuitDefinition def, float L)
        {
            var list = new List<CircuitDefinition.CornerMark>(def.corners);
            list.Sort((a, b) => ApexMath.PosMod(a.distance - def.startLineDistance, L).CompareTo(ApexMath.PosMod(b.distance - def.startLineDistance, L)));
            return list;
        }

        void DrawTrackMap(Rect r, TrackGeometry g, CircuitDefinition def, RaceController race, float dot, bool sectors, bool cornerNumbers = false)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var p in g.Positions)
            {
                minX = Mathf.Min(minX, p.X); maxX = Mathf.Max(maxX, p.X);
                minZ = Mathf.Min(minZ, p.Z); maxZ = Mathf.Max(maxZ, p.Z);
            }
            float scale = Mathf.Min(r.width / Mathf.Max(maxX - minX, 1f), r.height / Mathf.Max(maxZ - minZ, 1f)) * 0.92f;
            float ox = r.x + (r.width - (maxX - minX) * scale) * 0.5f;
            float oy = r.y + (r.height - (maxZ - minZ) * scale) * 0.5f;
            Vector2 Map(float px, float pz) => new Vector2(ox + (px - minX) * scale, oy + (maxZ - pz) * scale);
            int step = Mathf.Max(1, g.Count / 260);
            Color[] sectorCols = { new Color(0.9f, 0.3f, 0.25f), new Color(0.2f, 0.6f, 0.95f), new Color(0.95f, 0.75f, 0.2f) };
            for (int i = 0; i < g.Count; i += step)
            {
                var m = Map(g.Positions[i].X, g.Positions[i].Z);
                Color c = ApexStyle.Fg;
                if (sectors)
                {
                    float lapD = ApexMath.PosMod(g.Distances[i] - def.startLineDistance, g.Length) / g.Length;
                    int s = lapD < def.sectorFractions[Mathf.Min(1, def.sectorFractions.Length - 1)] ? 0 : (def.sectorFractions.Length > 2 && lapD >= def.sectorFractions[2] ? 2 : 1);
                    c = sectorCols[s];
                }
                ApexStyle.Box(new Rect(m.x - dot * 0.5f, m.y - dot * 0.5f, dot, dot), c);
            }
            var sl = g.PositionAt(def.startLineDistance);
            var sm2 = Map(sl.X, sl.Z);
            ApexStyle.Box(new Rect(sm2.x - dot, sm2.y - dot, dot * 2f, dot * 2f), Color.white);
            if (cornerNumbers)
            {
                var order = CornersInLapOrder(def, g.Length);
                for (int k = 0; k < order.Count; k++)
                {
                    // Number on the outside of the corner, a little off the line.
                    float d = order[k].distance;
                    g.FrameAt(d, out var cp, out _, out _, (order[k].right ? -1f : 1f) * 18f / Mathf.Max(scale, 1e-3f));
                    var m = Map(cp.X, cp.Z);
                    ApexStyle.Text(new Rect(m.x - 12f, m.y - 10f, 24f, 20f), (k + 1).ToString(), 14, ApexStyle.Fg, FontStyle.Bold, TextAnchor.MiddleCenter);
                }
            }
            if (race == null) return;
            foreach (var rc in race.Cars)
            {
                var p = rc.car.transform.position;
                var m = Map(p.x, p.z);
                var team = Roster.Teams[rc.teamIndex];
                float s = rc.isPlayer ? dot * 2.6f : dot * 1.8f;
                ApexStyle.Box(new Rect(m.x - s * 0.5f, m.y - s * 0.5f, s, s), rc.isPlayer ? ApexStyle.Accent : new Color(team.r, team.g, team.b));
            }
        }

        // --- Championship -------------------------------------------------------------------

        void DrawChampionship()
        {
            var safe = SafeRect();
            float w = Mathf.Min(1200f, safe.width - 80f);
            float x = safe.x + (safe.width - w) * 0.5f, y = safe.y + 30f;
            ApexStyle.Box(new Rect(x - 20f, y - 16f, w + 40f, safe.height - 30f), ApexStyle.PanelSolid);
            ApexStyle.Text(new Rect(x, y, w, 56f), "MEISTERSCHAFT – APEX SERIES", 40, ApexStyle.Fg, FontStyle.Bold);
            if (Career.ChampionshipActive)
            {
                ApexStyle.Text(new Rect(x, y + 64f, w, 32f), $"Runde {Career.nextRound + 1} von {Career.calendar.Length} · {Career.championshipLaps} Runden · {Career.championshipAi} KI-Gegner", 22, ApexStyle.TextDim);
                var lines = ChampionshipTable(12);
                for (int i = 0; i < lines.Length; i++)
                    ApexStyle.Text(new Rect(x, y + 110f + i * 34f, w, 32f), lines[i], 22, lines[i].Contains("Du ") || lines[i].Contains(" Du") ? ApexStyle.Accent : ApexStyle.Fg);
                var items = new List<(string, Action)>
                {
                    ($"Nächste Runde: {Definition(Career.calendar[Career.nextRound])?.name}", () => StartSession(SessionKind.Championship, SessionType.Race)),
                    ("Meisterschaft aufgeben", () => { Career.calendar = new string[0]; SaveCareer(); focus = 0; }),
                    ("Zurück", () => sm.TryTransition(GameState.MainMenu)),
                };
                MenuList(x, safe.yMax - 260f, 700f, 64f, items, 26);
            }
            else
            {
                ApexStyle.Text(new Rect(x, y + 64f, w, 60f), "Kalender: Bosphorus Grand Prix, Apex Testoval. Punkte 25-18-15-12-10-8-6-4-2-1, +1 für die schnellste Runde (Top 10).", 20, ApexStyle.TextDim);
                var rows = new List<Row>
                {
                    new Row { label = "Runden pro Rennen", value = () => Config.laps.ToString(), change = d => Config.laps += d == 0 ? 1 : d },
                    new Row { label = "KI-Gegner", value = () => Config.aiCount.ToString(), change = d => Config.aiCount += d == 0 ? 1 : d },
                    new Row { label = "KI-Stärke", value = () => $"{Config.difficulty * 100f:0} %", change = d => Config.difficulty += (d == 0 ? 1 : d) * 0.1f },
                    new Row { label = "Team", value = () => Roster.Teams[Config.playerTeam].name, change = d => Config.playerTeam = Wrap(Config.playerTeam + (d < 0 ? -1 : 1), Roster.Teams.Length) },
                };
                RowList(x, y + 140f, w, 56f, rows, 2, 22);
                float by = y + 150f + rows.Count * 56f;
                if (ApexStyle.Button(new Rect(x, by, 460f, 64f), "Meisterschaft starten", focus == rows.Count, 28) || (navConfirm && focus == rows.Count))
                {
                    navConfirm = false;
                    Career.StartChampionship(new[] { "bosphorus_a", "test_loop" }, Config.laps, Config.aiCount, Config.difficulty);
                    SaveCareer();
                    SaveConfig();
                    StartSession(SessionKind.Championship, SessionType.Race);
                    return;
                }
                if (ApexStyle.Button(new Rect(x + 480f, by, 300f, 64f), "Zurück", focus == rows.Count + 1, 28) || (navConfirm && focus == rows.Count + 1))
                    sm.TryTransition(GameState.MainMenu);
            }
        }

        // --- Garage -------------------------------------------------------------------------

        void DrawGarage()
        {
            var safe = SafeRect();
            float w = 640f, x = safe.x + 40f, y = safe.y + 30f;
            ApexStyle.Box(new Rect(x - 20f, y - 16f, w + 40f, safe.height - 30f), ApexStyle.Panel);
            ApexStyle.Text(new Rect(x, y, w, 56f), "GARAGE", 42, ApexStyle.Fg, FontStyle.Bold);
            ApexStyle.Text(new Rect(x, y + 56f, w, 30f), "Nur Optik – Leistung ist in allen Modi gleich.", 18, ApexStyle.TextDim);
            var paints = UnlockedPaints();
            var rows = new List<Row>
            {
                new Row { label = "Team", value = () => Roster.Teams[Config.playerTeam].name,
                    change = d => { Config.playerTeam = Wrap(Config.playerTeam + (d < 0 ? -1 : 1), Roster.Teams.Length); SaveConfig(); RebuildShowroomCar(); } },
                new Row { label = "Lackierung", value = () => PaintName(Career.paintIndex),
                    change = d =>
                    {
                        int i = Mathf.Max(0, paints.IndexOf(Career.paintIndex));
                        Career.paintIndex = paints[Wrap(i + (d < 0 ? -1 : 1), paints.Count)];
                        SaveCareer();
                        RebuildShowroomCar();
                    } },
                new Row { label = "Startnummer", value = () => Career.carNumber.ToString(), change = d => { Career.carNumber = Mathf.Clamp(Career.carNumber + (d == 0 ? 1 : d), 2, 99); SaveCareer(); } },
            };
            RowList(x, y + 100f, w, 56f, rows, 1, 22);
            int lvl = Career.Level;
            float ly = y + 120f + rows.Count * 56f;
            ApexStyle.Text(new Rect(x, ly, w, 30f), $"Level {lvl} · {paints.Count} von 20 Lackierungen freigeschaltet", 20, ApexStyle.Accent);
            if (lvl < Progression.MaxLevel)
            {
                var next = Progression.Rewards[lvl];
                ApexStyle.Text(new Rect(x, ly + 32f, w, 30f), $"Nächste Belohnung (Level {next.level}): {next.name}", 20, ApexStyle.TextDim);
            }
            var unlocked = Career.Unlocked();
            int shown = 0;
            for (int i = unlocked.Count - 1; i >= 0 && shown < 6; i--, shown++)
                ApexStyle.Text(new Rect(x, ly + 72f + shown * 28f, w, 26f), $"• Level {unlocked[i].level}: {unlocked[i].name}", 18, ApexStyle.Fg);
            if (ApexStyle.Button(new Rect(x, safe.yMax - 100f, 300f, 64f), "Zurück", focus == rows.Count, 28) || (navConfirm && focus == rows.Count))
                sm.TryTransition(GameState.MainMenu);
        }

        // --- Settings -------------------------------------------------------------------------

        List<Row> SettingsRows()
        {
            var s = Settings;
            var rows = new List<Row>
            {
                new Row { label = "Steuerung", value = () => s.controlPreset == ControlPreset.Keyboard ? "Tastatur/Gamepad" : s.controlPreset == ControlPreset.TouchButtons ? "Touch-Tasten" : "Neigung (Gyro)",
                    change = d => s.controlPreset = (ControlPreset)Wrap((int)s.controlPreset + (d < 0 ? -1 : 1), 3) },
                new Row { label = "Fahrprofil", value = () => SettingsData.ProfileName(s.drivingProfile),
                    change = d => s.ApplyProfile((DrivingProfile)Wrap((int)s.drivingProfile + (d < 0 ? -1 : 1), 3)) },
                new Row { label = "Traktionskontrolle", value = () => TcName(s.tractionLevel),
                    change = d => { s.tractionLevel = Wrap(s.tractionLevel + (d < 0 ? -1 : 1), 4); s.drivingProfile = DrivingProfile.Custom; } },
                new Row { label = "ABS", value = () => s.abs ? "An" : "Aus (empfohlen mit analogen Pedalen)", change = d => { s.abs = !s.abs; s.drivingProfile = DrivingProfile.Custom; } },
                new Row { label = "Lenkhilfe", value = () => s.steeringAssistLevel == 0 ? "Aus" : s.steeringAssistLevel == 1 ? "Leicht" : "Voll",
                    change = d => { s.steeringAssistLevel = Wrap(s.steeringAssistLevel + (d < 0 ? -1 : 1), 3); s.drivingProfile = DrivingProfile.Custom; } },
                new Row { label = "Tempoabhängige Lenkung", value = () => s.speedSensitiveSteering ? "An" : "Aus (Lenkrad/Gamepad)",
                    change = d => { s.speedSensitiveSteering = !s.speedSensitiveSteering; s.drivingProfile = DrivingProfile.Custom; } },
                new Row { label = "Automatikgetriebe", value = () => OnOff(s.autoGearbox), change = d => { s.autoGearbox = !s.autoGearbox; s.drivingProfile = DrivingProfile.Custom; } },
                new Row { label = "Bremsbalance vorne", value = () => $"{s.brakeBiasFront * 100f:0} %", change = d => s.brakeBiasFront += (d == 0 ? 1 : d) * 0.01f },
                new Row { label = "Fahrverhalten (Vergleich, ab nächster Session)", value = () => s.legacyHandling ? "Alt (vor Okt. 2026)" : "Neu (Okt. 2026)",
                    change = d => s.legacyHandling = !s.legacyHandling },
                new Row { label = "Boxen-Assistent", value = () => OnOff(s.pitAssist), change = d => s.pitAssist = !s.pitAssist },
                new Row { label = "Neigung: max. Winkel", value = () => $"{s.tiltMaxAngleDeg:0}°", change = d => s.tiltMaxAngleDeg += d == 0 ? 5 : d * 5 },
                new Row { label = "Neigung: Empfindlichkeit", value = () => $"{s.tiltSensitivity:0.0}", change = d => s.tiltSensitivity += (d == 0 ? 1 : d) * 0.1f },
                new Row { label = "Neigung: Totzone", value = () => $"{s.tiltDeadZoneDeg:0.0}°", change = d => s.tiltDeadZoneDeg += (d == 0 ? 1 : d) * 0.5f },
                new Row { label = "Neigung: Glättung", value = () => $"{s.tiltSmoothing * 1000f:0} ms", change = d => s.tiltSmoothing += (d == 0 ? 1 : d) * 0.01f },
                new Row { label = "Neigung umkehren", value = () => OnOff(s.invertTilt), change = d => s.invertTilt = !s.invertTilt },
                new Row { label = "Linkshänder-Layout", value = () => OnOff(s.leftHanded), change = d => s.leftHanded = !s.leftHanded },
                new Row { label = "Tastengröße", value = () => $"{s.touchButtonScale * 100f:0} %", change = d => s.touchButtonScale += (d == 0 ? 1 : d) * 0.1f },
                new Row { label = "Tasten-Deckkraft", value = () => $"{s.touchButtonOpacity * 100f:0} %", change = d => s.touchButtonOpacity += (d == 0 ? 1 : d) * 0.1f },
                new Row { label = "HUD-Größe", value = () => $"{s.hudScale * 100f:0} %", change = d => s.hudScale += (d == 0 ? 1 : d) * 0.05f },
                new Row { label = "Geschwindigkeit", value = () => s.speedInMph ? "mph" : "km/h", change = d => s.speedInMph = !s.speedInMph },
                new Row { label = "Reduzierte Bewegung", value = () => OnOff(s.reducedMotion), change = d => s.reducedMotion = !s.reducedMotion },
                new Row { label = "Grafikstufe", value = () => GraphicsTierLabel(s.graphicsTier),
                    change = d =>
                    {
                        // -1 (Auto) .. Ultra; the tier also sets its environment density and road textures.
                        s.graphicsTier = Wrap(s.graphicsTier + 1 + (d < 0 ? -1 : 1), GraphicsPreset.TierCount + 1) - 1;
                        var p = GraphicsPreset.For(GraphicsQuality.Resolve(s.graphicsTier));
                        s.environmentDetail = p.environmentDetail;
                        s.ultraTextures = p.ultraTextures;
                    } },
                new Row { label = "Umgebungsdetails (ab nächster Session)", value = () => s.environmentDetail == 0 ? "Niedrig" : s.environmentDetail == 1 ? "Mittel" : "Hoch",
                    change = d => s.environmentDetail = Wrap(s.environmentDetail + (d < 0 ? -1 : 1), 3) },
                new Row { label = "Lautstärke", value = () => $"{s.masterVolume * 100f:0} %", change = d => s.masterVolume += (d == 0 ? 1 : d) * 0.1f },
            };
            // Only where 4K road textures were fetched (tools/textures/fetch_cc0.py --tier ultra) and not on mobile.
            if (SurfaceTextures.UltraAvailable)
                rows.Insert(rows.Count - 1, new Row { label = "Straßentexturen (ab nächster Session)", value = () => s.ultraTextures ? "Ultra (4K)" : "Standard (1K)",
                    change = d => s.ultraTextures = !s.ultraTextures });
            // Only with URP: the built-in pipeline has no post-processing stack here.
            if (PostFxStack.Supported)
                rows.Insert(rows.Count - 1, new Row { label = "Nachbearbeitung (Farbe, Bloom)", value = () => OnOff(s.postProcessing), change = d => s.postProcessing = !s.postProcessing });
            return rows;
        }

        static string GraphicsTierLabel(int setting)
        {
            string name = GraphicsPreset.Name(GraphicsQuality.Resolve(setting));
            string label = setting < 0 ? "Auto (" + name + ")" : name;
            // Honest label: the tier asks for SSAO, but this renderer has no SSAO feature.
            if (GraphicsQuality.Current.ssao && !GraphicsQuality.SsaoAvailable && PostFxStack.Supported) label += ", ohne SSAO";
            return label;
        }

        void DrawSettings()
        {
            var safe = SafeRect();
            float w = Mathf.Min(1100f, safe.width - 80f);
            float x = safe.x + (safe.width - w) * 0.5f, y = safe.y + 24f;
            ApexStyle.Box(new Rect(x - 20f, y - 14f, w + 40f, safe.height - 20f), ApexStyle.PanelSolid);
            ApexStyle.Text(new Rect(x, y, w, 50f), "EINSTELLUNGEN", 38, ApexStyle.Fg, FontStyle.Bold);
            var rows = SettingsRows();
            float rowH = Mathf.Min(52f, (safe.height - 170f) / (rows.Count + 1));
            int before = focus;
            // Settings rows apply and save immediately.
            int f = MenuNav(rows.Count + 1);
            for (int i = 0; i < rows.Count; i++)
            {
                var r = new Rect(x, y + 60f + i * rowH, w, rowH - 4f);
                bool focused = i == f;
                ApexStyle.Box(r, focused ? new Color(0.12f, 0.2f, 0.24f, 0.95f) : new Color(1f, 1f, 1f, 0.03f));
                if (focused) ApexStyle.Box(new Rect(r.x, r.y, 5f, r.height), ApexStyle.Accent);
                ApexStyle.Text(new Rect(r.x + 20f, r.y, w * 0.55f, r.height), rows[i].label, 21, ApexStyle.Fg);
                var minus = new Rect(r.xMax - 330f, r.y + 3f, 64f, r.height - 6f);
                var plus = new Rect(r.xMax - 74f, r.y + 3f, 64f, r.height - 6f);
                ApexStyle.Text(new Rect(r.xMax - 260f, r.y, 180f, r.height), rows[i].value(), 21, ApexStyle.Accent, FontStyle.Bold, TextAnchor.MiddleCenter);
                if (ApexStyle.Button(minus, "−", false, 24)) { focus = i; ChangeSetting(rows[i], -1); }
                if (ApexStyle.Button(plus, "+", false, 24)) { focus = i; ChangeSetting(rows[i], 1); }
                if (focused && navLeft) ChangeSetting(rows[i], -1);
                if (focused && (navRight || navConfirm)) ChangeSetting(rows[i], navConfirm ? 0 : 1);
            }
            var back = new Rect(x, y + 66f + rows.Count * rowH, 320f, 58f);
            if (ApexStyle.Button(back, "Zurück", f == rows.Count, 26) || (navConfirm && f == rows.Count))
            {
                settingsOpen = false;
                focus = 0;
            }
            if (!input.TiltAvailable)
                ApexStyle.Text(new Rect(x + 340f, back.y, w - 340f, 58f), "Kein Bewegungssensor erkannt – Neigung fällt auf Touch-Tasten zurück.", 18, ApexStyle.Caution);
        }

        void ChangeSetting(Row row, int dir)
        {
            row.change(dir);
            Settings.showTelemetry = showTelemetry;
            Settings.MigrateAndValidate();
            input.ApplySettings(Settings);
            ApplyAssists();
            if (weatherVisuals != null) weatherVisuals.postProcessing = Settings.postProcessing;
            ApplyGraphics();
            SaveService.SaveSettings(Settings);
        }

        // --- Loading / pause / calibration / results / help ----------------------------------

        void DrawLoading()
        {
            ApexStyle.Box(new Rect(0, 0, virtualSize.x, virtualSize.y), new Color(0.04f, 0.05f, 0.06f, 1f));
            ApexStyle.Text(new Rect(0, virtualSize.y * 0.5f - 40f, virtualSize.x, 80f), "Strecke und Startaufstellung werden aufgebaut …", 36, ApexStyle.Fg, FontStyle.Normal, TextAnchor.MiddleCenter);
        }

        void DrawPause()
        {
            ApexStyle.Box(new Rect(0, 0, virtualSize.x, virtualSize.y), new Color(0f, 0f, 0f, 0.55f));
            float w = 620f, x = (virtualSize.x - w) * 0.5f, y = virtualSize.y * 0.5f - 300f;
            ApexStyle.Text(new Rect(x, y, w, 70f), "PAUSE", 54, ApexStyle.Fg, FontStyle.Bold, TextAnchor.MiddleCenter);
            var items = new List<(string, Action)> { ("Weiter", () => sm.Resume()) };
            if (Race != null && Race.Type != SessionType.Race)
            {
                items.Add(("Auto auf die Strecke setzen", () => { sm.Resume(); Race.ResetPlayerToTrack(); }));
                items.Add(("Session neu starten", () => { var k = kind; var t = currentType; sm.TryTransition(GameState.Loading); EndSession(); kind = k; currentType = t; StartCoroutine(LoadSession(null)); }));
            }
            items.Add(("Einstellungen", () => { settingsOpen = true; focus = 0; }));
            items.Add(("Steuerung & Hilfe", () => helpOpen = true));
            items.Add((Race != null && Race.Type == SessionType.Race ? "Rennen aufgeben (Hauptmenü)" : "Zum Hauptmenü", () => sm.TryTransition(GameState.MainMenu)));
            MenuList(x, y + 100f, w, 70f, items, 27);
        }

        void DrawCalibration()
        {
            float w = 900f, h = 300f, x = (virtualSize.x - w) * 0.5f, y = virtualSize.y * 0.28f;
            ApexStyle.Box(new Rect(x, y, w, h), ApexStyle.PanelSolid);
            ApexStyle.Text(new Rect(x, y + 20f, w, 50f), "Neigungssteuerung kalibrieren", 36, ApexStyle.Fg, FontStyle.Bold, TextAnchor.MiddleCenter);
            string hint = input.TiltAvailable
                ? (input.TiltReliability < 0.5f ? "Gerät aufrechter halten – es liegt zu flach." : "Halte das Gerät bequem wie ein Lenkrad und tippe auf „Lenkung zentrieren“.")
                : "Kein Bewegungssensor gefunden. Es werden Touch-Tasten verwendet.";
            ApexStyle.Text(new Rect(x + 30f, y + 80f, w - 60f, 50f), hint, 24, ApexStyle.TextDim, FontStyle.Normal, TextAnchor.MiddleCenter);
            ApexStyle.Text(new Rect(x, y + 130f, w, 40f), $"Aktueller Winkel: {input.RollDeg:0}°", 22, ApexStyle.Accent, FontStyle.Normal, TextAnchor.MiddleCenter);
            if (ApexStyle.Button(new Rect(x + (w - 420f) * 0.5f, y + 190f, 420f, 80f), "Lenkung zentrieren", true, 30))
                FinishCalibration();
        }

        void DrawResults()
        {
            var res = lastResults;
            if (res == null) return;
            var safe = SafeRect();
            ApexStyle.Box(new Rect(0, 0, virtualSize.x, virtualSize.y), new Color(0.04f, 0.05f, 0.06f, 0.96f));
            float x = safe.x + 40f, y = safe.y + 24f, w = safe.width - 80f;
            string title = res.type == SessionType.Race ? "ERGEBNIS – RENNEN" : res.type == SessionType.Qualifying ? "ERGEBNIS – QUALIFYING" : "SESSION BEENDET";
            ApexStyle.Text(new Rect(x, y, w, 50f), $"{title}  ·  {res.trackName}", 36, ApexStyle.Fg, FontStyle.Bold);
            float[] cols = { 0f, 60f, 140f, 480f, 820f, 1000f, 1150f, 1220f, 1320f };
            string[] heads = { "Pos", "", "Fahrer", "Team", "Zeit/Abstand", "Beste Runde", "Stopps", "Strafe", "Pkt." };
            float colScale = Mathf.Min(1f, w / 1400f);
            for (int c = 0; c < heads.Length; c++)
                ApexStyle.Text(new Rect(x + cols[c] * colScale, y + 56f, 200f, 28f), heads[c], 18, ApexStyle.TextDim, FontStyle.Bold);
            float rowH = Mathf.Min(34f, (safe.height - 300f) / Mathf.Max(res.rows.Count, 1));
            for (int i = 0; i < res.rows.Count; i++)
            {
                float ry = y + 88f + i * rowH;
                if (i == res.playerRow) ApexStyle.Box(new Rect(x - 10f, ry, w + 20f, rowH), new Color(0.12f, 0.25f, 0.3f, 0.9f));
                var row = res.rows[i];
                for (int c = 0; c < row.Length; c++)
                    ApexStyle.Text(new Rect(x + cols[c] * colScale, ry, (c + 1 < cols.Length ? cols[c + 1] - cols[c] : 120f) * colScale, rowH), row[c], Mathf.RoundToInt(rowH * 0.6f), i == res.playerRow ? ApexStyle.Accent : ApexStyle.Fg);
            }
            float by = safe.yMax - 92f;
            string xpLine = $"+{res.xpGained} XP" + (res.levelAfter > res.levelBefore ? $"  ·  LEVEL {res.levelAfter} erreicht! Neu: {Progression.Rewards[res.levelAfter - 1].name}" : "");
            ApexStyle.Text(new Rect(x, by - 46f, w, 40f), xpLine, 24, res.levelAfter > res.levelBefore ? ApexStyle.Best : ApexStyle.Faster, FontStyle.Bold);
            var items = new List<(string, Action)>();
            if (res.type == SessionType.Qualifying && res.kind == SessionKind.GrandPrix)
                items.Add(("Weiter zum Rennen", () => StartSession(SessionKind.GrandPrix, SessionType.Race, pendingRaceGrid)));
            if (res.kind == SessionKind.Championship && Career.ChampionshipActive)
                items.Add(("Nächste Meisterschaftsrunde", () => StartSession(SessionKind.Championship, SessionType.Race)));
            items.Add(("Hauptmenü", () => sm.TryTransition(GameState.MainMenu)));
            int f = MenuNav(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                var r = new Rect(x + i * 470f, by, 450f, 64f);
                if (ApexStyle.Button(r, items[i].Item1, i == f, 26) || (navConfirm && i == f))
                {
                    navConfirm = false;
                    items[i].Item2();
                    break;
                }
            }
            if (res.championshipLines != null)
            {
                float cx = safe.xMax - 560f;
                ApexStyle.Box(new Rect(cx - 16f, y + 50f, 560f, 40f + res.championshipLines.Length * 30f), ApexStyle.Panel);
                ApexStyle.Text(new Rect(cx, y + 54f, 540f, 30f), res.championshipFinished ? "MEISTERSCHAFT – ENDSTAND" : "MEISTERSCHAFT – ZWISCHENSTAND", 20, ApexStyle.Accent, FontStyle.Bold);
                for (int i = 0; i < res.championshipLines.Length; i++)
                    ApexStyle.Text(new Rect(cx, y + 86f + i * 30f, 540f, 28f), res.championshipLines[i], 19, ApexStyle.Fg);
            }
        }

        void DrawHelp()
        {
            float w = Mathf.Min(1300f, virtualSize.x - 80f), h = Mathf.Min(820f, virtualSize.y - 80f);
            float x = (virtualSize.x - w) * 0.5f, y = (virtualSize.y - h) * 0.5f;
            ApexStyle.Box(new Rect(x, y, w, h), ApexStyle.PanelSolid);
            ApexStyle.Text(new Rect(x + 30f, y + 16f, w, 50f), "STEUERUNG & HILFE", 36, ApexStyle.Fg, FontStyle.Bold);
            string[] left =
            {
                "W / ↑   Gas          S / ↓   Bremse (im Stand halten: Rückwärts)",
                "A / ←   links         D / →   rechts",
                "Q / E   runter-/hochschalten (manuelles Getriebe)",
                "C   Kamera (Cockpit, T-Cam, Verfolger, weit)",
                "R   Teamfunk   ·   1–6 Funk-Auswahl",
                "P   Boxenstopp anfordern (gewählte Reifen)",
                "Tab   Timing-Tower aufklappen   ·   M   Minimap",
                "H   HUD-Variante   ·   F3   Telemetrie   ·   F2   Layout-Prüfung",
                "Rücktaste   Auto zurücksetzen (Training/Zeitfahren)",
                "F5 (2×)   Training neu starten   ·   F9   Screenshot",
                "Esc / Start   Pause   ·   F1   diese Hilfe",
            };
            string[] right =
            {
                "Touch: GAS/BREMSE rechts, ◄ ► links (Linkshänder spiegelbar).",
                "Neigung: Gerät wie ein Lenkrad drehen, MITTE zentriert neu.",
                "Funk-Taste oben rechts, Pause darüber.",
                "Assistenzen: TC, ABS, Lenkhilfe (inkl. Gegenlenken),",
                "Automatik, Boxen-Assistent – in den Einstellungen.",
                "Tracklimits: alle 4 Räder neben der Strecke = Verwarnung,",
                "nach 3 Verwarnungen +5 s (wenn aktiviert).",
                "Sektorfarben: lila = Bestzeit, grün = verbessert, gelb = langsamer.",
                "Reifen: S/M/H trocken, I/W bei Nässe; Temperatur und",
                "Verschleiß im HUD (Quadrate unter dem Tacho).",
            };
            for (int i = 0; i < left.Length; i++)
                ApexStyle.Text(new Rect(x + 30f, y + 80f + i * 40f, w * 0.52f, 36f), left[i], 20, ApexStyle.Fg);
            for (int i = 0; i < right.Length; i++)
                ApexStyle.Text(new Rect(x + w * 0.54f, y + 80f + i * 40f, w * 0.44f, 36f), right[i], 20, ApexStyle.TextDim);
            if (ApexStyle.Button(new Rect(x + 30f, y + h - 84f, 300f, 60f), "Schließen", true, 26) || navConfirm)
            {
                navConfirm = false;
                helpOpen = false;
            }
        }
    }
}
