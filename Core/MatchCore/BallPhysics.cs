using System;
using Football.Core.Common;

namespace Football.Core.MatchCore
{
    /// <summary>Resultado de un paso de física que merece un evento.</summary>
    public enum BallHit
    {
        /// <summary>Sin choques con la portería.</summary>
        None,
        /// <summary>Choque con un poste.</summary>
        Post,
        /// <summary>Choque con el larguero.</summary>
        Crossbar
    }

    /// <summary>
    /// Física del balón con paso fijo: gravedad, arrastre cuadrático, rebote, rodadura con fricción,
    /// efecto (Magnus simplificado) y choques con postes y larguero. Sin allocations.
    /// </summary>
    public static class BallPhysics
    {
        private const float GroundEpsilon = 0.001f;
        private const float WoodworkSideKick = 0.3f;

        /// <summary>Avanza el balón un paso de simulación.</summary>
        /// <param name="ball">Estado a modificar.</param>
        /// <param name="dt">Duración del paso (s).</param>
        /// <param name="c">Parámetros del balón.</param>
        /// <param name="pitch">Geometría del campo (para postes y larguero).</param>
        /// <returns>Si el balón chocó con la portería en este paso.</returns>
        public static BallHit Step(BallState ball, float dt, BallConfig c, Pitch pitch)
        {
            Vec3 previous = ball.Pos;

            if (ball.Pos.Z <= GroundEpsilon && ball.Vel.Z <= GroundEpsilon)
            {
                RollOnGround(ball, dt, c);
            }
            else
            {
                Fly(ball, dt, c);
            }

            return CheckWoodwork(ball, previous, c, pitch);
        }

        private static void RollOnGround(BallState ball, float dt, BallConfig c)
        {
            ball.Pos.Z = 0f;
            ball.Vel.Z = 0f;
            float speed = ball.Vel.HorizontalLength;
            if (speed > 0f)
            {
                float decel = c.RollDeceleration + c.RollDrag * speed;
                float scale = MathF.Max(0f, speed - decel * dt) / speed;
                ball.Vel.X *= scale;
                ball.Vel.Y *= scale;
            }

            ball.Pos.X += ball.Vel.X * dt;
            ball.Pos.Y += ball.Vel.Y * dt;
            ball.Spin *= MathF.Max(0f, 1f - c.GroundSpinDecay * dt);
        }

        private static void Fly(BallState ball, float dt, BallConfig c)
        {
            Vec3 v = ball.Vel;
            float speed = v.Length;
            float drag = c.AirDrag * speed;
            float magnus = c.MagnusCoefficient * ball.Spin;

            // Aceleración en el instante inicial; integración exacta para aceleración constante.
            float ax = -drag * v.X - magnus * v.Y;
            float ay = -drag * v.Y + magnus * v.X;
            float az = -c.Gravity - drag * v.Z;

            float half = 0.5f * dt * dt;
            ball.Pos.X += v.X * dt + ax * half;
            ball.Pos.Y += v.Y * dt + ay * half;
            ball.Pos.Z += v.Z * dt + az * half;
            ball.Vel.X += ax * dt;
            ball.Vel.Y += ay * dt;
            ball.Vel.Z += az * dt;
            ball.Spin *= MathF.Max(0f, 1f - c.SpinDecay * dt);

            if (ball.Pos.Z <= 0f)
            {
                ball.Pos.Z = 0f;
                if (ball.Vel.Z < -c.MinBounceSpeed)
                {
                    ball.Vel.Z = -ball.Vel.Z * c.Restitution;
                    ball.Vel.X *= c.BounceFriction;
                    ball.Vel.Y *= c.BounceFriction;
                }
                else
                {
                    ball.Vel.Z = 0f;
                }
            }
        }

        private static BallHit CheckWoodwork(BallState ball, Vec3 previous, BallConfig c, Pitch pitch)
        {
            for (int i = 0; i < 2; i++)
            {
                float end = i == 0 ? 1f : -1f;
                float planeX = end * pitch.HalfLength;
                float before = previous.X - planeX;
                float after = ball.Pos.X - planeX;
                bool crossingOutward = end * (ball.Pos.X - previous.X) > 0f && before * after <= 0f && before != after;
                if (!crossingOutward)
                {
                    continue;
                }

                float t = before / (before - after);
                float y = previous.Y + (ball.Pos.Y - previous.Y) * t;
                float z = previous.Z + (ball.Pos.Z - previous.Z) * t;
                float contact = pitch.Config.PostRadius + c.Radius;

                bool barHit = MathF.Abs(y) <= pitch.GoalHalfWidth && MathF.Abs(z - pitch.GoalHeight) < contact;
                if (barHit)
                {
                    BounceOffWoodwork(ball, end, y, z, c, pitch);
                    ball.Vel.Z = z >= pitch.GoalHeight ? MathF.Abs(ball.Vel.Z) : -MathF.Abs(ball.Vel.Z);
                    return BallHit.Crossbar;
                }

                float postDistance = MathF.Abs(MathF.Abs(y) - pitch.GoalHalfWidth);
                bool postHit = postDistance < contact && z < pitch.GoalHeight + contact;
                if (postHit)
                {
                    BounceOffWoodwork(ball, end, y, z, c, pitch);
                    // Por dentro del poste el balón sale hacia el centro; por fuera, hacia fuera.
                    float inside = MathF.Abs(y) < pitch.GoalHalfWidth ? -1f : 1f;
                    ball.Vel.Y += MathF.Sign(y) * inside * MathF.Abs(ball.Vel.X) * WoodworkSideKick;
                    return BallHit.Post;
                }
            }

            return BallHit.None;
        }

        private static void BounceOffWoodwork(BallState ball, float end, float y, float z, BallConfig c, Pitch pitch)
        {
            ball.Pos = new Vec3(end * (pitch.HalfLength - c.Radius), y, MathF.Max(z, 0f));
            ball.Vel.X = -ball.Vel.X * c.WoodworkRestitution;
            ball.Vel.Y *= c.WoodworkRestitution;
            ball.Vel.Z *= c.WoodworkRestitution;
        }
    }
}
