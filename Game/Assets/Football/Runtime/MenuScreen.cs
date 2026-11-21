using UnityEngine;
using UnityEngine.UI;

namespace Football.Game
{
    /// <summary>Menú principal: elegir tu equipo, el rival, la camiseta de cada uno, la dificultad y la duración.</summary>
    public sealed class MenuScreen
    {
        private static readonly Color Idle = new Color(0.22f, 0.25f, 0.32f);
        private static readonly Color Selected = new Color(0.15f, 0.6f, 0.3f);
        private static readonly Color Disabled = new Color(0.15f, 0.15f, 0.18f);
        private static readonly Color Gold = new Color(1f, 0.9f, 0.4f);

        private const float HomeColumn = 330f;
        private const float AwayColumn = 960f;
        private const float OptionsColumn = 1590f;
        private const float ListTop = 830f;
        private const float RowStep = 92f;
        private const float KitRowY = 215f;

        private readonly PointerInput _pointers;
        private readonly GameSettings _settings;
        private readonly Canvas _canvas;
        private readonly UiButton[] _homeTeams;
        private readonly UiButton[] _awayTeams;
        private readonly UiButton _homeKit;
        private readonly UiButton _awayKit;
        private readonly Image _homeSwatch, _homeSwatchDetail, _awaySwatch, _awaySwatchDetail;
        private readonly UiButton[] _difficulties;
        private readonly UiButton[] _lengths;
        private readonly UiButton _play;
        private readonly UiButton _benchmark;
        private readonly Text _clash;
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
            UiKit.Label(c, "Titulo", new Vector2(960f, 1010f), new Vector2(900f, 100f), "FÚTBOL", 84, Color.white);
            UiKit.Label(c, "TuEquipo", new Vector2(HomeColumn, 915f), new Vector2(560f, 60f), "TU EQUIPO", 38, Gold);
            UiKit.Label(c, "Rival", new Vector2(AwayColumn, 915f), new Vector2(560f, 60f), "RIVAL", 38, Gold);
            UiKit.Label(c, "Dificultad", new Vector2(OptionsColumn, 915f), new Vector2(460f, 60f), "DIFICULTAD", 38, Gold);
            UiKit.Label(c, "Duracion", new Vector2(OptionsColumn, 445f), new Vector2(460f, 50f), "MINUTOS POR TIEMPO", 30, Gold);

            int count = TeamCatalog.Teams.Length;
            _homeTeams = new UiButton[count];
            _awayTeams = new UiButton[count];
            for (int i = 0; i < count; i++)
            {
                string text = TeamCatalog.Teams[i].Name + "  ·  " + TeamCatalog.Rating(i).ToString("F0") + "\n"
                              + TeamCatalog.StyleLabel(TeamCatalog.Teams[i].Style) + " · " + TeamCatalog.Teams[i].Formation;
                float y = ListTop - i * RowStep;
                _homeTeams[i] = UiKit.Button(c, "Equipo" + i, new Vector2(HomeColumn, y), new Vector2(560f, 84f), text, 26, Idle);
                _awayTeams[i] = UiKit.Button(c, "Rival" + i, new Vector2(AwayColumn, y), new Vector2(560f, 84f), text, 26, Idle);
            }

            _homeKit = UiKit.Button(c, "CamisetaPropia", new Vector2(HomeColumn - 50f, KitRowY), new Vector2(460f, 78f), string.Empty, 28, Idle);
            _awayKit = UiKit.Button(c, "CamisetaRival", new Vector2(AwayColumn - 50f, KitRowY), new Vector2(460f, 78f), string.Empty, 28, Idle);
            _homeSwatch = UiKit.Panel(c, "MuestraPropia", new Vector2(HomeColumn + 230f, KitRowY), new Vector2(70f, 78f), Color.white);
            _homeSwatchDetail = UiKit.Panel(c, "DetallePropia", new Vector2(HomeColumn + 230f, KitRowY), new Vector2(18f, 78f), Color.white);
            _awaySwatch = UiKit.Panel(c, "MuestraRival", new Vector2(AwayColumn + 230f, KitRowY), new Vector2(70f, 78f), Color.white);
            _awaySwatchDetail = UiKit.Panel(c, "DetalleRival", new Vector2(AwayColumn + 230f, KitRowY), new Vector2(18f, 78f), Color.white);
            _clash = UiKit.Label(c, "Choque", new Vector2(645f, 150f), new Vector2(1200f, 40f), string.Empty, 24, new Color(1f, 0.6f, 0.3f));

