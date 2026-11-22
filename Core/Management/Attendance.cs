using System;
using Football.Core.Common;

namespace Football.Core.Management
{
    /// <summary>Nivel de precio de las entradas.</summary>
    public enum TicketLevel
    {
        /// <summary>Popular / barato.</summary>
        Popular,
        /// <summary>Normal.</summary>
        Normal,
        /// <summary>Alto.</summary>
        High,
        /// <summary>Premium.</summary>
        Premium
    }

    /// <summary>Importancia de un partido para la hinchada.</summary>
    public enum MatchImportance
    {
        /// <summary>Final de copa o duelo decisivo de copa continental.</summary>
        CupFinal,
        /// <summary>Clásico o rivalidad.</summary>
        Classic,
        /// <summary>Partido clave en las fechas finales (título, clasificación o descenso).</summary>
        Decisive,
        /// <summary>Fecha regular.</summary>
        Regular,
        /// <summary>Sin nada en juego.</summary>
        Meaningless
    }

    /// <summary>Semáforo del precio frente al rango ideal.</summary>
    public enum PriceStatus
    {
        /// <summary>Dentro (o por debajo) del rango ideal.</summary>
        Ideal,
        /// <summary>Algo por encima del ideal.</summary>
        Elevated,
        /// <summary>Excesivo: riesgo de queja de los socios.</summary>
        Excessive
    }

    /// <summary>Parámetros de la asistencia y del precio de las entradas (en <c>career.json</c>).</summary>
    [Serializable]
    public sealed class AttendanceConfig
    {
        /// <summary>Multiplicador de demanda por importancia (final, clásico, decisivo, regular, intrascendente).</summary>
        public float[] Importance = { 1.45f, 1.35f, 1.25f, 1f, 0.65f };
        /// <summary>Multiplicador de precio por nivel (popular, normal, alto, premium) sobre el precio de referencia.</summary>
        public float[] LevelMultiplier = { 0.6f, 1f, 1.5f, 2.3f };
        /// <summary>Precio de referencia de una entrada.</summary>
        public long ReferencePrice = 14000;
        /// <summary>Ocupación base con reputación 50 y todo neutro.</summary>
        public float BaseOccupancy = 0.62f;
        /// <summary>Ocupación extra por punto de reputación sobre 50.</summary>
        public float PerReputation = 0.005f;
        /// <summary>Peso de la racha (0 = perdió todo, 1 = ganó todo; 0,5 es neutro).</summary>
        public float FormWeight = 0.4f;
        /// <summary>Peso de la posición frente a lo esperado (−1 muy por debajo, +1 muy por encima).</summary>
        public float PositionWeight = 0.15f;
        /// <summary>Peso del ánimo de la hinchada (0–1, 0,5 neutro).</summary>
        public float MoodWeight = 0.3f;
        /// <summary>Caída de la demanda con el disgusto de los socios al máximo.</summary>
        public float UnrestWeight = 0.35f;
        /// <summary>Elasticidad al precio en una fecha regular.</summary>
        public float PriceElasticity = 1.1f;
        /// <summary>Cuánto amortigua la importancia la elasticidad (exponente: en una final el precio pesa mucho menos).</summary>
        public float ImportanceDampening = 3f;
        /// <summary>Fracción de la capacidad que se puede usar sin estacionamiento ni accesos.</summary>
        public float BaseAccessCap = 0.85f;
        /// <summary>Capacidad utilizable extra por cada plaza de estacionamiento.</summary>
        public float AccessPerParkingSpace = 0.00003f;
        /// <summary>Ancho del rango ideal (± sobre el centro).</summary>
        public float IdealSpread = 0.15f;
        /// <summary>Hasta cuánto sobre el máximo ideal el precio es «elevado» (amarillo); más es «excesivo» (rojo).</summary>
        public float ElevatedMargin = 0.25f;
        /// <summary>Atractivo extra de un rival según su reputación (por punto sobre 50).</summary>
        public float OpponentAppeal = 0.006f;
    }

    /// <summary>Todo lo que influye en la asistencia a un partido de local.</summary>
    public struct AttendanceInput
    {
        /// <summary>Capacidad del estadio (sin los sectores en obra).</summary>
        public int Capacity;
        /// <summary>Plazas de estacionamiento.</summary>
        public int ParkingSpaces;
        /// <summary>Precio de la entrada.</summary>
        public long Price;
        /// <summary>Importancia del partido.</summary>
        public MatchImportance Importance;
        /// <summary>Reputación del club local (1–100).</summary>
        public int Reputation;
        /// <summary>Reputación del rival (1–100).</summary>
        public int OpponentReputation;
        /// <summary>Racha: puntos de los últimos 5 partidos divididos por 15 (0,5 si no hay datos).</summary>
        public float Form;
        /// <summary>Posición frente a lo esperado: −1 muy por debajo, 0 lo esperado, +1 muy por encima.</summary>
        public float PositionVsExpectation;
        /// <summary>Ánimo de la hinchada (0–1).</summary>
        public float FanMood;
        /// <summary>Disgusto de los socios (0–100).</summary>
        public float SupporterUnrest;
    }

