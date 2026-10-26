using System;
using System.Collections.Generic;
using Apex.Core;

namespace Apex.Tracks
{
    /// <summary>
    /// Data-driven layout of the street-circuit infrastructure from the centreline, widths,
    /// run-offs and curvature: kerbs (inside at the apex, outside at the exit, height by
    /// corner speed, never wider than the run-off allows), impact protection in front of the
    /// barrier on the outside of braking zones (energy-absorbing modules on fast approaches,
    /// tyre stacks at slow corners), marshal posts with LED flag panels before every corner
    /// and at most every ~450 m, emergency gates in the catch fence, braking-zone rubber and
    /// asphalt details. Rubber follows the racing line: two tyre tracks and the groove between them,
    /// darker where the tyres work hardest, plus lock-up streaks in heavy braking zones. Pure data, no rendering.
    /// </summary>
    public sealed class TrackInfrastructurePlan
    {
        public const float KerbMaxWidth = 1.3f;
        /// <summary>Highest kerb crown (m): front plank height at rest (35 mm) minus 5 mm.</summary>
        public const float MaxKerbHeight = 0.03f;
        public const float DefaultRunoff = 16f;

        public sealed class Kerbs
        {
            /// <summary>Kerb width (m) per sample and side; 0 = none.</summary>
            public float[] left, right;
            /// <summary>Kerb crown height (m) per sample.</summary>
            public float[] height;
        }

        public enum ProtectionKind { EnergyModules, TyreStack }

        public struct Protection
        {
            public float from, to;   // raw distances
            public int side;         // -1 left, +1 right
            public ProtectionKind kind;
        }

        public struct Post
        {
            public float distance;   // raw distance of the post
            public int side;
            /// <summary>Corner index the post protects (-1 = straight).</summary>
            public int corner;
        }

        /// <summary>
        /// A rubber strip on the asphalt: raw distances, centre offset at both ends (follows the line),
        /// width and opacity 0..1. <see cref="brakeMark"/> marks a lock-up streak.
        /// </summary>
        public struct RubberStrip
        {
            public float from, to;
            public float lateralFrom, lateralTo;
            public float width;
            public float strength;
            public bool brakeMark;
        }

        /// <summary>Lateral distance of each tyre track from the line (half the car's track width).</summary>
        public const float TyreTrack = 0.8f;
        const float RubberSegment = 4f;

        public readonly Kerbs kerbs = new Kerbs();
        public readonly List<Protection> protections = new List<Protection>();
        public readonly List<Post> posts = new List<Post>();
        public readonly List<float> gates = new List<float>();
        /// <summary>
        /// Where a catch fence is needed per sample and side (see <see cref="BuildFenceNeeds"/>); the start/finish
        /// straight with the main grandstands is added by the kit layout, which knows the start line.
        /// </summary>
        public bool[] fenceLeft, fenceRight;
        /// <summary>Line speed (m/s) above which both sides get a fence (debris energy on fast straights).</summary>
        public const float FenceSpeed = 65f;
        /// <summary>Fence on the outside of a corner until this far after the apex (m).</summary>
        public const float FenceAfterApex = 70f;

        public readonly List<RubberStrip> rubber = new List<RubberStrip>();
        /// <summary>Rubber intensity 0..1 per centreline sample (tyre work: braking, cornering, traction).</summary>
        public float[] rubberIntensity;
        public readonly List<(float distance, float lateral, float length, float width, float shade)> patches = new List<(float, float, float, float, float)>();
        public readonly List<(float distance, float lateral)> manholes = new List<(float, float)>();
        public readonly List<(float from, float to, float lateral)> laneDashes = new List<(float, float, float)>();

        readonly TrackGeometry g;

        public float RunoffAt(int i, int side)
        {
            float[] r = side < 0 ? g.RunoffLeft : g.RunoffRight;
            return r != null ? r[i] : DefaultRunoff;
        }

        public TrackInfrastructurePlan(TrackGeometry geometry, List<TrackGeometry.Corner> corners, float[] lineOffsets = null, float[] lineSpeed = null)
        {
            g = geometry;
            BuildKerbs(corners);
            BuildProtectionAndPosts(corners, lineSpeed);
            BuildGates();
            BuildSurface(corners, lineOffsets, lineSpeed);
            BuildFenceNeeds(corners, lineSpeed);
        }

        int N => g.Count;
        int Wrap(int i) => ((i % N) + N) % N;
        int Samples(float metres) => Math.Max(1, (int)MathF.Round(metres / g.Spacing));

