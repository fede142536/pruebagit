using System;
using System.Collections.Generic;
using System.Globalization;
using Football.Core.Career;
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
        /// <summary>Empezar una partida nueva con el DT y el club elegidos.</summary>
        NewGame,
        /// <summary>Seguir la partida guardada.</summary>
        LoadGame,
        /// <summary>Jugar en 3D el partido de hoy.</summary>
        Play,
        /// <summary>Guardar.</summary>
        Save,
        /// <summary>Volver al menú principal.</summary>
        Exit,
        /// <summary>Del DT a la elección de club (la resuelve la pantalla).</summary>
        PickClub,
        /// <summary>De la elección de club al DT (la resuelve la pantalla).</summary>
        BackToManager,
        /// <summary>La directiva despidió al DT: se guarda y se vuelve al menú.</summary>
        Sacked
    }

    /// <summary>
    /// Contenedor del Modo Carrera y del Torneo, armado con UiKit (sin EventSystem) y dentro del área segura. Para una
    /// carrera nueva muestra la creación del DT y la elección de club; durante la partida, una barra de cinco botones abajo
    /// (Central, Plantilla y Táctica, Mercado y Ojeo, Oficina y Objetivos, Finanzas y Estadio) y la cabecera con guardar y
    /// salir. El Torneo usa su elección de club simple y solo Central y Plantilla. No tiene lógica de juego: todo lo hace
    /// <see cref="CareerSession"/>; cada sección es una <see cref="CareerPage"/> (salvo Plantilla y Mercado, que viven acá).
    /// </summary>
    public sealed class CareerScreen : MenuScreenBase, ICareerHost
    {
        private const long Million = 1_000_000;
        private const int SquadRows = 13;
        private const int MarketRows = 8;
        private const int OfferRows = 3;
        private const int AgeStep = 1;
        private const int RatingStep = 2;
        private const float FeeStep = 0.1f;
        private const int Sections = 5;
        private const int MaxContractYears = 5; // igual que CareerConfig.MaxContractYears
        // Menú de secciones arriba (junto a Guardar y Salir); abajo quedan solo la cabecera del club y la línea de estado.
        private const float BarY = 1035f, BarHeight = 56f, BarStep = 262f, BarLeft = 40f;
        private const float HeaderY = 24f, StatusY = 62f, StartStatusY = 175f;
        // Plantilla y Mercado se diseñaron para toda la pantalla: se achican para dejar lugar a la cabecera y la barra.
        private const float SectionScale = 0.885f;
        private static readonly Vector2 SectionOffset = new Vector2(960f * (1f - SectionScale), 100f);
        private static readonly Vector2 Centre = new Vector2(960f, 540f);
        private static readonly string[] FormationNames = { "4-4-2", "4-3-3", "4-2-3-1", "4-3-1-2" };
        private static readonly string[] StyleNames = { "Balanced", "Possession", "Counter Attack", "High Press", "Defensive" };
        private static readonly PlayerDuty[] FullbackDuties = { PlayerDuty.Auto, PlayerDuty.Defensive, PlayerDuty.WingBack };
        private static readonly PlayerDuty[] MidfieldDuties = { PlayerDuty.Auto, PlayerDuty.BallWinner, PlayerDuty.Playmaker, PlayerDuty.BoxToBox };
        private static readonly PlayerDuty[] StrikerDuties = { PlayerDuty.Auto, PlayerDuty.TargetMan, PlayerDuty.Runner };
        private static readonly Position?[] PositionFilter =
            { null, Position.Goalkeeper, Position.CenterBack, Position.LeftBack, Position.RightBack, Position.DefensiveMidfielder,
              Position.CentralMidfielder, Position.AttackingMidfielder, Position.LeftWinger, Position.RightWinger, Position.Striker };

        private readonly Dictionary<string, string> _newsTemplates;
        private readonly Text _header;
        private readonly Text _status;
        private readonly UiButton[] _bar = new UiButton[Sections];
        private readonly UiButton _save, _exit;
        private readonly GameObject _startPanel;
        private readonly Transform _squadPanel, _marketPanel;
        private readonly ManagerCreationScreen _managerPage;
        private readonly CareerClubSelectScreen _clubPage;
        private readonly CareerHubScreen _hub;
        private readonly CareerObjectivesScreen _office;
        private readonly CareerFinanceScreen _finance;
        private readonly CareerStadiumScreen _stadium;
        private readonly CareerLineupScreen _lineup;
        private readonly CareerCalendarScreen _calendar;
        private readonly CareerHistoryScreen _history;
        private readonly CareerContractsScreen _contracts;
        private CareerSession _session;
        private PlayerCardPanel _card;
        private CareerSection _section;
        private CareerPage _startPage;
        private Vector2 _shift;
        private float _scale = 1f;

        /// <summary>Acción pedida este fotograma.</summary>
        public CareerAction Action { get; private set; }

        /// <summary>Club elegido para la partida nueva (índice de <see cref="TeamCatalog.Teams"/>).</summary>
        public int ChosenTeam { get; private set; }

        /// <summary>Clubes de Primera elegidos para la partida nueva (0 = una sola división con todos).</summary>
        public int PrimeraSize { get; private set; }

        /// <summary>DT de la partida nueva (null en el Torneo).</summary>
        public ManagerProfile Manager { get; private set; }

        /// <inheritdoc />
        public CareerSession Session => _session;

        /// <inheritdoc />
        public PointerInput Input => Pointers;

        // plantel
        private TextGrid _squad;
        private UiButton _squadPrev, _squadNext;
        private Text _squadPage;
        private int _squadOffset;
        private readonly UiButton[] _tactic = new UiButton[6];
        private UiButton _openLineup, _openContracts;

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
        private UiButton _nameField, _nameClear, _freeToggle, _signFree, _signYears;
        private string _nameQuery = string.Empty;
        private bool _freeOnly, _nameEditing, _caretOn;
        private int _signYearsValue = 3;
        private float _caretTimer;
        private TouchScreenKeyboard _nameKeyboard;
        private const int MaxNameQuery = 20;
        private const float CaretBlinkSeconds = 0.5f;
        private Text _feeText;
        private readonly Text[] _offerLines = new Text[OfferRows];
        private readonly UiButton[] _accept = new UiButton[OfferRows], _reject = new UiButton[OfferRows];

        // inicio del Torneo
        private Text _startTitle, _startClub;
        private UiButton _startPrev, _startNext, _startGo, _startLoad, _startBack;

        /// <summary>Crea la pantalla (oculta).</summary>
        public CareerScreen(Transform parent, PointerInput pointers) : base(parent, pointers, "CarreraUI")
        {
            _newsTemplates = LoadNews();
            _header = UiKit.Label(Root, "Cabecera", new Vector2(700f, HeaderY), new Vector2(1300f, 36f), string.Empty, 22, UiTheme.TextDim, TextAnchor.MiddleLeft);
            _status = UiKit.Label(Root, "Estado", new Vector2(960f, StatusY), new Vector2(1800f, 36f), string.Empty, 22, UiTheme.Accent);
            _save = UiKit.Button(Root, "Guardar", new Vector2(1660f, 1035f), new Vector2(160f, 54f), Locale.Get("career.save"), 22, UiTheme.AccentDark);
            _exit = UiKit.Button(Root, "Salir", new Vector2(1830f, 1035f), new Vector2(150f, 54f), Locale.Get("career.exit"), 22, UiTheme.Button);

            _hub = new CareerHubScreen(Root, this);
            _squadPanel = SectionGroup("Plantilla");
            BuildSquad(_squadPanel);
            _marketPanel = SectionGroup("Mercado");
            BuildMarket(_marketPanel);
            _office = new CareerObjectivesScreen(Root, this);
            _finance = new CareerFinanceScreen(Root, this);
            _stadium = new CareerStadiumScreen(Root, this);
            _lineup = new CareerLineupScreen(Root, this);
            _calendar = new CareerCalendarScreen(Root, this);
            _history = new CareerHistoryScreen(Root, this);
            _contracts = new CareerContractsScreen(Root, this);
            for (int i = 0; i < Sections; i++)
            {
                _bar[i] = UiKit.Button(Root, "Seccion" + i, new Vector2(BarLeft + BarStep * 0.5f + i * BarStep, BarY), new Vector2(BarStep - 10f, BarHeight),
                    Locale.Get("career.section." + i), 20, UiTheme.Button);
            }

            _managerPage = new ManagerCreationScreen(Root, this);
            _clubPage = new CareerClubSelectScreen(Root, this);
            _startPanel = CareerPage.CreateGroup(Root, "InicioTorneo").gameObject;
            BuildStart(_startPanel.transform);
            _card = new PlayerCardPanel(parent);
        }

        private Transform SectionGroup(string name)
        {
            Transform t = CareerPage.CreateGroup(Root, name);
            t.localScale = new Vector3(SectionScale, SectionScale, 1f);
            ((RectTransform)t).anchoredPosition = SectionOffset;
            t.gameObject.SetActive(false);
            return t;
        }

        /// <summary>Del área de diseño a las coordenadas de Plantilla y Mercado (achicadas).</summary>
        private static Vector2 InSection(Vector2 p) => (p - SectionOffset) / SectionScale;

        /// <summary>Parámetros de la carrera (directiva, precios), para las pantallas que los describen.</summary>
        public void Configure(CareerConfig config)
        {
            _clubPage.Configure(config);
            _office.Configure(config);
            _finance.Configure(config);
        }

        // ------------------------------------------------------------------ ciclo

        /// <summary>Muestra el comienzo: creación del DT (carrera) o elección de club (torneo), con «continuar» si hay partida.</summary>
        /// <param name="tournament">Modo Torneo.</param>
        /// <param name="favorite">Club preseleccionado.</param>
        /// <param name="hasSave">Hay una partida guardada para continuar.</param>
        public void ShowStart(bool tournament, int favorite, bool hasSave)
        {
            _session = null;
            Manager = null;
            ChosenTeam = Mathf.Clamp(favorite, 0, TeamCatalog.Teams.Length - 1);
            HideSections();
            SetTopBarVisible(false);
            _status.text = string.Empty;
            _status.rectTransform.anchoredPosition = new Vector2(Centre.x, StartStatusY); // sobre los botones de abajo
            _startPanel.SetActive(tournament);
            if (tournament)
            {
                _startPage = null;
                _startTitle.text = Locale.Get("career.new_tournament");
                _startLoad.SetVisible(hasSave);
                _startClub.text = TeamCatalog.Teams[ChosenTeam].Name;
            }
            else
            {
                _managerPage.Reset(hasSave);
                _clubPage.Select(ChosenTeam);
                ShowStartPage(_managerPage);
            }

            Show();
        }

        private void ShowStartPage(CareerPage page)
        {
            _managerPage.SetVisible(page == _managerPage);
            _clubPage.SetVisible(page == _clubPage);
            _startPage = page;
        }

        /// <summary>Muestra la partida en curso.</summary>
        /// <param name="session">Sesión.</param>
        public void ShowSession(CareerSession session)
        {
            _session = session;
            _startPanel.SetActive(false);
            _managerPage.SetVisible(false);
            _clubPage.SetVisible(false);
            _startPage = null;
            _status.rectTransform.anchoredPosition = new Vector2(Centre.x, StatusY);
            SetTopBarVisible(true);
            bool career = !session.Data.TournamentOnly;
            for (int i = 0; i < Sections; i++)
            {
                _bar[i].SetVisible(career || i <= (int)CareerSection.Squad);
            }

            Open(CareerSection.Hub);
            Show();
        }

        /// <summary>Mensaje en la línea de estado de abajo.</summary>
        public void SetStatus(string text)
        {
            _status.text = text ?? string.Empty;
        }

        /// <inheritdoc />
        public void Request(CareerAction action)
        {
            switch (action)
            {
                case CareerAction.PickClub:
                    ManagerProfile p = _managerPage.Profile;
                    Manager = new ManagerProfile
                    {
                        FirstName = p.FirstName, LastName = p.LastName, Nationality = p.Nationality, AvatarIndex = p.AvatarIndex, Outfit = p.Outfit
                    };
                    _status.text = string.Empty;
                    ShowStartPage(_clubPage);
                    break;
                case CareerAction.BackToManager:
                    ShowStartPage(_managerPage);
                    break;
                case CareerAction.NewGame:
                    ChosenTeam = _clubPage.Chosen;
                    PrimeraSize = _clubPage.PrimeraSize;
                    Action = action;
                    break;
                default:
                    Action = action;
                    break;
            }
        }

        /// <inheritdoc />
        public void Open(CareerSection section)
        {
            _section = section;
            _squadPanel.gameObject.SetActive(section == CareerSection.Squad);
            _lineup.SetVisible(section == CareerSection.Lineup);
            _calendar.SetVisible(section == CareerSection.Calendar);
            _history.SetVisible(section == CareerSection.History);
            _contracts.SetVisible(section == CareerSection.Contracts);
            _marketPanel.gameObject.SetActive(section == CareerSection.Market);
            _hub.SetVisible(section == CareerSection.Hub);
            _office.SetVisible(section == CareerSection.Office);
            _finance.SetVisible(section == CareerSection.Finance);
            _stadium.SetVisible(section == CareerSection.Stadium);
            int highlighted = section == CareerSection.Stadium ? (int)CareerSection.Finance : section == CareerSection.Lineup || section == CareerSection.Contracts ? (int)CareerSection.Squad
                : section == CareerSection.Calendar || section == CareerSection.History ? (int)CareerSection.Hub : (int)section;
            for (int i = 0; i < Sections; i++)
            {
                _bar[i].Background.color = i == highlighted ? UiTheme.Selected : UiTheme.Button;
            }

            Refresh();
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
            _managerPage.SetVisible(false);
        }

        /// <summary>Procesa los toques del fotograma.</summary>
        public void Update()
        {
            Action = CareerAction.None;
            _startPage?.Tick(Time.deltaTime);
            if (_session != null && _section == CareerSection.Market)
            {
                TickNameSearch(Time.deltaTime);
            }
            if (BackKey)
            {
                if (_startPage == _clubPage)
                {
                    ShowStartPage(_managerPage);
                }
                else
                {
                    Action = CareerAction.Exit;
                }

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

            for (int t = 0; t < n && Action == CareerAction.None; t++)
            {
                Vector2 p = Local(taps[t]);
                if (_session == null)
                {
                    if (_startPage != null)
                    {
                        _startPage.Tap(p);
                    }
                    else
                    {
                        TapStart(p);
                    }

                    continue;
                }

                if (_save.Contains(p)) { Action = CareerAction.Save; continue; }
                if (_exit.Contains(p)) { Action = CareerAction.Exit; continue; }
                bool barTapped = false;
                for (int i = 0; i < Sections; i++)
                {
                    if (_bar[i].Root.activeSelf && _bar[i].Contains(p))
                    {
                        Open((CareerSection)i);
                        barTapped = true;
                    }
                }

                if (barTapped)
                {
                    continue;
                }

                switch (_section)
                {
                    case CareerSection.Hub: _hub.Tap(p); break;
                    case CareerSection.Squad: TapSquad(InSection(p)); break;
                    case CareerSection.Market: TapMarket(InSection(p)); break;
                    case CareerSection.Office: _office.Tap(p); break;
                    case CareerSection.Finance: _finance.Tap(p); break;
                    case CareerSection.Stadium: _stadium.Tap(p); break;
                    case CareerSection.Lineup: _lineup.Tap(p); break;
                    case CareerSection.Calendar: _calendar.Tap(p); break;
                    case CareerSection.History: _history.Tap(p); break;
                    case CareerSection.Contracts: _contracts.Tap(p); break;
                }

                RefreshHeader();
            }
        }

        /// <summary>Vuelve a dibujar la sección actual (después de un partido, por ejemplo).</summary>
        public void Refresh()
        {
            if (_session == null)
            {
                return;
            }

            RefreshHeader();
            switch (_section)
            {
                case CareerSection.Hub: _hub.Refresh(); break;
                case CareerSection.Squad: RefreshSquad(); break;
                case CareerSection.Market: RefreshMarket(); break;
                case CareerSection.Office: _office.Refresh(); break;
                case CareerSection.Finance: _finance.Refresh(); break;
                case CareerSection.Stadium: _stadium.Refresh(); break;
                case CareerSection.Lineup: _lineup.Refresh(); break;
                case CareerSection.Calendar: _calendar.Refresh(); break;
                case CareerSection.History: _history.Refresh(); break;
                case CareerSection.Contracts: _contracts.Refresh(); break;
            }
        }

        private void RefreshHeader()
        {
            CareerData d = _session.Data;
            string manager = d.TournamentOnly || d.Manager.FullName.Length == 0 ? string.Empty : "   ·   " + Locale.Get("manager.dt") + " " + d.Manager.FullName;
            _header.text = d.Club(d.UserClubId).Name.ToUpperInvariant() + "   ·   " + d.Today.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                           + "   ·   " + CareerText.Money(d.Finances[d.UserClubId].Cash) + manager;
        }

        private void HideSections()
        {
            _squadPanel.gameObject.SetActive(false);
            _marketPanel.gameObject.SetActive(false);
            _hub.SetVisible(false);
            _office.SetVisible(false);
            _finance.SetVisible(false);
            _stadium.SetVisible(false);
            _lineup.SetVisible(false);
            _calendar.SetVisible(false);
            _history.SetVisible(false);
            _contracts.SetVisible(false);
            _managerPage.SetVisible(false);
            _clubPage.SetVisible(false);
        }

        private void SetTopBarVisible(bool visible)
        {
            foreach (UiButton b in _bar)
            {
                b.SetVisible(visible);
            }

            _save.SetVisible(visible);
            _exit.SetVisible(visible);
            _header.gameObject.SetActive(visible);
        }

        // ------------------------------------------------------------------ elección de club del Torneo

        private void BuildStart(Transform p)
        {
            _startTitle = UiKit.Label(p, "Titulo", new Vector2(960f, 880f), new Vector2(1400f, 90f), string.Empty, 64, UiTheme.Accent);
            UiKit.Label(p, "Elegi", new Vector2(960f, 760f), new Vector2(1200f, 50f), Locale.Get("career.pick_club"), 30, UiTheme.TextDim);
            _startPrev = UiKit.Button(p, "Anterior", new Vector2(520f, 620f), new Vector2(120f, 120f), "<", 60, UiTheme.Button);
            _startNext = UiKit.Button(p, "Siguiente", new Vector2(1400f, 620f), new Vector2(120f, 120f), ">", 60, UiTheme.Button);
            _startClub = UiKit.Label(p, "Club", new Vector2(960f, 620f), new Vector2(760f, 120f), string.Empty, 52, UiTheme.Text);
            _startGo = UiKit.Button(p, "Empezar", new Vector2(960f, 420f), new Vector2(520f, 110f), Locale.Get("career.start"), 44, UiTheme.AccentDark);
            _startLoad = UiKit.Button(p, "Continuar", new Vector2(960f, 270f), new Vector2(520f, 100f), Locale.Get("career.continue"), 36, UiTheme.Button);
            _startBack = UiKit.Button(p, "Volver", new Vector2(160f, 1020f), new Vector2(220f, 70f), Locale.Get("career.exit"), 28, UiTheme.Button);
        }

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

            _openLineup = UiKit.Button(p, "ArmarOnce", new Vector2(1450f, 108f), new Vector2(255f, 76f), Locale.Get("lineup.open"), 19, UiTheme.AccentDark);
            _openContracts = UiKit.Button(p, "Contratos", new Vector2(1710f, 108f), new Vector2(255f, 76f), Locale.Get("contracts.open"), 22, UiTheme.AccentDark);
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
                int yearsLeft = _session.ContractYearsLeft(pl);
                _squad.SetColor(i + 1, 6, yearsLeft <= 1 ? StatusColors.Bad : yearsLeft == 2 ? StatusColors.Fair : UiTheme.Text); // los que vencen este año, en rojo
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

            if (_openLineup.Contains(p))
            {
                Open(CareerSection.Lineup);
                return;
            }

            if (_openContracts.Contains(p))
            {
                Open(CareerSection.Contracts);
                return;
            }

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
            UiKit.Panel(p, "Filtros", new Vector2(960f, 905f), new Vector2(1800f, 160f), UiTheme.Panel);
            _filterPos = UiKit.Button(p, "Puesto", new Vector2(200f, 945f), new Vector2(260f, 70f), string.Empty, 24, UiTheme.Button);
            _ageMinus = UiKit.Button(p, "EdadMin-", new Vector2(390f, 945f), new Vector2(70f, 70f), "-", 32, UiTheme.Button);
            _agePlus = UiKit.Button(p, "EdadMin+", new Vector2(470f, 945f), new Vector2(70f, 70f), "+", 32, UiTheme.Button);
            _ageMaxMinus = UiKit.Button(p, "EdadMax-", new Vector2(560f, 945f), new Vector2(70f, 70f), "-", 32, UiTheme.Button);
            _ageMaxPlus = UiKit.Button(p, "EdadMax+", new Vector2(640f, 945f), new Vector2(70f, 70f), "+", 32, UiTheme.Button);
            _rateMinus = UiKit.Button(p, "MediaMin-", new Vector2(1060f, 945f), new Vector2(70f, 70f), "-", 32, UiTheme.Button);
            _ratePlus = UiKit.Button(p, "MediaMin+", new Vector2(1140f, 945f), new Vector2(70f, 70f), "+", 32, UiTheme.Button);
            _rateMaxMinus = UiKit.Button(p, "MediaMax-", new Vector2(1230f, 945f), new Vector2(70f, 70f), "-", 32, UiTheme.Button);
            _rateMaxPlus = UiKit.Button(p, "MediaMax+", new Vector2(1310f, 945f), new Vector2(70f, 70f), "+", 32, UiTheme.Button);
            _filterText = UiKit.Label(p, "FiltroTexto", new Vector2(850f, 945f), new Vector2(380f, 70f), string.Empty, 22, UiTheme.Text);
            _search = UiKit.Button(p, "Buscar", new Vector2(1500f, 945f), new Vector2(200f, 70f), Locale.Get("career.search"), 28, UiTheme.AccentDark);
            _windowText = UiKit.Label(p, "Libro", new Vector2(1740f, 945f), new Vector2(260f, 70f), string.Empty, 20, UiTheme.Gold);

            // Segunda fila: buscador por nombre (se escribe con el teclado) y filtro de jugadores libres.
            _nameField = UiKit.Button(p, "Nombre", new Vector2(460f, 868f), new Vector2(640f, 64f), string.Empty, 26, UiTheme.Button);
            _nameField.Label.alignment = TextAnchor.MiddleLeft;
            _nameClear = UiKit.Button(p, "NombreBorrar", new Vector2(835f, 868f), new Vector2(64f, 64f), "X", 28, UiTheme.Button);
            _freeToggle = UiKit.Button(p, "SoloLibres", new Vector2(1130f, 868f), new Vector2(400f, 64f), string.Empty, 24, UiTheme.Button);

            UiKit.Panel(p, "Resultados", new Vector2(700f, 495f), new Vector2(1280f, 660f), UiTheme.Panel);
            _resultMark = UiKit.Panel(p, "Marca", new Vector2(700f, 0f), new Vector2(1260f, 58f), UiTheme.Selected);
            _resultMark.gameObject.SetActive(false);
            _results = new TextGrid(p, "Lista", new Vector2(80f, 785f), 64f, new[] { 250f, 600f, 820f, 930f, 1090f, 1250f },
                new[] { 340f, 340f, 90f, 130f, 170f, 170f }, MarketRows + 1, 24);

            UiKit.Panel(p, "Acciones", new Vector2(1660f, 620f), new Vector2(480f, 400f), UiTheme.Panel);
            _scout = UiKit.Button(p, "Ojear", new Vector2(1660f, 765f), new Vector2(420f, 70f), Locale.Get("career.scout"), 28, UiTheme.Button);
            _feeText = UiKit.Label(p, "Oferta", new Vector2(1660f, 685f), new Vector2(420f, 60f), string.Empty, 24, UiTheme.Text);
            _feeMinus = UiKit.Button(p, "Oferta-", new Vector2(1520f, 605f), new Vector2(120f, 64f), "-10%", 24, UiTheme.Button);
            _feePlus = UiKit.Button(p, "Oferta+", new Vector2(1800f, 605f), new Vector2(120f, 64f), "+10%", 24, UiTheme.Button);
            _offer = UiKit.Button(p, "Ofertar", new Vector2(1660f, 605f), new Vector2(140f, 64f), Locale.Get("career.offer"), 22, UiTheme.AccentDark);
            _clause = UiKit.Button(p, "Clausula", new Vector2(1660f, 495f), new Vector2(420f, 70f), Locale.Get("career.pay_clause"), 26, UiTheme.Button);
            // Jugador libre: se firma sin transferencia, eligiendo los años del contrato (en el lugar de la oferta y la cláusula).
            _signFree = UiKit.Button(p, "FicharLibre", new Vector2(1660f, 605f), new Vector2(420f, 64f), Locale.Get("career.sign_free"), 26, UiTheme.AccentDark);
            _signYears = UiKit.Button(p, "AniosLibre", new Vector2(1660f, 495f), new Vector2(420f, 70f), string.Empty, 26, UiTheme.Button);

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
            _nameField.Label.text = "  " + Locale.Get("career.name_search") + ": " + _nameQuery + (_nameEditing && _caretOn ? "|" : string.Empty);
            _nameField.Background.color = _nameEditing ? UiTheme.Selected : UiTheme.Button;
            _nameClear.SetVisible(_nameQuery.Length > 0);
            _freeToggle.Label.text = Locale.Get(_freeOnly ? "career.free_only_on" : "career.free_only_off");
            _freeToggle.Background.color = _freeOnly ? UiTheme.Selected : UiTheme.Button;
            _results.Clear();
            _results.SetRow(0, Locale.Get("career.col.name"), Locale.Get("career.col.club"), Locale.Get("career.col.age"), Locale.Get("career.col.rating"),
                Locale.Get("career.col.value"), Locale.Get("career.col.clause"));
            for (int i = 0; i < MarketRows && i < _found.Count; i++)
            {
                Player pl = _found[i];
                _results.SetRow(i + 1, ShortName(pl), pl.ClubId.HasValue ? d.Club(pl.ClubId.Value).ShortName : Locale.Get("career.free_agent"), Num(pl.AgeOn(d.Today)),
                    Num(_session.VisibleRating(pl)), Money(_session.Market.Value(pl, d.Today)), Money(pl.Contract?.ReleaseClause ?? 0));
            }

            bool selected = _selected >= 0 && _selected < _found.Count;
            _resultMark.gameObject.SetActive(selected);
            if (selected)
            {
                ((RectTransform)_resultMark.transform).anchoredPosition = new Vector2(700f, _results.RowY(_selected + 1));
            }

            bool free = selected && !_found[_selected].ClubId.HasValue;
            _feeMinus.SetVisible(!free);
            _feePlus.SetVisible(!free);
            _offer.SetVisible(!free);
            _clause.SetVisible(!free);
            _signFree.SetVisible(free);
            _signYears.SetVisible(free);
            if (free)
            {
                _feeText.text = Format("career.free_wage", Money(_session.FreeAgentWage(_found[_selected])));
                _signYears.Label.text = Format("career.contract_years", _signYearsValue);
            }
            else
            {
                _feeText.text = selected ? Format("career.fee", Money(_fee)) : Locale.Get("career.select_player");
            }
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
            else if (_search.Contains(p)) { StopNameEditing(); RunSearch(); }
            else if (_nameField.Contains(p)) { StartNameEditing(); }
            else if (_nameClear.Root.activeSelf && _nameClear.Contains(p)) { _nameQuery = string.Empty; RunSearch(); }
            else if (_freeToggle.Contains(p)) { _freeOnly = !_freeOnly; RunSearch(); }
            else
            {
                StopNameEditing();
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

        /// <summary>Busca con los filtros y el nombre escritos; con «solo libres» deja nada más que los jugadores sin club.</summary>
        private void RunSearch()
        {
            _found.Clear();
            foreach (Player pl in _session.Search(PositionFilter[_posFilter], _minAge, _maxAge, _minRating, _maxRating, long.MaxValue, _nameQuery))
            {
                if (!_freeOnly || !pl.ClubId.HasValue)
                {
                    _found.Add(pl);
                }
            }

            _selected = -1;
        }

        private void StartNameEditing()
        {
            _nameEditing = true;
            _caretTimer = 0f;
            _caretOn = true;
            if (TouchScreenKeyboard.isSupported)
            {
                _nameKeyboard = TouchScreenKeyboard.Open(_nameQuery, TouchScreenKeyboardType.Default, false, false, false, false, Locale.Get("career.name_search"));
            }
        }

        private void StopNameEditing()
        {
            _nameEditing = false;
            _nameKeyboard = null;
        }

        /// <summary>Escribe en el buscador (teclado físico o en pantalla) y repite la búsqueda en cada cambio, así los resultados se acomodan mientras se tipea.</summary>
        private void TickNameSearch(float deltaTime)
        {
            if (!_nameEditing)
            {
                return;
            }

            string before = _nameQuery;
            if (_nameKeyboard != null)
            {
                string typed = _nameKeyboard.text ?? string.Empty;
                _nameQuery = typed.Length > MaxNameQuery ? typed.Substring(0, MaxNameQuery) : typed;
                if (_nameKeyboard.status != TouchScreenKeyboard.Status.Visible)
                {
                    StopNameEditing();
                }
            }
            else
            {
                PointerInput input = Input;
                for (int i = 0; i < input.TypedCount; i++)
                {
                    char c = input.Typed[i];
                    if (c == '\b')
                    {
                        _nameQuery = _nameQuery.Length > 0 ? _nameQuery.Substring(0, _nameQuery.Length - 1) : _nameQuery;
                    }
                    else if (c == '\n' || c == '\r')
                    {
                        StopNameEditing();
                        break;
                    }
                    else if ((char.IsLetter(c) || c == ' ' || c == '-' || c == '\'' || c == '.') && _nameQuery.Length < MaxNameQuery)
                    {
                        _nameQuery += c;
                    }
                }
            }

            _caretTimer += deltaTime;
            bool on = ((int)(_caretTimer / CaretBlinkSeconds) & 1) == 0;
            if (_nameQuery != before)
            {
                RunSearch();
            }

            if (_nameQuery != before || on != _caretOn || !_nameEditing)
            {
                _caretOn = on;
                RefreshMarket();
            }
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
            if (!pl.ClubId.HasValue)
            {
                if (_signYears.Contains(p)) { _signYearsValue = _signYearsValue % MaxContractYears + 1; }
                else if (_signFree.Contains(p))
                {
                    ContractOutcome r = _session.SignFreeAgent(pl.Id, _signYearsValue);
                    SetStatus(Locale.Get(r == ContractOutcome.Accepted ? "career.sign_ok" : r == ContractOutcome.NoBudget ? "career.sign_no_budget" : "career.sign_failed"));
                    if (r == ContractOutcome.Accepted)
                    {
                        RunSearch();
                    }
                }
                else if (_scout.Contains(p))
                {
                    SetStatus(Locale.Get(_session.SendScout(pl.Id) ? "career.scout_sent" : "career.scout_refused"));
                }

                return;
            }

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


        // ------------------------------------------------------------------ auxiliares

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

        /// <inheritdoc />
        public string NewsText(NewsItem item)
        {
            string[] args = item.Args ?? Array.Empty<string>();
            var shown = new string[args.Length];
            if (item.Key == "news.record" && args.Length >= 3)
            {
                // Récord: {0} el nombre del récord (clave de texto), {1} el jugador que lo tiene y {2} la marca (un número, no un jugador).
                shown[0] = Locale.Get(args[0]);
                bool isClub = args[0] == "record.biggest_win"; // la mayor goleada la tiene un club; los goles, un jugador
                shown[1] = !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int holder) ? args[1]
                    : isClub ? (_session.Data.Club(holder)?.Name ?? args[1])
                    : _session.Data.Players.TryGetValue(holder, out Player hp) ? ShortName(hp) : args[1];
                shown[2] = args[2];
                return Inbox.Format(new NewsItem { Key = item.Key, Args = shown }, _newsTemplates);
            }

            for (int i = 0; i < args.Length; i++)
            {
                // Los avisos del motor traen el identificador del jugador: se muestra su nombre; las claves de la interfaz, su texto.
                if (args[i].StartsWith("stadium.", StringComparison.Ordinal))
                {
                    shown[i] = Locale.Get(args[i]);
                    continue;
                }

                shown[i] = int.TryParse(args[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) && _session.Data.Players.TryGetValue(id, out Player p)
                           && item.Key != "news.result" && item.Key != "news.season_end" && item.Key != "news.scout_report"
                           && !item.Key.StartsWith("news.supporters", StringComparison.Ordinal) && item.Key != "news.stadium_work_done"
                           && item.Key != "news.contract_renewed" && item.Key != "news.contracts_expiring"
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

        private static string Money(long amount) => CareerText.Money(amount);

        private static string Num(int value) => CareerText.Num(value);

        private static string Format(string key, params object[] args) => CareerText.Format(key, args);

        private static T Next<T>(T[] values, T current)
        {
            int i = Array.IndexOf(values, current);
            return values[(i + 1) % values.Length];
        }
    }
}
