using UnityEngine;
using UnityEngine.UI;

namespace Football.Game
{
    /// <summary>Acción pedida en los ajustes del partido.</summary>
    public enum MatchSetupAction
    {
        /// <summary>Ninguna.</summary>
        None,
        /// <summary>Volver a la elección de equipos.</summary>
        Back,
        /// <summary>Empezar el partido.</summary>
        Play
    }

    /// <summary>Ajustes del partido amistoso: dificultad, duración y nombres sobre los jugadores.</summary>
    public sealed class MatchSetupScreen : MenuScreenBase
    {
        private readonly GameSettings _settings;
        private readonly FriendlySetup _setup;
        private readonly Text _match;
        private readonly UiButton[] _difficulties;
        private readonly UiButton[] _lengths;
        private readonly UiButton _names, _speed, _back, _play;
        private readonly UiButton[] _weather;

        /// <summary>Acción pedida en este fotograma.</summary>
        public MatchSetupAction Action { get; private set; }

        /// <summary>Crea la pantalla.</summary>
        public MatchSetupScreen(Transform parent, PointerInput pointers, GameSettings settings, FriendlySetup setup) : base(parent, pointers, "AjustesPartidoUI")
        {
            _settings = settings;
            _setup = setup;
            Title("AJUSTES DEL PARTIDO");
            _match = UiKit.Label(Root, "Partido", new Vector2(960f, 880f), new Vector2(1700f, 70f), string.Empty, 46, UiTheme.Text);

            UiKit.Label(Root, "Dificultad", new Vector2(960f, 800f), new Vector2(800f, 50f), "DIFICULTAD DEL RIVAL", 32, UiTheme.Accent);
            _difficulties = new UiButton[TeamCatalog.DifficultyLabels.Length];
            for (int i = 0; i < _difficulties.Length; i++)
            {
                _difficulties[i] = UiKit.Button(Root, "Dif" + i, new Vector2(360f + i * 300f, 725f), new Vector2(280f, 84f),
                    TeamCatalog.DifficultyLabels[i].ToUpperInvariant(), 28, UiTheme.Button);
            }

            UiKit.Label(Root, "Duracion", new Vector2(960f, 645f), new Vector2(800f, 50f), "MINUTOS POR TIEMPO", 32, UiTheme.Accent);
            _lengths = new UiButton[TeamCatalog.HalfMinutes.Length];
            for (int i = 0; i < _lengths.Length; i++)
            {
                _lengths[i] = UiKit.Button(Root, "Dur" + i, new Vector2(760f + i * 200f, 570f), new Vector2(170f, 84f),
                    TeamCatalog.HalfMinutes[i].ToString(), 36, UiTheme.Button);
            }

            UiKit.Label(Root, "Nombres", new Vector2(640f, 480f), new Vector2(600f, 50f), "NOMBRES SOBRE LOS JUGADORES", 32, UiTheme.Accent);
            _names = UiKit.Button(Root, "ModoNombres", new Vector2(640f, 405f), new Vector2(600f, 84f), string.Empty, 30, UiTheme.Button);
            UiKit.Label(Root, "Velocidad", new Vector2(1280f, 480f), new Vector2(600f, 50f), "VELOCIDAD DEL JUEGO", 32, UiTheme.Accent);
            _speed = UiKit.Button(Root, "ModoVelocidad", new Vector2(1280f, 405f), new Vector2(600f, 84f), string.Empty, 30, UiTheme.Button);

            UiKit.Label(Root, "Clima", new Vector2(960f, 315f), new Vector2(800f, 50f), Locale.Get("setup.weather"), 32, UiTheme.Accent);
            _weather = new UiButton[4];
            for (int i = 0; i < _weather.Length; i++)
            {
                _weather[i] = UiKit.Button(Root, "Clima" + i, new Vector2(510f + i * 300f, 240f), new Vector2(280f, 84f),
                    Locale.Get(i == 0 ? "weather.random" : "weather." + (i - 1)).ToUpperInvariant(), 28, UiTheme.Button);
            }

            _back = UiKit.Button(Root, "Atras", new Vector2(220f, 60f), new Vector2(300f, 84f), "ATRÁS", 36, UiTheme.Button);
            _play = UiKit.Button(Root, "Jugar", new Vector2(1660f, 80f), new Vector2(440f, 120f), "JUGAR", 56, UiTheme.AccentDark);
        }

        /// <inheritdoc />
        public override void Show()
        {
            Action = MatchSetupAction.None;
            base.Show();
            Refresh();
        }

