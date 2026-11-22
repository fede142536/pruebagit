using System;
using System.Collections.Generic;
using System.Globalization;
using Football.Core.Career;
using Football.Core.Competitions;
using Football.Core.Data;
using Football.Core.Domain;
using Football.Core.Management;
using Football.Core.MatchCore;
using UnityEngine;
using UnityEngine.UI;

namespace Football.Game
{
    /// <summary>Lo que pidió la persona en la pantalla de carrera este fotograma.</summary>
    public enum CareerAction
    {
        /// <summary>Nada.</summary>
        None,
        /// <summary>Empezar una partida nueva con el club elegido.</summary>
        NewGame,
        /// <summary>Seguir la partida guardada.</summary>
        LoadGame,
        /// <summary>Jugar en 3D el partido de hoy.</summary>
        Play,
        /// <summary>Guardar.</summary>
        Save,
        /// <summary>Volver al menú principal.</summary>
        Exit
    }

    /// <summary>
    /// Pantalla del Modo Carrera y del Torneo, armada con UiKit (sin EventSystem): pestañas INICIO (próximo rival, JUGAR,
    /// SIMULAR, AVANZAR, tablas Torneo/Anual/Promedios y buzón), PLANTEL (forma, moral, físico, contrato, estado y táctica
    /// con doble dibujo, estilo y roles por línea), MERCADO (búsqueda por rangos, ojeadores, ofertas, cláusulas y ofertas
    /// recibidas) y CLUB (finanzas, pases entre presupuestos, infraestructura y cuerpo técnico). En el Torneo solo hay
    /// INICIO y PLANTEL. No tiene lógica de juego: todo lo hace <see cref="CareerSession"/>. Respeta el área segura.
    /// </summary>
    public sealed class CareerScreen : MenuScreenBase
    {
        private const int TableRows = 16;
        private const int NewsRows = 6;
        private const int SquadRows = 13;
        private const int MarketRows = 8;
        private const int OfferRows = 3;
        private const int AgeStep = 1;
        private const int RatingStep = 2;
        private const float FeeStep = 0.1f;
        private const float BudgetMoveShare = 0.1f;
        private const int StaffImprovement = 2;
        private const int MaxStaffAbility = 20;
        private const long Million = 1_000_000;
        private static readonly Vector2 Centre = new Vector2(960f, 540f);
        private static readonly string[] FormationNames = { "4-4-2", "4-3-3", "4-2-3-1", "4-3-1-2" };
        private static readonly string[] StyleNames = { "Balanced", "Possession", "Counter Attack", "High Press", "Defensive" };
        private static readonly PlayerDuty[] FullbackDuties = { PlayerDuty.Auto, PlayerDuty.Defensive, PlayerDuty.WingBack };
        private static readonly PlayerDuty[] MidfieldDuties = { PlayerDuty.Auto, PlayerDuty.BallWinner, PlayerDuty.Playmaker, PlayerDuty.BoxToBox };
        private static readonly PlayerDuty[] StrikerDuties = { PlayerDuty.Auto, PlayerDuty.TargetMan, PlayerDuty.Runner };
        private static readonly Position?[] PositionFilter =
            { null, Position.Goalkeeper, Position.CenterBack, Position.LeftBack, Position.RightBack, Position.DefensiveMidfielder,
              Position.CentralMidfielder, Position.AttackingMidfielder, Position.LeftWinger, Position.RightWinger, Position.Striker };

        private enum Tab { Home, Squad, Market, Club }

        private readonly Dictionary<string, string> _newsTemplates;
        private readonly Text _header;
        private readonly Text _status;
        private readonly UiButton[] _tabs = new UiButton[4];
        private readonly UiButton _save, _exit;
        private readonly GameObject[] _panels = new GameObject[4];
        private readonly GameObject _startPanel;
        private CareerSession _session;
        private PlayerCardPanel _card;
        private Tab _tab;
        private Vector2 _shift;
        private float _scale = 1f;

        /// <summary>Acción pedida este fotograma.</summary>
        public CareerAction Action { get; private set; }

        /// <summary>Club elegido para la partida nueva (índice de <see cref="TeamCatalog.Teams"/>).</summary>
        public int ChosenTeam { get; private set; }

        // inicio
        private Text _nextTitle, _nextLine, _nextInfo;
        private UiButton _play, _simulate, _advance;
        private readonly UiButton[] _tableTabs = new UiButton[3];
        private TextGrid _table;
        private int _tableMode;
        private readonly Text[] _news = new Text[NewsRows];

        // plantel
        private TextGrid _squad;
        private UiButton _squadPrev, _squadNext;
        private Text _squadPage;
        private int _squadOffset;
        private readonly UiButton[] _tactic = new UiButton[6];

        // mercado
        private UiButton _filterPos, _ageMinus, _agePlus, _ageMaxMinus, _ageMaxPlus, _rateMinus, _ratePlus, _rateMaxMinus, _rateMaxPlus, _search;
        private Text _filterText, _windowText;
        private TextGrid _results;
        private Image _resultMark;
        private readonly List<Player> _found = new List<Player>();
        private int _selected = -1;
        private int _posFilter, _minAge = 18, _maxAge = 34, _minRating = 55, _maxRating = 90;
        private long _fee;
        private UiButton _scout, _feeMinus, _feePlus, _offer, _clause;
        private Text _feeText;
        private readonly Text[] _offerLines = new Text[OfferRows];
        private readonly UiButton[] _accept = new UiButton[OfferRows], _reject = new UiButton[OfferRows];

        // club
        private TextGrid _money;
        private UiButton _toWages, _toTransfers;
        private readonly Text[] _facilityText = new Text[4];
        private readonly UiButton[] _upgrade = new UiButton[4];
        private readonly Text[] _staffText = new Text[4];
        private readonly UiButton[] _hire = new UiButton[4];
        private static readonly StaffRole[] ShownStaff = { StaffRole.Manager, StaffRole.Scout, StaffRole.Physio, StaffRole.YouthCoach };

        // inicio de partida
        private Text _startTitle, _startClub;
        private UiButton _startPrev, _startNext, _startGo, _startLoad;

