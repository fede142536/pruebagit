using System;
using System.Collections.Generic;
using System.Linq;
using Football.Core.Common;
using Football.Core.Competitions;
using Football.Core.Domain;

namespace Football.Core.Career
{
    // Primera y Nacional B: promoción entre divisiones, ascensos y descensos al cerrar la temporada.
    public sealed partial class CareerSession
    {
        /// <summary>Hay dos divisiones (Primera y Nacional B).</summary>
        public bool HasDivisions => Data.PrimeraSize > 0 && !Data.TournamentOnly;

        /// <summary>
        /// Cuando terminan las ligas de las dos divisiones arma la promoción: el tercero del Nacional B (local en la ida) contra el club de Primera que
        /// quedó <see cref="CareerConfig.PlayoffFromBottom"/> puestos antes del final de los promedios (17.º en 20 clubes), ida y vuelta.
        /// </summary>
        private void AdvancePromotion()
        {
            if (!HasDivisions)
            {
                return;
            }

            CareerConfig cc = _cfg.Career;
            int season = Data.Season, id = CompetitionInfo.IdOf(season, TournamentKind.Promotion);
            if (Data.Competitions.Any(c => c.Id == id))
            {
                return;
            }

            List<Fixture> leagues = SeasonFixtures().Where(IsLeague).ToList();
            if (leagues.Count == 0 || !leagues.All(f => f.IsPlayed))
            {
                return;
            }

            List<Standing> second = Table(f => f.CompetitionId == CompetitionInfo.IdOf(season, TournamentKind.NacionalB));
            List<int> primera = PrimeraIds();
            if (second.Count <= cc.PromotedClubs || primera.Count <= cc.PlayoffFromBottom + cc.RelegatedClubs)
            {
                return;
            }

            int challenger = second[cc.PromotedClubs].ClubId;
            List<int> bestToWorst = BuildAverages().Worst(primera, season);
            bestToWorst.Reverse();
            int defender = bestToWorst[primera.Count - cc.PlayoffFromBottom - 1];
            DateTime first = leagues.Max(f => f.Date).AddDays(cc.PlayoffDelayDays);
            int nextId = Data.Fixtures.Max(f => f.Id) + 1;
            Data.Competitions.Add(new CompetitionInfo { Id = id, Name = "Promoción " + season, Season = season, Kind = TournamentKind.Promotion });
            Data.Fixtures.Add(new Fixture { Id = nextId++, CompetitionId = id, Round = 1, HomeClubId = challenger, AwayClubId = defender, Date = first });
            Data.Fixtures.Add(new Fixture
            {
                Id = nextId, CompetitionId = id, Round = 2, HomeClubId = defender, AwayClubId = challenger, Date = first.AddDays(cc.PlayoffLegSpacingDays)
            });
        }

        /// <summary>Club que juega en Primera la temporada siguiente según la promoción de una temporada (0 si no se jugó entera).</summary>
        /// <param name="season">Temporada.</param>
        public int PromotionWinner(int season)
        {
            List<Fixture> legs = Data.Fixtures.Where(f => f.CompetitionId == CompetitionInfo.IdOf(season, TournamentKind.Promotion)).OrderBy(f => f.Round).ToList();
            if (legs.Count != 2 || legs.Any(f => !f.IsPlayed))
            {
                return 0;
            }

            int challenger = legs[0].HomeClubId, defender = legs[0].AwayClubId;
            int challengerGoals = legs[0].HomeGoals.Value + legs[1].AwayGoals.Value, defenderGoals = legs[0].AwayGoals.Value + legs[1].HomeGoals.Value;
            if (challengerGoals != defenderGoals)
            {
                return challengerGoals > defenderGoals ? challenger : defender;
            }

            // Empate global: penales (azar con semilla, ventaja leve al de mejor reputación).
            float chance = 0.5f + Math.Sign(Data.Finances[challenger].Reputation - Data.Finances[defender].Reputation) * 0.05f;
            return new SeededRandom(Mix(legs[1].Id * 17L)).Chance(chance) ? challenger : defender;
        }

        /// <summary>
        /// Cierra la temporada entre divisiones: los dos últimos de los promedios bajan al Nacional B, los dos mejores del Nacional B suben y el ganador
        /// de la promoción juega en Primera. La cantidad de clubes de cada división no cambia.
        /// </summary>
        private void ApplyDivisionChanges()
        {
            CareerConfig cc = _cfg.Career;
            int season = Data.Season;
            List<int> relegated = BuildAverages().Worst(PrimeraIds(), season).Take(cc.RelegatedClubs).ToList();
            List<int> promoted = Table(f => f.CompetitionId == CompetitionInfo.IdOf(season, TournamentKind.NacionalB)).Take(cc.PromotedClubs).Select(s => s.ClubId).ToList();
            int winner = PromotionWinner(season);
            List<Fixture> legs = Data.Fixtures.Where(f => f.CompetitionId == CompetitionInfo.IdOf(season, TournamentKind.Promotion)).OrderBy(f => f.Round).ToList();
            if (winner != 0 && Data.Club(winner).Division == 2)
            {
                promoted.Add(winner);
                int defender = legs[0].AwayClubId;
                relegated.Add(defender); // el de Primera que perdió la promoción baja
            }

            foreach (int clubId in relegated)
            {
                Data.Club(clubId).Division = 2;
                ClubFinances fin = Data.Finances[clubId];
                fin.Reputation = Math.Max(MinReputation, fin.Reputation - ReputationStepForRelegation);
                _inbox.Post(clubId == Data.UserClubId ? "news.user_relegated" : "news.relegated", Data.Club(clubId).Name);
            }

            foreach (int clubId in promoted)
            {
                Data.Club(clubId).Division = 1;
                ClubFinances fin = Data.Finances[clubId];
                fin.Reputation = Math.Min(MaxReputation, fin.Reputation + ReputationStepForQualifying);
                _inbox.Post(clubId == Data.UserClubId ? "news.user_promoted" : "news.promoted", Data.Club(clubId).Name);
            }

            int change = promoted.Contains(Data.UserClubId) ? 1 : relegated.Contains(Data.UserClubId) ? -1 : 0;
            if (change != 0)
            {
                _ceremonies.Add(new CompetitionRecord
                {
                    Season = season, Kind = TournamentKind.Promotion, Name = "Temporada " + season, Finished = true, DivisionChange = change
                });
            }
        }
    }
}
