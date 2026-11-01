using System;
using System.Collections.Generic;
using System.Numerics;
using Apex.Core;
using Apex.Tracks;

namespace Apex.Environment
{
    /// <summary>Simple polygon helpers for footprints and green areas (x/z plane).</summary>
    public static class Polygon
    {
        /// <summary>Signed area (positive = counter-clockwise seen from above with x right and z up).</summary>
        public static float SignedArea(IReadOnlyList<Vector2> p)
        {
            float a = 0f;
            for (int i = 0, j = p.Count - 1; i < p.Count; j = i++) a += p[j].X * p[i].Y - p[i].X * p[j].Y;
            return a * 0.5f;
        }

        public static bool Contains(IReadOnlyList<Vector2> p, float x, float z)
        {
            bool inside = false;
            for (int i = 0, j = p.Count - 1; i < p.Count; j = i++)
                if ((p[i].Y > z) != (p[j].Y > z) && x < (p[j].X - p[i].X) * (z - p[i].Y) / (p[j].Y - p[i].Y) + p[i].X)
                    inside = !inside;
            return inside;
        }

        /// <summary>
        /// Ear clipping for a simple polygon (no holes). Returns index triples into `p`, wound counter-clockwise
        /// like a counter-clockwise input (so roofs built from it face up). Degenerate input gives a fan.
        /// </summary>
        public static List<int> Triangulate(IReadOnlyList<Vector2> p)
        {
            var result = new List<int>();
            int n = p.Count;
            if (n < 3) return result;
            var idx = new List<int>(n);
            bool ccw = SignedArea(p) > 0f;
            for (int i = 0; i < n; i++) idx.Add(ccw ? i : n - 1 - i);
            int guard = 0;
            while (idx.Count > 3 && guard++ < n * n)
            {
                bool clipped = false;
                for (int k = 0; k < idx.Count; k++)
                {
                    int ia = idx[(k + idx.Count - 1) % idx.Count], ib = idx[k], ic = idx[(k + 1) % idx.Count];
                    Vector2 a = p[ia], b = p[ib], c = p[ic];
                    float cross = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
                    if (cross <= 1e-6f) continue;   // reflex or degenerate corner
                    bool empty = true;
                    foreach (int t in idx)
                    {
                        if (t == ia || t == ib || t == ic) continue;
                        if (InTriangle(p[t], a, b, c)) { empty = false; break; }
                    }
                    if (!empty) continue;
                    result.Add(ia); result.Add(ib); result.Add(ic);
                    idx.RemoveAt(k);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
            if (idx.Count == 3) { result.Add(idx[0]); result.Add(idx[1]); result.Add(idx[2]); }
            else if (idx.Count > 3)
                for (int k = 1; k + 1 < idx.Count; k++) { result.Add(idx[0]); result.Add(idx[k]); result.Add(idx[k + 1]); }
            return result;
        }

        static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.X - b.X) * (a.Y - b.Y) - (a.X - b.X) * (p.Y - b.Y);
            float d2 = (p.X - c.X) * (b.Y - c.Y) - (b.X - c.X) * (p.Y - c.Y);
            float d3 = (p.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (p.Y - a.Y);
            bool neg = d1 < 0f || d2 < 0f || d3 < 0f, pos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(neg && pos);
        }
    }

    /// <summary>
    /// The real city around the circuit from OpenStreetMap (Resources/City/&lt;id&gt;.json, built by
    /// tools/geo/build_city_a.py): building footprints with heights, streets, green areas and walls.
    /// Data © OpenStreetMap contributors (ODbL).
    /// </summary>
    public sealed class CityData
    {
        public sealed class Building
        {
            public string id, type, name, historic, amenity;
            public List<Vector2> outline;
            public float height;
            public string source;
            /// <summary>Historic ensemble the data pipeline identified by place and form ("akaretler"), else empty.</summary>
            public string ensemble = "";
        }

        public sealed class Street
        {
            public string id, highway, name;
            public float width;
            public bool oneway, bridge;
            public List<Vector2> points;
        }

        public sealed class Area
        {
            public string id, kind, name;
            public List<Vector2> outline;
        }

        public float radius;
        public readonly List<Building> buildings = new List<Building>();
        public readonly List<Street> streets = new List<Street>();
        public readonly List<Area> green = new List<Area>();
        public readonly List<List<Vector2>> walls = new List<List<Vector2>>();

