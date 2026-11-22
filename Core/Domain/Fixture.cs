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

        /// <summary>Competición a la que pertenece.</summary>
        public int CompetitionId { get; set; }

        /// <summary>Fecha (jornada) dentro de la competición, desde 1.</summary>
        public int Round { get; set; }

        /// <summary>Zona o grupo (0 si la competición no tiene zonas).</summary>
        public int Group { get; set; }

        /// <summary>Penales del local en una eliminatoria empatada (null si no hubo).</summary>
        public int? HomePenalties { get; set; }

        /// <summary>Penales del visitante.</summary>
        public int? AwayPenalties { get; set; }

        /// <summary>Indica si el partido ya tiene resultado.</summary>
        public bool IsPlayed => HomeGoals.HasValue && AwayGoals.HasValue;
    }
}