            _difficulties = new UiButton[TeamCatalog.DifficultyLabels.Length];
            for (int i = 0; i < _difficulties.Length; i++)
            {
                _difficulties[i] = UiKit.Button(c, "Dif" + i, new Vector2(OptionsColumn, 835f - i * 76f), new Vector2(440f, 64f),
                    TeamCatalog.DifficultyLabels[i].ToUpperInvariant(), 30, Idle);
            }

            _lengths = new UiButton[TeamCatalog.HalfMinutes.Length];
            for (int i = 0; i < _lengths.Length; i++)
            {
                _lengths[i] = UiKit.Button(c, "Dur" + i, new Vector2(OptionsColumn - 150f + i * 150f, 375f), new Vector2(130f, 72f),
                    TeamCatalog.HalfMinutes[i].ToString(), 34, Idle);
            }

            _play = UiKit.Button(c, "Jugar", new Vector2(OptionsColumn, 215f), new Vector2(440f, 120f), "JUGAR", 60, new Color(0.15f, 0.65f, 0.25f));
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
            SetError(TeamCatalog.LoadError != null ? "No se pudieron leer los equipos (" + TeamCatalog.LoadError + "); se usan equipos de prueba." : null);
            _canvas.gameObject.SetActive(true);
            Refresh();
        }

        /// <summary>Oculta el menú.</summary>
        public void Hide()
        {
            _canvas.gameObject.SetActive(false);
        }

        /// <summary>Equipación elegida para un equipo (0 titular, 1 alternativa).</summary>
        public static KitColors Kit(int team, int which) =>
            KitColors.From(which == 0 ? TeamCatalog.Teams[team].Home : TeamCatalog.Teams[team].Away);

        /// <summary>Las camisetas elegidas se parecen demasiado.</summary>
        public bool KitsClash => Kit(_settings.HomeTeam, _settings.HomeKit).Distance(Kit(_settings.Opponent, _settings.AwayKit)) < TeamCatalog.KitClashDistance;

        private void Refresh()
        {
            for (int i = 0; i < _homeTeams.Length; i++)
            {
                _homeTeams[i].Background.color = i == _settings.HomeTeam ? Selected : Idle;
                _awayTeams[i].Background.color = i == _settings.HomeTeam ? Disabled : (i == _settings.Opponent ? Selected : Idle);
            }

            for (int i = 0; i < _difficulties.Length; i++)
            {
                _difficulties[i].Background.color = i == _settings.Difficulty ? Selected : Idle;
            }

            for (int i = 0; i < _lengths.Length; i++)
            {
                _lengths[i].Background.color = i == _settings.Length ? Selected : Idle;
            }

            ShowKit(_homeKit, _homeSwatch, _homeSwatchDetail, _settings.HomeTeam, _settings.HomeKit);
            ShowKit(_awayKit, _awaySwatch, _awaySwatchDetail, _settings.Opponent, _settings.AwayKit);
            _clash.text = KitsClash ? "Las camisetas se parecen: cambia una para distinguir a los equipos." : string.Empty;
        }

        private static void ShowKit(UiButton button, Image swatch, Image detail, int team, int which)
        {
            KitColors kit = Kit(team, which);
            button.Label.text = which == 0 ? "CAMISETA: TITULAR" : "CAMISETA: ALTERNATIVA";
            swatch.color = kit.Shirt;
            detail.color = kit.Pattern == KitPattern.Solid ? kit.Shirt : kit.Secondary;
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

                for (int i = 0; i < _homeTeams.Length; i++)
                {
                    if (_homeTeams[i].Contains(point))
                    {
                        _settings.HomeTeam = i;
                        if (_settings.Opponent == i)
                        {
                            _settings.Opponent = (i + 1) % _homeTeams.Length;
                        }
                    }

                    if (_awayTeams[i].Contains(point) && i != _settings.HomeTeam)
                    {
                        _settings.Opponent = i;
                    }
                }

                if (_homeKit.Contains(point))
                {
                    _settings.HomeKit = 1 - _settings.HomeKit;
                }

                if (_awayKit.Contains(point))
                {
                    _settings.AwayKit = 1 - _settings.AwayKit;
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
