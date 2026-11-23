using System;
using System.IO;
using System.Linq;
using Football.Core.Career;
using Football.Core.Domain;
using Football.Core.Management;
using Football.Core.Persistence;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace Football.Core.Tests.Career
{
    /// <summary>Orquestador con el club del usuario: copa, público y entradas, socios, estadio, directiva y medidor de gestión.</summary>
    public class CareerClubTests
    {
        private static CareerSession New(int seed = 9, string team = "Rosario Central")
        {
            var manager = new ManagerProfile { FirstName = "Ana", LastName = "Pérez", Nationality = "AR", AvatarIndex = 2, Outfit = ManagerOutfit.Tracksuit };
            return CareerSession.NewGame(CareerSessionTests.Argentina(), team, 2027, seed, new CareerConfigs(), false, manager);
        }

        private static DayOutcome PlayUntil(CareerSession s, Func<bool> stop, int maxDays = 400)
        {
            DayOutcome outcome = DayOutcome.Continue;
            for (int i = 0; i < maxDays && !stop(); i++)
            {
                outcome = s.AdvanceDay();
                if (outcome == DayOutcome.UserMatchToday)
                {
                    s.SimulateUserMatch();
                }
                else if (outcome == DayOutcome.ManagerSacked || outcome == DayOutcome.SeasonFinished)
                {
                    break;
                }
            }

            return outcome;
        }

        [Test]
        public void NewGame_UsesTheRealClubData_AndCreatesTheManager()
        {
            CareerSession s = New();
            CareerData d = s.Data;
            Assert.That(d.Manager.FullName, Is.EqualTo("Ana Pérez"));
            Assert.That(d.Manager.Popularity, Is.EqualTo(80f));
            int boca = d.Clubs.Single(c => c.Name == "Boca Juniors").Id, gimnasia = d.Clubs.Single(c => c.Name == "Gimnasia y Esgrima La Plata").Id;
            Assert.That(d.Stadiums[boca].Name, Does.Contain("Bombonera"));
            Assert.That(d.Stadiums[boca].Capacity, Is.EqualTo(57200));
            Assert.That(d.Finances[boca].TransferBudget, Is.GreaterThan(d.Finances[gimnasia].TransferBudget * 5), "un grande gasta mucho más");
            Assert.That(s.InfoOf(boca).ClubValue, Is.GreaterThan(s.InfoOf(gimnasia).ClubValue));
            Assert.That(s.TierOf(boca), Is.EqualTo(ClubTier.Big));
            Assert.That(s.TierOf(gimnasia), Is.EqualTo(ClubTier.Development));
            Assert.That(s.Objectives(), Has.Count.EqualTo(5));
            Assert.That(d.Finances.Values.All(f => f.WeeklyWageBudget > 0), Is.True);
        }

        [Test]
        public void TheCup_IsAKnockout_WithAFinal_AndItsTableDoesNotMixWithTheLeague()
        {
            CareerSession s = New();
            PlayUntil(s, () => s.Data.News.Concat(s.Inbox.Items).Any(n => n.Key == "news.cup_champion"), 200);
            Assert.That(s.Inbox.Items.Count(n => n.Key == "news.cup_champion"), Is.EqualTo(1));
            var cup = s.Data.Fixtures.Where(CareerSession.IsCup).ToList();
            Assert.That(cup.Select(f => f.Round).Distinct().Count(), Is.EqualTo(5), "30 clubes: 15 cruces, 7 y uno que pasa directo, 4, 2 y la final");
            Fixture final = cup.Single(f => f.Round == 5);
            Assert.That(s.IsCupFinal(final), Is.True);
            Assert.That(s.ImportanceOf(final), Is.EqualTo(MatchImportance.CupFinal));
            Assert.That(s.AnnualTable().Sum(r => r.Played), Is.EqualTo(2 * s.Data.Fixtures.Count(f => f.IsPlayed && !CareerSession.IsCup(f))));
        }

        [Test]
        public void HomeMatches_RecordTheCrowdAndTheGate_AndPricierTicketsBringFewerFans()
        {
            CareerSession s = New();
            TicketPreview cheap = s.PreviewNextHome(5000);
            TicketPreview dear = s.PreviewNextHome(60000);
            Assert.That(cheap.Fixture, Is.Not.Null);
            Assert.That(cheap.Expected.Ratio, Is.GreaterThan(dear.Expected.Ratio));
            Assert.That(cheap.MinIdeal, Is.LessThan(cheap.MaxIdeal));
            Assert.That(dear.Status, Is.EqualTo(PriceStatus.Excessive));
            PlayUntil(s, () => s.Data.TicketHistory.Count >= 2, 120);
            Assert.That(s.Data.TicketHistory, Has.Count.GreaterThanOrEqualTo(2));
            Assert.That(s.Data.TicketHistory.All(t => t.Attendance > 0f && t.Attendance <= 1f), Is.True);
            ClubFinances fin = s.Data.Finances[s.Data.UserClubId];
            Assert.That(fin.Ledger.Any(e => e.Category == FinanceCategory.Gate && e.Amount > 0), Is.True);
            Assert.That(s.ExpectedAttendance(s.NextUserHomeFixture()), Is.InRange(0f, 1f));
        }

        [Test]
        public void ExpensiveTickets_TriggerASupportersRequest_AndAcceptingItLowersThePrice()
        {
            CareerSession s = New();
            s.SetTicketLevel(TicketLevel.Premium);
            PlayUntil(s, () => s.SupporterRequestPending, 120);
            Assert.That(s.SupporterRequestPending, Is.True, "con entradas Premium, los socios piden una rebaja");
            Assert.That(s.Inbox.Items.Any(n => n.Key == "news.supporters_request"), Is.True);
            float relation = s.Data.Supporters.Relation;
            s.AnswerSupporters(true);
            Assert.That(s.SupporterRequestPending, Is.False);
            Assert.That(s.Data.Supporters.Relation, Is.GreaterThan(relation));
            TicketPreview preview = s.PreviewNextHome();
            Assert.That(preview.Price, Is.LessThan(MatchAttendanceCalculator.PriceOf(TicketLevel.Premium, new AttendanceConfig())));
        }

        [Test]
        public void IgnoringTheSupportersAllSeason_EndsWithThemDemandingTheManagersExit()
        {
            CareerSession s = New(4);
            s.SetTicketLevel(TicketLevel.Premium);
            s.SetNextTicketPrice(200000);
            PlayUntil(s, () =>
            {
                if (s.SupporterRequestPending)
                {
                    s.AnswerSupporters(false);
                    s.SetNextTicketPrice(200000);
                }

                return s.Inbox.Items.Any(n => n.Key == "news.supporters_demand_exit") || s.Data.Manager.Sacked;
            }, 365);
            Assert.That(s.Data.Supporters.Unrest, Is.GreaterThan(new SupporterConfig().CriticalUnrest));
            Assert.That(s.Inbox.Items.Any(n => n.Key == "news.supporters_repudiation"), Is.True);
            Assert.That(s.Inbox.Items.Any(n => n.Key == "news.supporters_demand_exit") || s.Data.Manager.Sacked, Is.True);
            Assert.That(s.Rating().Factors.Any(f => f.Key == "rating.price_above_ideal" && f.Points < 0), Is.True);
        }

        [Test]
        public void StadiumWorks_ProgressWithTheWeeks_AndUpdateTheStadium()
        {
            CareerSession s = New();
            s.Data.Finances[s.Data.UserClubId].Cash = 100_000_000_000;
            int capacity = s.UserStadium.Capacity;
            Assert.That(s.ContractWork(RemodelCategory.Equipment, 1), Is.EqualTo(RemodelResult.Ok));
            Assert.That(s.ContractWork(RemodelCategory.Stands, 1), Is.EqualTo(RemodelResult.Ok));
            Assert.That(s.ContractWork(RemodelCategory.Stands, 2), Is.EqualTo(RemodelResult.CategoryBusy));
            PlayUntil(s, () => s.UserStadium.Works.Count == 0, 200);
            Assert.That(s.UserStadium.Capacity, Is.EqualTo(capacity + 4000));
            Assert.That(s.UserStadium.Pitch, Is.GreaterThanOrEqualTo(PitchCondition.Good));
            Assert.That(s.Inbox.Items.Count(n => n.Key == "news.stadium_work_done"), Is.EqualTo(2));
        }

        [Test]
        public void TheManagementRating_IsTrackedMatchByMatch_AndSigningsCount()
        {
            CareerSession s = New();
            Player target = s.Data.Players.Values.Where(p => p.ClubId.HasValue && p.ClubId != s.Data.UserClubId).OrderBy(p => p.Contract.ReleaseClause).First();
            s.Data.Finances[s.Data.UserClubId].TransferBudget = target.Contract.ReleaseClause * 2;
            Assert.That(s.PayReleaseClause(target.Id), Is.True);
            Assert.That(s.Data.Signings.Single().PlayerId, Is.EqualTo(target.Id));
            PlayUntil(s, () => s.Data.ManagementHistory.Count >= 6, 120);
            Assert.That(s.Data.ManagementHistory, Has.Count.GreaterThanOrEqualTo(6));
            ManagementRating r = s.Rating();
            Assert.That(new[] { r.Supporters, r.Squad, r.Budget, r.Total }, Is.All.InRange(0f, 100f));
            Assert.That(s.Data.Signings[0].TeamMatches, Is.GreaterThanOrEqualTo(6));
        }

        [Test]
        public void ALosingRun_WithAFallingPopularity_GetsTheManagerSacked()
        {
            CareerSession s = New();
            s.Data.Manager.Popularity = 25.2f;
            DayOutcome outcome = PlayUntil(s, () => s.Data.Manager.Sacked, 365);
            Assert.That(s.Data.Manager.Sacked, Is.True, "con la popularidad al borde, los resultados o la caja terminan en despido");
            Assert.That(s.Inbox.Items.Any(n => n.Key == "news.manager_sacked"), Is.True);
            Assert.That(s.AdvanceDay(), Is.EqualTo(DayOutcome.ManagerSacked));
            _ = outcome;
        }

        [Test]
        public void TheNewClubData_SurvivesSqliteAndTheBinaryFile()
        {
            CareerSession s = New();
            s.Data.Finances[s.Data.UserClubId].Cash = 100_000_000_000;
            s.ContractWork(RemodelCategory.Services, 2);
            s.SetTicketLevel(TicketLevel.High);
            PlayUntil(s, () => s.Data.TicketHistory.Count >= 2, 120);
            s.SyncData();
            CareerData fromFile;
            using (var buffer = new MemoryStream())
            {
                CareerFile.Write(buffer, s.Data);
                buffer.Position = 0;
                fromFile = CareerFile.Read(buffer);
            }

            CareerData fromSql;
            using (var db = new SqliteConnection("Data Source=:memory:"))
            {
                db.Open();
                SqliteConnectionSetup.Apply(db);
                Migrator.Default.MigrateToLatest(db);
                var repo = new SqlCareerRepository(db);
                repo.Save(s.Data);
                fromSql = repo.Load();
            }

            foreach (CareerData d in new[] { fromFile, fromSql })
            {
                Assert.That(d.Manager.FullName, Is.EqualTo("Ana Pérez"));
                Assert.That(d.Manager.Outfit, Is.EqualTo(ManagerOutfit.Tracksuit));
                Assert.That(d.TicketLevel, Is.EqualTo(TicketLevel.High));
                Assert.That(d.SeasonStartCash, Is.EqualTo(s.Data.SeasonStartCash));
                Assert.That(d.Stadiums[s.Data.UserClubId].Works.Single().Category, Is.EqualTo(RemodelCategory.Services));
                Assert.That(d.Stadiums[s.Data.UserClubId].Works.Single().RemainingWeeks, Is.EqualTo(s.UserStadium.Works.Single().RemainingWeeks));
                Assert.That(d.TicketHistory.Select(t => t.Revenue), Is.EqualTo(s.Data.TicketHistory.Select(t => t.Revenue)));
                Assert.That(d.ManagementHistory.Count, Is.EqualTo(s.Data.ManagementHistory.Count));
                Assert.That(d.Supporters.Relation, Is.EqualTo(s.Data.Supporters.Relation));
                Assert.That(d.ClubInfos[s.Data.UserClubId].Founded, Is.EqualTo(1889));
            }
        }

        [Test]
        public void Forecast_IsDeterministic_AndFollowsTheConfiguredSeasons()
        {
            CareerSession a = New(seed: 4), b = New(seed: 4);
            var fixtures = a.Data.Fixtures.Take(60).ToList();
            foreach (Fixture f in fixtures)
            {
                MatchForecast fa = a.ForecastOf(f), fb = b.ForecastOf(b.Data.Fixtures.Single(x => x.Id == f.Id));
                Assert.That(fa.Weather, Is.EqualTo(fb.Weather));
                Assert.That(fa.Temperature, Is.EqualTo(fb.Temperature));
                var c = new CareerConfig();
                float mean = c.MeanTemperatureByMonth[f.Date.Month - 1];
                Assert.That(fa.Temperature, Is.InRange(mean - c.TemperatureSpread - c.RainCooling - 1, mean + c.TemperatureSpread + 1));
            }

            Assert.That(fixtures.Select(f => a.ForecastOf(f).Weather).Distinct().Count(), Is.GreaterThan(1), "no siempre el mismo cielo");
        }

        [Test]
        public void FinanceSummary_AddsUpTheSeasonLedger_AndProjectsTheCash()
        {
            CareerSession s = New();
            PlayUntil(s, () => s.Data.TicketHistory.Count >= 3);
            SeasonFinanceSummary sum = s.FinanceSummary();
            ClubFinances fin = s.Data.Finances[s.Data.UserClubId];
            long net = fin.Ledger.Where(e => e.Date.Year == s.Data.Season).Sum(e => e.Amount);
            Assert.That(sum.Net, Is.EqualTo(net));
            Assert.That(sum.ByCategory.Sum(), Is.EqualTo(net));
            Assert.That(sum.ByCategory[(int)FinanceCategory.Gate], Is.GreaterThan(0), "la taquilla entra en el libro");
            Assert.That(sum.Income, Is.GreaterThan(0));
            Assert.That(sum.Expenses, Is.GreaterThan(0));
            Assert.That(Math.Sign(sum.ProjectedCash - fin.Cash), Is.EqualTo(Math.Sign(sum.Net)).Or.EqualTo(0), "la proyección sigue el ritmo actual");
        }
    }
}
