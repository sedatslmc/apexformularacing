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
        public const string BridgeRed = "BridgeRed", BridgeWhite = "BridgeWhite";

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
        /// A real building from its OSM footprint (<see cref="OsmCity"/>): facade walls along every edge (the
        /// facade texture runs continuously around the building, shopfronts at street level), a cornice band,
        /// and either a flat roof with a parapet, water tanks and a stair head (most Istanbul apartment blocks)
        /// or, for small four-cornered houses, a tiled hip roof. `detailed` = false gives the LOD1 mass.
        /// </summary>
        public static void FootprintBuilding(Parts B, OsmCity.Building b, bool detailed)
        {
            var o = b.outline;
            int n = o.Count;
            float y0 = b.ground, y1 = b.ground + 0.6f + b.height;
            float tileH = CityLayout.FloorHeight * TileBays, tileW = BayWidth * TileBays;
            string wall = Facade(b.style);
            Vector3 W(int i, float y) => new Vector3(o[i % n].X, y, o[i % n].Y);
            float u = (o[0].X * 0.37f + o[0].Y * 0.11f) % tileW;
            for (int i = 0; i < n; i++)
            {
                var a = W(i, 0f); var c = W(i + 1, 0f);
                var edge = c - a;
                float len = edge.magnitude;
                if (len < 0.05f) continue;
                // The texture's shop row starts at this facade's own street level (OsmCity.Building.level).
                float street = b.level != null ? b.level[i] : y0 + 0.6f;
                float vb = (y0 - street) / tileH, vt = (y1 - street) / tileH;
                // Counter-clockwise footprint (x right, z up): the outside is to the right of the edge.
                var outward = new Vector3(edge.z, 0f, -edge.x) / len;
                B(wall).QuadFacing(Up(a, y0), Up(c, y0), Up(c, y1), Up(a, y1),
                    new Vector2(u / tileW, vb), new Vector2((u + len) / tileW, vb), new Vector2((u + len) / tileW, vt), new Vector2(u / tileW, vt), outward);
                u += len;
                if (!detailed) continue;
                // Cornice: a 0.35 m ledge projecting 0.3 m just under the roof line.
                var off = outward * 0.3f;
                string trim = b.historic ? LandmarkGeometry.Stone : Concrete;
                B(trim).QuadFacing(Up(a, y1 - 0.35f) + off, Up(c, y1 - 0.35f) + off, Up(c, y1) + off, Up(a, y1) + off, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, outward);
                B(trim).QuadFacing(Up(a, y1), Up(c, y1), Up(c, y1) + off, Up(a, y1) + off, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector3.up);
                B(trim).QuadFacing(Up(a, y1 - 0.35f), Up(c, y1 - 0.35f), Up(c, y1 - 0.35f) + off, Up(a, y1 - 0.35f) + off, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector3.down);
            }
            if (b.roof == 1 && n == 4)
            {
                HipRoof(B, o, y1);
                return;
            }
            // Flat roof (triangulated footprint) with a 0.9 m parapet.
            var tris = Polygon.Triangulate(o);
            var roof = B(RoofFlat);
            for (int t = 0; t + 2 < tris.Count; t += 3)
                roof.TriangleFacing(W(tris[t], y1), W(tris[t + 1], y1), W(tris[t + 2], y1),
                    new Vector2(o[tris[t]].X, o[tris[t]].Y) / 6f, new Vector2(o[tris[t + 1]].X, o[tris[t + 1]].Y) / 6f, new Vector2(o[tris[t + 2]].X, o[tris[t + 2]].Y) / 6f, Vector3.up);
            if (!detailed) return;
            for (int i = 0; i < n; i++)
            {
                var a = W(i, y1); var c = W(i + 1, y1);
                var edge = c - a;
                if (edge.sqrMagnitude < 0.01f) continue;
                var inward = new Vector3(-edge.z, 0f, edge.x).normalized;
                B(Concrete).QuadFacing(a, c, c + Vector3.up * 0.9f, a + Vector3.up * 0.9f, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, inward);
            }
            // Water tanks and a stair head on larger roofs (positions from the footprint, deterministic).
            float area = Polygon.SignedArea(o);
            if (area > 120f)
            {
                var ctr = System.Numerics.Vector2.Zero;
                foreach (var p in o) ctr += p;
                ctr /= n;
                var e0 = o[1] - o[0];
                float yaw = Mathf.Atan2(e0.X, e0.Y) * Mathf.Rad2Deg;
                var q = Quaternion.Euler(0f, yaw, 0f);
                var dir = new Vector3(e0.X, 0f, e0.Y).normalized;
                var side = new Vector3(dir.z, 0f, -dir.x);
                var c3 = new Vector3(ctr.X, y1, ctr.Y);
                B(RoofFlat).Box(c3 - dir * 2f + Vector3.up * 1.2f, new Vector3(3f, 2.4f, 3f), q);
                B(Tank).Box(c3 + dir * 2.5f + side * 1.5f + Vector3.up * 0.7f, new Vector3(1.6f, 1.4f, 1.1f), q);
                if (area > 400f) B(Tank).Box(c3 + dir * 4.5f - side * 1.8f + Vector3.up * 0.7f, new Vector3(1.6f, 1.4f, 1.1f), q);
            }
        }

        // --- Street fronts of the real city: balconies, bay windows (cumba), shop awnings, AC units, flags ---

        public static string FacadeSolid(int style) => "FacadeSolid" + style;
        public static string Awning(int i) => "Awning" + i;
        public const int Awnings = 4;
        public const string BalconyFlag = "BalconyFlag";

        static int Hash(int a, int b)
        {
            uint q = (uint)(a * 73856093 ^ b * 19349663);
            q = (q ^ (q >> 13)) * 1274126177u;
            return (int)((q ^ (q >> 16)) & 0x7fffffff);
        }

        static float Mod(float a, float m) => a - m * Mathf.Floor(a / m);

        /// <summary>
        /// Modelled relief on the street fronts of an OSM building (<see cref="OsmCity.Building.front"/>), aligned with
        /// the window bays of the facade texture (3.5 m bays, <see cref="CityLayout.FloorHeight"/> storeys):
        /// balconies with metal railings or solid parapets on apartment blocks, a corbelled bay window (cumba) on
        /// older low-rise houses, canvas awnings over the shops. level 2 adds air-conditioning units beside some
        /// windows and a Turkish flag on a short pole at some balconies. level 0: nothing (mobile).
        /// </summary>
        public static void FacadeOrnaments(Parts B, OsmCity.Building b, int level)
        {
            if (level <= 0 || b.front == null || b.classical != OsmCity.NotClassical || b.worship) return;
            var o = b.outline;
            int n = o.Count;
            float fh = CityLayout.FloorHeight, tileW = BayWidth * TileBays, roofY = b.ground + 0.6f + b.height;
            if (Mathf.FloorToInt((b.height - 0.6f) / fh) < 2) return;
            int seed = Hash((int)(o[0].X * 7.1f), (int)(o[0].Y * 3.3f));
            int kind = seed % 10;
            // The building family (BuildingFamilies) decides the relief: timber houses have a bay window and timber
            // bands, old masonry string courses and some iron balconies, the post-war blocks continuous balconies on
            // every floor, contemporary buildings glass balustrades and fins, office and shop buildings neither.
            var fam = b.family;
            bool ribbons = fam == BuildingFamilies.Family.Apartment1970s || fam == BuildingFamilies.Family.Contemporary;
            bool glassRail = fam == BuildingFamilies.Family.Contemporary;
            bool balconies = fam == BuildingFamilies.Family.Masonry19th ? kind < 3 && !b.historic : false;
            bool cumba = b.height <= 16f && (fam == BuildingFamilies.Family.OttomanTimber || (fam == BuildingFamilies.Family.Masonry19th && (kind == 4 || kind == 5 || b.historic)));
            bool metalRail = (seed >> 5) % 2 == 0;
            int awning = (seed >> 7) % Awnings;
            string solid = FacadeSolid(b.style);
            // Ortaköy reference section: entrances and window sills (elsewhere from level 2 on the near relief).
            bool detail = b.ortakoy || level >= 3;
            float u = (o[0].X * 0.37f + o[0].Y * 0.11f) % tileW;   // same running u as FootprintBuilding
            for (int i = 0; i < n; i++)
            {
                var a = new Vector3(o[i].X, 0f, o[i].Y);
                var c = new Vector3(o[(i + 1) % n].X, 0f, o[(i + 1) % n].Y);
                var edge = c - a;
                float len = edge.magnitude;
                if (len < 0.05f) continue;
                float uEdge = u;
                u += len;
                if (!b.front[i] || len < 6f) continue;
                // Storeys of this facade from its own street level (on a slope the uphill side has fewer).
                float y0 = b.level != null ? b.level[i] : b.ground + 0.6f;
                int floors = Mathf.FloorToInt((roofY - y0 - 0.6f) / fh);
                if (floors < 2) continue;
                var dir = edge / len;
                var outward = new Vector3(edge.z, 0f, -edge.x) / len;
                var q = Quaternion.LookRotation(outward);
                Vector3 P(float along, float y, float outOff) => a + dir * along + outward * outOff + Vector3.up * y;
                // Bay centres: where the facade texture has its windows (u mod 3.5 = 1.75).
                var bays = new List<float>();
                for (float t = Mod(BayWidth * 0.5f - uEdge, BayWidth); t <= len - 1.6f; t += BayWidth)
                    if (t >= 1.6f) bays.Add(t);
                if (bays.Count == 0) continue;
                int mid = bays.Count / 2;
                for (int k = 0; k < bays.Count; k++)
                {
                    float t = bays[k];
                    int h = Hash(seed + i * 131, k);
                    // Shops: awning, sign board with a Turkish trade name and the box of the rolled-up shutter over
                    // the ground-floor bays of shop buildings and some other street fronts (most in Ortaköy).
                    bool shopBay = StreetFronts.IsShopBay(seed, i, k, b.shop, b.ortakoy) && !(cumba && k == mid && bays.Count >= 2);
                    if (shopBay)
                    {
                        B(Awning((awning + StreetFronts.Hash(seed + i, k / 2)) % Awnings)).Box(P(t, y0 + 2.6f, 0.62f), new Vector3(3f, 0.06f, 1.35f), q * Quaternion.Euler(18f, 0f, 0f));
                        ShopSign(B, P, dir, outward, t, y0 + 3.3f, StreetFronts.SignFor(seed, i, k, b.ortakoy));
                        if (detail) B(LandmarkGeometry.Lead).Box(P(t, y0 + 2.42f, 0.14f), new Vector3(3.0f, 0.26f, 0.26f), q);   // shutter box
                    }
                    else if (detail && k == mid)
                    {
                        // Entrance of the house: panelled door in a stone surround under a small canopy.
                        B(solid).Box(P(t, y0 + 1.2f, 0.04f), new Vector3(1.6f, 2.55f, 0.08f), q);
                        B(Door).QuadFacing(P(t - 0.6f, y0 + 0.02f, 0.09f), P(t + 0.6f, y0 + 0.02f, 0.09f), P(t + 0.6f, y0 + 2.3f, 0.09f), P(t - 0.6f, y0 + 2.3f, 0.09f),
                            Vector2.zero, Vector2.right, Vector2.one, Vector2.up, outward);
                        B(Concrete).Box(P(t, y0 + 2.62f, 0.45f), new Vector3(1.9f, 0.09f, 0.9f), q);
                    }
                    if (cumba && k == mid && bays.Count >= 2)
                    {
                        float top = y0 + floors * fh - 0.3f, bottom = y0 + fh;
                        B(solid).Box(P(t, (top + bottom) * 0.5f, 0.45f), new Vector3(3.2f, top - bottom, 0.9f), q);
                        B(solid).Box(P(t, bottom - 0.2f, 0.3f), new Vector3(2.8f, 0.4f, 0.6f), q);           // corbel
                        B(LandmarkGeometry.Lead).Box(P(t, top + 0.08f, 0.5f), new Vector3(3.4f, 0.16f, 1.05f), q);
                        for (int f = 1; f < floors; f++)
                        {
                            float fb = y0 + f * fh;
                            B(Glass).Box(P(t, fb + 1.65f, 0.91f), new Vector3(2.3f, 1.5f, 0.04f), q);
                            foreach (float sx in new[] { -1.61f, 1.61f })
                                B(Glass).Box(P(t + sx, fb + 1.65f, 0.45f), new Vector3(0.04f, 1.4f, 0.5f), q);
                        }
                        continue;
                    }
                    for (int f = 1; f < floors; f++)
                    {
                        if (f % TileBays == 0) continue;   // the texture repeats its shop row every 8 storeys
                        float fb = y0 + f * fh;
                        int hf = Hash(h, f);
                        bool balcony = balconies && hf % 10 < 5;
                        if (balcony)
                        {
                            B(Concrete).Box(P(t, fb + 0.02f, 0.55f), new Vector3(2.7f, 0.16f, 1.1f), q);
                            string rail = metalRail ? LandmarkGeometry.Lead : solid;
                            B(rail).Box(P(t, fb + 0.6f, 1.08f), new Vector3(2.7f, 1f, metalRail ? 0.04f : 0.1f), q);
                            // Side railings as single outward-facing quads (seen from the street only).
                            foreach (float sx in new[] { -1.35f, 1.35f })
                                B(rail).QuadFacing(P(t + sx, fb + 0.1f, 0f), P(t + sx, fb + 0.1f, 1.08f), P(t + sx, fb + 1.1f, 1.08f), P(t + sx, fb + 1.1f, 0f),
                                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up, dir * sx);
                            if (level >= 2 && hf % 23 == 0)
                            {
                                // Turkish flag on a short pole from the railing.
                                var p0 = P(t + 1.15f, fb + 1.1f, 1.08f);
                                var p1 = p0 + outward * 0.85f + Vector3.up * 0.85f;
                                B(LandmarkGeometry.Lead).Tube(p0, p1, 0.02f, 4, true);
                                var fa = Vector3.Lerp(p0, p1, 0.15f);
                                B(BalconyFlag).QuadFacing(fa, p1, p1 - Vector3.up * 0.75f, fa - Vector3.up * 0.75f,
                                    new Vector2(0f, 1f), Vector2.one, Vector2.right, Vector2.zero, dir);
                            }
                        }
                        if (level >= 2 && hf % 20 == 1)
                            B(Tank).Box(P(t + 1.25f, fb + (balcony ? 1.6f : 0.55f), balcony ? 0.2f : 0.22f), new Vector3(0.8f, 0.55f, 0.32f), q);
                        // Stone window sill under the painted one of the facade texture (sill line 0.58-0.78 m).
                        if (detail && !balcony)
                            B(solid).Box(P(t, fb + 0.74f, 0.09f), new Vector3(1.7f, 0.08f, 0.18f), q);
                    }
                }
                FamilyRelief(B, b, fam, P, dir, outward, q, len, y0, floors, fh, ribbons, glassRail, level, solid);
                // Turkish flags hung vertically from a balcony railing or under a window (crescent and star at the top).
                if (StreetFronts.FlagFront(seed, i, b.ortakoy, b.raceFront != null && b.raceFront[i]))
                {
                    int count = 1 + StreetFronts.Hash(seed, i + 3) % 2;
                    for (int f2 = 0; f2 < count; f2++)
                    {
                        var (bay, floor) = StreetFronts.FlagSpot(seed, i, f2, bays.Count, floors);
                        if (floor >= floors || floor % TileBays == 0 || (cumba && bay == mid && bays.Count >= 2)) continue;
                        float t = bays[bay], fb = y0 + floor * fh;
                        bool onBalcony = ribbons || (balconies && Hash(Hash(seed + i * 131, bay), floor) % 10 < 5);
                        // A window flag on the first floor would hang in front of a shop sign.
                        if (!onBalcony && floor == 1 && StreetFronts.IsShopBay(seed, i, bay, b.shop, b.ortakoy)) continue;
                        HangingFlag(B, P, dir, outward, t, onBalcony ? fb + 1.08f : fb + 0.7f, onBalcony ? 1.12f : 0.2f, !onBalcony,
                            Mathf.Min(StreetFronts.FlagScale(seed, i, f2), (fh - 0.3f) / 1.35f));
                    }
                }
                // Rain downpipes at both ends of the street front (zinc), from the eaves to the pavement.
                foreach (float tp in new[] { 0.3f, len - 0.3f })
                    B(LandmarkGeometry.Lead).Tube(P(tp, y0, 0.09f), P(tp, roofY - 0.15f, 0.09f), 0.05f, 5, true);
                bool flatRoof = !(b.roof == 1 && n == 4);
                if (level >= 2 && flatRoof)
                {
                    // Satellite dishes on the parapet of many apartment blocks: none, one or two per street front.
                    int dishes = Hash(seed, i + 41) % 3;
                    for (int k = 0; k < dishes; k++)
                        SatelliteDish(B, P(len * (0.3f + 0.4f * k), roofY + 0.9f, -0.35f), outward, Hash(seed + k, i) % 360);
                    // A roof terrace with a canvas shade on some blocks (once per building, at its first street front).
                    if (Hash(seed, 77) % 4 == 0 && i == FirstFront(b) && len > 8f)
                    {
                        var c0 = P(len * 0.5f, roofY + 0.05f, -2.2f);
                        foreach (float sx in new[] { -1.4f, 1.4f })
                            foreach (float sz in new[] { -1.1f, 1.1f })
                                B(LandmarkGeometry.Lead).Tube(c0 + dir * sx + outward * sz, c0 + dir * sx + outward * sz + Vector3.up * 2.3f, 0.03f, 4, true);
                        B(Awning(awning)).Box(c0 + Vector3.up * 2.35f, new Vector3(3.2f, 0.04f, 2.6f), q);
                        B(LandmarkGeometry.Lead).Box(c0 + Vector3.up * 0.45f, new Vector3(1.2f, 0.05f, 0.8f), q);   // table
                    }
                }
            }
        }

        /// <summary>
        /// Family relief along a whole street front: continuous balcony slabs with railings (post-war blocks: concrete
        /// slab and painted metal or solid parapet; contemporary: glass balustrade, slim slabs, vertical fins), string
        /// courses and a stone plinth on 19th-century masonry, weatherboard bands and wide eaves on timber houses.
        /// </summary>
        static void FamilyRelief(Parts B, OsmCity.Building b, BuildingFamilies.Family fam, System.Func<float, float, float, Vector3> P,
            Vector3 dir, Vector3 outward, Quaternion q, float len, float y0, int floors, float fh, bool ribbons, bool glassRail, int level, string solid)
        {
            float x0 = 0.6f, x1 = len - 0.6f, w = x1 - x0, mid = (x0 + x1) * 0.5f;
            if (w < 3f) return;
            if (ribbons)
            {
                float depth = glassRail ? 1.4f : 1.2f;
                for (int f = 1; f < floors; f++)
                {
                    float fb = y0 + f * fh;
                    B(Concrete).Box(P(mid, fb + 0.02f, depth * 0.5f), new Vector3(w, glassRail ? 0.12f : 0.18f, depth), q);
                    if (glassRail) B(Glass).Box(P(mid, fb + 0.6f, depth - 0.02f), new Vector3(w, 1.0f, 0.03f), q);
                    else B((Hash(f, (int)(len * 7f)) % 3 == 0) ? solid : LandmarkGeometry.Lead).Box(P(mid, fb + 0.55f, depth - 0.03f), new Vector3(w, 0.95f, 0.05f), q);
                }
                if (glassRail && level >= 2)
                    for (float t = x0; t <= x1 + 0.01f; t += 3.5f)
                        B(TrackKitDark).Box(P(t, y0 + floors * fh * 0.5f + 0.3f, depth * 0.5f), new Vector3(0.18f, floors * fh - 0.6f, depth), q);
                return;
            }
            if (fam == BuildingFamilies.Family.Masonry19th)
            {
                // Rusticated plinth to the first floor and a string course at every floor line.
                B(LandmarkGeometry.Stone).Box(P(mid, y0 + fh * 0.5f, 0.06f), new Vector3(len, fh, 0.12f), q);
                for (int f = 1; f < floors; f++)
                    B(LandmarkGeometry.Stone).Box(P(mid, y0 + f * fh, 0.1f), new Vector3(len, 0.16f, 0.2f), q);
                return;
            }
            if (fam == BuildingFamilies.Family.OttomanTimber)
            {
                // Weatherboard bands between the storeys and corner boards.
                for (int f = 1; f <= floors; f++)
                    B(Wood).Box(P(mid, y0 + f * fh - 0.1f, 0.08f), new Vector3(len, 0.22f, 0.12f), q);
                foreach (float t in new[] { 0.08f, len - 0.08f })
                    B(Wood).Box(P(t, y0 + floors * fh * 0.5f, 0.07f), new Vector3(0.16f, floors * fh, 0.14f), q);
            }
        }

        const string TrackKitDark = "KitDark";

        static int FirstFront(OsmCity.Building b)
        {
            for (int i = 0; i < b.front.Length; i++) if (b.front[i]) return i;
            return -1;
        }

        /// <summary>Satellite dish (0.8 m) on a short arm, tilted up towards the southern sky and turned a little per dish.</summary>
        static void SatelliteDish(Parts B, Vector3 foot, Vector3 outward, int turnDeg)
        {
            var dish = new MeshBuilder();
            LandmarkGeometry.VerticalLathe(dish, Vector3.zero, new[] { new Vector2(0f, 0.4f), new Vector2(0.06f, 0.3f), new Vector2(0.12f, 0.001f) }, 10);
            var rot = Quaternion.LookRotation(Quaternion.Euler(0f, turnDeg % 60 - 30, 0f) * outward, Vector3.up) * Quaternion.Euler(-55f, 0f, 0f);
            var head = foot + Vector3.up * 0.55f;
            B(LandmarkGeometry.Marble).Append(dish, v => head + rot * new Vector3(v.x, v.z, v.y));
            B(LandmarkGeometry.Lead).Tube(foot, head, 0.025f, 4, true);
        }

        public const string ShopSignAtlas = "ShopSign", Door = "Door";

        /// <summary>Sign board over a shop: a dark frame and the atlas cell of the sign on its face.</summary>
        static void ShopSign(Parts B, System.Func<float, float, float, Vector3> P, Vector3 dir, Vector3 outward, float t, float y, int sign)
        {
            float w = StreetFronts.BoardWidth, h = StreetFronts.BoardHeight;
            B(LandmarkGeometry.Lead).Box(P(t, y, 0.05f), new Vector3(w + 0.08f, h + 0.08f, 0.1f), Quaternion.LookRotation(outward));
            var (u0, v0, u1, v1) = StreetFronts.Uv(sign);
            B(ShopSignAtlas).QuadFacing(P(t - w * 0.5f, y - h * 0.5f, 0.105f), P(t + w * 0.5f, y - h * 0.5f, 0.105f), P(t + w * 0.5f, y + h * 0.5f, 0.105f), P(t - w * 0.5f, y + h * 0.5f, 0.105f),
                new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1), outward);
        }

        /// <summary>
        /// Turkish flag hung vertically (G = 0.9 m wide, 1.35 m long): the hoist with the crescent and star at the top,
        /// the cloth in three slightly bulging strips; window flags hang from a short rod.
        /// </summary>
        static void HangingFlag(Parts B, System.Func<float, float, float, Vector3> P, Vector3 dir, Vector3 outward, float t, float top, float off, bool rod, float scale = 1f)
        {
            float g = 0.9f * scale, len = 1.35f * scale;
            float[] bulge = { 0f, 0.05f, 0.015f, 0.06f };
            for (int s = 0; s < 3; s++)
            {
                float ya = top - len * s / 3f, yb = top - len * (s + 1) / 3f;
                float ua = s / 3f, ub = (s + 1) / 3f;   // texture u runs from the hoist (top) along the flag
                B(BalconyFlag).QuadFacing(P(t - g * 0.5f, yb, off + bulge[s + 1]), P(t + g * 0.5f, yb, off + bulge[s + 1]), P(t + g * 0.5f, ya, off + bulge[s]), P(t - g * 0.5f, ya, off + bulge[s]),
                    new Vector2(ub, 0f), new Vector2(ub, 1f), new Vector2(ua, 1f), new Vector2(ua, 0f), outward);
            }
            if (rod) B(LandmarkGeometry.Lead).Tube(P(t - g * 0.6f, top + 0.02f, off), P(t + g * 0.6f, top + 0.02f, off), 0.015f, 4, true);
        }

        /// <summary>Tiled hip roof over a four-cornered footprint (ridge along the longer pair of sides).</summary>
        static void HipRoof(Parts B, List<System.Numerics.Vector2> o, float y1)
        {
            var e = new Vector3[4];
            for (int i = 0; i < 4; i++) e[i] = new Vector3(o[i].X, y1, o[i].Y);
            var ctr = (e[0] + e[1] + e[2] + e[3]) * 0.25f;
            float l01 = (e[1] - e[0]).magnitude + (e[3] - e[2]).magnitude, l12 = (e[2] - e[1]).magnitude + (e[0] - e[3]).magnitude;
            // Rotate so that edges 0-1 and 2-3 are the long sides.
            if (l12 > l01) e = new[] { e[1], e[2], e[3], e[0] };
            float halfLong = ((e[1] - e[0]).magnitude + (e[3] - e[2]).magnitude) * 0.25f;
            float halfShort = ((e[2] - e[1]).magnitude + (e[0] - e[3]).magnitude) * 0.25f;
            var along = ((e[1] - e[0]) + (e[2] - e[3])).normalized;
            float r = Mathf.Max(0f, halfLong - halfShort);
            float rise = halfShort * 1.1f * 0.5f;
            var ra = ctr - along * r + Vector3.up * rise;
            var rb = ctr + along * r + Vector3.up * rise;
            Vector3 Out(Vector3 p) => (p - ctr) + Vector3.up * 2f;
            B(RoofTile).QuadFacing(e[0], e[1], rb, ra, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.75f, 1), new Vector2(0.25f, 1), Out((e[0] + e[1]) * 0.5f));
            B(RoofTile).QuadFacing(e[2], e[3], ra, rb, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.75f, 1), new Vector2(0.25f, 1), Out((e[2] + e[3]) * 0.5f));
            B(RoofTile).TriangleFacing(e[1], e[2], rb, Vector2.zero, Vector2.right, new Vector2(0.5f, 1f), Out((e[1] + e[2]) * 0.5f));
            B(RoofTile).TriangleFacing(e[3], e[0], ra, Vector2.zero, Vector2.right, new Vector2(0.5f, 1f), Out((e[3] + e[0]) * 0.5f));
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

        /// <summary>
        /// The Bosphorus edge from <see cref="Waterfront"/>: stone quay walls from 3 m below the water to the quay top
        /// with a coping stone; on the promenade sections paving (world UVs, 3 m pavers), the inland kerb face, an
        /// iron railing along the water, mooring bollards and promenade lamps (lamp heads glow at night).
        /// level 0 (mobile): walls, coping and paving only.
        /// </summary>
        public static void WaterfrontEdge(Parts B, Waterfront wf, int level)
        {
            foreach (var s in wf.segments)
            {
                var a = new Vector3(s.a.X, 0f, s.a.Y);
                var b = new Vector3(s.b.X, 0f, s.b.Y);
                var inl = new Vector3(s.inland.X, 0f, s.inland.Y);
                float len = (b - a).magnitude, t = s.top;
                B(Quay).QuadFacing(Up(a, -3f), Up(b, -3f), Up(b, t - 0.25f), Up(a, t - 0.25f),
                    new Vector2(0f, 0f), new Vector2(len / 4f, 0f), new Vector2(len / 4f, 1f), new Vector2(0f, 1f), -inl);
                var o = -inl * 0.08f;
                B(LandmarkGeometry.Stone).QuadFacing(Up(a, t - 0.25f) + o, Up(b, t - 0.25f) + o, Up(b, t + 0.05f) + o, Up(a, t + 0.05f) + o,
                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up, -inl);
                B(LandmarkGeometry.Stone).QuadFacing(Up(a, t + 0.05f) + o, Up(b, t + 0.05f) + o, Up(b + inl * 0.6f, t + 0.05f), Up(a + inl * 0.6f, t + 0.05f),
                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector3.up);
                if (s.palace)
                {
                    PalaceRailing(B, s, wf.openings, level);
                    continue;
                }
                if (!s.promenade) continue;
                float w = Waterfront.PromenadeWidth;
                Vector2 Uv(Vector3 p) => new Vector2(p.x, p.z) / 3f;
                Vector3 p0 = Up(a + inl * 0.6f, t + 0.03f), p1 = Up(b + inl * 0.6f, t + 0.03f), p2 = Up(b + inl * w, t + 0.03f), p3 = Up(a + inl * w, t + 0.03f);
                B(Pavement).QuadFacing(p0, p1, p2, p3, Uv(p0), Uv(p1), Uv(p2), Uv(p3), Vector3.up);
                B(Concrete).QuadFacing(Up(a + inl * w, t - 1.5f), Up(b + inl * w, t - 1.5f), p2, p3, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, inl);
                if (level <= 0) continue;
                // Railing along the water: two rails and a post per 6 m piece.
                var r0 = a + inl * 0.3f; var r1 = b + inl * 0.3f;
                B(LandmarkGeometry.Lead).Tube(Up(r0, t + 1.05f), Up(r1, t + 1.05f), 0.035f, 5, false);
                B(LandmarkGeometry.Lead).Tube(Up(r0, t + 0.5f), Up(r1, t + 0.5f), 0.02f, 4, false);
                B(LandmarkGeometry.Lead).Box(Up(r0, t + 0.55f), new Vector3(0.06f, 1.1f, 0.06f));
            }
            if (level <= 0) return;
            foreach (var p in wf.bollards)
            {
                var c = new Vector3(p.X, p.Y, p.Z);
                B(LandmarkGeometry.Lead).Tube(c, c + Vector3.up * 0.55f, 0.13f, 8, true);
                B(LandmarkGeometry.Lead).Tube(c + Vector3.up * 0.55f, c + Vector3.up * 0.62f, 0.18f, 8, true);
            }
            foreach (var (foot, sea) in wf.lamps)
            {
                var f = new Vector3(foot.X, foot.Y, foot.Z);
                var s = new Vector3(sea.X, 0f, sea.Y);
                B(DarkSteel).Tube(f, f + Vector3.up * 4.6f, 0.07f, 6, true);
                B(DarkSteel).Tube(f + Vector3.up * 4.5f, f + Vector3.up * 4.5f + s * 0.8f, 0.04f, 4, false);
                B(LampHead).Box(f + Vector3.up * 4.35f + s * 0.8f, new Vector3(0.45f, 0.3f, 0.45f));
            }
        }

        /// <summary>
        /// Dolmabahçe's palace quay: a white marble plinth with piers and an ornamental wrought-iron railing (gilt spear
        /// tips) along the water, broken where the sea gates stand. Level 0: plinth only; 1: piers and rails; 2: bars.
        /// </summary>
        static void PalaceRailing(Parts B, Waterfront.Segment s, List<(System.Numerics.Vector2 at, float half)> openings, int level)
        {
            var inl = new Vector3(s.inland.X, 0f, s.inland.Y);
            float t = s.top;
            foreach (var (t0, t1) in Waterfront.OpenParts(s, openings))
            {
                var a2 = System.Numerics.Vector2.Lerp(s.a, s.b, t0) + s.inland * 0.35f;
                var b2 = System.Numerics.Vector2.Lerp(s.a, s.b, t1) + s.inland * 0.35f;
                var a = new Vector3(a2.X, 0f, a2.Y); var b = new Vector3(b2.X, 0f, b2.Y);
                var along = b - a;
                float len = along.magnitude;
                if (len < 0.2f) continue;
                var dir = along / len;
                var mid = (a + b) * 0.5f;
                var rot = Quaternion.LookRotation(inl, Vector3.up);
                // Plinth (0.55 m) with a moulded top.
                B(LandmarkGeometry.Marble).Box(Up(mid, t + 0.275f), new Vector3(len, 0.55f, 0.45f), rot);
                if (level <= 0) continue;
                B(LandmarkGeometry.Marble).Box(Up(mid, t + 0.58f), new Vector3(len, 0.08f, 0.55f), rot);
                // Pier at the start of each run, with cap and ball finial.
                B(LandmarkGeometry.Marble).Box(Up(a, t + 0.95f), new Vector3(0.55f, 1.9f, 0.55f), rot);
                B(LandmarkGeometry.Marble).Box(Up(a, t + 1.95f), new Vector3(0.7f, 0.12f, 0.7f), rot);
                B(LandmarkGeometry.Marble).Box(Up(a, t + 2.15f), new Vector3(0.32f, 0.3f, 0.32f), rot);
                // Rails.
                B(LandmarkGeometry.Lead).Tube(Up(a, t + 1.6f), Up(b, t + 1.6f), 0.03f, 5, false);
                B(LandmarkGeometry.Lead).Tube(Up(a, t + 0.75f), Up(b, t + 0.75f), 0.025f, 4, false);
                if (level < 2) continue;
                int n = Mathf.Max(1, Mathf.RoundToInt(len / 0.16f));
                for (int k = 1; k < n; k++)
                {
                    var p = a + dir * (len * k / n);
                    B(LandmarkGeometry.Lead).Box(Up(p, t + 1.18f), new Vector3(0.025f, 0.9f, 0.025f));
                    if (k % 4 == 0) B(LandmarkGeometry.Gilt).Box(Up(p, t + 1.7f), new Vector3(0.05f, 0.14f, 0.05f));
                }
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

        // --- City life: street furniture, vehicles, pedestrians (local frame: +z forward, y up, base at 0) ---

        public const string Wood = "Bark", DarkSteel = "KitDark", Glass = "Glass", Tyre = "Tire";
        /// <summary>Vehicle lamps: emissive at night (EnvironmentMaterials registers them as night lights).</summary>
        public const string Headlight = "CarHeadlight", Taillight = "CarTaillight";

        /// <summary>Street furniture by <see cref="CityLife.Prop"/>; +z faces the street.</summary>
        public static void StreetProp(Parts B, CityLife.Prop kind, int variant = 0)
        {
            switch (kind)
            {
                case CityLife.Prop.CafeSet:
                {
                    // Round café table, two chairs along the lane, a parasol in one of the awning colours.
                    B(DarkSteel).Tube(Vector3.zero, new Vector3(0f, 0.72f, 0f), 0.03f, 6, false);
                    B(DarkSteel).Tube(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.03f, 0f), 0.22f, 8, true);
                    B(Wood).Tube(new Vector3(0f, 0.72f, 0f), new Vector3(0f, 0.75f, 0f), 0.34f, 10, true);
                    foreach (float z in new[] { -0.55f, 0.55f })
                    {
                        float zs = Mathf.Sign(z);
                        B(Wood).Box(new Vector3(0f, 0.45f, z), new Vector3(0.42f, 0.04f, 0.42f));
                        B(Wood).Box(new Vector3(0f, 0.68f, z + zs * 0.2f), new Vector3(0.42f, 0.42f, 0.03f));
                        B(DarkSteel).Box(new Vector3(0f, 0.22f, z), new Vector3(0.36f, 0.44f, 0.36f));
                    }
                    B(DarkSteel).Tube(new Vector3(0f, 0.75f, 0f), new Vector3(0f, 2.35f, 0f), 0.022f, 6, false);
                    var apex = new Vector3(0f, 2.4f, 0f);
                    const int seg = 8;
                    for (int k = 0; k < seg; k++)
                    {
                        float a0 = k * Mathf.PI * 2f / seg, a1 = (k + 1) * Mathf.PI * 2f / seg;
                        var r0 = new Vector3(Mathf.Cos(a0) * 1.05f, 2.02f, Mathf.Sin(a0) * 1.05f);
                        var r1 = new Vector3(Mathf.Cos(a1) * 1.05f, 2.02f, Mathf.Sin(a1) * 1.05f);
                        B(Awning(variant % Awnings)).TriangleFacing(r1, r0, apex, Vector2.zero, Vector2.right, new Vector2(0.5f, 1f), Vector3.up + (r0 + r1) * 0.2f);
                    }
                    break;
                }
                case CityLife.Prop.SimitCart:
                {
                    // Red simit cart: glazed display case on a box body, two wheels and a handle.
                    B(Awning(0)).Box(new Vector3(0f, 0.75f, 0f), new Vector3(0.75f, 0.6f, 1.3f));
                    B(Glass).Box(new Vector3(0f, 1.32f, 0f), new Vector3(0.7f, 0.55f, 1.2f));
                    B(LandmarkGeometry.Lead).Box(new Vector3(0f, 1.63f, 0f), new Vector3(0.8f, 0.06f, 1.35f));
                    foreach (float x in new[] { -0.42f, 0.42f })
                        B(DarkSteel).Tube(new Vector3(x, 0.32f, 0.25f), new Vector3(x * 1.12f, 0.32f, 0.25f), 0.3f, 10, true);
                    B(DarkSteel).Tube(new Vector3(-0.3f, 0.95f, -0.65f), new Vector3(0.3f, 0.95f, -0.65f), 0.02f, 4, true);
                    B(DarkSteel).Box(new Vector3(0f, 0.25f, -0.55f), new Vector3(0.06f, 0.5f, 0.06f));
                    break;
                }
                case CityLife.Prop.Bench:
                    for (int k = 0; k < 4; k++) B(Wood).Box(new Vector3(0f, 0.45f, -0.18f + k * 0.12f), new Vector3(1.8f, 0.04f, 0.1f));
                    for (int k = 0; k < 3; k++) B(Wood).Box(new Vector3(0f, 0.62f + k * 0.13f, -0.3f), new Vector3(1.8f, 0.1f, 0.03f), Quaternion.Euler(-12f, 0f, 0f));
                    foreach (float x in new[] { -0.75f, 0.75f })
                    {
                        B(DarkSteel).Box(new Vector3(x, 0.22f, 0f), new Vector3(0.06f, 0.44f, 0.5f));
                        B(DarkSteel).Box(new Vector3(x, 0.7f, -0.3f), new Vector3(0.05f, 0.5f, 0.05f));
                    }
                    break;
                case CityLife.Prop.Bin:
                    B(DarkSteel).Tube(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.9f, 0f), 0.24f, 10, true);
                    B(DarkSteel).Tube(new Vector3(0f, 0.9f, 0f), new Vector3(0f, 0.96f, 0f), 0.27f, 10, true);
                    break;
                case CityLife.Prop.Bollard:
                    B(DarkSteel).Tube(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.85f, 0f), 0.07f, 8, true);
                    B(DarkSteel).Tube(new Vector3(0f, 0.85f, 0f), new Vector3(0f, 0.92f, 0f), 0.09f, 8, true);
                    break;
                case CityLife.Prop.Planter:
                    B(Concrete).Box(new Vector3(0f, 0.3f, 0f), new Vector3(1.2f, 0.6f, 1.2f));
                    B(Foliage).Box(new Vector3(0f, 0.85f, 0f), new Vector3(1.0f, 0.55f, 1.0f), Quaternion.Euler(0f, 45f, 0f));
                    break;
                case CityLife.Prop.BusShelter:
                    B(DarkSteel).Box(new Vector3(0f, 2.55f, 0f), new Vector3(4.2f, 0.12f, 1.8f));
                    foreach (float x in new[] { -2f, 2f }) B(DarkSteel).Box(new Vector3(x, 1.25f, -0.75f), new Vector3(0.08f, 2.5f, 0.08f));
                    B(Glass).Box(new Vector3(0f, 1.35f, -0.8f), new Vector3(4f, 2.1f, 0.03f));
                    B(Glass).Box(new Vector3(-2f, 1.35f, -0.1f), new Vector3(0.03f, 2.1f, 1.3f));
                    for (int k = 0; k < 3; k++) B(Wood).Box(new Vector3(-0.6f + k * 0.12f, 0.46f, -0.5f + k * 0.12f), new Vector3(2.2f, 0.04f, 0.1f));
                    B(Seat).Box(new Vector3(1.4f, 1.6f, -0.76f), new Vector3(1.0f, 1.4f, 0.04f));   // timetable / city map panel
                    break;
                case CityLife.Prop.FlagPole:
                {
                    // 7 m pole with a 1.8 x 1.2 m Turkish flag, frozen mid-flutter (three gently waved strips).
                    B(LandmarkGeometry.Lead).Tube(Vector3.zero, new Vector3(0f, 7f, 0f), 0.05f, 6, true);
                    B(LandmarkGeometry.Gilt).Box(new Vector3(0f, 7.05f, 0f), new Vector3(0.12f, 0.1f, 0.12f));
                    const int strips = 3;
                    for (int k = 0; k < strips; k++)
                    {
                        float u0 = k / (float)strips, u1 = (k + 1) / (float)strips;
                        float x0 = 0.06f + u0 * 1.8f, x1 = 0.06f + u1 * 1.8f;
                        float z0 = 0.12f * Mathf.Sin(u0 * 5f), z1 = 0.12f * Mathf.Sin(u1 * 5f);
                        B(BalconyFlag).QuadFacing(new Vector3(x0, 6.9f, z0), new Vector3(x1, 6.85f, z1), new Vector3(x1, 5.65f, z1), new Vector3(x0, 5.7f, z0),
                            new Vector2(u0, 1f), new Vector2(u1, 1f), new Vector2(u1, 0f), new Vector2(u0, 0f), Vector3.forward);
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// A city vehicle by <see cref="CityLife.Vehicle"/>, body material `body`. Sedan 4.5 m, van 5 m,
        /// İETT-style bus 12 m, scooter 1.9 m. Low-poly, with glass, wheels and lights.
        /// </summary>
        public static void CityVehicle(Parts B, CityLife.Vehicle kind, string body)
        {
            void Lamps(float halfX, float front, float rear, float y, float size)
            {
                foreach (float x in new[] { -halfX, halfX })
                {
                    B(Headlight).Box(new Vector3(x, y, front), new Vector3(size * 1.6f, size, 0.06f));
                    B(Taillight).Box(new Vector3(x, y + 0.05f, rear), new Vector3(size * 1.4f, size * 0.8f, 0.06f));
                }
            }
            void Wheels(float track, float[] zs, float r)
            {
                foreach (float z in zs)
                    foreach (float x in new[] { -track, track })
                        B(Tyre).Tube(new Vector3(x - 0.1f * Mathf.Sign(x), r, z), new Vector3(x + 0.1f * Mathf.Sign(x), r, z), r, 10, true);
            }
            switch (kind)
            {
                case CityLife.Vehicle.Bus:
                    B(body).Box(new Vector3(0f, 1.0f, 0f), new Vector3(2.5f, 1.3f, 12f));
                    B(Glass).Box(new Vector3(0f, 2.1f, 0f), new Vector3(2.52f, 0.9f, 11.6f));
                    B(body).Box(new Vector3(0f, 2.75f, 0f), new Vector3(2.5f, 0.4f, 12f));
                    Wheels(1.05f, new[] { -3.8f, 3.6f }, 0.5f);
                    Lamps(0.95f, 6.01f, -6.01f, 0.75f, 0.22f);
                    break;
                case CityLife.Vehicle.Van:
                    B(body).Box(new Vector3(0f, 0.95f, -0.3f), new Vector3(1.95f, 1.5f, 4.2f));
                    B(body).Box(new Vector3(0f, 0.75f, 2.0f), new Vector3(1.9f, 1.0f, 0.9f));
                    B(Glass).Box(new Vector3(0f, 1.4f, 1.62f), new Vector3(1.8f, 0.6f, 0.4f), Quaternion.Euler(-25f, 0f, 0f));
                    Wheels(0.85f, new[] { -1.5f, 1.7f }, 0.36f);
                    Lamps(0.75f, 2.46f, -2.41f, 0.8f, 0.18f);
                    break;
                case CityLife.Vehicle.Scooter:
                    B(body).Box(new Vector3(0f, 0.55f, 0f), new Vector3(0.45f, 0.5f, 1.3f));
                    B(DarkSteel).Box(new Vector3(0f, 1.0f, 0.55f), new Vector3(0.6f, 0.06f, 0.06f));
                    B(Skin).Box(new Vector3(0f, 1.15f, -0.1f), new Vector3(0.36f, 0.6f, 0.3f));   // rider
                    B(DarkSteel).Box(new Vector3(0f, 1.55f, -0.1f), new Vector3(0.24f, 0.26f, 0.26f));   // helmet
                    foreach (float z in new[] { -0.65f, 0.65f }) B(Tyre).Tube(new Vector3(-0.05f, 0.25f, z), new Vector3(0.05f, 0.25f, z), 0.25f, 10, true);
                    B(Headlight).Box(new Vector3(0f, 0.95f, 0.62f), new Vector3(0.16f, 0.12f, 0.06f));
                    B(Taillight).Box(new Vector3(0f, 0.7f, -0.66f), new Vector3(0.14f, 0.08f, 0.04f));
                    break;
                default:   // sedan (cars and taxis)
                    B(body).Box(new Vector3(0f, 0.6f, 0f), new Vector3(1.8f, 0.6f, 4.5f));
                    B(body).Box(new Vector3(0f, 1.1f, -0.2f), new Vector3(1.6f, 0.45f, 2.1f));
                    B(Glass).Box(new Vector3(0f, 1.12f, 0.95f), new Vector3(1.58f, 0.42f, 0.35f), Quaternion.Euler(-35f, 0f, 0f));
                    B(Glass).Box(new Vector3(0f, 1.12f, -1.35f), new Vector3(1.58f, 0.4f, 0.3f), Quaternion.Euler(35f, 0f, 0f));
                    B(Glass).Box(new Vector3(0f, 1.12f, -0.2f), new Vector3(1.62f, 0.34f, 1.8f));
                    if (body == "CityTaxi") B(DarkSteel).Box(new Vector3(0f, 1.42f, -0.2f), new Vector3(0.5f, 0.14f, 0.2f));   // TAKSİ roof sign
                    Wheels(0.82f, new[] { -1.4f, 1.4f }, 0.32f);
                    Lamps(0.66f, 2.26f, -2.26f, 0.68f, 0.16f);
                    break;
            }
        }

        public const string SignalOff = "SignalOff", SignalRed = "SignalRed", SignalAmber = "SignalAmber", SignalGreen = "SignalGreen";

        /// <summary>
        /// Traffic light on a 3.4 m pole with a three-lamp head facing +z; state as CityLife (0 red, 1 amber,
        /// 2 green) lights one lamp (emissive material), the others stay dark.
        /// </summary>
        public static void TrafficLight(Parts B, int state)
        {
            B(DarkSteel).Tube(Vector3.zero, new Vector3(0f, 3.4f, 0f), 0.06f, 6, true);
            B(DarkSteel).Box(new Vector3(0f, 3.0f, 0.12f), new Vector3(0.34f, 1.0f, 0.24f));
            B(DarkSteel).Box(new Vector3(0f, 3.0f, 0.03f), new Vector3(0.46f, 1.14f, 0.04f));   // backplate
            for (int k = 0; k < 3; k++)
            {
                // Top red, middle amber, bottom green.
                int lamp = 2 - k;
                string m = lamp == 0 ? (state == 0 ? SignalRed : SignalOff) : lamp == 1 ? (state == 1 ? SignalAmber : SignalOff) : (state == 2 ? SignalGreen : SignalOff);
                float y = 2.68f + k * 0.32f;
                B(m).Tube(new Vector3(0f, y, 0.24f), new Vector3(0f, y, 0.27f), 0.11f, 10, true);
                B(DarkSteel).Box(new Vector3(0f, y + 0.13f, 0.31f), new Vector3(0.26f, 0.03f, 0.12f));   // visor
            }
        }

        /// <summary>Pedestrian, 1.72 m, two walking poses (stride 0/1) so instanced crowds can alternate them; shirt material `shirt`.</summary>
        public static void Pedestrian(Parts B, string shirt, string trousers, int stride)
        {
            float s = stride == 0 ? 1f : -1f;
            // Legs (trousers), swinging opposite each other.
            foreach (float side in new[] { -1f, 1f })
            {
                var hip = new Vector3(0.1f * side, 0.86f, 0f);
                var foot = new Vector3(0.1f * side, 0.04f, 0.22f * s * side);
                B(trousers).Tube(foot, hip, 0.075f, 6, true);
                B(Tyre).Box(foot + new Vector3(0f, 0f, 0.06f), new Vector3(0.1f, 0.08f, 0.26f));   // shoe
            }
            B(shirt).Box(new Vector3(0f, 1.18f, 0f), new Vector3(0.36f, 0.62f, 0.22f));
            foreach (float side in new[] { -1f, 1f })
                B(shirt).Tube(new Vector3(0.22f * side, 1.46f, 0f), new Vector3(0.24f * side, 0.9f, -0.16f * s * side), 0.05f, 5, true);
            B(Skin).Box(new Vector3(0f, 1.6f, 0.01f), new Vector3(0.18f, 0.22f, 0.2f));
            B(DarkSteel).Box(new Vector3(0f, 1.72f, -0.01f), new Vector3(0.19f, 0.06f, 0.21f));   // hair
        }

        /// <summary>
        /// Person sitting on a café chair: origin on the ground under the seat (seat height 0.45 m), facing +z (the
        /// table); thighs forward, shins down, forearms on the table edge.
        /// </summary>
        public static void SeatedPerson(Parts B, string shirt, string trousers)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                var hip = new Vector3(0.1f * side, 0.5f, -0.02f);
                var knee = new Vector3(0.11f * side, 0.52f, 0.36f);
                var foot = new Vector3(0.11f * side, 0.05f, 0.42f);
                B(trousers).Tube(hip, knee, 0.075f, 6, true);
                B(trousers).Tube(knee, foot, 0.06f, 6, true);
                B(Tyre).Box(foot + new Vector3(0f, -0.01f, 0.06f), new Vector3(0.1f, 0.08f, 0.24f));
                // Upper arm down from the shoulder, forearm forward to the table.
                var shoulder = new Vector3(0.22f * side, 1.06f, -0.04f);
                var elbow = new Vector3(0.24f * side, 0.8f, 0.08f);
                B(shirt).Tube(shoulder, elbow, 0.05f, 5, true);
                B(shirt).Tube(elbow, new Vector3(0.16f * side, 0.8f, 0.36f), 0.045f, 5, true);
            }
            B(shirt).Box(new Vector3(0f, 0.8f, -0.06f), new Vector3(0.36f, 0.6f, 0.22f), Quaternion.Euler(4f, 0f, 0f));
            B(Skin).Box(new Vector3(0f, 1.22f, -0.03f), new Vector3(0.18f, 0.22f, 0.2f));
            B(DarkSteel).Box(new Vector3(0f, 1.34f, -0.05f), new Vector3(0.19f, 0.06f, 0.21f));   // hair
        }

        public static string CatCoat(int i) => "CatCoat" + i;

        /// <summary>Street cat (about 0.45 m long), origin on the ground, facing +z: sitting upright or lying curled.</summary>
        public static void Cat(Parts B, CityLife.CatPose pose, string coat)
        {
            if (pose == CityLife.CatPose.Sitting)
            {
                B(coat).Box(new Vector3(0f, 0.09f, -0.04f), new Vector3(0.17f, 0.16f, 0.22f));                                  // haunches
                B(coat).Box(new Vector3(0f, 0.19f, 0.03f), new Vector3(0.12f, 0.2f, 0.11f), Quaternion.Euler(-18f, 0f, 0f));      // chest
                B(coat).Box(new Vector3(0f, 0.33f, 0.07f), new Vector3(0.11f, 0.09f, 0.09f));                                   // head
                B(coat).Tube(new Vector3(0f, 0.02f, -0.14f), new Vector3(0.12f, 0.02f, 0.02f), 0.018f, 4, true);                // tail round the paws
                foreach (float x in new[] { -0.035f, 0.035f })
                    B(coat).TriangleFacing(new Vector3(x - 0.025f, 0.37f, 0.07f), new Vector3(x + 0.025f, 0.37f, 0.07f), new Vector3(x, 0.42f, 0.07f),
                        Vector2.zero, Vector2.right, Vector2.up, Vector3.forward);
            }
            else
            {
                B(coat).Box(new Vector3(0f, 0.06f, 0f), new Vector3(0.17f, 0.11f, 0.32f));                                     // body
                B(coat).Box(new Vector3(0.02f, 0.08f, 0.19f), new Vector3(0.1f, 0.08f, 0.09f), Quaternion.Euler(0f, 15f, 0f));    // head on the paws
                B(coat).Tube(new Vector3(-0.06f, 0.02f, -0.15f), new Vector3(-0.1f, 0.02f, 0.08f), 0.018f, 4, true);            // tail
                foreach (float x in new[] { -0.01f, 0.05f })
                    B(coat).TriangleFacing(new Vector3(x - 0.02f, 0.115f, 0.2f), new Vector3(x + 0.02f, 0.115f, 0.2f), new Vector3(x, 0.155f, 0.2f),
                        Vector2.zero, Vector2.right, Vector2.up, Vector3.forward);
            }
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
