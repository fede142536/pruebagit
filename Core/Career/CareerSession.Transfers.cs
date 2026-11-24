using System;
using System.Collections.Generic;
using System.Linq;
using Football.Core.Domain;
using Football.Core.Management;

namespace Football.Core.Career
{
    // Lista de transferibles y cedibles, ofertas de otros clubes (por los de la lista y por los que no lo están), la conversación con el jugador,
    // préstamos (los que cedés y los que pedís), compra obligatoria y cláusula de recompra.
    public sealed partial class CareerSession
    {
        // ------------------------------------------------------------------ lista de transferibles y cedibles

        /// <summary>Fracción del sueldo que paga el usuario al pedir un préstamo desde el mercado.</summary>
        public float LoanRequestWageShare => _cfg.Career.LoanWageShareMax;

        /// <summary>Datos de un jugador en la lista (null si no está).</summary>
        /// <param name="playerId">Jugador.</param>
        public TransferListing ListingOf(int playerId) => Data.Listings.TryGetValue(playerId, out TransferListing l) ? l : null;

        /// <summary>Precio que se pide por defecto: su valor de mercado por <see cref="CareerConfig.ListedAskFactor"/>.</summary>
        /// <param name="p">Jugador.</param>
        public long SuggestedAsk(Player p) => (long)(_market.Value(p, Data.Today) * (double)_cfg.Career.ListedAskFactor);

        /// <summary>
        /// Pone o saca a un jugador del club de la lista: transferible (venta), cedible (préstamo) o los dos. Con los dos en falso se saca.
        /// </summary>
        /// <param name="playerId">Jugador del usuario.</param>
        /// <param name="forSale">Se acepta venderlo.</param>
        /// <param name="forLoan">Se acepta cederlo a préstamo.</param>
        /// <param name="askingPrice">Precio que se pide por la venta; 0 = el sugerido (o el que ya tenía).</param>
        /// <returns>Falso si el jugador no es del plantel del usuario o está prestado.</returns>
        public bool SetListing(int playerId, bool forSale, bool forLoan, long askingPrice = 0)
        {
            if (!Data.Players.TryGetValue(playerId, out Player p) || p.ClubId != Data.UserClubId || LoanOf(playerId) != null)
            {
                return false;
            }

            if (!forSale && !forLoan)
            {
                Data.Listings.Remove(playerId);
                return true;
            }

            TransferListing l = ListingOf(playerId);
            if (l == null)
            {
                l = new TransferListing { PlayerId = playerId, Date = Data.Today };
                Data.Listings[playerId] = l;
            }

            l.ForSale = forSale;
            l.ForLoan = forLoan;
            l.AskingPrice = askingPrice > 0 ? askingPrice : l.AskingPrice > 0 ? l.AskingPrice : SuggestedAsk(p);
            _offers.RemoveAll(o => o.Offer.PlayerId == playerId && !o.Listed); // lo que llegó antes ya no es lo mismo
            return true;
        }

        // ------------------------------------------------------------------ ofertas que llegan

