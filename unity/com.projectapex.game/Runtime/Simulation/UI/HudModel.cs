using System;
using System.Collections.Generic;

namespace Apex.UI
{
    /// <summary>HUD presets (docs/UI_UX_OVERHAUL.md section 4). "Aus" keeps only start lights and flag warnings.</summary>
    public enum HudPreset { Minimal = 0, Standard = 1, Extended = 2, Off = 3 }

    /// <summary>Movable HUD elements (the start lights stay fixed in the centre).</summary>
    public enum HudElement { Tower = 0, Info = 1, Events = 2, Dash = 3, Tyres = 4, Map = 5, Timing = 6 }

    public enum HudAnchor { TopLeft, TopCenter, TopRight, BottomLeft, BottomCenter, BottomRight }

    public struct HudRect
    {
        public float x, y, w, h;
        public HudRect(float x, float y, float w, float h) { this.x = x; this.y = y; this.w = w; this.h = h; }
        public float XMax => x + w;
        public float YMax => y + h;
        public bool Overlaps(HudRect o) => x < o.XMax && o.x < XMax && y < o.YMax && o.y < YMax;
    }

    /// <summary>The player's adjustment of one element (HUD editor), persisted in the settings.</summary>
    [Serializable]
    public sealed class HudElementLayout
    {
        public int element;
        public float dx, dy;
        public float scale = 1f, opacity = 1f;
    }

    [Serializable]
    public sealed class HudLayoutData
    {
        public HudElementLayout[] elements = new HudElementLayout[0];

        public const float MinScale = 0.7f, MaxScale = 1.4f, MinOpacity = 0.3f, MaxOpacity = 1f;

        public HudElementLayout Get(HudElement e)
        {
            foreach (var l in elements) if (l != null && l.element == (int)e) return l;
            var n = new HudElementLayout { element = (int)e };
            var list = new List<HudElementLayout>(elements) { n };
            elements = list.ToArray();
            return n;
        }

        /// <summary>Drops unknown entries and clamps values (old or edited save files). Returns true if changed.</summary>
        public bool Normalize()
        {
            bool changed = false;
            var seen = new HashSet<int>();
            var list = new List<HudElementLayout>();
            foreach (var l in elements ?? new HudElementLayout[0])
            {
                if (l == null || !Enum.IsDefined(typeof(HudElement), l.element) || !seen.Add(l.element)) { changed = true; continue; }
                float s = Math.Max(MinScale, Math.Min(MaxScale, l.scale)), o = Math.Max(MinOpacity, Math.Min(MaxOpacity, l.opacity));
                if (float.IsNaN(l.dx) || float.IsNaN(l.dy)) { l.dx = 0f; l.dy = 0f; changed = true; }
                if (s != l.scale || o != l.opacity) { l.scale = s; l.opacity = o; changed = true; }
                list.Add(l);
            }
            elements = list.ToArray();
            return changed;
        }
    }

    /// <summary>
    /// Where the HUD elements sit (reference 1080 px height, any width: anchors keep right-hand elements at the
    /// right edge on ultrawide screens), which elements a preset shows, and the keep-clear zone in front of the
    /// car that no element may cover by default.
    /// </summary>
    public static class HudLayout
    {
        public const float RefHeight = 1080f;
        public static readonly HudElement[] All = { HudElement.Tower, HudElement.Info, HudElement.Events, HudElement.Dash, HudElement.Tyres, HudElement.Map, HudElement.Timing };

        public static string Name(HudElement e)
        {
            switch (e)
            {
                case HudElement.Tower: return "Positionsturm";
                case HudElement.Info: return "Rennstatus";
                case HudElement.Events: return "Ereignisse";
                case HudElement.Dash: return "Cockpit-Anzeige";
                case HudElement.Tyres: return "Reifen";
                case HudElement.Map: return "Streckenkarte";
                default: return "Zeiten";
            }
        }

        public static string PresetName(HudPreset p)
        {
            switch (p)
            {
                case HudPreset.Minimal: return "Minimal";
                case HudPreset.Standard: return "Standard";
                case HudPreset.Extended: return "Erweitert";
                default: return "Aus";
            }
        }

