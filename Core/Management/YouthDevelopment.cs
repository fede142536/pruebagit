using System;
using Football.Core.Common;
using Football.Core.Domain;

namespace Football.Core.Management
{
    /// <summary>Parámetros del desarrollo de los jóvenes (viven en <c>career.json</c>).</summary>
    public sealed class DevelopmentSettings
    {
        /// <summary>Edad hasta la que el jugador se desarrolla.</summary>
        public int MaxAge;
        /// <summary>Puntos de atributo por semana en el mejor caso.</summary>
        public float PointsPerWeek;
        /// <summary>Factor por edad desde los 16 años.</summary>
        public float[] AgeFactor;
        /// <summary>Minutos semanales con los que el desarrollo es completo.</summary>
        public float FullMinutes;
        /// <summary>Desarrollo sin jugar, respecto del completo.</summary>
        public float IdleShare;
        /// <summary>Factor de moral con 0 y con 100.</summary>
        public float[] MoraleRange;
        /// <summary>Factor del entrenador con capacidad 1 y con 20.</summary>
        public float[] CoachRange;
        /// <summary>Puntos que faltan para el potencial desde los cuales se frena.</summary>
        public float SlowGap;
    }

    /// <summary>
    /// Desarrollo de los jóvenes: cada semana el jugador gana una fracción de punto de atributo que depende de su edad, de los minutos que jugó
    /// (en primera o en la reserva), de su moral, del entrenador de juveniles y de lo lejos que está de su potencial (que no se ve). Los puntos
    /// se reparten casi siempre entre los atributos que componen su valoración, así que el efecto es visible en la media.
    /// </summary>
    public static class YouthDevelopment
    {
        private const float KeyShare = 0.8f;
        private const int FirstAge = 16;
        private const float Percent = 100f;
        private const float CoachScale = 19f;

        /// <summary>Puntos de atributo que gana en una semana.</summary>
        /// <param name="s">Parámetros.</param>
        /// <param name="age">Edad.</param>
        /// <param name="overall">Valoración actual.</param>
        /// <param name="potential">Potencial.</param>
        /// <param name="weekMinutes">Minutos jugados en la semana.</param>
        /// <param name="morale">Moral (0–100).</param>
        /// <param name="coachAbility">Capacidad del entrenador de juveniles (1–20).</param>
        public static float WeeklyPoints(DevelopmentSettings s, int age, float overall, int potential, float weekMinutes, float morale, int coachAbility)
        {
            if (age > s.MaxAge || overall >= potential)
            {
                return 0f;
            }

            float byAge = s.AgeFactor[Math.Max(0, Math.Min(s.AgeFactor.Length - 1, age - FirstAge))];
            float played = s.IdleShare + (1f - s.IdleShare) * MathUtil.Clamp(weekMinutes / s.FullMinutes, 0f, 1f);
            float byMorale = s.MoraleRange[0] + (s.MoraleRange[1] - s.MoraleRange[0]) * MathUtil.Clamp(morale / Percent, 0f, 1f);
            float byCoach = s.CoachRange[0] + (s.CoachRange[1] - s.CoachRange[0]) * MathUtil.Clamp((coachAbility - 1) / CoachScale, 0f, 1f);
            float bySlow = MathUtil.Clamp((potential - overall) / s.SlowGap, 0f, 1f);
            return s.PointsPerWeek * byAge * played * byMorale * byCoach * bySlow;
        }

        /// <summary>Suma los puntos al acumulado del jugador y sube un atributo por cada punto entero reunido (nunca pasa del potencial).</summary>
        /// <param name="p">Jugador.</param>
        /// <param name="cond">Su estado (guarda el resto fraccionario).</param>
        /// <param name="points">Puntos ganados.</param>
        /// <param name="rng">Azar.</param>
        /// <returns>Cantidad de atributos que subieron.</returns>
        public static int Apply(Player p, PlayerCondition cond, float points, IRandom rng)
        {
            cond.DevProgress += points;
            AttributeId[] key = PlayerRatings.KeyAttributes(PlayerRatings.LineOf(p.Position));
            int raised = 0;
            while (cond.DevProgress >= 1f)
            {
                cond.DevProgress -= 1f;
                if (PlayerRatings.Overall(p.Attributes, p.Position) >= p.Potential)
                {
                    cond.DevProgress = 0f;
                    break;
                }

                AttributeId id = rng.NextFloat() < KeyShare ? key[rng.NextInt(0, key.Length)] : AttributeSet.All[rng.NextInt(0, AttributeSet.All.Count)];
                if (p.Attributes[id] < AttributeScale.Max)
                {
                    p.Attributes[id] = p.Attributes[id] + 1;
                    raised++;
                }
            }

            return raised;
        }
    }
}