        void BuildKerbs(List<TrackGeometry.Corner> corners)
        {
            int n = N;
            kerbs.left = new float[n];
            kerbs.right = new float[n];
            kerbs.height = new float[n];
            foreach (var c in corners)
            {
                float r = c.minRadius;
                // Fast corners: low wide kerbs; slow corners: more pronounced ones, but never higher than the
                // car's body clearance at the front axle minus a margin (BodyCollider: 35 mm at rest), so a car
                // straddling a kerb does not ground on it.
                float h = r > 150f ? 0.02f : r > 60f ? 0.025f : MaxKerbHeight;
                int inside = c.right ? 1 : -1;
                // Corner extent from the curvature around the apex.
                int a = c.index, b = c.index;
                float thresh = MathF.Abs(g.Curvatures[c.index]) * 0.35f;
                while (MathF.Abs(g.Curvatures[Wrap(a - 1)]) > thresh && c.index - a < Samples(160f)) a--;
                while (MathF.Abs(g.Curvatures[Wrap(b + 1)]) > thresh && b - c.index < Samples(160f)) b++;
                // Inside kerb from turn-in to the exit, outside kerb on the exit.
                Paint(a - Samples(6f), b + Samples(4f), inside, KerbMaxWidth, h);
                Paint(c.index + Samples(4f), b + Samples(30f), -inside, KerbMaxWidth, h * 0.6f);
            }
            // Never wider than the run-off allows (barrier and median clearance).
            for (int i = 0; i < n; i++)
            {
                kerbs.left[i] = MathF.Min(kerbs.left[i], MathF.Max(0f, RunoffAt(i, -1) - 0.2f));
                kerbs.right[i] = MathF.Min(kerbs.right[i], MathF.Max(0f, RunoffAt(i, 1) - 0.2f));
                if (kerbs.left[i] < 0.35f) kerbs.left[i] = 0f;
                if (kerbs.right[i] < 0.35f) kerbs.right[i] = 0f;
            }
        }

        void Paint(int a, int b, int side, float width, float h)
        {
            int taper = Samples(4f);
            for (int i = a; i <= b; i++)
            {
                float t = MathF.Min(1f, MathF.Min(i - a, b - i) / (float)taper);
                int k = Wrap(i);
                float w = width * MathF.Max(0f, t);
                if (side < 0) kerbs.left[k] = MathF.Max(kerbs.left[k], w);
                else kerbs.right[k] = MathF.Max(kerbs.right[k], w);
                kerbs.height[k] = MathF.Max(kerbs.height[k], h);
            }
        }

        void BuildProtectionAndPosts(List<TrackGeometry.Corner> corners, float[] lineSpeed)
        {
            var sorted = new List<TrackGeometry.Corner>(corners);
            sorted.Sort((x, y) => g.Distances[x.index].CompareTo(g.Distances[y.index]));
            for (int ci = 0; ci < sorted.Count; ci++)
            {
                var c = sorted[ci];
                int outside = c.right ? -1 : 1;
                float apex = g.Distances[c.index];
                float approachSpeed = lineSpeed != null ? Max(lineSpeed, c.index - Samples(250f), c.index - Samples(60f)) : 60f;
                float cornerSpeed = lineSpeed != null ? lineSpeed[c.index] : 30f;
                bool braking = approachSpeed - cornerSpeed > 12f;
                if (braking)
                {
                    var kind = cornerSpeed < 25f ? ProtectionKind.TyreStack : ProtectionKind.EnergyModules;
                    // Outside of the braking zone and the corner entry: where a car goes straight on.
                    protections.Add(new Protection { from = g.Wrap(apex - 90f), to = g.Wrap(apex + 25f), side = outside, kind = kind });
                }
                // Marshal post with flag panel ~120 m before the corner, on the outside.
                posts.Add(new Post { distance = g.Wrap(apex - 120f), side = PostSide(g.IndexAt(apex - 120f), outside), corner = ci });
            }
            // Fill long gaps so no stretch exceeds ~450 m without a post.
            posts.Sort((x, y) => x.distance.CompareTo(y.distance));
            var extra = new List<Post>();
            for (int i = 0; i < posts.Count; i++)
            {
                float a = posts[i].distance, b = i + 1 < posts.Count ? posts[i + 1].distance : posts[0].distance + g.Length;
                int add = (int)MathF.Floor((b - a) / 450f);
                for (int k = 1; k <= add; k++)
                {
                    float d = g.Wrap(a + (b - a) * k / (add + 1));
                    extra.Add(new Post { distance = d, side = PostSide(g.IndexAt(d), 1), corner = -1 });
                }
            }
            posts.AddRange(extra);
            posts.Sort((x, y) => x.distance.CompareTo(y.distance));
        }

        float Max(float[] a, int from, int to)
        {
            float m = 0f;
            for (int i = from; i <= to; i++) m = MathF.Max(m, a[Wrap(i)]);
            return m;
        }