        public static CityData FromJson(string json)
        {
            if (!(MiniJson.Parse(json) is Dictionary<string, object> root)) throw new FormatException("City JSON must be an object");
            var d = new CityData { radius = Num(root, "radius", 0f) };
            foreach (var o in List(root, "buildings"))
            {
                if (!(o is Dictionary<string, object> b)) continue;
                var outline = Pts(b, "outline");
                if (outline.Count < 3) continue;
                d.buildings.Add(new Building
                {
                    id = Str(b, "id"), type = Str(b, "type"), name = Str(b, "name"), historic = Str(b, "historic"), amenity = Str(b, "amenity"),
                    outline = outline, height = Num(b, "height", 12f), source = Str(b, "source"), ensemble = Str(b, "ensemble"),
                });
            }
            foreach (var o in List(root, "streets"))
            {
                if (!(o is Dictionary<string, object> s)) continue;
                var pts = Pts(s, "points");
                if (pts.Count < 2) continue;
                d.streets.Add(new Street
                {
                    id = Str(s, "id"), highway = Str(s, "highway"), name = Str(s, "name"), width = Num(s, "width", 0f),
                    oneway = s.TryGetValue("oneway", out var ow) && ow is bool owb && owb, bridge = s.TryGetValue("bridge", out var br) && br is bool brb && brb,
                    points = pts,
                });
            }
            foreach (var o in List(root, "green"))
            {
                if (!(o is Dictionary<string, object> g)) continue;
                var outline = Pts(g, "outline");
                if (outline.Count < 3) continue;
                d.green.Add(new Area { id = Str(g, "id"), kind = Str(g, "kind"), name = Str(g, "name"), outline = outline });
            }
            foreach (var o in List(root, "walls"))
                if (o is Dictionary<string, object> w)
                {
                    var pts = Pts(w, "points");
                    if (pts.Count >= 2) d.walls.Add(pts);
                }
            return d;
        }

        static List<object> List(Dictionary<string, object> d, string key) =>
            d.TryGetValue(key, out var v) && v is List<object> l ? l : new List<object>();

        static List<Vector2> Pts(Dictionary<string, object> d, string key)
        {
            var r = new List<Vector2>();
            foreach (var o in List(d, key))
                if (o is List<object> p && p.Count >= 2 && p[0] is double x && p[1] is double z) r.Add(new Vector2((float)x, (float)z));
            return r;
        }

        static string Str(Dictionary<string, object> d, string key) => d.TryGetValue(key, out var v) && v is string s ? s : "";
        static float Num(Dictionary<string, object> d, string key, float fallback) => d.TryGetValue(key, out var v) && v is double n ? (float)n : fallback;

        /// <summary>Carriageway width (m) by OSM class where the data has none.</summary>
        public static float DefaultWidth(string highway)
        {
            switch (highway)
            {
                case "primary": case "primary_link": return 12f;
                case "secondary": case "secondary_link": return 10f;
                case "tertiary": case "tertiary_link": return 8f;
                case "residential": case "unclassified": return 6f;
                case "living_street": case "pedestrian": return 5f;
                case "service": return 4f;
                case "steps": return 3f;
                default: return 2.5f;   // footway, path
            }
        }

        public static bool IsRoad(string highway) =>
            highway != "footway" && highway != "steps" && highway != "path" && highway != "pedestrian" && highway != "corridor";
    }

    /// <summary>
    /// Places the real buildings and the greenery of <see cref="CityData"/> on the circuit's terrain:
    /// <list type="bullet">
    /// <item>buildings keep clear of the racing envelope (<see cref="TrackClearance"/>), the water, the
    /// areas of the landmark models and the circuit's sightlines;</item>
    /// <item>trees fill the real parks, gardens, woods (umbrella pines and planes) and cemeteries (cypresses),
    /// plus street trees along residential and main streets.</item>
    /// </list>
    /// Footprints are real (OSM); heights are tagged or estimated (see build_city_a.py), facades are generic.
    /// </summary>
    public sealed class OsmCity
    {
        public struct Building
        {
            public List<Vector2> outline;   // counter-clockwise
            public float ground, height;
            public int style;
            /// <summary>0 flat roof with parapet, 1 tiled hip roof (small, simple footprints).</summary>
            public int roof;
            public bool historic, shop;
            /// <summary>House of worship (mosque, church, synagogue): no shops, signs, balconies or hanging flags.</summary>
            public bool worship;
            /// <summary>Within <see cref="StreetFronts.OrtakoyRadius"/> of the Ortaköy Mosque (shop mix, more flags).</summary>
            public bool ortakoy;
            /// <summary>Building family (BuildingFamilies.Family).</summary>
            public BuildingFamilies.Family family;
            public string name;
            /// <summary>Built as an in-house landmark model, not as a generic block (see <see cref="ClassicalKind"/>).</summary>
            public int classical;
            /// <summary>Per outline edge (edge i runs from outline[i] to outline[i + 1]): faces a street or the circuit.</summary>
            public bool[] front;
            /// <summary>Per outline edge: a street front that looks onto the circuit (festive: more flags on race days).</summary>
            public bool[] raceFront;
            /// <summary>
            /// Per outline edge: street level in front of it (terrain 1.5 m outside the facade). On a slope the ground
            /// floor (shop row, doors, signs) of each facade starts at its own street, not at the lowest corner of the
            /// footprint; the walls still reach down to <see cref="ground"/> (a plinth on the downhill side).
            /// Between ground + 0.6 and two storeys below the roof.
            /// </summary>
            public float[] level;
        }

