using System;
using System.Collections.Generic;
using Apex.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Apex.Game
{
    /// <summary>
    /// Category screen used by the settings (Grafik, Steuerung, Fahrverhalten, Audio, HUD, Barrierefreiheit) and the
    /// garage (Lackierung, Team, Startnummer, Setup, Fortschritt): categories on the left, the category's options on
    /// the right with ‹ › values. Every option is an existing setting. The garage adds camera views and lets the
    /// pointer rotate (drag) and zoom (wheel) the car outside the panels.
    /// </summary>
    public sealed class OptionsView
    {
        public sealed class Category
        {
            public string name = "", description = "";
            public Func<List<SetupOption>> options;
            /// <summary>Optional colour swatch per option key (garage paints).</summary>
            public Func<string, Func<UiTokens.Rgb?>> swatch;
            /// <summary>Optional read-only lines below the options (e.g. unlocked rewards).</summary>
            public Func<List<string>> notes;
        }

        public sealed class Data
        {
            public string caption = "", title = "", footnote = "";
            public List<Category> categories = new List<Category>();
            public bool garage;
            public bool mobile;
        }

        public event Action Closed;
        public event Action OptionChanged;
        public event Action<GarageView> CameraView;
        public event Action<float, float> CameraDrag;
        public event Action<float> CameraZoom;
        public bool ReducedMotion;

        readonly ApexUi ui;
        readonly UiSounds sounds;
        readonly FocusList cats, rows, views;
        Data data;
        VisualElement layer, rowsHost, notesHost, left, right;
        Label catTitle, catDescription;
        int column;           // 0 categories, 1 options, 2 camera views (garage)
        int shownCategory = -1;
        float time;
        bool dragging;
        Vector2 dragLast;

        public OptionsView(UiSounds sounds, string name, int order)
        {
            this.sounds = sounds;
            ui = new ApexUi(name, order);
            cats = new FocusList(sounds);
            rows = new FocusList(sounds);
            views = new FocusList(sounds) { Horizontal = true };
            rows.Changed += () => { OptionChanged?.Invoke(); RefreshNotes(); };
            ui.SetVisible(false);
        }

        public bool Visible { get; private set; }

        public void Hide()
        {
            if (!Visible) return;
            Visible = false;
            ui.SetVisible(false);
        }

        public void Show(Data d)
        {
            data = d;
            Visible = true;
            ui.SetVisible(true);
            layer?.RemoveFromHierarchy();
            cats.Clear(); views.Clear();
            column = 0;
            shownCategory = -1;
            float m = UiTokens.ScreenMargin;
            layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute; layer.style.left = 0; layer.style.right = 0; layer.style.top = 0; layer.style.bottom = 0;
            ui.root.Add(layer);
            var shade = ApexUi.Box(layer, Color.clear);
            shade.style.position = Position.Absolute; shade.style.left = 0; shade.style.top = 0; shade.style.bottom = 0; shade.style.width = d.garage ? 1300 : 1920;
            if (d.garage) ApexUi.Background(shade, ApexUi.Ramp(ApexUi.C(UiTokens.Navy), 0.95f, 0f, false, 0.45f));
            else shade.style.backgroundColor = ApexUi.C(UiTokens.Navy, 0.92f);

            left = new VisualElement { pickingMode = PickingMode.Ignore };
            left.style.position = Position.Absolute; left.style.left = m; left.style.top = 56; left.style.width = 330;
            layer.Add(left);
            ApexUi.Caption(left, d.caption);
            ApexUi.Text(left, d.title.ToUpperInvariant(), ApexUi.DisplayStrong, UiTokens.H1, ApexUi.C(UiTokens.Ivory), 4f).style.marginTop = 6;
            var accent = ApexUi.Box(left, ApexUi.C(UiTokens.Cyan)); accent.style.width = 64; accent.style.height = 3; accent.style.marginTop = 10; accent.style.marginBottom = 40;
            foreach (var c in d.categories) cats.AddButton(left, c.name, c.description, false, null, 66f);

            right = ApexUi.Panel(layer, UiTokens.PanelStrongAlpha);
            right.style.position = Position.Absolute; right.style.left = m + 370; right.style.top = 56; right.style.width = d.garage ? 760 : 1920 - 2 * m - 370;
            right.style.bottom = d.garage ? 200 : 110;
            right.style.paddingLeft = 28; right.style.paddingRight = 28; right.style.paddingTop = 22; right.style.paddingBottom = 18;
            right.style.overflow = Overflow.Hidden;
            catTitle = ApexUi.Text(right, "", ApexUi.DisplayStrong, 34, ApexUi.C(UiTokens.Ivory), 2.5f);
            catDescription = ApexUi.Text(right, "", ApexUi.Body, 15, ApexUi.C(UiTokens.Steel));
            catDescription.style.marginTop = 2; catDescription.style.marginBottom = 18;
            rowsHost = new VisualElement { pickingMode = PickingMode.Ignore };
            right.Add(rowsHost);
            notesHost = new VisualElement { pickingMode = PickingMode.Ignore };
            notesHost.style.marginTop = 16;
            right.Add(notesHost);

            var bottom = new VisualElement { pickingMode = PickingMode.Ignore };
            bottom.style.position = Position.Absolute; bottom.style.left = m; bottom.style.right = m; bottom.style.bottom = 36;
            layer.Add(bottom);
            if (d.garage)
            {
                ApexUi.Caption(bottom, Loc.T("Kamera"));
                var bar = new VisualElement { pickingMode = PickingMode.Ignore };
                bar.style.flexDirection = FlexDirection.Row; bar.style.marginTop = 8; bar.style.marginBottom = 12;
                bottom.Add(bar);
                foreach (GarageView v in Enum.GetValues(typeof(GarageView)))
                {
                    var view = v;
                    var it = views.AddButton(bar, GarageOrbit.Name(v), "", false, () => CameraView?.Invoke(view), 52f);
                    it.row.style.minWidth = 150; it.row.style.marginRight = 10;
                }
            }
            string hints = d.mobile ? "" : Loc.T("↑↓  Auswählen     ←→  Ändern     Enter  Bestätigen     Esc  Zurück") + (d.garage ? "     " + Loc.T("Ziehen  Drehen     Mausrad  Zoom") : "");
            ApexUi.Text(bottom, hints, ApexUi.Body, 14, ApexUi.C(UiTokens.Steel), 0.5f);
            if (!string.IsNullOrEmpty(d.footnote)) ApexUi.Text(bottom, d.footnote, ApexUi.Body, 14, ApexUi.C(UiTokens.Amber)).style.marginTop = 4;
            var back = ApexUi.Text(layer, Loc.T("‹  Zurück"), ApexUi.BodyStrong, 16, ApexUi.C(UiTokens.Steel), 1f);
            back.pickingMode = PickingMode.Position; back.userData = "back";
            back.style.position = Position.Absolute; back.style.right = m; back.style.top = 64;
            ShowCategory(0);
            cats.ApplyFocus(true);
            time = 0f;
            sounds.Play(UiSounds.Cue.Transition);
        }

        void ShowCategory(int i)
        {
            if (i < 0 || i >= data.categories.Count) return;
            shownCategory = i;
            var c = data.categories[i];
            catTitle.text = c.name.ToUpperInvariant();
            catDescription.text = c.description;
            rowsHost.Clear();
            rows.Clear();
            foreach (var o in c.options()) rows.AddOption(rowsHost, o, c.swatch?.Invoke(o.key), data.garage ? 230f : 300f);
            rows.ApplyFocus(true, column == 1);
            RefreshNotes();
        }

        void RefreshNotes()
        {
            notesHost.Clear();
            if (shownCategory < 0) return;
            var notes = data.categories[shownCategory].notes?.Invoke();
            if (notes == null) return;
            foreach (var n in notes)
            {
                var l = ApexUi.Text(notesHost, n, ApexUi.Body, 15, ApexUi.C(UiTokens.Steel));
                l.style.whiteSpace = WhiteSpace.Normal; l.style.marginBottom = 4;
            }
        }

        /// <summary>Esc / B: options → categories → close.</summary>
        public void Back()
        {
            sounds.Play(UiSounds.Cue.Back);
            if (column != 0) { column = 0; ApplyColumns(); return; }
            Closed?.Invoke();
        }

        void ApplyColumns()
        {
            cats.ApplyFocus(false, column == 0);
            rows.ApplyFocus(false, column == 1);
            views.ApplyFocus(false, column == 2);
        }

        public void Tick(float dt, bool up, bool down, bool left_, bool right_, bool confirm)
        {
            if (!Visible || data == null) return;
            time += dt;
            ui.ReadPointer(out var pos, out bool moved, out bool released);
            int cBefore = cats.Focus, rBefore = rows.Focus, vBefore = views.Focus, colBefore = column;

            // Pointer first: whichever list is under the pointer takes the column.
            if (moved || released)
            {
                if (ui.PickData<FocusList.Item>(pos) is FocusList.Item hit)
                    column = hit.owner == cats ? 0 : hit.owner == rows ? 1 : 2;
            }
            bool handled = cats.Pointer(ui, pos, moved, released) || rows.Pointer(ui, pos, moved, released) || views.Pointer(ui, pos, moved, released);
            if (released && !handled && ui.PickData<string>(pos) == "back") { Back(); return; }

            if (!handled)
            {
                switch (column)
                {
                    case 0:
                        cats.Navigate(up, down, false, false, false);
                        if ((right_ || confirm) && rows.items.Count > 0) { column = 1; sounds.Play(UiSounds.Cue.Confirm); }
                        break;
                    case 1:
                        if (down && rows.Focus == rows.items.Count - 1 && data.garage) { column = 2; sounds.Play(UiSounds.Cue.Focus); }
                        else rows.Navigate(up, down, left_, right_, confirm);
                        break;
                    default:
                        if (up) { column = 1; sounds.Play(UiSounds.Cue.Focus); }
                        else views.Navigate(false, false, left_, right_, confirm);
                        break;
                }
            }
            if (cats.Focus != shownCategory) ShowCategory(cats.Focus);
            if (cats.Focus != cBefore || rows.Focus != rBefore || views.Focus != vBefore || column != colBefore) ApplyColumns();

            if (data.garage) GaragePointer(pos);
            float enter = UiMotion.OutCubic(Mathf.Clamp01(time / UiTokens.ScreenTime));
            right.style.opacity = enter;
            ApexUi.Translate(right, ReducedMotion ? 0f : UiTokens.SlideDistance * (1f - enter), 0f);
            cats.Animate(dt, ReducedMotion);
            rows.Animate(dt, ReducedMotion);
            views.Animate(dt, ReducedMotion);
        }

        /// <summary>Drag on the free area rotates the garage camera, the wheel zooms.</summary>
        void GaragePointer(Vector2 pos)
        {
            var p = Pointer.current;
            if (p == null) return;
            bool overUi = ui.PickData<FocusList.Item>(pos) != null || right.worldBound.Contains(pos) || left.worldBound.Contains(pos);
            if (p.press.wasPressedThisFrame && !overUi) { dragging = true; dragLast = pos; }
            if (dragging && p.press.isPressed)
            {
                var d = pos - dragLast;
                if (d.sqrMagnitude > 0.1f) CameraDrag?.Invoke(d.x, -d.y);
                dragLast = pos;
            }
            if (p.press.wasReleasedThisFrame) dragging = false;
            var mouse = Mouse.current;
            if (mouse != null && !overUi)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f) CameraZoom?.Invoke(Mathf.Sign(wheel));
            }
        }
    }
}
