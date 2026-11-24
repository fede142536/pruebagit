using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Football.Core.Career;
using Football.Core.Domain;
using NUnit.Framework;

namespace Football.Core.Tests.Career
{
    /// <summary>Lista de transferibles, ofertas, conversación con el jugador, préstamos y recompra.</summary>
    public class TransferTests
    {
        private static CareerSession NewOpenWindow()
        {
            CareerSession s = CareerSession.NewGame(CareerSessionTests.Argentina(), "Rosario Central", 2027, 5, new CareerConfigs(), false, null, 30);
            int month = new CareerConfigs().Career.TransferWindowMonths[0];
            s.Data.Today = new DateTime(s.Data.Today.Year, month, 10);
            Assert.That(s.TransferWindowOpen, Is.True);
            return s;
        }

        private static Player UserPlayer(CareerSession s) => s.Squad(s.Data.UserClubId).OrderBy(p => p.Id).First();

        private static PendingOffer AddOffer(CareerSession s, Player p, DealKind kind, long fee, float share = 0.5f, bool listed = false, PlayerStance stance = PlayerStance.Open)
        {
            int buyer = s.Data.Clubs.First(c => c.Id != s.Data.UserClubId).Id;
            s.Data.Finances[buyer].TransferBudget = Math.Max(s.Data.Finances[buyer].TransferBudget, fee * 3);
            var o = new PendingOffer
            {
                Received = s.Data.Today, Kind = kind, WageShare = share, Listed = listed, Stance = stance,
                Offer = new TransferOffer { PlayerId = p.Id, BuyerClubId = buyer, SellerClubId = s.Data.UserClubId, Fee = kind == DealKind.Sale ? fee : 0 },
                PurchaseFee = kind == DealKind.LoanObligation ? fee : 0, MinMinutes = kind == DealKind.LoanObligation ? 100f : 0f
            };
            ((List<PendingOffer>)s.Offers).Add(o);
            return o;
        }

        [Test]
        public void Listing_SetsAndClears_AndSuggestsAPrice()
        {
            CareerSession s = NewOpenWindow();
            Player p = UserPlayer(s);
            Assert.That(s.SetListing(p.Id, true, true), Is.True);
            Assert.That(s.ListingOf(p.Id).AskingPrice, Is.GreaterThan(0));
            Assert.That(s.SetListing(p.Id, true, false, 12345), Is.True);
            Assert.That(s.ListingOf(p.Id).AskingPrice, Is.EqualTo(12345));
            Assert.That(s.SetListing(p.Id, false, false), Is.True);
            Assert.That(s.ListingOf(p.Id), Is.Null);
        }

        [Test]
        public void AcceptingASale_MovesThePlayer_PaysTheClub_AndCreatesTheBuyback()
        {
            CareerSession s = NewOpenWindow();
            Player p = UserPlayer(s);
            long before = s.Data.Finances[s.Data.UserClubId].TransferBudget;
            PendingOffer o = AddOffer(s, p, DealKind.Sale, 1_000_000);
            Assert.That(s.Accept(o, 2, 0.5f), Is.EqualTo(DealOutcome.BuybackTooLow), "la recompra no puede costar menos que el mínimo");
            Assert.That(s.Accept(o, 2, 1.5f), Is.EqualTo(DealOutcome.Done));
            Assert.That(p.ClubId, Is.EqualTo(o.Offer.BuyerClubId));
            Assert.That(s.Data.Finances[s.Data.UserClubId].TransferBudget, Is.GreaterThan(before));
            Assert.That(s.BuybackOf(p.Id).Fee, Is.EqualTo(1_500_000));
        }

        [Test]
        public void ExerciseBuyback_BringsThePlayerBack_ForTheAgreedFee()
        {
            CareerSession s = NewOpenWindow();
            Player p = UserPlayer(s);
            PendingOffer o = AddOffer(s, p, DealKind.Sale, 1_000_000);
            s.Accept(o, 2, 1.5f);
            s.Data.Finances[s.Data.UserClubId].TransferBudget = 10_000_000;
            Assert.That(s.ExerciseBuyback(p.Id), Is.EqualTo(DealOutcome.Done));
            Assert.That(p.ClubId, Is.EqualTo(s.Data.UserClubId));
            Assert.That(s.BuybackOf(p.Id), Is.Null);
        }

        [Test]
        public void RejectingAnUnlistedOffer_AffectsMoraleByStance()
        {
            CareerSession s = NewOpenWindow();
            Player p = UserPlayer(s);
            s.Data.Conditions[p.Id].Morale = 60f;
            s.Reject(AddOffer(s, p, DealKind.Sale, 1_000_000, stance: PlayerStance.WantsOut));
            Assert.That(s.Data.Conditions[p.Id].Morale, Is.LessThan(60f));
            s.Data.Conditions[p.Id].Morale = 60f;
            s.Reject(AddOffer(s, p, DealKind.Sale, 1_000_000, stance: PlayerStance.WantsStay));
            Assert.That(s.Data.Conditions[p.Id].Morale, Is.GreaterThan(60f));
        }

        [Test]
        public void KeepWithRaise_RemovesTheOffer_AndRaisesTheWage_ButNotForThoseWhoWantOut()
        {
            CareerSession s = NewOpenWindow();
            Player p = UserPlayer(s);
            s.Data.Finances[s.Data.UserClubId].WeeklyWageBudget = long.MaxValue / 4;
            long wage = p.Contract.WeeklyWage;
            Assert.That(s.KeepWithRaise(AddOffer(s, p, DealKind.Sale, 1_000_000, stance: PlayerStance.WantsOut)), Is.EqualTo(DealOutcome.WontStay));
            PendingOffer o = AddOffer(s, p, DealKind.Sale, 1_000_000, stance: PlayerStance.Open);
            Assert.That(s.KeepWithRaise(o), Is.EqualTo(DealOutcome.Done));
            Assert.That(p.Contract.WeeklyWage, Is.GreaterThan(wage));
            Assert.That(s.Offers.Contains(o), Is.False);
        }

