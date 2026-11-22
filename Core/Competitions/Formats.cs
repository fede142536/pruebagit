using System;
using System.Collections.Generic;
using System.Linq;
using Football.Core.Common;
using Football.Core.Domain;

namespace Football.Core.Competitions
{
    /// <summary>
    /// Tabla de promedios del fútbol argentino: puntos divididos por partidos jugados en las últimas temporadas en las que
    /// el club estuvo en la categoría (hasta tres). Los de peor promedio descienden.
    /// </summary>
    public sealed class AveragesTable
    {
        private readonly Dictionary<int, SortedDictionary<int, (int points, int played)>> _seasons =
            new Dictionary<int, SortedDictionary<int, (int points, int played)>>();

        /// <summary>Cantidad de temporadas que cuentan.</summary>
        public int SeasonsCounted { get; }

        /// <summary>Crea la tabla.</summary>
        /// <param name="seasonsCounted">Temporadas que cuentan (3 en Argentina).</param>
        public AveragesTable(int seasonsCounted = 3)
        {
            SeasonsCounted = seasonsCounted;
        }

        /// <summary>Anota (o reemplaza) los puntos y partidos de un club en una temporada.</summary>
        public void Record(int clubId, int season, int points, int played)
        {
            if (!_seasons.TryGetValue(clubId, out var bySeason))
            {
                bySeason = new SortedDictionary<int, (int, int)>();
                _seasons[clubId] = bySeason;
            }

            bySeason[season] = (points, played);
        }

        /// <summary>Promedio de un club hasta la temporada indicada (incluida).</summary>
        public float Average(int clubId, int upToSeason)
        {
            if (!_seasons.TryGetValue(clubId, out var bySeason))
            {
                return 0f;
            }

            int points = 0, played = 0;
            foreach (var season in bySeason.Where(s => s.Key <= upToSeason).OrderByDescending(s => s.Key).Take(SeasonsCounted))
            {
                points += season.Value.points;
                played += season.Value.played;
            }

            return played > 0 ? (float)points / played : 0f;
        }

        /// <summary>Clubes de peor a mejor promedio (los primeros son los que descienden).</summary>
        public List<int> Worst(IEnumerable<int> clubIds, int upToSeason) =>
            clubIds.OrderBy(id => Average(id, upToSeason)).ThenBy(id => id).ToList();
    }

    /// <summary>Partidos de eliminación directa: ganador (con penales si hay empate) y cruces.</summary>
    public static class Knockout
    {
        private const int PenaltyRounds = 5;

        /// <summary>
        /// Ganador de un partido a eliminación directa. Si empataron, define por penales (con el azar recibido) y deja el
        /// resultado de la tanda en el partido.
        /// </summary>
        /// <param name="fixture">Partido jugado.</param>
        /// <param name="rng">Azar para los penales.</param>
        /// <param name="penaltyScoreChance">Probabilidad de convertir cada penal.</param>
        public static int Winner(Fixture fixture, IRandom rng, float penaltyScoreChance = 0.75f)
        {
            if (!fixture.IsPlayed)
            {
                throw new InvalidOperationException("El partido no se jugó.");
            }

            if (fixture.HomeGoals != fixture.AwayGoals)
            {
                return fixture.HomeGoals > fixture.AwayGoals ? fixture.HomeClubId : fixture.AwayClubId;
            }

            int home = 0, away = 0, kicks = 0;
            while (kicks < PenaltyRounds || home == away)
            {
                home += rng.Chance(penaltyScoreChance) ? 1 : 0;
                away += rng.Chance(penaltyScoreChance) ? 1 : 0;
                kicks++;
                if (kicks < PenaltyRounds && Math.Abs(home - away) > PenaltyRounds - kicks)
                {
                    break; // ya no se puede alcanzar
                }
            }

            fixture.HomePenalties = home;
            fixture.AwayPenalties = away;
            return home > away ? fixture.HomeClubId : fixture.AwayClubId;
        }

        /// <summary>Cruces de la siguiente ronda: 1 contra 2, 3 contra 4… en el orden recibido.</summary>
        public static List<Fixture> Pair(int competitionId, IReadOnlyList<int> clubIds, int round, DateTime date)
        {
            if (clubIds.Count % 2 != 0)
            {
                throw new ArgumentException("Hace falta una cantidad par de equipos.", nameof(clubIds));
            }

            var fixtures = new List<Fixture>();
            for (int i = 0; i < clubIds.Count; i += 2)
            {
                fixtures.Add(new Fixture { CompetitionId = competitionId, Round = round, HomeClubId = clubIds[i], AwayClubId = clubIds[i + 1], Date = date });
            }

            return fixtures;
        }

        /// <summary>
        /// Cruces de playoffs entre dos zonas: 1.º de A contra el último clasificado de B, 2.º de A contra el anteúltimo de B,
        /// y así (como en la Copa de la Liga). Juega de local el mejor ubicado.
        /// </summary>
        public static List<int> CrossZones(IReadOnlyList<int> zoneA, IReadOnlyList<int> zoneB, int qualifiersPerZone)
        {
            var order = new List<int>();
            for (int i = 0; i < qualifiersPerZone; i++)
            {
                int rank = qualifiersPerZone - 1 - i; // posición del rival en la otra zona
                bool aHome = i <= rank;               // juega de local el mejor ubicado
                order.Add(aHome ? zoneA[i] : zoneB[rank]);
                order.Add(aHome ? zoneB[rank] : zoneA[i]);
            }

            return order;
        }
    }

    /// <summary>Reparto en zonas o grupos en serpentina (el orden recibido es el de los cabezas de serie).</summary>
    public static class Groups
    {
        /// <summary>Reparte los equipos en grupos: 1.º al A, 2.º al B…, y vuelta en sentido contrario.</summary>
        public static List<List<int>> Snake(IReadOnlyList<int> seeded, int groupCount)
        {
            var groups = Enumerable.Range(0, groupCount).Select(_ => new List<int>()).ToList();
            for (int i = 0; i < seeded.Count; i++)
            {
                int lap = i / groupCount, pos = i % groupCount;
                groups[lap % 2 == 0 ? pos : groupCount - 1 - pos].Add(seeded[i]);
            }

            return groups;
        }
    }
}
