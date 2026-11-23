using System.Linq;
using Football.Core.Career;
using Football.Core.Domain;
using NUnit.Framework;

namespace Football.Core.Tests.Career
{
    /// <summary>Contratos: vencen, se renuevan (máximo 5 años) o el jugador se va libre; jugadores libres y búsqueda por nombre.</summary>
    public class CareerContractTests
    {
        private static CareerSession Start(int seed = 11) => CareerSessionTests.NewCareer(seed);

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
            while (outcome != DayOutcome.SeasonFinished && guard++ < 400);
            Assert.That(outcome, Is.EqualTo(DayOutcome.SeasonFinished));
        }

        [Test]
        public void NoContractIsLongerThanFiveYears_AndSomeExpireInTheFirstSeason()
        {
            CareerSession s = Start();
            Assert.That(s.Data.Players.Values.All(p => s.ContractYearsLeft(p) >= 1 && s.ContractYearsLeft(p) <= 5), Is.True);
            Assert.That(s.ExpiringContracts(), Is.Not.Empty, "hay contratos que vencen este año");
            Assert.That(s.Inbox.Items.Any(n => n.Key == "news.contracts_expiring"), Is.True, "el buzón avisa");
        }

        [Test]
        public void APlayerNotRenewed_LeavesFree_AndShowsUpInTheMarketAsAFreeAgent()
        {
            CareerSession s = Start();
            Player leaving = s.ExpiringContracts().First();
            int id = leaving.Id;
            PlaySeason(s);
            Player after = s.Data.Players[id];
            Assert.That(after.ClubId, Is.Not.EqualTo(s.Data.UserClubId), "se fue del club (libre, o firmó con otro que quedó corto de plantel)");
            Assert.That(s.Squad(s.Data.UserClubId).Any(p => p.Id == id), Is.False);
            if (s.IsFreeAgent(after))
            {
                Assert.That(s.Search(null, 15, 40, 1, 99, long.MaxValue, after.LastName), Has.Member(after), "el buscador lo muestra");
            }
        }

        [Test]
        public void Renewing_ExtendsTheContract_UpToFiveYears_WithARaise()
        {
            CareerSession s = Start();
            Player p = s.ExpiringContracts().First(x => s.MaxRenewalYears(x) >= 3);
            long wageBefore = p.Contract.WeeklyWage;
            Assert.That(s.Renew(p.Id, 6), Is.EqualTo(ContractOutcome.InvalidYears), "más de 5 años no existe");
            Assert.That(s.Renew(p.Id, 1), Is.EqualTo(ContractOutcome.InvalidYears), "no pasa de lo que le queda");
            Assert.That(s.Renew(p.Id, 3), Is.EqualTo(ContractOutcome.Accepted));
            Assert.That(s.ContractYearsLeft(p), Is.EqualTo(3));
            Assert.That(p.Contract.WeeklyWage, Is.GreaterThan(wageBefore));
            PlaySeason(s);
            Assert.That(s.Data.Players[p.Id].ClubId, Is.EqualTo(s.Data.UserClubId), "renovado, se queda");
            Assert.That(s.ContractYearsLeft(s.Data.Players[p.Id]), Is.EqualTo(2));
        }

        [Test]
        public void ARenewal_CanBeRefused_ByAnUnhappyPlayer_OrByTheWageBudget()
        {
            CareerSession s = Start();
            Player p = s.ExpiringContracts().First(x => s.MaxRenewalYears(x) >= 2);
            s.Data.Conditions[p.Id].Morale = 5f;
            Assert.That(s.Renew(p.Id, 2), Is.EqualTo(ContractOutcome.Unhappy));
            s.Data.Conditions[p.Id].Morale = 80f;
            long budget = s.Data.Finances[s.Data.UserClubId].WeeklyWageBudget;
            s.Data.Finances[s.Data.UserClubId].WeeklyWageBudget = 1;
            Assert.That(s.Renew(p.Id, 2), Is.EqualTo(ContractOutcome.NoBudget));
            s.Data.Finances[s.Data.UserClubId].WeeklyWageBudget = budget;
            Assert.That(s.Renew(p.Id, 2), Is.EqualTo(ContractOutcome.Accepted));
            Assert.That(s.Renew(s.Data.Players.Values.First(x => x.ClubId != s.Data.UserClubId).Id, 2), Is.EqualTo(ContractOutcome.NotAvailable), "solo se renuevan los propios");
        }

