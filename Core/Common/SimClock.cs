using System;

namespace Football.Core.Common
{
    /// <summary>
    /// Reloj de la simulación. El tiempo es una entrada del sistema: la lógica nunca consulta <c>DateTime.Now</c>.
    /// </summary>
    public sealed class SimClock
    {
        /// <summary>Fecha actual de la simulación (sin hora).</summary>
        public DateTime Today { get; private set; }

        /// <summary>Crea un reloj en la fecha indicada.</summary>
        /// <param name="start">Fecha inicial; se descarta la hora.</param>
        public SimClock(DateTime start)
        {
            Today = start.Date;
        }

        /// <summary>Avanza el reloj.</summary>
        /// <param name="days">Número de días a avanzar (no negativo).</param>
        public void AdvanceDays(int days)
        {
            if (days < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(days), "El tiempo de simulación no retrocede.");
            }

            Today = Today.AddDays(days);
        }
    }
}
