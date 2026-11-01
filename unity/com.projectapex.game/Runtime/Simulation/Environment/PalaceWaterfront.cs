using System;
using System.Collections.Generic;
using System.Numerics;

namespace Apex.Environment
{
    /// <summary>
    /// The Bosphorus front of Dolmabahçe Palace (docs/ISTANBUL_OVERHAUL.md, Phase 3): the palace quay – white marble
    /// coping and an ornamental iron railing on a marble plinth instead of the public promenade – and the five sea
    /// gates (yalı kapıları) for arrivals by water.
    ///
    /// Sources: the palace stands on a walled quay about 600 m long; five gates open to the sea, the one on the axis of
    /// the ceremonial hall (Muayede Salonu) larger and richer than the other four. That main gate is the OSM node
    /// "Dolmabahçe Sarayı Deniz Kapısı" (node 13084855510, ODbL). The positions of the four smaller gates are not in
    /// OSM; they are placed on the axes of the Selamlık and Harem wings and at the two ends of the palace front
    /// (approximation, marked as such in the docs).
    /// </summary>
    public sealed class PalaceWaterfront
    {
        public struct Gate
        {
            public Vector2 position;
            /// <summary>Unit vector from the gate towards the water.</summary>
            public Vector2 seaward;
            public bool main;
            public string name;
            /// <summary>True for the OSM-located main gate, false for the approximated ones.</summary>
            public bool surveyed;
        }

        /// <summary>OSM node 13084855510 "Dolmabahçe Sarayı Deniz Kapısı" in the game frame.</summary>
        public static readonly Vector2 MainSeaGate = new Vector2(-78.8f, -78.7f);
        /// <summary>Quay pieces within this distance of a palace wing footprint belong to the palace front (m).</summary>
        public const float FrontageReach = 75f;
        /// <summary>Strip behind the palace quay kept free of trees, so the facade shows from the water (m).</summary>
        public const float ForecourtDepth = 55f;
        public const float MinGateSpacing = 30f;
        /// <summary>Half widths of the gates' portals incl. plinth (LandmarkGeometry.SeaGate: opening + piers + 0.3 m).</summary>
        public const float MainHalfWidth = 4.3f, GateHalfWidth = 3.05f;

        readonly List<List<Vector2>> wings = new List<List<Vector2>>();
        /// <summary>Dolmabahçe's wings only (the sea gates belong to Dolmabahçe).</summary>
        readonly List<List<Vector2>> dolmabahce = new List<List<Vector2>>();
        public readonly List<Gate> gates = new List<Gate>();

        /// <summary>The Bosphorus-side wings of Dolmabahçe (Selamlık / Mabeyn, ceremonial hall, Harem) from the OSM city.</summary>
        public static bool IsWaterfrontWing(string name) =>
            name != null && name.StartsWith("Dolmabahçe Sarayı") && (name.Contains("Selamlık") || name.Contains("Muayede") || name.Contains("Harem"));

        /// <summary>
        /// Çırağan Palace (1871, rebuilt 1989) and its hotel wing on the same marble quay: a palace quay with the
        /// balustrade, but no sea gates of its own in the model (none are recorded in OSM).
        /// </summary>
        public static bool IsCiraganWing(string name) => name == "Çırağan Sarayı" || name == "Çırağan Sarayı Kempinski";

        public PalaceWaterfront(CityData data)
        {
            foreach (var b in data.buildings)
            {
                if (IsWaterfrontWing(b.name)) { wings.Add(b.outline); dolmabahce.Add(b.outline); }
                else if (IsCiraganWing(b.name)) wings.Add(b.outline);
            }
        }

        public bool HasPalace => wings.Count > 0;

