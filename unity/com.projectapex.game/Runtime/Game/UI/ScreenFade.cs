using Apex.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apex.Game
{
    /// <summary>Short fade from navy on screen changes (docs/UI_UX_OVERHAUL.md, Phase 6); quicker with reduced motion.</summary>
    public sealed class ScreenFade
    {
        readonly ApexUi ui;
        readonly VisualElement veil;
        float alpha, duration = UiTokens.ScreenTime;

        public ScreenFade()
        {
            ui = new ApexUi("ApexFade", 100);
            veil = ApexUi.Box(ui.root, ApexUi.C(UiTokens.Navy));
            veil.style.position = Position.Absolute; veil.style.left = 0; veil.style.right = 0; veil.style.top = 0; veil.style.bottom = 0;
            veil.style.opacity = 0f;
            ui.SetVisible(false);
        }

        public void Flash(bool reducedMotion)
        {
            alpha = 1f;
            duration = reducedMotion ? 0.12f : UiTokens.ScreenTime;
            ui.SetVisible(true);
            veil.style.opacity = 1f;
        }

        public void Tick(float dt)
        {
            if (alpha <= 0f) return;
            alpha = Mathf.Max(0f, alpha - dt / duration);
            veil.style.opacity = UiMotion.InOutCubic(alpha);
            if (alpha <= 0f) ui.SetVisible(false);
        }
    }
}
