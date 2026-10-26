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
                r.points.Add(sa);
                // Gentle S-curve via two offset control points.
                var dir = Vector2.Normalize(sb - sa);
                var side = new Vector2(dir.Y, -dir.X);
                float len = Vector2.Distance(sa, sb);
                for (int i = 1; i < 8; i++)
                {
                    float t = i / 8f;
                    var p = Vector2.Lerp(sa, sb, t) + side * (MathF.Sin(t * MathF.PI * 2f) * len * 0.04f);
                    if (!land.IsLand(p.X, p.Y)) r.points.Add(p);
                }
                r.points.Add(sb);
                routes.Add(r);
            }
            Shuttle("Beşiktaş–Üsküdar", "besiktas_pier", "uskudar_pier", 7.5f, 55f);
            Shuttle("Karaköy–Üsküdar", "karakoy_pier", "uskudar_pier", 7.5f, 70f);
            Shuttle("Eminönü–Üsküdar", "eminonu_pier", "uskudar_pier", 7f, 60f);
            // Channel transit along the mid-line between the shores, south to north.
            if (def.shoreline.Count > 1 && def.farShoreline.Count > 1)
            {
                var north = new MarineRoute { name = "Transit Nord", speed = 5.5f, shuttle = false, vesselKind = 1 };
                var south = new MarineRoute { name = "Transit Süd", speed = 6f, shuttle = false, vesselKind = 1 };
                for (float z = -2600f; z <= 4800f; z += 400f)
                {
                    float xe = TerrainField.ShoreX(def.shoreline, z), xa = TerrainField.ShoreX(def.farShoreline, z);
                    float mid = (xe + xa) * 0.5f, half = MathF.Abs(xa - xe) * 0.5f;
                    if (z < def.farShoreline[0].north) { mid = xe + 900f; half = 700f; }
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

        /// <summary>Moves from a pier (on the coast) towards a target until clear of land by `clear` metres.</summary>
        static Vector2 Offshore(LandModel land, Vector2 pier, Vector2 towards, float clear)
        {
            var dir = Vector2.Normalize(towards - pier);
            for (float s = 0f; s < 600f; s += 5f)
            {
                var p = pier + dir * s;
                if (!land.IsLand(p.X, p.Y) && land.WaterDistanceToCoast(p.X, p.Y) >= clear) return p;
            }
            return pier + dir * 600f;
        }
    }
}
