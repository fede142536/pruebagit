using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Football.Core.Common;
using Football.Core.Data;
using Football.Core.MatchCore;

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
        private const float BenchmarkHalfSeconds = 150f;

        private readonly Transform _parent;
        private readonly PointerInput _pointers;
        private readonly GameSettings _settings;
        private readonly Canvas _canvas;
        private readonly TouchControls _controls;
        private readonly DebugPanel _debug;
        private readonly Text _homeName, _awayName, _score, _clock, _banner, _hint, _warning, _controlsHelp;
        private readonly GameObject _pausePanel;
        private readonly UiButton _resume, _quit, _lefty, _names, _debugToggle, _fpsToggle, _mentality, _pressing;
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
            board.gameObject.AddComponent<Canvas>(); // lienzo propio: el reloj cambia seguido y no debe reconstruir el resto
            Color dark = new Color(0.05f, 0.06f, 0.09f, 0.85f);
            UiKit.Panel(board, "Fondo", new Vector2(ScoreboardWidth * 0.5f, ScoreboardHeight * 0.5f), new Vector2(ScoreboardWidth, ScoreboardHeight), dark);
            UiKit.Panel(board, "FondoReloj", new Vector2(ScoreboardWidth - 60f, ScoreboardHeight * 0.5f), new Vector2(120f, ScoreboardHeight), new Color(0.75f, 0.12f, 0.12f, 0.9f));
            _homeName = UiKit.Label(board, "Local", new Vector2(70f, ScoreboardHeight * 0.5f), new Vector2(120f, 50f), string.Empty, 30, Color.white);
            _score = UiKit.Label(board, "Goles", new Vector2(190f, ScoreboardHeight * 0.5f), new Vector2(120f, 50f), "0 - 0", 34, Color.white);
            _awayName = UiKit.Label(board, "Visitante", new Vector2(310f, ScoreboardHeight * 0.5f), new Vector2(120f, 50f), string.Empty, 30, Color.white);
            _clock = UiKit.Label(board, "Reloj", new Vector2(ScoreboardWidth - 60f, ScoreboardHeight * 0.5f), new Vector2(120f, 50f), "00:00", 28, Color.white);
            _banner = UiKit.Label(c, "Aviso", new Vector2(960f, 800f), new Vector2(1200f, 120f), string.Empty, 72, Color.white);
            _hint = UiKit.Label(c, "Ayuda", new Vector2(960f, 905f), new Vector2(1500f, 90f), string.Empty, 30, new Color(1f, 1f, 1f, 0.95f));
            _warning = UiKit.Label(c, "Error", new Vector2(960f, 640f), new Vector2(1700f, 300f), string.Empty, 28, new Color(1f, 0.35f, 0.3f));

            _controls = new TouchControls(_canvas);
            _debug = new DebugPanel(c);

            // Contenedor de la pausa del tamaño del área de diseño (sus botones se ubican en esas coordenadas) con un
            // fondo oscuro más grande que cubre cualquier proporción de pantalla.
            Image pauseRoot = UiKit.Panel(c, "Pausa", UiKit.Reference * 0.5f, UiKit.Reference, Color.clear);
            UiKit.Panel(pauseRoot.transform, "Oscurecer", UiKit.Reference * 0.5f, UiKit.FullScreenSize, new Color(0f, 0f, 0f, 0.7f));
            Image dim = pauseRoot;
            _pausePanel = dim.gameObject;
            UiKit.Label(dim.transform, "Titulo", new Vector2(960f, 880f), new Vector2(800f, 120f), "PAUSA", 80, Color.white);
            Color option = new Color(0.3f, 0.3f, 0.4f);
            _resume = UiKit.Button(dim.transform, "Reanudar", new Vector2(960f, 770f), new Vector2(620f, 100f), "REANUDAR", 44, new Color(0.15f, 0.6f, 0.25f));
            _names = UiKit.Button(dim.transform, "Nombres", new Vector2(960f, 660f), new Vector2(620f, 92f), string.Empty, 34, option);
            _lefty = UiKit.Button(dim.transform, "Zurdo", new Vector2(960f, 556f), new Vector2(620f, 92f), string.Empty, 34, option);
            _debugToggle = UiKit.Button(dim.transform, "Depuracion", new Vector2(960f, 452f), new Vector2(620f, 92f), string.Empty, 34, option);
            _fpsToggle = UiKit.Button(dim.transform, "Fps", new Vector2(960f, 348f), new Vector2(620f, 92f), string.Empty, 34, option);
            _quit = UiKit.Button(dim.transform, "Salir", new Vector2(960f, 225f), new Vector2(620f, 100f), "SALIR AL MENÚ", 44, new Color(0.7f, 0.2f, 0.2f));
            Color tactic = new Color(0.2f, 0.35f, 0.55f);
            UiKit.Label(dim.transform, "TituloTactica", new Vector2(1600f, 770f), new Vector2(520f, 70f), "TÁCTICA", 44, Color.white);
            _mentality = UiKit.Button(dim.transform, "Mentalidad", new Vector2(1600f, 660f), new Vector2(520f, 92f), string.Empty, 30, tactic);
            _pressing = UiKit.Button(dim.transform, "Presion", new Vector2(1600f, 556f), new Vector2(520f, 92f), string.Empty, 30, tactic);
            UiKit.Label(dim.transform, "AyudaTactica", new Vector2(1600f, 430f), new Vector2(520f, 140f),
                "Ofensiva: línea más alta y\njuego más directo y arriesgado.\nPresión alta: van a buscar arriba.", 24, new Color(1f, 1f, 1f, 0.8f));
            UiKit.Label(dim.transform, "TituloControles", new Vector2(330f, 770f), new Vector2(520f, 70f), "CONTROLES", 44, Color.white);
            _controlsHelp = UiKit.Label(dim.transform, "Controles", new Vector2(330f, 470f), new Vector2(520f, 520f), string.Empty, 22,
                new Color(1f, 1f, 1f, 0.85f), TextAnchor.UpperLeft);
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
        /// <param name="indoor">3 contra 3 en cancha cerrada (se juega con los tres mejores: arquero y dos de campo).</param>
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

        private void BeginUnsafe(TeamDefinition homeTeam, TeamDefinition awayTeam, SquadSelection homeSquad, SquadSelection awaySquad,
            KitColors homeKit, KitColors awayKit, int difficulty, int halfMinutes, bool benchmark, bool indoor)
        {
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
            MatchTeamSetup home = TeamBuilder.Build(homeTeam, config, teams.Fork("home"), homeSquad);
            MatchTeamSetup away = TeamBuilder.Build(awayTeam, config, teams.Fork("away"), awaySquad);
            _engine = new MatchEngine(config, rng.Fork("engine"), home, away, difficulty, difficulty);
            _engine.SetHuman(0, !benchmark);

            _view?.Destroy();
            _view = new MatchView(_engine.Pitch, homeKit, awayKit, _engine.PlayersPerTeam, _engine.Config.TickRate);
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
            _lastBallSpeed = 0f;
            AudioListener.pause = false;
            _audio.StartCrowd();
            _bannerTimer = 0f;
            _endTimer = 0f;
            _shownHome = _shownAway = _shownSecond = -1;
            _banner.text = string.Empty;
            _homeName.text = ShortName(homeTeam);
            _awayName.text = ShortName(awayTeam);
            _warning.text = _view.BuildErrors.Length > 0
                ? "Algo no se pudo dibujar (manda una captura de esto):\n" + _view.BuildErrors
                : string.Empty;
            // Sin texto permanente durante el juego: la guía de controles está en la pausa.
            _hintTimer = 0f;
            _hint.text = string.Empty;

            _controls.Layout(_settings.LeftHanded, _settings.BigButtons, _settings.ShowThrough, _settings.ShowCross);
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
                sb.Append("Dispositivo: ").Append(SystemInfo.deviceModel).Append(", ").Append(SystemInfo.systemMemorySize).Append(" MB RAM");
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

        /// <summary>Oculta la interfaz y libera la escena del partido.</summary>
        public void End()
        {
            _canvas.gameObject.SetActive(false);
            _nameTags.Hide();
            _radar.Hide();
            _audio.StopCrowd();
            AudioListener.pause = false;
            _replaying = false;
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

        /// <summary>Avisa si el reloj del partido deja de avanzar sin estar en pausa (el partido "se tildó").</summary>
        private void WatchProgress(float deltaTime)
        {
            if (_paused || _engine.IsFinished || _engine.TickCount != _lastTick)
            {
                _lastTick = _engine.TickCount;
                _stalledSeconds = 0f;
                return;
            }

            _stalledSeconds += deltaTime;
            if (_stalledSeconds >= StallSeconds)
            {
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
            if (_replaying)
            {
                PlayReplay(deltaTime, input);
                return;
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

            MatchPlayer next = _benchmark ? null : _engine.PreviewSwitchTarget(0);
            _view.Render(_previous, _current, alpha, controlled, target, deltaTime, next != null ? next.Index : -1);
            MatchPlayer holder = _engine.Owner ?? _engine.LastToucher;
            _radar.Update(_current, _view, controlled, holder != null && holder.Team == 0);
            if (!_benchmark)
            {
                float charge = Mathf.Max(_controls.ShootCharge, _controls.PassCharge);
                float stamina = controlled >= 0 ? _engine.Players[controlled].Stamina : 1f;
                _playerHud.Update(_view, controlled, charge, stamina);
            }

            _nameTags.Update(_view, _settings.Names, controlled, _engine.Owner != null ? _engine.Owner.Index : -1, _engine.Teams[0].Players.Length);
            UpdateHint(deltaTime, input);
            _debug.Frame(deltaTime, steps);

            if (_engine.IsFinished)
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
                        if (!_benchmark)
                        {
                            StartReplay();
                        }

                        string scorer = e.PlayerIndex >= 0 ? _engine.Players[e.PlayerIndex].Name : string.Empty;
                        bool ownGoal = e.PlayerIndex >= 0 && _engine.Players[e.PlayerIndex].Team != e.Team;
                        text = "¡GOL DE " + (e.Team == 0 ? _homeTeam.Name : _awayTeam.Name).ToUpperInvariant() + "!\n"
                               + (ownGoal ? "en contra de " + scorer : scorer);
                        break;
                    case MatchEventType.Offside: text = "FUERA DE JUEGO"; _audio.Whistle(); break;
                    case MatchEventType.Foul: text = "FALTA"; _audio.Whistle(); break;
                    case MatchEventType.KickOff: _audio.Whistle(); break;
                    case MatchEventType.YellowCard: text = "TARJETA AMARILLA"; break;
                    case MatchEventType.RedCard: text = "TARJETA ROJA"; break;
                    case MatchEventType.Penalty: text = "¡PENAL!"; _audio.Whistle(); break;
                    case MatchEventType.Corner: text = "CÓRNER"; break;
                    case MatchEventType.HalfTime: text = "FIN DEL PRIMER TIEMPO"; _audio.LongWhistle(); break;
                    case MatchEventType.FullTime: text = "FIN DEL PARTIDO"; _audio.LongWhistle(); break;
                    case MatchEventType.Save: text = "¡PARADÓN!"; break;
                    case MatchEventType.Woodwork:
                        text = "¡AL PALO!";
                        Haptics.Pulse(_settings.Vibration);
                        _audio.Post();
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
            int home = _engine.Teams[0].Stats.Goals;
            int away = _engine.Teams[1].Stats.Goals;
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

            if (!_benchmark)
            {
                // Mientras viaja un pase propio el balón no tiene dueño: se mantienen los botones de ataque para que un toque
                // rápido de PASE no caiga sobre BARRER.
                MatchPlayer holder = _engine.Owner ?? _engine.LastToucher;
                bool hasBall = holder != null && holder.Team == 0;
                bool ownHalf = _engine.Ball.Pos.X * _engine.Teams[0].AttackDirection < 0f;
                _shownHasBall = hasBall;
                _controls.SetContext(hasBall, ownHalf, _engine.Teams[0].KeeperLock);
            }
        }

        // ---- Pausa -----------------------------------------------------------------------------------------------

        private const int ClockMaxMinutes = 99;

        /// <summary>Todos los textos "mm:ss" preparados al arrancar: el reloj del partido no crea textos nuevos.</summary>
        private const int RadarTeamSize = 11;
        private const float ReplaySeconds = 4f;
        private const float ReplaySpeed = 0.5f;
        private const int HistoryLength = 300; // 5 s a 60 pasos por segundo
        private readonly MatchSnapshot[] _history = new MatchSnapshot[HistoryLength];
        private readonly GameAudio _audio;
        private int _historyHead, _historyCount;
        private bool _replaying;
        private float _replayCursor;
        private int _replayStart, _replayLength;
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
        private void StartReplay()
        {
            int wanted = (int)(ReplaySeconds * _engine.Config.TickRate);
            _replayLength = Math.Min(wanted, _historyCount);
            if (_replayLength < 2)
            {
                return;
            }

            _replayStart = (_historyHead - _replayLength + _history.Length) % _history.Length;
            _replayCursor = 0f;
            _replaying = true;
            _hint.text = "REPETICIÓN  ·  toca para saltar";
            _playerHud.SetVisible(false);
        }

        private void PlayReplay(float deltaTime, MatchInput input)
        {
            bool skip = input.Pass || input.Shoot || input.Slide || input.Switch || input.Clear || TappedThisFrame();
            _replayCursor += deltaTime * _engine.Config.TickRate * ReplaySpeed;
            int i = (int)_replayCursor;
            if (skip || i >= _replayLength - 1)
            {
                _replaying = false;
                _hint.text = string.Empty;
                _playerHud.SetVisible(true);
                return;
            }

            MatchSnapshot a = _history[(_replayStart + i) % _history.Length];
            MatchSnapshot b = _history[(_replayStart + i + 1) % _history.Length];
            _view.Render(a, b, _replayCursor - i, -1, -1, deltaTime * ReplaySpeed);
            _audio.UpdateCrowd(1f, deltaTime);
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

        private const float ScoreboardWidth = 500f;
        private const float ScoreboardHeight = 58f;
        private const float ScoreboardMargin = 20f;

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

        private static string ShortName(TeamDefinition team) => !string.IsNullOrEmpty(team.ShortName)
            ? team.ShortName.ToUpperInvariant()
            : team.Name.Substring(0, Math.Min(3, team.Name.Length)).ToUpperInvariant();

        /// <summary>Guía de controles de la pausa, con las teclas asignadas en Ajustes.</summary>
        private static string ControlsGuide()
        {
            string K(GameAction a) => KeyBindings.KeyName(KeyBindings.Get(a));
            return "TÁCTIL: mitad izquierda para mover. Tu jugador\nes el de la flecha amarilla.\n\n"
                   + "CON BALÓN\nPASE (toque: automático, mantener: potencia)\nTIRO (mantener y soltar) · DESPEJAR\n\n"
                   + "SIN BALÓN\nPRESIONAR (mantener) · BARRER · CAMBIAR\nARQUERO (controlar al arquero)\n\n"
                   + "TECLADO\nMover " + K(GameAction.Up) + " " + K(GameAction.Left) + " " + K(GameAction.Down) + " " + K(GameAction.Right)
                   + " · Esprint " + K(GameAction.Sprint) + "\nPase " + K(GameAction.Pass) + " · Tiro " + K(GameAction.Shoot)
                   + " · Despejar " + K(GameAction.Clear) + "\nPresionar " + K(GameAction.Press) + " · Barrer " + K(GameAction.Slide)
                   + " · Cambiar " + K(GameAction.Switch) + "\nArquero " + K(GameAction.Keeper) + " · Pausa " + K(GameAction.Pause)
                   + "\n(se cambian en Ajustes → Controles de teclado)";
        }

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
            _paused = paused;
            AudioListener.pause = paused;
            if (paused)
            {
                _controlsHelp.text = ControlsGuide(); // solo al pausar: nunca durante el juego
                _controls.ReleaseAll();
            }

            _pausePanel.SetActive(paused);
            if (paused)
            {
                _controls.SetActive(false);
            }
            else
            {
                ApplyControlsVisibility();
            }

            RefreshPauseLabels();
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

        private void RefreshPauseLabels()
        {
            _lefty.Label.text = "CONTROLES PARA ZURDOS: " + (_settings.LeftHanded ? "SÍ" : "NO");
            _names.Label.text = "NOMBRES: " + (_settings.Names == NameMode.All ? "TODOS" : _settings.Names == NameMode.Controlled ? "SOLO EL TUYO Y EL BALÓN" : "NINGUNO");
            _debugToggle.Label.text = "PANEL DE DEPURACIÓN: " + (_settings.ShowDebug ? "SÍ" : "NO");
            _fpsToggle.Label.text = "FPS OBJETIVO: " + _settings.TargetFps;
            if (_engine != null)
            {
                TeamState own = _engine.Teams[0];
                _mentality.Label.text = "MENTALIDAD: " + TeamCatalog.MentalityLabels[(int)own.Mentality + 2];
                _pressing.Label.text = "PRESIÓN: " + TeamCatalog.PressLabels[(int)own.Pressing + 1];
            }
        }

        private void HandlePauseInput()
        {
            for (int i = 0; i < _pointers.Count; i++)
            {
                PointerState p = _pointers.Pointers[i];
                if (p.Phase != PointerPhase.Began)
                {
                    continue;
                }

                Vector2 point = UiKit.ToArea(p.Position);
                if (_resume.Contains(point))
                {
                    SetPaused(false);
                }
                else if (_quit.Contains(point))
                {
                    SetPaused(false);
                    QuitRequested = true;
                }
                else if (_names.Contains(point))
                {
                    _settings.Names = (NameMode)(((int)_settings.Names + 1) % 3);
                    _settings.Save();
                    RefreshPauseLabels();
                }
                else if (_lefty.Contains(point))
                {
                    _settings.LeftHanded = !_settings.LeftHanded;
                    _settings.Save();
                    _controls.Layout(_settings.LeftHanded, _settings.BigButtons, _settings.ShowThrough, _settings.ShowCross);
                    RefreshPauseLabels();
                }
                else if (_debugToggle.Contains(point))
                {
                    _settings.ShowDebug = !_settings.ShowDebug;
                    _settings.Save();
                    _debug.SetVisible(_settings.ShowDebug);
                    RefreshPauseLabels();
                }
                else if (_mentality.Contains(point) && _engine != null)
                {
                    TeamState own = _engine.Teams[0];
                    int next = (int)own.Mentality + 1 > (int)Mentality.VeryAttacking ? (int)Mentality.VeryDefensive : (int)own.Mentality + 1;
                    _engine.SetTactics(0, (Mentality)next, own.Pressing);
                    RefreshPauseLabels();
                }
                else if (_pressing.Contains(point) && _engine != null)
                {
                    TeamState own = _engine.Teams[0];
                    int next = (int)own.Pressing + 1 > (int)PressLevel.High ? (int)PressLevel.Low : (int)own.Pressing + 1;
                    _engine.SetTactics(0, own.Mentality, (PressLevel)next);
                    RefreshPauseLabels();
                }
                else if (_fpsToggle.Contains(point))
                {
                    _settings.TargetFps = _settings.TargetFps == 30 ? 60 : 30;
                    _settings.Save();
                    Application.targetFrameRate = _settings.TargetFps;
                    RefreshPauseLabels();
                }
            }
        }
    }
}
