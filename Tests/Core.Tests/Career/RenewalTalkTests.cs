using System.IO;
using System.Linq;
using Football.Core.Career;
using Football.Core.Domain;
using Football.Core.Persistence;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace Football.Core.Tests.Career
{
    /// <summary>Renovaciones negociadas: pretensiones, tiempo para pensar, contraofertas y salida libre.</summary>
    public class RenewalTalkTests
    {
        private static readonly CareerConfig Cfg = new CareerConfigs().Career;

        private static CareerSession New(int seed = 11)
        {
            CareerSession s = CareerSession.NewGame(CareerSessionTests.Argentina(), "Boca Juniors", 2027, seed, new CareerConfigs());
            s.Data.Finances[s.Data.UserClubId].WeeklyWageBudget = long.MaxValue / 8;
            return s;
        }

        private static Player Expiring(CareerSession s)
        {
            Player p = s.Squad(s.Data.UserClubId).First(x => x.AgeOn(s.Data.Today) is >= 24 and <= 28);
            p.Contract.ExpiresOn = new System.DateTime(s.Data.Season, 12, 31); // le queda esta temporada
            return p;
        }

        private static void Days(CareerSession s, int days)
        {
            for (int i = 0; i < days; i++)
            {
                s.Data.Manager.Popularity = 100f;
                if (s.AdvanceDay() == DayOutcome.UserMatchToday)
                {
                    s.SimulateUserMatch();
                }
            }
        }

        [Test]
        public void ThePlayerAsksForMoreThanHeEarns_AndForYears()
        {
            CareerSession s = New();
            Player p = Expiring(s);
            RenewalDemand d = s.DemandOf(p);
            Assert.That(d.Wage, Is.GreaterThanOrEqualTo(p.Contract.WeeklyWage), "pide aumento");
            Assert.That(d.Years, Is.InRange(1, 5));
        }

        [Test]
        public void TheAnswerIsNotImmediate_ItComesAfterSomeDays()
        {
            CareerSession s = New();
            Player p = Expiring(s);
            RenewalDemand d = s.DemandOf(p);
            Assert.That(s.SendRenewalOffer(p.Id, d.Wage * 2, 5, PlayingTimePromise.Star), Is.EqualTo(ContractOutcome.Accepted));
            RenewalTalk talk = s.TalkOf(p.Id);
            Assert.That(talk.Status, Is.EqualTo(TalkStatus.Thinking));
            Assert.That((talk.ReplyOn - talk.SentOn).TotalDays, Is.InRange(Cfg.RenewalThinkDays[0], Cfg.RenewalThinkDays[1]));
            Assert.That(p.Contract.ExpiresOn.Year, Is.EqualTo(s.Data.Season), "todavía no firmó");
            Assert.That(s.SendRenewalOffer(p.Id, d.Wage * 2, 5, PlayingTimePromise.Star), Is.EqualTo(ContractOutcome.NotAvailable), "está pensando la oferta anterior");
        }

        [Test]
        public void AGenerousOffer_IsAcceptedWhenTheDayArrives()
        {
            CareerSession s = New();
            Player p = Expiring(s);
            RenewalDemand d = s.DemandOf(p);
            int years = System.Math.Min(5, System.Math.Max(d.Years, s.ContractYearsLeft(p) + 1));
            Assert.That(years <= s.MaxRenewalYears(p));
            Assert.That(s.SendRenewalOffer(p.Id, d.Wage * 2, years, PlayingTimePromise.Star), Is.EqualTo(ContractOutcome.Accepted));
            s.Data.Conditions[p.Id].Morale = 80f;
            Days(s, 16);
            Assert.That(s.TalkOf(p.Id).Status, Is.EqualTo(TalkStatus.Accepted));
            Assert.That(p.Contract.WeeklyWage, Is.EqualTo(d.Wage * 2));
            Assert.That(p.Contract.ExpiresOn.Year, Is.EqualTo(s.Data.Season + years - 1));
        }

        [Test]
        public void ALowOffer_GetsACounter_ThenARefusalAfterTheRounds()
        {
            CareerSession s = New();
            Player p = Expiring(s);
            s.Data.Conditions[p.Id].Morale = 80f;
            long low = p.Contract.WeeklyWage / 3;
            int rounds = Cfg.RenewalMaxRounds;
            for (int r = 1; r <= rounds; r++)
            {
                Assert.That(s.SendRenewalOffer(p.Id, low, 2, PlayingTimePromise.Regular), Is.EqualTo(ContractOutcome.Accepted), "ronda " + r);
                Days(s, 16);
                s.Data.Conditions[p.Id].Morale = 80f;
                TalkStatus expected = r < rounds ? TalkStatus.Counter : TalkStatus.Refused;
                Assert.That(s.TalkOf(p.Id).Status, Is.EqualTo(expected), "ronda " + r);
                Assert.That(s.TalkOf(p.Id).Reason, Is.EqualTo(TalkReason.Wage));
                Assert.That(s.TalkOf(p.Id).DemandWage, Is.GreaterThan(low));
            }

            Assert.That(s.SendRenewalOffer(p.Id, s.DemandOf(p).Wage * 3, 3, PlayingTimePromise.Star), Is.EqualTo(ContractOutcome.Unhappy), "ya dijo que no");
        }

        [Test]
        public void AnUnhappyPlayer_RefusesAtOnce()
        {
            CareerSession s = New();
            Player p = Expiring(s);
            Assert.That(s.SendRenewalOffer(p.Id, s.DemandOf(p).Wage * 3, 3, PlayingTimePromise.Star), Is.EqualTo(ContractOutcome.Accepted));
            Days(s, 1);
            s.Data.Conditions[p.Id].Morale = 5f;
            Days(s, 16);
            Assert.That(s.TalkOf(p.Id).Status, Is.EqualTo(TalkStatus.Refused));
            Assert.That(s.TalkOf(p.Id).Reason, Is.EqualTo(TalkReason.Unhappy));
        }

        [Test]
        public void AStarterWhoDoesNotPlay_DemandsAPlayingTimePromise()
        {
            CareerSession s = New();
            var squad = s.Squad(s.Data.UserClubId);
            foreach (Player x in squad)
            {
                s.Data.Conditions[x.Id].SeasonMinutes = 900f;
            }

            Player p = squad.OrderByDescending(x => Football.Core.MatchCore.RosterFactory.DisplayRating(x.Attributes, x.Position)).First();
            p.Contract.ExpiresOn = new System.DateTime(s.Data.Season, 12, 31);
            s.Data.Conditions[p.Id].SeasonMinutes = 100f;
            Assert.That(s.DemandOf(p).WantsPlayingTime, Is.True);
            s.Data.Conditions[p.Id].Morale = 80f;
            RenewalDemand d = s.DemandOf(p);
            int years = System.Math.Min(s.MaxRenewalYears(p), System.Math.Max(d.Years, s.ContractYearsLeft(p) + 1));
            s.SendRenewalOffer(p.Id, d.Wage * 2, years, PlayingTimePromise.Backup);
            Days(s, 16);
            Assert.That(s.TalkOf(p.Id).Reason, Is.EqualTo(TalkReason.PlayingTime), "con plata sola no alcanza");
        }

        [Test]
        public void ThePlayerWeighsWageYearsAndRole_TogetherNotOneByOne()
        {
            CareerSession s = New();
            Player p = Expiring(s);
            s.Data.Conditions[p.Id].Morale = 80f;
            RenewalDemand d = s.DemandOf(p);
            int shortYears = System.Math.Max(s.ContractYearsLeft(p) + 1, d.Years - 2);
            Assert.That(shortYears, Is.LessThanOrEqualTo(s.MaxRenewalYears(p)));
            PlayingTimePromise same = d.Role, better = d.Role == PlayingTimePromise.Star ? PlayingTimePromise.Star : PlayingTimePromise.Star;
            float justRight = s.Satisfaction(p, d.Wage, d.Years, d.Role, out _);
            Assert.That(justRight, Is.EqualTo(0f).Within(0.01f), "lo que pide: conforme");
            float fewerYears = s.Satisfaction(p, d.Wage, shortYears, same, out TalkReason why);
            if (shortYears < d.Years)
            {
                Assert.That(fewerYears, Is.LessThan(0f), "mismo sueldo y menos años: menos conforme");
                Assert.That(why, Is.EqualTo(TalkReason.Years));
            }

            if (d.Role != PlayingTimePromise.Star)
            {
                float lessWageButStar = s.Satisfaction(p, (long)(d.Wage * 0.9), d.Years, better, out _);
                Assert.That(lessWageButStar, Is.GreaterThan(s.Satisfaction(p, (long)(d.Wage * 0.9), d.Years, d.Role, out _)), "un rol más importante compensa");
            }

            float moreWageSameRole = s.Satisfaction(p, d.Wage * 2, d.Years, d.Role, out _);
            Assert.That(moreWageSameRole, Is.LessThanOrEqualTo(Cfg.RenewalWageBonusCap * 1.1f + 0.001f), "pagar el doble no compra todo");
        }

        [Test]
        public void AcceptingTheirDemand_SignsAtOnce()
        {
            CareerSession s = New();
            Player p = Expiring(s);
            s.Data.Conditions[p.Id].Morale = 80f;
            long low = p.Contract.WeeklyWage / 3;
            s.SendRenewalOffer(p.Id, low, s.ContractYearsLeft(p) + 1, PlayingTimePromise.Backup);
            Days(s, 16);
            RenewalTalk talk = s.TalkOf(p.Id);
            Assert.That(talk.Status, Is.EqualTo(TalkStatus.Counter));
            Assert.That(s.AcceptCounter(p.Id), Is.EqualTo(ContractOutcome.Accepted));
            Assert.That(p.Contract.WeeklyWage, Is.EqualTo(talk.DemandWage));
            Assert.That(s.TalkOf(p.Id).Status, Is.EqualTo(TalkStatus.Accepted));
            Assert.That(s.AcceptCounter(p.Id), Is.EqualTo(ContractOutcome.NotAvailable));
        }

        [Test]
        public void ATalk_SurvivesTheBinaryFileAndSqlite()
        {
            CareerSession s = New();
            Player p = Expiring(s);
            s.SendRenewalOffer(p.Id, s.DemandOf(p).Wage, s.ContractYearsLeft(p) + 1, PlayingTimePromise.Regular);
            s.SyncData();
            var buffer = new MemoryStream();
            CareerFile.Write(buffer, s.Data);
            buffer.Position = 0;
            RenewalTalk a = CareerFile.Read(buffer).Talks[p.Id], expected = s.TalkOf(p.Id);
            Assert.That(a.OfferWage, Is.EqualTo(expected.OfferWage));
            Assert.That(a.ReplyOn, Is.EqualTo(expected.ReplyOn));
            using (var db = new SqliteConnection("Data Source=:memory:"))
            {
                db.Open();
                SqliteConnectionSetup.Apply(db);
                Migrator.Default.MigrateToLatest(db);
                var repo = new SqlCareerRepository(db);
                repo.Save(s.Data);
                RenewalTalk b = repo.Load().Talks[p.Id];
                Assert.That(b.OfferYears, Is.EqualTo(expected.OfferYears));
                Assert.That(b.Status, Is.EqualTo(TalkStatus.Thinking));
            }
        }
    }
}
