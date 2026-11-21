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

        /// <summary>Acción pedida en este fotograma.</summary>
        public MatchSetupAction Action { get; private set; }

        /// <summary>Crea la pantalla.</summary>
        public MatchSetupScreen(Transform parent, PointerInput pointers, GameSettings settings, FriendlySetup setup) : base(parent, pointers, "AjustesPartidoUI")
        {
            _settings = settings;
            _setup = setup;
            Title("AJUSTES DEL PARTIDO");
            _match = UiKit.Label(Root, "Partido", new Vector2(960f, 880f), new Vector2(1700f, 70f), string.Empty, 46, UiTheme.Text);

            UiKit.Label(Root, "Dificultad", new Vector2(960f, 770f), new Vector2(800f, 50f), "DIFICULTAD DEL RIVAL", 32, UiTheme.Accent);
            _difficulties = new UiButton[TeamCatalog.DifficultyLabels.Length];
            for (int i = 0; i < _difficulties.Length; i++)
            {
                _difficulties[i] = UiKit.Button(Root, "Dif" + i, new Vector2(360f + i * 300f, 690f), new Vector2(280f, 84f),
                    TeamCatalog.DifficultyLabels[i].ToUpperInvariant(), 28, UiTheme.Button);
            }

            UiKit.Label(Root, "Duracion", new Vector2(960f, 570f), new Vector2(800f, 50f), "MINUTOS POR TIEMPO", 32, UiTheme.Accent);
            _lengths = new UiButton[TeamCatalog.HalfMinutes.Length];
            for (int i = 0; i < _lengths.Length; i++)
            {
                _lengths[i] = UiKit.Button(Root, "Dur" + i, new Vector2(760f + i * 200f, 490f), new Vector2(170f, 84f),
                    TeamCatalog.HalfMinutes[i].ToString(), 36, UiTheme.Button);
            }

            UiKit.Label(Root, "Nombres", new Vector2(640f, 380f), new Vector2(600f, 50f), "NOMBRES SOBRE LOS JUGADORES", 32, UiTheme.Accent);
            _names = UiKit.Button(Root, "ModoNombres", new Vector2(640f, 300f), new Vector2(600f, 84f), string.Empty, 30, UiTheme.Button);
            UiKit.Label(Root, "Velocidad", new Vector2(1280f, 380f), new Vector2(600f, 50f), "VELOCIDAD DEL JUEGO", 32, UiTheme.Accent);
            _speed = UiKit.Button(Root, "ModoVelocidad", new Vector2(1280f, 300f), new Vector2(600f, 84f), string.Empty, 30, UiTheme.Button);

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

    /// <summary>Ajustes generales: velocidad del juego, botones, controles, nombres, FPS y panel de depuración, con una guía de controles.</summary>
    public sealed class SettingsScreen : MenuScreenBase
    {
        private const float RowX = 560f;
        private const float RowTop = 860f;
        private const float RowStep = 92f;

        private const string Guide =
            "CONTROLES\n\n"
            + "Mitad izquierda de la pantalla: arrastra para mover.\n"
            + "ESPRINT: mantén pulsado para correr más.\n\n"
            + "CON EL BALÓN\n"
            + "PASE: al compañero hacia donde apuntas.\n"
            + "TIRO: mantén para cargar y suelta para tirar.\n"
            + "DESPEJAR: pelotazo largo y alto (en tu campo).\n"
            + "FILTRADO y CENTRO: opcionales (actívalos aquí).\n\n"
            + "SIN EL BALÓN\n"
            + "PRESIONAR: mantén; persigue al rival y le quita\nla pelota sin barrerse.\n"
            + "BARRER: llega más lejos, pero si fallas quedas\nen el suelo y puede ser falta (más por detrás).\n"
            + "CAMBIAR: controla al compañero más cercano.\n"
            + "ARQUERO: controla al arquero para salir, barrer,\npasar o despejar. JUGADOR lo devuelve.";

        private readonly GameSettings _settings;
        private readonly UiButton _speed, _through, _cross, _big, _lefty, _names, _fps, _debug, _back;

        /// <summary>Se pidió volver al menú principal.</summary>
        public bool BackRequested { get; private set; }

        /// <summary>Crea la pantalla.</summary>
        public SettingsScreen(Transform parent, PointerInput pointers, GameSettings settings) : base(parent, pointers, "AjustesUI")
        {
            _settings = settings;
            Title("AJUSTES");
            _speed = Row("Velocidad", 0);
            _through = Row("Filtrado", 1);
            _cross = Row("Centro", 2);
            _big = Row("Tamano", 3);
            _lefty = Row("Zurdo", 4);
            _names = Row("Nombres", 5);
            _fps = Row("Fps", 6);
            _debug = Row("Depuracion", 7);
            Image panel = UiKit.Panel(Root, "Guia", new Vector2(1440f, 520f), new Vector2(820f, 800f), UiTheme.Panel);
            UiKit.Label(panel.transform, "Texto", new Vector2(410f, 400f), new Vector2(760f, 760f), Guide, 25, UiTheme.Text, TextAnchor.MiddleLeft);
            _back = UiKit.Button(Root, "Atras", new Vector2(220f, 60f), new Vector2(300f, 84f), "ATRÁS", 36, UiTheme.Button);
        }

        private UiButton Row(string name, int index) =>
            UiKit.Button(Root, name, new Vector2(RowX, RowTop - index * RowStep), new Vector2(880f, 80f), string.Empty, 30, UiTheme.Button);

        /// <inheritdoc />
        public override void Show()
        {
            BackRequested = false;
            base.Show();
            Refresh();
        }

        private void Refresh()
        {
            _speed.Label.text = "VELOCIDAD DEL JUEGO: " + GameSettings.SpeedLabels[_settings.Speed];
            _through.Label.text = "BOTÓN FILTRADO: " + (_settings.ShowThrough ? "SÍ" : "NO");
            _cross.Label.text = "BOTÓN CENTRO: " + (_settings.ShowCross ? "SÍ" : "NO");
            _big.Label.text = "TAMAÑO DE LOS BOTONES: " + (_settings.BigButtons ? "GRANDE" : "NORMAL");
            _lefty.Label.text = "CONTROLES PARA ZURDOS: " + (_settings.LeftHanded ? "SÍ" : "NO");
            _names.Label.text = "NOMBRES: " + MatchSetupScreen.NameModeLabel(_settings.Names);
            _fps.Label.text = "FPS OBJETIVO: " + _settings.TargetFps;
            _debug.Label.text = "PANEL DE DEPURACIÓN: " + (_settings.ShowDebug ? "SÍ" : "NO");
        }

        /// <summary>Procesa los toques del fotograma.</summary>
        public void Update()
        {
            BackRequested = BackKey;
            int n = Taps(out Vector2[] taps);
            for (int t = 0; t < n; t++)
            {
                Vector2 p = taps[t];
                if (_speed.Contains(p))
                {
                    _settings.Speed = (_settings.Speed + 1) % GameSettings.Speeds.Length;
                }
                else if (_through.Contains(p))
                {
                    _settings.ShowThrough = !_settings.ShowThrough;
                }
                else if (_cross.Contains(p))
                {
                    _settings.ShowCross = !_settings.ShowCross;
                }
                else if (_big.Contains(p))
                {
                    _settings.BigButtons = !_settings.BigButtons;
                }
                else if (_lefty.Contains(p))
                {
                    _settings.LeftHanded = !_settings.LeftHanded;
                }
                else if (_names.Contains(p))
                {
                    _settings.Names = (NameMode)(((int)_settings.Names + 1) % 3);
                }
                else if (_fps.Contains(p))
                {
                    _settings.TargetFps = _settings.TargetFps == 30 ? 60 : 30;
                    Application.targetFrameRate = _settings.TargetFps;
                }
                else if (_debug.Contains(p))
                {
                    _settings.ShowDebug = !_settings.ShowDebug;
                }
                else if (_back.Contains(p))
                {
                    BackRequested = true;
                }

                _settings.Save();
                Refresh();
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
            Image panel = UiKit.Panel(Root, "Estadisticas", new Vector2(960f, 480f), new Vector2(1300f, 520f), UiTheme.Panel);
            _body = UiKit.Label(panel.transform, "Texto", new Vector2(650f, 260f), new Vector2(1240f, 500f), string.Empty, 34, UiTheme.Text);
            _again = UiKit.Button(Root, "Otra", new Vector2(640f, 110f), new Vector2(520f, 110f), "REVANCHA", 44, UiTheme.AccentDark);
            _menu = UiKit.Button(Root, "Menu", new Vector2(1280f, 110f), new Vector2(520f, 110f), "MENÚ PRINCIPAL", 44, UiTheme.Button);
        }

        /// <summary>Muestra el resultado.</summary>
        public void Show(string result, string summary)
        {
            AgainRequested = false;
            MenuRequested = false;
            _score.text = result;
            _body.text = summary;
            Show();
        }

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
