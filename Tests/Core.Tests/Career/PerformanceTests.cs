using System;
using System.IO;
using System.Linq;
using Football.Core.Career;
using Football.Core.Domain;
using Football.Core.Management;
using NUnit.Framework;

namespace Football.Core.Tests.Career
{
    /// <summary>La media sube o baja de a poco según el rendimiento y la edad, durante toda la carrera.</summary>
    public class PerformanceTests
    {
        private static CareerSession New() =>
            CareerSession.NewGame(CareerSessionTests.Argentina(), "Rosario Central", 2027, 5, new CareerConfigs(), false, null, 30);

        private static Player Make(CareerSession s, int index, int age, float form, int rating = 70, int potential = 70)
        {
            Player p = s.FullRoster(s.Data.UserClubId).Where(x => x.Position == Position.CentralMidfielder || x.Position == Position.Striker).OrderBy(x => x.Id).Skip(index).First();
            p.BirthDate = s.Data.Today.AddYears(-age).AddDays(-10);
            p.Potential = potential;
            p.Personality = PersonalityTrait.Balanced;
            foreach (AttributeId id in AttributeSet.All)
            {
                p.Attributes[id] = rating;
            }

            PlayerCondition c = s.Data.Conditions[p.Id];
            c.Form = form;
            c.Morale = 60f;
            c.InReserve = false;
            return p;
        }

        private static float Year(CareerSession s, Player p)
        {
            float before = PlayerRatings.Overall(p.Attributes, p.Position);
            for (int w = 0; w < 52; w++)
            {
                s.YouthWeek();
                s.Data.Today = s.Data.Today.AddDays(7);
            }

            return PlayerRatings.Overall(p.Attributes, p.Position) - before;
        }

        [Test]
        public void PerformanceIndex_IsZeroWhenThePlayerMeetsTheExpectation_AndRisesWithBetterMarks()
        {
            var c = new CareerConfig();
            float Index(float form, float overall) => YouthDevelopment.PerformanceIndex(form, overall, c.ExpectedRatingBase, c.ExpectedRatingPerOverall, c.ExpectedRatingPivot, c.PerformanceScale);
            Assert.That(Index(6f, 65f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(Index(6.6f, 85f), Is.EqualTo(0f).Within(1e-4f), "a los mejores se les pide más");
            Assert.That(Index(8f, 65f), Is.EqualTo(1f));
            Assert.That(Index(4f, 65f), Is.EqualTo(-1f));
            Assert.That(Index(7f, 65f), Is.GreaterThan(Index(6.5f, 65f)));
        }

        [Test]
        public void APlayerInHisPrime_ImprovesSlowlyWhenHePlaysWell_AndWorsensSlowlyWhenHePlaysBadly_AndHoldsWhenAsExpected()
        {
            CareerSession good = New(), bad = New(), neutral = New();
            float up = Year(good, Make(good, 0, 27, 8f)), down = Year(bad, Make(bad, 0, 27, 4f)), flat = Year(neutral, Make(neutral, 0, 27, 6.0f + (70f - 65f) * 0.03f));
            Assert.That(up, Is.InRange(0.5f, 4f), "mejora, pero no de golpe");
            Assert.That(down, Is.InRange(-4f, -0.5f), "empeora, pero no de golpe");
            Assert.That(Math.Abs(flat), Is.LessThan(0.5f), "si rinde lo esperado se mantiene");
        }

        [Test]
        public void TheImprovementOfAMaturePlayer_HasACeilingNearHisPotential()
        {
            CareerSession s = New();
            Player p = Make(s, 0, 27, 9f, 70, 70);
            for (int year = 0; year < 5; year++)
            {
                Year(s, p);
            }

            Assert.That(PlayerRatings.Overall(p.Attributes, p.Position), Is.LessThanOrEqualTo(70f + new CareerConfig().DriftOverPotential + 0.5f));
        }

        [Test]
        public void AYoungPlayerGrowsFaster_WhenHePlaysWell()
        {
            CareerSession good = New(), bad = New();
            float up = Year(good, Make(good, 0, 19, 8f, 55, 85)), down = Year(bad, Make(bad, 0, 19, 4f, 55, 85));
            Assert.That(up, Is.GreaterThan(down + 0.5f));
            Assert.That(down, Is.GreaterThan(0f), "aunque rinda mal, un joven no retrocede: crece más despacio");
        }

        [Test]
        public void AVeteranDeclinesLess_WhenHeKeepsPlayingWell()
        {
            CareerSession good = New(), bad = New();
            float well = Year(good, Make(good, 0, 35, 8f)), poorly = Year(bad, Make(bad, 0, 35, 4f));
            Assert.That(well, Is.GreaterThan(poorly));
            Assert.That(poorly, Is.LessThan(0f));
        }

        [Test]
        public void TheSeasonStartRating_IsRecorded_AndTheChangeIsReported()
        {
            CareerSession s = New();
            Player p = s.Squad(s.Data.UserClubId).First();
            Assert.That(s.Data.Conditions[p.Id].SeasonStartRating, Is.GreaterThan(0));
            Assert.That(s.RatingChange(p), Is.EqualTo(0));
            foreach (AttributeId id in AttributeSet.All)
            {
                p.Attributes[id] = Math.Min(AttributeScale.Max, p.Attributes[id] + 5);
            }

            Assert.That(s.RatingChange(p), Is.GreaterThan(0));
            var ms = new MemoryStream();
            CareerFile.Write(ms, s.Data);
            ms.Position = 0;
            Assert.That(CareerFile.Read(ms).Conditions[p.Id].SeasonStartRating, Is.EqualTo(s.Data.Conditions[p.Id].SeasonStartRating));
        }
    }
}

namespace Football.Core.Tests.Career
{
    /// <summary>Convocatoria de emergencia de la reserva cuando faltan jugadores en primera.</summary>
    public class EmergencyCallUpTests
    {
        [Test]
        public void WhenTheFirstTeamIsShort_TheBestOfTheReserveAreCalledUp_WithoutLeavingTheReserve()
        {
            CareerSession s = CareerSession.NewGame(CareerSessionTests.Argentina(), "Rosario Central", 2027, 5, new CareerConfigs(), false, null, 30);
            var first = s.Squad(s.Data.UserClubId);
            foreach (Player p in first.Skip(10))
            {
                s.Data.Conditions[p.Id].InReserve = true; // quedan solo 10 en primera
            }

            var kids = s.ReserveSquad(s.Data.UserClubId);
            Assert.That(kids.Count, Is.GreaterThan(5));
            var squad = s.MatchSquad(s.Data.UserClubId);
            Assert.That(squad.Count(p => s.Data.Conditions[p.Id].Available), Is.GreaterThanOrEqualTo(new CareerConfig().EmergencyCallUpTarget));
            Assert.That(s.UserLineup().Starters.Count, Is.EqualTo(11));
            Assert.That(s.IsInReserve(kids[0].Id), Is.True);
        }
    }
}
