using UnityEngine;
using UnityEngine.UI;

namespace Football.Game
{
    /// <summary>Elemento táctil de la interfaz: fondo, etiqueta y zona de toque (círculo o rectángulo) en unidades de lienzo.</summary>
    public sealed class UiButton
    {
        /// <summary>Objeto raíz.</summary>
        public readonly GameObject Root;
        /// <summary>Imagen de fondo.</summary>
        public readonly Image Background;
        /// <summary>Etiqueta de texto.</summary>
        public readonly Text Label;
        /// <summary>Centro en unidades de lienzo (origen abajo a la izquierda).</summary>
        public Vector2 Center;
        /// <summary>Tamaño en unidades de lienzo.</summary>
        public Vector2 Size;
        /// <summary>La zona de toque es un círculo (usa el ancho como diámetro).</summary>
        public bool Circle;

        /// <summary>Crea el botón.</summary>
        public UiButton(GameObject root, Image background, Text label, bool circle)
        {
            Root = root;
            Background = background;
            Label = label;
            Circle = circle;
        }

        /// <summary>Muestra u oculta el botón.</summary>
        public void SetVisible(bool visible)
        {
            if (Root.activeSelf != visible)
            {
                Root.SetActive(visible);
            }
        }

        /// <summary>Indica si un punto del lienzo cae dentro de la zona de toque.</summary>
        public bool Contains(Vector2 canvasPoint)
        {
            if (!Root.activeSelf)
            {
                return false;
            }

            Vector2 d = canvasPoint - Center;
            if (Circle)
            {
                float r = Size.x * 0.5f;
                return d.sqrMagnitude <= r * r;
            }

            return Mathf.Abs(d.x) <= Size.x * 0.5f && Mathf.Abs(d.y) <= Size.y * 0.5f;
        }

        /// <summary>Coloca y dimensiona el botón.</summary>
        public void Place(Vector2 center, Vector2 size)
        {
            Center = center;
            Size = size;
            var rect = (RectTransform)Root.transform;
            rect.anchoredPosition = center;
            rect.sizeDelta = size;
        }
    }

    /// <summary>Funciones para crear la interfaz por código (sin escenas ni prefabs).</summary>
    public static class UiKit
    {
        /// <summary>Resolución de referencia del lienzo (alto 1080 unidades).</summary>
        public static readonly Vector2 Reference = new Vector2(1920f, 1080f);

        private static Sprite _circle;
        private static Font _font;

        /// <summary>Crea un lienzo a pantalla completa que escala con el alto de la pantalla.</summary>
        public static Canvas CreateCanvas(string name, Transform parent, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            return canvas;
        }

        /// <summary>Fuente integrada de Unity.</summary>
        public static Font DefaultFont
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }

                return _font;
            }
        }

        /// <summary>Sprite de un círculo blanco generado al vuelo.</summary>
        public static Sprite CircleSprite
        {
            get
            {
                if (_circle != null)
                {
                    return _circle;
                }

                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color32[size * size];
                float radius = size * 0.5f - 1f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x - (size - 1) * 0.5f;
                        float dy = y - (size - 1) * 0.5f;
                        float distance = Mathf.Sqrt(dx * dx + dy * dy);
                        byte alpha = (byte)(Mathf.Clamp01(radius - distance + 1f) * 255f);
                        pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply();
                texture.filterMode = FilterMode.Bilinear;
                _circle = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
                return _circle;
            }
        }

        /// <summary>Crea un panel o círculo coloreado anclado abajo a la izquierda.</summary>
        public static Image Panel(Transform parent, string name, Vector2 center, Vector2 size, Color color, bool circle = false)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = size;
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (circle)
            {
                image.sprite = CircleSprite;
            }

            return image;
        }

        /// <summary>Crea un texto centrado.</summary>
        public static Text Label(Transform parent, string name, Vector2 center, Vector2 size, string text, int fontSize, Color color,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = size;
            var label = go.AddComponent<Text>();
            label.font = DefaultFont;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        /// <summary>Crea un botón con fondo y etiqueta.</summary>
        public static UiButton Button(Transform parent, string name, Vector2 center, Vector2 size, string text, int fontSize, Color color,
            bool circle = false)
        {
            Image background = Panel(parent, name, center, size, color, circle);
            Text label = Label(background.transform, "Label", size * 0.5f, size, text, fontSize, Color.white);
            var button = new UiButton(background.gameObject, background, label, circle);
            button.Place(center, size);
            return button;
        }
    }
}
