using System;

namespace Football.Core.Domain
{
    /// <summary>Contrato de un jugador con su club.</summary>
    public sealed class Contract
    {
        /// <summary>Salario semanal en unidades menores de moneda (centavos).</summary>
        public long WeeklyWage { get; set; }

        /// <summary>Fecha de vencimiento del contrato.</summary>
        public DateTime ExpiresOn { get; set; }
    }
}