        public static HudPreset Next(HudPreset p) => (HudPreset)(((int)p + 1) % 4);

        /// <summary>Minimal: dash, race status, events. Standard: all elements. Extended: plus telemetry. Off: none.</summary>
        public static bool Shows(HudPreset p, HudElement e)
        {
            switch (p)
            {
                case HudPreset.Off: return false;
                case HudPreset.Minimal: return e == HudElement.Dash || e == HudElement.Info || e == HudElement.Events;
                default: return true;
            }
        }

        public static bool ShowsTelemetry(HudPreset p) => p == HudPreset.Extended;

        /// <summary>Start lights and flag warnings stay visible even with the HUD off (safety information).</summary>
        public static bool ShowsSafetyInfo(HudPreset p) => true;

        static void Default(HudElement e, out HudAnchor a, out float ox, out float oy, out float w, out float h)
        {
            switch (e)
            {
                case HudElement.Tower: a = HudAnchor.TopLeft; ox = 40; oy = 40; w = 330; h = 300; break;
                // Leaves room for the pause / radio buttons in the top-right corner.
                case HudElement.Info: a = HudAnchor.TopRight; ox = 150; oy = 40; w = 340; h = 128; break;
                case HudElement.Events: a = HudAnchor.TopCenter; ox = 0; oy = 44; w = 600; h = 64; break;
                case HudElement.Dash: a = HudAnchor.BottomCenter; ox = 0; oy = 36; w = 520; h = 150; break;
                case HudElement.Tyres: a = HudAnchor.BottomLeft; ox = 40; oy = 36; w = 250; h = 150; break;
                case HudElement.Map: a = HudAnchor.BottomLeft; ox = 40; oy = 202; w = 250; h = 210; break;
                default: a = HudAnchor.BottomRight; ox = 40; oy = 36; w = 360; h = 190; break;
            }
        }

        public static HudAnchor AnchorOf(HudElement e) { Default(e, out var a, out _, out _, out _, out _); return a; }

        /// <summary>Default rectangle on a panel of the given size (no player adjustment).</summary>
        public static HudRect DefaultRect(HudElement e, float panelW, float panelH) => Rect(e, null, panelW, panelH);

        /// <summary>Final rectangle: anchored default, scaled about its anchor, moved by the player's offset, kept on screen.</summary>
        public static HudRect Rect(HudElement e, HudElementLayout l, float panelW, float panelH)
        {
            Default(e, out var a, out float ox, out float oy, out float w, out float h);
            float s = l?.scale ?? 1f;
            w *= s; h *= s;
            float x, y;
            switch (a)
            {
                case HudAnchor.TopLeft: case HudAnchor.BottomLeft: x = ox; break;
                case HudAnchor.TopRight: case HudAnchor.BottomRight: x = panelW - ox - w; break;
                default: x = (panelW - w) * 0.5f + ox; break;
            }
            y = a == HudAnchor.TopLeft || a == HudAnchor.TopCenter || a == HudAnchor.TopRight ? oy : panelH - oy - h;
            if (l != null) { x += l.dx; y += l.dy; }
            x = Math.Max(0f, Math.Min(panelW - w, x));
            y = Math.Max(0f, Math.Min(panelH - h, y));
            return new HudRect(x, y, w, h);
        }

        /// <summary>The road ahead of the car in the chase and cockpit views: elements should stay out of it.</summary>
        public static HudRect KeepClear(float panelW, float panelH) => new HudRect(panelW * 0.3f, panelH * 0.35f, panelW * 0.4f, panelH * 0.43f);

