using System.Linq;
using Football.Core.Career;
using Football.Core.Domain;
using NUnit.Framework;

namespace Football.Core.Tests.Career
{
    /// <summary>Los jugadores libres del archivo aparecen en el mercado desde el primer día y se pueden firmar.</summary>
    public class FreeAgentMarketTests
    {
        private static CareerSession New(bool vintage = false) =>
            CareerSession.NewGame(vintage ? CareerSessionTests.Vintage() : CareerSessionTests.Argentina(), "Boca Juniors", 2026, 5, new CareerConfigs());

        [Test]
        public void TheFileFreeAgents_AreInTheMarketFromDayOne()
        {
            CareerSession s = New();
            var free = s.Search(null, 18, 40, 1, 100, long.MaxValue).Where(p => s.IsFreeAgent(p)).ToList();
            Assert.That(free.Count, Is.GreaterThanOrEqualTo(16), "los de la lista del usuario");
            Assert.That(free.Select(p => p.LastName + p.FirstName), Is.Unique);
            Assert.That(free.Any(p => (p.FirstName + " " + p.LastName).Contains("Icardi")), Is.True);
            Assert.That(free.All(p => p.Contract == null && !p.ClubId.HasValue), Is.True);
            Assert.That(free.Select(p => p.AgeOn(s.Data.Today)), Is.All.InRange(24, 37), "con la edad del archivo");
        }

        [Test]
        public void TheOldestFreeAgents_AreNotRetiredAtStart()
        {
            CareerSession s = New();
            Player palomino = s.Data.Players.Values.First(p => p.LastName.Contains("Palomino"));
            Assert.That(s.IsFreeAgent(palomino), Is.True, "36 años: todavía juega");
        }

        [Test]
        public void AFreeAgent_CanBeSigned_WithoutATransferFee()
        {
            CareerSession s = New();
            s.Data.Finances[s.Data.UserClubId].WeeklyWageBudget = long.MaxValue / 8;
            Player p = s.Data.Players.Values.First(x => s.IsFreeAgent(x) && x.Position == Position.Striker);
            long cashBefore = s.Data.Finances[s.Data.UserClubId].Cash;
            Assert.That(s.SignFreeAgent(p.Id, 2), Is.EqualTo(ContractOutcome.Accepted));
            Assert.That(p.ClubId, Is.EqualTo(s.Data.UserClubId));
            Assert.That(s.Data.Finances[s.Data.UserClubId].Cash, Is.EqualTo(cashBefore), "sin transferencia");
        }

        [Test]
        public void VintageHasThemToo_AndTheRestOfThePlayersAreUntouched()
        {
            Assert.That(New(true).Data.Players.Values.Count(p => !p.ClubId.HasValue), Is.EqualTo(CareerSessionTests.Vintage().FreeAgents.Length));
            Assert.That(New().Data.Clubs.Count, Is.EqualTo(30));
        }
    }
}
