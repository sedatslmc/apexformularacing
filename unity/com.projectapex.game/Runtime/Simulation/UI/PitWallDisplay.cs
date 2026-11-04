using System;
using System.Collections.Generic;

namespace Apex.UI
{
    /// <summary>
    /// The pit wall monitors (docs/PIT_LANE.md, "Pitwall"): live race data drawn into one RGBA image of four 128×64 panels –
    /// timing (positions, codes, team colours, gaps, sector colours), telemetry (speed trace, throttle and brake), tyre
    /// strategy (compound, wear, stops) and a weather radar. Pure pixels from the game's data (PixelFont for text), so it
    /// is tested without Unity; PitWallComponent uploads it to the screens' texture a few times a second.
    /// </summary>
    public static class PitWallDisplay
    {
        public const int Width = 256, Height = 128, PanelW = 128, PanelH = 64;
        public enum Panel { Timing, Telemetry, Strategy, Radar }

        public struct Row
        {
            public string code;
            /// <summary>Gap or lap time text ("+1.2", "1:32.4", "BOX").</summary>
            public string gap;
            public uint team;
            /// <summary>Colour of the current compound and the tyre wear (0..1).</summary>
            public uint compound;
            public float wear;
            public int stops;
            /// <summary>Sector colours of the last lap (0 none, 1 yellow, 2 green personal best, 3 purple overall best).</summary>
            public int s1, s2, s3;
            public bool player;
            public bool inPit;
        }

        public sealed class Data
        {
            public readonly List<Row> rows = new List<Row>();
            /// <summary>Speed samples of the followed car, oldest first (m/s).</summary>
            public readonly List<float> speedTrace = new List<float>();
            public float throttle, brake;
            /// <summary>Lap / total, leader's lap.</summary>
            public int lap, laps;
            /// <summary>0..1 rain intensity, 0..1 track wetness, seconds for the radar sweep and cells.</summary>
            public float rain, wetness, time;
            public int seed = 7;
        }

        public static uint Rgba(byte r, byte g, byte b, byte a = 255) => (uint)(r | (g << 8) | (b << 16) | (a << 24));
        public static uint Rgba(float r, float g, float b) => Rgba((byte)(Clamp01(r) * 255f), (byte)(Clamp01(g) * 255f), (byte)(Clamp01(b) * 255f));
        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        static readonly uint Bg = Rgba(8, 12, 20), Grid = Rgba(24, 34, 48), Text = Rgba(220, 228, 236), Dim = Rgba(110, 122, 138),
            Cyan = Rgba(0, 200, 230), Red = Rgba(230, 40, 50), Green = Rgba(40, 210, 90), Yellow = Rgba(240, 200, 40), Purple = Rgba(170, 70, 230),
            Header = Rgba(16, 26, 40);

        /// <summary>Origin (top-left, y down) of a panel in the image.</summary>
        public static (int x, int y) Origin(Panel p) => ((int)p % 2 * PanelW, (int)p / 2 * PanelH);

        /// <summary>Renders all panels into <paramref name="px"/> (Width × Height, row 0 at the top).</summary>
        public static void Render(Data d, uint[] px)
        {
            if (px.Length < Width * Height) throw new ArgumentException("buffer too small");
            Fill(px, 0, 0, Width, Height, Bg);
            Timing(d, px);
            Telemetry(d, px);
            Strategy(d, px);
            Radar(d, px);
        }

        static void Set(uint[] px, int x, int y, uint c)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            px[y * Width + x] = c;
        }

        static void Fill(uint[] px, int x0, int y0, int w, int h, uint c)
        {
            for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                    Set(px, x, y, c);
        }

        static void Print(uint[] px, string s, int x, int y, uint c, int maxX = int.MaxValue)
        {
            PixelFont.Draw(s, x, y, 1, (a, b) => { if (a < maxX) Set(px, a, b, c); });
        }

        static void Title(uint[] px, Panel p, string title, string right)
        {
            var (ox, oy) = Origin(p);
            Fill(px, ox, oy, PanelW, 9, Header);
            Print(px, title, ox + 2, oy + 1, Cyan);
            if (!string.IsNullOrEmpty(right)) Print(px, right, ox + PanelW - 2 - PixelFont.Width(right), oy + 1, Dim);
            // Panel frame.
            for (int x = ox; x < ox + PanelW; x++) { Set(px, x, oy + PanelH - 1, Grid); }
            for (int y = oy; y < oy + PanelH; y++) { Set(px, ox + PanelW - 1, y, Grid); }
        }

        static uint SectorColour(int s) => s == 3 ? Purple : s == 2 ? Green : s == 1 ? Yellow : Grid;

        static void Timing(Data d, uint[] px)
        {
            var (ox, oy) = Origin(Panel.Timing);
            Title(px, Panel.Timing, "TIMING", d.laps > 0 ? $"L{d.lap}/{d.laps}" : "");
            for (int i = 0; i < Math.Min(6, d.rows.Count); i++)
            {
                var r = d.rows[i];
                int y = oy + 11 + i * 9;
                if (r.player) Fill(px, ox, y - 1, PanelW - 1, 9, Rgba(20, 44, 60));
                Print(px, (i + 1).ToString(), ox + 2, y, Dim);
                Fill(px, ox + 14, y, 2, 7, r.team);
                Print(px, r.code ?? "", ox + 18, y, Text);
                Print(px, r.inPit ? "BOX" : r.gap ?? "", ox + 40, y, r.inPit ? Yellow : Text, ox + 100);
                // Sector boxes of the last lap.
                Fill(px, ox + 103, y + 2, 6, 3, SectorColour(r.s1));
                Fill(px, ox + 110, y + 2, 6, 3, SectorColour(r.s2));
                Fill(px, ox + 117, y + 2, 6, 3, SectorColour(r.s3));
            }
        }

