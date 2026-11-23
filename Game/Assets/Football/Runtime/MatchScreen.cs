using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Football.Core.Common;
using Football.Core.Data;
using Football.Core.MatchCore;
using Football.Core.Domain;

namespace Football.Game
{
    /// <summary>
    /// Ejecuta un partido: avanza el motor con paso fijo, interpola entre instantáneas para dibujar, lee los controles
    /// táctiles, muestra el marcador, los avisos de las jugadas y el menú de pausa.
    /// </summary>
    public sealed class MatchScreen
    {
        private const int MaxStepsPerFrame = 8;
        private const float MaxFrameTime = 0.1f;
        private const float BannerSeconds = 2.4f;
        private const float EndDelaySeconds = 2.5f;
        private const ulong BenchmarkSeed = 12345UL;
        private const string BenchmarkReportFile = "benchmark_report.txt";
        private const float BenchmarkHalfSeconds = 150f;

        private readonly Transform _parent;
        private readonly PointerInput _pointers;
        private readonly GameSettings _settings;
        private readonly Canvas _canvas;
        private readonly TouchControls _controls;
        private readonly DebugPanel _debug;
        private readonly Text _homeName, _awayName, _score, _clock, _banner, _hint, _warning, _addedLabel;
        private readonly Image _addedPanel;
        private int _shownAdded = -1;
        private static readonly string[] AddedTexts = { string.Empty, "+1", "+2", "+3", "+4", "+5", "+6", "+7", "+8", "+9" };
        private readonly GameObject[] _pauseBase;
        private bool _stallShown;
        private readonly GameObject _pausePanel;
        private readonly UiButton _resume, _quit, _quick, _quickBack;
        private readonly UiButton[] _mentalityOptions = new UiButton[5], _pressingOptions = new UiButton[3]; // selectores segmentados de la pausa
        private static readonly string[] MentalityShort = { "MUY DEF.", "DEF.", "EQUIL.", "OFENS.", "MUY OF." };
        private readonly GameObject _quickPanel;
        private readonly SettingsList _quickList;
        private readonly NameTags _nameTags;

        private MatchEngine _engine;
        private MatchSnapshot _previous = new MatchSnapshot();
        private MatchSnapshot _current = new MatchSnapshot();
        private MatchView _view;
        private TeamDefinition _homeTeam;
        private TeamDefinition _awayTeam;
        private bool _benchmark;
        private bool _paused;
        private float _accumulator;
        private float _bannerTimer;
        private float _endTimer;
        private int _eventsSeen;
        private int _shownHome = -1, _shownAway = -1, _shownSecond = -1;
        private bool _shownHasBall = true;
        private float _benchmarkStart;
        private int _frameCounter;
        private float _speed = 1f;
        private float _hintTimer;
        private static readonly string[] ClockTexts = BuildClockTexts();
        private const float StallSeconds = 3f;
        private const int StackLinesShown = 4;
        private int _errorsShown;
        private long _lastTick = -1;
        private float _stalledSeconds;

        /// <summary>El partido terminó y pasó la pausa final.</summary>
        public bool Finished { get; private set; }

        /// <summary>El jugador pidió volver al menú.</summary>
        public bool QuitRequested { get; private set; }

