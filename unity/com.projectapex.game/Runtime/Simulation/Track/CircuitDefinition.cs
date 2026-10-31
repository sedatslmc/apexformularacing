using System;
using System.Collections.Generic;
using Apex.Core;

namespace Apex.Tracks
{
    /// <summary>
    /// Authored circuit data, loaded from data/tracks/*.json. Points are rows of
    /// [east, north, elevation, width] in metres (schematic, not georeferenced).
    /// Unity space mapping: x = east, y = elevation, z = north.
    /// </summary>
    public sealed class CircuitDefinition
    {
        public string id = "";
        public string name = "";
        public string description = "";
        public string direction = "";
        /// <summary>Real-city data (Resources/City/&lt;cityId&gt;): an alternative layout of the same circuit shares it.</summary>
        public string cityId = "";
        /// <summary>Event identity for branding (e.g. "bosphorus"); empty = generic circuit dressing.</summary>
        public string identity = "";
        public float defaultWidth = 13f;
        public float startLineDistance;
        public float[] sectorFractions = { 0f, 1f / 3f, 2f / 3f };
        /// <summary>Control points: x = east, y = north, z = elevation, w = width.</summary>
        public readonly List<(float east, float north, float elevation, float width)> points = new List<(float, float, float, float)>();
        /// <summary>Pit lane alongside the main straight (raw distances along the centreline).</summary>
        public sealed class PitLane
        {
            /// <summary>-1 = left of the driving direction, +1 = right.</summary>
            public int side = -1;
            public float entry;
            public float exit;
            public float boxStart;
            public float boxSpacing = 10f;
            public float speedLimitKmh = 80f;
            /// <summary>Lateral distance from the track edge to the lane centre.</summary>
            public float laneCentreFromEdge = 7.5f;
            public float laneWidth = 10f;

            public float SpeedLimit => speedLimitKmh / 3.6f;

            /// <summary>Is the raw distance between entry and exit (handles wrap-around)?</summary>
            public bool Contains(float raw, float length)
            {
                float span = Apex.Core.ApexMath.PosMod(exit - entry, length);
                return Apex.Core.ApexMath.PosMod(raw - entry, length) <= span;
            }

            public float BoxDistance(int box) => boxStart + box * boxSpacing;
        }

        /// <summary>
        /// Optional run-off (m) from the track edge to the barrier per control point, left and
        /// right of the driving direction (point columns 5 and 6). Empty = default run-off.
        /// Street circuits use 1–2 m with escape roads at braking zones.
        /// </summary>
        public readonly List<(float left, float right)> runoffs = new List<(float, float)>();

        /// <summary>Authored corner (apex raw distance, direction, angle) — overrides automatic detection when present.</summary>
        public sealed class CornerMark
        {
            public string name = "";
            public float distance, angle;
            public bool right;
            /// <summary>False for a street bend without kerbs (JSON "kerbs": false), e.g. one taken under braking.</summary>
            public bool kerbs = true;
        }

        public readonly List<CornerMark> corners = new List<CornerMark>();

        public PitLane pit;
        public readonly List<string> cornerNames = new List<string>();
        /// <summary>Optional shoreline polyline (east, north), water lies to the east.</summary>
        public readonly List<(float east, float north)> shoreline = new List<(float, float)>();

        /// <summary>A real-world landmark placed in game coordinates (see tools/bosphorus_geo.py).</summary>
        public sealed class Landmark
        {
            public string id = "";
            public string name = "";
            public string kind = "";
            public float east, north, height;
            public bool verified;
        }

        public readonly List<Landmark> landmarks = new List<Landmark>();
        /// <summary>Opposite (Asian) shoreline, water lies to the west of it.</summary>
        public readonly List<(float east, float north)> farShoreline = new List<(float, float)>();
        /// <summary>Bearing of true north in the game frame (deg, 0 = +z); NaN without geo data.</summary>
        public float trueNorthBearing = float.NaN;

        /// <summary>Distant land outline (game metres) with a hill profile for the skyline layer.</summary>
        public sealed class LandMass
        {
            public string id = "";
            /// <summary>Maximum hill height above the shore (m) and the distance scale of the rise (m).</summary>
            public float rise, scale;
            public readonly List<(float east, float north)> polygon = new List<(float, float)>();
        }

        public readonly List<LandMass> landMasses = new List<LandMass>();

        /// <summary>Optional terrain grid (street-level heights, m) in game metres; null if absent.</summary>
        public sealed class HeightGrid
        {
            public float originEast, originNorth, cell;
            public int nx, nz;
            public float[] h;