        static void Telemetry(Data d, uint[] px)
        {
            var (ox, oy) = Origin(Panel.Telemetry);
            float last = d.speedTrace.Count > 0 ? d.speedTrace[d.speedTrace.Count - 1] : 0f;
            Title(px, Panel.Telemetry, "SPEED", $"{last * 3.6f:0} KMH");
            // Grid lines every 100 km/h, trace scaled to 0..360 km/h over the plot.
            int top = oy + 11, bottom = oy + 50, left = ox + 2, right = ox + PanelW - 4;
            for (int k = 1; k <= 3; k++)
            {
                int y = bottom - (int)((bottom - top) * k / 3.6f);
                for (int x = left; x < right; x += 2) Set(px, x, y, Grid);
            }
            int n = d.speedTrace.Count;
            int prevY = -1;
            for (int x = left; x < right; x++)
            {
                if (n < 2) break;
                int i = (int)((x - left) / (float)(right - left - 1) * (n - 1));
                float kmh = d.speedTrace[Math.Min(n - 1, i)] * 3.6f;
                int y = bottom - (int)(Clamp01(kmh / 360f) * (bottom - top));
                if (prevY >= 0)
                    for (int yy = Math.Min(prevY, y); yy <= Math.Max(prevY, y); yy++) Set(px, x, yy, Cyan);
                prevY = y;
            }
            // Throttle and brake bars.
            int barY = oy + 53;
            Print(px, "T", ox + 2, barY, Dim);
            Fill(px, ox + 9, barY + 1, 50, 5, Grid);
            Fill(px, ox + 9, barY + 1, (int)(50 * Clamp01(d.throttle)), 5, Green);
            Print(px, "B", ox + 64, barY, Dim);
            Fill(px, ox + 71, barY + 1, 50, 5, Grid);
            Fill(px, ox + 71, barY + 1, (int)(50 * Clamp01(d.brake)), 5, Red);
        }

        static void Strategy(Data d, uint[] px)
        {
            var (ox, oy) = Origin(Panel.Strategy);
            Title(px, Panel.Strategy, "TYRES", "WEAR");
            for (int i = 0; i < Math.Min(6, d.rows.Count); i++)
            {
                var r = d.rows[i];
                int y = oy + 11 + i * 9;
                Fill(px, ox + 2, y, 2, 7, r.team);
                Print(px, r.code ?? "", ox + 6, y, Text);
                // Compound disc, stops, then the life left as a bar in the compound colour.
                Fill(px, ox + 26, y + 1, 5, 5, r.compound);
                Print(px, r.stops.ToString(), ox + 34, y, Dim);
                int life = (int)(60 * Clamp01(1f - r.wear));
                Fill(px, ox + 44, y + 2, 60, 3, Grid);
                Fill(px, ox + 44, y + 2, life, 3, r.wear > 0.7f ? Red : r.compound);
                Print(px, $"{(int)(r.wear * 100f)}%", ox + 107, y, r.wear > 0.7f ? Red : Dim, ox + PanelW - 1);
            }
        }

        static void Radar(Data d, uint[] px)
        {
            var (ox, oy) = Origin(Panel.Radar);
            Title(px, Panel.Radar, "RADAR", d.rain > 0.05f ? $"RAIN {(int)(d.rain * 100f)}%" : "DRY");
            int cx = ox + 64, cy = oy + 37, r = 25;
            // Rain cells: drifting blobs, more and denser with the rain intensity.
            int cells = 1 + (int)(d.rain * 7f);
            var rng = new Random(d.seed);
            for (int c = 0; c < cells; c++)
            {
                float bx = (float)rng.NextDouble() * 2f - 1f, by = (float)rng.NextDouble() * 2f - 1f;
                float size = 0.25f + (float)rng.NextDouble() * 0.35f;
                bx += (d.time * 0.004f) % 2f; if (bx > 1.2f) bx -= 2.4f;
                float strength = d.rain * (0.6f + (float)rng.NextDouble() * 0.4f);
                if (strength < 0.03f) continue;
                for (int y = -r; y <= r; y++)
                    for (int x = -r; x <= r; x++)
                    {
                        if (x * x + y * y > r * r) continue;
                        float dx = x / (float)r - bx, dy = y / (float)r - by;
                        float v = 1f - (dx * dx + dy * dy) / (size * size);
                        if (v <= 0f) continue;
                        float k = v * strength;
                        uint col = k > 0.55f ? Red : k > 0.3f ? Yellow : Green;
                        Set(px, cx + x, cy + y, col);
                    }
            }
            // Rings, the circuit's position in the middle, and the sweep line.
            for (int a = 0; a < 360; a += 2)
            {
                float rad = a * MathF.PI / 180f;
                foreach (int rr in new[] { r, r / 2 })
                    Set(px, cx + (int)(MathF.Cos(rad) * rr), cy + (int)(MathF.Sin(rad) * rr), Grid);
            }
            Fill(px, cx - 1, cy - 1, 3, 3, Cyan);
            float sweep = d.time * 1.5f;
            for (int k = 0; k < r; k++)
                Set(px, cx + (int)(MathF.Cos(sweep) * k), cy + (int)(MathF.Sin(sweep) * k), Cyan);
            Print(px, $"TRACK {(int)(d.wetness * 100f)}%", ox + 2, oy + PanelH - 9, Dim);
        }
    }
}