        public const int NotClassical = 0, PalaceWing = 1, PalaceDependency = 2, ClockTower = 3, RowHouse = 4, Gate = 5, BrickShell = 6;

        /// <summary>OSM amenities that are shops, cafés or similar street-front businesses (not houses of worship,
        /// schools or offices, which also carry an amenity tag).</summary>
        public static bool IsShopAmenity(string amenity) => amenity == "cafe" || amenity == "restaurant" || amenity == "fast_food" || amenity == "bar" ||
            amenity == "pub" || amenity == "ice_cream" || amenity == "pharmacy" || amenity == "bank" || amenity == "food_court" || amenity == "marketplace";

        /// <summary>
        /// Historic palace buildings from their OSM names, built with modelled classical facades instead of the
        /// generic facade texture: the Dolmabahçe and Çırağan palaces (wings, halls, gates), their named service
        /// buildings (Daire, Köşk, Kapı) and the Dolmabahçe clock tower. Later additions (the Kempinski hotel,
        /// the footbridge) stay generic.
        /// </summary>
        public static int ClassicalKind(CityData.Building b)
        {
            string n = b.name;
            if (b.ensemble == "akaretler") return RowHouse;
            // Esma Sultan Yalısı (1875; burnt out 1975, brick shell with a glass hall since 2001) beside the Ortaköy Mosque.
            if (n.Contains("Esma Sultan")) return BrickShell;
            if (n.Length == 0) return b.type == "palace" ? PalaceWing : NotClassical;
            if (n.Contains("Kempinski") || n.Contains("Geçidi")) return NotClassical;
            if (n.Contains("Saat Kulesi")) return ClockTower;
            // Monumental gates: a "Kapı" whose footprint is a slender block (the show gates of Dolmabahçe).
            if ((n.EndsWith("Kapı") || n.EndsWith("Kapısı")) && Slenderness(b.outline) >= 2f) return Gate;
            if (n.Contains("Saray") || b.type == "palace") return PalaceWing;
            if (n.EndsWith("Dairesi") || n.EndsWith("Daire") || n.Contains("Köşkü") || n.EndsWith("Kapı") || n.EndsWith("Kapısı")) return PalaceDependency;
            return NotClassical;
        }

        /// <summary>Length of the longest edge over the footprint's extent across it (1 = square, 5 = a slender block).</summary>
        public static float Slenderness(List<Vector2> o)
        {
            float best = 0f; Vector2 dir = Vector2.UnitX;
            for (int i = 0; i < o.Count; i++)
            {
                var d = o[(i + 1) % o.Count] - o[i];
                if (d.Length() > best) { best = d.Length(); dir = Vector2.Normalize(d); }
            }
            float minU = float.MaxValue, maxU = float.MinValue, minV = float.MaxValue, maxV = float.MinValue;
            foreach (var p in o)
            {
                float u = Vector2.Dot(p, dir), v = p.X * -dir.Y + p.Y * dir.X;
                minU = MathF.Min(minU, u); maxU = MathF.Max(maxU, u); minV = MathF.Min(minV, v); maxV = MathF.Max(maxV, v);
            }
            return (maxU - minU) / MathF.Max(0.1f, maxV - minV);
        }

        /// <summary>
        /// Height for the classical models. OSM has no height for most palace wings and the neighbourhood estimate
        /// is far too low for their tall storeys, so the known ones are set (estimates from photos and the storey
        /// count: Dolmabahçe selamlık 3 storeys, ceremonial hall 26 m to the cornice under its dome, harem and the
        /// crown prince's wing lower; Çırağan 2 tall storeys over a basement); others keep at least 7.5 m.
        /// </summary>
        public static float ClassicalHeight(CityData.Building b, int kind)
        {
            string n = b.name;
            if (kind == ClockTower) return 27f;
            if (kind == Gate) return 14f;
            if (kind == BrickShell) return 15f;
            if (n.Contains("Muayede")) return 26f;
            if (n.Contains("Selamlık")) return 21f;
            if (n.Contains("Harem")) return 17f;
            if (n.Contains("Çırağan")) return 17f;
            if (kind == PalaceWing) return MathF.Max(b.height, 14f);
            return MathF.Max(b.height, 7.5f);
        }