        /// <summary>Crea la pantalla (oculta).</summary>
        public CareerScreen(Transform parent, PointerInput pointers) : base(parent, pointers, "CarreraUI")
        {
            _newsTemplates = LoadNews();
            _header = UiKit.Label(Root, "Cabecera", new Vector2(560f, 1030f), new Vector2(1000f, 50f), string.Empty, 30, UiTheme.Text, TextAnchor.MiddleLeft);
            _status = UiKit.Label(Root, "Estado", new Vector2(960f, 28f), new Vector2(1800f, 40f), string.Empty, 24, UiTheme.Accent);
            string[] tabKeys = { "career.tab.home", "career.tab.squad", "career.tab.market", "career.tab.club" };
            for (int i = 0; i < _tabs.Length; i++)
            {
                _tabs[i] = UiKit.Button(Root, "Pestana" + i, new Vector2(1120f + i * 150f, 1030f), new Vector2(140f, 54f), Locale.Get(tabKeys[i]), 22, UiTheme.Button);
            }

            _save = UiKit.Button(Root, "Guardar", new Vector2(1720f, 1030f), new Vector2(120f, 54f), Locale.Get("career.save"), 22, UiTheme.AccentDark);
            _exit = UiKit.Button(Root, "Salir", new Vector2(1850f, 1030f), new Vector2(110f, 54f), Locale.Get("career.exit"), 22, UiTheme.Button);
            for (int i = 0; i < _panels.Length; i++)
            {
                _panels[i] = Group("Panel" + i);
            }

            BuildHome(_panels[(int)Tab.Home].transform);
            BuildSquad(_panels[(int)Tab.Squad].transform);
            BuildMarket(_panels[(int)Tab.Market].transform);
            BuildClub(_panels[(int)Tab.Club].transform);
            _startPanel = Group("Inicio");
            BuildStart(_startPanel.transform);
            _card = new PlayerCardPanel(parent);
        }

        // ------------------------------------------------------------------ ciclo

        /// <summary>Muestra la elección de club (partida nueva o continuar).</summary>
        /// <param name="tournament">Modo Torneo.</param>
        /// <param name="favorite">Club preseleccionado.</param>
        /// <param name="hasSave">Hay una partida guardada para continuar.</param>
        public void ShowStart(bool tournament, int favorite, bool hasSave)
        {
            _session = null;
            ChosenTeam = Mathf.Clamp(favorite, 0, TeamCatalog.Teams.Length - 1);
            _startTitle.text = Locale.Get(tournament ? "career.new_tournament" : "career.new_title");
            _startLoad.SetVisible(hasSave);
            _startClub.text = TeamCatalog.Teams[ChosenTeam].Name;
            SetTopBarVisible(false);
            _startPanel.SetActive(true);
            foreach (GameObject p in _panels)
            {
                p.SetActive(false);
            }

            _status.text = string.Empty;
            Show();
        }

        /// <summary>Muestra la partida en curso.</summary>
        /// <param name="session">Sesión.</param>
        public void ShowSession(CareerSession session)
        {
            _session = session;
            _startPanel.SetActive(false);
            SetTopBarVisible(true);
            bool career = !session.Data.TournamentOnly;
            _tabs[(int)Tab.Market].SetVisible(career);
            _tabs[(int)Tab.Club].SetVisible(career);
            SelectTab(Tab.Home);
            Show();
        }

        /// <summary>Mensaje en la línea de estado de abajo.</summary>
        public void SetStatus(string text)
        {
            _status.text = text ?? string.Empty;
        }

        /// <inheritdoc />
        public override void Show()
        {
            base.Show();
            FitSafeArea();
        }

        /// <inheritdoc />
        public override void Hide()
        {
            base.Hide();
            _card?.Hide();
        }

        /// <summary>Procesa los toques del fotograma.</summary>
        public void Update()
        {
            Action = CareerAction.None;
            if (BackKey)
            {
                Action = CareerAction.Exit;
                return;
            }

            int n = Taps(out Vector2[] taps);
            if (_card.Visible)
            {
                if (n > 0)
                {
                    _card.Hide(); // cualquier toque cierra la ficha
                }

                return;
            }

            for (int t = 0; t < n; t++)
            {
                Vector2 p = Local(taps[t]);
                if (_session == null)
                {
                    TapStart(p);
                    continue;
                }

                if (_save.Contains(p)) { Action = CareerAction.Save; }
                else if (_exit.Contains(p)) { Action = CareerAction.Exit; }
                else
                {
                    for (int i = 0; i < _tabs.Length; i++)
                    {
                        if (_tabs[i].Contains(p))
                        {
                            SelectTab((Tab)i);
                        }
                    }

                    switch (_tab)
                    {
                        case Tab.Home: TapHome(p); break;
                        case Tab.Squad: TapSquad(p); break;
                        case Tab.Market: TapMarket(p); break;
                        case Tab.Club: TapClub(p); break;
                    }
                }
            }
        }

        /// <summary>Vuelve a dibujar la pestaña actual (después de un partido, por ejemplo).</summary>
        public void Refresh()
        {
            if (_session == null)
            {
                return;
            }

            CareerData d = _session.Data;
            _header.text = d.Club(d.UserClubId).Name.ToUpperInvariant() + "   ·   " + d.Today.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                           + "   ·   " + _session.CurrentTournament().Name;
            switch (_tab)
            {
                case Tab.Home: RefreshHome(); break;
                case Tab.Squad: RefreshSquad(); break;
                case Tab.Market: RefreshMarket(); break;
                case Tab.Club: RefreshClub(); break;
            }
        }

        private void SelectTab(Tab tab)
        {
            _tab = tab;
            for (int i = 0; i < _panels.Length; i++)
            {
                _panels[i].SetActive(i == (int)tab);
                _tabs[i].Background.color = i == (int)tab ? UiTheme.Selected : UiTheme.Button;
            }

            Refresh();
        }

        private void SetTopBarVisible(bool visible)
        {
            foreach (UiButton b in _tabs)
            {
                b.SetVisible(visible);
            }

            _save.SetVisible(visible);
            _exit.SetVisible(visible);
            _header.gameObject.SetActive(visible);
        }

        // ------------------------------------------------------------------ elección de club

