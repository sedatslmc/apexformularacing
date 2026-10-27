using System;
using System.Collections.Generic;
using System.Numerics;
using Apex.Tracks;

namespace Apex.Environment
{
    /// <summary>
    /// Street furniture, pedestrians and traffic on the OSM street network (<see cref="CityData"/>), all
    /// outside the racing envelope. Deterministic from the data; positions move with <see cref="Advance"/>.
    /// <list type="bullet">
    /// <item>Furniture on the pavements of the roads: benches, bins, bollards, planters, bus shelters on the
    /// main streets.</item>
    /// <item>Pedestrians walk along footways and pavements (1.1-1.6 m/s), turn round at the end of a path or
    /// at the circuit's barriers.</item>
    /// <item>Traffic drives on the right-hand lane of the roads (city speeds 8-14 m/s, buses 7-9), keeps its
    /// distance to the vehicle ahead and turns round where a street ends, including at the closed circuit.
    /// Parked cars line the residential streets.</item>
    /// </list>
    /// No physics: background life only, never on the racing surface.
    /// </summary>
    public sealed class CityLife
    {
        public enum Prop { Bench, Bin, Bollard, Planter, BusShelter, FlagPole }
        public enum Vehicle { Car, Taxi, Van, Bus, Scooter }

        public struct Placed
        {
            public Vector3 position;
            public float yaw;
            public int kind;
            public int variant;
        }

        /// <summary>A walker or vehicle moving back and forth along a path (world points incl. height).</summary>
        public sealed class Mover
        {
            public List<Vector3> path;
            public float[] along;
            public float s, speed;
            public int dir = 1;
            public int kind, variant;
            public float lateral;
            public Vector3 position;
            public float yaw;
            /// <summary>Junctions taken (vehicles); also picks the next exit.</summary>
            public int turns;
            internal Road road;
        }

        /// <summary>A drivable street run with the junctions at its two ends (other runs reachable from there).</summary>
        internal sealed class Road
        {
            public List<Vector3> path;
            public float[] along;
            public float lateral;
            public readonly List<(Road road, float s)>[] exits = { new List<(Road, float)>(), new List<(Road, float)>() };
            public readonly List<Mover> movers = new List<Mover>();
            /// <summary>Traffic light controlling each end (null: none).</summary>
            public readonly Signal[] signal = new Signal[2];
        }

        /// <summary>
        /// A traffic light at one approach of a signalised junction. The junction alternates its two approach groups
        /// (roughly perpendicular axes): green <see cref="GreenTime"/>, amber <see cref="AmberTime"/>, then red while
        /// the other group runs; offsets differ per junction so the city does not switch in step.
        /// </summary>
        public sealed class Signal
        {
            /// <summary>Pole position at the kerb, right of the approaching lane, and the yaw the lamps face (towards the traffic).</summary>
            public Vector3 position;
            public float yaw;
            public int group;
            public float offset;
        }

        public const float GreenTime = 14f, AmberTime = 3f, Cycle = 2f * (GreenTime + AmberTime);
        public const int Red = 0, Amber = 1, Green = 2;
        public readonly List<Signal> signals = new List<Signal>();
        /// <summary>Simulated seconds (drives the signal phases).</summary>
        public float time;
        /// <summary>Most vehicles seen waiting at a red light in one step (for tests).</summary>
        public int maxWaiting;

        public int State(Signal sg)
        {
            float t = (time + sg.offset) % Cycle;
            if (sg.group == 1) t = (t + GreenTime + AmberTime) % Cycle;
            return t < GreenTime ? Green : t < GreenTime + AmberTime ? Amber : Red;
        }

        readonly List<Road> roads = new List<Road>();
        /// <summary>Junctions where a vehicle turned into another street instead of turning round.</summary>
        public int junctionTurns;
        /// <summary>Street ends with at least one connection to another street.</summary>
        public int connectedEnds;
        /// <summary>Distance between a street end and a point of another street that makes them a junction.</summary>
        public const float JunctionReach = 5f;

