using Football.Core.MatchCore;
using UnityEngine;
using UnityEngine.UI;
using Football.Core.Domain;

namespace Football.Game
{
    /// <summary>
    /// Pausa → PLANTEL Y TÁCTICA: formación en vivo, cambios y puestos. Toca un jugador del campo y después:
    /// otro del campo para intercambiar sus puestos, o un suplente para hacer el cambio (se hace en el próximo parón).
    /// Muestra cambios y ventanas que quedan y bloquea lo que el reglamento no permite. Todo el estado vive en el motor.
    /// </summary>
    public sealed class TacticsPanel
    {
        private const int MaxRows = 11;
        private const int MaxBench = 12;
        private const float FieldX = 330f;
        private const float BenchX = 1590f;
        private const float RowWidth = 540f;
        private const float PitchCenterX = 960f;
        private const float PitchCenterY = 465f;
        private const float PitchWidth = 420f;
        private const float PitchHeight = 620f;
        private const float PitchMargin = 0.9f;     // las bandas y las líneas de fondo no se usan: los puntos quedan dentro
        private const float DotSize = 54f;
        private const float DotRing = 5f;
        private const float LineWidth = 3f;
        private const float RowTop = 760f;
        private const float RowStep = 56f;

        private static readonly Color RowColor = UiTheme.Button;
        private static readonly Color Selected = UiTheme.Selected;
        private static readonly Color Pending = new Color(0.30f, 0.24f, 0.08f, 0.95f);
        private static readonly Color PitchColor = new Color(0.10f, 0.36f, 0.16f, 1f); // la misma cancha verde de la alineación
        private static readonly Color LineTint = new Color(1f, 1f, 1f, 0.55f);
        private static readonly Color KeeperDot = new Color(0.85f, 0.65f, 0.10f, 1f);
        private static readonly Color DefenderDot = new Color(0.18f, 0.40f, 0.80f, 1f);
        private static readonly Color MidfielderDot = new Color(0.20f, 0.62f, 0.55f, 1f);
        private static readonly Color AttackerDot = new Color(0.80f, 0.25f, 0.22f, 1f);
        private static readonly Color Unavailable = new Color(0.07f, 0.1f, 0.09f, 0.9f);
        private Color _kit = new Color(0.8f, 0.2f, 0.2f);

        private readonly GameObject _root;
        private readonly UiButton[] _field = new UiButton[MaxRows];
        private readonly UiButton[] _bench = new UiButton[MaxBench];
        private readonly Image[] _dotRings = new Image[MaxRows];            // anillo de cada punto: selección, cambio pedido o expulsado
        private readonly UiButton[] _dots = new UiButton[MaxRows];     // los mismos jugadores sobre la cancha, en su puesto de la formación
        private readonly Text[] _dotNames = new Text[MaxRows];
        private readonly UiButton _formationPrevious, _formationNext, _back, _defensive, _corners, _marking;
        private static readonly string[] CornerLabels = { "MIXTO", "PRIMER PALO", "PUNTO PENAL", "CORTO" };
        private readonly Text _formation, _status, _message;
        private MatchEngine _engine;
        private int _selected = -1;

        /// <summary>Se pidió volver al menú de pausa.</summary>
        public bool BackRequested { get; private set; }

        /// <summary>Está abierto.</summary>
        public bool Visible => _root.activeSelf;

