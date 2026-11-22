using System;
using Football.Core.Common;

namespace Football.Core.MatchCore
{
    /// <summary>Cinemática de los jugadores: aceleración limitada, giro con inercia y resistencia. Sin allocations.</summary>
    public static class PlayerMotion
    {
        private const float DiveAcceleration = 40f;
        private const float MinSpeedForTurnDrag = 0.01f;

        /// <summary>Avanza un jugador un paso hacia su objetivo.</summary>
        /// <param name="p">Jugador.</param>
        /// <param name="dt">Duración del paso (s).</param>
        /// <param name="c">Parámetros de jugadores.</param>
        public static void Step(MatchPlayer p, float dt, PlayerConfig c)
        {
            Vec2 toTarget = p.Target - p.Pos;
            float distance = toTarget.Length;

            // El cansancio resta velocidad punta y aceleración; una lesión leve, más velocidad.
            float staminaFactor = MathUtil.Lerp(c.StaminaSpeedFloor, 1f, p.Stamina);
            float topSpeed = p.TopSpeed * staminaFactor * (p.Injured ? c.InjurySpeedFactor : 1f);
            float desiredSpeed = topSpeed * MathUtil.Clamp01(p.SpeedFactor);
            float acceleration = p.Acceleration * MathUtil.Lerp(c.StaminaAccelerationFloor, 1f, p.Stamina);

            if (p.Diving)
            {
                desiredSpeed = c.KeeperDiveSpeed;
                acceleration = DiveAcceleration;
            }

            if (distance < c.StopRadius)
            {
                desiredSpeed = 0f;
            }
            else if (distance < c.ArriveRadius)
            {
                desiredSpeed *= MathUtil.Clamp(distance / c.ArriveRadius, 0.25f, 1f);
            }

            if (desiredSpeed > MathUtil.Epsilon && distance >= c.StopRadius)
            {
                float targetHeading = toTarget.Angle;
                float diff = MathUtil.AngleDiff(targetHeading, p.Heading);
                if (p.Diving)
                {
                    p.Heading = targetHeading;
                }
                else
                {
                    float turnRate = c.TurnRate * p.TurnScale / (1f + c.TurnSpeedPenalty * p.Speed);
                    float maxTurn = turnRate * dt;
                    p.Heading += MathUtil.Clamp(diff, -maxTurn, maxTurn);
                    float remaining = MathUtil.AngleDiff(targetHeading, p.Heading);
                    desiredSpeed *= MathF.Max(c.MisalignedSpeedFactor, MathF.Cos(remaining));
                }
            }

            if (desiredSpeed > p.Speed)
            {
                p.Speed = MathF.Min(desiredSpeed, p.Speed + acceleration * dt);
            }
            else
            {
                p.Speed = MathF.Max(desiredSpeed, p.Speed - c.Deceleration * dt);
            }

            p.Pos += Vec2.FromAngle(p.Heading) * (p.Speed * dt);
            UpdateStamina(p, dt, c);
        }

        private static void UpdateStamina(MatchPlayer p, float dt, PlayerConfig c)
        {
            float relative = p.Speed / p.TopSpeed;
            float sprintLoad = MathUtil.Clamp01((relative - c.JogFraction) / (1f - c.JogFraction));
            float drain = (c.StaminaDrainJog * relative + c.StaminaDrainSprint * sprintLoad) * p.StaminaDrainScale;
            float recovery = relative < 0.3f ? c.StaminaRecovery : 0f;
            p.Stamina = MathUtil.Clamp01(p.Stamina - (drain - recovery) * dt);
        }
    }
}
