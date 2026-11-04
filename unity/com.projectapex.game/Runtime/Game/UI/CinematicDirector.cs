using System;
using System.Collections.Generic;
using Apex.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apex.Game
{
    /// <summary>
    /// Plays a cinematic sequence (Apex.UI.CinematicScripts) with the game camera over the real scene: resolves each
    /// shot's subject to a world position and direction, moves the camera after every other camera script
    /// (LateUpdate, late execution order), and draws the overlay – letterbox bars, the title card with the red line,
    /// lower thirds on the grid, a light streak at each cut and the skip hint. Any input skips the whole sequence.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class CinematicDirector : MonoBehaviour
    {
        public Camera cam;
        public bool reducedMotion;

        /// <summary>Resolves a shot to (subject position, its forward direction); null = skip the shot.</summary>
        public Func<CinematicShot, (Vector3 pos, Vector3 fwd)?> resolve;
        /// <summary>Lower-third text for a shot (grid cards), or null.</summary>
        public Func<CinematicShot, string> lowerThird;

        List<CinematicShot> shots;
        Action done;
        float t;
        int lastIndex = -1;
        float baseFov;
        public bool Playing { get; private set; }

        ApexUi ui;
        VisualElement top, bottom, card, streak, third;
        Label title, subtitle, thirdText, skip;

        public void Play(List<CinematicShot> sequence, Action onDone)
        {
            shots = sequence;
            done = onDone;
            t = 0f;
            lastIndex = -1;
            if (cam != null) baseFov = cam.fieldOfView;
            Playing = shots != null && shots.Count > 0;
            EnsureOverlay();
            ui.SetVisible(Playing);
            if (!Playing) Finish();
        }

        /// <summary>Ends the sequence now (any input).</summary>
        public void Skip()
        {
            if (Playing) Finish();
        }

        void Finish()
        {
            Playing = false;
            if (cam != null && baseFov > 0f) cam.fieldOfView = baseFov;
            ui?.SetVisible(false);
            var d = done;
            done = null;
            d?.Invoke();
        }

        void EnsureOverlay()
        {
            if (ui != null) return;
            ui = new ApexUi("ApexCinematic", 20);
            top = ApexUi.Box(ui.root, ApexUi.C(UiTokens.CarbonBlack));
            ApexUi.Place(top, 0, 0, 1920, 0);
            bottom = ApexUi.Box(ui.root, ApexUi.C(UiTokens.CarbonBlack));
            ApexUi.Place(bottom, 0, 1080, 1920, 0);
            streak = ApexUi.Box(ui.root, Color.clear);
            ApexUi.Background(streak, ApexUi.Ramp(ApexUi.C(UiTokens.Cyan), 0f, 0.9f));
            ApexUi.Place(streak, 0, 539, 900, 2);
            card = new VisualElement { pickingMode = PickingMode.Ignore };
            card.style.position = Position.Absolute;
            card.style.left = 0; card.style.right = 0; card.style.top = 360;
            card.style.alignItems = Align.Center;
            ui.root.Add(card);
            title = ApexUi.Text(card, "", ApexUi.DisplayStrong, 110, ApexUi.C(UiTokens.Ivory), 14f, TextAnchor.MiddleCenter);
            var line = ApexUi.Box(card, ApexUi.C(UiTokens.Red));
            line.style.width = 160; line.style.height = 3; line.style.marginTop = 10;
            subtitle = ApexUi.Text(card, "", ApexUi.BodyStrong, 24, ApexUi.C(UiTokens.Steel), 10f, TextAnchor.MiddleCenter);
            subtitle.style.marginTop = 16;
            third = ApexUi.Panel(ui.root, UiTokens.PanelStrongAlpha);
            ApexUi.Place(third, 120, 860, 900, 60);
            var edge = ApexUi.Box(third, ApexUi.C(UiTokens.Cyan));
            ApexUi.Place(edge, 0, 0, 4, 60);
            thirdText = ApexUi.Text(third, "", ApexUi.DisplayStrong, 30, ApexUi.C(UiTokens.Ivory), 3f);
            ApexUi.Place(thirdText, 24, 10, 860);
            skip = ApexUi.Text(ui.root, Loc.T("Beliebige Taste: überspringen"), ApexUi.Body, UiTokens.Small, ApexUi.C(UiTokens.Steel, 0.8f), 1.5f, TextAnchor.MiddleRight);
            ApexUi.Place(skip, 1420, 1036, 440);
        }

        void LateUpdate()
        {
            if (!Playing || cam == null) return;
            t += Time.unscaledDeltaTime;
            var (index, local) = CinematicScripts.At(shots, t);
            if (index >= shots.Count) { Finish(); return; }
            var shot = shots[index];
            var subj = resolve?.Invoke(shot);
            if (subj == null)
            {
                // Unresolvable subject (e.g. no bridge on this circuit): jump to the next shot.
                t += shot.duration - local;
                return;
            }
            if (index != lastIndex)
            {
                lastIndex = index;
                thirdText.text = lowerThird?.Invoke(shot) ?? "";
                title.text = shot.title ?? "";
                subtitle.text = shot.subtitle ?? "";
            }
            var (eye, target) = CinematicScripts.Pose(shot, subj.Value.pos.ToNum(), subj.Value.fwd.ToNum(), reducedMotion ? shot.duration * 0.5f : local);
            var e = eye.ToUnity();
            var look = target.ToUnity() - e;
            if (look.sqrMagnitude > 1e-4f) cam.transform.SetPositionAndRotation(e, Quaternion.LookRotation(look, Vector3.up));
            if (shot.fov > 0f) cam.fieldOfView = shot.fov;

            // Overlay: letterbox in, title card fading in after a beat, light streak sweeping at each cut.
            float bars = Mathf.Min(1f, t / 0.5f) * 104f;
            top.style.height = bars;
            ApexUi.Place(bottom, 0, 1080 - bars, 1920, bars);
            float k = shot.duration <= 0f ? 1f : local / shot.duration;
            bool hasTitle = !string.IsNullOrEmpty(shot.title);
            card.style.opacity = hasTitle ? Mathf.Clamp01((k - 0.15f) / 0.2f) * Mathf.Clamp01((1f - k) / 0.12f) : 0f;
            third.style.opacity = string.IsNullOrEmpty(thirdText.text) ? 0f : Mathf.Clamp01(local / 0.25f) * Mathf.Clamp01((shot.duration - local) / 0.2f);
            float sweep = Mathf.Clamp01(local / 0.45f);
            streak.style.opacity = sweep < 1f ? 1f - sweep : 0f;
            ApexUi.Translate(streak, -900f + sweep * 2820f, 0f);
        }

        void OnDestroy() => ui?.Dispose();
    }
}
