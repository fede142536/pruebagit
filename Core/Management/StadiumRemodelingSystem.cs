using System;
using System.Collections.Generic;
using Football.Core.Common;
using Football.Core.Domain;

namespace Football.Core.Management
{
    /// <summary>Categoría de obra del estadio.</summary>
    public enum RemodelCategory
    {
        /// <summary>Tribunas (capacidad).</summary>
        Stands,
        /// <summary>Estacionamiento y accesos.</summary>
        Parking,
        /// <summary>Equipamiento: césped, drenaje e iluminación.</summary>
        Equipment,
        /// <summary>Servicios: gastronomía, tienda, palcos y sanidad.</summary>
        Services
    }

    /// <summary>Estado del campo de juego.</summary>
    public enum PitchCondition
    {
        /// <summary>Malo.</summary>
        Bad,
        /// <summary>Regular.</summary>
        Regular,
        /// <summary>Bueno.</summary>
        Good,
        /// <summary>Muy bueno.</summary>
        VeryGood,
        /// <summary>Excelente.</summary>
        Excellent
    }

    /// <summary>Resultado de contratar una obra.</summary>
    public enum RemodelResult
    {
        /// <summary>Contratada.</summary>
        Ok,
        /// <summary>Ya hay una obra en curso en esa categoría.</summary>
        CategoryBusy,
        /// <summary>Ese nivel ya está hecho (o no existe).</summary>
        InvalidTier,
        /// <summary>No alcanza la caja.</summary>
        NoMoney
    }

    /// <summary>Una obra en curso.</summary>
    public sealed class RemodelWork
    {
        /// <summary>Categoría.</summary>
        public RemodelCategory Category;
        /// <summary>Nivel o magnitud (1–3).</summary>
        public int Tier;
        /// <summary>Semanas totales.</summary>
        public int TotalWeeks;
        /// <summary>Semanas que faltan.</summary>
        public int RemainingWeeks;
        /// <summary>Costo pagado.</summary>
        public long Cost;

        /// <summary>Avance de 0 a 1.</summary>
        public float Progress => TotalWeeks <= 0 ? 1f : 1f - (float)RemainingWeeks / TotalWeeks;
    }

    /// <summary>Estado del estadio de un club (se guarda con la partida).</summary>
    public sealed class StadiumState
    {
        /// <summary>Nombre del estadio.</summary>
        public string Name = string.Empty;
        /// <summary>Capacidad definitiva (las tribunas en obra no suman hasta terminar).</summary>
        public int Capacity;
        /// <summary>Plazas de estacionamiento.</summary>
        public int ParkingSpaces;
        /// <summary>Nivel de estacionamiento y accesos hecho (0–3).</summary>
        public int ParkingLevel;
        /// <summary>Nivel de equipamiento hecho (0–3).</summary>
        public int EquipmentLevel;
        /// <summary>Nivel de servicios hecho (0–3).</summary>
        public int ServicesLevel;
        /// <summary>Estado del campo.</summary>
        public PitchCondition Pitch = PitchCondition.Regular;
        /// <summary>Obras en curso (una por categoría como máximo).</summary>
        public readonly List<RemodelWork> Works = new List<RemodelWork>();

        /// <summary>Obra en curso de una categoría, o null.</summary>
        public RemodelWork ActiveWork(RemodelCategory category) => Works.Find(w => w.Category == category);

        /// <summary>Nivel hecho de una categoría (las tribunas no tienen nivel: se pueden ampliar varias veces).</summary>
        public int LevelOf(RemodelCategory category) =>
            category == RemodelCategory.Parking ? ParkingLevel : category == RemodelCategory.Equipment ? EquipmentLevel
            : category == RemodelCategory.Services ? ServicesLevel : 0;
    }

    /// <summary>Parámetros de las obras (en <c>career.json</c>). Índices: [categoría × 3 + nivel − 1].</summary>
    [Serializable]
    public sealed class StadiumConfig
    {
        /// <summary>Costo de cada obra (tribunas chica/media/grande, parking 1/2/3, equipamiento 1/2/3, servicios 1/2/3).</summary>
        public long[] Cost =
        {
            4_000_000_000, 10_000_000_000, 18_000_000_000,
            800_000_000, 2_000_000_000, 4_500_000_000,
            500_000_000, 1_500_000_000, 3_500_000_000,
            700_000_000, 2_500_000_000, 6_000_000_000
        };
        /// <summary>Semanas mínimas de obra.</summary>
        public int[] WeeksMin = { 16, 32, 48, 10, 20, 32, 6, 14, 24, 8, 18, 30 };
        /// <summary>Semanas máximas de obra.</summary>
        public int[] WeeksMax = { 20, 36, 52, 10, 20, 32, 6, 14, 24, 8, 18, 30 };
        /// <summary>Asientos que suma cada ampliación de tribunas.</summary>
        public int[] StandSeats = { 4000, 8000, 12000 };
        /// <summary>Plazas que suma cada nivel de estacionamiento.</summary>
        public int[] ParkingSpaces = { 1000, 2500, 5000 };
        /// <summary>Fracción de la capacidad clausurada mientras hay obra en las tribunas.</summary>
        public float ClosedShareDuringStands = 0.08f;
        /// <summary>Gasto extra por espectador según el nivel de servicios (0–3).</summary>
        public long[] SecondaryPerSpectator = { 0, 1500, 2500, 3500 };
        /// <summary>Gasto extra por espectador según el nivel de estacionamiento (0–3).</summary>
        public long[] ParkingPerSpectator = { 0, 300, 700, 1200 };
        /// <summary>Ingreso fijo por partido de local de los palcos (servicios nivel 3).</summary>
        public long CorporatePerMatch = 120_000_000;
        /// <summary>Ventas mensuales de la tienda oficial (servicios nivel 2 o más), por punto de reputación.</summary>
        public long MerchPerReputation = 4_000_000;
        /// <summary>Multiplicador de los días de lesión según el estado del campo (malo … excelente).</summary>
        public float[] InjuryFactor = { 1.2f, 1.05f, 1f, 0.9f, 0.82f };
    }

