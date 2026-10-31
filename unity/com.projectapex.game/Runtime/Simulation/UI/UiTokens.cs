using System;

namespace Apex.UI
{
    /// <summary>
    /// APEX // RACE CONTROL design tokens (docs/UI_UX_OVERHAUL.md section 2): colours, spacing, type scale and
    /// motion timings shared by every screen. Engine-free so the contrast rules are tested; the runtime turns them
    /// into UI Toolkit styles (Apex.Game.UI).
    /// </summary>
    public static class UiTokens
    {
        public readonly struct Rgb
        {
            public readonly float r, g, b;
            public Rgb(float r, float g, float b) { this.r = r; this.g = g; this.b = b; }

            public static Rgb Hex(uint v) => new Rgb(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f);
        }

        public static readonly Rgb Navy = Rgb.Hex(0x101820);
        public static readonly Rgb Carbon = Rgb.Hex(0x18232E);
        public static readonly Rgb Surface = Rgb.Hex(0x253440);
        public static readonly Rgb Cyan = Rgb.Hex(0x20D9E8);
        public static readonly Rgb Red = Rgb.Hex(0xC8102E);
        public static readonly Rgb Amber = Rgb.Hex(0xF4B544);
        public static readonly Rgb Ivory = Rgb.Hex(0xF5F7FA);
        public static readonly Rgb Steel = Rgb.Hex(0x9BA8B5);
        public static readonly Rgb FlagGreen = Rgb.Hex(0x2FBF71);
        public static readonly Rgb FlagYellow = Rgb.Hex(0xF2D21B);
        public static readonly Rgb FlagRed = Rgb.Hex(0xE0242F);
        public static readonly Rgb FlagBlue = Rgb.Hex(0x2F7FE0);
        /// <summary>Personal / session best (motorsport convention), used sparingly.</summary>
        public static readonly Rgb Best = Rgb.Hex(0x9B5CF6);

        /// <summary>Panel opacity: light overlays over the 3D scene, strong panels for dense information.</summary>
        public const float PanelAlpha = 0.78f, PanelStrongAlpha = 0.92f, HairlineAlpha = 0.22f;

        // Spacing on a 4 px grid (reference 1920 x 1080).
        public const float S1 = 4f, S2 = 8f, S3 = 12f, S4 = 16f, S5 = 24f, S6 = 32f, S7 = 48f, S8 = 64f;
        public const float ScreenMargin = 72f, MobileMargin = 24f, CutCorner = 14f;

        // Type scale (px at 1080 p).
        public const int Display = 88, H1 = 54, H2 = 40, MenuItem = 26, MenuPrimary = 40, Body = 17, Label = 13, Small = 14;
        public const int HudGear = 92, HudSpeed = 54, HudTiming = 24;

        // Motion (seconds).
        public const float FocusTime = 0.12f, PanelTime = 0.22f, ScreenTime = 0.32f, StaggerStep = 0.04f, CountUpMax = 0.6f;
        /// <summary>Slide distance (px) of entering elements; 0 with reduced motion.</summary>
        public const float SlideDistance = 36f;

        /// <summary>WCAG relative luminance of an sRGB colour.</summary>
        public static float Luminance(Rgb c)
        {
            float L(float v) => v <= 0.03928f ? v / 12.92f : MathF.Pow((v + 0.055f) / 1.055f, 2.4f);
            return 0.2126f * L(c.r) + 0.7152f * L(c.g) + 0.0722f * L(c.b);
        }

        /// <summary>WCAG contrast ratio (1 .. 21).</summary>
        public static float Contrast(Rgb a, Rgb b)
        {
            float la = Luminance(a), lb = Luminance(b);
            return (MathF.Max(la, lb) + 0.05f) / (MathF.Min(la, lb) + 0.05f);
        }

        /// <summary>A colour at <paramref name="alpha"/> over a background (sRGB blend, as the UI renders it).</summary>
        public static Rgb Over(Rgb fg, float alpha, Rgb bg) =>
            new Rgb(bg.r + (fg.r - bg.r) * alpha, bg.g + (fg.g - bg.g) * alpha, bg.b + (fg.b - bg.b) * alpha);
    }
}
