using System;
using System.Collections.Generic;
using System.Globalization;
using Football.Core.Career;
using Football.Core.Competitions;
using Football.Core.Data;
using Football.Core.Domain;
using Football.Core.MatchSim;
using UnityEngine;
using UnityEngine.UI;

namespace Football.Game
{
    /// <summary>
    /// Central de la carrera: arriba a la izquierda, el cartel con el escudo y la competición; debajo, el pedido de los socios
    /// (si lo hay) con «Aceptar rebaja» y «Mantener precio», y el buzón. En el centro, la tarjeta del próximo partido (fecha,
    /// jornada, clima, los dos escudos con camiseta y estrellas, JUGAR y SIMULAR o AVANZAR) y una tira de calendario de cinco
    /// días. A la derecha, la tabla compacta. Todo se lee de <see cref="CareerSession"/>.
    /// </summary>
    public sealed class CareerHubScreen : CareerPage
    {
        private const int CalendarDays = 5;
        private const int TableRows = 16;
        private const int NewsRows = 6;
        private const float CrestSize = 120f;
        private const float DayWidth = 120f, DayStep = 128f;

        private readonly ClubCrest _bannerCrest, _homeCrest, _awayCrest;
        private readonly Text _bannerClub, _bannerCompetition, _date, _round, _weather, _homeName, _awayName, _versus, _noMatch;
        private readonly KitPreview _homeKit, _awayKit;
        private readonly StarRating _homeStars, _awayStars;
        private readonly UiButton _play, _simulate, _advance, _accept, _keep;
        private readonly GameObject _requestGroup, _matchGroup;
        private readonly Text _request;
        private readonly Text[] _news = new Text[NewsRows];
        private readonly Text[] _dayName = new Text[CalendarDays], _dayNumber = new Text[CalendarDays], _dayMatch = new Text[CalendarDays];
        private readonly Image[] _dayCell = new Image[CalendarDays];
        private readonly UiButton[] _tableTabs = new UiButton[3];
        private readonly TextGrid _table;
        private int _tableMode;

