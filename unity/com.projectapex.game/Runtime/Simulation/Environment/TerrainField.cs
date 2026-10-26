using System;
using System.Collections.Generic;
using System.Numerics;
using Apex.Core;
using Apex.Tracks;

namespace Apex.Environment
{
    /// <summary>
    /// Ground height around a circuit, shared by the terrain mesh and everything placed
    /// on it (buildings, trees, props). Near the track the ground follows the road's
    /// elevation (inverse-distance weighted) and stays below it; with geo land masses
    /// it blends into the far landscape of <see cref="LandModel"/> towards the edge of
    /// the near area, whose bounds are snapped to <see cref="FarCell"/> so the far grid
    /// meets it on shared grid lines.
    /// </summary>
    public sealed class TerrainField
    {
        public const float NearCell = 12f;
        public const float FarCell = 96f;
        public const float Margin = 400f;
        const float Blend = 260f;

        readonly List<Vector3> samples = new List<Vector3>();
        readonly CircuitDefinition def;
        public readonly LandModel land;
        readonly float baseLevel;
        readonly int binsX, binsZ;
        readonly List<int>[] bins;
        const float BinSize = 50f;

        public float MinX { get; }
        public float MaxX { get; }
        public float MinZ { get; }
        public float MaxZ { get; }

        public TerrainField(TrackGeometry g, CircuitDefinition def)
        {
            this.def = def;
            land = new LandModel(def);
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue, minY = float.MaxValue;
            foreach (var p in g.Positions)
            {
                minX = MathF.Min(minX, p.X); maxX = MathF.Max(maxX, p.X);
                minZ = MathF.Min(minZ, p.Z); maxZ = MathF.Max(maxZ, p.Z);
                minY = MathF.Min(minY, p.Y);
            }
            // Snap to the far grid so the far terrain can tile around the near area.
            MinX = MathF.Floor((minX - Margin) / FarCell) * FarCell;
            MinZ = MathF.Floor((minZ - Margin) / FarCell) * FarCell;
            MaxX = MathF.Ceiling((maxX + Margin) / FarCell) * FarCell;
            MaxZ = MathF.Ceiling((maxZ + Margin) / FarCell) * FarCell;
            baseLevel = minY - 0.8f;
            int step = Math.Max(1, (int)MathF.Round(10f / g.Spacing));
            for (int i = 0; i < g.Count; i += step) samples.Add(g.Positions[i]);
            binsX = (int)MathF.Ceiling((MaxX - MinX) / BinSize) + 1;
            binsZ = (int)MathF.Ceiling((MaxZ - MinZ) / BinSize) + 1;
            bins = new List<int>[binsX * binsZ];
            for (int i = 0; i < samples.Count; i++)
            {
                int bx = (int)((samples[i].X - MinX) / BinSize), bz = (int)((samples[i].Z - MinZ) / BinSize);
                int k = bz * binsX + bx;
                (bins[k] ??= new List<int>()).Add(i);
            }
        }

        public bool InNearArea(float x, float z) => x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;

        /// <summary>Nearest centreline sample (flat distance) within maxRange, else MaxValue.</summary>
        public float TrackDistance(float x, float z, float maxRange, out float trackY)
        {
            trackY = 0f;
            float best = maxRange * maxRange;
            bool found = false;
            int r = (int)MathF.Ceiling(maxRange / BinSize);
            int cx = (int)MathF.Floor((x - MinX) / BinSize), cz = (int)MathF.Floor((z - MinZ) / BinSize);
            for (int bz = cz - r; bz <= cz + r; bz++)
            {
                if (bz < 0 || bz >= binsZ) continue;
                for (int bx = cx - r; bx <= cx + r; bx++)
                {
                    if (bx < 0 || bx >= binsX) continue;
                    var list = bins[bz * binsX + bx];
                    if (list == null) continue;
                    foreach (int i in list)
                    {
                        float dx = samples[i].X - x, dz = samples[i].Z - z;
                        float d2 = dx * dx + dz * dz;
                        if (d2 < best) { best = d2; trackY = samples[i].Y; found = true; }
                    }
                }
            }
            return found ? MathF.Sqrt(best) : float.MaxValue;
        }

        /// <summary>Near-area height (the original circuit terrain rule).</summary>
        float NearHeight(float x, float z)
        {
            float wsum = 0f, hsum = 0f, nearest = float.MaxValue, nearestY = 0f;
            foreach (var s in samples)
            {
                float dx = s.X - x, dz = s.Z - z;
                float d2 = dx * dx + dz * dz;
                float w = 1f / (d2 + 400f);
                wsum += w;
                hsum += w * s.Y;
                if (d2 < nearest) { nearest = d2; nearestY = s.Y; }
            }
            float h = hsum / wsum - 1.2f;
            if (land.HasHeightGrid)
            {
                // Real terrain, with a verge that follows the road within 25 m and blends out by 60 m.
                float dem = land.Height(x, z);
                float dist = MathF.Sqrt(nearest);
                float t = ApexMath.Clamp01((dist - 25f) / 35f);
                h = ApexMath.Lerp(nearestY - 1.0f, dem - 0.3f, t);
                if (dist < 40f) h = MathF.Min(h, nearestY - 1.0f);
                return h;
            }
            // Never poke through the road corridor.
            if (nearest < 40f * 40f) h = MathF.Min(h, nearestY - 1.0f);
            return MathF.Max(h, baseLevel);
        }

        public bool IsWater(float x, float z)
        {
            if (land.HasLand) return !land.IsLand(x, z);
            var shore = def.shoreline;
            return shore != null && shore.Count > 1 && x > ShoreX(shore, z);
        }

        /// <summary>Ground height at (x, z) in game metres.</summary>
        public float Height(float x, float z)
        {
            if (!InNearArea(x, z)) return land.HasLand ? land.Height(x, z) : baseLevel;
            float h = NearHeight(x, z);
            if (land.HasLand)
            {
                float edge = MathF.Min(MathF.Min(x - MinX, MaxX - x), MathF.Min(z - MinZ, MaxZ - z));
                float t = ApexMath.SmoothStep01(1f - ApexMath.Clamp01(edge / Blend));
                if (t > 0f) h = ApexMath.Lerp(h, land.Height(x, z), t);
            }
            if (IsWater(x, z)) h = MathF.Min(h, -4f);
            return h;
        }

        public static float ShoreX(List<(float east, float north)> shore, float z)
        {
            if (z <= shore[0].north) return shore[0].east;
            for (int i = 0; i < shore.Count - 1; i++)
            {
                if (z <= shore[i + 1].north)
                {
                    float t = (z - shore[i].north) / MathF.Max(shore[i + 1].north - shore[i].north, 1e-3f);
                    return ApexMath.Lerp(shore[i].east, shore[i + 1].east, t);
                }
            }
            return shore[shore.Count - 1].east;
        }
    }
}