        static float DistanceToOutline(List<Vector2> o, Vector2 p)
        {
            if (Polygon.Contains(o, p.X, p.Y)) return 0f;
            float best = float.MaxValue;
            for (int i = 0; i < o.Count; i++)
            {
                var a = o[i]; var b = o[(i + 1) % o.Count];
                var ab = b - a;
                float t = ab.LengthSquared() > 1e-6f ? Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0f, 1f) : 0f;
                best = MathF.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        /// <summary>Distance to the nearest waterfront wing of the palace (m).</summary>
        public float WingDistance(float x, float z)
        {
            float best = float.MaxValue;
            var p = new Vector2(x, z);
            foreach (var w in wings) best = MathF.Min(best, DistanceToOutline(w, p));
            return best;
        }

        /// <summary>A quay point in front of the palace.</summary>
        public bool OnFrontage(float x, float z) => HasPalace && WingDistance(x, z) <= FrontageReach;

        /// <summary>Between the palace and its quay: kept open (lawn and paving) so the facade shows from the water.</summary>
        public bool InForecourt(float x, float z, float coastDistance) =>
            HasPalace && coastDistance < ForecourtDepth && WingDistance(x, z) < ForecourtDepth;

        /// <summary>Places the five sea gates on the palace quay pieces of a waterfront.</summary>
        public void PlaceGates(Waterfront wf)
        {
            gates.Clear();
            var front = new List<Waterfront.Segment>();
            foreach (var s in wf.segments)
            {
                if (!s.palace) continue;
                var m = (s.a + s.b) * 0.5f;
                float d = float.MaxValue;
                foreach (var w in dolmabahce) d = MathF.Min(d, DistanceToOutline(w, m));
                if (d <= FrontageReach) front.Add(s);
            }
            if (front.Count == 0) return;
            // Target points: the main gate (OSM), the wing axes, the two ends of the palace front.
            var targets = new List<(Vector2 p, bool main, string name, bool surveyed)> { (MainSeaGate, true, "Deniz Kapısı (Muayede)", true) };
            foreach (var w in dolmabahce)
            {
                var c = Vector2.Zero;
                foreach (var q in w) c += q;
                c /= w.Count;
                targets.Add((c, false, "Yalı Kapısı", false));
            }
            // Ends: the frontage pieces farthest apart along the quay.
            Waterfront.Segment e0 = front[0], e1 = front[0];
            float far = -1f;
            foreach (var a in front)
                foreach (var b in front)
                {
                    float d = Vector2.DistanceSquared(a.a, b.a);
                    if (d > far) { far = d; e0 = a; e1 = b; }
                }
            targets.Add(((e0.a + e0.b) * 0.5f, false, "Yalı Kapısı", false));
            targets.Add(((e1.a + e1.b) * 0.5f, false, "Yalı Kapısı", false));
            foreach (var t in targets)
            {
                // Nearest palace quay piece; the gate stands on its waterline, facing the sea.
                Waterfront.Segment best = front[0];
                float bd = float.MaxValue;
                foreach (var s in front)
                {
                    float d = Vector2.Distance((s.a + s.b) * 0.5f, t.p);
                    if (d < bd) { bd = d; best = s; }
                }
                var pos = (best.a + best.b) * 0.5f + best.inland * 0.3f;
                // The ends sit a few metres inside the frontage; skip a gate too close to one already placed.
                bool clash = false;
                foreach (var g in gates) if (Vector2.Distance(g.position, pos) < MinGateSpacing) clash = true;
                if (clash && !t.main) continue;
                gates.Add(new Gate { position = pos, seaward = -best.inland, main = t.main, name = t.name, surveyed = t.surveyed });
            }
            // Keep five: the main gate and the four farthest apart from it and each other (openings follow below).
            while (gates.Count > 5)
            {
                int drop = -1; float near = float.MaxValue;
                for (int i = 0; i < gates.Count; i++)
                {
                    if (gates[i].main) continue;
                    for (int j = 0; j < gates.Count; j++)
                        if (i != j && Vector2.Distance(gates[i].position, gates[j].position) < near) { near = Vector2.Distance(gates[i].position, gates[j].position); drop = i; }
                }
                gates.RemoveAt(drop);
            }
            wf.openings.Clear();
            foreach (var g in gates) wf.openings.Add((g.position - g.seaward * 0.3f, g.main ? MainHalfWidth : GateHalfWidth));
        }
    }
}
