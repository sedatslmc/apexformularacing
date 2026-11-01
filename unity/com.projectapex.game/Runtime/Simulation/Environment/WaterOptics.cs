using System;
using Apex.Core;

namespace Apex.Environment
{
    /// <summary>
    /// Physical inputs of the Bosphorus water shader (Shaders/ApexWater), kept here so they are tested:
    /// <list type="bullet">
    /// <item>Fresnel: Schlick with F0 from the refractive index of sea water (1.34 → 0.021).</item>
    /// <item>Sun glitter: the Cox–Munk (1954) slope variance of a wind-roughened clean sea surface,
    /// σ² = 0.003 + 0.00512·U, used as microfacet roughness (Beckmann m ≈ GGX α = σ).</item>
    /// <item>Wave layers: three scrolling normal-map layers whose drift follows the deep-water
    /// dispersion c = √(gλ / 2π) of their dominant wavelength, spread ±30° around the wind.</item>
    /// <item>Body colour: deep, slightly turbid blue-green strait water; lighter and greener in the
    /// shallow band along quays and shores; foam along the walls.</item>
    /// </list>
    /// </summary>
    public static class WaterOptics
    {
        public const float RefractiveIndex = 1.34f;
        public const float Gravity = 9.81f;
        /// <summary>Normal-map wave numbers peak around 3.5 periods per tile (EnvironmentBuilder.BuildWaterNormal).</summary>
        public const float PeriodsPerTile = 3.5f;

        public static float F0
        {
            get
            {
                float r = (RefractiveIndex - 1f) / (RefractiveIndex + 1f);
                return r * r;
            }
        }

        /// <summary>Schlick's Fresnel reflectance for the cosine between view and normal.</summary>
        public static float Fresnel(float cosTheta)
        {
            float m = 1f - ApexMath.Clamp01(cosTheta);
            float m2 = m * m;
            return F0 + (1f - F0) * m2 * m2 * m;
        }

        /// <summary>Total mean-square slope of the sea surface for a wind speed at 12.5 m (Cox–Munk, clean surface).</summary>
        public static float SlopeVariance(float windSpeed) => 0.003f + 0.00512f * ApexMath.Clamp(windSpeed, 0f, 20f);

        /// <summary>GGX roughness α of the whole surface (resolved waves plus the sub-pixel rest).</summary>
        public static float Roughness(float windSpeed, float rain = 0f)
        {
            float a = MathF.Sqrt(SlopeVariance(windSpeed));
            // Rain rings roughen the surface independently of the wind.
            return ApexMath.Lerp(a, MathF.Max(a, 0.32f), ApexMath.Clamp01(rain) * 0.8f);
        }

        /// <summary>Phase speed of a deep-water gravity wave (m/s).</summary>
        public static float PhaseSpeed(float wavelength) => MathF.Sqrt(Gravity * MathF.Max(wavelength, 0.01f) / (2f * MathF.PI));

        public struct Layer
        {
            /// <summary>Drift direction in the game frame (unit x/z).</summary>
            public float dirX, dirZ;
            /// <summary>World size of one normal-map tile (m) and the drift speed (m/s).</summary>
            public float tile, speed;
            /// <summary>Normal strength (URP normal scale).</summary>
            public float strength;
        }

        static readonly (float tile, float turn, float weight)[] LayerShape = { (64f, 0f, 0.55f), (23f, 28f, 0.4f), (8.5f, -32f, 0.3f) };

        /// <summary>The three normal-map layers for a wind blowing towards (windX, windZ) at windSpeed m/s.</summary>
        public static Layer[] Layers(float windX, float windZ, float windSpeed)
        {
            float len = MathF.Sqrt(windX * windX + windZ * windZ);
            if (len < 1e-4f) { windX = 0f; windZ = 1f; len = 1f; }
            windX /= len; windZ /= len;
            // Calm water still shows small ripples; a fresh breeze (8–10 m/s) gives full-strength chop.
            float sea = ApexMath.Clamp(0.3f + 0.08f * windSpeed, 0.3f, 1.2f);
            var r = new Layer[LayerShape.Length];
            for (int i = 0; i < r.Length; i++)
            {
                var (tile, turn, weight) = LayerShape[i];
                float a = turn * ApexMath.Deg2Rad, c = MathF.Cos(a), s = MathF.Sin(a);
                r[i] = new Layer
                {
                    dirX = windX * c + windZ * s,
                    dirZ = -windX * s + windZ * c,
                    tile = tile,
                    speed = PhaseSpeed(tile / PeriodsPerTile),
                    strength = weight * sea,
                };
            }
            return r;
        }

        /// <summary>Linear body colours (Unity Lit units: lit by ambient + sun without 1/π).</summary>
        public static readonly float[] DeepColor = { 0.006f, 0.03f, 0.04f };
        public static readonly float[] ShallowColor = { 0.02f, 0.075f, 0.07f };
        public const float FoamAlbedo = 0.75f;
        /// <summary>Foam band along quay walls and shores (m) and the shallow-colour band (m).</summary>
        public const float FoamWidth = 4f, ShallowWidth = 45f;
        /// <summary>Street lamps the water reflects at once (nearest to the camera).</summary>
        public const int MaxLamps = 16;
        /// <summary>Warm-white LED street light (linear) and its intensity for the water glitter: an art value
        /// in Unity light units, tuned in the offline preview (tools/carrender/water.html &lamps=1) to distinct light paths.</summary>
        public static readonly float[] LampColor = { 1f, 0.72f, 0.42f };
        public const float LampIntensity = 50f;

