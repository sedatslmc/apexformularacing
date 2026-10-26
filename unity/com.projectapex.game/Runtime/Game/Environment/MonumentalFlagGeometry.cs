using System.Collections.Generic;
using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// Meshes of the monumental flag installation (see <see cref="MonumentalFlagBuilder"/>), local frame
    /// with the origin at the pole axis on the terrace's foundation level (below the lowest ground):
    /// stone terrace with coping and paved top at `terrace`, stepped octagonal foundation up to `deck`,
    /// collar, tapered steel pole of <see cref="Apex.Environment.MonumentalFlagSite.PoleHeight"/> and a
    /// gilded ball finial. Three detail levels.
    /// </summary>
    public static class MonumentalFlagGeometry
    {
        public const float BaseRadius = 0.85f, TopRadius = 0.36f, TerraceRadius = 15f;

        public delegate MeshBuilder Parts(string material);

        /// <summary>
        /// Levels on a slope (height(x, z) of the terrain): the foundation base below the lowest ground
        /// under the terrace, the paved terrace just above the highest, the foundation top 1.05 m above it.
        /// </summary>
        public static void Levels(System.Func<float, float, float> height, float x, float z, out float baseY, out float terrace, out float deck)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI / 8f;
                foreach (float r in new[] { 0f, 4f, 8f, TerraceRadius - 1f, TerraceRadius + 0.2f })
                {
                    float h = height(x + Mathf.Cos(a) * r, z + Mathf.Sin(a) * r);
                    lo = Mathf.Min(lo, h);
                    hi = Mathf.Max(hi, h);
                }
            }
            baseY = lo - 0.4f;
            terrace = hi + 0.1f - baseY;
            deck = terrace + 1.05f;
        }

        public static float CollarTop(float deck) => deck + 1.6f;
        public static float PoleTop(float deck) => deck + Apex.Environment.MonumentalFlagSite.PoleHeight;

        public static void Build(Parts B, int lod, float terrace, float deck)
        {
            float collarTop = CollarTop(deck), poleTop = PoleTop(deck);
            int round = lod == 0 ? 32 : lod == 1 ? 16 : 8;
            // Terrace: retaining wall with a coping, paved top.
            LandmarkGeometry.VerticalLathe(B(LandmarkGeometry.Stone), Vector3.zero, new[]
            {
                new Vector2(0f, TerraceRadius), new Vector2(terrace - 0.12f, TerraceRadius), new Vector2(terrace - 0.12f, TerraceRadius + 0.12f), new Vector2(terrace, TerraceRadius + 0.12f), new Vector2(terrace, TerraceRadius - 0.3f),
            }, round);
            LandmarkGeometry.VerticalLathe(B(EnvironmentGeometry.Pavement), Vector3.zero, new[] { new Vector2(terrace, TerraceRadius - 0.3f), new Vector2(terrace, 6.25f) }, round);
            // Stepped octagonal foundation, collar.
            LandmarkGeometry.VerticalLathe(B(LandmarkGeometry.Stone), Vector3.zero, new[]
            {
                new Vector2(terrace - 0.2f, 6.2f), new Vector2(terrace + 0.35f, 6.2f), new Vector2(terrace + 0.35f, 5.4f), new Vector2(terrace + 0.7f, 5.4f),
                new Vector2(terrace + 0.7f, 4.6f), new Vector2(deck, 4.6f), new Vector2(deck, 1.6f), new Vector2(deck + 0.2f, 1.4f),
                new Vector2(collarTop - 0.3f, 1.25f), new Vector2(collarTop, 1.0f), new Vector2(collarTop, 0.01f),
            }, 8);
            // Tapered pole and the gilded ball finial.
            var pole = new List<Vector2> { new Vector2(collarTop - 0.05f, BaseRadius), new Vector2(poleTop, TopRadius), new Vector2(poleTop, 0.01f) };
            LandmarkGeometry.VerticalLathe(B(EnvironmentGeometry.FlagPole), Vector3.zero, pole, round);
            if (lod < 2)
            {
                var ball = new List<Vector2>();
                int seg = lod == 0 ? 10 : 6;
                float rb = 0.75f, cy = poleTop + 0.25f + rb;
                for (int i = 0; i <= seg; i++)
                {
                    float t = -Mathf.PI * 0.5f + Mathf.PI * i / seg;
                    ball.Add(new Vector2(cy + Mathf.Sin(t) * rb, Mathf.Max(0.01f, Mathf.Cos(t) * rb)));
                }
                LandmarkGeometry.VerticalLathe(B(EventGeometry.Gold), Vector3.zero, new[] { new Vector2(poleTop - 0.05f, 0.3f), new Vector2(poleTop + 0.3f, 0.3f), new Vector2(poleTop + 0.3f, 0.01f) }, round / 2);
                LandmarkGeometry.VerticalLathe(B(EventGeometry.Gold), Vector3.zero, ball, round / 2);
            }
        }
    }
}
