using System;
using System.IO;
using System.Linq;
using Football.Core.Career;
using Football.Core.Domain;
using Football.Core.MatchCore;
using NUnit.Framework;

namespace Football.Core.Tests.Career
{
    /// <summary>Reserva, subidas y bajadas de juveniles, desarrollo por minutos y moral, y límite de edad.</summary>
    public class YouthTests
    {
        private static CareerSession New() =>
            CareerSession.NewGame(CareerSessionTests.Argentina(), "Rosario Central", 2027, 5, new CareerConfigs(), false, null, 30);

        /// <summary>Un jugador del usuario convertido en juvenil de la edad dada, con la media y el potencial indicados.</summary>
        private static Player Youngster(CareerSession s, int index, int age, bool reserve, bool debuted, int rating = 50, int potential = 85)
        {
            Player p = index >= 0 ? s.FullRoster(s.Data.UserClubId).Where(x => x.Position != Position.Goalkeeper).OrderBy(x => x.Id).Skip(index).First() : NewKid(s);
            p.BirthDate = s.Data.Today.AddYears(-age).AddDays(-10);
            p.Potential = potential;
            foreach (AttributeId id in AttributeSet.All)
            {
                p.Attributes[id] = rating;
            }

            PlayerCondition c = s.Data.Conditions[p.Id];
            c.InReserve = reserve;
            c.Debuted = debuted;
            c.Morale = 60f;
            return p;
        }

        private static Player NewKid(CareerSession s)
        {
            var p = new Player
            {
                Id = s.Data.Players.Keys.Max() + 1, ClubId = s.Data.UserClubId, FirstName = "J", LastName = "Z", Position = Position.CentralMidfielder,
                Contract = new Contract { WeeklyWage = 100, ExpiresOn = new DateTime(s.Data.Season + 3, 12, 31) }
            };
            s.Data.Players[p.Id] = p;
            s.Data.Conditions[p.Id] = new PlayerCondition { PlayerId = p.Id };
            return p;
        }

        [Test]
        public void ReserveSquad_IsOutsideTheFirstTeam()
        {
            CareerSession s = New();
            Player p = Youngster(s, 0, 18, true, false);
            Assert.That(s.Squad(s.Data.UserClubId).Contains(p), Is.False);
            Assert.That(s.ReserveSquad(s.Data.UserClubId).Contains(p), Is.True);
            Assert.That(s.FullRoster(s.Data.UserClubId).Contains(p), Is.True);
            Assert.That(s.UserLineup().Starters.Contains(p), Is.False, "la reserva no entra en la alineación");
        }

        [Test]
        public void Promote_GivesAProfessionalContract_AndRespectsTheMinimumAge()
        {
            CareerSession s = New();
            Player ok = Youngster(s, 0, 17, true, false);
            long youthWage = ok.Contract.WeeklyWage = 1000;
            Assert.That(s.Promote(ok.Id), Is.EqualTo(SquadMove.Done));
            Assert.That(s.IsInReserve(ok.Id), Is.False);
            Assert.That(ok.Contract.WeeklyWage, Is.GreaterThan(youthWage));
            Assert.That(s.Promote(ok.Id), Is.EqualTo(SquadMove.AlreadyThere));
            Player kid = Youngster(s, 1, 15, true, false);
            Assert.That(s.Promote(kid.Id), Is.EqualTo(SquadMove.TooYoung));
        }

        [Test]
        public void Demote_RequiresADebut_AndAnAgeUnderTheLimit()
        {
            CareerSession s = New();
            Player debuted = Youngster(s, 0, 19, false, true);
            Player fresh = Youngster(s, 1, 19, false, false);
            Player old = Youngster(s, 2, 24, false, true);
            Assert.That(s.Demote(fresh.Id), Is.EqualTo(SquadMove.NotDebuted));
            Assert.That(s.Demote(old.Id), Is.EqualTo(SquadMove.TooOld));
            Assert.That(s.Demote(debuted.Id), Is.EqualTo(SquadMove.Done));
            Assert.That(s.IsInReserve(debuted.Id), Is.True);
            Assert.That(s.Demote(debuted.Id), Is.EqualTo(SquadMove.AlreadyThere));
        }

