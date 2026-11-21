using UnityEngine;
using UnityEngine.UI;

namespace Football.Game
{
    /// <summary>Opciones del menú principal.</summary>
    public enum MainMenuChoice
    {
        /// <summary>Nada elegido todavía.</summary>
        None,
        /// <summary>Partido amistoso.</summary>
        Friendly,
        /// <summary>3 contra 3 en cancha cerrada.</summary>
        Indoor,
        /// <summary>Ajustes.</summary>
        Settings,
        /// <summary>Prueba de rendimiento.</summary>
        Benchmark
    }

    /// <summary>Menú principal: modos de juego a la izquierda y una tarjeta con la descripción del modo elegido.</summary>
    public sealed class MainMenuScreen : MenuScreenBase
    {
        private struct Entry
        {
            public string Label;
            public string Title;
            public string Description;
            public MainMenuChoice Choice;
            public bool Available;
        }

        private static readonly Entry[] Entries =
        {
            new Entry { Label = "PARTIDO AMISTOSO", Title = "Partido amistoso", Choice = MainMenuChoice.Friendly, Available = true,
                Description = "Elige dos clubes, sus camisetas y tu alineación,\ny juega un partido suelto contra la máquina." },
            new Entry { Label = "3 CONTRA 3", Title = "3 contra 3 · cancha cerrada", Choice = MainMenuChoice.Indoor, Available = true,
                Description = "Cancha de 40 × 20 con paredes: el balón rebota y\nnunca sale. Arquero y dos jugadores por equipo." },
            new Entry { Label = "MODO CARRERA", Title = "Modo carrera", Choice = MainMenuChoice.None, Available = false,
                Description = "Dirige un club temporada tras temporada: fichajes,\nfinanzas, estadio, cantera y directiva." },
            new Entry { Label = "TORNEO", Title = "Torneo", Choice = MainMenuChoice.None, Available = false,
                Description = "Liga o copa con los clubes que elijas,\ncon tabla de posiciones y eliminatorias." },
            new Entry { Label = "AJUSTES", Title = "Ajustes", Choice = MainMenuChoice.Settings, Available = true,
                Description = "Controles para zurdos, nombres sobre los jugadores,\nFPS y panel de depuración." },
            new Entry { Label = "PRUEBA DE RENDIMIENTO", Title = "Prueba de rendimiento", Choice = MainMenuChoice.Benchmark, Available = true,
                Description = "Partido de 5 minutos IA contra IA con condiciones fijas\npara medir FPS y memoria en tu teléfono." }
        };

        private readonly UiButton[] _items = new UiButton[Entries.Length];
        private readonly Image[] _markers = new Image[Entries.Length];
        private readonly Text _cardTitle;
        private readonly Text _cardText;
        private readonly UiButton _action;
        private readonly Text _error;
        private int _selected;

        /// <summary>Opción elegida en este fotograma.</summary>
        public MainMenuChoice Choice { get; private set; }

        /// <summary>Crea el menú.</summary>
        public MainMenuScreen(Transform parent, PointerInput pointers) : base(parent, pointers, "MenuPrincipalUI")
        {
            UiKit.Label(Root, "Logo", new Vector2(420f, 960f), new Vector2(700f, 110f), "FÚTBOL", 96, UiTheme.Accent, TextAnchor.MiddleLeft);
            UiKit.Label(Root, "Lema", new Vector2(420f, 890f), new Vector2(700f, 40f), "Gestión y partidos · Liga argentina", 26, UiTheme.TextDim, TextAnchor.MiddleLeft);

            for (int i = 0; i < Entries.Length; i++)
            {
                float y = 760f - i * 92f;
                _markers[i] = UiKit.Panel(Root, "Marca" + i, new Vector2(80f, y), new Vector2(8f, 56f), UiTheme.Accent);
                _items[i] = UiTheme.MenuItem(Root, "Opcion" + i, new Vector2(400f, y), new Vector2(600f, 76f), Entries[i].Label, 40);
                _items[i].Label.color = Entries[i].Available ? UiTheme.Text : UiTheme.TextMuted;
            }

            Image card = UiKit.Panel(Root, "Tarjeta", new Vector2(1290f, 520f), new Vector2(1060f, 640f), UiTheme.Panel);
            _cardTitle = UiKit.Label(card.transform, "Titulo", new Vector2(530f, 520f), new Vector2(980f, 90f), string.Empty, 64, UiTheme.Text);
            _cardText = UiKit.Label(card.transform, "Texto", new Vector2(530f, 360f), new Vector2(980f, 200f), string.Empty, 32, UiTheme.TextDim);
            _action = UiKit.Button(card.transform, "Accion", new Vector2(530f, 140f), new Vector2(460f, 110f), string.Empty, 46, UiTheme.AccentDark);
            // El botón está dentro de la tarjeta: su zona táctil se expresa en coordenadas del lienzo.
            _action.Center = new Vector2(1290f, 340f);
            UiKit.Label(Root, "Version", new Vector2(960f, 40f), new Vector2(1800f, 40f), "Versión de prueba · Fase 1 · Datos de planteles de noviembre de 2026 (editables)", 22, UiTheme.TextMuted);
            _error = UiKit.Label(Root, "Error", new Vector2(1290f, 150f), new Vector2(1060f, 90f), string.Empty, 24, UiTheme.Warning);
            Select(0);
        }

        /// <summary>Muestra un aviso de error bajo la tarjeta (null para quitarlo).</summary>
        public void SetError(string message)
        {
            _error.text = message ?? string.Empty;
        }

        /// <inheritdoc />
        public override void Show()
        {
            Choice = MainMenuChoice.None;
            base.Show();
        }

        private void Select(int index)
        {
            _selected = index;
            Entry e = Entries[index];
            for (int i = 0; i < _items.Length; i++)
            {
                _markers[i].gameObject.SetActive(i == index);
                _items[i].Label.fontSize = i == index ? 46 : 40;
            }

            _cardTitle.text = e.Title;
            _cardText.text = e.Description;
            bool play = e.Choice == MainMenuChoice.Friendly || e.Choice == MainMenuChoice.Indoor;
            _action.Label.text = e.Available ? (play ? "JUGAR" : "ENTRAR") : "PRÓXIMAMENTE";
            _action.Background.color = e.Available ? UiTheme.AccentDark : UiTheme.Button;
        }

        /// <summary>Procesa los toques del fotograma.</summary>
        public void Update()
        {
            Choice = MainMenuChoice.None;
            int n = Taps(out Vector2[] taps);
            for (int t = 0; t < n; t++)
            {
                for (int i = 0; i < _items.Length; i++)
                {
                    if (_items[i].Contains(taps[t]))
                    {
                        if (i == _selected && Entries[i].Available)
                        {
                            Choice = Entries[i].Choice; // tocar de nuevo la opción elegida la abre
                        }

                        Select(i);
                    }
                }

                if (_action.Contains(taps[t]) && Entries[_selected].Available)
                {
                    Choice = Entries[_selected].Choice;
                }
            }
        }
    }
}
