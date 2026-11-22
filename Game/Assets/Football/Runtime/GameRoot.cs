using UnityEngine;

namespace Football.Game
{
    /// <summary>
    /// Raíz del juego: recorrido entre pantallas (menú principal → equipos → alineación → ajustes → partido → resultado)
    /// y ajustes del dispositivo.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        private enum State { MainMenu, Teams, Lineup, MatchSetup, Settings, Controls, Match, Result }

        private readonly PointerInput _pointers = new PointerInput();
        private GameSettings _settings;
        private FriendlySetup _friendly;
        private MainMenuScreen _mainMenu;
        private TeamSelectScreen _teams;
        private LineupScreen _lineup;
        private MatchSetupScreen _matchSetup;
        private SettingsScreen _settingsScreen;
        private ControlsScreen _controlsScreen;
        private MatchScreen _match;
        private ResultScreen _result;
        private State _state;
        private bool _lastWasBenchmark;
        private bool _indoor;
        private GameAudio _audio;
        private readonly MenuBackdrop _backdrop = new MenuBackdrop();

        /// <summary>Cámara única del juego fuera del partido (menús); el partido la apaga mientras usa la suya.</summary>
        public static Camera MenuCamera { get; private set; }

        private void Awake()
        {
            _settings = GameSettings.Load();
            _friendly = new FriendlySetup(_settings);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = _settings.TargetFps;
            // Horizontal hacia cualquiera de los dos lados (el teléfono puede girarse), nunca vertical.
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
            PrepareScene();
            SettingsCatalog.Apply(_settings);

            _mainMenu = new MainMenuScreen(transform, _pointers);
            _teams = new TeamSelectScreen(transform, _pointers, _friendly);
            _lineup = new LineupScreen(transform, _pointers, _friendly);
            _matchSetup = new MatchSetupScreen(transform, _pointers, _settings, _friendly);
            _settingsScreen = new SettingsScreen(transform, _pointers, _settings);
            _controlsScreen = new ControlsScreen(transform, _pointers);
            _audio = new GameAudio(gameObject, _settings);
            _match = new MatchScreen(transform, _pointers, _settings, _audio);
            _result = new ResultScreen(transform, _pointers);
            Go(State.MainMenu);
        }

        /// <summary>
        /// Apaga lo que trae la escena de plantilla (cámara con su AudioListener, sombras de las luces) y crea una cámara propia
        /// que dibuja los menús con un color liso. Se hace una sola vez al arrancar.
        /// </summary>
        private void PrepareScene()
        {
            Camera[] existing = Camera.allCameras;
            for (int i = 0; i < existing.Length; i++)
            {
                existing[i].gameObject.SetActive(false);
            }

            try
            {
                Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                for (int i = 0; lights != null && i < lights.Length; i++)
                {
                    lights[i].shadows = LightShadows.None; // materiales sin iluminación: las sombras solo costarían GPU
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Football: no se pudieron apagar las sombras de la escena (" + e.Message + ")");
            }

            var go = new GameObject("CamaraMenu");
            go.transform.SetParent(transform, false);
            go.tag = "MainCamera";
            MenuCamera = go.AddComponent<Camera>();
            MenuCamera.clearFlags = CameraClearFlags.SolidColor;
            MenuCamera.backgroundColor = new Color(0.02f, 0.03f, 0.05f);
            MenuCamera.cullingMask = 0; // los menús son interfaz superpuesta: la cámara solo limpia la pantalla
            MenuCamera.allowHDR = false;
            MenuCamera.allowMSAA = false;
            go.AddComponent<AudioListener>();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && _state == State.Match)
            {
                _match.PauseIfPlaying(); // al volver, el partido espera en pausa
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && _state == State.Match)
            {
                _match.PauseIfPlaying();
            }
        }

        private void Go(State next)
        {
            // La pantalla solo se mantiene encendida durante el partido.
            Screen.sleepTimeout = next == State.Match ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
            if (next == State.Match)
            {
                _backdrop.Stop(); // el partido usa la cámara y la CPU
            }
            _mainMenu.Hide();
            _teams.Hide();
            _lineup.Hide();
            _matchSetup.Hide();
            _settingsScreen.Hide();
            _controlsScreen.Hide();
            _result.Hide();
            if (next != State.Match)
            {
                _match.End();
            }

            _state = next;
            switch (next)
            {
                case State.MainMenu: _mainMenu.Show(); break;
                case State.Teams: _teams.Show(); break;
                case State.MatchSetup: _matchSetup.Show(); break;
                case State.Settings: _settingsScreen.Show(); break;
                case State.Controls: _controlsScreen.Show(); break;
            }
        }

        private void EditLineup(int side)
        {
            Go(State.Lineup);
            _lineup.Show(side);
        }

