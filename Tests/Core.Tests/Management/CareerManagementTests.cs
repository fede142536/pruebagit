using System;
using System.Collections.Generic;
using System.Linq;
using Football.Core.Common;
using Football.Core.Domain;
using Football.Core.Management;
using NUnit.Framework;

namespace Football.Core.Tests.Management
{
    /// <summary>Asistencia, rango ideal de entradas, socios, remodelación del estadio, expectativas y medidor de gestión.</summary>
    public class CareerManagementTests
    {
        private static readonly AttendanceConfig A = new AttendanceConfig();

        private static AttendanceInput Match(MatchImportance importance, long price, float form = 0.5f) => new AttendanceInput
        {
            Capacity = 50000, ParkingSpaces = 3000, Price = price, Importance = importance, Reputation = 70, OpponentReputation = 60,
            Form = form, PositionVsExpectation = 0f, FanMood = 0.5f
        };

        // ---------------------------------------------------------------- asistencia

        [Test]
        public void ACupFinal_FillsTheStadiumEvenWithPremiumPrices_WhileAMeaninglessMatchEmpties()
        {
            long premium = MatchAttendanceCalculator.PriceOf(TicketLevel.Premium, A);
            AttendanceResult final = MatchAttendanceCalculator.Compute(Match(MatchImportance.CupFinal, premium), A);
            AttendanceResult dull = MatchAttendanceCalculator.Compute(Match(MatchImportance.Meaningless, premium), A);
            Assert.That(final.Ratio, Is.GreaterThan(0.75f), "la final se llena con entradas caras");
            Assert.That(dull.Ratio, Is.LessThan(0.3f), "sin nada en juego y caro: tribunas semivacías");
            Assert.That(final.Revenue, Is.GreaterThan(dull.Revenue * 3));
        }

        [Test]
        public void RaisingThePrice_InAMeaninglessMatch_LowersNetRevenue()
        {
            long normal = MatchAttendanceCalculator.PriceOf(TicketLevel.Normal, A);
            long premium = MatchAttendanceCalculator.PriceOf(TicketLevel.Premium, A);
            AttendanceResult atNormal = MatchAttendanceCalculator.Compute(Match(MatchImportance.Meaningless, normal), A);
            AttendanceResult atPremium = MatchAttendanceCalculator.Compute(Match(MatchImportance.Meaningless, premium), A);
            Assert.That(atPremium.Ratio, Is.LessThan(atNormal.Ratio * 0.6f), "elasticidad alta en un partido intrascendente");
            Assert.That(atPremium.Revenue, Is.LessThan(atNormal.Revenue), "subir el precio desploma la recaudación neta");
        }

        [Test]
        public void ImportanceMultipliers_AndTeamForm_ChangeTheCrowd()
        {
            long price = MatchAttendanceCalculator.PriceOf(TicketLevel.Normal, A);
            float[] ratios = Enum.GetValues(typeof(MatchImportance)).Cast<MatchImportance>()
                .Select(i => MatchAttendanceCalculator.Compute(Match(i, price), A).Ratio).ToArray();
            Assert.That(ratios, Is.Ordered.Descending, "final > clásico > decisivo > regular > intrascendente");
            float good = MatchAttendanceCalculator.Compute(Match(MatchImportance.Regular, price, 1f), A).Ratio;
            float bad = MatchAttendanceCalculator.Compute(Match(MatchImportance.Regular, price, 0f), A).Ratio;
            Assert.That(good, Is.GreaterThan(bad + 0.15f), "la racha pesa");
            AttendanceInput angry = Match(MatchImportance.Regular, price);
            angry.SupporterUnrest = 100f;
            Assert.That(MatchAttendanceCalculator.Compute(angry, A).Ratio, Is.LessThan(MatchAttendanceCalculator.Compute(Match(MatchImportance.Regular, price), A).Ratio));
        }

        [Test]
        public void ParkingRaisesTheUsableCap()
        {
            AttendanceInput i = Match(MatchImportance.CupFinal, MatchAttendanceCalculator.PriceOf(TicketLevel.Popular, A));
            i.ParkingSpaces = 0;
            float noParking = MatchAttendanceCalculator.Compute(i, A).Ratio;
            i.ParkingSpaces = 8000;
            Assert.That(MatchAttendanceCalculator.Compute(i, A).Ratio, Is.GreaterThan(noParking));
            Assert.That(noParking, Is.LessThanOrEqualTo(A.BaseAccessCap + 1e-4f));
        }

