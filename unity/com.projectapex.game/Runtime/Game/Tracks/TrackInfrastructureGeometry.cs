using System.Collections.Generic;
using Apex.Tracks;
using UnityEngine;

namespace Apex.Game
{
    /// <summary>
    /// Mesh generation for the street-circuit infrastructure (no GameObjects, also used by the
    /// offline preview): concrete barriers with block joints, guardrail on escape roads,
    /// energy-absorbing modules and tyre stacks, catch fencing with overhang and gates,
    /// marshal shelters, flag panel housings and road-surface details. Builders are keyed by
    /// material. Procedural placeholder art with believable dimensions.
    /// </summary>
    public static class TrackInfrastructureGeometry
    {
        public const string Concrete = "BarrierConcrete";
        public const string Armco = "Armco";
        public const string Steel = "Steel";
        public const string ModuleA = "ModuleNavy";
        public const string ModuleB = "ModuleIvory";
        public const string ModuleC = "ModuleTurquoise";
        public const string Tyre = "Tire";
        public const string Belt = "ConveyorBelt";
        public const string Fence = EnvironmentGeometry.CatchFence;
        public const string Shelter = "Concrete";
        public const string Roof = "ShelterRoof";
        public const string Extinguisher = "Extinguisher";
        public const string Housing = "PanelHousing";
        public const string Rubber = "Rubber";
        public const string Patch = "AsphaltPatch";
        public const string Manhole = "Manhole";
        public const string Dash = "LaneDash";

        public const float BlockLength = 3.8f;
        public const float BarrierHeight = 1.0f;
        public const float FenceHeight = 3.0f;

        public delegate MeshBuilder Parts(string material);

        static Vector3 V(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);

        public static Vector3 P(TrackGeometry g, int i, float lateral, float dy) =>
            V(g.Positions[i] + g.Rights[i] * lateral) + Vector3.up * dy;

        static Vector3 Right(TrackGeometry g, int i) => V(g.Rights[i]);

        /// <summary>
        /// Concrete barrier (safety-shape profile) whose track face starts at `wall[i]` from the
        /// centreline on `side`, between samples a..b (indices may wrap). u = distance / block length,
        /// so the texture's block joint repeats every 3.8 m.
        /// </summary>
        public static void ConcreteBarrier(MeshBuilder mb, TrackGeometry g, float[] wall, int side, int a, int b, int step, float baseDy)
        {
            // Profile (outward from the face, height): lower slope, upper face, top, back.
            var prof = new[] { new Vector2(0f, 0f), new Vector2(0.09f, 0.28f), new Vector2(0.17f, BarrierHeight),
                               new Vector2(0.4f, BarrierHeight), new Vector2(0.6f, 0f) };
            float[] v = { 0f, 0.28f, 0.62f, 0.78f, 1f };
            int n = g.Count;
            for (int k = a; k < b; k += step)
            {
                int i = ((k % n) + n) % n, j = (((k + step) % n) + n) % n;
                float ui = g.Distances[i] / BlockLength, uj = ui + (g.Spacing * step) / BlockLength;
                for (int s = 0; s < prof.Length - 1; s++)
                {
                    Vector3 Pt(int idx, Vector2 pr) => P(g, idx, side * (wall[idx] + pr.x), baseDy + pr.y);
                    var p0 = Pt(i, prof[s]); var p1 = Pt(i, prof[s + 1]);
                    var q0 = Pt(j, prof[s]); var q1 = Pt(j, prof[s + 1]);
                    // Outward normal of this facet: towards the track for the face, up for the top, away for the back.
                    Vector3 outward;
                    if (s == 0 || s == 1) outward = -Right(g, i) * side + Vector3.up * 0.3f;
                    else if (s == 2) outward = Vector3.up;
                    else outward = Right(g, i) * side + Vector3.up * 0.3f;
                    mb.QuadFacing(p0, q0, q1, p1, new Vector2(ui, v[s]), new Vector2(uj, v[s]), new Vector2(uj, v[s + 1]), new Vector2(ui, v[s + 1]), outward);
                }
            }
        }

