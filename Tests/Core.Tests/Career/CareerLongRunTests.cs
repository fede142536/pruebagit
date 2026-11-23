using System.Linq;
using Football.Core.Career;
using Football.Core.Domain;
using NUnit.Framework;

namespace Football.Core.Tests.Career
{
    /// <summary>Varias temporadas seguidas con los 30 clubes: los equipos y las finanzas no deben degenerar.</summary>
    public class CareerLongRunTests
    {
        private const int Seasons = 5;
        private const long FloorCash = -5_000_000_000;   // ~1,5 veces la caja inicial del club más rico: más abajo sería una quiebra descontrolada
        private const int MaxClubsInDebt = 10;           // de 30

        /// <summary>Un DT que renueva a los que vencen (lo que hace una persona); si no, el plantel se vacía en pocas temporadas.</summary>
        private static void RenewTheExpiring(CareerSession s)
        {
            foreach (Player p in s.ExpiringContracts())
            {
                s.Renew(p.Id, s.MaxRenewalYears(p));
            }
        }

        private static void PlaySeason(CareerSession s)
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
            Assert.That(outcome, Is.EqualTo(DayOutcome.SeasonFinished));
        }

        [Test]
        public void FiveSeasons_KeepEverySquadPlayableAndTheFinancesBounded()
        {
            CareerSession s = CareerSessionTests.NewCareer(21);
            int cap = new CareerConfig().MaxAiSquadSize;
            for (int season = 1; season <= Seasons; season++)
            {
                RenewTheExpiring(s);
                PlaySeason(s);
                long[] cash = s.Data.Clubs.Select(c => s.Data.Finances[c.Id].Cash).ToArray();
                TestContext.Out.WriteLine($"temporada {season}: caja min/mediana/max {cash.Min():N0}/{cash.OrderBy(x => x).ElementAt(15):N0}/{cash.Max():N0}, " +
                                          $"clubes en deuda {cash.Count(x => x < 0)}, jugadores {s.Data.Players.Count}");
                foreach (Club club in s.Data.Clubs)
                {
                    var squad = s.Squad(club.Id);
                    Assert.That(squad.Count, Is.InRange(18, club.Id == s.Data.UserClubId ? 60 : cap), club.Name + " temporada " + season + ": tamaño del plantel");
                    Assert.That(squad.Count(p => p.Position == Position.Goalkeeper), Is.GreaterThanOrEqualTo(2), club.Name + ": arqueros");
                }

                Assert.That(cash.Min(), Is.GreaterThan(FloorCash), "temporada " + season + ": ningún club se funde sin control");
                Assert.That(cash.Count(x => x < 0), Is.LessThanOrEqualTo(MaxClubsInDebt), "temporada " + season + ": clubes en deuda");
            }
        }

        [Test]
        public void TheWageBillMatchesTheSizeOfTheClub()
        {
            CareerSession s = CareerSessionTests.NewCareer(21);
            CareerConfig cc = new CareerConfig();
            foreach (Club club in s.Data.Clubs)
            {
                ClubFinances fin = s.Data.Finances[club.Id];
                double expected = (double)cc.RevenuePerReputation * System.Math.Max(1, fin.Reputation - cc.RevenueFloorReputation);
                double annualWages = s.WeeklyWages(club.Id) * 52.0;
                Assert.That(annualWages / expected, Is.InRange(0.3, 1.1), club.Name + ": la masa salarial debe seguir lo que factura el club");
            }
        }
    }
}
