using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Football.Core.Domain;
using Football.Core.Management;
using Football.Core.MatchCore;
using UnityEngine;
using UnityEngine.UI;

namespace Football.Game
{
    /// <summary>
    /// Alineación del DT en el modo carrera: a la izquierda la cancha con el once inicial en los puestos de la formación, en el centro los
    /// suplentes (hasta nueve) y a la derecha los jugadores no convocados. Se toca un jugador y después otro para intercambiarlos
    /// (titular con titular cambia los puestos; titular con suplente o con no convocado, el que juega; suplente con no convocado, el banco).
    /// Cada cambio se guarda en la partida y es la alineación con la que sale el equipo; los lesionados y sancionados que se dejen en el once
    /// los reemplaza el partido. «AUTOMÁTICA» devuelve la elección a la computadora.
    /// </summary>
    public sealed class CareerLineupScreen : CareerPage
    {
        private const int MaxStarters = 11;
        private const int MaxBench = MatchPreparation.BenchSize;
        private const int ReserveRows = 9;
        private static readonly string[] FormationNames = { "4-4-2", "4-3-3", "4-2-3-1", "4-3-1-2" };
        private static readonly Color Unavailable = new Color(0.45f, 0.14f, 0.12f, 1f);
        private static readonly Color OutOfPositionInk = new Color(1f, 0.6f, 0.3f);
        private static readonly Vector2 BoardScale = new Vector2(1f, 0.84f), BoardOffset = new Vector2(0f, 40f);
        private const float BoardSize = 0.84f;

        private enum Group { Starter, Bench, Reserve }

        private readonly LineupBoard _board;
        private readonly UiButton _prev, _next;
        private readonly UiButton[] _bench, _reserves;
        private readonly Text _mode, _page;
        private readonly List<int> _starterIds = new List<int>(), _benchIds = new List<int>(), _reserveIds = new List<int>();
        private Group _selectedGroup;
        private int _selectedIndex = -1;
        private int _reserveOffset;

        /// <summary>Crea la pantalla (oculta).</summary>
        public CareerLineupScreen(Transform root, ICareerHost host) : base(root, "Alineacion", host)
        {
            Transform p = Panel;
            // Misma vista que la alineación del amistoso (cancha con fichas, suplentes y no convocados), ajustada al alto que deja la barra de la carrera.
            _board = new LineupBoard(p, MaxBench, ReserveRows, BoardScale, BoardOffset, BoardSize, true);
            _board.Title.text = Locale.Get("lineup.title");
            _board.Hint.text = Locale.Get("lineup.hint");
            _bench = _board.Bench;
            _reserves = _board.Reserves;
            _board.Auto.Label.text = Locale.Get("lineup.auto");
            _board.Done.Label.text = Locale.Get("career.back");
            _prev = UiKit.Button(p, "Anterior", new Vector2(1675f - 170f, 285f), new Vector2(110f, 56f), "<", 30, UiTheme.Button);
            _next = UiKit.Button(p, "Siguiente", new Vector2(1675f + 170f, 285f), new Vector2(110f, 56f), ">", 30, UiTheme.Button);
            _page = UiKit.Label(p, "Pagina", new Vector2(1675f, 285f), new Vector2(240f, 50f), string.Empty, 22, UiTheme.TextDim);
            _mode = UiKit.Label(p, "Modo", new Vector2(1265f, 285f), new Vector2(390f, 40f), string.Empty, 22, UiTheme.Amber);
            _board.BenchHeader.text = Locale.Get("lineup.bench").ToUpperInvariant();
            _board.ReservesHeader.text = Locale.Get("lineup.reserves").ToUpperInvariant();
        }

        /// <inheritdoc />
        public override void SetVisible(bool visible)
        {
            if (visible)
            {
                _selectedIndex = -1;
                _reserveOffset = 0;
                LoadFromSession();
            }

            base.SetVisible(visible);
        }

