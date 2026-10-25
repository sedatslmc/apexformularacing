using System.Collections.Generic;
using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// Project Apex visual language for the interim IMGUI layer (M0/M1): graphite
    /// translucent panels, cool neutral text, restrained cyan accent; purple =
    /// session best, green = improvement, yellow = caution, red = urgent.
    /// The production HUD (M4) will replace this with a layout-managed UI.
    /// </summary>
    public static class ApexStyle
    {
        public static readonly Color Panel = new Color(0.07f, 0.08f, 0.1f, 0.72f);
        public static readonly Color PanelSolid = new Color(0.07f, 0.08f, 0.1f, 0.94f);
        public static readonly Color Fg = new Color(0.88f, 0.91f, 0.94f);
        public static readonly Color TextDim = new Color(0.6f, 0.65f, 0.7f);
        public static readonly Color Accent = new Color(0.15f, 0.85f, 0.95f);
        public static readonly Color Best = new Color(0.72f, 0.4f, 1f);
        public static readonly Color Faster = new Color(0.3f, 0.9f, 0.45f);
        public static readonly Color Caution = new Color(1f, 0.82f, 0.25f);
        public static readonly Color Urgent = new Color(1f, 0.32f, 0.27f);

        static readonly Dictionary<Color, Texture2D> Textures = new Dictionary<Color, Texture2D>();
        static readonly Dictionary<(int, FontStyle, TextAnchor), GUIStyle> LabelStyles = new Dictionary<(int, FontStyle, TextAnchor), GUIStyle>();

        public static Texture2D Tex(Color c)
        {
            if (Textures.TryGetValue(c, out var t) && t != null) return t;
            t = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            Textures[c] = t;
            return t;
        }

        public static void Box(Rect r, Color c)
        {
            GUI.DrawTexture(r, Tex(c));
        }

        public static GUIStyle Label(int size, FontStyle style = FontStyle.Normal, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var key = (size, style, anchor);
            if (LabelStyles.TryGetValue(key, out var s)) return s;
            s = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = style, alignment = anchor, wordWrap = false, clipping = TextClipping.Overflow };
            LabelStyles[key] = s;
            return s;
        }

        public static void Text(Rect r, string text, int size, Color color, FontStyle style = FontStyle.Normal, TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.Label(r, text, Label(size, style, anchor));
            GUI.color = prev;
        }

        /// <summary>Menu button with focus highlight. Returns true when clicked/tapped.</summary>
        public static bool Button(Rect r, string text, bool focused, int size = 30, bool enabled = true)
        {
            Box(r, focused ? new Color(0.12f, 0.2f, 0.24f, 0.95f) : PanelSolid);
            if (focused) Box(new Rect(r.x, r.y, 5f, r.height), Accent);
            Text(new Rect(r.x + 24f, r.y, r.width - 30f, r.height), text, size, enabled ? Fg : TextDim, focused ? FontStyle.Bold : FontStyle.Normal);
            if (!enabled) return false;
            var e = Event.current;
            if (e.type == EventType.MouseUp && r.Contains(e.mousePosition))
            {
                e.Use();
                return true;
            }
            return false;
        }

        public static string FormatTime(double t)
        {
            if (t <= 0 || double.IsNaN(t)) return "-:--.---";
            long ms = (long)System.Math.Round(t * 1000.0);
            return $"{ms / 60000}:{(ms / 1000) % 60:00}.{ms % 1000:000}";
        }

        /// <summary>m:ss for session clocks.</summary>
        public static string FormatClock(double seconds)
        {
            if (double.IsInfinity(seconds) || double.IsNaN(seconds)) return "--:--";
            int s = (int)System.Math.Ceiling(System.Math.Max(0, seconds));
            return $"{s / 60}:{s % 60:00}";
        }

        public static string FormatDelta(double d)
        {
            if (double.IsNaN(d)) return "";
            return (d >= 0 ? "+" : "-") + System.Math.Abs(d).ToString("0.000");
        }
    }
}
