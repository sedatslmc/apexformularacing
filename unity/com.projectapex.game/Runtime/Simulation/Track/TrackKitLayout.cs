using System;
using System.Collections.Generic;
using System.Numerics;
using Apex.Core;

namespace Apex.Tracks
{
    /// <summary>
    /// Placement of the modular circuit kit (precast concrete barrier blocks, guardrail spans,
    /// catch-fence posts and panels with gates, energy-absorbing modules, tyre stacks, fence
    /// banners) along the barrier line of the <see cref="RoadSurface"/>. Rigid modules are fitted
    /// as chords whose ends lie on the barrier line, so consecutive modules meet without gaps on
    /// straights and curves, the way real pinned blocks do. Pure data: the runtime turns
    /// placements into meshes (or authored prefabs); collision stays on the road surface's walls.
    /// Local module frame: +z along the module (driving direction), +x away from the track,
    /// +y up, origin at the module's centre on the run-off level at the barrier face.
    /// </summary>
    public sealed class TrackKitLayout
    {
        public enum Module
        {
            BarrierBlock,
            GuardrailSpan,
            FencePost,
            FencePanel,
            FenceGate,
            EnergyModule,
            TyreStack,
            Banner,
        }

        public struct Placement
        {
            public Module module;
            /// <summary>Centre on the run-off level (world space).</summary>
            public Vector3 position;
            /// <summary>Unit chord direction (driving direction), and unit "away from the track" (horizontal).</summary>
            public Vector3 forward, outward;
            /// <summary>Chord length (m); stretchable modules (panels, banners) are scaled to it.</summary>
            public float length;
            /// <summary>Raw distance of the module centre, side (-1 left, +1 right), design/colour variant.</summary>
            public float distance;
            public int side, variant;
            /// <summary>Part of a transverse closure across a step in the barrier line.</summary>
            public bool transverse;
        }

        /// <summary>Precast block length (m), guardrail span, fence post spacing, energy module and tyre-stack section.</summary>
        public const float BlockLength = 3.8f;
        public const float GuardrailSpanLength = 4f;
        public const float FencePostSpacing = 4f;
        public const float EnergyModuleLength = 1.5f;
        public const float TyreStackLength = 1.25f;
        public const float GateWidth = 3.5f;
        /// <summary>Depth of the impact protection in front of the barrier face (m).</summary>
        public const float EnergyModuleDepth = 0.9f, TyreStackDepth = 1.3f;
        /// <summary>Run-off level relative to the road (same as the infrastructure builder).</summary>
        public const float BaseDy = -0.12f;
        /// <summary>Fence line behind the barrier face (on the block's top).</summary>
        public const float FenceInset = 0.3f;
        /// <summary>Banners hang on the fence this far before a braking corner's apex (start, end of the zone).</summary>
        public const float BannerZoneStart = 320f, BannerZoneEnd = 40f;

        public readonly List<Placement> placements = new List<Placement>();
        readonly TrackGeometry g;
        readonly TrackInfrastructurePlan plan;
        readonly float[] wallLeft, wallRight;
        readonly List<(float distance, int side)> gates = new List<(float, int)>();

