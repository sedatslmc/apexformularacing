using System;
using System.Collections.Generic;
using Apex.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apex.Game
{
    /// <summary>
    /// Focusable rows shared by the pause, results and options screens: cyan edge and gradient on focus, hover takes
    /// the focus, release activates, ‹ › arrows (and left/right) change option rows. Keyboard and gamepad come from
    /// the game's navigation flags; the pointer from <see cref="ApexUi"/>.
    /// </summary>
    public sealed class FocusList
    {
        public sealed class Item
        {
            public VisualElement row, highlight, edge;
            public Label label, value, detail;
            public VisualElement swatch;
            public Action activate;
            public SetupOption option;
            public bool primary;
            public Tween focus;
            public FocusList owner;
        }

        sealed class Arrow { public Item item; public int dir; }

        public readonly List<Item> items = new List<Item>();
        readonly UiSounds sounds;
        public int Focus { get; private set; }
        public bool Horizontal;
        public event Action Changed;
        public event Action<int> FocusChanged;

        public FocusList(UiSounds sounds) { this.sounds = sounds; }

        public void Clear() { items.Clear(); Focus = 0; }

        static void Decorate(Item it, bool primary)
        {
            it.highlight = ApexUi.Box(it.row, Color.clear);
            ApexUi.Place(it.highlight, 0, 0); it.highlight.style.right = 0; it.highlight.style.bottom = 0;
            ApexUi.Background(it.highlight, ApexUi.Ramp(ApexUi.C(UiTokens.Cyan), primary ? 0.3f : 0.18f, primary ? 0.06f : 0f));
            it.edge = ApexUi.Box(it.row, ApexUi.C(UiTokens.Cyan));
            ApexUi.Place(it.edge, 0, 0, 3, -1); it.edge.style.bottom = 0;
        }

        /// <summary>A button row (label, optional detail line).</summary>
        public Item AddButton(VisualElement parent, string label, string detail, bool primary, Action activate, float height = 62f)
        {
            var it = new Item { activate = activate, primary = primary, owner = this };
            it.row = primary ? ApexUi.Panel(parent, UiTokens.PanelAlpha) : new VisualElement();
            it.row.pickingMode = PickingMode.Position;
            it.row.userData = it;
            it.row.style.height = height;
            it.row.style.marginBottom = 6;
            it.row.style.justifyContent = Justify.Center;
            it.row.style.paddingLeft = 24; it.row.style.paddingRight = 24;
            if (!primary) parent.Add(it.row);
            Decorate(it, primary);
            it.label = ApexUi.Text(it.row, label.ToUpperInvariant(), primary ? ApexUi.DisplayStrong : ApexUi.Display, primary ? 32 : UiTokens.MenuItem, ApexUi.C(UiTokens.Ivory), 2.2f);
            if (!string.IsNullOrEmpty(detail))
            {
                it.detail = ApexUi.Text(it.row, detail, ApexUi.Body, 14, ApexUi.C(UiTokens.Ivory, 0.75f), 0.2f);
                it.detail.style.marginTop = 2;
            }
            items.Add(it);
            return it;
        }

        /// <summary>An option row: label, value between ‹ › arrows, optional colour swatch.</summary>
        public Item AddOption(VisualElement parent, SetupOption o, Func<UiTokens.Rgb?> swatch = null, float valueWidth = 260f)
        {
            var it = new Item { option = o, owner = this };
            it.row = new VisualElement { pickingMode = PickingMode.Position, userData = it };
            it.row.style.height = 48; it.row.style.marginBottom = 4;
            it.row.style.flexDirection = FlexDirection.Row; it.row.style.alignItems = Align.Center;
            it.row.style.paddingLeft = 20; it.row.style.paddingRight = 8;
            parent.Add(it.row);
            Decorate(it, false);
            it.label = ApexUi.Text(it.row, Loc.T(o.label), ApexUi.Body, 18, ApexUi.C(UiTokens.Ivory, 0.85f));
            var sp = new VisualElement { pickingMode = PickingMode.Ignore }; sp.style.flexGrow = 1; it.row.Add(sp);
            if (swatch != null)
            {
                it.swatch = ApexUi.Box(it.row, Color.white);
                it.swatch.style.width = 22; it.swatch.style.height = 22; it.swatch.style.marginRight = 8;
                it.swatch.userData = swatch;
            }
            ArrowButton(it, -1, "‹");
            it.value = ApexUi.Text(it.row, "", ApexUi.BodyStrong, 18, ApexUi.C(UiTokens.Cyan), 0.4f, TextAnchor.MiddleCenter);
            it.value.style.width = valueWidth;
            ArrowButton(it, 1, "›");
            Refresh(it);
            items.Add(it);
            return it;
        }

        static void ArrowButton(Item it, int dir, string glyph)
        {
            var b = new VisualElement { pickingMode = PickingMode.Position, userData = new Arrow { item = it, dir = dir } };
            b.style.width = 36; b.style.height = 36;
            b.style.alignItems = Align.Center; b.style.justifyContent = Justify.Center;
            it.row.Add(b);
            ApexUi.Text(b, glyph, ApexUi.DisplayStrong, 28, ApexUi.C(UiTokens.Ivory, 0.7f), 0f, TextAnchor.MiddleCenter);
        }

        static void Refresh(Item it)
        {
            if (it.option == null) return;
            it.value.text = Loc.T(it.option.value());
            if (it.swatch != null && it.swatch.userData is Func<UiTokens.Rgb?> f)
            {
                var c = f();
                it.swatch.style.display = c.HasValue ? DisplayStyle.Flex : DisplayStyle.None;
                if (c.HasValue) it.swatch.style.backgroundColor = ApexUi.C(c.Value);
            }
        }

        public void RefreshValues() { foreach (var it in items) Refresh(it); }

        public void SetFocus(int i, bool sound = true)
        {
            if (i < 0 || i >= items.Count || i == Focus) return;
            Focus = i;
            if (sound) sounds?.Play(UiSounds.Cue.Focus);
            FocusChanged?.Invoke(i);
        }

        public void Change(Item it, int dir)
        {
            it.option.change(dir);
            sounds?.Play(UiSounds.Cue.Focus);
            Changed?.Invoke();
            RefreshValues();
        }

        public void Activate(Item it)
        {
            if (it.option != null) { Change(it, 1); return; }
            sounds?.Play(it.primary ? UiSounds.Cue.Start : UiSounds.Cue.Confirm);
            it.activate?.Invoke();
        }

        /// <summary>Keyboard / gamepad: returns true if something was activated (the caller may rebuild).</summary>
        public bool Navigate(bool up, bool down, bool left, bool right, bool confirm)
        {
            int n = items.Count;
            if (n == 0) return false;
            bool prev = Horizontal ? left : up, next = Horizontal ? right : down;
            if (prev || next) SetFocus(((Focus + (next ? 1 : -1)) % n + n) % n);
            var cur = items[Mathf.Clamp(Focus, 0, n - 1)];
            if (!Horizontal && cur.option != null && (left || right)) Change(cur, right ? 1 : -1);
            if (confirm) { Activate(cur); return true; }
            return false;
        }

        /// <summary>Pointer: hover focus (when moved), release on an arrow or row. Returns true if it handled a click.</summary>
        public bool Pointer(ApexUi ui, Vector2 pos, bool moved, bool released)
        {
            var hovered = ui.PickData<Item>(pos);
            if (hovered != null && hovered.owner != this) hovered = null;
            if (hovered != null && moved) SetFocus(items.IndexOf(hovered));
            if (!released) return false;
            var arrow = ui.PickData<Arrow>(pos);
            if (arrow != null && arrow.item.owner == this) { SetFocus(items.IndexOf(arrow.item), false); Change(arrow.item, arrow.dir); return true; }
            if (hovered != null) { SetFocus(items.IndexOf(hovered), false); Activate(hovered); return true; }
            return false;
        }

        public void ApplyFocus(bool instant, bool active = true)
        {
            for (int i = 0; i < items.Count; i++)
            {
                float t = active && i == Focus ? 1f : 0f;
                if (instant) items[i].focus = Tween.Start(t, t, 0f);
                else items[i].focus.Retarget(t, UiTokens.FocusTime);
            }
        }

        public void Animate(float dt, bool reducedMotion)
        {
            foreach (var it in items)
            {
                it.focus.Advance(dt);
                float f = it.focus.Value;
                it.highlight.style.opacity = it.primary ? 0.5f + 0.5f * f : f;
                it.edge.style.opacity = it.primary ? 1f : f;
                it.label.style.color = Color.Lerp(ApexUi.C(UiTokens.Ivory, 0.75f), ApexUi.C(UiTokens.Ivory), it.primary ? 1f : f);
                if (!reducedMotion && it.primary) ApexUi.Scale(it.row, 1f + 0.02f * f);
            }
        }
    }
}
