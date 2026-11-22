using System;
using Football.Core.Common;
using Football.Core.Domain;

namespace Football.Core.Management
{
    /// <summary>
    /// Avanza el calendario de la carrera día a día sobre <see cref="SimClock"/> y avisa por el bus de eventos cada semana
    /// (<see cref="WeekPassed"/>) y cada fin de mes (<see cref="MonthClosed"/>). Los sistemas se suscriben; no se llaman
    /// entre sí.
    /// </summary>
    public sealed class SeasonClock
    {
        private const int DaysPerWeek = 7;
        private readonly SimClock _clock;
        private readonly EventBus _bus;
        private int _dayOfWeek;

        /// <summary>Crea el reloj.</summary>
        /// <param name="clock">Reloj de simulación.</param>
        /// <param name="bus">Bus de eventos.</param>
        /// <param name="dayOfWeek">Días ya transcurridos de la semana en curso (0 a 6), para retomar una partida guardada.</param>
        public SeasonClock(SimClock clock, EventBus bus, int dayOfWeek = 0)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _dayOfWeek = ((dayOfWeek % DaysPerWeek) + DaysPerWeek) % DaysPerWeek;
        }

        /// <summary>Fecha actual.</summary>
        public DateTime Today => _clock.Today;

        /// <summary>Avanza días, publicando los eventos de semana y de mes que correspondan.</summary>
        /// <param name="days">Días a avanzar.</param>
        public void Advance(int days)
        {
            for (int d = 0; d < days; d++)
            {
                DateTime before = _clock.Today;
                _clock.AdvanceDays(1);
                if (_clock.Today.Month != before.Month)
                {
                    _bus.Publish(new MonthClosed { Date = before });
                }

                _dayOfWeek++;
                if (_dayOfWeek >= DaysPerWeek)
                {
                    _dayOfWeek = 0;
                    _bus.Publish(new WeekPassed { Date = _clock.Today });
                }
            }
        }
    }
}