        public TrackKitLayout(TrackGeometry geometry, TrackInfrastructurePlan plan, RoadSurface surface)
        {
            g = geometry;
            this.plan = plan;
            wallLeft = surface.wallLeft;
            wallRight = surface.wallRight;
            var pitMask = surface.pitMask;
            int pitSide = surface.definition.pit != null ? surface.definition.pit.side : 0;
            int n = g.Count;
            var banner = BannerMask(plan);
            // Gates alternate sides; a gate moves to the other side where its side has no fence.
            bool FenceSide(int i, int side) => !(side == pitSide && pitMask != null && pitMask[i]) && plan.RunoffAt(i, side) >= 1.0f;
            for (int k = 0; k < plan.gates.Count; k++)
            {
                int i = g.IndexAt(plan.gates[k]);
                int gs = k % 2 == 0 ? 1 : -1;
                if (!FenceSide(i, gs)) gs = -gs;
                if (FenceSide(i, gs)) gates.Add((plan.gates[k], gs));
            }
            foreach (int side in new[] { -1, 1 })
            {
                float[] wall = side < 0 ? wallLeft : wallRight;
                // A lateral step in the barrier line (pit entry/exit, run-off pockets) ends a run; the
                // step itself is closed by a transverse row of blocks.
                bool Step(int i) => MathF.Abs(wall[i] - wall[(i - 1 + n) % n]) > MaxWallStep;
                // Runs of the same barrier type (as TrackInfrastructureBuilder).
                int Type(int i)
                {
                    if (side == pitSide && pitMask != null && pitMask[i]) return 3;   // pit wall: concrete, no fence
                    float r = plan.RunoffAt(i, side);
                    if (r < 1.0f) return 2;                                          // median: concrete, no fence
                    return r >= 4f ? 1 : 0;                                          // escape road: guardrail; else concrete
                }
                // Start the scan at a type change so no run is split at the lap seam.
                int s0 = 0;
                for (int i = 0; i < n; i++)
                    if (Type(i) != Type((i - 1 + n) % n) || Step(i)) { s0 = i; break; }
                int runStart = s0;
                for (int k = 1; k <= n; k++)
                {
                    int i = (s0 + k) % n;
                    if (k < n && Type(i) == Type(runStart) && !Step(i)) continue;
                    float from = g.Distances[runStart];
                    float len = ApexMath.PosMod(g.Distances[i] - from, g.Length);
                    if (k == n && len < 1e-3f) len = g.Length;   // a single run around the whole lap
                    // Before a step the run ends on its last sample; the transverse row spans the step.
                    if (Step(i)) len = MathF.Max(0f, len - g.Spacing);
                    int type = Type(runStart);
                    if (type == 1) Chain(Module.GuardrailSpan, side, from, len, 0f, GuardrailSpanLength, 0);
                    else Chain(Module.BarrierBlock, side, from, len, 0f, BlockLength, 0);
                    if (type == 0 || type == 1) Fence(side, from, len, banner);
                    if (Step(i))
                    {
                        // Transverse closure between the end of this run and the start of the next.
                        int prev = (i - 1 + n) % n;
                        var a = g.Positions[prev] + g.Rights[prev] * (side * wall[prev]) + new Vector3(0f, BaseDy, 0f);
                        var b = g.Positions[i] + g.Rights[i] * (side * wall[i]) + new Vector3(0f, BaseDy, 0f);
                        Segment(Module.BarrierBlock, side, a, b, g.Distances[i], BlockLength);
                    }
                    runStart = i;
                }
            }
            foreach (var p in MergedProtections(plan))
            {
                float len = ApexMath.PosMod(p.to - p.from, g.Length);
                if (p.kind == TrackInfrastructurePlan.ProtectionKind.TyreStack)
                    Chain(Module.TyreStack, p.side, p.from, len, -TyreStackDepth * 0.5f, TyreStackLength, 0, TyreStackDepth + 0.2f);
                else
                    Chain(Module.EnergyModule, p.side, p.from, len, -EnergyModuleDepth * 0.5f, EnergyModuleLength, 3, EnergyModuleDepth + 0.2f);
            }
        }

        /// <summary>Largest lateral change of the barrier line between two samples that still counts as one run (m).</summary>
        public const float MaxWallStep = 0.5f;