        /// <summary>Crea el panel (oculto) dentro del menú de pausa.</summary>
        public TacticsPanel(Transform parent)
        {
            Image back = UiKit.Panel(parent, "PlantelTactica", UiKit.Reference * 0.5f, UiKit.Reference, new Color(0.059f, 0.102f, 0.09f, 0.98f));
            _root = back.gameObject;
            Transform t = back.transform;
            UiKit.Panel(t, "Acento", new Vector2(68f, 1010f), new Vector2(8f, 64f), UiTheme.Accent);
            UiKit.Label(t, "Titulo", new Vector2(600f, 1010f), new Vector2(1000f, 70f), "PLANTEL Y TÁCTICA", 48, UiTheme.Text, TextAnchor.MiddleLeft);
            _formationPrevious = UiKit.Button(t, "FormacionAnterior", new Vector2(760f, 920f), new Vector2(90f, 70f), "‹", 48, UiTheme.Button);
            _formation = UiKit.Label(t, "Formacion", new Vector2(960f, 920f), new Vector2(300f, 70f), string.Empty, 40, UiTheme.AccentStrong);
            _formationNext = UiKit.Button(t, "FormacionSiguiente", new Vector2(1160f, 920f), new Vector2(90f, 70f), "›", 48, UiTheme.Button);
            // Doble dibujo y pelota parada.
            _defensive = UiKit.Button(t, "SinBalon", new Vector2(300f, 920f), new Vector2(440f, 60f), string.Empty, 24, UiTheme.Button);
            _corners = UiKit.Button(t, "Corners", new Vector2(1620f, 955f), new Vector2(440f, 52f), string.Empty, 22, UiTheme.Button);
            _marking = UiKit.Button(t, "Marca", new Vector2(1620f, 895f), new Vector2(440f, 52f), string.Empty, 22, UiTheme.Button);
            _status = UiKit.Label(t, "Cupos", new Vector2(960f, 850f), new Vector2(1400f, 44f), string.Empty, 28, UiTheme.Text);
            UiKit.Label(t, "EnCampo", new Vector2(FieldX, 805f), new Vector2(RowWidth, 36f), "EN EL CAMPO · toca uno y luego otro para cambiar puestos", 17, UiTheme.AccentText);
            UiKit.Label(t, "Suplentes", new Vector2(BenchX, 805f), new Vector2(RowWidth, 36f), "SUPLENTES · toca uno del campo y luego un suplente", 17, UiTheme.AccentText);
            BuildPitch(t);
            for (int i = 0; i < MaxRows; i++)
            {
                _field[i] = Row(t, "Campo" + i, FieldX, i);
            }

            for (int i = 0; i < MaxBench; i++)
            {
                _bench[i] = Row(t, "Banco" + i, BenchX, i);
            }

            for (int i = 0; i < MaxRows; i++)
            {
                _dotRings[i] = UiKit.Panel(t, "AnilloPunto" + i, Vector2.zero, new Vector2(DotSize + 2f * DotRing, DotSize + 2f * DotRing), Color.clear, true);
                _dots[i] = UiKit.Button(t, "Punto" + i, Vector2.zero, new Vector2(DotSize, DotSize), string.Empty, 22, DefenderDot, true);
                _dotNames[i] = UiKit.Label(t, "NombrePunto" + i, Vector2.zero, new Vector2(140f, 24f), string.Empty, 16, Color.white);
            }

            _message = UiKit.Label(t, "Aviso", new Vector2(960f, 75f), new Vector2(1300f, 40f), string.Empty, 26, UiTheme.Warning);
            _back = UiKit.Button(t, "Volver", new Vector2(1700f, 75f), new Vector2(300f, 80f), "VOLVER", 34, UiTheme.AccentDark);
            _root.SetActive(false);
        }

        /// <summary>Cancha vertical (ataque hacia arriba) con sus líneas, donde se dibujan los jugadores en su puesto.</summary>
        private static void BuildPitch(Transform t)
        {
            var center = new Vector2(PitchCenterX, PitchCenterY);
            UiKit.Panel(t, "Cancha", center, new Vector2(PitchWidth, PitchHeight), PitchColor);
            Outline(t, "Borde", center, new Vector2(PitchWidth, PitchHeight));
            UiKit.Panel(t, "Mitad", center, new Vector2(PitchWidth, LineWidth), LineTint);
            UiKit.Panel(t, "CirculoMedio", center, new Vector2(PitchWidth * 0.26f, PitchWidth * 0.26f), LineTint, true);
            UiKit.Panel(t, "CirculoMedioInterior", center, new Vector2(PitchWidth * 0.26f - 2f * LineWidth, PitchWidth * 0.26f - 2f * LineWidth), PitchColor, true);
            Vector2 area = new Vector2(PitchWidth * 0.58f, PitchHeight * 0.16f);
            Vector2 small = new Vector2(PitchWidth * 0.28f, PitchHeight * 0.06f);
            for (int end = -1; end <= 1; end += 2)
            {
                float edge = PitchCenterY + end * PitchHeight * 0.5f;
                Outline(t, "Area" + end, new Vector2(PitchCenterX, edge - end * area.y * 0.5f), area);
                Outline(t, "AreaChica" + end, new Vector2(PitchCenterX, edge - end * small.y * 0.5f), small);
            }
        }

