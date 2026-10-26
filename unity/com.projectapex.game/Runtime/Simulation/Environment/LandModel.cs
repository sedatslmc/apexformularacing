using System;
using System.Collections.Generic;
using Apex.Core;
using Apex.Tracks;

namespace Apex.Environment
{
    /// <summary>
    /// Land and water from the circuit's geo block: a point is land if it lies inside
    /// one of the land-mass outlines, water otherwise. Hills rise from the coast as
    /// rise * (1 - exp(-d / scale)) plus gentle value noise, so the Asian shore climbs
    /// towards its ridge and the historic peninsula forms low hills. Outlines are
    /// approximations (see docs/BOSPHORUS_ENVIRONMENT_PLAN.md), not survey data.
    /// </summary>
    public sealed class LandModel
    {
        public const float SeabedHeight = -8f;

        sealed class Mass
        {
            public string id;
            public float rise, scale;
            public float[] x, z;
            public float minX, maxX, minZ, maxZ;
        }

        readonly List<Mass> masses = new List<Mass>();

        public bool HasLand => masses.Count > 0;

        public LandModel(CircuitDefinition def)
        {
            foreach (var m in def.landMasses)
            {
                var mass = new Mass { id = m.id, rise = m.rise, scale = Math.Max(m.scale, 1f), x = new float[m.polygon.Count], z = new float[m.polygon.Count] };
                mass.minX = mass.minZ = float.MaxValue;
                mass.maxX = mass.maxZ = float.MinValue;
                for (int i = 0; i < m.polygon.Count; i++)
                {
                    mass.x[i] = m.polygon[i].east;
                    mass.z[i] = m.polygon[i].north;
                    mass.minX = Math.Min(mass.minX, mass.x[i]); mass.maxX = Math.Max(mass.maxX, mass.x[i]);
                    mass.minZ = Math.Min(mass.minZ, mass.z[i]); mass.maxZ = Math.Max(mass.maxZ, mass.z[i]);
                }
                masses.Add(mass);
            }
        }

        static bool Inside(Mass m, float x, float z)
        {
            if (x < m.minX || x > m.maxX || z < m.minZ || z > m.maxZ) return false;
            bool inside = false;
            int n = m.x.Length;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                if ((m.z[i] > z) != (m.z[j] > z) &&
                    x < (m.x[j] - m.x[i]) * (z - m.z[i]) / (m.z[j] - m.z[i]) + m.x[i])
                    inside = !inside;
            }
            return inside;
        }

        static float EdgeDistance(Mass m, float x, float z)
        {
            float best = float.MaxValue;
            int n = m.x.Length;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float ax = m.x[j], az = m.z[j], bx = m.x[i], bz = m.z[i];
                float dx = bx - ax, dz = bz - az;
                float len2 = dx * dx + dz * dz;
                float t = len2 > 1e-6f ? ApexMath.Clamp(((x - ax) * dx + (z - az) * dz) / len2, 0f, 1f) : 0f;
                float px = ax + dx * t - x, pz = az + dz * t - z;
                best = Math.Min(best, px * px + pz * pz);
            }
            return MathF.Sqrt(best);
        }

        Mass Find(float x, float z)
        {
            foreach (var m in masses)
                if (Inside(m, x, z)) return m;
            return null;
        }

        public bool IsLand(float x, float z) => Find(x, z) != null;

        /// <summary>Id of the land mass containing the point, or null over water.</summary>
        public string MassAt(float x, float z) => Find(x, z)?.id;

        /// <summary>Distance to the coast of the containing mass (0 over water).</summary>
        public float CoastDistance(float x, float z)
        {
            var m = Find(x, z);
            return m == null ? 0f : EdgeDistance(m, x, z);
        }

        /// <summary>Distance from a water point to the nearest coast (0 on land).</summary>
        public float WaterDistanceToCoast(float x, float z)
        {
            if (IsLand(x, z)) return 0f;
            float best = float.MaxValue;
            foreach (var m in masses) best = Math.Min(best, EdgeDistance(m, x, z));
            return best;
        }

        /// <summary>Ground height of the far landscape (m); seabed over water.</summary>
        public float Height(float x, float z)
        {
            var m = Find(x, z);
            if (m == null) return SeabedHeight;
            float d = EdgeDistance(m, x, z);
            // Quay edge at +1.5 m, then the hill profile with soft noise (±8 % of rise).
            float h = 1.5f + m.rise * (1f - MathF.Exp(-d / m.scale));
            float noise = ValueNoise(x / 420f, z / 420f) * 0.6f + ValueNoise(x / 150f + 17f, z / 150f - 5f) * 0.4f;
            return h + m.rise * 0.08f * noise * Math.Min(1f, d / 120f);
        }

        /// <summary>Smooth deterministic noise in [-1, 1].</summary>
        public static float ValueNoise(float x, float z)
        {
            int xi = (int)MathF.Floor(x), zi = (int)MathF.Floor(z);
            float fx = x - xi, fz = z - zi;
            fx = fx * fx * (3f - 2f * fx);
            fz = fz * fz * (3f - 2f * fz);
            float a = Hash(xi, zi), b = Hash(xi + 1, zi), c = Hash(xi, zi + 1), d = Hash(xi + 1, zi + 1);
            return (a + (b - a) * fx) + ((c + (d - c) * fx) - (a + (b - a) * fx)) * fz;
        }

        /// <summary>Integer hash in [-1, 1].</summary>
        public static float Hash(int x, int z)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + z * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF * 2f - 1f;
            }
        }
    }
}