        // ---------------------------------------------------------------- rango ideal y socios

        [Test]
        public void IdealRange_IsHigherForAClassicAndAFinal_AndLowerForADullMatchInABadRun()
        {
            (long Min, long Max) classic = TicketPricingAdvisor.IdealRange(Match(MatchImportance.Classic, 0, 0.7f), A);
            (long Min, long Max) dull = TicketPricingAdvisor.IdealRange(Match(MatchImportance.Meaningless, 0, 0.1f), A);
            Assert.That(classic.Min, Is.LessThan(classic.Max));
            Assert.That(classic.Min, Is.GreaterThan(dull.Max));
            AttendanceInput big = Match(MatchImportance.Regular, 0);
            big.OpponentReputation = 90;
            AttendanceInput small = Match(MatchImportance.Regular, 0);
            small.OpponentReputation = 30;
            Assert.That(TicketPricingAdvisor.IdealRange(big, A).Max, Is.GreaterThan(TicketPricingAdvisor.IdealRange(small, A).Max), "rival grande, entrada más cara");
            Assert.That(TicketPricingAdvisor.Status(classic.Max, classic, A), Is.EqualTo(PriceStatus.Ideal));
            Assert.That(TicketPricingAdvisor.Status((long)(classic.Max * 1.1), classic, A), Is.EqualTo(PriceStatus.Elevated));
            Assert.That(TicketPricingAdvisor.Status(classic.Max * 2, classic, A), Is.EqualTo(PriceStatus.Excessive));
        }

        [Test]
        public void Supporters_AskForACut_AndAcceptingItCalmsThemDown()
        {
            var c = new SupporterConfig();
            var s = new SupporterState { Relation = c.StartingRelation, Unrest = 30f };
            (long, long) range = (10000L, 14000L);
            Assert.That(SupporterRequestSystem.Review(s, 7, 13000, range), Is.False, "dentro del ideal no hay pedido");
            Assert.That(SupporterRequestSystem.Review(s, 7, 20000, range), Is.True);
            Assert.That(SupporterRequestSystem.Review(s, 7, 20000, range), Is.False, "un solo pedido por partido");
            long newPrice = SupporterRequestSystem.Accept(s, c);
            Assert.That(newPrice, Is.EqualTo(14000));
            Assert.That(s.Unrest, Is.LessThan(30f));
            Assert.That(s.Relation, Is.GreaterThan(c.StartingRelation));
        }

        [Test]
        public void IgnoringTheSupporters_EscalatesUnrest_UntilTheyDemandTheManagersExit()
        {
            var c = new SupporterConfig();
            var s = new SupporterState { Relation = c.StartingRelation };
            (long, long) range = (10000L, 14000L);
            var outcomes = new List<SupporterOutcome>();
            for (int match = 1; match <= 12; match++)
            {
                SupporterRequestSystem.Review(s, match, 30000, range);
                SupporterRequestSystem.Keep(s, c);
                outcomes.Add(SupporterRequestSystem.AfterHomeMatch(s, 30000, range, MatchImportance.Meaningless, 0.2f, c));
            }

            Assert.That(s.Unrest, Is.GreaterThan(c.CriticalUnrest));
            Assert.That(outcomes, Does.Contain(SupporterOutcome.Repudiation), "primero, mensajes de repudio");
            Assert.That(outcomes, Does.Contain(SupporterOutcome.DemandSacking), "si se sostiene, piden la salida del DT");
            int firstRepudiation = outcomes.IndexOf(SupporterOutcome.Repudiation);
            Assert.That(outcomes.IndexOf(SupporterOutcome.DemandSacking), Is.GreaterThan(firstRepudiation));

            var lowStakes = new SupporterState();
            var bigMatch = new SupporterState();
            SupporterRequestSystem.AfterHomeMatch(lowStakes, 20000, range, MatchImportance.Meaningless, 0.2f, c);
            SupporterRequestSystem.AfterHomeMatch(bigMatch, 20000, range, MatchImportance.CupFinal, 0.8f, c);
            Assert.That(lowStakes.Unrest, Is.GreaterThan(bigMatch.Unrest), "cobrar caro un partido intrascendente enoja más");
        }

