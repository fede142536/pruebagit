using System;

namespace Football.Core.Management
{
    /// <summary>Parámetros de la gestión del plantel (<c>Data/balance/management.json</c>).</summary>
    [Serializable]
    public sealed class ManagementConfig
    {
        /// <summary>Atributos efectivos extra por cada punto de forma sobre 6 (fracción).</summary>
        public float FormWeight = 0.015f;
        /// <summary>Atributos efectivos extra por cada punto de moral sobre 50 (fracción).</summary>
        public float MoraleWeight = 0.0008f;
        /// <summary>Atributos efectivos que se pierden con el estado físico en 0 (fracción).</summary>
        public float FitnessPenalty = 0.15f;
        /// <summary>Ventaja de local: atributos efectivos extra del equipo local (fracción).</summary>
        public float HomeAdvantage = 0.03f;
        /// <summary>Cuánto pesa la nota del último partido en la forma (media móvil).</summary>
        public float FormSmoothing = 0.3f;
        /// <summary>Moral por ganar, empatar y perder.</summary>
        public float[] ResultMorale = { 4f, 0f, -4f };
        /// <summary>Moral extra por cada punto de nota por encima (o debajo) de 6.</summary>
        public float RatingMorale = 2f;
        /// <summary>Recuperación física por día de descanso.</summary>
        public float FitnessRecoveryPerDay = 0.12f;
        /// <summary>Días de baja de una lesión leve (mínimo y máximo, con departamento médico de nivel 1).</summary>
        public int[] InjuryDays = { 4, 21 };
        /// <summary>Días de baja que ahorra cada nivel del departamento médico por encima del 1 (fracción).</summary>
        public float MedicalLevelSaving = 0.08f;
        /// <summary>Partidos de sanción por roja.</summary>
        public int RedCardBan = 2;
        /// <summary>Amarillas que acumuladas dan una fecha de sanción.</summary>
        public int YellowLimit = 5;
        /// <summary>Fracción de los partidos del equipo que cada promesa garantiza jugar (índice = promesa).</summary>
        public float[] PromiseShare = { 0f, 0.8f, 0.6f, 0.35f, 0.15f, 0.2f };
        /// <summary>Moral que cuesta por mes una promesa incumplida.</summary>
        public float BrokenPromiseMorale = 8f;
        /// <summary>Partidos mínimos antes de evaluar una promesa.</summary>
        public int PromiseGraceMatches = 6;

        // ---- Scouting progresivo ----
        /// <summary>Ancho del rango de un atributo sin ojear nada (puntos).</summary>
        public float ScoutingMaxWidth = 30f;
        /// <summary>Conocimiento ganado por día de ojeo con un ojeador de capacidad 10.</summary>
        public float ScoutingPerDay = 0.05f;

        // ---- Mercado ----
        /// <summary>Valor de mercado de un jugador de media 60 y 25 años.</summary>
        public long BaseValue = 400_000_000;
        /// <summary>El valor se multiplica por e^(esto × (media − 60)).</summary>
        public float ValuePerRating = 0.11f;
        /// <summary>Edad de máximo valor.</summary>
        public int PeakValueAge = 26;
        /// <summary>Pérdida de valor por año de distancia a esa edad (fracción).</summary>
        public float ValuePerYearFromPeak = 0.07f;
        /// <summary>Cuánto pide el vendedor sobre el valor, por perfil (comprador, canterano-vendedor, defensivo).</summary>
        public float[] AskFactor = { 1.1f, 1.5f, 1.25f };
        /// <summary>Cuánto rebaja el vendedor el pedido por cada 10 % de porcentaje de venta futura.</summary>
        public float SellOnDiscount = 0.06f;
        /// <summary>Por debajo de esta fracción del pedido, rechaza sin contraoferta.</summary>
        public float RejectBelow = 0.6f;
        /// <summary>Sueldo semanal pedido por un jugador de media 60.</summary>
        public long BaseWage = 1_500_000;
        /// <summary>El sueldo pedido se multiplica por e^(esto × (media − 60)).</summary>
        public float WagePerRating = 0.09f;
        /// <summary>Sueldo extra pedido según la promesa de minutos (índice = promesa; ser figura cuesta más).</summary>
        public float[] PromiseWageFactor = { 1f, 1.3f, 1.1f, 0.95f, 0.85f, 0.8f };
        /// <summary>Cuánto puede rebajar su pedido según la personalidad (índice = rasgo).</summary>
        public float[] WageFlexibility = { 0.1f, 0.1f, 0.08f, 0.02f, 0.25f, 0.05f };

        // ---- Cantera ----
        /// <summary>Juveniles por año.</summary>
        public int[] YouthIntakeSize = { 3, 5 };
        /// <summary>Potencial medio de los juveniles con cantera de nivel 1.</summary>
        public float YouthPotentialBase = 52f;
        /// <summary>Potencial extra por nivel de cantera.</summary>
        public float YouthPotentialPerLevel = 4f;
        /// <summary>Potencial extra por punto de capacidad del entrenador de juveniles sobre 10.</summary>
        public float YouthPotentialPerCoach = 0.8f;
        /// <summary>Media actual de un juvenil respecto de su potencial (fracción).</summary>
        public float YouthCurrentShare = 0.7f;

        // ---- Vestuario y química ----
        /// <summary>Minutos juntos en el campo para que dos jugadores se entiendan del todo.</summary>
        public float ChemistryFullMinutes = 1800f;
        /// <summary>Atributos efectivos extra con química máxima en el once (fracción).</summary>
        public float ChemistryBonus = 0.02f;
        /// <summary>Cuánto arrastran los líderes la moral del resto hacia la suya (fracción por mes).</summary>
        public float LeaderMoralePull = 0.25f;

        // ---- Historia viva ----
        /// <summary>Edad desde la que un jugador puede retirarse.</summary>
        public int RetirementAge = 34;
        /// <summary>Edad máxima de un jugador libre que figura en el mercado (los veteranos que siguen buscando club no se dan por retirados todavía).</summary>
        public int FreeAgentMaxAge = 40;
        /// <summary>Probabilidad anual de retirarse por cada año sobre esa edad.</summary>
        public float RetirementChancePerYear = 0.2f;
        /// <summary>Goles en la carrera para entrar al salón de la fama al retirarse.</summary>
        public int HallOfFameGoals = 100;
    }
}