        private void RivalOffers()
        {
            if (!TransferWindowOpen)
            {
                _offers.Clear(); // al cerrarse el libro, las ofertas pendientes se pierden
                return;
            }

            CareerConfig cc = _cfg.Career;
            // Primero, los de la lista: cada uno puede recibir una oferta por semana (más probable que por los demás).
            foreach (TransferListing l in Data.Listings.Values.OrderBy(x => x.PlayerId).ToList())
            {
                if (!_dayRng.Chance(cc.ListedOfferChancePerWeek) || _offers.Any(o => o.Offer.PlayerId == l.PlayerId))
                {
                    continue;
                }

                if (Data.Players.TryGetValue(l.PlayerId, out Player listed) && listed.ClubId == Data.UserClubId)
                {
                    MakeOfferFor(listed, l);
                }
            }

            // Después, los que no están en la lista: un club cualquiera se fija en el que más le conviene.
            if (!_dayRng.Chance(cc.RivalOfferChancePerWeek))
            {
                return;
            }

            long budgetNeeded = Squad(Data.UserClubId).Where(p => !Data.Listings.ContainsKey(p.Id) && LoanOf(p.Id) == null)
                .Select(p => (long)(_market.Value(p, Data.Today) * 0.6)).DefaultIfEmpty(long.MaxValue).Min();
            List<Club> others = Data.Clubs.Where(c => c.Id != Data.UserClubId && Data.Finances[c.Id].TransferBudget >= budgetNeeded).ToList();
            if (others.Count == 0)
            {
                return;
            }

            Club buyer = others[_dayRng.NextInt(0, others.Count)];
            long budget = Data.Finances[buyer.Id].TransferBudget;
            IEnumerable<Player> candidates = Squad(Data.UserClubId)
                .Where(p => !Data.Listings.ContainsKey(p.Id) && LoanOf(p.Id) == null && _offers.All(o => o.Offer.PlayerId != p.Id) && _market.Value(p, Data.Today) * 0.6 <= budget);
            Player target = TransferMarket.AiTarget(ProfileOf(buyer.Id), candidates, Data.Today);
            if (target == null)
            {
                return;
            }

            float factor = Factor(cc.UnlistedOfferMean, cc.UnlistedOfferSpread, buyer.Id);
            long fee = (long)(_market.Value(target, Data.Today) * factor);
            if (fee > budget)
            {
                return;
            }

            var offer = new PendingOffer
            {
                Received = Data.Today, Kind = DealKind.Sale, Stance = StanceFor(target, buyer.Id),
                Offer = new TransferOffer { PlayerId = target.Id, BuyerClubId = buyer.Id, SellerClubId = Data.UserClubId, Fee = fee }
            };
            _offers.Add(offer);
            _inbox.Post("news.offer_received", buyer.Name, FullName(target), Num(fee));
        }

        /// <summary>Oferta de un club (que puede pagarla) por un jugador de la lista: compra o préstamo según lo que se aceptó.</summary>
        private void MakeOfferFor(Player p, TransferListing l)
        {
            CareerConfig cc = _cfg.Career;
            bool loan = l.ForLoan && (!l.ForSale || _dayRng.Chance(cc.ListedLoanShare));
            long value = _market.Value(p, Data.Today);
            List<Club> candidates = Data.Clubs.Where(c => c.Id != Data.UserClubId && (loan || Data.Finances[c.Id].TransferBudget >= value * 0.6)).ToList();
            if (candidates.Count == 0)
            {
                return;
            }

            Club buyer = candidates[_dayRng.NextInt(0, candidates.Count)];
            float factor = Factor(cc.ListedOfferMean, cc.ListedOfferSpread, buyer.Id);
            var offer = new PendingOffer { Received = Data.Today, Listed = true, Stance = PlayerStance.Open };
            long fee = (long)(l.AskingPrice * factor);
            if (loan)
            {
                bool obligation = _dayRng.Chance(cc.LoanObligationChance);
                long purchase = (long)(value * Factor(cc.UnlistedOfferMean, cc.UnlistedOfferSpread * 0.5f, buyer.Id));
                if (obligation && Data.Finances[buyer.Id].TransferBudget < purchase)
                {
                    obligation = false;
                }

                offer.Kind = obligation ? DealKind.LoanObligation : DealKind.Loan;
                offer.WageShare = (float)Math.Round(cc.LoanWageShareMin + _dayRng.NextFloat() * (cc.LoanWageShareMax - cc.LoanWageShareMin), 1);
                offer.PurchaseFee = obligation ? purchase : 0;
                offer.MinMinutes = obligation ? cc.LoanMinMinutes : 0f;
                fee = 0;
            }
            else
            {
                if (Data.Finances[buyer.Id].TransferBudget < fee)
                {
                    return;
                }

                offer.Kind = DealKind.Sale;
            }

            offer.Offer = new TransferOffer { PlayerId = p.Id, BuyerClubId = buyer.Id, SellerClubId = Data.UserClubId, Fee = fee };
            _offers.Add(offer);
            if (offer.Kind == DealKind.Sale)
            {
                _inbox.Post("news.offer_received", buyer.Name, FullName(p), Num(fee));
            }
            else
            {
                _inbox.Post(offer.Kind == DealKind.Loan ? "news.loan_offer" : "news.loan_offer_obligation", buyer.Name, FullName(p), Num((long)Math.Round(offer.WageShare * 100)),
                    Num(offer.PurchaseFee));
            }
        }