        /// <summary>Crea la pantalla (los objetos de interfaz se construyen una sola vez).</summary>
        /// <param name="audio">Sonido del juego.</param>
        public MatchScreen(Transform parent, PointerInput pointers, GameSettings settings, GameAudio audio)
        {
            _audio = audio;
            for (int i = 0; i < _history.Length; i++)
            {
                _history[i] = new MatchSnapshot(); // búfer de la repetición, creado una sola vez
            }

            _parent = parent;
            _pointers = pointers;
            _settings = settings;
            _canvas = UiKit.CreateCanvas("PartidoUI", parent, 10);
            // El marcador y los avisos van en un lienzo anidado: al cambiar el reloj no se reconstruye el resto de la interfaz.
            RectTransform area = UiKit.CreateArea(_canvas);
            area.gameObject.AddComponent<Canvas>();
            Transform c = area;

            // Marcador compacto arriba a la izquierda, estilo transmisión de TV: deja libre la vista del campo.
            RectTransform board = ScoreboardRoot(_canvas.transform);
            _scoreboard = board;
            board.gameObject.AddComponent<Canvas>(); // lienzo propio: el reloj cambia seguido y no debe reconstruir el resto
            Color dark = new Color(0.047f, 0.102f, 0.078f, 0.88f);
            UiKit.Panel(board, "Fondo", new Vector2(ScoreboardWidth * 0.5f, ScoreboardHeight * 0.5f), new Vector2(ScoreboardWidth, ScoreboardHeight), dark);
            UiKit.Panel(board, "FondoReloj", new Vector2(ScoreboardWidth - 60f, ScoreboardHeight * 0.5f), new Vector2(120f, ScoreboardHeight), UiTheme.Accent); // reloj verde con texto oscuro, como el diseño
            // Marcador compacto: escudo, sigla, goles, sigla, escudo y reloj.
            _homeCrest = new ClubCrest(board, new Vector2(30f, ScoreboardHeight * 0.5f), ScoreboardHeight - 8f);
            _homeName = UiKit.Label(board, "Local", new Vector2(115f, ScoreboardHeight * 0.5f), new Vector2(110f, 50f), string.Empty, 30, Color.white);
            _score = UiKit.Label(board, "Goles", new Vector2(240f, ScoreboardHeight * 0.5f), new Vector2(120f, 50f), "0 - 0", 34, Color.white);
            _awayName = UiKit.Label(board, "Visitante", new Vector2(365f, ScoreboardHeight * 0.5f), new Vector2(110f, 50f), string.Empty, 30, Color.white);
            _awayCrest = new ClubCrest(board, new Vector2(450f, ScoreboardHeight * 0.5f), ScoreboardHeight - 8f);
            _clock = UiKit.Label(board, "Reloj", new Vector2(ScoreboardWidth - 60f, ScoreboardHeight * 0.5f), new Vector2(120f, 50f), "00:00", 28, UiTheme.OnAccent);
            _addedPanel = UiKit.Panel(board, "FondoAgregado", new Vector2(ScoreboardWidth + 34f, ScoreboardHeight * 0.5f), new Vector2(68f, ScoreboardHeight), new Color(0.05f, 0.06f, 0.09f, 0.9f));
            _addedLabel = UiKit.Label(board, "Agregado", new Vector2(ScoreboardWidth + 34f, ScoreboardHeight * 0.5f), new Vector2(68f, 50f), string.Empty, 28, new Color(1f, 0.85f, 0.2f));
            _addedPanel.gameObject.SetActive(false);
            _banner = UiKit.Label(c, "Aviso", new Vector2(960f, 800f), new Vector2(1200f, 120f), string.Empty, 72, Color.white);
            _hint = UiKit.Label(c, "Ayuda", new Vector2(960f, 905f), new Vector2(1500f, 90f), string.Empty, 30, new Color(1f, 1f, 1f, 0.95f));
            _warning = UiKit.Label(c, "Error", new Vector2(960f, 640f), new Vector2(1700f, 300f), string.Empty, 28, new Color(1f, 0.35f, 0.3f));

            _plate = new LowerThird(_canvas.transform);
            BuildLineupCard(c);
            _presentation = new TacticalPresentation(c);
            _controls = new TouchControls(_canvas);
            _debug = new DebugPanel(c);

            // Contenedor de la pausa del tamaño del área de diseño (sus botones se ubican en esas coordenadas) con un
            // fondo oscuro más grande que cubre cualquier proporción de pantalla.
            Image pauseRoot = UiKit.Panel(c, "Pausa", UiKit.Reference * 0.5f, UiKit.Reference, Color.clear);
            UiKit.Panel(pauseRoot.transform, "Oscurecer", UiKit.Reference * 0.5f, UiKit.FullScreenSize, new Color(0f, 0f, 0f, 0.7f));
            Image dim = pauseRoot;
            _pausePanel = dim.gameObject;
            // Pausa (diseño «Noche + verde cancha»): acciones a la izquierda, táctica con selectores segmentados a la derecha, charla al centro.
            Text pauseTitle = UiKit.Label(dim.transform, "Titulo", new Vector2(330f, 920f), new Vector2(520f, 120f), "PAUSA", 80, UiTheme.Text, TextAnchor.MiddleLeft);
            _resume = UiKit.Button(dim.transform, "Reanudar", new Vector2(330f, 770f), new Vector2(520f, 100f), "REANUDAR", 44, UiTheme.Accent);
            _resume.Label.color = UiTheme.OnAccent;
            _quick = UiKit.Button(dim.transform, "AjustesRapidos", new Vector2(330f, 650f), new Vector2(520f, 92f), "AJUSTES RÁPIDOS ›", 30, UiTheme.Button);
            _quick.Label.color = UiTheme.AccentStrong;
            // Ajustes rápidos: solo lo que se puede cambiar en vivo (audio, cámara, controles, calidad); las reglas, no.
            Image quick = UiKit.Panel(dim.transform, "PanelAjustes", UiKit.Reference * 0.5f, UiKit.Reference, new Color(0.03f, 0.055f, 0.045f, 0.97f));
            _quickPanel = quick.gameObject;
            UiKit.Label(quick.transform, "Titulo", new Vector2(960f, 1000f), new Vector2(1200f, 70f), "AJUSTES RÁPIDOS", 46, UiTheme.Text);
            UiKit.Label(quick.transform, "Nota", new Vector2(960f, 945f), new Vector2(1500f, 40f),
                "Las reglas del partido (duración, dificultad, velocidad) se cambian desde el menú principal.", 24, UiTheme.TextDim);
            _quickList = new SettingsList(quick.transform, new Vector2(520f, 870f), new Vector2(820f, 62f), 72f, 9, 880f, 2);
            _quickBack = UiKit.Button(quick.transform, "Volver", new Vector2(1700f, 70f), new Vector2(300f, 80f), "VOLVER", 34, UiTheme.Accent);
            _quickBack.Label.color = UiTheme.OnAccent;
            _quickPanel.SetActive(false);
            _quit = UiKit.Button(dim.transform, "Salir", new Vector2(330f, 530f), new Vector2(520f, 92f), "SALIR AL MENÚ ›", 30, UiTheme.Button);
            _quit.Label.color = UiTheme.Danger;
            Image tacticBack = UiKit.Panel(dim.transform, "FondoTactica", new Vector2(1560f, 560f), new Vector2(700f, 740f), UiTheme.Panel);
            Text tacticTitle = UiKit.Label(dim.transform, "TituloTactica", new Vector2(1560f, 880f), new Vector2(620f, 50f), "TÁCTICA", 34, UiTheme.AccentStrong, TextAnchor.MiddleLeft);
            Text mentalityLabel = UiKit.Label(dim.transform, "EtiquetaMentalidad", new Vector2(1560f, 805f), new Vector2(620f, 36f), "MENTALIDAD", 22, UiTheme.TextDim, TextAnchor.MiddleLeft);
            for (int i = 0; i < _mentalityOptions.Length; i++)
            {
                _mentalityOptions[i] = UiKit.Button(dim.transform, "Mentalidad" + i, new Vector2(1560f + (i - 2) * 124f, 745f), new Vector2(122f, 70f), MentalityShort[i], 20, UiTheme.Button);
            }

            Text pressingLabel = UiKit.Label(dim.transform, "EtiquetaPresion", new Vector2(1560f, 665f), new Vector2(620f, 36f), "PRESIÓN", 22, UiTheme.TextDim, TextAnchor.MiddleLeft);
            for (int i = 0; i < _pressingOptions.Length; i++)
            {
                _pressingOptions[i] = UiKit.Button(dim.transform, "Presion" + i, new Vector2(1560f + (i - 1) * 205f, 605f), new Vector2(203f, 70f), TeamCatalog.PressLabels[i], 22, UiTheme.Button);
            }

            _squadButton = UiKit.Button(dim.transform, "Plantel", new Vector2(1560f, 400f), new Vector2(620f, 84f), "PLANTEL Y TÁCTICA ›", 28, UiTheme.Button);
            _squadButton.Label.color = UiTheme.AccentStrong;
            // Gritos desde el banco: el botón muestra el siguiente grito; tocarlo lo da (y avanza al siguiente).
            _shoutButton = UiKit.Button(dim.transform, "Grito", new Vector2(1560f, 300f), new Vector2(620f, 76f), string.Empty, 24, UiTheme.Button);
            // Charla del entretiempo (solo en el descanso).
            _talkTitle = UiKit.Label(dim.transform, "TituloCharla", new Vector2(960f, 770f), new Vector2(560f, 70f), "CHARLA DEL ENTRETIEMPO", 34, Color.white);
            for (int i = 0; i < _talkButtons.Length; i++)
            {
                _talkButtons[i] = UiKit.Button(dim.transform, "Charla" + i, new Vector2(960f, 660f - i * 104f), new Vector2(480f, 90f),
                    TalkLabels[i], 30, UiTheme.Button);
            }
            _tacticsPanel = new TacticsPanel(dim.transform); // va al final: se dibuja encima del resto de la pausa
            _card = new PlayerCardPanel(parent);
            Text tacticHelp = UiKit.Label(dim.transform, "AyudaTactica", new Vector2(1560f, 505f), new Vector2(620f, 140f),
                "Ofensiva: línea más alta y\njuego más directo y arriesgado.\nPresión alta: van a buscar arriba.", 24, new Color(1f, 1f, 1f, 0.8f));
            // Elementos de la pantalla base de la pausa: se ocultan al abrir un submenú para que nada quede detrás de sus listas.
            // (La guía de controles ya no está aquí: vive en Ajustes → Controles.)
            var pauseBase = new System.Collections.Generic.List<GameObject>
            {
                pauseTitle.gameObject, _resume.Root, _quick.Root, _quit.Root, tacticTitle.gameObject, tacticBack.gameObject, mentalityLabel.gameObject, pressingLabel.gameObject,
                _squadButton.Root, tacticHelp.gameObject, _shoutButton.Root
            };
            foreach (UiButton b in _mentalityOptions) { pauseBase.Add(b.Root); }
            foreach (UiButton b in _pressingOptions) { pauseBase.Add(b.Root); }
            _pauseBase = pauseBase.ToArray();
            _pausePanel.SetActive(false);
            _canvas.gameObject.SetActive(false);
            _nameTags = new NameTags(parent);
            _radar = new Radar(parent);
            _playerHud = new PlayerHud(parent);
        }

        /// <summary>Empieza un partido nuevo.</summary>
        /// <param name="home">Equipo del jugador.</param>
        /// <param name="away">Rival.</param>
        /// <param name="homeSquad">Convocatoria del jugador (null = automática).</param>
        /// <param name="awaySquad">Convocatoria del rival (null = automática).</param>
        /// <param name="homeKit">Camiseta del jugador.</param>
        /// <param name="awayKit">Camiseta del rival.</param>
        /// <param name="difficulty">Índice de dificultad.</param>
        /// <param name="halfMinutes">Duración de cada parte en minutos reales.</param>
        /// <param name="benchmark">Partido IA contra IA con semilla fija para medir el rendimiento.</param>
        /// <param name="indoor">3 contra 3 en cancha cerrada (potrero sin arqueros: los tres mejores de campo).</param>
        /// <returns>Verdadero si el partido arrancó; si no, el motivo queda en <see cref="LastError"/>.</returns>
        public bool Begin(TeamDefinition home, TeamDefinition away, SquadSelection homeSquad, SquadSelection awaySquad,
            KitColors homeKit, KitColors awayKit, int difficulty, int halfMinutes, bool benchmark, bool indoor = false)
        {
            try
            {
                if (indoor)
                {
                    // Por seguridad: en la cancha cerrada solo valen alineaciones de tres.
                    if (homeSquad == null || homeSquad.Formation.Slots.Length != Formations.Indoor3.Slots.Length)
                    {
                        homeSquad = SquadSelection.Auto(home, Formations.Indoor3, 0);
                    }

                    if (awaySquad == null || awaySquad.Formation.Slots.Length != Formations.Indoor3.Slots.Length)
                    {
                        awaySquad = SquadSelection.Auto(away, Formations.Indoor3, 0);
                    }
                }

                BeginUnsafe(home, away, homeSquad, awaySquad, homeKit, awayKit, difficulty, halfMinutes, benchmark, indoor);
                LastError = null;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                LastError = e.GetType().Name + ": " + e.Message;
                End();
                _engine = null;
                return false;
            }
        }

