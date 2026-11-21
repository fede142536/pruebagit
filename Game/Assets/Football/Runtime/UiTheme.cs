using UnityEngine;
using UnityEngine.UI;
using Football.Core.Domain;

namespace Football.Game
{
    /// <summary>Colores y piezas visuales comunes de los menús (estilo oscuro con acento turquesa).</summary>
    public static class UiTheme
    {
        /// <summary>Color de acento.</summary>
        public static readonly Color Accent = new Color(0.22f, 0.88f, 0.78f);
        /// <summary>Acento oscuro (botón principal).</summary>
        public static readonly Color AccentDark = new Color(0.08f, 0.55f, 0.48f);
        /// <summary>Texto principal.</summary>
        public static readonly Color Text = new Color(0.95f, 0.97f, 1f);
        /// <summary>Texto secundario.</summary>
        public static readonly Color TextDim = new Color(0.65f, 0.71f, 0.78f);
        /// <summary>Texto apagado (opciones no disponibles).</summary>
        public static readonly Color TextMuted = new Color(0.4f, 0.44f, 0.5f);
        /// <summary>Panel translúcido.</summary>
        public static readonly Color Panel = new Color(0.12f, 0.15f, 0.2f, 0.85f);
        /// <summary>Botón normal.</summary>
        public static readonly Color Button = new Color(0.18f, 0.22f, 0.28f, 0.95f);
        /// <summary>Elemento seleccionado.</summary>
        public static readonly Color Selected = new Color(0.12f, 0.42f, 0.38f, 0.95f);
        /// <summary>Aviso.</summary>
        public static readonly Color Warning = new Color(1f, 0.62f, 0.3f);
        /// <summary>Estrella encendida.</summary>
        public static readonly Color Gold = new Color(1f, 0.82f, 0.25f);

        private static Sprite _gradient;

        /// <summary>Fondo a pantalla completa con degradado vertical.</summary>
        public static void Background(Transform parent)
        {
            Image image = UiKit.Panel(parent, "Fondo", UiKit.Reference * 0.5f, UiKit.Reference, Color.white);
            image.sprite = Gradient();
            // Líneas de cancha muy tenues como decoración.
            Color line = new Color(1f, 1f, 1f, 0.035f);
            UiKit.Panel(parent, "LineaCentral", new Vector2(1500f, 540f), new Vector2(4f, 1080f), line);
            UiKit.Panel(parent, "Circulo", new Vector2(1500f, 540f), new Vector2(420f, 420f), line, true);
            UiKit.Panel(parent, "CirculoInterior", new Vector2(1500f, 540f), new Vector2(412f, 412f), new Color(0.05f, 0.08f, 0.12f, 0.9f), true);
        }

        private static Sprite Gradient()
        {
            if (_gradient != null)
            {
                return _gradient;
            }

            const int height = 64;
            var texture = new Texture2D(2, height, TextureFormat.RGBA32, false);
            var top = new Color(0.07f, 0.12f, 0.18f);
            var bottom = new Color(0.02f, 0.03f, 0.05f);
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
                _stars[i] = UiKit.Panel(parent, "Estrella" + i, center + new Vector2((i - 2) * 42f, 0f), new Vector2(30f, 30f), UiTheme.TextMuted, true);
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
        private readonly Image _body, _sleeveL, _sleeveR, _shorts, _band, _sash;
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
            Root = Canvas.transform;
            UiTheme.Background(Root);
            Canvas.gameObject.SetActive(false);
        }

        /// <summary>Muestra la pantalla.</summary>
        public virtual void Show()
        {
            Canvas.gameObject.SetActive(true);
        }

        /// <summary>Oculta la pantalla.</summary>
        public void Hide()
        {
            Canvas.gameObject.SetActive(false);
        }

        /// <summary>Se pulsó "atrás" (Esc en el ordenador o el botón atrás de Android).</summary>
        protected bool BackKey => Pointers.Keyboard.PauseDown;

        /// <summary>Toques que empezaron en este fotograma, en unidades de lienzo. Devuelve cuántos hay en <paramref name="taps"/>.</summary>
        protected int Taps(out Vector2[] taps)
        {
            taps = _taps;
            float scale = Canvas.scaleFactor > 0f ? Canvas.scaleFactor : 1f;
            int n = 0;
            for (int i = 0; i < Pointers.Count; i++)
            {
                if (Pointers.Pointers[i].Phase == PointerPhase.Began)
                {
                    _taps[n++] = Pointers.Pointers[i].Position / scale;
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