        private static void Outline(Transform t, string name, Vector2 center, Vector2 size)
        {
            UiKit.Panel(t, name + "N", center + new Vector2(0f, size.y * 0.5f), new Vector2(size.x, LineWidth), LineTint);
            UiKit.Panel(t, name + "S", center - new Vector2(0f, size.y * 0.5f), new Vector2(size.x, LineWidth), LineTint);
            UiKit.Panel(t, name + "O", center - new Vector2(size.x * 0.5f, 0f), new Vector2(LineWidth, size.y), LineTint);
            UiKit.Panel(t, name + "E", center + new Vector2(size.x * 0.5f, 0f), new Vector2(LineWidth, size.y), LineTint);
        }

        /// <summary>Punto de la cancha donde va un puesto de la formación (ataque hacia arriba; Y positivo es la izquierda del equipo).</summary>
        private static Vector2 PitchPoint(FormationSlot slot)
        {
            float x = PitchCenterX - slot.Y * PitchWidth * 0.5f * PitchMargin;
            float y = PitchCenterY - PitchHeight * 0.5f * PitchMargin + slot.X * PitchHeight * PitchMargin;
            return new Vector2(x, y);
        }

        private static Color LineColor(Position role)
        {
            switch (PlayerRatings.LineOf(role))
            {
                case PitchLine.Goalkeeper: return KeeperDot;
                case PitchLine.Defence: return DefenderDot;
                case PitchLine.Midfield: return MidfielderDot;
                default: return AttackerDot;
            }
        }

        /// <summary>Índice del jugador del campo cuya fila o punto está en el punto, o -1.</summary>
        private int FieldAt(Vector2 point)
        {
            int count = Mathf.Min(_engine.Teams[0].Players.Length, MaxRows);
            for (int i = 0; i < count; i++)
            {
                if (_field[i].Contains(point) || _dots[i].Contains(point))
                {
                    return i;
                }
            }

            return -1;
        }

        private static UiButton Row(Transform parent, string name, float x, int index)
        {
            UiButton row = UiKit.Button(parent, name, new Vector2(x, RowTop - index * RowStep), new Vector2(RowWidth, 50f), string.Empty, 22, RowColor);
            row.Label.alignment = TextAnchor.MiddleLeft;
            return row;
        }

        /// <summary>Abre el panel para el partido en curso (equipo 0, el de la persona).</summary>
        /// <param name="kit">Color de la camiseta del equipo: las fichas de la cancha se pintan con él, como en la pantalla de alineación.</param>
        public void Show(MatchEngine engine, Color kit)
        {
            _kit = kit;
            _engine = engine;
            _selected = -1;
            BackRequested = false;
            _message.text = string.Empty;
            _root.SetActive(true);
            Refresh();
        }

        /// <summary>Cierra el panel.</summary>
        public void Hide()
        {
            _root.SetActive(false);
        }

        /// <summary>Ficha del jugador (de la cancha o del banco) que hay en un punto, o null.</summary>
        /// <param name="point">Punto en el área de diseño.</param>
        public PlayerCardInfo CardAt(Vector2 point)
        {
            TeamState team = _engine.Teams[0];
            int onField = FieldAt(point);
            if (onField >= 0)
            {
                _selected = -1;
                Refresh();
                return PlayerCards.FromMatch(team.Players[onField]);
            }

            for (int i = 0; i < team.Bench.Length && i < MaxBench; i++)
            {
                if (_bench[i].Contains(point))
                {
                    return PlayerCards.FromView(team.Bench[i]);
                }
            }

            return null;
        }

