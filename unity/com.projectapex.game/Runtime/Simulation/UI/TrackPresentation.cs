using System;
using System.Collections.Generic;
using Apex.Core;
using Apex.Race;
using Apex.Tracks;

namespace Apex.UI
{
    /// <summary>Key facts of a circuit for the track selection, all derived from the track data (nothing invented).</summary>
    public sealed class TrackFacts
    {
        public string id = "", name = "", description = "";
        public bool istanbul;
        public float lengthKm;
        public int corners, sectors;
        public bool clockwise;
        /// <summary>Highest minus lowest point of the centreline (m).</summary>
        public float elevationRange;
        /// <summary>Personal best lap (s); 0 = none.</summary>
        public double bestLap;
        /// <summary>Named corners in lap order (empty when the track has no authored corner names).</summary>
        public readonly List<string> cornerNames = new List<string>();

        public string Direction => clockwise ? "im Uhrzeigersinn" : "gegen den Uhrzeigersinn";

        public static TrackFacts From(CircuitDefinition def, TrackGeometry g, double bestLap)
        {
            var f = new TrackFacts
            {
                id = def.id, name = def.name, description = def.description,
                istanbul = def.identity == EventIdentity.Bosphorus,
                lengthKm = g.Length / 1000f,
                corners = g.CornersFor(def).Count,
                sectors = def.sectorFractions.Length,
                bestLap = bestLap,
            };
            float minY = float.MaxValue, maxY = float.MinValue;
            double area = 0;
            for (int i = 0; i < g.Count; i++)
            {
                var p = g.Positions[i];
                var q = g.Positions[(i + 1) % g.Count];
                minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
                area += (double)p.X * q.Z - (double)q.X * p.Z;   // shoelace in (east, north): > 0 = counter-clockwise
            }
            f.elevationRange = maxY - minY;
            f.clockwise = area < 0;
            foreach (var c in TrackMap.CornersInLapOrder(def, g.Length)) if (!string.IsNullOrEmpty(c.name)) f.cornerNames.Add(c.name);
            return f;
        }
    }

    /// <summary>A circuit drawn top-down (north up) into a rectangle: polyline, sector per point, start line, corner numbers.</summary>
    public sealed class TrackMapLine
    {
        public float[] x, y;
        public int[] sector;
        public float startX, startY;
        public float[] cornerX = new float[0], cornerY = new float[0];
        public int Count => x.Length;
    }

    public static class TrackMap
    {
        public static List<CircuitDefinition.CornerMark> CornersInLapOrder(CircuitDefinition def, float length)
        {
            var list = new List<CircuitDefinition.CornerMark>(def.corners);
            list.Sort((a, b) => ApexMath.PosMod(a.distance - def.startLineDistance, length).CompareTo(ApexMath.PosMod(b.distance - def.startLineDistance, length)));
            return list;
        }

        /// <summary>Sector (0-based) of a lap fraction 0..1, from the circuit's sector boundaries.</summary>
        public static int SectorAt(float lapFraction, float[] sectorFractions)
        {
            int s = 0;
            for (int k = 1; k < sectorFractions.Length; k++) if (lapFraction >= sectorFractions[k]) s = k;
            return s;
        }

        /// <summary>
        /// Fits the centreline into width x height (y down, north up) with a padding, keeping the aspect ratio and
        /// centring it; at most <paramref name="maxPoints"/> points (closed loop: the first point is not repeated).
        /// Corner numbers sit <paramref name="cornerOffset"/> px outside the corner.
        /// </summary>
        public static TrackMapLine Fit(CircuitDefinition def, TrackGeometry g, float width, float height, float pad, int maxPoints, float cornerOffset = 16f)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var p in g.Positions)
            {
                minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
                minZ = Math.Min(minZ, p.Z); maxZ = Math.Max(maxZ, p.Z);
            }
            float w = Math.Max(1f, width - 2f * pad), h = Math.Max(1f, height - 2f * pad);
            float scale = Math.Min(w / Math.Max(maxX - minX, 1f), h / Math.Max(maxZ - minZ, 1f));
            float ox = (width - (maxX - minX) * scale) * 0.5f, oy = (height - (maxZ - minZ) * scale) * 0.5f;
            float MapX(float px) => ox + (px - minX) * scale;
            float MapY(float pz) => oy + (maxZ - pz) * scale;

