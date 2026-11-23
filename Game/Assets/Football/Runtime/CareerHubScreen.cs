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
    /// jornada, clima, los dos escudos con sus estrellas, JUGAR y SIMULAR o AVANZAR) y una tira de calendario de cinco
    /// días. A la derecha, la tabla compacta. Todo se lee de <see cref="CareerSession"/>.
    /// </summary>
    public sealed class CareerHubScreen : CareerPage
    {
        private const int CalendarDays = 5;
        private const int PageRows = 15;
        private const int TableRows = PageRows + 1; // más el encabezado
        private const float TableRowHeight = 44f;
        private const float PagerY = 150f;
        private const int NewsRows = 5;
        private const int TabCount = 4;
        private const int CupMode = 3;
        private const int CupRows = 15;
        private const float CupRowHeight = 40f;
        private const float CrestSize = 120f;
        private const float DayWidth = 120f, DayStep = 128f;

        private readonly ClubCrest _bannerCrest, _homeCrest, _awayCrest;
        private readonly Text _bannerClub, _bannerCompetition, _date, _round, _weather, _homeName, _awayName, _versus, _noMatch;
        private readonly StarRating _homeStars, _awayStars;
        private readonly UiButton _play, _simulate, _advance, _accept, _keep;
        private readonly GameObject _requestGroup, _matchGroup;
        private readonly Text _request;
        private readonly Text[] _news = new Text[NewsRows];
        private readonly Text[] _dayName = new Text[CalendarDays], _dayNumber = new Text[CalendarDays], _dayMatch = new Text[CalendarDays];
        private readonly Image[] _dayCell = new Image[CalendarDays];
        private readonly UiButton[] _tableTabs = new UiButton[TabCount];
        private readonly TextGrid _table;
        private int _tableMode;
        private int _tablePage = -1; // -1: la página donde está el club del usuario
        private readonly UiButton _tablePrev, _tableNext;
        private readonly Text _tableRange, _cupTitle, _cupBye;
        private readonly TextGrid _cup;
        private readonly UiButton _openCalendar, _openHistory;
        private int _cupRound = -1; // -1: la ronda del usuario
        private int _autoKind = -1;  // 0 sin partido, 1 liga, 2 copa: al cambiar se elige la tabla que corresponde

        // cartel de campeón o de fin de torneo
        private readonly GameObject _ceremony;
        private readonly Text _ceremonyTitle, _ceremonySub, _ceremonyDetail;
        private readonly UiButton _ceremonyNext, _ceremonyHistory;
        private readonly Image[] _ceremonyStars = new Image[5];
        private readonly Queue<CompetitionRecord> _ceremonies = new Queue<CompetitionRecord>();

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
            UiKit.Panel(p, "Buzon", new Vector2(330f, 400f), new Vector2(600f, 440f), UiTheme.Panel);
            UiKit.Label(p, "BuzonTitulo", new Vector2(330f, 595f), new Vector2(560f, 40f), Locale.Get("career.news"), 26, UiTheme.Accent);
            _openCalendar = UiKit.Button(p, "AbrirCalendario", new Vector2(185f, 125f), new Vector2(285f, 64f), Locale.Get("career.cal.open"), 24, UiTheme.Button);
            _openHistory = UiKit.Button(p, "AbrirHistorial", new Vector2(475f, 125f), new Vector2(285f, 64f), Locale.Get("career.history.open"), 24, UiTheme.Button);
            for (int i = 0; i < NewsRows; i++)
            {
                _news[i] = UiKit.Label(p, "Noticia" + i, new Vector2(330f, 535f - i * 76f), new Vector2(560f, 70f), string.Empty, 19, UiTheme.Text, TextAnchor.MiddleLeft);
                _news[i].horizontalOverflow = HorizontalWrapMode.Wrap;
            }

            // Tarjeta del próximo partido.
            UiKit.Panel(p, "Partido", new Vector2(985f, 660f), new Vector2(680f, 640f), UiTheme.Panel);
            _date = UiKit.Label(p, "Fecha", new Vector2(985f, 945f), new Vector2(640f, 44f), string.Empty, 28, UiTheme.Text);
            _round = UiKit.Label(p, "Jornada", new Vector2(985f, 900f), new Vector2(640f, 40f), string.Empty, 22, UiTheme.Accent);
            _weather = UiKit.Label(p, "Clima", new Vector2(985f, 860f), new Vector2(640f, 36f), string.Empty, 20, UiTheme.TextDim);
            _matchGroup = CreateGroup(p, "Rivales").gameObject;
            Transform m = _matchGroup.transform;
            _homeCrest = new ClubCrest(m, new Vector2(820f, 740f), CrestSize);
            _awayCrest = new ClubCrest(m, new Vector2(1150f, 740f), CrestSize);
            _versus = UiKit.Label(m, "Vs", new Vector2(985f, 740f), new Vector2(120f, 60f), "VS", 40, UiTheme.TextDim);
            _homeName = UiKit.Label(m, "Local", new Vector2(820f, 650f), new Vector2(300f, 40f), string.Empty, 24, UiTheme.Text);
            _awayName = UiKit.Label(m, "Visitante", new Vector2(1150f, 650f), new Vector2(300f, 40f), string.Empty, 24, UiTheme.Text);
            _homeStars = new StarRating(m, new Vector2(820f, 600f));
            _awayStars = new StarRating(m, new Vector2(1150f, 600f));
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
            string[] keys = { "career.table.tournament", "career.table.annual", "career.table.averages", "career.table.cup" };
            for (int i = 0; i < _tableTabs.Length; i++)
            {
                _tableTabs[i] = UiKit.Button(p, "TablaModo" + i, new Vector2(1425f + i * 130f, 950f), new Vector2(124f, 52f), Locale.Get(keys[i]), 15, UiTheme.Button);
            }

            _tablePrev = UiKit.Button(p, "TablaAnterior", new Vector2(1490f, PagerY), new Vector2(90f, 46f), "<", 28, UiTheme.Button);
            _tableNext = UiKit.Button(p, "TablaSiguiente", new Vector2(1760f, PagerY), new Vector2(90f, 46f), ">", 28, UiTheme.Button);
            _tableRange = UiKit.Label(p, "TablaRango", new Vector2(1625f, PagerY), new Vector2(190f, 40f), string.Empty, 22, UiTheme.TextDim);
            _table = new TextGrid(p, "Posiciones", new Vector2(1370f, 890f), TableRowHeight, new[] { 1395f, 1560f, 1720f, 1790f, 1855f },
                new[] { 44f, 240f, 60f, 70f, 70f }, TableRows, 20);

            // Cruces de la copa: local, resultado y visitante de cada partido de la ronda.
            _cupTitle = UiKit.Label(p, "CopaTitulo", new Vector2(1625f, 890f), new Vector2(500f, 40f), string.Empty, 22, UiTheme.Gold);
            _cup = new TextGrid(p, "Cruces", new Vector2(1370f, 850f), CupRowHeight, new[] { 1480f, 1625f, 1770f }, new[] { 210f, 80f, 210f }, CupRows + 1, 18);
            _cupBye = UiKit.Label(p, "CopaDirecto", new Vector2(1625f, 205f), new Vector2(500f, 36f), string.Empty, 18, UiTheme.TextDim);

            // Cartel de campeón: tapa la central hasta que se toca CONTINUAR.
            _ceremony = CreateGroup(p, "Cartel").gameObject;
            Transform c = _ceremony.transform;
            UiKit.Panel(c, "Velo", new Vector2(960f, 540f), new Vector2(1920f, 1080f), new Color(0f, 0f, 0f, 0.75f));
            UiKit.Panel(c, "Tarjeta", new Vector2(960f, 540f), new Vector2(980f, 620f), new Color(0.1f, 0.13f, 0.18f, 0.98f));
            for (int i = 0; i < _ceremonyStars.Length; i++)
            {
                _ceremonyStars[i] = UiKit.Panel(c, "Trofeo" + i, new Vector2(960f + (i - 2) * 100f, 720f), new Vector2(84f, 84f), UiTheme.Gold);
                _ceremonyStars[i].sprite = UiKit.StarSprite;
            }

            _ceremonyTitle = UiKit.Label(c, "CartelTitulo", new Vector2(960f, 610f), new Vector2(900f, 90f), string.Empty, 64, UiTheme.Gold);
            _ceremonySub = UiKit.Label(c, "CartelSub", new Vector2(960f, 530f), new Vector2(900f, 50f), string.Empty, 32, UiTheme.Text);
            _ceremonyDetail = UiKit.Label(c, "CartelDetalle", new Vector2(960f, 470f), new Vector2(900f, 50f), string.Empty, 26, UiTheme.TextDim);
            _ceremonyNext = UiKit.Button(c, "CartelContinuar", new Vector2(760f, 350f), new Vector2(340f, 84f), Locale.Get("ceremony.continue"), 30, UiTheme.AccentDark);
            _ceremonyHistory = UiKit.Button(c, "CartelHistorial", new Vector2(1160f, 350f), new Vector2(340f, 84f), Locale.Get("ceremony.history"), 28, UiTheme.Button);
            _ceremony.SetActive(false);
        }

        /// <inheritdoc />
        public override void Refresh()
        {
            CareerData d = Data;
            Club user = d.Club(d.UserClubId);
            _bannerCrest.Set(TeamOf(user));
            _bannerClub.text = user.Name.ToUpperInvariant();
            _bannerCompetition.text = Session.CurrentTournament().Name;

            Fixture upcoming = Session.PendingUserFixture ?? Session.NextUserFixture();
            int kind = upcoming == null ? 0 : CareerSession.IsCup(upcoming) ? 2 : 1;
            if (kind != _autoKind)
            {
                // Cuando el próximo partido es de copa se muestran los cruces de la copa; con partido de liga, la tabla del torneo.
                _autoKind = kind;
                _tablePage = -1;
                if (kind == 2 && !d.TournamentOnly)
                {
                    _tableMode = CupMode;
                    _cupRound = -1;
                }
                else if (_tableMode == CupMode)
                {
                    _tableMode = 0;
                }
            }

            RefreshMatch(d);
            RefreshCalendar(d);
            RefreshRequest(d);
            RefreshCeremony();
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

            _openCalendar.SetVisible(!d.TournamentOnly);
            _openHistory.SetVisible(!d.TournamentOnly);
            FillTable(d);
        }

        // ------------------------------------------------------------------ cartel de campeón

        private void RefreshCeremony()
        {
            foreach (CompetitionRecord r in Session.TakeCeremonies())
            {
                _ceremonies.Enqueue(r);
            }

            if (!_ceremony.activeSelf && _ceremonies.Count > 0)
            {
                ShowCeremony(_ceremonies.Dequeue());
            }
        }

        private void ShowCeremony(CompetitionRecord r)
        {
            CareerData d = Data;
            _ceremony.SetActive(true);
            foreach (Image star in _ceremonyStars)
            {
                star.gameObject.SetActive(r.UserChampion);
            }

            if (r.UserChampion)
            {
                _ceremonyTitle.fontSize = 72;
                _ceremonyTitle.text = Locale.Get("ceremony.champion");
                _ceremonySub.text = CareerText.Format("ceremony.champion_sub", r.Name);
                _ceremonyDetail.text = string.Empty;
                return;
            }

            _ceremonyTitle.fontSize = 46;
            _ceremonyTitle.text = CareerText.Format("ceremony.end", r.Name.ToUpperInvariant());
            _ceremonySub.text = CareerText.Format("ceremony.end_sub", d.Club(r.ChampionClubId).Name);
            if (r.Kind == TournamentKind.Cup)
            {
                _ceremonyDetail.text = r.UserLastCupRound == 0 ? string.Empty
                    : r.UserLastCupRoundSize == 1 && r.UserLastCupRound > 1 ? Locale.Get("ceremony.cup_runner_up")
                    : CareerText.Format("ceremony.cup_out", CareerText.CupStage(r.UserLastCupRoundSize, r.UserLastCupRound));
            }
            else
            {
                _ceremonyDetail.text = CareerText.Format("ceremony.your_position", r.UserPosition);
            }
        }

        private bool TapCeremony(Vector2 p)
        {
            if (!_ceremony.activeSelf)
            {
                return false;
            }

            if (_ceremonyNext.Contains(p))
            {
                _ceremony.SetActive(_ceremonies.Count > 0);
                if (_ceremonies.Count > 0)
                {
                    ShowCeremony(_ceremonies.Dequeue());
                }
            }
            else if (_ceremonyHistory.Contains(p))
            {
                _ceremony.SetActive(false);
                Host.Open(CareerSection.History);
            }

            return true; // mientras está el cartel, la central no recibe toques
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
                SetSide(home, _homeCrest, _homeName, _homeStars);
                SetSide(away, _awayCrest, _awayName, _awayStars);
            }

            _play.SetVisible(today);
            _simulate.SetVisible(today);
            _advance.SetVisible(!today && !Session.TournamentOver);
        }

        private static void SetSide(Club club, ClubCrest crest, Text name, StarRating stars)
        {
            crest.Set(TeamOf(club));
            name.text = club.Name;
            stars.Set(RatingOf(club));
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

        private void FillCup(CareerData d)
        {
            int season = d.Season;
            int rounds = Session.CupRounds(season);
            _tablePrev.SetVisible(rounds > 1);
            _tableNext.SetVisible(rounds > 1);
            if (rounds == 0)
            {
                _cupTitle.text = Locale.Get("cup.none");
                _tableRange.text = string.Empty;
                return;
            }

            int round = Mathf.Clamp(_cupRound < 0 ? Session.UserCupRound(season) : _cupRound, 1, rounds);
            List<Fixture> ties = Session.CupFixtures(season, round);
            _cupTitle.text = CareerText.CupStage(ties.Count, round);
            _tableRange.text = round + " / " + rounds;
            _cup.SetRow(0, Locale.Get("calendar.col.home"), string.Empty, Locale.Get("calendar.col.away"));
            for (int k = 0; k < CupRows && k < ties.Count; k++)
            {
                Fixture f = ties[k];
                int row = k + 1;
                _cup.SetRow(row, d.Club(f.HomeClubId).ShortName, f.IsPlayed ? CareerText.Score(f) : "vs", d.Club(f.AwayClubId).ShortName);
                bool mine = f.HomeClubId == d.UserClubId || f.AwayClubId == d.UserClubId;
                _cup.Highlight(row, mine);
                if (f.IsPlayed)
                {
                    int winner = Session.CupWinner(f);
                    _cup.SetColor(row, 0, f.HomeClubId == winner ? UiTheme.Text : UiTheme.TextMuted);
                    _cup.SetColor(row, 2, f.AwayClubId == winner ? UiTheme.Text : UiTheme.TextMuted);
                }
            }

            List<int> byes = Session.CupByesOf(season, round);
            _cupBye.text = byes.Count == 0 ? string.Empty : CareerText.Format("cup.bye", string.Join(", ", byes.ConvertAll(id => d.Club(id).ShortName)));
        }

        private void FillTable(CareerData d)
        {
            _table.Clear();
            _cup.Clear();
            _cupTitle.text = string.Empty;
            _cupBye.text = string.Empty;
            if (_tableMode == CupMode)
            {
                FillCup(d);
                return;
            }

            bool averages = _tableMode == 2;
            _table.SetRow(0, "#", Locale.Get("career.col.club"), averages ? string.Empty : Locale.Get("career.col.played"),
                averages ? string.Empty : Locale.Get("career.col.gd"), Locale.Get(averages ? "career.col.average" : "career.col.points"));
            List<AverageRow> rows = averages ? Session.AveragesTable() : null;
            List<Standing> table = averages ? null : _tableMode == 0 ? Session.TournamentTable() : Session.AnnualTable();
            int count = averages ? rows.Count : table.Count;
            int userAt = averages ? rows.FindIndex(r => r.ClubId == d.UserClubId) : table.FindIndex(r => r.ClubId == d.UserClubId);
            int pages = Mathf.Max(1, (count + PageRows - 1) / PageRows);
            int page = Mathf.Clamp(_tablePage >= 0 ? _tablePage : Mathf.Max(0, userAt) / PageRows, 0, pages - 1);
            _tablePage = _tablePage < 0 ? -1 : page;
            _tablePrev.SetVisible(pages > 1);
            _tableNext.SetVisible(pages > 1);
            int first = page * PageRows;
            _tableRange.text = pages > 1 ? (first + 1) + "-" + Mathf.Min(count, first + PageRows) + " / " + count : string.Empty;
            for (int k = 0; k < PageRows && first + k < count; k++)
            {
                int i = first + k, row = k + 1;
                if (averages)
                {
                    _table.SetRow(row, CareerText.Num(i + 1), d.Club(rows[i].ClubId).Name, string.Empty, string.Empty,
                        rows[i].Average.ToString("0.000", CultureInfo.InvariantCulture));
                    _table.Highlight(row, rows[i].ClubId == d.UserClubId);
                }
                else
                {
                    Standing s = table[i];
                    _table.SetRow(row, CareerText.Num(i + 1), d.Club(s.ClubId).Name, CareerText.Num(s.Played), CareerText.Num(s.GoalDifference), CareerText.Num(s.Points));
                    _table.Highlight(row, s.ClubId == d.UserClubId);
                }
            }
        }

        private int CurrentPage()
        {
            CareerData d = Data;
            int count = _tableMode == 2 ? Session.AveragesTable().Count : (_tableMode == 0 ? Session.TournamentTable() : Session.AnnualTable()).Count;
            int userAt = _tableMode == 2 ? Session.AveragesTable().FindIndex(r => r.ClubId == d.UserClubId)
                : (_tableMode == 0 ? Session.TournamentTable() : Session.AnnualTable()).FindIndex(r => r.ClubId == d.UserClubId);
            return _tablePage >= 0 ? _tablePage : Mathf.Max(0, userAt) / PageRows;
        }

        /// <inheritdoc />
        public override void Tap(Vector2 p)
        {
            if (TapCeremony(p))
            {
                Refresh();
                return;
            }

            if (_openCalendar.Root.activeSelf && _openCalendar.Contains(p))
            {
                Host.Open(CareerSection.Calendar);
                return;
            }

            if (_openHistory.Root.activeSelf && _openHistory.Contains(p))
            {
                Host.Open(CareerSection.History);
                return;
            }

            for (int i = 0; i < _tableTabs.Length; i++)
            {
                if (_tableTabs[i].Root.activeSelf && _tableTabs[i].Contains(p))
                {
                    _tableMode = i;
                    _tablePage = -1;
                    Refresh();
                    return;
                }
            }

            if (_tableMode == CupMode && (_tablePrev.Root.activeSelf && _tablePrev.Contains(p) || _tableNext.Root.activeSelf && _tableNext.Contains(p)))
            {
                int rounds = Session.CupRounds(Data.Season);
                int current = _cupRound < 0 ? Session.UserCupRound(Data.Season) : _cupRound;
                _cupRound = Mathf.Clamp(current + (_tableNext.Contains(p) ? 1 : -1), 1, Mathf.Max(1, rounds));
                Refresh();
                return;
            }

            if (_tablePrev.Root.activeSelf && _tablePrev.Contains(p))
            {
                _tablePage = Mathf.Max(0, CurrentPage() - 1);
                Refresh();
                return;
            }

            if (_tableNext.Root.activeSelf && _tableNext.Contains(p))
            {
                _tablePage = CurrentPage() + 1;
                Refresh();
                return;
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
