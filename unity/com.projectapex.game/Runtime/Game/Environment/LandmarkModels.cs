using System.Collections.Generic;
using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// In-house detailed landmark models (Phase 4) and the small facade kit they share: arched and
    /// rectangular windows with frames, archivolts, engaged columns, cornices, balustrades, urn finials.
    /// Everything is built in a facade frame (x along the wall, y up, z out of the wall face, face at
    /// z = 0) and moved into place with <see cref="InFrame"/>. Proportions come from the OSM footprints
    /// and published heights where known, the rest is estimated; these are procedural models, not scans,
    /// and are not photorealistic.
    /// Detail: 0 = near (columns, mullions, balusters), 1 = middle (mouldings, fewer segments),
    /// 2 = far (massing and window bands only).
    /// </summary>
    public sealed partial class LandmarkGeometry
    {
        public const string Gilt = "Gilt";

        public int Detail => detail;

        /// <summary>Metric box-projected UVs on the textured masonry materials (marble ashlar, lime plaster).</summary>
        public void MasonryUVs()
        {
            foreach (var key in new[] { Marble, RowPlaster, Brick })
                if (parts.TryGetValue(key, out var mb)) mb.MetricUVs();
        }

        /// <summary>Runs build() and moves every vertex it added, in all materials, by yaw about y and then origin.</summary>
        public void InFrame(Vector3 origin, float yawDeg, System.Action build)
        {
            var before = new Dictionary<string, int>();
            foreach (var kv in parts) before[kv.Key] = kv.Value.VertexCount;
            build();
            float a = yawDeg * Mathf.Deg2Rad, s = Mathf.Sin(a), c = Mathf.Cos(a);
            foreach (var kv in parts)
            {
                before.TryGetValue(kv.Key, out int b0);
                int n = kv.Value.VertexCount - b0;
                if (n > 0) kv.Value.TransformLast(n, v => origin + new Vector3(c * v.x + s * v.z, v.y, -s * v.x + c * v.z));
            }
        }

        // --- Facade kit (facade frame) ---------------------------------------------------------

        /// <summary>Round-arched outline: rectangle from y0 to the springing line, semicircle above.</summary>
        static List<Vector2> ArchOutline(float cx, float y0, float width, float spring, int seg)
        {
            float r = width * 0.5f;
            var p = new List<Vector2> { new Vector2(cx - r, y0), new Vector2(cx + r, y0) };
            for (int i = 0; i <= seg; i++)
            {
                float t = Mathf.PI * i / seg;
                p.Add(new Vector2(cx + r * Mathf.Cos(t), spring + r * Mathf.Sin(t)));
            }
            return p;
        }

        /// <summary>Flat slab from a convex outline in the facade plane, from z = front − thickness to front.</summary>
        static void Panel(MeshBuilder mb, IList<Vector2> polyXY, float front, float thickness)
        {
            int b0 = mb.VertexCount;
            mb.PlateYZ(polyXY, -(front - thickness * 0.5f), thickness);
            // (x, y, z) -> (z, y, -x): proper rotation taking the plate's (z, y) plane into the facade plane.
            mb.TransformLast(mb.VertexCount - b0, v => new Vector3(v.z, v.y, -v.x));
        }

        /// <summary>Semicircular band (archivolt, arched cornice) between radii rIn and rOut, from z0 to z0 + depth.</summary>
        static void ArchBand(MeshBuilder mb, float cx, float spring, float rIn, float rOut, float z0, float depth, int seg)
        {
            float z1 = z0 + depth;
            Vector3 P(float t, float r, float z) => new Vector3(cx + r * Mathf.Cos(t), spring + r * Mathf.Sin(t), z);
            for (int i = 0; i < seg; i++)
            {
                float t0 = Mathf.PI * i / seg, t1 = Mathf.PI * (i + 1) / seg, tm = (t0 + t1) * 0.5f;
                var radial = new Vector3(Mathf.Cos(tm), Mathf.Sin(tm), 0f);
                mb.QuadFacing(P(t0, rIn, z1), P(t0, rOut, z1), P(t1, rOut, z1), P(t1, rIn, z1), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector3.forward);
                mb.QuadFacing(P(t0, rOut, z1), P(t1, rOut, z1), P(t1, rOut, z0), P(t0, rOut, z0), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, radial);
                mb.QuadFacing(P(t0, rIn, z1), P(t1, rIn, z1), P(t1, rIn, z0), P(t0, rIn, z0), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, -radial);
            }
        }

        int ArcSeg(float radius) => System.Math.Clamp(Mathf.RoundToInt(radius * (detail == 0 ? 5f : 2.5f)), 4, detail == 0 ? 24 : 12);

        /// <summary>Round-arched window: glass, moulded frame down to the sill, sill, keystone; mullions at detail 0.</summary>
        public void ArchWindow(string wall, float cx, float y0, float width, float spring, float frame = 0.22f)
        {
            float r = width * 0.5f;
            int seg = ArcSeg(r);
            Panel(B(windowGlass), ArchOutline(cx, y0, width, spring, seg), 0.04f, 0.06f);
            if (detail >= 2) return;
            ArchBand(B(wall), cx, spring, r, r + frame, 0f, 0.14f, seg);
            foreach (float s in new[] { -1f, 1f })
                B(wall).Box(new Vector3(cx + s * (r + frame * 0.5f), (y0 + spring) * 0.5f, 0.07f), new Vector3(frame, spring - y0, 0.14f));
            B(wall).Box(new Vector3(cx, y0 - 0.08f, 0.1f), new Vector3(width + frame * 2.6f, 0.16f, 0.2f));
            B(wall).Box(new Vector3(cx, spring + r + frame * 0.4f, 0.1f), new Vector3(frame * 1.3f, frame * 1.8f, 0.2f));
            if (detail == 0 && width > 0.8f)
            {
                // Glazing bars: centre mullion, transom at the springing line, radial bars in the head.
                B(Lead).Box(new Vector3(cx, (y0 + spring) * 0.5f, 0.06f), new Vector3(0.05f, spring - y0, 0.04f));
                B(Lead).Box(new Vector3(cx, spring, 0.06f), new Vector3(width, 0.05f, 0.04f));
                for (int k = 1; k < 4; k++)
                {
                    float t = Mathf.PI * k / 4f;
                    var d = new Vector3(Mathf.Cos(t), Mathf.Sin(t), 0f);
                    B(Lead).Tube(new Vector3(cx, spring, 0.06f), new Vector3(cx, spring, 0.06f) + d * r, 0.025f, 4, false);
                }
            }
        }

        /// <summary>Rectangular window with an architrave frame and a cornice-like lintel.</summary>
        public void RectWindow(string wall, float cx, float y0, float width, float height, float frame = 0.18f)
        {
            Panel(B(windowGlass), new[] { new Vector2(cx - width * 0.5f, y0), new Vector2(cx + width * 0.5f, y0), new Vector2(cx + width * 0.5f, y0 + height), new Vector2(cx - width * 0.5f, y0 + height) }, 0.04f, 0.06f);
            if (detail >= 2) return;
            foreach (float s in new[] { -1f, 1f })
                B(wall).Box(new Vector3(cx + s * (width + frame) * 0.5f, y0 + height * 0.5f, 0.06f), new Vector3(frame, height + frame * 2f, 0.12f));
            B(wall).Box(new Vector3(cx, y0 - frame * 0.5f, 0.09f), new Vector3(width + frame * 3f, frame, 0.18f));
            B(wall).Box(new Vector3(cx, y0 + height + frame * 0.5f, 0.06f), new Vector3(width + frame * 2f, frame, 0.12f));
            B(wall).Box(new Vector3(cx, y0 + height + frame * 1.4f, 0.12f), new Vector3(width + frame * 3.4f, frame * 0.8f, 0.24f));
            if (detail == 0 && width > 0.8f)
                B(Lead).Box(new Vector3(cx, y0 + height * 0.5f, 0.06f), new Vector3(0.05f, height, 0.04f));
        }

        /// <summary>Stepped cornice along x from x0 to x1 with its underside at y.</summary>
        public void Cornice(string mat, float x0, float x1, float y, float projection = 0.6f)
        {
            float len = x1 - x0, cx = (x0 + x1) * 0.5f;
            if (detail >= 2) { B(mat).Box(new Vector3(cx, y + 0.35f, projection * 0.5f), new Vector3(len, 0.7f, projection)); return; }
            B(mat).Box(new Vector3(cx, y + 0.12f, projection * 0.15f), new Vector3(len, 0.24f, projection * 0.3f));
            B(mat).Box(new Vector3(cx, y + 0.38f, projection * 0.3f), new Vector3(len + projection * 0.3f, 0.28f, projection * 0.6f));
            B(mat).Box(new Vector3(cx, y + 0.64f, projection * 0.5f), new Vector3(len + projection * 0.6f, 0.24f, projection));
        }

        /// <summary>Engaged column on a base with a flared capital (Corinthian at a distance).</summary>
        public void Column(string mat, float x, float y0, float height, float radius)
        {
            int seg = detail == 0 ? 12 : 8;
            float z = radius * 0.7f;
            B(mat).Box(new Vector3(x, y0 + 0.25f, z), new Vector3(radius * 2.7f, 0.5f, radius * 2.7f));
            if (detail >= 2) { B(mat).Box(new Vector3(x, y0 + height * 0.5f, z), new Vector3(radius * 1.8f, height, radius * 1.8f)); return; }
            VerticalLathe(B(mat), new Vector3(x, y0 + 0.5f, z), new[]
            {
                new Vector2(0f, radius * 1.15f), new Vector2(0.2f, radius), new Vector2(height - 1.5f, radius * 0.88f),
                new Vector2(height - 1.4f, radius * 1.0f), new Vector2(height - 0.9f, radius * 1.3f), new Vector2(height - 0.85f, radius * 1.55f),
                new Vector2(height - 0.84f, 0.01f),
            }, seg);
            B(mat).Box(new Vector3(x, y0 + height - 0.2f, z), new Vector3(radius * 3.4f, 0.3f, radius * 3.4f));
        }

        /// <summary>Balustrade from x0 to x1 standing on y (plinth, balusters at detail 0, hand rail).</summary>
        public void Balustrade(string mat, float x0, float x1, float y, float z, float height = 1f, bool turned = true)
        {
            float len = x1 - x0, cx = (x0 + x1) * 0.5f;
            B(mat).Box(new Vector3(cx, y + 0.1f, z), new Vector3(len, 0.2f, 0.4f));
            B(mat).Box(new Vector3(cx, y + height - 0.08f, z), new Vector3(len, 0.16f, 0.36f));
            if (detail >= 1) { B(mat).Box(new Vector3(cx, y + height * 0.5f, z), new Vector3(len, height - 0.3f, 0.16f)); return; }
            int n = Mathf.Max(2, Mathf.RoundToInt(len / 0.3f));
            for (int i = 0; i < n; i++)
            {
                float x = x0 + (i + 0.5f) * len / n;
                float h = height - 0.36f;
                if (!turned) { B(mat).Box(new Vector3(x, y + 0.2f + h * 0.5f, z), new Vector3(0.13f, h, 0.13f)); continue; }
                VerticalLathe(B(mat), new Vector3(x, y + 0.2f, z), new[]
                {
                    new Vector2(0f, 0.07f), new Vector2(h * 0.12f, 0.08f), new Vector2(h * 0.3f, 0.05f), new Vector2(h * 0.55f, 0.095f),
                    new Vector2(h * 0.85f, 0.045f), new Vector2(h, 0.07f), new Vector2(h + 0.001f, 0.001f),
                }, 6);
            }
        }

        /// <summary>Iron railing between two points: posts every 2 m, top and middle rails, bars every 0.15 m at detail 0.</summary>
        public void Railing(Vector3 a, Vector3 b, float height = 1.1f)
        {
            var mb = B(Lead);
            float len = Vector3.Distance(a, b);
            mb.Tube(a + Vector3.up * height, b + Vector3.up * height, 0.03f, 4, true);
            mb.Tube(a + Vector3.up * 0.15f, b + Vector3.up * 0.15f, 0.02f, 4, true);
            int posts = Mathf.Max(1, Mathf.RoundToInt(len / 2f));
            for (int i = 0; i <= posts; i++)
            {
                var p = Vector3.Lerp(a, b, i / (float)posts);
                mb.Box(p + Vector3.up * height * 0.5f, new Vector3(0.06f, height, 0.06f));
            }
            if (detail > 0) return;
            int bars = Mathf.RoundToInt(len / 0.15f);
            for (int i = 0; i < bars; i++)
            {
                var p = Vector3.Lerp(a, b, (i + 0.5f) / bars);
                mb.Box(p + Vector3.up * (height * 0.5f + 0.07f), new Vector3(0.015f, height - 0.15f, 0.015f));
            }
        }

        /// <summary>Urn finial on a pedestal.</summary>
        public void Urn(string mat, Vector3 at, float scale = 1f)
        {
            B(mat).Box(at + new Vector3(0f, 0.25f * scale, 0f), new Vector3(0.6f, 0.5f, 0.6f) * scale);
            if (detail >= 2) return;
            VerticalLathe(B(mat), at + new Vector3(0f, 0.5f * scale, 0f), new[]
            {
                new Vector2(0f, 0.2f * scale), new Vector2(0.1f * scale, 0.12f * scale), new Vector2(0.35f * scale, 0.28f * scale),
                new Vector2(0.6f * scale, 0.2f * scale), new Vector2(0.7f * scale, 0.1f * scale), new Vector2(0.9f * scale, 0.001f),
            }, detail == 0 ? 10 : 6);
        }

        /// <summary>Gilded crescent finial (alem) on a short pole, opening upwards like the real ones, at top.</summary>
        public void Alem(Vector3 top, float size)
        {
            B(Gilt).Tube(top, top + Vector3.up * size * 1.2f, size * 0.06f, 6, true);
            VerticalLathe(B(Gilt), top + Vector3.up * size * 0.35f, new[] { new Vector2(0f, size * 0.001f), new Vector2(size * 0.15f, size * 0.14f), new Vector2(size * 0.3f, size * 0.001f) }, 8);
            if (detail >= 2) return;
            var arc = new List<Vector3>();
            for (int i = 0; i <= 10; i++)
            {
                float t = Mathf.Lerp(-0.75f, 0.75f, i / 10f) * Mathf.PI + Mathf.PI;
                arc.Add(top + new Vector3(Mathf.Sin(t) * size * 0.4f, size * 1.6f + Mathf.Cos(t) * size * 0.4f, 0f));
            }
            B(Gilt).TubePath(arc, size * 0.07f, 6, true);
        }

        /// <summary>Hipped roof over a w×d rectangle at height y, ridge along the longer side.</summary>
        public void HippedRoof(string mat, Vector3 centre, float w, float d, float rise)
        {
            var mb = B(mat);
            float hw = w * 0.5f, hd = d * 0.5f, ridge = Mathf.Max(0f, Mathf.Abs(hw - hd));
            bool alongZ = d >= w;
            Vector3 c = centre;
            Vector3 r0 = c + (alongZ ? new Vector3(0f, rise, -ridge) : new Vector3(-ridge, rise, 0f));
            Vector3 r1 = c + (alongZ ? new Vector3(0f, rise, ridge) : new Vector3(ridge, rise, 0f));
            Vector3 a = c + new Vector3(-hw, 0f, -hd), b = c + new Vector3(hw, 0f, -hd), e = c + new Vector3(hw, 0f, hd), f = c + new Vector3(-hw, 0f, hd);
            Vector2 z2 = Vector2.zero;
            if (alongZ)
            {
                mb.QuadFacing(b, e, r1, r0, z2, z2, z2, z2, new Vector3(1f, 1f, 0f));
                mb.QuadFacing(f, a, r0, r1, z2, z2, z2, z2, new Vector3(-1f, 1f, 0f));
                mb.TriangleFacing(a, b, r0, z2, z2, z2, new Vector3(0f, 1f, -1f));
                mb.TriangleFacing(e, f, r1, z2, z2, z2, new Vector3(0f, 1f, 1f));
            }
            else
            {
                mb.QuadFacing(e, f, r0, r1, z2, z2, z2, z2, new Vector3(0f, 1f, 1f));
                mb.QuadFacing(a, b, r1, r0, z2, z2, z2, z2, new Vector3(0f, 1f, -1f));
                mb.TriangleFacing(b, e, r1, z2, z2, z2, new Vector3(1f, 1f, 0f));
                mb.TriangleFacing(f, a, r0, z2, z2, z2, new Vector3(-1f, 1f, 0f));
            }
        }

        // --- Ortaköy Mosque (Büyük Mecidiye Camii, 1853-56, Garabet and Nigoğayos Balyan) --------

        /// <summary>Axis of the mosque in the game frame: bearing of the qibla side, from the OSM footprint (way edges at 140.8°/50.8°).</summary>
        public const float OrtakoyAxis = 140.8f;
        /// <summary>Prayer-hall centre relative to the footprint centroid (the landmark point), in the model frame.</summary>
        public static readonly Vector3 OrtakoyHallOffset = new Vector3(0.46f, 0f, 16.1f);

        /// <summary>
        /// Detailed Ortaköy Mosque. Origin: centre of the prayer hall at terrace level; +z = qibla side
        /// (towards the water), +x = right when facing the qibla. Plan from the OSM footprint: square hall of
        /// 21.2 m; behind it a 30 m wide block holding the minaret bases, then the U-shaped royal pavilion
        /// (hünkâr kasrı) with two 10.3 × 13.3 m wings around the entrance court. Heights are estimates
        /// (main cornice 15.5 m, crest of the great arches 20.4 m, dome top about 29 m, minarets 40 m).
        /// Neo-baroque facades: corner piers with paired columns, a great arched window on each free side
        /// divided by stone mullions and transoms, the cornice rising over the arch, balustrades and urns.
        /// </summary>
        public void OrtakoyMosqueDetailed()
        {
            windowGlass = MosqueGlass;
            try { OrtakoyMosqueBody(); }
            finally { windowGlass = Glass; }
        }

        void OrtakoyMosqueBody()
        {
            const float half = 10.6f, wallH = 15.5f, plinth = 1.2f;
            string W = Marble;
            // Terrace on the quay (the model stands on its own platform, top at y = 0).
            B(Stone).Box(new Vector3(0f, -3f, -9f), new Vector3(44f, 6f, 54f));
            B(Stone).Box(new Vector3(0f, 0.15f, -9f), new Vector3(42f, 0.3f, 52f));
            if (detail == 0)
            {
                // Iron railing along the three waterfront sides of the terrace.
                Railing(new Vector3(-21f, 0.3f, 17f), new Vector3(21f, 0.3f, 17f));
                Railing(new Vector3(21f, 0.3f, 17f), new Vector3(21f, 0.3f, -35f));
                Railing(new Vector3(-21f, 0.3f, 17f), new Vector3(-21f, 0.3f, -35f));
            }
            // Prayer hall core, roof.
            B(W).Box(new Vector3(0f, wallH * 0.5f, 0f), new Vector3(half * 2f, wallH, half * 2f));
            B(Lead).Box(new Vector3(0f, wallH + 0.75f, 0f), new Vector3(half * 2f - 1f, 0.3f, half * 2f - 1f));
            // Three free facades (qibla, left, right); the rear one meets the minaret block.
            foreach (float yaw in new[] { 0f, 90f, -90f })
            {
                var n = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                InFrame(n * half, yaw, () => HallFacade(W, half, wallH, plinth));
            }
            // Drum with 16 arched windows and buttresses, cornice ring, lead dome, lantern and alem.
            float drumR = 7.8f, drumY = wallH + 0.9f, drumH = 4.6f;
            int dseg = Seg(32);
            VerticalLathe(B(W), new Vector3(0f, drumY, 0f), new[] { new Vector2(-0.01f, drumR + 0.6f), new Vector2(0.5f, drumR + 0.6f), new Vector2(0.6f, drumR), new Vector2(drumH, drumR), new Vector2(drumH + 0.01f, 0.01f) }, dseg);
            for (int i = 0; i < 16; i++)
            {
                float a = i * 22.5f;
                var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                InFrame(new Vector3(0f, drumY, 0f) + dir * (drumR - 0.02f), a, () => ArchWindow(W, 0f, 1.1f, 1.0f, 3.1f, 0.16f));
                if (detail < 2)
                {
                    var bdir = Quaternion.Euler(0f, a + 11.25f, 0f) * Vector3.forward;
                    InFrame(new Vector3(0f, drumY, 0f) + bdir * drumR, a + 11.25f, () => B(W).Box(new Vector3(0f, drumH * 0.5f + 0.3f, 0.25f), new Vector3(0.7f, drumH - 0.6f, 0.7f)));
                }
            }
            VerticalLathe(B(W), new Vector3(0f, drumY + drumH, 0f), new[] { new Vector2(0f, drumR), new Vector2(0.3f, drumR + 0.5f), new Vector2(0.7f, drumR + 0.6f), new Vector2(0.71f, 0.01f) }, dseg);
            float domeY = drumY + drumH + 0.7f;
            VerticalLathe(B(Lead), new Vector3(0f, domeY, 0f), Dome(drumR - 0.2f, 0f, detail == 0 ? 14 : detail == 1 ? 8 : 5, 0.05f), dseg);
            float top = domeY + (drumR - 0.2f) * 1.05f;
            VerticalLathe(B(Lead), new Vector3(0f, top - 0.3f, 0f), new[] { new Vector2(0f, 1.1f), new Vector2(0.5f, 0.8f), new Vector2(1.2f, 0.35f), new Vector2(1.21f, 0.01f) }, Seg(12));
            Alem(new Vector3(0f, top + 0.9f, 0f), 1.4f);
            // Minaret block between the hall and the pavilion (u ±15, v -10.5 .. -16.5).
            const float blockH = 13f;
            B(W).Box(new Vector3(0f, blockH * 0.5f, -13.5f), new Vector3(30f, blockH, 6f));
            foreach (float s in new[] { -1f, 1f })
            {
                InFrame(new Vector3(s * 15f, 0f, -13.5f), s * 90f, () =>
                {
                    RectWindow(W, 0f, 2.2f, 1.2f, 2.2f);
                    ArchWindow(W, 0f, 7f, 1.2f, 9.2f);
                    Cornice(W, -3f, 3f, blockH - 0.7f, 0.5f);
                });
            }
            InFrame(new Vector3(0f, 0f, -16.5f), 180f, () => Cornice(W, -15f, 15f, blockH - 0.7f, 0.5f));
            // Royal pavilion: two wings and the link block, entrance court with steps between the wings.
            const float pavH = 11.5f;
            foreach (float s in new[] { -1f, 1f })
            {
                var c = new Vector3(s * 12.5f, 0f, -23.15f);
                B(W).Box(c + new Vector3(0f, pavH * 0.5f, 0f), new Vector3(10.3f, pavH, 13.3f));
                HippedRoof(Lead, c + new Vector3(0f, pavH + 0.7f, 0f), 10.9f, 13.9f, 2.6f);
                // Rear face (3 bays), outer side face (4 bays), court face (3 bays).
                InFrame(c + new Vector3(0f, 0f, -6.65f), 180f, () => PavilionFacade(W, 10.3f, pavH, 3));
                InFrame(c + new Vector3(s * 5.15f, 0f, 0f), s * 90f, () => PavilionFacade(W, 13.3f, pavH, 4));
                InFrame(c + new Vector3(-s * 5.15f, 0f, -1.65f), -s * 90f, () => PavilionFacade(W, 10f, pavH, 3));
            }
            B(W).Box(new Vector3(0f, pavH * 0.5f, -18.15f), new Vector3(14.7f, pavH, 3.3f));
            B(Lead).Box(new Vector3(0f, pavH + 0.75f, -18.15f), new Vector3(14.7f, 0.3f, 3.3f));
            InFrame(new Vector3(0f, 0f, -19.8f), 180f, () =>
            {
                // Entrance portal: arched doorway between paired columns, balcony window above.
                ArchWindow(W, 0f, 0.6f, 3.2f, 3.8f, 0.35f);
                if (detail < 2) foreach (float x in new[] { -2.6f, -3.4f, 2.6f, 3.4f }) Column(W, x, 0f, 6.4f, 0.28f);
                Cornice(W, -4.2f, 4.2f, 6.4f, 0.7f);
                ArchWindow(W, 0f, 7.6f, 2.2f, 9.4f);
                Cornice(W, -7.35f, 7.35f, pavH - 0.7f, 0.6f);
                if (detail < 2) Balustrade(W, -7.35f, 7.35f, pavH, 0.25f);
            });
            for (int k = 0; k < 4; k++)
                B(Stone).Box(new Vector3(0f, 0.15f + k * 0.15f, -21.5f - k * 0.35f), new Vector3(6f - k * 0.4f, 0.3f + k * 0.3f, 0.35f));
            // Minarets at the corners of the block (u ±12.75, v -13.5).
            foreach (float s in new[] { -1f, 1f }) MinaretDetailed(new Vector3(s * 12.75f, 0f, -13.5f), 40f, blockH);
        }

        /// <summary>One prayer-hall facade (facade frame, width 2·half).</summary>
        void HallFacade(string W, float half, float wallH, float plinth)
        {
            float pier = 2.6f;
            // Rusticated base and corner piers.
            B(Stone).Box(new Vector3(0f, plinth * 0.5f, 0.15f), new Vector3(half * 2f + 0.8f, plinth, 0.3f));
            foreach (float s in new[] { -1f, 1f })
            {
                B(W).Box(new Vector3(s * (half - pier * 0.5f), wallH * 0.5f, 0.2f), new Vector3(pier, wallH, 0.4f));
                if (detail < 2)
                    foreach (float off in new[] { 0.62f, 1.98f })
                        Column(W, s * (half - off), plinth, wallH - plinth - 0.1f, 0.3f);
            }
            // Frontispiece: the wall rises in a great arch over the central bay.
            float fw = 12.4f, fSpring = 14.2f;
            Panel(B(W), ArchOutline(0f, 0f, fw, fSpring, ArcSeg(fw * 0.5f)), 0.45f, 0.45f);
            ArchBand(B(W), 0f, fSpring, fw * 0.5f, fw * 0.5f + 0.45f, 0f, 0.75f, ArcSeg(fw * 0.5f));
            // Great window: glass, archivolt, keystone; stone mullions and two transoms make three tiers.
            float gw = 9f, gy = plinth + 0.3f, gSpring = 11.5f;
            int gseg = ArcSeg(gw * 0.5f);
            Panel(B(windowGlass), ArchOutline(0f, gy, gw, gSpring, gseg), 0.5f, 0.06f);
            ArchBand(B(W), 0f, gSpring, gw * 0.5f, gw * 0.5f + 0.6f, 0.45f, 0.25f, gseg);
            foreach (float s in new[] { -1f, 1f })
                B(W).Box(new Vector3(s * (gw * 0.5f + 0.3f), (gy + gSpring) * 0.5f, 0.57f), new Vector3(0.6f, gSpring - gy, 0.25f));
            B(W).Box(new Vector3(0f, gSpring + gw * 0.5f + 0.4f, 0.75f), new Vector3(0.8f, 1.2f, 0.6f));
            if (detail < 2)
            {
                foreach (float y in new[] { 5.6f, gSpring })
                    B(W).Box(new Vector3(0f, y, 0.6f), new Vector3(gw, 0.45f, 0.2f));
                foreach (float x in new[] { -1.6f, 1.6f })
                    B(W).Box(new Vector3(x, (gy + gSpring) * 0.5f, 0.6f), new Vector3(0.35f, gSpring - gy, 0.2f));
                // Radial bars in the lunette.
                for (int k = 1; k < 6; k++)
                {
                    float t = Mathf.PI * k / 6f;
                    var d = new Vector3(Mathf.Cos(t), Mathf.Sin(t), 0f);
                    B(detail == 0 ? Lead : W).Tube(new Vector3(0f, gSpring, 0.56f), new Vector3(0f, gSpring, 0.56f) + d * gw * 0.5f, 0.07f, 4, false);
                }
                if (detail == 0)
                {
                    // Small arched lights and grilles in the lower tiers.
                    foreach (float x in new[] { -3.05f, 0f, 3.05f })
                    {
                        B(Lead).Box(new Vector3(x, 3.4f, 0.56f), new Vector3(0.04f, 3.6f, 0.04f));
                        B(Lead).Box(new Vector3(x, 8.5f, 0.56f), new Vector3(0.04f, 5.4f, 0.04f));
                    }
                    Balustrade(W, -gw * 0.5f, gw * 0.5f, gy, 0.75f, 0.9f);
                }
            }
            // Side strips between frontispiece and piers: two stacked arched windows each.
            foreach (float s in new[] { -1f, 1f })
            {
                float x = s * (fw * 0.5f + (half - pier - fw * 0.5f) * 0.5f);
                ArchWindow(W, x, 2.4f, 0.9f, 4.6f, 0.16f);
                ArchWindow(W, x, 8.2f, 0.9f, 10.4f, 0.16f);
            }
            // Main cornice over the strips and piers; parapet with balustrade and urns above.
            foreach (float s in new[] { -1f, 1f })
            {
                float a = s * (fw * 0.5f + 0.45f), b = s * (half + 0.4f);
                Cornice(W, Mathf.Min(a, b), Mathf.Max(a, b), wallH - 0.7f, 0.8f);
                if (detail < 2) Balustrade(W, Mathf.Min(a, b), Mathf.Max(a, b), wallH, 0.2f);
                Urn(W, new Vector3(s * (half - 0.3f), wallH, 0.2f));
            }
        }

        /// <summary>Royal pavilion facade: rusticated ground floor with rectangular windows, arched upper windows, cornice.</summary>
        void PavilionFacade(string W, float width, float height, int bays)
        {
            B(Stone).Box(new Vector3(0f, 0.5f, 0.12f), new Vector3(width, 1f, 0.24f));
            if (detail < 2)
            {
                B(W).Box(new Vector3(0f, 5.2f, 0.1f), new Vector3(width, 0.35f, 0.2f));   // string course
                foreach (float s in new[] { -1f, 1f })
                    B(W).Box(new Vector3(s * (width * 0.5f - 0.35f), height * 0.5f, 0.12f), new Vector3(0.7f, height, 0.24f));
            }
            float bay = width / bays;
            for (int i = 0; i < bays; i++)
            {
                float x = -width * 0.5f + bay * (i + 0.5f);
                RectWindow(W, x, 1.7f, 1.2f, 2.4f);
                ArchWindow(W, x, 6.3f, 1.2f, 8.4f);
            }
            Cornice(W, -width * 0.5f, width * 0.5f, height - 0.7f, 0.6f);
        }

        /// <summary>
        /// Slender neo-baroque minaret: square pedestal up to the roof of the block, moulded base, plain shaft
        /// (the fluting was lost in a 19th-century rebuild), one balcony (şerefe) on corbels with a balustrade,
        /// upper shaft, conical lead cap and gilded alem.
        /// </summary>
        void MinaretDetailed(Vector3 basePos, float height, float pedestal)
        {
            int seg = Seg(16);
            string W = Marble;
            B(W).Box(basePos + new Vector3(0f, pedestal * 0.5f + 0.5f, 0f), new Vector3(3.4f, pedestal + 1f, 3.4f));
            Cornice1(basePos + new Vector3(0f, pedestal + 1f, 0f), 3.4f);
            float y0 = pedestal + 1.3f, balcony = height * 0.775f, capBase = height * 0.9f;
            VerticalLathe(B(W), basePos + new Vector3(0f, y0, 0f), new[]
            {
                new Vector2(0f, 1.3f), new Vector2(0.5f, 1.3f), new Vector2(0.7f, 1.0f), new Vector2(1f, 0.9f),
                new Vector2(balcony - y0 - 1.4f, 0.8f), new Vector2(balcony - y0 - 0.6f, 1.1f), new Vector2(balcony - y0 - 0.1f, 1.6f),
                new Vector2(balcony - y0, 1.7f), new Vector2(balcony - y0 + 0.01f, 0.7f), new Vector2(capBase - y0 - 0.3f, 0.66f),
                new Vector2(capBase - y0 - 0.1f, 0.85f), new Vector2(capBase - y0, 0.85f), new Vector2(capBase - y0 + 0.01f, 0.01f),
            }, seg);
            if (detail == 0)
            {
                for (int i = 0; i < 28; i++)
                {
                    float a = i / 28f * Mathf.PI * 2f;
                    var p = basePos + new Vector3(Mathf.Sin(a) * 1.5f, balcony, Mathf.Cos(a) * 1.5f);
                    VerticalLathe(B(W), p, new[] { new Vector2(0f, 0.06f), new Vector2(0.3f, 0.09f), new Vector2(0.75f, 0.05f), new Vector2(0.8f, 0.001f) }, 5);
                }
            }
            if (detail < 2)
            {
                var ring = new List<Vector3>();
                for (int i = 0; i <= seg; i++)
                {
                    float a = i / (float)seg * Mathf.PI * 2f;
                    ring.Add(basePos + new Vector3(Mathf.Sin(a) * 1.5f, balcony + (detail == 0 ? 0.85f : 0.5f), Mathf.Cos(a) * 1.5f));
                }
                B(W).TubePath(ring, detail == 0 ? 0.08f : 0.45f, 4, false);
            }
            // Gallery door on the qibla side of the upper shaft.
            B(Glass).Box(basePos + new Vector3(0f, balcony + 1.1f, 0.62f), new Vector3(0.5f, 1.8f, 0.12f));
            VerticalLathe(B(Lead), basePos + new Vector3(0f, capBase, 0f), new[] { new Vector2(0f, 0.82f), new Vector2(0.4f, 0.78f), new Vector2(height - capBase, 0.04f), new Vector2(height - capBase + 0.01f, 0.001f) }, seg);
            Alem(basePos + new Vector3(0f, height, 0f), 0.9f);
        }

        void Cornice1(Vector3 at, float size)
        {
            B(Marble).Box(at + new Vector3(0f, 0.15f, 0f), new Vector3(size + 0.5f, 0.3f, size + 0.5f));
        }

        // --- Classical palace buildings from footprints (Dolmabahçe, Çırağan and their service buildings) ---

        public const string ClockFace = "ClockFace";

        /// <summary>
        /// Palace building on its real footprint (outline in the model frame, x = east, y = north, counter-
        /// clockwise, ground at y = 0): walls, lead flat roof, and on every edge longer than 4 m a classical facade
        /// after the Balyan palaces: rusticated base, arched windows on the ground floor, pedimented windows above,
        /// pilasters between the bays, string courses, main cornice, balustrade with urns. Long palace facades get a
        /// central projection with columns and a pediment. The bay rhythm and storeys are typical, not surveyed.
        /// </summary>
        public void ClassicalBuilding(IList<Vector2> outline, float height, int seed, bool palace, float sink = 1.5f)
        {
            string W = Marble;
            int n = outline.Count;
            FootprintShell(W, outline, height, sink);
            for (int i = 0; i < n; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % n];
                float len = Vector2.Distance(a, b);
                if (len < 4f) continue;
                int edgeSeed = seed * 31 + i;
                OnEdge(a, b, () => ClassicalFacade(W, len, height, edgeSeed, palace));
            }
        }

        /// <summary>Runs build() in the facade frame of the footprint edge a→b (outline counter-clockwise, outward to its right).</summary>
        void OnEdge(Vector2 a, Vector2 b, System.Action build)
        {
            float len = Vector2.Distance(a, b);
            var nrm = new Vector2(b.y - a.y, -(b.x - a.x)) / len;
            var mid = (a + b) * 0.5f;
            InFrame(new Vector3(mid.x, 0f, mid.y), Mathf.Atan2(nrm.x, nrm.y) * Mathf.Rad2Deg, build);
        }

        /// <summary>Walls of a footprint from below ground to height, and a flat lead roof.</summary>
        void FootprintShell(string W, IList<Vector2> outline, float height, float sink)
        {
            int n = outline.Count;
            var mb = B(W);
            for (int i = 0; i < n; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % n];
                var outward = new Vector3(b.y - a.y, 0f, -(b.x - a.x));
                mb.QuadFacing(new Vector3(a.x, -sink, a.y), new Vector3(b.x, -sink, b.y), new Vector3(b.x, height, b.y), new Vector3(a.x, height, a.y),
                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up, outward);
            }
            var poly = new List<System.Numerics.Vector2>(n);
            foreach (var p in outline) poly.Add(new System.Numerics.Vector2(p.x, p.y));
            var tris = Apex.Environment.Polygon.Triangulate(poly);
            for (int t = 0; t + 2 < tris.Count; t += 3)
            {
                Vector2 a = outline[tris[t]], b = outline[tris[t + 1]], c = outline[tris[t + 2]];
                B(Lead).TriangleFacing(new Vector3(a.x, height - 0.2f, a.y), new Vector3(b.x, height - 0.2f, b.y), new Vector3(c.x, height - 0.2f, c.y), Vector2.zero, Vector2.right, Vector2.up, Vector3.up);
            }
        }

        public const string RowPlaster = "RowPlaster";
        public const string Brick = "Brick";
        /// <summary>Window glass of the Ortaköy Mosque: dark by day, lit warm from inside at night.</summary>
        public const string MosqueGlass = "MosqueGlass";
        /// <summary>Material of the window panes ArchWindow, RectWindow and the hall's great windows draw.</summary>
        string windowGlass = Glass;

        /// <summary>
        /// Esma Sultan Yalısı (1875, Ortaköy): three-storey brick mansion on the Bosphorus, burnt out in 1975; since
        /// 2001 the roofless shell holds a glass hall. Built on its OSM footprint (local, centroid at the origin,
        /// ground at y = 0): each wall in bays of about 3.6 m with brick piers, sill and head zones and open window
        /// voids; stone sills, keystones and string courses (detail 0-1); a ragged wall crown; inside, a glass and
        /// steel hall on a stone floor. Proportions are estimated, not surveyed.
        /// </summary>
        public void BrickShellYali(IList<Vector2> outline, float height, int seed)
        {
            int n = outline.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = outline[i], c = outline[(i + 1) % n];
                var e = c - a;
                float len = e.magnitude;
                if (len < 1.5f) continue;
                // Counter-clockwise footprint: the outside is to the right of the edge.
                var outward = new Vector2(e.y, -e.x) / len;
                var mid = (a + c) * 0.5f;
                int edgeSeed = seed * 31 + i;
                InFrame(new Vector3(mid.x, 0f, mid.y), Mathf.Atan2(outward.x, outward.y) * Mathf.Rad2Deg, () => ShellWall(len, height, edgeSeed));
            }
            // Glass hall and floor inside: the footprint's oriented bounding box along its longest edge.
            int longest = 0;
            float Len2(Vector2 d) => d.x * d.x + d.y * d.y;
            for (int i = 1; i < n; i++)
                if (Len2(outline[(i + 1) % n] - outline[i]) > Len2(outline[(longest + 1) % n] - outline[longest])) longest = i;
            var ax = (outline[(longest + 1) % n] - outline[longest]) / Mathf.Sqrt(Len2(outline[(longest + 1) % n] - outline[longest]));
            var ay = new Vector2(-ax.y, ax.x);
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in outline)
            {
                float u = p.x * ax.x + p.y * ax.y, v = p.x * ay.x + p.y * ay.y;
                minX = Mathf.Min(minX, u); maxX = Mathf.Max(maxX, u); minY = Mathf.Min(minY, v); maxY = Mathf.Max(maxY, v);
            }
            var rot = Quaternion.LookRotation(new Vector3(ax.x, 0f, ax.y));
            var centre2 = ax * ((minX + maxX) * 0.5f) + ay * ((minY + maxY) * 0.5f);
            var centre = new Vector3(centre2.x, 0f, centre2.y);
            float along = maxX - minX, across = maxY - minY;
            B(Stone).Box(centre + Vector3.up * 0.15f, new Vector3(Mathf.Max(1f, across - 1.2f), 0.3f, Mathf.Max(1f, along - 1.2f)), rot);
            float hallH = Mathf.Min(9f, height * 0.6f);
            float gx = Mathf.Max(2f, across - 6f), gz = Mathf.Max(2f, along - 6f);
            B(Glass).Box(centre + Vector3.up * (0.3f + hallH * 0.5f), new Vector3(gx, hallH, gz), rot);
            B(Steel).Box(centre + Vector3.up * (0.3f + hallH + 0.15f), new Vector3(gx + 0.4f, 0.3f, gz + 0.4f), rot);
            if (detail < 2)
            {
                // Steel mullions of the glass hall every 3 m on the long sides.
                for (float z = -gz * 0.5f; z <= gz * 0.5f + 0.01f; z += 3f)
                    foreach (float sx in new[] { -1f, 1f })
                        B(Steel).Box(centre + rot * new Vector3(sx * (gx * 0.5f + 0.05f), 0.3f + hallH * 0.5f, z), new Vector3(0.12f, hallH, 0.12f), rot);
            }
        }

        /// <summary>One brick wall of the shell in its facade frame (x along, y up, z out; wall 0.6 m thick inwards).</summary>
        void ShellWall(float len, float height, int seed)
        {
            const int floors = 3;
            const float t = 0.6f;
            float fh = height / floors;
            int bays = Mathf.Max(1, Mathf.RoundToInt(len / 3.6f));
            float bw = len / bays, pier = Mathf.Min(1.1f, bw * 0.35f), ow = bw - pier;
            for (int k = 0; k <= bays; k++)
            {
                float x = -len * 0.5f + k * bw;
                // Ragged crown of the burnt-out shell: the piers end at slightly different heights.
                float top = height + (detail < 2 ? 0.5f * Mathf.Sin(seed * 12.9898f + k * 78.233f) : 0f);
                B(Brick).Box(new Vector3(x, top * 0.5f, -t * 0.5f), new Vector3(pier, top, t));
            }
            for (int j = 0; j < bays; j++)
            {
                float xc = -len * 0.5f + (j + 0.5f) * bw;
                for (int f = 0; f < floors; f++)
                {
                    float y0 = f * fh, sill = f == 0 ? 1.4f : 1.0f, head = 0.9f;
                    B(Brick).Box(new Vector3(xc, y0 + sill * 0.5f, -t * 0.5f), new Vector3(ow, sill, t));
                    B(Brick).Box(new Vector3(xc, y0 + fh - head * 0.5f, -t * 0.5f), new Vector3(ow, head, t));
                    if (detail < 2)
                    {
                        B(Stone).Box(new Vector3(xc, y0 + sill + 0.05f, 0.05f), new Vector3(ow + 0.2f, 0.1f, 0.3f));
                        // Projecting brick header course and a stone keystone over the opening.
                        B(Brick).Box(new Vector3(xc, y0 + fh - head + 0.17f, 0.04f), new Vector3(ow + 0.3f, 0.34f, 0.1f));
                        B(Stone).Box(new Vector3(xc, y0 + fh - head + 0.22f, 0.07f), new Vector3(0.36f, 0.46f, 0.16f));
                    }
                }
            }
            if (detail < 2)
                for (int f = 1; f < floors; f++)
                    B(Stone).Box(new Vector3(0f, f * fh, 0.08f), new Vector3(len + 0.2f, 0.22f, 0.16f));
        }

        /// <summary>
        /// One Akaretler row house (Akaretler Sıra Evleri, 1875, Sarkis Balyan) on its OSM footprint: three storeys,
        /// street front (edge front) with a rusticated ground floor, arched door and window, two tall windows with
        /// a small iron balcony and triangular pediments on the first floor, segmental pediments on the second,
        /// corner pilasters, dentil cornice and parapet. The garden front gets plain framed windows; the long sides
        /// are party walls. Typical composition after photos of the row, not a survey of each house.
        /// </summary>
        public void RowHouse(IList<Vector2> outline, float height, int front, int seed)
        {
            string W = RowPlaster;
            int n = outline.Count;
            FootprintShell(W, outline, height, 1f);
            Vector2 a = outline[front], b = outline[(front + 1) % n];
            float len = Vector2.Distance(a, b);
            OnEdge(a, b, () => RowHouseFront(W, len, height, seed));
            if (n == 4)
            {
                Vector2 c = outline[(front + 2) % n], d = outline[(front + 3) % n];
                float back = Vector2.Distance(c, d);
                OnEdge(c, d, () =>
                {
                    float fh = height / 3f;
                    for (int f = 0; f < 3; f++)
                        foreach (float x in new[] { -back * 0.22f, back * 0.22f })
                            RectWindow(W, x, f * fh + 0.9f, 1f, fh * 0.5f, 0.14f);
                    Cornice(W, -back * 0.5f, back * 0.5f, height - 0.6f, 0.4f);
                });
            }
        }

        void RowHouseFront(string W, float len, float h, int seed)
        {
            float fh = (h - 0.8f) / 3f, half = len * 0.5f;
            B(Stone).Box(new Vector3(0f, fh * 0.5f, 0.12f), new Vector3(len, fh, 0.24f));
            if (detail == 0)
                for (float y = 0.5f; y < fh - 0.1f; y += 0.5f)
                    B(Stone).Box(new Vector3(0f, y, 0.25f), new Vector3(len, 0.05f, 0.03f));
            bool doorLeft = seed % 2 == 0;
            float dx = (doorLeft ? -1f : 1f) * len * 0.22f;
            // Door: arched, with steps; a window beside it.
            ArchWindow(W, dx, 0.35f, 1.3f, fh - 1.2f, 0.2f);
            if (detail < 2) for (int k = 0; k < 2; k++) B(Stone).Box(new Vector3(dx, 0.1f + k * 0.12f, 0.45f - k * 0.15f), new Vector3(1.9f, 0.2f + k * 0.12f, 0.35f));
            ArchWindow(W, -dx, 0.9f, 1.1f, fh - 1.1f, 0.18f);
            B(W).Box(new Vector3(0f, fh, 0.16f), new Vector3(len + 0.1f, 0.3f, 0.32f));
            for (int f = 1; f < 3; f++)
            {
                float y0 = f * fh;
                foreach (float x in new[] { -len * 0.22f, len * 0.22f })
                {
                    float wh = fh * (f == 1 ? 0.62f : 0.55f);
                    RectWindow(W, x, y0 + 0.5f, 1.05f, wh, 0.15f);
                    if (detail < 2) Pediment(W, x, y0 + 0.5f + wh + 0.35f, 1.7f, f == 1, 0.2f);
                }
                if (f == 1 && detail < 2)
                {
                    // Small iron balcony on stone corbels across both first-floor windows.
                    B(Stone).Box(new Vector3(0f, y0 + 0.35f, 0.45f), new Vector3(len * 0.75f, 0.14f, 0.9f));
                    if (detail == 0) Railing(new Vector3(-len * 0.37f, y0 + 0.42f, 0.86f), new Vector3(len * 0.37f, y0 + 0.42f, 0.86f), 0.9f);
                }
                B(W).Box(new Vector3(0f, y0 + fh, 0.1f), new Vector3(len, 0.2f, 0.2f));
            }
            if (detail < 2)
                foreach (float s in new[] { -1f, 1f })
                    B(W).Box(new Vector3(s * (half - 0.25f), h * 0.5f, 0.1f), new Vector3(0.5f, h, 0.2f));
            Cornice(W, -half, half, h - 0.8f, 0.6f);
            if (detail == 0)
                for (float x = -half + 0.2f; x < half; x += 0.35f)
                    B(W).Box(new Vector3(x, h - 0.88f, 0.12f), new Vector3(0.14f, 0.16f, 0.24f));   // dentils
            B(W).Box(new Vector3(0f, h + 0.3f, 0.12f), new Vector3(len, 0.6f, 0.24f));   // parapet
        }

        /// <summary>One classical facade in the facade frame (x from −len/2 to len/2).</summary>
        void ClassicalFacade(string W, float len, float h, int seed, bool palace)
        {
            float plinth = Mathf.Min(2.4f, h * 0.18f);
            int floors = Mathf.Max(1, Mathf.RoundToInt((h - plinth - 1.2f) / 5.2f));
            float fh = (h - 1.2f - plinth) / floors;
            int bays = Mathf.Max(1, Mathf.FloorToInt((len - 1.2f) / 4.4f));
            float bw = (len - 1.2f) / bays, x0 = -len * 0.5f + 0.6f;
            B(Stone).Box(new Vector3(0f, plinth * 0.5f, 0.15f), new Vector3(len, plinth, 0.3f));
            if (detail == 0)
                for (float y = 0.6f; y < plinth - 0.1f; y += 0.6f)
                    B(Stone).Box(new Vector3(0f, y, 0.31f), new Vector3(len, 0.06f, 0.04f));   // rustication joints
            // Central projection (risalit) on long palace facades: the middle three bays.
            int r0 = int.MaxValue, r1 = int.MinValue;
            if (palace && bays >= 7) { r0 = (bays - 3) / 2; r1 = r0 + 2; }
            float win = Mathf.Min(1.5f, bw * 0.4f);
            for (int i = 0; i < bays; i++)
            {
                if (i >= r0 && i <= r1) continue;
                float x = x0 + bw * (i + 0.5f);
                for (int f = 0; f < floors; f++)
                {
                    float y0 = plinth + f * fh;
                    if (f == 0 && floors > 1) ArchWindow(W, x, y0 + 0.5f, win, y0 + fh * 0.66f - win * 0.5f, 0.18f);
                    else
                    {
                        float wh = fh * 0.55f;
                        RectWindow(W, x, y0 + 0.7f, win, wh, 0.16f);
                        if (detail < 2) Pediment(W, x, y0 + 0.7f + wh + 0.4f, win + 0.7f, (i + f + seed) % 2 == 0, 0.25f);
                    }
                }
            }
            if (detail < 2)
            {
                for (int k = 0; k <= bays; k++)
                {
                    float x = x0 + bw * k;
                    if (k > r0 && k <= r1) continue;
                    B(W).Box(new Vector3(x, (plinth + h - 1.2f) * 0.5f, 0.12f), new Vector3(0.55f, h - 1.2f - plinth, 0.24f));
                    B(W).Box(new Vector3(x, h - 1.35f, 0.17f), new Vector3(0.8f, 0.3f, 0.34f));
                }
                for (int f = 1; f < floors; f++)
                    B(W).Box(new Vector3(0f, plinth + f * fh, 0.14f), new Vector3(len, 0.3f, 0.28f));
            }
            Cornice(W, -len * 0.5f, len * 0.5f, h - 1.2f, 0.8f);
            if (palace && detail < 2)
            {
                Balustrade(W, -len * 0.5f, len * 0.5f, h - 0.45f, 0.2f, 1f, false);
                for (int k = 0; k <= bays; k += 2) Urn(W, new Vector3(x0 + bw * k, h + 0.55f, 0.2f), 0.9f);
            }
            if (r0 > r1) return;
            // Risalit: projecting centre with paired columns, arched centre windows and a pediment.
            float rx0 = x0 + bw * r0, rx1 = x0 + bw * (r1 + 1), rw = rx1 - rx0, rc = (rx0 + rx1) * 0.5f;
            B(W).Box(new Vector3(rc, (h + 0.8f) * 0.5f - 0.75f, 0.45f), new Vector3(rw, h + 0.8f + 1.5f, 0.9f));
            InFrame(new Vector3(0f, 0f, 0.9f), 0f, () =>
            {
                B(Stone).Box(new Vector3(rc, plinth * 0.5f, 0.15f), new Vector3(rw + 0.2f, plinth, 0.3f));
                for (int f = 0; f < floors; f++)
                {
                    float y0 = plinth + f * fh;
                    ArchWindow(W, rc, y0 + 0.5f, Mathf.Min(2.4f, bw * 0.6f), y0 + fh * 0.72f - 1.2f, 0.25f);
                    foreach (float s in new[] { -1f, 1f })
                        RectWindow(W, rc + s * bw, y0 + 0.8f, win, fh * 0.5f, 0.16f);
                }
                if (detail < 2)
                    foreach (float s in new[] { -1f, 1f })
                        foreach (float off in new[] { 0.45f, 1.25f })
                            Column(W, rc + s * (rw * 0.5f - off), plinth, h - plinth - 0.9f, 0.32f);
                Cornice(W, rx0 - 0.2f, rx1 + 0.2f, h - 0.4f, 0.9f);
                Pediment(W, rc, h + 0.45f, rw + 0.4f, seed % 2 == 0, 0.6f);
            });
        }

        /// <summary>Window or portico pediment above (x, y): triangular, or segmental (shallow arc).</summary>
        void Pediment(string W, float x, float y, float width, bool triangular, float depth)
        {
            if (triangular)
            {
                Panel(B(W), new[] { new Vector2(x - width * 0.5f, y), new Vector2(x + width * 0.5f, y), new Vector2(x, y + width * 0.24f) }, depth + 0.05f, depth);
                return;
            }
            var p = new List<Vector2>();
            int seg = detail == 0 ? 8 : 4;
            float rise = width * 0.16f, r = (width * width * 0.25f + rise * rise) / (2f * rise), half = Mathf.Asin(width * 0.5f / r);
            for (int i = 0; i <= seg; i++)
            {
                float t = -half + 2f * half * i / seg;
                p.Add(new Vector2(x + r * Mathf.Sin(t), y + rise - r + r * Mathf.Cos(t)));
            }
            p.Reverse();
            p.Insert(0, new Vector2(x - width * 0.5f, y));
            Panel(B(W), p, depth + 0.05f, depth);
        }

        /// <summary>Central dome on a windowed drum over a palace hall (Dolmabahçe ceremonial hall: about 36 m to the top).</summary>
        public void HallDome(float baseY, float radius)
        {
            int seg = Seg(32);
            float drumH = 2.6f;
            VerticalLathe(B(Marble), new Vector3(0f, baseY - 0.5f, 0f), new[] { new Vector2(0f, radius + 0.5f), new Vector2(drumH + 0.5f, radius + 0.5f), new Vector2(drumH + 0.9f, radius + 1f), new Vector2(drumH + 0.91f, 0.01f) }, seg);
            int windows = detail == 0 ? 24 : 12;
            for (int i = 0; i < windows; i++)
            {
                float a = i * 360f / windows;
                InFrame(Quaternion.Euler(0f, a, 0f) * Vector3.forward * (radius + 0.48f) + Vector3.up * baseY, a, () => ArchWindow(Marble, 0f, 0.3f, 1.1f, drumH - 0.85f, 0.15f));
            }
            VerticalLathe(B(Lead), new Vector3(0f, baseY + drumH + 0.4f, 0f), Dome(radius, 0f, detail == 0 ? 12 : 6, -0.35f), seg);
            float top = baseY + drumH + 0.4f + radius * 0.65f;
            VerticalLathe(B(Lead), new Vector3(0f, top - 0.2f, 0f), new[] { new Vector2(0f, 1.6f), new Vector2(1.6f, 1.4f), new Vector2(1.7f, 1.8f), new Vector2(2.6f, 0.01f) }, Seg(12));
            Alem(new Vector3(0f, top + 2.4f, 0f), 1.2f);
        }

        /// <summary>
        /// Dolmabahçe Clock Tower (1890-95, Sarkis Balyan): neo-baroque, four storeys on an 8.5 m square base,
        /// 27 m high, clocks on the third storey, small dome with a finial; platform with corner fountain basins.
        /// Origin at the centre of the base, faces along ±x and ±z.
        /// </summary>
        public void ClockTowerDetailed()
        {
            string W = Marble;
            B(Stone).Box(new Vector3(0f, -1f, 0f), new Vector3(12f, 2.4f, 12f));
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                {
                    var c = new Vector3(sx * 5f, 0.2f, sz * 5f);
                    B(Stone).Box(c + new Vector3(0f, 0.25f, 0f), new Vector3(2f, 0.5f, 2f));
                    B(Glass).Box(c + new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.12f, 1.6f));   // water surface
                }
            // Storeys: (half width, bottom, top).
            var storeys = new[] { (4.25f, 0.2f, 3.2f), (3.8f, 3.2f, 11f), (3.2f, 11f, 16.5f), (2.8f, 16.5f, 21.5f) };
            for (int k = 0; k < storeys.Length; k++)
            {
                var (hw, y0, y1) = storeys[k];
                B(W).Box(new Vector3(0f, (y0 + y1) * 0.5f, 0f), new Vector3(hw * 2f, y1 - y0, hw * 2f));
                int storey = k;
                for (int f = 0; f < 4; f++)
                {
                    float yaw = f * 90f;
                    InFrame(Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * hw, yaw, () =>
                    {
                        if (storey == 0) { B(Stone).Box(new Vector3(0f, (y1 - y0) * 0.5f + y0, 0.1f), new Vector3(hw * 2f, y1 - y0, 0.2f)); return; }
                        if (detail < 2)
                            foreach (float s in new[] { -1f, 1f })
                                foreach (float off in new[] { 0.35f, 0.95f })
                                    Column(W, s * (hw - off), y0, y1 - y0 - 0.8f, storey == 1 ? 0.24f : 0.2f);
                        if (storey == 1) { ArchWindow(W, 0f, y0 + 0.4f, 2.2f, y0 + 4.6f, 0.3f); Pediment(W, 0f, y0 + 6.6f, 3f, true, 0.3f); }
                        if (storey == 2) ArchWindow(W, 0f, y0 + 0.8f, 1.4f, y0 + 3.4f, 0.22f);
                        if (storey == 3) ClockDial(new Vector3(0f, (y0 + y1) * 0.5f, 0.05f), 1.45f);
                        Cornice(W, -hw - 0.2f, hw + 0.2f, y1 - 0.8f, 0.55f);
                    });
                }
                if (k > 0 && detail < 2)
                    foreach (float sx in new[] { -1f, 1f })
                        foreach (float sz in new[] { -1f, 1f })
                            Urn(W, new Vector3(sx * (hw - 0.2f), y1, sz * (hw - 0.2f)), 0.7f);
            }
            // Octagonal lantern and dome.
            VerticalLathe(B(W), new Vector3(0f, 21.5f, 0f), new[] { new Vector2(0f, 2.3f), new Vector2(2.4f, 2.2f), new Vector2(2.6f, 2.6f), new Vector2(2.9f, 2.6f), new Vector2(2.91f, 0.01f) }, 8);
            if (detail < 2)
                for (int i = 0; i < 8; i++)
                {
                    float a = 22.5f + i * 45f;
                    InFrame(Quaternion.Euler(0f, a, 0f) * Vector3.forward * 2.15f + Vector3.up * 21.5f, a, () => ArchWindow(W, 0f, 0.3f, 0.8f, 1.6f, 0.12f));
                }
            VerticalLathe(B(Lead), new Vector3(0f, 24.4f, 0f), Dome(2.3f, 0f, detail == 0 ? 8 : 4, 0.1f), Seg(16));
            Alem(new Vector3(0f, 26.9f, 0f), 0.5f);
        }

        void ClockDial(Vector3 c, float r)
        {
            var ring = new List<Vector2>();
            int seg = detail == 0 ? 24 : 12;
            for (int i = 0; i < seg; i++) { float t = Mathf.PI * 2f * i / seg; ring.Add(new Vector2(c.x + Mathf.Cos(t) * r, c.y + Mathf.Sin(t) * r)); }
            Panel(B(ClockFace), ring, c.z + 0.08f, 0.08f);
            if (detail >= 2) return;
            ArchBandRing(B(Gilt), c, r, r + 0.18f, c.z, 0.12f, seg);
            // Hands at ten past ten.
            B(Lead).Box(new Vector3(c.x - 0.3f, c.y + 0.25f, c.z + 0.12f), new Vector3(0.08f, 0.8f, 0.03f), Quaternion.Euler(0f, 0f, 50f));
            B(Lead).Box(new Vector3(c.x + 0.42f, c.y + 0.35f, c.z + 0.12f), new Vector3(0.06f, 1.15f, 0.03f), Quaternion.Euler(0f, 0f, -50f));
        }

        static void ArchBandRing(MeshBuilder mb, Vector3 c, float rIn, float rOut, float z0, float depth, int seg)
        {
            ArchBand(mb, c.x, c.y, rIn, rOut, z0, depth, seg / 2);
            int b0 = mb.VertexCount;
            ArchBand(mb, 0f, 0f, rIn, rOut, z0, depth, seg / 2);
            // Lower half: the upper half rotated by 180° about z, then moved to the centre.
            mb.TransformLast(mb.VertexCount - b0, v => new Vector3(c.x - v.x, c.y - v.y, v.z));
        }

        // --- Monumental palace gates (Dolmabahçe: Saltanat Kapısı, Hazine Kapısı) ---------------

        /// <summary>
        /// Monumental palace gate after the two Dolmabahçe show gates: a triumphal-arch block (width × depth from the
        /// OSM footprint, long axis along x, passage along z) with a round-arched passage and wrought-iron gate
        /// leaves, paired columns on pedestals on both faces, niches in the side bays, a deep cornice, an attic with a
        /// gilded medallion (no inscription) under a segmental pediment, and urns. Origin at the footprint centre.
        /// </summary>
        public void GateMonument(float width, float depth, float height)
        {
            string W = Marble;
            float cornice = height * 0.72f, r = Mathf.Min(3.2f, width * 0.16f), spring = cornice * 0.6f;
            float half = width * 0.5f, hd = depth * 0.5f;
            B(Stone).Box(new Vector3(0f, -0.4f, 0f), new Vector3(width + 1f, 1.6f, depth + 1f));
            ArchedWall(W, half, spring, r, cornice, -hd, depth);
            // Gate leaves in the passage.
            var leaf = ArchOutline(0f, 0f, r * 2f, spring, ArcSeg(r));
            if (detail == 0)
            {
                for (float x = -r + 0.12f; x < r; x += 0.14f)
                {
                    float top = spring + Mathf.Sqrt(Mathf.Max(0f, r * r - x * x)) - 0.1f;
                    B(Lead).Box(new Vector3(x, top * 0.5f, 0f), new Vector3(0.035f, top, 0.035f));
                    if (Mathf.Abs(x) < r - 0.4f && Mathf.Repeat(x, 0.56f) < 0.14f) B(Gilt).Box(new Vector3(x, top + 0.06f, 0f), new Vector3(0.08f, 0.14f, 0.08f));
                }
                foreach (float y in new[] { 0.25f, 2.2f, spring - 0.2f })
                    B(Lead).Box(new Vector3(0f, y, 0f), new Vector3(r * 2f, 0.08f, 0.06f));
                B(Gilt).Box(new Vector3(0f, spring * 0.5f, 0f), new Vector3(0.06f, spring, 0.08f));
            }
            else Panel(B(Lead), leaf, 0.04f, 0.08f);
            // Both faces.
            foreach (float yaw in new[] { 0f, 180f })
            {
                var n = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                InFrame(n * hd, yaw, () =>
                {
                    B(Stone).Box(new Vector3(0f, 0.6f, 0.2f), new Vector3(width + 0.6f, 1.2f, 0.4f));
                    ArchBand(B(W), 0f, spring, r, r + 0.55f, 0f, 0.3f, ArcSeg(r));
                    foreach (float sx in new[] { -1f, 1f })
                        B(W).Box(new Vector3(sx * (r + 0.27f), spring * 0.5f, 0.15f), new Vector3(0.55f, spring, 0.3f));
                    B(W).Box(new Vector3(0f, spring + r + 0.45f, 0.35f), new Vector3(0.9f, 1.1f, 0.7f));   // keystone
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        // Paired columns on pedestals flanking the arch.
                        foreach (float off in new[] { 1.05f, 2.25f })
                        {
                            float x = sx * (r + off);
                            B(W).Box(new Vector3(x, 1.2f + 0.9f, 0.55f), new Vector3(1f, 1.8f, 1.1f));
                            if (detail < 2) InFrame(new Vector3(0f, 0f, 0.5f), 0f, () => Column(W, x, 3f, cornice - 3.3f, 0.38f));
                            else B(W).Box(new Vector3(x, (cornice + 3f) * 0.5f, 0.55f), new Vector3(0.7f, cornice - 3f, 0.7f));
                        }
                        // Side bay: arched niche with a statue-less shell head, a framed panel above.
                        float bx = sx * (r + 2.25f + (half - r - 2.25f) * 0.5f + 0.3f);
                        if (half - r - 2.8f > 1.4f)
                        {
                            ArchWindow(W, bx, 2.4f, 1.3f, cornice * 0.5f, 0.2f);
                            if (detail < 2)
                            {
                                Pediment(W, bx, cornice * 0.5f + 1.25f, 2f, true, 0.25f);
                                B(W).Box(new Vector3(bx, cornice - 1.9f, 0.08f), new Vector3(1.8f, 1.1f, 0.16f));
                            }
                        }
                    }
                    Cornice(W, -half - 0.3f, half + 0.3f, cornice - 0.8f, 1.1f);
                    // Attic with the medallion, segmental pediment and urns.
                    float attic = height - cornice;
                    B(W).Box(new Vector3(0f, cornice + attic * 0.4f, 0.2f), new Vector3(r * 2f + 4.6f, attic * 0.8f, 0.4f));
                    ClockDialLike(new Vector3(0f, cornice + attic * 0.42f, 0.45f), Mathf.Min(1.1f, attic * 0.3f));
                    Pediment(W, 0f, cornice + attic * 0.8f, r * 2f + 5.2f, false, 0.7f);
                    if (detail < 2)
                        foreach (float sx in new[] { -1f, 1f })
                        {
                            Urn(W, new Vector3(sx * (half - 0.4f), cornice, 0.3f), 1.2f);
                            Urn(W, new Vector3(sx * (r + 2.6f), cornice + attic * 0.8f, 0.3f), 0.9f);
                        }
                });
            }
            // Attic core and roof.
            B(W).Box(new Vector3(0f, cornice + (height - cornice) * 0.4f, 0f), new Vector3(r * 2f + 4.6f, (height - cornice) * 0.8f, depth));
            B(Lead).Box(new Vector3(0f, cornice + 0.05f, 0f), new Vector3(width, 0.1f, depth));
        }

        // --- Dolmabahçe sea gates (yalı kapıları) on the palace quay ---------------------------------

        /// <summary>
        /// Sea gate of Dolmabahçe on the palace quay (PalaceWaterfront): a white marble portal with a round-arched
        /// opening and wrought-iron gate leaves, columns on pedestals on both faces (paired on the main gate), a
        /// moulded archivolt with keystone, an entablature, and a baroque crest – a raised cartouche with a gilded
        /// medallion (no inscription) under an arched hood, flanked by stepped scroll blocks and urns. The main gate on
        /// the axis of the ceremonial hall is larger and richer than the four others. Origin at the threshold on the
        /// quay top, opening across x, +z towards the water. After photographs of the Bosphorus front; proportions
        /// approximate (no measured drawings available here).
        /// </summary>
        public void SeaGate(bool main)
        {
            string W = Marble;
            float r = main ? 2.3f : 1.6f;                 // half the opening
            float spring = main ? 5.2f : 3.9f;            // springing line of the arch
            float pier = main ? 1.7f : 1.15f;             // pier width each side
            float depth = main ? 1.5f : 1.1f;
            float half = r + pier, cornice = spring + r + (main ? 1.3f : 1.0f);
            float hd = depth * 0.5f;
            // Plinth on the quay, the arched portal body.
            B(Stone).Box(new Vector3(0f, -0.15f, 0f), new Vector3(half * 2f + 0.6f, 0.3f, depth + 0.8f));
            ArchedWall(W, half, spring, r, cornice, -hd, depth);
            // Gate leaves: vertical bars with gilt spear tips, three rails, a centre stile.
            if (detail == 0)
            {
                for (float x = -r + 0.11f; x < r; x += 0.13f)
                {
                    float top = spring + Mathf.Sqrt(Mathf.Max(0f, r * r - x * x)) - 0.12f;
                    B(Lead).Box(new Vector3(x, top * 0.5f, 0f), new Vector3(0.035f, top, 0.035f));
                    if (Mathf.Repeat(x + r, 0.52f) < 0.13f) B(Gilt).Box(new Vector3(x, top + 0.05f, 0f), new Vector3(0.07f, 0.12f, 0.07f));
                }
                foreach (float y in new[] { 0.3f, spring * 0.45f, spring - 0.15f })
                    B(Lead).Box(new Vector3(0f, y, 0f), new Vector3(r * 2f, 0.09f, 0.06f));
                // Scrollwork band at mid height (rings), and the gilt centre stile.
                for (float x = -r + 0.35f; x < r - 0.2f; x += 0.7f)
                    ArchBandRing(B(Lead), new Vector3(x, spring * 0.45f + 0.35f, 0f), 0.22f, 0.27f, -0.02f, 0.04f, 10);
                B(Gilt).Box(new Vector3(0f, spring * 0.5f, 0f), new Vector3(0.07f, spring, 0.08f));
            }
            else Panel(B(Lead), ArchOutline(0f, 0f, r * 2f, spring, ArcSeg(r)), 0.04f, 0.08f);
            foreach (float yaw in new[] { 0f, 180f })
            {
                var n = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                InFrame(n * hd, yaw, () =>
                {
                    // Archivolt and keystone.
                    ArchBand(B(W), 0f, spring, r, r + (main ? 0.5f : 0.38f), 0f, 0.28f, ArcSeg(r));
                    B(W).Box(new Vector3(0f, spring + r + 0.3f, 0.3f), new Vector3(main ? 0.8f : 0.6f, main ? 1.0f : 0.8f, 0.6f));
                    // Columns on pedestals: paired on the main gate.
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        var offs = main ? new[] { 0.55f, 1.3f } : new[] { 0.6f };
                        foreach (float off in offs)
                        {
                            float x = sx * (r + off);
                            B(W).Box(new Vector3(x, 0.85f, 0.4f), new Vector3(0.75f, 1.7f, 0.8f));
                            if (detail < 2) InFrame(new Vector3(0f, 0f, 0.35f), 0f, () => Column(W, x, 1.7f, cornice - 2.0f, main ? 0.3f : 0.24f));
                            else B(W).Box(new Vector3(x, (cornice + 1.7f) * 0.5f, 0.4f), new Vector3(0.55f, cornice - 1.7f, 0.55f));
                        }
                        // Garland panel between the column and the arch (raised tablet).
                        if (detail == 0)
                            B(W).Box(new Vector3(sx * (r + (main ? 0.92f : 0.3f)), spring + 0.6f, 0.12f), new Vector3(main ? 0.35f : 0.25f, 1.2f, 0.2f));
                    }
                    Cornice(W, -half - 0.2f, half + 0.2f, cornice - 0.6f, main ? 0.9f : 0.7f);
                    // Crest: cartouche with the gilded medallion under an arched hood, scroll blocks and urns.
                    float cw = main ? 3.2f : 2.2f, ch = main ? 2.4f : 1.7f, y0 = cornice + 0.25f;
                    B(W).Box(new Vector3(0f, y0 + ch * 0.5f, 0.15f), new Vector3(cw, ch, 0.5f));
                    ClockDialLike(new Vector3(0f, y0 + ch * 0.5f, 0.45f), main ? 0.75f : 0.5f);
                    if (detail < 2)
                    {
                        ArchBand(B(W), 0f, y0 + ch, cw * 0.5f - 0.15f, cw * 0.5f + 0.15f, 0f, 0.45f, ArcSeg(cw * 0.5f));
                        Urn(W, new Vector3(0f, y0 + ch + cw * 0.5f, 0.2f), main ? 0.9f : 0.65f);
                        foreach (float sx in new[] { -1f, 1f })
                        {
                            // Stepped scroll blocks falling away from the cartouche.
                            for (int k = 0; k < 3; k++)
                            {
                                float w = (main ? 0.9f : 0.6f) - k * 0.15f;
                                B(W).Box(new Vector3(sx * (cw * 0.5f + w * 0.5f + k * w * 0.8f), y0 + ch * (0.62f - k * 0.22f), 0.15f), new Vector3(w, ch * 0.3f, 0.45f));
                            }
                            Urn(W, new Vector3(sx * (half - 0.35f), cornice + 0.2f, 0.25f), main ? 1.0f : 0.75f);
                        }
                    }
                });
            }
            // Crest core between the two faces.
            float cwi = main ? 3.2f : 2.2f, chi = main ? 2.4f : 1.7f;
            B(W).Box(new Vector3(0f, cornice + 0.25f + chi * 0.5f, 0f), new Vector3(cwi, chi, depth));
            B(Lead).Box(new Vector3(0f, cornice + 0.05f, 0f), new Vector3(half * 2f, 0.1f, depth));
        }

        /// <summary>Gilded round medallion (frame and disc), as on the palace gates; no lettering.</summary>
        void ClockDialLike(Vector3 c, float r)
        {
            var ring = new List<Vector2>();
            int seg = detail == 0 ? 24 : 12;
            for (int i = 0; i < seg; i++) { float t = Mathf.PI * 2f * i / seg; ring.Add(new Vector2(c.x + Mathf.Cos(t) * r, c.y + Mathf.Sin(t) * r)); }
            Panel(B(Gilt), ring, c.z, 0.1f);
            if (detail < 2) ArchBandRing(B(W0), c, r, r + 0.25f, c.z - 0.05f, 0.15f, seg);
        }

        const string W0 = Marble;

        /// <summary>
        /// Wall from y = 0 to top across x = ±halfW, z = z0 .. z0 + depth, with a round-arched passage of radius r
        /// springing at `spring` (piers, spandrels on both faces, the arch soffit).
        /// </summary>
        void ArchedWall(string mat, float halfW, float spring, float r, float top, float z0, float depth)
        {
            var mb = B(mat);
            float z1 = z0 + depth;
            foreach (float s in new[] { -1f, 1f })
                mb.Box(new Vector3(s * (halfW + r) * 0.5f, top * 0.5f, (z0 + z1) * 0.5f), new Vector3(halfW - r, top, depth));
            // Angles of the arch, including the two where the rays from the centre hit the spandrel's top corners.
            float corner = Mathf.Atan2(top - spring, r);
            var angles = new List<float>();
            int seg = ArcSeg(r) * 2;
            for (int i = 0; i <= seg; i++) angles.Add(Mathf.PI * i / seg);
            angles.Add(corner); angles.Add(Mathf.PI - corner);
            angles.Sort();
            Vector2 Arc(float t) => new Vector2(r * Mathf.Cos(t), spring + r * Mathf.Sin(t));
            Vector2 Rim(float t)
            {
                float c = Mathf.Cos(t), sn = Mathf.Sin(t);
                float sSide = Mathf.Abs(c) > 1e-5f ? r / Mathf.Abs(c) : float.MaxValue;
                float sTop = sn > 1e-5f ? (top - spring) / sn : float.MaxValue;
                float k = Mathf.Min(sSide, sTop);
                return new Vector2(c * k, spring + sn * k);
            }
            for (int i = 0; i + 1 < angles.Count; i++)
            {
                float a = angles[i], b = angles[i + 1];
                if (b - a < 1e-5f) continue;
                Vector2 p0 = Arc(a), p1 = Arc(b), q0 = Rim(a), q1 = Rim(b);
                foreach (var (z, f) in new[] { (z1, Vector3.forward), (z0, Vector3.back) })
                    mb.QuadFacing(new Vector3(p0.x, p0.y, z), new Vector3(p1.x, p1.y, z), new Vector3(q1.x, q1.y, z), new Vector3(q0.x, q0.y, z), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, f);
                var inward = -new Vector3(Mathf.Cos((a + b) * 0.5f), Mathf.Sin((a + b) * 0.5f), 0f);
                mb.QuadFacing(new Vector3(p0.x, p0.y, z0), new Vector3(p1.x, p1.y, z0), new Vector3(p1.x, p1.y, z1), new Vector3(p0.x, p0.y, z1), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, inward);
            }
            mb.QuadFacing(new Vector3(-r, top, z0), new Vector3(r, top, z0), new Vector3(r, top, z1), new Vector3(-r, top, z1), Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector3.up);
        }
    }
}
