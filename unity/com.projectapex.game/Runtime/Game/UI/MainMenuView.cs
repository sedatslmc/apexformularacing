using System;
using System.Collections.Generic;
using Apex.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apex.Game
{
    /// <summary>
    /// Main menu of APEX // RACE CONTROL (docs/UI_UX_OVERHAUL.md section 4, mockup ui_mock_main_menu.jpg), over the
    /// 3D showroom: menu on the left with the highlighted "RENNEN STARTEN", profile chip and settings at the top,
    /// track and last-result cards at the bottom, control hints and version. Also the boot overlay (logo while the
    /// menu backdrop loads) and the driver profile screen. Entrances are staggered and skippable; with reduced
    /// motion only fades remain.
    /// </summary>
    public sealed class MainMenuView
    {
        public sealed class Data
        {
            public List<MenuEntry> entries = new List<MenuEntry>();
            public int carNumber;
            public string driver = "Fahrer", team = "";
            public int level;
            public float levelProgress;
            public string trackName = "", trackMeta = "";
            public string lastTitle = "", lastMeta = "";
            public string version = "";
            public bool mobile;
            public Profile profile = new Profile();
        }

        public sealed class Profile
        {
            public string name = "", nationality = "", team = "";
            public int number, level, races, wins, podiums, poles;
            public long xp, xpNext;
            public double distanceKm;
            public float averageFinish;   // 0 = no finished race yet
            public List<(string track, string time)> bestLaps = new List<(string, string)>();
        }

        public event Action<MenuAction> Activated;
        public event Action SettingsRequested;
        public bool ReducedMotion;

        readonly ApexUi ui;
        readonly UiSounds sounds;
        readonly UiNavigator nav = new UiNavigator(UiScreen.Boot);
        VisualElement menuLayer, profileLayer, bootLayer, items, settingsButton;
        Label bootStatus;
        readonly List<ItemView> itemViews = new List<ItemView>();
        Data data;
        float screenTime, bootFade = 1f;
        bool booting = true;

        sealed class ItemView
        {
            public MenuEntry entry;
            public VisualElement row, highlight, edge;
            public Label label, detail;
            public Tween focus;
        }

        public MainMenuView(UiSounds sounds)
        {
            this.sounds = sounds;
            ui = new ApexUi("ApexMainMenu", 10);
            BuildBoot();
        }

        public UiScreen Screen => nav.Current;
        public bool Visible { get; private set; } = true;

        public void SetVisible(bool on)
        {
            if (on == Visible) return;
            Visible = on;
            ui.SetVisible(on);
            if (on) screenTime = 0f;
        }

        // --- Boot ---------------------------------------------------------------------------------------------

        void BuildBoot()
        {
            bootLayer = ApexUi.Box(ui.root, ApexUi.C(UiTokens.Navy));
            ApexUi.Place(bootLayer, 0, 0);
            bootLayer.style.right = 0; bootLayer.style.bottom = 0;
            var centre = new VisualElement { pickingMode = PickingMode.Ignore };
            centre.style.position = Position.Absolute;
            centre.style.left = 0; centre.style.right = 0; centre.style.top = 0; centre.style.bottom = 0;
            centre.style.alignItems = Align.Center;
            centre.style.justifyContent = Justify.Center;
            bootLayer.Add(centre);
            var logo = new VisualElement { pickingMode = PickingMode.Ignore };
            logo.style.flexDirection = FlexDirection.Row;
            centre.Add(logo);
            ApexUi.Text(logo, "AP", ApexUi.DisplayStrong, 96, ApexUi.C(UiTokens.Ivory), 30f);
            ApexUi.Text(logo, "E", ApexUi.DisplayStrong, 96, ApexUi.C(UiTokens.Cyan), 30f);
            ApexUi.Text(logo, "X", ApexUi.DisplayStrong, 96, ApexUi.C(UiTokens.Ivory), 30f);
            ApexUi.Text(centre, "RACE CONTROL", ApexUi.BodyStrong, 15, ApexUi.C(UiTokens.Steel), 8f).style.marginTop = 6;
            var line = ApexUi.Box(centre, ApexUi.C(UiTokens.Red));
            line.style.width = 120; line.style.height = 2; line.style.marginTop = 26;
            bootStatus = ApexUi.Text(centre, "", ApexUi.Body, 15, ApexUi.C(UiTokens.Steel), 1.5f);
            bootStatus.style.marginTop = 18;
        }

        /// <summary>Shows the boot overlay with a status line (e.g. while the Istanbul backdrop loads).</summary>
        public void ShowBoot(string status)
        {
            booting = true;
            bootFade = 1f;
            bootLayer.style.display = DisplayStyle.Flex;
            bootLayer.style.opacity = 1f;
            bootStatus.text = status;
            nav.Reset(UiScreen.Boot);
        }

        /// <summary>Ends the boot overlay: it fades out and the menu enters.</summary>
        public void EndBoot()
        {
            booting = false;
            nav.Reset(UiScreen.MainMenu);
            screenTime = 0f;
            sounds.Play(UiSounds.Cue.Transition);
        }

        // --- Main menu ----------------------------------------------------------------------------------------

        public void Show(Data d)
        {
            data = d;
            menuLayer?.RemoveFromHierarchy();
            profileLayer?.RemoveFromHierarchy();
            profileLayer = null;
            itemViews.Clear();
            menuLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            menuLayer.style.position = Position.Absolute;
            menuLayer.style.left = 0; menuLayer.style.right = 0; menuLayer.style.top = 0; menuLayer.style.bottom = 0;
            ui.root.Insert(0, menuLayer);
            float m = d.mobile ? UiTokens.MobileMargin + 24f : UiTokens.ScreenMargin;

            // Shades over the 3D scene: left for the menu, top and bottom for the bars.
            var shadeL = ApexUi.Box(menuLayer, Color.clear);
            ApexUi.Place(shadeL, 0, 0, 1180, -1); shadeL.style.bottom = 0;
            ApexUi.Background(shadeL, ApexUi.Ramp(ApexUi.C(UiTokens.Navy), 0.94f, 0f, false, 0.25f));
            var shadeB = ApexUi.Box(menuLayer, Color.clear);
            ApexUi.Place(shadeB, 0, 820); shadeB.style.right = 0; shadeB.style.bottom = 0;
            ApexUi.Background(shadeB, ApexUi.Ramp(ApexUi.C(UiTokens.Navy), 0f, 0.9f, true));
            var shadeT = ApexUi.Box(menuLayer, Color.clear);
            ApexUi.Place(shadeT, 0, 0, -1, 170); shadeT.style.right = 0;
            ApexUi.Background(shadeT, ApexUi.Ramp(ApexUi.C(UiTokens.Navy), 0.75f, 0f, true));

            // Top bar: logo, profile chip, settings.
            var logo = new VisualElement { pickingMode = PickingMode.Ignore };
            ApexUi.Place(logo, m, 40);
            var word = new VisualElement { pickingMode = PickingMode.Ignore };
            word.style.flexDirection = FlexDirection.Row;
            logo.Add(word);
            ApexUi.Text(word, "AP", ApexUi.DisplayStrong, 34, ApexUi.C(UiTokens.Ivory), 11f);
            ApexUi.Text(word, "E", ApexUi.DisplayStrong, 34, ApexUi.C(UiTokens.Cyan), 11f);
            ApexUi.Text(word, "X", ApexUi.DisplayStrong, 34, ApexUi.C(UiTokens.Ivory), 11f);
            ApexUi.Text(logo, "RACE CONTROL", ApexUi.BodyStrong, 11, ApexUi.C(UiTokens.Steel), 4.5f);
            menuLayer.Add(logo);

            settingsButton = ApexUi.Panel(menuLayer, UiTokens.PanelAlpha);
            settingsButton.pickingMode = PickingMode.Position;
            ApexUi.Place(settingsButton, 1920 - m - 48, 40, 48, 48);
            settingsButton.style.alignItems = Align.Center; settingsButton.style.justifyContent = Justify.Center;
            settingsButton.userData = "settings";
            // Settings icon: three slider bars with knobs (drawn, no glyph needed).
            for (int k = 0; k < 3; k++)
            {
                var bar = ApexUi.Box(settingsButton, ApexUi.C(UiTokens.Ivory, 0.85f));
                ApexUi.Place(bar, 13, 15 + k * 8, 22, 2);
                var knob = ApexUi.Box(settingsButton, ApexUi.C(UiTokens.Ivory));
                ApexUi.Place(knob, 15 + (k * 7) % 14, 13 + k * 8, 6, 6);
            }

            var chip = ApexUi.Panel(menuLayer, UiTokens.PanelAlpha);
            ApexUi.Place(chip, 1920 - m - 48 - 14 - 300, 40, 300, 48);
            chip.style.flexDirection = FlexDirection.Row; chip.style.alignItems = Align.Center;
            var num = ApexUi.Box(chip, ApexUi.C(UiTokens.Surface));
            num.style.width = 36; num.style.height = 36; num.style.marginLeft = 6; num.style.marginRight = 12;
            num.style.borderLeftWidth = 1; num.style.borderRightWidth = 1; num.style.borderTopWidth = 1; num.style.borderBottomWidth = 1;
            var cy = ApexUi.C(UiTokens.Cyan);
            num.style.borderLeftColor = cy; num.style.borderRightColor = cy; num.style.borderTopColor = cy; num.style.borderBottomColor = cy;
            num.style.alignItems = Align.Center; num.style.justifyContent = Justify.Center;
            ApexUi.Text(num, d.carNumber.ToString(), ApexUi.DisplayStrong, 18, ApexUi.C(UiTokens.Ivory), 0f, TextAnchor.MiddleCenter);
            var who = new VisualElement { pickingMode = PickingMode.Ignore };
            chip.Add(who);
            ApexUi.Text(who, d.driver, ApexUi.BodyStrong, 15, ApexUi.C(UiTokens.Ivory));
            ApexUi.Text(who, $"LEVEL {d.level} · {d.team.ToUpperInvariant()}", ApexUi.BodyStrong, 11, ApexUi.C(UiTokens.Steel), 1.2f);
            var xp = ApexUi.Box(who, ApexUi.C(UiTokens.Surface));
            xp.style.width = 150; xp.style.height = 3; xp.style.marginTop = 3;
            var xpFill = ApexUi.Box(xp, cy);
            xpFill.style.width = new Length(Mathf.Clamp01(d.levelProgress) * 100f, LengthUnit.Percent); xpFill.style.height = 3;

            // Menu.
            var caption = ApexUi.Caption(menuLayer, "Hauptmenü");
            ApexUi.Place(caption, m, 200);
            items = new VisualElement { pickingMode = PickingMode.Ignore };
            ApexUi.Place(items, m, 230, 580, -1);
            menuLayer.Add(items);
            foreach (var e in d.entries) itemViews.Add(BuildItem(e));

            // Bottom: cards, hints, version.
            var cards = new VisualElement { pickingMode = PickingMode.Ignore };
            cards.style.position = Position.Absolute; cards.style.left = m; cards.style.bottom = 36;
            cards.style.flexDirection = FlexDirection.Row;
            menuLayer.Add(cards);
            Card(cards, "Ausgewählte Strecke", d.trackName, d.trackMeta, true);
            if (!string.IsNullOrEmpty(d.lastTitle)) Card(cards, "Letztes Ergebnis", d.lastTitle, d.lastMeta, false);
            var hints = new VisualElement { pickingMode = PickingMode.Ignore };
            hints.style.position = Position.Absolute; hints.style.right = m; hints.style.bottom = 40;
            hints.style.alignItems = Align.FlexEnd;
            menuLayer.Add(hints);
            if (!d.mobile)
                ApexUi.Text(hints, "↑↓  Auswählen     Enter  Bestätigen     Esc  Zurück", ApexUi.Body, 14, ApexUi.C(UiTokens.Steel), 0.5f, TextAnchor.MiddleRight);
            ApexUi.Text(hints, d.version.ToUpperInvariant(), ApexUi.BodyStrong, 11, ApexUi.C(UiTokens.Steel, 0.6f), 2.5f, TextAnchor.MiddleRight).style.marginTop = 6;

            ApplyFocus(true);
            screenTime = 0f;
        }

        ItemView BuildItem(MenuEntry e)
        {
            var v = new ItemView { entry = e };
            v.row = new VisualElement { pickingMode = PickingMode.Position, userData = v };
            v.row.style.height = e.primary ? 104 : 62;
            v.row.style.marginBottom = e.primary ? 22 : 6;
            v.row.style.justifyContent = Justify.Center;
            v.row.style.paddingLeft = 26;
            v.row.style.paddingRight = 20;
            items.Add(v.row);
            v.highlight = ApexUi.Box(v.row, Color.clear);
            ApexUi.Place(v.highlight, 0, 0); v.highlight.style.right = 0; v.highlight.style.bottom = 0;
            ApexUi.Background(v.highlight, ApexUi.Ramp(ApexUi.C(UiTokens.Cyan), e.primary ? 0.24f : 0.16f, 0f));
            v.edge = ApexUi.Box(v.row, ApexUi.C(UiTokens.Cyan));
            ApexUi.Place(v.edge, 0, 0, 3, -1); v.edge.style.bottom = 0;
            if (e.primary)
            {
                // The start entry carries its summary below the title and a thin speed line.
                var speed = ApexUi.Box(v.row, Color.clear);
                ApexUi.Place(speed, 0, 92, 300, 1);
                ApexUi.Background(speed, ApexUi.Ramp(ApexUi.C(UiTokens.Cyan), 1f, 0f));
                v.label = ApexUi.Text(v.row, e.label.ToUpperInvariant(), ApexUi.DisplayStrong, UiTokens.MenuPrimary, ApexUi.C(UiTokens.Ivory), 3f);
                v.detail = ApexUi.Text(v.row, e.detail, ApexUi.Body, 15, ApexUi.C(UiTokens.Ivory, 0.8f), 0.3f);
                v.detail.style.marginTop = 2;
                var arrow = ApexUi.Text(v.row, "›", ApexUi.DisplayStrong, 34, ApexUi.C(UiTokens.Cyan), 0f, TextAnchor.MiddleRight);
                arrow.style.position = Position.Absolute; arrow.style.right = 18; arrow.style.top = 30;
            }
            else
            {
                var line = new VisualElement { pickingMode = PickingMode.Ignore };
                line.style.flexDirection = FlexDirection.Row; line.style.alignItems = Align.Center;
                v.row.Add(line);
                v.label = ApexUi.Text(line, e.label.ToUpperInvariant(), ApexUi.Display, UiTokens.MenuItem, ApexUi.C(UiTokens.Ivory, 0.8f), 2f);
                var spacer = new VisualElement { pickingMode = PickingMode.Ignore };
                spacer.style.flexGrow = 1;
                line.Add(spacer);
                v.detail = ApexUi.Text(line, e.detail, ApexUi.Body, 14, ApexUi.C(UiTokens.Steel), 0.2f, TextAnchor.MiddleRight);
            }
            return v;
        }

        static void Card(VisualElement parent, string caption, string title, string meta, bool istanbul)
        {
            var c = ApexUi.Panel(parent);
            c.style.minWidth = 340;
            c.style.paddingLeft = 22; c.style.paddingRight = 22; c.style.paddingTop = 14; c.style.paddingBottom = 16;
            c.style.marginRight = 20;
            if (istanbul)
            {
                var red = ApexUi.Box(c, ApexUi.C(UiTokens.Red));
                ApexUi.Place(red, 0, 0, 3, -1); red.style.bottom = 0;
            }
            ApexUi.Caption(c, caption);
            ApexUi.Text(c, title.ToUpperInvariant(), ApexUi.DisplayStrong, 26, ApexUi.C(UiTokens.Ivory), 1f).style.marginTop = 6;
            ApexUi.Text(c, meta, ApexUi.Body, 15, ApexUi.C(UiTokens.Steel)).style.marginTop = 4;
        }

        // --- Driver profile -----------------------------------------------------------------------------------

        void ShowProfile()
        {
            var p = data.profile;
            profileLayer?.RemoveFromHierarchy();
            profileLayer = ApexUi.Box(ui.root, ApexUi.C(UiTokens.Navy, 0.9f));
            ApexUi.Place(profileLayer, 0, 0); profileLayer.style.right = 0; profileLayer.style.bottom = 0;
            float m = UiTokens.ScreenMargin;
            var head = new VisualElement { pickingMode = PickingMode.Ignore };
            ApexUi.Place(head, m, 60);
            profileLayer.Add(head);
            ApexUi.Caption(head, "Fahrerprofil");
            ApexUi.Text(head, p.name.ToUpperInvariant(), ApexUi.DisplayStrong, UiTokens.H1, ApexUi.C(UiTokens.Ivory), 4f).style.marginTop = 8;
            ApexUi.Text(head, $"#{p.number} · {p.nationality} · {p.team}", ApexUi.Body, 18, ApexUi.C(UiTokens.Steel)).style.marginTop = 4;

            var grid = new VisualElement { pickingMode = PickingMode.Ignore };
            ApexUi.Place(grid, m, 230, 1180, -1);
            grid.style.flexDirection = FlexDirection.Row; grid.style.flexWrap = Wrap.Wrap;
            profileLayer.Add(grid);
            Stat(grid, "Rennen", p.races.ToString());
            Stat(grid, "Siege", p.wins.ToString(), p.wins > 0);
            Stat(grid, "Podien", p.podiums.ToString());
            Stat(grid, "Poles", p.poles.ToString());
            Stat(grid, "Level", p.level.ToString());
            Stat(grid, "XP", $"{p.xp} / {p.xpNext}");
            Stat(grid, "Ø Platz", p.averageFinish > 0f ? p.averageFinish.ToString("0.0") : "–");
            Stat(grid, "Distanz", p.distanceKm > 0 ? $"{p.distanceKm:0} km" : "–");

            var laps = ApexUi.Panel(profileLayer);
            ApexUi.Place(laps, 1920 - m - 520, 230, 520, -1);
            laps.style.paddingLeft = 22; laps.style.paddingRight = 22; laps.style.paddingTop = 18; laps.style.paddingBottom = 18;
            ApexUi.Caption(laps, "Bestzeiten");
            if (p.bestLaps.Count == 0) ApexUi.Text(laps, "Noch keine gewerteten Runden.", ApexUi.Body, 16, ApexUi.C(UiTokens.Steel)).style.marginTop = 12;
            foreach (var (track, time) in p.bestLaps)
            {
                var row = new VisualElement { pickingMode = PickingMode.Ignore };
                row.style.flexDirection = FlexDirection.Row; row.style.height = 40; row.style.alignItems = Align.Center;
                laps.Add(row);
                ApexUi.Text(row, track, ApexUi.Body, 17, ApexUi.C(UiTokens.Steel));
                var sp = new VisualElement { pickingMode = PickingMode.Ignore }; sp.style.flexGrow = 1; row.Add(sp);
                ApexUi.Text(row, time, ApexUi.Display, UiTokens.HudTiming, ApexUi.C(UiTokens.Ivory), 0f, TextAnchor.MiddleRight);
            }
            var back = ApexUi.Text(profileLayer, data.mobile ? "‹  Zurück" : "Esc  Zurück", ApexUi.BodyStrong, 16, ApexUi.C(UiTokens.Steel), 1f);
            back.pickingMode = PickingMode.Position;
            back.userData = "back";
            ApexUi.Place(back, m, 1000);
            screenTime = 0f;
        }

        static void Stat(VisualElement parent, string caption, string value, bool accent = false)
        {
            var c = ApexUi.Panel(parent);
            c.style.width = 280; c.style.height = 118;
            c.style.marginRight = 14; c.style.marginBottom = 14;
            c.style.paddingLeft = 22; c.style.paddingTop = 18;
            ApexUi.Caption(c, caption);
            ApexUi.Text(c, value, ApexUi.DisplayStrong, 46, accent ? ApexUi.C(UiTokens.Cyan) : ApexUi.C(UiTokens.Ivory)).style.marginTop = 6;
        }

        // --- Input and animation --------------------------------------------------------------------------------

        /// <summary>Back request (Esc, gamepad B): profile → menu. Returns true if handled.</summary>
        public bool Back()
        {
            if (nav.Current != UiScreen.Profile) return false;
            profileLayer?.RemoveFromHierarchy();
            profileLayer = null;
            nav.Back();
            sounds.Play(UiSounds.Cue.Back);
            screenTime = 1f;
            return true;
        }

        /// <summary>Per frame: navigation from keyboard/gamepad plus the pointer; runs the animations.</summary>
        public void Tick(float dt, bool up, bool down, bool confirm)
        {
            screenTime += dt;
            // Boot overlay fade.
            if (!booting && bootFade > 0f)
            {
                bootFade = Mathf.Max(0f, bootFade - dt / UiTokens.ScreenTime);
                bootLayer.style.opacity = bootFade;
                if (bootFade <= 0f) bootLayer.style.display = DisplayStyle.None;
            }
            if (booting || data == null || !Visible) return;
            bool anyInput = up || down || confirm;
            ui.ReadPointer(out var pos, out bool moved, out bool released);

            if (nav.Current == UiScreen.Profile)
            {
                if (released && ui.PickData<string>(pos) == "back") Back();
                return;
            }

            // Keyboard / gamepad.
            int count = itemViews.Count;
            if (up || down)
            {
                nav.MoveFocus(down ? 1 : -1, count);
                sounds.Play(UiSounds.Cue.Focus);
                ApplyFocus(false);
            }
            // Pointer: hover takes the focus (only when the pointer moved), release activates.
            var hovered = ui.PickData<ItemView>(pos);
            if (hovered != null && moved)
            {
                int i = itemViews.IndexOf(hovered);
                if (i != nav.Focus(UiScreen.MainMenu)) { nav.SetFocus(i); sounds.Play(UiSounds.Cue.Focus); ApplyFocus(false); }
            }
            if (released)
            {
                if (hovered != null) { nav.SetFocus(itemViews.IndexOf(hovered)); Activate(hovered); return; }
                if (ui.PickData<string>(pos) == "settings") { sounds.Play(UiSounds.Cue.Confirm); SettingsRequested?.Invoke(); return; }
            }
            if (confirm && count > 0) { Activate(itemViews[nav.Focus(UiScreen.MainMenu) % count]); return; }

            // Any input skips the entrance animation.
            if (anyInput || released) screenTime = Mathf.Max(screenTime, 2f);
            Animate(dt);
        }

        void Activate(ItemView v)
        {
            sounds.Play(v.entry.primary ? UiSounds.Cue.Start : UiSounds.Cue.Confirm);
            if (v.entry.action == MenuAction.Profile)
            {
                nav.Push(UiScreen.Profile);
                ShowProfile();
                return;
            }
            Activated?.Invoke(v.entry.action);
        }

        void ApplyFocus(bool instant)
        {
            int f = nav.Focus(UiScreen.MainMenu);
            for (int i = 0; i < itemViews.Count; i++)
            {
                var v = itemViews[i];
                float target = i == f ? 1f : 0f;
                if (instant) v.focus = Tween.Start(target, target, 0f);
                else v.focus.Retarget(target, UiTokens.FocusTime);
            }
        }

        void Animate(float dt)
        {
            float slide = ReducedMotion ? 0f : UiTokens.SlideDistance;
            for (int i = 0; i < itemViews.Count; i++)
            {
                var v = itemViews[i];
                v.focus.Advance(dt);
                float f = v.focus.Value;
                float enter = UiMotion.Staggered(screenTime, i, UiTokens.PanelTime, UiTokens.StaggerStep);
                v.row.style.opacity = enter;
                float nudge = ReducedMotion ? 0f : 8f * f;
                ApexUi.Translate(v.row, -slide * (1f - enter) + nudge, 0f);
                v.highlight.style.opacity = v.entry.primary ? 0.55f + 0.45f * f : f;
                v.edge.style.opacity = v.entry.primary ? 1f : f;
                if (!v.entry.primary)
                    v.label.style.color = Color.Lerp(ApexUi.C(UiTokens.Ivory, 0.72f), ApexUi.C(UiTokens.Ivory), f);
                else if (!ReducedMotion)
                    ApexUi.Scale(v.row, 1f + 0.02f * f);
            }
        }
    }
}