        /// <summary>Motivo por el que no arrancó el último partido, o null.</summary>
        public string LastError { get; private set; }

        /// <summary>
        /// Empieza un partido del modo carrera con los equipos que armó el núcleo (forma, moral y físico ya aplicados). El
        /// usuario es el lado 0.
        /// </summary>
        /// <param name="userTeam">Club del usuario (nombre y camisetas).</param>
        /// <param name="rivalTeam">Rival.</param>
        /// <param name="user">Equipo del usuario para el motor.</param>
        /// <param name="rival">Equipo rival para el motor.</param>
        /// <param name="userKit">Camiseta del usuario.</param>
        /// <param name="rivalKit">Camiseta del rival.</param>
        /// <param name="difficulty">Índice de dificultad.</param>
        /// <param name="halfMinutes">Minutos reales por parte.</param>
        /// <param name="stadium">Público, tribunas y obras del estadio del partido.</param>
        /// <returns>Verdadero si arrancó; si no, el motivo queda en <see cref="LastError"/>.</returns>
        /// <param name="userIsHome">El usuario juega de local; si es visitante, el estadio, la tribuna, el marcador y la hinchada son los del rival.</param>
        public bool BeginCareer(TeamDefinition userTeam, TeamDefinition rivalTeam, MatchTeamSetup user, MatchTeamSetup rival,
            KitColors userKit, KitColors rivalKit, int difficulty, int halfMinutes, StadiumLook stadium, bool userIsHome = true)
        {
            try
            {
                BeginUnsafe(userTeam, rivalTeam, null, null, userKit, rivalKit, difficulty, halfMinutes, false, false, user, rival, stadium, !userIsHome);
                LastError = null;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                LastError = e.GetType().Name + ": " + e.Message;
                End();
                _engine = null;
                return false;
            }
        }

        /// <summary>Informe completo del partido que terminó (para el modo carrera); null si no hay partido.</summary>
        public MatchReport BuildReport() => _engine?.BuildReport();

        private void BeginUnsafe(TeamDefinition homeTeam, TeamDefinition awayTeam, SquadSelection homeSquad, SquadSelection awaySquad,
            KitColors homeKit, KitColors awayKit, int difficulty, int halfMinutes, bool benchmark, bool indoor,
            MatchTeamSetup prebuiltHome = null, MatchTeamSetup prebuiltAway = null, StadiumLook? stadium = null, bool hostIsSecondTeam = false)
        {
            _hostIsSecondTeam = hostIsSecondTeam;
            _homeTeam = homeTeam;
            _awayTeam = awayTeam;
            _benchmark = benchmark;
            Finished = false;
            QuitRequested = false;
            _paused = false;
            _pausePanel.SetActive(false);
            _canvas.gameObject.SetActive(true);

            MatchConfig config = BalanceConfig.Create(indoor);
            // La velocidad del juego frena la simulación; la duración en tiempo de simulación se ajusta para que cada parte
            // siga durando los minutos reales elegidos.
            _speed = benchmark ? 1f : _settings.SpeedFactor;
            config.Rules.HalfDuration = benchmark ? BenchmarkHalfSeconds : halfMinutes * 60f * _speed;
            ulong seed = benchmark ? BenchmarkSeed : (ulong)DateTime.Now.Ticks;
            var rng = new SeededRandom(seed);
            IRandom teams = rng.Fork("teams");
            MatchTeamSetup home = prebuiltHome ?? TeamBuilder.Build(homeTeam, config, teams.Fork("home"), homeSquad);
            MatchTeamSetup away = prebuiltAway ?? TeamBuilder.Build(awayTeam, config, teams.Fork("away"), awaySquad);
            _engine = new MatchEngine(config, rng.Fork("engine"), home, away, difficulty, difficulty);
            _engine.SetHuman(0, !benchmark);

            _view?.Destroy();
            var numbers = new int[home.Players.Length + away.Players.Length];
            for (int n = 0; n < home.Players.Length; n++)
            {
                numbers[n] = home.Players[n].Number;
                numbers[home.Players.Length + n] = away.Players[n].Number;
            }

            _view = new MatchView(_engine.Pitch, homeKit, awayKit, _engine.PlayersPerTeam, _engine.Config.TickRate, stadium ?? StadiumLook.Full, numbers, hostIsSecondTeam);
            _nameTags.Setup(_engine);
            _radar.Setup(_engine, _engine.PlayersPerTeam == RadarTeamSize); // en 3 contra 3 no hace falta
            _playerHud.SetVisible(!benchmark);
            _previous.CopyFrom(_engine);
            _current.CopyFrom(_engine);
            _accumulator = 0f;
            _eventsSeen = 0;
            _errorsShown = 0;
            _stalledSeconds = 0f;
            _lastTick = -1;
            _historyHead = _historyCount = 0;
            _replaying = false;
            _celebrationTimer = 0f;
            _homeKit = homeKit;
            _awayKit = awayKit;
            _plate.Hide();
            _homeSetup = home;
            _awaySetup = away;
            bool presentation = !benchmark && _engine.PlayersPerTeam == FullTeam;
            _introTimer = benchmark || presentation ? 0f : IntroSeconds;
            if (presentation)
            {
                // 11 contra 11: presentación táctica de TV (local y visitante) y después la bajada de cámara.
                _presentation.Begin(_engine, _view, home, away, homeTeam, awayTeam, homeKit, awayKit, hostIsSecondTeam);
            }
            else if (!benchmark)
            {
                FillLineupCard();
                _view.PlayIntro(IntroSeconds);
            }

            _lineupCard.SetActive(!benchmark && !presentation);
            _lastBallSpeed = 0f;
            AudioListener.pause = false;
            _audio.StartCrowd(IsBoca(hostIsSecondTeam ? awayTeam : homeTeam)); // los cánticos de La 12 son de local: solo suenan si Boca es el dueño de casa
            _bannerTimer = 0f;
            _endTimer = 0f;
            _shownHome = _shownAway = _shownSecond = -1;
            _shownAdded = -1;
            _banner.text = string.Empty;
            // El marcador pone primero al dueño de casa: con el usuario de visitante (modo carrera) el rival va a la izquierda.
            _homeName.text = ShortName(hostIsSecondTeam ? awayTeam : homeTeam);
            _awayName.text = ShortName(hostIsSecondTeam ? homeTeam : awayTeam);
            _homeCrest.Set(hostIsSecondTeam ? awayTeam : homeTeam);
            _awayCrest.Set(hostIsSecondTeam ? homeTeam : awayTeam);
            _warning.text = _view.BuildErrors.Length > 0
                ? "Algo no se pudo dibujar (manda una captura de esto):\n" + _view.BuildErrors
                : string.Empty;
            // Sin texto permanente durante el juego: la guía de controles está en la pausa.
            _hintTimer = 0f;
            _hint.text = string.Empty;

            ApplyLiveSettings();
            ApplyControlsVisibility();
            _controls.SetContext(true, true, false);
            _shownHasBall = true;
            _debug.SetVisible(_settings.ShowDebug || benchmark);
            _debug.Reset();
            _benchmarkStart = Time.realtimeSinceStartup;
            _frameCounter = 0;
            Application.targetFrameRate = _settings.TargetFps;
            RefreshPauseLabels();
        }

