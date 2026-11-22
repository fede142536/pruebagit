using System;
using System.Collections.Generic;
using Football.Core.Common;

namespace Football.Core.Management
{
    /// <summary>Un factor que suma o resta puntos en un pilar del medidor (clave de texto + datos para la interfaz).</summary>
    public struct RatingFactor
    {
        /// <summary>Pilar: 0 socios, 1 plantilla y fichajes, 2 presupuesto.</summary>
        public int Pillar;
        /// <summary>Clave del texto en <c>ui_*.json</c> (por ejemplo <c>rating.signing</c>).</summary>
        public string Key;
        /// <summary>Dato que se inserta en el texto (nombre de un jugador, porcentaje…).</summary>
        public string Arg;
        /// <summary>Puntos (positivos o negativos).</summary>
        public float Points;
    }

    /// <summary>Medidor integral de gestión con sus tres pilares (0–100 cada uno).</summary>
    public sealed class ManagementRating
    {
        /// <summary>Relación con los socios.</summary>
        public float Supporters;
        /// <summary>Gestión de la plantilla y de los fichajes.</summary>
        public float Squad;
        /// <summary>Gestión del presupuesto.</summary>
        public float Budget;
        /// <summary>Total ponderado (35 % / 35 % / 30 %).</summary>
        public float Total;
        /// <summary>Detalle de lo que sumó o restó.</summary>
        public readonly List<RatingFactor> Factors = new List<RatingFactor>();
    }

    /// <summary>Un fichaje del DT y cómo viene rindiendo.</summary>
    public sealed class SigningRecord
    {
        /// <summary>Jugador.</summary>
        public int PlayerId;
        /// <summary>Nombre para mostrar.</summary>
        public string Name = string.Empty;
        /// <summary>Monto pagado.</summary>
        public long Fee;
        /// <summary>Fecha del fichaje.</summary>
        public DateTime Date;
        /// <summary>Partidos que jugó desde que llegó.</summary>
        public int Matches;
        /// <summary>Suma de notas.</summary>
        public float RatingSum;
        /// <summary>Goles más asistencias.</summary>
        public int Contributions;
        /// <summary>Partidos que jugó el equipo desde que llegó.</summary>
        public int TeamMatches;

        /// <summary>Nota media (0 si no jugó).</summary>
        public float AverageRating => Matches > 0 ? RatingSum / Matches : 0f;
    }

    /// <summary>Fotografía del medidor en una fecha (historial).</summary>
    public struct ManagementHistoryEntry
    {
        /// <summary>Fecha.</summary>
        public DateTime Date;
        /// <summary>Pilar socios.</summary>
        public float Supporters;
        /// <summary>Pilar plantilla.</summary>
        public float Squad;
        /// <summary>Pilar presupuesto.</summary>
        public float Budget;
        /// <summary>Total.</summary>
        public float Total;
    }

    /// <summary>Datos de la temporada con los que se calcula el medidor.</summary>
    public sealed class ManagementInputs
    {
        /// <summary>Relación con los socios (0–100).</summary>
        public float SupporterRelation = 60f;
        /// <summary>Exceso medio del precio sobre el máximo ideal en los partidos de local (0 = siempre dentro).</summary>
        public float AveragePriceExcess;
        /// <summary>Pedidos de rebaja aceptados e ignorados.</summary>
        public int RequestsAccepted, RequestsIgnored;
        /// <summary>Ocupación media del estadio (negativa si no hubo partidos de local).</summary>
        public float AverageOccupancy = -1f;
        /// <summary>Niveles de servicios y de estacionamiento (0–3).</summary>
        public int ServicesLevel, ParkingLevel;
        /// <summary>Clásicos ganados y perdidos en la temporada.</summary>
        public int ClassicsWon, ClassicsLost;
        /// <summary>Moral media del plantel (0–100).</summary>
        public float AverageMorale = 50f;
        /// <summary>Fracción del plantel con minutos razonables (rotación).</summary>
        public float RotationShare = 0.6f;
        /// <summary>Promesas de minutos incumplidas en la temporada.</summary>
        public int BrokenPromises;
        /// <summary>Fichajes del DT.</summary>
        public readonly List<SigningRecord> Signings = new List<SigningRecord>();
        /// <summary>Valor del club (para medir lo caro de un fichaje).</summary>
        public long ClubValue = 1;
        /// <summary>Caja actual y al empezar la temporada.</summary>
        public long Cash, StartCash = 1;
        /// <summary>Sueldos semanales pagados y presupuesto semanal.</summary>
        public long WageBill, WageBudget = 1;
        /// <summary>Gastado en fichajes y en obras en la temporada.</summary>
        public long TransferSpend, WorksSpend;
    }

