using System;

namespace Apex.Environment
{
    /// <summary>
    /// Procedural artwork of the original Bosphorus GP identity, drawn from signed distances so
    /// it needs no external assets: the round emblem (the strait as a turquoise S-curve, the
    /// suspension bridge in gold and a red apex arc on a navy disc) and a tileable İznik-inspired
    /// ceramic tile (tulip rosette in turquoise, red and navy on ivory). Output is RGBA in 0..1,
    /// row-major from the bottom row up (Unity's SetPixels order).
    /// </summary>
    public static class EventArt
    {
        // Palette #C8102E, #087F8C, #C9A66B, #1D2835, #F0E9D9.
        public static readonly float[] Red = { 0.784f, 0.063f, 0.18f };
        public static readonly float[] Turquoise = { 0.031f, 0.498f, 0.549f };
        public static readonly float[] Gold = { 0.788f, 0.651f, 0.42f };
        public static readonly float[] Navy = { 0.114f, 0.157f, 0.208f };
        public static readonly float[] Ivory = { 0.941f, 0.914f, 0.851f };

        static float Smooth(float edge, float d, float aa) => Clamp01(0.5f - d / aa + edge);
        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        static void Over(float[] px, int k, float[] c, float a)
        {
            if (a <= 0f) return;
            for (int i = 0; i < 3; i++) px[k + i] = px[k + i] * (1f - a) + c[i] * a;
            px[k + 3] = px[k + 3] + (1f - px[k + 3]) * a;
        }

        /// <summary>Distance from p to the segment ab.</summary>
        static float Segment(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float t = Math.Max(0f, Math.Min(1f, ((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy)));
            float ex = px - ax - t * dx, ey = py - ay - t * dy;
            return MathF.Sqrt(ex * ex + ey * ey);
        }

        /// <summary>The Bosphorus centreline across the emblem (x, y in -0.5..0.5).</summary>
        public static float StraitY(float x) => -0.2f + 0.07f * MathF.Sin(x * 9f + 0.6f);

        /// <summary>Round emblem, transparent outside the disc.</summary>
        public static float[] Emblem(int n)
        {
            var px = new float[n * n * 4];
            float aa = 1.5f / n;
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float x = (i + 0.5f) / n - 0.5f, y = (j + 0.5f) / n - 0.5f;
                    int k = (j * n + i) * 4;
                    float r = MathF.Sqrt(x * x + y * y);
                    // Disc with a gold rim and an ivory inner ring.
                    Over(px, k, Gold, Smooth(0f, r - 0.49f, aa));
                    Over(px, k, Navy, Smooth(0f, r - 0.465f, aa));
                    float ring = MathF.Abs(r - 0.43f) - 0.008f;
                    Over(px, k, Ivory, Smooth(0f, ring, aa));
                    float inside = Smooth(0f, r - 0.42f, aa);
                    // The strait: turquoise band, widening to the south like the real channel mouth.
                    float band = MathF.Abs(y - StraitY(x)) - (0.05f + 0.025f * (0.5f - x));
                    Over(px, k, Turquoise, Smooth(0f, band, aa) * inside);
                    // Suspension bridge: two towers, deck, main cable and hangers.
                    const float tx = 0.2f, deck = -0.08f, top = 0.2f;
                    float towers = Math.Min(Segment(x, y, -tx, -0.2f, -tx, top), Segment(x, y, tx, -0.2f, tx, top)) - 0.014f;
                    float deckD = Segment(x, y, -0.4f, deck, 0.4f, deck) - 0.009f;
                    float cableY = deck + 0.02f + (top - deck - 0.02f) * (x / tx) * (x / tx);
                    float cable = MathF.Abs(x) <= tx ? MathF.Abs(y - cableY) - 0.006f
                        : Segment(x, y, MathF.Sign(x) * tx, top, MathF.Sign(x) * 0.4f, deck + 0.01f) - 0.006f;
                    float hanger = 1f;
                    for (int h = -3; h <= 3; h++)
                    {
                        float hx = h * tx / 4f;
                        float hy = deck + 0.02f + (top - deck - 0.02f) * (hx / tx) * (hx / tx);
                        hanger = Math.Min(hanger, Segment(x, y, hx, deck, hx, hy) - 0.003f);
                    }
                    float bridge = Math.Min(Math.Min(towers, deckD), Math.Min(cable, hanger));
                    Over(px, k, Gold, Smooth(0f, bridge, aa) * inside);
                    // Red apex arc: a racing line sweeping over the bridge.
                    float ax = x, ay = y + 0.78f;
                    float arc = MathF.Abs(MathF.Sqrt(ax * ax + ay * ay) - 1.06f) - 0.028f * (1f - MathF.Abs(x) * 1.6f);
                    if (MathF.Abs(x) < 0.36f) Over(px, k, Red, Smooth(0f, arc, aa) * inside);
                }
            return px;
        }

