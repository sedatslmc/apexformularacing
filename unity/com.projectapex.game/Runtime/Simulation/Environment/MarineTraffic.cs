using System;
using System.Collections.Generic;
using System.Numerics;
using Apex.Core;
using Apex.Tracks;

namespace Apex.Environment
{
    /// <summary>
    /// Ship movements on the strait: commuter ferries shuttle between real piers
    /// (Beşiktaş–Üsküdar, Karaköy–Üsküdar, Eminönü–Üsküdar) with a dwell at each end;
    /// freighters transit along the channel mid-line and re-enter at the other end.
    /// Pure kinematics, sampled by time; routes keep to the water.
    /// </summary>
    public sealed class MarineRoute
    {
        public string name = "";
        public readonly List<Vector2> points = new List<Vector2>();
        public float speed = 7f;
        /// <summary>Dwell at each end (s) for shuttles; transits loop without returning.</summary>
        public float dwell = 50f;
        public bool shuttle = true;
        public int vesselKind;

        float[] cumulative;

        public float Length
        {
            get
            {
                Prepare();
                return cumulative[cumulative.Length - 1];
            }
        }

        void Prepare()
        {
            if (cumulative != null && cumulative.Length == points.Count) return;
            cumulative = new float[points.Count];
            for (int i = 1; i < points.Count; i++) cumulative[i] = cumulative[i - 1] + Vector2.Distance(points[i - 1], points[i]);
        }

        public float CycleTime => shuttle ? 2f * (Length / speed + dwell) : Length / speed;

        Vector2 At(float s, out Vector2 tangent)
        {
            Prepare();
            s = ApexMath.Clamp(s, 0f, Length);
            int i = 1;
            while (i < points.Count - 1 && cumulative[i] < s) i++;
            float seg = MathF.Max(cumulative[i] - cumulative[i - 1], 1e-3f);
            float t = (s - cumulative[i - 1]) / seg;
            tangent = Vector2.Normalize(points[i] - points[i - 1]);
            return Vector2.Lerp(points[i - 1], points[i], t);
        }

        /// <summary>Position, heading (rad, 0 = +z, clockwise) and speed at time t (offset spreads vessels).</summary>
        public void Sample(double time, float offset, out Vector2 position, out float heading, out float currentSpeed)
        {
            float L = Length, travel = L / speed;
            float c = (float)ApexMath.PosMod((float)(time + offset), CycleTime);
            Vector2 tan;
            if (!shuttle)
            {
                position = At(c * speed, out tan);
                currentSpeed = speed;
            }
            else
            {
                bool back = c >= travel + dwell;
                float local = back ? c - travel - dwell : c;
                float s;
                if (local >= travel) { s = L; currentSpeed = 0f; }
                else
                {
                    // Ease in/out over the first and last 15 % (manoeuvring at the piers).
                    float u = local / travel;
                    float eased = u * u * (3f - 2f * u);
                    float blend = ApexMath.Clamp01(MathF.Min(u, 1f - u) / 0.15f);
                    float k = ApexMath.Lerp(eased, u, blend);
                    s = k * L;
                    currentSpeed = speed * ApexMath.Lerp(0.3f, 1f, blend);
                }
                if (back) s = L - s;
                position = At(s, out tan);
                if (back) tan = -tan;
            }
            heading = MathF.Atan2(tan.X, tan.Y);
        }