        /// <summary>Double W-beam guardrail (rails in 2 m pieces) on posts every 4 m (escape roads, wide run-off).</summary>
        public static void Guardrail(MeshBuilder rail, MeshBuilder posts, TrackGeometry g, float[] wall, int side, int a, int b, float baseDy)
        {
            int n = g.Count;
            int step = Mathf.Max(1, Mathf.RoundToInt(2f / g.Spacing));
            int postEvery = 2;
            int count = 0;
            for (int k = a; k < b; k += step, count++)
            {
                int i = ((k % n) + n) % n, j = (((k + step) % n) + n) % n;
                foreach (float h0 in new[] { 0.45f, 0.62f })
                {
                    // W profile: out - in - out over 0.31 m.
                    var pts = new[] { new Vector2(0f, h0), new Vector2(0.06f, h0 + 0.08f), new Vector2(0f, h0 + 0.16f) };
                    for (int s = 0; s < 2; s++)
                    {
                        Vector3 Pt(int idx, Vector2 pr) => P(g, idx, side * (wall[idx] + pr.x), baseDy + pr.y);
                        var outward = -Right(g, i) * side;
                        rail.QuadFacing(Pt(i, pts[s]), Pt(j, pts[s]), Pt(j, pts[s + 1]), Pt(i, pts[s + 1]), new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), outward);
                        rail.QuadFacing(Pt(i, pts[s]), Pt(j, pts[s]), Pt(j, pts[s + 1]), Pt(i, pts[s + 1]), new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), -outward);
                    }
                }
                if (count % postEvery != 0) continue;
                var basePt = P(g, i, side * (wall[i] + 0.12f), baseDy);
                posts.Box(basePt + Vector3.up * 0.42f, new Vector3(0.12f, 0.84f, 0.08f), Quaternion.LookRotation(V(g.Tangents[i])));
            }
        }

        /// <summary>Energy-absorbing modules (1.5 m blocks, alternating colours) in front of the barrier.</summary>
        public static void EnergyModules(Parts B, TrackGeometry g, float[] wall, int side, float from, float to, float baseDy)
        {
            float len = Mathf.Repeat(to - from, g.Length);
            int k = 0;
            for (float d = 0f; d < len; d += 1.55f, k++)
            {
                float dd = g.Wrap(from + d);
                int i = g.IndexAt(dd);
                var c = P(g, i, side * (wall[i] - 0.5f), baseDy + 0.5f);
                var rot = Quaternion.LookRotation(V(g.Tangents[i]));
                string mat = k % 3 == 0 ? ModuleA : k % 3 == 1 ? ModuleB : ModuleC;
                B(mat).Box(c, new Vector3(0.9f, 1.0f, 1.5f), rot);
            }
        }

        /// <summary>Tyre stacks (two rows, three tyres high) behind a conveyor-belt facing.</summary>
        public static void TyreStacks(Parts B, TrackGeometry g, float[] wall, int side, float from, float to, float baseDy)
        {
            float len = Mathf.Repeat(to - from, g.Length);
            for (float d = 0f; d < len; d += 0.62f)
            {
                int i = g.IndexAt(g.Wrap(from + d));
                foreach (float depth in new[] { 0.35f, 0.95f })
                {
                    var c = P(g, i, side * (wall[i] - depth), baseDy);
                    LandmarkGeometry.VerticalLathe(B(Tyre), c, new[] { new Vector2(0f, 0.3f), new Vector2(0.75f, 0.3f), new Vector2(0.75f, 0.01f) }, 5);
                }
            }
            // Belt in front of the stacks (what a car actually touches).
            int a = g.IndexAt(from), n = g.Count, cnt = Mathf.Max(1, Mathf.RoundToInt(len / g.Spacing));
            for (int s = 0; s < cnt; s += 2)
            {
                int i = (a + s) % n, j = (a + Mathf.Min(s + 2, cnt)) % n;
                var outward = -Right(g, i) * side;
                B(Belt).QuadFacing(P(g, i, side * (wall[i] - 1.3f), baseDy), P(g, j, side * (wall[j] - 1.3f), baseDy),
                    P(g, j, side * (wall[j] - 1.3f), baseDy + 0.8f), P(g, i, side * (wall[i] - 1.3f), baseDy + 0.8f),
                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up, outward);
            }
        }

        /// <summary>Catch fence on the barrier top: posts every 5 m, mesh panel, overhang towards the track; gaps for gates.</summary>
        public static void CatchFence(Parts B, TrackGeometry g, float[] wall, int side, int a, int b, float baseDy, System.Func<float, bool> isGate)
        {
            int n = g.Count;
            int step = Mathf.Max(1, Mathf.RoundToInt(5f / g.Spacing));
            float h0 = baseDy + BarrierHeight;
            for (int k = a; k < b; k += step)
            {
                int i = ((k % n) + n) % n, j = (((k + step) % n) + n) % n;
                if (isGate(g.Distances[i])) continue;
                float li = side * (wall[i] + 0.3f), lj = side * (wall[j] + 0.3f);
                var p0 = P(g, i, li, h0); var p1 = P(g, j, lj, h0);
                var p2 = P(g, j, lj, h0 + FenceHeight); var p3 = P(g, i, li, h0 + FenceHeight);
                // Overhang: 0.9 m leaning 35° towards the track.
                var lean = -Right(g, i) * side * 0.52f + Vector3.up * 0.74f;
                var p4 = p2 + lean; var p5 = p3 + lean;
                float len = (p1 - p0).magnitude;
                var toTrack = -Right(g, i) * side;
                foreach (var face in new[] { toTrack, -toTrack })
                {
                    B(Fence).QuadFacing(p0, p1, p2, p3, Vector2.zero, new Vector2(len / 2f, 0f), new Vector2(len / 2f, FenceHeight / 2f), new Vector2(0f, FenceHeight / 2f), face);
                    B(Fence).QuadFacing(p3, p2, p4, p5, Vector2.zero, new Vector2(len / 2f, 0f), new Vector2(len / 2f, 0.45f), new Vector2(0f, 0.45f), face + Vector3.up * 0.5f);
                }
                // Post (three faces, no caps) up to the overhang hinge.
                B(Steel).Tube(p0, p3, 0.06f, 3, false);
            }
        }

        /// <summary>Gate frame in the fence (two heavy posts, top bar).</summary>
        public static void Gate(MeshBuilder mb, TrackGeometry g, float[] wall, int side, float distance, float baseDy)
        {
            int i = g.IndexAt(distance), j = g.IndexAt(distance + 3.5f);
            float h0 = baseDy + BarrierHeight;
            var a = P(g, i, side * (wall[i] + 0.3f), h0);
            var b = P(g, j, side * (wall[j] + 0.3f), h0);
            mb.Tube(a, a + Vector3.up * (FenceHeight + 0.2f), 0.09f, 6, true);
            mb.Tube(b, b + Vector3.up * (FenceHeight + 0.2f), 0.09f, 6, true);
            mb.Tube(a + Vector3.up * FenceHeight, b + Vector3.up * FenceHeight, 0.06f, 6, true);
        }

        /// <summary>Marshal shelter behind the barrier (local frame: +z faces the approaching cars).</summary>
        public static void MarshalShelter(Parts B)
        {
            B(Shelter).Box(new Vector3(0f, 0.15f, 0f), new Vector3(2.4f, 0.3f, 1.8f));
            B(Shelter).Box(new Vector3(0f, 1.25f, -0.85f), new Vector3(2.4f, 2.2f, 0.12f));
            B(Shelter).Box(new Vector3(-1.15f, 1.25f, 0f), new Vector3(0.1f, 2.2f, 1.8f));
            B(Roof).Box(new Vector3(0f, 2.42f, 0.1f), new Vector3(2.7f, 0.12f, 2.2f));
            LandmarkGeometry.VerticalLathe(B(Extinguisher), new Vector3(0.9f, 0.3f, -0.6f), new[] { new Vector2(0f, 0.09f), new Vector2(0.55f, 0.09f), new Vector2(0.62f, 0.04f), new Vector2(0.64f, 0.01f) }, 8);
        }

        /// <summary>LED panel housing on a post; the emissive face is a separate quad (built by the caller).</summary>
        public static void PanelHousing(MeshBuilder mb)
        {
            mb.Tube(Vector3.zero, new Vector3(0f, 2.3f, 0f), 0.07f, 6, true);
            mb.Box(new Vector3(0f, 2.75f, 0f), new Vector3(1.3f, 0.95f, 0.18f));
        }

        /// <summary>Flat ribbon on the road between two lateral offsets (decals: rubber, patches, dashes).</summary>
        public static void RoadDecal(MeshBuilder mb, TrackGeometry g, float from, float to, float latA, float latB, float lift, float taper)
        {
            float len = Mathf.Repeat(to - from, g.Length);
            if (len < 0.5f) return;
            float step = Mathf.Max(g.Spacing, Mathf.Min(4f, len / 2f));
            for (float d = 0f; d < len - 1e-3f; d += step)
            {
                float d1 = Mathf.Min(len, d + step);
                float w0 = taper > 0f ? Mathf.Clamp01(Mathf.Min(d, len - d) / taper) : 1f;
                float w1 = taper > 0f ? Mathf.Clamp01(Mathf.Min(d1, len - d1) / taper) : 1f;
                float mid = (latA + latB) * 0.5f, half = (latB - latA) * 0.5f;
                int i = g.IndexAt(g.Wrap(from + d)), j = g.IndexAt(g.Wrap(from + d1));
                var a0 = P(g, i, mid - half * w0, lift); var b0 = P(g, i, mid + half * w0, lift);
                var a1 = P(g, j, mid - half * w1, lift); var b1 = P(g, j, mid + half * w1, lift);
                mb.QuadFacing(a0, b0, b1, a1, new Vector2(0f, d / 6f), new Vector2(1f, d / 6f), new Vector2(1f, d1 / 6f), new Vector2(0f, d1 / 6f), Vector3.up);
            }
        }

        /// <summary>Manhole cover (octagon) on the road.</summary>
        public static void ManholeCover(MeshBuilder mb, Vector3 centre, float radius)
        {
            for (int k = 0; k < 8; k++)
            {
                float a0 = k * Mathf.PI / 4f, a1 = (k + 1) * Mathf.PI / 4f;
                var p0 = centre + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                var p1 = centre + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                mb.TriangleFacing(centre, p0, p1, new Vector2(0.5f, 0.5f), new Vector2(0.5f + Mathf.Cos(a0) * 0.5f, 0.5f + Mathf.Sin(a0) * 0.5f),
                    new Vector2(0.5f + Mathf.Cos(a1) * 0.5f, 0.5f + Mathf.Sin(a1) * 0.5f), Vector3.up);
            }
        }
    }
}
