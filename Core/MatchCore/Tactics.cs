using Football.Core.Common;
using Football.Core.Domain;

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

    /// <summary>
    /// Rol individual de un jugador según su puesto: cambia dónde se coloca sin balón. <see cref="Auto"/> usa el rol que el
    /// estilo del equipo da a ese puesto (o ninguno).
    /// </summary>
    public enum PlayerDuty
    {
        /// <summary>El que marque el estilo del equipo.</summary>
        Auto = 0,
        /// <summary>Lateral defensivo: no pasa al ataque, cierra por dentro.</summary>
        Defensive = 1,
        /// <summary>Lateral carrilero: sube por la banda, muy abierto.</summary>
        WingBack = 2,
        /// <summary>Mediocampista recuperador: se pega al balón en defensa y no se adelanta.</summary>
        BallWinner = 3,
        /// <summary>Mediocampista organizador: se acerca al poseedor para recibir y distribuir.</summary>
        Playmaker = 4,
        /// <summary>Mediocampista llegador: pisa el área en ataque.</summary>
        BoxToBox = 5,
        /// <summary>Delantero pivote: se queda centrado y baja a recibir de espaldas.</summary>
        TargetMan = 6,
        /// <summary>Delantero al espacio: juega en la última línea, pegado al fuera de juego.</summary>
        Runner = 7
    }

    /// <summary>Ajustes tácticos en vivo sobre el estilo base de un equipo.</summary>
    public static class Tactics
    {
        private static readonly PlayerDuty[] FullbackDuties = { PlayerDuty.Auto, PlayerDuty.Defensive, PlayerDuty.WingBack };
        private static readonly PlayerDuty[] MidfieldDuties = { PlayerDuty.Auto, PlayerDuty.BallWinner, PlayerDuty.Playmaker, PlayerDuty.BoxToBox };
        private static readonly PlayerDuty[] StrikerDuties = { PlayerDuty.Auto, PlayerDuty.TargetMan, PlayerDuty.Runner };
        private static readonly PlayerDuty[] NoDuties = { PlayerDuty.Auto };

        /// <summary>Es un lateral.</summary>
        public static bool IsFullback(Position p) => p == Position.LeftBack || p == Position.RightBack;

        /// <summary>Es un mediocampista central (defensivo, central u ofensivo).</summary>
        public static bool IsCentralMidfielder(Position p) =>
            p == Position.DefensiveMidfielder || p == Position.CentralMidfielder || p == Position.AttackingMidfielder;

        /// <summary>Es un delantero centro.</summary>
        public static bool IsStriker(Position p) => p == Position.Striker;

        /// <summary>Roles que admite un puesto (siempre incluye <see cref="PlayerDuty.Auto"/>).</summary>
        /// <param name="position">Puesto.</param>
        public static PlayerDuty[] DutiesFor(Position position) =>
            IsFullback(position) ? FullbackDuties : IsCentralMidfielder(position) ? MidfieldDuties : IsStriker(position) ? StrikerDuties : NoDuties;

        /// <summary>Verdadero si el puesto admite el rol.</summary>
        /// <param name="position">Puesto.</param>
        /// <param name="duty">Rol.</param>
        public static bool Allows(Position position, PlayerDuty duty) => System.Array.IndexOf(DutiesFor(position), duty) >= 0;

        /// <summary>
        /// Rol efectivo de un puesto: el elegido si es válido; si es <see cref="PlayerDuty.Auto"/>, el que el estilo da a ese
        /// tipo de puesto.
        /// </summary>
        /// <param name="position">Puesto.</param>
        /// <param name="chosen">Rol elegido.</param>
        /// <param name="style">Estilo del equipo.</param>
        public static PlayerDuty Effective(Position position, PlayerDuty chosen, TeamStyle style)
        {
            if (chosen != PlayerDuty.Auto && Allows(position, chosen))
            {
                return chosen;
            }

            PlayerDuty fromStyle = IsFullback(position) ? style.FullbackDuty
                : IsCentralMidfielder(position) ? style.MidfieldDuty
                : IsStriker(position) ? style.StrikerDuty
                : PlayerDuty.Auto;
            return Allows(position, fromStyle) ? fromStyle : PlayerDuty.Auto;
        }

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
            target.FullbackDuty = baseStyle.FullbackDuty;
            target.MidfieldDuty = baseStyle.MidfieldDuty;
            target.StrikerDuty = baseStyle.StrikerDuty;
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