        public readonly List<Building> buildings = new List<Building>();
        public readonly List<CityLayout.Tree> trees = new List<CityLayout.Tree>();
        public int removedTrack, removedWater, removedReserved;
        readonly Vector2? ortakoyCentre;
        /// <summary>The Ortaköy Mosque (centre of the quarter's reference detail), if the circuit has it.</summary>
        public Vector2? OrtakoyCentre => ortakoyCentre;

        /// <summary>Distance a building keeps from the racing envelope (behind barrier, pavement).</summary>
        public const float BuildingClearance = 3f;
        public const float TreeClearance = 2.5f;

        readonly TerrainField field;
        readonly TrackClearance clearance;
        readonly List<CityLayout.Reserved> reserved;

        public OsmCity(CityData data, TerrainField field, TrackClearance clearance, CircuitDefinition def, IReadOnlyList<CityLayout.Reserved> extraReserved, int quality)
        {
            this.field = field;
            this.clearance = clearance;
            reserved = LandmarkModelAreas(def);
            var mosque = def.FindLandmark("ortakoy_mosque");
            if (mosque != null) ortakoyCentre = new Vector2(mosque.east, mosque.north);
            if (extraReserved != null) reserved.AddRange(extraReserved);
            palaceFront = new PalaceWaterfront(data);
            foreach (var b in data.buildings) TryBuilding(b, def);
            DemoteStrayDependencies(data);
            IndexBuildings();
            IndexStreets(data);
            MarkStreetFronts();
            PlantGreen(data, quality);
            PlantStreetTrees(data, quality);
            PlantOpenLand(data, def, quality);
        }

        /// <summary>Edges whose face looks onto a street (or the circuit) within a few metres: these get balconies, bay windows and shop awnings.</summary>
        void MarkStreetFronts()
        {
            for (int k = 0; k < buildings.Count; k++)
            {
                var b = buildings[k];
                int n = b.outline.Count;
                b.front = new bool[n];
                b.raceFront = new bool[n];
                b.level = new float[n];
                float lowest = b.ground + 0.6f, highest = MathF.Max(lowest, b.ground + 0.6f + b.height - 2f * CityLayout.FloorHeight - 0.6f);
                for (int i = 0; i < n; i++)
                {
                    var a = b.outline[i]; var c = b.outline[(i + 1) % n];
                    var e = c - a;
                    float len = e.Length();
                    b.level[i] = lowest;
                    if (len < 0.05f) continue;
                    var outward = new Vector2(e.Y, -e.X) / len;
                    var street = (a + c) * 0.5f + outward * 1.5f;
                    if (b.classical == NotClassical && !field.IsWater(street.X, street.Y))
                        b.level[i] = ApexMath.Clamp(field.Height(street.X, street.Y), lowest, highest);
                    if (len < 3f) continue;
                    var p = (a + c) * 0.5f + outward * 3f;
                    // Not where a neighbour stands in front of the facade (balconies would cut into its roof).
                    bool open = true;
                    foreach (float f in new[] { 0.25f, 0.5f, 0.75f })
                    {
                        var q = Vector2.Lerp(a, c, f) + outward * 1.5f;
                        if (InsideBuilding(q.X, q.Y, 0f)) open = false;
                    }
                    b.front[i] = open && (StreetDistance(p.X, p.Y) < FrontReach || clearance.Margin(p.X, p.Y) < FrontReach + 3f);
                    b.raceFront[i] = b.front[i] && clearance.Margin(p.X, p.Y) < RaceFrontReach;
                }
                buildings[k] = b;
            }
        }

        /// <summary>Distance from a point 3 m in front of a facade to a street edge (or the racing envelope, plus 3 m) that makes the facade a street front.</summary>
        public const float FrontReach = 5f;
        /// <summary>A street front within this margin of the racing envelope faces the circuit (m).</summary>
        public const float RaceFrontReach = 25f;

