using System;
using System.Collections.Generic;
using Football.Core.Common;
using Football.Core.Domain;

namespace Football.Core.Management
{
    /// <summary>Jerarquía de un club (define las expectativas de su directiva).</summary>
    public enum ClubTier
    {
        /// <summary>Club grande (Boca, River, Racing, Independiente, San Lorenzo).</summary>
        Big,
        /// <summary>Club de media tabla.</summary>
        Mid,
        /// <summary>Club en desarrollo.</summary>
        Development
    }

    /// <summary>Categoría de un objetivo de la directiva.</summary>
    public enum ObjectiveCategory
    {
        /// <summary>Posición final en el torneo local.</summary>
        NationalSuccess,
        /// <summary>Clasificar a la copa continental (tabla anual).</summary>
        ContinentalSuccess,
        /// <summary>Minutos para juveniles (sub-21).</summary>
        YouthDevelopment,
        /// <summary>Cuidar la caja.</summary>
        Finances,
        /// <summary>Llenar el estadio (asistencia media).</summary>
        BrandExposure
    }

    /// <summary>Prioridad de un objetivo (1 = crítica).</summary>
    public enum ObjectivePriority
    {
        /// <summary>1 · Crítica.</summary>
        Critical = 1,
        /// <summary>2 · Alta.</summary>
        High = 2,
        /// <summary>3 · Media.</summary>
        Medium = 3,
        /// <summary>4 · Baja.</summary>
        Low = 4
    }

    /// <summary>Un objetivo de la directiva para la temporada.</summary>
    public sealed class BoardObjective
    {
        /// <summary>Categoría.</summary>
        public ObjectiveCategory Category;
        /// <summary>Prioridad.</summary>
        public ObjectivePriority Priority;
        /// <summary>Meta medible: puesto máximo, 1 = clasificar, fracción de minutos juveniles, caja final / inicial o asistencia media.</summary>
        public float Target;
    }

    /// <summary>Datos de la temporada con los que se mide el avance de los objetivos.</summary>
    public struct BoardSnapshot
    {
        /// <summary>Puesto actual en la tabla anual (1 = primero).</summary>
        public int Position;
        /// <summary>Clubes en la tabla.</summary>
        public int Clubs;
        /// <summary>Puestos que clasifican a la copa continental.</summary>
        public int QualifyingPlaces;
        /// <summary>Fracción de los minutos jugados por sub-21.</summary>
        public float YouthMinutesShare;
        /// <summary>Caja actual dividida por la caja al empezar la temporada.</summary>
        public float CashRatio;
        /// <summary>Ocupación media del estadio en los partidos de local (0–1; negativa si todavía no hubo).</summary>
        public float AverageAttendance;
    }

    /// <summary>Parámetros de la directiva y de la popularidad del DT (en <c>career.json</c>).</summary>
    [Serializable]
    public sealed class BoardConfig
    {
        /// <summary>Puesto máximo exigido por jerarquía (grande, media, desarrollo).</summary>
        public int[] NationalTarget = { 3, 8, 12 };
        /// <summary>Fracción de minutos para sub-21 por jerarquía.</summary>
        public float[] YouthTarget = { 0.05f, 0.1f, 0.15f };
        /// <summary>Caja final / inicial exigida por jerarquía.</summary>
        public float[] FinanceTarget = { 0.75f, 0.9f, 1.0f };
        /// <summary>Ocupación media del estadio exigida por jerarquía.</summary>
        public float[] AttendanceTarget = { 0.85f, 0.72f, 0.6f };
        /// <summary>Edad máxima de un juvenil.</summary>
        public int YouthAge = 21;
        /// <summary>Puntos de popularidad que mueve cada prioridad al cerrar la temporada (crítica, alta, media, baja).</summary>
        public float[] PriorityWeight = { 15f, 10f, 5f, 2f };
        /// <summary>Popularidad por victoria, empate y derrota.</summary>
        public float[] ResultPopularity = { 1f, 0f, -1.5f };
        /// <summary>Popularidad extra por ganar o perder un clásico.</summary>
        public float ClassicWin = 3f, ClassicLoss = 4f;
        /// <summary>Popularidad mensual por salud financiera: por cada 10 % de caja sobre o bajo la inicial.</summary>
        public float FinancePerTenPercent = 0.5f;
        /// <summary>Por debajo de esta popularidad la directiva despide al DT.</summary>
        public float FireThreshold = 25f;
    }

    /// <summary>
    /// Expectativas de la directiva: genera los 5 objetivos de la temporada según la jerarquía del club y mide su avance.
    /// También mueve la popularidad del DT (resultados, clásicos, finanzas y objetivos).
    /// </summary>
    public static class BoardExpectations
    {
        private static readonly ObjectivePriority[][] PriorityByTier =
        {
            // Grande: ganar es crítico; la cantera, baja.
            new[] { ObjectivePriority.Critical, ObjectivePriority.High, ObjectivePriority.Low, ObjectivePriority.Medium, ObjectivePriority.Medium },
            // Media tabla: pelear arriba y cuidar la plata.
            new[] { ObjectivePriority.High, ObjectivePriority.Medium, ObjectivePriority.Medium, ObjectivePriority.High, ObjectivePriority.Medium },
            // En desarrollo: la caja es crítica y la cantera, alta.
            new[] { ObjectivePriority.High, ObjectivePriority.Low, ObjectivePriority.High, ObjectivePriority.Critical, ObjectivePriority.Low }
        };