        /// <summary>Resumen de lo ocurrido, para la pantalla final.</summary>
        public string BuildSummary()
        {
            if (_engine == null)
            {
                return string.Empty;
            }

            TeamStats a = _engine.Teams[0].Stats, b = _engine.Teams[1].Stats;
            float possession = a.PossessionTicks + b.PossessionTicks > 0 ? 100f * a.PossessionTicks / (a.PossessionTicks + b.PossessionTicks) : 50f;
            var sb = new StringBuilder();
            sb.Append("Tiros: ").Append(a.Shots).Append(" - ").Append(b.Shots).Append('\n');
            sb.Append("Posesión: ").Append(possession.ToString("F0")).Append("% - ").Append((100f - possession).ToString("F0")).Append("%\n");
            sb.Append("Pases completados: ").Append(a.PassesCompleted).Append('/').Append(a.Passes).Append(" - ")
                .Append(b.PassesCompleted).Append('/').Append(b.Passes).Append('\n');
            sb.Append("Faltas: ").Append(a.Fouls).Append(" - ").Append(b.Fouls).Append('\n');
            sb.Append("Córners: ").Append(a.Corners).Append(" - ").Append(b.Corners).Append('\n');
            sb.Append("Fueras de juego: ").Append(a.Offsides).Append(" - ").Append(b.Offsides).Append('\n');
            sb.Append("Tarjetas: ").Append(a.Yellows).Append("A ").Append(a.Reds).Append("R - ").Append(b.Yellows).Append("A ").Append(b.Reds).Append('R');
            if (_benchmark)
            {
                sb.Append("\n\nBENCHMARK (partido IA contra IA, semilla fija)\n");
                sb.Append("FPS medios: ").Append(AverageFps().ToString("F1")).Append('\n');
                sb.Append("Fotograma medio / p99: ").Append(_debug.AverageFrameMs.ToString("F1")).Append(" / ").Append(_debug.P99FrameMs.ToString("F1")).Append(" ms\n");
                sb.Append("Recolecciones de basura (gen0): ").Append(_debug.GcCollections).Append('\n');
                sb.Append("Asignaciones: ").Append(_debug.AllocKbPerSecond.ToString("F1")).Append(" KB/s (objetivo: 0)\n");
                sb.Append("Ajustes: calidad ").Append(GameSettings.QualityLabels[_settings.Quality]).Append(", resolución ")
                    .Append(GameSettings.ResolutionLabels[_settings.Resolution]).Append(", límite ").Append(_settings.TargetFps).Append(" FPS\n");
                sb.Append("Dispositivo: ").Append(SystemInfo.deviceModel).Append(", ").Append(SystemInfo.systemMemorySize).Append(" MB RAM, ")
                    .Append(SystemInfo.graphicsDeviceName);
                SaveBenchmarkReport(sb.ToString());
            }

            return sb.ToString();
        }

        /// <summary>Texto del marcador final, como "Tu Equipo 2 - 1 Rival".</summary>
        public string BuildResultLine()
        {
            return _homeTeam.Name + "  " + _engine.Teams[0].Stats.Goals + " - " + _engine.Teams[1].Stats.Goals + "  " + _awayTeam.Name;
        }

        private float AverageFps()
        {
            float seconds = Time.realtimeSinceStartup - _benchmarkStart;
            return seconds > 0f ? _frameCounter / seconds : 0f;
        }

        /// <summary>No hay escena de partido creada (se puede mostrar el fondo vivo de los menús).</summary>
        public bool IsIdle => _view == null;

        /// <summary>Oculta la interfaz y libera la escena del partido.</summary>
        public void End()
        {
            _canvas.gameObject.SetActive(false);
            _nameTags.Hide();
            _radar.Hide();
            _audio.StopCrowd();
            AudioListener.pause = false;
            _replaying = false;
            _view?.SetReplayAngle(-1, 0f);
            _celebrationTimer = 0f;
            _introTimer = 0f;
            _presentation.Stop();
            _card.Hide();
            _plate.Hide();
            _lineupCard.SetActive(false);
            _playerHud.SetVisible(false);
            _view?.Destroy();
            _view = null;
        }

        /// <summary>Avanza un fotograma.</summary>
        public void Update(float deltaTime)
        {
            if (_engine == null)
            {
                return;
            }

            // Si algo falla durante el partido, en lugar de quedarse congelado sin explicación se muestra el error en
            // pantalla (para poder mandar una captura) y se sigue intentando en el siguiente fotograma.
            try
            {
                UpdateUnsafe(deltaTime);
                WatchProgress(deltaTime);
            }
            catch (Exception e)
            {
                if (_errorsShown == 0)
                {
                    Debug.LogException(e);
                }

                _errorsShown++;
                _warning.text = "ERROR EN EL PARTIDO (manda una captura de esto)\n" + e.GetType().Name + ": " + e.Message + "\n"
                                + FirstStackLines(e.StackTrace, StackLinesShown) + "\nMinuto " + _engine.Minute.ToString("F1")
                                + " · fase " + _engine.Phase + " · veces " + _errorsShown;
            }
        }

        /// <summary>
        /// Avisa si el motor deja de avanzar en juego activo (el partido "se tildó"). Solo cuenta cuando el motor debería
        /// avanzar: en juego, sin pausa, sin presentación, repetición ni festejo, y fuera de saques y goles. En cuanto los
        /// pasos vuelven, el aviso desaparece; nunca se muestra durante la pausa.
        /// </summary>
        private void WatchProgress(float deltaTime)
        {
            bool advanced = _engine.TickCount != _lastTick;
            _lastTick = _engine.TickCount;
            bool activePlay = !_paused && !_engine.IsFinished && _introTimer <= 0f && !_presentation.Running && !_replaying && _celebrationTimer <= 0f
                              && _engine.Phase == MatchPhase.Playing;
            if (advanced || !activePlay)
            {
                _stalledSeconds = 0f;
                if (_stallShown)
                {
                    // El aviso se retira apenas deja de cumplirse la condición (el motor avanzó o el juego pasó a un gol, saque o pausa);
                    // antes se quedaba pegado en pantalla durante partidos que seguían perfectamente.
                    _stallShown = false;
                    if (_warning.text.StartsWith("EL PARTIDO DEJÓ", StringComparison.Ordinal))
                    {
                        _warning.text = string.Empty;
                    }
                }

                return;
            }

            _stalledSeconds += deltaTime;
            if (_stalledSeconds >= StallSeconds && !_stallShown)
            {
                _stallShown = true;
                _warning.text = "EL PARTIDO DEJÓ DE AVANZAR (manda una captura de esto)\nMinuto " + _engine.Minute.ToString("F1")
                                + " · fase " + _engine.Phase + " · saque " + _engine.CurrentRestart.Kind
                                + " · paso de tiempo " + deltaTime.ToString("F3") + " s · acumulado " + _accumulator.ToString("F3");
            }
        }

        private static string FirstStackLines(string stack, int count)
        {
            if (string.IsNullOrEmpty(stack))
            {
                return string.Empty;
            }

            string[] lines = stack.Split('\n');
            return string.Join("\n", lines, 0, Math.Min(count, lines.Length));
        }

        private void UpdateUnsafe(float deltaTime)
        {
            PlaceScoreboard();

            MatchInput input = _controls.Read(_pointers, Time.realtimeSinceStartup);
            if (_controls.PauseDown || _pointers.Keyboard.PauseDown)
            {
                SetPaused(!_paused);
            }

            if (_paused)
            {
                HandlePauseInput();
                return;
            }

            _frameCounter++;
            _plate.Update(deltaTime);
            if (_presentation.Running)
            {
                // Presentación táctica: el partido espera; SALTAR (o PASE) la termina y sigue la bajada de cámara.
                if (input.Pass || !_presentation.Update(deltaTime, FirstTap()))
                {
                    _presentation.Stop();
                    _introTimer = AfterPresentationSeconds;
                    _view.PlayIntro(AfterPresentationSeconds);
                }

                _view.Render(_previous, _current, 1f, -1, -1, deltaTime);
                return;
            }

            if (_introTimer > 0f)
            {
                // Presentación: plano aéreo y alineaciones; el partido espera (toca para saltar).
                _introTimer -= deltaTime;
                if (_introTimer <= 0f || input.Pass || input.Shoot || TappedThisFrame())
                {
                    _introTimer = 0f;
                    _lineupCard.SetActive(false);
                    _view.SkipIntro();
                }

                _view.Render(_previous, _current, 1f, -1, -1, deltaTime);
                return;
            }

            if (_replaying)
            {
                PlayReplay(deltaTime, input);
                return;
            }

            if (_celebrationTimer > 0f)
            {
                _celebrationTimer -= deltaTime;
                if (_celebrationTimer <= 0f || TappedThisFrame())
                {
                    _celebrationTimer = 0f;
                    _view.Focus(-1);
                    StartReplay();
                }
            }

            int steps = Step(deltaTime, input);
            HandleEvents();
            UpdateHud();
            _audio.UpdateCrowd(Mathf.Abs(_engine.Ball.Pos.X) / _engine.Pitch.HalfLength, deltaTime);

            float alpha = Mathf.Clamp01(_accumulator * _engine.Config.TickRate);
            int controlled = _engine.Teams[0].Controlled != null ? _engine.Teams[0].Controlled.Index : -1;
            int target = -1;
            if (controlled >= 0 && _engine.Owner != null && _engine.Owner.Index == controlled && _controls.Stick.sqrMagnitude > 0.01f)
            {
                MatchPlayer preview = _engine.PreviewPassTarget(0, new Vec2(_controls.Stick.x, _controls.Stick.y), false);
                target = preview != null ? preview.Index : -1;
            }

            if (!_benchmark && _engine.TryGetRestartAim(0, out Vec2 aimFrom, out Vec2 aimDir))
            {
                float aimCharge = Mathf.Max(_controls.PassCharge, Mathf.Max(_controls.CrossCharge, _controls.ShootCharge));
                _view.SetAim(true, aimFrom.X, aimFrom.Y, aimDir.X, aimDir.Y, aimCharge);
            }
            else
            {
                _view.SetAim(false, 0f, 0f, 0f, 0f, 0f);
            }

            MatchPlayer next = _benchmark ? null : _engine.PreviewSwitchTarget(0);
            _view.Render(_previous, _current, alpha, controlled, target, deltaTime, next != null ? next.Index : -1);
            MatchPlayer holder = _engine.Owner ?? _engine.LastToucher;
            _radar.Update(_current, _view, controlled, holder != null && holder.Team == 0);
            if (!_benchmark)
            {
                float charge = Mathf.Max(_controls.ShootCharge, Mathf.Max(_controls.PassCharge, _controls.CrossCharge));
                float stamina = controlled >= 0 ? _engine.Players[controlled].Stamina : 1f;
                _playerHud.Update(_view, controlled, charge, stamina);
            }

            _nameTags.Update(_view, _settings.Names, controlled, _engine.Owner != null ? _engine.Owner.Index : -1, _engine.Teams[0].Players.Length);
            UpdateHint(deltaTime, input);
            _debug.Frame(deltaTime, steps);

            if (_engine.IsFinished && _celebrationTimer <= 0f)
            {
                _endTimer += deltaTime;
                if (_endTimer >= EndDelaySeconds)
                {
                    Finished = true;
                }
            }
        }

