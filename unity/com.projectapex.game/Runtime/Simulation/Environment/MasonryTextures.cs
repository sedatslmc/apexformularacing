using System;

namespace Apex.Environment
{
    /// <summary>
    /// Procedural, seamlessly tiling wall textures for the landmark models (no external assets):
    /// <list type="bullet">
    /// <item><b>Marble ashlar</b> (palaces, mosque): courses of 0.6 m, blocks of 1.2 m staggered by half a block,
    /// 8 mm joints, slight per-block tint, faint grey veining. One repeat covers <see cref="MarbleTile"/> metres.</item>
    /// <item><b>Lime plaster</b> (Akaretler row houses): fine trowel mottling and soft weathering streaks, no joints.
    /// One repeat covers <see cref="PlasterTile"/> metres.</item>
    /// </list>
    /// Output: linear RGBA bytes (albedo, multiplied with the material colour, so its mean is close to 1) and a
    /// tangent-space normal map as RGB = n * 0.5 + 0.5 (the project's convention for runtime normal maps).
    /// The landmark meshes get metric box-projected UVs, so the texture scale is 1 / tile.
    /// </summary>
    public static class MasonryTextures
    {
        public const float MarbleTile = 2.4f, PlasterTile = 2.0f;
        public const float CourseHeight = 0.6f, BlockLength = 1.2f, JointWidth = 0.008f;

        public static (byte[] albedo, byte[] normal) Marble(int n)
        {
            var h = new float[n * n];
            var a = new float[n * n];
            float px = MarbleTile / n;
            int courses = (int)Math.Round(MarbleTile / CourseHeight);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = x * px, v = y * px;
                    int course = (int)(v / CourseHeight);
                    float shift = (course % 2) * BlockLength * 0.5f;
                    float bu = Mod(u + shift, MarbleTile);
                    int block = (int)(bu / BlockLength);
                    // Distance to the nearest joint (in metres) gives the joint groove and a small arris bevel.
                    float dv = MathF.Min(Mod(v, CourseHeight), CourseHeight - Mod(v, CourseHeight));
                    float du = MathF.Min(Mod(bu, BlockLength), BlockLength - Mod(bu, BlockLength));
                    float d = MathF.Min(du, dv);
                    float height = d < JointWidth * 0.5f ? 0f : MathF.Min(1f, (d - JointWidth * 0.5f) / 0.006f);
                    // Per-block tint (±3 %) and veining: thin bands of a warped sine, seamless (integer frequencies).
                    float tint = 1f + 0.03f * Hash(block + course * 17, courses);
                    float s = x / (float)n, t = y / (float)n;
                    float warp = 0.35f * Fbm(s, t, 4, 7);
                    float vein = MathF.Abs(MathF.Sin(2f * MathF.PI * (2f * s + 1f * t + warp)));
                    float veinDark = MathF.Pow(1f - vein, 40f) * 0.07f;
                    float mottle = 0.04f * Fbm(s, t, 16, 3);
                    float albedo = tint * (1f - veinDark + mottle);
                    if (height <= 0f) albedo *= 0.72f;   // mortar
                    a[y * n + x] = albedo;
                    h[y * n + x] = height * 0.9f + 0.1f * (0.5f + 0.5f * Fbm(s, t, 32, 5));
                }
            return (Pack(a, n, new[] { 1f, 0.99f, 0.96f }), NormalFromHeight(h, n, 1.6f));
        }

        public static (byte[] albedo, byte[] normal) Plaster(int n)
        {
            var h = new float[n * n];
            var a = new float[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float s = x / (float)n, t = y / (float)n;
                    float trowel = Fbm(s, t, 8, 11);
                    float fine = Fbm(s, t, 48, 13);
                    // Vertical weathering streaks (rain run-off), seamless in both directions.
                    float streak = 0.5f + 0.5f * MathF.Sin(2f * MathF.PI * (5f * s + 0.3f * Fbm(s, t, 2, 17)));
                    a[y * n + x] = 1f + 0.05f * trowel + 0.025f * fine - 0.04f * streak * (0.5f + 0.5f * Fbm(s, t, 3, 19));
                    h[y * n + x] = 0.5f + 0.35f * trowel + 0.15f * fine;
                }
            return (Pack(a, n, new[] { 1f, 0.98f, 0.95f }), NormalFromHeight(h, n, 0.8f));
        }

        static float Mod(float a, float m) => a - m * MathF.Floor(a / m);

        /// <summary>Deterministic value in [-1, 1] for an integer key (wraps with period so blocks tile).</summary>
        static float Hash(int k, int period)
        {
            uint q = (uint)(((k % 97) + 97) % 97 * 374761393 + period * 668265263);
            q = (q ^ (q >> 13)) * 1274126177u;
            return ((q ^ (q >> 16)) & 0xffff) / 32767.5f - 1f;
        }

        /// <summary>Periodic value-noise fBm on the unit square (period 1 in s and t), roughly in [-1, 1].</summary>
        static float Fbm(float s, float t, int freq, int seed)
        {
            float sum = 0f, amp = 0.5f;
            for (int o = 0; o < 4; o++)
            {
                sum += amp * Value(s * freq, t * freq, freq, seed + o * 31);
                freq *= 2;
                amp *= 0.5f;
            }
            return sum * 1.07f;
        }

        static float Value(float x, float y, int period, int seed)
        {
            int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float L(int i, int j) => Lattice(((i % period) + period) % period, ((j % period) + period) % period, seed);
            float a = L(x0, y0) + (L(x0 + 1, y0) - L(x0, y0)) * fx;
            float b = L(x0, y0 + 1) + (L(x0 + 1, y0 + 1) - L(x0, y0 + 1)) * fx;
            return a + (b - a) * fy;
        }

        static float Lattice(int i, int j, int seed)
        {
            uint q = (uint)(i * 73856093 ^ j * 19349663 ^ seed * 83492791);
            q = (q ^ (q >> 13)) * 1274126177u;
            return ((q ^ (q >> 16)) & 0xffff) / 32767.5f - 1f;
        }

        static byte[] Pack(float[] lum, int n, float[] rgb)
        {
            var b = new byte[n * n * 4];
            for (int i = 0; i < n * n; i++)
            {
                for (int c = 0; c < 3; c++) b[i * 4 + c] = ToByte(lum[i] * rgb[c] * 0.92f);
                b[i * 4 + 3] = 255;
            }
            return b;
        }

        static byte[] NormalFromHeight(float[] h, int n, float strength)
        {
            var b = new byte[n * n * 4];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = h[y * n + (x + 1) % n] - h[y * n + (x + n - 1) % n];
                    float dy = h[((y + 1) % n) * n + x] - h[((y + n - 1) % n) * n + x];
                    float nx = -dx * strength, ny = -dy * strength, nz = 1f;
                    float l = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
                    int i = (y * n + x) * 4;
                    b[i] = ToByte(nx / l * 0.5f + 0.5f);
                    b[i + 1] = ToByte(ny / l * 0.5f + 0.5f);
                    b[i + 2] = ToByte(nz / l * 0.5f + 0.5f);
                    b[i + 3] = 255;
                }
            return b;
        }

        static byte ToByte(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255f), 0, 255);
    }
}
