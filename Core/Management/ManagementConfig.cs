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
    }
}
