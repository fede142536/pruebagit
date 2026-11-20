using System;
using Football.Core.Common;

namespace Football.Core.MatchCore
{
    /// <summary>Cálculo de velocidades de salida para que el balón llegue a donde se quiere.</summary>
    public static class Ballistics
    {
        private const int GroundSpeedIterations = 3;
        private const float MinSinDouble = 0.05f;

        /// <summary>
        /// Velocidad inicial de un pase raso para que el balón llegue a la distancia indicada con la velocidad deseada,
        /// teniendo en cuenta la fricción de rodadura.
        /// </summary>
        /// <param name="distance">Distancia a recorrer (m).</param>
        /// <param name="arrivalSpeed">Velocidad con la que debe llegar (m/s).</param>
        /// <param name="c">Parámetros del balón.</param>
        public static float GroundSpeedForDistance(float distance, float arrivalSpeed, BallConfig c)
        {
            float speed = arrivalSpeed;
            for (int i = 0; i < GroundSpeedIterations; i++)
            {
                float averageSpeed = 0.5f * (speed + arrivalSpeed);
                float decel = c.RollDeceleration + c.RollDrag * averageSpeed;
                speed = MathF.Sqrt(arrivalSpeed * arrivalSpeed + 2f * decel * distance);
            }

            return speed;
        }

        /// <summary>Velocidad total de salida de un pase por alto que aterriza a la distancia dada.</summary>
        /// <param name="distance">Distancia horizontal al punto de caída (m).</param>
        /// <param name="loftAngle">Ángulo de elevación (rad).</param>
        /// <param name="c">Parámetros del balón.</param>
        public static float LobSpeed(float distance, float loftAngle, BallConfig c)
        {
            float sin2 = MathF.Max(MinSinDouble, MathF.Sin(2f * loftAngle));
            return MathF.Sqrt(distance * c.Gravity / sin2) * c.LobDragCompensation;
        }

        /// <summary>Da al balón una velocidad de salida.</summary>
        /// <param name="ball">Balón a patear.</param>
        /// <param name="direction">Dirección horizontal (se normaliza).</param>
        /// <param name="speed">Velocidad total (m/s).</param>
        /// <param name="loftAngle">Ángulo de elevación (rad); 0 para pase raso.</param>
        /// <param name="spin">Giro (rad/s).</param>
        public static void Launch(BallState ball, Vec2 direction, float speed, float loftAngle, float spin)
        {
            Vec2 d = direction.Normalized;
            float horizontal = speed * MathF.Cos(loftAngle);
            ball.Vel = new Vec3(d.X * horizontal, d.Y * horizontal, speed * MathF.Sin(loftAngle));
            ball.Spin = spin;
        }
    }
}