        /// <summary>"Daire", "Köşk" and "Kapı" are palace service buildings only next to a palace; elsewhere (a tax office, "Vergi Dairesi") they stay generic.</summary>
        void DemoteStrayDependencies(CityData data)
        {
            var palaces = new List<Vector2>();
            foreach (var b in buildings) if (b.classical == PalaceWing || b.classical == ClockTower || b.classical == Gate) palaces.Add(Centre(b.outline));
            for (int i = 0; i < buildings.Count; i++)
            {
                var b = buildings[i];
                if (b.classical != PalaceDependency) continue;
                var c = Centre(b.outline);
                bool near = false;
                foreach (var p in palaces) if (Vector2.DistanceSquared(p, c) < 400f * 400f) near = true;
                if (near) continue;
                b.classical = NotClassical;
                foreach (var d in data.buildings) if (d.name == b.name) b.height = d.height;
                buildings[i] = b;
            }
        }

        static Vector2 Centre(List<Vector2> o)
        {
            var c = Vector2.Zero;
            foreach (var p in o) c += p;
            return c / o.Count;
        }

        /// <summary>Areas the landmark models occupy (their own geometry); parks are planted, not reserved.</summary>
        static List<CityLayout.Reserved> LandmarkModelAreas(CircuitDefinition def)
        {
            var r = new List<CityLayout.Reserved>();
            foreach (var l in def.landmarks)
            {
                float radius;
                switch (l.kind)
                {
                    // Palaces: built from their own OSM footprints (ClassicalKind); the area around them stays real city.
                    case "mosque": radius = 45f; break;
                    case "mosque_big": radius = 120f; break;
                    case "tower": radius = 25f; break;
                    case "pier": radius = 40f; break;
                    case "bridge_end": radius = 60f; break;
                    default: continue;
                }
                r.Add(new CityLayout.Reserved(l.east, l.north, radius));
            }
            return r;
        }

        bool Reserved(float x, float z, float pad)
        {
            foreach (var r in reserved)
            {
                float dx = x - r.x, dz = z - r.z, rr = r.radius + pad;
                if (dx * dx + dz * dz < rr * rr) return true;
            }
            return false;
        }

        void TryBuilding(CityData.Building b, CircuitDefinition def)
        {
            var o = new List<Vector2>(b.outline);
            if (Polygon.SignedArea(o) < 0f) o.Reverse();
            float area = Polygon.SignedArea(o);
            if (area < 12f) return;
            Vector2 c = Vector2.Zero;
            foreach (var p in o) c += p;
            c /= o.Count;
            if (field.IsWater(c.X, c.Y)) { removedWater++; return; }
            int classical = ClassicalKind(b);
            if (classical == NotClassical && Reserved(c.X, c.Y, 0f)) { removedReserved++; return; }
            // Every corner and edge midpoint outside the racing envelope.
            float ground = float.MaxValue;
            for (int i = 0; i < o.Count; i++)
            {
                var p = o[i];
                var m = (p + o[(i + 1) % o.Count]) * 0.5f;
                if (clearance.Margin(p.X, p.Y) < BuildingClearance || clearance.Margin(m.X, m.Y) < BuildingClearance) { removedTrack++; return; }
                if (field.IsWater(p.X, p.Y)) { removedWater++; return; }
                ground = MathF.Min(ground, field.Height(p.X, p.Y));
            }
            int hash = Hash(b.id);
            bool historic = b.historic.Length > 0 || b.type == "mosque" || b.type == "church" || b.type == "palace" || b.amenity == "place_of_worship";
            bool ortakoy = ortakoyCentre.HasValue && Vector2.Distance(c, ortakoyCentre.Value) < StreetFronts.OrtakoyRadius;
            // Building family (BuildingFamilies): colours, roof and street-front relief follow it.
            var family = BuildingFamilies.Classify(b.type, b.height, area, historic, ortakoy, hash);
            int roof = BuildingFamilies.HipRoof(family, o.Count, hash) ? 1 : 0;
            buildings.Add(new Building
            {
                outline = o,
                ground = ground - 0.6f,
                height = classical != NotClassical ? ClassicalHeight(b, classical) : b.height,
                classical = classical,
                style = BuildingFamilies.Style(family, historic, hash),
                family = family,
                roof = roof,
                historic = historic,
                shop = IsShopAmenity(b.amenity) || ((b.type == "commercial" || b.type == "retail") && b.amenity.Length == 0),
                worship = b.amenity == "place_of_worship" || b.type == "mosque" || b.type == "church" || b.type == "synagogue",
                ortakoy = ortakoy,
                name = b.name,
            });
        }

