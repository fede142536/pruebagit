using System;

namespace Football.Core.Domain
{
    /// <summary>Estilo de juego de un equipo.</summary>
    [Serializable]
    public sealed class TeamStyle
    {
        /// <summary>Nombre del estilo.</summary>
        public string Name = "Balanced";
        /// <summary>Altura de la línea defensiva (0 = muy atrás, 1 = muy adelantada).</summary>
        public float LineHeight = 0.5f;
        /// <summary>Intensidad de la presión (0..1).</summary>
        public float PressIntensity = 0.5f;
        /// <summary>Amplitud del equipo (0..1).</summary>
        public float Width = 0.5f;
        /// <summary>Preferencia por el juego directo hacia delante (0..1).</summary>
        public float Directness = 0.5f;
        /// <summary>Disposición a asumir riesgos en pases y tiros (0..1).</summary>
        public float Risk = 0.5f;
        /// <summary>Compactación entre líneas (0..1).</summary>
        public float Compactness = 0.5f;
        /// <summary>Rol de los laterales cuando el jugador no tiene uno elegido.</summary>
        public PlayerDuty FullbackDuty = PlayerDuty.Auto;
        /// <summary>Rol de los mediocampistas centrales cuando no tienen uno elegido.</summary>
        public PlayerDuty MidfieldDuty = PlayerDuty.Auto;
        /// <summary>Rol de los delanteros centro cuando no tienen uno elegido.</summary>
        public PlayerDuty StrikerDuty = PlayerDuty.Auto;

        /// <summary>Crea una copia independiente (para modificarla durante un partido sin tocar la configuración).</summary>
        public TeamStyle Clone() => (TeamStyle)MemberwiseClone();
    }

    /// <summary>Pesos de la nota de cada jugador en el informe del partido (escala 1–10).</summary>
    [Serializable]
    public sealed class ReportConfig
    {
        /// <summary>Nota de partida.</summary>
        public float RatingBase = 6.0f;
        /// <summary>Nota mínima.</summary>
        public float RatingMin = 3.0f;
        /// <summary>Nota máxima.</summary>
        public float RatingMax = 10.0f;
        /// <summary>Por gol.</summary>
        public float GoalWeight = 1.0f;
        /// <summary>Por asistencia.</summary>
        public float AssistWeight = 0.7f;
        /// <summary>Por remate al arco.</summary>
        public float ShotOnTargetWeight = 0.15f;
        /// <summary>Por cada punto de acierto de pase por encima (o por debajo) de <see cref="PassAccuracyReference"/>, con al menos 5 pases.</summary>
        public float PassAccuracyWeight = 2.0f;
        /// <summary>Acierto de pase que no suma ni resta.</summary>
        public float PassAccuracyReference = 0.8f;
        /// <summary>Por balón recuperado.</summary>
        public float RecoveryWeight = 0.08f;
        /// <summary>Por entrada ganada.</summary>
        public float TackleWonWeight = 0.12f;
        /// <summary>Por parada.</summary>
        public float SaveWeight = 0.3f;
        /// <summary>Por gol recibido (arquero y defensores).</summary>
        public float GoalConcededPenalty = 0.25f;
        /// <summary>Por amarilla.</summary>
        public float YellowPenalty = 0.3f;
        /// <summary>Por roja.</summary>
        public float RedPenalty = 1.5f;
        /// <summary>Por gol en propia puerta.</summary>
        public float OwnGoalPenalty = 1.0f;
        /// <summary>Por ganar (por perder resta lo mismo).</summary>
        public float ResultWeight = 0.3f;
        /// <summary>
        /// Factor que convierte el xG con el que decide la IA (un valor relativo) en una probabilidad de gol: medido para que
        /// la suma del xG de los remates coincida con los goles del motor (BALANCE.md §1.7).
        /// </summary>
        public float XgCalibration = 11.1f;
        /// <summary>xG máximo de un remate en el informe.</summary>
        public float XgMax = 0.95f;
        /// <summary>Minutos mínimos para recibir nota (los demás quedan sin calificar).</summary>
        public float MinMinutesForRating = 10f;
    }
}
