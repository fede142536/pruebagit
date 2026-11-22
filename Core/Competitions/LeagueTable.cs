using System;
using System.Collections.Generic;
using System.Linq;
using Football.Core.Domain;

namespace Football.Core.Competitions
{
    /// <summary>Criterio de desempate (se aplican en el orden configurado).</summary>
    public enum Tiebreaker
    {
        /// <summary>Puntos.</summary>
        Points,
        /// <summary>Diferencia de gol.</summary>
        GoalDifference,
        /// <summary>Goles a favor.</summary>
        GoalsFor,
        /// <summary>Puntos en los partidos entre los empatados.</summary>
        HeadToHeadPoints,
        /// <summary>Diferencia de gol entre los empatados.</summary>
        HeadToHeadGoalDifference,
        /// <summary>Partidos ganados.</summary>
        Wins,
        /// <summary>Goles de visitante.</summary>
        AwayGoals,
        /// <summary>Juego limpio: menos tarjetas.</summary>
        FairPlay
    }

    /// <summary>Fila de una tabla de posiciones.</summary>
    public sealed class Standing
    {
        /// <summary>Club.</summary>
        public int ClubId;
        /// <summary>Partidos jugados.</summary>
        public int Played;
        /// <summary>Ganados.</summary>
        public int Won;
        /// <summary>Empatados.</summary>
        public int Drawn;
        /// <summary>Perdidos.</summary>
        public int Lost;
        /// <summary>Goles a favor.</summary>
        public int GoalsFor;
        /// <summary>Goles en contra.</summary>
        public int GoalsAgainst;
        /// <summary>Goles de visitante.</summary>
        public int AwayGoals;
        /// <summary>Tarjetas (amarilla 1, roja 3) para el juego limpio.</summary>
        public int CardPoints;
        /// <summary>Puntos.</summary>
        public int Points => Won * 3 + Drawn;
        /// <summary>Diferencia de gol.</summary>
        public int GoalDifference => GoalsFor - GoalsAgainst;
    }

    /// <summary>
    /// Tabla de posiciones con desempates configurables (por ejemplo, la de la Liga Profesional: puntos, diferencia de gol,
    /// goles a favor, resultado entre ellos). Se calcula desde los partidos: no guarda nada que pueda desincronizarse.
    /// </summary>
    public sealed class LeagueTable
    {
        private readonly Dictionary<int, Standing> _rows = new Dictionary<int, Standing>();
        private readonly List<Fixture> _played = new List<Fixture>();
        private readonly Tiebreaker[] _order;

        /// <summary>Desempates por defecto del fútbol argentino.</summary>
        public static readonly Tiebreaker[] ArgentineOrder =
        {
            Tiebreaker.Points, Tiebreaker.GoalDifference, Tiebreaker.GoalsFor, Tiebreaker.HeadToHeadPoints,
            Tiebreaker.HeadToHeadGoalDifference, Tiebreaker.FairPlay
        };

        /// <summary>Crea una tabla vacía.</summary>
        /// <param name="clubIds">Equipos.</param>
        /// <param name="order">Desempates en orden (null = <see cref="ArgentineOrder"/>).</param>
        public LeagueTable(IEnumerable<int> clubIds, Tiebreaker[] order = null)
        {
            foreach (int id in clubIds)
            {
                _rows[id] = new Standing { ClubId = id };
            }

            _order = order ?? ArgentineOrder;
        }

        /// <summary>Suma un partido jugado.</summary>
        /// <param name="fixture">Partido (con resultado).</param>
        /// <param name="homeCards">Tarjetas del local (amarilla 1, roja 3).</param>
        /// <param name="awayCards">Tarjetas del visitante.</param>
        public void Add(Fixture fixture, int homeCards = 0, int awayCards = 0)
        {
            if (!fixture.IsPlayed || !_rows.TryGetValue(fixture.HomeClubId, out Standing home) || !_rows.TryGetValue(fixture.AwayClubId, out Standing away))
            {
                return;
            }

            int h = fixture.HomeGoals.Value, a = fixture.AwayGoals.Value;
            Apply(home, h, a, 0, homeCards);
            Apply(away, a, h, a, awayCards);
            _played.Add(fixture);
        }

        private static void Apply(Standing s, int scored, int conceded, int awayGoals, int cards)
        {
            s.Played++;
            s.GoalsFor += scored;
            s.GoalsAgainst += conceded;
            s.AwayGoals += awayGoals;
            s.CardPoints += cards;
            if (scored > conceded) s.Won++;
            else if (scored == conceded) s.Drawn++;
            else s.Lost++;
        }

        /// <summary>Fila de un club.</summary>
        public Standing Row(int clubId) => _rows[clubId];

        /// <summary>Posiciones ordenadas según los desempates.</summary>
        public List<Standing> Ranked()
        {
            var list = _rows.Values.OrderBy(r => r.ClubId).ToList();
            list.Sort((x, y) => Compare(x, y, list));
            return list;
        }

        private int Compare(Standing x, Standing y, List<Standing> all)
        {
            foreach (Tiebreaker t in _order)
            {
                int c;
                switch (t)
                {
                    case Tiebreaker.Points: c = y.Points.CompareTo(x.Points); break;
                    case Tiebreaker.GoalDifference: c = y.GoalDifference.CompareTo(x.GoalDifference); break;
                    case Tiebreaker.GoalsFor: c = y.GoalsFor.CompareTo(x.GoalsFor); break;
                    case Tiebreaker.Wins: c = y.Won.CompareTo(x.Won); break;
                    case Tiebreaker.AwayGoals: c = y.AwayGoals.CompareTo(x.AwayGoals); break;
                    case Tiebreaker.FairPlay: c = x.CardPoints.CompareTo(y.CardPoints); break;
                    case Tiebreaker.HeadToHeadPoints:
                    case Tiebreaker.HeadToHeadGoalDifference:
                    {
                        HeadToHead(x.ClubId, y.ClubId, out int px, out int py, out int dx, out int dy);
                        c = t == Tiebreaker.HeadToHeadPoints ? py.CompareTo(px) : dy.CompareTo(dx);
                        break;
                    }

                    default: c = 0; break;
                }

                if (c != 0)
                {
                    return c;
                }
            }

            return x.ClubId.CompareTo(y.ClubId); // orden estable y determinista si todo empata
        }

        private void HeadToHead(int a, int b, out int pointsA, out int pointsB, out int diffA, out int diffB)
        {
            pointsA = pointsB = diffA = diffB = 0;
            foreach (Fixture f in _played)
            {
                bool ab = f.HomeClubId == a && f.AwayClubId == b, ba = f.HomeClubId == b && f.AwayClubId == a;
                if (!ab && !ba)
                {
                    continue;
                }

                int ga = ab ? f.HomeGoals.Value : f.AwayGoals.Value;
                int gb = ab ? f.AwayGoals.Value : f.HomeGoals.Value;
                diffA += ga - gb;
                diffB += gb - ga;
                pointsA += ga > gb ? 3 : ga == gb ? 1 : 0;
                pointsB += gb > ga ? 3 : ga == gb ? 1 : 0;
            }
        }
    }
}
