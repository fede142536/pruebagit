// Matemática real mínima de UnityEngine para correr SkinnedBodyGeometry fuera de Unity (no es Unity).
using System;
namespace UnityEngine
{
    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; } public static Vector2 zero => new Vector2(0, 0); }
    public struct Vector3 { public float x, y, z; public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0, 0, 0); public static Vector3 one => new Vector3(1, 1, 1);
        public float sqrMagnitude => x * x + y * y + z * z; public float magnitude => (float)Math.Sqrt(sqrMagnitude);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float s) => new Vector3(a.x * s, a.y * s, a.z * s);
        public static Vector3 operator *(float s, Vector3 a) => new Vector3(a.x * s, a.y * s, a.z * s);
        public static Vector3 operator /(Vector3 a, float s) => new Vector3(a.x / s, a.y / s, a.z / s);
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static Vector3 Scale(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z); }
    public struct Color { public float r, g, b, a; public Color(float r, float g, float b, float a = 1) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public float grayscale => 0.299f * r + 0.587f * g + 0.114f * b; public static Color white => new Color(1, 1, 1);
        public static Color Lerp(Color x, Color y, float t) => new Color(x.r + (y.r - x.r) * t, x.g + (y.g - x.g) * t, x.b + (y.b - x.b) * t, x.a + (y.a - x.a) * t); }
    public static class Mathf { public const float PI = (float)Math.PI; public const float Deg2Rad = PI / 180f;
        public static float Sin(float a) => (float)Math.Sin(a); public static float Cos(float a) => (float)Math.Cos(a); public static float Sqrt(float a) => (float)Math.Sqrt(a);
        public static float Acos(float a) => (float)Math.Acos(a); public static float Abs(float a) => Math.Abs(a); public static float Sign(float a) => a >= 0 ? 1 : -1;
        public static float Min(float a, float b) => Math.Min(a, b); public static float Max(float a, float b) => Math.Max(a, b); public static int Max(int a, int b) => Math.Max(a, b);
        public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v; public static float Clamp01(float v) => Clamp(v, 0, 1);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t); }
}
namespace Football.Game { public enum KitPattern { Solid, Stripes, Band, Sash, Chevron, Halves } }
