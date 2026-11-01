using System.Collections.Generic;
using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// Original, simplified silhouette models of the Istanbul landmarks around the
    /// Bosphorus GP (no GameObjects; per-material builders). Dimensions follow
    /// published figures where known (bridge: 1,074 m main span, 165 m towers, 64 m
    /// clearance); everything else is an approximation for recognisable silhouettes
    /// at racing distances. These are placeholders, not photorealistic hero assets.
    /// Local frame: origin at the base, +y up, +z along the building's main axis.
    /// </summary>
    public sealed partial class LandmarkGeometry
    {
        public const string Stone = "Stone";
        public const string Marble = "Marble";
        public const string Lead = "Lead";
        public const string Steel = "BridgeSteel";
        public const string Cable = "BridgeCable";
        public const string Glass = "Glass";
        public const string HagiaPlaster = "HagiaPlaster";
        public const string Hull = "FerryHull";
        public const string Funnel = "FerryFunnel";
        public const string Rock = "Rock";
        public const string Roof = "Roof";

        public readonly Dictionary<string, MeshBuilder> parts = new Dictionary<string, MeshBuilder>();
        readonly int detail;

        /// <summary>detail: 0 = near (full), 1 = far (fewer segments).</summary>
        public LandmarkGeometry(int detail = 0)
        {
            this.detail = detail;
        }

        public MeshBuilder B(string mat)
        {
            if (!parts.TryGetValue(mat, out var mb)) parts[mat] = mb = new MeshBuilder();
            return mb;
        }

        int Seg(int near) => detail == 0 ? near : Mathf.Max(6, near / 2);

        /// <summary>Solid of revolution around +y; profile = (height, radius) pairs from bottom to top.</summary>
        public static void VerticalLathe(MeshBuilder mb, Vector3 centre, IList<Vector2> heightRadius, int segments)
        {
            int before = mb.VertexCount;
            // MeshKit lathes around x with the outside to the left of (x, r); (h, r) bottom->top
            // keeps the outside facing out after the rotation below.
            mb.Lathe(heightRadius, segments);
            // (x, y, z) -> (y, x, -z): proper rotation (determinant +1) taking the lathe axis to +y.
            mb.TransformLast(mb.VertexCount - before, v => centre + new Vector3(v.y, v.x, -v.z));
        }

        static Vector2[] Dome(float radius, float baseH, int steps, float pointiness = 0f)
        {
            var p = new List<Vector2>();
            for (int i = 0; i <= steps; i++)
            {
                float a = i / (float)steps * Mathf.PI * 0.5f;
                float r = radius * Mathf.Cos(a);
                float h = baseH + radius * Mathf.Sin(a) * (1f + pointiness);
                p.Add(new Vector2(h, Mathf.Max(r, 0.001f)));
            }
            return p.ToArray();
        }

        // --- 15 July Martyrs Bridge ------------------------------------------------------------

        /// <summary>
        /// Suspension bridge between two ground points (world space). Side spans 231 m
        /// (European) and 255 m (Asian), main span 1,074 m, towers 165 m above the water,
        /// deck soffit 64 m above the water, 33 m wide box girder.
        /// </summary>
        public void Bridge(Vector3 euEnd, Vector3 asEnd, float waterY)
        {
            Vector3 flat = asEnd - euEnd;
            flat.y = 0f;
            float total = flat.magnitude;
            Vector3 dir = flat / total;
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            float scale = total / 1560f;
            float tEu = 231f * scale, tAs = total - 255f * scale;
            float deckY = waterY + 64f + 1.5f;
            const float halfWidth = 16.7f;
            Vector3 P(float t, float y, float lateral) => new Vector3(euEnd.x, 0f, euEnd.z) + dir * t + side * lateral + Vector3.up * y;
            // Deck: aerodynamic box girder, gently cambered towards mid-span.
            var deck = new List<Vector3[]>();
            int n = detail == 0 ? 60 : 24;
            for (int i = 0; i <= n; i++)
            {
                float t = total * i / n;
                float camber = 4f * Mathf.Sin(Mathf.PI * i / n);
                float y = deckY + camber;
                deck.Add(new[]
                {
                    P(t, y - 1.5f, -halfWidth * 0.8f), P(t, y, -halfWidth), P(t, y + 0.4f, 0f), P(t, y, halfWidth), P(t, y - 1.5f, halfWidth * 0.8f), P(t, y - 3f, 0f),
                });
            }
            B(Steel).RingLoft(deck, true, true);
            // Towers: two tapered box legs with three cross beams.
            foreach (float t in new[] { tEu, tAs })
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    var legRings = new List<Vector3[]>();
                    foreach (var (y, w) in new[] { (waterY - 2f, 3.4f), (deckY, 3f), (waterY + 165f, 2.4f) })
                    {
                        Vector3 c = P(t, y, s * (halfWidth + 1.5f));
                        legRings.Add(new[] { c + dir * w + side * w * 0.6f, c - dir * w + side * w * 0.6f, c - dir * w - side * w * 0.6f, c + dir * w - side * w * 0.6f });
                    }
                    B(Steel).RingLoft(legRings, true, true);
                }
                foreach (float y in new[] { deckY - 6f, deckY + 45f, waterY + 160f })
                {
                    Vector3 c = P(t, y, 0f);
                    var beam = new List<Vector3[]>();
                    foreach (float s in new[] { -1f, 1f })
                    {
                        Vector3 e = c + side * s * (halfWidth + 1.5f);
                        beam.Add(new[] { e + dir * 2f + Vector3.up * 2.5f, e - dir * 2f + Vector3.up * 2.5f, e - dir * 2f - Vector3.up * 2.5f, e + dir * 2f - Vector3.up * 2.5f });
                    }
                    B(Steel).RingLoft(beam, true, true);
                }
                // Tower foundation where it meets the ground or water.
                B(Stone).Box(P(t, waterY - 1f, 0f), new Vector3(46f, 6f, 14f), Quaternion.LookRotation(side));
            }
            // Main cables: parabolic sag in the main span, straight backstays to the anchorages.
            float topY = waterY + 165f;
            float lowY = deckY + 4f;
            foreach (float s in new[] { -1f, 1f })
            {
                var path = new List<Vector3>();
                path.Add(P(0f, euEnd.y + 6f, s * (halfWidth + 1.5f)));
                path.Add(P(tEu, topY, s * (halfWidth + 1.5f)));
                int m = detail == 0 ? 40 : 14;
                for (int i = 1; i < m; i++)
                {
                    float u = i / (float)m;
                    float t = Mathf.Lerp(tEu, tAs, u);
                    float y = lowY + (topY - lowY) * (2f * u - 1f) * (2f * u - 1f);
                    path.Add(P(t, y, s * (halfWidth + 1.5f)));
                }
                path.Add(P(tAs, topY, s * (halfWidth + 1.5f)));
                path.Add(P(total, asEnd.y + 6f, s * (halfWidth + 1.5f)));
                B(Cable).TubePath(path, detail == 0 ? 0.7f : 1.1f, detail == 0 ? 6 : 4, true);
                // Inclined hangers (the bridge's zig-zag pattern, pairs meeting at the deck), near view only.
                if (detail != 0) continue;
                foreach (var (deckPt, cablePt) in Hangers(total, tEu, tAs, deckY, topY, lowY, P, s * (halfWidth + 1.5f)))
                    B(Cable).Tube(deckPt, cablePt, 0.18f, 4, false);
            }
            // Cable anchorages (concrete blocks where the backstays meet the ground).
            foreach (var (t, y) in new[] { (0f, euEnd.y), (total, asEnd.y) })
                B(Stone).Box(P(t, y + 3f, 0f), new Vector3(46f, 14f, 34f), Quaternion.LookRotation(dir));
            if (detail != 0) return;
            // Saddle housings on the tower tops.
            foreach (float t in new[] { tEu, tAs })
                foreach (float s in new[] { -1f, 1f })
                    B(Steel).Box(P(t, topY + 1.6f, s * (halfWidth + 1.5f)), new Vector3(4.2f, 3.2f, 7f), Quaternion.LookRotation(dir));
            // Deck furniture: three lanes each way with dashed markings, edge lines, a median barrier, walkway
            // railings and lamp posts on the median every 36 m (lamp heads glow at night).
            float Surface(float t, float lateral) => deckY + 4f * Mathf.Sin(Mathf.PI * t / total) + 0.4f * (1f - Mathf.Abs(lateral) / halfWidth) + 0.03f;
            var road = B(BridgeRoad);
            var line = B("Line");
            const float step = 12f;
            for (float t = 0f; t < total - 0.1f; t += step)
            {
                float t1 = Mathf.Min(total, t + step);
                // Carriageway surface (both directions) just above the girder top.
                foreach (float s in new[] { -1f, 1f })
                {
                    float a = s * 0.9f, b = s * 11.6f;
                    road.QuadFacing(P(t, Surface(t, a), a), P(t1, Surface(t1, a), a), P(t1, Surface(t1, b), b), P(t, Surface(t, b), b), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector3.up);
                    foreach (float lane in new[] { 4.5f, 8.1f, 11.4f })
                    {
                        bool solid = lane > 11f;
                        float tt1 = solid ? t1 : t + 4.5f;
                        float l = s * lane;
                        line.QuadFacing(P(t, Surface(t, l) + 0.01f, l - 0.08f), P(tt1, Surface(tt1, l) + 0.01f, l - 0.08f), P(tt1, Surface(tt1, l) + 0.01f, l + 0.08f), P(t, Surface(t, l) + 0.01f, l + 0.08f), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector3.up);
                    }
                    // Walkway railing: top rail along the step, one post per step.
                    float r = s * (halfWidth - 0.4f);
                    B(Steel).Tube(P(t, Surface(t, r) + 1.1f, r), P(t1, Surface(t1, r) + 1.1f, r), 0.05f, 4, false);
                    B(Steel).Tube(P(t, Surface(t, r) + 0.5f, r), P(t1, Surface(t1, r) + 0.5f, r), 0.03f, 4, false);
                    B(Steel).Box(P(t, Surface(t, r) + 0.55f, r), new Vector3(0.08f, 1.1f, 0.08f));
                    // Traffic barrier between carriageway and walkway.
                    float g = s * 12.1f;
                    B(Steel).Tube(P(t, Surface(t, g) + 0.7f, g), P(t1, Surface(t1, g) + 0.7f, g), 0.12f, 4, false);
                }
                B(EnvironmentGeometry.Concrete).Box(P(t + step * 0.5f, Surface(t + step * 0.5f, 0f) + 0.45f, 0f), new Vector3(0.6f, 0.9f, step), Quaternion.LookRotation(dir));
                if (Mathf.Repeat(t, 36f) < 0.1f)
                {
                    var foot = P(t, Surface(t, 0f) + 0.9f, 0f);
                    B(Steel).Tube(foot, foot + Vector3.up * 11f, 0.12f, 6, true);
                    foreach (float s in new[] { -1f, 1f })
                    {
                        var arm = foot + Vector3.up * 11f + side * s * 2.2f;
                        B(Steel).Tube(foot + Vector3.up * 10.6f, arm, 0.07f, 4, false);
                        B(EnvironmentGeometry.LampHead).Box(arm - Vector3.up * 0.15f, new Vector3(0.9f, 0.25f, 0.45f), Quaternion.LookRotation(dir));
                    }
                }
            }
        }

        public const string BridgeRoad = "BridgeRoad";

        /// <summary>
        /// Hanger end points on one cable plane: every 18 m along the main span, inclined so that consecutive
        /// hangers meet at the deck (V pairs), like the bridge's real zig-zag hangers.
        /// </summary>
        static List<(Vector3 deck, Vector3 cable)> Hangers(float total, float tEu, float tAs, float deckY, float topY, float lowY, System.Func<float, float, float, Vector3> P, float lateral)
        {
            var r = new List<(Vector3, Vector3)>();
            int k = 0;
            for (float t = tEu + 18f; t < tAs - 9f; t += 18f, k++)
            {
                float u = (t - tEu) / (tAs - tEu);
                float y = lowY + (topY - lowY) * (2f * u - 1f) * (2f * u - 1f);
                float td = Mathf.Clamp(t + (k % 2 == 0 ? 9f : -9f), tEu + 4f, tAs - 4f);
                float camber = 4f * Mathf.Sin(Mathf.PI * td / total);
                r.Add((P(td, deckY + camber + 0.5f, lateral), P(t, y, lateral)));
            }
            return r;
        }

        /// <summary>
        /// Architectural night lighting of the 15 July Martyrs Bridge in the national colours, laid on the same
        /// parametric frame as <see cref="Bridge"/>: red LED lines along both main cables (the silhouette), white
        /// on the hangers, on the inner edges of the tower legs and on the top cross beams. Not a uniformly
        /// glowing bridge: the steel stays dark between the lines. Returns the four tower-wash light positions
        /// (foot of each leg at deck level) for real spot lights.
        /// </summary>
        public static List<Vector3> BridgeIllumination(Vector3 euEnd, Vector3 asEnd, float waterY, MeshBuilder red, MeshBuilder white)
        {
            Vector3 flat = asEnd - euEnd;
            flat.y = 0f;
            float total = flat.magnitude;
            Vector3 dir = flat / total;
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            float scale = total / 1560f;
            float tEu = 231f * scale, tAs = total - 255f * scale;
            float deckY = waterY + 64f + 1.5f;
            const float halfWidth = 16.7f;
            float topY = waterY + 165f, lowY = deckY + 4f;
            Vector3 P(float t, float y, float lateral) => new Vector3(euEnd.x, 0f, euEnd.z) + dir * t + side * lateral + Vector3.up * y;
            var wash = new List<Vector3>();
            foreach (float s in new[] { -1f, 1f })
            {
                float lat = s * (halfWidth + 1.5f);
                // Red line on the cable (slightly larger than the 0.7 m cable so it reads at night).
                var path = new List<Vector3> { P(0f, euEnd.y + 6f, lat), P(tEu, topY, lat) };
                for (int i = 1; i < 40; i++)
                {
                    float u = i / 40f;
                    path.Add(P(Mathf.Lerp(tEu, tAs, u), lowY + (topY - lowY) * (2f * u - 1f) * (2f * u - 1f), lat));
                }
                path.Add(P(tAs, topY, lat));
                path.Add(P(total, asEnd.y + 6f, lat));
                red.TubePath(path, 0.85f, 6, true);
                // White light on the inclined hangers (same end points as the steel hangers).
                foreach (var (deckPt, cablePt) in Hangers(total, tEu, tAs, deckY, topY, lowY, P, lat))
                    white.Tube(deckPt, cablePt, 0.28f, 4, false);
                // White light bands on the inner edges of each tower leg, from the deck to the top.
                foreach (float t in new[] { tEu, tAs })
                {
                    // On the leg's inner corners (the leg tapers from 3 x 1.8 m half size at the deck to 2.4 x 1.44 m at the top).
                    foreach (float e in new[] { -1f, 1f })
                    {
                        var a = P(t + e * 3.25f, deckY + 2f, lat - s * 2.0f);
                        var b = P(t + e * 2.65f, topY - 3f, lat - s * 1.65f);
                        white.Tube(a, b, 0.35f, 4, false);
                    }
                    wash.Add(P(t, deckY + 1.5f, lat - s * 4f));
                }
            }
            // White crown on the top cross beams.
            foreach (float t in new[] { tEu, tAs })
                white.Tube(P(t, waterY + 160f + 2.7f, -(halfWidth + 1.5f)), P(t, waterY + 160f + 2.7f, halfWidth + 1.5f), 0.45f, 6, true);
            return wash;
        }

        /// <summary>Points along the bridge deck edges (for night lighting strips).</summary>
        public static List<Vector3> BridgeLightPath(Vector3 euEnd, Vector3 asEnd, float waterY, float lateral)
        {
            Vector3 flat = asEnd - euEnd;
            flat.y = 0f;
            float total = flat.magnitude;
            Vector3 dir = flat / total;
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            var list = new List<Vector3>();
            for (int i = 0; i <= 40; i++)
            {
                float t = total * i / 40f;
                list.Add(new Vector3(euEnd.x, 0f, euEnd.z) + dir * t + side * lateral + Vector3.up * (waterY + 66f + 4f * Mathf.Sin(Mathf.PI * i / 40f)));
            }
            return list;
        }

        // --- Mosques ------------------------------------------------------------------------------

        void Minaret(Vector3 baseCentre, float height, float radius)
        {
            int seg = Seg(12);
            float balcony = height * 0.72f;
            VerticalLathe(B(Stone), baseCentre, new[]
            {
                new Vector2(0f, radius * 1.5f), new Vector2(height * 0.08f, radius * 1.5f), new Vector2(height * 0.1f, radius),
                new Vector2(balcony - 1.2f, radius * 0.9f), new Vector2(balcony - 0.6f, radius * 1.8f), new Vector2(balcony, radius * 1.8f),
                new Vector2(balcony + 0.4f, radius * 0.8f), new Vector2(height * 0.9f, radius * 0.75f), new Vector2(height * 0.9f + 0.01f, 0.01f),
            }, seg);
            VerticalLathe(B(Lead), baseCentre, new[] { new Vector2(height * 0.9f - 0.2f, radius * 0.85f), new Vector2(height, 0.01f) }, seg);
        }

        /// <summary>
        /// Ortaköy (Büyük Mecidiye) Mosque, neo-baroque: square prayer hall with tall arched
        /// windows, single dome on a drum, sultan's pavilion at the entrance side, two slender
        /// minarets at the entrance corners. +z = qibla direction (towards the water).
        /// </summary>
        public void OrtakoyMosque()
        {
            const float side = 19f, wallH = 15f;
            B(Stone).Box(new Vector3(0f, wallH * 0.5f, 0f), new Vector3(side, wallH, side));
            // Corner piers and a cornice give the baroque massing.
            foreach (float x in new[] { -1f, 1f })
                foreach (float z in new[] { -1f, 1f })
                    B(Stone).Box(new Vector3(x * side * 0.5f, wallH * 0.55f, z * side * 0.5f), new Vector3(2.6f, wallH * 1.1f, 2.6f));
            B(Stone).Box(new Vector3(0f, wallH + 0.5f, 0f), new Vector3(side + 1.6f, 1f, side + 1.6f));
            // Large arched windows (dark recesses) on the three free sides.
            for (int k = -1; k <= 1; k += 2)
            {
                B(Glass).Box(new Vector3(k * (side * 0.5f + 0.05f), wallH * 0.55f, 0f), new Vector3(0.1f, wallH * 0.65f, side * 0.45f));
                B(Glass).Box(new Vector3(0f, wallH * 0.55f, side * 0.5f + 0.05f), new Vector3(side * 0.45f, wallH * 0.65f, 0.1f));
            }
            // Drum and dome.
            VerticalLathe(B(Stone), new Vector3(0f, wallH + 1f, 0f), new[] { new Vector2(0f, 8.2f), new Vector2(3.5f, 8.2f), new Vector2(3.5f, 0.01f) }, Seg(24));
            VerticalLathe(B(Lead), new Vector3(0f, wallH + 4.5f, 0f), Dome(8f, 0f, detail == 0 ? 8 : 4), Seg(24));
            VerticalLathe(B(Lead), new Vector3(0f, wallH + 12.4f, 0f), new[] { new Vector2(0f, 1.2f), new Vector2(1.5f, 0.9f), new Vector2(3f, 0.01f) }, Seg(10));
            // Sultan's pavilion (two storeys) on the entrance side.
            B(Stone).Box(new Vector3(0f, 6f, -side * 0.5f - 6f), new Vector3(side * 0.95f, 12f, 12f));
            B(Lead).Box(new Vector3(0f, 12.4f, -side * 0.5f - 6f), new Vector3(side * 0.95f + 0.8f, 0.8f, 12.8f));
            for (int i = -2; i <= 2; i++)
                B(Glass).Box(new Vector3(i * 3.4f, 7f, -side * 0.5f - 12.05f), new Vector3(1.6f, 3.2f, 0.1f));
            foreach (float x in new[] { -1f, 1f })
                Minaret(new Vector3(x * (side * 0.95f * 0.5f + 1.8f), 0f, -side * 0.5f - 1.8f), 40f, 1.15f);
        }

        /// <summary>Classical Ottoman mosque silhouette: cascading semi-domes around a central dome, n minarets.</summary>
        public void ImperialMosque(float domeRadius, float domeBase, int minarets, float minaretHeight, string plaster)
        {
            float half = domeRadius * 2.2f;
            float wallH = domeBase * 0.5f;
            B(plaster).Box(new Vector3(0f, wallH * 0.5f, 0f), new Vector3(half * 2f, wallH, half * 2f));
            // Cascade: corner domes and semi-domes on the walls, central dome on a drum.
            foreach (var (dx, dz) in new[] { (0f, 1f), (0f, -1f), (1f, 0f), (-1f, 0f) })
            {
                B(plaster).Box(new Vector3(dx * domeRadius * 1.15f, wallH + (domeBase - wallH) * 0.3f, dz * domeRadius * 1.15f), new Vector3(domeRadius * (dx == 0f ? 2f : 1f), (domeBase - wallH) * 0.6f, domeRadius * (dz == 0f ? 2f : 1f)));
                VerticalLathe(B(Lead), new Vector3(dx * domeRadius * 1.15f, wallH + (domeBase - wallH) * 0.6f, dz * domeRadius * 1.15f), Dome(domeRadius * 0.7f, 0f, 4), Seg(16));
            }
            foreach (var (dx, dz) in new[] { (1f, 1f), (1f, -1f), (-1f, 1f), (-1f, -1f) })
                VerticalLathe(B(Lead), new Vector3(dx * half * 0.68f, wallH, dz * half * 0.68f), Dome(domeRadius * 0.38f, 0f, 4), Seg(12));
            VerticalLathe(B(plaster), new Vector3(0f, wallH, 0f), new[] { new Vector2(0f, domeRadius * 1.08f), new Vector2(domeBase - wallH, domeRadius * 1.04f), new Vector2(domeBase - wallH, 0.01f) }, Seg(24));
            VerticalLathe(B(Lead), new Vector3(0f, domeBase, 0f), Dome(domeRadius, 0f, detail == 0 ? 8 : 4, -0.15f), Seg(24));
            var spots = new List<Vector3>();
            foreach (var (dx, dz) in new[] { (1f, 1f), (1f, -1f), (-1f, 1f), (-1f, -1f) }) spots.Add(new Vector3(dx * half * 1.08f, 0f, dz * half * 1.08f));
            if (minarets == 6) { spots.Add(new Vector3(-half * 2.4f, 0f, half * 1.08f)); spots.Add(new Vector3(-half * 2.4f, 0f, -half * 1.08f)); }
            for (int i = 0; i < Mathf.Min(minarets, spots.Count); i++) Minaret(spots[i], minaretHeight, 1.6f);
        }

        // --- Palaces and towers ---------------------------------------------------------------

        /// <summary>Long waterfront palace (Dolmabahçe-like massing): wings, taller central hall, cornices. +z along the shore.</summary>
        public void Palace(float length, float depth, float height, bool centralHall)
        {
            float hallLen = centralHall ? length * 0.16f : 0f;
            float wing = (length - hallLen) * 0.5f;
            foreach (float s in new[] { -1f, 1f })
            {
                Vector3 c = new Vector3(0f, height * 0.5f, s * (hallLen * 0.5f + wing * 0.5f));
                B(Marble).Box(c, new Vector3(depth, height, wing));
                B(Marble).Box(c + new Vector3(0f, height * 0.5f + 0.4f, 0f), new Vector3(depth + 1.2f, 0.8f, wing + 1.2f));
                // Window rows on the waterfront facade.
                for (int row = 0; row < 2; row++)
                    B(Glass).Box(c + new Vector3(depth * 0.5f + 0.05f, -height * 0.18f + row * height * 0.42f, 0f), new Vector3(0.1f, height * 0.18f, wing * 0.92f));
                // End pavilions.
                B(Marble).Box(new Vector3(0f, height * 0.6f, s * (length * 0.5f - 6f)), new Vector3(depth + 3f, height * 1.2f, 12f));
            }
            if (centralHall)
            {
                B(Marble).Box(new Vector3(0f, height * 0.85f, 0f), new Vector3(depth * 1.1f, height * 1.7f, hallLen));
                VerticalLathe(B(Lead), new Vector3(0f, height * 1.7f, 0f), Dome(Mathf.Min(depth, hallLen) * 0.38f, 0f, detail == 0 ? 6 : 3, -0.4f), Seg(20));
            }
            // Quay in front of the facade.
            B(Stone).Box(new Vector3(depth * 0.5f + 6f, -0.6f, 0f), new Vector3(12f, 2f, length + 20f));
        }

        public void ClockTower(float height)
        {
            B(Marble).Box(new Vector3(0f, height * 0.4f, 0f), new Vector3(4.5f, height * 0.8f, 4.5f));
            B(Marble).Box(new Vector3(0f, height * 0.85f, 0f), new Vector3(3.6f, height * 0.12f, 3.6f));
            VerticalLathe(B(Lead), new Vector3(0f, height * 0.91f, 0f), Dome(1.8f, 0f, 4, 0.6f), Seg(12));
        }

        /// <summary>Galata Tower: stone cylinder (16.45 m diameter), arcaded top storey, conical roof.</summary>
        public void GalataTower(float height)
        {
            float body = height * 0.77f;
            VerticalLathe(B(Stone), Vector3.zero, new[]
            {
                new Vector2(0f, 8.6f), new Vector2(body * 0.6f, 8.2f), new Vector2(body, 7.9f), new Vector2(body + 0.6f, 9f), new Vector2(body + 1.2f, 9f), new Vector2(body + 1.2f, 7.6f),
                new Vector2(body + 5f, 7.6f), new Vector2(body + 5f, 0.01f),
            }, Seg(20));
            VerticalLathe(B(Lead), new Vector3(0f, body + 5f, 0f), new[] { new Vector2(0f, 8.2f), new Vector2(height - body - 5f, 0.01f) }, Seg(20));
            VerticalLathe(B(Glass), new Vector3(0f, body + 1.4f, 0f), new[] { new Vector2(0f, 7.65f), new Vector2(3.2f, 7.65f) }, Seg(20));
        }

        /// <summary>Maiden's Tower: rocky islet, low building, slim tower with a lantern.</summary>
        public void MaidensTower()
        {
            VerticalLathe(B(Rock), new Vector3(0f, -3f, 0f), new[] { new Vector2(0f, 32f), new Vector2(3f, 30f), new Vector2(4.2f, 24f), new Vector2(4.2f, 0.01f) }, Seg(16));
            B(Stone).Box(new Vector3(0f, 7f, -4f), new Vector3(20f, 6f, 12f));
            B(Lead).Box(new Vector3(0f, 10.3f, -4f), new Vector3(20.6f, 0.6f, 12.6f));
            VerticalLathe(B(Stone), new Vector3(4f, 4f, 2f), new[] { new Vector2(0f, 3.4f), new Vector2(15f, 3.1f), new Vector2(15.5f, 3.8f), new Vector2(16f, 3.8f), new Vector2(16f, 0.01f) }, Seg(12));
            VerticalLathe(B(Lead), new Vector3(4f, 20f, 2f), new[] { new Vector2(0f, 2.4f), new Vector2(3.5f, 0.01f) }, Seg(12));
        }

        // --- Maritime ---------------------------------------------------------------------------

        /// <summary>Double-ended Bosphorus passenger ferry (~70 m): white hull, two decks, funnel. +z = bow.</summary>
        public void Ferry()
        {
            const float L = 70f, W = 13.5f;
            var hull = new List<Vector3[]>();
            int n = detail == 0 ? 16 : 8;
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                float z = (t - 0.5f) * L;
                float w = W * 0.5f * Mathf.Sqrt(Mathf.Max(0.02f, 1f - Mathf.Pow(2f * t - 1f, 4f)));
                hull.Add(new[] { new Vector3(-w * 0.75f, -2.5f, z), new Vector3(-w, 1.2f, z), new Vector3(w, 1.2f, z), new Vector3(w * 0.75f, -2.5f, z) });
            }
            B(Hull).RingLoft(hull, true, true);
            B(Marble).Box(new Vector3(0f, 3.4f, 0f), new Vector3(W * 0.88f, 4.2f, L * 0.72f));
            B(Glass).Box(new Vector3(0f, 3.8f, 0f), new Vector3(W * 0.885f, 1.4f, L * 0.68f));
            B(Marble).Box(new Vector3(0f, 6.8f, 0f), new Vector3(W * 0.7f, 2.6f, L * 0.5f));
            B(Glass).Box(new Vector3(0f, 7f, 0f), new Vector3(W * 0.705f, 1.1f, L * 0.46f));
            VerticalLathe(B(Funnel), new Vector3(0f, 8f, 0f), new[] { new Vector2(0f, 1.9f), new Vector2(4.5f, 1.8f), new Vector2(4.5f, 0.01f) }, Seg(12));
        }

        /// <summary>Bosphorus tour boat (~28 m): white hull with a blue band, enclosed lower deck, open upper deck under an awning. +z = bow.</summary>
        public void TourBoat()
        {
            const float L = 28f, W = 6.5f;
            var hull = new List<Vector3[]>();
            int n = detail == 0 ? 12 : 6;
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                float z = (t - 0.5f) * L;
                float w = W * 0.5f * (t > 0.7f ? Mathf.Lerp(1f, 0.1f, (t - 0.7f) / 0.3f) : t < 0.1f ? Mathf.Lerp(0.85f, 1f, t / 0.1f) : 1f);
                hull.Add(new[] { new Vector3(-w * 0.7f, -1.4f, z), new Vector3(-w, 0.9f, z), new Vector3(w, 0.9f, z), new Vector3(w * 0.7f, -1.4f, z) });
            }
            B(Hull).RingLoft(hull, true, true);
            B(Funnel).Box(new Vector3(0f, 0.7f, -1f), new Vector3(W * 1.01f, 0.35f, L * 0.68f));                 // blue band
            B(Marble).Box(new Vector3(0f, 2.1f, -1.5f), new Vector3(W * 0.9f, 2.4f, L * 0.62f));               // lower saloon
            B(Glass).Box(new Vector3(0f, 2.3f, -1.5f), new Vector3(W * 0.905f, 1.1f, L * 0.58f));
            B(Marble).Box(new Vector3(0f, 3.35f, -1.5f), new Vector3(W * 0.95f, 0.1f, L * 0.64f));             // upper deck
            if (detail >= 2) return;
            for (float z = -L * 0.3f; z <= L * 0.18f; z += 3f)
                foreach (float x in new[] { -W * 0.44f, W * 0.44f })
                    B(Lead).Box(new Vector3(x, 4.5f, z), new Vector3(0.06f, 2.2f, 0.06f));
            B(Funnel).Box(new Vector3(0f, 5.65f, -1.5f), new Vector3(W * 0.95f, 0.08f, L * 0.55f));             // awning
            B(Lead).Box(new Vector3(-W * 0.47f, 3.9f, -1.5f), new Vector3(0.04f, 1.0f, L * 0.62f));            // rails
            B(Lead).Box(new Vector3(W * 0.47f, 3.9f, -1.5f), new Vector3(0.04f, 1.0f, L * 0.62f));
        }

        /// <summary>Private motor boat (~9 m): white hull, small windscreen and cabin. +z = bow.</summary>
        public void MotorBoat()
        {
            const float L = 9f, W = 2.8f;
            var hull = new List<Vector3[]>();
            for (int i = 0; i <= 6; i++)
            {
                float t = i / 6f;
                float z = (t - 0.5f) * L;
                float w = W * 0.5f * (t > 0.6f ? Mathf.Lerp(1f, 0.05f, (t - 0.6f) / 0.4f) : 1f);
                hull.Add(new[] { new Vector3(0f, -0.7f, z), new Vector3(-w, 0.5f, z), new Vector3(w, 0.5f, z), new Vector3(0f, -0.7f, z) });
            }
            B(Marble).RingLoft(hull, true, true);
            B(Marble).Box(new Vector3(0f, 0.85f, -0.6f), new Vector3(W * 0.8f, 0.7f, 2.6f));
            B(Glass).Box(new Vector3(0f, 1.2f, 0.65f), new Vector3(W * 0.75f, 0.45f, 0.08f));
        }

        /// <summary>Generic coastal freighter for the strait traffic (~120 m). +z = bow.</summary>
        public void Freighter()
        {
            const float L = 120f, W = 18f;
            var hull = new List<Vector3[]>();
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                float z = (t - 0.5f) * L;
                float w = W * 0.5f * (t > 0.8f ? Mathf.Lerp(1f, 0.15f, (t - 0.8f) / 0.2f) : t < 0.08f ? Mathf.Lerp(0.8f, 1f, t / 0.08f) : 1f);
                hull.Add(new[] { new Vector3(-w * 0.9f, -6f, z), new Vector3(-w, 3f, z), new Vector3(w, 3f, z), new Vector3(w * 0.9f, -6f, z) });
            }
            B(Hull).RingLoft(hull, true, true);
            B(Marble).Box(new Vector3(0f, 9f, -L * 0.38f), new Vector3(W * 0.9f, 12f, 12f));
            B(Glass).Box(new Vector3(0f, 13f, -L * 0.38f + 6.05f), new Vector3(W * 0.8f, 1.5f, 0.1f));
            for (int i = 0; i < 4; i++) B(Roof).Box(new Vector3(0f, 5.5f, -L * 0.2f + i * 18f), new Vector3(W * 0.8f, 5f, 15f));
        }
    }
}