        /// <summary>Protection ranges per side without overlaps (adjacent corners): a later range starts where the earlier ends.</summary>
        List<TrackInfrastructurePlan.Protection> MergedProtections(TrackInfrastructurePlan plan)
        {
            var list = new List<TrackInfrastructurePlan.Protection>(plan.protections);
            list.Sort((a, b) => a.side != b.side ? a.side.CompareTo(b.side) : a.from.CompareTo(b.from));
            var result = new List<TrackInfrastructurePlan.Protection>();
            foreach (var p in list)
            {
                var q = p;
                if (result.Count > 0)
                {
                    var last = result[result.Count - 1];
                    float lastLen = ApexMath.PosMod(last.to - last.from, g.Length);
                    float into = ApexMath.PosMod(q.from - last.from, g.Length);
                    if (last.side == q.side && into < lastLen)
                    {
                        if (ApexMath.PosMod(q.to - last.from, g.Length) <= lastLen) continue;   // fully inside
                        q.from = last.to;
                    }
                }
                result.Add(q);
            }
            return result;
        }

        /// <summary>Straight row of modules from a to b (transverse closures).</summary>
        void Segment(Module m, int side, Vector3 a, Vector3 b, float distance, float unit)
        {
            float len = Vector3.Distance(a, b);
            if (len < 0.2f) return;
            int count = Math.Max(1, (int)MathF.Round(len / unit));
            for (int k = 0; k < count; k++)
                Add(m, side, Vector3.Lerp(a, b, k / (float)count), Vector3.Lerp(a, b, (k + 1) / (float)count), distance, 0, transverse: true);
        }

        public float WallAt(int side, float d)
        {
            float[] w = side < 0 ? wallLeft : wallRight;
            float x = g.Wrap(d) / g.Spacing;
            int i = (int)x % g.Count;
            return ApexMath.Lerp(w[i], w[(i + 1) % g.Count], x - MathF.Floor(x));
        }

        /// <summary>Point on the barrier line (face + `inset` away from the track) at run-off level.</summary>
        public Vector3 LinePoint(int side, float d, float inset)
        {
            var p = g.PositionAt(d) + g.RightAt(d) * (side * (WallAt(side, d) + inset));
            return p + new Vector3(0f, BaseDy, 0f);
        }

        /// <summary>
        /// Stations (raw distance, point) along the barrier line at `inset`, equally spaced in arc length
        /// of the line itself (outside of a hairpin the line is much longer than the centreline), so
        /// rigid modules keep their nominal length. count = round(arc / unit).
        /// </summary>
        List<(float d, Vector3 p)> ArcStations(int side, float from, float len, float inset, float unit)
        {
            int samples = Math.Max(2, (int)MathF.Ceiling(len / (g.Spacing * 0.5f)));
            var ds = new float[samples + 1];
            var pts = new Vector3[samples + 1];
            var arc = new float[samples + 1];
            for (int k = 0; k <= samples; k++)
            {
                ds[k] = from + len * k / samples;
                pts[k] = LinePoint(side, ds[k], inset);
                arc[k] = k == 0 ? 0f : arc[k - 1] + Vector3.Distance(pts[k], pts[k - 1]);
            }
            float total = arc[samples];
            var result = new List<(float, Vector3)>();
            if (total < 0.3f) return result;
            int count = Math.Max(1, (int)MathF.Round(total / unit));
            int j = 0;
            for (int c = 0; c <= count; c++)
            {
                float target = total * c / count;
                while (j < samples - 1 && arc[j + 1] < target) j++;
                float t = arc[j + 1] > arc[j] ? ApexMath.Clamp01((target - arc[j]) / (arc[j + 1] - arc[j])) : 0f;
                result.Add((ApexMath.Lerp(ds[j], ds[j + 1], t), Vector3.Lerp(pts[j], pts[j + 1], t)));
            }
            return result;
        }

