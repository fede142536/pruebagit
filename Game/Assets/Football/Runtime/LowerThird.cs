using UnityEngine;
using UnityEngine.UI;

namespace Football.Game
{
    /// <summary>
    /// Placa animada estilo transmisión (debajo del marcador): entra deslizándose, se queda unos segundos y sale. Muestra
    /// el tipo de jugada (gol, tarjeta), el minuto, el jugador y el equipo con su color (sin escudos ni logotipos).
    /// </summary>
    public sealed class LowerThird
    {
        private const float Width = 620f;
        private const float Height = 56f;
        private const float Margin = 20f;
        private const float Top = 92f;          // debajo del marcador
        private const float SlideSeconds = 0.3f;
        private const float HoldSeconds = 3.2f;

        private readonly RectTransform _root;
        private readonly Image _stripe;
        private readonly Image _tag;
        private readonly Text _tagText;
        private readonly Text _text;
        private float _time = float.MaxValue;

        /// <summary>Crea la placa (oculta) dentro del lienzo.</summary>
        public LowerThird(Transform canvas)
        {
            var go = new GameObject("Placa", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            _root = (RectTransform)go.transform;
            _root.anchorMin = new Vector2(0f, 1f);
            _root.anchorMax = new Vector2(0f, 1f);
            _root.pivot = new Vector2(0f, 1f);
            _root.sizeDelta = new Vector2(Width, Height);
            go.AddComponent<Canvas>(); // se mueve sola: no reconstruye el resto de la interfaz

            UiKit.Panel(_root, "Fondo", new Vector2(Width * 0.5f, Height * 0.5f), new Vector2(Width, Height), new Color(0.05f, 0.06f, 0.09f, 0.88f));
            _stripe = UiKit.Panel(_root, "Color", new Vector2(6f, Height * 0.5f), new Vector2(12f, Height), Color.white);
            _tag = UiKit.Panel(_root, "Etiqueta", new Vector2(85f, Height * 0.5f), new Vector2(130f, Height - 14f), Color.white);
            _tagText = UiKit.Label(_tag.transform, "Texto", new Vector2(65f, (Height - 14f) * 0.5f), new Vector2(130f, 40f), string.Empty, 26, Color.black);
            _text = UiKit.Label(_root, "Detalle", new Vector2(165f + (Width - 175f) * 0.5f, Height * 0.5f), new Vector2(Width - 175f, 46f),
                string.Empty, 26, Color.white, TextAnchor.MiddleLeft);
            go.SetActive(false);
        }

        /// <summary>Muestra la placa.</summary>
        /// <param name="tag">Tipo de jugada ("GOL", "AMARILLA"...).</param>
        /// <param name="tagColor">Color de la etiqueta.</param>
        /// <param name="detail">Minuto, jugador y equipo.</param>
        /// <param name="teamColor">Color del equipo.</param>
        public void Show(string tag, Color tagColor, string detail, Color teamColor)
        {
            _tagText.text = tag;
            _tag.color = tagColor;
            _text.text = detail;
            _stripe.color = teamColor;
            _time = 0f;
            _root.gameObject.SetActive(true);
        }

        /// <summary>Oculta la placa ya.</summary>
        public void Hide()
        {
            _time = float.MaxValue;
            _root.gameObject.SetActive(false);
        }

        /// <summary>Anima la entrada y la salida.</summary>
        public void Update(float deltaTime)
        {
            if (_time == float.MaxValue)
            {
                return;
            }

            _time += deltaTime;
            float total = SlideSeconds * 2f + HoldSeconds;
            if (_time >= total)
            {
                Hide();
                return;
            }

            float inT = Mathf.Clamp01(_time / SlideSeconds);
            float outT = Mathf.Clamp01((_time - SlideSeconds - HoldSeconds) / SlideSeconds);
            float x = Mathf.Lerp(-Width, Margin, inT * inT * (3f - 2f * inT)) - (Width + Margin) * outT * outT;
            _root.anchoredPosition = new Vector2(x, -Top);
        }
    }
}
