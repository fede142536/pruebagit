using Football.Core.Data;
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
        /// <summary>Modo carrera.</summary>
        Career,
        /// <summary>Torneo.</summary>
        Tournament,
        /// <summary>Prueba de rendimiento.</summary>
        Benchmark
    }

    /// <summary>Menú principal: modos de juego a la izquierda y una tarjeta con la descripción del modo elegido.</summary>
    public sealed class MainMenuScreen : MenuScreenBase
    {
        private struct Entry
        {
            public string Key; // prefijo de los textos en Data/locale/ui.*.json
            public MainMenuChoice Choice;
            public bool Available;
        }

        private static readonly Entry[] Entries =
        {
            new Entry { Key = "menu.friendly", Choice = MainMenuChoice.Friendly, Available = true },
            new Entry { Key = "menu.potrero", Choice = MainMenuChoice.Indoor, Available = true },
            new Entry { Key = "menu.career", Choice = MainMenuChoice.Career, Available = true },
            new Entry { Key = "menu.tournament", Choice = MainMenuChoice.Tournament, Available = true },
            new Entry { Key = "menu.settings", Choice = MainMenuChoice.Settings, Available = true },
            new Entry { Key = "menu.benchmark", Choice = MainMenuChoice.Benchmark, Available = true }
        };

        private const int ClubCards = 3;
        private const float CardScale = 0.42f;
        private readonly KitPreview[] _cardKits = new KitPreview[ClubCards];
        private readonly Text[] _cardNames = new Text[ClubCards];
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
            UiKit.Label(Root, "Logo", new Vector2(420f, 960f), new Vector2(700f, 110f), Locale.Get("app.title"), 104, UiTheme.Accent, TextAnchor.MiddleLeft);
            UiKit.Label(Root, "Lema", new Vector2(420f, 890f), new Vector2(700f, 40f), Locale.Get("app.tagline"), 26, UiTheme.TextDim, TextAnchor.MiddleLeft);

            for (int i = 0; i < Entries.Length; i++)
            {
                float y = 760f - i * 92f;
                _markers[i] = UiKit.Panel(Root, "Marca" + i, new Vector2(80f, y), new Vector2(8f, 56f), UiTheme.Accent);
                _items[i] = UiTheme.MenuItem(Root, "Opcion" + i, new Vector2(400f, y), new Vector2(600f, 76f), Locale.Get(Entries[i].Key), 40);
                _items[i].Label.color = Entries[i].Available ? UiTheme.Text : UiTheme.TextMuted;
            }

            // Tarjetas de los clubes que visten las figuras de la portada (camiseta dibujada, sin escudos oficiales).
            for (int i = 0; i < ClubCards; i++)
            {
                var center = new Vector2(1240f + i * 260f, 950f);
                UiKit.Panel(Root, "TarjetaClub" + i, center, new Vector2(240f, 150f), UiTheme.Panel);
                var holder = new GameObject("Camiseta" + i, typeof(RectTransform));
                holder.transform.SetParent(Root, false);
                var rect = (RectTransform)holder.transform;
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.anchoredPosition = center + new Vector2(-70f, 8f);
                rect.localScale = new Vector3(CardScale, CardScale, 1f);
                _cardKits[i] = new KitPreview(holder.transform, Vector2.zero);
                _cardNames[i] = UiKit.Label(Root, "NombreClub" + i, center + new Vector2(40f, 0f), new Vector2(150f, 120f), string.Empty, 22, UiTheme.Text);
            }

            UiKit.Label(Root, "Favorito", new Vector2(1500f, 1045f), new Vector2(250f, 30f), Locale.Get("title.favorite"), 20, UiTheme.Accent);

            // La tarjeta va a la derecha para dejar a la vista las figuras de la portada.
            Image card = UiKit.Panel(Root, "Tarjeta", new Vector2(1530f, 430f), new Vector2(680f, 560f), UiTheme.Panel);
            _cardTitle = UiKit.Label(card.transform, "Titulo", new Vector2(340f, 470f), new Vector2(640f, 90f), string.Empty, 52, UiTheme.Text);
            _cardText = UiKit.Label(card.transform, "Texto", new Vector2(340f, 320f), new Vector2(640f, 200f), string.Empty, 27, UiTheme.TextDim);
            _action = UiKit.Button(card.transform, "Accion", new Vector2(340f, 110f), new Vector2(420f, 100f), string.Empty, 46, UiTheme.AccentDark);
            // El botón está dentro de la tarjeta: su zona táctil se expresa en coordenadas del lienzo.
            _action.Center = new Vector2(1530f, 260f);
            UiKit.Label(Root, "Version", new Vector2(960f, 40f), new Vector2(1800f, 40f), Locale.Get("app.version"), 22, UiTheme.TextMuted);
            _error = UiKit.Label(Root, "Error", new Vector2(1530f, 100f), new Vector2(680f, 80f), string.Empty, 24, UiTheme.Warning);
            Select(0);
        }

        /// <summary>Muestra en las tarjetas los clubes que visten las figuras de la portada (izquierda, favorito, derecha).</summary>
        /// <param name="clubs">Índices de <see cref="TeamCatalog.Teams"/>.</param>
        public void ShowClubs(int[] clubs)
        {
            TeamDefinition[] teams = TeamCatalog.Teams;
            for (int i = 0; i < ClubCards && i < clubs.Length; i++)
            {
                TeamDefinition team = teams[clubs[i]];
                _cardKits[i].Set(KitColors.From(team.Home));
                _cardNames[i].text = string.IsNullOrEmpty(team.ShortName) ? team.Name : team.ShortName;
            }
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

            _cardTitle.text = Locale.Get(e.Key + ".title");
            _cardText.text = Locale.Get(e.Key + ".desc");
            bool play = e.Choice == MainMenuChoice.Friendly || e.Choice == MainMenuChoice.Indoor || e.Choice == MainMenuChoice.Tournament;
            _action.Label.text = Locale.Get(e.Available ? (play ? "menu.play" : "menu.enter") : "menu.soon");
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