        private void UpdateHint(float deltaTime, MatchInput input)
        {
            if (_hintTimer <= 0f)
            {
                return;
            }

            _hintTimer -= deltaTime;
            if (_hintTimer <= 0f)
            {
                _hint.text = string.Empty;
            }
        }

        private int Step(float deltaTime, MatchInput input)
        {
            bool humanHadBall = _engine.Owner != null && _engine.Owner.Team == 0 && _engine.Teams[0].Controlled == _engine.Owner;
            if (!_benchmark)
            {
                input.SkipWait = _engine.Phase == MatchPhase.SetPiece && !_engine.AwaitingHumanRestart && TappedThisFrame(); // un toque salta la espera
                _engine.SetInput(0, input);
            }

            _accumulator += Mathf.Min(deltaTime, MaxFrameTime) * _speed;
            float tick = 1f / _engine.Config.TickRate;
            int steps = 0;
            while (_accumulator >= tick && steps < MaxStepsPerFrame && !_engine.IsFinished)
            {
                MatchSnapshot swap = _previous;
                _previous = _current;
                _current = swap;
                _engine.Tick();
                _current.CopyFrom(_engine);
                _accumulator -= tick;
                steps++;
                Record();
            }

            if (_engine.IsFinished)
            {
                _accumulator = 0f;
            }

            bool kickAction = input.Pass || input.Through || input.Cross || input.Shoot;
            if (!_benchmark && humanHadBall && kickAction && _engine.Owner != _engine.Teams[0].Controlled)
            {
                _debug.RecordLatency(Time.realtimeSinceStartup - _controls.LastActionTime);
            }

            return steps;
        }

        private void HandleEvents()
        {
            for (; _eventsSeen < _engine.Events.Count; _eventsSeen++)
            {
                MatchEvent e = _engine.Events[_eventsSeen];
                string text = null;
                switch (e.Type)
                {
                    case MatchEventType.Goal:
                        Haptics.Pulse(_settings.Vibration);
                        _audio.Goal();
                        _view.BulgeNet(e.X >= 0f ? 1f : -1f);
                        _view.Cheer(GoalCheerSeconds); // la hinchada salta
                        if (!_benchmark)
                        {
                            // Festejo con la cámara sobre el goleador y después la repetición (que termina en el gol).
                            _goalHead = _historyHead;
                            MatchPlayer celebrator = _engine.Celebrator;
                            _view.Focus(celebrator != null ? celebrator.Index : -1);
                            _celebrationTimer = CelebrationSeconds;
                            ShowPlate("GOL", new Color(1f, 0.85f, 0.2f), e);
                        }

                        bool ownGoal = e.PlayerIndex >= 0 && _engine.Players[e.PlayerIndex].Team != e.Team;
                        // El detalle (minuto, jugador, equipo) va en la placa; el aviso grande solo celebra.
                        text = ownGoal ? "¡GOL EN CONTRA!" : "¡GOOOL!";
                        break;
                    case MatchEventType.Offside: text = "FUERA DE JUEGO"; _audio.Whistle(); _audio.React(ReactMild, true); break;
                    case MatchEventType.Foul: text = "FALTA"; _audio.Whistle(); _audio.React(ReactMedium, true); break;
                    case MatchEventType.Shot: _audio.React(ReactMild, false); break;
                    case MatchEventType.KickOff: _audio.Whistle(); break;
                    case MatchEventType.YellowCard: ShowPlate("AMARILLA", new Color(1f, 0.85f, 0.1f), e); _audio.React(ReactMedium, true); break;
                    case MatchEventType.RedCard: ShowPlate("ROJA", new Color(0.9f, 0.15f, 0.15f), e); _audio.React(ReactStrong, true); break;
                    case MatchEventType.Penalty: text = "¡PENAL!"; _audio.Whistle(); break;
                    case MatchEventType.Corner: text = "TIRO DE ESQUINA"; break;
                    case MatchEventType.ThrowIn: text = "SAQUE LATERAL"; break;
                    case MatchEventType.GoalKick: text = "SAQUE DE ARCO"; break;
                    case MatchEventType.FreeKick: text = "TIRO LIBRE"; break;
                    case MatchEventType.HalfTime:
                        text = "FIN DEL PRIMER TIEMPO";
                        _audio.LongWhistle();
                        if (!_benchmark)
                        {
                            SetPaused(true); // entretiempo: se abre la pausa para la charla y los cambios
                        }

                        break;
                    case MatchEventType.FullTime: text = "FIN DEL PARTIDO"; _audio.LongWhistle(); break;
                    case MatchEventType.Save: text = "¡PARADÓN!"; _audio.React(ReactStrong, false); break;
                    case MatchEventType.Woodwork:
                        text = "¡AL PALO!";
                        Haptics.Pulse(_settings.Vibration);
                        _view.Shake(PostShake);
                        _audio.Post();
                        break;
                    case MatchEventType.Substitution:
                        // Entra un jugador nuevo: nombres, radar y placa de TV.
                        _nameTags.Setup(_engine);
                        _radar.Setup(_engine, _engine.PlayersPerTeam == RadarTeamSize);
                        TeamState subTeam = _engine.Teams[e.Team];
                        string left = subTeam.SubstitutedOut.Count > 0 ? subTeam.SubstitutedOut[subTeam.SubstitutedOut.Count - 1] : string.Empty;
                        _plate.Show("CAMBIO", new Color(0.3f, 0.75f, 1f),
                            MinuteLabel(e) + "'   ENTRA " + _engine.Players[e.PlayerIndex].Name.ToUpperInvariant() + "  ·  SALE " + left.ToUpperInvariant(),
                            e.Team == 0 ? _homeKit.Shirt : _awayKit.Shirt);
                        break;
                    case MatchEventType.Injury:
                        // Lesión leve: el árbitro para el juego para que se pueda hacer el cambio.
                        _plate.Show("LESIÓN", new Color(1f, 0.55f, 0.2f), _engine.Players[e.PlayerIndex].Name.ToUpperInvariant()
                            + (e.Team == 0 ? "  ·  CÁMBIALO DESDE PLANTEL Y TÁCTICA" : string.Empty), e.Team == 0 ? _homeKit.Shirt : _awayKit.Shirt);
                        break;
                    case MatchEventType.TacticsChanged:
                        // Solo se anuncia el cambio del rival (el propio lo elige la persona en la pausa) y sin tapar otro aviso.
                        if (e.Team == 1 && _bannerTimer <= 0f)
                        {
                            Mentality m = _engine.Teams[1].Mentality;
                            text = _awayTeam.Name.ToUpperInvariant() + (m > Mentality.Balanced ? " SE VUELCA AL ATAQUE"
                                : m < Mentality.Balanced ? " SE CIERRA ATRÁS" : " VUELVE AL EQUILIBRIO");
                        }

                        break;
                }

                if (text != null)
                {
                    _banner.text = text;
                    _bannerTimer = BannerSeconds;
                }
            }

            if (_bannerTimer > 0f)
            {
                _bannerTimer -= Time.unscaledDeltaTime;
                if (_bannerTimer <= 0f)
                {
                    _banner.text = string.Empty;
                }
            }
        }