        [Test]
        public void Demote_IsRefused_WhenTheReserveIsFull()
        {
            CareerSession s = New();
            int max = new CareerConfigs().Career.MaxReserveSize;
            Player extra = Youngster(s, 0, 19, false, true);
            while (s.ReserveSquad(s.Data.UserClubId).Count < max)
            {
                Youngster(s, -1, 18, true, false);
            }

            Assert.That(s.Demote(extra.Id), Is.EqualTo(SquadMove.ReserveFull));
        }

        [Test]
        public void ADemotedStarter_DoesNotBreakTheCustomLineup()
        {
            CareerSession s = New();
            MatchPreparationHelper.UseAutomaticLineupWith(s, out Player starter);
            starter.BirthDate = s.Data.Today.AddYears(-19);
            Assert.That(s.Demote(starter.Id), Is.EqualTo(SquadMove.Done));
            Assert.That(s.UserLineup().Starters.Contains(starter), Is.False);
        }

        [Test]
        public void PlayingMinutesInTheReserve_DevelopThePlayer_MoreThanSittingOut()
        {
            CareerSession s = New();
            Player plays = Youngster(s, 0, 17, true, false, 80, 99);
            Player idle = Youngster(s, 1, 17, true, false, 60, 99);
            int max = s.Config().ReserveStarters + s.Config().ReserveBench;
            for (int i = 0; i < max + 2; i++)
            {
                Youngster(s, -1, 17, true, false, 70, 70); // mejores que el que no juega, peores que el titular; sin margen para crecer
            }

            float before = PlayerRatings.Overall(plays.Attributes, plays.Position);
            float idleBefore = PlayerRatings.Overall(idle.Attributes, idle.Position);
            for (int w = 0; w < 52; w++)
            {
                s.YouthWeek();
                s.Data.Today = s.Data.Today.AddDays(7); // cada semana es otra tirada
            }

            float gainPlays = PlayerRatings.Overall(plays.Attributes, plays.Position) - before;
            float gainIdle = PlayerRatings.Overall(idle.Attributes, idle.Position) - idleBefore;
            Assert.That(gainPlays, Is.GreaterThan(2f).And.LessThan(10f), "un año de rodaje sube la media unos puntos");
            Assert.That(gainIdle, Is.GreaterThan(0f).And.LessThan(gainPlays), "sin jugar se desarrolla menos");
        }

        [Test]
        public void Development_StopsAtThePotential()
        {
            CareerSession s = New();
            Player p = Youngster(s, 0, 17, true, false, 60, 62);
            for (int w = 0; w < 200; w++)
            {
                s.YouthWeek();
                s.Data.Today = s.Data.Today.AddDays(7); // cada semana es otra tirada
            }

            Assert.That(PlayerRatings.Overall(p.Attributes, p.Position), Is.LessThanOrEqualTo(62.5f));
        }

        [Test]
        public void ReserveMorale_RisesWhenPlaying_AndFallsWhenIdle()
        {
            CareerSession s = New();
            Player plays = Youngster(s, 0, 17, true, false, 90);
            Player idle = Youngster(s, 1, 17, true, false, 5);
            int max = s.Config().ReserveStarters + s.Config().ReserveBench;
            for (int i = 2; i < max + 3; i++)
            {
                Youngster(s, -1, 17, true, false, 60);
            }

            for (int w = 0; w < 20; w++)
            {
                s.YouthWeek();
                s.Data.Today = s.Data.Today.AddDays(7); // cada semana es otra tirada
            }

            Assert.That(s.Data.Conditions[plays.Id].Morale, Is.GreaterThan(60f));
            Assert.That(s.Data.Conditions[idle.Id].Morale, Is.LessThan(60f));
        }

        [Test]
        public void AgeOut_PromotesWhoExceedTheLimit_WithAProfessionalContract()
        {
            CareerSession s = New();
            int limit = s.Config().MaxReserveAge;
            Player old = Youngster(s, 0, limit + 1, true, true);
            Player young = Youngster(s, 1, limit - 1, true, true);
            s.AgeOutReserve();
            Assert.That(s.IsInReserve(old.Id), Is.False);
            Assert.That(old.ClubId, Is.EqualTo(s.Data.UserClubId));
            Assert.That(s.IsInReserve(young.Id), Is.True);
        }