        /// <summary>Lee del núcleo la alineación que saldría hoy (la elegida o la automática).</summary>
        private void LoadFromSession()
        {
            MatchPreparation.Lineup l = Session.UserLineup();
            _starterIds.Clear();
            _benchIds.Clear();
            _reserveIds.Clear();
            _starterIds.AddRange(l.Starters.Select(x => x.Id));
            _benchIds.AddRange(l.Bench.Select(x => x.Id));
            var chosen = new HashSet<int>(_starterIds.Concat(_benchIds));
            _reserveIds.AddRange(Session.Squad(Data.UserClubId)
                .Where(x => !chosen.Contains(x.Id))
                .OrderByDescending(x => PlayerRatings.Overall(x.Attributes, x.Position)).Select(x => x.Id));
        }

        private Player PlayerOf(int id) => Data.Players[id];

        private string StatusTag(int id)
        {
            PlayerCondition c = Data.Conditions[id];
            return c.InjuryDays > 0 ? "  " + CareerText.Format("career.status.injured", c.InjuryDays)
                : c.SuspendedMatches > 0 ? "  " + CareerText.Format("career.status.banned", c.SuspendedMatches) : string.Empty;
        }

        private bool Available(int id) => Data.Conditions[id].Available;

        /// <inheritdoc />
        public override void Refresh()
        {
            if (Session == null || _starterIds.Count == 0)
            {
                return;
            }

            Formation formation = Session.UserFormation();
            _board.Formation.text = formation.Name;
            Club club = Data.Club(Data.UserClubId);
            _board.Title.text = Locale.Get("lineup.title").ToUpperInvariant() + " · " + club.Name.ToUpperInvariant();
            Color kit = KitColors.From(TeamOf(club).Home).Shirt;
            float sum = 0f;
            _board.HideTokensFrom(_starterIds.Count);
            for (int i = 0; i < MaxStarters && i < _starterIds.Count; i++)
            {
                Player pl = PlayerOf(_starterIds[i]);
                FormationSlot slot = formation.Slots[i];
                int shown = Mathf.RoundToInt(RosterFactory.DisplayRating(pl.Attributes, slot.Role));
                sum += shown;
                bool selected = _selectedGroup == Group.Starter && _selectedIndex == i;
                Color fill = selected ? UiTheme.Accent : !Available(pl.Id) ? Unavailable : kit;
                bool offPosition = PlayerRatings.LineOf(pl.Position) != PlayerRatings.LineOf(slot.Role);
                Color ink = offPosition ? OutOfPositionInk : LineupBoard.ReadableOn(fill);
                _board.SetToken(i, _board.SlotPosition(slot.X, slot.Y), PlayerLabels.Code(slot.Role), PlayerLabels.Surname(pl.LastName), shown, fill, ink);
            }

            _board.Rating.text = CareerText.Format("lineup.rating", (sum / Mathf.Max(1, _starterIds.Count)).ToString("0.0", CultureInfo.InvariantCulture));
            for (int i = 0; i < MaxBench; i++)
            {
                bool selected = _selectedGroup == Group.Bench && _selectedIndex == i;
                if (i < _benchIds.Count)
                {
                    Player pl = PlayerOf(_benchIds[i]);
                    _bench[i].Label.text = "  " + PlayerLabels.Code(pl.Position).PadRight(4) + Name(pl) + "  ·  " + Session.VisibleRating(pl) + StatusTag(pl.Id);
                    _bench[i].Background.color = selected ? UiTheme.Selected : Available(pl.Id) ? UiTheme.Button : Unavailable;
                }
                else
                {
                    _bench[i].Label.text = "  —";
                    _bench[i].Background.color = UiTheme.Button;
                }
            }

            for (int i = 0; i < ReserveRows; i++)
            {
                int index = _reserveOffset + i;
                bool used = index < _reserveIds.Count;
                _reserves[i].SetVisible(used);
                if (!used)
                {
                    continue;
                }

                Player pl = PlayerOf(_reserveIds[index]);
                bool selected = _selectedGroup == Group.Reserve && _selectedIndex == index;
                _reserves[i].Label.text = "  " + PlayerLabels.Code(pl.Position).PadRight(4) + Name(pl) + "  ·  " + Session.VisibleRating(pl) + StatusTag(pl.Id);
                _reserves[i].Background.color = selected ? UiTheme.Selected : Available(pl.Id) ? UiTheme.Button : Unavailable;
            }

            int pages = Mathf.Max(1, (_reserveIds.Count + ReserveRows - 1) / ReserveRows);
            _page.text = CareerText.Format("career.page", _reserveOffset / ReserveRows + 1, pages);
            _mode.text = Locale.Get(Session.HasCustomLineup ? "lineup.manual" : "lineup.automatic");
        }

