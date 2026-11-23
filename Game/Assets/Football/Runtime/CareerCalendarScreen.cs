using System.Collections.Generic;
using System.Globalization;
using Football.Core.Career;
using Football.Core.Domain;
using UnityEngine;
using UnityEngine.UI;

namespace Football.Game
{
    /// <summary>
    /// Calendario del club del usuario: todos sus partidos de una temporada en orden de fecha (Apertura, Copa Argentina y Clausura), los jugados con
    /// su resultado (verde ganó, amarillo empató, rojo perdió) y los que faltan. Se cambia de temporada con las flechas; las temporadas viejas
    /// quedan guardadas, así que se puede ver todo lo jugado.
    /// </summary>
    public sealed class CareerCalendarScreen : CareerPage
    {
        private const int Rows = 13;
        private static readonly float[] Centres = { 175f, 520f, 1010f, 1245f, 1480f, 1740f };
        private static readonly float[] Widths = { 150f, 490f, 330f, 150f, 330f, 100f };

        private readonly TextGrid _grid;
        private readonly UiButton _seasonPrev, _seasonNext, _pagePrev, _pageNext, _back;
        private readonly Text _season, _page, _title;
        private List<int> _seasons = new List<int>();
        private int _seasonIndex;
        private int _offset;

        /// <summary>Crea la pantalla (oculta).</summary>
        public CareerCalendarScreen(Transform root, ICareerHost host) : base(root, "Calendario", host)
        {
            Transform p = Panel;
            UiKit.Panel(p, "Fondo", new Vector2(960f, 565f), new Vector2(1840f, 810f), UiTheme.Panel);
            _title = UiKit.Label(p, "Titulo", new Vector2(330f, 932f), new Vector2(500f, 50f), Locale.Get("calendar.title"), 36, UiTheme.Accent, TextAnchor.MiddleLeft);
            _seasonPrev = UiKit.Button(p, "TemporadaAnterior", new Vector2(1300f, 932f), new Vector2(90f, 54f), "<", 30, UiTheme.Button);
            _season = UiKit.Label(p, "Temporada", new Vector2(1480f, 932f), new Vector2(260f, 54f), string.Empty, 28, UiTheme.Text);
            _seasonNext = UiKit.Button(p, "TemporadaSiguiente", new Vector2(1660f, 932f), new Vector2(90f, 54f), ">", 30, UiTheme.Button);
            _grid = new TextGrid(p, "Partidos", new Vector2(100f, 872f), 52f, Centres, Widths, Rows + 1, 24);
            _pagePrev = UiKit.Button(p, "PaginaAnterior", new Vector2(900f, 120f), new Vector2(110f, 56f), "<", 30, UiTheme.Button);
            _pageNext = UiKit.Button(p, "PaginaSiguiente", new Vector2(1280f, 120f), new Vector2(110f, 56f), ">", 30, UiTheme.Button);
            _page = UiKit.Label(p, "Pagina", new Vector2(1090f, 120f), new Vector2(240f, 56f), string.Empty, 22, UiTheme.TextDim);
            _back = UiKit.Button(p, "Volver", new Vector2(250f, 120f), new Vector2(240f, 64f), Locale.Get("career.back"), 28, UiTheme.AccentDark);
        }

        /// <inheritdoc />
        public override void SetVisible(bool visible)
        {
            if (visible)
            {
                _seasons = Session.SeasonsWithFixtures();
                _seasonIndex = Mathf.Max(0, _seasons.IndexOf(Data.Season)); // empieza en la temporada en curso
                _offset = FirstUnplayedOffset();
            }

            base.SetVisible(visible);
        }

        private int Season => _seasons.Count == 0 ? Data.Season : _seasons[Mathf.Clamp(_seasonIndex, 0, _seasons.Count - 1)];

        /// <summary>Página donde está el próximo partido por jugar, para que se vea lo que viene.</summary>
        private int FirstUnplayedOffset()
        {
            List<Fixture> mine = Session.UserFixtures(Season);
            int next = mine.FindIndex(f => !f.IsPlayed);
            return next < 0 ? 0 : next / Rows * Rows;
        }