        private void BuildStart(Transform p)
        {
            _startTitle = UiKit.Label(p, "Titulo", new Vector2(960f, 880f), new Vector2(1400f, 90f), string.Empty, 64, UiTheme.Accent);
            UiKit.Label(p, "Elegi", new Vector2(960f, 760f), new Vector2(1200f, 50f), Locale.Get("career.pick_club"), 30, UiTheme.TextDim);
            _startPrev = UiKit.Button(p, "Anterior", new Vector2(520f, 620f), new Vector2(120f, 120f), "<", 60, UiTheme.Button);
            _startNext = UiKit.Button(p, "Siguiente", new Vector2(1400f, 620f), new Vector2(120f, 120f), ">", 60, UiTheme.Button);
            _startClub = UiKit.Label(p, "Club", new Vector2(960f, 620f), new Vector2(760f, 120f), string.Empty, 52, UiTheme.Text);
            _startGo = UiKit.Button(p, "Empezar", new Vector2(960f, 420f), new Vector2(520f, 110f), Locale.Get("career.start"), 44, UiTheme.AccentDark);
            _startLoad = UiKit.Button(p, "Continuar", new Vector2(960f, 270f), new Vector2(520f, 100f), Locale.Get("career.continue"), 36, UiTheme.Button);
            UiButton back = UiKit.Button(p, "Volver", new Vector2(160f, 1020f), new Vector2(220f, 70f), Locale.Get("career.exit"), 28, UiTheme.Button);
            _startBack = back;
        }

        private UiButton _startBack;

        private void TapStart(Vector2 p)
        {
            int count = TeamCatalog.Teams.Length;
            if (_startPrev.Contains(p)) { ChosenTeam = (ChosenTeam + count - 1) % count; }
            else if (_startNext.Contains(p)) { ChosenTeam = (ChosenTeam + 1) % count; }
            else if (_startGo.Contains(p)) { Action = CareerAction.NewGame; }
            else if (_startLoad.Contains(p)) { Action = CareerAction.LoadGame; }
            else if (_startBack.Contains(p)) { Action = CareerAction.Exit; }

            _startClub.text = TeamCatalog.Teams[ChosenTeam].Name;
        }

        // ------------------------------------------------------------------ inicio

        private void BuildHome(Transform p)
        {
            UiKit.Panel(p, "Proximo", new Vector2(460f, 760f), new Vector2(840f, 380f), UiTheme.Panel);
            _nextTitle = UiKit.Label(p, "ProximoTitulo", new Vector2(460f, 910f), new Vector2(800f, 50f), Locale.Get("career.next_match"), 30, UiTheme.Accent);
            _nextLine = UiKit.Label(p, "ProximoRival", new Vector2(460f, 830f), new Vector2(800f, 70f), string.Empty, 44, UiTheme.Text);
            _nextInfo = UiKit.Label(p, "ProximoInfo", new Vector2(460f, 760f), new Vector2(800f, 50f), string.Empty, 26, UiTheme.TextDim);
            _play = UiKit.Button(p, "Jugar", new Vector2(200f, 640f), new Vector2(240f, 96f), Locale.Get("career.play"), 38, UiTheme.AccentDark);
            _simulate = UiKit.Button(p, "Simular", new Vector2(460f, 640f), new Vector2(240f, 96f), Locale.Get("career.simulate"), 34, UiTheme.Button);
            _advance = UiKit.Button(p, "Avanzar", new Vector2(720f, 640f), new Vector2(240f, 96f), Locale.Get("career.advance"), 34, UiTheme.Button);

            UiKit.Panel(p, "Buzon", new Vector2(460f, 300f), new Vector2(840f, 460f), UiTheme.Panel);
            UiKit.Label(p, "BuzonTitulo", new Vector2(460f, 500f), new Vector2(800f, 44f), Locale.Get("career.news"), 28, UiTheme.Accent);
            for (int i = 0; i < NewsRows; i++)
            {
                _news[i] = UiKit.Label(p, "Noticia" + i, new Vector2(460f, 440f - i * 60f), new Vector2(800f, 56f), string.Empty, 22, UiTheme.Text, TextAnchor.MiddleLeft);
            }

            UiKit.Panel(p, "Tabla", new Vector2(1370f, 520f), new Vector2(900f, 900f), UiTheme.Panel);
            string[] keys = { "career.table.tournament", "career.table.annual", "career.table.averages" };
            for (int i = 0; i < _tableTabs.Length; i++)
            {
                _tableTabs[i] = UiKit.Button(p, "TablaModo" + i, new Vector2(1100f + i * 270f, 925f), new Vector2(250f, 56f), Locale.Get(keys[i]), 24, UiTheme.Button);
            }

            _table = new TextGrid(p, "Posiciones", new Vector2(940f, 860f), 50f, new[] { 960f, 1180f, 1520f, 1620f, 1730f },
                new[] { 60f, 420f, 90f, 90f, 110f }, TableRows, 24);
        }

