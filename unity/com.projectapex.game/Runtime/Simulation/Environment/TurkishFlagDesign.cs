using System;

namespace Apex.Environment
{
    /// <summary>
    /// Geometry of the Turkish flag (Law No. 2994 of 1936 and Regulation 85/9034),
    /// in units of the flag height G: length 1.5 G; outer crescent circle diameter
    /// G/2 centred G/2 from the hoist; inner circle diameter 2G/5, offset G/16 towards
    /// the fly; star in a circle of diameter G/4 that touches the line through the
    /// crescent tips, with one point towards the hoist; white hoist sleeve G/30
    /// (not drawn on the cloth). Red is approximately Pantone 186 C.
    /// </summary>
    public static class TurkishFlagDesign
    {
        public const float AspectRatio = 1.5f;
        public static readonly (byte r, byte g, byte b) Red = (227, 10, 23);
        public static readonly (byte r, byte g, byte b) White = (255, 255, 255);

        const float OuterCx = 0.5f, OuterR = 0.25f;
        const float InnerCx = 0.5f + 1f / 16f, InnerR = 0.2f;

        /// <summary>x of the crescent tips (intersection of the two circles).</summary>
        public static float TipX
        {
            get
            {
                // (x-a)^2 - (x-b)^2 = R^2 - r^2  =>  x = (R^2 - r^2 + b^2 - a^2) / (2 (b - a))
                float a = OuterCx, b = InnerCx;
                return (OuterR * OuterR - InnerR * InnerR + b * b - a * a) / (2f * (b - a));
            }
        }

        public static float StarRadius => 0.125f;
        public static float StarCx => TipX + StarRadius;

        /// <summary>True if (x, y) — x from the hoist in [0, 1.5], y from the centre line in [-0.5, 0.5] — is white.</summary>
        public static bool IsWhite(float x, float y)
        {
            float dOuter = (x - OuterCx) * (x - OuterCx) + y * y;
            float dInner = (x - InnerCx) * (x - InnerCx) + y * y;
            if (dOuter <= OuterR * OuterR && dInner > InnerR * InnerR) return true;
            return InStar(x - StarCx, y, StarRadius);
        }

        /// <summary>Regular five-pointed star (inner/outer radius ratio 0.382) with one point towards -x.</summary>
        static bool InStar(float x, float y, float r)
        {
            if (x * x + y * y > r * r) return false;
            // Ten alternating outer/inner vertices, the first one pointing to the hoist (-x).
            const int n = 10;
            bool inside = false;
            double inner = r * 0.381966;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double ai = Math.PI + i * Math.PI / 5.0, aj = Math.PI + j * Math.PI / 5.0;
                double ri = i % 2 == 0 ? r : inner, rj = j % 2 == 0 ? r : inner;
                double xi = ri * Math.Cos(ai), yi = ri * Math.Sin(ai);
                double xj = rj * Math.Cos(aj), yj = rj * Math.Sin(aj);
                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
            }
            return inside;
        }

        /// <summary>RGBA pixels (row 0 = bottom) for a texture of the given height.</summary>
        public static byte[] Rasterize(int height, int samples = 3)
        {
            int width = (int)Math.Round(height * AspectRatio);
            var px = new byte[width * height * 4];
            for (int j = 0; j < height; j++)
                for (int i = 0; i < width; i++)
                {
                    int white = 0;
                    for (int sy = 0; sy < samples; sy++)
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float x = (i + (sx + 0.5f) / samples) / height;
                            float y = (j + (sy + 0.5f) / samples) / height - 0.5f;
                            if (IsWhite(x, y)) white++;
                        }
                    float w = white / (float)(samples * samples);
                    int k = (j * width + i) * 4;
                    px[k] = (byte)(Red.r + (White.r - Red.r) * w);
                    px[k + 1] = (byte)(Red.g + (White.g - Red.g) * w);
                    px[k + 2] = (byte)(Red.b + (White.b - Red.b) * w);
                    px[k + 3] = 255;
                }
            return px;
        }
    }
}