        private void StartMatch(bool benchmark)
        {
            _lastWasBenchmark = benchmark;
            if (benchmark)
            {
                _indoor = false; // la prueba de rendimiento es siempre 11 contra 11
                _friendly.Indoor = false;
            }

            KitColors homeKit = _friendly.KitOf(0);
            KitColors awayKit = _friendly.KitOf(1);
            if (_friendly.KitsClash)
            {
                // Si las camisetas se confunden, el rival usa la otra.
                Football.Core.Data.TeamDefinition rival = _friendly.Definition(1);
                awayKit = KitColors.From(_friendly.Kit(1) == 0 ? rival.Away : rival.Home);
            }

            Go(State.Match);
            if (_match.Begin(_friendly.Definition(0), _friendly.Definition(1), _friendly.Squad(0), _friendly.Squad(1),
                    homeKit, awayKit, _settings.Difficulty, TeamCatalog.HalfMinutes[_settings.Length], benchmark, _indoor && !benchmark))
            {
                _mainMenu.SetError(null);
                return;
            }

            // Si el partido no puede arrancar se vuelve al menú mostrando el motivo.
            Go(State.MainMenu);
            _mainMenu.SetError("No se pudo empezar el partido. Manda una captura de este mensaje:\n" + _match.LastError);
        }

        private void Update()
        {
            _pointers.Update();
            if (_state != State.Match)
            {
                if (_match.IsIdle)
                {
                    _backdrop.Start();
                }

                _backdrop.Update(Time.unscaledDeltaTime);
            }

            switch (_state)
            {
                case State.MainMenu:
                    _mainMenu.Update();
                    if (_pointers.Keyboard.PauseDown)
                    {
                        Application.Quit(); // botón atrás de Android en el menú principal
                    }
                    else if (_mainMenu.Choice == MainMenuChoice.Friendly || _mainMenu.Choice == MainMenuChoice.Indoor)
                    {
                        _indoor = _mainMenu.Choice == MainMenuChoice.Indoor;
                        _friendly.Indoor = _indoor;
                        Go(State.Teams);
                    }
                    else if (_mainMenu.Choice == MainMenuChoice.Settings)
                    {
                        Go(State.Settings);
                    }
                    else if (_mainMenu.Choice == MainMenuChoice.Benchmark)
                    {
                        StartMatch(true);
                    }

                    break;

                case State.Teams:
                    _teams.Update();
                    switch (_teams.Action)
                    {
                        case TeamSelectAction.Back: Go(State.MainMenu); break;
                        case TeamSelectAction.Continue: Go(State.MatchSetup); break;
                        case TeamSelectAction.EditHomeLineup: EditLineup(0); break;
                        case TeamSelectAction.EditAwayLineup: EditLineup(1); break;
                    }

                    break;

                case State.Lineup:
                    _lineup.Update();
                    if (_lineup.Done)
                    {
                        Go(State.Teams);
                    }

                    break;

                case State.MatchSetup:
                    _matchSetup.Update();
                    if (_matchSetup.Action == MatchSetupAction.Back)
                    {
                        Go(State.Teams);
                    }
                    else if (_matchSetup.Action == MatchSetupAction.Play)
                    {
                        StartMatch(false);
                    }

                    break;

                case State.Settings:
                    _settingsScreen.Update();
                    if (_settingsScreen.BackRequested)
                    {
                        Go(State.MainMenu);
                    }
                    else if (_settingsScreen.KeysRequested)
                    {
                        Go(State.Controls);
                    }

                    break;

                case State.Controls:
                    _controlsScreen.Update();
                    if (_controlsScreen.BackRequested)
                    {
                        Go(State.Settings);
                    }

                    break;

                case State.Match:
                    _match.Update(Time.unscaledDeltaTime);
                    if (_match.QuitRequested)
                    {
                        Go(State.MainMenu);
                    }
                    else if (_match.Finished)
                    {
                        string line = _match.BuildResultLine();
                        string summary = _match.BuildSummary();
                        Go(State.Result);
                        _result.Show(line, summary);
                    }

                    break;

                case State.Result:
                    _result.Update();
                    if (_pointers.Keyboard.PauseDown)
                    {
                        Go(State.MainMenu);
                    }
                    else if (_result.AgainRequested)
                    {
                        StartMatch(_lastWasBenchmark);
                    }
                    else if (_result.MenuRequested)
                    {
                        Go(State.MainMenu);
                    }

                    break;
            }
        }
    }

    /// <summary>Arranque automático: crea el juego al cargar cualquier escena, sin tener que montar nada a mano.</summary>
    public static class Bootstrap
    {
        // Se recuerda el juego creado en lugar de buscarlo en la escena: la búsqueda de Unity (FindFirstObjectByType)
        // falló con NullReferenceException al arrancar en el editor del usuario.
        private static GameObject _instance;

        /// <summary>Olvida la instancia al entrar en modo de juego (por si el editor no recarga los scripts entre partidas).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (_instance != null)
            {
                return;
            }

            _instance = new GameObject("FootballGame");
            Object.DontDestroyOnLoad(_instance);
            _instance.AddComponent<GameRoot>();
        }
    }
}