        /// <summary>Múltiplo que ofrece un club: media de su perfil más o menos la mitad de la dispersión (fijo por club, semana y jugador).</summary>
        private float Factor(float[] meanByProfile, float spread, int clubId)
        {
            float mean = meanByProfile[(int)ProfileOf(clubId)];
            return Math.Max(0.5f, mean + (_dayRng.NextFloat() - 0.5f) * spread);
        }

        // ------------------------------------------------------------------ qué piensa el jugador

        private int RankInSquad(Player p)
        {
            float mine = PlayerRatings.Overall(p.Attributes, p.Position);
            return Squad(Data.UserClubId).Count(q => q.Id != p.Id && PlayerRatings.Overall(q.Attributes, q.Position) > mine);
        }

        /// <summary>
        /// Qué quiere un jugador ante una oferta de otro club: se quiere ir si no es titular, si el club que lo quiere es más grande o si es ambicioso;
        /// se quiere quedar si es líder, leal o está contento. Los que están entre medio piden una mejora de contrato. Determinista por jugador y temporada.
        /// </summary>
        /// <param name="p">Jugador.</param>
        /// <param name="buyerClubId">Club que ofrece.</param>
        public PlayerStance StanceFor(Player p, int buyerClubId)
        {
            CareerConfig cc = _cfg.Career;
            float gap = (Data.Finances[buyerClubId].Reputation - Data.Finances[Data.UserClubId].Reputation) / 20f;
            float desire = 0.35f * Math.Max(-1f, Math.Min(1f, gap));
            desire += RankInSquad(p) >= cc.StarterRank ? 0.45f : 0f;
            desire += p.Personality == PersonalityTrait.Ambitious ? 0.25f : p.Personality == PersonalityTrait.Loyal ? -0.3f : p.Personality == PersonalityTrait.Leader ? -0.2f : 0f;
            desire += p.AgeOn(Data.Today) >= 31 ? 0.1f : 0f;
            if (Data.Conditions.TryGetValue(p.Id, out PlayerCondition c))
            {
                desire += c.Morale >= 70f ? -0.2f : c.Morale <= 35f ? 0.25f : 0f;
            }

            desire += ((float)new Common.SeededRandom(Mix(p.Id * 977L + Data.Season * 31L + buyerClubId)).NextFloat() - 0.5f) * 0.4f;
            return desire > 0.55f ? PlayerStance.WantsOut : desire < 0.15f ? PlayerStance.WantsStay : PlayerStance.Open;
        }

        /// <summary>La conversación con el jugador sobre una oferta: qué quiere y, si dudaba, con qué sueldo se quedaría.</summary>
        /// <param name="offer">Oferta pendiente.</param>
        public PlayerTalk TalkAbout(PendingOffer offer)
        {
            Player p = Data.Players[offer.Offer.PlayerId];
            var talk = new PlayerTalk { Listed = offer.Listed, Stance = offer.Stance, KeepYears = _cfg.Career.KeepYears };
            if (p.Contract != null)
            {
                talk.KeepWage = (long)(p.Contract.WeeklyWage * (1.0 + _cfg.Career.KeepRaise));
            }

            return talk;
        }

        /// <summary>
        /// Convence a un jugador que dudaba de quedarse con una mejora de contrato (más sueldo y más años): la oferta se retira y su moral sube.
        /// Si quería irse no se queda; si ya quería quedarse no hace falta.
        /// </summary>
        /// <param name="offer">Oferta pendiente.</param>
        public DealOutcome KeepWithRaise(PendingOffer offer)
        {
            if (!_offers.Contains(offer) || offer.Listed)
            {
                return DealOutcome.Expired;
            }

            Player p = Data.Players[offer.Offer.PlayerId];
            if (offer.Stance == PlayerStance.WantsOut)
            {
                return DealOutcome.WontStay;
            }

            long wage = offer.Stance == PlayerStance.Open ? (long)(p.Contract.WeeklyWage * (1.0 + _cfg.Career.KeepRaise)) : p.Contract.WeeklyWage;
            if (WeeklyWages(Data.UserClubId) - p.Contract.WeeklyWage + wage > Data.Finances[Data.UserClubId].WeeklyWageBudget)
            {
                return DealOutcome.NoBudget;
            }

            Sign(p, wage, Math.Max(_cfg.Career.KeepYears, ContractYearsLeft(p)), p.Contract.Promise);
            Moral(p.Id, _cfg.Career.KeepMoraleBonus);
            _offers.Remove(offer);
            return DealOutcome.Done;
        }