            int step = Math.Max(1, (g.Count + maxPoints - 1) / Math.Max(2, maxPoints));
            int n = (g.Count + step - 1) / step;
            var line = new TrackMapLine { x = new float[n], y = new float[n], sector = new int[n] };
            for (int k = 0; k < n; k++)
            {
                int i = k * step;
                line.x[k] = MapX(g.Positions[i].X);
                line.y[k] = MapY(g.Positions[i].Z);
                float lap = ApexMath.PosMod(g.Distances[i] - def.startLineDistance, g.Length) / g.Length;
                line.sector[k] = SectorAt(lap, def.sectorFractions);
            }
            var s = g.PositionAt(def.startLineDistance);
            line.startX = MapX(s.X); line.startY = MapY(s.Z);
            var corners = CornersInLapOrder(def, g.Length);
            line.cornerX = new float[corners.Count]; line.cornerY = new float[corners.Count];
            for (int k = 0; k < corners.Count; k++)
            {
                g.FrameAt(corners[k].distance, out var cp, out _, out _, (corners[k].right ? -1f : 1f) * cornerOffset / Math.Max(scale, 1e-3f));
                line.cornerX[k] = MapX(cp.X); line.cornerY[k] = MapY(cp.Z);
            }
            return line;
        }
    }

    public enum RoundState { Done, Next, Upcoming }

    public struct CalendarRound
    {
        public int number;
        public string trackId, name;
        public RoundState state;
    }

    public struct StandingRow
    {
        public int position, points, team;
        public string name, code;
        public bool player;
    }

    /// <summary>Season overview: calendar with the round states and the drivers' standings (player = points index 0).</summary>
    public static class ChampionshipModel
    {
        public static List<CalendarRound> Calendar(string[] calendar, int nextRound, Func<string, string> name)
        {
            var list = new List<CalendarRound>();
            for (int i = 0; i < calendar.Length; i++)
                list.Add(new CalendarRound
                {
                    number = i + 1, trackId = calendar[i], name = name(calendar[i]),
                    state = i < nextRound ? RoundState.Done : i == nextRound ? RoundState.Next : RoundState.Upcoming,
                });
            return list;
        }

        /// <summary>All entries sorted by points; on a tie the player ranks first, then roster order.</summary>
        static List<int> Order(int[] points)
        {
            var idx = new List<int>();
            for (int i = 0; i < points.Length; i++) idx.Add(i);
            idx.Sort((a, b) => points[b] != points[a] ? points[b].CompareTo(points[a]) : a.CompareTo(b));
            return idx;
        }

        public static int PlayerPosition(int[] points) => points == null || points.Length == 0 ? 0 : Order(points).IndexOf(0) + 1;

        /// <summary>The top <paramref name="rows"/> of the standings; a player outside them takes the last row.</summary>
        public static List<StandingRow> Standings(int[] points, int rows, int playerTeam)
        {
            var list = new List<StandingRow>();
            if (points == null) return list;
            var order = Order(points);
            StandingRow Row(int k)
            {
                int i = order[k];
                bool me = i == 0;
                var d = me ? default : Roster.Drivers[Math.Min(i - 1, Roster.Drivers.Length - 1)];
                return new StandingRow { position = k + 1, points = points[i], player = me, team = me ? playerTeam : d.team, name = me ? "Du" : d.name, code = me ? "DU" : d.code };
            }
            int shown = Math.Min(rows, order.Count);
            for (int k = 0; k < shown; k++) list.Add(Row(k));
            int pk = order.IndexOf(0);
            if (pk >= shown && shown > 0) list[shown - 1] = Row(pk);
            return list;
        }
    }
}
