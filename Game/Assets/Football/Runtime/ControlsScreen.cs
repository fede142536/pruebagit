using UnityEngine;
using UnityEngine.UI;

namespace Football.Game
{
    /// <summary>
    /// Ajustes → Controles de teclado: cada acción muestra su tecla; al tocarla, la siguiente tecla que se pulse queda asignada
    /// (si ya la usaba otra acción, se intercambian). Esc cancela la espera.
    /// </summary>
    public sealed class ControlsScreen : MenuScreenBase
    {
        private const float ColumnLeft = 520f;
        private const float ColumnRight = 1400f;
        private const float RowTop = 830f;
        private const float RowStep = 88f;
        private const int RowsPerColumn = 8;

        private readonly UiButton[] _rows = new UiButton[KeyBindings.Count];
        private readonly UiButton _reset, _back;
        private readonly Text _help;
        private int _waiting = -1;

        /// <summary>Se pidió volver a Ajustes.</summary>
        public bool BackRequested { get; private set; }

        /// <summary>Crea la pantalla.</summary>
        public ControlsScreen(Transform parent, PointerInput pointers) : base(parent, pointers, "ControlesUI")
        {
            Title("CONTROLES DE TECLADO", "Toca una acción y pulsa la tecla que quieres usar");
            for (int i = 0; i < _rows.Length; i++)
            {
                float x = i < RowsPerColumn ? ColumnLeft : ColumnRight;
                float y = RowTop - (i % RowsPerColumn) * RowStep;
                _rows[i] = UiKit.Button(Root, "Accion" + i, new Vector2(x, y), new Vector2(820f, 76f), string.Empty, 30, UiTheme.Button);
            }

            _help = UiKit.Label(Root, "Ayuda", new Vector2(960f, 150f), new Vector2(1700f, 50f), string.Empty, 28, UiTheme.Warning);
            _reset = UiKit.Button(Root, "Restablecer", new Vector2(1500f, 60f), new Vector2(420f, 84f), "RESTABLECER", 32, UiTheme.Button);
            _back = UiKit.Button(Root, "Atras", new Vector2(220f, 60f), new Vector2(300f, 84f), "ATRÁS", 36, UiTheme.Button);
        }

        /// <inheritdoc />
        public override void Show()
        {
            BackRequested = false;
            _waiting = -1;
            base.Show();
            Refresh();
        }

        private void Refresh()
        {
            for (int i = 0; i < _rows.Length; i++)
            {
                string key = i == _waiting ? "PULSA UNA TECLA…" : KeyBindings.KeyName(KeyBindings.Get((GameAction)i));
                _rows[i].Label.text = KeyBindings.Labels[i] + ":  " + key;
                _rows[i].Background.color = i == _waiting ? UiTheme.Selected : UiTheme.Button;
            }

            _help.text = _waiting >= 0 ? "Pulsa la tecla para " + KeyBindings.Labels[_waiting] + " (Esc cancela)" : string.Empty;
        }

        /// <summary>Procesa toques y teclas del fotograma.</summary>
        public void Update()
        {
            if (_waiting >= 0)
            {
                if (Pointers.TryGetPressedKey(out KeyCode key))
                {
                    if (key != KeyCode.Escape || (GameAction)_waiting == GameAction.Pause)
                    {
                        KeyBindings.Set((GameAction)_waiting, key);
                    }

                    _waiting = -1;
                    Refresh();
                }

                return;
            }

            BackRequested = BackKey;
            int n = Taps(out Vector2[] taps);
            for (int t = 0; t < n; t++)
            {
                Vector2 p = taps[t];
                if (_back.Contains(p))
                {
                    BackRequested = true;
                }
                else if (_reset.Contains(p))
                {
                    KeyBindings.ResetAll();
                }
                else
                {
                    for (int i = 0; i < _rows.Length; i++)
                    {
                        if (_rows[i].Contains(p))
                        {
                            _waiting = i;
                        }
                    }
                }

                Refresh();
            }
        }
    }
}
