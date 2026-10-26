using System;
using System.Collections.Generic;
using System.Numerics;

namespace Apex.Environment
{
    /// <summary>
    /// Night lighting of the circuit by its street lamps: a pool of real spot lights (no shadows) follows the
    /// camera and takes the lamps nearest ahead of it; lamps behind the camera count as farther away. Slots
    /// keep their lamp while it stays wanted and fade in and out, so lights never pop.
    /// Light values are art values for the night exposure of <see cref="SkyModel"/> (a moon of 0.16, ambient
    /// ~0.1): a lamp 9.2 m above the road gives ~0.53 under itself (intensity / h²); with lamps every 34 m
    /// (StreetFurnitureLayout) and the wide cone the pools overlap. They are not photometric lux values.
    /// </summary>
    public sealed class StreetLighting
    {
        /// <summary>Unity light intensity (linear: illuminance = intensity / d²) and range of one lamp.</summary>
        public const float Intensity = 45f, Range = 32f;
        /// <summary>Cone of a road luminaire (wide, batwing-like): full output within 90°, falling off to 150°.</summary>
        public const float InnerAngle = 90f, OuterAngle = 150f;
        /// <summary>Warm-white LED (about 3000 K), sRGB.</summary>
        public static readonly float[] Color = { 1f, 0.82f, 0.62f };
        /// <summary>Lamps behind the camera are treated as this many times farther away.</summary>
        public const float BehindPenalty = 2f;
        /// <summary>Fade speed of a slot (full intensity per second).</summary>
        public const float FadeRate = 4f;

        public struct Slot
        {
            /// <summary>Lamp index, -1 when free.</summary>
            public int lamp;
            /// <summary>0..1 fade level.</summary>
            public float level;
        }

        public readonly Slot[] slots;
        readonly List<int> wanted = new List<int>();
        readonly List<(float score, int index)> scored = new List<(float, int)>();

        public StreetLighting(int poolSize)
        {
            slots = new Slot[Math.Max(0, poolSize)];
            for (int i = 0; i < slots.Length; i++) slots[i].lamp = -1;
        }

        /// <summary>The `count` lamps that matter most from the camera (nearest, ahead preferred).</summary>
        public static void Pick(IReadOnlyList<Vector3> lamps, Vector3 camera, Vector3 forward, int count, List<int> result, List<(float, int)> scratch = null)
        {
            result.Clear();
            var s = scratch ?? new List<(float, int)>();
            s.Clear();
            var f = new Vector2(forward.X, forward.Z);
            if (f.LengthSquared() > 1e-6f) f = Vector2.Normalize(f);
            for (int i = 0; i < lamps.Count; i++)
            {
                var d = new Vector2(lamps[i].X - camera.X, lamps[i].Z - camera.Z);
                float dist = d.Length();
                // Lamps in the field behind the camera matter less (they light only what the mirrors show).
                float ahead = dist > 1e-3f ? Vector2.Dot(d / dist, f) : 1f;
                float score = dist * (ahead < -0.2f ? BehindPenalty : 1f);
                s.Add((score, i));
            }
            s.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            for (int k = 0; k < Math.Min(count, s.Count); k++) result.Add(s[k].Item2);
        }

        /// <summary>Advances the pool: wanted lamps keep or take a slot and fade in, the rest fade out and free their slot.</summary>
        public void Update(IReadOnlyList<Vector3> lamps, Vector3 camera, Vector3 forward, float dt)
        {
            Pick(lamps, camera, forward, slots.Length, wanted, scored);
            float step = FadeRate * Math.Max(0f, dt);
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].lamp < 0) continue;
                bool keep = wanted.Remove(slots[i].lamp);
                slots[i].level = Math.Clamp(slots[i].level + (keep ? step : -step), 0f, 1f);
                if (!keep && slots[i].level <= 0f) slots[i].lamp = -1;
            }
            // Lamps still wanted take free slots (nearest first).
            int w = 0;
            for (int i = 0; i < slots.Length && w < wanted.Count; i++)
            {
                if (slots[i].lamp >= 0) continue;
                slots[i].lamp = wanted[w++];
                slots[i].level = Math.Min(step, 1f);
            }
        }
    }
}
