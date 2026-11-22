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
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; // mismo criterio que UiKit.Scale
            return canvas;
        }

        /// <summary>
        /// Píxeles por unidad de lienzo. El lienzo nunca es más chico que 1920×1080 (modo «Expand»): en pantallas más anchas
        /// que 16:9 crece a lo ancho y en las más angostas (ventana del editor, tabletas) crece a lo alto.
        /// </summary>
        public static float Scale
        {
            get
            {
                if (Screen.width <= 0 || Screen.height <= 0)
                {
                    return 1f;
                }

                return Mathf.Min(Screen.width / Reference.x, Screen.height / Reference.y);
            }
        }

        /// <summary>Ancho real del lienzo en unidades (≈2408 en un teléfono 20:9, 1920 en 16:9).</summary>
        public static float CanvasWidth => Screen.width / Scale;

        /// <summary>Alto real del lienzo en unidades (1080, o más en pantallas más angostas que 16:9).</summary>
        public static float CanvasHeight => Screen.height / Scale;

        /// <summary>
        /// Margen que ocupa la muesca o la cámara frontal a la izquierda, en unidades del lienzo (0 si la pantalla no tiene).
        /// Sale de <c>Screen.safeArea</c>: en un 20:9 apaisado la muesca queda a la izquierda o a la derecha según el giro.
        /// </summary>
        public static float SafeLeft => Screen.width > 0 ? Mathf.Max(0f, Screen.safeArea.xMin) / Scale : 0f;

        /// <summary>Margen inseguro a la derecha (unidades del lienzo).</summary>
        public static float SafeRight => Screen.width > 0 ? Mathf.Max(0f, Screen.width - Screen.safeArea.xMax) / Scale : 0f;

        /// <summary>Margen inseguro arriba (unidades del lienzo).</summary>
        public static float SafeTop => Screen.height > 0 ? Mathf.Max(0f, Screen.height - Screen.safeArea.yMax) / Scale : 0f;

        /// <summary>Margen inseguro abajo (barra de gestos; unidades del lienzo).</summary>
        public static float SafeBottom => Screen.height > 0 ? Mathf.Max(0f, Screen.safeArea.yMin) / Scale : 0f;

        /// <summary>Desplazamiento del área de diseño de 1920×1080, centrada en el lienzo real.</summary>
        public static Vector2 AreaOffset => new Vector2((CanvasWidth - Reference.x) * 0.5f, (CanvasHeight - Reference.y) * 0.5f);

        /// <summary>Convierte una posición de pantalla (píxeles) a unidades del lienzo real.</summary>
        public static Vector2 ToCanvas(Vector2 screen) => screen / Scale;

        /// <summary>Convierte una posición de pantalla (píxeles) a unidades del área de diseño centrada.</summary>
        public static Vector2 ToArea(Vector2 screen) => screen / Scale - AreaOffset;

        /// <summary>
        /// Crea dentro del lienzo un área de 1920×1080 centrada: los menús se diseñan en ella y quedan centrados en pantallas
        /// más anchas (20:9). Lo que debe pegarse a los bordes (controles táctiles) va directamente en el lienzo.
        /// </summary>
        public static RectTransform CreateArea(Canvas canvas)
        {
            var go = new GameObject("Area", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Reference;
            return rect;
        }

        /// <summary>Tamaño de un fondo que cubre cualquier proporción de pantalla, centrado en el área de diseño.</summary>
        public static Vector2 FullScreenSize => new Vector2(Reference.x * 2f, Reference.y * 2f);

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
