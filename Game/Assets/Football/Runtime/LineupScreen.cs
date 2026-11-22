using UnityEngine;
using UnityEngine.UI;
using Football.Core.Data;
using Football.Core.Domain;
using Football.Core.MatchCore;

namespace Football.Game
{
    /// <summary>
    /// Alineación: formación, titulares sobre el campo, suplentes y no convocados. Se toca un jugador y luego otro para
    /// intercambiarlos (entre cualquier grupo). Muestra la media efectiva si un jugador juega fuera de su posición.
    /// </summary>
    public sealed class LineupScreen : MenuScreenBase
    {
        private const int MaxBench = SquadSelection.DefaultBenchSize;
        private const int MaxReserves = 14;
        private const float PitchCenterX = 520f;
        private const float PitchCenterY = 505f;
        private const float PitchWidth = 820f;
        private const float PitchHeight = 790f;
        private const float BenchX = 1265f;
        private const float ReserveX = 1675f;
        private const float RowTop = 880f;
        private const float RowStep = 56f;

        private static readonly Color RowColor = new Color(0.16f, 0.2f, 0.26f, 0.95f);
        private static readonly Color OutOfPosition = new Color(1f, 0.6f, 0.3f);

        private sealed class Token
        {
            public UiButton Circle;
            public Text Role, Name;
        }

        private readonly FriendlySetup _setup;
        private readonly Token[] _tokens = new Token[11];
        private readonly UiButton[] _bench = new UiButton[MaxBench];
        private readonly UiButton[] _reserves = new UiButton[MaxReserves];
        private readonly Text _title, _formation, _rating, _reservesHeader;
        private readonly UiButton _formationPrevious, _formationNext, _auto, _done;
        private int _side;
        private SquadGroup _selectedGroup;
        private int _selectedIndex = -1;
        private readonly PlayerCardPanel _card;
        private readonly HoldDetector _hold = new HoldDetector();

        /// <summary>Se terminó de editar (volver a la pantalla de equipos).</summary>
        public bool Done { get; private set; }

        /// <summary>Crea la pantalla.</summary>
        public LineupScreen(Transform parent, PointerInput pointers, FriendlySetup setup) : base(parent, pointers, "AlineacionUI")
        {
            _setup = setup;
            _title = Title("ALINEACIÓN", "Toca un jugador y luego otro para intercambiarlos  ·  " + Locale.Get("card.hint"));
            _card = new PlayerCardPanel(parent);
            BuildPitch();

            _formationPrevious = UiKit.Button(Root, "FormacionAnterior", new Vector2(1180f, 990f), new Vector2(80f, 76f), "<", 40, UiTheme.Button);
            _formation = UiKit.Label(Root, "Formacion", new Vector2(1340f, 990f), new Vector2(220f, 76f), string.Empty, 40, UiTheme.Text);
            _formationNext = UiKit.Button(Root, "FormacionSiguiente", new Vector2(1500f, 990f), new Vector2(80f, 76f), ">", 40, UiTheme.Button);
            _rating = UiKit.Label(Root, "Media", new Vector2(1740f, 990f), new Vector2(300f, 76f), string.Empty, 30, UiTheme.TextDim);

            UiKit.Label(Root, "Suplentes", new Vector2(BenchX, 930f), new Vector2(380f, 44f), "SUPLENTES", 30, UiTheme.Accent);
            _reservesHeader = UiKit.Label(Root, "Reservas", new Vector2(ReserveX, 930f), new Vector2(380f, 44f), "NO CONVOCADOS", 30, UiTheme.Accent);
            for (int i = 0; i < MaxBench; i++)
            {
                _bench[i] = Row("Suplente" + i, BenchX, i);
            }

            for (int i = 0; i < MaxReserves; i++)
            {
                _reserves[i] = Row("Reserva" + i, ReserveX, i);
            }

            for (int i = 0; i < _tokens.Length; i++)
            {
                var token = new Token();
                token.Circle = UiKit.Button(Root, "Titular" + i, Vector2.zero, new Vector2(92f, 92f), string.Empty, 32, Color.white, true);
                token.Role = UiKit.Label(Root, "Puesto" + i, Vector2.zero, new Vector2(120f, 30f), string.Empty, 20, UiTheme.TextDim);
                token.Name = UiKit.Label(Root, "NombreTitular" + i, Vector2.zero, new Vector2(200f, 30f), string.Empty, 24, UiTheme.Text);
                _tokens[i] = token;
            }

            _auto = UiKit.Button(Root, "Automatica", new Vector2(BenchX, 55f), new Vector2(380f, 80f), "AUTOMÁTICA", 32, UiTheme.Button);
            _done = UiKit.Button(Root, "Listo", new Vector2(ReserveX, 55f), new Vector2(380f, 80f), "LISTO", 36, UiTheme.AccentDark);
        }

