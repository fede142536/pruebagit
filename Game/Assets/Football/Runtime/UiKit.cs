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
            FitLabel();
        }

        /// <summary>Ajusta el recuadro de la etiqueta al tamaño actual del botón (con un margen a cada lado) para que el texto nunca se salga.</summary>
        public void FitLabel()
        {
            if (Label == null)
            {
                return;
            }

            RectTransform rect = Label.rectTransform;
            rect.anchoredPosition = Size * 0.5f;
            rect.sizeDelta = new Vector2(Mathf.Max(1f, Size.x - 2f * LabelMargin), Size.y);
        }

        /// <summary>Margen horizontal (unidades de lienzo) entre el borde del botón y su texto.</summary>
        public const float LabelMargin = 10f;
    }

    /// <summary>
    /// Pinta el contorno de un botón: línea fina normal y, cuando el botón está elegido (su fondo es el color de «seleccionado»), el contorno claro del diseño.
    /// </summary>
    public sealed class OutlineSkin : MonoBehaviour
    {
        private const float SameColor = 0.02f;
        private Image _background;
        private Image[] _edges;

        /// <summary>Asocia el fondo del botón con los cuatro lados de su contorno.</summary>
        public void Setup(Image background, Image[] edges)
        {
            _background = background;
            _edges = edges;
        }

        private void LateUpdate()
        {
            if (_background == null)
            {
                return;
            }

            Color b = _background.color, s = UiTheme.Selected;
            bool selected = Mathf.Abs(b.r - s.r) + Mathf.Abs(b.g - s.g) + Mathf.Abs(b.b - s.b) < SameColor;
            Color target = selected ? UiTheme.AccentStrong : UiTheme.Line;
            if (_edges[0].color != target)
            {
                for (int i = 0; i < _edges.Length; i++)
                {
                    _edges[i].color = target;
                }
            }
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

        private static Sprite _star;

        /// <summary>Sprite de una estrella de cinco puntas blanca generada al vuelo (bordes suavizados por muestreo).</summary>
        public static Sprite StarSprite
        {
            get
            {
                if (_star != null)
                {
                    return _star;
                }

                const int size = 64, samples = 3, points = 5;
                const float outer = 0.96f, inner = 0.40f;   // radios relativos de las puntas y los vértices interiores
                var polygon = new Vector2[points * 2];
                for (int i = 0; i < polygon.Length; i++)
                {
                    float angle = Mathf.PI * 0.5f + i * Mathf.PI / points;
                    float radius = (i % 2 == 0 ? outer : inner) * 0.5f;
                    polygon[i] = new Vector2(0.5f + Mathf.Cos(angle) * radius * 2f * 0.5f, 0.5f + Mathf.Sin(angle) * radius * 2f * 0.5f);
                }

                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        int inside = 0;
                        for (int sy = 0; sy < samples; sy++)
                        {
                            for (int sx = 0; sx < samples; sx++)
                            {
                                var p = new Vector2((x + (sx + 0.5f) / samples) / size, (y + (sy + 0.5f) / samples) / size);
                                inside += InsidePolygon(polygon, p) ? 1 : 0;
                            }
                        }

                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255 * inside / (samples * samples)));
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply();
                texture.filterMode = FilterMode.Bilinear;
                _star = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
                return _star;
            }
        }

        private static bool InsidePolygon(Vector2[] polygon, Vector2 point)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                if ((polygon[i].y > point.y) != (polygon[j].y > point.y)
                    && point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>Contorno de 1,5 unidades en el borde de un elemento (dentro de su rectángulo).</summary>
        private static void Outline(Image background, Vector2 size)
        {
            const float t = 2f;
            Transform parent = background.transform;
            Color line = UiTheme.Line;
            var edges = new[]
            {
                Panel(parent, "BordeN", new Vector2(size.x * 0.5f, size.y - t * 0.5f), new Vector2(size.x, t), line),
                Panel(parent, "BordeS", new Vector2(size.x * 0.5f, t * 0.5f), new Vector2(size.x, t), line),
                Panel(parent, "BordeO", new Vector2(t * 0.5f, size.y * 0.5f), new Vector2(t, size.y), line),
                Panel(parent, "BordeE", new Vector2(size.x - t * 0.5f, size.y * 0.5f), new Vector2(t, size.y), line)
            };
            background.gameObject.AddComponent<OutlineSkin>().Setup(background, edges);
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

        /// <summary>Crea un texto centrado. Por defecto se ajusta a su recuadro (<see cref="FitToBox"/>): se achica o pasa a otra línea en vez de salirse.</summary>
        /// <param name="fit">Falso para los textos que cambian cada cuadro o de tamaño (etiquetas sobre los jugadores).</param>
        public static Text Label(Transform parent, string name, Vector2 center, Vector2 size, string text, int fontSize, Color color,
            TextAnchor alignment = TextAnchor.MiddleCenter, bool fit = true)
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
            if (fit)
            {
                FitToBox(label); // ningún texto se sale de su recuadro: se achica o pasa a otra línea
            }

            label.text = text;
            return label;
        }

        /// <summary>Cambia el tamaño de letra de un texto con ajuste automático (el nuevo tamaño pasa a ser el máximo).</summary>
        /// <param name="label">Texto.</param>
        /// <param name="fontSize">Tamaño de letra deseado.</param>
        public static void SetFontSize(Text label, int fontSize)
        {
            float ratio = label.fontSize > 0 ? label.resizeTextMinSize / (float)label.fontSize : FitMinScale;
            label.fontSize = fontSize;
            if (label.resizeTextForBestFit)
            {
                label.resizeTextMaxSize = fontSize;
                label.resizeTextMinSize = Mathf.Max(10, Mathf.RoundToInt(fontSize * ratio));
            }
        }

        /// <summary>Proporción mínima del tamaño de letra al achicar un texto largo para que entre en su recuadro.</summary>
        public const float FitMinScale = 0.55f;

        /// <summary>
        /// Hace que el texto nunca se salga de su recuadro: si no entra al tamaño pedido, se achica hasta <see cref="FitMinScale"/> (y si aún no entra,
        /// pasa a dos líneas). Usar en textos que cambian (nombres, números) o cuyo largo depende del idioma.
        /// </summary>
        /// <param name="label">Texto a ajustar (el tamaño de letra actual es el máximo).</param>
        /// <param name="minScale">Proporción mínima del tamaño de letra.</param>
        public static void FitToBox(Text label, float minScale = FitMinScale)
        {
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate; // con Truncate el ajuste automático mide también el alto del recuadro
            label.resizeTextForBestFit = true;
            label.resizeTextMaxSize = label.fontSize;
            label.resizeTextMinSize = Mathf.Max(10, Mathf.RoundToInt(label.fontSize * minScale));
        }

        /// <summary>Crea un botón con fondo y etiqueta.</summary>
        public static UiButton Button(Transform parent, string name, Vector2 center, Vector2 size, string text, int fontSize, Color color,
            bool circle = false)
        {
            Image background = Panel(parent, name, center, size, color, circle);
            Text label = Label(background.transform, "Label", size * 0.5f, size, text, fontSize, Color.white);
            FitToBox(label); // la etiqueta de un botón nunca se sale del botón
            if (!circle && color == UiTheme.Button)
            {
                Outline(background, size); // los botones secundarios del manual: contorno fino, fondo casi vacío
            }

            var button = new UiButton(background.gameObject, background, label, circle);
            button.Place(center, size); // también deja la etiqueta con su margen
            return button;
        }
    }
}
