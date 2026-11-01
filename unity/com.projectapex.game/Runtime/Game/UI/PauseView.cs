using System;
using Apex.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apex.Game
{
    /// <summary>
    /// Pause screen (docs/UI_UX_OVERHAUL.md section 4): the race stays visible and dimmed, a side panel on the right
    /// shows session, lap and position and the entries of <see cref="PauseModel"/>; leaving a race needs a second
    /// confirmation.
    /// </summary>
    public sealed class PauseView
    {
        public event Action<PauseAction> Chosen;
        public bool ReducedMotion;

        readonly ApexUi ui;
        readonly UiSounds sounds;
        readonly FocusList list;
        readonly ConfirmGate gate = new ConfirmGate();
        VisualElement layer, panel;
        float time;

        public PauseView(UiSounds sounds)
        {
            this.sounds = sounds;
            ui = new ApexUi("ApexPause", 20);
            list = new FocusList(sounds);
            ui.SetVisible(false);
        }

        public bool Visible { get; private set; }

        public void Hide()
        {
            if (!Visible) return;
            Visible = false;
            ui.SetVisible(false);
        }

        /// <param name="session">e.g. "Bosphorus Grand Prix · Rennen"</param>
        /// <param name="status">e.g. "Runde 3/5 · P4 von 12"</param>
        public void Show(string session, string status, bool race, bool hudEditor)
        {
            Visible = true;
            ui.SetVisible(true);
            layer?.RemoveFromHierarchy();
            list.Clear();
            gate.Disarm();
            layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute; layer.style.left = 0; layer.style.right = 0; layer.style.top = 0; layer.style.bottom = 0;
            ui.root.Add(layer);
            var dim = ApexUi.Box(layer, ApexUi.C(UiTokens.Navy, 0.35f));
            dim.style.position = Position.Absolute; dim.style.left = 0; dim.style.right = 0; dim.style.top = 0; dim.style.bottom = 0;
            var shade = ApexUi.Box(layer, Color.clear);
            shade.style.position = Position.Absolute; shade.style.right = 0; shade.style.top = 0; shade.style.bottom = 0; shade.style.width = 900;
            ApexUi.Background(shade, ApexUi.Ramp(ApexUi.C(UiTokens.Navy), 0f, 0.92f, false, 0.1f));

            panel = new VisualElement { pickingMode = PickingMode.Ignore };
            panel.style.position = Position.Absolute; panel.style.right = UiTokens.ScreenMargin; panel.style.top = 120; panel.style.width = 560;
            layer.Add(panel);
            ApexUi.Caption(panel, session);
            ApexUi.Text(panel, Loc.T("PAUSE"), ApexUi.DisplayStrong, UiTokens.Display, ApexUi.C(UiTokens.Ivory), 6f).style.marginTop = 4;
            var red = ApexUi.Box(panel, ApexUi.C(UiTokens.Red)); red.style.width = 64; red.style.height = 3; red.style.marginTop = 4;
            ApexUi.Text(panel, status, ApexUi.Body, UiTokens.Body, ApexUi.C(UiTokens.Steel)).style.marginTop = 14;
            var items = new VisualElement { pickingMode = PickingMode.Ignore };
            items.style.marginTop = 40;
            panel.Add(items);
            foreach (var e in PauseModel.Entries(race, hudEditor))
            {
                var entry = e;
                var it = list.AddButton(items, e.label, "", e.action == PauseAction.Resume, () => Choose(entry), e.action == PauseAction.Resume ? 76f : 58f);
                it.row.userData = it;
                it.label.userData = entry;
            }
            var hints = ApexUi.Text(panel, Loc.T("↑↓  Auswählen     Enter  Bestätigen     Esc  Zurück"), ApexUi.Body, 14, ApexUi.C(UiTokens.Steel), 0.5f);
            hints.style.marginTop = 30;
            list.ApplyFocus(true);
            time = 0f;
        }

        void Choose(PauseEntry e)
        {
            if (e.confirm)
            {
                int idx = list.Focus;
                if (!gate.Press(idx)) { RefreshLabels(); return; }
            }
            Chosen?.Invoke(e.action);
        }

        void RefreshLabels()
        {
            for (int i = 0; i < list.items.Count; i++)
            {
                var it = list.items[i];
                if (!(it.label.userData is PauseEntry e)) continue;
                bool armed = gate.Armed(i);
                it.label.text = armed ? Loc.T("WIRKLICH?  ERNEUT BESTÄTIGEN") : e.label.ToUpperInvariant();
                it.label.style.color = armed ? ApexUi.C(UiTokens.Amber) : ApexUi.C(UiTokens.Ivory);
            }
        }

        /// <summary>Esc / B: disarms a pending confirmation, otherwise resumes.</summary>
        public void Back()
        {
            for (int i = 0; i < list.items.Count; i++)
                if (gate.Armed(i)) { gate.Disarm(); RefreshLabels(); sounds.Play(UiSounds.Cue.Back); return; }
            sounds.Play(UiSounds.Cue.Back);
            Chosen?.Invoke(PauseAction.Resume);
        }

        public void Tick(float dt, bool up, bool down, bool confirm)
        {
            if (!Visible) return;
            time += dt;
            ui.ReadPointer(out var pos, out bool moved, out bool released);
            int before = list.Focus;
            if (!list.Pointer(ui, pos, moved, released)) list.Navigate(up, down, false, false, confirm);
            if (list.Focus != before)
            {
                // Moving away disarms a pending confirmation (a click that just armed this item keeps it).
                if (!gate.Armed(list.Focus)) gate.Disarm();
                RefreshLabels();
                list.ApplyFocus(false);
            }
            float enter = UiMotion.OutCubic(Mathf.Clamp01(time / UiTokens.ScreenTime));
            if (panel != null)
            {
                panel.style.opacity = enter;
                ApexUi.Translate(panel, ReducedMotion ? 0f : UiTokens.SlideDistance * (1f - enter), 0f);
            }
            list.Animate(dt, ReducedMotion);
        }
    }
}