        /// <summary>Jerarquía a partir del texto del archivo de equipos.</summary>
        public static ClubTier ParseTier(string tier) =>
            Enum.TryParse(tier, true, out ClubTier t) ? t : ClubTier.Mid;

        /// <summary>Los 5 objetivos de la temporada, ordenados por prioridad.</summary>
        public static List<BoardObjective> Generate(ClubTier tier, int clubs, BoardConfig c)
        {
            int k = (int)tier;
            ObjectivePriority[] p = PriorityByTier[k];
            var list = new List<BoardObjective>
            {
                new BoardObjective { Category = ObjectiveCategory.NationalSuccess, Priority = p[0], Target = Math.Min(c.NationalTarget[k], clubs) },
                new BoardObjective { Category = ObjectiveCategory.ContinentalSuccess, Priority = p[1], Target = 1f },
                new BoardObjective { Category = ObjectiveCategory.YouthDevelopment, Priority = p[2], Target = c.YouthTarget[k] },
                new BoardObjective { Category = ObjectiveCategory.Finances, Priority = p[3], Target = c.FinanceTarget[k] },
                new BoardObjective { Category = ObjectiveCategory.BrandExposure, Priority = p[4], Target = c.AttendanceTarget[k] }
            };
            list.Sort((a, b) => a.Priority != b.Priority ? a.Priority.CompareTo(b.Priority) : a.Category.CompareTo(b.Category));
            return list;
        }

        /// <summary>Avance de un objetivo de 0 a 1 (1 = cumplido).</summary>
        public static float Progress(BoardObjective o, BoardSnapshot s)
        {
            switch (o.Category)
            {
                case ObjectiveCategory.NationalSuccess:
                    if (s.Position <= 0)
                    {
                        return 0f;
                    }

                    return s.Position <= o.Target ? 1f : MathUtil.Clamp01((s.Clubs - s.Position) / Math.Max(1f, s.Clubs - o.Target));
                case ObjectiveCategory.ContinentalSuccess:
                    return s.Position > 0 && s.Position <= s.QualifyingPlaces ? 1f
                        : s.Position <= 0 ? 0f : MathUtil.Clamp01((s.Clubs - s.Position) / Math.Max(1f, s.Clubs - s.QualifyingPlaces));
                case ObjectiveCategory.YouthDevelopment:
                    return MathUtil.Clamp01(s.YouthMinutesShare / Math.Max(0.001f, o.Target));
                case ObjectiveCategory.Finances:
                    return MathUtil.Clamp01(s.CashRatio / Math.Max(0.001f, o.Target));
                default:
                    return s.AverageAttendance < 0f ? 0f : MathUtil.Clamp01(s.AverageAttendance / Math.Max(0.001f, o.Target));
            }
        }

        /// <summary>Popularidad tras un partido (resultado y clásicos).</summary>
        /// <param name="popularity">Popularidad actual.</param>
        /// <param name="goalsFor">Goles a favor.</param>
        /// <param name="goalsAgainst">Goles en contra.</param>
        /// <param name="classic">Fue un clásico.</param>
        /// <param name="c">Parámetros.</param>
        public static float AfterMatch(float popularity, int goalsFor, int goalsAgainst, bool classic, BoardConfig c)
        {
            int result = goalsFor > goalsAgainst ? 0 : goalsFor == goalsAgainst ? 1 : 2;
            float delta = c.ResultPopularity[result];
            if (classic && result != 1)
            {
                delta += result == 0 ? c.ClassicWin : -c.ClassicLoss;
            }

            return MathUtil.Clamp(popularity + delta, 0f, 100f);
        }

        /// <summary>Popularidad tras el cierre de mes (salud financiera).</summary>
        public static float AfterMonth(float popularity, float cashRatio, BoardConfig c) =>
            MathUtil.Clamp(popularity + (cashRatio - 1f) * 10f * c.FinancePerTenPercent, 0f, 100f);

        /// <summary>Popularidad al cerrar la temporada según los objetivos (cumplidos suman la mitad del peso, fallidos restan).</summary>
        public static float AfterSeason(float popularity, IReadOnlyList<BoardObjective> objectives, BoardSnapshot s, BoardConfig c)
        {
            foreach (BoardObjective o in objectives)
            {
                float weight = c.PriorityWeight[(int)o.Priority - 1];
                float progress = Progress(o, s);
                popularity += progress >= 1f ? weight * 0.5f : -weight * (1f - progress);
            }

            return MathUtil.Clamp(popularity, 0f, 100f);
        }

        /// <summary>La directiva despide al DT si la popularidad cae bajo el umbral.</summary>
        public static bool ShouldFire(float popularity, BoardConfig c) => popularity < c.FireThreshold;
    }
}
