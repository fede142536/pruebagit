using System.Collections.Generic;
using System.Linq;
using Football.Core.Career;
using Football.Core.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Football.Game
{
    /// <summary>
    /// Reserva y juveniles: dos pestañas, los chicos de la reserva (que se pueden subir a primera con contrato profesional) y los jóvenes de primera
    /// (que, si ya debutaron y no superan la edad máxima, se pueden bajar a la reserva para seguir ganando rodaje). La reserva juega sola; acá se ve
    /// quién juega, su moral, su media y su potencial aproximado (lo estima el entrenador de juveniles). Sin lógica de juego: la resuelve <see cref="CareerSession"/>.
    /// </summary>
    public sealed class CareerReserveScreen : CareerPage
    {
        private const int Rows = 11;
        private static readonly float[] Centres = { 260f, 450f, 530f, 620f, 720f, 840f, 960f, 1075f };
        private static readonly float[] Widths = { 330f, 80f, 70f, 100f, 110f, 120f, 110f, 100f };

        private readonly TextGrid _grid;
        private readonly UiButton[] _tab = new UiButton[2];
        private readonly UiButton _back, _pagePrev, _pageNext, _move;
        private readonly Text _page, _name, _info, _result, _hint, _title;
        private List<Player> _players = new List<Player>();
        private int _tabIndex, _offset, _selected = -1;

        /// <summary>Crea la pantalla (oculta).</summary>
        public CareerReserveScreen(Transform root, ICareerHost host) : base(root, "Reserva", host)
        {
            Transform p = Panel;
            UiTheme.Frame(p, "Fondo", new Vector2(960f, 565f), new Vector2(1840f, 810f));
            _title = UiKit.Label(p, "Titulo", new Vector2(330f, 940f), new Vector2(520f, 50f), Locale.Get("reserve.title"), 36, UiTheme.Accent, TextAnchor.MiddleLeft);
            _tab[0] = UiKit.Button(p, "PestanaReserva", new Vector2(880f, 940f), new Vector2(300f, 60f), Locale.Get("reserve.tab0"), 24, UiTheme.Button);
            _tab[1] = UiKit.Button(p, "PestanaPrimera", new Vector2(1200f, 940f), new Vector2(300f, 60f), Locale.Get("reserve.tab1"), 24, UiTheme.Button);
            _grid = new TextGrid(p, "Juveniles", new Vector2(80f, 860f), 52f, Centres, Widths, Rows + 1, 22);
            _pagePrev = UiKit.Button(p, "PaginaAnterior", new Vector2(420f, 120f), new Vector2(110f, 56f), "<", 30, UiTheme.Button);
            _pageNext = UiKit.Button(p, "PaginaSiguiente", new Vector2(800f, 120f), new Vector2(110f, 56f), ">", 30, UiTheme.Button);
            _page = UiKit.Label(p, "Pagina", new Vector2(610f, 120f), new Vector2(240f, 56f), string.Empty, 22, UiTheme.TextDim);
            _back = UiKit.Button(p, "Volver", new Vector2(1550f, 120f), new Vector2(300f, 64f), Locale.Get("career.back"), 28, UiTheme.AccentDark);

            UiKit.Panel(p, "Detalle", new Vector2(1545f, 540f), new Vector2(640f, 700f), new Color(0f, 0f, 0f, 0.28f));
            _name = UiKit.Label(p, "Jugador", new Vector2(1545f, 840f), new Vector2(600f, 50f), string.Empty, 32, UiTheme.Text);
            _info = UiKit.Label(p, "Info", new Vector2(1545f, 715f), new Vector2(600f, 200f), string.Empty, 22, UiTheme.TextDim);
            _info.horizontalOverflow = HorizontalWrapMode.Wrap;
            _move = UiKit.Button(p, "Mover", new Vector2(1545f, 540f), new Vector2(560f, 72f), string.Empty, 26, UiTheme.AccentDark);
            _result = UiKit.Label(p, "Resultado", new Vector2(1545f, 440f), new Vector2(600f, 100f), string.Empty, 21, UiTheme.Accent);
            _result.horizontalOverflow = HorizontalWrapMode.Wrap;
            _hint = UiKit.Label(p, "Ayuda", new Vector2(1545f, 270f), new Vector2(600f, 180f), string.Empty, 18, UiTheme.TextDim);
            _hint.horizontalOverflow = HorizontalWrapMode.Wrap;
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

        private static string Name(Player p) => (p.FirstName + " " + p.LastName).Trim();

        private void Load()
        {
            int limit = Session.MaxReserveAge;
            _players = (_tabIndex == 0 ? Session.ReserveSquad(Data.UserClubId) : Session.Squad(Data.UserClubId).Where(p => p.AgeOn(Data.Today) <= limit).ToList())
                .OrderByDescending(p => Session.VisiblePotential(p)).ThenByDescending(p => PlayerRatings.Overall(p.Attributes, p.Position)).ToList();
        }

        /// <inheritdoc />
        public override void Refresh()
        {
            if (Session == null)
            {
                return;
            }

            Load();
            _selected = _selected >= _players.Count ? -1 : _selected;
            for (int i = 0; i < _tab.Length; i++)
            {
                _tab[i].Background.color = i == _tabIndex ? UiTheme.Selected : UiTheme.Button;
            }

            _title.text = CareerText.Format("reserve.count", Session.ReserveSquad(Data.UserClubId).Count, Session.MaxReserveSize);
            _grid.Clear();
            _grid.SetRow(0, Locale.Get("career.col.name"), Locale.Get("career.col.pos"), Locale.Get("career.col.age"), Locale.Get("career.col.rating"),
                Locale.Get("reserve.col.potential"), Locale.Get("reserve.col.minutes"), Locale.Get("reserve.col.morale"), Locale.Get("reserve.col.debut"));
            for (int k = 0; k < Rows && _offset + k < _players.Count; k++)
            {
                Player pl = _players[_offset + k];
                PlayerCondition c = Data.Conditions[pl.Id];
                int row = k + 1;
                _grid.SetRow(row, Name(pl), PlayerLabels.Code(pl.Position), CareerText.Num(pl.AgeOn(Data.Today)), CareerText.Num(Session.VisibleRating(pl)),
                    "~" + CareerText.Num(Session.VisiblePotential(pl)), CareerText.Num((int)c.SeasonMinutes), CareerText.Num((int)c.Morale), Locale.Get(c.Debuted ? "reserve.yes" : "reserve.no"));
                _grid.SetColor(row, 6, c.Morale >= 60f ? StatusColors.Good : c.Morale >= 40f ? StatusColors.Fair : StatusColors.Bad);
                _grid.Highlight(row, _offset + k == _selected);
            }

            int pages = Mathf.Max(1, (_players.Count + Rows - 1) / Rows);
            _page.text = CareerText.Format("career.page", _offset / Rows + 1, pages);
            _pagePrev.SetVisible(pages > 1);
            _pageNext.SetVisible(pages > 1);
            _hint.text = Locale.Get("reserve.hint" + _tabIndex);
            RefreshDetail();
        }

        private void RefreshDetail()
        {
            bool has = _selected >= 0 && _selected < _players.Count;
            _move.SetVisible(has);
            if (!has)
            {
                _name.text = Locale.Get("reserve.pick");
                _info.text = string.Empty;
                return;
            }

            Player pl = _players[_selected];
            PlayerCondition c = Data.Conditions[pl.Id];
            _name.text = Name(pl);
            _info.text = CareerText.Format("reserve.info", pl.AgeOn(Data.Today), PlayerLabels.Code(pl.Position), Session.VisibleRating(pl), Session.VisiblePotential(pl),
                CareerText.Money(pl.Contract?.WeeklyWage ?? 0), CareerText.Num((int)c.SeasonMinutes), CareerText.Num((int)c.Morale));
            SquadMove can = _tabIndex == 0 ? Session.CanPromote(pl.Id) : Session.CanDemote(pl.Id);
            _move.Label.text = Locale.Get(_tabIndex == 0 ? "reserve.promote" : "reserve.demote");
            _move.Background.color = can == SquadMove.Done ? UiTheme.AccentDark : UiTheme.Button;
            if (can != SquadMove.Done && _result.text.Length == 0)
            {
                _result.text = Locale.Get("reserve.move." + can.ToString().ToLowerInvariant());
            }
        }

        /// <inheritdoc />
        public override void Tap(Vector2 p)
        {
            if (_back.Contains(p)) { Host.Open(CareerSection.Squad); return; }
            for (int i = 0; i < _tab.Length; i++)
            {
                if (_tab[i].Contains(p)) { _tabIndex = i; _offset = 0; _selected = -1; _result.text = string.Empty; Refresh(); return; }
            }

            if (_pagePrev.Contains(p) && _offset >= Rows) { _offset -= Rows; _selected = -1; }
            else if (_pageNext.Contains(p) && _offset + Rows < _players.Count) { _offset += Rows; _selected = -1; }
            else if (_move.Root.activeSelf && _move.Contains(p) && _selected >= 0 && _selected < _players.Count)
            {
                int id = _players[_selected].Id;
                SquadMove r = _tabIndex == 0 ? Session.Promote(id) : Session.Demote(id);
                string text = Locale.Get(r == SquadMove.Done ? (_tabIndex == 0 ? "reserve.done_up" : "reserve.done_down") : "reserve.move." + r.ToString().ToLowerInvariant());
                if (r == SquadMove.Done)
                {
                    _selected = -1;
                }

                _result.text = text;
                Host.SetStatus(text);
            }
            else
            {
                int row = _grid.RowAt(p) - 1;
                if (row >= 0 && _offset + row < _players.Count)
                {
                    _selected = _offset + row;
                    _result.text = string.Empty;
                }
            }

            Refresh();
        }
    }
}