        private void Moral(int playerId, float delta)
        {
            if (Data.Conditions.TryGetValue(playerId, out PlayerCondition c))
            {
                c.Morale = Math.Max(0f, Math.Min(100f, c.Morale + delta));
            }
        }

        // ------------------------------------------------------------------ responder

        /// <summary>Acepta o rechaza una oferta recibida (una venta se acepta sin cláusula de recompra).</summary>
        /// <param name="offer">Oferta pendiente.</param>
        /// <param name="accept">Verdadero para aceptar.</param>
        public void Respond(PendingOffer offer, bool accept)
        {
            if (accept)
            {
                Accept(offer);
            }
            else
            {
                Reject(offer);
            }
        }

        /// <summary>
        /// Acepta una oferta. Una venta puede llevar cláusula de recompra: el monto es un múltiplo del precio de venta (como mínimo
        /// <see cref="CareerConfig.BuybackMinFactor"/>) y dura hasta <paramref name="buybackYears"/> años.
        /// </summary>
        /// <param name="offer">Oferta pendiente.</param>
        /// <param name="buybackYears">Años de la cláusula de recompra (0 = sin cláusula).</param>
        /// <param name="buybackFactor">Múltiplo del precio de venta que cuesta recomprarlo.</param>
        public DealOutcome Accept(PendingOffer offer, int buybackYears = 0, float buybackFactor = 0f)
        {
            if (!_offers.Contains(offer) || !Data.Players.TryGetValue(offer.Offer.PlayerId, out Player p) || p.ClubId != Data.UserClubId)
            {
                _offers.Remove(offer);
                return DealOutcome.Expired;
            }

            if (!TransferWindowOpen)
            {
                return DealOutcome.WindowClosed;
            }

            if (offer.Kind == DealKind.Sale)
            {
                if (buybackYears > 0 && buybackFactor < _cfg.Career.BuybackMinFactor)
                {
                    return DealOutcome.BuybackTooLow;
                }

                if (Data.Finances[offer.Offer.BuyerClubId].TransferBudget < offer.Offer.Fee)
                {
                    _offers.Remove(offer);
                    return DealOutcome.Expired;
                }

                _offers.Remove(offer);
                if (!offer.Listed && offer.Stance == PlayerStance.WantsStay)
                {
                    Moral(p.Id, -_cfg.Career.SoldAgainstWillMorale); // se va contra su voluntad: no se lo olvida el vestuario
                }

                Data.Listings.Remove(p.Id);
                Transfer(offer.Offer);
                if (buybackYears > 0)
                {
                    Data.Buybacks.Add(new BuybackClause
                    {
                        PlayerId = p.Id, ClubId = Data.UserClubId, HolderClubId = offer.Offer.BuyerClubId,
                        Fee = (long)(offer.Offer.Fee * (double)buybackFactor), Expires = Data.Today.AddYears(Math.Min(buybackYears, _cfg.Career.BuybackMaxYears))
                    });
                }

                return DealOutcome.Done;
            }

            _offers.Remove(offer);
            Data.Listings.Remove(p.Id);
            StartLoan(p, Data.UserClubId, offer.Offer.BuyerClubId, offer.WageShare, offer.Kind == DealKind.LoanObligation, offer.PurchaseFee, offer.MinMinutes);
            return DealOutcome.Done;
        }

        /// <summary>Rechaza una oferta. Si el jugador no estaba en la lista, su moral cambia según lo que quería.</summary>
        /// <param name="offer">Oferta pendiente.</param>
        public void Reject(PendingOffer offer)
        {
            if (!_offers.Remove(offer) || offer.Listed || !Data.Players.TryGetValue(offer.Offer.PlayerId, out Player p))
            {
                return;
            }

            CareerConfig cc = _cfg.Career;
            switch (offer.Stance)
            {
                case PlayerStance.WantsOut:
                    Moral(p.Id, -cc.RejectWantsOutMorale);
                    _inbox.Post("news.player_upset_stays", FullName(p));
                    break;
                case PlayerStance.Open:
                    Moral(p.Id, -cc.RejectOpenMorale);
                    break;
                default:
                    Moral(p.Id, cc.RejectWantsStayMorale);
                    break;
            }
        }

