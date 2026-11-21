using UnityEngine;
using UnityEngine.UI;

namespace Football.Game
{
    /// <summary>
    /// Barras bajo los pies del jugador controlado: potencia mientras se carga un pase o un tiro (verde → amarillo → rojo) y
    /// resistencia (se vacía al esprintar y se recupera al caminar). Sin basura por fotograma.
    /// </summary>
    public sealed class PlayerHud
    {
        private const float BarWidth = 110f;
        private const float PowerHeight = 12f;
        private const float StaminaHeight = 6f;
        private const float PowerOffset = 56f;   // debajo del nombre
        private const float StaminaOffset = 70f;
        private const float StaminaShowBelow = 0.98f; // con la resistencia llena no se muestra

        private static readonly Color Low = new Color(0.25f, 0.85f, 0.3f);
        private static readonly Color Mid = new Color(1f, 0.85f, 0.2f);
        private static readonly Color High = new Color(0.95f, 0.25f, 0.2f);
        private static readonly Color Back = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color StaminaColor = new Color(0.35f, 0.75f, 1f);

        private readonly Canvas _canvas;
        private readonly RectTransform _powerBack, _powerFill, _staminaBack, _staminaFill;
        private readonly Image _powerImage;

        /// <summary>Crea las barras (ocultas).</summary>
        public PlayerHud(Transform parent)
        {
            _canvas = UiKit.CreateCanvas("BarrasJugadorUI", parent, 9);
            _powerBack = Bar("FondoPotencia", PowerHeight, Back, out _);
            _powerFill = Bar("Potencia", PowerHeight, Low, out _powerImage);
            _staminaBack = Bar("FondoResistencia", StaminaHeight, Back, out _);
            _staminaFill = Bar("Resistencia", StaminaHeight, StaminaColor, out _);
            _canvas.gameObject.SetActive(false);
        }

        private RectTransform Bar(string name, float height, Color color, out Image image)
        {
            image = UiKit.Panel(_canvas.transform, name, Vector2.zero, new Vector2(BarWidth, height), color);
            var rect = (RectTransform)image.transform;
            rect.pivot = new Vector2(0f, 0.5f); // crece hacia la derecha desde el borde izquierdo
            rect.gameObject.SetActive(false);
            return rect;
        }

        /// <summary>Muestra u oculta todo.</summary>
        public void SetVisible(bool visible)
        {
            if (_canvas.gameObject.activeSelf != visible)
            {
                _canvas.gameObject.SetActive(visible);
            }
        }

        /// <summary>Actualiza las barras para este fotograma.</summary>
        /// <param name="view">Vista del partido (posición en pantalla).</param>
        /// <param name="player">Jugador controlado, o -1.</param>
        /// <param name="charge">Carga de pase o tiro de 0 a 1 (0 = no se está cargando).</param>
        /// <param name="stamina">Resistencia del jugador de 0 a 1.</param>
        public void Update(MatchView view, int player, float charge, float stamina)
        {
            Vector3 screen = default;
            bool onScreen = player >= 0 && view.FeetOnScreen(player, out screen);
            float scale = UiKit.Scale;
            var feet = new Vector2(screen.x / scale - BarWidth * 0.5f, screen.y / scale);

            bool showPower = onScreen && charge > 0f;
            Place(_powerBack, showPower, feet - new Vector2(0f, PowerOffset), BarWidth, PowerHeight);
            Place(_powerFill, showPower, feet - new Vector2(0f, PowerOffset), BarWidth * charge, PowerHeight);
            if (showPower)
            {
                _powerImage.color = charge < 0.5f ? Color.Lerp(Low, Mid, charge * 2f) : Color.Lerp(Mid, High, (charge - 0.5f) * 2f);
            }

            bool showStamina = onScreen && stamina < StaminaShowBelow;
            Place(_staminaBack, showStamina, feet - new Vector2(0f, StaminaOffset), BarWidth, StaminaHeight);
            Place(_staminaFill, showStamina, feet - new Vector2(0f, StaminaOffset), BarWidth * Mathf.Clamp01(stamina), StaminaHeight);
        }

        private static void Place(RectTransform bar, bool show, Vector2 position, float width, float height)
        {
            if (bar.gameObject.activeSelf != show)
            {
                bar.gameObject.SetActive(show);
            }

            if (show)
            {
                bar.anchoredPosition = position;
                bar.sizeDelta = new Vector2(width, height);
            }
        }
    }
}