        /// <summary>Procesa un toque (en coordenadas del área de diseño).</summary>
        public void Tap(Vector2 point)
        {
            if (_back.Contains(point))
            {
                BackRequested = true;
                return;
            }

            TeamState team = _engine.Teams[0];
            if (_formationPrevious.Contains(point) || _formationNext.Contains(point))
            {
                CycleFormation(_formationNext.Contains(point) ? 1 : -1);
            }
            else if (_defensive.Contains(point))
            {
                CycleDefensiveFormation();
            }
            else if (_corners.Contains(point))
            {
                _engine.SetSetPieces(0, (CornerRoutine)(((int)team.Corners + 1) % CornerLabels.Length), team.Marking);
            }
            else if (_marking.Contains(point))
            {
                _engine.SetSetPieces(0, team.Corners,
                    team.Marking == SetPieceMarking.Zonal ? SetPieceMarking.ManToMan : SetPieceMarking.Zonal);
            }

            int tapped = FieldAt(point);
            if (tapped >= 0)
            {
                if (_selected < 0 || _selected == tapped)
                {
                    _selected = _selected == tapped ? -1 : tapped;
                }
                else
                {
                    _message.text = _engine.SwapPositions(team.Players[_selected], team.Players[tapped])
                        ? "Puestos intercambiados."
                        : "El arquero no cambia de puesto con un jugador de campo.";
                    _selected = -1;
                }
            }

            for (int i = 0; i < team.Bench.Length && i < MaxBench; i++)
            {
                if (!_bench[i].Contains(point))
                {
                    continue;
                }

                if (_selected < 0)
                {
                    _message.text = "Primero toca al jugador del campo que sale.";
                    continue;
                }

                SubstitutionResult result = _engine.RequestSubstitution(0, team.Players[_selected], i);
                _message.text = Describe(result);
                _selected = -1;
            }

            Refresh();
        }

        private void CycleFormation(int step)
        {
            TeamState team = _engine.Teams[0];
            Formation[] list = team.Players.Length == Formations.Indoor3.Slots.Length ? Formations.Indoor : Formations.All;
            int current = System.Array.IndexOf(list, team.Formation);
            Formation next = list[((current < 0 ? 0 : current) + step + list.Length) % list.Length];
            _engine.SetFormation(0, next);
        }

        /// <summary>Formación sin balón: la misma que con balón, o cualquiera de las otras con los mismos puestos.</summary>
        private void CycleDefensiveFormation()
        {
            TeamState team = _engine.Teams[0];
            Formation[] list = team.Players.Length == Formations.Indoor3.Slots.Length ? Formations.Indoor : Formations.All;
            Formation current = team.DefensiveFormation ?? team.Formation;
            for (int k = 1; k <= list.Length; k++)
            {
                Formation next = list[(System.Array.IndexOf(list, current) + k + list.Length) % list.Length];
                if (_engine.SetDefensiveFormation(0, next == team.Formation ? null : next))
                {
                    return;
                }
            }
        }

        private string Describe(SubstitutionResult r)
        {
            switch (r)
            {
                case SubstitutionResult.Accepted:
                    return _engine.Phase == MatchPhase.Playing ? "Cambio pedido: se hará en el próximo parón." : "Cambio hecho.";
                case SubstitutionResult.NoSubstitutionsLeft: return "No quedan cambios (máximo " + _engine.Config.Rules.MaxSubstitutions + ").";
                case SubstitutionResult.NoWindowsLeft: return "No quedan ventanas de cambios (los del entretiempo no cuentan).";
                case SubstitutionResult.PlayerUnavailable: return "Ese suplente ya entró o ya está pedido.";
                default: return "Ese jugador no puede salir (expulsado o ya sustituido).";
            }
        }