        // ------------------------------------------------------------------ préstamos

        /// <summary>Préstamo vigente de un jugador (null si no está prestado).</summary>
        /// <param name="playerId">Jugador.</param>
        public LoanRecord LoanOf(int playerId) => Data.Loans.Find(l => l.PlayerId == playerId);

        /// <summary>Jugadores del usuario cedidos a otros clubes.</summary>
        public IEnumerable<LoanRecord> LoansOut() => Data.Loans.Where(l => l.OwnerClubId == Data.UserClubId);

        /// <summary>Jugadores de otros clubes que el usuario tiene prestados.</summary>
        public IEnumerable<LoanRecord> LoansIn() => Data.Loans.Where(l => l.BorrowerClubId == Data.UserClubId);

        private void StartLoan(Player p, int ownerId, int borrowerId, float share, bool obligation, long fee, float minMinutes)
        {
            Data.Loans.Add(new LoanRecord
            {
                PlayerId = p.Id, OwnerClubId = ownerId, BorrowerClubId = borrowerId, Start = Data.Today, BorrowerWageShare = share,
                Obligation = obligation, PurchaseFee = fee, MinMinutes = minMinutes
            });
            p.ClubId = borrowerId;
            p.ShirtNumber = 0;
            FixShirtNumbers(Data, borrowerId);
            FixShirtNumbers(Data, ownerId);
            _inbox.Post(borrowerId == Data.UserClubId ? "news.loan_in" : "news.loan_out", FullName(p), Data.Club(borrowerId == Data.UserClubId ? ownerId : borrowerId).Name);
        }

        /// <summary>
        /// Pide a un jugador de otro club a préstamo hasta fin de temporada. El club lo cede si no es de sus mejores (o es un juvenil y es de los que
        /// venden jóvenes) y si el usuario paga al menos la mitad del sueldo. Con compra obligatoria, el monto es el valor de mercado y hace falta tener
        /// ese presupuesto; al final, si el jugador jugó los minutos pedidos, se compra.
        /// </summary>
        /// <param name="playerId">Jugador.</param>
        /// <param name="wageShare">Fracción del sueldo que paga el usuario (0,5–1).</param>
        /// <param name="obligation">Con compra obligatoria.</param>
        public DealOutcome RequestLoan(int playerId, float wageShare, bool obligation)
        {
            CareerConfig cc = _cfg.Career;
            if (!Data.Players.TryGetValue(playerId, out Player p) || !p.ClubId.HasValue || p.ClubId == Data.UserClubId || LoanOf(playerId) != null || p.Contract == null)
            {
                return DealOutcome.Expired;
            }

            if (!TransferWindowOpen)
            {
                return DealOutcome.WindowClosed;
            }

            if (WouldBreakForeignQuota(p, Data.UserClubId))
            {
                return DealOutcome.ForeignQuota;
            }

            int owner = p.ClubId.Value;
            float mine = PlayerRatings.Overall(p.Attributes, p.Position);
            int rank = Squad(owner).Count(q => q.Id != p.Id && PlayerRatings.Overall(q.Attributes, q.Position) > mine);
            bool youth = ProfileOf(owner) == ClubProfile.YouthSeller && p.AgeOn(Data.Today) <= 21;
            if ((rank < cc.LoanLenderRank && !youth) || wageShare < cc.LoanWageShareMin)
            {
                return DealOutcome.Rejected;
            }

            long fee = obligation ? _market.Value(p, Data.Today) : 0;
            if (obligation && fee > Data.Finances[Data.UserClubId].TransferBudget)
            {
                return DealOutcome.NoBudget;
            }

            long extra = (long)(p.Contract.WeeklyWage * wageShare);
            if (WeeklyWages(Data.UserClubId) + extra > Data.Finances[Data.UserClubId].WeeklyWageBudget)
            {
                return DealOutcome.NoBudget;
            }

            StartLoan(p, owner, Data.UserClubId, wageShare, obligation, fee, obligation ? cc.LoanMinMinutes : 0f);
            return DealOutcome.Done;
        }

