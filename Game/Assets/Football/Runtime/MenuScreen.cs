using UnityEngine;
using UnityEngine.UI;

namespace Football.Game
{
    /// <summary>Menú principal: elegir rival (con su estilo de juego), dificultad y duración.</summary>
    public sealed class MenuScreen
    {
        private static readonly Color Idle = new Color(0.22f, 0.25f, 0.32f);
        private static readonly Color Selected = new Color(0.15f, 0.6f, 0.3f);

        private readonly PointerInput _pointers;
        private readonly GameSettings _settings;
        private readonly Canvas _canvas;
        private readonly UiButton[] _opponents;
        private readonly UiButton[] _difficulties;
        private readonly UiButton[] _lengths;
        private readonly UiButton _play;
        private readonly UiButton _benchmark;
        private readonly Text _error;
        private readonly GameObject _errorPanel;

        /// <summary>Se pidió empezar un partido.</summary>
        public bool PlayRequested { get; private set; }

        /// <summary>Se pidió el partido de medición de rendimiento.</summary>
        public bool BenchmarkRequested { get; private set; }

        /// <summary>Crea el menú.</summary>
        public MenuScreen(Transform parent, PointerInput pointers, GameSettings settings)
        {
            _pointers = pointers;
            _settings = settings;
            _canvas = UiKit.CreateCanvas("MenuUI", parent, 5);
            Transform c = _canvas.transform;

            UiKit.Panel(c, "Fondo", new Vector2(960f, 540f), UiKit.Reference, new Color(0.06f, 0.16f, 0.1f));
            UiKit.Label(c, "Titulo", new Vector2(960f, 1000f), new Vector2(1400f, 120f), "FÚTBOL", 96, Color.white);
            UiKit.Label(c, "Rival", new Vector2(480f, 905f), new Vector2(700f, 60f), "RIVAL", 40, new Color(1f, 0.9f, 0.4f));
            UiKit.Label(c, "Dificultad", new Vector2(1380f, 905f), new Vector2(600f, 60f), "DIFICULTAD", 40, new Color(1f, 0.9f, 0.4f));
            UiKit.Label(c, "Duracion", new Vector2(1380f, 405f), new Vector2(600f, 60f), "DURACIÓN DE CADA PARTE", 36, new Color(1f, 0.9f, 0.4f));

            _opponents = new UiButton[TeamCatalog.Opponents.Length];
            for (int i = 0; i < _opponents.Length; i++)
            {
                OpponentInfo o = TeamCatalog.Opponents[i];
                string text = o.Name + "   ·   " + TeamCatalog.StyleLabels[o.StyleIndex] + "   ·   " + o.Rating.ToString("F0");
                _opponents[i] = UiKit.Button(c, "Rival" + i, new Vector2(480f, 810f - i * 125f), new Vector2(780f, 105f), text, 34, Idle);
            }

            _difficulties = new UiButton[TeamCatalog.DifficultyLabels.Length];
            for (int i = 0; i < _difficulties.Length; i++)
            {
                _difficulties[i] = UiKit.Button(c, "Dif" + i, new Vector2(1380f, 810f - i * 78f), new Vector2(520f, 66f),
                    TeamCatalog.DifficultyLabels[i].ToUpperInvariant(), 32, Idle);
            }

            _lengths = new UiButton[TeamCatalog.HalfMinutes.Length];
            for (int i = 0; i < _lengths.Length; i++)
            {
                _lengths[i] = UiKit.Button(c, "Dur" + i, new Vector2(1160f + i * 220f, 330f), new Vector2(190f, 80f),
                    TeamCatalog.HalfMinutes[i] + " MIN", 32, Idle);
            }

            _play = UiKit.Button(c, "Jugar", new Vector2(1380f, 170f), new Vector2(640f, 140f), "JUGAR", 64, new Color(0.15f, 0.65f, 0.25f));
            _benchmark = UiKit.Button(c, "Benchmark", new Vector2(240f, 1010f), new Vector2(420f, 70f), "PRUEBA DE RENDIMIENTO", 24, new Color(0.35f, 0.35f, 0.45f));
            UiKit.Label(c, "Ayuda", new Vector2(960f, 20f), new Vector2(1800f, 40f),
                "En el ordenador: WASD mover · J pase · K tiro (mantener) · L filtrado · I centro · E presionar · Tab cambiar · Shift esprint · Esc pausa",
                22, new Color(1f, 1f, 1f, 0.6f));
            UiKit.Label(c, "Foco", new Vector2(960f, 60f), new Vector2(1800f, 40f),
                "En el ordenador, haz clic dentro de esta ventana antes de usar el teclado.", 22, new Color(1f, 1f, 1f, 0.6f));
            Image errorPanel = UiKit.Panel(c, "ErrorMenu", new Vector2(960f, 540f), new Vector2(1600f, 300f), new Color(0.35f, 0.05f, 0.05f, 0.95f));
            _errorPanel = errorPanel.gameObject;
            _error = UiKit.Label(errorPanel.transform, "Texto", new Vector2(800f, 150f), new Vector2(1560f, 280f), string.Empty, 28, Color.white);
            _errorPanel.SetActive(false);
            Refresh();
            Hide();
        }

