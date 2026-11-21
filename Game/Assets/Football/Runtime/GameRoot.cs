using UnityEngine;

namespace Football.Game
{
    /// <summary>Raíz del juego: gestiona las pantallas (menú, partido, resultado) y los ajustes del dispositivo.</summary>
    public sealed class GameRoot : MonoBehaviour
    {
        private enum State { Menu, Match, Result }

        private readonly PointerInput _pointers = new PointerInput();
        private GameSettings _settings;
        private MenuScreen _menu;
        private MatchScreen _match;
        private ResultScreen _result;
        private State _state;
        private bool _lastWasBenchmark;

        private void Awake()
        {
            _settings = GameSettings.Load();
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = _settings.TargetFps;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Screen.orientation = ScreenOrientation.LandscapeLeft;

            _menu = new MenuScreen(transform, _pointers, _settings);
            _match = new MatchScreen(transform, _pointers, _settings);
            _result = new ResultScreen(transform, _pointers);
            ShowMenu();
        }

        private void ShowMenu()
        {
            _match.End();
            _result.Hide();
            _menu.Show();
            _state = State.Menu;
        }

        private void StartMatch(bool benchmark)
        {
            _lastWasBenchmark = benchmark;
            _menu.Hide();
            _result.Hide();
            if (_match.Begin(_menu.SelectedOpponent, _settings.Difficulty, TeamCatalog.HalfMinutes[_settings.Length], benchmark))
            {
                _state = State.Match;
                return;
            }

            // Si el partido no puede arrancar se vuelve al menú mostrando el motivo (antes se reintentaba en bucle).
            ShowMenu();
            _menu.SetError("No se pudo empezar el partido. Manda una captura de este mensaje:\n" + _match.LastError);
        }

        private void Update()
        {
            _pointers.Update();
            switch (_state)
            {
                case State.Menu:
                    _menu.Update();
                    if (_menu.PlayRequested)
                    {
                        StartMatch(false);
                    }
                    else if (_menu.BenchmarkRequested)
                    {
                        StartMatch(true);
                    }

                    break;

                case State.Match:
                    _match.Update(Time.unscaledDeltaTime);
                    if (_match.QuitRequested)
                    {
                        ShowMenu();
                    }
                    else if (_match.Finished)
                    {
                        _result.Show(_match.BuildResultLine(), _match.BuildSummary());
                        _match.End();
                        _state = State.Result;
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
                        ShowMenu();
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