        private void UpdateHud()
        {
            int home = _engine.Teams[_hostIsSecondTeam ? 1 : 0].Stats.Goals;   // el marcador va con el dueño de casa a la izquierda
            int away = _engine.Teams[_hostIsSecondTeam ? 0 : 1].Stats.Goals;
            if (home != _shownHome || away != _shownAway)
            {
                _shownHome = home;
                _shownAway = away;
                _score.text = home + " - " + away;
            }

            // Reloj sin crear textos nuevos: se eligen de una tabla "00".."99" ya preparada.
            int second = Mathf.FloorToInt(_engine.Minute * 60f);
            if (second != _shownSecond)
            {
                _shownSecond = second;
                _clock.text = ClockText(second);
            }

            // Tiempo agregado: el reloj sigue contando pasada la hora reglamentaria y un cartel anuncia los minutos de más.
            int added = _engine.AddedMinutes;
            if (added != _shownAdded)
            {
                _shownAdded = added;
                _addedLabel.text = AddedTexts[Mathf.Clamp(added, 0, AddedTexts.Length - 1)];
                _addedPanel.gameObject.SetActive(added > 0);
                if (added > 0 && !_benchmark && !_engine.IsFinished)
                {
                    _banner.text = "TIEMPO AGREGADO: " + added + (added == 1 ? " MINUTO" : " MINUTOS");
                    _bannerTimer = BannerSeconds;
                }
            }

            if (!_benchmark)
            {
                // Mientras viaja un pase propio el balón no tiene dueño: se mantienen los botones de ataque para que un toque
                // rápido de PASE no caiga sobre BARRER.
                MatchPlayer holder = _engine.Owner ?? _engine.LastToucher;
                bool hasBall = holder != null && holder.Team == 0;
                bool ownHalf = _engine.Ball.Pos.X * _engine.Teams[0].AttackDirection < 0f;
                bool ownRestart = _engine.Phase == MatchPhase.SetPiece && _engine.CurrentRestart.Team == 0;
                RestartKind kind = _engine.CurrentRestart.Kind;
                if (ownRestart)
                {
                    hasBall = true; // en tus saques, botones de ataque
                    ownHalf = false;
                }

                _shownHasBall = hasBall;
                bool crossRestart = ownRestart && (kind == RestartKind.Corner || kind == RestartKind.FreeKick);
                _controls.SetContext(hasBall, ownHalf, _engine.Teams[0].KeeperLock, crossRestart);
                ShowRestartHelp(kind);
            }
        }

        // ---- Pausa -----------------------------------------------------------------------------------------------

        private const int ClockMaxMinutes = 99;

        /// <summary>Todos los textos "mm:ss" preparados al arrancar: el reloj del partido no crea textos nuevos.</summary>
        private const int RadarTeamSize = 11;
        private UiButton _squadButton;
        private UiButton _shoutButton;
        private readonly UiButton[] _talkButtons = new UiButton[3];
        private Text _talkTitle;
        private int _shoutChoice = 1;
        private static readonly string[] ShoutLabels = { "", "ENCARAR MÁS", "TOCAR EN CORTO", "PERDER TIEMPO", "PRESIÓN TRAS PÉRDIDA", "BUSCAR AL 9" };
        private static readonly string[] TalkLabels = { "CALMADO", "APASIONADO", "EXIGENTE" };
        private TacticsPanel _tacticsPanel;
        private PlayerCardPanel _card;
        private readonly HoldDetector _hold = new HoldDetector();
        private const float IntroSeconds = 3.5f;
        private const float AfterPresentationSeconds = 2f;
        private const int FullTeam = 11;
        private TacticalPresentation _presentation;
        private MatchTeamSetup _homeSetup, _awaySetup;
        private const float CelebrationSeconds = 2.2f;
        private const float PostShake = 0.25f;
        private const int LineupRows = 11;
        private LowerThird _plate;
        private GameObject _lineupCard;
        private readonly Text[] _lineupHome = new Text[LineupRows];
        private readonly Text[] _lineupAway = new Text[LineupRows];
        private Text _lineupHomeTitle, _lineupAwayTitle;
        private float _introTimer;
        private float _celebrationTimer;
        private int _goalHead;
        private KitColors _homeKit, _awayKit;
        private bool _hostIsSecondTeam;
        private const float ReplaySeconds = 4f;
        private const float GoalCheerSeconds = 3.5f;
        private const float ReplaySpeed = 0.5f;
        private const int HistoryLength = 300; // 5 s a 60 pasos por segundo
        private readonly MatchSnapshot[] _history = new MatchSnapshot[HistoryLength];
        private const string BocaName = "Boca Juniors";
        private const float ReactMild = 0.35f, ReactMedium = 0.55f, ReactStrong = 0.8f;   // intensidad de la reacción de la hinchada
        private readonly GameAudio _audio;
        private int _historyHead, _historyCount;
        private bool _replaying;
        private float _replayCursor;
        private int _replayStart, _replayLength;
        private int _replayPass; // 0 plano de transmisión, 1 a ras del césped detrás del arco
        private int _replayPasses = 2; // en la cancha cerrada solo hay una: detrás del arco queda la pared
        private float _lastBallSpeed;
        private Radar _radar;
        private PlayerHud _playerHud;
        /// <summary>Guarda el paso actual en el búfer de la repetición y hace sonar los golpeos de balón.</summary>
        private void Record()
        {
            _history[_historyHead].CopyFrom(_engine);
            _historyHead = (_historyHead + 1) % _history.Length;
            _historyCount = Math.Min(_historyCount + 1, _history.Length);

            float speed = _engine.Ball.Vel.Length;
            _audio.Kick(speed - _lastBallSpeed);
            _lastBallSpeed = speed;
        }

        /// <summary>Repetición del gol: los últimos segundos en cámara lenta (el partido espera).</summary>
        private bool _restartHelpShown;

        /// <summary>Mientras tu saque espera, una línea de ayuda dice cómo sacar (sin crear textos por fotograma).</summary>
        private void ShowRestartHelp(RestartKind kind)
        {
            bool waiting = _engine.AwaitingHumanRestart && !_replaying;
            if (waiting == _restartHelpShown)
            {
                return;
            }

            _restartHelpShown = waiting;
            _hint.text = !waiting ? string.Empty
                : kind == RestartKind.Corner ? "Apunta con el joystick: PASE (corto o al área) · CENTRO al área"
                : kind == RestartKind.FreeKick ? "Apunta con el joystick: PASE · CENTRO · TIRO (mantén) al arco"
                : "Apunta con el joystick y toca PASE (mantén para más fuerza)";
        }

        /// <summary>Placa de TV con el minuto, el jugador y el equipo de un evento.</summary>
        /// <summary>Minuto de un hecho para la placa: «23», «45+2» (tiempo agregado de la primera parte) o «90+1».</summary>
        private string MinuteLabel(MatchEvent e)
        {
            float regulation = _engine.Config.Rules.MinutesPerHalf * Mathf.Max(1, e.Half);
            return e.Minute >= regulation
                ? ((int)regulation).ToString() + "+" + ((int)(e.Minute - regulation) + 1)
                : ((int)e.Minute + 1).ToString();
        }