        [Test]
        public void LoanOut_ChangesTheClub_SplitsTheWage_AndReturnsAtSeasonEnd()
        {
            CareerSession s = NewOpenWindow();
            Player p = UserPlayer(s);
            long total = s.WeeklyWages(s.Data.UserClubId);
            PendingOffer o = AddOffer(s, p, DealKind.Loan, 0, 0.5f, listed: true);
            Assert.That(s.Accept(o), Is.EqualTo(DealOutcome.Done));
            Assert.That(p.ClubId, Is.EqualTo(o.Offer.BuyerClubId));
            Assert.That(s.LoansOut().Count(), Is.EqualTo(1));
            Assert.That(s.WeeklyWages(s.Data.UserClubId), Is.LessThan(total), "el otro club paga parte del sueldo");
            Assert.That(s.WeeklyWages(o.Offer.BuyerClubId), Is.GreaterThan(0));
            Player probe = p;
            int owner = s.Data.UserClubId;
            s.ProcessLoansAtSeasonEnd();
            Assert.That(probe.ClubId, Is.EqualTo(owner));
            Assert.That(s.Data.Loans, Is.Empty);
        }

        [Test]
        public void LoanWithObligation_BuysThePlayer_OnlyIfHePlayedEnoughMinutes()
        {
            CareerSession s = NewOpenWindow();
            Player a = s.Squad(s.Data.UserClubId).OrderBy(p => p.Id).First();
            Player b = s.Squad(s.Data.UserClubId).OrderBy(p => p.Id).Skip(1).First();
            PendingOffer oa = AddOffer(s, a, DealKind.LoanObligation, 500_000, 0.5f, listed: true);
            PendingOffer ob = AddOffer(s, b, DealKind.LoanObligation, 500_000, 0.5f, listed: true);
            s.Accept(oa);
            s.Accept(ob);
            s.Data.Conditions[a.Id].SeasonMinutes = 5000f;
            s.Data.Conditions[b.Id].SeasonMinutes = 0f;
            int owner = s.Data.UserClubId;
            s.ProcessLoansAtSeasonEnd();
            Assert.That(a.ClubId, Is.EqualTo(oa.Offer.BuyerClubId), "jugó los minutos: se compra");
            Assert.That(b.ClubId, Is.EqualTo(owner), "no los jugó: vuelve");
        }

        [Test]
        public void RequestLoan_FromAnotherClub_BringsThePlayerHere_AndFailsWhenWindowIsClosed()
        {
            CareerSession s = NewOpenWindow();
            Player star = s.Data.Players.Values.Where(p => p.ClubId.HasValue && p.ClubId != s.Data.UserClubId && p.Contract != null)
                .OrderBy(p => PlayerRatings.Overall(p.Attributes, p.Position)).First(); // el peor de otro club: lo ceden
            s.Data.Finances[s.Data.UserClubId].WeeklyWageBudget = long.MaxValue / 4;
            DealOutcome r = s.RequestLoan(star.Id, 0.6f, false);
            Assert.That(r, Is.EqualTo(DealOutcome.Done).Or.EqualTo(DealOutcome.ForeignQuota));
            if (r == DealOutcome.Done)
            {
                Assert.That(star.ClubId, Is.EqualTo(s.Data.UserClubId));
                Assert.That(s.LoansIn().Count(), Is.EqualTo(1));
            }

            s.Data.Today = new DateTime(s.Data.Today.Year, 3, 1);
            Assert.That(s.TransferWindowOpen, Is.False);
            Assert.That(s.RequestLoan(UserPlayer(s).Id, 0.6f, false), Is.Not.EqualTo(DealOutcome.Done));
        }

        [Test]
        public void ListedPlayers_ReceiveOffers_OverTheWeeks()
        {
            CareerSession s = NewOpenWindow();
            foreach (Player p in s.Squad(s.Data.UserClubId))
            {
                s.SetListing(p.Id, true, true);
            }

            int guard = 0;
            while (!s.Offers.Any(o => o.Listed) && guard++ < 60)
            {
                s.Data.Today = new DateTime(s.Data.Today.Year, new CareerConfigs().Career.TransferWindowMonths[0], 6); // sigue abierto el libro
                s.AdvanceDay();
            }

            Assert.That(s.Offers.Any(o => o.Listed), Is.True);
        }

        [Test]
        public void SaveFile_KeepsListingsLoansAndBuybacks()
        {
            CareerSession s = NewOpenWindow();
            Player a = UserPlayer(s);
            Player b = s.Squad(s.Data.UserClubId).OrderBy(p => p.Id).Skip(1).First();
            s.SetListing(a.Id, true, false, 777);
            s.Accept(AddOffer(s, b, DealKind.Loan, 0, 0.7f, listed: true));
            var ms = new MemoryStream();
            CareerFile.Write(ms, s.Data);
            ms.Position = 0;
            CareerData back = CareerFile.Read(ms);
            Assert.That(back.Listings[a.Id].AskingPrice, Is.EqualTo(777));
            Assert.That(back.Loans.Single().BorrowerWageShare, Is.EqualTo(0.7f));
        }
    }
}
