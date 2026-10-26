using System.Collections.Generic;
using Apex.Tracks;
using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// Mesh generation for the street-circuit infrastructure (no GameObjects, also used by the
    /// offline preview): concrete barriers with block joints, guardrail on escape roads,
    /// energy-absorbing modules and tyre stacks, catch fencing with overhang and gates,
    /// marshal shelters, flag panel housings and road-surface details. Builders are keyed by
    /// material. Procedural placeholder art with believable dimensions.
    /// </summary>
    public static class TrackInfrastructureGeometry
    {
        public const string Concrete = "BarrierConcrete";
        public const string Armco = "Armco";
        public const string Steel = "Steel";
        public const string ModuleA = "ModuleNavy";
        public const string ModuleB = "ModuleIvory";
        public const string ModuleC = "ModuleTurquoise";
        public const string Tyre = "Tire";
        public const string Belt = "ConveyorBelt";
        public const string Fence = EnvironmentGeometry.CatchFence;
        public const string Shelter = "Concrete";
        public const string Roof = "ShelterRoof";
        public const string Extinguisher = "Extinguisher";
        public const string Housing = "PanelHousing";
        public const string Rubber = "Rubber";
        public const string Patch = "AsphaltPatch";
        public const string Manhole = "Manhole";
        public const string Dash = "LaneDash";

        public const float BarrierHeight = 1.0f;
        public const float FenceHeight = 3.0f;

        static Vector3 V(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);

        public static Vector3 P(TrackGeometry g, int i, float lateral, float dy) =>
            V(g.Positions[i] + g.Rights[i] * lateral) + Vector3.up * dy;

        /// <summary>Flat ribbon on the road between two lateral offsets (decals: rubber, patches, dashes).</summary>
        public static void RoadDecal(MeshBuilder mb, TrackGeometry g, float from, float to, float latA, float latB, float lift, float taper)
        {
            float len = Mathf.Repeat(to - from, g.Length);
            if (len < 0.5f) return;
            float step = Mathf.Max(g.Spacing, Mathf.Min(4f, len / 2f));
            for (float d = 0f; d < len - 1e-3f; d += step)
            {
                float d1 = Mathf.Min(len, d + step);
                float w0 = taper > 0f ? Mathf.Clamp01(Mathf.Min(d, len - d) / taper) : 1f;
                float w1 = taper > 0f ? Mathf.Clamp01(Mathf.Min(d1, len - d1) / taper) : 1f;
                float mid = (latA + latB) * 0.5f, half = (latB - latA) * 0.5f;
                int i = g.IndexAt(g.Wrap(from + d)), j = g.IndexAt(g.Wrap(from + d1));
                var a0 = P(g, i, mid - half * w0, lift); var b0 = P(g, i, mid + half * w0, lift);
                var a1 = P(g, j, mid - half * w1, lift); var b1 = P(g, j, mid + half * w1, lift);
                mb.QuadFacing(a0, b0, b1, a1, new Vector2(0f, d / 6f), new Vector2(1f, d / 6f), new Vector2(1f, d1 / 6f), new Vector2(0f, d1 / 6f), Vector3.up);
            }
        }

        /// <summary>Opacity levels of the rubber texture atlas (one band of u per level).</summary>
        public const int RubberLevels = 16;

        /// <summary>
        /// A rubber strip following the line: centre offset interpolated from start to end, u mapped into
        /// the atlas band of its strength (so stock URP Lit shows per-strip opacity), v along the road.
        /// Brake marks taper at both ends.
        /// </summary>
        public static void RubberStrip(MeshBuilder mb, TrackGeometry g, TrackInfrastructurePlan.RubberStrip s, float lift)
        {
            float len = Mathf.Repeat(s.to - s.from, g.Length);
            if (len < 0.5f) return;
            int level = System.Math.Max(0, System.Math.Min(RubberLevels - 1, (int)System.Math.Round(s.strength * (RubberLevels - 1))));
            // Inset from the band edges keeps bilinear filtering inside the band.
            float u0 = (level + 0.06f) / RubberLevels, u1 = (level + 0.94f) / RubberLevels;
            float step = Mathf.Max(g.Spacing, Mathf.Min(4f, len / 2f));
            float taper = s.brakeMark ? Mathf.Min(3f, len * 0.3f) : 0f;
            for (float d = 0f; d < len - 1e-3f; d += step)
            {
                float d1 = Mathf.Min(len, d + step);
                float w0 = taper > 0f ? Mathf.Clamp01(Mathf.Min(d, len - d) / taper) : 1f;
                float w1 = taper > 0f ? Mathf.Clamp01(Mathf.Min(d1, len - d1) / taper) : 1f;
                float c0 = Mathf.Lerp(s.lateralFrom, s.lateralTo, d / len), c1 = Mathf.Lerp(s.lateralFrom, s.lateralTo, d1 / len);
                float h0 = s.width * 0.5f * Mathf.Max(0.2f, w0), h1 = s.width * 0.5f * Mathf.Max(0.2f, w1);
                int i = g.IndexAt(g.Wrap(s.from + d)), j = g.IndexAt(g.Wrap(s.from + d1));
                var a0 = P(g, i, c0 - h0, lift); var b0 = P(g, i, c0 + h0, lift);
                var a1 = P(g, j, c1 - h1, lift); var b1 = P(g, j, c1 + h1, lift);
                float v0 = (s.from + d) / 6f, v1 = (s.from + d1) / 6f;
                mb.QuadFacing(a0, b0, b1, a1, new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1), Vector3.up);
            }
        }

        /// <summary>Manhole cover (octagon) on the road.</summary>
        public static void ManholeCover(MeshBuilder mb, Vector3 centre, float radius)
        {
            for (int k = 0; k < 8; k++)
            {
                float a0 = k * Mathf.PI / 4f, a1 = (k + 1) * Mathf.PI / 4f;
                var p0 = centre + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                var p1 = centre + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                mb.TriangleFacing(centre, p0, p1, new Vector2(0.5f, 0.5f), new Vector2(0.5f + Mathf.Cos(a0) * 0.5f, 0.5f + Mathf.Sin(a0) * 0.5f),
                    new Vector2(0.5f + Mathf.Cos(a1) * 0.5f, 0.5f + Mathf.Sin(a1) * 0.5f), Vector3.up);
            }
        }
    }
}