        /// <summary>Routes for the Bosphorus circuit from its landmarks and shorelines.</summary>
        public static List<MarineRoute> ForCircuit(CircuitDefinition def, LandModel land)
        {
            var routes = new List<MarineRoute>();
            if (!land.HasLand) return routes;
            Vector2? P(string id)
            {
                var l = def.FindLandmark(id);
                return l == null ? (Vector2?)null : new Vector2(l.east, l.north);
            }
            void Shuttle(string name, string a, string b, float speed, float dwell)
            {
                var pa = P(a); var pb = P(b);
                if (pa == null || pb == null) return;
                var r = new MarineRoute { name = name, speed = speed, dwell = dwell, shuttle = true, vesselKind = 0 };
                var sa = Offshore(land, pa.Value, pb.Value, 30f);
                var sb = Offshore(land, pb.Value, pa.Value, 30f);
                // Gentle S-curve; points over land are pushed sideways onto open water.
                var dir = Vector2.Normalize(sb - sa);
                var side = new Vector2(dir.Y, -dir.X);
                float len = Vector2.Distance(sa, sb);
                r.points.Add(sa);
                int steps = Math.Max(8, (int)(len / 120f));
                for (int i = 1; i < steps; i++)
                {
                    float t = i / (float)steps;
                    var p = Vector2.Lerp(sa, sb, t) + side * (MathF.Sin(t * MathF.PI * 2f) * len * 0.04f);
                    var w = ToWater(land, p, side, 40f);
                    if (w.HasValue) r.points.Add(w.Value);
                }
                r.points.Add(sb);
                routes.Add(r);
            }
            Shuttle("Beşiktaş–Üsküdar", "besiktas_pier", "uskudar_pier", 7.5f, 55f);
            Shuttle("Karaköy–Üsküdar", "karakoy_pier", "uskudar_pier", 7.5f, 70f);
            Shuttle("Eminönü–Üsküdar", "eminonu_pier", "uskudar_pier", 7f, 60f);
            // Channel transit south to north: on each cross-section the open-water span of the strait (the water run
            // next to the previous one, from the land outlines; the shorelines only cover the circuit's stretch).
            if (def.shoreline.Count > 1)
            {
                var north = new MarineRoute { name = "Transit Nord", speed = 5.5f, shuttle = false, vesselKind = 1 };
                var south = new MarineRoute { name = "Transit Süd", speed = 6f, shuttle = false, vesselKind = 1 };
                float guess = TerrainField.ShoreX(def.shoreline, -900f) + 700f;
                for (float z = -2600f; z <= 4800f; z += 200f)
                {
                    if (!Channel(land, z, guess, out float xe, out float xa)) continue;
                    float mid = (xe + xa) * 0.5f, half = (xa - xe) * 0.5f;
                    guess = mid;
                    var pn = new Vector2(mid - half * 0.2f, z);
                    var ps = new Vector2(mid + half * 0.25f, z);
                    if (!land.IsLand(pn.X, pn.Y)) north.points.Add(pn);
                    if (!land.IsLand(ps.X, ps.Y)) south.points.Insert(0, ps);
                }
                if (north.points.Count > 1) routes.Add(north);
                if (south.points.Count > 1) routes.Add(south);
            }
            return routes;
        }

        /// <summary>
        /// Water span of the strait on the cross-section z: the run of water samples (20 m) that contains `guess`,
        /// or the nearest one to it, limited to 1.2 km either side of the guess.
        /// </summary>
        static bool Channel(LandModel land, float z, float guess, out float west, out float east)
        {
            west = east = 0f;
            float bestScore = float.MaxValue;
            float runStart = float.NaN;
            for (float x = guess - 1200f; x <= guess + 1220f; x += 20f)
            {
                bool water = x <= guess + 1200f && !land.IsLand(x, z);
                if (water && float.IsNaN(runStart)) runStart = x;
                if (!water && !float.IsNaN(runStart))
                {
                    float a = runStart, b = x - 20f;
                    float score = guess < a ? a - guess : guess > b ? guess - b : 0f;
                    if (b - a >= 200f && score < bestScore) { bestScore = score; west = a; east = b; }
                    runStart = float.NaN;
                }
            }
            return bestScore < float.MaxValue;
        }

        /// <summary>
        /// Nearest open-water point from a pier (on the coast), searching a fan of directions
        /// around the target direction, at least `clear` metres from land.
        /// </summary>
        static Vector2 Offshore(LandModel land, Vector2 pier, Vector2 towards, float clear)
        {
            var dir = Vector2.Normalize(towards - pier);
            for (float s = 0f; s < 900f; s += 10f)
                foreach (float deg in new[] { 0f, 25f, -25f, 50f, -50f, 75f, -75f, 100f, -100f, 130f, -130f })
                {
                    float a = deg * MathF.PI / 180f;
                    var d = new Vector2(dir.X * MathF.Cos(a) - dir.Y * MathF.Sin(a), dir.X * MathF.Sin(a) + dir.Y * MathF.Cos(a));
                    var p = pier + d * s;
                    if (!land.IsLand(p.X, p.Y) && land.WaterDistanceToCoast(p.X, p.Y) >= clear) return p;
                }
            return pier;
        }

        /// <summary>The point itself if it is on open water, else the nearest water point sideways (or null).</summary>
        static Vector2? ToWater(LandModel land, Vector2 p, Vector2 side, float clear)
        {
            for (float o = 0f; o < 1500f; o += 20f)
                foreach (float sgn in new[] { 1f, -1f })
                {
                    var q = p + side * (o * sgn);
                    if (!land.IsLand(q.X, q.Y) && land.WaterDistanceToCoast(q.X, q.Y) >= clear) return q;
                    if (o == 0f) break;
                }
            return null;
        }
    }
}