        /// <summary>
        /// The buildings as oriented boxes (centre, longest-edge direction, extents) for code written for
        /// generic lots: rooftop flags, ambience enclosure. Band 0.
        /// </summary>
        public List<CityLayout.Lot> AsLots()
        {
            var r = new List<CityLayout.Lot>(buildings.Count);
            foreach (var b in buildings)
            {
                var o = b.outline;
                int best = 0;
                float bestLen = 0f;
                for (int i = 0; i < o.Count; i++)
                {
                    float l = Vector2.Distance(o[i], o[(i + 1) % o.Count]);
                    if (l > bestLen) { bestLen = l; best = i; }
                }
                var dir = Vector2.Normalize(o[(best + 1) % o.Count] - o[best]);
                var nrm = new Vector2(-dir.Y, dir.X);
                float minU = float.MaxValue, maxU = float.MinValue, minV = float.MaxValue, maxV = float.MinValue;
                foreach (var p in o)
                {
                    float u = Vector2.Dot(p, dir), v = Vector2.Dot(p, nrm);
                    minU = MathF.Min(minU, u); maxU = MathF.Max(maxU, u); minV = MathF.Min(minV, v); maxV = MathF.Max(maxV, v);
                }
                var c = dir * ((minU + maxU) * 0.5f) + nrm * ((minV + maxV) * 0.5f);
                // Lot yaw is the facade normal direction (0 = +z, clockwise towards +x); w runs along the facade.
                r.Add(new CityLayout.Lot
                {
                    x = c.X, z = c.Y, ground = b.ground, w = maxU - minU, d = maxV - minV, height = b.height + 0.6f,
                    floors = Math.Max(1, (int)(b.height / CityLayout.FloorHeight)), yaw = MathF.Atan2(nrm.X, nrm.Y), style = b.style,
                    roof = b.roof, band = 0,
                });
            }
            return r;
        }

        static int Hash(string s)
        {
            unchecked
            {
                int h = 17;
                foreach (char ch in s) h = h * 31 + ch;
                return h & 0x7fffffff;
            }
        }

        void PlantGreen(CityData data, int quality)
        {
            float scale = quality == 0 ? 1.6f : quality == 1 ? 1.2f : 1f;
            foreach (var a in data.green)
            {
                float spacing; int kind; float density;
                switch (a.kind)
                {
                    case "forest": case "wood": spacing = 7f; kind = 1; density = 0.85f; break;
                    case "park": spacing = 11f; kind = -1; density = 0.6f; break;
                    case "garden": spacing = 12f; kind = 0; density = 0.5f; break;
                    case "cemetery": spacing = 8f; kind = 2; density = 0.7f; break;   // cypresses, as in every Istanbul cemetery
                    case "scrub": spacing = 10f; kind = 0; density = 0.4f; break;
                    default: spacing = 22f; kind = 0; density = 0.25f; break;          // grass: a few trees
                }
                spacing *= scale;
                float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
                foreach (var p in a.outline) { minX = MathF.Min(minX, p.X); maxX = MathF.Max(maxX, p.X); minZ = MathF.Min(minZ, p.Y); maxZ = MathF.Max(maxZ, p.Y); }
                int seed = Hash(a.id);
                for (float z = minZ + spacing * 0.5f; z < maxZ; z += spacing)
                    for (float x = minX + spacing * 0.5f; x < maxX; x += spacing)
                    {
                        float jx = (LandModel.Hash((int)(x * 7) ^ seed, (int)z) * 0.5f) * spacing * 0.8f;
                        float jz = (LandModel.Hash((int)x, (int)(z * 7) ^ seed) * 0.5f) * spacing * 0.8f;
                        float px = x + jx, pz = z + jz;
                        if (LandModel.Hash((int)(px * 3), (int)(pz * 3)) * 0.5f + 0.5f > density) continue;
                        if (!Polygon.Contains(a.outline, px, pz)) continue;
                        int k = kind >= 0 ? kind : (LandModel.Hash((int)px, (int)pz) > 0.1f ? 0 : 1);
                        AddTree(px, pz, k);
                    }
            }
        }

