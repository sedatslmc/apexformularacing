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
            return def;
        }

        static string Str(Dictionary<string, object> d, string key) =>
            d.TryGetValue(key, out var v) && v is string s ? s : "";

        static float Num(Dictionary<string, object> d, string key, float fallback) =>
            d.TryGetValue(key, out var v) && v is double n ? (float)n : fallback;
    }
}
