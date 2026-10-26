// Minimal stand-ins for the UnityEngine math types used by MeshKit/CarGeometry.
// Only for the offline preview tool; semantics follow Unity (left-handed, degrees).
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0);
        public static Vector2 one => new Vector2(1, 1);
        public static Vector2 right => new Vector2(1, 0);
        public static Vector2 up => new Vector2(0, 1);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator /(Vector2 a, float d) => new Vector2(a.x / d, a.y / d);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator *(Vector2 a, float d) => new Vector2(a.x * d, a.y * d);
        public static Vector2 operator *(float d, Vector2 a) => new Vector2(a.x * d, a.y * d);
        public float magnitude => MathF.Sqrt(x * x + y * y);
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 up => new Vector3(0, 1, 0);
        public static Vector3 down => new Vector3(0, -1, 0);
        public static Vector3 right => new Vector3(1, 0, 0);
        public static Vector3 left => new Vector3(-1, 0, 0);
        public static Vector3 forward => new Vector3(0, 0, 1);
        public static Vector3 back => new Vector3(0, 0, -1);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator *(float d, Vector3 a) => a * d;
        public static Vector3 operator /(Vector3 a, float d) => new Vector3(a.x / d, a.y / d, a.z / d);
        public float magnitude => MathF.Sqrt(x * x + y * y + z * z);
        public Vector3 normalized { get { float m = magnitude; return m > 1e-9f ? this / m : zero; } }
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static Vector3 Scale(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        public static Vector3 ProjectOnPlane(Vector3 v, Vector3 n) { float d = Dot(n, n); return d < 1e-12f ? v : v - n * (Dot(v, n) / d); }
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity => new Quaternion(0, 0, 0, 1);
        static Quaternion Axis(Vector3 a, float deg) { float h = deg * MathF.PI / 360f; float s = MathF.Sin(h); return new Quaternion(a.x * s, a.y * s, a.z * s, MathF.Cos(h)); }
        // Unity applies Euler rotations in Z, X, Y order (q = qy * qx * qz).
        public static Quaternion Euler(float ex, float ey, float ez) => Axis(Vector3.up, ey) * Axis(Vector3.right, ex) * Axis(Vector3.forward, ez);
        /// <summary>Rotation whose +z points along forward with +y near up (Unity semantics).</summary>
        public static Quaternion LookRotation(Vector3 forward, Vector3 up)
        {
            var f = forward.normalized;
            var r = Vector3.Cross(up, f).normalized;
            var u = Vector3.Cross(f, r);
            float m00 = r.x, m01 = u.x, m02 = f.x, m10 = r.y, m11 = u.y, m12 = f.y, m20 = r.z, m21 = u.z, m22 = f.z;
            float tr = m00 + m11 + m22;
            if (tr > 0f) { float s = MathF.Sqrt(tr + 1f) * 2f; return new Quaternion((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25f * s); }
            if (m00 > m11 && m00 > m22) { float s = MathF.Sqrt(1f + m00 - m11 - m22) * 2f; return new Quaternion(0.25f * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s); }
            if (m11 > m22) { float s = MathF.Sqrt(1f + m11 - m00 - m22) * 2f; return new Quaternion((m01 + m10) / s, 0.25f * s, (m12 + m21) / s, (m02 - m20) / s); }
            { float s = MathF.Sqrt(1f + m22 - m00 - m11) * 2f; return new Quaternion((m02 + m20) / s, (m12 + m21) / s, 0.25f * s, (m10 - m01) / s); }
        }
        public static Quaternion LookRotation(Vector3 forward) => LookRotation(forward, Vector3.up);

        public static Quaternion operator *(Quaternion a, Quaternion b) => new Quaternion(
            a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
            a.w * b.y + a.y * b.w + a.z * b.x - a.x * b.z,
            a.w * b.z + a.z * b.w + a.x * b.y - a.y * b.x,
            a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);
        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            var u = new Vector3(q.x, q.y, q.z);
            var t = Vector3.Cross(u, v) * 2f;
            return v + t * q.w + Vector3.Cross(u, t);
        }
    }

    public struct Matrix4x4
    {
        public Vector3 offset;
        public static Matrix4x4 identity => new Matrix4x4();
        public Vector3 MultiplyPoint3x4(Vector3 p) => p + offset;
    }

    public static class Mathf
    {
        public const float PI = MathF.PI;
        public const float Deg2Rad = MathF.PI / 180f;
        public const float Rad2Deg = 180f / MathF.PI;
        public static float Lerp(float a, float b, float t) { t = t < 0f ? 0f : t > 1f ? 1f : t; return a + (b - a) * t; }
        public static float Max(float a, float b) => MathF.Max(a, b);
        public static int Min(int a, int b) => Math.Min(a, b);
        public static float Sin(float a) => MathF.Sin(a);
        public static float Cos(float a) => MathF.Cos(a);
        public static float Sqrt(float a) => MathF.Sqrt(a);
        public static float Abs(float a) => MathF.Abs(a);
        public static float Sign(float a) => a >= 0f ? 1f : -1f;
        public static float Pow(float a, float b) => MathF.Pow(a, b);
        public static float Min(float a, float b) => MathF.Min(a, b);
        public static int Max(int a, int b) => Math.Max(a, b);
        public static float Ceil(float a) => MathF.Ceiling(a);
        public static int CeilToInt(float a) => (int)MathF.Ceiling(a);
        public static int FloorToInt(float a) => (int)MathF.Floor(a);
        public static float Round(float a) => MathF.Round(a);
        public static int RoundToInt(float a) => (int)MathF.Round(a);
        public static float Repeat(float t, float l) => t - MathF.Floor(t / l) * l;
        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
    }

    public class Mesh
    {
        public string name;
        public Rendering.IndexFormat indexFormat;
        public List<Vector3> vertices = new List<Vector3>();
        public List<int> triangles = new List<int>();
        public void SetVertices(List<Vector3> v) => vertices = new List<Vector3>(v);
        public void SetUVs(int ch, List<Vector2> uv) { }
        public void SetTriangles(List<int> t, int sub) => triangles = new List<int>(t);
        public void RecalculateNormals() { }
        public void RecalculateBounds() { }
    }
}

namespace UnityEngine.Rendering
{
    public enum IndexFormat { UInt16, UInt32 }
}