        public readonly List<Placed> props = new List<Placed>();
        public readonly List<Placed> parked = new List<Placed>();
        public readonly List<Mover> pedestrians = new List<Mover>();
        public readonly List<Mover> vehicles = new List<Mover>();

        /// <summary>Distance everything keeps from the racing envelope.</summary>
        public const float TrackGap = 2f;

        readonly TerrainField field;
        readonly TrackClearance clearance;
        readonly OsmCity city;

        public CityLife(CityData data, TerrainField field, TrackClearance clearance, OsmCity city, int quality, int pedestrianBudget, int vehicleBudget)
        {
            this.field = field;
            this.clearance = clearance;
            this.city = city;
            var rng = new Random(1923);
            var walkPaths = new List<List<Vector3>>();
            var drivePaths = new List<List<Vector3>>();
            var driveLanes = new List<float>();
            var driveMain = new List<bool>();
            var driveHalf = new List<float>();
            foreach (var s in data.streets)
            {
                if (s.bridge) continue;
                bool road = CityData.IsRoad(s.highway);
                float width = s.width > 0f ? MathF.Min(s.width, 24f) : CityData.DefaultWidth(s.highway);
                var pts = Resample(s.points, 3f);
                // Split into runs that stay clear of the circuit (a street crossing it becomes two dead ends).
                foreach (var run in ClearRuns(pts, width * 0.5f + (road ? StreetNetwork.SidewalkWidth : 0f)))
                {
                    if (run.Count < 3) continue;
                    if (road)
                    {
                        if (s.highway != "service") Furnish(run, width, s.highway, rng, quality);
                        if (s.highway == "residential" || s.highway == "living_street") Park(run, width, rng, quality);
                        if (s.highway != "living_street")
                        {
                            // Centreline path; the lane offset (right of the travel direction) is per vehicle.
                            drivePaths.Add(Lift(run, StreetNetwork.Lift));
                            driveLanes.Add(s.oneway ? 0f : width * 0.25f);
                            driveMain.Add(s.highway.StartsWith("primary") || s.highway.StartsWith("secondary") || s.highway.StartsWith("tertiary"));
                            driveHalf.Add(width * 0.5f);
                        }
                        // Pavement walkers on both sides.
                        if (s.highway != "service")
                        {
                            walkPaths.Add(Lift(Offset(run, width * 0.5f + StreetNetwork.SidewalkWidth * 0.5f), StreetNetwork.Lift + StreetNetwork.KerbHeight));
                            walkPaths.Add(Lift(Offset(run, -(width * 0.5f + StreetNetwork.SidewalkWidth * 0.5f)), StreetNetwork.Lift + StreetNetwork.KerbHeight));
                        }
                    }
                    else if (s.highway != "steps") walkPaths.Add(Lift(run, StreetNetwork.Lift + 0.03f));
                }
            }
            Populate(walkPaths, null, pedestrians, pedestrianBudget, 1.1f, 1.6f, 6, rng);
            Populate(drivePaths, driveLanes, vehicles, vehicleBudget, 8f, 14f, 0, rng);
            BuildRoads(drivePaths, driveLanes);
            BuildSignals(driveMain, driveHalf);
            foreach (var v in vehicles)
            {
                // Istanbul mix: a third yellow taxis, some vans, scooters and buses on the bigger roads.
                double r = rng.NextDouble();
                v.kind = (int)(r < 0.34 ? Vehicle.Taxi : r < 0.72 ? Vehicle.Car : r < 0.84 ? Vehicle.Van : r < 0.95 ? Vehicle.Scooter : Vehicle.Bus);
                if (v.kind == (int)Vehicle.Bus) v.speed = 7f + (float)rng.NextDouble() * 2f;
                v.variant = rng.Next(8);
            }
            Advance(0f);
        }

