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
    /// asphalt details. Pure data, no rendering.
    /// </summary>
    public sealed class TrackInfrastructurePlan
    {
        public const float KerbMaxWidth = 1.3f;
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

        public struct Zone
        {
            public float from, to;
            public float lateralFrom, lateralTo;
            public float strength;
        }

        public readonly Kerbs kerbs = new Kerbs();
        public readonly List<Protection> protections = new List<Protection>();
        public readonly List<Post> posts = new List<Post>();
        public readonly List<float> gates = new List<float>();
        public readonly List<Zone> rubber = new List<Zone>();
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
                // Fast corners: low wide kerbs; slow corners: shorter, more pronounced ones.
                float h = r > 150f ? 0.025f : r > 60f ? 0.038f : 0.05f;
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
            foreach (var c in corners)
            {
                float apex = g.Distances[c.index];
                float line = lineOffsets != null ? lineOffsets[c.index] : 0f;
                float hw = g.Widths[c.index] * 0.5f;
                // Braking rubber along the line before the corner, exit rubber after it.
                float brake = lineSpeed != null ? MathF.Min(160f, MathF.Max(40f, (Max(lineSpeed, c.index - Samples(250f), c.index) - lineSpeed[c.index]) * 2.2f)) : 80f;
                float entryLine = lineOffsets != null ? lineOffsets[Wrap(c.index - Samples(brake * 0.6f))] : 0f;
                rubber.Add(new Zone { from = g.Wrap(apex - brake), to = g.Wrap(apex - 5f), lateralFrom = Clamp(entryLine - 0.9f, hw), lateralTo = Clamp(entryLine + 0.9f, hw), strength = 0.55f });
                rubber.Add(new Zone { from = g.Wrap(apex - 5f), to = g.Wrap(apex + 60f), lateralFrom = Clamp(line - 1f, hw), lateralTo = Clamp(line + 1f, hw), strength = 0.4f });
            }
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

        static float Clamp(float v, float hw) => ApexMath.Clamp(v, -hw + 0.4f, hw - 0.4f);
    }
}
