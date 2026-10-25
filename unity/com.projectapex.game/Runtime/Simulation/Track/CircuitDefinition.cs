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

        public PitLane pit;
        public readonly List<string> cornerNames = new List<string>();
        /// <summary>Optional shoreline polyline (east, north), water lies to the east.</summary>
        public readonly List<(float east, float north)> shoreline = new List<(float, float)>();

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
                defaultWidth = Num(root, "default_width", 13f),
                startLineDistance = Num(root, "start_line_distance", 0f),
            };
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
            }
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
            if (root.TryGetValue("corner_names", out var cn) && cn is List<object> cnl)
                foreach (var c in cnl) def.cornerNames.Add(c as string ?? "");
            if (root.TryGetValue("shoreline", out var sh) && sh is List<object> shl)
            {
                foreach (var row in shl)
                    if (row is List<object> r && r.Count >= 2)
                        def.shoreline.Add((Convert.ToSingle(r[0]), Convert.ToSingle(r[1])));
            }
            return def;
        }

        static string Str(Dictionary<string, object> d, string key) =>
            d.TryGetValue(key, out var v) && v is string s ? s : "";

        static float Num(Dictionary<string, object> d, string key, float fallback) =>
            d.TryGetValue(key, out var v) && v is double n ? (float)n : fallback;
    }
}