        private void ShowPlate(string tag, Color tagColor, MatchEvent e)
        {
            string player = e.PlayerIndex >= 0 ? _engine.Players[e.PlayerIndex].Name : string.Empty;
            int team = e.PlayerIndex >= 0 ? _engine.Players[e.PlayerIndex].Team : e.Team;
            string teamName = ShortName(team == 0 ? _homeTeam : _awayTeam);
            _plate.Show(tag, tagColor, MinuteLabel(e) + "'   " + player.ToUpperInvariant() + "  ·  " + teamName,
                team == 0 ? _homeKit.Shirt : _awayKit.Shirt);
        }

        /// <summary>Cartel de alineaciones de la presentación: dos columnas con los titulares.</summary>
        private void BuildLineupCard(Transform parent)
        {
            Image card = UiKit.Panel(parent, "Alineaciones", new Vector2(960f, 520f), new Vector2(1100f, 760f), new Color(0.04f, 0.05f, 0.08f, 0.88f));
            _lineupCard = card.gameObject;
            _lineupHomeTitle = UiKit.Label(card.transform, "Local", new Vector2(280f, 700f), new Vector2(500f, 60f), string.Empty, 40, Color.white);
            _lineupAwayTitle = UiKit.Label(card.transform, "Visitante", new Vector2(820f, 700f), new Vector2(500f, 60f), string.Empty, 40, Color.white);
            for (int i = 0; i < LineupRows; i++)
            {
                float y = 620f - i * 54f;
                _lineupHome[i] = UiKit.Label(card.transform, "L" + i, new Vector2(280f, y), new Vector2(500f, 48f), string.Empty, 28, new Color(0.9f, 0.92f, 0.95f));
                _lineupAway[i] = UiKit.Label(card.transform, "V" + i, new Vector2(820f, y), new Vector2(500f, 48f), string.Empty, 28, new Color(0.9f, 0.92f, 0.95f));
            }

            _lineupCard.SetActive(false);
        }

        private void FillLineupCard()
        {
            _lineupHomeTitle.text = _homeTeam.Name.ToUpperInvariant();
            _lineupAwayTitle.text = _awayTeam.Name.ToUpperInvariant();
            int perTeam = _engine.PlayersPerTeam;
            for (int i = 0; i < LineupRows; i++)
            {
                _lineupHome[i].text = i < perTeam ? _engine.Teams[0].Players[i].Number + "  " + _engine.Teams[0].Players[i].Name : string.Empty;
                _lineupAway[i].text = i < perTeam ? _engine.Teams[1].Players[i].Number + "  " + _engine.Teams[1].Players[i].Name : string.Empty;
            }
        }

        private void StartReplay()
        {
            int wanted = (int)(ReplaySeconds * _engine.Config.TickRate);
            _replayLength = Math.Min(wanted, _historyCount);
            if (_replayLength < 2)
            {
                return;
            }

            // La repetición termina en el momento del gol (no en el festejo que vino después).
            int end = _goalHead;
            int sinceGoal = (_historyHead - end + _history.Length) % _history.Length;
            _replayLength = Math.Min(_replayLength, _historyCount - sinceGoal);
            if (_replayLength < 2)
            {
                return;
            }

            _replayStart = (end - _replayLength + _history.Length) % _history.Length;
            _replayCursor = 0f;
            _replayPass = 0;
            _replayPasses = _engine.Pitch.Config.Walls ? 1 : 2;
            _replaying = true;
            _view.SetReplayAngle(0, _history[(end - 1 + _history.Length) % _history.Length].BallX);
            _hint.text = "REPETICIÓN  ·  toca para saltar";
            _controls.SetActive(false); // repetición limpia, como en la televisión: sin botones táctiles
            _playerHud.SetVisible(false);
        }

        private void PlayReplay(float deltaTime, MatchInput input)
        {
            bool skip = input.Pass || input.Shoot || input.Slide || input.Switch || input.Clear || TappedThisFrame();
            _replayCursor += deltaTime * _engine.Config.TickRate * ReplaySpeed;
            int i = (int)_replayCursor;
            if (!skip && i >= _replayLength - 1 && _replayPass + 1 < _replayPasses)
            {
                // Segunda pasada con el otro ángulo: la cámara a ras del césped, detrás del arco.
                _replayPass++;
                _replayCursor = 0f;
                i = 0;
                int end = (_replayStart + _replayLength - 1) % _history.Length;
                _view.SetReplayAngle(_replayPass, _history[end].BallX);
            }

            if (skip || i >= _replayLength - 1)
            {
                _replaying = false;
                _view.SetReplayAngle(-1, 0f);
                _view.SnapCamera(); // vuelve de golpe a la cámara de juego (sin arrastrar la de la repetición)
                _accumulator = 0f;
                _previous.CopyFrom(_engine);
                _current.CopyFrom(_engine);
                _hint.text = string.Empty;
                _playerHud.SetVisible(true);
                ApplyControlsVisibility(); // vuelven los botones táctiles
                return;
            }

            MatchSnapshot a = _history[(_replayStart + i) % _history.Length];
            MatchSnapshot b = _history[(_replayStart + i + 1) % _history.Length];
            _view.Render(a, b, _replayCursor - i, -1, -1, deltaTime * ReplaySpeed);
            _audio.UpdateCrowd(1f, deltaTime);
        }

        /// <summary>Primer toque de este fotograma en el área de diseño, o null.</summary>
        private Vector2? FirstTap()
        {
            for (int i = 0; i < _pointers.Count; i++)
            {
                if (_pointers.Pointers[i].Phase == PointerPhase.Began)
                {
                    return UiKit.ToArea(_pointers.Pointers[i].Position);
                }
            }

            return null;
        }

        private bool TappedThisFrame()
        {
            for (int i = 0; i < _pointers.Count; i++)
            {
                if (_pointers.Pointers[i].Phase == PointerPhase.Began)
                {
                    return true;
                }
            }

            return false;
        }

        private const float ScoreboardWidth = 600f;
        private ClubCrest _homeCrest, _awayCrest;
        private const float ScoreboardHeight = 58f;
        private static float ScoreboardMargin => ViewTuning.Current.Hud.ScoreboardMargin;

        private RectTransform _scoreboard;
        private Rect _scoreboardSafeArea;

        /// <summary>Recoloca el marcador dentro de la zona segura si cambió (muesca a la izquierda o a la derecha según el giro).</summary>
        private void PlaceScoreboard()
        {
            if (_scoreboard == null || Screen.safeArea == _scoreboardSafeArea)
            {
                return;
            }

            _scoreboardSafeArea = Screen.safeArea;
            _scoreboard.anchoredPosition = new Vector2(UiKit.SafeLeft + ScoreboardMargin, -(UiKit.SafeTop + ScoreboardMargin));
        }