        /// <summary>Preferred side unless the barrier there is a median (too little room for a post).</summary>
        int PostSide(int i, int preferred) => RunoffAt(i, preferred) >= 1.2f ? preferred : -preferred;

        void BuildGates()
        {
            // Emergency/maintenance gates in the catch fence about every 160 m (alternating sides).
            for (float d = 80f; d < g.Length - 40f; d += 160f) gates.Add(d);
        }

        void BuildSurface(List<TrackGeometry.Corner> corners, float[] lineOffsets, float[] lineSpeed)
        {
            var rng = new Random(1907);
            if (lineOffsets != null && lineSpeed != null) BuildRubber(lineOffsets, lineSpeed);
            for (float d = 30f; d < g.Length; d += 110f + (float)rng.NextDouble() * 80f)
            {
                int i = g.IndexAt(d);
                float hw = g.Widths[i] * 0.5f;
                patches.Add((d, ((float)rng.NextDouble() * 2f - 1f) * (hw - 2f), 2f + (float)rng.NextDouble() * 5f, 1.2f + (float)rng.NextDouble() * 2.5f, (float)rng.NextDouble()));
            }
            for (float d = 55f; d < g.Length; d += 140f + (float)rng.NextDouble() * 60f)
            {
                int i = g.IndexAt(d);
                manholes.Add((d, (rng.NextDouble() < 0.5 ? -1f : 1f) * g.Widths[i] * 0.22f));
            }
            // Faded lane markings of the original streets on wide straight stretches.
            float start = -1f;
            for (int i = 0; i <= N; i++)
            {
                int k = Wrap(i);
                bool ok = i < N && g.Widths[k] >= 10.5f && MathF.Abs(g.Curvatures[k]) < 1f / 500f;
                if (ok && start < 0f) start = g.Distances[k];
                if ((!ok || i == N) && start >= 0f)
                {
                    float end = i < N ? g.Distances[k] : g.Length;
                    if (end - start > 60f)
                        for (float d = start + 10f; d < end - 10f; d += 9f) laneDashes.Add((d, d + 3f, 0f));
                    start = -1f;
                }
            }
        }

        /// <summary>
        /// Tyre work per sample from the line's speed profile: longitudinal acceleration (braking and
        /// traction) and lateral acceleration v²κ, as a fraction of 1.5 g, smoothed over ±12 m because
        /// cars never run exactly the same line. Rubber deposition grows with it; a light film everywhere.
        /// </summary>
        void BuildRubber(float[] lineOffsets, float[] lineSpeed)
        {
            int n = N;
            var work = new float[n];
            for (int i = 0; i < n; i++)
            {
                float v0 = lineSpeed[i], v1 = lineSpeed[Wrap(i + 1)];
                float along = MathF.Abs(v1 * v1 - v0 * v0) / (2f * g.Spacing);
                // Braking deposits more than traction (four tyres and higher slip).
                if (v1 < v0) along *= 1.4f;
                float lateral = v0 * v0 * MathF.Abs(g.Curvatures[i]);
                work[i] = ApexMath.Clamp01(MathF.Sqrt(along * along + lateral * lateral) / (1.5f * 9.81f));
            }
            int r = Samples(12f);
            rubberIntensity = new float[n];
            for (int i = 0; i < n; i++)
            {
                float sum = 0f;
                for (int k = -r; k <= r; k++) sum += work[Wrap(i + k)];
                rubberIntensity[i] = ApexMath.Clamp01(0.12f + 0.95f * sum / (2 * r + 1));
            }
            // Two tyre tracks and the groove, in segments that follow the line.
            int step = Samples(RubberSegment);
            for (int i = 0; i < n; i += step)
            {
                int j = Math.Min(i + step, n) % n;
                float strength = 0.5f * (rubberIntensity[i] + rubberIntensity[j]);
                float hwA = g.Widths[i] * 0.5f, hwB = g.Widths[j] * 0.5f;
                float from = g.Distances[i], to = i + step >= n ? g.Length : g.Distances[j];
                // Wider where the line varies more between cars (hard work = fighting cars, different lines).
                float spread = 0.45f + 0.5f * strength;
                foreach (float side in new[] { -TyreTrack, TyreTrack })
                    rubber.Add(new RubberStrip
                    {
                        from = from, to = to,
                        lateralFrom = Clamp(lineOffsets[i] + side, hwA), lateralTo = Clamp(lineOffsets[j] + side, hwB),
                        width = spread, strength = strength,
                    });
                rubber.Add(new RubberStrip
                {
                    from = from, to = to,
                    lateralFrom = Clamp(lineOffsets[i], hwA), lateralTo = Clamp(lineOffsets[j], hwB),
                    width = 2f * TyreTrack + 0.6f, strength = 0.35f * strength,
                });
            }
            BuildBrakeMarks(lineOffsets, lineSpeed);
        }

