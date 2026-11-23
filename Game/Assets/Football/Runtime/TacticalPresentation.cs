using Football.Core.Data;
using Football.Core.Domain;
using Football.Core.MatchCore;
using UnityEngine;
using UnityEngine.UI;

namespace Football.Game
{
    /// <summary>
    /// Presentación televisiva previa al pitido inicial (solo 11 contra 11): primero el local y después el visitante, con una
    /// toma panorámica de su mitad, una pizarra táctica sobre el césped que une las líneas del dibujo y, flotando sobre cada
    /// titular, una ficha con su silueta, media (OVR), puesto, flecha de forma y apellido. A un costado, una placa con el
    /// escudo procedural, el nombre del club y el dibujo. Botón SALTAR abajo a la derecha. Solo presenta: no toca el motor.
    /// Sin objetos nuevos por fotograma (las fichas se crean una vez y se reubican).
    /// </summary>
    public sealed class TacticalPresentation
    {
        private const int Tokens = 11;
        private const float SideSeconds = 4.5f;
        private const float TokenSize = 74f;
        private const float TokenLift = 70f;     // la ficha queda por encima de la cabeza (píxeles del área de diseño)
        private const float BoardDepthShare = 0.5f;

        private readonly GameObject _root;
        private readonly RectTransform[] _token = new RectTransform[Tokens];
        private readonly Image[] _ring = new Image[Tokens], _body = new Image[Tokens], _head = new Image[Tokens];
        private readonly Text[] _ovr = new Text[Tokens], _pos = new Text[Tokens], _name = new Text[Tokens], _arrow = new Text[Tokens];
        private readonly ClubCrest _crest;
        private readonly Text _club, _formation, _title;
        private readonly Image _plate;
        private readonly UiButton _skip;
        private readonly Vector2[] _boardPlayers = new Vector2[Tokens];
        private readonly int[] _boardLines = new int[Tokens];

        private MatchEngine _engine;
        private MatchView _view;
        private MatchTeamSetup[] _setups;
        private TeamDefinition[] _teams;
        private KitColors[] _kits;
        private float _time;
        private int _side = -1;

        /// <summary>La presentación está en curso.</summary>
        public bool Running => _side >= 0;

