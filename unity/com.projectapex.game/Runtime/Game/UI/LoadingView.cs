using System.Collections.Generic;
using Apex.Tracks;
using Apex.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Apex.Game
{
    /// <summary>
    /// Loading screens (docs/UI_UX_OVERHAUL.md section 13.4): one layout, content per situation (LoadContext). Session
    /// loads show the event – title, place, the track map drawn in while loading, length, corners, direction, weather,
    /// time of day and mode – with the Istanbul band for the Bosphorus GP; the bar is a thin cyan line driven by the
    /// real build stages (LoadingModel, no timer), the current stage under it, and a rotating tip. The game start and the
    /// return to the paddock use the same frame without a map.
    /// </summary>
    public sealed class LoadingView
    {
        public sealed class Info
        {
            public LoadContext context;
            public string title = "", subtitle = "";
            public bool istanbul;
            public CircuitDefinition def;
            public TrackGeometry geometry;
            public readonly List<(string label, string value)> facts = new List<(string, string)>();
            public int seed;
        }

        readonly ApexUi ui;
        VisualElement layer, bar, barTrack;
        Label stage, tip, percentHint;
        TrackMapElement map;
        LoadingModel model;
        float time, shown, drawnProgress;
        int seed;
        bool visible;

        public LoadingView()
        {
            ui = new ApexUi("ApexLoading", 30);
            ui.SetVisible(false);
        }

        public bool Visible => visible;

        public void Show(Info info, LoadingModel m)
        {
            model = m;
            seed = info.seed;
            time = 0f;
            shown = 0f;
            drawnProgress = 0f;
            layer?.RemoveFromHierarchy();
            layer = ApexUi.Box(ui.root, ApexUi.C(UiTokens.CarbonBlack));
            Fill(layer);
            // Diagonal light: a soft navy wash from the left, the cyan hairline at the top.
            var wash = ApexUi.Box(layer, Color.clear);
            Fill(wash);
            ApexUi.Background(wash, ApexUi.Ramp(ApexUi.C(UiTokens.Navy), 1f, 0f));
            var top = ApexUi.Box(layer, ApexUi.C(UiTokens.Cyan, 0.8f));
            ApexUi.Place(top, 0, 0, 1920, 2);

            if (info.def != null && info.geometry != null)
            {
                map = new TrackMapElement { pickingMode = PickingMode.Ignore };
                ApexUi.Place(map, 980, 150, 840, 700);
                map.Set(TrackMap.Fit(info.def, info.geometry, 840, 700, 40f, 420), 5f, true, Color.white, false);
                map.Progress = 0f;
                layer.Add(map);
            }
            else map = null;

            float x = 120f;
            if (info.istanbul)
            {
                // Red-white-red band of the Istanbul event.
                var band = new VisualElement { pickingMode = PickingMode.Ignore };
                band.style.flexDirection = FlexDirection.Row;
                ApexUi.Place(band, x, 236, 180, 4);
                foreach (var c in new[] { UiTokens.Red, UiTokens.Ivory, UiTokens.Red })
                {
                    var b = ApexUi.Box(band, ApexUi.C(c));
                    b.style.flexGrow = 1;
                }
                layer.Add(band);
                var welcome = ApexUi.Text(layer, Loc.T("WELCOME TO ISTANBUL"), ApexUi.BodyStrong, 16, ApexUi.C(UiTokens.Red), 6f);
                ApexUi.Place(welcome, x, 200);
            }
            var title = ApexUi.Text(layer, info.title, ApexUi.DisplayStrong, UiTokens.Display, ApexUi.C(UiTokens.Ivory), 4f);
            ApexUi.Place(title, x, 252, 860);
            title.style.whiteSpace = WhiteSpace.Normal;
            var sub = ApexUi.Text(layer, info.subtitle, ApexUi.BodyStrong, 22, ApexUi.C(UiTokens.Steel), 5f);
            ApexUi.Place(sub, x, 372, 860);

            // Facts in a row of small cards.
            float fx = x;
            foreach (var (label, value) in info.facts)
            {
                var card = ApexUi.Panel(layer, UiTokens.PanelAlpha);
                ApexUi.Place(card, fx, 440, 160, 74);
                card.style.paddingLeft = 14; card.style.paddingTop = 10;
                ApexUi.Caption(card, label);
                ApexUi.Text(card, value, ApexUi.DisplayStrong, 26, ApexUi.C(UiTokens.Ivory)).style.marginTop = 2;
                fx += 172f;
                if (fx > 900f) break;
            }

            // Bottom: tip, stage, progress line.
            tip = ApexUi.Text(layer, "", ApexUi.Body, UiTokens.Body + 2, ApexUi.C(UiTokens.Ivory, 0.86f));
            ApexUi.Place(tip, x, 900, 1200);
            tip.style.whiteSpace = WhiteSpace.Normal;
            var tipCaption = ApexUi.Caption(layer, Loc.T("TIPP"));
            ApexUi.Place(tipCaption, x, 874);
            tipCaption.style.color = ApexUi.C(UiTokens.Gold);
            barTrack = ApexUi.Box(layer, ApexUi.C(UiTokens.Surface));
            ApexUi.Place(barTrack, x, 1000, 1680, 3);
            bar = ApexUi.Box(barTrack, ApexUi.C(UiTokens.Cyan));
            ApexUi.Place(bar, 0, 0, 0, 3);
            stage = ApexUi.Text(layer, "", ApexUi.BodyStrong, UiTokens.Label + 1, ApexUi.C(UiTokens.Steel), 2.5f);
            ApexUi.Place(stage, x, 1012, 900);
            percentHint = ApexUi.Text(layer, "", ApexUi.Display, 20, ApexUi.C(UiTokens.Cyan), 2f, TextAnchor.MiddleRight);
            ApexUi.Place(percentHint, 1500, 1008, 300);
            var logo = ApexUi.Text(layer, "APEX", ApexUi.DisplayStrong, 34, ApexUi.C(UiTokens.Ivory, 0.9f), 12f, TextAnchor.MiddleRight);
            ApexUi.Place(logo, 1500, 56, 300);
            layer.style.opacity = 0f;
            ui.SetVisible(true);
            visible = true;
            Tick(0f);
        }

        static void Fill(VisualElement e)
        {
            e.style.position = Position.Absolute;
            e.style.left = 0; e.style.top = 0; e.style.right = 0; e.style.bottom = 0;
        }

        /// <summary>Per frame while visible: fade, the line easing towards the real progress, map drawing, tips.</summary>
        public void Tick(float dt)
        {
            if (!visible || model == null) return;
            time += dt;
            shown = Mathf.Min(1f, shown + dt / UiTokens.PanelTime);
            layer.style.opacity = shown;
            float target = model.Progress;
            // The line only follows real progress (eases to it, never ahead of it).
            drawnProgress = Mathf.Min(target, drawnProgress + Mathf.Max(0.004f, (target - drawnProgress) * Mathf.Min(1f, dt * 8f)));
            bar.style.width = 1680f * drawnProgress;
            if (map != null) map.Progress = Mathf.Max(map.Progress, drawnProgress);
            string current = model.Current;
            stage.text = string.IsNullOrEmpty(current) ? Loc.T("BEREIT") : current.ToUpperInvariant();
            percentHint.text = $"{Mathf.RoundToInt(target * 100f)} %";
            tip.text = LoadingModel.TipAt(time, seed);
        }

        public void Hide()
        {
            visible = false;
            ui.SetVisible(false);
            layer?.RemoveFromHierarchy();
            layer = null;
        }
    }
}
