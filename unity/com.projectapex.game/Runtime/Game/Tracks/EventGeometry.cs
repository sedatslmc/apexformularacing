using System.Collections.Generic;
using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// Mesh generation for the Bosphorus GP event identity (no GameObjects, also used by the
    /// offline preview): starting gantry with bridge-tower pylons, pit building with garage
    /// bays and tiled end panels, timing tower, race control, podium and the original trophy.
    /// Local frames are given per method. Builders are keyed by material.
    /// </summary>
    public static class EventGeometry
    {
        // Event palette (#C8102E, #087F8C, #C9A66B, #1D2835, #F0E9D9).
        public const string Red = "EventRed";
        public const string Turquoise = "EventTurquoise";
        public const string Gold = "EventGold";
        public const string Navy = "EventNavy";
        public const string Ivory = "EventIvory";
        public const string Tile = "IznikTile";
        public const string Glass = "TowerGlass";
        public const string Dark = "GarageDark";
        public const string Steel = "Steel";
        public const string Housing = "PanelHousing";

        public static readonly Color RedC = new Color(0.784f, 0.063f, 0.18f);
        public static readonly Color TurquoiseC = new Color(0.031f, 0.498f, 0.549f);
        public static readonly Color GoldC = new Color(0.788f, 0.651f, 0.42f);
        public static readonly Color NavyC = new Color(0.114f, 0.157f, 0.208f);
        public static readonly Color IvoryC = new Color(0.941f, 0.914f, 0.851f);

        public delegate MeshBuilder Parts(string material);

        static void Leg(MeshBuilder mb, Vector3 bottom, Vector3 top, float wBottom, float wTop, float depth)
        {
            var rings = new List<Vector3[]>();
            foreach (var (p, w) in new[] { (bottom, wBottom), (top, wTop) })
                rings.Add(new[] { p + new Vector3(-w, 0f, -depth), p + new Vector3(w, 0f, -depth), p + new Vector3(w, 0f, depth), p + new Vector3(-w, 0f, depth) });
            mb.RingLoft(rings, true, true);
        }

        /// <summary>
        /// Starting gantry over a track of width `w`. Local frame: origin on the start line at
        /// road level, +z = driving direction, +x = right. Pylons echo the bridge towers (two
        /// tapered legs with cross beams); the beam carries five light pods facing the grid (-z).
        /// Returns the lamp centres (two per pod) for the emissive lamp faces.
        /// </summary>
        public const float GantryBeamY = 7.0f, GantryBeamH = 1.5f;

        public static List<Vector3> Gantry(Parts B, float w)
        {
            float half = w * 0.5f + 3f;
            const float beamY = GantryBeamY, beamH = GantryBeamH;
            foreach (float s in new[] { -1f, 1f })
            {
                float x = s * half;
                foreach (float dz in new[] { -0.7f, 0.7f })
                    Leg(B(Navy), new Vector3(x, 0f, dz), new Vector3(x, beamY + beamH + 2.2f, dz * 0.7f), 0.38f, 0.26f, 0.22f);
                foreach (float y in new[] { 2.4f, beamY - 0.4f, beamY + beamH + 1.6f })
                    B(Gold).Box(new Vector3(x, y, 0f), new Vector3(0.7f, 0.28f, 1.6f));
                // Pylon caps.
                B(Gold).Box(new Vector3(x, beamY + beamH + 2.35f, 0f), new Vector3(0.8f, 0.3f, 1.2f));
            }
            // Box beam with an ivory pinstripe at the top (the lettering sits below it) and a red lower lip.
            B(Navy).Box(new Vector3(0f, beamY + beamH * 0.5f, 0f), new Vector3(2f * half + 0.6f, beamH, 1.1f));
            B(Ivory).Box(new Vector3(0f, beamY + beamH - 0.14f, -0.56f), new Vector3(2f * half - 1.2f, 0.08f, 0.02f));
            B(Ivory).Box(new Vector3(0f, beamY + beamH - 0.14f, 0.56f), new Vector3(2f * half - 1.2f, 0.08f, 0.02f));
            B(Red).Box(new Vector3(0f, beamY + 0.05f, 0f), new Vector3(2f * half + 0.6f, 0.1f, 1.14f));
            // Suspension-cable arc above the beam (the event motif).
            var arc = new List<Vector3>();
            for (int i = 0; i <= 16; i++)
            {
                float t = i / 16f;
                float x = Mathf.Lerp(-half, half, t);
                float y = beamY + beamH + 2.1f - 1.6f * (1f - (2f * t - 1f) * (2f * t - 1f));
                arc.Add(new Vector3(x, y, 0f));
            }
            B(Gold).TubePath(arc, 0.07f, 6, true);
            for (int i = 2; i <= 14; i += 2)
                B(Gold).Tube(arc[i] + Vector3.down * 0.05f, new Vector3(arc[i].x, beamY + beamH, 0f), 0.025f, 4, false);
            // Five light pods hanging under the beam, two lamps each, facing the grid.
            var lamps = new List<Vector3>();
            for (int k = 0; k < 5; k++)
            {
                float x = (k - 2) * 0.95f;
                B(Housing).Box(new Vector3(x, beamY - 0.75f, -0.1f), new Vector3(0.6f, 1.45f, 0.4f));
                lamps.Add(new Vector3(x, beamY - 0.45f, -0.31f));
                lamps.Add(new Vector3(x, beamY - 1.05f, -0.31f));
            }
            return lamps;
        }

        /// <summary>
        /// Pit building. Local frame: origin at the middle of its track-side face at ground level,
        /// +x away from the track, +z along the track. Garage bays (dark recesses) at the given
        /// z positions, navy header with an ivory band, glazed team suites, roof terrace, end
        /// walls clad in İznik-inspired tile panels.
        /// </summary>
        public const float PitDepth = Apex.Environment.EventLayout.PitDepth, PitGroundH = 4.4f, PitUpperH = 3.2f;
        /// <summary>Height of the pit building roof terrace (top of the slab).</summary>
        public const float PitRoof = PitGroundH + PitUpperH + 0.4f;

        public static void PitBuilding(Parts B, float length, IList<float> bayCentres, float bayWidth)
        {
            float half = length * 0.5f;
            const float depth = PitDepth, groundH = PitGroundH, upperH = PitUpperH;
            // Core volume behind the facade.
            B(Ivory).Box(new Vector3(depth * 0.5f + 0.4f, groundH * 0.5f, 0f), new Vector3(depth - 0.8f, groundH, length));
            B(Glass).Box(new Vector3(depth * 0.5f + 0.9f, groundH + upperH * 0.5f, 0f), new Vector3(depth - 1.8f, upperH, length - 1f));
            // Garage openings: dark recess per bay with ivory piers in between.
            foreach (float z in bayCentres)
                B(Dark).Box(new Vector3(0.36f, 1.75f, z), new Vector3(0.1f, 3.5f, bayWidth - 1.1f));
            B(Navy).Box(new Vector3(0.25f, groundH - 0.45f, 0f), new Vector3(0.3f, 0.9f, length));
            B(Ivory).Box(new Vector3(0.08f, groundH - 0.45f, 0f), new Vector3(0.04f, 0.16f, length - 0.4f));
            // Slab and canopy over the pit lane side.
            B(Navy).Box(new Vector3(depth * 0.5f - 0.6f, groundH + upperH + 0.2f, 0f), new Vector3(depth + 1.6f, 0.4f, length + 0.6f));
            B(Turquoise).Box(new Vector3(-0.7f, groundH + 0.1f, 0f), new Vector3(1.6f, 0.2f, length));
            // Roof terrace railing.
            float top = groundH + upperH + 0.4f;
            B(Steel).Box(new Vector3(0.2f, top + 1.0f, 0f), new Vector3(0.06f, 0.06f, length));
            for (float z = -half; z <= half + 0.01f; z += 3f)
                B(Steel).Box(new Vector3(0.2f, top + 0.5f, z), new Vector3(0.05f, 1.0f, 0.05f));
            // Tiled end walls.
            foreach (float s in new[] { -1f, 1f })
                B(Tile).Box(new Vector3(depth * 0.5f, (groundH + upperH) * 0.5f, s * (half + 0.06f)), new Vector3(depth, groundH + upperH, 0.12f));
        }

        /// <summary>Timing tower pylon (local frame: +z faces the track/approach), 3 m wide, 15 m high.</summary>
        public static void TimingTower(Parts B)
        {
            B(Navy).Box(new Vector3(0f, 7.5f, 0f), new Vector3(3.0f, 15f, 0.9f));
            B(Gold).Box(new Vector3(0f, 15.2f, 0f), new Vector3(3.3f, 0.4f, 1.1f));
            B(Red).Box(new Vector3(0f, 13.9f, 0.46f), new Vector3(2.8f, 0.08f, 0.02f));
            B(Tile).Box(new Vector3(0f, 0.9f, 0f), new Vector3(3.2f, 1.8f, 1.1f));
        }

        /// <summary>Race control building (local frame like the pit building), two glazed storeys.</summary>
        public static void RaceControl(Parts B)
        {
            B(Ivory).Box(new Vector3(6f, 2f, 0f), new Vector3(12f, 4f, 16f));
            B(Glass).Box(new Vector3(5.5f, 6f, 0f), new Vector3(11f, 4f, 15f));
            B(Navy).Box(new Vector3(5.6f, 8.2f, 0f), new Vector3(13f, 0.4f, 17f));
            B(Tile).Box(new Vector3(0.05f, 2f, 0f), new Vector3(0.1f, 3.2f, 6f));
        }

        /// <summary>Trophy position on the podium's top step (podium local frame).</summary>
        public static readonly Vector3 TrophyOnPodium = new Vector3(0f, 2.1f, 0.6f);

        /// <summary>Podium (local frame: +z faces the audience): three steps, backdrop, tile panels.</summary>
        public static void Podium(Parts B)
        {
            B(Navy).Box(new Vector3(0f, 0.3f, 0f), new Vector3(14f, 0.6f, 6f));
            B(Gold).Box(new Vector3(0f, 1.35f, 0.6f), new Vector3(2.4f, 1.5f, 2f));
            B(Ivory).Box(new Vector3(-2.6f, 1.05f, 0.6f), new Vector3(2.4f, 0.9f, 2f));
            B(Ivory).Box(new Vector3(2.6f, 0.85f, 0.6f), new Vector3(2.4f, 0.5f, 2f));
            B(Navy).Box(new Vector3(0f, 3.6f, -2.2f), new Vector3(14f, 6f, 0.3f));
            foreach (float x in new[] { -5.5f, 5.5f })
                B(Tile).Box(new Vector3(x, 3.6f, -2.0f), new Vector3(2.2f, 5.2f, 0.06f));
            B(Turquoise).Box(new Vector3(0f, 6.7f, -2.2f), new Vector3(14.4f, 0.3f, 0.5f));
        }

        /// <summary>
        /// Original Bosphorus GP trophy (≈0.75 m): stepped base, two slender pylons joined by a
        /// sagging cable (the bridge) and a twisted turquoise ribbon rising between them (the
        /// strait), topped by a gold apex crest. Origin at the base, +y up.
        /// </summary>
        public static void Trophy(Parts B)
        {
            LandmarkGeometry.VerticalLathe(B(Navy), Vector3.zero, new[] { new Vector2(0f, 0.13f), new Vector2(0.06f, 0.13f), new Vector2(0.08f, 0.1f), new Vector2(0.12f, 0.1f), new Vector2(0.12f, 0.001f) }, 16);
            foreach (float s in new[] { -1f, 1f })
                Leg(B(Gold), new Vector3(s * 0.07f, 0.12f, 0f), new Vector3(s * 0.05f, 0.62f, 0f), 0.012f, 0.008f, 0.012f);
            var cable = new List<Vector3>();
            for (int i = 0; i <= 12; i++)
            {
                float t = i / 12f;
                cable.Add(new Vector3(Mathf.Lerp(-0.05f, 0.05f, t), 0.6f - 0.12f * (1f - (2f * t - 1f) * (2f * t - 1f)), 0f));
            }
            B(Gold).TubePath(cable, 0.004f, 5, true);
            // Ribbon: a thin twisted strip (two-sided) spiralling up between the pylons.
            var rib = B(Turquoise);
            for (int i = 0; i < 20; i++)
            {
                float t0 = i / 20f, t1 = (i + 1) / 20f;
                Vector3 C(float t) => new Vector3(Mathf.Sin(t * Mathf.PI * 1.6f) * 0.025f, 0.13f + t * 0.42f, Mathf.Cos(t * Mathf.PI * 1.6f) * 0.012f);
                Vector3 Wd(float t) => new Vector3(Mathf.Cos(t * Mathf.PI * 2.2f), 0f, Mathf.Sin(t * Mathf.PI * 2.2f)) * 0.022f;
                var a0 = C(t0) - Wd(t0); var b0 = C(t0) + Wd(t0); var a1 = C(t1) - Wd(t1); var b1 = C(t1) + Wd(t1);
                var n = Vector3.Cross(b0 - a0, a1 - a0);
                rib.QuadFacing(a0, b0, b1, a1, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, n);
                rib.QuadFacing(a0, b0, b1, a1, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, -n);
            }
            // Apex crest: a gold chevron.
            B(Gold).Box(new Vector3(-0.015f, 0.66f, 0f), new Vector3(0.05f, 0.012f, 0.012f), Quaternion.Euler(0f, 0f, 35f));
            B(Gold).Box(new Vector3(0.015f, 0.66f, 0f), new Vector3(0.05f, 0.012f, 0.012f), Quaternion.Euler(0f, 0f, -35f));
        }

        /// <summary>Banner frame on two posts (local frame: +z = side the text faces), panel itself built by the caller.</summary>
        public static void BannerFrame(MeshBuilder mb, float width, float height, float bottom)
        {
            foreach (float s in new[] { -0.5f, 0.5f })
                mb.Tube(new Vector3(s * width, 0f, 0.08f), new Vector3(s * width, bottom + height + 0.1f, 0.08f), 0.05f, 5, false);
            mb.Box(new Vector3(0f, bottom + height + 0.05f, 0.08f), new Vector3(width, 0.06f, 0.06f));
        }
    }
}