        /// <summary>Crea la pantalla (oculta).</summary>
        public CareerHubScreen(Transform root, ICareerHost host) : base(root, "Central", host)
        {
            Transform p = Panel;

            // Cartel del club y la competición.
            UiKit.Panel(p, "Cartel", new Vector2(330f, 915f), new Vector2(600f, 130f), UiTheme.Panel);
            UiKit.Panel(p, "CartelAcento", new Vector2(34f, 915f), new Vector2(8f, 130f), UiTheme.Accent);
            _bannerCrest = new ClubCrest(p, new Vector2(110f, 915f), 100f);
            _bannerClub = UiKit.Label(p, "CartelClub", new Vector2(400f, 940f), new Vector2(420f, 50f), string.Empty, 30, UiTheme.Text, TextAnchor.MiddleLeft);
            _bannerCompetition = UiKit.Label(p, "CartelTorneo", new Vector2(400f, 890f), new Vector2(420f, 40f), string.Empty, 22, UiTheme.Accent, TextAnchor.MiddleLeft);

            // Pedido de los socios.
            _requestGroup = CreateGroup(p, "PedidoSocios").gameObject;
            Transform r = _requestGroup.transform;
            UiKit.Panel(r, "PedidoFondo", new Vector2(330f, 745f), new Vector2(600f, 180f), new Color(0.35f, 0.22f, 0.08f, 0.92f));
            _request = UiKit.Label(r, "Pedido", new Vector2(330f, 790f), new Vector2(560f, 80f), string.Empty, 20, UiTheme.Text, TextAnchor.MiddleLeft);
            _request.horizontalOverflow = HorizontalWrapMode.Wrap;
            _accept = UiKit.Button(r, "Aceptar", new Vector2(185f, 700f), new Vector2(280f, 64f), Locale.Get("supporters.accept"), 20, UiTheme.AccentDark);
            _keep = UiKit.Button(r, "Mantener", new Vector2(475f, 700f), new Vector2(280f, 64f), Locale.Get("supporters.keep"), 20, UiTheme.Button);

            // Buzón.
            UiKit.Panel(p, "Buzon", new Vector2(330f, 360f), new Vector2(600f, 500f), UiTheme.Panel);
            UiKit.Label(p, "BuzonTitulo", new Vector2(330f, 580f), new Vector2(560f, 40f), Locale.Get("career.news"), 26, UiTheme.Accent);
            for (int i = 0; i < NewsRows; i++)
            {
                _news[i] = UiKit.Label(p, "Noticia" + i, new Vector2(330f, 520f - i * 70f), new Vector2(560f, 66f), string.Empty, 19, UiTheme.Text, TextAnchor.MiddleLeft);
                _news[i].horizontalOverflow = HorizontalWrapMode.Wrap;
            }

            // Tarjeta del próximo partido.
            UiKit.Panel(p, "Partido", new Vector2(985f, 660f), new Vector2(680f, 640f), UiTheme.Panel);
            _date = UiKit.Label(p, "Fecha", new Vector2(985f, 945f), new Vector2(640f, 44f), string.Empty, 28, UiTheme.Text);
            _round = UiKit.Label(p, "Jornada", new Vector2(985f, 900f), new Vector2(640f, 40f), string.Empty, 22, UiTheme.Accent);
            _weather = UiKit.Label(p, "Clima", new Vector2(985f, 860f), new Vector2(640f, 36f), string.Empty, 20, UiTheme.TextDim);
            _matchGroup = CreateGroup(p, "Rivales").gameObject;
            Transform m = _matchGroup.transform;
            _homeCrest = new ClubCrest(m, new Vector2(820f, 760f), CrestSize);
            _awayCrest = new ClubCrest(m, new Vector2(1150f, 760f), CrestSize);
            _versus = UiKit.Label(m, "Vs", new Vector2(985f, 760f), new Vector2(120f, 60f), "VS", 40, UiTheme.TextDim);
            _homeName = UiKit.Label(m, "Local", new Vector2(820f, 670f), new Vector2(300f, 40f), string.Empty, 24, UiTheme.Text);
            _awayName = UiKit.Label(m, "Visitante", new Vector2(1150f, 670f), new Vector2(300f, 40f), string.Empty, 24, UiTheme.Text);
            _homeStars = new StarRating(m, new Vector2(820f, 625f));
            _awayStars = new StarRating(m, new Vector2(1150f, 625f));
            _homeKit = new KitPreview(m, new Vector2(820f, 520f));
            _awayKit = new KitPreview(m, new Vector2(1150f, 520f));
            _noMatch = UiKit.Label(p, "SinPartido", new Vector2(985f, 680f), new Vector2(620f, 60f), string.Empty, 28, UiTheme.TextDim);
            _play = UiKit.Button(p, "Jugar", new Vector2(855f, 395f), new Vector2(250f, 84f), Locale.Get("career.play"), 34, UiTheme.AccentDark);
            _simulate = UiKit.Button(p, "Simular", new Vector2(1115f, 395f), new Vector2(250f, 84f), Locale.Get("career.simulate"), 30, UiTheme.Button);
            _advance = UiKit.Button(p, "Avanzar", new Vector2(985f, 395f), new Vector2(380f, 84f), Locale.Get("career.advance"), 30, UiTheme.Button);

            // Calendario de cinco días.
            for (int i = 0; i < CalendarDays; i++)
            {
                float x = 985f + (i - CalendarDays / 2) * DayStep;
                _dayCell[i] = UiKit.Panel(p, "Dia" + i, new Vector2(x, 235f), new Vector2(DayWidth, 170f), UiTheme.Panel);
                _dayName[i] = UiKit.Label(p, "DiaNombre" + i, new Vector2(x, 295f), new Vector2(DayWidth, 34f), string.Empty, 18, UiTheme.TextDim);
                _dayNumber[i] = UiKit.Label(p, "DiaNumero" + i, new Vector2(x, 250f), new Vector2(DayWidth, 50f), string.Empty, 34, UiTheme.Text);
                _dayMatch[i] = UiKit.Label(p, "DiaPartido" + i, new Vector2(x, 190f), new Vector2(DayWidth, 50f), string.Empty, 18, UiTheme.Accent);
            }

            // Tabla compacta.
            UiKit.Panel(p, "Tabla", new Vector2(1625f, 560f), new Vector2(530f, 860f), UiTheme.Panel);
            string[] keys = { "career.table.tournament", "career.table.annual", "career.table.averages" };
            for (int i = 0; i < _tableTabs.Length; i++)
            {
                _tableTabs[i] = UiKit.Button(p, "TablaModo" + i, new Vector2(1460f + i * 167f, 950f), new Vector2(160f, 52f), Locale.Get(keys[i]), 17, UiTheme.Button);
            }

            _table = new TextGrid(p, "Posiciones", new Vector2(1370f, 890f), 48f, new[] { 1395f, 1560f, 1720f, 1790f, 1855f },
                new[] { 44f, 240f, 60f, 70f, 70f }, TableRows, 20);
        }

