using System;
using System.IO;
using System.Linq;
using Football.Core.Career;
using Football.Core.Domain;
using Football.Core.Persistence;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace Football.Core.Tests.Career
{
    /// <summary>Primera y Nacional B: reparto inicial, calendario, promoción, ascensos y descensos, y guardado.</summary>
    public class CareerDivisionTests
    {
        private const int PrimeraSize = 20;

        private static CareerSession NewTwoDivisions(int seed = 3, string club = "Rosario Central", int size = PrimeraSize) =>
            CareerSession.NewGame(CareerSessionTests.Argentina(), club, 2027, seed, new CareerConfigs(), false, null, size);

        private static void PlaySeason(CareerSession s)
        {
            DayOutcome outcome;
            int guard = 0;
            do
            {
                s.Data.Manager.Popularity = 100f; // estas pruebas miden divisiones, no la paciencia de la directiva
                outcome = s.AdvanceToNextMatch();
                if (outcome == DayOutcome.UserMatchToday)
                {
                    s.SimulateUserMatch();
                }
            }
            while (outcome != DayOutcome.SeasonFinished && guard++ < 400);
            Assert.That(outcome, Is.EqualTo(DayOutcome.SeasonFinished));
        }

        [Test]
        public void TheBestClubsStartInPrimera_AndTheRestInNacionalB()
        {
            CareerSession s = NewTwoDivisions();
            Assert.That(s.Data.Clubs.Count(c => c.Division == 1), Is.EqualTo(PrimeraSize));
            Assert.That(s.Data.Clubs.Count(c => c.Division == 2), Is.EqualTo(30 - PrimeraSize));
            Assert.That(s.HasDivisions, Is.True);
            double top = s.Data.Clubs.Where(c => c.Division == 1).Average(c => s.Data.Finances[c.Id].Reputation);
            double bottom = s.Data.Clubs.Where(c => c.Division == 2).Average(c => s.Data.Finances[c.Id].Reputation);
            Assert.That(top, Is.GreaterThan(bottom), "Primera reúne a los más fuertes");
        }

        [Test]
        public void TheCalendar_HasASingleRoundRobinForPrimera_AndADoubleOneForNacionalB()
        {
            CareerSession s = NewTwoDivisions();
            int season = s.Data.Season;
            int ApertureCount(TournamentKind k) => s.Data.Fixtures.Count(f => f.CompetitionId == CompetitionInfo.IdOf(season, k));
            Assert.That(ApertureCount(TournamentKind.Apertura), Is.EqualTo(PrimeraSize * (PrimeraSize - 1) / 2), "todos contra todos, una rueda");
            Assert.That(ApertureCount(TournamentKind.Clausura), Is.EqualTo(PrimeraSize * (PrimeraSize - 1) / 2));
            int second = 30 - PrimeraSize;
            Assert.That(ApertureCount(TournamentKind.NacionalB), Is.EqualTo(second * (second - 1)), "ida y vuelta");
            Assert.That(s.Data.Fixtures.Where(f => f.CompetitionId == CompetitionInfo.IdOf(season, TournamentKind.Apertura))
                .All(f => s.Data.Club(f.HomeClubId).Division == 1 && s.Data.Club(f.AwayClubId).Division == 1), Is.True);
            Assert.That(s.Data.Fixtures.Count(f => CareerSession.IsCup(f) && f.Round == 1), Is.EqualTo(15), "la copa la juegan los 30");
        }

        [Test]
        public void ImpossibleSizes_AreRejected()
        {
            Assert.Throws<ArgumentException>(() => NewTwoDivisions(3, "Rosario Central", 27), "quedarían menos de 6 en el Nacional B");
            Assert.Throws<ArgumentException>(() => NewTwoDivisions(3, "Rosario Central", 1));
        }

        [Test]
        public void AUserClubOutsideThePrimeraStartsInNacionalB()
        {
            CareerSession s = NewTwoDivisions(3, "San Martín (San Juan)");
            Assert.That(s.UserDivision, Is.EqualTo(2));
            Assert.That(s.CurrentTournament().Kind, Is.EqualTo(TournamentKind.NacionalB));
            Assert.That(s.TournamentTable(), Has.Count.EqualTo(30 - PrimeraSize));
            Assert.That(s.UserFixtures(s.Data.Season).Count(f => !CareerSession.IsCup(f)), Is.EqualTo((30 - PrimeraSize - 1) * 2));
            Assert.That(s.AveragesTable(), Has.Count.EqualTo(PrimeraSize), "los promedios son de Primera");
        }

        [Test]
        public void EverySeason_TwoGoDownTwoGoUp_AndThePlayoffDecidesTheThird()
        {
            CareerSession s = NewTwoDivisions(7);
            for (int season = 1; season <= 3; season++)
            {
                int year = s.Data.Season;
                var before = s.Data.Clubs.ToDictionary(c => c.Id, c => c.Division);
                PlaySeason(s);
                var after = s.Data.Clubs.ToDictionary(c => c.Id, c => c.Division);
                Assert.That(after.Values.Count(d => d == 1), Is.EqualTo(PrimeraSize), "Primera siempre tiene los mismos clubes (año " + year + ")");
                int[] topTwoB = s.AnnualTable(year, 2).Take(2).Select(t => t.ClubId).ToArray();
                Assert.That(topTwoB.All(id => after[id] == 1), Is.True, "los dos mejores del Nacional B suben");
                Assert.That(s.Data.Competitions.Any(c => c.Id == CompetitionInfo.IdOf(year, TournamentKind.Promotion)), Is.True, "hubo promoción");
                int winner = s.PromotionWinner(year);
                Assert.That(winner, Is.GreaterThan(0));
                Assert.That(after[winner], Is.EqualTo(1), "el ganador de la promoción juega en Primera");
                int moved = before.Count(kv => after[kv.Key] != kv.Value);
                Assert.That(moved, Is.InRange(4, 6), "2 + 2 y, si gana el de B, uno más en cada sentido");
                Assert.That(s.Data.Clubs.Where(c => before[c.Id] == 1 && after[c.Id] == 2).Count(), Is.InRange(2, 3));
            }
        }

        [Test]
        public void TheDivisionsSurviveTheBinaryFileAndSqlite()
        {
            CareerSession s = NewTwoDivisions();
            PlaySeason(s);
            s.SyncData();
            var buffer = new MemoryStream();
            CareerFile.Write(buffer, s.Data);
            buffer.Position = 0;
            CareerData fromFile = CareerFile.Read(buffer);
            Assert.That(fromFile.PrimeraSize, Is.EqualTo(PrimeraSize));
            Assert.That(fromFile.Clubs.Select(c => c.Division), Is.EqualTo(s.Data.Clubs.Select(c => c.Division)));

            using (var db = new SqliteConnection("Data Source=:memory:"))
            {
                db.Open();
                SqliteConnectionSetup.Apply(db);
                Migrator.Default.MigrateToLatest(db);
                var repo = new SqlCareerRepository(db);
                repo.Save(s.Data);
                CareerData loaded = repo.Load();
                Assert.That(loaded.PrimeraSize, Is.EqualTo(PrimeraSize));
                Assert.That(loaded.Clubs.Select(c => c.Division), Is.EqualTo(s.Data.Clubs.Select(c => c.Division)));
            }

            var resumed = new CareerSession(fromFile, new CareerConfigs());
            Assert.That(resumed.UserDivision, Is.EqualTo(s.UserDivision));
            Assert.That(resumed.HasCeremony, Is.False, "lo ya terminado no se anuncia de nuevo");
        }
    }
}
