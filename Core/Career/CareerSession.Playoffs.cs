using System;
using System.Collections.Generic;
using System.Linq;
using Football.Core.Common;
using Football.Core.Competitions;
using Football.Core.Domain;

namespace Football.Core.Career
{
    // Fases finales del Apertura y del Clausura (los mejores de cada zona, a partido único, mejor ubicado de local) y Trofeo de Campeones.
    public sealed partial class CareerSession
    {
        private static TournamentKind PlayoffKindOf(TournamentKind regular) => regular == TournamentKind.Apertura ? TournamentKind.AperturaPlayoff : TournamentKind.ClausuraPlayoff;

        /// <summary>Si el Apertura o el Clausura tienen fase final: con zonas y fuera del modo Torneo.</summary>
        /// <param name="regular">Torneo (Apertura o Clausura).</param>
        public bool HasPlayoffs(CompetitionInfo regular) =>
            !Data.TournamentOnly && (regular.Kind == TournamentKind.Apertura || regular.Kind == TournamentKind.Clausura)
            && Data.Fixtures.Any(f => f.CompetitionId == regular.Id && f.Group > 0);

        /// <summary>Final de la fase final de un torneo (null si todavía no se armó).</summary>
        /// <param name="regular">Apertura o Clausura.</param>
        public Fixture PlayoffFinal(CompetitionInfo regular)
        {
            List<Fixture> po = Data.Fixtures.Where(f => f.CompetitionId == CompetitionInfo.IdOf(regular.Season, PlayoffKindOf(regular.Kind))).ToList();
            if (po.Count == 0)
            {
                return null;
            }

            int last = po.Max(f => f.Round);
            List<Fixture> round = po.Where(f => f.Round == last).ToList();
            return round.Count == 1 && last > 1 ? round[0] : null;
        }

        private void AdvancePlayoffs()
        {
            if (Data.TournamentOnly)
            {
                return;
            }

            int season = Data.Season;
            foreach (TournamentKind regular in new[] { TournamentKind.Apertura, TournamentKind.Clausura })
            {
                int regularId = CompetitionInfo.IdOf(season, regular), playoffId = CompetitionInfo.IdOf(season, PlayoffKindOf(regular));
                List<Fixture> phase = Data.Fixtures.Where(f => f.CompetitionId == regularId).ToList();
                if (phase.Count == 0 || phase.All(f => f.Group == 0))
                {
                    continue; // sin zonas no hay fase final
                }

                if (Data.Competitions.All(c => c.Id != playoffId))
                {
                    if (phase.All(f => f.IsPlayed))
                    {
                        StartPlayoffs(regular, regularId, playoffId, phase);
                    }
                }
                else
                {
                    AdvancePlayoffRound(regularId, playoffId);
                }
            }

            StartTrofeo(season);
        }

        /// <summary>Orden de siembra de una llave de <paramref name="size"/> puestos: 1-8, 4-5, 2-7, 3-6 para ocho.</summary>
        private static List<int> BracketOrder(int size)
        {
            var order = new List<int> { 1 };
            while (order.Count < size)
            {
                int total = order.Count * 2 + 1;
                order = order.SelectMany(s => new[] { s, total - s }).ToList();
            }

            return order;
        }

        private void StartPlayoffs(TournamentKind regular, int regularId, int playoffId, List<Fixture> phase)
        {
            List<int> zoneA = phase.Where(f => f.Group == 1).SelectMany(f => new[] { f.HomeClubId, f.AwayClubId }).Distinct().ToList();
            List<int> zoneB = phase.Where(f => f.Group == 2).SelectMany(f => new[] { f.HomeClubId, f.AwayClubId }).Distinct().ToList();
            int perZone = 1;
            while (perZone * 2 <= Math.Min(_cfg.Career.PlayoffTeamsPerZone, Math.Min(zoneA.Count, zoneB.Count)))
            {
                perZone *= 2;
            }

            List<Standing> all = Table(f => f.CompetitionId == regularId);
            List<int> a = all.Where(s => zoneA.Contains(s.ClubId)).Take(perZone).Select(s => s.ClubId).ToList();
            List<int> b = all.Where(s => zoneB.Contains(s.ClubId)).Take(perZone).Select(s => s.ClubId).ToList();
            string name = Data.Competitions.Find(c => c.Id == regularId).Name;
            Data.Competitions.Add(new CompetitionInfo { Id = playoffId, Name = name + " · Fase final", Season = Data.Season, Kind = PlayoffKindOf(regular) });
            if (perZone < 2)
            {
                return;
            }

            int nextId = Data.Fixtures.Max(f => f.Id) + 1;
            DateTime date = phase.Max(f => f.Date).AddDays(_cfg.Career.PlayoffFirstDelayDays);
            List<int> order = BracketOrder(perZone);
            int slot = 1;
            foreach ((List<int> own, List<int> other) in new[] { (a, b), (b, a) })
            {
                for (int i = 0; i < perZone; i += 2)
                {
                    Data.Fixtures.Add(new Fixture
                    {
                        Id = nextId++, CompetitionId = playoffId, Round = 1, Group = slot++, HomeClubId = own[order[i] - 1], AwayClubId = other[order[i + 1] - 1], Date = date
                    });
                }
            }

            foreach (int club in a.Concat(b))
            {
                if (club == Data.UserClubId)
                {
                    _inbox.Post("news.playoff_qualified", name);
                }
            }

            if (!a.Concat(b).Contains(Data.UserClubId) && phase.Any(f => f.HomeClubId == Data.UserClubId || f.AwayClubId == Data.UserClubId))
            {
                _inbox.Post("news.playoff_missed", name);
            }
        }

