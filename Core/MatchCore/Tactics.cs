using Football.Core.Common;

namespace Football.Core.MatchCore
{
    /// <summary>Mentalidad del equipo: cuánto arriesga y cuánto se adelanta.</summary>
    public enum Mentality
    {
        /// <summary>Muy defensiva: bloque bajo, sin riesgos.</summary>
        VeryDefensive = -2,
        /// <summary>Defensiva.</summary>
        Defensive = -1,
        /// <summary>Equilibrada: el estilo del equipo tal cual.</summary>
        Balanced = 0,
        /// <summary>Ofensiva.</summary>
        Attacking = 1,
        /// <summary>Muy ofensiva: bloque alto, juego directo y arriesgado.</summary>
        VeryAttacking = 2
    }

    /// <summary>Intensidad de la presión sin balón.</summary>
    public enum PressLevel
    {
        /// <summary>Presión baja: se espera replegado.</summary>
        Low = -1,
        /// <summary>La presión propia del estilo.</summary>
        Normal = 0,
        /// <summary>Presión alta: se va a buscar el balón arriba.</summary>
        High = 1
    }

    /// <summary>Ajustes tácticos en vivo sobre el estilo base de un equipo.</summary>
    public static class Tactics
    {
        /// <summary>Escribe en <paramref name="target"/> el estilo base modificado por la mentalidad y la presión.</summary>
        /// <param name="baseStyle">Estilo con el que el equipo empezó el partido (no se modifica).</param>
        /// <param name="mentality">Mentalidad elegida.</param>
        /// <param name="pressing">Presión elegida.</param>
        /// <param name="ai">Parámetros de la IA (tamaño de cada paso de mentalidad y presión).</param>
        /// <param name="target">Estilo efectivo que usa el motor.</param>
        public static void Apply(TeamStyle baseStyle, Mentality mentality, PressLevel pressing, AiConfig ai, TeamStyle target)
        {
            float m = (int)mentality;
            target.Name = baseStyle.Name;
            target.Width = baseStyle.Width;
            target.Compactness = baseStyle.Compactness;
            target.LineHeight = MathUtil.Clamp01(baseStyle.LineHeight + ai.MentalityLineStep * m);
            target.Risk = MathUtil.Clamp01(baseStyle.Risk + ai.MentalityRiskStep * m);
            target.Directness = MathUtil.Clamp01(baseStyle.Directness + ai.MentalityDirectnessStep * m);
            target.PressIntensity = MathUtil.Clamp01(baseStyle.PressIntensity + ai.PressLevelStep * (int)pressing);
        }

        /// <summary>
        /// Táctica que adopta la IA según el marcador y el minuto: si pierde, se vuelca al ataque (más cuanto más tarde);
        /// si gana al final, cierra el partido.
        /// </summary>
        /// <param name="goalDifference">Goles propios menos goles del rival.</param>
        /// <param name="minute">Minuto de partido (0..90).</param>
        /// <param name="ai">Parámetros de la IA.</param>
        /// <param name="pressing">Presión recomendada.</param>
        /// <returns>Mentalidad recomendada.</returns>
        public static Mentality Adaptive(int goalDifference, float minute, AiConfig ai, out PressLevel pressing)
        {
            pressing = PressLevel.Normal;
            if (minute < ai.AdaptStartMinute || goalDifference == 0)
            {
                return Mentality.Balanced;
            }

            bool late = minute >= ai.AdaptLateMinute;
            if (goalDifference < 0)
            {
                pressing = PressLevel.High;
                return late ? Mentality.VeryAttacking : Mentality.Attacking;
            }

            return late ? Mentality.Defensive : Mentality.Balanced;
        }
    }
}