        [Test]
        public void VisiblePotential_IsCloserToTheTruth_WithAGoodYouthCoach()
        {
            CareerSession s = New();
            Player p = Youngster(s, 0, 17, true, false, 50, 80);
            StaffMember coach = s.Data.Staff.Find(x => x.ClubId == s.Data.UserClubId && x.Role == StaffRole.YouthCoach);
            if (coach == null)
            {
                coach = new StaffMember { Id = 99999, ClubId = s.Data.UserClubId, Role = StaffRole.YouthCoach, Name = "E" };
                s.Data.Staff.Add(coach);
            }

            coach.Ability = 1;
            int errBad = Math.Abs(s.VisiblePotential(p) - p.Potential);
            coach.Ability = 20;
            int errGood = Math.Abs(s.VisiblePotential(p) - p.Potential);
            Assert.That(errGood, Is.LessThanOrEqualTo(errBad));
        }

        [Test]
        public void AfterASeason_TheIntakeArrivesInTheReserve_WithACheapYouthContract()
        {
            CareerSession s = New();
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
            while (outcome != DayOutcome.SeasonFinished && guard++ < 400);
            Assert.That(outcome, Is.EqualTo(DayOutcome.SeasonFinished));
            var reserve = s.ReserveSquad(s.Data.UserClubId);
            Assert.That(reserve, Is.Not.Empty, "la camada nueva queda en la reserva");
            Assert.That(reserve.All(p => !s.HasDebuted(p.Id)), Is.True);
            Assert.That(reserve.All(p => p.AgeOn(s.Data.Today) <= s.Config().MaxReserveAge), Is.True);
            Assert.That(reserve.Count, Is.LessThanOrEqualTo(s.Config().MaxReserveSize));
            Assert.That(s.Squad(s.Data.UserClubId).Any(p => reserve.Contains(p)), Is.False);
        }

        [Test]
        public void SaveFile_KeepsTheReserveState()
        {
            CareerSession s = New();
            Player p = Youngster(s, 0, 17, true, false);
            s.Data.Conditions[p.Id].DevProgress = 0.5f;
            var ms = new MemoryStream();
            CareerFile.Write(ms, s.Data);
            ms.Position = 0;
            PlayerCondition back = CareerFile.Read(ms).Conditions[p.Id];
            Assert.That(back.InReserve, Is.True);
            Assert.That(back.Debuted, Is.False);
            Assert.That(back.DevProgress, Is.EqualTo(0.5f));
        }
    }

    internal static class MatchPreparationHelper
    {
        /// <summary>Un titular de la alineación automática del usuario.</summary>
        public static void UseAutomaticLineupWith(CareerSession s, out Player starter)
        {
            s.ClearUserLineup();
            starter = s.UserLineup().Starters.First(p => p.Position != Position.Goalkeeper);
        }

        /// <summary>Configuración de carrera por defecto (atajo de las pruebas).</summary>
        public static CareerConfig Config(this CareerSession s) => new CareerConfigs().Career;
    }
}

namespace Football.Core.Tests.Career
{
    using Football.Core.Management;

    /// <summary>Declive de los veteranos, retiro anunciado y estadísticas de la reserva.</summary>
    public class VeteranTests
    {
        private static CareerSession New() =>
            CareerSession.NewGame(CareerSessionTests.Argentina(), "Rosario Central", 2027, 5, new CareerConfigs(), false, null, 30);

        private static Player Old(CareerSession s, int index, int age)
        {
            Player p = s.FullRoster(s.Data.UserClubId).Where(x => x.Position == Position.CentralMidfielder || x.Position == Position.Striker)
                .OrderBy(x => x.Id).Skip(index).First();
            p.BirthDate = s.Data.Today.AddYears(-age).AddDays(-10);
            foreach (AttributeId id in AttributeSet.All)
            {
                p.Attributes[id] = 70;
            }

            return p;
        }

