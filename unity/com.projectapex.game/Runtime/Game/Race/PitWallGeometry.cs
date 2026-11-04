using System;
using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// Own procedural models for the pit wall (placeholder art, docs/PIT_LANE.md "Pitwall"): a team stand on the deck
    /// behind the concrete wall – raised floor, the engineers' desk with two rows of monitors (screens UV-mapped to the
    /// four panels of the live PitWallDisplay texture), status LEDs, cable bundles, four stools with footrests, a roof in
    /// the team colour with a lit underside and a geometric team emblem – and the race engineer's head with headset.
    /// Stand frame: x away from the track (0 = the wall's pit-side face), y up from the lane, z along the lane, centred.
    /// </summary>
    public static class PitWallGeometry
    {
        public const string Deck = "PitWallDeck", Desk = "PitWallDesk", Screen = "PitWallScreen", Bezel = "PitWallBezel",
            Light = "PitWallLight", LedGreen = "PitWallLedGreen", LedAmber = "PitWallLedAmber", White = "Line";

        /// <summary>Stand dimensions (m): length along the lane, deck depth, floor height, desk top above the floor.</summary>
        public const float Length = 6f, Depth = 1.4f, Floor = 0.22f, DeskTop = 0.96f;
        /// <summary>Seats (stand frame, on the floor): x and the z of each engineer.</summary>
        public const float SeatX = 0.9f;
        public static readonly float[] SeatZ = { -2.2f, -0.75f, 0.75f, 2.2f };

        /// <summary>Screen (lower row i = 0..3, upper row 4..7) → panel of the display texture.</summary>
        public static Apex.UI.PitWallDisplay.Panel PanelOf(int screen) =>
            (Apex.UI.PitWallDisplay.Panel)((screen + (screen >= 4 ? 2 : 0)) % 4);

        /// <summary>One team stand; <paramref name="team"/> is the material key of the team colour.</summary>
        public static void Stand(Func<string, MeshBuilder> B, string team, int emblem)
        {
            float h = Length * 0.5f;
            // Raised floor with a checker plate edge, the desk cabinet along the wall.
            B(Deck).Box(new Vector3(Depth * 0.5f, Floor * 0.5f, 0f), new Vector3(Depth, Floor, Length));
            B(Desk).Box(new Vector3(0.3f, Floor + DeskTop * 0.5f, 0f), new Vector3(0.5f, DeskTop, Length - 0.2f));
            B(Desk).Box(new Vector3(0.36f, Floor + DeskTop + 0.02f, 0f), new Vector3(0.68f, 0.04f, Length - 0.1f));
            // Monitors: lower row on stands on the desk, upper row on a gantry, both tilted towards the seats.
            for (int i = 0; i < 8; i++)
            {
                bool upper = i >= 4;
                float z = SeatZ[i % 4];
                float y = Floor + DeskTop + (upper ? 0.68f : 0.27f);
                float x = upper ? 0.12f : 0.2f;
                var tilt = Quaternion.Euler(0f, 0f, upper ? -12f : 6f);   // screen faces +x: lower row up, upper row down to the eyes
                B(Bezel).Box(new Vector3(x - 0.03f, y, z), new Vector3(0.05f, 0.42f, 0.68f), tilt);
                var panel = PanelOf(i);
                var (px, py) = Apex.UI.PitWallDisplay.Origin(panel);
                float u0 = px / (float)Apex.UI.PitWallDisplay.Width, u1 = (px + Apex.UI.PitWallDisplay.PanelW) / (float)Apex.UI.PitWallDisplay.Width;
                // Texture row 0 is the top: v = 1 at the top of the image.
                float v1 = 1f - py / (float)Apex.UI.PitWallDisplay.Height, v0 = 1f - (py + Apex.UI.PitWallDisplay.PanelH) / (float)Apex.UI.PitWallDisplay.Height;
                Vector3 C(float dz, float dy) => new Vector3(x, y, z) + tilt * new Vector3(0f, dy, dz);
                // Seen from +x (looking towards −x) the viewer's right is +z: the panel's left edge (u0) at −z.
                var a = C(-0.32f, -0.18f); var b = C(0.32f, -0.18f); var c = C(0.32f, 0.18f); var d = C(-0.32f, 0.18f);
                B(Screen).QuadFacing(a, b, c, d, new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1), tilt * Vector3.right);
                // Status LEDs under the screen.
                for (int k = 0; k < 3; k++)
                    B(k == 2 ? LedAmber : LedGreen).Box(C(-0.2f + k * 0.04f, -0.2f) + new Vector3(0.01f, 0f, 0f), new Vector3(0.012f, 0.012f, 0.018f));
                if (!upper) B(Desk).Box(new Vector3(x - 0.05f, Floor + DeskTop + 0.04f, z), new Vector3(0.18f, 0.06f, 0.28f));   // monitor foot
            }
            // Upper monitor gantry and keyboards.
            foreach (float z in new[] { -h + 0.15f, h - 0.15f })
                B(Deck).Box(new Vector3(0.1f, Floor + DeskTop + 0.5f, z), new Vector3(0.06f, 1.0f, 0.06f));
            B(Deck).Box(new Vector3(0.1f, Floor + DeskTop + 0.98f, 0f), new Vector3(0.06f, 0.06f, Length - 0.3f));
            foreach (float z in SeatZ)
            {
                B(Bezel).Box(new Vector3(0.5f, Floor + DeskTop + 0.05f, z), new Vector3(0.16f, 0.02f, 0.44f));   // keyboard
                // Stool: base, post, seat, footrest ring.
                var seat = new Vector3(SeatX, Floor, z);
                B(Deck).Tube(seat + new Vector3(0f, 0.02f, 0f), seat + new Vector3(0f, 0.04f, 0f), 0.24f, 10, true);
                B(Deck).Tube(seat + new Vector3(0f, 0.04f, 0f), seat + new Vector3(0f, Apex.Race.PitCrewFigure.SeatHeight - 0.06f, 0f), 0.03f, 8, false);
                B(Desk).Tube(seat + new Vector3(0f, Apex.Race.PitCrewFigure.SeatHeight - 0.06f, 0f), seat + new Vector3(0f, Apex.Race.PitCrewFigure.SeatHeight, 0f), 0.2f, 12, true);
                B(Deck).Box(seat + new Vector3(-0.36f, 0.26f, 0f), new Vector3(0.04f, 0.04f, 0.42f));
            }
            // Cable bundles from the desk down over the floor to the back.
            foreach (float z in new[] { -1.5f, 1.5f })
                B(Bezel).TubePath(new[] { new Vector3(0.52f, Floor + 0.6f, z), new Vector3(0.6f, Floor + 0.05f, z), new Vector3(1.2f, Floor + 0.03f, z + 0.2f), new Vector3(1.38f, Floor - 0.3f, z + 0.25f) }, 0.03f, 6, true);
            // Back rail on the pit lane side with a panel in the team colour.
            B(team).Box(new Vector3(Depth - 0.03f, Floor + 0.35f, 0f), new Vector3(0.04f, 0.5f, Length));
            B(Deck).Box(new Vector3(Depth - 0.03f, Floor + 1.0f, 0f), new Vector3(0.06f, 0.05f, Length));
            for (float z = -h; z <= h + 0.01f; z += 1.5f) B(Deck).Box(new Vector3(Depth - 0.03f, Floor + 0.5f, z), new Vector3(0.05f, 1.0f, 0.05f));
            // Roof on four posts: team colour fascia, a white stripe and emblem, lit underside.
            const float roofY = 2.7f;
            foreach (float x in new[] { 0.06f, Depth - 0.06f })
                foreach (float z in new[] { -h + 0.08f, h - 0.08f })
                    B(Deck).Box(new Vector3(x, (Floor + roofY) * 0.5f, z), new Vector3(0.08f, roofY - Floor, 0.08f));
            B(team).Box(new Vector3(Depth * 0.5f - 0.05f, roofY + 0.08f, 0f), new Vector3(Depth + 0.5f, 0.16f, Length + 0.3f));
            B(team).Box(new Vector3(-0.2f, roofY - 0.08f, 0f), new Vector3(0.06f, 0.32f, Length + 0.3f));
            B(team).Box(new Vector3(Depth + 0.2f, roofY - 0.08f, 0f), new Vector3(0.06f, 0.32f, Length + 0.3f));
            B(White).Box(new Vector3(-0.235f, roofY - 0.16f, 0f), new Vector3(0.012f, 0.04f, Length + 0.3f));
            B(White).Box(new Vector3(Depth + 0.235f, roofY - 0.16f, 0f), new Vector3(0.012f, 0.04f, Length + 0.3f));
            B(Light).Box(new Vector3(Depth * 0.5f, roofY - 0.005f, 0f), new Vector3(Depth - 0.3f, 0.01f, Length - 0.6f));
            // Team emblem on both fascias: a geometric mark (variant per team), never a real brand.
            foreach (float fx in new[] { -0.24f, Depth + 0.24f })
                Emblem(B(White), new Vector3(fx, roofY - 0.08f, 0f), emblem, fx < 0f ? -1f : 1f);
        }

        /// <summary>Simple geometric team mark on a fascia (centre, facing ±x): chevron, ring, bars or diamond.</summary>
        static void Emblem(MeshBuilder mb, Vector3 c, int variant, float face)
        {
            var t = new Vector3(face * 0.01f, 0f, 0f);
            switch (variant % 4)
            {
                case 0:
                    mb.Box(c + t + new Vector3(0f, 0f, -0.08f), new Vector3(0.012f, 0.05f, 0.22f), Quaternion.Euler(35f, 0f, 0f));
                    mb.Box(c + t + new Vector3(0f, 0f, 0.08f), new Vector3(0.012f, 0.05f, 0.22f), Quaternion.Euler(-35f, 0f, 0f));
                    break;
                case 1:
                    for (int k = 0; k < 10; k++)
                    {
                        float a = k * Mathf.PI * 0.2f;
                        mb.Box(c + t + new Vector3(0f, Mathf.Sin(a) * 0.1f, Mathf.Cos(a) * 0.1f), new Vector3(0.012f, 0.04f, 0.07f), Quaternion.Euler(-a * Mathf.Rad2Deg, 0f, 0f));
                    }
                    break;
                case 2:
                    for (int k = -1; k <= 1; k++) mb.Box(c + t + new Vector3(0f, 0f, k * 0.09f), new Vector3(0.012f, 0.2f - Mathf.Abs(k) * 0.05f, 0.05f));
                    break;
                default:
                    mb.Box(c + t, new Vector3(0.012f, 0.16f, 0.16f), Quaternion.Euler(45f, 0f, 0f));
                    break;
            }
        }

        /// <summary>Race engineer's head (neck frame like PitCrewGeometry's head): face, hair, headset with microphone.
        /// Slots: 0 skin, 1 dark (hair, headset).</summary>
        public static MeshBuilder[] EngineerHead()
        {
            var mb = new[] { new MeshBuilder(), new MeshBuilder() };
            int before = mb[0].VertexCount;
            var prof = new System.Collections.Generic.List<Vector2>();
            // Neck, then the head as a slightly elongated ball (lathe along x, turned upright below).
            prof.Add(new Vector2(0f, 0f)); prof.Add(new Vector2(0.01f, 0.05f)); prof.Add(new Vector2(0.07f, 0.05f));
            for (int i = 0; i <= 8; i++)
            {
                float a = -0.45f * Mathf.PI + 0.95f * Mathf.PI * i / 8f;
                prof.Add(new Vector2(0.17f + 0.11f * Mathf.Sin(a), i == 8 ? 0f : 0.095f * Mathf.Cos(a)));
            }
            mb[0].Lathe(prof, 12);
            mb[0].TransformLast(mb[0].VertexCount - before, v => new Vector3(-v.y, v.x, v.z * 1.1f));
            // Short hair cap over the back and top.
            int hb = mb[1].VertexCount;
            var hair = new System.Collections.Generic.List<Vector2>();
            for (int i = 0; i <= 6; i++)
            {
                float a = 0.0f + 0.5f * Mathf.PI * i / 6f;
                hair.Add(new Vector2(0.19f + 0.1f * Mathf.Sin(a), i == 6 ? 0f : 0.1f * Mathf.Cos(a)));
            }
            mb[1].Lathe(hair, 12);
            mb[1].TransformLast(mb[1].VertexCount - hb, v => new Vector3(-v.y * 1.02f, v.x, v.z * 1.1f - 0.008f));
            // Headset: band over the top, ear cups, boom microphone to the mouth.
            var band = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i <= 8; i++)
            {
                float a = Mathf.PI * i / 8f;
                band.Add(new Vector3(Mathf.Cos(a) * 0.105f, 0.19f + Mathf.Sin(a) * 0.1f, -0.01f));
            }
            mb[1].TubePath(band, 0.008f, 5, true);
            foreach (float sx in new[] { -1f, 1f })
                mb[1].Tube(new Vector3(sx * 0.1f, 0.17f, -0.01f), new Vector3(sx * 0.125f, 0.17f, -0.01f), 0.045f, 10, true);
            mb[1].TubePath(new[] { new Vector3(0.12f, 0.16f, 0.01f), new Vector3(0.1f, 0.12f, 0.08f), new Vector3(0.04f, 0.11f, 0.11f) }, 0.006f, 5, true);
            return mb;
        }
    }
}