        /// <summary>Muestra un mensaje de error en el menú (vacío para quitarlo).</summary>
        public void SetError(string message)
        {
            bool show = !string.IsNullOrEmpty(message);
            _error.text = show ? message + "\n\n(toca para cerrar)" : string.Empty;
            _errorPanel.SetActive(show);
        }

        /// <summary>Muestra el menú.</summary>
        public void Show()
        {
            PlayRequested = false;
            BenchmarkRequested = false;
            SetError(null);
            _canvas.gameObject.SetActive(true);
            Refresh();
        }

        /// <summary>Oculta el menú.</summary>
        public void Hide()
        {
            _canvas.gameObject.SetActive(false);
        }

        /// <summary>Elección actual de rival.</summary>
        public OpponentInfo SelectedOpponent => TeamCatalog.Opponents[_settings.Opponent];

        private void Refresh()
        {
            for (int i = 0; i < _opponents.Length; i++)
            {
                _opponents[i].Background.color = i == _settings.Opponent ? Selected : Idle;
            }

            for (int i = 0; i < _difficulties.Length; i++)
            {
                _difficulties[i].Background.color = i == _settings.Difficulty ? Selected : Idle;
            }

            for (int i = 0; i < _lengths.Length; i++)
            {
                _lengths[i].Background.color = i == _settings.Length ? Selected : Idle;
            }
        }

        /// <summary>Procesa los toques del fotograma.</summary>
        public void Update()
        {
            float scale = _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            for (int p = 0; p < _pointers.Count; p++)
            {
                if (_pointers.Pointers[p].Phase != PointerPhase.Began)
                {
                    continue;
                }

                Vector2 point = _pointers.Pointers[p].Position / scale;
                if (_errorPanel.activeSelf)
                {
                    SetError(null);
                    continue;
                }

                for (int i = 0; i < _opponents.Length; i++)
                {
                    if (_opponents[i].Contains(point))
                    {
                        _settings.Opponent = i;
                    }
                }

                for (int i = 0; i < _difficulties.Length; i++)
                {
                    if (_difficulties[i].Contains(point))
                    {
                        _settings.Difficulty = i;
                    }
                }

                for (int i = 0; i < _lengths.Length; i++)
                {
                    if (_lengths[i].Contains(point))
                    {
                        _settings.Length = i;
                    }
                }

                if (_play.Contains(point))
                {
                    PlayRequested = true;
                }
                else if (_benchmark.Contains(point))
                {
                    BenchmarkRequested = true;
                }

                _settings.Save();
                Refresh();
            }
        }
    }

    /// <summary>Pantalla de resultado al terminar el partido.</summary>
    public sealed class ResultScreen
    {
        private readonly PointerInput _pointers;
        private readonly Canvas _canvas;
        private readonly Text _title;
        private readonly Text _body;
        private readonly UiButton _again;
        private readonly UiButton _menu;

        /// <summary>Se pidió jugar otra vez.</summary>
        public bool AgainRequested { get; private set; }

        /// <summary>Se pidió volver al menú.</summary>
        public bool MenuRequested { get; private set; }

        /// <summary>Crea la pantalla.</summary>
        public ResultScreen(Transform parent, PointerInput pointers)
        {
            _pointers = pointers;
            _canvas = UiKit.CreateCanvas("ResultadoUI", parent, 20);
            Transform c = _canvas.transform;
            UiKit.Panel(c, "Fondo", new Vector2(960f, 540f), UiKit.Reference, new Color(0.04f, 0.1f, 0.07f, 0.96f));
            UiKit.Label(c, "Final", new Vector2(960f, 960f), new Vector2(1200f, 100f), "FINAL DEL PARTIDO", 64, new Color(1f, 0.9f, 0.4f));
            _title = UiKit.Label(c, "Marcador", new Vector2(960f, 840f), new Vector2(1600f, 110f), string.Empty, 72, Color.white);
            _body = UiKit.Label(c, "Estadisticas", new Vector2(960f, 520f), new Vector2(1500f, 480f), string.Empty, 36, Color.white);
            _again = UiKit.Button(c, "Otra", new Vector2(640f, 130f), new Vector2(520f, 120f), "OTRA VEZ", 48, new Color(0.15f, 0.65f, 0.25f));
            _menu = UiKit.Button(c, "Menu", new Vector2(1280f, 130f), new Vector2(520f, 120f), "MENÚ", 48, new Color(0.3f, 0.3f, 0.45f));
            Hide();
        }

        /// <summary>Muestra el resultado.</summary>
        public void Show(string result, string summary)
        {
            AgainRequested = false;
            MenuRequested = false;
            _title.text = result;
            _body.text = summary;
            _canvas.gameObject.SetActive(true);
        }

        /// <summary>Oculta la pantalla.</summary>
        public void Hide()
        {
            _canvas.gameObject.SetActive(false);
        }

        /// <summary>Procesa los toques del fotograma.</summary>
        public void Update()
        {
            float scale = _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            for (int p = 0; p < _pointers.Count; p++)
            {
                if (_pointers.Pointers[p].Phase != PointerPhase.Began)
                {
                    continue;
                }

                Vector2 point = _pointers.Pointers[p].Position / scale;
                if (_again.Contains(point))
                {
                    AgainRequested = true;
                }
                else if (_menu.Contains(point))
                {
                    MenuRequested = true;
                }
            }
        }
    }
}