        /// <summary>Contenedor del marcador anclado a la esquina superior izquierda de la pantalla real.</summary>
        private static RectTransform ScoreboardRoot(Transform parent)
        {
            var go = new GameObject("Marcador", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(ScoreboardWidth, ScoreboardHeight);
            rect.anchoredPosition = new Vector2(ScoreboardMargin, -ScoreboardMargin);
            return rect;
        }

        /// <summary>Boca Juniors (por nombre): activa los cánticos de su hinchada.</summary>
        private static bool IsBoca(TeamDefinition team) => team != null && team.Name == BocaName;

        private static string ShortName(TeamDefinition team) => !string.IsNullOrEmpty(team.ShortName)
            ? team.ShortName.ToUpperInvariant()
            : team.Name.Substring(0, Math.Min(3, team.Name.Length)).ToUpperInvariant();

        private static string[] BuildClockTexts()
        {
            var texts = new string[ClockMaxMinutes * 60 + 60];
            for (int i = 0; i < texts.Length; i++)
            {
                texts[i] = (i / 60).ToString("00") + ":" + (i % 60).ToString("00");
            }

            return texts;
        }

        private static string ClockText(int second) => ClockTexts[Mathf.Clamp(second, 0, ClockTexts.Length - 1)];

        /// <summary>Pausa el partido si está en juego (por ejemplo, al salir de la aplicación o con el botón atrás).</summary>
        public void PauseIfPlaying()
        {
            if (_engine != null && !_paused && !_engine.IsFinished)
            {
                SetPaused(true);
            }
        }

        private void SetPaused(bool paused)
        {
            if (!paused)
            {
                _tacticsPanel.Hide();
                _quickPanel.SetActive(false);
            }

            _paused = paused;
            if (!paused)
            {
                _card.Hide();
            }
            AudioListener.pause = paused;
            if (paused)
            {
                _controls.ReleaseAll();
            }

            _pausePanel.SetActive(paused);
            SetPauseBaseVisible(true);
            if (paused && _stallShown)
            {
                _stallShown = false;
                _warning.text = string.Empty; // el aviso nunca queda dibujado sobre la pausa
            }

            if (paused || _replaying)
            {
                _controls.SetActive(false); // al salir de la pausa en plena repetición, los botones siguen ocultos
            }
            else
            {
                ApplyControlsVisibility();
            }

            RefreshPauseLabels();
        }

        private bool TalkTapped(Vector2 point, out int talk)
        {
            for (talk = 0; talk < _talkButtons.Length; talk++)
            {
                if (_talkButtons[talk].Root.activeSelf && _talkButtons[talk].Contains(point))
                {
                    return true;
                }
            }

            return false;
        }

        private bool _pauseBaseShown = true;

        private void SetPauseBaseVisible(bool visible)
        {
            bool changed = visible != _pauseBaseShown;
            _pauseBaseShown = visible;
            if (visible && changed)
            {
                RefreshPauseLabels(); // vuelve la pantalla base: la charla reaparece si corresponde
            }

            if (!visible)
            {
                _talkTitle.gameObject.SetActive(false);
                for (int i = 0; i < _talkButtons.Length; i++)
                {
                    _talkButtons[i].SetVisible(false);
                }
            }

            for (int i = 0; i < _pauseBase.Length; i++)
            {
                if (_pauseBase[i].activeSelf != visible)
                {
                    _pauseBase[i].SetActive(visible);
                }
            }
        }

        /// <summary>Guarda el informe del benchmark en el almacenamiento de la aplicación (para copiarlo desde el celular).</summary>
        private static void SaveBenchmarkReport(string text)
        {
            try
            {
                string path = System.IO.Path.Combine(Application.persistentDataPath, BenchmarkReportFile);
                System.IO.File.WriteAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n" + text);
                Debug.Log("Football: informe del benchmark guardado en " + path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Football: no se pudo guardar el informe del benchmark (" + e.Message + ")");
            }
        }

        private void ApplyControlsVisibility()
        {
            if (_benchmark)
            {
                _controls.ShowPauseOnly();
            }
            else
            {
                _controls.SetActive(true);
            }
        }

        /// <summary>Un toque en un selector segmentado de mentalidad o presión lo aplica al instante.</summary>
        private bool TacticOptionTapped(Vector2 point)
        {
            TeamState own = _engine.Teams[0];
            for (int i = 0; i < _mentalityOptions.Length; i++)
            {
                if (_mentalityOptions[i].Contains(point))
                {
                    _engine.SetTactics(0, (Mentality)(i - 2), own.Pressing);
                    return true;
                }
            }

            for (int i = 0; i < _pressingOptions.Length; i++)
            {
                if (_pressingOptions[i].Contains(point))
                {
                    _engine.SetTactics(0, own.Mentality, (PressLevel)(i - 1));
                    return true;
                }
            }

            return false;
        }

        private void RefreshPauseLabels()
        {
            if (_engine != null)
            {
                TeamState own = _engine.Teams[0];
                for (int i = 0; i < _mentalityOptions.Length; i++)
                {
                    bool on = i == (int)own.Mentality + 2;
                    _mentalityOptions[i].Background.color = on ? UiTheme.Selected : UiTheme.Button;
                    _mentalityOptions[i].Label.color = on ? UiTheme.AccentStrong : UiTheme.TextDim;
                }

                for (int i = 0; i < _pressingOptions.Length; i++)
                {
                    bool on = i == (int)own.Pressing + 1;
                    _pressingOptions[i].Background.color = on ? UiTheme.Selected : UiTheme.Button;
                    _pressingOptions[i].Label.color = on ? UiTheme.AccentStrong : UiTheme.TextDim;
                }

                _shoutButton.Label.text = own.Shout != TouchlineShout.None
                    ? "GRITANDO: " + ShoutLabels[(int)own.Shout] + " · OTRO ›"
                    : "GRITAR: " + ShoutLabels[_shoutChoice] + " ›";
                bool talk = _engine.InHalfTimeBreak && !own.TalkGiven;
                _talkTitle.gameObject.SetActive(talk);
                for (int i = 0; i < _talkButtons.Length; i++)
                {
                    _talkButtons[i].SetVisible(talk);
                }
            }
        }

        private void HandlePauseInput()
        {
            if (_card.Visible)
            {
                // Ficha abierta: cualquier toque la cierra.
                if (FirstTap().HasValue)
                {
                    _card.Hide();
                }

                return;
            }

            if (_tacticsPanel.Visible && _hold.Update(_pointers, Time.unscaledDeltaTime, out Vector2 held))
            {
                PlayerCardInfo info = _tacticsPanel.CardAt(held);
                if (info != null)
                {
                    _card.Show(info);
                    return;
                }
            }

            for (int i = 0; i < _pointers.Count; i++)
            {
                PointerState p = _pointers.Pointers[i];
                if (p.Phase != PointerPhase.Began)
                {
                    continue;
                }

                Vector2 point = UiKit.ToArea(p.Position);
                if (_tacticsPanel.Visible)
                {
                    _tacticsPanel.Tap(point);
                    if (_tacticsPanel.BackRequested)
                    {
                        _tacticsPanel.Hide();
                        RefreshPauseLabels();
                    }

                    continue;
                }

                bool submenu = _quickPanel.activeSelf || _tacticsPanel.Visible;
                SetPauseBaseVisible(!submenu);
                if (_quickPanel.activeSelf)
                {
                    if (_quickBack.Contains(point))
                    {
                        _quickPanel.SetActive(false);
                    }
                    else if (_quickList.Tap(point, _settings))
                    {
                        ApplyLiveSettings();
                    }

                    continue;
                }

                if (_quick.Contains(point))
                {
                    _quickPanel.SetActive(true);
                    SetPauseBaseVisible(false);
                    _quickList.Show(_settings, SettingCategory.Graphics, true);
                }
                else if (_shoutButton.Contains(point) && _engine != null)
                {
                    _engine.Shout(0, (TouchlineShout)_shoutChoice);
                    _banner.text = "GRITO: " + ShoutLabels[_shoutChoice];
                    _shoutChoice = _shoutChoice % (ShoutLabels.Length - 1) + 1;
                    RefreshPauseLabels();
                }
                else if (TalkTapped(point, out int talk) && _engine != null)
                {
                    _engine.GiveTeamTalk(0, (TeamTalk)talk, out TalkReaction reaction);
                    _banner.text = "CHARLA " + TalkLabels[talk] + ": " + (reaction == TalkReaction.Positive ? "EL EQUIPO RESPONDE"
                        : reaction == TalkReaction.Negative ? "EL EQUIPO SE PONE NERVIOSO" : "SIN EFECTO");
                    RefreshPauseLabels();
                }
                else if (_squadButton.Contains(point) && _engine != null)
                {
                    _tacticsPanel.Show(_engine, _homeKit.Shirt);
                    SetPauseBaseVisible(false);
                }
                else if (_resume.Contains(point))
                {
                    SetPaused(false);
                }
                else if (_quit.Contains(point))
                {
                    SetPaused(false);
                    QuitRequested = true;
                }
                else if (_engine != null && TacticOptionTapped(point))
                {
                    RefreshPauseLabels();
                }
            }
        }

        /// <summary>Aplica los ajustes que se pueden cambiar sin salir del partido.</summary>
        private void ApplyLiveSettings()
        {
            SettingsCatalog.Apply(_settings);
            _controls.Layout(_settings.LeftHanded, _settings.BigButtons, _settings.ShowThrough, _settings.ShowCross);
            _controls.SetStyle(GameSettings.Opacities[_settings.ButtonOpacity], _settings.FixedJoystick);
            float[] deadZones = ViewTuning.Current.Controls.DeadZones;
            _controls.SetDeadZone(deadZones[Mathf.Clamp(_settings.DeadZone, 0, deadZones.Length - 1)]);
            _debug.SetVisible(_settings.ShowDebug || _benchmark);
            if (_engine != null)
            {
                _engine.Teams[0].HumanPassAssist = GameSettings.AssistValues[_settings.PassAssist];
            }

            _view?.SetCameraMode(_settings.CameraMode);
        }
    }
}
