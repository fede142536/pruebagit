using System;
using System.Linq;
using Football.Core.Common;
using Football.Core.Domain;

namespace Football.Core.Career
{
    /// <summary>Pronóstico de un partido: cielo y temperatura.</summary>
    public struct MatchForecast
    {
        /// <summary>Cielo.</summary>
        public MatchWeather Weather;
        /// <summary>Temperatura (°C, redondeada).</summary>
        public int Temperature;
    }

    /// <summary>Resumen del dinero de la temporada del usuario para la pantalla de finanzas.</summary>
    public struct SeasonFinanceSummary
    {
        /// <summary>Ingresos de la temporada (positivo).</summary>
        public long Income;
        /// <summary>Gastos de la temporada (positivo).</summary>
        public long Expenses;
        /// <summary>Movimiento neto por categoría (índice = <see cref="FinanceCategory"/>).</summary>
        public long[] ByCategory;
        /// <summary>Caja proyectada al cierre de la temporada si sigue el ritmo semanal actual.</summary>
        public long ProjectedCash;
        /// <summary>Ganancia neta (ingresos menos gastos).</summary>
        public long Net => Income - Expenses;
    }

    public sealed partial class CareerSession
    {
        private const int ForecastSalt = 0x5EA7;

        /// <summary>Pronóstico determinista de un partido (sale de la semilla y del partido, no cambia al recargar).</summary>
        /// <param name="f">Partido.</param>
        public MatchForecast ForecastOf(Fixture f)
        {
            CareerConfig c = _cfg.Career;
            int month = f.Date.Month - 1;
            IRandom rng = new SeededRandom(Mix(((long)f.Id << 16) ^ ForecastSalt));
            bool rain = rng.NextFloat() < c.RainChanceByMonth[month];
            bool clouds = !rain && rng.NextFloat() < c.CloudChance;
            float temperature = c.MeanTemperatureByMonth[month] + (rng.NextFloat() * 2f - 1f) * c.TemperatureSpread - (rain ? c.RainCooling : 0f);
            return new MatchForecast
            {
                Weather = rain ? MatchWeather.Rain : clouds ? MatchWeather.Cloudy : MatchWeather.Sunny,
                Temperature = (int)Math.Round(temperature)
            };
        }

        /// <summary>Ingresos, gastos y proyección de la caja del usuario en la temporada en curso.</summary>
        public SeasonFinanceSummary FinanceSummary()
        {
            ClubFinances fin = Data.Finances[Data.UserClubId];
            DateTime start = SeasonStartDate;
            var byCategory = new long[Enum.GetValues(typeof(FinanceCategory)).Length];
            long income = 0, expenses = 0;
            foreach (LedgerEntry e in fin.Ledger.Where(e => e.Date >= start))
            {
                byCategory[(int)e.Category] += e.Amount;
                if (e.Amount >= 0)
                {
                    income += e.Amount;
                }
                else
                {
                    expenses -= e.Amount;
                }
            }

            DateTime end = new DateTime(Data.Season, 12, 31);
            double elapsedWeeks = Math.Max(1.0, (Data.Today - start).TotalDays / DaysPerWeek);
            double remainingWeeks = Math.Max(0.0, (end - Data.Today).TotalDays / DaysPerWeek);
            long projected = fin.Cash + (long)((income - expenses) / elapsedWeeks * remainingWeeks);
            return new SeasonFinanceSummary { Income = income, Expenses = expenses, ByCategory = byCategory, ProjectedCash = projected };
        }
    }
}
