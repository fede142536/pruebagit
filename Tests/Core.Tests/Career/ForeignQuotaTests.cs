using System.IO;
using System.Linq;
using Football.Core.Career;
using Football.Core.Domain;
using NUnit.Framework;

namespace Football.Core.Tests.Career
{
    /// <summary>Cupo de extranjeros de la Liga Profesional: 6 con contrato por club y 5 en la planilla de cada partido.</summary>
    public class ForeignQuotaTests
    {
        private static CareerSession New() => CareerSession.NewGame(CareerSessionTests.Argentina(), "Boca Juniors", 2026, 5, new CareerConfigs());

        [Test]
        public void TheFile_HasNationalities_AndBocaStartsWithTheFullQuota()
        {
            CareerSession s = New();
            Assert.That(s.Data.Players.Values.Select(p => p.Nationality).Distinct().Count(), Is.GreaterThanOrEqualTo(4));
            Assert.That(s.ForeignCount(s.Data.UserClubId), Is.EqualTo(s.MaxForeigners));
        }

        [Test]
        public void SigningAForeigner_IsBlockedWhenTheQuotaIsFull_AndAllowedAfterSellingOne()
        {
            CareerSession s = New();
            s.Data.Finances[s.Data.UserClubId].WeeklyWageBudget = long.MaxValue / 8;
            Player foreignFree = s.Data.Players.Values.First(p => s.IsFreeAgent(p) && s.IsForeign(p));
            Assert.That(s.SignFreeAgent(foreignFree.Id, 2), Is.EqualTo(ContractOutcome.ForeignQuota), "cupo completo");
            Player argentineFree = s.Data.Players.Values.First(p => s.IsFreeAgent(p) && !s.IsForeign(p));
            Assert.That(s.SignFreeAgent(argentineFree.Id, 2), Is.EqualTo(ContractOutcome.Accepted), "los argentinos no ocupan cupo");

            Player sold = s.Squad(s.Data.UserClubId).First(p => s.IsForeign(p));
            sold.ClubId = null; // vendido
            Assert.That(s.SignFreeAgent(foreignFree.Id, 2), Is.EqualTo(ContractOutcome.Accepted), "vendiendo uno se libera un lugar");
        }

        [Test]
        public void AnOfferForAForeignPlayer_SaysTheQuotaIsFull()
        {
            CareerSession s = New();
            Player target = s.Data.Players.Values.First(p => p.ClubId.HasValue && p.ClubId != s.Data.UserClubId && s.IsForeign(p));
            s.Data.Finances[s.Data.UserClubId].TransferBudget = long.MaxValue / 8;
            Assert.That(s.CheckOffer(target.Id, 1), Is.AnyOf(OfferBlock.ForeignQuota, OfferBlock.WindowClosed));
            if (s.TransferWindowOpen)
            {
                Assert.That(s.CheckOffer(target.Id, 1), Is.EqualTo(OfferBlock.ForeignQuota));
                Assert.That(s.PayReleaseClause(target.Id), Is.False);
            }
        }

        [Test]
        public void TheMatchLineup_NeverHasMoreThanFiveForeigners()
        {
            CareerSession s = New();
            var l = s.UserLineup();
            int foreign = l.Starters.Concat(l.Bench).Count(p => s.IsForeign(p));
            Assert.That(foreign, Is.LessThanOrEqualTo(s.MaxForeignersPerMatch));

            var squad = s.Squad(s.Data.UserClubId);
            var six = squad.Where(p => s.IsForeign(p)).Take(6).Select(p => p.Id).ToList();
            var rest = squad.Where(p => !six.Contains(p.Id)).Select(p => p.Id).ToList();
            var starters = six.Concat(rest).Take(11).ToList();
            Assert.That(s.SetUserLineup(starters, new System.Collections.Generic.List<int>()), Is.False, "seis extranjeros no se pueden anotar");
        }

        [Test]
        public void Nationality_SurvivesTheBinaryFile()
        {
            CareerSession s = New();
            s.SyncData();
            var buffer = new MemoryStream();
            CareerFile.Write(buffer, s.Data);
            buffer.Position = 0;
            CareerData back = CareerFile.Read(buffer);
            Player any = s.Data.Players.Values.First(p => p.Nationality != "ARG");
            Assert.That(back.Players[any.Id].Nationality, Is.EqualTo(any.Nationality));
        }
    }
}
