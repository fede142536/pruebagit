using System;

namespace Football.Core.Domain
{
    /// <summary>Partido programado en el calendario.</summary>
    public sealed class Fixture
    {
        /// <summary>Identificador único (positivo).</summary>
        public int Id { get; set; }

        /// <summary>Club local.</summary>
        public int HomeClubId { get; set; }

        /// <summary>Club visitante.</summary>
        public int AwayClubId { get; set; }

        /// <summary>Fecha del partido.</summary>
        public DateTime Date { get; set; }

        /// <summary>Goles del local, o <c>null</c> si no se jugó.</summary>
        public int? HomeGoals { get; set; }

        /// <summary>Goles del visitante, o <c>null</c> si no se jugó.</summary>
        public int? AwayGoals { get; set; }

        /// <summary>Indica si el partido ya tiene resultado.</summary>
        public bool IsPlayed => HomeGoals.HasValue && AwayGoals.HasValue;
    }
}