        /// <summary>Visible elements that cover the keep-clear zone or each other (shown as warnings in the HUD editor).</summary>
        public static List<string> Conflicts(HudLayoutData data, HudPreset preset, float panelW, float panelH)
        {
            var issues = new List<string>();
            var clear = KeepClear(panelW, panelH);
            var shown = new List<HudElement>();
            foreach (var e in All) if (Shows(preset, e)) shown.Add(e);
            for (int i = 0; i < shown.Count; i++)
            {
                var r = Rect(shown[i], data?.Get(shown[i]), panelW, panelH);
                if (r.Overlaps(clear)) issues.Add($"{Name(shown[i])} verdeckt die Fahrbahn");
                for (int j = i + 1; j < shown.Count; j++)
                    if (r.Overlaps(Rect(shown[j], data?.Get(shown[j]), panelW, panelH))) issues.Add($"{Name(shown[i])} überdeckt {Name(shown[j])}");
            }
            return issues;
        }
    }

    /// <summary>In-game HUD editor: pick an element, move, resize, fade, reset (changes the persisted layout).</summary>
    public sealed class HudEditor
    {
        public readonly HudLayoutData data;
        public int Selected { get; private set; }
        public HudElement Current => HudLayout.All[Selected];

        public HudEditor(HudLayoutData data) { this.data = data; }

        public void Select(int dir) => Selected = ((Selected + dir) % HudLayout.All.Length + HudLayout.All.Length) % HudLayout.All.Length;

        public void SelectElement(HudElement e) => Selected = Array.IndexOf(HudLayout.All, e);

        /// <summary>Moves the element; the offset is stored so that the element stays fully on screen.</summary>
        public void Nudge(float dx, float dy, float panelW, float panelH)
        {
            var l = data.Get(Current);
            l.dx += dx; l.dy += dy;
            // Rect clamps on screen; store the clamped displacement so the offset cannot drift off screen.
            var r = HudLayout.Rect(Current, l, panelW, panelH);
            var b = HudLayout.Rect(Current, new HudElementLayout { element = l.element, scale = l.scale }, panelW, panelH);
            l.dx = r.x - b.x; l.dy = r.y - b.y;
        }

        public void Resize(float delta)
        {
            var l = data.Get(Current);
            l.scale = Math.Max(HudLayoutData.MinScale, Math.Min(HudLayoutData.MaxScale, (float)Math.Round((l.scale + delta) * 100f) / 100f));
        }

        public void Fade(float delta)
        {
            var l = data.Get(Current);
            l.opacity = Math.Max(HudLayoutData.MinOpacity, Math.Min(HudLayoutData.MaxOpacity, (float)Math.Round((l.opacity + delta) * 100f) / 100f));
        }

        public void Reset()
        {
            var l = data.Get(Current);
            l.dx = 0f; l.dy = 0f; l.scale = 1f; l.opacity = 1f;
        }

        public void ResetAll() => data.elements = new HudElementLayout[0];
    }

    /// <summary>15 shift lights: 5 green, 5 red, 5 blue, filling from <see cref="StartFraction"/> of the limiter;
    /// at the shift point all flash.</summary>
    public static class ShiftLights
    {
        public const int Count = 15;
        public const float StartFraction = 0.72f, ShiftFraction = 0.975f;

        public enum Colour { Green, Red, Blue }

        public static Colour ColourOf(int i) => i < 5 ? Colour.Green : i < 10 ? Colour.Red : Colour.Blue;

        public static int Lit(float rpm, float limiterRpm)
        {
            float f = (rpm / Math.Max(1f, limiterRpm) - StartFraction) / (ShiftFraction - StartFraction);
            return Math.Max(0, Math.Min(Count, (int)Math.Ceiling(f * Count - 1e-4f)));
        }

        public static bool Flash(float rpm, float limiterRpm) => rpm >= limiterRpm * ShiftFraction;
    }

    public enum TyreBand { Cold, Optimal, Hot }

    public static class TyreTemps
    {
        /// <summary>Same bands as the interim HUD: more than 0.6 windows away from the optimum is cold or hot.</summary>
        public static TyreBand Band(float temperature, float optimal, float window)
        {
            float rel = (temperature - optimal) / Math.Max(1e-3f, window);
            return rel < -0.6f ? TyreBand.Cold : rel > 0.6f ? TyreBand.Hot : TyreBand.Optimal;
        }
    }
}
