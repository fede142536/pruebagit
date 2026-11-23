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
        private static readonly Color OutOfPosition = new Color(1f, 0.6f, 0.3f);
        private static readonly Color RowColor = UiTheme.Button;

        private readonly FriendlySetup _setup;
        private readonly LineupBoard _board;
        private readonly LineupBoard.Token[] _tokens;
        private readonly UiButton[] _bench, _reserves;
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
            _board = new LineupBoard(Root, MaxBench, MaxReserves, Vector2.one, Vector2.zero, 1f, true);
            _card = new PlayerCardPanel(parent);
            _title = _board.Title;
            _board.Hint.text = "Toca un jugador y luego otro para intercambiarlos  ·  " + Locale.Get("card.hint");
            _tokens = _board.Tokens;
            _bench = _board.Bench;
            _reserves = _board.Reserves;
            _formation = _board.Formation;
            _rating = _board.Rating;
            _reservesHeader = _board.ReservesHeader;
            _formationPrevious = _board.FormationPrevious;
            _formationNext = _board.FormationNext;
            _auto = _board.Auto;
            _done = _board.Done;
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

        private void Refresh()
        {
            TeamDefinition team = _setup.Definition(_side);
            SquadSelection squad = _setup.Squad(_side);
            KitColors kit = _setup.KitOf(_side);
            _title.text = "ALINEACIÓN · " + team.Name.ToUpperInvariant();
            _formation.text = squad.Formation.Name;

            float sum = 0f;
            int starters = squad.Formation.Slots.Length; // 11, o 3 en la cancha cerrada
            _board.HideTokensFrom(starters);
            for (int i = 0; i < starters && i < _tokens.Length; i++)
            {
                PlayerDefinition p = team.Players[squad.Starters[i]];
                FormationSlot slot = squad.Formation.Slots[i];
                int effective = squad.EffectiveRating(team, i);
                sum += effective;
                Color fill = IsSelected(SquadGroup.Starter, i) ? UiTheme.Accent : kit.Shirt;
                Color ink = effective < p.Rating ? OutOfPosition : LineupBoard.ReadableOn(kit.Shirt);
                _board.SetToken(i, _board.SlotPosition(slot.X, slot.Y), UiTheme.Code(slot.Role), TeamBuilder.DisplayName(p), effective, fill, ink);
            }

            _rating.text = "Media " + (sum / starters).ToString("F1");
            FillRows(_bench, squad.Bench, SquadGroup.Bench, team);
            FillRows(_reserves, squad.Reserves, SquadGroup.Reserve, team);
            _reservesHeader.text = squad.Reserves.Count > MaxReserves ? "NO CONVOCADOS (" + squad.Reserves.Count + ")" : "NO CONVOCADOS";
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
