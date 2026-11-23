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

    /// <summary>
    /// Menú principal (diseño «Noche + verde cancha»): carrusel de modos con la tarjeta elegida grande y las demás chicas, puntos de posición,
    /// botón de acción abajo a la derecha y los clubes de la portada arriba a la derecha. Se desliza con el dedo, tocar una tarjeta la elige y tocar
    /// la elegida (o el botón) la abre.
    /// </summary>
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

        // Paleta «Noche + verde cancha» del diseño del menú.
        private static readonly Color Bg = new Color(0.059f, 0.102f, 0.090f);
        private static readonly Color Ink = new Color(0.910f, 0.937f, 0.914f);
        private static readonly Color Accent = new Color(0.247f, 0.714f, 0.478f);
        private static readonly Color AccentSoft = new Color(0.102f, 0.227f, 0.176f);
        private static readonly Color AccentText = new Color(0.498f, 0.863f, 0.667f);
        private static readonly Color AccentStrong = new Color(0.663f, 0.925f, 0.780f);
        private static readonly Color Line = new Color(0.239f, 0.318f, 0.282f);
        private static readonly Color Dim = new Color(0.616f, 0.702f, 0.659f);
        private static readonly Color ButtonText = new Color(0.047f, 0.102f, 0.078f);

        private const int ClubTags = 3;
        private const float CardY = 540f, BigW = 640f, BigH = 460f, SmallW = 330f, SmallH = 330f, Gap = 30f, Border = 3f, MarginX = 90f, Pad = 30f;
        private const float ScrollSpeed = 9f, SwipeDistance = 60f, DotsY = 130f;
        private readonly Image[] _tagBack = new Image[ClubTags];
        private readonly Text[] _tagText = new Text[ClubTags];
        private readonly Image[] _edge = new Image[Entries.Length], _fill = new Image[Entries.Length], _dot = new Image[Entries.Length];
        private readonly Text[] _number = new Text[Entries.Length], _title = new Text[Entries.Length], _desc = new Text[Entries.Length];
        private readonly UiButton _action;
        private readonly Text _error;
        private readonly float[] _width = new float[Entries.Length], _center = new float[Entries.Length];
        private int _selected;
        private float _scroll; // posición continua del carrusel (índice de la tarjeta centrada)
        private int _dragId = -1;
        private float _dragStartX;

        /// <summary>Opción elegida en este fotograma.</summary>
        public MainMenuChoice Choice { get; private set; }

        /// <summary>Crea el menú.</summary>
        public MainMenuScreen(Transform parent, PointerInput pointers) : base(parent, pointers, "MenuPrincipalUI")
        {
            UiKit.Panel(Root, "FondoNoche", new Vector2(960f, 540f), UiKit.FullScreenSize, Bg); // cubre toda la pantalla, sea cual sea la proporción
            DrawPitch();

            UiKit.Label(Root, "Logo", new Vector2(MarginX + 350f, 960f), new Vector2(700f, 100f), Locale.Get("app.title"), 84, AccentStrong, TextAnchor.MiddleLeft);
            UiKit.Label(Root, "Lema", new Vector2(MarginX + 380f, 890f), new Vector2(760f, 36f), Locale.Get("app.tagline"), 22, Dim, TextAnchor.MiddleLeft);

            // Clubes de la portada (arriba a la derecha).
            UiKit.Label(Root, "TuClub", new Vector2(1920f - MarginX - 3f * 126f - 130f, 960f), new Vector2(240f, 36f), Locale.Get("title.favorite"), 20, Dim, TextAnchor.MiddleRight);
            for (int i = 0; i < ClubTags; i++)
            {
                var center = new Vector2(1920f - MarginX - 60f - (ClubTags - 1 - i) * 126f, 960f);
                _tagBack[i] = UiKit.Panel(Root, "EtiquetaClub" + i, center, new Vector2(116f, 46f), i == 1 ? Accent : Line);
                _tagText[i] = UiKit.Label(Root, "TextoClub" + i, center, new Vector2(116f, 46f), string.Empty, 22, i == 1 ? ButtonText : Ink);
            }

            for (int i = 0; i < Entries.Length; i++)
            {
                _edge[i] = UiKit.Panel(Root, "BordeModo" + i, new Vector2(960f, CardY), new Vector2(SmallW, SmallH), Line);
                _fill[i] = UiKit.Panel(Root, "FondoModo" + i, new Vector2(960f, CardY), new Vector2(SmallW - 2f * Border, SmallH - 2f * Border), Bg);
                _number[i] = UiKit.Label(Root, "NumeroModo" + i, new Vector2(960f, CardY), new Vector2(120f, 30f), (i + 1).ToString("00"), 22, Dim, TextAnchor.MiddleLeft);
                _title[i] = UiKit.Label(Root, "TituloModo" + i, new Vector2(960f, CardY), new Vector2(SmallW - 2f * Pad, 120f), Locale.Get(Entries[i].Key).ToUpperInvariant(), 36, Ink,
                    TextAnchor.MiddleLeft);
                _title[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                _desc[i] = UiKit.Label(Root, "DescModo" + i, new Vector2(960f, CardY), new Vector2(BigW - 2f * Pad, 180f), Locale.Get(Entries[i].Key + ".desc"), 26, Dim,
                    TextAnchor.UpperLeft);
                _desc[i].horizontalOverflow = HorizontalWrapMode.Wrap;
                _dot[i] = UiKit.Panel(Root, "Punto" + i, new Vector2(MarginX + 10f + i * 26f, DotsY), new Vector2(10f, 6f), Line);
            }

            _action = UiKit.Button(Root, "Accion", new Vector2(1920f - MarginX - 220f, DotsY), new Vector2(440f, 96f), string.Empty, 44, Accent);
            _action.Label.color = ButtonText;
            UiKit.Label(Root, "Version", new Vector2(960f, 40f), new Vector2(1800f, 40f), Locale.Get("app.version"), 22, Dim);
            _error = UiKit.Label(Root, "Error", new Vector2(960f, 230f), new Vector2(1200f, 60f), string.Empty, 24, UiTheme.Warning);
            Select(0, true);
        }

        /// <summary>Dibuja las líneas de una cancha, tenues, de fondo (marco, línea media y áreas).</summary>
        private void DrawPitch()
        {
            var c = new Color(Accent.r, Accent.g, Accent.b, 0.20f);
            void Rect(string name, float cx, float cy, float w, float h)
            {
                const float t = 3f;
                UiKit.Panel(Root, name + "N", new Vector2(cx, cy + h / 2f), new Vector2(w, t), c);
                UiKit.Panel(Root, name + "S", new Vector2(cx, cy - h / 2f), new Vector2(w, t), c);
                UiKit.Panel(Root, name + "O", new Vector2(cx - w / 2f, cy), new Vector2(t, h), c);
                UiKit.Panel(Root, name + "E", new Vector2(cx + w / 2f, cy), new Vector2(t, h), c);
            }

            Rect("Cancha", 960f, 540f, 1920f - 2f * 40f, 1080f - 2f * 40f);
            UiKit.Panel(Root, "LineaMedia", new Vector2(960f, 540f), new Vector2(3f, 1080f - 2f * 40f), c);
            UiKit.Panel(Root, "Circulo", new Vector2(960f, 540f), new Vector2(260f, 260f), c, true);
            UiKit.Panel(Root, "CirculoCentro", new Vector2(960f, 540f), new Vector2(254f, 254f), Bg, true);
        }

        /// <summary>Muestra en las etiquetas los clubes que visten las figuras de la portada (izquierda, favorito, derecha).</summary>
        /// <param name="clubs">Índices de <see cref="TeamCatalog.Teams"/>.</param>
        public void ShowClubs(int[] clubs)
        {
            TeamDefinition[] teams = TeamCatalog.Teams;
            for (int i = 0; i < ClubTags && i < clubs.Length; i++)
            {
                TeamDefinition team = teams[clubs[i]];
                _tagText[i].text = string.IsNullOrEmpty(team.ShortName) ? team.Name : team.ShortName;
            }
        }

        /// <summary>Muestra un aviso de error bajo el carrusel (null para quitarlo).</summary>
        public void SetError(string message)
        {
            _error.text = message ?? string.Empty;
        }

        /// <inheritdoc />
        public override void Show()
        {
            Choice = MainMenuChoice.None;
            _scroll = _selected;
            Layout();
            base.Show();
        }

        private void Select(int index, bool instant = false)
        {
            _selected = Mathf.Clamp(index, 0, Entries.Length - 1);
            Entry e = Entries[_selected];
            bool play = e.Choice == MainMenuChoice.Friendly || e.Choice == MainMenuChoice.Indoor || e.Choice == MainMenuChoice.Tournament;
            _action.Label.text = Locale.Get(e.Available ? (play ? "menu.play" : "menu.enter") : "menu.soon");
            _action.Background.color = e.Available ? Accent : Line;
            _action.Label.color = e.Available ? ButtonText : Dim;
            for (int i = 0; i < _dot.Length; i++)
            {
                var rect = (RectTransform)_dot[i].transform;
                rect.sizeDelta = new Vector2(i == _selected ? 24f : 10f, 6f);
                _dot[i].color = i == _selected ? Accent : Line;
            }

            if (instant)
            {
                _scroll = _selected;
                Layout();
            }
        }

        /// <summary>Coloca las tarjetas según la posición del carrusel: la más cercana al centro crece y las demás se achican.</summary>
        private void Layout()
        {
            int n = Entries.Length;
            for (int i = 0; i < n; i++)
            {
                float near = Mathf.Clamp01(1f - Mathf.Abs(i - _scroll));
                _width[i] = Mathf.Lerp(SmallW, BigW, near);
                _center[i] = i == 0 ? 0f : _center[i - 1] + _width[i - 1] * 0.5f + Gap + _width[i] * 0.5f;
            }

            int lo = Mathf.Clamp(Mathf.FloorToInt(_scroll), 0, n - 1), hi = Mathf.Min(lo + 1, n - 1);
            float pivot = Mathf.Lerp(_center[lo], _center[hi], _scroll - lo);
            for (int i = 0; i < n; i++)
            {
                float near = Mathf.Clamp01(1f - Mathf.Abs(i - _scroll));
                float w = _width[i], h = Mathf.Lerp(SmallH, BigH, near), x = 960f + _center[i] - pivot;
                bool on = near > 0.5f;
                Place(_edge[i], new Vector2(x, CardY), new Vector2(w, h));
                Place(_fill[i], new Vector2(x, CardY), new Vector2(w - 2f * Border, h - 2f * Border));
                _edge[i].color = on ? AccentStrong : Line;
                _fill[i].color = Color.Lerp(Bg, AccentSoft, near);
                float top = CardY + h * 0.5f, bottom = CardY - h * 0.5f, left = x - w * 0.5f + Pad;
                _number[i].rectTransform.anchoredPosition = new Vector2(left + 60f, top - 34f);
                // Tarjeta chica: el título va abajo. Tarjeta grande: el título arriba y la descripción debajo, sin pisarse.
                float titleY = Mathf.Lerp(bottom + 75f, top - 125f, near);
                _title[i].rectTransform.anchoredPosition = new Vector2(x, titleY);
                _title[i].rectTransform.sizeDelta = new Vector2(w - 2f * Pad, 120f);
                _title[i].fontSize = on ? 48 : 32;
                _title[i].color = on ? AccentStrong : Ink;
                _desc[i].rectTransform.anchoredPosition = new Vector2(x, top - 290f);
                _desc[i].rectTransform.sizeDelta = new Vector2(w - 2f * Pad, 170f);
                _desc[i].gameObject.SetActive(near > 0.85f);
            }
        }

        private static void Place(Image image, Vector2 center, Vector2 size)
        {
            RectTransform rect = image.rectTransform;
            rect.anchoredPosition = center;
            rect.sizeDelta = size;
        }

        private bool OnCard(int i, Vector2 point)
        {
            Vector2 c = _edge[i].rectTransform.anchoredPosition, s = _edge[i].rectTransform.sizeDelta;
            return Mathf.Abs(point.x - c.x) <= s.x * 0.5f && Mathf.Abs(point.y - c.y) <= s.y * 0.5f;
        }

        /// <summary>Procesa los toques del fotograma y anima el carrusel.</summary>
        public void Update()
        {
            Choice = MainMenuChoice.None;
            HandleDrag();
            int n = Taps(out Vector2[] taps);
            for (int t = 0; t < n; t++)
            {
                if (_action.Contains(taps[t]) && Entries[_selected].Available)
                {
                    Choice = Entries[_selected].Choice;
                    continue;
                }

                for (int i = 0; i < Entries.Length; i++)
                {
                    if (!OnCard(i, taps[t]))
                    {
                        continue;
                    }

                    if (i == _selected && Entries[i].Available)
                    {
                        Choice = Entries[i].Choice; // tocar de nuevo la tarjeta elegida la abre
                    }

                    Select(i);
                }
            }

            _scroll = Mathf.Lerp(_scroll, _selected, 1f - Mathf.Exp(-ScrollSpeed * Time.unscaledDeltaTime));
            Layout();
        }

        /// <summary>Deslizar el dedo hacia los lados pasa a la tarjeta anterior o siguiente.</summary>
        private void HandleDrag()
        {
            for (int i = 0; i < Pointers.Count; i++)
            {
                PointerState p = Pointers.Pointers[i];
                float x = UiKit.ToArea(p.Position).x;
                if (p.Phase == PointerPhase.Began && _dragId < 0)
                {
                    _dragId = p.Id;
                    _dragStartX = x;
                }
                else if (p.Id == _dragId && p.Phase == PointerPhase.Ended)
                {
                    float dx = x - _dragStartX;
                    if (Mathf.Abs(dx) >= SwipeDistance)
                    {
                        Select(_selected + (dx < 0f ? 1 : -1));
                    }

                    _dragId = -1;
                }
            }

            if (Pointers.Count == 0)
            {
                _dragId = -1;
            }
        }
    }
}