        /// <summary>Coloca el punto del jugador en el puesto táctico que ocupa ahora (cambia con la formación y los intercambios).</summary>
        private void PlaceDot(int i, MatchPlayer p, bool pending)
        {
            TeamState team = _engine.Teams[0];
            Vector2 at = PitchPoint(team.Formation.Slots[p.Spot]);
            _dots[i].Place(at, new Vector2(DotSize, DotSize));
            ((RectTransform)_dotNames[i].transform).anchoredPosition = at - new Vector2(0f, DotSize * 0.5f + 10f);
            PlayerMatchView incoming = pending ? _engine.PendingIncoming(0, p.Index) : null;
            _dots[i].Label.text = Mathf.RoundToInt(RosterFactory.DisplayRating(incoming != null ? incoming.Attributes : p.Attr, p.Role)).ToString();
            _dots[i].Label.color = LineupBoard.ReadableOn(_kit);
            _dotNames[i].text = PlayerLabels.Surname(incoming != null ? incoming.Name : p.Name);
            _dots[i].Background.color = _kit; // siempre del color del club (número en blanco o negro); el estado va en el anillo
            Color? ring = i == _selected ? UiTheme.Accent : pending ? LineupBoard.OffPositionRing : p.SentOff ? LineupBoard.UnavailableRing : (Color?)null;
            _dotRings[i].gameObject.SetActive(ring.HasValue);
            if (ring.HasValue)
            {
                _dotRings[i].color = ring.Value;
                _dotRings[i].rectTransform.anchoredPosition = at;
            }
        }

        private void Refresh()
        {
            TeamState team = _engine.Teams[0];
            RulesConfig rules = _engine.Config.Rules;
            _formation.text = team.Formation.Name;
            _defensive.Label.text = "SIN BALÓN: " + (team.DefensiveFormation != null ? team.DefensiveFormation.Name : "IGUAL");
            _corners.Label.text = "CÓRNERS: " + CornerLabels[(int)team.Corners];
            _marking.Label.text = "MARCA EN PELOTA PARADA: " + (team.Marking == SetPieceMarking.Zonal ? "ZONAL" : "INDIVIDUAL");
            string window = _engine.InHalfTimeBreak ? "entretiempo: no gasta ventana" : "ventanas disponibles: " + _engine.WindowsLeft(0) + "/" + rules.MaxSubstitutionWindows;
            _status.text = "Cambios restantes: " + _engine.SubstitutionsLeft(0) + "/" + rules.MaxSubstitutions + "   ·   " + window;

            for (int i = 0; i < MaxRows; i++)
            {
                bool used = i < team.Players.Length;
                _field[i].SetVisible(used);
                _dots[i].SetVisible(used);
                if (!used)
                {
                    _dotRings[i].gameObject.SetActive(false);
                }

                _dotNames[i].gameObject.SetActive(used);
                if (!used)
                {
                    continue;
                }

                MatchPlayer p = team.Players[i];
                bool pending = _engine.IsPendingOut(0, p.Index);
                PlayerMatchView incoming = pending ? _engine.PendingIncoming(0, p.Index) : null;
                if (incoming != null)
                {
                    // Cambio pedido: el que entra ya aparece en su puesto (entra en el próximo parón) y el que sale queda anotado.
                    _field[i].Label.text = "  " + incoming.Number + "  " + incoming.Name + "  ·  " + UiTheme.Code(p.Role) + "  ·  ENTRA POR " + PlayerLabels.Surname(p.Name);
                }
                else
                {
                    _field[i].Label.text = "  " + p.Number + "  " + p.Name + "  ·  " + UiTheme.Code(p.Role) + "  ·  RES "
                                           + Mathf.RoundToInt(p.Stamina * 100f) + "%" + (pending ? "  (SALE)" : string.Empty) + (p.SentOff ? "  (EXPULSADO)" : string.Empty) + (p.Injured ? "  (LESIONADO)" : string.Empty);
                }
                _field[i].Background.color = i == _selected ? Selected : pending ? Pending : p.SentOff ? Unavailable : RowColor;
                PlaceDot(i, p, pending);
            }

            for (int i = 0; i < MaxBench; i++)
            {
                bool used = i < team.Bench.Length;
                _bench[i].SetVisible(used);
                if (!used)
                {
                    continue;
                }

                PlayerMatchView b = team.Bench[i];
                bool unavailable = team.BenchUsed[i];
                bool entering = _engine.IsPendingIn(0, i);
                _bench[i].Label.text = "  " + b.Number + "  " + b.Name + "   ·   " + UiTheme.Code(b.Role) + (entering ? "   (ENTRA)" : unavailable ? "   (USADO)" : string.Empty);
                _bench[i].Background.color = entering ? Pending : unavailable ? Unavailable : RowColor;
            }
        }
    }
}