    /// <summary>Resultado del cálculo de asistencia.</summary>
    public struct AttendanceResult
    {
        /// <summary>Ocupación del estadio (0–1).</summary>
        public float Ratio;
        /// <summary>Espectadores.</summary>
        public int Spectators;
        /// <summary>Recaudación de taquilla.</summary>
        public long Revenue;
    }

    /// <summary>
    /// Asistencia a un partido de local: demanda por reputación, importancia, racha, posición, ánimo y disgusto de los socios,
    /// con elasticidad al precio que se amortigua en los partidos importantes (una final se llena con entradas caras; un
    /// partido sin nada en juego con entradas caras se vacía). El tope lo pone la capacidad útil (accesos y estacionamiento).
    /// </summary>
    public static class MatchAttendanceCalculator
    {
        /// <summary>Calcula ocupación, espectadores y recaudación.</summary>
        public static AttendanceResult Compute(AttendanceInput i, AttendanceConfig c)
        {
            float importance = c.Importance[(int)i.Importance];
            float demand = c.BaseOccupancy + c.PerReputation * (i.Reputation - 50);
            demand *= importance;
            demand *= 1f + c.FormWeight * (MathUtil.Clamp01(i.Form) - 0.5f) * 2f;
            demand *= 1f + c.PositionWeight * MathUtil.Clamp(i.PositionVsExpectation, -1f, 1f);
            demand *= 1f + c.MoodWeight * (MathUtil.Clamp01(i.FanMood) - 0.5f) * 2f;
            demand *= 1f - c.UnrestWeight * MathUtil.Clamp01(i.SupporterUnrest / 100f);
            demand *= 1f + c.OpponentAppeal * (i.OpponentReputation - 50);

            double price = Math.Max(1L, i.Price);
            double elasticity = c.PriceElasticity / Math.Pow(importance, c.ImportanceDampening);
            demand *= (float)Math.Pow(c.ReferencePrice / price, elasticity);

            float accessCap = MathUtil.Clamp01(c.BaseAccessCap + c.AccessPerParkingSpace * i.ParkingSpaces);
            float ratio = MathUtil.Clamp(demand, 0f, accessCap);
            int spectators = (int)(Math.Max(0, i.Capacity) * ratio);
            return new AttendanceResult { Ratio = ratio, Spectators = spectators, Revenue = spectators * (long)price };
        }

        /// <summary>Precio de un nivel.</summary>
        public static long PriceOf(TicketLevel level, AttendanceConfig c) => (long)(c.ReferencePrice * (double)c.LevelMultiplier[(int)level]);
    }

    /// <summary>
    /// Rango ideal del precio de las entradas para un partido: más alto contra un clásico, un grande o en una final; más bajo
    /// en una fecha sin nada en juego o con el equipo en mala racha. Fuera del rango, los socios se quejan.
    /// </summary>
    public static class TicketPricingAdvisor
    {
        /// <summary>Rango ideal [mínimo, máximo] para el partido.</summary>
        public static (long Min, long Max) IdealRange(AttendanceInput i, AttendanceConfig c)
        {
            float importance = c.Importance[(int)i.Importance];
            float momentum = 0.8f + 0.4f * MathUtil.Clamp01(i.Form) + 0.1f * MathUtil.Clamp(i.PositionVsExpectation, -1f, 1f);
            float appeal = 1f + c.OpponentAppeal * (i.OpponentReputation - 50) + c.PerReputation * (i.Reputation - 50);
            double center = c.ReferencePrice * importance * momentum * Math.Max(0.5f, appeal);
            return ((long)(center * (1 - c.IdealSpread)), (long)(center * (1 + c.IdealSpread)));
        }

        /// <summary>Semáforo del precio frente al rango.</summary>
        public static PriceStatus Status(long price, (long Min, long Max) range, AttendanceConfig c) =>
            price <= range.Max ? PriceStatus.Ideal : price <= range.Max * (1 + c.ElevatedMargin) ? PriceStatus.Elevated : PriceStatus.Excessive;
    }
}
