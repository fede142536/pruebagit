using System;
using System.Collections.Generic;
using Football.Core.Common;
using Football.Core.Domain;

namespace Football.Core.Competitions
{
    /// <summary>Calendarios sin conflictos: todos contra todos (método del círculo), a una o dos ruedas.</summary>
    public static class FixtureGenerator
    {
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