        [Test]
        public void FairPrices_CalmTheSupporters_AndResetTheCriticalStreak()
        {
            var c = new SupporterConfig();
            var s = new SupporterState { Unrest = 90f, CriticalStreak = 2 };
            SupporterRequestSystem.AfterHomeMatch(s, 9000, (10000L, 14000L), MatchImportance.Regular, 0.5f, c);
            Assert.That(s.Unrest, Is.LessThan(90f));
            for (int i = 0; i < 5; i++)
            {
                SupporterRequestSystem.AfterHomeMatch(s, 9000, (10000L, 14000L), MatchImportance.Regular, 0.5f, c);
            }

            Assert.That(s.CriticalStreak, Is.Zero);
        }

        // ---------------------------------------------------------------- remodelación

        private static (StadiumState, ClubFinances) Stadium(long cash = 100_000_000_000) =>
            (new StadiumState { Name = "Test", Capacity = 40000, Pitch = PitchCondition.Regular }, new ClubFinances { Cash = cash });

        [TestCase(RemodelCategory.Stands, 1, 16, 20)]
        [TestCase(RemodelCategory.Stands, 2, 32, 36)]
        [TestCase(RemodelCategory.Stands, 3, 48, 52)]
        [TestCase(RemodelCategory.Parking, 1, 10, 10)]
        [TestCase(RemodelCategory.Parking, 2, 20, 20)]
        [TestCase(RemodelCategory.Parking, 3, 32, 32)]
        [TestCase(RemodelCategory.Equipment, 1, 6, 6)]
        [TestCase(RemodelCategory.Equipment, 2, 14, 14)]
        [TestCase(RemodelCategory.Equipment, 3, 24, 24)]
        [TestCase(RemodelCategory.Services, 1, 8, 8)]
        [TestCase(RemodelCategory.Services, 2, 18, 18)]
        [TestCase(RemodelCategory.Services, 3, 30, 30)]
        public void EachWork_TakesItsWeeks_AndBiggerWorksCostMore(RemodelCategory category, int tier, int min, int max)
        {
            var c = new StadiumConfig();
            (StadiumState s, ClubFinances f) = Stadium();
            Assert.That(StadiumRemodelingSystem.Contract(s, f, category, tier, new DateTime(2027, 1, 1), new SeededRandom((ulong)tier), c), Is.EqualTo(RemodelResult.Ok));
            Assert.That(s.Works[0].TotalWeeks, Is.InRange(min, max));
            if (tier > 1)
            {
                Assert.That(StadiumRemodelingSystem.CostOf(category, tier, c), Is.GreaterThan(StadiumRemodelingSystem.CostOf(category, tier - 1, c)));
            }
        }

        [Test]
        public void TwoWorks_InTheSameCategory_AreBlocked_ButDifferentCategoriesRunTogether()
        {
            var c = new StadiumConfig();
            (StadiumState s, ClubFinances f) = Stadium();
            var rng = new SeededRandom(1);
            var date = new DateTime(2027, 1, 1);
            Assert.That(StadiumRemodelingSystem.Contract(s, f, RemodelCategory.Stands, 1, date, rng, c), Is.EqualTo(RemodelResult.Ok));
            Assert.That(StadiumRemodelingSystem.Contract(s, f, RemodelCategory.Stands, 2, date, rng, c), Is.EqualTo(RemodelResult.CategoryBusy));
            Assert.That(StadiumRemodelingSystem.Contract(s, f, RemodelCategory.Parking, 1, date, rng, c), Is.EqualTo(RemodelResult.Ok));
            long quote = StadiumRemodelingSystem.Quote(new[] { (RemodelCategory.Equipment, 2), (RemodelCategory.Services, 1) }, c);
            Assert.That(quote, Is.EqualTo(c.Cost[7] + c.Cost[9]), "total de la remodelación seleccionada");
            Assert.That(f.Ledger.Count(e => e.Category == FinanceCategory.Construction), Is.EqualTo(2), "se paga al contratar");
            (StadiumState poor, ClubFinances broke) = Stadium(1000);
            Assert.That(StadiumRemodelingSystem.Contract(poor, broke, RemodelCategory.Stands, 3, date, rng, c), Is.EqualTo(RemodelResult.NoMoney));
        }