        private static string Name(Player p) => string.IsNullOrEmpty(p.FirstName) ? p.LastName : p.FirstName[0] + ". " + p.LastName;

        /// <inheritdoc />
        public override void Tap(Vector2 p)
        {
            if (_board.Done.Contains(p))
            {
                Host.Open(CareerSection.Squad);
                return;
            }

            if (_board.Auto.Contains(p))
            {
                Session.ClearUserLineup();
                _selectedIndex = -1;
                LoadFromSession();
                Host.SetStatus(Locale.Get("lineup.reset"));
                Refresh();
                return;
            }

            if (_board.FormationPrevious.Contains(p) || _board.FormationNext.Contains(p))
            {
                int at = System.Array.IndexOf(FormationNames, Data.Tactics.Formation);
                int step = _board.FormationNext.Contains(p) ? 1 : -1;
                Data.Tactics.Formation = FormationNames[((at < 0 ? 0 : at) + step + FormationNames.Length) % FormationNames.Length];
                _selectedIndex = -1;
                LoadFromSession();
            }
            else if (_prev.Contains(p))
            {
                _reserveOffset = Mathf.Max(0, _reserveOffset - ReserveRows);
            }
            else if (_next.Contains(p) && _reserveOffset + ReserveRows < _reserveIds.Count)
            {
                _reserveOffset += ReserveRows;
            }
            else if (TryHit(p, out Group group, out int index))
            {
                Choose(group, index);
            }

            Refresh();
        }

        private bool TryHit(Vector2 p, out Group group, out int index)
        {
            for (int i = 0; i < _starterIds.Count; i++)
            {
                if (_board.Tokens[i].Circle.Contains(p))
                {
                    group = Group.Starter;
                    index = i;
                    return true;
                }
            }

            for (int i = 0; i < MaxBench; i++)
            {
                if (_bench[i].Contains(p))
                {
                    group = Group.Bench;
                    index = i;
                    return true;
                }
            }

            for (int i = 0; i < ReserveRows; i++)
            {
                if (_reserves[i].Contains(p))
                {
                    group = Group.Reserve;
                    index = _reserveOffset + i;
                    return true;
                }
            }

            group = Group.Starter;
            index = -1;
            return false;
        }

        private List<int> ListOf(Group g) => g == Group.Starter ? _starterIds : g == Group.Bench ? _benchIds : _reserveIds;

        private void Choose(Group group, int index)
        {
            // Un lugar vacío del banco solo recibe a un no convocado: lo suma al banco.
            if (group == Group.Bench && index >= _benchIds.Count)
            {
                if (_selectedIndex >= 0 && _selectedGroup == Group.Reserve && _benchIds.Count < MaxBench)
                {
                    int id = _reserveIds[_selectedIndex];
                    _reserveIds.RemoveAt(_selectedIndex);
                    _benchIds.Add(id);
                    _selectedIndex = -1;
                    Save();
                }

                return;
            }

            if (_selectedIndex < 0)
            {
                _selectedGroup = group;
                _selectedIndex = index;
                return;
            }

            if (_selectedGroup == group && _selectedIndex == index)
            {
                _selectedIndex = -1; // tocar otra vez el mismo suelta la selección
                return;
            }

            if (_selectedGroup == Group.Reserve && group == Group.Reserve)
            {
                _selectedIndex = index; // entre no convocados no hay nada que intercambiar: se cambia la selección
                return;
            }

            List<int> a = ListOf(_selectedGroup), b = ListOf(group);
            int temp = a[_selectedIndex];
            a[_selectedIndex] = b[index];
            b[index] = temp;
            _selectedIndex = -1;
            Save();
        }

        private void Save()
        {
            if (!Session.SetUserLineup(_starterIds, _benchIds))
            {
                Host.SetStatus(Locale.Get("lineup.invalid"));
                LoadFromSession();
                return;
            }

            bool injured = _starterIds.Any(id => !Available(id));
            Host.SetStatus(Locale.Get(injured ? "lineup.saved_injured" : "lineup.saved"));
        }
    }
}
