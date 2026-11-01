using System;
using System.Collections.Generic;
using Apex.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apex.Game
{
    /// <summary>
    /// Results screen (docs/UI_UX_OVERHAUL.md section 4): headline with the player's result, Grand Prix weekend steps
    /// (qualifying → grid → race), the classification table, XP and level, championship standings and the next
    /// steps. After a top-3 finish on a circuit with a podium the podium shot plays first (any input skips it).
    /// </summary>
    public sealed class ResultsView
    {
        public sealed class Data
        {
            public string session = "", title = "", sub = "";
            public bool win;
            public List<WeekendStep> weekend = new List<WeekendStep>();
            public readonly List<string[]> rows = new List<string[]>();
            public readonly List<Color> teams = new List<Color>();
            public int playerRow = -1;
            public bool race;
            public string xp = "", levelUp = "";
            public string standingsTitle = "";
            public List<StandingRow> standings;
            public List<(ResultAction action, string label)> actions = new List<(ResultAction, string)>();
            public bool podium;
        }

        public event Action<ResultAction> Chosen;
        public bool ReducedMotion;

        readonly ApexUi ui;
        readonly UiSounds sounds;
        readonly FocusList list;
        VisualElement layer, head, table, side, footer, shade;
        float time;
        bool podium;

        public ResultsView(UiSounds sounds)
        {
            this.sounds = sounds;
            ui = new ApexUi("ApexResults", 12);
            list = new FocusList(sounds) { Horizontal = true };
            ui.SetVisible(false);
        }

        public bool Visible { get; private set; }

        public void Hide()
        {
            if (!Visible) return;
            Visible = false;
            ui.SetVisible(false);
        }

        /// <summary>True while the podium shot is on screen (the game holds the podium camera).</summary>
        public bool PodiumShot => Visible && podium && time < ResultsModel.PodiumSeconds;

        public void Show(Data d)
        {
            Visible = true;
            ui.SetVisible(true);
            layer?.RemoveFromHierarchy();
            list.Clear();
            podium = d.podium;
            float m = UiTokens.ScreenMargin;
            layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute; layer.style.left = 0; layer.style.right = 0; layer.style.top = 0; layer.style.bottom = 0;
            ui.root.Add(layer);
            shade = ApexUi.Box(layer, ApexUi.C(UiTokens.Navy, 0.9f));
            shade.style.position = Position.Absolute; shade.style.left = 0; shade.style.right = 0; shade.style.top = 0; shade.style.bottom = 0;

            head = new VisualElement { pickingMode = PickingMode.Ignore };
            head.style.position = Position.Absolute; head.style.left = m; head.style.top = 48;
            layer.Add(head);
            ApexUi.Caption(head, d.session);
            var titleRow = new VisualElement { pickingMode = PickingMode.Ignore };
            titleRow.style.flexDirection = FlexDirection.Row; titleRow.style.alignItems = Align.FlexEnd;
            head.Add(titleRow);
            ApexUi.Text(titleRow, d.title, ApexUi.DisplayStrong, UiTokens.Display, d.win ? ApexUi.C(UiPalette.Current.best) : ApexUi.C(UiTokens.Ivory), 5f);
            var sub = ApexUi.Text(titleRow, d.sub, ApexUi.Display, 30, ApexUi.C(UiTokens.Steel), 1.5f);
            sub.style.marginLeft = 24; sub.style.marginBottom = 16;
            var red = ApexUi.Box(head, ApexUi.C(UiTokens.Red)); red.style.width = 64; red.style.height = 3;
            if (d.weekend.Count > 0)
            {
                // Grand Prix weekend: qualifying ✓ → grid → race.
                var steps = new VisualElement { pickingMode = PickingMode.Ignore };
                steps.style.flexDirection = FlexDirection.Row; steps.style.marginTop = 18; steps.style.alignItems = Align.Center;
                head.Add(steps);
                ApexUi.Caption(steps, Loc.T("Rennwochenende")).style.marginRight = 20;
                for (int i = 0; i < d.weekend.Count; i++)
                {
                    var s = d.weekend[i];
                    var chip = ApexUi.Panel(steps, s.current ? UiTokens.PanelStrongAlpha : UiTokens.PanelAlpha);
                    chip.style.flexDirection = FlexDirection.Row; chip.style.alignItems = Align.Center;
                    chip.style.paddingLeft = 14; chip.style.paddingRight = 16; chip.style.height = 34;
                    var dot = ApexUi.Box(chip, s.done ? ApexUi.C(UiPalette.Current.good) : s.current ? ApexUi.C(UiTokens.Cyan) : ApexUi.C(UiTokens.Surface));
                    dot.style.width = 8; dot.style.height = 8; dot.style.marginRight = 10;
                    ApexUi.Text(chip, (s.done ? "✓ " : "") + s.name.ToUpperInvariant(), ApexUi.BodyStrong, 13, s.current ? ApexUi.C(UiTokens.Cyan) : ApexUi.C(UiTokens.Ivory), 2f);
                    if (i < d.weekend.Count - 1) ApexUi.Text(steps, "  ›  ", ApexUi.DisplayStrong, 18, ApexUi.C(UiTokens.Steel));
                }
            }

            // Classification.
            bool hasSide = d.standings != null && d.standings.Count > 0;
            float tableW = hasSide ? 1180f : 1920f - 2f * m;
            table = ApexUi.Panel(layer, UiTokens.PanelStrongAlpha);
            table.style.position = Position.Absolute; table.style.left = m; table.style.top = 290; table.style.width = tableW;
            table.style.paddingLeft = 20; table.style.paddingRight = 20; table.style.paddingTop = 12; table.style.paddingBottom = 12;
            float[] cols = { 50, 64, 260, 230, 170, 140, 70, 80, 60 };
            string[] heads = { "Pos", "", "Fahrer", "Team", "Zeit / Abstand", "Beste Runde", "Stopps", "Strafe", "Pkt." };
            float scale = (tableW - 40f) / 1124f;
            Row(table, heads, cols, scale, true, false, Color.clear, 30);
            int n = d.rows.Count;
            float rowH = Mathf.Clamp(560f / Mathf.Max(1, n), 24f, 36f);
            for (int i = 0; i < n; i++)
                Row(table, d.rows[i], cols, scale, false, i == d.playerRow, i < d.teams.Count ? d.teams[i] : Color.clear, rowH);

            if (hasSide)
            {
                side = ApexUi.Panel(layer, UiTokens.PanelStrongAlpha);
                side.style.position = Position.Absolute; side.style.right = m; side.style.top = 290; side.style.width = 1920f - 2f * m - tableW - 24f;
                side.style.paddingLeft = 22; side.style.paddingRight = 22; side.style.paddingTop = 16; side.style.paddingBottom = 16;
                ApexUi.Caption(side, d.standingsTitle);
                foreach (var s in d.standings)
                {
                    var r = new VisualElement { pickingMode = PickingMode.Ignore };
                    r.style.flexDirection = FlexDirection.Row; r.style.alignItems = Align.Center; r.style.height = 36; r.style.marginTop = 4;
                    side.Add(r);
                    if (s.player)
                    {
                        var hl = ApexUi.Box(r, Color.clear);
                        ApexUi.Place(hl, -10, 0); hl.style.right = -10; hl.style.bottom = 0;
                        ApexUi.Background(hl, ApexUi.Ramp(ApexUi.C(UiTokens.Cyan), 0.22f, 0.02f));
                    }
                    ApexUi.Text(r, s.position.ToString(), ApexUi.DisplayStrong, 20, ApexUi.C(UiTokens.Ivory), 0f, TextAnchor.MiddleRight).style.width = 34;
                    var team = Apex.Core.Roster.Teams[Mathf.Clamp(s.team, 0, Apex.Core.Roster.Teams.Length - 1)];
                    var bar = ApexUi.Box(r, new Color(team.r, team.g, team.b)); bar.style.width = 3; bar.style.height = 20; bar.style.marginLeft = 14; bar.style.marginRight = 12;
                    ApexUi.Text(r, s.name, s.player ? ApexUi.BodyStrong : ApexUi.Body, 17, ApexUi.C(UiTokens.Ivory));
                    var sp = new VisualElement { pickingMode = PickingMode.Ignore }; sp.style.flexGrow = 1; r.Add(sp);
                    ApexUi.Text(r, s.points.ToString(), ApexUi.DisplayStrong, 20, s.player ? ApexUi.C(UiTokens.Cyan) : ApexUi.C(UiTokens.Ivory), 0f, TextAnchor.MiddleRight);
                }
            }

            // XP and next steps.
            footer = new VisualElement { pickingMode = PickingMode.Ignore };
            footer.style.position = Position.Absolute; footer.style.left = m; footer.style.right = m; footer.style.bottom = 40;
            layer.Add(footer);
            var xp = new VisualElement { pickingMode = PickingMode.Ignore };
            xp.style.flexDirection = FlexDirection.Row; xp.style.marginBottom = 14;
            footer.Add(xp);
            ApexUi.Text(xp, d.xp, ApexUi.DisplayStrong, 26, ApexUi.C(UiPalette.Current.good), 1.5f);
            if (!string.IsNullOrEmpty(d.levelUp)) ApexUi.Text(xp, "   " + d.levelUp, ApexUi.DisplayStrong, 26, ApexUi.C(UiPalette.Current.best), 1.5f);
            var buttons = new VisualElement { pickingMode = PickingMode.Ignore };
            buttons.style.flexDirection = FlexDirection.Row;
            footer.Add(buttons);
            for (int i = 0; i < d.actions.Count; i++)
            {
                var a = d.actions[i].action;
                var it = list.AddButton(buttons, d.actions[i].label, "", i == 0, () => Chosen?.Invoke(a), 72f);
                it.row.style.minWidth = i == 0 ? 420 : 300; it.row.style.marginRight = 14;
            }
            list.ApplyFocus(true);
            time = 0f;
            sounds.Play(podium ? UiSounds.Cue.Start : UiSounds.Cue.Transition);
        }

        static void Row(VisualElement parent, string[] cells, float[] cols, float scale, bool header, bool player, Color team, float h)
        {
            var r = new VisualElement { pickingMode = PickingMode.Ignore };
            r.style.flexDirection = FlexDirection.Row; r.style.alignItems = Align.Center; r.style.height = h;
            parent.Add(r);
            if (player)
            {
                var hl = ApexUi.Box(r, Color.clear);
                ApexUi.Place(hl, -10, 0); hl.style.right = -10; hl.style.bottom = 0;
                ApexUi.Background(hl, ApexUi.Ramp(ApexUi.C(UiTokens.Cyan), 0.26f, 0.04f));
                var e = ApexUi.Box(hl, ApexUi.C(UiTokens.Cyan)); ApexUi.Place(e, 0, 0, 3, -1); e.style.bottom = 0;
            }
            int size = header ? 12 : Mathf.RoundToInt(Mathf.Clamp(h * 0.5f, 13f, 17f));
            for (int c = 0; c < cols.Length && c < cells.Length; c++)
            {
                var cell = new VisualElement { pickingMode = PickingMode.Ignore };
                cell.style.width = cols[c] * scale; cell.style.flexDirection = FlexDirection.Row; cell.style.alignItems = Align.Center;
                r.Add(cell);
                if (!header && c == 1 && team.a > 0f)
                {
                    var bar = ApexUi.Box(cell, team); bar.style.width = 3; bar.style.height = h * 0.55f; bar.style.marginRight = 10;
                }
                string text = header ? Loc.T(cells[c]).ToUpperInvariant() : cells[c];
                var l = ApexUi.Text(cell, text, header ? ApexUi.BodyStrong : c == 0 || c == 1 ? ApexUi.DisplayStrong : c >= 4 && c <= 5 ? ApexUi.Display : ApexUi.Body,
                    header ? 12 : c <= 1 ? size + 3 : size, header ? ApexUi.C(UiTokens.Steel) : player ? ApexUi.C(UiTokens.Cyan) : ApexUi.C(UiTokens.Ivory), header ? 1.5f : 0.3f);
                l.style.overflow = Overflow.Hidden;
            }
        }

        public void Tick(float dt, bool left, bool right, bool confirm, bool anyKey)
        {
            if (!Visible) return;
            time += dt;
            ui.ReadPointer(out var pos, out bool moved, out bool released);
            bool shot = PodiumShot;
            if (shot)
            {
                // Podium moment: only the headline over the podium camera; any input skips to the table.
                if (anyKey || released || confirm) time = ResultsModel.PodiumSeconds;
                shade.style.opacity = 0f;
                table.style.opacity = 0f; footer.style.opacity = 0f;
                if (side != null) side.style.opacity = 0f;
                head.style.top = 760;
                return;
            }
            head.style.top = 48;
            float t0 = podium ? ResultsModel.PodiumSeconds : 0f;
            float enter = UiMotion.OutCubic(Mathf.Clamp01((time - t0) / UiTokens.ScreenTime));
            shade.style.opacity = enter;
            table.style.opacity = enter; footer.style.opacity = enter;
            if (side != null) side.style.opacity = enter;
            ApexUi.Translate(table, 0f, ReducedMotion ? 0f : 24f * (1f - enter));
            int before = list.Focus;
            if (!list.Pointer(ui, pos, moved, released)) list.Navigate(false, false, left, right, confirm);
            if (list.Focus != before) list.ApplyFocus(false);
            list.Animate(dt, ReducedMotion);
        }
    }
}
