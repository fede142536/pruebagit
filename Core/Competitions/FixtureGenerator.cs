using System;
using System.Collections.Generic;
using System.Linq;
using Football.Core.Common;
using Football.Core.Domain;

namespace Football.Core.Competitions
{
    /// <summary>Calendarios sin conflictos: todos contra todos (método del círculo), a una o dos ruedas.</summary>
    public static class FixtureGenerator
    {
        /// <summary>
        /// Torneo en dos zonas, como el fútbol argentino: los equipos (en orden de cabeza de serie) se reparten en serpentina,
        /// cada zona juega todos contra todos una vez y, en cada fecha, los que quedan libres de cada zona se enfrentan entre sí
        /// (cruce interzonal, <c>Group</c> 0). Con zonas impares (15 y 15) cada club juega una fecha por semana durante
        /// <c>zona</c> fechas: 14 partidos de zona y 1 interzonal. Con zonas pares se agrega una fecha final de cruces
        /// interzonales entre los equipos de igual puesto de serie. Las zonas son 1 y 2.
        /// </summary>
        /// <param name="competitionId">Competición.</param>
        /// <param name="seededClubs">Equipos de mejor a peor cabeza de serie.</param>
        /// <param name="firstDate">Fecha de la primera jornada.</param>
        /// <param name="daysBetweenRounds">Días entre jornadas.</param>
        /// <param name="rng">Azar del sorteo dentro de cada zona (null = en el orden recibido).</param>
        public static List<Fixture> TwoZones(int competitionId, IReadOnlyList<int> seededClubs, DateTime firstDate, int daysBetweenRounds, IRandom rng = null)
        {
            if (seededClubs == null || seededClubs.Count < 4)
            {
                throw new ArgumentException("Hacen falta al menos cuatro equipos para dos zonas.", nameof(seededClubs));
            }

            List<List<int>> zones = Groups.Snake(seededClubs, 2);
            var fixtures = new List<Fixture>();
            for (int z = 0; z < 2; z++)
            {
                fixtures.AddRange(RoundRobin(competitionId, zones[z], firstDate, daysBetweenRounds, false, rng?.Fork("zona" + z), z + 1));
            }

            int rounds = fixtures.Max(f => f.Round);
            for (int round = 1; round <= rounds; round++)
            {
                List<int> restA = Resting(fixtures, zones[0], round), restB = Resting(fixtures, zones[1], round);
                for (int k = 0; k < Math.Min(restA.Count, restB.Count); k++)
                {
                    AddInterzonal(fixtures, competitionId, restA[k], restB[k], round, firstDate.AddDays((round - 1) * daysBetweenRounds), round);
                }
            }

            if (zones[0].Count % 2 == 0)
            {
                // Zonas pares: nadie queda libre, así que una fecha más cruza a los de igual cabeza de serie.
                for (int k = 0; k < Math.Min(zones[0].Count, zones[1].Count); k++)
                {
                    AddInterzonal(fixtures, competitionId, zones[0][k], zones[1][k], rounds + 1, firstDate.AddDays(rounds * daysBetweenRounds), k);
                }
            }

            fixtures.Sort((a, b) => a.Round != b.Round ? a.Round.CompareTo(b.Round) : a.Group != b.Group ? a.Group.CompareTo(b.Group) : a.HomeClubId.CompareTo(b.HomeClubId));
            return fixtures;
        }

        private static List<int> Resting(List<Fixture> fixtures, List<int> zone, int round) =>
            zone.Where(c => !fixtures.Any(f => f.Round == round && (f.HomeClubId == c || f.AwayClubId == c))).ToList();

        private static void AddInterzonal(List<Fixture> fixtures, int competitionId, int a, int b, int round, DateTime date, int parity)
        {
            bool aHome = parity % 2 == 0;
            fixtures.Add(new Fixture { CompetitionId = competitionId, Round = round, Group = 0, HomeClubId = aHome ? a : b, AwayClubId = aHome ? b : a, Date = date });
        }

        /// <summary>
        /// Todos contra todos. Con número impar de equipos, cada fecha uno queda libre. En cada fecha nadie juega dos veces;
        /// la segunda rueda repite la primera con las localías invertidas; las localías se alternan.
        /// </summary>
        /// <param name="competitionId">Competición.</param>
        /// <param name="clubIds">Equipos.</param>
        /// <param name="firstDate">Fecha de la primera jornada.</param>
        /// <param name="daysBetweenRounds">Días entre jornadas.</param>
        /// <param name="doubleRound">Ida y vuelta.</param>
        /// <param name="rng">Azar (orden de los equipos), o null para el orden recibido.</param>
        /// <param name="group">Zona (0 si no hay).</param>
        public static List<Fixture> RoundRobin(int competitionId, IReadOnlyList<int> clubIds, DateTime firstDate, int daysBetweenRounds,
            bool doubleRound, IRandom rng = null, int group = 0)
        {
            if (clubIds == null || clubIds.Count < 2)
            {
                throw new ArgumentException("Hacen falta al menos dos equipos.", nameof(clubIds));
            }

            var teams = new List<int>(clubIds);
            if (rng != null)
            {
                for (int i = teams.Count - 1; i > 0; i--)
                {
                    int j = rng.NextInt(0, i + 1);
                    (teams[i], teams[j]) = (teams[j], teams[i]);
                }
            }

            const int Bye = -1;
            if (teams.Count % 2 == 1)
            {
                teams.Add(Bye);
            }

            int n = teams.Count;
            int rounds = n - 1;
            var fixtures = new List<Fixture>();
            var homeGames = new Dictionary<int, int>();
            var lastHome = new Dictionary<int, bool>();
            foreach (int t in teams)
            {
                homeGames[t] = 0;
                lastHome[t] = false;
            }

            for (int round = 0; round < rounds; round++)
            {
                for (int i = 0; i < n / 2; i++)
                {
                    int a = teams[i], b = teams[n - 1 - i];
                    if (a == Bye || b == Bye)
                    {
                        continue;
                    }

                    // Localía equilibrada: juega de local quien lleva menos partidos de local; si van iguales, quien jugó
                    // de visitante la fecha anterior (así nadie encadena muchas localías seguidas).
                    bool aHome = homeGames[a] != homeGames[b] ? homeGames[a] < homeGames[b] : !lastHome[a];
                    int host = aHome ? a : b, guest = aHome ? b : a;
                    homeGames[host]++;
                    lastHome[host] = true;
                    lastHome[guest] = false;
                    fixtures.Add(new Fixture
                    {
                        CompetitionId = competitionId, Round = round + 1, Group = group,
                        HomeClubId = aHome ? a : b, AwayClubId = aHome ? b : a, Date = firstDate.AddDays(round * daysBetweenRounds)
                    });
                }

                // Rotación: el primero queda fijo, el resto gira.
                int last = teams[n - 1];
                teams.RemoveAt(n - 1);
                teams.Insert(1, last);
            }

            if (doubleRound)
            {
                int count = fixtures.Count;
                for (int k = 0; k < count; k++)
                {
                    Fixture f = fixtures[k];
                    fixtures.Add(new Fixture
                    {
                        CompetitionId = competitionId, Round = f.Round + rounds, Group = group, HomeClubId = f.AwayClubId,
                        AwayClubId = f.HomeClubId, Date = firstDate.AddDays((f.Round - 1 + rounds) * daysBetweenRounds)
                    });
                }
            }

            return fixtures;
        }
    }
}
