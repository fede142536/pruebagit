using UnityEngine;
using UnityEngine.UI;
using Football.Core.Domain;

namespace Football.Game
{
    /// <summary>Colores y piezas visuales comunes de los menús (tema «Noche + verde cancha»: fondo verde muy oscuro y acento verde césped).</summary>
    public static class UiTheme
    {
        /// <summary>Color de acento.</summary>
        public static readonly Color Accent = new Color(0.247f, 0.714f, 0.478f);
        /// <summary>Acento oscuro (botón principal).</summary>
        public static readonly Color AccentDark = new Color(0.13f, 0.47f, 0.31f);
        /// <summary>Texto principal.</summary>
        public static readonly Color Text = new Color(0.91f, 0.937f, 0.914f);
        /// <summary>Texto secundario.</summary>
        public static readonly Color TextDim = new Color(0.616f, 0.702f, 0.659f);
        /// <summary>Texto apagado (opciones no disponibles).</summary>
        public static readonly Color TextMuted = new Color(0.36f, 0.45f, 0.40f);
        /// <summary>Panel translúcido.</summary>
        public static readonly Color Panel = new Color(0.075f, 0.13f, 0.11f, 0.88f);
        /// <summary>Botón normal.</summary>
        public static readonly Color Button = new Color(0.12f, 0.19f, 0.16f, 0.95f);
        /// <summary>Elemento seleccionado.</summary>
        public static readonly Color Selected = new Color(0.102f, 0.227f, 0.176f, 0.97f);
        /// <summary>Texto sobre los botones de acento (verde claro): casi negro, como en el diseño.</summary>
        public static readonly Color OnAccent = new Color(0.047f, 0.102f, 0.078f);
        /// <summary>Borde de los elementos inactivos (líneas del diseño).</summary>
        public static readonly Color Line = new Color(0.239f, 0.318f, 0.282f);
        /// <summary>Acento claro para elementos activos y títulos.</summary>
        public static readonly Color AccentStrong = new Color(0.663f, 0.925f, 0.780f);
        /// <summary>Peligro (salir, descartar).</summary>
        public static readonly Color Danger = new Color(0.878f, 0.408f, 0.353f);
        /// <summary>Aviso.</summary>
        public static readonly Color Warning = new Color(1f, 0.62f, 0.3f);
        /// <summary>Estrella encendida.</summary>
        public static readonly Color Gold = new Color(1f, 0.82f, 0.25f);

        private static Sprite _gradient;
        private const float BackgroundAlpha = 0.82f;

        /// <summary>Fondo a pantalla completa con degradado vertical.</summary>
        public static void Background(Transform parent)
        {
            // Semitransparente: detrás se ve la portada del túnel (TitleStage).
            Image image = UiKit.Panel(parent, "Fondo", UiKit.Reference * 0.5f, UiKit.FullScreenSize, new Color(1f, 1f, 1f, BackgroundAlpha));
            image.sprite = Gradient();
            // Líneas de cancha muy tenues como decoración.
            Color line = new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, 0.07f);
            UiKit.Panel(parent, "LineaCentral", new Vector2(1500f, 540f), new Vector2(4f, 1080f), line);
            UiKit.Panel(parent, "Circulo", new Vector2(1500f, 540f), new Vector2(420f, 420f), line, true);
            UiKit.Panel(parent, "CirculoInterior", new Vector2(1500f, 540f), new Vector2(412f, 412f), new Color(0.04f, 0.075f, 0.06f, 0.9f), true);
        }

