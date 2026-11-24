using System.Linq;
using Football.Core.Career;
using Football.Core.Domain;
using NUnit.Framework;

namespace Football.Core.Tests.Career
{
    /// <summary>Historial de competiciones, calendario del usuario, cruces de la copa y cartel de campeón.</summary>
    public class CareerHistoryTests
    {
        private static void PlayUntilSeasonEnds(CareerSession s)
        {
            DayOutcome outcome;
            int guard = 0;
            do
            {
                outcome = s.AdvanceToNextMatch();
                if (outcome == DayOutcome.UserMatchToday)
                {
                    s.SimulateUserMatch();
                }
            }
            while (outcome != DayOutcome.SeasonFinished && guard++ < 300);
        }

        [Test]
        public void ASeason_ProducesOneCeremonyPerCompetition_WithTheRightChampion()
        {
            CareerSession s = CareerSessionTests.NewCareer(9);
            PlayUntilSeasonEnds(s);
            var ceremonies = s.TakeCeremonies();
            Assert.That(ceremonies.Select(c => c.Kind), Is.EquivalentTo(new[] { TournamentKind.Apertura, TournamentKind.Clausura, TournamentKind.Cup }));
            Assert.That(s.TakeCeremonies(), Is.Empty, "se entregan una sola vez");
            foreach (CompetitionRecord r in ceremonies)
            {
                Assert.That(r.Finished, Is.True, r.Name);
                Assert.That(r.ChampionClubId, Is.GreaterThan(0), r.Name);
                Assert.That(r.UserChampion, Is.EqualTo(r.ChampionClubId == s.Data.UserClubId), r.Name);
            }

            CompetitionRecord apertura = ceremonies.First(c => c.Kind == TournamentKind.Apertura);
            Assert.That(apertura.ChampionClubId, Is.GreaterThan(0));
            Assert.That(s.Inbox.Items.Any(n => n.Key == "news.user_champion" || n.Key == "news.competition_champion"), Is.True);
        }

        [Test]
        public void TheHistory_ListsEveryCompetition_AndTheUserCalendarHasAllHisMatches()
        {
            CareerSession s = CareerSessionTests.NewCareer(9);
            int season = s.Data.Season;
            PlayUntilSeasonEnds(s);
            var history = s.CompetitionHistory();
            var kinds = history.Where(h => h.Season == season).Select(h => h.Kind).ToList();
            Assert.That(kinds, Is.SupersetOf(new[] { TournamentKind.Apertura, TournamentKind.Cup, TournamentKind.Clausura }));
            Assert.That(kinds.Except(new[] { TournamentKind.Apertura, TournamentKind.Cup, TournamentKind.Clausura, TournamentKind.AperturaPlayoff, TournamentKind.ClausuraPlayoff, TournamentKind.TrofeoCampeones }), Is.Empty, "solo las competiciones esperadas (las fases finales y el Trofeo si el usuario jugó)");
            Assert.That(history.Where(h => h.Season == season && (h.Kind == TournamentKind.Apertura || h.Kind == TournamentKind.Clausura)).All(h => h.UserPosition >= 1 && h.UserPosition <= 30), Is.True);

            var mine = s.UserFixtures(season);
            Assert.That(mine.All(f => f.IsPlayed), Is.True);
            Assert.That(mine.Select(f => f.Date), Is.Ordered);
            Assert.That(mine.Count(f => !CareerSession.IsCup(f)), Is.EqualTo(30), "15 del Apertura y 15 del Clausura (sin fases finales ni copas)");
            Assert.That(s.SeasonsWithFixtures(), Does.Contain(season));
            Assert.That(s.UserFixtures(season + 1).All(f => !f.IsPlayed), Is.True, "la temporada siguiente ya está armada, sin jugar");
        }

        [Test]
        public void TheCupBracket_ShowsTheRoundOfTheUser_AndEveryTieOfThatRound()
        {
            CareerSession s = CareerSessionTests.NewCareer(9);
            int season = s.Data.Season;
            PlayUntilSeasonEnds(s);
            int rounds = s.CupRounds(season);
            Assert.That(rounds, Is.GreaterThanOrEqualTo(4), "30 clubes: al menos cuatro rondas hasta la final");
            Assert.That(s.CupFixtures(season, 1), Has.Count.EqualTo(15));
            Assert.That(s.CupFixtures(season, rounds), Has.Count.EqualTo(1), "la última ronda es la final");
            Assert.That(s.UserCupRound(season), Is.InRange(1, rounds));
            foreach (Fixture f in s.CupFixtures(season, 2))
            {
                Assert.That(s.CupWinner(f), Is.EqualTo(f.HomeGoals > f.AwayGoals ? f.HomeClubId : f.HomeGoals < f.AwayGoals ? f.AwayClubId : s.CupWinner(f)));
            }
        }
    }
}