        [Test]
        public void Weeks_CountDown_AndTheImprovementAppliesWhenTheWorkEnds()
        {
            var c = new StadiumConfig();
            (StadiumState s, ClubFinances f) = Stadium();
            var date = new DateTime(2027, 1, 1);
            StadiumRemodelingSystem.Contract(s, f, RemodelCategory.Stands, 1, date, new SeededRandom(3), c);
            StadiumRemodelingSystem.Contract(s, f, RemodelCategory.Equipment, 1, date, new SeededRandom(3), c);
            int weeks = s.ActiveWork(RemodelCategory.Stands).TotalWeeks;
            Assert.That(StadiumRemodelingSystem.UsableCapacity(s, c), Is.LessThan(40000), "el sector en obra queda clausurado");
            var done = new List<RemodelWork>();
            var finished = new List<RemodelCategory>();
            for (int w = 1; w <= weeks; w++)
            {
                StadiumRemodelingSystem.AdvanceWeek(s, c, done);
                finished.AddRange(done.Select(d => d.Category));
                if (w < weeks)
                {
                    Assert.That(s.Capacity, Is.EqualTo(40000), "la capacidad nueva llega al terminar");
                }
            }

            Assert.That(finished, Is.EqualTo(new[] { RemodelCategory.Equipment, RemodelCategory.Stands }));
            Assert.That(s.Capacity, Is.EqualTo(44000));
            Assert.That(s.Pitch, Is.EqualTo(PitchCondition.Good), "resembrado: de Regular a Bueno");
            Assert.That(s.Works, Is.Empty);
            Assert.That(StadiumRemodelingSystem.UsableCapacity(s, c), Is.EqualTo(44000));
            Assert.That(StadiumRemodelingSystem.IsAvailable(s, RemodelCategory.Equipment, 1), Is.False, "ese nivel ya está hecho");
            Assert.That(StadiumRemodelingSystem.IsAvailable(s, RemodelCategory.Stands, 1), Is.True, "las tribunas se pueden volver a ampliar");
        }

        // ---------------------------------------------------------------- directiva

        [Test]
        public void BoardExpectations_FollowTheClubsHierarchy()
        {
            var c = new BoardConfig();
            List<BoardObjective> big = BoardExpectations.Generate(ClubTier.Big, 15, c);
            List<BoardObjective> dev = BoardExpectations.Generate(ClubTier.Development, 15, c);
            Assert.That(big, Has.Count.EqualTo(5));
            Assert.That(big.Select(o => o.Category).Distinct().Count(), Is.EqualTo(5));
            Assert.That(big[0].Category, Is.EqualTo(ObjectiveCategory.NationalSuccess), "a un grande se le exige ganar");
            Assert.That(big[0].Priority, Is.EqualTo(ObjectivePriority.Critical));
            Assert.That(dev[0].Category, Is.EqualTo(ObjectiveCategory.Finances), "a uno en desarrollo, cuidar la caja");
            Assert.That(big.Single(o => o.Category == ObjectiveCategory.NationalSuccess).Target,
                Is.LessThan(dev.Single(o => o.Category == ObjectiveCategory.NationalSuccess).Target));
            Assert.That(big.Select(o => (int)o.Priority), Is.Ordered);
        }

        [Test]
        public void Popularity_StartsAt80_RisesWithResultsAndClassics_AndFallsUntilTheSack()
        {
            var c = new BoardConfig();
            var m = new ManagerProfile();
            Assert.That(m.Popularity, Is.EqualTo(80f));
            float afterWin = BoardExpectations.AfterMatch(m.Popularity, 2, 0, false, c);
            float afterClassicWin = BoardExpectations.AfterMatch(m.Popularity, 2, 0, true, c);
            float afterClassicLoss = BoardExpectations.AfterMatch(m.Popularity, 0, 2, true, c);
            Assert.That(afterClassicWin, Is.GreaterThan(afterWin));
            Assert.That(afterClassicLoss, Is.LessThan(BoardExpectations.AfterMatch(m.Popularity, 0, 2, false, c)));
            Assert.That(BoardExpectations.AfterMonth(50f, 1.3f, c), Is.GreaterThan(50f));

            List<BoardObjective> goals = BoardExpectations.Generate(ClubTier.Big, 15, c);
            var awful = new BoardSnapshot { Position = 15, Clubs = 15, QualifyingPlaces = 4, CashRatio = 0.3f, AverageAttendance = 0.3f };
            var great = new BoardSnapshot { Position = 1, Clubs = 15, QualifyingPlaces = 4, CashRatio = 1.2f, AverageAttendance = 0.95f, YouthMinutesShare = 0.1f };
            float bad = BoardExpectations.AfterSeason(40f, goals, awful, c);
            Assert.That(BoardExpectations.AfterSeason(40f, goals, great, c), Is.GreaterThan(40f));
            Assert.That(BoardExpectations.ShouldFire(bad, c), Is.True, "una temporada desastrosa termina en despido");
            Assert.That(goals.All(o => BoardExpectations.Progress(o, great) >= 1f), Is.True);
        }

