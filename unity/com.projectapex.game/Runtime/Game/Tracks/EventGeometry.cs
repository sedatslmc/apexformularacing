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

        public static List<Vector3> Gantry(Parts B, float w) => Gantry(B, w, 3f, 3f);

        /// <summary>
        /// Start gantry with its pylons <paramref name="leftOut"/> / <paramref name="rightOut"/> metres beyond the track
        /// edges. On the pit side the pylon stands on the pit wall deck (PitLaneLayout), never in the pit lane: the beam
        /// then spans the track only and the lights stay centred over it.
        /// </summary>
        public static List<Vector3> Gantry(Parts B, float w, float leftOut, float rightOut)
        {
            float xl = -(w * 0.5f + leftOut), xr = w * 0.5f + rightOut;
            float half = (xr - xl) * 0.5f, mid = (xr + xl) * 0.5f;
            const float beamY = GantryBeamY, beamH = GantryBeamH;
            foreach (float x in new[] { xl, xr })
            {
                foreach (float dz in new[] { -0.7f, 0.7f })
                    Leg(B(Navy), new Vector3(x, 0f, dz), new Vector3(x, beamY + beamH + 2.2f, dz * 0.7f), 0.38f, 0.26f, 0.22f);
                foreach (float y in new[] { 2.4f, beamY - 0.4f, beamY + beamH + 1.6f })
                    B(Gold).Box(new Vector3(x, y, 0f), new Vector3(0.7f, 0.28f, 1.6f));
                // Pylon caps.
                B(Gold).Box(new Vector3(x, beamY + beamH + 2.35f, 0f), new Vector3(0.8f, 0.3f, 1.2f));
            }
            // Box beam with an ivory pinstripe at the top (the lettering sits below it) and a red lower lip.
            B(Navy).Box(new Vector3(mid, beamY + beamH * 0.5f, 0f), new Vector3(2f * half + 0.6f, beamH, 1.1f));
            B(Ivory).Box(new Vector3(mid, beamY + beamH - 0.14f, -0.56f), new Vector3(2f * half - 1.2f, 0.08f, 0.02f));
            B(Ivory).Box(new Vector3(mid, beamY + beamH - 0.14f, 0.56f), new Vector3(2f * half - 1.2f, 0.08f, 0.02f));
            B(Red).Box(new Vector3(mid, beamY + 0.05f, 0f), new Vector3(2f * half + 0.6f, 0.1f, 1.14f));
            // Suspension-cable arc above the beam (the event motif).
            var arc = new List<Vector3>();
            for (int i = 0; i <= 16; i++)
            {
                float t = i / 16f;
                float x = Mathf.Lerp(xl, xr, t);
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

        // Pit building detail materials (registered by EventBrandingBuilder).
        public const string GarageFloor = "GarageFloor", GarageLight = "GarageLight", ToolCart = "GarageToolCart", Blanket = "GarageBlanket",
            FlightCase = "GarageCase", RoofScreen = PitWallGeometry.Screen, NumberLight = "GarageNumberLight", FlagVertical = "TurkishFlagVertical";

        /// <summary>
        /// Pit building (local frame: +x away from the track, front face at x = 0, +z along the lane): per bay a garage
        /// with a wide opening split by a slim mullion, the roller-door drums and a fascia in the team colour (keys per
        /// bay, or navy), a lit box number plate, and a furnished interior – epoxy floor, ceiling light strips, team-colour
        /// back wall with live data screens, tyre trolleys with tyres in blankets, tool carts, flight cases and a spare
        /// front wing on the wall. Upper floor: glazed team suites with mullions and a balcony over the lane with a glass
        /// railing. Red-white band and turquoise balcony edge, tiled end walls, roof terrace with a glazed VIP pavilion
        /// (on the half away from the podium) and two LED screens facing the track that show the live timing.
        /// </summary>
        public static void PitBuilding(Parts B, float length, IList<float> bayCentres, float bayWidth, IList<string> bayTeam = null, float podiumZ = 0f)
        {
            float half = length * 0.5f;
            const float depth = PitDepth, groundH = PitGroundH, upperH = PitUpperH;
            const float back = depth - 1.0f;        // garage back wall
            // Core volume behind the garages and the glazed upper floor.
            B(Ivory).Box(new Vector3(depth - 0.5f, groundH * 0.5f, 0f), new Vector3(1.0f, groundH, length));
            B(Glass).Box(new Vector3(depth * 0.5f + 0.9f, groundH + upperH * 0.5f, 0f), new Vector3(depth - 1.8f, upperH, length - 1f));
            for (float z = -half + 0.75f; z < half; z += 1.5f)
                B(Steel).Box(new Vector3(1.78f, groundH + upperH * 0.5f, z), new Vector3(0.08f, upperH, 0.08f));
            // Ground floor slab (ceiling of the garages) and the header with the red-white band.
            B(Ivory).Box(new Vector3(depth * 0.5f, groundH - 0.15f, 0f), new Vector3(depth, 0.3f, length));
            B(Navy).Box(new Vector3(0.25f, groundH - 0.45f, 0f), new Vector3(0.3f, 0.9f, length));
            B(Red).Box(new Vector3(0.08f, groundH - 0.2f, 0f), new Vector3(0.04f, 0.12f, length - 0.4f));
            B(Ivory).Box(new Vector3(0.08f, groundH - 0.34f, 0f), new Vector3(0.04f, 0.12f, length - 0.4f));
            // Balcony over the lane: slab with a turquoise edge, glass railing and handrail.
            B(Navy).Box(new Vector3(-0.8f, groundH + 0.08f, 0f), new Vector3(1.8f, 0.2f, length));
            B(Turquoise).Box(new Vector3(-1.69f, groundH + 0.08f, 0f), new Vector3(0.04f, 0.24f, length));
            B(Glass).Box(new Vector3(-1.62f, groundH + 0.7f, 0f), new Vector3(0.03f, 1.0f, length - 0.4f));
            B(Steel).Box(new Vector3(-1.62f, groundH + 1.22f, 0f), new Vector3(0.07f, 0.05f, length - 0.4f));
            // Garages.
            float pier = 0.9f;
            for (int k = 0; k < bayCentres.Count; k++)
            {
                float z = bayCentres[k];
                string team = bayTeam != null && k < bayTeam.Count && bayTeam[k] != null ? bayTeam[k] : Navy;
                float w = bayWidth - pier;
                // Piers between the garages (and at the building ends).
                B(Ivory).Box(new Vector3(back * 0.5f, (groundH - 0.3f) * 0.5f, z - bayWidth * 0.5f), new Vector3(back, groundH - 0.3f, pier));
                if (k == bayCentres.Count - 1) B(Ivory).Box(new Vector3(back * 0.5f, (groundH - 0.3f) * 0.5f, z + bayWidth * 0.5f), new Vector3(back, groundH - 0.3f, pier));
                // Slim centre mullion: two doors per garage; roller-door drums and fascia in the team colour.
                B(Steel).Box(new Vector3(0.3f, (groundH - 0.9f) * 0.5f, z), new Vector3(0.25f, groundH - 0.9f, 0.3f));
                foreach (float dz in new[] { -w * 0.25f, w * 0.25f })
                    B(team).Tube(new Vector3(0.45f, groundH - 1.15f, z + dz - w * 0.24f), new Vector3(0.45f, groundH - 1.15f, z + dz + w * 0.24f), 0.22f, 10, true);
                B(team).Box(new Vector3(0.2f, groundH - 1.15f, z), new Vector3(0.12f, 0.5f, w));
                // Lit box number plate on the fascia (the number itself is a TextMesh by EventBrandingBuilder).
                B(NumberLight).Box(new Vector3(0.12f, groundH - 1.15f, z - w * 0.5f + 1.1f), new Vector3(0.04f, 0.42f, 1.4f));
                // Interior: floor, ceiling lights, back wall in the team colour with three data screens, side panels.
                B(GarageFloor).Box(new Vector3(back * 0.5f, 0.01f, z), new Vector3(back - 0.2f, 0.02f, w));
                for (int l = -1; l <= 1; l++)
                    B(GarageLight).Box(new Vector3(back * 0.5f, groundH - 0.32f, z + l * w * 0.3f), new Vector3(back - 1.5f, 0.03f, 0.25f));
                B(team).Box(new Vector3(back - 0.05f, 1.6f, z), new Vector3(0.1f, 3.2f, w));
                B(Ivory).Box(new Vector3(back - 0.11f, 2.6f, z), new Vector3(0.02f, 0.12f, w));
                for (int sIdx = -1; sIdx <= 1; sIdx++)
                {
                    var c = new Vector3(back - 0.13f, 2.0f, z + sIdx * 1.4f);
                    B(Dark).Box(c + new Vector3(0.01f, 0f, 0f), new Vector3(0.04f, 0.72f, 1.2f));
                    var panel = (Apex.UI.PitWallDisplay.Panel)((sIdx + 1 + k) % 4);
                    var (px, py) = Apex.UI.PitWallDisplay.Origin(panel);
                    float u0 = px / 256f, u1 = (px + 128) / 256f, v1 = 1f - py / 128f, v0 = 1f - (py + 64) / 128f;
                    // Facing −x (out to the lane): the viewer's right is −z.
                    B(RoofScreen).QuadFacing(c + new Vector3(-0.02f, -0.32f, 0.56f), c + new Vector3(-0.02f, -0.32f, -0.56f), c + new Vector3(-0.02f, 0.32f, -0.56f), c + new Vector3(-0.02f, 0.32f, 0.56f),
                        new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1), Vector3.left);
                }
                foreach (float sz in new[] { -1f, 1f })
                    B(team).Box(new Vector3(back * 0.5f + 0.5f, 0.6f, z + sz * (w * 0.5f - 0.03f)), new Vector3(back - 1f, 1.2f, 0.04f));
                // Tyre trolleys (two stacks of wheels in blankets), tool carts, flight cases, spare front wing.
                foreach (float tz in new[] { z - w * 0.36f, z + w * 0.36f })
                {
                    var trolley = new Vector3(back - 1.2f, 0f, tz);
                    B(Steel).Box(trolley + new Vector3(0f, 0.15f, 0f), new Vector3(0.9f, 0.05f, 1.6f));
                    foreach (float oz in new[] { -0.38f, 0.38f })
                        for (int t = 0; t < 2; t++)
                            B(Blanket).Tube(trolley + new Vector3(0f, 0.2f + t * 0.4f, oz), trolley + new Vector3(0f, 0.56f + t * 0.4f, oz), 0.34f, 12, true);
                    B(Steel).Box(trolley + new Vector3(0.45f, 0.6f, 0f), new Vector3(0.04f, 1.2f, 0.04f));
                }
                for (int t = 0; t < 2; t++)
                {
                    var cart = new Vector3(back - 0.6f, 0.5f, z - 2.2f + t * 1.1f);
                    B(ToolCart).Box(cart, new Vector3(0.6f, 1.0f, 1.0f));
                    for (int dr = 0; dr < 4; dr++) B(Dark).Box(cart + new Vector3(-0.31f, -0.35f + dr * 0.22f, 0f), new Vector3(0.01f, 0.02f, 0.85f));
                }
                for (int t = 0; t < 3; t++)
                    B(FlightCase).Box(new Vector3(back - 0.55f, 0.3f + (t == 2 ? 0.6f : 0f), z + 1.6f + (t % 2) * 0.9f), new Vector3(0.7f, 0.6f, 0.8f));
                // Tyre racks along both side walls: sets of four in blankets, two shelves.
                foreach (float sz in new[] { -1f, 1f })
                {
                    float rz = z + sz * (w * 0.5f - 0.45f);
                    for (int shelf = 0; shelf < 2; shelf++)
                    {
                        float ry = 0.25f + shelf * 1.0f;
                        B(Steel).Box(new Vector3(back * 0.5f, ry - 0.05f, rz), new Vector3(5.2f, 0.04f, 0.7f));
                        for (int t = 0; t < 7; t++)
                            B(t % 4 == 3 ? Dark : Blanket).Tube(new Vector3(back * 0.5f - 2.3f + t * 0.75f, ry, rz), new Vector3(back * 0.5f - 2.3f + t * 0.75f, ry + 0.58f, rz), 0.31f, 10, true);
                    }
                    foreach (float rx in new[] { back * 0.5f - 2.6f, back * 0.5f + 2.6f })
                        B(Steel).Box(new Vector3(rx, 1.0f, rz), new Vector3(0.05f, 2.0f, 0.7f));
                }
                // Data station in the middle: desk with two screens and a chair, a stack of flight cases by the door.
                var station = new Vector3(back - 2.8f, 0f, z + 0.3f);
                B(FlightCase).Box(station + new Vector3(0f, 0.45f, 0f), new Vector3(0.7f, 0.9f, 1.6f));
                for (int sIdx = 0; sIdx < 2; sIdx++)
                {
                    var c = station + new Vector3(-0.05f, 1.2f, -0.4f + sIdx * 0.8f);
                    B(Dark).Box(c + new Vector3(0.03f, 0f, 0f), new Vector3(0.04f, 0.42f, 0.7f));
                    var (px, py) = Apex.UI.PitWallDisplay.Origin((Apex.UI.PitWallDisplay.Panel)((sIdx + k + 1) % 4));
                    float u0 = px / 256f, u1 = (px + 128) / 256f, v1 = 1f - py / 128f, v0 = 1f - (py + 64) / 128f;
                    B(RoofScreen).QuadFacing(c + new Vector3(0f, -0.19f, 0.33f), c + new Vector3(0f, -0.19f, -0.33f), c + new Vector3(0f, 0.19f, -0.33f), c + new Vector3(0f, 0.19f, 0.33f),
                        new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1), Vector3.left);
                }
                B(Dark).Tube(station + new Vector3(-0.8f, 0.05f, 0f), station + new Vector3(-0.8f, 0.55f, 0f), 0.05f, 6, false);
                B(Dark).Tube(station + new Vector3(-0.8f, 0.55f, 0f), station + new Vector3(-0.8f, 0.62f, 0f), 0.24f, 10, true);
                for (int t = 0; t < 3; t++)
                    B(FlightCase).Box(new Vector3(1.6f, 0.3f + t * 0.55f, z - w * 0.5f + 1.2f), new Vector3(0.9f, 0.52f, 0.7f));
                B(Dark).Box(new Vector3(back - 0.16f, 3.35f, z), new Vector3(0.2f, 0.08f, 1.9f));   // spare front wing on brackets
                B(Steel).Box(new Vector3(back - 0.12f, 3.25f, z - 0.7f), new Vector3(0.14f, 0.2f, 0.04f));
                B(Steel).Box(new Vector3(back - 0.12f, 3.25f, z + 0.7f), new Vector3(0.14f, 0.2f, 0.04f));
            }
            // Building ends beyond the garages: closed ivory walls.
            if (bayCentres.Count > 0)
            {
                float first = bayCentres[0] - bayWidth * 0.5f - pier * 0.5f, last = bayCentres[bayCentres.Count - 1] + bayWidth * 0.5f + pier * 0.5f;
                float lo = Mathf.Min(first, last), hi = Mathf.Max(first, last);
                if (lo > -half) B(Ivory).Box(new Vector3(back * 0.5f, groundH * 0.5f, (-half + lo) * 0.5f), new Vector3(back, groundH, lo + half));
                if (hi < half) B(Ivory).Box(new Vector3(back * 0.5f, groundH * 0.5f, (hi + half) * 0.5f), new Vector3(back, groundH, half - hi));
            }
            // Roof slab and terrace railing.
            B(Navy).Box(new Vector3(depth * 0.5f - 0.6f, groundH + upperH + 0.2f, 0f), new Vector3(depth + 1.6f, 0.4f, length + 0.6f));
            float top = groundH + upperH + 0.4f;
            B(Steel).Box(new Vector3(0.2f, top + 1.0f, 0f), new Vector3(0.06f, 0.06f, length));
            for (float z = -half; z <= half + 0.01f; z += 3f)
                B(Steel).Box(new Vector3(0.2f, top + 0.5f, z), new Vector3(0.05f, 1.0f, 0.05f));
            // VIP hospitality pavilion on the half away from the podium: glazed, with a canopy over the terrace.
            float vipZ = podiumZ <= 0f ? half * 0.45f : -half * 0.45f, vipL = length * 0.35f;
            B(Glass).Box(new Vector3(depth * 0.5f + 1.2f, top + 1.6f, vipZ), new Vector3(depth - 3f, 3.2f, vipL));
            B(Ivory).Box(new Vector3(depth * 0.5f + 0.4f, top + 3.3f, vipZ), new Vector3(depth - 0.4f, 0.2f, vipL + 2f));
            B(Turquoise).Box(new Vector3(0.6f, top + 3.3f, vipZ), new Vector3(0.06f, 0.26f, vipL + 2f));
            for (float z = vipZ - vipL * 0.5f - 1f; z <= vipZ + vipL * 0.5f + 1.01f; z += 4f)
                B(Steel).Box(new Vector3(0.7f, top + 1.6f, z), new Vector3(0.08f, 3.2f, 0.08f));
            // LED screens on the roof edge facing the track, with the live timing (the pit wall's display texture).
            foreach (float sz in new[] { -half * 0.62f, half * 0.62f })
            {
                if (Mathf.Abs(sz - podiumZ) < 9f) continue;
                var c = new Vector3(0.3f, top + 3.6f, sz);
                B(Navy).Box(c + new Vector3(0.12f, 0f, 0f), new Vector3(0.2f, 3.8f, 6.6f));
                B(Steel).Box(c + new Vector3(0.2f, -2.4f, 0f), new Vector3(0.2f, 1.6f, 0.2f));
                // Timing panel (top-left of the texture), seen from the track (−x): the viewer's right is −z.
                B(RoofScreen).QuadFacing(c + new Vector3(-0.01f, -1.7f, 3.1f), c + new Vector3(-0.01f, -1.7f, -3.1f), c + new Vector3(-0.01f, 1.7f, -3.1f), c + new Vector3(-0.01f, 1.7f, 3.1f),
                    new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 1f), Vector3.left);
            }
            // Tiled end walls.
            foreach (float s in new[] { -1f, 1f })
                B(Tile).Box(new Vector3(depth * 0.5f, (groundH + upperH) * 0.5f, s * (half + 0.06f)), new Vector3(depth, groundH + upperH, 0.12f));
            // Two great vertical Turkish flags on the upper facade near the ends (hoist at the top), between the balcony
            // and the roof: one each side, not more.
            foreach (float s in new[] { -1f, 1f })
            {
                float fz = s * (half - 6f);
                const float fw = 2.4f, fh = 3.0f;
                var c = new Vector3(1.7f - 0.06f, groundH + 0.4f + fh * 0.5f, fz);
                B(Steel).Box(c + new Vector3(0f, fh * 0.5f + 0.06f, 0f), new Vector3(0.05f, 0.05f, fw + 0.3f));
                // Facing −x: viewer's right is −z. u along the flag's length (hoist → fly) runs top → bottom.
                B(FlagVertical).QuadFacing(c + new Vector3(0f, -fh * 0.5f, fw * 0.5f), c + new Vector3(0f, -fh * 0.5f, -fw * 0.5f), c + new Vector3(0f, fh * 0.5f, -fw * 0.5f), c + new Vector3(0f, fh * 0.5f, fw * 0.5f),
                    new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), Vector3.left);
            }
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
