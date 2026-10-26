using System.Collections.Generic;
using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// Meshes of the modular circuit kit (see Apex.Tracks.TrackKitLayout) in three detail levels
    /// (0 near, 1 mid, 2 far). Local frame: +z along the module (nominal length centred on 0),
    /// +x away from the track, +y up, y = 0 on the run-off level. Origins: barrier modules at the
    /// barrier face, fence modules on the fence line, impact protection at its centre.
    /// Original designs after the common types used on street circuits (precast safety-shape
    /// blocks, W-beam guardrail, catch fence with cables and overhang, energy-absorbing modules,
    /// bolted tyre stacks); no manufacturer's branding. Procedural placeholder art: shapes and
    /// proportions are modelled, surface detail comes from the materials.
    /// </summary>
    public static class TrackKitGeometry
    {
        public const string Dark = "KitDark";
        public const string Strap = "KitStrap";
        public const string Reflector = "KitReflector";
        public const string BannerBack = "KitBannerBack";
        public static string Banner(int variant) => "KitBanner" + variant;
        public const int BannerVariants = 3;

        public const float BarrierHeight = TrackInfrastructureGeometry.BarrierHeight;
        public const float FenceTop = BarrierHeight + TrackInfrastructureGeometry.FenceHeight;

        public delegate MeshBuilder Parts(string material);

        // --- helpers ------------------------------------------------------------------------

        static Vector2 Centroid(IList<Vector2> poly)
        {
            var c = Vector2.zero;
            foreach (var p in poly) c += p;
            return c / poly.Count;
        }

        static Vector3 At(Vector2 p, float z) => new Vector3(p.x, p.y, z);

        /// <summary>
        /// Extrudes a closed (x, y) profile along z from z0 to z1. With bevel > 0 the profile shrinks
        /// towards its centroid by `bevel` over the last `bevel` metres at both ends (chamfered block
        /// ends). u runs along z (metres / uLength), v along the profile perimeter (metres / vLength).
        /// </summary>
        static void Extrude(MeshBuilder mb, IList<Vector2> prof, float z0, float z1, float bevel, bool caps, float uLength, float vLength, bool closed = true)
        {
            var c = Centroid(prof);
            int n = prof.Count;
            Vector2 Shrink(Vector2 p)
            {
                var d = c - p;
                float len = d.magnitude;
                return len < 1e-5f ? p : p + d / len * Mathf.Min(bevel, len * 0.5f);
            }
            var perim = new float[n + 1];
            for (int i = 1; i <= n; i++) perim[i] = perim[i - 1] + (prof[i % n] - prof[i - 1]).magnitude;
            var zs = bevel > 0f ? new[] { z0, z0 + bevel, z1 - bevel, z1 } : new[] { z0, z1 };
            int segments = closed ? n : n - 1;
            for (int s = 0; s + 1 < zs.Length; s++)
            {
                bool shrinkA = bevel > 0f && s == 0, shrinkB = bevel > 0f && s == zs.Length - 2;
                for (int i = 0; i < segments; i++)
                {
                    var p = prof[i];
                    var q = prof[(i + 1) % n];
                    var pa = shrinkA ? Shrink(p) : p; var qa = shrinkA ? Shrink(q) : q;
                    var pb = shrinkB ? Shrink(p) : p; var qb = shrinkB ? Shrink(q) : q;
                    var edge = q - p;
                    var mid = (p + q) * 0.5f;
                    var nrm = new Vector2(edge.y, -edge.x);
                    if (nrm.x * (mid.x - c.x) + nrm.y * (mid.y - c.y) < 0f) nrm = nrm * -1f;
                    var outward = new Vector3(nrm.x, nrm.y, shrinkA ? -0.7f : shrinkB ? 0.7f : 0f);
                    float va = perim[i] / vLength, vb = perim[i + 1] / vLength;
                    mb.QuadFacing(At(pa, zs[s]), At(qa, zs[s]), At(qb, zs[s + 1]), At(pb, zs[s + 1]),
                        new Vector2(zs[s] / uLength, va), new Vector2(zs[s] / uLength, vb), new Vector2(zs[s + 1] / uLength, vb), new Vector2(zs[s + 1] / uLength, va), outward);
                }
            }
            if (!caps) return;
            foreach (var (z, dir) in new[] { (z0, -1f), (z1, 1f) })
            {
                var cc = At(c, z);
                for (int i = 0; i < n; i++)
                {
                    var p = bevel > 0f ? Shrink(prof[i]) : prof[i];
                    var q = bevel > 0f ? Shrink(prof[(i + 1) % n]) : prof[(i + 1) % n];
                    mb.TriangleFacing(cc, At(p, z), At(q, z), c, p, q, new Vector3(0f, 0f, dir));
                }
            }
        }

        /// <summary>Flat panel in the y-z plane at x, from y0 to y1 and z0 to z1, front facing -x (the track) or both ways.</summary>
        static void PanelX(MeshBuilder mb, float x, float y0, float y1, float z0, float z1, float uScale, float vScale, bool bothSides)
        {
            var a = new Vector3(x, y0, z0); var b = new Vector3(x, y0, z1);
            var c = new Vector3(x, y1, z1); var d = new Vector3(x, y1, z0);
            var ua = new Vector2(z0 * uScale, y0 * vScale); var ub = new Vector2(z1 * uScale, y0 * vScale);
            var uc = new Vector2(z1 * uScale, y1 * vScale); var ud = new Vector2(z0 * uScale, y1 * vScale);
            mb.QuadFacing(a, b, c, d, ua, ub, uc, ud, Vector3.left);
            if (bothSides) mb.QuadFacing(a, b, c, d, ua, ub, uc, ud, Vector3.right);
        }

        // --- concrete barrier block ---------------------------------------------------------

        /// <summary>
        /// Precast safety-shape barrier block, 1.0 m high, 0.6 m base, 0.15 m top: toe, lower slope
        /// (55°), near-vertical upper face, chamfered top edges and block ends (the joint reveal reads
        /// as a dark line between blocks), lifting recesses, joint pins, a drainage slot and a
        /// reflector on the face. Face at x = 0, length `length` along z.
        /// </summary>
        public static void BarrierBlock(Parts B, int lod, float length = Apex.Tracks.TrackKitLayout.BlockLength)
        {
            float h = BarrierHeight, z0 = -length * 0.5f, z1 = length * 0.5f;
            var mb = B(TrackInfrastructureGeometry.Concrete);
            if (lod >= 2)
            {
                Extrude(mb, new[] { new Vector2(0f, 0f), new Vector2(0.2f, h), new Vector2(0.4f, h), new Vector2(0.6f, 0f) }, z0, z1, 0f, false, length, 1.6f, closed: false);
                return;
            }
            Vector2[] prof = lod == 0
                ? new[] { new Vector2(0f, 0f), new Vector2(0f, 0.075f), new Vector2(0.18f, 0.33f), new Vector2(0.243f, h - 0.025f), new Vector2(0.265f, h),
                          new Vector2(0.385f, h), new Vector2(0.407f, h - 0.025f), new Vector2(0.6f, 0f) }
                : new[] { new Vector2(0f, 0f), new Vector2(0f, 0.075f), new Vector2(0.18f, 0.33f), new Vector2(0.25f, h), new Vector2(0.4f, h), new Vector2(0.6f, 0f) };
            Extrude(mb, prof, z0, z1, lod == 0 ? 0.02f : 0f, true, length, 1.6f, closed: false);
            if (lod > 0) return;
            // Lifting recesses on the top and joint pins at both ends.
            foreach (float z in new[] { -length * 0.25f, length * 0.25f })
                B(Dark).Box(new Vector3(0.325f, h + 0.002f, z), new Vector3(0.07f, 0.006f, 0.16f));
            foreach (float z in new[] { z0 + 0.07f, z1 - 0.07f })
                B(TrackInfrastructureGeometry.Steel).Tube(new Vector3(0.325f, h - 0.01f, z), new Vector3(0.325f, h + 0.035f, z), 0.016f, 6, true);
            // Drainage slot at the foot of the face and a reflector on the upper face.
            PanelX(B(Dark), -0.004f, 0.0f, 0.05f, -0.12f, 0.12f, 1f, 1f, false);
            var r0 = new Vector3(0.226f, 0.78f, -0.05f); var r1 = new Vector3(0.226f, 0.78f, 0.05f);
            var r2 = new Vector3(0.232f, 0.84f, 0.05f); var r3 = new Vector3(0.232f, 0.84f, -0.05f);
            var lift = new Vector3(-0.004f, 0f, 0f);
            B(Reflector).QuadFacing(r0 + lift, r1 + lift, r2 + lift, r3 + lift, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, new Vector3(-1f, 0.1f, 0f));
        }

        // --- guardrail ----------------------------------------------------------------------

        /// <summary>W-beam guardrail span (rail face at x = 0, 0.45-0.76 m) with a C-post and spacer block at the span start.</summary>
        public static void GuardrailSpan(Parts B, int lod, float length = Apex.Tracks.TrackKitLayout.GuardrailSpanLength)
        {
            float z0 = -length * 0.5f, z1 = length * 0.5f;
            var rail = B(TrackInfrastructureGeometry.Armco);
            Vector2[] w = lod == 0
                ? new[] { new Vector2(0.083f, 0.45f), new Vector2(0.06f, 0.49f), new Vector2(0f, 0.565f), new Vector2(0.035f, 0.606f),
                          new Vector2(0f, 0.647f), new Vector2(0.06f, 0.722f), new Vector2(0.083f, 0.762f) }
                : lod == 1 ? new[] { new Vector2(0.083f, 0.45f), new Vector2(0f, 0.565f), new Vector2(0f, 0.647f), new Vector2(0.083f, 0.762f) }
                : new[] { new Vector2(0.04f, 0.45f), new Vector2(0.04f, 0.762f) };
            for (int i = 0; i + 1 < w.Length; i++)
            {
                var a = At(w[i], z0); var b = At(w[i], z1); var c = At(w[i + 1], z1); var d = At(w[i + 1], z0);
                var ua = new Vector2(0f, i / 6f); var ub = new Vector2(length / 4f, i / 6f);
                var uc = new Vector2(length / 4f, (i + 1) / 6f); var ud = new Vector2(0f, (i + 1) / 6f);
                rail.QuadFacing(a, b, c, d, ua, ub, uc, ud, Vector3.left);
                rail.QuadFacing(a, b, c, d, ua, ub, uc, ud, Vector3.right);
            }
            var steel = B(TrackInfrastructureGeometry.Steel);
            float pz = z0 + 0.1f;
            steel.Box(new Vector3(0.22f, 0.42f, pz), new Vector3(0.075f, 0.84f, 0.15f));
            if (lod == 0)
            {
                steel.Box(new Vector3(0.135f, 0.6f, pz), new Vector3(0.1f, 0.33f, 0.15f));   // spacer block
                foreach (float y in new[] { 0.565f, 0.647f })
                    B(Dark).Box(new Vector3(-0.004f, y, pz), new Vector3(0.012f, 0.03f, 0.03f));   // splice bolts
            }
        }

        // --- catch fence --------------------------------------------------------------------

        /// <summary>Fence post on the barrier top (variant 0) or from the ground behind a guardrail (variant 1), with base plate, cable clamps and the overhang arm leaning towards the track.</summary>
        public static void FencePost(Parts B, int lod, int variant)
        {
            float y0 = variant == 1 ? 0f : BarrierHeight, top = FenceTop;
            var steel = B(TrackInfrastructureGeometry.Steel);
            var hinge = new Vector3(0f, top, 0f);
            var tip = hinge + new Vector3(-0.52f, 0.74f, 0f);
            if (lod >= 2)
            {
                steel.Box(new Vector3(0f, (y0 + top) * 0.5f, 0f), new Vector3(0.11f, top - y0, 0.11f));
                return;
            }
            int sides = lod == 0 ? 8 : 5;
            steel.Tube(new Vector3(0f, y0, 0f), hinge, 0.057f, sides, true);
            steel.Tube(hinge, tip, 0.045f, sides, true);
            if (lod > 0) return;
            steel.Box(new Vector3(0f, y0 + 0.01f, 0f), new Vector3(0.24f, 0.02f, 0.24f));
            // Diagonal stay behind the post and the cable clamps.
            steel.Tube(new Vector3(0.0f, y0 + 1.6f, 0f), new Vector3(0.55f, y0, 0f), 0.035f, 6, true);
            foreach (float y in CableHeights(variant))
                B(Dark).Box(new Vector3(-0.06f, y, 0f), new Vector3(0.05f, 0.06f, 0.09f));
        }

        static float[] CableHeights(int variant) => variant == 1 ? new[] { 0.9f, 2.0f, 3.1f } : new[] { 1.6f, 2.6f, 3.6f };

        /// <summary>Fence panel between two posts: mesh (alpha material), top rail, horizontal cables and the overhang.</summary>
        public static void FencePanel(Parts B, int lod, int variant, float length = Apex.Tracks.TrackKitLayout.FencePostSpacing)
        {
            float y0 = variant == 1 ? 0f : BarrierHeight, top = FenceTop;
            float z0 = -length * 0.5f, z1 = length * 0.5f;
            var mesh = B(TrackInfrastructureGeometry.Fence);
            PanelX(mesh, -0.02f, y0, top, z0, z1, 0.5f, 0.5f, true);
            // Overhang: mesh from the hinge to the arm tip.
            var a = new Vector3(-0.02f, top, z0); var b = new Vector3(-0.02f, top, z1);
            var c = new Vector3(-0.54f, top + 0.74f, z1); var d = new Vector3(-0.54f, top + 0.74f, z0);
            foreach (var face in new[] { new Vector3(-0.8f, -0.6f, 0f), new Vector3(0.8f, 0.6f, 0f) })
                mesh.QuadFacing(a, b, c, d, new Vector2(z0 * 0.5f, 0f), new Vector2(z1 * 0.5f, 0f), new Vector2(z1 * 0.5f, 0.45f), new Vector2(z0 * 0.5f, 0.45f), face);
            if (lod >= 2) return;
            var steel = B(TrackInfrastructureGeometry.Steel);
            steel.Tube(new Vector3(-0.02f, top, z0), new Vector3(-0.02f, top, z1), 0.03f, lod == 0 ? 6 : 4, false);
            if (lod > 0) return;
            foreach (float y in CableHeights(variant))
                steel.Tube(new Vector3(-0.06f, y, z0), new Vector3(-0.06f, y, z1), 0.011f, 4, false);
            steel.Tube(new Vector3(-0.54f, top + 0.74f, z0), new Vector3(-0.54f, top + 0.74f, z1), 0.011f, 4, false);
        }

        /// <summary>Emergency gate in the fence: two heavy posts, top bar, two mesh leaves with braces.</summary>
        public static void FenceGate(Parts B, int lod, int variant, float length = Apex.Tracks.TrackKitLayout.FencePostSpacing)
        {
            float y0 = variant == 1 ? 0f : BarrierHeight, top = FenceTop + 0.2f;
            float z0 = -length * 0.5f, z1 = length * 0.5f;
            var steel = B(TrackInfrastructureGeometry.Steel);
            int sides = lod == 0 ? 8 : 5;
            steel.Tube(new Vector3(0f, y0, z0), new Vector3(0f, top, z0), 0.09f, sides, true);
            steel.Tube(new Vector3(0f, y0, z1), new Vector3(0f, top, z1), 0.09f, sides, true);
            steel.Tube(new Vector3(0f, top - 0.1f, z0), new Vector3(0f, top - 0.1f, z1), 0.06f, sides, true);
            PanelX(B(TrackInfrastructureGeometry.Fence), -0.03f, y0 + 0.05f, top - 0.2f, z0 + 0.1f, z1 - 0.1f, 0.5f, 0.5f, true);
            if (lod > 0) return;
            float mid = (z0 + z1) * 0.5f;
            foreach (var (za, zb) in new[] { (z0 + 0.1f, mid - 0.02f), (mid + 0.02f, z1 - 0.1f) })
            {
                steel.Tube(new Vector3(-0.03f, y0 + 0.05f, za), new Vector3(-0.03f, y0 + 0.05f, zb), 0.03f, 6, false);
                steel.Tube(new Vector3(-0.03f, top - 0.2f, za), new Vector3(-0.03f, top - 0.2f, zb), 0.03f, 6, false);
                steel.Tube(new Vector3(-0.03f, y0 + 0.05f, za), new Vector3(-0.03f, top - 0.2f, zb), 0.025f, 6, false);
            }
        }

        /// <summary>Event banner on the fence (track side; plain back), 1.2 m high, stretched to the panel.</summary>
        public static void Banner(Parts B, int lod, int variant, float length = Apex.Tracks.TrackKitLayout.FencePostSpacing)
        {
            float y0 = BarrierHeight + 0.12f, y1 = y0 + 1.2f, z0 = -length * 0.5f + 0.06f, z1 = length * 0.5f - 0.06f;
            var front = B(Banner(variant % BannerVariants));
            var a = new Vector3(-0.045f, y0, z0); var b = new Vector3(-0.045f, y0, z1);
            var c = new Vector3(-0.045f, y1, z1); var d = new Vector3(-0.045f, y1, z0);
            front.QuadFacing(a, b, c, d, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f), Vector3.left);
            if (lod >= 2) return;
            var back = new Vector3(0.004f, 0f, 0f);
            B(BannerBack).QuadFacing(a + back, b + back, c + back, d + back, Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector3.right);
        }

        // --- impact protection --------------------------------------------------------------

        /// <summary>
        /// Energy-absorbing module (centred, 0.9 m deep, 1.0 m high): rounded plan shape, chamfered
        /// top, two retaining straps. Colour from the variant (navy / ivory / turquoise).
        /// </summary>
        public static void EnergyModule(Parts B, int lod, int variant, float length = Apex.Tracks.TrackKitLayout.EnergyModuleLength)
        {
            string mat = variant % 3 == 0 ? TrackInfrastructureGeometry.ModuleA : variant % 3 == 1 ? TrackInfrastructureGeometry.ModuleB : TrackInfrastructureGeometry.ModuleC;
            float hx = Apex.Tracks.TrackKitLayout.EnergyModuleDepth * 0.5f, hz = length * 0.5f - 0.02f, h = 1f;
            if (lod >= 2) { B(mat).Box(new Vector3(0f, h * 0.5f, 0f), new Vector3(hx * 2f, h, hz * 2f)); return; }
            int seg = lod == 0 ? 4 : 2;
            float r = 0.12f;
            var ring = RoundedRect(hx, hz, r, seg);
            // Body walls, chamfer ring and top (rings in the x-z plane, extruded up).
            Wall(B(mat), ring, 0f, h - 0.05f, 1f);
            var inner = RoundedRect(hx - 0.04f, hz - 0.04f, r - 0.03f, seg);
            Band(B(mat), ring, h - 0.05f, inner, h);
            Cap(B(mat), inner, h);
            if (lod > 0) return;
            var strapRing = RoundedRect(hx + 0.006f, hz + 0.006f, r + 0.006f, seg);
            foreach (float y in new[] { 0.22f, 0.68f }) Wall(B(Strap), strapRing, y, y + 0.06f, 1f);
        }

        static List<Vector2> RoundedRect(float hx, float hz, float r, int seg)
        {
            var pts = new List<Vector2>();
            var corners = new[] { new Vector2(hx - r, hz - r), new Vector2(-hx + r, hz - r), new Vector2(-hx + r, -hz + r), new Vector2(hx - r, -hz + r) };
            for (int c = 0; c < 4; c++)
                for (int k = 0; k <= seg; k++)
                {
                    float a = (c * 90f + 90f * k / seg) * Mathf.Deg2Rad;
                    pts.Add(corners[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                }
            return pts;
        }

        /// <summary>Vertical wall around a closed ring of (x, z) points from y0 to y1.</summary>
        static void Wall(MeshBuilder mb, IList<Vector2> ring, float y0, float y1, float uLength)
        {
            int n = ring.Count;
            float u = 0f;
            for (int i = 0; i < n; i++)
            {
                var p = ring[i]; var q = ring[(i + 1) % n];
                float du = (q - p).magnitude / uLength;
                var mid = (p + q) * 0.5f;
                mb.QuadFacing(new Vector3(p.x, y0, p.y), new Vector3(q.x, y0, q.y), new Vector3(q.x, y1, q.y), new Vector3(p.x, y1, p.y),
                    new Vector2(u, y0), new Vector2(u + du, y0), new Vector2(u + du, y1), new Vector2(u, y1), new Vector3(mid.x, 0f, mid.y));
                u += du;
            }
        }

        /// <summary>Sloped band between ring a at ya and ring b at yb (same point count).</summary>
        static void Band(MeshBuilder mb, IList<Vector2> a, float ya, IList<Vector2> b, float yb)
        {
            int n = a.Count;
            for (int i = 0; i < n; i++)
            {
                var p = a[i]; var q = a[(i + 1) % n]; var pb = b[i]; var qb = b[(i + 1) % n];
                var mid = (p + q) * 0.5f;
                mb.QuadFacing(new Vector3(p.x, ya, p.y), new Vector3(q.x, ya, q.y), new Vector3(qb.x, yb, qb.y), new Vector3(pb.x, yb, pb.y),
                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up, new Vector3(mid.x, 0.6f, mid.y));
            }
        }

        static void Cap(MeshBuilder mb, IList<Vector2> ring, float y)
        {
            var c = new Vector3(0f, y, 0f);
            for (int i = 0; i < ring.Count; i++)
            {
                var p = ring[i]; var q = ring[(i + 1) % ring.Count];
                mb.TriangleFacing(c, new Vector3(p.x, y, p.y), new Vector3(q.x, y, q.y), new Vector2(0.5f, 0.5f), p, q, Vector3.up);
            }
        }

        /// <summary>Tyre stack section (centred, 1.3 m deep): 2 x 2 columns of three bolted tyres behind a conveyor-belt facing.</summary>
        public static void TyreStack(Parts B, int lod, float length = Apex.Tracks.TrackKitLayout.TyreStackLength)
        {
            float depth = Apex.Tracks.TrackKitLayout.TyreStackDepth;
            var tyre = B(TrackInfrastructureGeometry.Tyre);
            float hz = length * 0.5f;
            if (lod >= 2)
            {
                tyre.Box(new Vector3(0.05f, 0.45f, 0f), new Vector3(depth - 0.1f, 0.9f, length - 0.04f));
            }
            else
            {
                // One lathe per column: three tyres with their sidewall grooves, the open centre on top.
                int seg = lod == 0 ? 10 : 8;
                var column = new List<Vector2>();
                if (lod == 0)
                {
                    for (int t = 0; t < 3; t++)
                    {
                        float y0 = t * 0.3f;
                        column.Add(new Vector2(y0, 0.285f));
                        column.Add(new Vector2(y0 + 0.025f, 0.31f));
                        column.Add(new Vector2(y0 + 0.275f, 0.31f));
                        column.Add(new Vector2(y0 + 0.3f, 0.285f));
                    }
                    column.Add(new Vector2(0.9f, 0.2f));
                    column.Add(new Vector2(0.75f, 0.2f));
                }
                else
                {
                    column.Add(new Vector2(0f, 0.31f));
                    column.Add(new Vector2(0.9f, 0.31f));
                    column.Add(new Vector2(0.9f, 0.01f));
                }
                foreach (float x in new[] { -depth * 0.5f + 0.4f, -depth * 0.5f + 1.0f })
                    foreach (float z in new[] { -hz + 0.31f, hz - 0.31f })
                        LandmarkGeometry.VerticalLathe(tyre, new Vector3(x, 0f, z), column, seg);
            }
            // Conveyor-belt facing (what a car touches) with a folded top edge.
            float bx = -depth * 0.5f;
            PanelX(B(TrackInfrastructureGeometry.Belt), bx, 0f, 0.95f, -hz, hz, 0.5f, 0.5f, lod == 0);
            if (lod == 0)
            {
                B(TrackInfrastructureGeometry.Belt).QuadFacing(new Vector3(bx, 0.95f, -hz), new Vector3(bx, 0.95f, hz), new Vector3(bx + 0.12f, 0.95f, hz), new Vector3(bx + 0.12f, 0.95f, -hz),
                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector3.up);
                foreach (float z in new[] { -hz * 0.5f, hz * 0.5f })
                    foreach (float y in new[] { 0.25f, 0.7f })
                        B(TrackInfrastructureGeometry.Steel).Box(new Vector3(bx - 0.006f, y, z), new Vector3(0.012f, 0.05f, 0.05f));
            }
        }

        // --- marshal post and LED panel -----------------------------------------------------

        /// <summary>
        /// Marshal post behind the barrier (local frame: +z faces the approaching cars): raised floor
        /// with steps, back and side walls, cantilevered roof with fascia, hand rail, flag box and extinguisher.
        /// </summary>
        public static void MarshalPost(Parts B, int lod)
        {
            var c = B(TrackInfrastructureGeometry.Shelter);
            c.Box(new Vector3(0f, 0.2f, 0f), new Vector3(2.6f, 0.4f, 2.0f));
            c.Box(new Vector3(0f, 1.45f, -0.94f), new Vector3(2.6f, 2.1f, 0.12f));
            c.Box(new Vector3(-1.24f, 1.45f, -0.2f), new Vector3(0.12f, 2.1f, 1.6f));
            var roof = B(TrackInfrastructureGeometry.Roof);
            roof.Box(new Vector3(0f, 2.56f, 0.1f), new Vector3(2.9f, 0.1f, 2.4f));
            if (lod >= 2) return;
            roof.Box(new Vector3(0f, 2.48f, 1.28f), new Vector3(2.9f, 0.18f, 0.04f));   // fascia
            var steel = B(TrackInfrastructureGeometry.Steel);
            steel.Tube(new Vector3(1.25f, 0.4f, 0.95f), new Vector3(1.25f, 2.5f, 0.95f), 0.04f, 6, true);
            steel.Tube(new Vector3(-1.25f, 0.4f, 0.95f), new Vector3(-1.25f, 2.5f, 0.95f), 0.04f, 6, true);
            if (lod > 0) return;
            steel.Tube(new Vector3(-1.25f, 1.4f, 0.95f), new Vector3(1.25f, 1.4f, 0.95f), 0.025f, 6, true);   // hand rail
            // Two steps up to the floor on the open side.
            c.Box(new Vector3(1.5f, 0.07f, 0.2f), new Vector3(0.4f, 0.14f, 0.9f));
            c.Box(new Vector3(1.45f, 0.2f, 0.2f), new Vector3(0.3f, 0.12f, 0.9f));
            B(Dark).Box(new Vector3(0.6f, 0.65f, -0.7f), new Vector3(0.9f, 0.5f, 0.3f));   // flag box
            LandmarkGeometry.VerticalLathe(B(TrackInfrastructureGeometry.Extinguisher), new Vector3(-0.9f, 0.4f, -0.7f),
                new[] { new Vector2(0f, 0.09f), new Vector2(0.55f, 0.09f), new Vector2(0.62f, 0.04f), new Vector2(0.64f, 0.01f) }, 10);
        }

        /// <summary>
        /// LED flag panel housing on its post (the emissive faces at y 2.53 / 2.98, z 0.1 are built by
        /// the caller): cabinet with bezel and sun hood, post with base plate and stay.
        /// </summary>
        public static void FlagPanelHousing(Parts B, int lod)
        {
            var h = B(TrackInfrastructureGeometry.Housing);
            h.Box(new Vector3(0f, 2.75f, 0f), new Vector3(1.3f, 0.95f, 0.18f));
            var steel = B(TrackInfrastructureGeometry.Steel);
            steel.Tube(Vector3.zero, new Vector3(0f, 2.3f, -0.05f), 0.07f, lod == 0 ? 8 : 5, true);
            if (lod >= 2) return;
            // Sun hood over the LED faces and the bezel frame.
            h.Box(new Vector3(0f, 3.25f, 0.2f), new Vector3(1.34f, 0.04f, 0.26f));
            h.Box(new Vector3(-0.66f, 2.85f, 0.18f), new Vector3(0.03f, 0.84f, 0.22f));
            h.Box(new Vector3(0.66f, 2.85f, 0.18f), new Vector3(0.03f, 0.84f, 0.22f));
            if (lod > 0) return;
            h.Box(new Vector3(0f, 2.755f, 0.095f), new Vector3(1.22f, 0.04f, 0.02f));   // divider between the two faces
            steel.Box(new Vector3(0f, 0.01f, -0.05f), new Vector3(0.3f, 0.02f, 0.3f));
            steel.Tube(new Vector3(0f, 1.2f, -0.05f), new Vector3(0f, 0f, -0.75f), 0.03f, 6, true);
        }

        /// <summary>
        /// Maps module-local vertices to the world for a placement: x along `outward`, y up, z along the
        /// chord scaled to the placement length (rigid modules: within a few percent). On the left side
        /// (outward = -right) the z axis is reversed so the frame stays a proper rotation (no mirrored faces).
        /// </summary>
        public static System.Func<Vector3, Vector3> Transform(Apex.Tracks.TrackKitLayout.Placement p)
        {
            var o = new Vector3(p.position.X, p.position.Y, p.position.Z);
            var xAxis = new Vector3(p.outward.X, p.outward.Y, p.outward.Z);
            var zAxis = new Vector3(p.forward.X, p.forward.Y, p.forward.Z);
            var yAxis = Vector3.up;
            if (Vector3.Dot(Vector3.Cross(xAxis, yAxis), zAxis) < 0f) zAxis = zAxis * -1f;
            float nominal = NominalLength(p.module);
            float sz = nominal > 0f ? p.length / nominal : 1f;
            return v => o + xAxis * v.x + yAxis * v.y + zAxis * (v.z * sz);
        }

        /// <summary>Module geometry by kit module (rigid modules at nominal length; the builder scales z to the placement).</summary>
        public static void Module(Apex.Tracks.TrackKitLayout.Module m, Parts B, int lod, int variant)
        {
            switch (m)
            {
                case Apex.Tracks.TrackKitLayout.Module.BarrierBlock: BarrierBlock(B, lod); break;
                case Apex.Tracks.TrackKitLayout.Module.GuardrailSpan: GuardrailSpan(B, lod); break;
                case Apex.Tracks.TrackKitLayout.Module.FencePost: FencePost(B, lod, variant); break;
                case Apex.Tracks.TrackKitLayout.Module.FencePanel: FencePanel(B, lod, variant); break;
                case Apex.Tracks.TrackKitLayout.Module.FenceGate: FenceGate(B, lod, variant); break;
                case Apex.Tracks.TrackKitLayout.Module.EnergyModule: EnergyModule(B, lod, variant); break;
                case Apex.Tracks.TrackKitLayout.Module.TyreStack: TyreStack(B, lod); break;
                case Apex.Tracks.TrackKitLayout.Module.Banner: Banner(B, lod, variant); break;
            }
        }

        /// <summary>Nominal length of a module along z (the placement's chord length scales it).</summary>
        public static float NominalLength(Apex.Tracks.TrackKitLayout.Module m)
        {
            switch (m)
            {
                case Apex.Tracks.TrackKitLayout.Module.BarrierBlock: return Apex.Tracks.TrackKitLayout.BlockLength;
                case Apex.Tracks.TrackKitLayout.Module.GuardrailSpan: return Apex.Tracks.TrackKitLayout.GuardrailSpanLength;
                case Apex.Tracks.TrackKitLayout.Module.EnergyModule: return Apex.Tracks.TrackKitLayout.EnergyModuleLength;
                case Apex.Tracks.TrackKitLayout.Module.TyreStack: return Apex.Tracks.TrackKitLayout.TyreStackLength;
                case Apex.Tracks.TrackKitLayout.Module.FencePost: return 0f;   // a point: not scaled
                default: return Apex.Tracks.TrackKitLayout.FencePostSpacing;
            }
        }
    }
}