        void PlantStreetTrees(CityData data, int quality)
        {
            float step = quality == 0 ? 24f : 15f;
            foreach (var s in data.streets)
            {
                if (s.highway != "residential" && s.highway != "secondary" && s.highway != "tertiary" && s.highway != "primary" && s.highway != "living_street") continue;
                float w = s.width > 0f ? s.width : CityData.DefaultWidth(s.highway);
                float side = w * 0.5f + 1.6f;
                float next = step * 0.5f, run = 0f;
                int seed = Hash(s.id);
                for (int i = 0; i + 1 < s.points.Count; i++)
                {
                    var a = s.points[i]; var b = s.points[i + 1];
                    float len = Vector2.Distance(a, b);
                    if (len < 1e-3f) continue;
                    var dir = (b - a) / len;
                    var right = new Vector2(dir.Y, -dir.X);
                    while (next <= run + len)
                    {
                        var p = a + dir * (next - run);
                        int k = (int)(next / step);
                        // Alternate sides; leave some gaps (driveways, shopfronts).
                        if ((seed + k * 7) % 5 != 0)
                        {
                            var q = p + right * ((seed + k) % 2 == 0 ? side : -side);
                            AddTree(q.X, q.Y, 0);
                        }
                        next += step;
                    }
                    run += len;
                }
            }
        }

        readonly Dictionary<(int, int), List<(Vector2 a, Vector2 b, float half)>> streetBins = new Dictionary<(int, int), List<(Vector2, Vector2, float)>>();

        void IndexStreets(CityData data)
        {
            foreach (var s in data.streets)
            {
                float half = (s.width > 0f ? s.width : CityData.DefaultWidth(s.highway)) * 0.5f;
                for (int i = 0; i + 1 < s.points.Count; i++)
                {
                    var a = s.points[i]; var b = s.points[i + 1];
                    int x0 = (int)MathF.Floor(MathF.Min(a.X, b.X) / BinSize), x1 = (int)MathF.Floor(MathF.Max(a.X, b.X) / BinSize);
                    int z0 = (int)MathF.Floor(MathF.Min(a.Y, b.Y) / BinSize), z1 = (int)MathF.Floor(MathF.Max(a.Y, b.Y) / BinSize);
                    for (int bz = z0; bz <= z1; bz++)
                        for (int bx = x0; bx <= x1; bx++)
                        {
                            if (!streetBins.TryGetValue((bx, bz), out var l)) streetBins[(bx, bz)] = l = new List<(Vector2, Vector2, float)>();
                            l.Add((a, b, half));
                        }
                }
            }
        }

        /// <summary>Distance from a point to the nearest street edge (negative on the carriageway), up to ~BinSize.</summary>
        public float StreetDistance(float x, float z)
        {
            float best = float.MaxValue;
            var p = new Vector2(x, z);
            int cx = (int)MathF.Floor(x / BinSize), cz = (int)MathF.Floor(z / BinSize);
            for (int bz = cz - 1; bz <= cz + 1; bz++)
                for (int bx = cx - 1; bx <= cx + 1; bx++)
                {
                    if (!streetBins.TryGetValue((bx, bz), out var l)) continue;
                    foreach (var (a, b, half) in l)
                    {
                        var ab = b - a;
                        float t = ab.LengthSquared() > 1e-6f ? Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0f, 1f) : 0f;
                        best = MathF.Min(best, Vector2.Distance(p, a + ab * t) - half);
                    }
                }
            return best;
        }

        /// <summary>True when (x, z) lies inside a building footprint or within `pad` of its bounding box edge.</summary>
        public bool InsideBuilding(float x, float z, float pad)
        {
            if (!NearBuilding(x, z, pad)) return false;
            if (pad > 0f) return true;
            if (bins.TryGetValue(((int)MathF.Floor(x / BinSize), (int)MathF.Floor(z / BinSize)), out var list))
                foreach (int i in list)
                    if (Polygon.Contains(buildings[i].outline, x, z)) return true;
            return false;
        }

        bool NearBuilding(float x, float z, float pad)
        {
            for (int bz = (int)MathF.Floor((z - pad) / BinSize); bz <= (int)MathF.Floor((z + pad) / BinSize); bz++)
                for (int bx = (int)MathF.Floor((x - pad) / BinSize); bx <= (int)MathF.Floor((x + pad) / BinSize); bx++)
                {
                    if (!bins.TryGetValue((bx, bz), out var list)) continue;
                    foreach (int i in list)
                    {
                        var bb = boxes[i];
                        if (x >= bb.X - pad && x <= bb.Z + pad && z >= bb.Y - pad && z <= bb.W + pad) return true;
                    }
                }
            return false;
        }

        /// <summary>
        /// Land with neither buildings nor streets (wooded hillsides, palace grounds, parks that OSM holds only as
        /// relations, such as Yıldız Park) gets natural tree cover: dense inside the circuit's park landmarks
        /// (umbrella pines and planes), sparse elsewhere (8 %, earlier 30 % made a uniform forest), and none within
        /// <see cref="OpenCoastBand"/> of the coast. Only within the real-city radius.
        /// </summary>
        /// <summary>Strip behind the coast kept free of filler trees (real gardens and street trees still grow there).</summary>
        public const float OpenCoastBand = 40f;

