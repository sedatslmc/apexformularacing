using System;
using System.Collections.Generic;
using Apex.Environment;
using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// Mesh generation for the city environment (no GameObjects, so it also runs in the
    /// offline preview tool): building blocks, far terrain, quay walls, trees,
    /// grandstands, hospitality, catch fencing, lamps and small instanced meshes.
    /// All geometry is procedural placeholder art, not photorealistic.
    /// Builders are keyed by material.
    /// </summary>
    public static class EnvironmentGeometry
    {
        public const string RoofTile = "RoofTile";
        public const string RoofFlat = "RoofFlat";
        public const string Tank = "Tank";
        public const string TowerGlass = "TowerGlass";
        public const string FarGround = "FarGround";
        public const string Quay = "Quay";
        public const string Pavement = "Pavement";
        public const string Concrete = "Concrete";
        public const string Seat = "Seat";
        public const string SeatAccent = "SeatAccent";
        public const string Canopy = "Canopy";
        public const string CatchFence = "CatchFence";
        public const string Post = "Steel";
        public const string LampHead = "LampHead";
        public const string Bark = "Bark";
        public const string Foliage = "Foliage";
        public const string Pine = "Pine";
        public const string Cypress = "Cypress";
        public const string Skin = "Skin";
        public const string Shirt = "Shirt";
        public const string Gull = "Gull";
        public const string FlagPole = "FlagPole";
        public const string BridgeLight = "BridgeLight";

        public static string Facade(int style) => "Facade" + style;

        /// <summary>Facade texture repeat: 8 window bays (3.5 m) by 8 floors per tile.</summary>
        public const float BayWidth = 3.5f;
        public const int TileBays = 8;

        public delegate MeshBuilder Parts(string material);

        static Vector3 Up(Vector3 v, float y) => new Vector3(v.x, v.y + y, v.z);

        /// <summary>Building block from a city lot. detailed adds roof forms and water tanks.</summary>
        public static void Building(Parts B, CityLayout.Lot lot, bool detailed)
        {
            float s = Mathf.Sin(lot.yaw), c = Mathf.Cos(lot.yaw);
            Vector3 L(float u, float v, float y) => new Vector3(lot.x + c * u + s * v, y, lot.z - s * u + c * v);
            float hw = lot.w * 0.5f, hd = lot.d * 0.5f;
            float y0 = lot.ground, y1 = lot.ground + lot.height;
            // Facade v starts at the visible ground floor so shopfronts sit at street level.
            float tileH = CityLayout.FloorHeight * TileBays, tileW = BayWidth * TileBays;
            var corners = new[] { L(-hw, -hd, 0f), L(hw, -hd, 0f), L(hw, hd, 0f), L(-hw, hd, 0f) };
            string wall = lot.roof == 2 ? TowerGlass : Facade(lot.style);
            float u0 = (lot.x * 0.37f + lot.z * 0.11f) % tileW;
            for (int i = 0; i < 4; i++)
            {
                var a = corners[i]; var b = corners[(i + 1) % 4];
                float len = (b - a).magnitude;
                var outward = Vector3.Cross(b - a, Vector3.up) * -1f;
                float ua = u0 / tileW, ub = (u0 + len) / tileW;
                float vb = -0.6f / tileH, vt = (lot.height - 0.6f) / tileH;
                B(wall).QuadFacing(Up(a, y0), Up(b, y0), Up(b, y1), Up(a, y1),
                    new Vector2(ua, vb), new Vector2(ub, vb), new Vector2(ub, vt), new Vector2(ua, vt), outward);
                u0 += len;
            }
            if (lot.roof == 1 && lot.band < 2)
            {
                // Hip roof with terracotta tiles, ridge along the long side.
                float rise = Mathf.Min(hw, hd) * 0.55f;
                bool alongU = hw >= hd;
                float r = alongU ? hw - hd : hd - hw;
                Vector3 ra = alongU ? L(-r, 0f, 0f) : L(0f, -r, 0f);
                Vector3 rb = alongU ? L(r, 0f, 0f) : L(0f, r, 0f);
                ra = Up(ra, y1 + rise); rb = Up(rb, y1 + rise);
                var e = new Vector3[4];
                for (int i = 0; i < 4; i++) e[i] = Up(corners[i], y1);
                var ctr = new Vector3(lot.x, y1, lot.z);
                Vector3 Out(Vector3 p) => (p - ctr) + Vector3.up * 2f;
                if (alongU)
                {
                    B(RoofTile).QuadFacing(e[0], e[1], rb, ra, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.75f, 1), new Vector2(0.25f, 1), Out((e[0] + e[1]) * 0.5f));
                    B(RoofTile).QuadFacing(e[2], e[3], ra, rb, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.75f, 1), new Vector2(0.25f, 1), Out((e[2] + e[3]) * 0.5f));
                    B(RoofTile).TriangleFacing(e[1], e[2], rb, Vector2.zero, Vector2.right, new Vector2(0.5f, 1f), Out((e[1] + e[2]) * 0.5f));
                    B(RoofTile).TriangleFacing(e[3], e[0], ra, Vector2.zero, Vector2.right, new Vector2(0.5f, 1f), Out((e[3] + e[0]) * 0.5f));
                }
                else
                {
                    B(RoofTile).QuadFacing(e[1], e[2], rb, ra, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.75f, 1), new Vector2(0.25f, 1), Out((e[1] + e[2]) * 0.5f));
                    B(RoofTile).QuadFacing(e[3], e[0], ra, rb, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.75f, 1), new Vector2(0.25f, 1), Out((e[3] + e[0]) * 0.5f));
                    B(RoofTile).TriangleFacing(e[0], e[1], ra, Vector2.zero, Vector2.right, new Vector2(0.5f, 1f), Out((e[0] + e[1]) * 0.5f));
                    B(RoofTile).TriangleFacing(e[2], e[3], rb, Vector2.zero, Vector2.right, new Vector2(0.5f, 1f), Out((e[2] + e[3]) * 0.5f));
                }
                return;
            }
            B(lot.roof == 2 ? TowerGlass : RoofFlat).QuadFacing(Up(corners[0], y1), Up(corners[1], y1), Up(corners[2], y1), Up(corners[3], y1),
                Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector3.up);
            if (detailed && lot.roof == 0)
            {
                // Rooftop water tanks and a stair head, typical of Istanbul flat roofs.
                var q = Quaternion.Euler(0f, lot.yaw * Mathf.Rad2Deg, 0f);
                B(Tank).Box(Up(L(hw * 0.4f, hd * 0.3f, 0f), y1 + 0.7f), new Vector3(1.6f, 1.4f, 1.1f), q);
                B(RoofFlat).Box(Up(L(-hw * 0.35f, -hd * 0.2f, 0f), y1 + 1.2f), new Vector3(3f, 2.4f, 3f), q);
            }
        }

        /// <summary>
        /// Far landscape on a 96 m grid around the near terrain (cells inside the near
        /// area are left out; it meets them on shared grid lines). Cells entirely under
        /// water are skipped.
        /// </summary>
        public static void FarTerrain(MeshBuilder mb, TerrainField field, float centreX, float centreZ, float extent)
        {
            float cell = TerrainField.FarCell;
            float x0 = field.MinX - Mathf.Ceil((field.MinX - (centreX - extent)) / cell) * cell;
            float z0 = field.MinZ - Mathf.Ceil((field.MinZ - (centreZ - extent)) / cell) * cell;
            int nx = Mathf.CeilToInt(2f * extent / cell) + 1, nz = nx;
            var h = new float[nx + 1, nz + 1];
            for (int i = 0; i <= nx; i++)
                for (int j = 0; j <= nz; j++)
                    h[i, j] = field.Height(x0 + i * cell, z0 + j * cell);
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < nz; j++)
                {
                    float xa = x0 + i * cell, za = z0 + j * cell;
                    float mx = xa + cell * 0.5f, mz = za + cell * 0.5f;
                    if (field.InNearArea(mx, mz)) continue;
                    if (h[i, j] < -2f && h[i + 1, j] < -2f && h[i, j + 1] < -2f && h[i + 1, j + 1] < -2f) continue;
                    var a = new Vector3(xa, h[i, j], za);
                    var b = new Vector3(xa + cell, h[i + 1, j], za);
                    var cc = new Vector3(xa + cell, h[i + 1, j + 1], za + cell);
                    var d = new Vector3(xa, h[i, j + 1], za + cell);
                    mb.QuadFacing(a, b, cc, d, new Vector2(xa, za) / 40f, new Vector2(xa + cell, za) / 40f,
                        new Vector2(xa + cell, za + cell) / 40f, new Vector2(xa, za + cell) / 40f, Vector3.up);
                }
        }

        /// <summary>Stone quay wall along a shoreline polyline (top +1.6 m, foot under water).</summary>
        public static void QuayWall(MeshBuilder mb, IList<(float east, float north)> shore, bool waterEast)
        {
            for (int i = 0; i < shore.Count - 1; i++)
            {
                var a = new Vector3(shore[i].east, 0f, shore[i].north);
                var b = new Vector3(shore[i + 1].east, 0f, shore[i + 1].north);
                var along = b - a;
                var toEast = Vector3.Cross(Vector3.up, along).normalized;
                if (toEast.x < 0f) toEast = -toEast;
                var outward = waterEast ? toEast : -toEast;
                float len = along.magnitude;
                mb.QuadFacing(Up(a, -2.5f), Up(b, -2.5f), Up(b, 1.6f), Up(a, 1.6f),
                    new Vector2(0f, 0f), new Vector2(len / 4f, 0f), new Vector2(len / 4f, 1f), new Vector2(0f, 1f), outward);
                // Coping stone on top.
                var inland = -outward * 1.2f;
                mb.QuadFacing(Up(a, 1.6f), Up(b, 1.6f), Up(b + inland, 1.6f), Up(a + inland, 1.6f),
                    Vector2.zero, new Vector2(len / 4f, 0f), new Vector2(len / 4f, 0.3f), new Vector2(0f, 0.3f), Vector3.up);
            }
        }

        /// <summary>Tree at the origin, 1 m tall (scaled per instance): trunk and crown builders.</summary>
        public static void TreeMesh(int kind, MeshBuilder trunk, MeshBuilder crown)
        {
            switch (kind)
            {
                case 1:
                    // Umbrella (stone) pine: bare trunk, flat wide crown.
                    trunk.Tube(Vector3.zero, new Vector3(0.03f, 0.72f, 0f), 0.035f, 5, false);
                    LandmarkGeometry.VerticalLathe(crown, new Vector3(0.03f, 0.62f, 0f), new[]
                    {
                        new Vector2(0f, 0.02f), new Vector2(0.06f, 0.33f), new Vector2(0.2f, 0.36f), new Vector2(0.32f, 0.2f), new Vector2(0.35f, 0.01f),
                    }, 7);
                    break;
                case 2:
                    // Cypress: narrow spindle.
                    trunk.Tube(Vector3.zero, new Vector3(0f, 0.15f, 0f), 0.03f, 5, false);
                    LandmarkGeometry.VerticalLathe(crown, new Vector3(0f, 0.08f, 0f), new[]
                    {
                        new Vector2(0f, 0.05f), new Vector2(0.15f, 0.12f), new Vector2(0.45f, 0.11f), new Vector2(0.92f, 0.01f),
                    }, 6);
                    break;
                default:
                    // Plane tree: trunk with a broad rounded crown.
                    trunk.Tube(Vector3.zero, new Vector3(0f, 0.5f, 0f), 0.045f, 5, false);
                    LandmarkGeometry.VerticalLathe(crown, new Vector3(0f, 0.32f, 0f), new[]
                    {
                        new Vector2(0f, 0.05f), new Vector2(0.1f, 0.3f), new Vector2(0.3f, 0.38f), new Vector2(0.5f, 0.3f), new Vector2(0.66f, 0.12f), new Vector2(0.68f, 0.01f),
                    }, 7);
                    break;
            }
        }

        /// <summary>Spectator, 1 m tall at the origin facing +z: shirt (body) and skin (head, arms).</summary>
        public static void SpectatorMesh(MeshBuilder shirt, MeshBuilder skin)
        {
            shirt.Box(new Vector3(0f, 0.27f, 0f), new Vector3(0.2f, 0.54f, 0.14f));
            shirt.Box(new Vector3(0f, 0.66f, 0f), new Vector3(0.26f, 0.26f, 0.15f));
            skin.Box(new Vector3(0f, 0.88f, 0.01f), new Vector3(0.12f, 0.15f, 0.13f));
            skin.Box(new Vector3(-0.16f, 0.62f, 0.02f), new Vector3(0.06f, 0.3f, 0.07f));
            skin.Box(new Vector3(0.16f, 0.62f, 0.02f), new Vector3(0.06f, 0.3f, 0.07f));
        }

        /// <summary>Gull, 1 m wingspan at the origin heading +z; wings form a shallow V (flap by scaling y).</summary>
        public static void GullMesh(MeshBuilder mb)
        {
            mb.Box(new Vector3(0f, 0f, 0f), new Vector3(0.09f, 0.08f, 0.38f));
            foreach (float s in new[] { -1f, 1f })
            {
                var root = new Vector3(s * 0.04f, 0.01f, 0.05f);
                var rootB = new Vector3(s * 0.04f, 0.01f, -0.08f);
                var tip = new Vector3(s * 0.5f, 0.12f, -0.06f);
                mb.TriangleFacing(root, rootB, tip, Vector2.zero, Vector2.right, Vector2.up, Vector3.up);
                mb.TriangleFacing(root, rootB, tip, Vector2.zero, Vector2.right, Vector2.up, Vector3.down);
            }
        }

        /// <summary>
        /// Grandstand in a local frame: origin at the front-left corner at ground level,
        /// +x along the stand, +z away from the track. Returns seat positions (local) for spectators.
        /// </summary>
        public static List<Vector3> Grandstand(Parts B, float length, int rows, bool roof)
        {
            const float depth = 0.85f, rise = 0.5f, front = 1.6f;
            var seats = new List<Vector3>();
            float zBack = rows * depth;
            for (int r = 0; r < rows; r++)
            {
                float y = front + r * rise, z = r * depth;
                string mat = (r / 3) % 3 == 1 ? SeatAccent : Seat;
                // Tread (seats) and riser (concrete).
                B(mat).QuadFacing(new Vector3(0f, y, z), new Vector3(length, y, z), new Vector3(length, y, z + depth), new Vector3(0f, y, z + depth),
                    Vector2.zero, new Vector2(length / 2f, 0f), new Vector2(length / 2f, 1f), Vector2.up, Vector3.up);
                B(Concrete).QuadFacing(new Vector3(0f, y - rise, z), new Vector3(length, y - rise, z), new Vector3(length, y, z), new Vector3(0f, y, z),
                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector3.back);
                for (float x = 0.45f; x < length - 0.3f; x += 0.62f) seats.Add(new Vector3(x, y, z + depth * 0.45f));
            }
            float top = front + rows * rise;
            // Front wall, back wall and side walls (closed so the stand reads from behind too).
            B(Concrete).Box(new Vector3(length * 0.5f, front * 0.5f - 0.5f, -0.1f), new Vector3(length, front + 1f, 0.2f));
            B(Concrete).Box(new Vector3(length * 0.5f, top * 0.5f, zBack + 0.15f), new Vector3(length, top + 1.2f, 0.3f));
            foreach (float x in new[] { -0.15f, length + 0.15f })
                B(Concrete).Box(new Vector3(x, top * 0.5f, zBack * 0.5f), new Vector3(0.3f, top + 1.2f, zBack + 0.4f));
            if (roof)
            {
                for (float x = 0f; x <= length + 0.01f; x += Mathf.Max(8f, length / Mathf.Max(1, Mathf.Round(length / 12f))))
                    B(Post).Box(new Vector3(x, top + 2.4f, zBack), new Vector3(0.35f, 4.8f, 0.35f));
                var p0 = new Vector3(-0.5f, top + 5.2f, -1.5f);
                var p1 = new Vector3(length + 0.5f, top + 5.2f, -1.5f);
                var p2 = new Vector3(length + 0.5f, top + 4.6f, zBack + 0.8f);
                var p3 = new Vector3(-0.5f, top + 4.6f, zBack + 0.8f);
                B(Canopy).QuadFacing(p0, p1, p2, p3, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector3.up);
                B(Canopy).QuadFacing(p0, p1, p2, p3, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector3.down);
            }
            return seats;
        }

        /// <summary>Paddock hospitality unit (local frame as grandstand): glass storeys, slabs, roof terrace.</summary>
        public static void Hospitality(Parts B, float length, float depth, int storeys)
        {
            const float h = 3.6f;
            for (int i = 0; i < storeys; i++)
            {
                float y = i * h;
                B(TowerGlass).Box(new Vector3(length * 0.5f, y + h * 0.5f, depth * 0.5f), new Vector3(length - 0.4f, h - 0.4f, depth - 0.4f));
                B(Concrete).Box(new Vector3(length * 0.5f, y + h - 0.15f, depth * 0.5f - 0.6f), new Vector3(length + 0.2f, 0.3f, depth + 1.4f));
            }
            // Roof terrace railing.
            float top = storeys * h;
            B(Post).Box(new Vector3(length * 0.5f, top + 0.55f, -0.75f), new Vector3(length, 0.08f, 0.08f));
            for (float x = 0f; x <= length; x += 2f) B(Post).Box(new Vector3(x, top + 0.3f, -0.75f), new Vector3(0.06f, 0.6f, 0.06f));
        }

        /// <summary>Catch fence segment between two base points (alpha-cut mesh panel plus a post at a).</summary>
        public static void FenceSegment(Parts B, Vector3 a, Vector3 b, float height, Vector3 trackSide)
        {
            float len = (b - a).magnitude;
            B(CatchFence).QuadFacing(a, b, Up(b, height), Up(a, height), Vector2.zero, new Vector2(len / 2f, 0f), new Vector2(len / 2f, height / 2f), new Vector2(0f, height / 2f), trackSide);
            B(CatchFence).QuadFacing(a, b, Up(b, height), Up(a, height), Vector2.zero, new Vector2(len / 2f, 0f), new Vector2(len / 2f, height / 2f), new Vector2(0f, height / 2f), -trackSide);
            var dir = (b - a) / Mathf.Max(len, 1e-3f);
            var side = Vector3.Cross(Vector3.up, dir) * 0.06f;
            dir *= 0.06f;
            var c = new[] { a - dir - side, a + dir - side, a + dir + side, a - dir + side };
            for (int i = 0; i < 4; i++)
            {
                var p = c[i]; var q = c[(i + 1) % 4];
                B(Post).QuadFacing(p, q, Up(q, height + 0.3f), Up(p, height + 0.3f), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector3.Cross(q - p, Vector3.up) * -1f);
            }
        }

        /// <summary>Street lamp at a base point, arm reaching towards `towards` (horizontal).</summary>
        /// <summary>Centre of the lamp head's light-emitting underside (same geometry as <see cref="Lamp"/>).</summary>
        public static Vector3 LampHeadPosition(Vector3 basePoint, Vector3 towards, float height)
        {
            var dir = new Vector3(towards.x, 0f, towards.z).normalized;
            return Up(basePoint, height) + dir * 1.8f + Vector3.down * 0.19f;
        }

        public static void Lamp(Parts B, Vector3 basePoint, Vector3 towards, float height)
        {
            var dir = new Vector3(towards.x, 0f, towards.z).normalized;
            B(Post).Tube(basePoint, Up(basePoint, height), 0.09f, 5, false);
            var armEnd = Up(basePoint, height) + dir * 1.8f;
            B(Post).Tube(Up(basePoint, height - 0.1f), armEnd, 0.05f, 4, false);
            B(LampHead).Box(armEnd + Vector3.down * 0.12f, new Vector3(0.5f, 0.14f, 0.5f), Quaternion.LookRotation(dir));
        }
    }
}
