using Football.Core.Data;
using Football.Core.Domain;
using Football.Core.MatchCore;
using UnityEngine;
using UnityEngine.UI;

namespace Football.Game
{
    /// <summary>
    /// Presentación previa al pitido inicial (solo 11 contra 11): primero el equipo de la persona y después el rival, cada uno con la misma
    /// pantalla de alineación que se elige antes del partido (<see cref="LineupBoard"/>): la cancha con los once, el dibujo, la media y los
    /// suplentes, sobre un fondo oscuro. Botón SALTAR abajo a la derecha. Solo presenta: no toca el motor. Sin objetos nuevos por fotograma
    /// (las fichas se crean una vez y se reubican al cambiar de equipo).
    /// </summary>
    public sealed class TacticalPresentation
    {
        private const int Tokens = 11;
        private const int BenchRows = 9;
        private const float SideSeconds = 5f;

        private readonly GameObject _root;
        private readonly LineupBoard _board;
        private readonly UiButton _skip;
        private readonly Text _side2;
        private MatchEngine _engine;
        private MatchView _view;
        private MatchTeamSetup[] _setups;
        private TeamDefinition[] _teams;
        private KitColors[] _kits;
        private float _time;
        private int _side = -1;
        private bool _hostIsSecondTeam;

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

            UiKit.Panel(go.transform, "Fondo", UiKit.Reference * 0.5f, UiKit.FullScreenSize, new Color(0.059f, 0.102f, 0.09f, 1f));
            _board = new LineupBoard(go.transform, BenchRows, 0, Vector2.one, Vector2.zero, 1f, false);
            _board.FormationPrevious.SetVisible(false);
            _board.FormationNext.SetVisible(false);
            _board.ReservesHeader.gameObject.SetActive(false);
            _side2 = _board.Hint;
            _skip = UiKit.Button(go.transform, "Saltar", new Vector2(1675f, 55f), new Vector2(380f, 80f), "SALTAR  ▶", 34, UiTheme.Accent);
            _skip.Label.color = UiTheme.OnAccent;
            _root.SetActive(false);
        }

        /// <summary>Empieza la presentación (primero el equipo de la persona; el rótulo LOCAL/VISITANTE sigue al dueño de casa).</summary>
        /// <param name="engine">Motor.</param>
        /// <param name="view">Vista del partido.</param>
        /// <param name="home">Equipo local.</param>
        /// <param name="away">Equipo visitante.</param>
        /// <param name="homeTeam">Datos del local.</param>
        /// <param name="awayTeam">Datos del visitante.</param>
        /// <param name="homeKit">Camiseta del local.</param>
        /// <param name="awayKit">Camiseta del visitante.</param>
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

        /// <summary>Avanza la presentación. Devuelve falso cuando terminó o se saltó.</summary>
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
            MatchTeamSetup setup = _setups[side];
            Color shirt = _kits[side].Shirt;
            Color ink = LineupBoard.ReadableOn(shirt);
            _board.Title.text = "ALINEACIÓN · " + _teams[side].Name.ToUpperInvariant();
            _side2.text = (side == 0) != _hostIsSecondTeam ? "LOCAL" : "VISITANTE";
            _board.Formation.text = team.Formation.Name;

            int count = Mathf.Min(Tokens, setup.Players.Length);
            float sum = 0f;
            for (int i = 0; i < count; i++)
            {
                PlayerMatchView p = setup.Players[i];
                FormationSlot slot = team.Formation.Slots[i];
                int rating = Mathf.RoundToInt(RosterFactory.DisplayRating(p.Attributes, slot.Role));
                sum += rating;
                _board.SetToken(i, _board.SlotPosition(slot.X, slot.Y), PlayerLabels.Code(slot.Role), PlayerLabels.Surname(p.Name), rating, shirt, ink);
            }

            _board.HideTokensFrom(count);
            _board.Rating.text = "Media " + (sum / Mathf.Max(1, count)).ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
            PlayerMatchView[] bench = setup.Bench ?? new PlayerMatchView[0];
            for (int i = 0; i < _board.Bench.Length; i++)
            {
                bool used = i < bench.Length;
                _board.Bench[i].SetVisible(used);
                if (used)
                {
                    PlayerMatchView p = bench[i];
                    _board.Bench[i].Label.text = "  " + PlayerLabels.Code(p.Role).PadRight(4) + PlayerLabels.Surname(p.Name) + "  ·  "
                        + Mathf.RoundToInt(RosterFactory.DisplayRating(p.Attributes, p.Role));
                }
            }
        }
    }
}