        /// <inheritdoc />
        public override void Refresh()
        {
            if (Session == null)
            {
                return;
            }

            CareerData d = Data;
            List<Fixture> mine = Session.UserFixtures(Season);
            _season.text = CareerText.Format("calendar.season", Season);
            _seasonPrev.SetVisible(_seasonIndex < _seasons.Count - 1);
            _seasonNext.SetVisible(_seasonIndex > 0);
            _grid.Clear();
            _grid.SetRow(0, Locale.Get("calendar.col.date"), Locale.Get("calendar.col.competition"), Locale.Get("calendar.col.home"),
                Locale.Get("calendar.col.score"), Locale.Get("calendar.col.away"), string.Empty);
            int next = mine.FindIndex(f => !f.IsPlayed);
            for (int k = 0; k < Rows && _offset + k < mine.Count; k++)
            {
                Fixture f = mine[_offset + k];
                int row = k + 1;
                string competition = Session.CompetitionName(f) + "  ·  " + (CareerSession.IsCup(f)
                    ? CareerText.CupStage(Session.CupFixtures(Season, f.Round).Count, f.Round)
                    : CareerText.Format("career.matchday", f.Round));
                _grid.SetRow(row, f.Date.ToString("dd/MM/yy", CultureInfo.InvariantCulture), competition, d.Club(f.HomeClubId).ShortName, CareerText.Score(f),
                    d.Club(f.AwayClubId).ShortName, Letter(f));
                bool userHome = f.HomeClubId == d.UserClubId;
                _grid.SetColor(row, userHome ? 2 : 4, UiTheme.Accent);
                _grid.SetColor(row, 5, ResultColor(f));
                if (CareerSession.IsCup(f))
                {
                    _grid.SetColor(row, 1, UiTheme.Gold);
                }

                _grid.Highlight(row, _offset + k == next);
            }

            int pages = Mathf.Max(1, (mine.Count + Rows - 1) / Rows);
            _page.text = CareerText.Format("career.page", _offset / Rows + 1, pages);
            _pagePrev.SetVisible(pages > 1);
            _pageNext.SetVisible(pages > 1);
        }

        private string Letter(Fixture f)
        {
            if (!f.IsPlayed)
            {
                return string.Empty;
            }

            int mine = f.HomeClubId == Data.UserClubId ? f.HomeGoals.Value : f.AwayGoals.Value;
            int theirs = f.HomeClubId == Data.UserClubId ? f.AwayGoals.Value : f.HomeGoals.Value;
            if (mine == theirs && CareerSession.IsCup(f))
            {
                return Locale.Get(Session.CupWinner(f) == Data.UserClubId ? "calendar.win" : "calendar.loss"); // la copa se define por penales
            }

            return Locale.Get(mine > theirs ? "calendar.win" : mine == theirs ? "calendar.draw" : "calendar.loss");
        }

        private Color ResultColor(Fixture f)
        {
            string l = Letter(f);
            return l == Locale.Get("calendar.win") ? StatusColors.Good : l == Locale.Get("calendar.draw") ? StatusColors.Fair : StatusColors.Bad;
        }

        /// <inheritdoc />
        public override void Tap(Vector2 p)
        {
            if (_back.Contains(p))
            {
                Host.Open(CareerSection.Hub);
                return;
            }

            if (_seasonPrev.Root.activeSelf && _seasonPrev.Contains(p))
            {
                _seasonIndex = Mathf.Min(_seasons.Count - 1, _seasonIndex + 1);
                _offset = FirstUnplayedOffset();
            }
            else if (_seasonNext.Root.activeSelf && _seasonNext.Contains(p))
            {
                _seasonIndex = Mathf.Max(0, _seasonIndex - 1);
                _offset = FirstUnplayedOffset();
            }
            else if (_pagePrev.Root.activeSelf && _pagePrev.Contains(p))
            {
                _offset = Mathf.Max(0, _offset - Rows);
            }
            else if (_pageNext.Root.activeSelf && _pageNext.Contains(p) && _offset + Rows < Session.UserFixtures(Season).Count)
            {
                _offset += Rows;
            }

            Refresh();
        }
    }
}