        private void Refresh()
        {
            _match.text = _setup.Definition(0).Name + "   vs   " + _setup.Definition(1).Name;
            for (int i = 0; i < _difficulties.Length; i++)
            {
                _difficulties[i].Background.color = i == _settings.Difficulty ? UiTheme.Selected : UiTheme.Button;
            }

            for (int i = 0; i < _lengths.Length; i++)
            {
                _lengths[i].Background.color = i == _settings.Length ? UiTheme.Selected : UiTheme.Button;
            }

            for (int i = 0; i < _weather.Length; i++)
            {
                _weather[i].Background.color = i == _settings.Weather ? UiTheme.Selected : UiTheme.Button;
            }

            _names.Label.text = NameModeLabel(_settings.Names);
            _speed.Label.text = GameSettings.SpeedLabels[_settings.Speed];
        }

        /// <summary>Texto de cada modo de nombres.</summary>
        public static string NameModeLabel(NameMode mode) =>
            mode == NameMode.All ? "TODOS" : (mode == NameMode.Controlled ? "SOLO EL TUYO Y EL DEL BALÓN" : "NINGUNO");

        /// <summary>Procesa los toques del fotograma.</summary>
        public void Update()
        {
            Action = MatchSetupAction.None;
            if (BackKey)
            {
                Action = MatchSetupAction.Back;
                return;
            }

            int n = Taps(out Vector2[] taps);
            for (int t = 0; t < n; t++)
            {
                Vector2 p = taps[t];
                for (int i = 0; i < _difficulties.Length; i++)
                {
                    if (_difficulties[i].Contains(p))
                    {
                        _settings.Difficulty = i;
                    }
                }

                for (int i = 0; i < _lengths.Length; i++)
                {
                    if (_lengths[i].Contains(p))
                    {
                        _settings.Length = i;
                    }
                }

                for (int i = 0; i < _weather.Length; i++)
                {
                    if (_weather[i].Contains(p))
                    {
                        _settings.Weather = i;
                    }
                }

                if (_names.Contains(p))
                {
                    _settings.Names = (NameMode)(((int)_settings.Names + 1) % 3);
                }
                else if (_speed.Contains(p))
                {
                    _settings.Speed = (_settings.Speed + 1) % GameSettings.Speeds.Length;
                }
                else if (_back.Contains(p))
                {
                    Action = MatchSetupAction.Back;
                }
                else if (_play.Contains(p))
                {
                    Action = MatchSetupAction.Play;
                }

                _settings.Save();
                Refresh();
            }
        }
    }

    /// <summary>
    /// Ajustes completos del menú principal, por secciones (gráficos, audio, controles y cámara, reglas), con acceso a las
    /// teclas del ordenador. Durante el partido, la pausa ofrece solo los ajustes que se pueden cambiar en vivo.
    /// </summary>
    public sealed class SettingsScreen : MenuScreenBase
    {

        private readonly GameSettings _settings;
        private readonly UiButton[] _tabs = new UiButton[SettingsCatalog.CategoryLabels.Length];
        private readonly SettingsList _list;
        private readonly UiButton _keys, _back;
        private readonly GameObject _guidePanel;
        private readonly Text _guide;
        private SettingCategory _category;

        /// <summary>Se pidió volver al menú principal.</summary>
        public bool BackRequested { get; private set; }

        /// <summary>Se pidió abrir la configuración de teclas.</summary>
        public bool KeysRequested { get; private set; }

        /// <summary>Crea la pantalla.</summary>
        public SettingsScreen(Transform parent, PointerInput pointers, GameSettings settings) : base(parent, pointers, "AjustesUI")
        {
            _settings = settings;
            Title("AJUSTES");
            for (int i = 0; i < _tabs.Length; i++)
            {
                _tabs[i] = UiKit.Button(Root, "Seccion" + i, new Vector2(330f + i * 420f, 900f), new Vector2(400f, 64f),
                    SettingsCatalog.CategoryLabels[i], 28, UiTheme.Button);
            }

            _list = new SettingsList(Root, new Vector2(500f, 800f), new Vector2(880f, 60f), 68f, 10, 940f, 2); // dos columnas: el audio tiene una fila por canción
            // Guía de controles: solo en la pestaña de controles, sobre un panel opaco para que se lea bien.
            Image panel = UiKit.Panel(Root, "Guia", new Vector2(1440f, 500f), new Vector2(820f, 700f), new Color(0.07f, 0.09f, 0.13f, 1f));
            _guidePanel = panel.gameObject;
            UiKit.Label(panel.transform, "TituloGuia", new Vector2(410f, 655f), new Vector2(760f, 50f), "CONTROLES", 34, Color.white);
            _guide = UiKit.Label(panel.transform, "Texto", new Vector2(410f, 320f), new Vector2(760f, 600f), string.Empty, 23, UiTheme.Text,
                TextAnchor.UpperLeft);
            _keys = UiKit.Button(Root, "Teclas", new Vector2(560f, 100f), new Vector2(880f, 64f), "CONTROLES DE TECLADO (ORDENADOR) ›", 26, UiTheme.Button);
            _back = UiKit.Button(Root, "Atras", new Vector2(1440f, 60f), new Vector2(300f, 84f), "ATRÁS", 36, UiTheme.Button);
        }

