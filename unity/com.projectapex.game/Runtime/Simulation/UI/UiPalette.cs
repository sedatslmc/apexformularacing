using System;

namespace Apex.UI
{
    public enum ColourVision { Standard = 0, Deuteranopia = 1, Protanopia = 2, Tritanopia = 3 }

    /// <summary>
    /// Semantic colours of the HUD (good/bad, cold/optimal/hot, sectors) for normal vision and the three
    /// dichromacies. Every pair that carries meaning stays apart under the matching simulation
    /// (<see cref="Simulate"/>, Machado et al. 2009, severity 1). Shapes and text carry the same information too
    /// (+/− deltas, temperatures in °C), colour only reinforces it.
    /// </summary>
    public sealed class UiPalette
    {
        public UiTokens.Rgb good, bad, warn, best, cold, optimal, hot;
        public UiTokens.Rgb sector1, sector2, sector3;

        public static UiPalette Current = For(ColourVision.Standard);
        public static bool HighContrast;

        public static string Name(ColourVision v)
        {
            switch (v)
            {
                case ColourVision.Deuteranopia: return Loc.T("Deuteranopie");
                case ColourVision.Protanopia: return Loc.T("Protanopie");
                case ColourVision.Tritanopia: return Loc.T("Tritanopie");
                default: return Loc.T("Standardsicht");
            }
        }

        public static UiPalette For(ColourVision v)
        {
            switch (v)
            {
                case ColourVision.Deuteranopia:
                case ColourVision.Protanopia:
                    // Red/green confusion: blue vs orange, cold = blue, hot = orange-red, optimal = light grey-white.
                    return new UiPalette
                    {
                        good = UiTokens.Rgb.Hex(0x3B8EEA), bad = UiTokens.Rgb.Hex(0xF08A24), warn = UiTokens.Rgb.Hex(0xF2D21B), best = UiTokens.Rgb.Hex(0xB98CFF),
                        cold = UiTokens.Rgb.Hex(0x2F6FD6), optimal = UiTokens.Rgb.Hex(0xE8EDF2), hot = UiTokens.Rgb.Hex(0xF08A24),
                        sector1 = UiTokens.Rgb.Hex(0xF08A24), sector2 = UiTokens.Rgb.Hex(0x3B8EEA), sector3 = UiTokens.Rgb.Hex(0xF5F7FA),
                    };
                case ColourVision.Tritanopia:
                    // Blue/yellow confusion: teal vs red-pink, cold = teal, hot = red.
                    return new UiPalette
                    {
                        good = UiTokens.Rgb.Hex(0x20C9C0), bad = UiTokens.Rgb.Hex(0xE0245E), warn = UiTokens.Rgb.Hex(0xFF7A8A), best = UiTokens.Rgb.Hex(0xF5F7FA),
                        cold = UiTokens.Rgb.Hex(0x20C9C0), optimal = UiTokens.Rgb.Hex(0xE8EDF2), hot = UiTokens.Rgb.Hex(0xE0245E),
                        sector1 = UiTokens.Rgb.Hex(0xE0245E), sector2 = UiTokens.Rgb.Hex(0x20C9C0), sector3 = UiTokens.Rgb.Hex(0xF5F7FA),
                    };
                default:
                    return new UiPalette
                    {
                        good = UiTokens.FlagGreen, bad = UiTokens.FlagRed, warn = UiTokens.Amber, best = UiTokens.Best,
                        cold = UiTokens.FlagBlue, optimal = UiTokens.FlagGreen, hot = UiTokens.FlagRed,
                        sector1 = UiTokens.Red, sector2 = UiTokens.Cyan, sector3 = UiTokens.Amber,
                    };
            }
        }

        static float ToLinear(float v) => v <= 0.04045f ? v / 12.92f : (float)Math.Pow((v + 0.055f) / 1.055f, 2.4f);
        static float ToSrgb(float v) => v <= 0.0031308f ? v * 12.92f : 1.055f * (float)Math.Pow(v, 1f / 2.4f) - 0.055f;

        static readonly float[][] Machado =
        {
            null,
            new[] { 0.367322f, 0.860646f, -0.227968f, 0.280085f, 0.672501f, 0.047413f, -0.011820f, 0.042940f, 0.968881f },   // deuteranopia
            new[] { 0.152286f, 1.052583f, -0.204868f, 0.114503f, 0.786281f, 0.099216f, -0.003882f, -0.048116f, 1.051998f },  // protanopia
            new[] { 1.255528f, -0.076749f, -0.178779f, -0.078411f, 0.930809f, 0.147602f, 0.004733f, 0.691367f, 0.303900f },  // tritanopia
        };

        /// <summary>How a colour looks with the given colour vision (linear-RGB matrix, clamped).</summary>
        public static UiTokens.Rgb Simulate(UiTokens.Rgb c, ColourVision v)
        {
            var m = Machado[(int)v];
            if (m == null) return c;
            float r = ToLinear(c.r), g = ToLinear(c.g), b = ToLinear(c.b);
            float Clamp(float x) => Math.Max(0f, Math.Min(1f, x));
            return new UiTokens.Rgb(ToSrgb(Clamp(m[0] * r + m[1] * g + m[2] * b)), ToSrgb(Clamp(m[3] * r + m[4] * g + m[5] * b)), ToSrgb(Clamp(m[6] * r + m[7] * g + m[8] * b)));
        }

        /// <summary>CIE76 colour difference (ΔE in L*a*b*, D65).</summary>
        public static float DeltaE(UiTokens.Rgb a, UiTokens.Rgb b)
        {
            Lab(a, out float l1, out float a1, out float b1);
            Lab(b, out float l2, out float a2, out float b2);
            return (float)Math.Sqrt((l1 - l2) * (l1 - l2) + (a1 - a2) * (a1 - a2) + (b1 - b2) * (b1 - b2));
        }

        static void Lab(UiTokens.Rgb c, out float l, out float a, out float b)
        {
            float r = ToLinear(c.r), g = ToLinear(c.g), bl = ToLinear(c.b);
            float x = (0.4124f * r + 0.3576f * g + 0.1805f * bl) / 0.95047f;
            float y = 0.2126f * r + 0.7152f * g + 0.0722f * bl;
            float z = (0.0193f * r + 0.1192f * g + 0.9505f * bl) / 1.08883f;
            float F(float t) => t > 0.008856f ? (float)Math.Pow(t, 1.0 / 3.0) : 7.787f * t + 16f / 116f;
            float fx = F(x), fy = F(y), fz = F(z);
            l = 116f * fy - 16f;
            a = 500f * (fx - fy);
            b = 200f * (fy - fz);
        }
    }
}