        private static Sprite Gradient()
        {
            if (_gradient != null)
            {
                return _gradient;
            }

            const int height = 64;
            var texture = new Texture2D(2, height, TextureFormat.RGBA32, false);
            var top = new Color(0.06f, 0.105f, 0.09f);
            var bottom = new Color(0.03f, 0.06f, 0.05f);
            var pixels = new Color32[2 * height];
            for (int y = 0; y < height; y++)
            {
                Color c = Color.Lerp(bottom, top, y / (float)(height - 1));
                pixels[y * 2] = c;
                pixels[y * 2 + 1] = c;
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            _gradient = Sprite.Create(texture, new Rect(0, 0, 2, height), new Vector2(0.5f, 0.5f), 100f);
            return _gradient;
        }

        /// <summary>Botón de texto alineado a la izquierda sin fondo (estilo menú principal).</summary>
        public static UiButton MenuItem(Transform parent, string name, Vector2 center, Vector2 size, string text, int fontSize)
        {
            UiButton button = UiKit.Button(parent, name, center, size, text, fontSize, new Color(0f, 0f, 0f, 0f));
            button.Label.alignment = TextAnchor.MiddleLeft;
            return button;
        }

        /// <summary>Código corto en español de una posición (POR, DFC, LI…).</summary>
        public static string Code(Position p)
        {
            switch (p)
            {
                case Position.Goalkeeper: return "POR";
                case Position.CenterBack: return "DFC";
                case Position.LeftBack: return "LI";
                case Position.RightBack: return "LD";
                case Position.DefensiveMidfielder: return "MCD";
                case Position.CentralMidfielder: return "MC";
                case Position.AttackingMidfielder: return "MCO";
                case Position.LeftMidfielder: return "MI";
                case Position.RightMidfielder: return "MD";
                case Position.LeftWinger: return "EI";
                case Position.RightWinger: return "ED";
                default: return "DC";
            }
        }
    }

    /// <summary>Cinco estrellas (círculos) que resumen la media de un equipo.</summary>
    public sealed class StarRating
    {
        private const float MinRating = 62f;
        private const float RatingPerStar = 3f;
        private readonly Image[] _stars = new Image[5];

        /// <summary>Crea las estrellas centradas en el punto dado.</summary>
        public StarRating(Transform parent, Vector2 center)
        {
            for (int i = 0; i < _stars.Length; i++)
            {
                _stars[i] = UiKit.Panel(parent, "Estrella" + i, center + new Vector2((i - 2) * 26f, 0f), new Vector2(24f, 24f), UiTheme.TextMuted);
                _stars[i].sprite = UiKit.StarSprite;
            }
        }

        /// <summary>Enciende las estrellas según la media.</summary>
        public void Set(float rating)
        {
            int count = Mathf.Clamp(Mathf.RoundToInt((rating - MinRating) / RatingPerStar), 1, 5);
            for (int i = 0; i < _stars.Length; i++)
            {
                _stars[i].color = i < count ? UiTheme.Gold : UiTheme.TextMuted;
            }
        }
    }

    /// <summary>Dibujo de una camiseta con su diseño (lisa, rayas, franja o banda) y el pantalón.</summary>
    public sealed class KitPreview
    {
        private readonly Image _body, _sleeveL, _sleeveR, _shorts, _band, _sash, _half;
        private readonly Image[] _chevron = new Image[2];
        private readonly Image[] _stripes = new Image[3];

        /// <summary>Crea la camiseta centrada en el punto dado.</summary>
        public KitPreview(Transform parent, Vector2 center)
        {
            _sleeveL = Sleeve(parent, center + new Vector2(-82f, 52f), 32f);
            _sleeveR = Sleeve(parent, center + new Vector2(82f, 52f), -32f);
            _body = UiKit.Panel(parent, "Camiseta", center + new Vector2(0f, 20f), new Vector2(130f, 150f), Color.white);
            _body.gameObject.AddComponent<RectMask2D>();
            Transform body = _body.transform;
            for (int i = 0; i < _stripes.Length; i++)
            {
                _stripes[i] = UiKit.Panel(body, "Raya" + i, new Vector2(25f + i * 40f, 75f), new Vector2(18f, 150f), Color.black);
            }

            _band = UiKit.Panel(body, "Franja", new Vector2(65f, 85f), new Vector2(130f, 30f), Color.black);
            _sash = UiKit.Panel(body, "Banda", new Vector2(65f, 75f), new Vector2(30f, 230f), Color.black);
            _sash.transform.localRotation = Quaternion.Euler(0f, 0f, 38f);
            _half = UiKit.Panel(body, "Mitad", new Vector2(97.5f, 75f), new Vector2(65f, 150f), Color.black);
            for (int i = 0; i < _chevron.Length; i++)
            {
                int side = i == 0 ? -1 : 1;
                _chevron[i] = UiKit.Panel(body, "V" + i, new Vector2(65f + side * 24f, 110f), new Vector2(18f, 80f), Color.black);
                _chevron[i].transform.localRotation = Quaternion.Euler(0f, 0f, side * 35f);
            }
            UiKit.Panel(body, "Cuello", new Vector2(65f, 146f), new Vector2(44f, 10f), new Color(0f, 0f, 0f, 0.35f));
            _shorts = UiKit.Panel(parent, "Pantalon", center + new Vector2(0f, -88f), new Vector2(120f, 56f), Color.white);
        }