        [Test]
        public void VeteransOnlyAcceptShortContracts()
        {
            CareerSession s = Start();
            Player p = s.Squad(s.Data.UserClubId).First();
            p.BirthDate = s.Data.Today.AddYears(-36);
            p.Contract.ExpiresOn = s.ContractEnd(1);
            Assert.That(s.MaxRenewalYears(p), Is.EqualTo(1));
            Assert.That(s.Renew(p.Id, 2), Is.EqualTo(ContractOutcome.InvalidYears));
        }

        [Test]
        public void AFreeAgent_CanBeSigned_WithoutPayingAFee_IfTheBudgetAllows()
        {
            CareerSession s = Start();
            PlaySeason(s);
            Player free = s.Data.Players.Values.Where(s.IsFreeAgent).OrderByDescending(x => PlayerRatings.Overall(x.Attributes, x.Position)).FirstOrDefault();
            Assert.That(free, Is.Not.Null, "al cerrar la temporada hay jugadores libres");
            long transferBudget = s.Data.Finances[s.Data.UserClubId].TransferBudget;
            long wageBudget = s.Data.Finances[s.Data.UserClubId].WeeklyWageBudget;
            s.Data.Finances[s.Data.UserClubId].WeeklyWageBudget = 1;
            Assert.That(s.SignFreeAgent(free.Id, 3), Is.EqualTo(ContractOutcome.NoBudget));
            s.Data.Finances[s.Data.UserClubId].WeeklyWageBudget = wageBudget + s.FreeAgentWage(free);
            Assert.That(s.SignFreeAgent(free.Id, 6), Is.EqualTo(ContractOutcome.InvalidYears));
            Assert.That(s.SignFreeAgent(free.Id, 3), Is.EqualTo(ContractOutcome.Accepted));
            Assert.That(free.ClubId, Is.EqualTo(s.Data.UserClubId));
            Assert.That(s.ContractYearsLeft(free), Is.EqualTo(3));
            Assert.That(s.Data.Finances[s.Data.UserClubId].TransferBudget, Is.EqualTo(transferBudget), "no cuesta transferencia");
            Assert.That(s.SignFreeAgent(free.Id, 3), Is.EqualTo(ContractOutcome.NotAvailable), "ya no está libre");
        }

        [Test]
        public void TheMarketSearch_FindsPlayersByName_IgnoringCaseAndAccents()
        {
            CareerSession s = Start();
            Player target = s.Data.Players.Values.First(p => p.ClubId != s.Data.UserClubId && p.LastName.Any(c => c > 127));
            string accentless = CareerSession.Normalize(target.LastName);
            var found = s.Search(null, 15, 45, 1, 99, long.MaxValue, accentless.ToUpperInvariant());
            Assert.That(found, Has.Member(target));
            Assert.That(found.All(p => CareerSession.Normalize(p.FirstName + " " + p.LastName).Contains(accentless)), Is.True);
            Assert.That(s.Search(null, 15, 45, 1, 99, long.MaxValue, "zzzzqx"), Is.Empty);
            Assert.That(s.Search(null, 15, 45, 1, 99, long.MaxValue, "").Count, Is.GreaterThan(found.Count), "sin texto devuelve a todos");
        }

        [Test]
        public void AfterTheSeason_EveryComputerClubKeepsAPlayableSquad()
        {
            CareerSession s = Start();
            int min = new CareerConfig().MinAiSquadSize, max = new CareerConfig().MaxAiSquadSize;
            PlaySeason(s);
            foreach (Club c in s.Data.Clubs.Where(c => c.Id != s.Data.UserClubId))
            {
                var squad = s.Squad(c.Id);
                Assert.That(squad.Count, Is.InRange(min - 2, max), c.Name);
                Assert.That(squad.Count(p => p.Position == Position.Goalkeeper), Is.GreaterThanOrEqualTo(2), c.Name);
                Assert.That(squad.All(p => p.Contract != null && s.ContractYearsLeft(p) >= 1 && s.ContractYearsLeft(p) <= 5), Is.True, c.Name);
            }
        }

        [Test]
        public void AUserInNacionalB_PlaysHisSeason_AndSeesNoCeremonyOfAnotherDivision()
        {
            CareerSession s = CareerSession.NewGame(CareerSessionTests.Argentina(), "San Martín (San Juan)", 2027, 4, new CareerConfigs(), false, null, 20);
            PlaySeason(s);
            var ceremonies = s.TakeCeremonies();
            Assert.That(ceremonies.Select(c => c.Kind), Does.Contain(TournamentKind.NacionalB));
            Assert.That(ceremonies.Select(c => c.Kind), Does.Not.Contain(TournamentKind.Apertura));
            Assert.That(s.CompetitionHistory().Select(h => h.Kind), Does.Contain(TournamentKind.NacionalB));
        }
    }
}