        [Test]
        public void Decline_NeverStartsBeforeThirty_AndStartsAtADifferentAgePerPlayer()
        {
            var c = new CareerConfig();
            var d = new DeclineSettings
            {
                MinAge = c.DeclineMinAge, OnsetSpread = c.DeclineOnsetSpread, PointsPerWeek = c.DeclinePointsPerWeek, Acceleration = c.DeclineAcceleration,
                SpeedRange = c.DeclineSpeedRange, WeeklyNoise = c.DeclineWeeklyNoise, PhysicalShare = c.DeclinePhysicalShare,
                GoalkeeperFactor = c.DeclineGoalkeeperFactor, ProfessionalFactor = c.DeclineProfessionalFactor
            };
            var onsets = Enumerable.Range(1, 200).Select(id => YouthDevelopment.OnsetAge(d, id)).ToList();
            Assert.That(onsets.Min(), Is.EqualTo(30));
            Assert.That(onsets.Max(), Is.EqualTo(30 + c.DeclineOnsetSpread - 1));
            Assert.That(onsets.Distinct().Count(), Is.GreaterThan(1), "no es la misma edad para todos");
            var rng = new Football.Core.Common.SeededRandom(1);
            Assert.That(YouthDevelopment.WeeklyDecline(d, 1, 29, Position.Striker, false, rng), Is.EqualTo(0f));
        }

        [Test]
        public void AVeteran_LosesLevelOverTheYears_MoreThanAPlayerInHisPrime()
        {
            CareerSession s = New();
            Player vet = Old(s, 0, 35);
            Player prime = Old(s, 1, 26);
            s.Data.Conditions[prime.Id].Form = 6f + (70f - 65f) * 0.03f; // rinde lo que se espera de su nivel
            float vetBefore = PlayerRatings.Overall(vet.Attributes, vet.Position), primeBefore = PlayerRatings.Overall(prime.Attributes, prime.Position);
            for (int w = 0; w < 52; w++)
            {
                s.YouthWeek();
                s.Data.Today = s.Data.Today.AddDays(7); // cada semana es otra tirada
            }

            Assert.That(vetBefore - PlayerRatings.Overall(vet.Attributes, vet.Position), Is.GreaterThan(0.5f).And.LessThan(8f));
            Assert.That(primeBefore - PlayerRatings.Overall(prime.Attributes, prime.Position), Is.EqualTo(0f));
        }

        [Test]
        public void Retirement_IsAnnouncedAtTheStartOfTheSeason_AndHappensAtTheEnd()
        {
            CareerSession s = New();
            Player old = Old(s, 0, 40);
            s.Data.Conditions[old.Id].RetiringAtEnd = false;
            s.AnnounceRetirements();
            Assert.That(s.IsRetiringThisSeason(old.Id), Is.True, "con 40 años el retiro está casi asegurado");
            Assert.That(s.Inbox.Chronological.Any(n => n.Key == "news.last_season"), Is.True, "el buzón avisa que es su última temporada");
            Player young = Old(s, 1, 25);
            s.AnnounceRetirements();
            Assert.That(s.IsRetiringThisSeason(young.Id), Is.False);
        }

        [Test]
        public void ReserveMatches_AccumulateStatsForThosePlaying()
        {
            CareerSession s = New();
            Player p = s.FullRoster(s.Data.UserClubId).First(x => x.Position == Position.Striker);
            p.BirthDate = s.Data.Today.AddYears(-18);
            s.Data.Conditions[p.Id].InReserve = true;
            for (int w = 0; w < 52; w++)
            {
                s.YouthWeek();
                s.Data.Today = s.Data.Today.AddDays(7); // cada semana es otra tirada
            }

            CardStats st = s.ReserveStatsOf(p.Id);
            Assert.That(st, Is.Not.Null);
            Assert.That(st.Matches, Is.InRange(15, 52));
            Assert.That(st.AverageRating, Is.InRange(4f, 9.5f));
        }

        [Test]
        public void SaveFile_KeepsRetirementAndReserveStats()
        {
            CareerSession s = New();
            Player p = s.FullRoster(s.Data.UserClubId).First();
            s.Data.Conditions[p.Id].RetiringAtEnd = true;
            s.Data.ReserveStats[p.Id] = new CardStats { Matches = 7, Goals = 3, Assists = 2, RatingSum = 40f, Rated = 6 };
            var ms = new MemoryStream();
            CareerFile.Write(ms, s.Data);
            ms.Position = 0;
            CareerData back = CareerFile.Read(ms);
            Assert.That(back.Conditions[p.Id].RetiringAtEnd, Is.True);
            Assert.That(back.ReserveStats[p.Id].Goals, Is.EqualTo(3));
        }
    }
}
