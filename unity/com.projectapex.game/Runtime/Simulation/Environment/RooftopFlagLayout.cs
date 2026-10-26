using System;
using System.Collections.Generic;
using System.Numerics;
using Apex.Tracks;

namespace Apex.Environment
{
    /// <summary>
    /// Turkish flags on selected rooftops near the circuit: flat roofs and towers of the near band within
    /// 160 m of the lap, preferring focal points (start/finish, the Ortaköy mosque and T1, the Yıldız
    /// slope with the monumental flag) and keeping at least <see cref="MinSpacing"/> between flags, so they
    /// read as a deliberate dressing of the event and the city, not as a pattern. Deterministic.
    /// </summary>
    public sealed class RooftopFlagLayout
    {
        public struct Flag
        {
            /// <summary>Foot of the pole on the roof, at the front corner of the building.</summary>
            public Vector3 position;
            public float length, pole;
        }

        public const float MinSpacing = 70f, MaxTrackDistance = 160f;
        public const float FlagLength = 2.4f, PoleHeight = 6f;

        public readonly List<Flag> flags = new List<Flag>();

        public RooftopFlagLayout(CityLayout city, TerrainField field, CircuitDefinition def, TrackGeometry g, int quality)
        {
            int max = quality == 0 ? 14 : quality == 1 ? 32 : 48;
            var focal = new List<Vector2>();
            var start = g.PositionAt(def.startLineDistance);
            focal.Add(new Vector2(start.X, start.Z));
            var mosque = def.FindLandmark("ortakoy_mosque");
            if (mosque != null) focal.Add(new Vector2(mosque.east, mosque.north));
            if (def.monumentalFlag.HasValue) focal.Add(new Vector2(def.monumentalFlag.Value.east, def.monumentalFlag.Value.north));
            var candidates = new List<(float score, CityLayout.Lot lot)>();
            foreach (var lot in city.lots)
            {
                if (lot.band != 0 || lot.roof == 1) continue;   // tiled hip roofs carry no flag poles
                float td = field.TrackDistance(lot.x, lot.z, MaxTrackDistance, out _);
                if (td > MaxTrackDistance) continue;
                float near = float.MaxValue;
                foreach (var f in focal) near = MathF.Min(near, Vector2.Distance(f, new Vector2(lot.x, lot.z)));
                // Close to a focal point and to the track; taller buildings are seen from farther away.
                float score = -MathF.Min(near, 1500f) * 0.05f - td * 0.2f + lot.height * 0.3f;
                candidates.Add((score, lot));
            }
            candidates.Sort((a, b) => b.score.CompareTo(a.score) != 0 ? b.score.CompareTo(a.score) : a.lot.x.CompareTo(b.lot.x));
            foreach (var (_, lot) in candidates)
            {
                if (flags.Count >= max) break;
                // Front corner of the roof (the facade faces the street), clear of the central roof plant.
                var front = new Vector3(MathF.Sin(lot.yaw), 0f, MathF.Cos(lot.yaw));
                var along = new Vector3(front.Z, 0f, -front.X);
                var p = new Vector3(lot.x, lot.ground + lot.height, lot.z) + front * MathF.Max(0f, lot.d * 0.5f - 1.2f) + along * MathF.Max(0f, lot.w * 0.5f - 1.5f);
                bool spaced = true;
                foreach (var f in flags)
                    if (Vector2.Distance(new Vector2(f.position.X, f.position.Z), new Vector2(p.X, p.Z)) < MinSpacing) { spaced = false; break; }
                if (spaced) flags.Add(new Flag { position = p, length = FlagLength, pole = PoleHeight });
            }
        }
    }
}
