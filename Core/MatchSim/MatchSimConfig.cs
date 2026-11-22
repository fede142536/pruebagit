using System;

namespace Football.Core.MatchSim
{
    /// <summary>
    /// Parámetros del simulador rápido (<c>Data/balance/matchsim.json</c>). Calibrados contra el motor en tiempo real
    /// (<c>MatchCore</c>) con equipos de calidad conocida: ver <c>Docs/BALANCE.md</c> §7.
    /// </summary>
    [Serializable]
    public sealed class MatchSimConfig
    {
        /// <summary>Goles esperados por equipo entre equipos iguales (MatchCore: 1,35).</summary>
        public float BaseGoals = 1.33f;
        /// <summary>Cuánto multiplica los goles cada punto de diferencia de fuerza: goles × e^(esto × diferencia).</summary>
        public float StrengthSensitivity = 0.074f;
        /// <summary>
        /// Variación del «tono» del partido, común a los dos equipos (desviación del logaritmo): hay partidos cerrados y
        /// abiertos. Con goles independientes salen menos empates que en el motor en tiempo real.
        /// </summary>
        public float Openness = 0.3f;
        /// <summary>
        /// Saturación del favorito: con mucha diferencia, el equipo fuerte no sigue sumando goles al mismo ritmo (en el motor
        /// en tiempo real, con diferencia 20 marca 4,2 y no 5). Se usa diferencia / (1 + diferencia / este valor).
        /// </summary>
        public float StrengthSaturation = 80f;
        /// <summary>Tiros por equipo entre equipos iguales (MatchCore: 4,7).</summary>
        public float BaseShots = 4.7f;
        /// <summary>Parte de la diferencia de fuerza que se traduce en más tiros (el resto, en mejores ocasiones).</summary>
        public float ShotShare = 0.35f;
        /// <summary>Cuánto cambia la cantidad de tiros el juego directo (fracción por unidad de Directness por encima de 0,5).</summary>
        public float DirectnessShots = 0.3f;
        /// <summary>xG máximo de un remate.</summary>
        public float MaxShotXg = 0.9f;
        /// <summary>Fracción de los remates que van al arco (MatchCore: 0,43).</summary>
        public float OnTargetShare = 0.43f;
        /// <summary>Fracción de los goles con asistencia (MatchCore: 0,42).</summary>
        public float AssistShare = 0.42f;
        /// <summary>Posesión extra por cada punto de diferencia de mediocampo.</summary>
        public float PossessionSensitivity = 0.012f;
        /// <summary>Pases por equipo con la posesión repartida a medias (MatchCore: 60).</summary>
        public float BasePasses = 60f;
        /// <summary>Acierto de pase medio (MatchCore: 0,857).</summary>
        public float PassAccuracy = 0.857f;
        /// <summary>Acierto de pase extra por cada punto de pase medio por encima de 65.</summary>
        public float PassAccuracyPerPoint = 0.003f;
        /// <summary>Faltas por equipo (MatchCore: 1,75).</summary>
        public float Fouls = 1.75f;
        /// <summary>Amarillas por equipo (MatchCore: 0,32).</summary>
        public float Yellows = 0.32f;
        /// <summary>Rojas por equipo (MatchCore: 0,02).</summary>
        public float Reds = 0.02f;
        /// <summary>Córners por equipo (MatchCore: 0,4).</summary>
        public float Corners = 0.4f;
        /// <summary>Lesiones leves por equipo (MatchCore: 0,13).</summary>
        public float Injuries = 0.13f;
        /// <summary>Recuperaciones por equipo (MatchCore: 16,8).</summary>
        public float Recoveries = 16.8f;
        /// <summary>Resistencia media al final para un jugador de campo de Stamina 65 (MatchCore: 0,72).</summary>
        public float StaminaLeft = 0.72f;
        /// <summary>Variación de la resistencia final por punto de Stamina.</summary>
        public float StaminaPerPoint = 0.006f;
        /// <summary>Distancia por jugador de campo en un partido (km; MatchCore comprime 90' en 6 min).</summary>
        public float DistanceKm = 1.0f;
        /// <summary>Cambios por equipo cuando hay suplentes.</summary>
        public int Substitutions = 3;
        /// <summary>Primer minuto en que la IA hace cambios.</summary>
        public float SubstitutionFrom = 58f;
        /// <summary>Último minuto de cambios.</summary>
        public float SubstitutionTo = 82f;
        /// <summary>Estilos con efecto medido en el motor en tiempo real (mismo orden que los factores).</summary>
        public string[] StyleNames = { "Balanced", "Possession", "Counter Attack", "High Press", "Defensive" };
        /// <summary>Goles a favor de cada estilo frente a uno equilibrado (medido con MatchCore, 150 partidos).</summary>
        public float[] StyleAttack = { 1f, 0.8f, 0.87f, 1.3f, 0.75f };
        /// <summary>Goles en contra de cada estilo frente a uno equilibrado.</summary>
        public float[] StyleDefense = { 1f, 1f, 0.9f, 1.1f, 0.7f };

        /// <summary>Factor de goles a favor (o en contra) de un estilo por su nombre (1 si no se conoce).</summary>
        /// <param name="style">Nombre del estilo.</param>
        /// <param name="attack">Verdadero para goles a favor; falso para goles en contra.</param>
        public float StyleFactor(string style, bool attack)
        {
            int i = Array.IndexOf(StyleNames, style);
            float[] factors = attack ? StyleAttack : StyleDefense;
            return i >= 0 && i < factors.Length ? factors[i] : 1f;
        }

        /// <summary>Peso relativo de cada línea al repartir los remates (arquero, defensa, medio, ataque).</summary>
        public float[] ShotWeights = { 0f, 0.4f, 1.2f, 3.5f };
        /// <summary>Peso relativo de cada línea al repartir las asistencias y los pases.</summary>
        public float[] PassWeights = { 0.4f, 1f, 1.8f, 1.2f };
    }
}
