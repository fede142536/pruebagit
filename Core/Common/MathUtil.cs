using System;

namespace Football.Core.Common
{
    /// <summary>Funciones matemáticas pequeñas compartidas por la simulación.</summary>
    public static class MathUtil
    {
        /// <summary>Umbral por debajo del cual una longitud se considera nula.</summary>
        public const float Epsilon = 1e-6f;

        /// <summary>Grados a radianes.</summary>
        public const float Deg2Rad = MathF.PI / 180f;

        /// <summary>Acota un valor a un rango.</summary>
        public static float Clamp(float value, float min, float max) => value < min ? min : (value > max ? max : value);

        /// <summary>Acota un valor a [0, 1].</summary>
        public static float Clamp01(float value) => value < 0f ? 0f : (value > 1f ? 1f : value);

        /// <summary>Interpolación lineal sin acotar.</summary>
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// <summary>Diferencia angular más corta <c>to - from</c> en el rango (-π, π].</summary>
        /// <param name="to">Ángulo de destino en radianes.</param>
        /// <param name="from">Ángulo de origen en radianes.</param>
        public static float AngleDiff(float to, float from)
        {
            float d = (to - from) % (2f * MathF.PI);
            if (d > MathF.PI)
            {
                d -= 2f * MathF.PI;
            }
            else if (d <= -MathF.PI)
            {
                d += 2f * MathF.PI;
            }

            return d;
        }

        /// <summary>Distancia de un punto a un segmento.</summary>
        /// <param name="p">Punto.</param>
        /// <param name="a">Inicio del segmento.</param>
        /// <param name="b">Fin del segmento.</param>
        /// <param name="t">Posición del punto más cercano sobre el segmento, en [0, 1].</param>
        public static float DistancePointSegment(Vec2 p, Vec2 a, Vec2 b, out float t)
        {
            Vec2 ab = b - a;
            float lengthSq = ab.LengthSquared;
            t = lengthSq > Epsilon ? Clamp01(Vec2.Dot(p - a, ab) / lengthSq) : 0f;
            return Vec2.Distance(p, a + ab * t);
        }

        /// <summary>Convierte un atributo (1..100) a una habilidad en [0, 1].</summary>
        public static float Skill01(int attribute) => (attribute - 1) / 99f;
    }
}