    /// <summary>Aviso de obra terminada.</summary>
    public struct StadiumWorkCompleted
    {
        /// <summary>Club.</summary>
        public int ClubId;
        /// <summary>Categoría.</summary>
        public RemodelCategory Category;
        /// <summary>Nivel.</summary>
        public int Tier;
    }

    /// <summary>
    /// Remodelación del estadio: cuatro categorías con tres niveles de magnitud; cuanto más grande la obra, más cuesta y más
    /// semanas tarda. Se paga al contratar, solo puede haber una obra por categoría y cada semana descuenta una semana a
    /// todas las obras; al llegar a cero se aplica la mejora (capacidad, plazas, estado del campo o servicios).
    /// </summary>
    public static class StadiumRemodelingSystem
    {
        /// <summary>Índice de una obra en las tablas de la configuración.</summary>
        public static int Index(RemodelCategory category, int tier) => (int)category * 3 + tier - 1;

        /// <summary>Costo de una obra.</summary>
        public static long CostOf(RemodelCategory category, int tier, StadiumConfig c) => c.Cost[Index(category, tier)];

        /// <summary>Costo total de varias obras seleccionadas.</summary>
        public static long Quote(IEnumerable<(RemodelCategory Category, int Tier)> works, StadiumConfig c)
        {
            long total = 0;
            foreach ((RemodelCategory category, int tier) in works)
            {
                total += CostOf(category, tier, c);
            }

            return total;
        }

        /// <summary>El nivel se puede contratar (las tribunas siempre; el resto, solo niveles todavía no hechos).</summary>
        public static bool IsAvailable(StadiumState s, RemodelCategory category, int tier) =>
            tier >= 1 && tier <= 3 && (category == RemodelCategory.Stands || tier > s.LevelOf(category));

        /// <summary>Contrata una obra: la paga y la deja en curso.</summary>
        /// <param name="s">Estadio.</param>
        /// <param name="finances">Caja del club.</param>
        /// <param name="category">Categoría.</param>
        /// <param name="tier">Nivel o magnitud (1–3).</param>
        /// <param name="date">Fecha (para el libro contable).</param>
        /// <param name="rng">Azar para la duración dentro de su rango.</param>
        /// <param name="c">Parámetros.</param>
        public static RemodelResult Contract(StadiumState s, ClubFinances finances, RemodelCategory category, int tier, DateTime date, IRandom rng,
            StadiumConfig c)
        {
            if (s.ActiveWork(category) != null)
            {
                return RemodelResult.CategoryBusy;
            }

            if (!IsAvailable(s, category, tier))
            {
                return RemodelResult.InvalidTier;
            }

            int k = Index(category, tier);
            long cost = c.Cost[k];
            if (finances.Cash < cost)
            {
                return RemodelResult.NoMoney;
            }

            finances.Record(date, FinanceCategory.Construction, -cost);
            int weeks = rng.NextInt(c.WeeksMin[k], c.WeeksMax[k] + 1);
            s.Works.Add(new RemodelWork { Category = category, Tier = tier, TotalWeeks = weeks, RemainingWeeks = weeks, Cost = cost });
            return RemodelResult.Ok;
        }

        /// <summary>Pasa una semana: descuenta a cada obra y aplica las terminadas (se devuelven en <paramref name="completed"/>).</summary>
        public static void AdvanceWeek(StadiumState s, StadiumConfig c, List<RemodelWork> completed)
        {
            completed.Clear();
            for (int i = s.Works.Count - 1; i >= 0; i--)
            {
                RemodelWork w = s.Works[i];
                if (--w.RemainingWeeks > 0)
                {
                    continue;
                }

                Apply(s, w, c);
                s.Works.RemoveAt(i);
                completed.Add(w);
            }
        }

        private static void Apply(StadiumState s, RemodelWork w, StadiumConfig c)
        {
            switch (w.Category)
            {
                case RemodelCategory.Stands:
                    s.Capacity += c.StandSeats[w.Tier - 1];
                    break;
                case RemodelCategory.Parking:
                    s.ParkingSpaces += c.ParkingSpaces[w.Tier - 1];
                    s.ParkingLevel = Math.Max(s.ParkingLevel, w.Tier);
                    break;
                case RemodelCategory.Equipment:
                    s.EquipmentLevel = Math.Max(s.EquipmentLevel, w.Tier);
                    s.Pitch = (PitchCondition)Math.Max((int)s.Pitch, (int)PitchCondition.Regular + w.Tier);
                    break;
                default:
                    s.ServicesLevel = Math.Max(s.ServicesLevel, w.Tier);
                    break;
            }
        }

        /// <summary>Capacidad habilitada hoy (con el sector en obra clausurado mientras se amplían las tribunas).</summary>
        public static int UsableCapacity(StadiumState s, StadiumConfig c) =>
            s.ActiveWork(RemodelCategory.Stands) != null ? (int)(s.Capacity * (1f - c.ClosedShareDuringStands)) : s.Capacity;

        /// <summary>Ingreso complementario de un partido de local (gastronomía, tienda y estacionamiento por espectador, más palcos).</summary>
        public static long MatchdayExtras(StadiumState s, int spectators, StadiumConfig c) =>
            spectators * (c.SecondaryPerSpectator[s.ServicesLevel] + c.ParkingPerSpectator[s.ParkingLevel])
            + (s.ServicesLevel >= 3 ? c.CorporatePerMatch : 0);
    }
}