        /// <inheritdoc />
        public override void Refresh()
        {
            CareerData d = Data;
            Club user = d.Club(d.UserClubId);
            _bannerCrest.Set(TeamOf(user));
            _bannerClub.text = user.Name.ToUpperInvariant();
            _bannerCompetition.text = Session.CurrentTournament().Name;

            RefreshMatch(d);
            RefreshCalendar(d);
            RefreshRequest(d);
            IReadOnlyList<NewsItem> items = Session.Inbox.Items;
            for (int i = 0; i < NewsRows; i++)
            {
                _news[i].text = i < items.Count ? items[i].Date.ToString("dd/MM", CultureInfo.InvariantCulture) + "  " + Host.NewsText(items[i]) : string.Empty;
            }

            for (int i = 0; i < _tableTabs.Length; i++)
            {
                _tableTabs[i].Background.color = i == _tableMode ? UiTheme.Selected : UiTheme.Button;
                _tableTabs[i].SetVisible(i == 0 || !d.TournamentOnly);
            }

            FillTable(d);
        }

        private void RefreshMatch(CareerData d)
        {
            Fixture next = Session.PendingUserFixture ?? Session.NextUserFixture();
            bool today = Session.PendingUserFixture != null;
            _matchGroup.SetActive(next != null);
            _noMatch.gameObject.SetActive(next == null);
            if (next == null)
            {
                _noMatch.text = Session.TournamentOver
                    ? CareerText.Format("career.tournament_over", d.Club(Session.ChampionId()).Name)
                    : Locale.Get("career.no_match");
                _date.text = d.Today.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
                _round.text = _weather.text = string.Empty;
            }
            else
            {
                Club home = d.Club(next.HomeClubId), away = d.Club(next.AwayClubId);
                _date.text = (today ? Locale.Get("career.today") + "  ·  " : string.Empty) + next.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
                _round.text = CareerSession.IsCup(next)
                    ? Locale.Get("career.cup") + "  ·  " + CareerText.Format(Session.IsCupFinal(next) ? "career.cup_final" : "career.cup_round", next.Round)
                    : CareerText.Format("career.matchday", next.Round) + "  ·  " + Session.CurrentTournament().Name;
                MatchForecast forecast = Session.ForecastOf(next);
                _weather.text = Locale.Get("weather." + (int)forecast.Weather) + "  ·  " + CareerText.Num(forecast.Temperature) + " °C"
                                + "  ·  " + d.Stadiums[next.HomeClubId].Name;
                SetSide(home, _homeCrest, _homeName, _homeStars, _homeKit);
                SetSide(away, _awayCrest, _awayName, _awayStars, _awayKit);
            }

            _play.SetVisible(today);
            _simulate.SetVisible(today);
            _advance.SetVisible(!today && !Session.TournamentOver);
        }

        private static void SetSide(Club club, ClubCrest crest, Text name, StarRating stars, KitPreview kit)
        {
            TeamDefinition team = TeamOf(club);
            crest.Set(team);
            name.text = club.Name;
            stars.Set(RatingOf(club));
            kit.Set(KitColors.From(team.Home));
        }

        private void RefreshCalendar(CareerData d)
        {
            for (int i = 0; i < CalendarDays; i++)
            {
                DateTime day = d.Today.Date.AddDays(i);
                _dayName[i].text = Locale.Get("day." + (int)day.DayOfWeek);
                _dayNumber[i].text = CareerText.Num(day.Day);
                Fixture f = d.Fixtures.Find(x => x.Date.Date == day && (x.HomeClubId == d.UserClubId || x.AwayClubId == d.UserClubId));
                bool cup = f != null && CareerSession.IsCup(f);
                _dayMatch[i].text = f == null ? string.Empty
                    : (f.HomeClubId == d.UserClubId ? Locale.Get("career.home_short") : Locale.Get("career.away_short")) + " "
                      + d.Club(f.HomeClubId == d.UserClubId ? f.AwayClubId : f.HomeClubId).ShortName;
                _dayMatch[i].color = cup ? UiTheme.Gold : UiTheme.Accent;
                _dayCell[i].color = i == 0 ? UiTheme.Selected : f != null ? UiTheme.Button : UiTheme.Panel;
            }
        }