        /// <summary>Rigid modules of nominal `unit` length along the line; their chord ends lie on the line, so the chain is closed.
        /// `variants` cycles a colour variant (0 = none).</summary>
        void Chain(Module m, int side, float from, float len, float inset, float unit, int variants, float minRunoff = 0f)
        {
            var st = ArcStations(side, from, len, inset, unit);
            for (int k = 0; k + 1 < st.Count; k++)
            {
                float mid = (st[k].d + st[k + 1].d) * 0.5f;
                var a = st[k].p;
                var b = st[k + 1].p;
                // Where the line curves towards the track, a chord's middle would stand in front of the
                // line (by the sagitta) and of the collision wall: move the module back by that much.
                var right = g.RightAt(mid) * side;
                float ahead = Vector3.Dot(LinePoint(side, mid, inset) - (a + b) * 0.5f, right);
                if (ahead > 0f) { a += right * ahead; b += right * ahead; }
                // Impact protection stands on the run-off in front of the barrier: not where the run-off
                // is narrower than the module (it would stand on the asphalt).
                if (minRunoff > 0f && plan.RunoffAt(g.IndexAt(mid), side) < minRunoff) continue;
                Add(m, side, a, b, g.Wrap(mid), variants > 0 ? k % variants : 0);
            }
        }

        void Add(Module m, int side, Vector3 a, Vector3 b, float distance, int variant, bool transverse = false)
        {
            var chord = b - a;
            float length = chord.Length();
            if (length < 1e-3f) return;
            var fwd = chord / length;
            if (m == Module.FencePost)
            {
                // Posts are points: orient them with the track, not with a tiny chord.
                var t = g.TangentAt(distance);
                fwd = Vector3.Normalize(new Vector3(t.X, 0f, t.Z));
            }
            // Away from the track: perpendicular to the chord; for transverse rows, the driving direction.
            var outward = transverse ? Vector3.Normalize(new Vector3(g.TangentAt(distance).X, 0f, g.TangentAt(distance).Z))
                : Vector3.Normalize(new Vector3(fwd.Z, 0f, -fwd.X)) * side;
            placements.Add(new Placement { module = m, position = (a + b) * 0.5f, forward = fwd, outward = outward, length = length, distance = distance, side = side, variant = variant, transverse = transverse });
        }

        void Fence(int side, float from, float len, bool[] bannerMask)
        {
            if (len < FencePostSpacing) return;
            // A gate replaces the posts and panels over its width.
            bool InGate(float d)
            {
                foreach (var (gd, gs) in gates)
                    if (gs == side && ApexMath.PosMod(d - gd, g.Length) < GateWidth) return true;
                return false;
            }
            var st = ArcStations(side, from, len, FenceInset, FencePostSpacing);
            for (int k = 0; k < st.Count; k++)
            {
                float d = g.Wrap(st[k].d);
                if (!InGate(d)) Add(Module.FencePost, side, st[k].p, st[k].p + new Vector3(0.01f, 0f, 0f), d, 0);
                if (k + 1 == st.Count) break;
                var a = st[k].p;
                var b = st[k + 1].p;
                float mid = g.Wrap((st[k].d + st[k + 1].d) * 0.5f);
                float prevMid = g.Wrap(st[k].d - (st[k + 1].d - st[k].d) * 0.5f);
                if (InGate(mid))
                {
                    if (k == 0 || !InGate(prevMid)) Add(Module.FenceGate, side, a, b, mid, 0);
                }
                else
                {
                    Add(Module.FencePanel, side, a, b, mid, 0);
                    if (bannerMask[g.IndexAt(mid)]) Add(Module.Banner, side, a, b, mid, k % 3);
                }
            }
        }

        /// <summary>Samples in a braking zone's approach (where the cameras and the drivers look).</summary>
        bool[] BannerMask(TrackInfrastructurePlan plan)
        {
            var mask = new bool[g.Count];
            foreach (var p in plan.protections)
            {
                // Protection spans apex - 90 .. apex + 25: recover the apex.
                float apex = g.Wrap(p.from + 90f);
                for (float d = apex - BannerZoneStart; d < apex - BannerZoneEnd; d += g.Spacing) mask[g.IndexAt(d)] = true;
            }
            return mask;
        }

        public int Count(Module m)
        {
            int c = 0;
            foreach (var p in placements) if (p.module == m) c++;
            return c;
        }
    }
}