        void PlantOpenLand(CityData data, CircuitDefinition def, int quality)
        {
            float spacing = quality == 0 ? 16f : quality == 1 ? 12f : 10f;
            var parks = new List<CircuitDefinition.Landmark>();
            foreach (var l in def.landmarks) if (l.kind == "park") parks.Add(l);
            float minX = field.MinX, maxX = field.MaxX, minZ = field.MinZ, maxZ = field.MaxZ;
            for (float z = minZ; z < maxZ; z += spacing)
                for (float x = minX; x < maxX; x += spacing)
                {
                    float jx = LandModel.Hash((int)x * 3 + 1, (int)z) * spacing * 0.4f, jz = LandModel.Hash((int)x, (int)z * 3 + 1) * spacing * 0.4f;
                    float px = x + jx, pz = z + jz;
                    if (field.TrackDistance(px, pz, data.radius, out _) > data.radius) continue;
                    bool park = false;
                    foreach (var l in parks)
                        if ((px - l.east) * (px - l.east) + (pz - l.north) * (pz - l.north) < 230f * 230f) park = true;
                    // Dense only in the circuit's park landmarks (Yıldız Park, which OSM holds as a relation); elsewhere
                    // a few scattered trees, none on the first 40 m behind the quays (open views onto the Bosphorus).
                    float density = park ? 0.9f : 0.08f;
                    if (LandModel.Hash((int)(px * 5), (int)(pz * 5)) * 0.5f + 0.5f > density) continue;
                    if (!park && field.land.HasLand && field.land.CoastDistance(px, pz) < OpenCoastBand) continue;
                    if (NearBuilding(px, pz, 6f) || StreetDistance(px, pz) < 3f) continue;
                    AddTree(px, pz, park && LandModel.Hash((int)px, (int)pz) < -0.2f ? 1 : 0);
                }
        }

        /// <summary>Dolmabahçe's Bosphorus front (quay, sea gates, the open forecourt in front of the facade).</summary>
        public PalaceWaterfront PalaceFront => palaceFront;
        PalaceWaterfront palaceFront;

        void AddTree(float x, float z, int kind)
        {
            if (field.IsWater(x, z)) return;
            // The garden strip between the palace and its quay stays open: the facade must show from the water.
            if (palaceFront != null && field.land.HasLand && palaceFront.InForecourt(x, z, field.land.CoastDistance(x, z))) return;
            if (clearance.Margin(x, z) < TreeClearance) return;
            if (Reserved(x, z, 2f)) return;
            if (bins.TryGetValue(((int)MathF.Floor(x / BinSize), (int)MathF.Floor(z / BinSize)), out var list))
                foreach (int i in list)
                {
                    var bb = boxes[i];
                    if (x >= bb.X && x <= bb.Z && z >= bb.Y && z <= bb.W && Polygon.Contains(buildings[i].outline, x, z)) return;
                }
            float h01 = LandModel.Hash((int)(x * 13), (int)(z * 13)) * 0.5f + 0.5f;
            float h = kind == 2 ? 9f + 5f * h01 : kind == 1 ? 8f + 5f * h01 : 8f + 7f * h01;
            trees.Add(new CityLayout.Tree { x = x, z = z, ground = field.Height(x, z), height = h, kind = kind });
        }

        const float BinSize = 40f;
        readonly Dictionary<(int, int), List<int>> bins = new Dictionary<(int, int), List<int>>();
        readonly List<Vector4> boxes = new List<Vector4>();   // minX, minZ, maxX, maxZ

        void IndexBuildings()
        {
            for (int i = 0; i < buildings.Count; i++)
            {
                float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
                foreach (var p in buildings[i].outline) { minX = MathF.Min(minX, p.X); maxX = MathF.Max(maxX, p.X); minZ = MathF.Min(minZ, p.Y); maxZ = MathF.Max(maxZ, p.Y); }
                boxes.Add(new Vector4(minX, minZ, maxX, maxZ));
                for (int bz = (int)MathF.Floor(minZ / BinSize); bz <= (int)MathF.Floor(maxZ / BinSize); bz++)
                    for (int bx = (int)MathF.Floor(minX / BinSize); bx <= (int)MathF.Floor(maxX / BinSize); bx++)
                    {
                        if (!bins.TryGetValue((bx, bz), out var l)) bins[(bx, bz)] = l = new List<int>();
                        l.Add(i);
                    }
            }
        }
    }
}
