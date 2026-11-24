using System;
using System.Linq;
using Football.Core.Career;
using Football.Core.Domain;
using NUnit.Framework;

namespace Football.Core.Tests.Career
{
    /// <summary>Fases finales del Apertura y del Clausura, y Trofeo de Campeones.</summary>
    public class PlayoffTests
    {
        private static CareerSession PlayFullSeason()
        {
            CareerSession s = CareerSession.NewGame(CareerSessionTests.Argentina(), "Rosario Central", 2027, 9, new CareerConfigs(), false, null, 30);
            int guard = 0;
            DayOutcome outcome;
            do
            {
                s.Data.Manager.Popularity = 100f;
                outcome = s.AdvanceToNextMatch();
                if (outcome == DayOutcome.UserMatchToday)
                {
                    s.SimulateUserMatch();
                }
            }
            while (outcome != DayOutcome.SeasonFinished && guard++ < 800);
            Assert.That(outcome, Is.EqualTo(DayOutcome.SeasonFinished));
            return s;
        }

        [Test]
        public void ASeason_HasAPlayoffForEachTournament_WithSixteenClubsAndASingleFinal()
        {
            CareerSession s = PlayFullSeason();
            foreach (TournamentKind kind in new[] { TournamentKind.AperturaPlayoff, TournamentKind.ClausuraPlayoff })
            {
                int id = CompetitionInfo.IdOf(2027, kind);
                var fixtures = s.Data.Fixtures.Where(f => f.CompetitionId == id).ToList();
                Assert.That(fixtures.Count, Is.EqualTo(15), kind + ": 8 + 4 + 2 + 1 cruces");
                Assert.That(fixtures.Count(f => f.Round == 1), Is.EqualTo(8));
                Assert.That(fixtures.Count(f => f.Round == 4), Is.EqualTo(1), "una sola final");
                Assert.That(fixtures.Where(f => f.Round == 1).SelectMany(f => new[] { f.HomeClubId, f.AwayClubId }).Distinct().Count(), Is.EqualTo(16));
                Assert.That(fixtures.All(f => f.IsPlayed), Is.True);
            }
        }

        [Test]
        public void TheFirstRound_PairsTheBestOfEachZoneWithTheWorstOfTheOther()
        {
            CareerSession s = PlayFullSeason();
            int regular = CompetitionInfo.IdOf(2027, TournamentKind.Apertura);
            var zoneOf = s.Data.Fixtures.Where(f => f.CompetitionId == regular && f.Group > 0)
                .SelectMany(f => new[] { (f.HomeClubId, f.Group), (f.AwayClubId, f.Group) }).Distinct().ToDictionary(x => x.Item1, x => x.Item2);
            var round1 = s.Data.Fixtures.Where(f => f.CompetitionId == CompetitionInfo.IdOf(2027, TournamentKind.AperturaPlayoff) && f.Round == 1).ToList();
            Assert.That(round1.All(f => zoneOf[f.HomeClubId] != zoneOf[f.AwayClubId]), Is.True, "se cruzan las zonas");
        }

        [Test]
        public void TheTournamentChampion_IsTheWinnerOfTheFinal_NotTheLeaderOfTheTable()
        {
            CareerSession s = PlayFullSeason();
            CompetitionInfo apertura = s.Data.Competitions.Find(c => c.Id == CompetitionInfo.IdOf(2027, TournamentKind.Apertura));
            Assert.That(s.HasPlayoffs(apertura), Is.True);
            Fixture final = s.PlayoffFinal(apertura);
            Assert.That(final, Is.Not.Null);
            int champion = Football.Core.Competitions.Knockout.Winner(final, new Football.Core.Common.SeededRandom(1));
            Assert.That(champion, Is.EqualTo(final.HomeClubId).Or.EqualTo(final.AwayClubId));
            Assert.That(s.Inbox.Chronological.Any(n => n.Key == "news.competition_champion" || n.Key == "news.user_champion"), Is.True, "se anuncia el campeón al terminar la fase final");
        }

        [Test]
        public void TheTrofeoDeCampeones_IsPlayed_BetweenTheTwoChampions_WhenTheyDiffer()
        {
            CareerSession s = PlayFullSeason();
            var a = s.Data.Competitions.Find(c => c.Id == CompetitionInfo.IdOf(2027, TournamentKind.Apertura));
            var c2 = s.Data.Competitions.Find(c => c.Id == CompetitionInfo.IdOf(2027, TournamentKind.Clausura));
            int ca = Football.Core.Competitions.Knockout.Winner(s.PlayoffFinal(a), new Football.Core.Common.SeededRandom(1));
            int cc = Football.Core.Competitions.Knockout.Winner(s.PlayoffFinal(c2), new Football.Core.Common.SeededRandom(1));
            var trofeo = s.Data.Fixtures.Where(f => f.CompetitionId == CompetitionInfo.IdOf(2027, TournamentKind.TrofeoCampeones)).ToList();
            if (ca == cc)
            {
                Assert.That(trofeo, Is.Empty);
            }
            else
            {
                Assert.That(trofeo.Count, Is.EqualTo(1));
                Assert.That(new[] { trofeo[0].HomeClubId, trofeo[0].AwayClubId }, Is.EquivalentTo(new[] { ca, cc }));
            }
        }

        [Test]
        public void TheWholeSeason_FitsInTheCalendarYear()
        {
            CareerSession s = PlayFullSeason();
            DateTime last = s.Data.Fixtures.Where(f => f.CompetitionId / 10 == 2027).Max(f => f.Date);
            Assert.That(last, Is.LessThan(new DateTime(2028, 1, 1)), "última fecha: " + last.ToString("yyyy-MM-dd"));
        }

        [Test]
        public void PlayoffMatches_AreKnockouts_AndTheBetterSeededPlaysAtHomeInLaterRounds()
        {
            CareerSession s = PlayFullSeason();
            int id = CompetitionInfo.IdOf(2027, TournamentKind.AperturaPlayoff);
            Assert.That(s.Data.Fixtures.Where(f => f.CompetitionId == id).All(CareerSession.IsCup), Is.True);
            int regular = CompetitionInfo.IdOf(2027, TournamentKind.Apertura);
            var rank = s.Data.Fixtures.Where(f => f.CompetitionId == regular).SelectMany(f => new[] { f.HomeClubId, f.AwayClubId }).Distinct().ToList();
            Assert.That(rank.Count, Is.EqualTo(30));
        }
    }
}
