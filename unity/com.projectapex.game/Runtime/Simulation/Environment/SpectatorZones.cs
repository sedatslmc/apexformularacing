using System;
using System.Collections.Generic;
using System.Numerics;
using Apex.Tracks;

namespace Apex.Environment
{
    /// <summary>
    /// Standing-spectator zones away from the Ortaköy section (docs/ISTANBUL_OVERHAUL.md, Phase 5): the heavily
    /// braked corners, where overtaking happens (TrackInfrastructurePlan.viewingSpots). Each zone covers the track
    /// within <see cref="Radius"/> of the apex; the fans there stand in groups (<see cref="TracksideCrowd.Groups"/>).
    /// Mobile (quality 0) has none; the number of zones grows with the quality level.
    /// </summary>
    public static class SpectatorZones
    {
        public const float Radius = 85f, GroupScale = 7f;

        public static int MaxZones(int quality) => quality <= 0 ? 0 : quality == 1 ? 3 : quality == 2 ? 6 : 10;

        public static List<Vector2> Centres(TrackGeometry g, TrackInfrastructurePlan plan, Vector2 ortakoy, float ortakoyRadius, int quality)
        {
            var list = new List<Vector2>();
            int max = MaxZones(quality);
            foreach (var s in plan.viewingSpots)
            {
                if (list.Count >= max) break;
                var p3 = g.PositionAt(s.distance);
                var p = new Vector2(p3.X, p3.Z);
                // Ortaköy has its own crowd; zones do not overlap each other.
                if (Vector2.Distance(p, ortakoy) < ortakoyRadius + Radius) continue;
                bool near = false;
                foreach (var q in list) if (Vector2.Distance(p, q) < 2f * Radius) near = true;
                if (!near) list.Add(p);
            }
            return list;
        }
    }
}