        /// <inheritdoc />
        public override void Show()
        {
            BackRequested = false;
            KeysRequested = false;
            base.Show();
            SelectCategory(_category);
        }

        private void SelectCategory(SettingCategory category)
        {
            _category = category;
            for (int i = 0; i < _tabs.Length; i++)
            {
                _tabs[i].Background.color = i == (int)category ? UiTheme.Selected : UiTheme.Button;
            }

            _list.Show(_settings, category, false);
            bool controls = category == SettingCategory.Controls;
            _guidePanel.SetActive(controls);
            if (controls)
            {
                _guide.text = ControlsHelp.Text(); // con las teclas asignadas ahora
            }
        }

        /// <summary>Procesa los toques del fotograma.</summary>
        public void Update()
        {
            BackRequested = BackKey;
            int n = Taps(out Vector2[] taps);
            for (int t = 0; t < n; t++)
            {
                Vector2 p = taps[t];
                for (int i = 0; i < _tabs.Length; i++)
                {
                    if (_tabs[i].Contains(p))
                    {
                        SelectCategory((SettingCategory)i);
                    }
                }

                if (_list.Tap(p, _settings))
                {
                    SettingsCatalog.Apply(_settings);
                }
                else if (_keys.Contains(p))
                {
                    KeysRequested = true;
                }
                else if (_back.Contains(p))
                {
                    BackRequested = true;
                }
            }
        }
    }

    /// <summary>Pantalla de resultado al terminar el partido.</summary>
    public sealed class ResultScreen : MenuScreenBase
    {
        private readonly Text _score;
        private readonly Text _body;
        private readonly UiButton _again;
        private readonly UiButton _menu;

        /// <summary>Se pidió la revancha.</summary>
        public bool AgainRequested { get; private set; }

        /// <summary>Se pidió volver al menú principal.</summary>
        public bool MenuRequested { get; private set; }

        /// <summary>Crea la pantalla.</summary>
        public ResultScreen(Transform parent, PointerInput pointers) : base(parent, pointers, "ResultadoUI")
        {
            Title("FINAL DEL PARTIDO");
            _score = UiKit.Label(Root, "Marcador", new Vector2(960f, 840f), new Vector2(1700f, 110f), string.Empty, 68, UiTheme.Text);
            Image panel = UiTheme.Frame(Root, "Estadisticas", new Vector2(960f, 480f), new Vector2(1300f, 520f));
            _body = UiKit.Label(panel.transform, "Texto", new Vector2(650f, 260f), new Vector2(1240f, 500f), string.Empty, 34, UiTheme.Text);
            _again = UiKit.Button(Root, "Otra", new Vector2(640f, 110f), new Vector2(520f, 110f), "REVANCHA", 44, UiTheme.AccentDark);
            _menu = UiKit.Button(Root, "Menu", new Vector2(1280f, 110f), new Vector2(520f, 110f), MenuLabel, 44, UiTheme.Button);
        }

        /// <summary>Muestra el resultado.</summary>
        public void Show(string result, string summary)
        {
            Show(result, summary, false);
        }

        /// <summary>Muestra el resultado; en el modo carrera no hay revancha y el botón vuelve a la carrera.</summary>
        /// <param name="result">Marcador.</param>
        /// <param name="summary">Estadísticas.</param>
        /// <param name="career">Partido del modo carrera o del torneo.</param>
        public void Show(string result, string summary, bool career)
        {
            AgainRequested = false;
            MenuRequested = false;
            _score.text = result;
            _body.text = summary;
            _again.SetVisible(!career);
            _menu.Label.text = career ? Locale.Get("career.continue_button") : MenuLabel;
            Show();
        }

        private const string MenuLabel = "MENÚ PRINCIPAL";

        /// <summary>Procesa los toques del fotograma.</summary>
        public void Update()
        {
            int n = Taps(out Vector2[] taps);
            for (int t = 0; t < n; t++)
            {
                if (_again.Contains(taps[t]))
                {
                    AgainRequested = true;
                }
                else if (_menu.Contains(taps[t]))
                {
                    MenuRequested = true;
                }
            }
        }
    }
}