        /// <summary>Crea los elementos (ocultos).</summary>
        /// <param name="parent">Área de diseño del lienzo del partido.</param>
        public TacticalPresentation(Transform parent)
        {
            var go = new GameObject("PresentacionTactica", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.sizeDelta = UiKit.Reference;
            _root = go;

            _plate = UiKit.Panel(go.transform, "Placa", new Vector2(190f, 560f), new Vector2(300f, 440f), new Color(0.04f, 0.05f, 0.08f, 0.88f));
            _crest = new ClubCrest(go.transform, new Vector2(190f, 660f), 170f);
            _club = UiKit.Label(go.transform, "Club", new Vector2(190f, 520f), new Vector2(290f, 60f), string.Empty, 30, Color.white);
            _formation = UiKit.Label(go.transform, "Dibujo", new Vector2(190f, 460f), new Vector2(290f, 50f), string.Empty, 34, UiTheme.Accent);
            _title = UiKit.Label(go.transform, "Titulo", new Vector2(190f, 395f), new Vector2(290f, 40f), string.Empty, 22, UiTheme.TextDim);
            for (int i = 0; i < Tokens; i++)
            {
                BuildToken(go.transform, i);
            }

            _skip = UiKit.Button(go.transform, "Saltar", new Vector2(1760f, 90f), new Vector2(230f, 84f), "SALTAR  ▶", 32, new Color(0.05f, 0.06f, 0.09f, 0.85f));
            _root.SetActive(false);
        }

        private void BuildToken(Transform parent, int i)
        {
            var go = new GameObject("Ficha" + i, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(TokenSize, TokenSize);
            _token[i] = rect;
            Vector2 c = new Vector2(TokenSize * 0.5f, TokenSize * 0.5f);
            _ring[i] = UiKit.Panel(go.transform, "Aro", c, new Vector2(TokenSize, TokenSize), Color.white, true);
            UiKit.Panel(go.transform, "Fondo", c, new Vector2(TokenSize - 8f, TokenSize - 8f), new Color(0.06f, 0.08f, 0.12f, 0.95f), true);
            // Silueta procedural: hombros (en el color de la camiseta) y cabeza.
            _body[i] = UiKit.Panel(go.transform, "Torso", c + new Vector2(0f, -16f), new Vector2(TokenSize * 0.62f, TokenSize * 0.34f), Color.white, true);
            _head[i] = UiKit.Panel(go.transform, "Cabeza", c + new Vector2(0f, 6f), new Vector2(TokenSize * 0.3f, TokenSize * 0.3f), new Color(0.85f, 0.7f, 0.55f), true);
            _ovr[i] = UiKit.Label(go.transform, "Media", c + new Vector2(-TokenSize * 0.42f, TokenSize * 0.42f), new Vector2(46f, 30f), string.Empty, 24, UiTheme.Gold);
            _ovr[i].fontStyle = FontStyle.Bold;
            _pos[i] = UiKit.Label(go.transform, "Puesto", c + new Vector2(TokenSize * 0.44f, TokenSize * 0.42f), new Vector2(56f, 26f), string.Empty, 18, Color.white);
            _arrow[i] = UiKit.Label(go.transform, "Forma", c + new Vector2(TokenSize * 0.5f, -TokenSize * 0.2f), new Vector2(30f, 30f), "→", 26, Color.white);
            _name[i] = UiKit.Label(go.transform, "Apellido", c + new Vector2(0f, -TokenSize * 0.72f), new Vector2(170f, 30f), string.Empty, 22, Color.white);
        }

        private bool _hostIsSecondTeam;

        /// <summary>Empieza la presentación (primero el equipo de la persona; el rótulo LOCAL/VISITANTE sigue al dueño de casa).</summary>
        /// <param name="hostIsSecondTeam">El dueño de casa es el segundo equipo (modo carrera de visitante).</param>
        public void Begin(MatchEngine engine, MatchView view, MatchTeamSetup home, MatchTeamSetup away, TeamDefinition homeTeam,
            TeamDefinition awayTeam, KitColors homeKit, KitColors awayKit, bool hostIsSecondTeam = false)
        {
            _hostIsSecondTeam = hostIsSecondTeam;
            _engine = engine;
            _view = view;
            _setups = new[] { home, away };
            _teams = new[] { homeTeam, awayTeam };
            _kits = new[] { homeKit, awayKit };
            _time = 0f;
            _root.SetActive(true);
            ShowSide(0);
        }

        /// <summary>Avanza la presentación y reubica las fichas. Devuelve falso cuando terminó o se saltó.</summary>
        /// <param name="deltaTime">Tiempo del fotograma.</param>
        /// <param name="tap">Hubo un toque en este fotograma (en el área de diseño), o null.</param>
        public bool Update(float deltaTime, Vector2? tap)
        {
            if (_side < 0)
            {
                return false;
            }

            if (tap.HasValue && _skip.Contains(tap.Value))
            {
                Stop();
                return false;
            }

            _time += deltaTime;
            int side = (int)(_time / SideSeconds);
            if (side >= 2)
            {
                Stop();
                return false;
            }

            if (side != _side)
            {
                ShowSide(side);
            }

            PlaceTokens();
            return true;
        }

        /// <summary>Termina la presentación y devuelve la cámara a la transmisión.</summary>
        public void Stop()
        {
            _side = -1;
            _root.SetActive(false);
            if (_view != null)
            {
                _view.SetPresentationShot(float.NaN);
                _view.HideFormationBoard();
            }
        }

        private void ShowSide(int side)
        {
            _side = side;
            TeamState team = _engine.Teams[side];
            TeamDefinition def = _teams[side];
            KitColors kit = _kits[side];
            _crest.Set(def);
            _club.text = def.Name.ToUpperInvariant();
            _formation.text = team.Formation.Name;
            _title.text = (side == 0) != _hostIsSecondTeam ? "LOCAL" : "VISITANTE";
            _plate.color = new Color(kit.Shirt.r * 0.25f, kit.Shirt.g * 0.25f, kit.Shirt.b * 0.25f, 0.9f);

            // Toma y pizarra sobre la mitad propia (los jugadores están formados para el saque del medio).
            float dir = team.AttackDirection;
            float half = _engine.Pitch.HalfLength;
            _view.SetPresentationShot(-dir * half * BoardDepthShare);
            int count = Mathf.Min(Tokens, team.Players.Length);
            Vector2[] players = count == Tokens ? _boardPlayers : new Vector2[count];
            int[] lines = count == Tokens ? _boardLines : new int[count];
            for (int i = 0; i < count; i++)
            {
                MatchPlayer p = team.Players[i];
                players[i] = new Vector2(p.Pos.X, p.Pos.Y);
                lines[i] = (int)PlayerRatings.LineOf(p.Role);
                PlayerMatchView pv = i < _setups[side].Players.Length ? _setups[side].Players[i] : null;
                _ovr[i].text = Mathf.RoundToInt(RosterFactory.DisplayRating(p.Attr, p.Role)).ToString();
                _pos[i].text = PlayerLabels.Code(p.Role);
                _name[i].text = PlayerLabels.Surname(p.Name).ToUpperInvariant();
                float form = pv?.Form ?? 6f;
                _arrow[i].text = "→";
                _arrow[i].color = PlayerLabels.FormColor(form);
                _arrow[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, PlayerLabels.FormArrowDegrees(form));
                _ring[i].color = kit.Secondary;
                _body[i].color = kit.Shirt;
            }

            for (int i = 0; i < Tokens; i++)
            {
                _token[i].gameObject.SetActive(i < count);
            }

            _view.ShowFormationBoard(-dir * half, 0f, players, lines);
        }

        private void PlaceTokens()
        {
            TeamState team = _engine.Teams[_side];
            int count = Mathf.Min(Tokens, team.Players.Length);
            for (int i = 0; i < count; i++)
            {
                MatchPlayer p = team.Players[i];
                bool visible = _view.HeadOnScreen(p.Index, out Vector3 screen);
                if (_token[i].gameObject.activeSelf != visible)
                {
                    _token[i].gameObject.SetActive(visible);
                }

                if (visible)
                {
                    _token[i].anchoredPosition = UiKit.ToArea(new Vector2(screen.x, screen.y)) + new Vector2(0f, TokenLift);
                }
            }
        }
    }
}