        private UiButton Row(string name, float x, int index)
        {
            UiButton row = UiKit.Button(Root, name, new Vector2(x, RowTop - index * RowStep), new Vector2(390f, 50f), string.Empty, 24, RowColor);
            row.Label.alignment = TextAnchor.MiddleLeft;
            return row;
        }

        private void BuildPitch()
        {
            var grass = new Color(0.1f, 0.36f, 0.16f, 0.95f);
            var line = new Color(1f, 1f, 1f, 0.35f);
            var center = new Vector2(PitchCenterX, PitchCenterY);
            UiKit.Panel(Root, "Cancha", center, new Vector2(PitchWidth, PitchHeight), grass);
            UiKit.Panel(Root, "LineaMedia", center, new Vector2(PitchWidth, 3f), line);
            UiKit.Panel(Root, "AreaPropia", center + new Vector2(0f, -PitchHeight * 0.5f + 70f), new Vector2(380f, 3f), line);
            UiKit.Panel(Root, "AreaRival", center + new Vector2(0f, PitchHeight * 0.5f - 70f), new Vector2(380f, 3f), line);
            UiKit.Label(Root, "Ataque", center + new Vector2(0f, PitchHeight * 0.5f - 22f), new Vector2(400f, 30f), "ATAQUE", 20, line);
        }

        /// <inheritdoc />
        public override void Hide()
        {
            base.Hide();
            _card?.Hide();
        }

        /// <summary>Muestra la alineación de un lado (0 tu equipo, 1 el rival).</summary>
        public void Show(int side)
        {
            _side = side;
            _selectedIndex = -1;
            Done = false;
            Show();
            Refresh();
        }

        private Vector2 SlotPosition(FormationSlot slot)
        {
            // Avance 0..0,75 de abajo (propia portería) hacia arriba; lado positivo = izquierda del equipo.
            float y = PitchCenterY - PitchHeight * 0.5f + 70f + Mathf.Clamp01(slot.X / 0.75f) * (PitchHeight - 150f);
            float x = PitchCenterX - slot.Y * (PitchWidth * 0.5f - 90f);
            return new Vector2(x, y);
        }

        private void Refresh()
        {
            TeamDefinition team = _setup.Definition(_side);
            SquadSelection squad = _setup.Squad(_side);
            KitColors kit = _setup.KitOf(_side);
            _title.text = "ALINEACIÓN · " + team.Name.ToUpperInvariant();
            _formation.text = squad.Formation.Name;

            float sum = 0f;
            int starters = squad.Formation.Slots.Length; // 11, o 3 en la cancha cerrada
            for (int i = 0; i < _tokens.Length; i++)
            {
                Token token = _tokens[i];
                bool used = i < starters;
                token.Circle.SetVisible(used);
                token.Role.gameObject.SetActive(used);
                token.Name.gameObject.SetActive(used);
                if (!used)
                {
                    continue;
                }

                PlayerDefinition p = team.Players[squad.Starters[i]];
                FormationSlot slot = squad.Formation.Slots[i];
                Vector2 pos = SlotPosition(slot);
                int effective = squad.EffectiveRating(team, i);
                sum += effective;
                token.Circle.Place(pos, token.Circle.Size);
                token.Circle.Background.color = IsSelected(SquadGroup.Starter, i) ? UiTheme.Accent : kit.Shirt;
                token.Circle.Label.text = effective.ToString();
                token.Circle.Label.color = effective < p.Rating ? OutOfPosition : ReadableOn(kit.Shirt);
                Place(token.Role, pos + new Vector2(0f, 62f));
                token.Role.text = UiTheme.Code(slot.Role);
                Place(token.Name, pos + new Vector2(0f, -64f));
                token.Name.text = TeamBuilder.DisplayName(p);
            }

            _rating.text = "Media " + (sum / starters).ToString("F1");
            FillRows(_bench, squad.Bench, SquadGroup.Bench, team);
            FillRows(_reserves, squad.Reserves, SquadGroup.Reserve, team);
            _reservesHeader.text = squad.Reserves.Count > MaxReserves ? "NO CONVOCADOS (" + squad.Reserves.Count + ")" : "NO CONVOCADOS";
        }

