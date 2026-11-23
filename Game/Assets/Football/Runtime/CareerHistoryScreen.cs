using System.Collections.Generic;
using System.Linq;
using Football.Core.Career;
using UnityEngine;
using UnityEngine.UI;

namespace Football.Game
{
    /// <summary>
    /// Historial de las competiciones del club del usuario: Apertura, Copa Argentina y Clausura de cada temporada, con su campeón y cómo terminó el
    /// usuario (campeón, puesto en la tabla, o hasta dónde llegó en la copa). Arriba, la cuenta de títulos.
    /// </summary>
    public sealed class CareerHistoryScreen : CareerPage
    {
        private const int Rows = 13;
        private static readonly float[] Centres = { 420f, 1020f, 1560f };
        private static readonly float[] Widths = { 600f, 520f, 460f };

        private readonly TextGrid _grid;
        private readonly UiButton _pagePrev, _pageNext, _back;
        private readonly Text _titles, _page;
        private int _offset;

        /// <summary>Crea la pantalla (oculta).</summary>
        public CareerHistoryScreen(Transform root, ICareerHost host) : base(root, "Historial", host)
        {
            Transform p = Panel;
            UiTheme.Frame(p, "Fondo", new Vector2(960f, 565f), new Vector2(1840f, 810f));
            UiKit.Label(p, "Titulo", new Vector2(520f, 932f), new Vector2(700f, 50f), Locale.Get("history.title"), 34, UiTheme.Accent, TextAnchor.MiddleLeft);
            _titles = UiKit.Label(p, "Titulos", new Vector2(1550f, 932f), new Vector2(500f, 50f), string.Empty, 30, UiTheme.Gold);
            _grid = new TextGrid(p, "Competiciones", new Vector2(100f, 872f), 52f, Centres, Widths, Rows + 1, 26);
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
                _offset = 0;
            }

            base.SetVisible(visible);
        }

        /// <inheritdoc />
        public override void Refresh()
        {
            if (Session == null)
            {
                return;
            }

            CareerData d = Data;
            List<CompetitionRecord> history = Session.CompetitionHistory();
            _titles.text = CareerText.Format("history.titles", history.Count(h => h.UserChampion));
            _grid.Clear();
            _grid.SetRow(0, Locale.Get("history.col.competition"), Locale.Get("history.col.champion"), Locale.Get("history.col.result"));
            for (int k = 0; k < Rows && _offset + k < history.Count; k++)
            {
                CompetitionRecord r = history[_offset + k];
                int row = k + 1;
                string champion = r.Finished ? d.Club(r.ChampionClubId).Name : Locale.Get("history.in_progress");
                if (r.Kind == TournamentKind.Promotion && r.Finished)
                {
                    champion = CareerText.Format("history.promotion_winner", champion);
                }
                _grid.SetRow(row, r.Name, champion, ResultText(r));
                _grid.SetColor(row, 0, r.Kind == TournamentKind.Cup ? UiTheme.Gold : UiTheme.Text);
                _grid.SetColor(row, 2, r.UserChampion ? UiTheme.Gold : r.Finished ? UiTheme.Text : UiTheme.TextDim);
                _grid.Highlight(row, r.UserChampion);
            }

            int pages = Mathf.Max(1, (history.Count + Rows - 1) / Rows);
            _page.text = CareerText.Format("career.page", _offset / Rows + 1, pages);
            _pagePrev.SetVisible(pages > 1);
            _pageNext.SetVisible(pages > 1);
        }

        /// <summary>Cómo terminó el usuario una competición, en palabras.</summary>
        public static string ResultText(CompetitionRecord r)
        {
            if (r.UserChampion)
            {
                return Locale.Get("history.champion");
            }

            if (r.Kind == TournamentKind.Promotion)
            {
                return Locale.Get(r.UserChampion ? "history.promotion_won" : "history.promotion_lost");
            }

            if (r.Kind == TournamentKind.Cup)
            {
                if (r.UserLastCupRound == 0)
                {
                    return Locale.Get("history.not_played");
                }

                if (!r.UserEliminated)
                {
                    return Locale.Get(r.Finished ? "history.not_played" : "history.cup_alive");
                }

                return r.UserLastCupRoundSize == 1 && r.UserLastCupRound > 1
                    ? Locale.Get("history.cup_runner_up")
                    : CareerText.Format("history.cup_out", CareerText.CupStage(r.UserLastCupRoundSize, r.UserLastCupRound));
            }

            return r.UserPosition > 0 ? CareerText.Format("history.position", r.UserPosition) : Locale.Get("history.not_played");
        }

        /// <inheritdoc />
        public override void Tap(Vector2 p)
        {
            if (_back.Contains(p))
            {
                Host.Open(CareerSection.Hub);
                return;
            }

            if (_pagePrev.Root.activeSelf && _pagePrev.Contains(p))
            {
                _offset = Mathf.Max(0, _offset - Rows);
            }
            else if (_pageNext.Root.activeSelf && _pageNext.Contains(p) && _offset + Rows < Session.CompetitionHistory().Count)
            {
                _offset += Rows;
            }

            Refresh();
        }
    }
}
