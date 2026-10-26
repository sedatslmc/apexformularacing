using System;

namespace Apex.Environment
{
    /// <summary>
    /// Drop-in convention for authored landmark models (replacing the procedural placeholders):
    /// a prefab at Resources/Landmarks/&lt;id&gt; with the landmark's id from the circuit file, in
    /// metres, origin at the base centre, +y up, +z along the building's main axis (same frame as
    /// LandmarkGeometry). The bridge (<see cref="BridgeId"/>) has its origin at water level midway
    /// between the two anchorages, +z from the European to the Asian end. See docs/VISUAL_OVERHAUL.md.
    /// </summary>
    public static class LandmarkAssets
    {
        public const string ResourceFolder = "Landmarks";
        public const string BridgeId = "bridge_15temmuz";
        public const string DolmabahceClockTowerId = "dolmabahce_clock_tower";
        /// <summary>Published tower height of the 15 July Martyrs Bridge (m above water).</summary>
        public const float BridgeTowerHeight = 165f;
        /// <summary>Allowed deviation of an authored model's height from the reference.</summary>
        public const float HeightTolerance = 0.25f;

        /// <summary>Null if the authored model's height fits the reference, else a message for the log.</summary>
        public static string CheckHeight(string id, float referenceHeight, float modelHeight)
        {
            if (referenceHeight <= 0f) return null;
            float ratio = modelHeight / referenceHeight;
            if (MathF.Abs(ratio - 1f) <= HeightTolerance) return null;
            return $"Landmark '{id}': model height {modelHeight:F1} m vs. reference {referenceHeight:F1} m (x{ratio:F2}); wrong scale or units?";
        }
    }
}