            /// <summary>Bilinear height; 0 outside the grid.</summary>
            public float Sample(float east, float north)
            {
                float fx = (east - originEast) / cell, fz = (north - originNorth) / cell;
                if (fx < 0f || fz < 0f || fx > nx - 1 || fz > nz - 1) return 0f;
                int x0 = Math.Min((int)fx, nx - 2), z0 = Math.Min((int)fz, nz - 2);
                float tx = fx - x0, tz = fz - z0;
                float a = h[z0 * nx + x0], b = h[z0 * nx + x0 + 1], c = h[(z0 + 1) * nx + x0], d = h[(z0 + 1) * nx + x0 + 1];
                return (a + (b - a) * tx) + ((c + (d - c) * tx) - (a + (b - a) * tx)) * tz;
            }
        }

        public HeightGrid heightGrid;

        /// <summary>Signature view from a point of the circuit (game-frame bearing, deg).</summary>
        public sealed class Sightline
        {
            public string label = "";
            public float east, north, bearing;
        }

        public readonly List<Sightline> sightlines = new List<Sightline>();

        /// <summary>Site of the monumental Turkish flag (game frame east, north), if the circuit has one.</summary>
        public (float east, float north)? monumentalFlag;

        public Landmark FindLandmark(string landmarkId) => landmarks.Find(l => l.id == landmarkId);