    /// <summary>Pesos del medidor (en <c>career.json</c>).</summary>
    [Serializable]
    public sealed class RatingConfig
    {
        /// <summary>Peso de cada pilar en el total (socios, plantilla, presupuesto).</summary>
        public float[] PillarWeight = { 0.35f, 0.35f, 0.3f };
        /// <summary>Puntos por punto de relación sobre 50.</summary>
        public float RelationWeight = 0.4f;
        /// <summary>Puntos perdidos por cada 10 % de precio medio sobre el ideal.</summary>
        public float PricePerTenPercent = 5f;
        /// <summary>Puntos por pedido aceptado / ignorado.</summary>
        public float RequestAccepted = 2f, RequestIgnored = 3f;
        /// <summary>Ocupación de referencia y puntos por cada 10 % sobre o bajo ella.</summary>
        public float OccupancyReference = 0.7f, OccupancyPerTenPercent = 4f;
        /// <summary>Puntos por nivel de servicios o de estacionamiento.</summary>
        public float PerFacilityLevel = 2f;
        /// <summary>Puntos por clásico ganado o perdido.</summary>
        public float PerClassic = 3f;
        /// <summary>Puntos por punto de moral media sobre 50.</summary>
        public float MoraleWeight = 0.5f;
        /// <summary>Rotación de referencia y puntos por cada 10 % sobre o bajo ella.</summary>
        public float RotationReference = 0.6f, RotationPerTenPercent = 2f;
        /// <summary>Puntos por promesa incumplida.</summary>
        public float PerBrokenPromise = 4f;
        /// <summary>Partidos mínimos para evaluar un fichaje.</summary>
        public int SigningMinMatches = 3;
        /// <summary>Nota esperada de un refuerzo.</summary>
        public float SigningExpectedRating = 6.6f;
        /// <summary>Puntos por décima de nota sobre o bajo la esperada.</summary>
        public float SigningPerTenth = 0.8f;
        /// <summary>Puntos por gol o asistencia.</summary>
        public float SigningPerContribution = 0.6f;
        /// <summary>Castigo por lo pagado (fracción del valor del club × este peso) si el fichaje rinde mal o no juega.</summary>
        public float SigningFeeWeight = 40f;
        /// <summary>Tope de puntos por fichaje (±).</summary>
        public float SigningCap = 8f;
        /// <summary>Puntos por cada 10 % de caja sobre o bajo la inicial (con tope).</summary>
        public float BalancePerTenPercent = 5f, BalanceCap = 25f;
        /// <summary>Puntos perdidos por cada 10 % de sueldos sobre el presupuesto.</summary>
        public float WageOverrunPerTenPercent = 8f;
        /// <summary>Premio por invertir a la vez en plantel y en obras.</summary>
        public float BalancedInvestment = 5f;
        /// <summary>Fracción de la caja inicial gastada que se considera despilfarro, y su castigo.</summary>
        public float OverspendShare = 0.6f, Overspend = 10f;
        /// <summary>Castigo por terminar con la caja en negativo.</summary>
        public float Deficit = 20f;
    }

    /// <summary>
    /// Medidor integral de gestión (0–100) con tres pilares auditables: relación con los socios (35 %), gestión de la
    /// plantilla y de los fichajes (35 %) y gestión del presupuesto (30 %). Cada punto que suma o resta queda como un
    /// <see cref="RatingFactor"/> para mostrarlo en la Oficina.
    /// </summary>
    public static class ManagementRatingSystem
    {
        private const float Neutral = 50f;

        /// <summary>Calcula el medidor.</summary>
        public static ManagementRating Compute(ManagementInputs i, RatingConfig c)
        {
            var r = new ManagementRating();
            r.Supporters = Pillar(r, 0, SupporterFactors(i, c, r));
            r.Squad = Pillar(r, 1, SquadFactors(i, c, r));
            r.Budget = Pillar(r, 2, BudgetFactors(i, c, r));
            r.Total = MathUtil.Clamp(r.Supporters * c.PillarWeight[0] + r.Squad * c.PillarWeight[1] + r.Budget * c.PillarWeight[2], 0f, 100f);
            return r;
        }

        /// <summary>Impacto de un fichaje en el pilar de plantilla (0 si todavía no hay partidos suficientes para juzgarlo).</summary>
        public static float SigningImpact(SigningRecord s, long clubValue, RatingConfig c)
        {
            float feeShare = (float)s.Fee / Math.Max(1L, clubValue);
            if (s.Matches < c.SigningMinMatches)
            {
                // No juega: si el equipo ya jugó bastante desde su llegada, lo pagado pesa en contra.
                return s.TeamMatches >= c.SigningMinMatches * 2 ? -Math.Min(c.SigningCap, c.SigningFeeWeight * feeShare + 1f) : 0f;
            }

            float points = (s.AverageRating - c.SigningExpectedRating) * 10f * c.SigningPerTenth + s.Contributions * c.SigningPerContribution;
            if (points < 0f)
            {
                points -= c.SigningFeeWeight * feeShare; // caro y malo: resta más
            }

            return MathUtil.Clamp(points, -c.SigningCap, c.SigningCap);
        }

