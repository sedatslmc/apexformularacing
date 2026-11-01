using System;
using System.Collections.Generic;
using Apex.Core;
using Apex.Tracks;
using Apex.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Apex.Game
{
    /// <summary>
    /// Race preparation of APEX // RACE CONTROL (docs/UI_UX_OVERHAUL.md Phase 3, mockup ui_mock_track_select.jpg):
    /// Grand Prix, time trial and practice setup with the track presentation ("WELCOME TO ISTANBUL" for the
    /// Bosphorus GP, map from the real track data, key facts, track cards), and the championship screen (calendar,
    /// standings, start / continue / abandon with confirmation). Every option edits the real
    /// <see cref="SessionConfig"/> through <see cref="SetupModel"/>.
    /// </summary>
    public sealed class SetupView
    {
        public sealed class TrackInfo
        {
            public CircuitDefinition def;
            public TrackGeometry geometry;
            public TrackFacts facts;
        }

        public sealed class Data
        {
            public SetupMode mode;
            public Func<List<SetupOption>> options;
            public Func<string> summary;
            public Func<string> selectedTrack;
            public Action<string> selectTrack;
            public IList<string> trackIds = new List<string>();
            public Func<string, TrackInfo> track;
            public bool mobile;
            // Championship.
            public bool championshipActive;
            public string seasonLine = "", nextRound = "";
            public List<CalendarRound> calendar = new List<CalendarRound>();
            public List<StandingRow> standings = new List<StandingRow>();
        }

        /// <summary>Start the session (setup) or the next championship round.</summary>
        public event Action StartRequested;
        public event Action BackRequested;
        public event Action AbandonRequested;
        /// <summary>An option changed the config (validate and save).</summary>
        public event Action OptionChanged;
        public bool ReducedMotion;

        readonly ApexUi ui;
        readonly UiSounds sounds;
        Data data;
        VisualElement layer, listContent, listViewport, presentation;
        Label startDetail;
        TrackMapElement map;
        readonly List<Item> items = new List<Item>();
        readonly List<(VisualElement card, string id)> trackCards = new List<(VisualElement, string)>();
        int focus;
        float screenTime, mapTime, scroll, scrollTarget;
        bool abandonArmed;
        string shownTrack = "";
        string optionSignature = "";

        const float ListTop = 236f, ListHeight = 600f, RowH = 50f, LeftW = 720f, RightX = 880f;

        sealed class Item
        {
            public SetupOption option;      // null = button
            public Action action;
            public bool primary;
            public VisualElement row, highlight, edge;
            public Label label, value;
            public Tween focus;
        }

        sealed class Arrow { public Item item; public int dir; }
        sealed class TrackCard { public string id; }

        public SetupView(UiSounds sounds)
        {
            this.sounds = sounds;
            ui = new ApexUi("ApexSetup", 11);
            ui.SetVisible(false);
            Visible = false;
        }

        public bool Visible { get; private set; }

        public void SetVisible(bool on)
        {
            if (on == Visible) return;
            Visible = on;
            ui.SetVisible(on);
        }

        // --- Build --------------------------------------------------------------------------------------------

        public void Show(Data d)
        {
            data = d;
            focus = 0;
            abandonArmed = false;
            scroll = scrollTarget = 0f;
            Build();
            screenTime = 0f;
            sounds.Play(UiSounds.Cue.Transition);
        }

        void Build()
        {
            layer?.RemoveFromHierarchy();
            items.Clear();
            trackCards.Clear();
            layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute;
            layer.style.left = 0; layer.style.right = 0; layer.style.top = 0; layer.style.bottom = 0;
            ui.root.Add(layer);
            float m = UiTokens.ScreenMargin;

            var shadeL = ApexUi.Box(layer, Color.clear);
            ApexUi.Place(shadeL, 0, 0, 980, -1); shadeL.style.bottom = 0;
            ApexUi.Background(shadeL, ApexUi.Ramp(ApexUi.C(UiTokens.Navy), 0.95f, 0.55f, false, 0.6f));
            var shadeR = ApexUi.Box(layer, ApexUi.C(UiTokens.Navy, 0.55f));
            ApexUi.Place(shadeR, 980, 0); shadeR.style.right = 0; shadeR.style.bottom = 0;

            bool champ = data.mode == SetupMode.Championship;
            var head = new VisualElement { pickingMode = PickingMode.Ignore };
            ApexUi.Place(head, m, 56, LeftW, -1);
            layer.Add(head);
            ApexUi.Caption(head, champ ? Loc.T("Saison · Apex Series") : Loc.T("Rennvorbereitung"));
            ApexUi.Text(head, SetupModel.Title(data.mode).ToUpperInvariant(), ApexUi.DisplayStrong, UiTokens.H1, ApexUi.C(UiTokens.Ivory), 4f).style.marginTop = 6;
            var accent = ApexUi.Box(head, ApexUi.C(UiTokens.Cyan));
            accent.style.width = 64; accent.style.height = 3; accent.style.marginTop = 10;
            if (champ && data.championshipActive)
                ApexUi.Text(head, data.seasonLine, ApexUi.Body, UiTokens.Body, ApexUi.C(UiTokens.Steel)).style.marginTop = 12;

            listViewport = new VisualElement { pickingMode = PickingMode.Ignore };
            ApexUi.Place(listViewport, m, ListTop, LeftW, ListHeight);
            listViewport.style.overflow = Overflow.Hidden;
            layer.Add(listViewport);
            listContent = new VisualElement { pickingMode = PickingMode.Ignore };
            listViewport.Add(listContent);

            if (champ && data.championshipActive) BuildCalendarList();
            else BuildOptions();

            // Actions below the list.
            var actions = new VisualElement { pickingMode = PickingMode.Ignore };
            ApexUi.Place(actions, m, ListTop + ListHeight + 40f, LeftW, -1);
            actions.style.flexDirection = FlexDirection.Row;
            layer.Add(actions);
            if (champ && data.championshipActive)
            {
                AddButton(actions, Loc.T("Nächste Runde"), data.nextRound, true, () => StartRequested?.Invoke());
                AddButton(actions, Loc.T("Aufgeben"), "", false, Abandon);
            }
            else
                AddButton(actions, champ ? Loc.T("Saison starten") : Loc.T("Start"), data.summary(), true, () => StartRequested?.Invoke());
            AddButton(actions, Loc.T("Zurück"), "", false, () => BackRequested?.Invoke());

            if (!data.mobile)
            {
                var hints = ApexUi.Text(layer, Loc.T("↑↓  Auswählen     ←→  Ändern     Enter  Bestätigen     Esc  Zurück"), ApexUi.Body, 14, ApexUi.C(UiTokens.Steel), 0.5f);
                ApexUi.Place(hints, m, 1036);
            }

            presentation = new VisualElement { pickingMode = PickingMode.Ignore };
            ApexUi.Place(presentation, RightX, 0, 1920 - RightX - m, 1080);
            layer.Add(presentation);
            shownTrack = "";
            if (champ) BuildChampionshipRight();
            else RefreshPresentation(true);
            ApplyFocus(true);
        }

        void BuildOptions()
        {
            var opts = data.options();
            optionSignature = Signature(opts);
            string group = null;
            foreach (var o in opts)
            {
                if (o.group != group)
                {
                    group = o.group;
                    var cap = ApexUi.Caption(listContent, group);
                    cap.style.marginTop = items.Count == 0 ? 0 : 18;
                    cap.style.marginBottom = 6;
                }
                AddOption(o);
            }
        }

        static string Signature(List<SetupOption> opts)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var o in opts) sb.Append(o.key).Append(';');
            return sb.ToString();
        }

        void AddOption(SetupOption o)
        {
            var it = new Item { option = o };
            it.row = new VisualElement { pickingMode = PickingMode.Position, userData = it };
            it.row.style.height = RowH - 4f;
            it.row.style.marginBottom = 4;
            it.row.style.flexDirection = FlexDirection.Row;
            it.row.style.alignItems = Align.Center;
            it.row.style.paddingLeft = 20; it.row.style.paddingRight = 8;
            listContent.Add(it.row);
            Decorate(it, false);
            it.label = ApexUi.Text(it.row, o.label, ApexUi.Body, 18, ApexUi.C(UiTokens.Ivory, 0.85f));
            var sp = new VisualElement { pickingMode = PickingMode.Ignore }; sp.style.flexGrow = 1; it.row.Add(sp);
            ArrowButton(it, -1, "‹");
            it.value = ApexUi.Text(it.row, o.value(), ApexUi.BodyStrong, 18, ApexUi.C(UiTokens.Cyan), 0.4f, TextAnchor.MiddleCenter);
            it.value.style.width = 230;
            ArrowButton(it, 1, "›");
            items.Add(it);
        }

        void ArrowButton(Item it, int dir, string glyph)
        {
            var b = new VisualElement { pickingMode = PickingMode.Position, userData = new Arrow { item = it, dir = dir } };
            b.style.width = 36; b.style.height = 36;
            b.style.alignItems = Align.Center; b.style.justifyContent = Justify.Center;
            it.row.Add(b);
            ApexUi.Text(b, glyph, ApexUi.DisplayStrong, 28, ApexUi.C(UiTokens.Ivory, 0.7f), 0f, TextAnchor.MiddleCenter);
        }

        void Decorate(Item it, bool primary)
        {
            it.highlight = ApexUi.Box(it.row, Color.clear);
            ApexUi.Place(it.highlight, 0, 0); it.highlight.style.right = 0; it.highlight.style.bottom = 0;
            ApexUi.Background(it.highlight, ApexUi.Ramp(ApexUi.C(UiTokens.Cyan), primary ? 0.3f : 0.16f, primary ? 0.06f : 0f));
            it.edge = ApexUi.Box(it.row, ApexUi.C(UiTokens.Cyan));
            ApexUi.Place(it.edge, 0, 0, 3, -1); it.edge.style.bottom = 0;
        }

        void AddButton(VisualElement parent, string label, string detail, bool primary, Action action)
        {
            var it = new Item { action = action, primary = primary };
            it.row = primary ? ApexUi.Panel(parent, UiTokens.PanelAlpha) : new VisualElement();
            it.row.pickingMode = PickingMode.Position;
            it.row.userData = it;
            it.row.style.height = 84;
            it.row.style.minWidth = primary ? 380 : 150;
            it.row.style.marginRight = 14;
            it.row.style.justifyContent = Justify.Center;
            it.row.style.paddingLeft = 24; it.row.style.paddingRight = 24;
            Decorate(it, primary);
            it.label = ApexUi.Text(it.row, label.ToUpperInvariant(), primary ? ApexUi.DisplayStrong : ApexUi.Display, primary ? 34 : UiTokens.MenuItem, ApexUi.C(UiTokens.Ivory), 2.5f);
            if (!string.IsNullOrEmpty(detail))
            {
                var dl = ApexUi.Text(it.row, detail, ApexUi.Body, 14, ApexUi.C(UiTokens.Ivory, 0.75f), 0.2f);
                dl.style.marginTop = 2;
                if (primary) startDetail = dl;
            }
            parent.Add(it.row);
            items.Add(it);
        }

        void BuildCalendarList()
        {
            ApexUi.Caption(listContent, Loc.T("Kalender")).style.marginBottom = 8;
            foreach (var r in data.calendar)
            {
                var row = ApexUi.Panel(listContent, r.state == RoundState.Next ? UiTokens.PanelStrongAlpha : UiTokens.PanelAlpha);
                row.style.height = 64; row.style.marginBottom = 8;
                row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.Center;
                row.style.paddingLeft = 20; row.style.paddingRight = 20;
                if (r.state == RoundState.Next)
                {
                    var edge = ApexUi.Box(row, ApexUi.C(UiTokens.Cyan));
                    ApexUi.Place(edge, 0, 0, 3, -1); edge.style.bottom = 0;
                }
                ApexUi.Text(row, $"R{r.number}", ApexUi.DisplayStrong, 24, ApexUi.C(UiTokens.Steel), 1f).style.width = 56;
                ApexUi.Text(row, r.name.ToUpperInvariant(), ApexUi.DisplayStrong, 24, ApexUi.C(r.state == RoundState.Done ? UiTokens.Steel : UiTokens.Ivory), 1.5f);
                var sp = new VisualElement { pickingMode = PickingMode.Ignore }; sp.style.flexGrow = 1; row.Add(sp);
                string st = r.state == RoundState.Done ? Loc.T("GEFAHREN") : r.state == RoundState.Next ? Loc.T("NÄCHSTE RUNDE") : Loc.T("AUSSTEHEND");
                ApexUi.Text(row, st, ApexUi.BodyStrong, UiTokens.Label, ApexUi.C(r.state == RoundState.Next ? UiTokens.Cyan : UiTokens.Steel), 2f, TextAnchor.MiddleRight);
            }
            ApexUi.Text(listContent, Loc.T("Punkte ") + SetupModel.PointsScheme(), ApexUi.Body, 15, ApexUi.C(UiTokens.Steel)).style.marginTop = 10;
        }

        // --- Right side: track presentation / championship ----------------------------------------------------

        void RefreshPresentation(bool force)
        {
            string id = data.selectedTrack();
            if (!force && id == shownTrack) return;
            shownTrack = id;
            presentation.Clear();
            var info = data.track(id);
            if (info == null) return;
            var f = info.facts;
            float w = presentation.style.width.value.value;

            var head = new VisualElement { pickingMode = PickingMode.Ignore };
            ApexUi.Place(head, 0, 56, w, -1);
            presentation.Add(head);
            if (f.istanbul)
            {
                ApexUi.Text(head, "WELCOME TO ISTANBUL", ApexUi.BodyStrong, 15, ApexUi.C(UiTokens.Red), 6f);
                ApexUi.Text(head, f.name.ToUpperInvariant(), ApexUi.DisplayStrong, 64, ApexUi.C(UiTokens.Ivory), 3f).style.marginTop = 4;
                var loc = new VisualElement { pickingMode = PickingMode.Ignore };
                loc.style.flexDirection = FlexDirection.Row; loc.style.alignItems = Align.Center; loc.style.marginTop = 6;
                head.Add(loc);
                // Band in the national colours: red - white - red.
                foreach (var c in new[] { UiTokens.Red, UiTokens.Ivory, UiTokens.Red })
                {
                    var b = ApexUi.Box(loc, ApexUi.C(c));
                    b.style.width = c.Equals(UiTokens.Ivory) ? 10 : 34; b.style.height = 4; b.style.marginRight = 4;
                }
                ApexUi.Text(loc, "ISTANBUL · TÜRKIYE  —  " + Apex.Race.EventIdentity.Slogan, ApexUi.BodyStrong, 13, ApexUi.C(UiTokens.Steel), 2.5f).style.marginLeft = 10;
            }
            else
            {
                ApexUi.Caption(head, Loc.T("Strecke"));
                ApexUi.Text(head, f.name.ToUpperInvariant(), ApexUi.DisplayStrong, 64, ApexUi.C(UiTokens.Ivory), 3f).style.marginTop = 4;
            }

            // Map panel.
            var mapPanel = ApexUi.Panel(presentation, UiTokens.PanelAlpha);
            ApexUi.Place(mapPanel, 0, 210, w, 480);
            map = new TrackMapElement();
            ApexUi.Place(map, 0, 0, w, f.cornerNames.Count > 0 ? 420 : 480);
            mapPanel.Add(map);
            map.Set(TrackMap.Fit(info.def, info.geometry, w, f.cornerNames.Count > 0 ? 420 : 480, 34f, 360), 4f, true, Color.white, true);
            map.Progress = ReducedMotion ? 1f : 0f;
            mapTime = 0f;
            var legend = new VisualElement { pickingMode = PickingMode.Ignore };
            ApexUi.Place(legend, 20, 16);
            legend.style.flexDirection = FlexDirection.Row;
            mapPanel.Add(legend);
            var sc = new[] { UiPalette.Current.sector1, UiPalette.Current.sector2, UiPalette.Current.sector3 };
            for (int s = 0; s < Mathf.Min(f.sectors, 3); s++)
            {
                var sw = ApexUi.Box(legend, ApexUi.C(sc[s])); sw.style.width = 14; sw.style.height = 3; sw.style.marginTop = 7; sw.style.marginRight = 6;
                ApexUi.Text(legend, $"S{s + 1}", ApexUi.BodyStrong, 12, ApexUi.C(UiTokens.Steel), 1.5f).style.marginRight = 14;
            }
            if (f.cornerNames.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                for (int k = 0; k < f.cornerNames.Count; k++) sb.Append(k == 0 ? "" : "  ·  ").Append(k + 1).Append(' ').Append(f.cornerNames[k]);
                var names = ApexUi.Text(mapPanel, sb.ToString(), ApexUi.Body, 13, ApexUi.C(UiTokens.Steel));
                names.style.whiteSpace = WhiteSpace.Normal;
                ApexUi.Place(names, 20, 416, w - 40, 60);
            }

            // Facts.
            var facts = new VisualElement { pickingMode = PickingMode.Ignore };
            ApexUi.Place(facts, 0, 706, w, 104);
            facts.style.flexDirection = FlexDirection.Row;
            presentation.Add(facts);
            float tw = (w - 4 * 12f) / 5f;
            Fact(facts, tw, Loc.T("Länge"), $"{f.lengthKm:0.00} km".Replace('.', ','));
            Fact(facts, tw, Loc.T("Kurven"), f.corners.ToString());
            Fact(facts, tw, Loc.T("Richtung"), f.clockwise ? Loc.T("Uhrzeigersinn") : Loc.T("Gegen Uhrzeiger"));
            Fact(facts, tw, Loc.T("Höhenunterschied"), $"{f.elevationRange:0} m");
            Fact(facts, tw, Loc.T("Deine Bestzeit"), f.bestLap > 0 ? MainMenuModel.FormatLap(f.bestLap) : "–", f.bestLap > 0);

            // Track cards (pointer shortcut for the "Strecke" option).
            var cards = new VisualElement { pickingMode = PickingMode.Ignore };
            ApexUi.Place(cards, 0, 830, w, 170);
            cards.style.flexDirection = FlexDirection.Row;
            presentation.Add(cards);
            float cw = Mathf.Min(300f, (w - (data.trackIds.Count - 1) * 12f) / Mathf.Max(1, data.trackIds.Count));
            foreach (var tid in data.trackIds)
            {
                var ti = data.track(tid);
                if (ti == null) continue;
                bool sel = tid == id;
                var card = ApexUi.Panel(cards, sel ? UiTokens.PanelStrongAlpha : UiTokens.PanelAlpha);
                card.pickingMode = PickingMode.Position;
                card.userData = new TrackCard { id = tid };
                card.style.width = cw; card.style.height = 160; card.style.marginRight = 12;
                if (sel)
                {
                    var e = ApexUi.Box(card, ApexUi.C(ti.facts.istanbul ? UiTokens.Red : UiTokens.Cyan));
                    ApexUi.Place(e, 0, 0, -1, 3); e.style.right = 0;
                }
                var mini = new TrackMapElement();
                ApexUi.Place(mini, 0, 8, cw, 100);
                card.Add(mini);
                mini.Set(TrackMap.Fit(ti.def, ti.geometry, cw, 100, 14f, 160), 2f, false, sel ? Color.white : ApexUi.C(UiTokens.Steel), false);
                var nl = ApexUi.Text(card, ti.facts.name.ToUpperInvariant(), ApexUi.DisplayStrong, 18, ApexUi.C(sel ? UiTokens.Ivory : UiTokens.Steel), 1f);
                ApexUi.Place(nl, 14, 112, cw - 28, 24);
                var ml = ApexUi.Text(card, $"{ti.facts.lengthKm:0.00} km · {ti.facts.corners} Kurven".Replace('.', ','), ApexUi.Body, 13, ApexUi.C(UiTokens.Steel));
                ApexUi.Place(ml, 14, 134, cw - 28, 18);
                trackCards.Add((card, tid));
            }
        }

        static void Fact(VisualElement parent, float w, string caption, string value, bool best = false)
        {
            var c = ApexUi.Panel(parent, UiTokens.PanelAlpha);
            c.style.width = w; c.style.height = 100; c.style.marginRight = 12;
            c.style.paddingLeft = 18; c.style.paddingTop = 16;
            ApexUi.Caption(c, caption);
            ApexUi.Text(c, value, ApexUi.DisplayStrong, 30, best ? ApexUi.C(UiPalette.Current.best) : ApexUi.C(UiTokens.Ivory)).style.marginTop = 8;
        }

        void BuildChampionshipRight()
        {
            float w = presentation.style.width.value.value;
            if (!data.championshipActive)
            {
                // Season preview: calendar cards with the real maps, points scheme.
                var head = new VisualElement { pickingMode = PickingMode.Ignore };
                ApexUi.Place(head, 0, 56, w, -1);
                presentation.Add(head);
                ApexUi.Caption(head, Loc.T("Kalender"));
                ApexUi.Text(head, Loc.Format("{0} RENNEN", data.calendar.Count), ApexUi.DisplayStrong, 64, ApexUi.C(UiTokens.Ivory), 3f).style.marginTop = 4;
                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                ApexUi.Place(row, 0, 210, w, 420);
                row.style.flexDirection = FlexDirection.Row;
                presentation.Add(row);
                float cw = (w - (data.calendar.Count - 1) * 14f) / Mathf.Max(1, data.calendar.Count);
                foreach (var r in data.calendar)
                {
                    var ti = data.track(r.trackId);
                    var card = ApexUi.Panel(row, UiTokens.PanelAlpha);
                    card.style.width = cw; card.style.height = 420; card.style.marginRight = 14;
                    if (ti != null && ti.facts.istanbul)
                    {
                        var e = ApexUi.Box(card, ApexUi.C(UiTokens.Red));
                        ApexUi.Place(e, 0, 0, -1, 3); e.style.right = 0;
                    }
                    var cap = ApexUi.Caption(card, Loc.Format("Runde {0}", r.number));
                    ApexUi.Place(cap, 22, 20);
                    var nm = ApexUi.Text(card, r.name.ToUpperInvariant(), ApexUi.DisplayStrong, 28, ApexUi.C(UiTokens.Ivory), 1.5f);
                    ApexUi.Place(nm, 22, 42);
                    if (ti == null) continue;
                    var mini = new TrackMapElement();
                    ApexUi.Place(mini, 0, 90, cw, 270);
                    card.Add(mini);
                    mini.Set(TrackMap.Fit(ti.def, ti.geometry, cw, 270, 30f, 260), 3f, true, Color.white, false);
                    var meta = ApexUi.Text(card, $"{ti.facts.lengthKm:0.00} km · {ti.facts.corners} Kurven · {ti.facts.Direction}".Replace('.', ','), ApexUi.Body, 14, ApexUi.C(UiTokens.Steel));
                    ApexUi.Place(meta, 22, 376);
                }
                var pts = ApexUi.Text(presentation, Loc.T("Punkte ") + SetupModel.PointsScheme(), ApexUi.Body, 16, ApexUi.C(UiTokens.Steel));
                ApexUi.Place(pts, 0, 660);
                return;
            }
            // Standings.
            var panel = ApexUi.Panel(presentation, UiTokens.PanelStrongAlpha);
            ApexUi.Place(panel, 0, 56, w, 940);
            panel.style.paddingLeft = 28; panel.style.paddingRight = 28; panel.style.paddingTop = 22;
            ApexUi.Caption(panel, Loc.T("Fahrerwertung"));
            foreach (var s in data.standings)
            {
                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.Center;
                row.style.height = 52; row.style.marginTop = 6;
                panel.Add(row);
                if (s.player)
                {
                    var hl = ApexUi.Box(row, Color.clear);
                    ApexUi.Place(hl, -12, 0); hl.style.right = -12; hl.style.bottom = 0;
                    ApexUi.Background(hl, ApexUi.Ramp(ApexUi.C(UiTokens.Cyan), 0.22f, 0.02f));
                    var e = ApexUi.Box(row, ApexUi.C(UiTokens.Cyan));
                    ApexUi.Place(e, -12, 0, 3, -1); e.style.bottom = 0;
                }
                ApexUi.Text(row, s.position.ToString(), ApexUi.DisplayStrong, 26, ApexUi.C(s.position <= 3 ? UiTokens.Ivory : UiTokens.Steel), 0f, TextAnchor.MiddleRight).style.width = 44;
                var team = Roster.Teams[Mathf.Clamp(s.team, 0, Roster.Teams.Length - 1)];
                var bar = ApexUi.Box(row, new Color(team.r, team.g, team.b));
                bar.style.width = 4; bar.style.height = 28; bar.style.marginLeft = 18; bar.style.marginRight = 16;
                ApexUi.Text(row, s.name, s.player ? ApexUi.BodyStrong : ApexUi.Body, 19, ApexUi.C(UiTokens.Ivory));
                ApexUi.Text(row, "  " + team.name, ApexUi.Body, 14, ApexUi.C(UiTokens.Steel));
                var sp = new VisualElement { pickingMode = PickingMode.Ignore }; sp.style.flexGrow = 1; row.Add(sp);
                ApexUi.Text(row, $"{s.points} PKT", ApexUi.DisplayStrong, 24, ApexUi.C(s.player ? UiTokens.Cyan : UiTokens.Ivory), 1f, TextAnchor.MiddleRight);
            }
        }

        // --- Input --------------------------------------------------------------------------------------------

        /// <summary>Back (Esc / B): disarms a pending "abandon" first, otherwise leaves the screen.</summary>
        public void Back()
        {
            if (abandonArmed) { abandonArmed = false; RefreshAbandon(); sounds.Play(UiSounds.Cue.Back); return; }
            sounds.Play(UiSounds.Cue.Back);
            BackRequested?.Invoke();
        }

        public void Tick(float dt, bool up, bool down, bool left, bool right, bool confirm)
        {
            if (!Visible || data == null) return;
            screenTime += dt;
            ui.ReadPointer(out var pos, out bool moved, out bool released);
            int count = items.Count;
            if ((up || down) && count > 0)
            {
                SetFocus(((focus + (down ? 1 : -1)) % count + count) % count);
            }
            var cur = count > 0 ? items[Mathf.Clamp(focus, 0, count - 1)] : null;
            if (cur?.option != null && (left || right)) Change(cur, right ? 1 : -1);
            else if (cur != null && cur.option == null && (left || right))
            {
                // Buttons sit side by side: left/right moves between them.
                int i = focus + (right ? 1 : -1);
                if (i >= 0 && i < count && items[i].option == null) SetFocus(i);
            }

            var hovered = ui.PickData<Item>(pos);
            if (hovered != null && moved) { int i = items.IndexOf(hovered); if (i != focus) SetFocus(i); }
            if (released)
            {
                var arrow = ui.PickData<Arrow>(pos);
                var card = ui.PickData<TrackCard>(pos);
                if (arrow != null) { SetFocus(items.IndexOf(arrow.item)); Change(arrow.item, arrow.dir); }
                else if (card != null && card.id != data.selectedTrack()) { sounds.Play(UiSounds.Cue.Confirm); data.selectTrack(card.id); OptionChanged?.Invoke(); Refresh(); }
                else if (hovered != null) Activate(hovered);
            }
            else if (confirm && cur != null) Activate(cur);

            // Mouse wheel scrolls the option list.
            var mouse = Mouse.current;
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f) scrollTarget -= Mathf.Sign(wheel) * RowH * 1.5f;
            }
            if (up || down || left || right || confirm || released) screenTime = Mathf.Max(screenTime, 2f);
            Animate(dt);
        }

        void SetFocus(int i)
        {
            if (i < 0 || i >= items.Count || i == focus) return;
            focus = i;
            if (abandonArmed) { abandonArmed = false; RefreshAbandon(); }
            sounds.Play(UiSounds.Cue.Focus);
            ApplyFocus(false);
            // Keep the focused option inside the list viewport.
            var it = items[i];
            if (it.option != null && !float.IsNaN(it.row.layout.y))
            {
                float top = it.row.layout.y, bottom = top + it.row.layout.height;
                if (top - 40f < scrollTarget) scrollTarget = top - 40f;
                if (bottom + 12f > scrollTarget + ListHeight) scrollTarget = bottom + 12f - ListHeight;
            }
        }

        void Activate(Item it)
        {
            if (it.option != null) { Change(it, 1); return; }
            sounds.Play(it.primary ? UiSounds.Cue.Start : UiSounds.Cue.Confirm);
            it.action?.Invoke();
        }

        void Change(Item it, int dir)
        {
            it.option.change(dir);
            sounds.Play(UiSounds.Cue.Focus);
            OptionChanged?.Invoke();
            Refresh();
        }

        /// <summary>After a change: rebuild when the visible options changed, otherwise update the values.</summary>
        void Refresh()
        {
            if (data.mode != SetupMode.Championship || !data.championshipActive)
            {
                var opts = data.options();
                if (Signature(opts) != optionSignature)
                {
                    string key = focus < items.Count ? items[focus].option?.key : null;
                    float keep = scrollTarget;
                    Build();
                    int i = items.FindIndex(x => x.option != null && x.option.key == key);
                    focus = i >= 0 ? i : 0;
                    ApplyFocus(true);
                    scroll = scrollTarget = keep;
                    screenTime = 2f;
                    if (map != null) map.Progress = 1f;   // same track: no second draw-in
                    return;
                }
            }
            foreach (var it in items) if (it.option != null) it.value.text = it.option.value();
            if (startDetail != null) startDetail.text = data.summary();
            if (data.mode != SetupMode.Championship) RefreshPresentation(false);
        }

        void Abandon()
        {
            if (!abandonArmed) { abandonArmed = true; RefreshAbandon(); return; }
            abandonArmed = false;
            AbandonRequested?.Invoke();
        }

        void RefreshAbandon()
        {
            foreach (var it in items)
                if (it.option == null && it.action == (Action)Abandon)
                {
                    it.label.text = abandonArmed ? Loc.T("WIRKLICH AUFGEBEN?") : Loc.T(Loc.T("Aufgeben")).ToUpperInvariant();
                    it.label.style.color = abandonArmed ? ApexUi.C(UiTokens.Amber) : ApexUi.C(UiTokens.Ivory);
                }
        }

        void ApplyFocus(bool instant)
        {
            for (int i = 0; i < items.Count; i++)
            {
                float t = i == focus ? 1f : 0f;
                if (instant) items[i].focus = Tween.Start(t, t, 0f);
                else items[i].focus.Retarget(t, UiTokens.FocusTime);
            }
        }

        void Animate(float dt)
        {
            float maxScroll = Mathf.Max(0f, listContent.layout.height - ListHeight);
            if (float.IsNaN(maxScroll)) maxScroll = 0f;
            scrollTarget = Mathf.Clamp(scrollTarget, 0f, maxScroll);
            scroll = ReducedMotion ? scrollTarget : Mathf.Lerp(scroll, scrollTarget, 1f - Mathf.Exp(-dt * 18f));
            ApexUi.Translate(listContent, 0f, -scroll);

            float slide = ReducedMotion ? 0f : UiTokens.SlideDistance;
            float enterL = UiMotion.Staggered(screenTime, 0, UiTokens.ScreenTime, 0f);
            float enterR = UiMotion.Staggered(screenTime, 3, UiTokens.ScreenTime, UiTokens.StaggerStep);
            listViewport.style.opacity = enterL;
            ApexUi.Translate(listViewport, -slide * (1f - enterL), 0f);
            presentation.style.opacity = enterR;
            ApexUi.Translate(presentation, slide * (1f - enterR), 0f);
            if (map != null && map.Progress < 1f)
            {
                mapTime += dt;
                map.Progress = ReducedMotion ? 1f : UiMotion.InOutCubic(Mathf.Clamp01(mapTime / 1.1f));
            }
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                it.focus.Advance(dt);
                float f = it.focus.Value;
                it.highlight.style.opacity = it.primary ? 0.5f + 0.5f * f : f;
                it.edge.style.opacity = it.primary ? 1f : f;
                if (it.option != null) it.label.style.color = Color.Lerp(ApexUi.C(UiTokens.Ivory, 0.75f), ApexUi.C(UiTokens.Ivory), f);
                else if (!ReducedMotion && it.primary) ApexUi.Scale(it.row, 1f + 0.02f * f);
            }
        }
    }
}
