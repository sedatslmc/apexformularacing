using System;
using System.Collections.Generic;

namespace Apex.UI
{
    /// <summary>
    /// Own 5×7 pixel font (uppercase Latin with the Turkish letters, digits, a few signs) for things the game draws itself:
    /// the pit wall monitors (PitWallDisplay) and the painted lettering on the pit lane (box numbers, "PIT", "80").
    /// Glyph rows top to bottom, bit 4 = leftmost column.
    /// </summary>
    public static class PixelFont
    {
        public const int W = 5, H = 7, Advance = 6;

        static readonly Dictionary<char, byte[]> Glyphs = new Dictionary<char, byte[]>
        {
            ['0'] = new byte[] { 0x0E, 0x11, 0x13, 0x15, 0x19, 0x11, 0x0E },
            ['1'] = new byte[] { 0x04, 0x0C, 0x04, 0x04, 0x04, 0x04, 0x0E },
            ['2'] = new byte[] { 0x0E, 0x11, 0x01, 0x02, 0x04, 0x08, 0x1F },
            ['3'] = new byte[] { 0x1F, 0x02, 0x04, 0x02, 0x01, 0x11, 0x0E },
            ['4'] = new byte[] { 0x02, 0x06, 0x0A, 0x12, 0x1F, 0x02, 0x02 },
            ['5'] = new byte[] { 0x1F, 0x10, 0x1E, 0x01, 0x01, 0x11, 0x0E },
            ['6'] = new byte[] { 0x06, 0x08, 0x10, 0x1E, 0x11, 0x11, 0x0E },
            ['7'] = new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x08, 0x08 },
            ['8'] = new byte[] { 0x0E, 0x11, 0x11, 0x0E, 0x11, 0x11, 0x0E },
            ['9'] = new byte[] { 0x0E, 0x11, 0x11, 0x0F, 0x01, 0x02, 0x0C },
            ['A'] = new byte[] { 0x0E, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 },
            ['B'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x1E },
            ['C'] = new byte[] { 0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E },
            ['D'] = new byte[] { 0x1C, 0x12, 0x11, 0x11, 0x11, 0x12, 0x1C },
            ['E'] = new byte[] { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x1F },
            ['F'] = new byte[] { 0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x10 },
            ['G'] = new byte[] { 0x0E, 0x11, 0x10, 0x17, 0x11, 0x11, 0x0F },
            ['H'] = new byte[] { 0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11 },
            ['I'] = new byte[] { 0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E },
            ['J'] = new byte[] { 0x07, 0x02, 0x02, 0x02, 0x02, 0x12, 0x0C },
            ['K'] = new byte[] { 0x11, 0x12, 0x14, 0x18, 0x14, 0x12, 0x11 },
            ['L'] = new byte[] { 0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x1F },
            ['M'] = new byte[] { 0x11, 0x1B, 0x15, 0x15, 0x11, 0x11, 0x11 },
            ['N'] = new byte[] { 0x11, 0x11, 0x19, 0x15, 0x13, 0x11, 0x11 },
            ['O'] = new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E },
            ['P'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10 },
            ['Q'] = new byte[] { 0x0E, 0x11, 0x11, 0x11, 0x15, 0x12, 0x0D },
            ['R'] = new byte[] { 0x1E, 0x11, 0x11, 0x1E, 0x14, 0x12, 0x11 },
            ['S'] = new byte[] { 0x0F, 0x10, 0x10, 0x0E, 0x01, 0x01, 0x1E },
            ['T'] = new byte[] { 0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04 },
            ['U'] = new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E },
            ['V'] = new byte[] { 0x11, 0x11, 0x11, 0x11, 0x11, 0x0A, 0x04 },
            ['W'] = new byte[] { 0x11, 0x11, 0x11, 0x15, 0x15, 0x15, 0x0A },
            ['X'] = new byte[] { 0x11, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0x11 },
            ['Y'] = new byte[] { 0x11, 0x11, 0x11, 0x0A, 0x04, 0x04, 0x04 },
            ['Z'] = new byte[] { 0x1F, 0x01, 0x02, 0x04, 0x08, 0x10, 0x1F },
            // Turkish letters (bilingual signs and names).
            ['Ç'] = new byte[] { 0x0E, 0x11, 0x10, 0x10, 0x11, 0x0E, 0x04 },
            ['Ğ'] = new byte[] { 0x0E, 0x00, 0x0F, 0x10, 0x17, 0x11, 0x0F },
            ['İ'] = new byte[] { 0x04, 0x00, 0x0E, 0x04, 0x04, 0x04, 0x0E },
            ['Ö'] = new byte[] { 0x0A, 0x00, 0x0E, 0x11, 0x11, 0x11, 0x0E },
            ['Ş'] = new byte[] { 0x0F, 0x10, 0x0E, 0x01, 0x1E, 0x04, 0x08 },
            ['Ü'] = new byte[] { 0x0A, 0x00, 0x11, 0x11, 0x11, 0x11, 0x0E },
            [' '] = new byte[] { 0, 0, 0, 0, 0, 0, 0 },
            ['.'] = new byte[] { 0, 0, 0, 0, 0, 0x0C, 0x0C },
            [','] = new byte[] { 0, 0, 0, 0, 0x0C, 0x04, 0x08 },
            [':'] = new byte[] { 0, 0x0C, 0x0C, 0, 0x0C, 0x0C, 0 },
            ['-'] = new byte[] { 0, 0, 0, 0x1F, 0, 0, 0 },
            ['+'] = new byte[] { 0, 0x04, 0x04, 0x1F, 0x04, 0x04, 0 },
            ['/'] = new byte[] { 0x01, 0x01, 0x02, 0x04, 0x08, 0x10, 0x10 },
            ['%'] = new byte[] { 0x18, 0x19, 0x02, 0x04, 0x08, 0x13, 0x03 },
            ['\''] = new byte[] { 0x04, 0x04, 0, 0, 0, 0, 0 },
            ['#'] = new byte[] { 0x0A, 0x1F, 0x0A, 0x0A, 0x1F, 0x0A, 0 },
            ['>'] = new byte[] { 0x08, 0x04, 0x02, 0x01, 0x02, 0x04, 0x08 },
            ['<'] = new byte[] { 0x02, 0x04, 0x08, 0x10, 0x08, 0x04, 0x02 },
        };

        /// <summary>Glyph of a character (lowercase maps to uppercase, Turkish ı/i too); unknown characters draw as '#'.</summary>
        public static byte[] Glyph(char c)
        {
            if (c == 'i') c = 'İ';
            else if (c == 'ı') c = 'I';
            else c = char.ToUpperInvariant(c);
            return Glyphs.TryGetValue(c, out var g) ? g : Glyphs['#'];
        }

        public static bool Has(char c) => Glyphs.ContainsKey(char.ToUpperInvariant(c)) || c == 'i' || c == 'ı';

        /// <summary>Width of a text in pixels (without the trailing gap).</summary>
        public static int Width(string text) => string.IsNullOrEmpty(text) ? 0 : text.Length * Advance - 1;

        /// <summary>Calls <paramref name="pixel"/>(x, y) for every set pixel, y = 0 at the top row, scaled by <paramref name="scale"/>.</summary>
        public static void Draw(string text, int x0, int y0, int scale, Action<int, int> pixel)
        {
            int x = x0;
            foreach (char c in text)
            {
                var g = Glyph(c);
                for (int row = 0; row < H; row++)
                    for (int col = 0; col < W; col++)
                        if ((g[row] & (0x10 >> col)) != 0)
                            for (int sy = 0; sy < scale; sy++)
                                for (int sx = 0; sx < scale; sx++)
                                    pixel(x + col * scale + sx, y0 + row * scale + sy);
                x += Advance * scale;
            }
        }

        /// <summary>
        /// Horizontal runs of set pixels of a text (column, row, length; row 0 at the top) – painted lettering is built
        /// from these as few quads as possible.
        /// </summary>
        public static List<(int x, int y, int len)> Runs(string text)
        {
            var runs = new List<(int, int, int)>();
            int w = Width(text);
            for (int row = 0; row < H; row++)
            {
                int start = -1;
                for (int x = 0; x <= w; x++)
                {
                    bool on = x < w && On(text, x, row);
                    if (on && start < 0) start = x;
                    if (!on && start >= 0) { runs.Add((start, row, x - start)); start = -1; }
                }
            }
            return runs;
        }

        static bool On(string text, int x, int row)
        {
            int ci = x / Advance, col = x % Advance;
            if (ci >= text.Length || col >= W) return false;
            return (Glyph(text[ci])[row] & (0x10 >> col)) != 0;
        }
    }
}
