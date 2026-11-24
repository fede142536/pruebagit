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
        // Líneas de la cancha del minimapa: cada una con su centro y tamaño como fracción del radar (el alto cambia con la cancha).
        private struct Line { public RectTransform Rect; public float Fx, Fy, Fw, Fh; }
        private const float LineThickness = 2f;
        private static readonly Color LineColor = new Color(1f, 1f, 1f, 0.45f);
        private readonly System.Collections.Generic.List<Line> _lines = new System.Collections.Generic.List<Line>();
        private RectTransform _circle, _circleInner;
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

            BuildPitchLines();
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

        /// <summary>Dibuja el contorno de la cancha (bordes, línea media, círculo central y las dos áreas) con proporciones reglamentarias.</summary>
        private void BuildPitchLines()
        {
            AddBox("Contorno", 0.5f, 0.5f, 1f, 1f);
            AddLine("Media", 0.5f, 0.5f, 0f, 1f);
            const float penaltyDepth = 0.157f, penaltyWidth = 0.593f, goalDepth = 0.052f, goalWidth = 0.269f;
            AddBox("AreaIzq", penaltyDepth * 0.5f, 0.5f, penaltyDepth, penaltyWidth);
            AddBox("AreaDer", 1f - penaltyDepth * 0.5f, 0.5f, penaltyDepth, penaltyWidth);
            AddBox("AreaChicaIzq", goalDepth * 0.5f, 0.5f, goalDepth, goalWidth);
            AddBox("AreaChicaDer", 1f - goalDepth * 0.5f, 0.5f, goalDepth, goalWidth);
            _circle = (RectTransform)UiKit.Panel(_panel, "CirculoCentral", Vector2.zero, Vector2.one, LineColor, true).transform;
            _circleInner = (RectTransform)UiKit.Panel(_panel, "CirculoCentralInterior", Vector2.zero, Vector2.one, new Color(0.05f, 0.25f, 0.1f, 1f), true).transform;
        }

        private void AddBox(string name, float fx, float fy, float fw, float fh)
        {
            AddLine(name + "N", fx, fy + fh * 0.5f, fw, 0f);
            AddLine(name + "S", fx, fy - fh * 0.5f, fw, 0f);
            AddLine(name + "O", fx - fw * 0.5f, fy, 0f, fh);
            AddLine(name + "E", fx + fw * 0.5f, fy, 0f, fh);
        }

        private void AddLine(string name, float fx, float fy, float fw, float fh)
        {
            Image image = UiKit.Panel(_panel, name, Vector2.zero, Vector2.one, LineColor);
            _lines.Add(new Line { Rect = (RectTransform)image.transform, Fx = fx, Fy = fy, Fw = fw, Fh = fh });
        }

        /// <summary>Coloca las líneas según el tamaño actual del radar.</summary>
        private void LayoutPitchLines()
        {
            for (int i = 0; i < _lines.Count; i++)
            {
                Line l = _lines[i];
                l.Rect.anchoredPosition = new Vector2(l.Fx * Width, l.Fy * _height);
                l.Rect.sizeDelta = new Vector2(l.Fw > 0f ? l.Fw * Width : LineThickness, l.Fh > 0f ? l.Fh * _height : LineThickness);
            }

            const float circleShare = 0.174f; // el círculo central mide el 17,4 % del largo de la cancha
            float d = Width * circleShare;
            _circle.anchoredPosition = _circleInner.anchoredPosition = new Vector2(Width * 0.5f, _height * 0.5f);
            _circle.sizeDelta = new Vector2(d, d);
            _circleInner.sizeDelta = new Vector2(d - 2f * LineThickness, d - 2f * LineThickness);
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
            LayoutPitchLines();
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
