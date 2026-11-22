using Football.Core.MatchCore;
using UnityEngine;
using UnityEngine.UI;
using Football.Core.Domain;

namespace Football.Game
{
    /// <summary>
    /// Orientación en el partido: radar semitransparente abajo al centro (solo 11 contra 11) con los jugadores y el balón, y
    /// fichas con el dorsal en el borde de la pantalla para los compañeros que quedan fuera de cámara cuando tu equipo
    /// tiene el balón (para saber hacia dónde dar un pase largo). Todo se crea una vez; por fotograma solo se mueven objetos.
    /// </summary>
    public sealed class Radar
    {
        private const float Width = 300f;
        private const float BottomMargin = 18f;
        private const float DotSize = 11f;
        private const float BallDotSize = 8f;
        private const float ChipSize = 46f;
        private const float EdgeMargin = 34f;

        private static readonly Color Own = new Color(0.2f, 0.6f, 1f);
        private static readonly Color Rival = new Color(1f, 0.3f, 0.3f);
        private static readonly Color Controlled = new Color(1f, 0.85f, 0.05f);

        private readonly Canvas _canvas;
        private readonly RectTransform _panel;
        private readonly RectTransform[] _dots = new RectTransform[MatchSnapshot.PlayerCount];
        private readonly Image[] _dotImages = new Image[MatchSnapshot.PlayerCount];
        private readonly RectTransform _ballDot;
        private readonly RectTransform[] _chips = new RectTransform[MatchSnapshot.PlayerCount];
        private readonly Text[] _chipLabels = new Text[MatchSnapshot.PlayerCount];
        private float _height;
        private float _halfLength = 1f, _halfWidth = 1f;
        private int _count, _perTeam;
        private bool _radarEnabled;

        /// <summary>Crea el radar y las fichas (ocultos hasta <see cref="Setup"/>).</summary>
        public Radar(Transform parent)
        {
            _canvas = UiKit.CreateCanvas("RadarUI", parent, 7);
            var go = new GameObject("Radar", typeof(RectTransform));
            go.transform.SetParent(_canvas.transform, false);
            _panel = (RectTransform)go.transform;
            _panel.anchorMin = new Vector2(0.5f, 0f);
            _panel.anchorMax = new Vector2(0.5f, 0f);
            _panel.pivot = new Vector2(0.5f, 0f);
            _panel.anchoredPosition = new Vector2(0f, BottomMargin);
            Image back = go.AddComponent<Image>();
            back.color = new Color(0.05f, 0.25f, 0.1f, 0.45f);
            back.raycastTarget = false;

            for (int i = 0; i < _dots.Length; i++)
            {
                _dotImages[i] = UiKit.Panel(_panel, "Punto" + i, Vector2.zero, new Vector2(DotSize, DotSize), Own, true);
                _dots[i] = (RectTransform)_dotImages[i].transform;

                Image chip = UiKit.Panel(_canvas.transform, "Ficha" + i, Vector2.zero, new Vector2(ChipSize, ChipSize), new Color(0.1f, 0.35f, 0.75f, 0.8f), true);
                _chips[i] = (RectTransform)chip.transform;
                _chipLabels[i] = UiKit.Label(chip.transform, "Dorsal", new Vector2(ChipSize * 0.5f, ChipSize * 0.5f), new Vector2(ChipSize, ChipSize), string.Empty, 22, Color.white);
                chip.gameObject.SetActive(false);
            }

            _ballDot = (RectTransform)UiKit.Panel(_panel, "Balon", Vector2.zero, new Vector2(BallDotSize, BallDotSize), Color.white, true).transform;
            _canvas.gameObject.SetActive(false);
        }

        /// <summary>Prepara el radar para un partido.</summary>
        /// <param name="engine">Motor del partido.</param>
        /// <param name="showRadar">Mostrar el radar (en 3 contra 3 no hace falta).</param>
        public void Setup(MatchEngine engine, bool showRadar)
        {
            _count = engine.Players.Length;
            _perTeam = engine.PlayersPerTeam;
            _halfLength = engine.Pitch.HalfLength;
            _halfWidth = engine.Pitch.HalfWidth;
            _height = Width * engine.Pitch.Width / engine.Pitch.Length;
            _panel.sizeDelta = new Vector2(Width, _height);
            _radarEnabled = showRadar;
            _panel.gameObject.SetActive(showRadar);
            for (int i = 0; i < _dots.Length; i++)
            {
                bool used = i < _count;
                _dots[i].gameObject.SetActive(used);
                if (used)
                {
                    _dotImages[i].color = i < _perTeam ? Own : Rival;
                    _chipLabels[i].text = engine.Players[i].Number.ToString();
                }

                _chips[i].gameObject.SetActive(false);
            }

            _canvas.gameObject.SetActive(true);
        }

        /// <summary>Oculta todo.</summary>
        public void Hide()
        {
            _canvas.gameObject.SetActive(false);
        }

        /// <summary>Actualiza el radar y las fichas para este fotograma.</summary>
        /// <param name="s">Instantánea actual.</param>
        /// <param name="view">Vista del partido (para saber quién está fuera de cámara).</param>
        /// <param name="controlled">Jugador controlado, o -1.</param>
        /// <param name="ownTeamHasBall">Tu equipo tiene el balón (solo entonces se muestran las fichas).</param>
        public void Update(MatchSnapshot s, MatchView view, int controlled, bool ownTeamHasBall)
        {
            if (_radarEnabled)
            {
                for (int i = 0; i < _count; i++)
                {
                    bool show = !s.SentOff[i];
                    if (_dots[i].gameObject.activeSelf != show)
                    {
                        _dots[i].gameObject.SetActive(show);
                    }

                    _dots[i].anchoredPosition = ToRadar(s.PlayerX[i], s.PlayerY[i]);
                    _dotImages[i].color = i == controlled ? Controlled : i < _perTeam ? Own : Rival;
                }

                _ballDot.anchoredPosition = ToRadar(s.BallX, s.BallY);
            }

            float scale = UiKit.Scale;
            float width = UiKit.CanvasWidth, height = UiKit.CanvasHeight;
            for (int i = 0; i < _perTeam; i++)
            {
                bool show = false;
                if (ownTeamHasBall && i != controlled && i != 0 && !s.SentOff[i] && view.HeadOnScreen(i, out Vector3 screen))
                {
                    var p = new Vector2(screen.x / scale, screen.y / scale);
                    bool outside = p.x < 0f || p.x > width || p.y < 0f || p.y > height;
                    if (outside)
                    {
                        show = true;
                        _chips[i].anchoredPosition = new Vector2(Mathf.Clamp(p.x, EdgeMargin, width - EdgeMargin),
                            Mathf.Clamp(p.y, EdgeMargin, height - EdgeMargin));
                    }
                }

                if (_chips[i].gameObject.activeSelf != show)
                {
                    _chips[i].gameObject.SetActive(show);
                }
            }
        }

        private Vector2 ToRadar(float x, float y) => new Vector2(
            (x / _halfLength + 1f) * 0.5f * Width,
            (y / _halfWidth + 1f) * 0.5f * _height);
    }
}