        private void RefreshRequest(CareerData d)
        {
            bool pending = !d.TournamentOnly && Session.SupporterRequestPending;
            _requestGroup.SetActive(pending);
            if (!pending)
            {
                return;
            }

            TicketPreview preview = Session.PreviewNextHome();
            _request.text = CareerText.Format("supporters.request", CareerText.Price(preview.Price), CareerText.Price(preview.MinIdeal),
                CareerText.Price(preview.MaxIdeal), Mathf.RoundToInt(d.Supporters.Unrest));
        }

        private void FillTable(CareerData d)
        {
            _table.Clear();
            bool averages = _tableMode == 2;
            _table.SetRow(0, "#", Locale.Get("career.col.club"), averages ? string.Empty : Locale.Get("career.col.played"),
                averages ? string.Empty : Locale.Get("career.col.gd"), Locale.Get(averages ? "career.col.average" : "career.col.points"));
            if (averages)
            {
                List<AverageRow> rows = Session.AveragesTable();
                for (int i = 0; i < rows.Count && i + 1 < TableRows; i++)
                {
                    _table.SetRow(i + 1, CareerText.Num(i + 1), d.Club(rows[i].ClubId).Name, string.Empty, string.Empty,
                        rows[i].Average.ToString("0.000", CultureInfo.InvariantCulture));
                    _table.Highlight(i + 1, rows[i].ClubId == d.UserClubId);
                }

                return;
            }

            List<Standing> table = _tableMode == 0 ? Session.TournamentTable() : Session.AnnualTable();
            for (int i = 0; i < table.Count && i + 1 < TableRows; i++)
            {
                Standing s = table[i];
                _table.SetRow(i + 1, CareerText.Num(i + 1), d.Club(s.ClubId).Name, CareerText.Num(s.Played), CareerText.Num(s.GoalDifference), CareerText.Num(s.Points));
                _table.Highlight(i + 1, s.ClubId == d.UserClubId);
            }
        }

        /// <inheritdoc />
        public override void Tap(Vector2 p)
        {
            for (int i = 0; i < _tableTabs.Length; i++)
            {
                if (_tableTabs[i].Contains(p))
                {
                    _tableMode = i;
                    Refresh();
                    return;
                }
            }

            if (_requestGroup.activeSelf && (_accept.Contains(p) || _keep.Contains(p)))
            {
                bool accept = _accept.Contains(p);
                Session.AnswerSupporters(accept);
                Host.SetStatus(Locale.Get(accept ? "supporters.accepted" : "supporters.kept"));
                Refresh();
                return;
            }

            if (_play.Contains(p))
            {
                Host.Request(CareerAction.Play);
            }
            else if (_simulate.Contains(p))
            {
                Fixture f = Session.PendingUserFixture;
                MatchReport r = Session.SimulateUserMatch();
                CareerData d = Data;
                Host.SetStatus(d.Club(f.HomeClubId).Name + "  " + r.HomeGoals + " - " + r.AwayGoals + "  " + d.Club(f.AwayClubId).Name);
                AfterDay(Session.Data.Manager.Sacked ? DayOutcome.ManagerSacked : DayOutcome.Continue);
            }
            else if (_advance.Contains(p))
            {
                AfterDay(Session.AdvanceToNextMatch());
            }
        }

        private void AfterDay(DayOutcome outcome)
        {
            if (outcome == DayOutcome.SeasonFinished)
            {
                Host.SetStatus(Locale.Get("career.season_over"));
            }
            else if (outcome == DayOutcome.ManagerSacked)
            {
                Host.SetStatus(Locale.Get("career.sacked"));
                Host.Request(CareerAction.Sacked);
                return;
            }

            Refresh();
        }
    }
}