        private void RefreshHome()
        {
            CareerData d = _session.Data;
            Fixture next = _session.PendingUserFixture ?? _session.NextUserFixture();
            bool today = _session.PendingUserFixture != null;
            if (next == null)
            {
                _nextLine.text = _session.TournamentOver
                    ? string.Format(CultureInfo.InvariantCulture, Locale.Get("career.tournament_over"), d.Club(_session.ChampionId()).Name)
                    : Locale.Get("career.no_match");
                _nextInfo.text = string.Empty;
            }
            else
            {
                bool home = next.HomeClubId == d.UserClubId;
                int rival = home ? next.AwayClubId : next.HomeClubId;
                _nextLine.text = d.Club(rival).Name;
                _nextInfo.text = Locale.Get(home ? "career.home_game" : "career.away_game") + "  ·  "
                                 + (today ? Locale.Get("career.today") : next.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
            }

            _play.SetVisible(today);
            _simulate.SetVisible(today);
            _advance.SetVisible(!today && !_session.TournamentOver);
            for (int i = 0; i < _tableTabs.Length; i++)
            {
                _tableTabs[i].Background.color = i == _tableMode ? UiTheme.Selected : UiTheme.Button;
                _tableTabs[i].SetVisible(i == 0 || !d.TournamentOnly);
            }

            FillTable();
            IReadOnlyList<NewsItem> items = _session.Inbox.Items;
            for (int i = 0; i < NewsRows; i++)
            {
                _news[i].text = i < items.Count ? items[i].Date.ToString("dd/MM", CultureInfo.InvariantCulture) + "  " + NewsText(items[i]) : string.Empty;
            }
        }

        private void FillTable()
        {
            CareerData d = _session.Data;
            _table.Clear();
            bool averages = _tableMode == 2;
            _table.SetRow(0, "#", Locale.Get("career.col.club"), averages ? string.Empty : Locale.Get("career.col.played"),
                averages ? string.Empty : Locale.Get("career.col.gd"), Locale.Get(averages ? "career.col.average" : "career.col.points"));
            if (averages)
            {
                List<AverageRow> rows = _session.AveragesTable();
                for (int i = 0; i < rows.Count && i + 1 < TableRows; i++)
                {
                    _table.SetRow(i + 1, Num(i + 1), d.Club(rows[i].ClubId).Name, string.Empty, string.Empty,
                        rows[i].Average.ToString("0.000", CultureInfo.InvariantCulture));
                    _table.Highlight(i + 1, rows[i].ClubId == d.UserClubId);
                }

                return;
            }

            List<Standing> table = _tableMode == 0 ? _session.TournamentTable() : _session.AnnualTable();
            for (int i = 0; i < table.Count && i + 1 < TableRows; i++)
            {
                Standing s = table[i];
                _table.SetRow(i + 1, Num(i + 1), d.Club(s.ClubId).Name, Num(s.Played), Num(s.GoalDifference), Num(s.Points));
                _table.Highlight(i + 1, s.ClubId == d.UserClubId);
            }
        }

        private void TapHome(Vector2 p)
        {
            for (int i = 0; i < _tableTabs.Length; i++)
            {
                if (_tableTabs[i].Contains(p))
                {
                    _tableMode = i;
                    RefreshHome();
                }
            }

            if (_play.Contains(p))
            {
                Action = CareerAction.Play;
            }
            else if (_simulate.Contains(p))
            {
                Fixture f = _session.PendingUserFixture;
                MatchReport r = _session.SimulateUserMatch();
                CareerData d = _session.Data;
                SetStatus(d.Club(f.HomeClubId).Name + "  " + r.HomeGoals + " - " + r.AwayGoals + "  " + d.Club(f.AwayClubId).Name);
                Refresh();
            }
            else if (_advance.Contains(p))
            {
                DayOutcome outcome = _session.AdvanceToNextMatch();
                if (outcome == DayOutcome.SeasonFinished)
                {
                    SetStatus(Locale.Get("career.season_over"));
                }

                Refresh();
            }
        }

        // ------------------------------------------------------------------ plantel y táctica

        private void BuildSquad(Transform p)
        {
            UiKit.Panel(p, "Plantel", new Vector2(640f, 520f), new Vector2(1200f, 900f), UiTheme.Panel);
            _squad = new TextGrid(p, "Jugadores", new Vector2(60f, 940f), 56f, new[] { 230f, 470f, 560f, 660f, 760f, 860f, 990f, 1140f },
                new[] { 330f, 90f, 90f, 90f, 90f, 100f, 150f, 150f }, SquadRows + 1, 24);
            _squadPrev = UiKit.Button(p, "Anterior", new Vector2(420f, 110f), new Vector2(120f, 64f), "<", 34, UiTheme.Button);
            _squadNext = UiKit.Button(p, "Siguiente", new Vector2(860f, 110f), new Vector2(120f, 64f), ">", 34, UiTheme.Button);
            _squadPage = UiKit.Label(p, "Pagina", new Vector2(640f, 110f), new Vector2(300f, 60f), string.Empty, 24, UiTheme.TextDim);

            UiKit.Panel(p, "Tactica", new Vector2(1580f, 520f), new Vector2(600f, 900f), UiTheme.Panel);
            UiKit.Label(p, "TacticaTitulo", new Vector2(1580f, 930f), new Vector2(560f, 50f), Locale.Get("career.tactics"), 30, UiTheme.Accent);
            string[] keys = { "career.formation", "career.defensive_formation", "career.style", "career.role.fullbacks", "career.role.midfield", "career.role.strikers" };
            for (int i = 0; i < _tactic.Length; i++)
            {
                float y = 830f - i * 125f;
                UiKit.Label(p, "TacticaNombre" + i, new Vector2(1580f, y + 40f), new Vector2(560f, 40f), Locale.Get(keys[i]), 24, UiTheme.TextDim);
                _tactic[i] = UiKit.Button(p, "Tactica" + i, new Vector2(1580f, y - 10f), new Vector2(520f, 64f), string.Empty, 28, UiTheme.Button);
            }
        }

        private void RefreshSquad()
        {
            CareerData d = _session.Data;
            List<Player> squad = _session.Squad(d.UserClubId);
            _squad.Clear();
            _squad.SetRow(0, Locale.Get("career.col.name"), Locale.Get("career.col.pos"), Locale.Get("career.col.rating"), Locale.Get("career.col.form"),
                Locale.Get("career.col.morale"), Locale.Get("career.col.fitness"), Locale.Get("career.col.contract"), Locale.Get("career.col.status"));
            for (int i = 0; i < SquadRows && _squadOffset + i < squad.Count; i++)
            {
                Player pl = squad[_squadOffset + i];
                PlayerCondition c = d.Conditions[pl.Id];
                string status = c.InjuryDays > 0 ? Format("career.status.injured", c.InjuryDays)
                    : c.SuspendedMatches > 0 ? Format("career.status.banned", c.SuspendedMatches) : Locale.Get("career.status.ok");
                _squad.SetRow(i + 1, ShortName(pl), PositionCode(pl.Position), Num(_session.VisibleRating(pl)),
                    c.Form.ToString("0.0", CultureInfo.InvariantCulture), Num((int)c.Morale), Num((int)(c.Fitness * 100f)) + "%",
                    pl.Contract != null ? pl.Contract.ExpiresOn.Year.ToString(CultureInfo.InvariantCulture) : "-", status);
            }

            int pages = Math.Max(1, (squad.Count + SquadRows - 1) / SquadRows);
            _squadPage.text = Format("career.page", _squadOffset / SquadRows + 1, pages);
            UserTactics t = d.Tactics;
            _tactic[0].Label.text = t.Formation;
            _tactic[1].Label.text = string.IsNullOrEmpty(t.DefensiveFormation) ? Locale.Get("career.same") : t.DefensiveFormation;
            _tactic[2].Label.text = Locale.Get("style." + t.Style);
            _tactic[3].Label.text = Locale.Get("career.duty." + (int)t.FullbackDuty);
            _tactic[4].Label.text = Locale.Get("career.duty." + (int)t.MidfieldDuty);
            _tactic[5].Label.text = Locale.Get("career.duty." + (int)t.StrikerDuty);
        }

        private void TapSquad(Vector2 p)
        {
            List<Player> squad = _session.Squad(_session.Data.UserClubId);
            int count = squad.Count;
            int row = _squad.RowAt(p) - 1;
            if (row >= 0 && _squadOffset + row < count)
            {
                _card.Show(_session.Card(squad[_squadOffset + row].Id)); // tocar un jugador abre su ficha
                return;
            }

            if (_squadPrev.Contains(p)) { _squadOffset = Math.Max(0, _squadOffset - SquadRows); }
            else if (_squadNext.Contains(p) && _squadOffset + SquadRows < count) { _squadOffset += SquadRows; }

            UserTactics t = _session.Data.Tactics;
            if (_tactic[0].Contains(p)) { t.Formation = Next(FormationNames, t.Formation); }
            else if (_tactic[1].Contains(p))
            {
                // Sin balón: «igual» y después cada dibujo (todos tienen once puestos).
                int i = string.IsNullOrEmpty(t.DefensiveFormation) ? -1 : Array.IndexOf(FormationNames, t.DefensiveFormation);
                t.DefensiveFormation = i + 1 >= FormationNames.Length ? string.Empty : FormationNames[i + 1];
            }
            else if (_tactic[2].Contains(p)) { t.Style = Next(StyleNames, t.Style); }
            else if (_tactic[3].Contains(p)) { t.FullbackDuty = Next(FullbackDuties, t.FullbackDuty); }
            else if (_tactic[4].Contains(p)) { t.MidfieldDuty = Next(MidfieldDuties, t.MidfieldDuty); }
            else if (_tactic[5].Contains(p)) { t.StrikerDuty = Next(StrikerDuties, t.StrikerDuty); }

            RefreshSquad();
        }

        // ------------------------------------------------------------------ mercado

        private void BuildMarket(Transform p)
        {
            UiKit.Panel(p, "Filtros", new Vector2(960f, 930f), new Vector2(1800f, 110f), UiTheme.Panel);
            _filterPos = UiKit.Button(p, "Puesto", new Vector2(200f, 930f), new Vector2(260f, 70f), string.Empty, 24, UiTheme.Button);
            _ageMinus = UiKit.Button(p, "EdadMin-", new Vector2(390f, 930f), new Vector2(70f, 70f), "-", 32, UiTheme.Button);
            _agePlus = UiKit.Button(p, "EdadMin+", new Vector2(470f, 930f), new Vector2(70f, 70f), "+", 32, UiTheme.Button);
            _ageMaxMinus = UiKit.Button(p, "EdadMax-", new Vector2(560f, 930f), new Vector2(70f, 70f), "-", 32, UiTheme.Button);
            _ageMaxPlus = UiKit.Button(p, "EdadMax+", new Vector2(640f, 930f), new Vector2(70f, 70f), "+", 32, UiTheme.Button);
            _rateMinus = UiKit.Button(p, "MediaMin-", new Vector2(1060f, 930f), new Vector2(70f, 70f), "-", 32, UiTheme.Button);
            _ratePlus = UiKit.Button(p, "MediaMin+", new Vector2(1140f, 930f), new Vector2(70f, 70f), "+", 32, UiTheme.Button);
            _rateMaxMinus = UiKit.Button(p, "MediaMax-", new Vector2(1230f, 930f), new Vector2(70f, 70f), "-", 32, UiTheme.Button);
            _rateMaxPlus = UiKit.Button(p, "MediaMax+", new Vector2(1310f, 930f), new Vector2(70f, 70f), "+", 32, UiTheme.Button);
            _filterText = UiKit.Label(p, "FiltroTexto", new Vector2(850f, 930f), new Vector2(380f, 70f), string.Empty, 22, UiTheme.Text);
            _search = UiKit.Button(p, "Buscar", new Vector2(1500f, 930f), new Vector2(200f, 70f), Locale.Get("career.search"), 28, UiTheme.AccentDark);
            _windowText = UiKit.Label(p, "Libro", new Vector2(1740f, 930f), new Vector2(260f, 70f), string.Empty, 20, UiTheme.Gold);

            UiKit.Panel(p, "Resultados", new Vector2(700f, 520f), new Vector2(1280f, 680f), UiTheme.Panel);
            _resultMark = UiKit.Panel(p, "Marca", new Vector2(700f, 0f), new Vector2(1260f, 58f), UiTheme.Selected);
            _resultMark.gameObject.SetActive(false);
            _results = new TextGrid(p, "Lista", new Vector2(80f, 820f), 64f, new[] { 250f, 600f, 820f, 930f, 1090f, 1250f },
                new[] { 340f, 340f, 90f, 130f, 170f, 170f }, MarketRows + 1, 24);

            UiKit.Panel(p, "Acciones", new Vector2(1660f, 640f), new Vector2(480f, 440f), UiTheme.Panel);
            _scout = UiKit.Button(p, "Ojear", new Vector2(1660f, 800f), new Vector2(420f, 70f), Locale.Get("career.scout"), 28, UiTheme.Button);
            _feeText = UiKit.Label(p, "Oferta", new Vector2(1660f, 715f), new Vector2(420f, 50f), string.Empty, 24, UiTheme.Text);
            _feeMinus = UiKit.Button(p, "Oferta-", new Vector2(1520f, 640f), new Vector2(120f, 64f), "-10%", 24, UiTheme.Button);
            _feePlus = UiKit.Button(p, "Oferta+", new Vector2(1800f, 640f), new Vector2(120f, 64f), "+10%", 24, UiTheme.Button);
            _offer = UiKit.Button(p, "Ofertar", new Vector2(1660f, 640f), new Vector2(140f, 64f), Locale.Get("career.offer"), 22, UiTheme.AccentDark);
            _clause = UiKit.Button(p, "Clausula", new Vector2(1660f, 520f), new Vector2(420f, 70f), Locale.Get("career.pay_clause"), 26, UiTheme.Button);

            UiKit.Panel(p, "Recibidas", new Vector2(1660f, 240f), new Vector2(480f, 330f), UiTheme.Panel);
            UiKit.Label(p, "RecibidasTitulo", new Vector2(1660f, 375f), new Vector2(440f, 40f), Locale.Get("career.offers_in"), 24, UiTheme.Accent);
            for (int i = 0; i < OfferRows; i++)
            {
                float y = 320f - i * 95f;
                _offerLines[i] = UiKit.Label(p, "Recibida" + i, new Vector2(1660f, y), new Vector2(460f, 40f), string.Empty, 20, UiTheme.Text);
                _accept[i] = UiKit.Button(p, "Aceptar" + i, new Vector2(1550f, y - 40f), new Vector2(200f, 44f), Locale.Get("career.accept"), 20, UiTheme.AccentDark);
                _reject[i] = UiKit.Button(p, "Rechazar" + i, new Vector2(1770f, y - 40f), new Vector2(200f, 44f), Locale.Get("career.reject"), 20, UiTheme.Button);
            }
        }

        private void RefreshMarket()
        {
            CareerData d = _session.Data;
            Position? pos = PositionFilter[_posFilter];
            _filterPos.Label.text = Locale.Get("career.filter.position") + ": " + (pos.HasValue ? PositionCode(pos.Value) : Locale.Get("career.filter.any"));
            _filterText.text = Locale.Get("career.filter.age") + " " + _minAge + "-" + _maxAge + "   " + Locale.Get("career.filter.rating") + " " + _minRating + "-" + _maxRating;
            _windowText.text = Locale.Get(_session.TransferWindowOpen ? "career.window_open" : "career.window_closed");
            _results.Clear();
            _results.SetRow(0, Locale.Get("career.col.name"), Locale.Get("career.col.club"), Locale.Get("career.col.age"), Locale.Get("career.col.rating"),
                Locale.Get("career.col.value"), Locale.Get("career.col.clause"));
            for (int i = 0; i < MarketRows && i < _found.Count; i++)
            {
                Player pl = _found[i];
                _results.SetRow(i + 1, ShortName(pl), pl.ClubId.HasValue ? d.Club(pl.ClubId.Value).ShortName : "-", Num(pl.AgeOn(d.Today)),
                    Num(_session.VisibleRating(pl)), Money(_session.Market.Value(pl, d.Today)), Money(pl.Contract?.ReleaseClause ?? 0));
            }

            bool selected = _selected >= 0 && _selected < _found.Count;
            _resultMark.gameObject.SetActive(selected);
            if (selected)
            {
                ((RectTransform)_resultMark.transform).anchoredPosition = new Vector2(700f, _results.RowY(_selected + 1));
            }

            _feeText.text = selected ? Format("career.fee", Money(_fee)) : Locale.Get("career.select_player");
            IReadOnlyList<PendingOffer> offers = _session.Offers;
            for (int i = 0; i < OfferRows; i++)
            {
                bool show = i < offers.Count;
                _offerLines[i].text = show
                    ? Format("career.offer_line", d.Club(offers[i].Offer.BuyerClubId).ShortName, ShortName(d.Players[offers[i].Offer.PlayerId]), Money(offers[i].Offer.Fee))
                    : string.Empty;
                _accept[i].SetVisible(show);
                _reject[i].SetVisible(show);
            }
        }

        private void TapMarket(Vector2 p)
        {
            if (_filterPos.Contains(p)) { _posFilter = (_posFilter + 1) % PositionFilter.Length; }
            else if (_ageMinus.Contains(p)) { _minAge = Math.Max(15, _minAge - AgeStep); }
            else if (_agePlus.Contains(p)) { _minAge = Math.Min(_maxAge, _minAge + AgeStep); }
            else if (_ageMaxMinus.Contains(p)) { _maxAge = Math.Max(_minAge, _maxAge - AgeStep); }
            else if (_ageMaxPlus.Contains(p)) { _maxAge = Math.Min(45, _maxAge + AgeStep); }
            else if (_rateMinus.Contains(p)) { _minRating = Math.Max(1, _minRating - RatingStep); }
            else if (_ratePlus.Contains(p)) { _minRating = Math.Min(_maxRating, _minRating + RatingStep); }
            else if (_rateMaxMinus.Contains(p)) { _maxRating = Math.Max(_minRating, _maxRating - RatingStep); }
            else if (_rateMaxPlus.Contains(p)) { _maxRating = Math.Min(99, _maxRating + RatingStep); }
            else if (_search.Contains(p))
            {
                _found.Clear();
                _found.AddRange(_session.Search(PositionFilter[_posFilter], _minAge, _maxAge, _minRating, _maxRating, long.MaxValue));
                _selected = -1;
            }
            else
            {
                int row = _results.RowAt(p) - 1;
                if (row >= 0 && row < Math.Min(MarketRows, _found.Count))
                {
                    _selected = row;
                    _fee = _session.Market.Value(_found[row], _session.Data.Today);
                }

                TapMarketActions(p);
            }

            RefreshMarket();
        }

        private void TapMarketActions(Vector2 p)
        {
            IReadOnlyList<PendingOffer> offers = _session.Offers;
            for (int i = 0; i < OfferRows && i < offers.Count; i++)
            {
                if (_accept[i].Contains(p) || _reject[i].Contains(p))
                {
                    _session.Respond(offers[i], _accept[i].Contains(p));
                    return;
                }
            }

            if (_selected < 0 || _selected >= _found.Count)
            {
                return;
            }

            Player pl = _found[_selected];
            if (_scout.Contains(p))
            {
                SetStatus(Locale.Get(_session.SendScout(pl.Id) ? "career.scout_sent" : "career.scout_refused"));
            }
            else if (_feeMinus.Contains(p)) { _fee = Math.Max(Million, (long)(_fee * (1.0 - FeeStep))); }
            else if (_feePlus.Contains(p)) { _fee = (long)(_fee * (1.0 + FeeStep)); }
            else if (_offer.Contains(p))
            {
                OfferResponse r = _session.MakeOffer(pl.Id, _fee, out long counter);
                SetStatus(r == OfferResponse.Accepted ? Locale.Get("career.offer.accepted")
                    : r == OfferResponse.CounterOffer ? Format("career.offer.counter", Money(counter)) : Locale.Get("career.offer.rejected"));
                if (r == OfferResponse.CounterOffer)
                {
                    _fee = counter;
                }
            }
            else if (_clause.Contains(p))
            {
                SetStatus(Locale.Get(_session.PayReleaseClause(pl.Id) ? "career.clause_paid" : "career.clause_failed"));
            }
        }

        // ------------------------------------------------------------------ club

        private void BuildClub(Transform p)
        {
            UiKit.Panel(p, "Finanzas", new Vector2(480f, 600f), new Vector2(880f, 740f), UiTheme.Panel);
            UiKit.Label(p, "FinanzasTitulo", new Vector2(480f, 930f), new Vector2(840f, 50f), Locale.Get("career.finances"), 30, UiTheme.Accent);
            _money = new TextGrid(p, "Dinero", new Vector2(80f, 860f), 70f, new[] { 330f, 760f }, new[] { 500f, 300f }, 6, 26);
            _toWages = UiKit.Button(p, "ASueldos", new Vector2(270f, 330f), new Vector2(360f, 80f), Locale.Get("career.to_wages"), 28, UiTheme.Button);
            _toTransfers = UiKit.Button(p, "AFichajes", new Vector2(690f, 330f), new Vector2(360f, 80f), Locale.Get("career.to_transfers"), 28, UiTheme.Button);

            UiKit.Panel(p, "Obras", new Vector2(1420f, 760f), new Vector2(920f, 420f), UiTheme.Panel);
            UiKit.Label(p, "ObrasTitulo", new Vector2(1420f, 935f), new Vector2(880f, 50f), Locale.Get("career.facilities"), 30, UiTheme.Accent);
            for (int i = 0; i < 4; i++)
            {
                float y = 860f - i * 85f;
                _facilityText[i] = UiKit.Label(p, "Obra" + i, new Vector2(1240f, y), new Vector2(520f, 60f), string.Empty, 24, UiTheme.Text, TextAnchor.MiddleLeft);
                _upgrade[i] = UiKit.Button(p, "Ampliar" + i, new Vector2(1700f, y), new Vector2(320f, 64f), string.Empty, 22, UiTheme.Button);
            }

            UiKit.Panel(p, "CuerpoTecnico", new Vector2(1420f, 300f), new Vector2(920f, 440f), UiTheme.Panel);
            UiKit.Label(p, "CuerpoTitulo", new Vector2(1420f, 485f), new Vector2(880f, 50f), Locale.Get("career.staff"), 30, UiTheme.Accent);
            for (int i = 0; i < 4; i++)
            {
                float y = 410f - i * 85f;
                _staffText[i] = UiKit.Label(p, "Staff" + i, new Vector2(1240f, y), new Vector2(520f, 60f), string.Empty, 22, UiTheme.Text, TextAnchor.MiddleLeft);
                _hire[i] = UiKit.Button(p, "Contratar" + i, new Vector2(1700f, y), new Vector2(320f, 64f), Locale.Get("career.hire"), 22, UiTheme.Button);
            }
        }

        private void RefreshClub()
        {
            CareerData d = _session.Data;
            ClubFinances f = d.Finances[d.UserClubId];
            _money.Clear();
            _money.SetRow(0, Locale.Get("career.cash"), Money(f.Cash));
            _money.SetRow(1, Locale.Get("career.transfer_budget"), Money(f.TransferBudget));
            _money.SetRow(2, Locale.Get("career.wage_budget"), Money(f.WeeklyWageBudget));
            _money.SetRow(3, Locale.Get("career.wage_bill"), Money(_session.WeeklyWages(d.UserClubId)));
            _money.SetRow(4, Locale.Get("career.reputation"), Num(f.Reputation));
            _money.SetRow(5, Locale.Get("career.members"), f.Members.ToString("N0", CultureInfo.InvariantCulture));
            ClubFacilities facilities = d.Facilities[d.UserClubId];
            for (int i = 0; i < 4; i++)
            {
                Facility fac = facilities.All[i];
                _facilityText[i].text = Locale.Get("career.facility." + i) + "  ·  " + Format("career.level", fac.Level)
                                        + (fac.WeeksToFinish > 0 ? "  ·  " + Format("career.building", fac.WeeksToFinish) : string.Empty);
                _upgrade[i].Label.text = Format("career.upgrade", Money(_session.Finance.UpgradeCost(fac.Level + 1)));
            }

            for (int i = 0; i < ShownStaff.Length; i++)
            {
                StaffMember s = d.Staff.Find(x => x.ClubId == d.UserClubId && x.Role == ShownStaff[i]);
                _staffText[i].text = Locale.Get("career.staff." + (int)ShownStaff[i]) + ": " + (s == null ? "-" : s.Name + " (" + s.Ability + ") " + Money(s.WeeklyWage));
            }
        }

        private void TapClub(Vector2 p)
        {
            CareerData d = _session.Data;
            ClubFinances f = d.Finances[d.UserClubId];
            if (_toWages.Contains(p))
            {
                bool ok = _session.Finance.MoveToWages(d.UserClubId, (long)(f.TransferBudget * (double)BudgetMoveShare), d.Today);
                SetStatus(Locale.Get(ok ? "career.moved" : "career.move_failed"));
            }
            else if (_toTransfers.Contains(p))
            {
                long spare = f.WeeklyWageBudget - _session.WeeklyWages(d.UserClubId);
                bool ok = spare > 0 && _session.Finance.MoveToTransfers(d.UserClubId, (long)(spare * (double)BudgetMoveShare) + 1, d.Today);
                SetStatus(Locale.Get(ok ? "career.moved" : "career.move_failed"));
            }

            for (int i = 0; i < 4; i++)
            {
                if (_upgrade[i].Contains(p))
                {
                    SetStatus(Locale.Get(_session.Finance.StartUpgrade(d.UserClubId, (FacilityKind)i, d.Today) ? "career.upgrade_started" : "career.upgrade_failed"));
                }

                if (_hire[i].Contains(p))
                {
                    StaffMember s = d.Staff.Find(x => x.ClubId == d.UserClubId && x.Role == ShownStaff[i]);
                    int ability = Math.Min(MaxStaffAbility, (s?.Ability ?? 0) + StaffImprovement);
                    string name = s?.Name ?? Locale.Get("career.staff." + (int)ShownStaff[i]);
                    SetStatus(Locale.Get(_session.HireStaff(ShownStaff[i], ability, name) ? "career.hired" : "career.hire_failed"));
                }
            }

            RefreshClub();
        }

        // ------------------------------------------------------------------ auxiliares

        private GameObject Group(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(Root, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = UiKit.Reference;
            return go;
        }

        /// <summary>Encoge y corre el área de diseño para que no la tape el recorte de la cámara o los bordes curvos.</summary>
        private void FitSafeArea()
        {
            Vector2 offset = UiKit.AreaOffset;
            float left = Mathf.Max(0f, UiKit.SafeLeft - offset.x), right = Mathf.Max(0f, UiKit.SafeRight - offset.x);
            float bottom = Mathf.Max(0f, UiKit.SafeBottom - offset.y), top = Mathf.Max(0f, UiKit.SafeTop - offset.y);
            _scale = Mathf.Min((UiKit.Reference.x - left - right) / UiKit.Reference.x, (UiKit.Reference.y - top - bottom) / UiKit.Reference.y);
            _shift = new Vector2((left - right) * 0.5f, (bottom - top) * 0.5f);
            Root.localScale = new Vector3(_scale, _scale, 1f);
            ((RectTransform)Root).anchoredPosition = _shift;
        }

        /// <summary>Del toque (área de diseño sin escalar) a las coordenadas de los elementos.</summary>
        private Vector2 Local(Vector2 tap) => (tap - Centre - _shift) / _scale + Centre;

        private string NewsText(NewsItem item)
        {
            string[] args = item.Args ?? Array.Empty<string>();
            var shown = new string[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                // Los avisos del motor traen el identificador del jugador: se muestra su nombre.
                shown[i] = int.TryParse(args[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) && _session.Data.Players.TryGetValue(id, out Player p)
                           && item.Key != "news.result" && item.Key != "news.season_end" && item.Key != "news.scout_report"
                    ? ShortName(p)
                    : args[i];
            }

            var copy = new NewsItem { Key = item.Key, Args = shown };
            return Inbox.Format(copy, _newsTemplates);
        }

        private static Dictionary<string, string> LoadNews()
        {
            string language = Application.systemLanguage == SystemLanguage.English ? "en" : "es";
            TextAsset text = Resources.Load<TextAsset>("news_" + language) ?? Resources.Load<TextAsset>("news_es");
            try
            {
                return text != null ? Football.Core.Common.StringTable.Parse(text.text) : new Dictionary<string, string>();
            }
            catch (FormatException e)
            {
                Debug.LogWarning("Football: no se pudieron leer las noticias (" + e.Message + ")");
                return new Dictionary<string, string>();
            }
        }

        private static string ShortName(Player p) => string.IsNullOrEmpty(p.FirstName) ? p.LastName : p.FirstName[0] + ". " + p.LastName;

        private static string PositionCode(Position p) => PlayerLabels.Code(p);

        private static string Money(long amount) => "$" + (amount / Million).ToString("N0", CultureInfo.InvariantCulture) + " M";

        private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Format(string key, params object[] args) => string.Format(CultureInfo.InvariantCulture, Locale.Get(key), args);

        private static T Next<T>(T[] values, T current)
        {
            int i = Array.IndexOf(values, current);
            return values[(i + 1) % values.Length];
        }

        /// <summary>Tabla de textos en filas y columnas (se crea una vez; después solo cambian los textos).</summary>
        private sealed class TextGrid
        {
            private readonly Text[,] _cells;
            private readonly Image[] _marks;
            private readonly float _top, _rowHeight, _left, _right;

            public TextGrid(Transform parent, string name, Vector2 topLeft, float rowHeight, float[] columnCentres, float[] widths, int rows, int fontSize)
            {
                _cells = new Text[rows, columnCentres.Length];
                _marks = new Image[rows];
                _top = topLeft.y;
                _rowHeight = rowHeight;
                _left = topLeft.x;
                _right = columnCentres[columnCentres.Length - 1] + widths[widths.Length - 1] * 0.5f;
                for (int r = 0; r < rows; r++)
                {
                    float y = RowY(r);
                    _marks[r] = UiKit.Panel(parent, name + "Fila" + r, new Vector2((_left + _right) * 0.5f, y), new Vector2(_right - _left, rowHeight - 4f),
                        new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, 0.18f));
                    _marks[r].gameObject.SetActive(false);
                    for (int c = 0; c < columnCentres.Length; c++)
                    {
                        _cells[r, c] = UiKit.Label(parent, name + r + "_" + c, new Vector2(columnCentres[c], y), new Vector2(widths[c], rowHeight),
                            string.Empty, fontSize, r == 0 ? UiTheme.TextDim : UiTheme.Text, c == 1 || c == 0 && widths[c] > 200f ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
                    }
                }
            }

            public float RowY(int row) => _top - row * _rowHeight;

            public void Clear()
            {
                for (int r = 0; r < _cells.GetLength(0); r++)
                {
                    _marks[r].gameObject.SetActive(false);
                    for (int c = 0; c < _cells.GetLength(1); c++)
                    {
                        _cells[r, c].text = string.Empty;
                    }
                }
            }

            public void SetRow(int row, params string[] values)
            {
                for (int c = 0; c < values.Length && c < _cells.GetLength(1); c++)
                {
                    _cells[row, c].text = values[c];
                }
            }

            public void Highlight(int row, bool on) => _marks[row].gameObject.SetActive(on);

            public int RowAt(Vector2 p)
            {
                if (p.x < _left || p.x > _right)
                {
                    return -1;
                }

                int row = Mathf.RoundToInt((_top - p.y) / _rowHeight);
                return row >= 0 && row < _cells.GetLength(0) && Mathf.Abs(RowY(row) - p.y) <= _rowHeight * 0.5f ? row : -1;
            }
        }
    }
}