        private static float SupporterFactors(ManagementInputs i, RatingConfig c, ManagementRating r)
        {
            float sum = 0f;
            sum += Add(r, 0, "rating.relation", Round(i.SupporterRelation), (i.SupporterRelation - Neutral) * c.RelationWeight);
            if (i.AveragePriceExcess > 0f)
            {
                sum += Add(r, 0, "rating.price_above_ideal", Percent(i.AveragePriceExcess), -i.AveragePriceExcess * 10f * c.PricePerTenPercent);
            }

            sum += Add(r, 0, "rating.requests_accepted", i.RequestsAccepted.ToString(), i.RequestsAccepted * c.RequestAccepted);
            sum += Add(r, 0, "rating.requests_ignored", i.RequestsIgnored.ToString(), -i.RequestsIgnored * c.RequestIgnored);
            if (i.AverageOccupancy >= 0f)
            {
                sum += Add(r, 0, "rating.occupancy", Percent(i.AverageOccupancy), (i.AverageOccupancy - c.OccupancyReference) * 10f * c.OccupancyPerTenPercent);
            }

            sum += Add(r, 0, "rating.facilities", (i.ServicesLevel + i.ParkingLevel).ToString(), (i.ServicesLevel + i.ParkingLevel) * c.PerFacilityLevel);
            sum += Add(r, 0, "rating.classics", i.ClassicsWon + "-" + i.ClassicsLost, (i.ClassicsWon - i.ClassicsLost) * c.PerClassic);
            return sum;
        }

        private static float SquadFactors(ManagementInputs i, RatingConfig c, ManagementRating r)
        {
            float sum = 0f;
            sum += Add(r, 1, "rating.morale", Round(i.AverageMorale), (i.AverageMorale - Neutral) * c.MoraleWeight);
            sum += Add(r, 1, "rating.rotation", Percent(i.RotationShare), (i.RotationShare - c.RotationReference) * 10f * c.RotationPerTenPercent);
            sum += Add(r, 1, "rating.promises", i.BrokenPromises.ToString(), -i.BrokenPromises * c.PerBrokenPromise);
            foreach (SigningRecord s in i.Signings)
            {
                float impact = SigningImpact(s, i.ClubValue, c);
                if (impact != 0f)
                {
                    sum += Add(r, 1, "rating.signing", s.Name, impact);
                }
            }

            return sum;
        }

        private static float BudgetFactors(ManagementInputs i, RatingConfig c, ManagementRating r)
        {
            float sum = 0f;
            float balance = (float)(i.Cash - i.StartCash) / Math.Max(1L, Math.Abs(i.StartCash));
            sum += Add(r, 2, "rating.operating_balance", Percent(balance),
                MathUtil.Clamp(balance * 10f * c.BalancePerTenPercent, -c.BalanceCap, c.BalanceCap));
            if (i.WageBill > i.WageBudget)
            {
                float over = (float)(i.WageBill - i.WageBudget) / Math.Max(1L, i.WageBudget);
                sum += Add(r, 2, "rating.wage_overrun", Percent(over), -over * 10f * c.WageOverrunPerTenPercent);
            }

            if (i.TransferSpend > 0 && i.WorksSpend > 0)
            {
                sum += Add(r, 2, "rating.balanced_investment", string.Empty, c.BalancedInvestment);
            }

            if (i.TransferSpend + i.WorksSpend > c.OverspendShare * Math.Max(1L, i.StartCash))
            {
                sum += Add(r, 2, "rating.overspending", string.Empty, -c.Overspend);
            }

            if (i.Cash < 0)
            {
                sum += Add(r, 2, "rating.deficit", string.Empty, -c.Deficit);
            }

            return sum;
        }

        private static float Pillar(ManagementRating r, int pillar, float sum) => MathUtil.Clamp(Neutral + sum, 0f, 100f);

        private static float Add(ManagementRating r, int pillar, string key, string arg, float points)
        {
            if (Math.Abs(points) >= 0.05f)
            {
                r.Factors.Add(new RatingFactor { Pillar = pillar, Key = key, Arg = arg, Points = points });
            }

            return points;
        }

        private static string Percent(float v) => ((int)Math.Round(v * 100f)).ToString() + "%";

        private static string Round(float v) => ((int)Math.Round(v)).ToString();
    }
}
