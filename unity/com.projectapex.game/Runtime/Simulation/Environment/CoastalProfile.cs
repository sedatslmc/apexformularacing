using System;

namespace Apex.Environment
{
    /// <summary>
    /// Ground height along the Bosphorus shore (docs/BOSPHORUS_COAST_HEIGHTS.md). The European shore from Kabataş to
    /// Ortaköy is a narrow coastal flat at quay level – Dolmabahçe ("filled garden") stands on 17th–18th century
    /// landfill – with the hills rising behind the coastal road. The Copernicus model is a surface model (trees,
    /// walls, embankments) and its 100 m street-level grid smears the hills down to the water: the game had 10–56 m
    /// of ground at the quay and a cliff into the sea. This profile bounds the ground from above near the water:
    /// quay level at the shore, a gentle shelf, then at most a steep hillside; between the shore and a nearby road
    /// the ground ramps evenly from the quay up to the road, so neither the shore nor the road gets a wall.
    /// </summary>
    public static class CoastalProfile
    {
        /// <summary>Top of the quay above the Bosphorus (m); the quay wall below it is built by <see cref="Waterfront"/>.</summary>
        public const float QuayLevel = 1.6f;
        /// <summary>Width of the coastal flat (m) and its mean rise (m/m, ~3°).</summary>
        public const float ShelfWidth = 60f, ShelfGradient = 0.05f;
        /// <summary>Steepest hillside behind the flat (m/m, ~19°), as <see cref="TerrainField.MaxHillGradient"/>.</summary>
        public const float HillGradient = 0.35f;
        /// <summary>Beyond this distance from the water the profile no longer applies (m).</summary>
        public const float Reach = 500f;
        /// <summary>Level quay promenade behind the wall (m), as <see cref="Waterfront.PromenadeWidth"/>.</summary>
        public const float PromenadeWidth = 8f;
        /// <summary>Street band beside a road centreline (m), as <see cref="TerrainField.PlateauWidth"/>.</summary>
        public const float RoadBand = 30f;

        /// <summary>Highest plausible ground at a distance from the water (no road involved).</summary>
        public static float Bound(float coastDistance)
        {
            float d = Math.Max(0f, coastDistance);
            return QuayLevel + ShelfGradient * Math.Min(d, ShelfWidth) + HillGradient * Math.Max(0f, d - ShelfWidth);
        }

        /// <summary>
        /// Even ramp from the quay to a road's street band (road height − 1 m, like the near terrain), for a point
        /// <paramref name="coastDistance"/> from the water and <paramref name="roadDistance"/> from the road centreline.
        /// </summary>
        public static float Ramp(float coastDistance, float roadDistance, float roadHeight)
        {
            float street = roadHeight - 1f;
            if (street <= QuayLevel) return QuayLevel;
            float toBand = Math.Max(0f, roadDistance - RoadBand);
            // The quay promenade stays level; the ramp starts behind it.
            float d = Math.Max(0f, coastDistance - PromenadeWidth);
            float t = d + toBand <= 1e-3f ? 1f : d / (d + toBand);
            return QuayLevel + (street - QuayLevel) * t;
        }

        /// <summary>A road at most this far from the water counts as the coastal road (m).</summary>
        public const float CoastalRoadReach = 260f;

        /// <summary>
        /// The ground near the water: the given height, but no higher than the shore allows. Seaward of a coastal
        /// road (<paramref name="roadCoastDistance"/> ≤ <see cref="CoastalRoadReach"/> and the point nearer the water
        /// than the road) the land is the coastal flat – palace grounds, squares, quays – and only ramps from the quay
        /// up to the road; elsewhere the shelf-and-hill bound applies.
        /// </summary>
        public static float Limit(float height, float coastDistance, float roadDistance, float roadHeight, float roadCoastDistance = float.MaxValue)
        {
            if (coastDistance >= Reach) return height;
            float cap = Bound(coastDistance);
            if (roadDistance < float.MaxValue)
            {
                float ramp = Ramp(coastDistance, roadDistance, roadHeight);
                bool seaward = roadCoastDistance <= CoastalRoadReach && coastDistance < roadCoastDistance;
                cap = seaward ? ramp : Math.Max(cap, ramp);
            }
            return Math.Min(height, cap);
        }
    }
}