        static List<Vector2> Resample(List<Vector2> pts, float step)
        {
            var r = new List<Vector2> { pts[0] };
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                int n = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(a, b) / step));
                for (int k = 1; k <= n; k++) r.Add(Vector2.Lerp(a, b, k / (float)n));
            }
            return r;
        }

        IEnumerable<List<Vector2>> ClearRuns(List<Vector2> pts, float half)
        {
            var run = new List<Vector2>();
            foreach (var p in pts)
            {
                bool ok = field.InNearArea(p.X, p.Y) && !field.IsWater(p.X, p.Y) && clearance.Margin(p.X, p.Y) > TrackGap + half;
                if (ok) { run.Add(p); continue; }
                if (run.Count > 0) yield return run;
                run = new List<Vector2>();
            }
            if (run.Count > 0) yield return run;
        }

        static Vector2 Dir(List<Vector2> pts, int i)
        {
            var d = pts[Math.Min(pts.Count - 1, i + 1)] - pts[Math.Max(0, i - 1)];
            return d.LengthSquared() > 1e-8f ? Vector2.Normalize(d) : new Vector2(0f, 1f);
        }

        static List<Vector2> Offset(List<Vector2> pts, float lateral)
        {
            var r = new List<Vector2>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
            {
                var d = Dir(pts, i);
                r.Add(pts[i] + new Vector2(d.Y, -d.X) * lateral);
            }
            return r;
        }

        List<Vector3> Lift(List<Vector2> pts, float lift)
        {
            var r = new List<Vector3>(pts.Count);
            foreach (var p in pts) r.Add(new Vector3(p.X, field.Height(p.X, p.Y) + lift, p.Y));
            return r;
        }

        bool Free(Vector2 p, float pad) => clearance.Margin(p.X, p.Y) > TrackGap + pad && (city == null || !city.InsideBuilding(p.X, p.Y, pad));

        void Furnish(List<Vector2> run, float width, string highway, Random rng, int quality)
        {
            float spacing = quality == 0 ? 45f : 28f;
            bool main = highway.StartsWith("primary") || highway.StartsWith("secondary");
            float lateral = width * 0.5f + StreetNetwork.SidewalkWidth * 0.75f;
            float next = spacing * (float)rng.NextDouble(), run0 = 0f, shelterAt = 150f + 150f * (float)rng.NextDouble();
            for (int i = 0; i + 1 < run.Count; i++)
            {
                float seg = Vector2.Distance(run[i], run[i + 1]);
                while (next <= run0 + seg)
                {
                    var d = Dir(run, i);
                    var right = new Vector2(d.Y, -d.X);
                    float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                    var p = run[i] + d * (next - run0) + right * (lateral * side);
                    Prop kind;
                    if (main && next >= shelterAt) { kind = Prop.BusShelter; shelterAt += 300f; }
                    else
                    {
                        double r = rng.NextDouble();
                        kind = r < 0.3 ? Prop.Bench : r < 0.55 ? Prop.Bin : r < 0.85 ? Prop.Bollard : Prop.Planter;
                        // Turkish flags on poles along the main streets (Istanbul flies many, more so on race weekends).
                        if (main && rng.NextDouble() < 0.12) kind = Prop.FlagPole;
                    }
                    if (Free(p, kind == Prop.BusShelter ? 2.5f : kind == Prop.FlagPole ? 2.2f : 0.8f))
                    {
                        // Benches and shelters face the street.
                        float yaw = MathF.Atan2(-right.X * side, -right.Y * side);
                        props.Add(new Placed { position = new Vector3(p.X, field.Height(p.X, p.Y) + StreetNetwork.Lift + StreetNetwork.KerbHeight, p.Y), yaw = yaw, kind = (int)kind, variant = rng.Next(4) });
                    }
                    next += spacing * (0.7f + 0.6f * (float)rng.NextDouble());
                }
                run0 += seg;
            }
        }

        void Park(List<Vector2> run, float width, Random rng, int quality)
        {
            float lateral = width * 0.5f - 1.1f;
            if (lateral < 1.5f) return;
            float step = 5.5f, next = 3f, run0 = 0f;
            for (int i = 0; i + 1 < run.Count; i++)
            {
                float seg = Vector2.Distance(run[i], run[i + 1]);
                while (next <= run0 + seg)
                {
                    if (rng.NextDouble() < (quality == 0 ? 0.3 : 0.55))
                    {
                        var d = Dir(run, i);
                        float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                        var p = run[i] + d * (next - run0) + new Vector2(d.Y, -d.X) * (lateral * side);
                        if (Free(p, 1.5f))
                            parked.Add(new Placed
                            {
                                position = new Vector3(p.X, field.Height(p.X, p.Y) + StreetNetwork.Lift, p.Y),
                                yaw = MathF.Atan2(d.X * side, d.Y * side), kind = rng.NextDouble() < 0.25 ? (int)Vehicle.Taxi : (int)Vehicle.Car, variant = rng.Next(8),
                            });
                    }
                    next += step;
                }
                run0 += seg;
            }
        }

        static void Populate(List<List<Vector3>> paths, List<float> laterals, List<Mover> movers, int budget, float vMin, float vMax, int kinds, Random rng)
        {
            if (paths.Count == 0 || budget <= 0) return;
            float total = 0f;
            var lengths = new float[paths.Count];
            for (int k = 0; k < paths.Count; k++)
            {
                float l = 0f;
                for (int i = 0; i + 1 < paths[k].Count; i++) l += Vector3.Distance(paths[k][i], paths[k][i + 1]);
                lengths[k] = l;
                total += l;
            }
            // Movers spread by path length; at most one per 12 m of path.
            for (int k = 0; k < paths.Count; k++)
            {
                if (lengths[k] < 20f) continue;
                int n = Math.Min((int)(lengths[k] / 12f), (int)MathF.Round(budget * lengths[k] / total + (float)rng.NextDouble() - 0.5f));
                if (n <= 0) continue;
                var along = new float[paths[k].Count];
                for (int i = 1; i < along.Length; i++) along[i] = along[i - 1] + Vector3.Distance(paths[k][i - 1], paths[k][i]);
                for (int m = 0; m < n; m++)
                    movers.Add(new Mover
                    {
                        path = paths[k], along = along, s = (float)rng.NextDouble() * lengths[k], dir = rng.NextDouble() < 0.5 ? 1 : -1,
                        speed = vMin + (float)rng.NextDouble() * (vMax - vMin), kind = kinds > 0 ? rng.Next(kinds) : 0, variant = rng.Next(8), lateral = laterals != null ? laterals[k] : 0f,
                    });
            }
        }

        /// <summary>
        /// Road graph for the traffic: every drive path becomes a road; a road end within <see cref="JunctionReach"/>
        /// of a point of another road is a junction there (OSM ways end at or pass through junctions). Runs on both
        /// sides of the closed circuit are never joined: they are more than the track width apart.
        /// </summary>
        void BuildRoads(List<List<Vector3>> paths, List<float> laterals)
        {
            var byPath = new Dictionary<List<Vector3>, Road>();
            for (int k = 0; k < paths.Count; k++)
            {
                var along = new float[paths[k].Count];
                for (int i = 1; i < along.Length; i++) along[i] = along[i - 1] + Vector3.Distance(paths[k][i - 1], paths[k][i]);
                var r = new Road { path = paths[k], along = along, lateral = laterals[k] };
                roads.Add(r);
                byPath[paths[k]] = r;
            }
            const float cell = 10f;
            var grid = new Dictionary<(int, int), List<(Road, int)>>();
            foreach (var r in roads)
                for (int i = 0; i < r.path.Count; i++)
                {
                    var key = ((int)MathF.Floor(r.path[i].X / cell), (int)MathF.Floor(r.path[i].Z / cell));
                    if (!grid.TryGetValue(key, out var l)) grid[key] = l = new List<(Road, int)>();
                    l.Add((r, i));
                }
            foreach (var r in roads)
                for (int end = 0; end < 2; end++)
                {
                    var p = r.path[end == 0 ? 0 : r.path.Count - 1];
                    var best = new Dictionary<Road, (float d, int i)>();
                    int cx = (int)MathF.Floor(p.X / cell), cz = (int)MathF.Floor(p.Z / cell);
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dz = -1; dz <= 1; dz++)
                            if (grid.TryGetValue((cx + dx, cz + dz), out var l))
                                foreach (var (o, i) in l)
                                {
                                    if (o == r) continue;
                                    float d = MathF.Sqrt((o.path[i].X - p.X) * (o.path[i].X - p.X) + (o.path[i].Z - p.Z) * (o.path[i].Z - p.Z));
                                    if (d < JunctionReach && (!best.TryGetValue(o, out var b) || d < b.d)) best[o] = (d, i);
                                }
                    foreach (var kv in best) r.exits[end].Add((kv.Key, kv.Key.along[kv.Value.i]));
                    if (best.Count > 0) connectedEnds++;
                }
            foreach (var v in vehicles)
            {
                v.road = byPath[v.path];
                v.along = v.road.along;
                v.road.movers.Add(v);
            }
        }

        /// <summary>
        /// Signalised junctions: clusters of connected road ends (within 8 m) that form a crossroads with at least two
        /// main-street approaches (primary, secondary, tertiary), or a T-junction of three main-street approaches. Each approach gets a light at its kerb; approaches along the first one's axis form
        /// group 0, the crossing ones group 1.
        /// </summary>
        void BuildSignals(List<bool> main, List<float> half)
        {
            var ends = new List<(Road road, int end, Vector3 p, Vector3 dir, bool main, float half)>();
            for (int k = 0; k < roads.Count; k++)
            {
                var r = roads[k];
                for (int end = 0; end < 2; end++)
                {
                    if (r.exits[end].Count == 0 || r.path.Count < 2) continue;
                    int i = end == 0 ? 0 : r.path.Count - 1, j = end == 0 ? 1 : r.path.Count - 2;
                    // Direction of travel into the junction.
                    var d = r.path[i] - r.path[j];
                    d.Y = 0f;
                    if (d.LengthSquared() < 1e-6f) continue;
                    ends.Add((r, end, r.path[i], Vector3.Normalize(d), main[k], half[k]));
                }
            }
            var used = new bool[ends.Count];
            int junction = 0;
            for (int a = 0; a < ends.Count; a++)
            {
                if (used[a]) continue;
                var cluster = new List<int> { a };
                used[a] = true;
                for (int b = a + 1; b < ends.Count; b++)
                    if (!used[b] && Vector3.Distance(ends[a].p, ends[b].p) < 8f) { cluster.Add(b); used[b] = true; }
                int mains = cluster.FindAll(c => ends[c].main).Count;
                if (!(cluster.Count >= 4 && mains >= 2) && !(cluster.Count == 3 && mains == 3)) continue;
                var axis = ends[cluster[0]].dir;
                float offset = (junction++ * 7.3f) % Cycle;
                foreach (int c in cluster)
                {
                    var e = ends[c];
                    var right = new Vector3(e.dir.Z, 0f, -e.dir.X);
                    var sg = new Signal
                    {
                        position = e.p - e.dir * 2.5f + right * (e.half + 0.4f),
                        yaw = MathF.Atan2(-e.dir.X, -e.dir.Z),
                        group = MathF.Abs(Vector3.Dot(e.dir, axis)) > 0.7f ? 0 : 1,
                        offset = offset,
                    };
                    if (clearance.Margin(sg.position.X, sg.position.Z) < TrackGap + 0.5f) continue;
                    e.road.signal[e.end] = sg;
                    signals.Add(sg);
                }
            }
        }

        readonly List<(Mover, Road)> transfers = new List<(Mover, Road)>();

        /// <summary>
        /// Moves everyone by dt seconds. Vehicles keep 9 m to the one ahead on the same road and direction; at a
        /// road end they turn into a connected street (exits taken in turn), or turn round at a dead end.
        /// </summary>
        public void Advance(float dt)
        {
            time += dt;
            int waiting = 0;
            foreach (var p in pedestrians) Step(p, p.speed * dt);
            foreach (var r in roads)
            {
                var l = r.movers;
                for (int i = 0; i < l.Count; i++)
                {
                    var v = l[i];
                    float move = v.speed * dt;
                    // Nearest vehicle ahead in the same direction (roads carry a handful of vehicles).
                    float gap = float.MaxValue;
                    for (int k = 0; k < l.Count; k++)
                    {
                        var o = l[k];
                        if (k == i || o.dir != v.dir) continue;
                        float ahead = (o.s - v.s) * v.dir;
                        if (ahead > 0f && ahead < gap) gap = ahead;
                    }
                    if (gap < float.MaxValue) move = MathF.Max(0f, MathF.Min(move, gap - 9f));
                    // Stop line 3 m before a red (or amber, if still far enough to stop) light at the end ahead.
                    var sg = r.signal[v.dir > 0 ? 1 : 0];
                    if (sg != null)
                    {
                        int st = State(sg);
                        float toEnd = v.dir > 0 ? r.along[r.along.Length - 1] - v.s : v.s;
                        if (st == Red || (st == Amber && toEnd > 8f))
                        {
                            float allowed = MathF.Max(0f, toEnd - 3f);
                            if (toEnd > 2.5f && move > allowed) { move = allowed; if (allowed < 0.5f) waiting++; }
                        }
                    }
                    Drive(v, move);
                }
            }
            maxWaiting = Math.Max(maxWaiting, waiting);
            foreach (var (v, from) in transfers)
            {
                from.movers.Remove(v);
                v.road.movers.Add(v);
            }
            transfers.Clear();
        }

        void Drive(Mover v, float move)
        {
            var r = v.road;
            float len = r.along[r.along.Length - 1];
            float ns = v.s + v.dir * move;
            int end = ns > len ? 1 : ns < 0f ? 0 : -1;
            if (end < 0 || r.exits[end].Count == 0) { Step(v, move); return; }
            var (next, s2) = r.exits[end][(v.turns + v.variant) % r.exits[end].Count];
            v.turns++;
            junctionTurns++;
            float rest = end == 1 ? ns - len : -ns;
            float len2 = next.along[next.along.Length - 1];
            // Leave the junction: along the new street away from it; mid-street junctions alternate left and right.
            int dir = s2 < 1f ? 1 : s2 > len2 - 1f ? -1 : (v.turns % 2 == 0 ? 1 : -1);
            transfers.Add((v, r));
            v.road = next;
            v.path = next.path;
            v.along = next.along;
            v.lateral = next.lateral;
            v.dir = dir;
            v.s = s2;
            Step(v, rest);
        }

        static void Step(Mover m, float move)
        {
            float len = m.along[m.along.Length - 1];
            m.s += m.dir * move;
            if (m.s > len) { m.s = 2f * len - m.s; m.dir = -1; }
            if (m.s < 0f) { m.s = -m.s; m.dir = 1; }
            m.s = Math.Clamp(m.s, 0f, len);
            int i = Array.BinarySearch(m.along, m.s);
            if (i < 0) i = ~i - 1;
            i = Math.Clamp(i, 0, m.along.Length - 2);
            float t = (m.s - m.along[i]) / MathF.Max(1e-4f, m.along[i + 1] - m.along[i]);
            var a = m.path[i]; var b = m.path[i + 1];
            var dir = Vector3.Normalize(b - a) * m.dir;
            var side = new Vector3(dir.Z, 0f, -dir.X);
            m.position = Vector3.Lerp(a, b, t) + side * m.lateral;
            m.yaw = MathF.Atan2(dir.X, dir.Z);
        }
    }
}
