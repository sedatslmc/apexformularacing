using System;
using System.Collections.Generic;
using System.Numerics;
using Apex.Core;
using Apex.Tracks;

namespace Apex.Environment
{
    /// <summary>
    /// Where the event structures of a street circuit go: pit building along the pit lane, timing
    /// tower before it (towards the pit entry), race control after it, podium on the pit roof at
    /// the start line, and event flags at the pit roof and at named squares on the route. Every
    /// footprint is checked against water, the coast and other parts of the track; a structure
    /// that does not fit is marked as not placed. Pure data, shared by the Unity builder and the
    /// offline preview.
    /// </summary>
    public sealed class EventLayout
    {
        /// <summary>Pose on the ground: position, and yaw in degrees (Unity convention, 0 = +z/north).</summary>
        public struct Placement
        {
            public bool placed;
            public Vector3 position;
            public float yaw;
        }

        // Dimensions of the structures (must match the mesh generators).
        public const float PitDepth = 11f, PitRoof = 8.0f;
        public const float TowerHalf = 2f;
        public const float RaceControlDepth = 12f, RaceControlLength = 16f;
        /// <summary>Clearance from other parts of the circuit (centreline) for any footprint corner.</summary>
        public const float OtherTrackClearance = 22f;

        public Placement pitBuilding, timingTower, raceControl;
        public float pitLength;
        /// <summary>Bay centres along the pit building's local +z.</summary>
        public readonly List<float> bayZ = new List<float>();
        /// <summary>Podium centre along the pit building's local +z.</summary>
        public float podiumZ;
        public readonly List<(Vector3 pos, float length, float pole)> flags = new List<(Vector3, float, float)>();

        readonly TrackGeometry g;
        readonly TerrainField field;

        public static float Yaw(Vector3 dir) => MathF.Atan2(dir.X, dir.Z) * 180f / MathF.PI;

        /// <summary>Rotates a local offset (x right, z forward) by a yaw in degrees.</summary>
        public static Vector3 Rotate(float yaw, Vector3 local)
        {
            float r = yaw * MathF.PI / 180f, c = MathF.Cos(r), s = MathF.Sin(r);
            return new Vector3(local.X * c + local.Z * s, local.Y, -local.X * s + local.Z * c);
        }

        public EventLayout(TrackGeometry geometry, CircuitDefinition def, TerrainField terrain, float[] wallLeft, float[] wallRight, float pitOuter)
        {
            g = geometry;
            field = terrain;
            var pit = def.pit;
            if (pit != null)
            {
                int side = pit.side;
                float start = pit.BoxDistance(0) - 15f, end = pit.BoxDistance(19) + 15f;
                float mid = (start + end) * 0.5f;
                pitLength = end - start;
                float s = side > 0 ? 1f : -1f;
                // Pit building: track-side face 1.5 m behind the outer pit wall (the straight curves slightly, so the
                // building's ends come a little closer to the lane than its middle); local +x away from the track.
                g.FrameAt(g.Wrap(mid), out var p, out var f, out _, side * (g.WidthAt(mid) * 0.5f + pitOuter + 1.5f));
                float yaw = Yaw(f) + (side > 0 ? 0f : 180f);
                pitBuilding = new Placement { position = p, yaw = yaw };
                pitBuilding.placed = Clear(pitBuilding, PitDepth * 0.5f, 0f, PitDepth * 0.5f, pitLength * 0.5f, mid);
                for (int b = 0; b < 20; b++) bayZ.Add(s * (pit.BoxDistance(b) - mid));
                podiumZ = Math.Max(-pitLength * 0.5f + 8f, Math.Min(pitLength * 0.5f - 8f, s * (def.startLineDistance - mid)));
                if (pitBuilding.placed)
                    foreach (float e in new[] { -1f, 1f })
                        flags.Add((p + Rotate(yaw, new Vector3(1.2f, PitRoof, e * (pitLength * 0.5f - 3f))), 2.4f, 6f));

                // Timing tower before the building, turned towards the approaching cars and the track.
                float dT = g.Wrap(start - 14f);
                g.FrameAt(dT, out var tp, out var tf, out var tr, side * (g.WidthAt(dT) * 0.5f + pitOuter + 3f));
                var look = Vector3.Normalize(-tf * 0.8f - tr * side * 0.6f);
                timingTower = new Placement { position = tp, yaw = Yaw(look) };
                timingTower.placed = Clear(timingTower, 0f, 0f, TowerHalf, TowerHalf, dT);

                // Race control after the building (local frame like the pit building).
                float dR = g.Wrap(end + 16f);
                g.FrameAt(dR, out var rp, out var rf, out _, side * (g.WidthAt(dR) * 0.5f + pitOuter + 1f));
                raceControl = new Placement { position = rp, yaw = Yaw(rf) + (side > 0 ? 0f : 180f) };
                raceControl.placed = Clear(raceControl, RaceControlDepth * 0.5f, 0f, RaceControlDepth * 0.5f, RaceControlLength * 0.5f, dR);
            }
            // Event flags at the two real squares on the route (next to the landmark that names them).
            foreach (var id in new[] { "besiktas_pier", "ortakoy_mosque" })
            {
                var lm = def.FindLandmark(id);
                if (lm == null || wallLeft == null || wallRight == null) continue;
                var pr = g.Project(new Vector3(lm.east, 0f, lm.north));
                int toward = pr.lateral >= 0f ? 1 : -1;
                for (int k = -1; k <= 1; k++)
                {
                    float d = g.Wrap(g.Distances[pr.index] + k * 7f);
                    int i = g.IndexAt(d);
                    float wall = toward < 0 ? wallLeft[i] : wallRight[i];
                    g.FrameAt(d, out var fp, out _, out _, toward * (wall + 3f));
                    fp.Y -= 0.12f;
                    if (field != null && (field.IsWater(fp.X, fp.Z) || field.land.CoastDistance(fp.X, fp.Z) < 1.5f)) continue;
                    if (OtherTrackDistance(fp, d) < OtherTrackClearance) continue;
                    flags.Add((fp, 3.2f, 8f));
                }
            }
        }

        /// <summary>
        /// Footprint check for a box around the placement: local centre (cx, cz), half sizes (hx, hz).
        /// Corners and edge midpoints must be on land, off the quay edge and clear of other track parts.
        /// </summary>
        bool Clear(Placement pl, float cx, float cz, float hx, float hz, float anchor)
        {
            for (int a = -1; a <= 1; a++)
                for (int b = -1; b <= 1; b++)
                {
                    var p = pl.position + Rotate(pl.yaw, new Vector3(cx + a * hx, 0f, cz + b * hz));
                    if (field != null && (field.IsWater(p.X, p.Z) || field.land.CoastDistance(p.X, p.Z) < 2f)) return false;
                    if (OtherTrackDistance(p, anchor) < OtherTrackClearance) return false;
                }
            return true;
        }

        /// <summary>Flat distance to the nearest centreline sample more than 200 m (along the track) from `anchor`.</summary>
        public float OtherTrackDistance(Vector3 p, float anchor)
        {
            float best = float.MaxValue;
            for (int i = 0; i < g.Count; i++)
            {
                float along = MathF.Abs(ApexMath.PosMod(g.Distances[i] - anchor + g.Length * 0.5f, g.Length) - g.Length * 0.5f);
                if (along < 200f) continue;
                best = MathF.Min(best, TrackGeometry.FlatDist2(g.Positions[i], p));
            }
            return MathF.Sqrt(best);
        }
    }
}