        /// <summary>
        /// Tileable İznik-inspired tile: a central four-petal tulip rosette, quarter rosettes in the
        /// corners (they join across tile edges), turquoise leaves and a thin grout line.
        /// </summary>
        /// <summary>
        /// Fence banner (w x h, 3:1 recommended) of the event identity, without text: variant 0 navy with
        /// red crescent rings and a gold band, 1 red with ivory rings and a navy band, 2 turquoise with
        /// ivory rings and a gold band; the emblem sits at the left end.
        /// </summary>
        public static float[] Banner(int variant, int w, int h)
        {
            float[][] bg = { Navy, Red, Turquoise };
            float[][] fg = { Red, Ivory, Ivory };
            float[][] band = { Gold, Navy, Gold };
            int v = ((variant % 3) + 3) % 3;
            var px = new float[w * h * 4];
            int en = h;
            var emblem = Emblem(en);
            float aa = 1.5f / h;
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    int k = (j * w + i) * 4;
                    float x = (i + 0.5f) / h, y = (j + 0.5f) / h;   // units of banner height
                    Over(px, k, bg[v], 1f);
                    // Accent band near the bottom.
                    Over(px, k, band[v], Smooth(0f, MathF.Abs(y - 0.14f) - 0.045f, aa));
                    // Interlocking crescent rings along the banner (offset discs), right of the emblem.
                    if (x > 1.15f)
                    {
                        float cx = 1.55f + MathF.Round((x - 1.55f) / 0.62f) * 0.62f;
                        float dOuter = MathF.Sqrt((x - cx) * (x - cx) + (y - 0.56f) * (y - 0.56f)) - 0.24f;
                        float dInner = MathF.Sqrt((x - cx - 0.06f) * (x - cx - 0.06f) + (y - 0.56f) * (y - 0.56f)) - 0.19f;
                        Over(px, k, fg[v], Smooth(0f, MathF.Max(dOuter, -dInner), aa));
                    }
                    // Emblem at the left end.
                    if (i < en)
                    {
                        int e = (j * en + i) * 4;
                        Over(px, k, new[] { emblem[e], emblem[e + 1], emblem[e + 2] }, emblem[e + 3]);
                    }
                    px[k + 3] = 1f;
                }
            return px;
        }

        public static float[] IznikTile(int n)
        {
            var px = new float[n * n * 4];
            float aa = 1.5f / n;
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float x = (i + 0.5f) / n - 0.5f, y = (j + 0.5f) / n - 0.5f;
                    int k = (j * n + i) * 4;
                    for (int c = 0; c < 3; c++) px[k + c] = Ivory[c];
                    px[k + 3] = 1f;
                    // Corner quarter-rosette: nearest corner as the second centre.
                    float cx = x < 0f ? -0.5f : 0.5f, cy = y < 0f ? -0.5f : 0.5f;
                    Rosette(px, k, x, y, 0f, 0f, 0.3f, aa, Red, Navy);
                    Rosette(px, k, x, y, cx, cy, 0.2f, aa, Turquoise, Navy);
                    // Leaves on the diagonals between the rosettes.
                    float lx = MathF.Abs(x) - 0.25f, ly = MathF.Abs(y) - 0.25f;
                    float leaf = Ellipse(lx, ly, 0.785398f, 0.075f, 0.028f);
                    Over(px, k, Turquoise, Smooth(0f, leaf, aa));
                    // Grout.
                    float edge = 0.5f - Math.Max(MathF.Abs(x), MathF.Abs(y));
                    float grout = Smooth(0f, edge - 0.008f, aa);
                    Over(px, k, new[] { 0.62f, 0.6f, 0.56f }, grout);
                }
            return px;
        }

        /// <summary>Approximate signed distance to an ellipse (semi-axes a along the angle, b across).</summary>
        static float Ellipse(float x, float y, float angle, float a, float b)
        {
            float c = MathF.Cos(angle), s = MathF.Sin(angle);
            float u = x * c + y * s, v = -x * s + y * c;
            float q = MathF.Sqrt(u * u / (a * a) + v * v / (b * b));
            return (q - 1f) * Math.Min(a, b);
        }

        static void Rosette(float[] px, int k, float x, float y, float ox, float oy, float size, float aa, float[] petal, float[] centre)
        {
            float dx = x - ox, dy = y - oy;
            float d = 1f;
            for (int p = 0; p < 4; p++)
            {
                float ang = p * MathF.PI * 0.5f;
                float ex = dx - MathF.Cos(ang) * size * 0.5f, ey = dy - MathF.Sin(ang) * size * 0.5f;
                d = Math.Min(d, Ellipse(ex, ey, ang, size * 0.5f, size * 0.22f));
            }
            Over(px, k, petal, Smooth(0f, d, aa));
            // Thin navy outline of the petals and a navy/ivory centre.
            Over(px, k, centre, Smooth(0f, MathF.Abs(d) - 0.004f, aa) * 0.9f);
            float r = MathF.Sqrt(dx * dx + dy * dy);
            Over(px, k, centre, Smooth(0f, r - size * 0.2f, aa));
            Over(px, k, Ivory, Smooth(0f, r - size * 0.08f, aa));
        }
    }
}
