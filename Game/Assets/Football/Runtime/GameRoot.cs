using UnityEngine;

namespace Football.Game
{
    /// <summary>
    /// Raíz del juego: recorrido entre pantallas (menú principal → equipos → alineación → ajustes → partido → resultado)
    /// y ajustes del dispositivo.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        private enum State { MainMenu, Teams, Lineup, MatchSetup, Settings, Match, Result }

        private readonly PointerInput _pointers = new PointerInput();
        private GameSettings _settings;
        private FriendlySetup _friendly;
        private MainMenuScreen _mainMenu;
        private TeamSelectScreen _teams;
        private LineupScreen _lineup;
        private MatchSetupScreen _matchSetup;
        private SettingsScreen _settingsScreen;
        private MatchScreen _match;
        private ResultScreen _result;
        private State _state;
        private bool _lastWasBenchmark;

        private void Awake()
        {
            _settings = GameSettings.Load();
            _friendly = new FriendlySetup(_settings);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = _settings.TargetFps;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.LandscapeLeft;

            _mainMenu = new MainMenuScreen(transform, _pointers);
            _teams = new TeamSelectScreen(transform, _pointers, _friendly);
            _lineup = new LineupScreen(transform, _pointers, _friendly);
            _matchSetup = new MatchSetupScreen(transform, _pointers, _settings, _friendly);
            _settingsScreen = new SettingsScreen(transform, _pointers, _settings);
            _match = new MatchScreen(transform, _pointers, _settings);
            _result = new ResultScreen(transform, _pointers);
            Go(State.MainMenu);
        }

        private void Go(State next)
        {
            _mainMenu.Hide();
            _teams.Hide();
            _lineup.Hide();
            _matchSetup.Hide();
            _settingsScreen.Hide();
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
                    homeKit, awayKit, _settings.Difficulty, TeamCatalog.HalfMinutes[_settings.Length], benchmark))
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
            switch (_state)
            {
                case State.MainMenu:
                    _mainMenu.Update();
                    if (_mainMenu.Choice == MainMenuChoice.Friendly)
                    {
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
                    if (_result.AgainRequested)
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
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (Object.FindFirstObjectByType<GameRoot>() != null)
            {
                return;
            }

            var go = new GameObject("FootballGame");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<GameRoot>();
        }
    }
}
