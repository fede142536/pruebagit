using System;

namespace Football.Core.Common
{
    /// <summary>Vector 3D (X largo del campo, Y ancho, Z altura).</summary>
    public struct Vec3
    {
        /// <summary>Componente X.</summary>
        public float X;

        /// <summary>Componente Y.</summary>
        public float Y;

        /// <summary>Componente Z (altura sobre el césped).</summary>
        public float Z;

        /// <summary>Crea un vector.</summary>
        /// <param name="x">Componente X.</param>
        /// <param name="y">Componente Y.</param>
        /// <param name="z">Componente Z.</param>
        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>Vector nulo.</summary>
        public static readonly Vec3 Zero = new Vec3(0f, 0f, 0f);

        /// <summary>Proyección sobre el plano del césped.</summary>
        public Vec2 Horizontal => new Vec2(X, Y);

        /// <summary>Longitud del vector.</summary>
        public float Length => MathF.Sqrt(X * X + Y * Y + Z * Z);

        /// <summary>Longitud de la componente horizontal.</summary>
        public float HorizontalLength => MathF.Sqrt(X * X + Y * Y);

        /// <summary>Suma de vectores.</summary>
        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        /// <summary>Resta de vectores.</summary>
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        /// <summary>Producto por escalar.</summary>
        public static Vec3 operator *(Vec3 a, float s) => new Vec3(a.X * s, a.Y * s, a.Z * s);

        /// <summary>Distancia entre dos puntos.</summary>
        public static float Distance(Vec3 a, Vec3 b) => (a - b).Length;
    }
}