        private void AdvancePlayoffRound(int regularId, int playoffId)
        {
            List<Fixture> po = Data.Fixtures.Where(f => f.CompetitionId == playoffId).ToList();
            if (po.Count == 0)
            {
                return;
            }

            int round = po.Max(f => f.Round);
            List<Fixture> last = po.Where(f => f.Round == round).OrderBy(f => f.Group).ToList();
            if (last.Count == 1 || !last.All(f => f.IsPlayed))
            {
                return;
            }

            List<int> winners = last.Select(f => Knockout.Winner(f, new SeededRandom(Mix(f.Id * 31L)))).ToList();
            List<int> seedOrder = Table(f => f.CompetitionId == regularId).Select(s => s.ClubId).ToList(); // mejor ubicado en la fase regular, de local
            int nextId = Data.Fixtures.Max(f => f.Id) + 1;
            DateTime date = last.Max(f => f.Date).AddDays(_cfg.Career.PlayoffRoundSpacingDays);
            for (int i = 0; i + 1 < winners.Count; i += 2)
            {
                bool firstBetter = seedOrder.IndexOf(winners[i]) <= seedOrder.IndexOf(winners[i + 1]);
                Data.Fixtures.Add(new Fixture
                {
                    Id = nextId++, CompetitionId = playoffId, Round = round + 1, Group = i / 2 + 1, Date = date,
                    HomeClubId = firstBetter ? winners[i] : winners[i + 1], AwayClubId = firstBetter ? winners[i + 1] : winners[i]
                });
            }
        }

        /// <summary>Cuando terminaron las dos fases finales, el campeón del Apertura juega contra el del Clausura (si es el mismo club, no hay Trofeo: ya ganó los dos).</summary>
        private void StartTrofeo(int season)
        {
            int id = CompetitionInfo.IdOf(season, TournamentKind.TrofeoCampeones);
            CompetitionInfo apertura = Data.Competitions.Find(c => c.Id == CompetitionInfo.IdOf(season, TournamentKind.Apertura));
            CompetitionInfo clausura = Data.Competitions.Find(c => c.Id == CompetitionInfo.IdOf(season, TournamentKind.Clausura));
            if (Data.Competitions.Any(c => c.Id == id) || apertura == null || clausura == null || !HasPlayoffs(apertura) || !HasPlayoffs(clausura))
            {
                return;
            }

            Fixture finalA = PlayoffFinal(apertura), finalC = PlayoffFinal(clausura);
            if (finalA == null || finalC == null || !finalA.IsPlayed || !finalC.IsPlayed)
            {
                return;
            }

            int championA = Knockout.Winner(finalA, new SeededRandom(Mix(finalA.Id * 31L))), championC = Knockout.Winner(finalC, new SeededRandom(Mix(finalC.Id * 31L)));
            if (championA == championC)
            {
                return;
            }

            Data.Competitions.Add(new CompetitionInfo { Id = id, Name = "Trofeo de Campeones " + season, Season = season, Kind = TournamentKind.TrofeoCampeones });
            Data.Fixtures.Add(new Fixture
            {
                Id = Data.Fixtures.Max(f => f.Id) + 1, CompetitionId = id, Round = 1, HomeClubId = championA, AwayClubId = championC,
                Date = finalC.Date.AddDays(_cfg.Career.TrofeoDelayDays)
            });
        }
    }
}