        // ---------------------------------------------------------------- medidor de gestión

        [Test]
        public void ManagementRating_HasThreeWeightedPillars_WithAuditableFactors()
        {
            var c = new RatingConfig();
            var fair = new ManagementInputs { SupporterRelation = 80, AverageOccupancy = 0.9f, Cash = 120, StartCash = 100, WageBill = 90, WageBudget = 100, AverageMorale = 70 };
            var greedy = new ManagementInputs { SupporterRelation = 30, AveragePriceExcess = 0.4f, RequestsIgnored = 3, AverageOccupancy = 0.4f, Cash = -10, StartCash = 100,
                WageBill = 150, WageBudget = 100, AverageMorale = 30, BrokenPromises = 2 };
            ManagementRating good = ManagementRatingSystem.Compute(fair, c);
            ManagementRating bad = ManagementRatingSystem.Compute(greedy, c);
            Assert.That(good.Supporters, Is.GreaterThan(bad.Supporters));
            Assert.That(good.Squad, Is.GreaterThan(bad.Squad));
            Assert.That(good.Budget, Is.GreaterThan(bad.Budget));
            Assert.That(good.Total, Is.EqualTo(good.Supporters * 0.35f + good.Squad * 0.35f + good.Budget * 0.3f).Within(1e-3f));
            Assert.That(bad.Factors.Any(f => f.Key == "rating.price_above_ideal" && f.Points < 0), Is.True, "entradas sobre el ideal restan");
            Assert.That(bad.Factors.Any(f => f.Key == "rating.deficit"), Is.True);
            foreach (ManagementRating r in new[] { good, bad })
            {
                Assert.That(new[] { r.Supporters, r.Squad, r.Budget, r.Total }, Is.All.InRange(0f, 100f));
            }
        }

        [Test]
        public void Signings_ThatPerform_AddPoints_AndExpensiveFlopsOrBenchwarmersSubtract()
        {
            var c = new RatingConfig();
            const long value = 100_000_000_000;
            var star = new SigningRecord { Name = "Crack", Fee = 5_000_000_000, Matches = 8, RatingSum = 8 * 7.6f, Contributions = 6, TeamMatches = 8 };
            var flop = new SigningRecord { Name = "Caro", Fee = 20_000_000_000, Matches = 8, RatingSum = 8 * 5.8f, TeamMatches = 8 };
            var bench = new SigningRecord { Name = "Banco", Fee = 10_000_000_000, Matches = 0, TeamMatches = 10 };
            var fresh = new SigningRecord { Name = "Nuevo", Fee = 10_000_000_000, Matches = 1, TeamMatches = 1 };
            Assert.That(ManagementRatingSystem.SigningImpact(star, value, c), Is.GreaterThan(0f));
            Assert.That(ManagementRatingSystem.SigningImpact(flop, value, c), Is.LessThan(0f));
            Assert.That(ManagementRatingSystem.SigningImpact(bench, value, c), Is.LessThan(0f), "pagó y no juega");
            Assert.That(ManagementRatingSystem.SigningImpact(fresh, value, c), Is.Zero, "todavía no se lo juzga");
            var cheapFlop = new SigningRecord { Name = "Barato", Fee = 500_000_000, Matches = 8, RatingSum = 8 * 5.8f, TeamMatches = 8 };
            Assert.That(ManagementRatingSystem.SigningImpact(flop, value, c), Is.LessThan(ManagementRatingSystem.SigningImpact(cheapFlop, value, c)), "caro y malo resta más");

            var withStar = new ManagementInputs { ClubValue = value };
            withStar.Signings.Add(star);
            var withFlop = new ManagementInputs { ClubValue = value };
            withFlop.Signings.Add(flop);
            ManagementRating up = ManagementRatingSystem.Compute(withStar, c);
            Assert.That(up.Squad, Is.GreaterThan(ManagementRatingSystem.Compute(withFlop, c).Squad));
            Assert.That(up.Factors.Single(f => f.Key == "rating.signing").Arg, Is.EqualTo("Crack"));
        }
    }
}
