using System.Collections.Generic;
using Apex.Core;
using Apex.Race;
using Apex.Tracks;
using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// The visible pit wall (docs/PIT_LANE.md "Pitwall"), on the cross-section of Apex.Race.PitLaneLayout: concrete wall
    /// with a turquoise cap and sponsor boards (fictional, TrackKit banners) on its track face, catch fence above it, a
    /// crash cushion in front of its nose in the entry lane, and one stand per team on the deck behind the wall, opposite
    /// the team's garage (PitWallGeometry). The wall's collider comes from RoadSurface; the stands get box colliders.
    /// The stands are recorded in CircuitInstance.PitWallStands for PitWallComponent (engineers, live monitors).
    /// </summary>
    public static class PitWallBuilder
    {
        sealed class PartSet
        {
            public readonly Dictionary<string, MeshBuilder> parts = new Dictionary<string, MeshBuilder>();
            public MeshBuilder B(string key)
            {
                if (!parts.TryGetValue(key, out var mb)) parts[key] = mb = new MeshBuilder();
                return mb;
            }
        }

        static bool materialsReady;

        static Material Emissive(string name, Color c, float strength)
        {
            var m = new Material(EnvironmentMaterials.Solid(c)) { name = name };
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * strength);
            return m;
        }

        static void EnsureMaterials()
        {
            if (materialsReady) return;
            materialsReady = true;
            ApexMaterials.Register(PitWallGeometry.Deck, new Material(EnvironmentMaterials.Solid(new Color(0.24f, 0.25f, 0.27f))) { name = PitWallGeometry.Deck });
            ApexMaterials.Register(PitWallGeometry.Desk, new Material(EnvironmentMaterials.Solid(new Color(0.07f, 0.075f, 0.085f))) { name = PitWallGeometry.Desk });
            ApexMaterials.Register(PitWallGeometry.Bezel, new Material(EnvironmentMaterials.Solid(new Color(0.03f, 0.03f, 0.035f))) { name = PitWallGeometry.Bezel });
            ApexMaterials.Register(PitWallGeometry.Light, Emissive(PitWallGeometry.Light, new Color(1f, 0.96f, 0.88f), 1.6f));
            ApexMaterials.Register(PitWallGeometry.LedGreen, Emissive(PitWallGeometry.LedGreen, new Color(0.1f, 1f, 0.35f), 2.5f));
            ApexMaterials.Register(PitWallGeometry.LedAmber, Emissive(PitWallGeometry.LedAmber, new Color(1f, 0.6f, 0.05f), 2.5f));
            // Screens: the live texture is set by PitWallComponent (base and emission).
            ApexMaterials.Register(PitWallGeometry.Screen, Emissive(PitWallGeometry.Screen, Color.white, 1.1f));
            ApexMaterials.Register("PitCushionYellow", new Material(EnvironmentMaterials.Solid(new Color(0.98f, 0.78f, 0.05f))) { name = "PitCushionYellow" });
        }

        public static void Build(Transform parent, CircuitInstance ci)
        {
            var pit = ci.Pit;
            if (pit == null) return;
            EnsureMaterials();
            TrackInfrastructureBuilder.EnsureMaterials();
            var g = ci.Geometry;
            var plan = pit.PlanFor(g.Length);
            float side = pit.side;
            var root = new GameObject("PitWall").transform;
            root.SetParent(parent, false);

            // --- Wall, cap, sponsor boards, catch fence, crash cushion (track frame, 3.8 m blocks) ---------------
            var wall = new PartSet();
            float nose = PitLaneLayout.WallStart, end = plan.WallEnd;
            const float inner = PitLaneLayout.WallInner, outer = PitLaneLayout.WallOuter, top = 1.05f, fenceTop = 3.9f;
            int k = 0;
            for (float s = nose; s < end - 0.1f; s += 3.8f, k++)
            {
                float s1 = Mathf.Min(s + 3.8f, end);
                float d0 = g.Wrap(pit.entry + s), d1 = g.Wrap(pit.entry + s1);
                Vector3 P(float d, float fromEdge, float y) => (g.PositionAt(d) + g.RightAt(d) * (side * (g.WidthAt(d) * 0.5f + fromEdge))).ToUnity() + Vector3.up * y;
                var mid0 = P(d0, (inner + outer) * 0.5f, 0f);
                var mid1 = P(d1, (inner + outer) * 0.5f, 0f);
                var along = mid1 - mid0;
                float len = along.magnitude;
                if (len < 0.2f) continue;
                var rot = Quaternion.LookRotation(along / len, Vector3.up);
                var c = (mid0 + mid1) * 0.5f;
                wall.B(TrackInfrastructureGeometry.Concrete).Box(c + Vector3.up * (top * 0.5f - 0.1f), new Vector3(outer - inner, top + 0.2f, len - 0.02f), rot);
                wall.B(TrackInfrastructureGeometry.ModuleC).Box(c + Vector3.up * (top + 0.02f), new Vector3(outer - inner + 0.04f, 0.04f, len - 0.02f), rot);
                // Sponsor board on the track face of every other block (fictional brands of the circuit kit).
                if (k % 2 == 0)
                {
                    var trackFace = P(d0, inner - 0.005f, 0f);
                    var trackFace1 = P(d1, inner - 0.005f, 0f);
                    var n = (P(d0, inner - 1f, 0f) - trackFace).normalized;
                    var mb = wall.B(TrackKitGeometry.Banner(k / 2 % TrackKitGeometry.BannerVariants));
                    var lo = Vector3.up * 0.12f; var hi = Vector3.up * 0.92f;
                    var ta = trackFace + (trackFace1 - trackFace) * 0.04f; var tb = trackFace + (trackFace1 - trackFace) * 0.96f;
                    mb.QuadFacing(ta + lo, tb + lo, tb + hi, ta + hi, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), n);
                }
                // Catch fence: post at the block start, mesh panel over the block (both faces).
                var post = P(d0, inner + 0.08f, top);
                wall.B(TrackInfrastructureGeometry.Steel).Tube(post, post + Vector3.up * (fenceTop - top), 0.05f, 6, true);
                var fa = P(d0, inner + 0.06f, top + 0.05f); var fb = P(d1, inner + 0.06f, top + 0.05f);
                var fh = Vector3.up * (fenceTop - top - 0.1f);
                var fn = (P(d0, inner - 1f, 0f) - P(d0, inner, 0f)).normalized;
                wall.B(TrackInfrastructureGeometry.Fence).QuadFacing(fa, fb, fb + fh, fa + fh, new Vector2(0f, 0f), new Vector2(len / 2f, 0f), new Vector2(len / 2f, 1.4f), new Vector2(0f, 1.4f), fn);
                wall.B(TrackInfrastructureGeometry.Fence).QuadFacing(fb, fa, fa + fh, fb + fh, new Vector2(0f, 0f), new Vector2(len / 2f, 0f), new Vector2(len / 2f, 1.4f), new Vector2(0f, 1.4f), -fn);
            }
            // Crash cushion in front of the nose: yellow and dark segments, a little wider than the wall.
            for (int i = 0; i < 6; i++)
            {
                float s = nose - PitLaneLayout.AttenuatorLength + i * (PitLaneLayout.AttenuatorLength / 6f);
                float d = g.Wrap(pit.entry + s + PitLaneLayout.AttenuatorLength / 12f);
                var c = (g.PositionAt(d) + g.RightAt(d) * (side * (g.WidthAt(d) * 0.5f + (inner + outer) * 0.5f))).ToUnity();
                var fwd = g.TangentAt(d).ToUnity();
                var rot = Quaternion.LookRotation(new Vector3(fwd.x, 0f, fwd.z).normalized, Vector3.up);
                float hgt = 0.55f + 0.06f * i;
                wall.B(i % 2 == 0 ? "PitCushionYellow" : TrackKitGeometry.Dark).Box(c + Vector3.up * hgt * 0.5f, new Vector3(0.75f, hgt, PitLaneLayout.AttenuatorLength / 6f - 0.04f), rot);
            }
            EnvironmentBuilder.AddMeshObject(root, "PitWallStructure", wall.parts, true);

            // --- Team stands opposite the garages -------------------------------------------------------------
            var teamKeys = CircuitBuilder.TeamMaterials();
            int teams = Roster.Teams.Length;
            var done = new HashSet<int>();
            for (int b = 0; b < pit.boxCount; b++)
            {
                int team = pit.TeamOfBox(b, teams);
                if (!done.Add(team)) continue;   // one stand per team (opposite its first box)
                float d = g.Wrap(pit.BoxDistance(b));
                float since = -pit.ToEntry(d, g.Length);
                if (since < 2f || since > plan.WallEnd - PitWallGeometry.Length * 0.5f) continue;
                g.FrameAt(d, out var p, out var f, out _, side * (g.WidthAt(d) * 0.5f + outer));
                float yaw = Mathf.Atan2(f.X, f.Z) * Mathf.Rad2Deg + (side > 0 ? 0f : 180f);
                var set = new PartSet();
                PitWallGeometry.Stand(set.B, teamKeys[team], team);
                var go = EnvironmentBuilder.AddMeshObject(root, "PitWallStand" + team, set.parts, true);
                var rotY = Quaternion.Euler(0f, yaw, 0f);
                go.transform.SetPositionAndRotation(p.ToUnity(), rotY);
                var col = go.AddComponent<BoxCollider>();
                col.center = new Vector3(PitWallGeometry.Depth * 0.5f, 0.7f, 0f);
                col.size = new Vector3(PitWallGeometry.Depth, 1.4f, PitWallGeometry.Length);
                ci.PitWallStands.Add((p.ToUnity(), rotY, team));
            }
        }
    }
}