        /// <summary>
        /// Lock-up streaks: in braking zones that slow the car by more than 25 m/s, 2–5 narrow dark streaks
        /// from 15–40 % of the zone, 8–30 m long, drifting up to 0.4 m sideways (the locked tyre slides).
        /// </summary>
        void BuildBrakeMarks(float[] lineOffsets, float[] lineSpeed)
        {
            var rng = new Random(2207);
            int n = N;
            for (int i = 0; i < n; i++)
            {
                // Start of a braking zone: speed falls from here onwards.
                if (!(lineSpeed[Wrap(i + 1)] < lineSpeed[i] - 0.02f && lineSpeed[Wrap(i - 1)] <= lineSpeed[i] + 0.02f)) continue;
                int end = i;
                while (lineSpeed[Wrap(end + 1)] < lineSpeed[Wrap(end)] && end - i < n / 2) end++;
                float drop = lineSpeed[i] - lineSpeed[Wrap(end)];
                if (drop < 25f) continue;
                float zone = (end - i) * g.Spacing;
                int marks = 2 + rng.Next(4);
                for (int m = 0; m < marks; m++)
                {
                    float start = g.Distances[i] + zone * (0.15f + 0.25f * (float)rng.NextDouble());
                    float length = 8f + 22f * (float)rng.NextDouble();
                    int a = g.IndexAt(g.Wrap(start)), b = g.IndexAt(g.Wrap(start + length));
                    float side = (rng.Next(2) == 0 ? -1f : 1f) * TyreTrack + ((float)rng.NextDouble() - 0.5f) * 0.6f;
                    float drift = ((float)rng.NextDouble() - 0.5f) * 0.8f;
                    rubber.Add(new RubberStrip
                    {
                        from = g.Wrap(start), to = g.Wrap(start + length),
                        lateralFrom = Clamp(lineOffsets[a] + side, g.Widths[a] * 0.5f),
                        lateralTo = Clamp(lineOffsets[b] + side + drift, g.Widths[b] * 0.5f),
                        width = 0.28f, strength = 0.7f + 0.3f * (float)rng.NextDouble(), brakeMark = true,
                    });
                }
                i = end;
            }
        }

        /// <summary>
        /// Debris fences where cars arrive or leave fast, not as a cage along the whole lap: the outside of each
        /// corner from its braking point to <see cref="FenceAfterApex"/> m after the apex (where a car leaving
        /// the road goes), both sides wherever the line is faster than <see cref="FenceSpeed"/>. Gaps under 60 m
        /// are closed and pieces under 40 m dropped, so the fence runs in calm stretches. Inside of corners and
        /// slow sections stay open (views of the city, the Bosphorus and the landmarks).
        /// </summary>
        void BuildFenceNeeds(List<TrackGeometry.Corner> corners, float[] lineSpeed)
        {
            int n = N;
            fenceLeft = new bool[n];
            fenceRight = new bool[n];
            foreach (var c in corners)
            {
                float brake = lineSpeed != null ? MathF.Min(260f, MathF.Max(60f, (Max(lineSpeed, c.index - Samples(300f), c.index) - lineSpeed[c.index]) * 3f)) : 120f;
                var outside = c.right ? fenceLeft : fenceRight;
                for (int k = -Samples(brake); k <= Samples(FenceAfterApex); k++) outside[Wrap(c.index + k)] = true;
            }
            if (lineSpeed != null)
                for (int i = 0; i < n; i++)
                    if (lineSpeed[i] >= FenceSpeed) fenceLeft[i] = fenceRight[i] = true;
            foreach (var f in new[] { fenceLeft, fenceRight }) Tidy(f, Samples(60f), Samples(40f));
        }

        /// <summary>Closes false gaps shorter than `gap` samples, then drops true runs shorter than `piece`.</summary>
        void Tidy(bool[] f, int gap, int piece)
        {
            foreach (bool value in new[] { false, true })
            {
                int limit = value ? piece : gap;
                int n = N, start = -1;
                for (int i = 0; i < n && start < 0; i++) if (f[i] != f[Wrap(i - 1)]) start = i;
                if (start < 0) return;
                int runStart = start;
                for (int k = 1; k <= n; k++)
                {
                    int i = Wrap(start + k);
                    if (k < n && f[i] == f[runStart]) continue;
                    int len = Wrap(i - runStart);
                    if (len == 0) len = n;
                    if (f[runStart] == value && len < limit)
                        for (int j = 0; j < len; j++) f[Wrap(runStart + j)] = !value;
                    runStart = i;
                }
            }
        }

        static float Clamp(float v, float hw) => ApexMath.Clamp(v, -hw + 0.4f, hw - 0.4f);
    }
}