        /// <summary>Indices of the `result.Length` smallest values of distance(i) (unordered), returns the count.</summary>
        public static int NearestLamps(int count, Func<int, float> distance, int[] result)
        {
            int k = Math.Min(result.Length, count);
            var best = new float[k];
            for (int j = 0; j < k; j++) { result[j] = j; best[j] = distance(j); }
            for (int i = k; i < count; i++)
            {
                float d = distance(i);
                int worst = 0;
                for (int j = 1; j < k; j++) if (best[j] > best[worst]) worst = j;
                if (k > 0 && d < best[worst]) { best[worst] = d; result[worst] = i; }
            }
            return k;
        }

        /// <summary>Distance range the coast texture encodes (m).</summary>
        public const float CoastMaxDistance = 64f;
    }

    /// <summary>
    /// Distance from every water cell to the nearest land cell on a square grid (exact Euclidean
    /// distance transform, Felzenszwalb &amp; Huttenlocher), built once per circuit for the water
    /// shader's shallow tint and shore foam. Land cells hold 0.
    /// </summary>
    public sealed class CoastDistanceField
    {
        public readonly int Resolution;
        public readonly float MinX, MinZ, Cell;
        public readonly float[] Distance;

        CoastDistanceField(int res, float minX, float minZ, float cell)
        {
            Resolution = res; MinX = minX; MinZ = minZ; Cell = cell;
            Distance = new float[res * res];
        }

        /// <param name="rasterizeLand">fills mask[z * res + x] with true for land cell centres</param>
        /// <param name="inland">false: distance of water cells to the nearest land; true: of land cells to the nearest water</param>
        public static CoastDistanceField Build(float minX, float minZ, float size, int res, Action<bool[], int, float, float, float> rasterizeLand, bool inland = false)
        {
            var f = new CoastDistanceField(res, minX, minZ, size / res);
            var land = new bool[res * res];
            rasterizeLand(land, res, minX, minZ, f.Cell);
            if (inland) for (int i = 0; i < land.Length; i++) land[i] = !land[i];
            const float Inf = 1e20f;
            var g = new float[res * res];
            for (int i = 0; i < g.Length; i++) g[i] = land[i] ? 0f : Inf;
            var line = new float[res];
            var outp = new float[res];
            var v = new int[res];
            var zb = new float[res + 1];
            // Rows, then columns: squared distances in cells.
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++) line[x] = g[z * res + x];
                Edt1D(line, outp, v, zb, res);
                for (int x = 0; x < res; x++) g[z * res + x] = outp[x];
            }
            for (int x = 0; x < res; x++)
            {
                for (int z = 0; z < res; z++) line[z] = g[z * res + x];
                Edt1D(line, outp, v, zb, res);
                for (int z = 0; z < res; z++) f.Distance[z * res + x] = outp[z] >= Inf * 0.5f ? float.MaxValue : MathF.Sqrt(outp[z]) * f.Cell;
            }
            return f;
        }

        static void Edt1D(float[] f, float[] d, int[] v, float[] z, int n)
        {
            int k = 0;
            v[0] = 0;
            z[0] = float.NegativeInfinity;
            z[1] = float.PositiveInfinity;
            for (int q = 1; q < n; q++)
            {
                float s = Intersect(f, q, v[k]);
                // z[0] is −∞, so k never drops below 0.
                while (s <= z[k]) { k--; s = Intersect(f, q, v[k]); }
                k++;
                v[k] = q;
                z[k] = s;
                z[k + 1] = float.PositiveInfinity;
            }
            k = 0;
            for (int q = 0; q < n; q++)
            {
                while (z[k + 1] < q) k++;
                int p = v[k];
                d[q] = (q - p) * (q - p) + f[p];
            }
        }

        static float Intersect(float[] f, int q, int p) => ((f[q] + q * q) - (f[p] + p * p)) / (2f * q - 2f * p);

        /// <summary>Bilinear distance at a world point (clamped to the grid).</summary>
        public float Sample(float x, float z)
        {
            float gx = (x - MinX) / Cell - 0.5f, gz = (z - MinZ) / Cell - 0.5f;
            int x0 = (int)MathF.Floor(gx), z0 = (int)MathF.Floor(gz);
            float tx = gx - x0, tz = gz - z0;
            float At(int ix, int iz)
            {
                ix = Math.Max(0, Math.Min(Resolution - 1, ix));
                iz = Math.Max(0, Math.Min(Resolution - 1, iz));
                return Math.Min(Distance[iz * Resolution + ix], 1e6f);
            }
            float a = ApexMath.Lerp(At(x0, z0), At(x0 + 1, z0), tx);
            float b = ApexMath.Lerp(At(x0, z0 + 1), At(x0 + 1, z0 + 1), tx);
            return ApexMath.Lerp(a, b, tz);
        }

        /// <summary>8-bit encoding for the shader: distance / maxDistance, clamped.</summary>
        public byte[] Encode(float maxDistance)
        {
            var b = new byte[Distance.Length];
            for (int i = 0; i < b.Length; i++) b[i] = (byte)Math.Round(ApexMath.Clamp01(Distance[i] / maxDistance) * 255f);
            return b;
        }
    }
}
