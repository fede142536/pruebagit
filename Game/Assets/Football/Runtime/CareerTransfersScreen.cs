using System.Collections.Generic;
using System.Linq;
using Football.Core.Career;
using Football.Core.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Football.Game
{
    /// <summary>
    /// Traspasos del club: tres pestañas. «Transferibles» pone o saca jugadores de la lista (venta y/o cesión, con su precio); «Ofertas» muestra lo que
    /// llega de otros clubes (por los de la lista y por los que no) y permite aceptar, rechazar, hablar con el jugador y fijar una recompra;
    /// «Cesiones» muestra los jugadores cedidos, los prestados al club y las cláusulas de recompra que se pueden ejercer. Sin lógica de juego:
    /// todo lo resuelve <see cref="CareerSession"/>.
    /// </summary>
    public sealed class CareerTransfersScreen : CareerPage
    {
        private const int Rows = 11;
        private const int Tabs = 3;
        private const float PriceStep = 0.1f;
        private const float MinPriceFactor = 0.2f;
        private static readonly int[] BuybackYears = { 0, 1, 2, 3 };
        private static readonly float[] BuybackFactors = { 1.25f, 1.5f, 2f, 3f };
        private static readonly float[] ListCentres = { 270f, 470f, 550f, 640f, 780f, 975f, 1135f };
        private static readonly float[] ListWidths = { 330f, 80f, 70f, 90f, 150f, 220f, 150f };
        private static readonly float[] OfferCentres = { 290f, 640f, 940f, 1120f };
        private static readonly float[] OfferWidths = { 360f, 320f, 230f, 140f };
        private static readonly float[] LoanCentres = { 250f, 590f, 900f, 1130f };
        private static readonly float[] LoanWidths = { 250f, 360f, 260f, 220f };

        private readonly UiButton[] _tab = new UiButton[Tabs];
        private readonly TextGrid _squadGrid, _offerGrid, _loanGrid;
        private readonly UiButton _back, _pagePrev, _pageNext;
        private readonly UiButton _sale, _loan, _priceMinus, _pricePlus, _unlist;
        private readonly UiButton _accept, _reject, _talk, _keep, _buybackYears, _buybackFactor, _exercise;
        private readonly Text _title, _page, _name, _info, _price, _result, _hint;
        private List<Player> _players = new List<Player>();
        private readonly List<LoanLine> _lines = new List<LoanLine>();
        private int _tabIndex, _offset, _selected = -1;
        private int _yearsIndex, _factorIndex;
        private bool _talking;

        private struct LoanLine
        {
            public string Kind, Who, Club, Detail;
            public int PlayerId;
            public bool Buyback;
        }

        /// <summary>Crea la pantalla (oculta).</summary>
        public CareerTransfersScreen(Transform root, ICareerHost host) : base(root, "Traspasos", host)
        {
            Transform p = Panel;
            UiTheme.Frame(p, "Fondo", new Vector2(960f, 565f), new Vector2(1840f, 810f));
            _title = UiKit.Label(p, "Titulo", new Vector2(330f, 940f), new Vector2(520f, 50f), Locale.Get("transfers.title"), 36, UiTheme.Accent, TextAnchor.MiddleLeft);
            for (int i = 0; i < Tabs; i++)
            {
                _tab[i] = UiKit.Button(p, "Pestana" + i, new Vector2(880f + i * 300f, 940f), new Vector2(280f, 60f), Locale.Get("transfers.tab" + i), 24, UiTheme.Button);
            }

            _squadGrid = new TextGrid(p, "Plantel", new Vector2(80f, 860f), 52f, ListCentres, ListWidths, Rows + 1, 22);
            _offerGrid = new TextGrid(p, "Ofertas", new Vector2(80f, 860f), 52f, OfferCentres, OfferWidths, Rows + 1, 22);
            _loanGrid = new TextGrid(p, "Cesiones", new Vector2(80f, 860f), 52f, LoanCentres, LoanWidths, Rows + 1, 22);
            _pagePrev = UiKit.Button(p, "PaginaAnterior", new Vector2(420f, 120f), new Vector2(110f, 56f), "<", 30, UiTheme.Button);
            _pageNext = UiKit.Button(p, "PaginaSiguiente", new Vector2(800f, 120f), new Vector2(110f, 56f), ">", 30, UiTheme.Button);
            _page = UiKit.Label(p, "Pagina", new Vector2(610f, 120f), new Vector2(240f, 56f), string.Empty, 22, UiTheme.TextDim);
            _back = UiKit.Button(p, "Volver", new Vector2(1550f, 120f), new Vector2(300f, 64f), Locale.Get("career.back"), 28, UiTheme.AccentDark);

            UiKit.Panel(p, "Detalle", new Vector2(1545f, 540f), new Vector2(640f, 700f), new Color(0f, 0f, 0f, 0.28f));
            _name = UiKit.Label(p, "Jugador", new Vector2(1545f, 840f), new Vector2(600f, 50f), string.Empty, 32, UiTheme.Text);
            _info = UiKit.Label(p, "Info", new Vector2(1545f, 735f), new Vector2(600f, 130f), string.Empty, 22, UiTheme.TextDim);
            _info.horizontalOverflow = HorizontalWrapMode.Wrap;
            _price = UiKit.Label(p, "Precio", new Vector2(1545f, 640f), new Vector2(380f, 62f), string.Empty, 24, UiTheme.Gold);

            // Pestaña 0: lista de transferibles.
            _sale = UiKit.Button(p, "Venta", new Vector2(1400f, 540f), new Vector2(290f, 64f), string.Empty, 22, UiTheme.Button);
            _loan = UiKit.Button(p, "Cesion", new Vector2(1700f, 540f), new Vector2(290f, 64f), string.Empty, 22, UiTheme.Button);
            _priceMinus = UiKit.Button(p, "PrecioMenos", new Vector2(1295f, 640f), new Vector2(90f, 62f), "-", 34, UiTheme.Button);
            _pricePlus = UiKit.Button(p, "PrecioMas", new Vector2(1795f, 640f), new Vector2(90f, 62f), "+", 34, UiTheme.Button);
            _unlist = UiKit.Button(p, "SacarDeLista", new Vector2(1545f, 455f), new Vector2(600f, 62f), Locale.Get("transfers.unlist"), 24, UiTheme.Button);

            // Pestaña 1: ofertas.
            _accept = UiKit.Button(p, "Aceptar", new Vector2(1400f, 540f), new Vector2(290f, 64f), Locale.Get("career.accept"), 24, UiTheme.AccentDark);
            _reject = UiKit.Button(p, "Rechazar", new Vector2(1700f, 540f), new Vector2(290f, 64f), Locale.Get("career.reject"), 24, UiTheme.Button);
            _talk = UiKit.Button(p, "Hablar", new Vector2(1545f, 455f), new Vector2(600f, 62f), Locale.Get("transfers.talk"), 24, UiTheme.Button);
            _keep = UiKit.Button(p, "Quedarse", new Vector2(1545f, 380f), new Vector2(600f, 62f), string.Empty, 22, UiTheme.AccentDark);
            _buybackYears = UiKit.Button(p, "RecompraAnios", new Vector2(1400f, 640f), new Vector2(290f, 62f), string.Empty, 22, UiTheme.Button);
            _buybackFactor = UiKit.Button(p, "RecompraMonto", new Vector2(1700f, 640f), new Vector2(290f, 62f), string.Empty, 22, UiTheme.Button);

            // Pestaña 2: recompra.
            _exercise = UiKit.Button(p, "Recomprar", new Vector2(1545f, 540f), new Vector2(600f, 64f), Locale.Get("transfers.exercise"), 26, UiTheme.AccentDark);

            _result = UiKit.Label(p, "Resultado", new Vector2(1545f, 295f), new Vector2(600f, 100f), string.Empty, 21, UiTheme.Accent);
            _result.horizontalOverflow = HorizontalWrapMode.Wrap;
            _hint = UiKit.Label(p, "Ayuda", new Vector2(1545f, 205f), new Vector2(600f, 80f), string.Empty, 18, UiTheme.TextDim);
            _hint.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        /// <inheritdoc />
        public override void SetVisible(bool visible)
        {
            if (visible)
            {
                _offset = 0;
                _selected = -1;
                _talking = false;
                _result.text = string.Empty;
            }

            base.SetVisible(visible);
        }

        private static string Name(Player p) => (p.FirstName + " " + p.LastName).Trim();

        private IReadOnlyList<PendingOffer> Offers => Session.Offers;

        private int Count => _tabIndex == 0 ? _players.Count : _tabIndex == 1 ? Offers.Count : _lines.Count;

        /// <inheritdoc />
        public override void Refresh()
        {
            if (Session == null)
            {
                return;
            }

            _players = Session.Squad(Data.UserClubId).OrderByDescending(p => Session.ListingOf(p.Id) != null)
                .ThenByDescending(p => PlayerRatings.Overall(p.Attributes, p.Position)).ToList();
            BuildLoanLines();
            _selected = _selected >= Count ? -1 : _selected;
            for (int i = 0; i < Tabs; i++)
            {
                _tab[i].Background.color = i == _tabIndex ? UiTheme.Selected : UiTheme.Button;
            }

            _squadGrid.Clear();
            _offerGrid.Clear();
            _loanGrid.Clear();
            _title.text = Locale.Get(Session.TransferWindowOpen ? "career.window_open" : "career.window_closed");
            switch (_tabIndex)
            {
                case 0: FillSquad(); break;
                case 1: FillOffers(); break;
                default: FillLoans(); break;
            }

            int pages = Mathf.Max(1, (Count + Rows - 1) / Rows);
            _page.text = CareerText.Format("career.page", _offset / Rows + 1, pages);
            _pagePrev.SetVisible(pages > 1);
            _pageNext.SetVisible(pages > 1);
            RefreshDetail();
        }

        private void FillSquad()
        {
            _squadGrid.SetRow(0, Locale.Get("career.col.name"), Locale.Get("career.col.pos"), Locale.Get("career.col.age"), Locale.Get("career.col.rating"),
                Locale.Get("career.col.value"), Locale.Get("transfers.col.status"), Locale.Get("transfers.col.ask"));
            for (int k = 0; k < Rows && _offset + k < _players.Count; k++)
            {
                Player pl = _players[_offset + k];
                TransferListing l = Session.ListingOf(pl.Id);
                bool loaned = Session.LoanOf(pl.Id) != null;
                string state = loaned ? Locale.Get("transfers.status.loaned") : l == null ? "-" : l.ForSale && l.ForLoan ? Locale.Get("transfers.status.both")
                    : l.ForSale ? Locale.Get("transfers.status.sale") : Locale.Get("transfers.status.loan");
                _squadGrid.SetRow(k + 1, Name(pl), PlayerLabels.Code(pl.Position), CareerText.Num(pl.AgeOn(Data.Today)), CareerText.Num(Session.VisibleRating(pl)),
                    CareerText.Money(Session.Market.Value(pl, Data.Today)), state, l != null && l.ForSale ? CareerText.Money(l.AskingPrice) : "-");
                _squadGrid.Highlight(k + 1, _offset + k == _selected);
            }
        }

        private string Kind(PendingOffer o) => Locale.Get(o.Kind == DealKind.Sale ? "transfers.kind.sale" : o.Kind == DealKind.Loan ? "transfers.kind.loan" : "transfers.kind.loan_buy");

        private void FillOffers()
        {
            _offerGrid.SetRow(0, Locale.Get("career.col.club"), Locale.Get("career.col.name"), Locale.Get("transfers.col.kind"), Locale.Get("transfers.col.amount"));
            for (int k = 0; k < Rows && _offset + k < Offers.Count; k++)
            {
                PendingOffer o = Offers[_offset + k];
                long amount = o.Kind == DealKind.Sale ? o.Offer.Fee : o.Kind == DealKind.LoanObligation ? o.PurchaseFee : 0;
                _offerGrid.SetRow(k + 1, CareerText.Fit(Data.Club(o.Offer.BuyerClubId).Name, 26), Name(Data.Players[o.Offer.PlayerId]), Kind(o), amount > 0 ? CareerText.Money(amount) : "-");
                _offerGrid.SetColor(k + 1, 1, o.Listed ? UiTheme.Text : UiTheme.Gold); // en dorado, lo que llegó por un jugador que no estaba en la lista
                _offerGrid.Highlight(k + 1, _offset + k == _selected);
            }
        }

        private void BuildLoanLines()
        {
            _lines.Clear();
            foreach (LoanRecord l in Session.LoansOut())
            {
                _lines.Add(new LoanLine
                {
                    Kind = Locale.Get("transfers.line.out"), Who = Name(Data.Players[l.PlayerId]), Club = Data.Club(l.BorrowerClubId).ShortName, PlayerId = l.PlayerId,
                    Detail = CareerText.Format("transfers.line.share", CareerText.Percent(1f - l.BorrowerWageShare))
                });
            }

            foreach (LoanRecord l in Session.LoansIn())
            {
                _lines.Add(new LoanLine
                {
                    Kind = Locale.Get("transfers.line.in"), Who = Name(Data.Players[l.PlayerId]), Club = Data.Club(l.OwnerClubId).ShortName, PlayerId = l.PlayerId,
                    Detail = l.Obligation ? CareerText.Format("transfers.line.obligation", CareerText.Money(l.PurchaseFee)) : CareerText.Format("transfers.line.share", CareerText.Percent(l.BorrowerWageShare))
                });
            }

            foreach (BuybackClause b in Data.Buybacks.Where(x => x.ClubId == Data.UserClubId && x.Expires >= Data.Today))
            {
                Player pl = Data.Players[b.PlayerId];
                if (pl.ClubId == b.HolderClubId && pl.ClubId.HasValue)
                {
                    _lines.Add(new LoanLine
                    {
                        Kind = Locale.Get("transfers.line.buyback"), Who = Name(pl), Club = Data.Club(b.HolderClubId).ShortName, PlayerId = pl.Id, Buyback = true,
                        Detail = CareerText.Money(b.Fee) + " · " + b.Expires.Year
                    });
                }
            }
        }

        private void FillLoans()
        {
            _loanGrid.SetRow(0, Locale.Get("transfers.col.kind"), Locale.Get("career.col.name"), Locale.Get("career.col.club"), Locale.Get("transfers.col.detail"));
            for (int k = 0; k < Rows && _offset + k < _lines.Count; k++)
            {
                LoanLine l = _lines[_offset + k];
                _loanGrid.SetRow(k + 1, l.Kind, l.Who, l.Club, l.Detail);
                _loanGrid.Highlight(k + 1, _offset + k == _selected);
            }
        }

        private void HideAll()
        {
            foreach (UiButton b in new[] { _sale, _loan, _priceMinus, _pricePlus, _unlist, _accept, _reject, _talk, _keep, _buybackYears, _buybackFactor, _exercise })
            {
                b.SetVisible(false);
            }

            _price.text = string.Empty;
        }

        private void RefreshDetail()
        {
            HideAll();
            bool has = _selected >= 0 && _selected < Count;
            _hint.text = Locale.Get("transfers.hint" + _tabIndex);
            if (!has)
            {
                _name.text = Locale.Get(_tabIndex == 1 && Offers.Count == 0 ? "transfers.no_offers" : "transfers.pick");
                _info.text = string.Empty;
                return;
            }

            switch (_tabIndex)
            {
                case 0: DetailSquad(_players[_selected]); break;
                case 1: DetailOffer(Offers[_selected]); break;
                default: DetailLine(_lines[_selected]); break;
            }
        }

        private void DetailSquad(Player pl)
        {
            TransferListing l = Session.ListingOf(pl.Id);
            bool loaned = Session.LoanOf(pl.Id) != null;
            _name.text = Name(pl);
            _info.text = CareerText.Format("transfers.info", pl.AgeOn(Data.Today), PlayerLabels.Code(pl.Position), Session.VisibleRating(pl),
                CareerText.Money(Session.Market.Value(pl, Data.Today)), CareerText.Money(pl.Contract?.WeeklyWage ?? 0));
            if (loaned)
            {
                _info.text += "\n" + Locale.Get("transfers.on_loan");
                return;
            }

            _sale.SetVisible(true);
            _loan.SetVisible(true);
            _sale.Label.text = Locale.Get(l != null && l.ForSale ? "transfers.sale_on" : "transfers.sale_off");
            _loan.Label.text = Locale.Get(l != null && l.ForLoan ? "transfers.loan_on" : "transfers.loan_off");
            _sale.Background.color = l != null && l.ForSale ? UiTheme.Selected : UiTheme.Button;
            _loan.Background.color = l != null && l.ForLoan ? UiTheme.Selected : UiTheme.Button;
            if (l != null)
            {
                _unlist.SetVisible(true);
                _price.text = CareerText.Format("transfers.price", CareerText.Money(l.AskingPrice));
                _priceMinus.SetVisible(l.ForSale);
                _pricePlus.SetVisible(l.ForSale);
                _price.gameObject.SetActive(l.ForSale);
            }
        }

        private void DetailOffer(PendingOffer o)
        {
            Player pl = Data.Players[o.Offer.PlayerId];
            Club buyer = Data.Club(o.Offer.BuyerClubId);
            _name.text = Name(pl);
            string terms = o.Kind == DealKind.Sale ? CareerText.Format("transfers.terms.sale", CareerText.Money(o.Offer.Fee))
                : o.Kind == DealKind.Loan ? CareerText.Format("transfers.terms.loan", CareerText.Percent(o.WageShare))
                : CareerText.Format("transfers.terms.loan_buy", CareerText.Percent(o.WageShare), CareerText.Money(o.PurchaseFee), CareerText.Num((int)o.MinMinutes));
            _info.text = buyer.Name + "\n" + terms + (o.Listed ? string.Empty : "\n" + Locale.Get("transfers.unlisted"));
            _accept.SetVisible(true);
            _reject.SetVisible(true);
            if (o.Kind == DealKind.Sale)
            {
                _buybackYears.SetVisible(true);
                _buybackFactor.SetVisible(BuybackYears[_yearsIndex] > 0);
                _buybackYears.Label.text = BuybackYears[_yearsIndex] == 0 ? Locale.Get("transfers.buyback_none") : CareerText.Format("transfers.buyback_years", BuybackYears[_yearsIndex]);
                _buybackFactor.Label.text = CareerText.Format("transfers.buyback_fee", CareerText.Money((long)(o.Offer.Fee * BuybackFactors[_factorIndex])));
            }

            if (!o.Listed)
            {
                _talk.SetVisible(true);
                if (_talking)
                {
                    PlayerTalk talk = Session.TalkAbout(o);
                    _result.text = Locale.Get("transfers.stance." + talk.Stance.ToString().ToLowerInvariant());
                    bool canKeep = talk.Stance != PlayerStance.WantsOut;
                    _keep.SetVisible(canKeep);
                    _keep.Label.text = talk.Stance == PlayerStance.Open ? CareerText.Format("transfers.keep_raise", CareerText.Money(talk.KeepWage), talk.KeepYears) : Locale.Get("transfers.keep");
                }
            }
        }

        private void DetailLine(LoanLine l)
        {
            Player pl = Data.Players[l.PlayerId];
            _name.text = Name(pl);
            _info.text = l.Kind + " · " + l.Club + "\n" + l.Detail;
            _exercise.SetVisible(l.Buyback);
        }

        /// <inheritdoc />
        public override void Tap(Vector2 p)
        {
            if (_back.Contains(p)) { Host.Open(CareerSection.Market); return; }
            for (int i = 0; i < Tabs; i++)
            {
                if (_tab[i].Contains(p)) { _tabIndex = i; _offset = 0; _selected = -1; _talking = false; _result.text = string.Empty; Refresh(); return; }
            }

            if (_pagePrev.Contains(p) && _offset >= Rows) { _offset -= Rows; _selected = -1; }
            else if (_pageNext.Contains(p) && _offset + Rows < Count) { _offset += Rows; _selected = -1; }
            else if (!TapActions(p))
            {
                int row = (_tabIndex == 0 ? _squadGrid : _tabIndex == 1 ? _offerGrid : _loanGrid).RowAt(p) - 1;
                if (row >= 0 && _offset + row < Count)
                {
                    _selected = _offset + row;
                    _talking = false;
                    _result.text = string.Empty;
                }
            }

            Refresh();
        }

        private bool TapActions(Vector2 p)
        {
            if (_selected < 0 || _selected >= Count)
            {
                return false;
            }

            switch (_tabIndex)
            {
                case 0: return TapSquad(_players[_selected], p);
                case 1: return TapOffer(Offers[_selected], p);
                default: return TapLine(_lines[_selected], p);
            }
        }

        private bool TapSquad(Player pl, Vector2 p)
        {
            TransferListing l = Session.ListingOf(pl.Id);
            bool sale = l != null && l.ForSale, loan = l != null && l.ForLoan;
            if (_sale.Root.activeSelf && _sale.Contains(p)) { Session.SetListing(pl.Id, !sale, loan); return true; }
            if (_loan.Root.activeSelf && _loan.Contains(p)) { Session.SetListing(pl.Id, sale, !loan); return true; }
            if (_unlist.Root.activeSelf && _unlist.Contains(p)) { Session.SetListing(pl.Id, false, false); return true; }
            if (l != null && l.ForSale && (_priceMinus.Contains(p) || _pricePlus.Contains(p)))
            {
                float factor = _pricePlus.Contains(p) ? 1f + PriceStep : 1f - PriceStep;
                long min = (long)(Session.Market.Value(pl, Data.Today) * MinPriceFactor);
                Session.SetListing(pl.Id, true, loan, System.Math.Max(min, (long)(l.AskingPrice * factor)));
                return true;
            }

            return false;
        }

        private bool TapOffer(PendingOffer o, Vector2 p)
        {
            if (_buybackYears.Root.activeSelf && _buybackYears.Contains(p)) { _yearsIndex = (_yearsIndex + 1) % BuybackYears.Length; return true; }
            if (_buybackFactor.Root.activeSelf && _buybackFactor.Contains(p)) { _factorIndex = (_factorIndex + 1) % BuybackFactors.Length; return true; }
            if (_talk.Root.activeSelf && _talk.Contains(p)) { _talking = !_talking; return true; }
            if (_reject.Contains(p))
            {
                Session.Reject(o);
                Done(string.Empty);
                return true;
            }

            if (_accept.Contains(p))
            {
                DealOutcome r = Session.Accept(o, o.Kind == DealKind.Sale ? BuybackYears[_yearsIndex] : 0, BuybackFactors[_factorIndex]);
                Done(Locale.Get("transfers.outcome." + r.ToString().ToLowerInvariant()), r == DealOutcome.Done || r == DealOutcome.Expired);
                return true;
            }

            if (_keep.Root.activeSelf && _keep.Contains(p))
            {
                DealOutcome r = Session.KeepWithRaise(o);
                Done(Locale.Get("transfers.outcome." + r.ToString().ToLowerInvariant()), r == DealOutcome.Done || r == DealOutcome.Expired);
                return true;
            }

            return false;
        }

        private bool TapLine(LoanLine l, Vector2 p)
        {
            if (l.Buyback && _exercise.Contains(p))
            {
                DealOutcome r = Session.ExerciseBuyback(l.PlayerId);
                Done(Locale.Get("transfers.outcome." + r.ToString().ToLowerInvariant()), r == DealOutcome.Done || r == DealOutcome.Expired);
                return true;
            }

            return false;
        }

        private void Done(string message, bool clearSelection = true)
        {
            if (clearSelection)
            {
                _selected = -1;
                _talking = false;
            }

            _result.text = message;
            Host.SetStatus(message);
        }
    }
}