        /// <summary>Termina los préstamos al cerrar la temporada: vuelven a su club, o se compran si había compra obligatoria y jugaron los minutos pedidos.</summary>
        internal void ProcessLoansAtSeasonEnd()
        {
            foreach (LoanRecord loan in Data.Loans.ToList())
            {
                if (!Data.Players.TryGetValue(loan.PlayerId, out Player p) || p.ClubId != loan.BorrowerClubId)
                {
                    continue; // se retiró o se movió por otro lado
                }

                float minutes = Data.Conditions.TryGetValue(p.Id, out PlayerCondition c) ? c.SeasonMinutes : 0f;
                bool buy = loan.Obligation && minutes >= loan.MinMinutes && Data.Finances[loan.BorrowerClubId].TransferBudget >= loan.PurchaseFee;
                if (buy)
                {
                    Transfer(new TransferOffer { PlayerId = p.Id, BuyerClubId = loan.BorrowerClubId, SellerClubId = loan.OwnerClubId, Fee = loan.PurchaseFee });
                    _inbox.Post("news.loan_bought", FullName(p), Data.Club(loan.BorrowerClubId).Name, Num(loan.PurchaseFee));
                }
                else
                {
                    p.ClubId = loan.OwnerClubId;
                    p.ShirtNumber = 0;
                    FixShirtNumbers(Data, loan.OwnerClubId);
                    FixShirtNumbers(Data, loan.BorrowerClubId);
                    _inbox.Post("news.loan_returned", FullName(p), Data.Club(loan.BorrowerClubId == Data.UserClubId ? loan.OwnerClubId : loan.BorrowerClubId).Name);
                }
            }

            Data.Loans.Clear();
        }

        // ------------------------------------------------------------------ recompra

        /// <summary>Cláusula de recompra vigente de un jugador vendido por el usuario (null si no hay).</summary>
        /// <param name="playerId">Jugador.</param>
        public BuybackClause BuybackOf(int playerId) => Data.Buybacks.Find(b => b.PlayerId == playerId && b.Expires >= Data.Today);

        /// <summary>Ejerce la cláusula de recompra: el club que lo tiene lo vende por el monto fijado, si hay libro abierto y presupuesto.</summary>
        /// <param name="playerId">Jugador vendido.</param>
        public DealOutcome ExerciseBuyback(int playerId)
        {
            BuybackClause b = BuybackOf(playerId);
            if (b == null || !Data.Players.TryGetValue(playerId, out Player p) || p.ClubId != b.HolderClubId || LoanOf(playerId) != null)
            {
                return DealOutcome.Expired;
            }

            if (!TransferWindowOpen)
            {
                return DealOutcome.WindowClosed;
            }

            if (WouldBreakForeignQuota(p, Data.UserClubId))
            {
                return DealOutcome.ForeignQuota;
            }

            if (b.Fee > Data.Finances[Data.UserClubId].TransferBudget)
            {
                return DealOutcome.NoBudget;
            }

            Transfer(new TransferOffer { PlayerId = playerId, BuyerClubId = Data.UserClubId, SellerClubId = b.HolderClubId, Fee = b.Fee });
            Data.Buybacks.Remove(b);
            return DealOutcome.Done;
        }

        private void ExpireBuybacks() => Data.Buybacks.RemoveAll(b => b.Expires < Data.Today);

        // ------------------------------------------------------------------ sueldos con préstamos

        /// <inheritdoc />
        public long WeeklyWages(int clubId)
        {
            long total = 0;
            Dictionary<int, LoanRecord> loans = Data.Loans.Count == 0 ? null : Data.Loans.ToDictionary(l => l.PlayerId);
            foreach (Player p in Data.Players.Values)
            {
                if (p.Contract == null)
                {
                    continue;
                }

                if (loans != null && loans.TryGetValue(p.Id, out LoanRecord loan))
                {
                    // Prestado: el que lo recibe paga una parte del sueldo y el dueño paga el resto.
                    if (loan.BorrowerClubId == clubId)
                    {
                        total += (long)(p.Contract.WeeklyWage * loan.BorrowerWageShare);
                    }
                    else if (loan.OwnerClubId == clubId)
                    {
                        total += (long)(p.Contract.WeeklyWage * (1f - loan.BorrowerWageShare));
                    }

                    continue;
                }

                if (p.ClubId == clubId)
                {
                    total += p.Contract.WeeklyWage;
                }
            }

            return total + Data.Staff.Where(s => s.ClubId == clubId).Sum(s => s.WeeklyWage);
        }
    }
}
