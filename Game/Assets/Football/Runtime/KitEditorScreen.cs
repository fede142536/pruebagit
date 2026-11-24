using Football.Core.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Football.Game
{
    /// <summary>
    /// Editor de la camiseta CASA de un club (estilo del manual): a la izquierda el jugador 3D con la equipación en vivo; a la derecha el selector segmentado de patrón (los seis del
    /// manual) y una fila de muestras cuadradas por prenda (camiseta, secundario, short y medias). ATRÁS descarta, RESTAURAR vuelve a la original del archivo de equipos y GUARDAR
    /// guarda la equipación (<see cref="KitStore"/>) y la deja aplicada a todos los modos. Sin lógica de juego.
    /// </summary>
    public sealed class KitEditorScreen : MenuScreenBase
    {
        private const int PatternColumns = 3;
        private const float PatternTop = 800f, PatternRowStep = 82f;
        private const float SwatchSize = 52f, SwatchStep = 58f, SwatchLeft = 1085f, SwatchBorder = 8f;
        private const float RowTop = 600f, RowStep = 110f;
        private const int Rows = 4;
        private const int ShowroomIndex = 7;

        private readonly KitShowroom _showroom;
        private readonly UiButton[] _patterns = new UiButton[6];
        private readonly UiButton[,] _swatches = new UiButton[Rows, 14];
        private readonly Image[] _marks = new Image[Rows];
        private readonly UiButton _back, _save, _reset;
        private readonly Text _team;
        private TeamDefinition _target;
        private KitData _kit = KitData.Default();

        /// <summary>La pantalla pidió volver (guardando o no).</summary>
        public bool Done { get; private set; }

        /// <summary>Crea la pantalla (oculta).</summary>
        public KitEditorScreen(Transform parent, PointerInput pointers) : base(parent, pointers, "EditorEquipacionUI")
        {
            Title(Locale.Get("kit.title"), Locale.Get("kit.subtitle"));
            _team = UiKit.Label(Root, "Club", new Vector2(1390f, 940f), new Vector2(900f, 50f), string.Empty, 34, UiTheme.Text, TextAnchor.MiddleRight);
            UiTheme.Frame(Root, "MarcoJugador", new Vector2(480f, 520f), new Vector2(620f, 780f));
            _showroom = new KitShowroom(Root, new Vector2(480f, 520f), new Vector2(460f, 740f), ShowroomIndex, 160f);

            UiTheme.Frame(Root, "MarcoPatron", new Vector2(1390f, 740f), new Vector2(940f, 200f));
            UiKit.Label(Root, "TituloPatron", new Vector2(1390f, 830f), new Vector2(900f, 34f), Locale.Get("kit.pattern"), 24, UiTheme.TextDim);
            for (int i = 0; i < _patterns.Length; i++)
            {
                var center = new Vector2(1130f + (i % PatternColumns) * 300f, PatternTop - 50f - (i / PatternColumns) * (PatternRowStep - 18f));
                _patterns[i] = UiKit.Button(Root, "Patron" + i, center, new Vector2(280f, 60f), Locale.Get("kit.pattern." + i), 22, UiTheme.Button);
            }

            UiTheme.Frame(Root, "MarcoColores", new Vector2(1390f, 360f), new Vector2(940f, 470f));
            string[] rowKeys = { "kit.row.shirt", "kit.row.secondary", "kit.row.shorts", "kit.row.socks" };
            for (int r = 0; r < Rows; r++)
            {
                float y = RowTop - r * RowStep + 30f;
                UiKit.Label(Root, "TituloFila" + r, new Vector2(1390f, y + 40f), new Vector2(900f, 30f), Locale.Get(rowKeys[r]), 22, UiTheme.TextDim);
                _marks[r] = UiKit.Panel(Root, "Marca" + r, Vector2.zero, new Vector2(SwatchSize + SwatchBorder, SwatchSize + SwatchBorder), UiTheme.Accent);
                for (int c = 0; c < KitPalette.Swatches.Length; c++)
                {
                    ColorUtility.TryParseHtmlString(KitPalette.Swatches[c], out Color color);
                    _swatches[r, c] = UiKit.Button(Root, "Muestra" + r + "_" + c, new Vector2(SwatchLeft + c * SwatchStep, y - 10f), new Vector2(SwatchSize, SwatchSize), string.Empty, 10, color);
                }
            }

            _back = UiKit.Button(Root, "Atras", new Vector2(220f, 60f), new Vector2(300f, 84f), Locale.Get("kit.back"), 36, UiTheme.Button);
            _reset = UiKit.Button(Root, "Restaurar", new Vector2(960f, 60f), new Vector2(420f, 84f), Locale.Get("kit.reset"), 30, UiTheme.Button);
            _save = UiKit.Button(Root, "Guardar", new Vector2(1680f, 60f), new Vector2(400f, 84f), Locale.Get("kit.save"), 38, UiTheme.AccentDark);
        }

        /// <summary>Abre el editor con la camiseta CASA de un club.</summary>
        /// <param name="team">Club a editar.</param>
        public void Show(TeamDefinition team)
        {
            _target = team;
            _kit = KitData.FromDefinition(team.Home);
            Done = false;
            base.Show();
            _showroom.SetVisible(true);
            Refresh();
        }

        /// <inheritdoc />
        public override void Hide()
        {
            base.Hide();
            _showroom?.SetVisible(false);
        }

        private string[] Colors => new[] { _kit.primaryColorHex, _kit.secondaryColorHex, _kit.shortsColorHex, _kit.socksColorHex };

        private void SetColor(int row, string hex)
        {
            switch (row)
            {
                case 0: _kit.primaryColorHex = hex; break;
                case 1: _kit.secondaryColorHex = hex; _kit.trimColorHex = hex; break;
                case 2: _kit.shortsColorHex = hex; break;
                default: _kit.socksColorHex = hex; break;
            }
        }

        private void Refresh()
        {
            _team.text = _target != null ? TeamCatalog.ScoreboardName(_target) : string.Empty;
            int pattern = (int)_kit.Pattern;
            for (int i = 0; i < _patterns.Length; i++)
            {
                bool on = i == pattern;
                _patterns[i].Background.color = on ? UiTheme.Selected : UiTheme.Button;
                _patterns[i].Label.color = on ? UiTheme.AccentText : UiTheme.Text;
            }

            string[] current = Colors;
            for (int r = 0; r < Rows; r++)
            {
                int match = System.Array.FindIndex(KitPalette.Swatches, s => string.Equals(s, current[r], System.StringComparison.OrdinalIgnoreCase));
                _marks[r].gameObject.SetActive(match >= 0);
                if (match >= 0)
                {
                    ((RectTransform)_marks[r].transform).anchoredPosition = _swatches[r, match].Center;
                }
            }

            _showroom.SetKit(KitColors.From(_kit.ToDefinition()));
        }

        /// <summary>Procesa los toques y gira la vitrina; llamar una vez por fotograma.</summary>
        public void Update()
        {
            Done = false;
            _showroom.Update(Time.unscaledDeltaTime);
            if (BackKey)
            {
                Done = true;
                return;
            }

            int n = Taps(out Vector2[] taps);
            for (int t = 0; t < n; t++)
            {
                Vector2 p = taps[t];
                if (_back.Contains(p)) { Done = true; return; }
                if (_save.Contains(p))
                {
                    KitStore.Save(_target, _kit.ToDefinition());
                    Done = true;
                    return;
                }

                if (_reset.Contains(p))
                {
                    KitStore.Reset(_target);
                    _kit = KitData.FromDefinition(_target.Home);
                }

                for (int i = 0; i < _patterns.Length; i++)
                {
                    if (_patterns[i].Contains(p)) { _kit.patternId = KitPatterns.Names[i]; }
                }

                for (int r = 0; r < Rows; r++)
                {
                    for (int c = 0; c < KitPalette.Swatches.Length; c++)
                    {
                        if (_swatches[r, c].Contains(p)) { SetColor(r, KitPalette.Swatches[c]); }
                    }
                }

                Refresh();
            }
        }
    }
}
