using System;
using System.Collections.Generic;
using System.Numerics;
using Apex.Vehicle;

namespace Apex.Tracks
{
    public struct RayHit
    {
        public float distance;
        public Vector3 point;
        /// <summary>Face normal (front side), like a MeshCollider hit.</summary>
        public Vector3 normal;
        public SurfaceType surface;
        /// <summary>Name of the surface mesh that was hit (Road, Kerbs, Terrain …).</summary>
        public string part;
    }

    /// <summary>
    /// Ray casts against the collider meshes of a <see cref="RoadSurface"/> (every mesh with a
    /// physical surface), front faces only, as Unity's MeshCollider queries do by default
    /// (Physics.queriesHitBackfaces = false). Triangles are binned on a horizontal grid.
    /// </summary>
    public sealed class CollisionWorld
    {
        const float Cell = 6f;
        readonly List<Vector3> a = new List<Vector3>(), e1 = new List<Vector3>(), e2 = new List<Vector3>(), n = new List<Vector3>();
        readonly List<int> meshOf = new List<int>();
        readonly List<SurfaceMesh> meshes = new List<SurfaceMesh>();
        readonly Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();
        int[] stamp = new int[0];
        int stampId;

        public CollisionWorld(IEnumerable<SurfaceMesh> parts)
        {
            foreach (var m in parts) if (m != null && m.surface.HasValue) Add(m);
            stamp = new int[a.Count];
        }

        public int TriangleCount => a.Count;

        static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

        void Add(SurfaceMesh m)
        {
            int mi = meshes.Count;
            meshes.Add(m);
            var v = m.vertices;
            foreach (var t in m.triangles)
                for (int k = 0; k < t.Count; k += 3)
                {
                    Vector3 p0 = v[t[k]], p1 = v[t[k + 1]], p2 = v[t[k + 2]];
                    var nn = Vector3.Cross(p1 - p0, p2 - p0);
                    float len = nn.Length();
                    if (len < 1e-9f) continue;   // degenerate
                    int id = a.Count;
                    a.Add(p0); e1.Add(p1 - p0); e2.Add(p2 - p0); n.Add(nn / len); meshOf.Add(mi);
                    int x0 = (int)MathF.Floor(MathF.Min(p0.X, MathF.Min(p1.X, p2.X)) / Cell), x1 = (int)MathF.Floor(MathF.Max(p0.X, MathF.Max(p1.X, p2.X)) / Cell);
                    int z0 = (int)MathF.Floor(MathF.Min(p0.Z, MathF.Min(p1.Z, p2.Z)) / Cell), z1 = (int)MathF.Floor(MathF.Max(p0.Z, MathF.Max(p1.Z, p2.Z)) / Cell);
                    for (int x = x0; x <= x1; x++)
                        for (int z = z0; z <= z1; z++)
                        {
                            long key = Key(x, z);
                            if (!grid.TryGetValue(key, out var list)) grid[key] = list = new List<int>();
                            list.Add(id);
                        }
                }
        }

        /// <summary>Nearest front-face hit along dir (unit) within maxDistance.</summary>
        public bool Raycast(Vector3 origin, Vector3 dir, float maxDistance, out RayHit hit, Func<SurfaceMesh, bool> filter = null)
        {
            hit = default;
            float best = maxDistance;
            int bestId = -1;
            var end = origin + dir * maxDistance;
            int x0 = (int)MathF.Floor(MathF.Min(origin.X, end.X) / Cell), x1 = (int)MathF.Floor(MathF.Max(origin.X, end.X) / Cell);
            int z0 = (int)MathF.Floor(MathF.Min(origin.Z, end.Z) / Cell), z1 = (int)MathF.Floor(MathF.Max(origin.Z, end.Z) / Cell);
            if ((x1 - x0 + 1) * (z1 - z0 + 1) > 4096) throw new ArgumentException("ray too long for the grid query");
            if (++stampId == int.MaxValue) { Array.Clear(stamp, 0, stamp.Length); stampId = 1; }
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    if (!grid.TryGetValue(Key(x, z), out var list)) continue;
                    foreach (int id in list)
                    {
                        if (stamp[id] == stampId) continue;
                        stamp[id] = stampId;
                        if (Vector3.Dot(dir, n[id]) >= 0f) continue;   // back face
                        if (filter != null && !filter(meshes[meshOf[id]])) continue;
                        // Möller–Trumbore.
                        var p = Vector3.Cross(dir, e2[id]);
                        float det = Vector3.Dot(e1[id], p);
                        if (MathF.Abs(det) < 1e-12f) continue;
                        float inv = 1f / det;
                        var s = origin - a[id];
                        float u = Vector3.Dot(s, p) * inv;
                        if (u < -1e-6f || u > 1f + 1e-6f) continue;
                        var q = Vector3.Cross(s, e1[id]);
                        float w = Vector3.Dot(dir, q) * inv;
                        if (w < -1e-6f || u + w > 1f + 1e-6f) continue;
                        float t = Vector3.Dot(e2[id], q) * inv;
                        if (t < 0f || t >= best) continue;
                        best = t;
                        bestId = id;
                    }
                }
            if (bestId < 0) return false;
            var m = meshes[meshOf[bestId]];
            hit = new RayHit { distance = best, point = origin + dir * best, normal = n[bestId], surface = m.surface.Value, part = m.name };
            return true;
        }

        /// <summary>Highest front-facing surface below (x, z) between yMin and yMax, by a downward ray.</summary>
        public bool SurfaceBelow(float x, float z, float yMax, float yMin, out RayHit hit, Func<SurfaceMesh, bool> filter = null) =>
            Raycast(new Vector3(x, yMax, z), new Vector3(0f, -1f, 0f), yMax - yMin, out hit, filter);
    }
}
