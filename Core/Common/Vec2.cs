using System;

namespace Football.Core.Common
{
    /// <summary>Vector 2D de coma flotante. Propio del núcleo para no depender de Unity.</summary>
    public struct Vec2
    {
        /// <summary>Componente X.</summary>
        public float X;

        /// <summary>Componente Y.</summary>
        public float Y;

        /// <summary>Crea un vector.</summary>
        /// <param name="x">Componente X.</param>
        /// <param name="y">Componente Y.</param>
        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        /// <summary>Vector nulo.</summary>
        public static readonly Vec2 Zero = new Vec2(0f, 0f);

        /// <summary>Longitud del vector.</summary>
        public float Length => MathF.Sqrt(X * X + Y * Y);

        /// <summary>Longitud al cuadrado (evita la raíz).</summary>
        public float LengthSquared => X * X + Y * Y;

        /// <summary>Vector unitario en la misma dirección, o cero si el vector es casi nulo.</summary>
        public Vec2 Normalized
        {
            get
            {
                float length = Length;
                return length > MathUtil.Epsilon ? new Vec2(X / length, Y / length) : Zero;
            }
        }

        /// <summary>Ángulo en radianes respecto al eje X.</summary>
        public float Angle => MathF.Atan2(Y, X);

        /// <summary>Vector unitario con el ángulo dado.</summary>
        /// <param name="angle">Ángulo en radianes.</param>
        public static Vec2 FromAngle(float angle) => new Vec2(MathF.Cos(angle), MathF.Sin(angle));

        /// <summary>Gira el vector el ángulo dado.</summary>
        /// <param name="angle">Ángulo en radianes (antihorario).</param>
        public Vec2 Rotated(float angle)
        {
            float c = MathF.Cos(angle);
            float s = MathF.Sin(angle);
            return new Vec2(X * c - Y * s, X * s + Y * c);
        }

        /// <summary>Producto escalar.</summary>
        public static float Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;

        /// <summary>Distancia entre dos puntos.</summary>
        public static float Distance(Vec2 a, Vec2 b) => (a - b).Length;

        /// <summary>Interpolación lineal.</summary>
        /// <param name="a">Valor en t=0.</param>
        /// <param name="b">Valor en t=1.</param>
        /// <param name="t">Parámetro (no se acota).</param>
        public static Vec2 Lerp(Vec2 a, Vec2 b, float t) => new Vec2(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

        /// <summary>Suma de vectores.</summary>
        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);

        /// <summary>Resta de vectores.</summary>
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);

        /// <summary>Vector opuesto.</summary>
        public static Vec2 operator -(Vec2 a) => new Vec2(-a.X, -a.Y);

        /// <summary>Producto por escalar.</summary>
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.X * s, a.Y * s);

        /// <summary>Producto por escalar.</summary>
        public static Vec2 operator *(float s, Vec2 a) => new Vec2(a.X * s, a.Y * s);

        /// <summary>División por escalar.</summary>
        public static Vec2 operator /(Vec2 a, float s) => new Vec2(a.X / s, a.Y / s);
    }
}
