using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Football.Core.Domain;
using Football.Core.Management;
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
        private const int ReserveRows = 10;
        private const float PitchX = 400f, PitchY = 520f, PitchWidth = 540f, PitchHeight = 820f, PitchMargin = 0.9f, LineWidth = 3f;
        private const float DotSize = 68f;
        private const float BenchX = 1050f, ReserveX = 1600f, ListWidth = 500f, RowHeight = 62f, RowStep = 76f, ListTop = 840f;

        private static readonly Color PitchColor = new Color(0.10f, 0.36f, 0.16f, 1f);
        private static readonly Color LineTint = new Color(1f, 1f, 1f, 0.55f);
        private static readonly Color KeeperDot = new Color(0.85f, 0.65f, 0.10f, 1f);
        private static readonly Color DefenderDot = new Color(0.18f, 0.40f, 0.80f, 1f);
        private static readonly Color MidfielderDot = new Color(0.20f, 0.62f, 0.55f, 1f);
        private static readonly Color AttackerDot = new Color(0.80f, 0.25f, 0.22f, 1f);
        private static readonly Color Unavailable = new Color(0.35f, 0.12f, 0.12f, 1f);
        private static readonly Color RowColor = new Color(0.16f, 0.18f, 0.24f, 0.95f);

        private enum Group { Starter, Bench, Reserve }

        private readonly UiButton[] _dots = new UiButton[MaxStarters];
        private readonly Text[] _dotNames = new Text[MaxStarters];
        private readonly UiButton[] _bench = new UiButton[MaxBench];
        private readonly UiButton[] _reserves = new UiButton[ReserveRows];
        private readonly UiButton _auto, _back, _prev, _next;
        private readonly Text _title, _mode, _hint, _page, _formation;
        private readonly List<int> _starterIds = new List<int>(), _benchIds = new List<int>(), _reserveIds = new List<int>();
        private Group _selectedGroup;
        private int _selectedIndex = -1;
        private int _reserveOffset;

        /// <summary>Crea la pantalla (oculta).</summary>
        public CareerLineupScreen(Transform root, ICareerHost host) : base(root, "Alineacion", host)
        {
            Transform p = Panel;
            UiKit.Panel(p, "Fondo", new Vector2(960f, 520f), new Vector2(1840f, 900f), UiTheme.Panel);
            _title = UiKit.Label(p, "Titulo", new Vector2(400f, 960f), new Vector2(560f, 44f), Locale.Get("lineup.title"), 32, UiTheme.Accent);
            _formation = UiKit.Label(p, "Formacion", new Vector2(400f, 930f), new Vector2(560f, 30f), string.Empty, 22, UiTheme.TextDim);
            _hint = UiKit.Label(p, "Ayuda", new Vector2(1320f, 990f), new Vector2(1000f, 36f), Locale.Get("lineup.hint"), 20, UiTheme.TextDim);
            BuildPitch(p);
            for (int i = 0; i < MaxStarters; i++)
            {
                _dots[i] = UiKit.Button(p, "Titular" + i, Vector2.zero, new Vector2(DotSize, DotSize), string.Empty, 24, DefenderDot, true);
                _dotNames[i] = UiKit.Label(p, "TitularNombre" + i, Vector2.zero, new Vector2(150f, 26f), string.Empty, 17, Color.white);
            }

            UiKit.Label(p, "TituloBanco", new Vector2(BenchX, 905f), new Vector2(ListWidth, 40f), Locale.Get("lineup.bench"), 28, UiTheme.Accent);
            for (int i = 0; i < MaxBench; i++)
            {
                _bench[i] = Row(p, "Suplente" + i, BenchX, i);
            }

            UiKit.Label(p, "TituloReservas", new Vector2(ReserveX, 905f), new Vector2(ListWidth, 40f), Locale.Get("lineup.reserves"), 28, UiTheme.Accent);
            for (int i = 0; i < ReserveRows; i++)
            {
                _reserves[i] = Row(p, "Reserva" + i, ReserveX, i);
            }

            _prev = UiKit.Button(p, "Anterior", new Vector2(ReserveX - 170f, 110f), new Vector2(110f, 60f), "<", 32, UiTheme.Button);
            _next = UiKit.Button(p, "Siguiente", new Vector2(ReserveX + 170f, 110f), new Vector2(110f, 60f), ">", 32, UiTheme.Button);
            _page = UiKit.Label(p, "Pagina", new Vector2(ReserveX, 110f), new Vector2(240f, 50f), string.Empty, 22, UiTheme.TextDim);
            _mode = UiKit.Label(p, "Modo", new Vector2(BenchX, 170f), new Vector2(ListWidth, 40f), string.Empty, 22, UiTheme.Gold);
            _auto = UiKit.Button(p, "Automatica", new Vector2(BenchX - 110f, 110f), new Vector2(280f, 64f), Locale.Get("lineup.auto"), 24, UiTheme.Button);
            _back = UiKit.Button(p, "Volver", new Vector2(BenchX + 160f, 110f), new Vector2(200f, 64f), Locale.Get("career.back"), 26, UiTheme.AccentDark);
        }

        private static UiButton Row(Transform parent, string name, float x, int index)
        {
            UiButton row = UiKit.Button(parent, name, new Vector2(x, ListTop - index * RowStep), new Vector2(ListWidth, RowHeight), string.Empty, 22, RowColor);
            row.Label.alignment = TextAnchor.MiddleLeft;
            return row;
        }

        private static void BuildPitch(Transform t)
        {
            var center = new Vector2(PitchX, PitchY);
            UiKit.Panel(t, "Cancha", center, new Vector2(PitchWidth, PitchHeight), PitchColor);
            Outline(t, "Borde", center, new Vector2(PitchWidth, PitchHeight));
            UiKit.Panel(t, "Mitad", center, new Vector2(PitchWidth, LineWidth), LineTint);
            UiKit.Panel(t, "CirculoMedio", center, new Vector2(PitchWidth * 0.26f, PitchWidth * 0.26f), LineTint, true);
            UiKit.Panel(t, "CirculoMedioInterior", center, new Vector2(PitchWidth * 0.26f - 2f * LineWidth, PitchWidth * 0.26f - 2f * LineWidth), PitchColor, true);
            var area = new Vector2(PitchWidth * 0.58f, PitchHeight * 0.16f);
            var small = new Vector2(PitchWidth * 0.28f, PitchHeight * 0.06f);
            for (int end = -1; end <= 1; end += 2)
            {
                float edge = PitchY + end * PitchHeight * 0.5f;
                Outline(t, "Area" + end, new Vector2(PitchX, edge - end * area.y * 0.5f), area);
                Outline(t, "AreaChica" + end, new Vector2(PitchX, edge - end * small.y * 0.5f), small);
            }
        }

        private static void Outline(Transform t, string name, Vector2 center, Vector2 size)
        {
            UiKit.Panel(t, name + "N", center + new Vector2(0f, size.y * 0.5f), new Vector2(size.x, LineWidth), LineTint);
            UiKit.Panel(t, name + "S", center - new Vector2(0f, size.y * 0.5f), new Vector2(size.x, LineWidth), LineTint);
            UiKit.Panel(t, name + "O", center - new Vector2(size.x * 0.5f, 0f), new Vector2(LineWidth, size.y), LineTint);
            UiKit.Panel(t, name + "E", center + new Vector2(size.x * 0.5f, 0f), new Vector2(LineWidth, size.y), LineTint);
        }

        /// <summary>Punto de la cancha de un puesto (ataque hacia arriba; Y positivo es la izquierda del equipo).</summary>
        private static Vector2 PitchPoint(FormationSlot slot) => new Vector2(
            PitchX - slot.Y * PitchWidth * 0.5f * PitchMargin,
            PitchY - PitchHeight * 0.5f * PitchMargin + slot.X * PitchHeight * PitchMargin);

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
            _formation.text = formation.Name;
            for (int i = 0; i < MaxStarters; i++)
            {
                bool used = i < _starterIds.Count;
                _dots[i].SetVisible(used);
                _dotNames[i].gameObject.SetActive(used);
                if (!used)
                {
                    continue;
                }

                Player pl = PlayerOf(_starterIds[i]);
                FormationSlot slot = formation.Slots[i];
                Vector2 at = PitchPoint(slot);
                _dots[i].Place(at, new Vector2(DotSize, DotSize));
                ((RectTransform)_dotNames[i].transform).anchoredPosition = at - new Vector2(0f, DotSize * 0.5f + 12f);
                _dots[i].Label.text = Mathf.RoundToInt(PlayerRatings.Overall(pl.Attributes, slot.Role)).ToString(CultureInfo.InvariantCulture);
                _dotNames[i].text = PlayerLabels.Surname(pl.LastName);
                bool selected = _selectedGroup == Group.Starter && _selectedIndex == i;
                _dots[i].Background.color = selected ? UiTheme.Selected : !Available(pl.Id) ? Unavailable : LineColor(slot.Role);
            }

            for (int i = 0; i < MaxBench; i++)
            {
                bool selected = _selectedGroup == Group.Bench && _selectedIndex == i;
                if (i < _benchIds.Count)
                {
                    Player pl = PlayerOf(_benchIds[i]);
                    _bench[i].Label.text = "  " + (i + 1) + "  " + Name(pl) + "  ·  " + PlayerLabels.Code(pl.Position) + "  ·  " + Session.VisibleRating(pl) + StatusTag(pl.Id);
                    _bench[i].Background.color = selected ? UiTheme.Selected : Available(pl.Id) ? RowColor : Unavailable;
                }
                else
                {
                    _bench[i].Label.text = "  " + (i + 1) + "  —";
                    _bench[i].Background.color = RowColor;
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
                _reserves[i].Label.text = "  " + Name(pl) + "  ·  " + PlayerLabels.Code(pl.Position) + "  ·  " + Session.VisibleRating(pl) + StatusTag(pl.Id);
                _reserves[i].Background.color = selected ? UiTheme.Selected : Available(pl.Id) ? RowColor : Unavailable;
            }

            int pages = Mathf.Max(1, (_reserveIds.Count + ReserveRows - 1) / ReserveRows);
            _page.text = CareerText.Format("career.page", _reserveOffset / ReserveRows + 1, pages);
            _mode.text = Locale.Get(Session.HasCustomLineup ? "lineup.manual" : "lineup.automatic");
        }

        private static string Name(Player p) => string.IsNullOrEmpty(p.FirstName) ? p.LastName : p.FirstName[0] + ". " + p.LastName;

        /// <inheritdoc />
        public override void Tap(Vector2 p)
        {
            if (_back.Contains(p))
            {
                Host.Open(CareerSection.Squad);
                return;
            }

            if (_auto.Contains(p))
            {
                Session.ClearUserLineup();
                _selectedIndex = -1;
                LoadFromSession();
                Host.SetStatus(Locale.Get("lineup.reset"));
                Refresh();
                return;
            }

            if (_prev.Contains(p))
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
                if (_dots[i].Contains(p))
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