        private static Image Sleeve(Transform parent, Vector2 center, float angle)
        {
            Image sleeve = UiKit.Panel(parent, "Manga", center, new Vector2(54f, 70f), Color.white);
            sleeve.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            return sleeve;
        }

        /// <summary>Muestra una equipación.</summary>
        public void Set(KitColors kit)
        {
            _body.color = kit.Shirt;
            _sleeveL.color = kit.Shirt;
            _sleeveR.color = kit.Shirt;
            _shorts.color = kit.Shorts;
            for (int i = 0; i < _stripes.Length; i++)
            {
                _stripes[i].color = kit.Secondary;
                _stripes[i].gameObject.SetActive(kit.Pattern == KitPattern.Stripes);
            }

            _band.color = kit.Secondary;
            _band.gameObject.SetActive(kit.Pattern == KitPattern.Band);
            _sash.color = kit.Secondary;
            _sash.gameObject.SetActive(kit.Pattern == KitPattern.Sash);
            _half.color = kit.Secondary;
            _half.gameObject.SetActive(kit.Pattern == KitPattern.Halves);
            for (int i = 0; i < _chevron.Length; i++)
            {
                _chevron[i].color = kit.Secondary;
                _chevron[i].gameObject.SetActive(kit.Pattern == KitPattern.Chevron);
            }
        }
    }

    /// <summary>Base de las pantallas de menú: lienzo propio, fondo y lectura de toques.</summary>
    public abstract class MenuScreenBase
    {
        private readonly Vector2[] _taps = new Vector2[PointerInput.MaxPointers];

        /// <summary>Lienzo de la pantalla.</summary>
        protected readonly Canvas Canvas;
        /// <summary>Raíz donde se crean los elementos.</summary>
        protected readonly Transform Root;
        /// <summary>Entrada.</summary>
        protected readonly PointerInput Pointers;

        /// <summary>Crea el lienzo con el fondo del tema (oculto hasta <see cref="Show"/>).</summary>
        protected MenuScreenBase(Transform parent, PointerInput pointers, string name)
        {
            Pointers = pointers;
            Canvas = UiKit.CreateCanvas(name, parent, 5);
            Root = UiKit.CreateArea(Canvas);
            UiTheme.Background(Root);
            Canvas.gameObject.SetActive(false);
        }

        /// <summary>Muestra la pantalla.</summary>
        public virtual void Show()
        {
            Canvas.gameObject.SetActive(true);
        }

        /// <summary>Oculta la pantalla.</summary>
        public virtual void Hide()
        {
            Canvas.gameObject.SetActive(false);
        }

        /// <summary>Se pulsó "atrás" (Esc en el ordenador o el botón atrás de Android).</summary>
        protected bool BackKey => Pointers.Keyboard.PauseDown;

        /// <summary>Toques que empezaron en este fotograma, en unidades del área de diseño. Devuelve cuántos hay en <paramref name="taps"/>.</summary>
        protected int Taps(out Vector2[] taps)
        {
            taps = _taps;
            int n = 0;
            for (int i = 0; i < Pointers.Count; i++)
            {
                if (Pointers.Pointers[i].Phase == PointerPhase.Began)
                {
                    _taps[n++] = UiKit.ToArea(Pointers.Pointers[i].Position);
                }
            }

            return n;
        }

        /// <summary>Crea el título de la pantalla arriba a la izquierda.</summary>
        protected Text Title(string text, string subtitle = null)
        {
            Text title = UiKit.Label(Root, "Titulo", new Vector2(600f, 1005f), new Vector2(1000f, 80f), text, 54, UiTheme.Text, TextAnchor.MiddleLeft);
            UiKit.Panel(Root, "Acento", new Vector2(68f, 1005f), new Vector2(8f, 64f), UiTheme.Accent);
            if (subtitle != null)
            {
                UiKit.Label(Root, "Subtitulo", new Vector2(600f, 950f), new Vector2(1000f, 40f), subtitle, 26, UiTheme.TextDim, TextAnchor.MiddleLeft);
            }

            return title;
        }
    }
}