        public static CircuitDefinition FromJson(string json)
        {
            if (!(MiniJson.Parse(json) is Dictionary<string, object> root))
                throw new FormatException("Circuit JSON must be an object");
            var def = new CircuitDefinition
            {
                id = Str(root, "id"),
                name = Str(root, "name"),
                description = Str(root, "description"),
                direction = Str(root, "direction"),
                identity = Str(root, "identity"),
                cityId = Str(root, "city_id"),
                defaultWidth = Num(root, "default_width", 13f),
                startLineDistance = Num(root, "start_line_distance", 0f),
            };
            if (def.cityId.Length == 0) def.cityId = def.id;
            if (root.TryGetValue("sector_fractions", out var sf) && sf is List<object> sfl && sfl.Count > 0)
            {
                def.sectorFractions = new float[sfl.Count];
                for (int i = 0; i < sfl.Count; i++) def.sectorFractions[i] = Convert.ToSingle(sfl[i]);
            }
            if (!(root.TryGetValue("points", out var pts) && pts is List<object> list) || list.Count < 4)
                throw new FormatException("Circuit needs at least 4 control points");
            foreach (var row in list)
            {
                if (!(row is List<object> r) || r.Count < 3) throw new FormatException("Bad control point row");
                float w = r.Count > 3 ? Convert.ToSingle(r[3]) : def.defaultWidth;
                def.points.Add((Convert.ToSingle(r[0]), Convert.ToSingle(r[1]), Convert.ToSingle(r[2]), w));
                if (r.Count > 5) def.runoffs.Add((Convert.ToSingle(r[4]), Convert.ToSingle(r[5])));
            }
            if (def.runoffs.Count != 0 && def.runoffs.Count != def.points.Count)
                throw new FormatException("Run-off columns must be given for all control points or none");
            if (root.TryGetValue("pit", out var pv) && pv is Dictionary<string, object> pd)
            {
                def.pit = new PitLane
                {
                    side = (int)Num(pd, "side", -1f),
                    entry = Num(pd, "entry", 0f),
                    exit = Num(pd, "exit", 0f),
                    boxStart = Num(pd, "box_start", 0f),
                    boxSpacing = Num(pd, "box_spacing", 10f),
                    speedLimitKmh = Num(pd, "speed_limit_kmh", 80f),
                };
            }
            if (root.TryGetValue("corners", out var cm) && cm is List<object> cml)
                foreach (var o in cml)
                    if (o is Dictionary<string, object> cd)
                        def.corners.Add(new CornerMark
                        {
                            name = Str(cd, "name"), distance = Num(cd, "distance", 0f), angle = Num(cd, "angle", 90f),
                            right = cd.TryGetValue("right", out var rv) && rv is bool rb && rb,
                            kerbs = !(cd.TryGetValue("kerbs", out var kv) && kv is bool kb && !kb),
                        });
            if (root.TryGetValue("corner_names", out var cn) && cn is List<object> cnl)
                foreach (var c in cnl) def.cornerNames.Add(c as string ?? "");
            if (root.TryGetValue("shoreline", out var sh) && sh is List<object> shl)
            {
                foreach (var row in shl)
                    if (row is List<object> r && r.Count >= 2)
                        def.shoreline.Add((Convert.ToSingle(r[0]), Convert.ToSingle(r[1])));
            }
            if (root.TryGetValue("sightlines", out var sl) && sl is List<object> sll)
                foreach (var o in sll)
                {
                    if (!(o is Dictionary<string, object> sd)) continue;
                    var from = sd.TryGetValue("from", out var fr) ? fr as List<object> : null;
                    if (from == null || from.Count < 2) continue;
                    def.sightlines.Add(new Sightline
                    {
                        label = Str(sd, "label"),
                        east = Convert.ToSingle(from[0]),
                        north = Convert.ToSingle(from[1]),
                        bearing = Num(sd, "bearing_deg", 0f),
                    });
                }
            if (root.TryGetValue("monumental_flag", out var mf) && mf is Dictionary<string, object> mfd
                && mfd.TryGetValue("game", out var mfg) && mfg is List<object> mfl && mfl.Count >= 2)
                def.monumentalFlag = (Convert.ToSingle(mfl[0]), Convert.ToSingle(mfl[1]));
            if (root.TryGetValue("geo", out var geo) && geo is Dictionary<string, object> gd)
            {
                def.trueNorthBearing = Num(gd, "true_north_bearing_deg", float.NaN);
                if (gd.TryGetValue("landmarks", out var lm) && lm is List<object> lml)
                    foreach (var o in lml)
                    {
                        if (!(o is Dictionary<string, object> ld)) continue;
                        var pos = ld.TryGetValue("game", out var gp) ? gp as List<object> : null;
                        def.landmarks.Add(new Landmark
                        {
                            id = Str(ld, "id"),
                            name = Str(ld, "name"),
                            kind = Str(ld, "kind"),
                            height = Num(ld, "height", 0f),
                            east = pos != null && pos.Count > 1 ? Convert.ToSingle(pos[0]) : 0f,
                            north = pos != null && pos.Count > 1 ? Convert.ToSingle(pos[1]) : 0f,
                            verified = ld.TryGetValue("verified", out var vf) && vf is bool b && b,
                        });
                    }
                if (gd.TryGetValue("shoreline_asia", out var sa) && sa is List<object> sal)
                    foreach (var row in sal)
                        if (row is List<object> r && r.Count >= 2)
                            def.farShoreline.Add((Convert.ToSingle(r[0]), Convert.ToSingle(r[1])));
                if (gd.TryGetValue("height_grid", out var hg) && hg is Dictionary<string, object> hgd
                    && hgd.TryGetValue("h", out var hv) && hv is List<object> hl && hgd.TryGetValue("origin", out var ov) && ov is List<object> ol)
                {
                    var grid = new HeightGrid
                    {
                        originEast = Convert.ToSingle(ol[0]), originNorth = Convert.ToSingle(ol[1]),
                        cell = Num(hgd, "cell", 100f), nx = (int)Num(hgd, "nx", 0f), nz = (int)Num(hgd, "nz", 0f), h = new float[hl.Count],
                    };
                    for (int i = 0; i < hl.Count; i++) grid.h[i] = Convert.ToSingle(hl[i]);
                    if (grid.nx >= 2 && grid.nz >= 2 && grid.h.Length == grid.nx * grid.nz) def.heightGrid = grid;
                }
                if (gd.TryGetValue("land", out var la) && la is List<object> lal)
                    foreach (var o in lal)
                    {
                        if (!(o is Dictionary<string, object> ld)) continue;
                        var mass = new LandMass { id = Str(ld, "id"), rise = Num(ld, "rise", 40f), scale = Num(ld, "scale", 500f) };
                        if (ld.TryGetValue("polygon", out var pg) && pg is List<object> pgl)
                            foreach (var row in pgl)
                                if (row is List<object> r && r.Count >= 2)
                                    mass.polygon.Add((Convert.ToSingle(r[0]), Convert.ToSingle(r[1])));
                        if (mass.polygon.Count >= 3) def.landMasses.Add(mass);
                    }
            }
            return def;
        }

        static string Str(Dictionary<string, object> d, string key) =>
            d.TryGetValue(key, out var v) && v is string s ? s : "";

        static float Num(Dictionary<string, object> d, string key, float fallback) =>
            d.TryGetValue(key, out var v) && v is double n ? (float)n : fallback;
    }
}