        private static void Place(Text label, Vector2 position)
        {
            ((RectTransform)label.transform).anchoredPosition = position;
        }

        private static Color ReadableOn(Color background)
        {
            float brightness = background.r * 0.3f + background.g * 0.59f + background.b * 0.11f;
            return brightness > 0.6f ? new Color(0.08f, 0.08f, 0.1f) : Color.white;
        }

        private void FillRows(UiButton[] rows, System.Collections.Generic.List<int> players, SquadGroup group, TeamDefinition team)
        {
            for (int i = 0; i < rows.Length; i++)
            {
                bool used = i < players.Count;
                rows[i].SetVisible(used);
                if (!used)
                {
                    continue;
                }

                PlayerDefinition p = team.Players[players[i]];
                rows[i].Label.text = "  " + p.Position.PadRight(4) + TeamBuilder.DisplayName(p) + "  ·  " + p.Rating;
                rows[i].Background.color = IsSelected(group, i) ? UiTheme.Selected : RowColor;
            }
        }

        private bool IsSelected(SquadGroup group, int index) => _selectedIndex == index && _selectedGroup == group;

        /// <summary>Procesa los toques del fotograma.</summary>
        public void Update()
        {
            Done = false;
            if (_card.Visible)
            {
                // Ficha abierta: cualquier toque (o atrás) la cierra.
                if (BackKey || Taps(out _) > 0)
                {
                    _card.Hide();
                }

                return;
            }

            if (BackKey)
            {
                Finish();
                return;
            }

            if (_hold.Update(Pointers, Time.unscaledDeltaTime, out Vector2 held)
                && HitPlayer(held, _setup.Squad(_side), out SquadGroup heldGroup, out int heldIndex))
            {
                // Mantener presionado: ficha del jugador (y se cancela el intercambio que había empezado con ese toque).
                _selectedIndex = -1;
                SquadSelection sq = _setup.Squad(_side);
                _card.Show(PlayerCards.FromDefinition(_setup.Definition(_side).Players[sq.Group(heldGroup)[heldIndex]]));
                Refresh();
                return;
            }

            int n = Taps(out Vector2[] taps);
            for (int t = 0; t < n; t++)
            {
                Vector2 p = taps[t];
                if (_done.Contains(p))
                {
                    Finish();
                    return;
                }

                TeamDefinition team = _setup.Definition(_side);
                SquadSelection squad = _setup.Squad(_side);
                if (_auto.Contains(p))
                {
                    _setup.ResetSquad(_side);
                    _selectedIndex = -1;
                }
                else if (_formationPrevious.Contains(p) || _formationNext.Contains(p))
                {
                    int step = _formationNext.Contains(p) ? 1 : -1;
                    Formation[] formations = _setup.AvailableFormations;
                    int current = System.Array.IndexOf(formations, squad.Formation);
                    int next = (current + step + formations.Length) % formations.Length;
                    squad.ChangeFormation(team, formations[next]);
                    _selectedIndex = -1;
                }
                else
                {
                    TrySelect(p, squad);
                }

                Refresh();
            }
        }

        private void TrySelect(Vector2 point, SquadSelection squad)
        {
            SquadGroup group;
            int index;
            if (!HitPlayer(point, squad, out group, out index))
            {
                return;
            }

            if (_selectedIndex < 0)
            {
                _selectedGroup = group;
                _selectedIndex = index;
                return;
            }

            if (_selectedGroup != group || _selectedIndex != index)
            {
                squad.Swap(_selectedGroup, _selectedIndex, group, index);
            }

            _selectedIndex = -1;
        }

        private bool HitPlayer(Vector2 point, SquadSelection squad, out SquadGroup group, out int index)
        {
            for (int i = 0; i < _tokens.Length; i++)
            {
                if (_tokens[i].Circle.Contains(point))
                {
                    group = SquadGroup.Starter;
                    index = i;
                    return true;
                }
            }

            for (int i = 0; i < squad.Bench.Count && i < _bench.Length; i++)
            {
                if (_bench[i].Contains(point))
                {
                    group = SquadGroup.Bench;
                    index = i;
                    return true;
                }
            }

            for (int i = 0; i < squad.Reserves.Count && i < _reserves.Length; i++)
            {
                if (_reserves[i].Contains(point))
                {
                    group = SquadGroup.Reserve;
                    index = i;
                    return true;
                }
            }

            group = SquadGroup.Starter;
            index = -1;
            return false;
        }

        private void Finish()
        {
            _setup.SaveSquad(_side);
            Done = true;
            Hide();
        }
    }
}
