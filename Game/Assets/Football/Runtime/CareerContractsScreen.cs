using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Football.Core.Career;
using Football.Core.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Football.Game
{
    /// <summary>
    /// Contratos del plantel del usuario, de los que vencen antes a los que vencen después. Al tocar un jugador se elige por cuántos años se le renueva
    /// (más de los que le quedan, hasta 5 y menos si es veterano), se ve el sueldo que pide y se renueva; si no se renueva, se va libre cuando termina
    /// la temporada. Los que vencen este año figuran en rojo.
    /// </summary>
    public sealed class CareerContractsScreen : CareerPage
    {
        private const int Rows = 12;
        private static readonly float[] Centres = { 270f, 475f, 560f, 650f, 790f, 905f, 1020f };
        private static readonly float[] Widths = { 330f, 80f, 80f, 90f, 160f, 110f, 120f };

        private readonly TextGrid _grid;
        private readonly UiButton _pagePrev, _pageNext, _back, _yearsMinus, _yearsPlus, _renew, _wageMinus, _wagePlus, _promise, _acceptDemand;
        private readonly Text _page, _name, _info, _years, _ask, _result, _note, _wage;
        private List<Player> _players = new List<Player>();
        private int _offset;
        private int _selected = -1;
        private int _chosenYears;
        private long _chosenWage;
        private int _wagePlayer = -1; // jugador para el que se armó _chosenWage
        private PlayingTimePromise _chosenPromise = PlayingTimePromise.Rotation;
        private static readonly PlayingTimePromise[] Promises = { PlayingTimePromise.Star, PlayingTimePromise.Regular, PlayingTimePromise.Rotation, PlayingTimePromise.Backup };
        private const double WageStep = 0.05;

        /// <summary>Crea la pantalla (oculta).</summary>
        public CareerContractsScreen(Transform root, ICareerHost host) : base(root, "Contratos", host)
        {
            Transform p = Panel;
            UiTheme.Frame(p, "Fondo", new Vector2(960f, 565f), new Vector2(1840f, 810f));
            UiKit.Label(p, "Titulo", new Vector2(330f, 932f), new Vector2(520f, 50f), Locale.Get("contracts.title"), 36, UiTheme.Accent, TextAnchor.MiddleLeft);
            _grid = new TextGrid(p, "Plantel", new Vector2(80f, 872f), 52f, Centres, Widths, Rows + 1, 22);
            _pagePrev = UiKit.Button(p, "PaginaAnterior", new Vector2(420f, 120f), new Vector2(110f, 56f), "<", 30, UiTheme.Button);
            _pageNext = UiKit.Button(p, "PaginaSiguiente", new Vector2(800f, 120f), new Vector2(110f, 56f), ">", 30, UiTheme.Button);
            _page = UiKit.Label(p, "Pagina", new Vector2(610f, 120f), new Vector2(240f, 56f), string.Empty, 22, UiTheme.TextDim);
            _back = UiKit.Button(p, "Volver", new Vector2(1550f, 120f), new Vector2(300f, 64f), Locale.Get("career.back"), 28, UiTheme.AccentDark);

            // Detalle del jugador elegido.
            UiKit.Panel(p, "Detalle", new Vector2(1545f, 540f), new Vector2(640f, 700f), new Color(0f, 0f, 0f, 0.28f));
            _name = UiKit.Label(p, "Jugador", new Vector2(1545f, 840f), new Vector2(600f, 50f), string.Empty, 32, UiTheme.Text);
            _info = UiKit.Label(p, "Info", new Vector2(1545f, 735f), new Vector2(600f, 130f), string.Empty, 22, UiTheme.TextDim);
            _info.horizontalOverflow = HorizontalWrapMode.Wrap;
            _ask = UiKit.Label(p, "Pide", new Vector2(1545f, 665f), new Vector2(600f, 80f), string.Empty, 21, UiTheme.Gold);
            _ask.horizontalOverflow = HorizontalWrapMode.Wrap;
            _wageMinus = UiKit.Button(p, "SueldoMenos", new Vector2(1295f, 590f), new Vector2(90f, 62f), "-", 34, UiTheme.Button);
            _wage = UiKit.Label(p, "SueldoOferta", new Vector2(1545f, 590f), new Vector2(380f, 62f), string.Empty, 24, UiTheme.Text);
            _wagePlus = UiKit.Button(p, "SueldoMas", new Vector2(1795f, 590f), new Vector2(90f, 62f), "+", 34, UiTheme.Button);
            _yearsMinus = UiKit.Button(p, "AniosMenos", new Vector2(1295f, 515f), new Vector2(90f, 62f), "-", 34, UiTheme.Button);
            _years = UiKit.Label(p, "Anios", new Vector2(1545f, 515f), new Vector2(380f, 62f), string.Empty, 24, UiTheme.Text);
            _yearsPlus = UiKit.Button(p, "AniosMas", new Vector2(1795f, 515f), new Vector2(90f, 62f), "+", 34, UiTheme.Button);
            _promise = UiKit.Button(p, "Promesa", new Vector2(1545f, 440f), new Vector2(600f, 62f), string.Empty, 22, UiTheme.Button);
            _renew = UiKit.Button(p, "Renovar", new Vector2(1545f, 355f), new Vector2(440f, 72f), Locale.Get("contracts.offer"), 30, UiTheme.AccentDark);
            _result = UiKit.Label(p, "Resultado", new Vector2(1545f, 275f), new Vector2(600f, 70f), string.Empty, 21, UiTheme.Accent);
            _result.horizontalOverflow = HorizontalWrapMode.Wrap;
            _acceptDemand = UiKit.Button(p, "AceptarPedido", new Vector2(1545f, 205f), new Vector2(520f, 64f), Locale.Get("contracts.accept_demand"), 24, UiTheme.AccentDark);
            _acceptDemand.SetVisible(false);
            _note = UiKit.Label(p, "Aviso", new Vector2(1545f, 205f), new Vector2(580f, 70f), Locale.Get("contracts.free_note"), 18, UiTheme.TextDim);
            _note.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        /// <inheritdoc />
        public override void SetVisible(bool visible)
        {
            if (visible)
            {
                _offset = 0;
                _selected = -1;
                _result.text = string.Empty;
            }

            base.SetVisible(visible);
        }

        private void Load()
        {
            _players = Session.Squad(Data.UserClubId)
                .OrderBy(p => Session.ContractYearsLeft(p)).ThenByDescending(p => PlayerRatings.Overall(p.Attributes, p.Position)).ToList();
        }

        /// <inheritdoc />
        public override void Refresh()
        {
            if (Session == null)
            {
                return;
            }

            Player keep = _selected >= 0 && _selected < _players.Count ? _players[_selected] : null;
            Load();
            _selected = keep == null ? -1 : _players.FindIndex(x => x.Id == keep.Id);
            _grid.Clear();
            _grid.SetRow(0, Locale.Get("career.col.name"), Locale.Get("career.col.pos"), Locale.Get("career.col.age"), Locale.Get("career.col.rating"),
                Locale.Get("contracts.col.wage"), Locale.Get("contracts.col.left"), Locale.Get("contracts.col.until"));
            for (int k = 0; k < Rows && _offset + k < _players.Count; k++)
            {
                Player pl = _players[_offset + k];
                int row = k + 1, left = Session.ContractYearsLeft(pl);
                _grid.SetRow(row, Name(pl), PlayerLabels.Code(pl.Position), CareerText.Num(pl.AgeOn(Data.Today)), CareerText.Num(Session.VisibleRating(pl)),
                    CareerText.Money(pl.Contract?.WeeklyWage ?? 0), CareerText.Num(left), pl.Contract != null ? CareerText.Num(pl.Contract.ExpiresOn.Year) : "-");
                Color warn = left <= 1 ? StatusColors.Bad : left == 2 ? StatusColors.Fair : UiTheme.Text;
                _grid.SetColor(row, 5, warn);
                _grid.SetColor(row, 6, warn);
                if (Session.IsRetiringThisSeason(pl.Id))
                {
                    _grid.SetColor(row, 0, UiTheme.Gold); // se retira al terminar la temporada
                }

                _grid.Highlight(row, _offset + k == _selected);
            }

            int pages = Mathf.Max(1, (_players.Count + Rows - 1) / Rows);
            _page.text = CareerText.Format("career.page", _offset / Rows + 1, pages);
            _pagePrev.SetVisible(pages > 1);
            _pageNext.SetVisible(pages > 1);
            RefreshDetail();
        }

        private static string Name(Player p) => (p.FirstName + " " + p.LastName).Trim();

        private void RefreshDetail()
        {
            bool has = _selected >= 0 && _selected < _players.Count;
            if (!has)
            {
                SetControls(false);
                _name.text = Locale.Get("contracts.pick");
                _info.text = _years.text = _ask.text = _wage.text = string.Empty;
                return;
            }

            Player pl = _players[_selected];
            int left = Session.ContractYearsLeft(pl), max = Session.MaxRenewalYears(pl);
            PlayerCondition c = Data.Conditions[pl.Id];
            RenewalDemand want = Session.DemandOf(pl);
            RenewalTalk talk = Session.TalkOf(pl.Id);
            _name.text = pl.FirstName + " " + pl.LastName;
            _info.text = CareerText.Format("contracts.info", pl.AgeOn(Data.Today), PlayerLabels.Code(pl.Position), Session.VisibleRating(pl),
                CareerText.Num(pl.Contract.ExpiresOn.Year), CareerText.Money(pl.Contract.WeeklyWage), CareerText.Num((int)c.Morale));
            if (Session.IsRetiringThisSeason(pl.Id))
            {
                _info.text += "\n" + Locale.Get("contracts.retiring");
            }
            bool canExtend = left < max;
            bool thinking = talk != null && talk.Status == TalkStatus.Thinking;
            bool refused = talk != null && talk.Status == TalkStatus.Refused;
            bool signed = talk != null && talk.Status == TalkStatus.Accepted && left > 1;
            bool canOffer = canExtend && !thinking && !refused && !signed;
            SetControls(canOffer);
            bool counter = talk != null && talk.Status == TalkStatus.Counter;
            if (_wagePlayer != pl.Id)
            {
                // Las pretensiones no se ven hasta que el jugador responde: se empieza ofreciendo lo que cobra hoy.
                _wagePlayer = pl.Id;
                _chosenWage = counter ? talk.DemandWage : pl.Contract.WeeklyWage;
                _chosenYears = counter ? talk.DemandYears : left + 1;
                _chosenPromise = counter ? talk.DemandRole : PlayingTimePromise.Regular;
            }

            _chosenYears = canExtend ? Mathf.Clamp(_chosenYears, left + 1, max) : left;
            _acceptDemand.SetVisible(counter);
            _note.gameObject.SetActive(!counter);
            _wage.text = CareerText.Format("contracts.offer_wage", CareerText.Money(_chosenWage));
            _years.text = canExtend ? CareerText.Format("contracts.renew_for", _chosenYears) : Locale.Get("contracts.no_extension");
            _promise.Label.text = Locale.Get("contracts.promise") + ": " + Locale.Get("promise." + _chosenPromise);
            if (talk == null)
            {
                _ask.text = Locale.Get("contracts.asks_hidden"); // todavía no se sabe qué pretende: primero se ofrece
            }
            else if (counter || refused)
            {
                _ask.text = CareerText.Format("contracts.asks", CareerText.Money(talk.DemandWage), talk.DemandYears) + "  ·  " + Locale.Get("promise." + talk.DemandRole);
            }
            else
            {
                _ask.text = string.Empty;
            }
            if (thinking)
            {
                _result.text = CareerText.Format("contracts.thinking", talk.ReplyOn.ToString("dd/MM", CultureInfo.InvariantCulture));
                _result.color = UiTheme.Gold;
            }
            else if (refused)
            {
                _result.text = Locale.Get("contracts.refused." + talk.Reason);
                _result.color = StatusColors.Bad;
            }
            else if (signed)
            {
                _result.text = Locale.Get("contracts.result.Accepted");
                _result.color = StatusColors.Good;
            }
            else if (talk != null && talk.Status == TalkStatus.Counter)
            {
                _result.text = Locale.Get("contracts.counter." + talk.Reason);
                _result.color = StatusColors.Fair;
            }
        }

        private void SetControls(bool visible)
        {
            _wageMinus.SetVisible(visible);
            _wagePlus.SetVisible(visible);
            _yearsMinus.SetVisible(visible);
            _yearsPlus.SetVisible(visible);
            _promise.SetVisible(visible);
            _renew.SetVisible(visible);
            _wage.gameObject.SetActive(visible);
            _years.gameObject.SetActive(visible);
        }

        /// <inheritdoc />
        public override void Tap(Vector2 p)
        {
            if (_back.Contains(p))
            {
                Host.Open(CareerSection.Squad);
                return;
            }

            int row = _grid.RowAt(p) - 1;
            if (row >= 0 && _offset + row < _players.Count)
            {
                _selected = _offset + row;
                _chosenYears = 0;
                _wagePlayer = -1;
                _result.text = string.Empty;
            }
            else if (_pagePrev.Root.activeSelf && _pagePrev.Contains(p))
            {
                _offset = Mathf.Max(0, _offset - Rows);
            }
            else if (_pageNext.Root.activeSelf && _pageNext.Contains(p) && _offset + Rows < _players.Count)
            {
                _offset += Rows;
            }
            else if (_selected >= 0 && _selected < _players.Count)
            {
                Player pl = _players[_selected];
                int left = Session.ContractYearsLeft(pl), max = Session.MaxRenewalYears(pl);
                if (_yearsMinus.Root.activeSelf && _yearsMinus.Contains(p)) { _chosenYears = Mathf.Max(left + 1, _chosenYears - 1); }
                else if (_yearsPlus.Root.activeSelf && _yearsPlus.Contains(p)) { _chosenYears = Mathf.Min(max, _chosenYears + 1); }
                else if (_wageMinus.Root.activeSelf && _wageMinus.Contains(p)) { _chosenWage = (long)(_chosenWage * (1.0 - WageStep)); }
                else if (_wagePlus.Root.activeSelf && _wagePlus.Contains(p)) { _chosenWage = (long)(_chosenWage * (1.0 + WageStep)) + 1; }
                else if (_promise.Root.activeSelf && _promise.Contains(p))
                {
                    _chosenPromise = Promises[(System.Array.IndexOf(Promises, _chosenPromise) + 1) % Promises.Length];
                }
                else if (_acceptDemand.Root.activeSelf && _acceptDemand.Contains(p))
                {
                    ContractOutcome accepted = Session.AcceptCounter(pl.Id);
                    _result.text = Locale.Get(accepted == ContractOutcome.Accepted ? "contracts.result.Accepted" : "contracts.result." + accepted);
                    _result.color = accepted == ContractOutcome.Accepted ? StatusColors.Good : StatusColors.Bad;
                }
                else if (_renew.Root.activeSelf && _renew.Contains(p))
                {
                    ContractOutcome outcome = Session.SendRenewalOffer(pl.Id, _chosenWage, _chosenYears, _chosenPromise);
                    _result.text = Locale.Get(outcome == ContractOutcome.Accepted ? "contracts.sent" : "contracts.result." + outcome);
                    _result.color = outcome == ContractOutcome.Accepted ? StatusColors.Good : StatusColors.Bad;
                }
            }

            Refresh();
        }
    }
}
