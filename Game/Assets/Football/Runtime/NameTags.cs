using UnityEngine;
using UnityEngine.UI;
using Football.Core.MatchCore;
using Football.Core.Domain;

namespace Football.Game
{
    /// <summary>Qué nombres se muestran sobre los jugadores.</summary>
    public enum NameMode
    {
        /// <summary>Todos los jugadores.</summary>
        All,
        /// <summary>Solo el jugador controlado y el que lleva el balón.</summary>
        Controlled,
        /// <summary>Ninguno.</summary>
        None
    }

    /// <summary>Nombres sobre la cabeza de los jugadores, en un lienzo propio para no redibujar el resto de la interfaz.</summary>
    public sealed class NameTags
    {
        private const int FontSize = 22;
        private const float FeetOffset = 24f;
        private const int HighlightFontSize = 28;

        private static readonly Color HomeColor = new Color(1f, 1f, 1f, 0.95f);
        private static readonly Color AwayColor = new Color(1f, 0.92f, 0.75f, 0.95f);
        private static readonly Color HighlightColor = new Color(1f, 0.88f, 0.1f);

        private readonly Canvas _canvas;
        private readonly Text[] _labels = new Text[MatchSnapshot.PlayerCount];
        private readonly RectTransform[] _rects = new RectTransform[MatchSnapshot.PlayerCount];
        private int _highlighted = -1;
        private int _count;

        /// <summary>Crea las etiquetas (ocultas hasta <see cref="Setup"/>).</summary>
        public NameTags(Transform parent)
        {
            _canvas = UiKit.CreateCanvas("NombresUI", parent, 8);
            for (int i = 0; i < _labels.Length; i++)
            {
                Text label = UiKit.Label(_canvas.transform, "Nombre" + i, Vector2.zero, new Vector2(320f, 40f), string.Empty, FontSize, HomeColor);
                Shadow shadow = label.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
                shadow.effectDistance = new Vector2(2f, -2f);
                _labels[i] = label;
                _rects[i] = (RectTransform)label.transform;
                label.gameObject.SetActive(false);
            }

            _canvas.gameObject.SetActive(false);
        }

        /// <summary>Pone los nombres de los jugadores del partido.</summary>
        public void Setup(MatchEngine engine)
        {
            _count = engine.Players.Length;
            for (int i = _count; i < _labels.Length; i++)
            {
                _labels[i].gameObject.SetActive(false); // partidos con menos jugadores (cancha cerrada)
            }

            for (int i = 0; i < _count; i++)
            {
                MatchPlayer p = engine.Players[i];
                // El dorsal delante del nombre (los jugadores son figuras sin textura: el número va en la etiqueta).
                _labels[i].text = p.Number > 0 ? p.Number + " " + p.Name : p.Name;
                _labels[i].color = p.Team == 0 ? HomeColor : AwayColor;
                _labels[i].fontSize = FontSize;
            }

            _highlighted = -1;
            _canvas.gameObject.SetActive(true);
        }

        /// <summary>Oculta todas las etiquetas.</summary>
        public void Hide()
        {
            _canvas.gameObject.SetActive(false);
        }

        /// <summary>Coloca las etiquetas sobre los jugadores para este fotograma.</summary>
        /// <param name="view">Vista del partido (da la posición en pantalla).</param>
        /// <param name="mode">Qué nombres mostrar.</param>
        /// <param name="controlled">Jugador controlado por la persona, o -1.</param>
        /// <param name="owner">Jugador con el balón, o -1.</param>
        /// <param name="homeTeamColorIndex">Primer índice del equipo visitante (para restaurar colores).</param>
        public void Update(MatchView view, NameMode mode, int controlled, int owner, int homeTeamColorIndex)
        {
            if (controlled != _highlighted)
            {
                if (_highlighted >= 0)
                {
                    _labels[_highlighted].color = _highlighted < homeTeamColorIndex ? HomeColor : AwayColor;
                    _labels[_highlighted].fontSize = FontSize;
                }

                if (controlled >= 0)
                {
                    _labels[controlled].color = HighlightColor;
                    _labels[controlled].fontSize = HighlightFontSize;
                }

                _highlighted = controlled;
            }

            float scale = UiKit.Scale;
            for (int i = 0; i < _count; i++)
            {
                bool wanted = mode == NameMode.All || (mode == NameMode.Controlled && (i == controlled || i == owner));
                Vector3 screen = default;
                bool show = wanted && view.FeetOnScreen(i, out screen);
                GameObject go = _labels[i].gameObject;
                if (go.activeSelf != show)
                {
                    go.SetActive(show);
                }

                if (show)
                {
                    // Debajo de los pies (y del anillo): no tapa al jugador ni choca con la flecha.
                    _rects[i].anchoredPosition = new Vector2(screen.x / scale, screen.y / scale - FeetOffset);
                }
            }
        }
    }
}
