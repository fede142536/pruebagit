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
        private const float FieldX = 560f;
        private const float BenchX = 1360f;
        private const float RowTop = 760f;
        private const float RowStep = 56f;

        private static readonly Color RowColor = new Color(0.16f, 0.18f, 0.24f, 0.95f);
        private static readonly Color Selected = new Color(0.12f, 0.42f, 0.38f, 0.95f);
        private static readonly Color Pending = new Color(0.45f, 0.35f, 0.1f, 0.95f);
        private static readonly Color Unavailable = new Color(0.12f, 0.12f, 0.14f, 0.9f);

        private readonly GameObject _root;
        private readonly UiButton[] _field = new UiButton[MaxRows];
        private readonly UiButton[] _bench = new UiButton[MaxBench];
        private readonly UiButton _formationPrevious, _formationNext, _back;
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
            Image back = UiKit.Panel(parent, "PlantelTactica", UiKit.Reference * 0.5f, UiKit.Reference, new Color(0.03f, 0.04f, 0.07f, 0.97f));
            _root = back.gameObject;
            Transform t = back.transform;
            UiKit.Label(t, "Titulo", new Vector2(960f, 1010f), new Vector2(1200f, 70f), "PLANTEL Y TÁCTICA", 48, Color.white);
            _formationPrevious = UiKit.Button(t, "FormacionAnterior", new Vector2(760f, 920f), new Vector2(90f, 70f), "‹", 48, UiTheme.Button);
            _formation = UiKit.Label(t, "Formacion", new Vector2(960f, 920f), new Vector2(300f, 70f), string.Empty, 40, UiTheme.Accent);
            _formationNext = UiKit.Button(t, "FormacionSiguiente", new Vector2(1160f, 920f), new Vector2(90f, 70f), "›", 48, UiTheme.Button);
            _status = UiKit.Label(t, "Cupos", new Vector2(960f, 850f), new Vector2(1400f, 44f), string.Empty, 28, Color.white);
            UiKit.Label(t, "EnCampo", new Vector2(FieldX, 805f), new Vector2(700f, 36f), "EN EL CAMPO (toca uno y luego otro para cambiar puestos)", 20, UiTheme.TextDim);
            UiKit.Label(t, "Suplentes", new Vector2(BenchX, 805f), new Vector2(700f, 36f), "SUPLENTES (toca uno del campo y luego un suplente)", 20, UiTheme.TextDim);
            for (int i = 0; i < MaxRows; i++)
            {
                _field[i] = Row(t, "Campo" + i, FieldX, i);
            }

            for (int i = 0; i < MaxBench; i++)
            {
                _bench[i] = Row(t, "Banco" + i, BenchX, i);
            }

            _message = UiKit.Label(t, "Aviso", new Vector2(960f, 75f), new Vector2(1300f, 40f), string.Empty, 26, UiTheme.Warning);
            _back = UiKit.Button(t, "Volver", new Vector2(1700f, 75f), new Vector2(300f, 80f), "VOLVER", 34, UiTheme.AccentDark);
            _root.SetActive(false);
        }

        private static UiButton Row(Transform parent, string name, float x, int index)
        {
            UiButton row = UiKit.Button(parent, name, new Vector2(x, RowTop - index * RowStep), new Vector2(720f, 50f), string.Empty, 24, RowColor);
            row.Label.alignment = TextAnchor.MiddleLeft;
            return row;
        }

        /// <summary>Abre el panel para el partido en curso (equipo 0, el de la persona).</summary>
        public void Show(MatchEngine engine)
        {
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

            for (int i = 0; i < team.Players.Length && i < MaxRows; i++)
            {
                if (!_field[i].Contains(point))
                {
                    continue;
                }

                if (_selected < 0 || _selected == i)
                {
                    _selected = _selected == i ? -1 : i;
                }
                else
                {
                    _message.text = _engine.SwapPositions(team.Players[_selected], team.Players[i])
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

        private void Refresh()
        {
            TeamState team = _engine.Teams[0];
            RulesConfig rules = _engine.Config.Rules;
            _formation.text = team.Formation.Name;
            string window = _engine.InHalfTimeBreak ? "entretiempo: no gasta ventana" : "ventanas disponibles: " + _engine.WindowsLeft(0) + "/" + rules.MaxSubstitutionWindows;
            _status.text = "Cambios restantes: " + _engine.SubstitutionsLeft(0) + "/" + rules.MaxSubstitutions + "   ·   " + window;

            for (int i = 0; i < MaxRows; i++)
            {
                bool used = i < team.Players.Length;
                _field[i].SetVisible(used);
                if (!used)
                {
                    continue;
                }

                MatchPlayer p = team.Players[i];
                bool pending = _engine.IsPendingOut(0, p.Index);
                _field[i].Label.text = "  " + p.Number + "  " + p.Name + "   ·   " + UiTheme.Code(p.Role) + "   ·   resistencia "
                                       + Mathf.RoundToInt(p.Stamina * 100f) + "%" + (pending ? "   (SALE)" : string.Empty) + (p.SentOff ? "   (EXPULSADO)" : string.Empty) + (p.Injured ? "   (LESIONADO)" : string.Empty);
                _field[i].Background.color = i == _selected ? Selected : pending ? Pending : p.SentOff ? Unavailable : RowColor;
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
                _bench[i].Label.text = "  " + b.Number + "  " + b.Name + "   ·   " + UiTheme.Code(b.Role) + (unavailable ? "   (USADO)" : string.Empty);
                _bench[i].Background.color = unavailable ? Unavailable : RowColor;
            }
        }
    }
}
