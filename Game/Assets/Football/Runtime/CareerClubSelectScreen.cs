using System.Collections.Generic;
using Football.Core.Career;
using Football.Core.Data;
using Football.Core.Management;
using UnityEngine;
using UnityEngine.UI;

namespace Football.Game
{
    /// <summary>
    /// Elección del club (segundo paso del Modo Carrera). A la izquierda, la ficha: bandera, escudo, estrellas, liga y
    /// flechas para recorrer los clubes (con contador y un filtro por jerarquía para moverse entre los 30 de Primera); en el centro, las cinco expectativas de la directiva ordenadas por prioridad
    /// («1 CRÍTICA» … «4 BAJA»); a la derecha, historia y fundación, presupuesto de fichajes, valor del club, camisetas
    /// titular y alternativa, y el estadio con su capacidad. Los datos salen de <see cref="ClubInfo"/> del archivo de equipos.
    /// </summary>
    public sealed class CareerClubSelectScreen : CareerPage
    {
        private const int Objectives = 5;
        private const float CrestSize = 250f;
        private const float RowStep = 132f;
        private static readonly string[] FilterTiers = { null, "Big", "Mid", "Development" };

        private readonly UiButton _back, _prev, _next, _go, _filter, _size, _mode;
        private bool _vintage;
        private readonly Text _counter;
        private readonly System.Collections.Generic.List<int> _shown = new System.Collections.Generic.List<int>();
        private int _filterIndex;
        private readonly ClubCrest _crest;
        private readonly StarRating _stars;
        private readonly FlagView _flag;
        private readonly Text _division;
        private int _sizeIndex = -1;
        private readonly Text _name, _league, _tier, _founded, _history, _budget, _value, _stadium, _capacity;
        private readonly Text[] _priority = new Text[Objectives], _objective = new Text[Objectives], _target = new Text[Objectives];
        private readonly Image[] _badge = new Image[Objectives];
        private readonly KitPreview _home, _away;
        private CareerConfig _config = new CareerConfig();

        /// <summary>Club elegido (índice de <see cref="TeamCatalog.AllTeams"/>; los 30 clásicos conservan su índice de <see cref="TeamCatalog.Teams"/>).</summary>
        public int Chosen { get; private set; }

        /// <summary>
        /// Clubes de Primera elegidos (el resto juega el Nacional B); 0 = todos los clubes en una sola división.
        /// </summary>
        public int PrimeraSize
        {
            get
            {
                if (_vintage)
                {
                    return CareerSession.VintageFirstDivision;
                }

                int size = CurrentSize;
                return size >= TeamCatalog.Teams.Length ? 0 : size;
            }
        }

        /// <summary>true = «Fútbol vintage» (Primera de 20 + Nacional de 29); false = el modo clásico de 30 clubes.</summary>
        public bool Vintage => _vintage;

        /// <summary>Cantidades ofrecidas: las del archivo que dejan al menos los clubes mínimos en el Nacional B (o «todos»).</summary>
        private int[] Sizes
        {
            get
            {
                int total = TeamCatalog.Teams.Length;
                var list = new List<int>();
                foreach (int s in _config.PrimeraSizes ?? new[] { total })
                {
                    if (s >= total || (s >= 2 && total - s >= _config.MinSecondDivisionClubs))
                    {
                        list.Add(Mathf.Min(s, total));
                    }
                }

                if (list.Count == 0)
                {
                    list.Add(total);
                }

                return list.ToArray();
            }
        }

        private int CurrentSize
        {
            get
            {
                int[] sizes = Sizes;
                if (_sizeIndex < 0 || _sizeIndex >= sizes.Length)
                {
                    _sizeIndex = Mathf.Max(0, System.Array.IndexOf(sizes, DefaultSize)); // por defecto, 20 equipos
                }

                return Mathf.Min(sizes[_sizeIndex], TeamCatalog.Teams.Length);
            }
        }

        private const int DefaultSize = 20;

        /// <summary>Crea la pantalla (oculta).</summary>
        public CareerClubSelectScreen(Transform root, ICareerHost host) : base(root, "ElegirClub", host)
        {
            Transform p = Panel;
            _back = UiKit.Button(p, "Volver", new Vector2(160f, 1020f), new Vector2(220f, 70f), Locale.Get("career.back"), 28, UiTheme.Button);
            UiKit.Label(p, "Titulo", new Vector2(520f, 1020f), new Vector2(420f, 80f), Locale.Get("career.pick_club"), 50, UiTheme.Text, TextAnchor.MiddleLeft);
            _filter = UiKit.Button(p, "Filtro", new Vector2(1530f, 1020f), new Vector2(320f, 70f), string.Empty, 24, UiTheme.Button);
            _counter = UiKit.Label(p, "Contador", new Vector2(1790f, 1020f), new Vector2(170f, 60f), string.Empty, 28, UiTheme.TextDim);
            _size = UiKit.Button(p, "CantidadPrimera", new Vector2(1240f, 1020f), new Vector2(300f, 70f), string.Empty, 20, UiTheme.Button);
            _mode = UiKit.Button(p, "Modalidad", new Vector2(930f, 1020f), new Vector2(300f, 70f), string.Empty, 20, UiTheme.AccentDark);

            // Ficha del club.
            UiKit.Panel(p, "Ficha", new Vector2(330f, 545f), new Vector2(580f, 760f), UiTheme.Panel);
            _flag = new FlagView(p, new Vector2(110f, 885f), new Vector2(84f, 54f));
            _flag.Set(NationFlag.All[0]);
            _league = UiKit.Label(p, "Liga", new Vector2(370f, 885f), new Vector2(400f, 40f), Locale.Get("career.league"), 22, UiTheme.TextDim, TextAnchor.MiddleLeft);
            _crest = new ClubCrest(p, new Vector2(330f, 665f), CrestSize);
            _prev = UiKit.Button(p, "Anterior", new Vector2(95f, 665f), new Vector2(90f, 120f), "<", 54, UiTheme.Button);
            _next = UiKit.Button(p, "Siguiente", new Vector2(565f, 665f), new Vector2(90f, 120f), ">", 54, UiTheme.Button);
            _name = UiKit.Label(p, "Club", new Vector2(330f, 465f), new Vector2(540f, 64f), string.Empty, 38, UiTheme.Text);
            _stars = new StarRating(p, new Vector2(330f, 400f));
            _tier = UiKit.Label(p, "Jerarquia", new Vector2(330f, 345f), new Vector2(540f, 40f), string.Empty, 24, UiTheme.Gold);
            _division = UiKit.Label(p, "Division", new Vector2(330f, 298f), new Vector2(540f, 40f), string.Empty, 24, UiTheme.Accent);
            _stadium = UiKit.Label(p, "Estadio", new Vector2(330f, 250f), new Vector2(540f, 44f), string.Empty, 26, UiTheme.Text);
            _capacity = UiKit.Label(p, "Capacidad", new Vector2(330f, 205f), new Vector2(540f, 40f), string.Empty, 22, UiTheme.TextDim);

            // Expectativas de la directiva.
            UiKit.Panel(p, "Expectativas", new Vector2(960f, 545f), new Vector2(640f, 760f), UiTheme.Panel);
            UiKit.Label(p, "ExpTitulo", new Vector2(960f, 885f), new Vector2(600f, 44f), Locale.Get("career.board_expectations"), 28, UiTheme.Accent);
            for (int i = 0; i < Objectives; i++)
            {
                float y = 790f - i * RowStep;
                _badge[i] = UiKit.Panel(p, "Prioridad" + i, new Vector2(745f, y), new Vector2(160f, 56f), UiTheme.Button);
                _priority[i] = UiKit.Label(p, "PrioridadTexto" + i, new Vector2(745f, y), new Vector2(160f, 56f), string.Empty, 22, Color.white);
                _objective[i] = UiKit.Label(p, "Objetivo" + i, new Vector2(1035f, y + 16f), new Vector2(400f, 36f), string.Empty, 24, UiTheme.Text, TextAnchor.MiddleLeft);
                _target[i] = UiKit.Label(p, "Meta" + i, new Vector2(1035f, y - 20f), new Vector2(400f, 34f), string.Empty, 19, UiTheme.TextDim, TextAnchor.MiddleLeft);
            }

            // Historia, dinero y camisetas.
            UiKit.Panel(p, "Historia", new Vector2(1590f, 545f), new Vector2(580f, 760f), UiTheme.Panel);
            _founded = UiKit.Label(p, "Fundacion", new Vector2(1590f, 885f), new Vector2(540f, 44f), string.Empty, 28, UiTheme.Accent);
            _history = UiKit.Label(p, "HistoriaTexto", new Vector2(1590f, 770f), new Vector2(520f, 170f), string.Empty, 20, UiTheme.Text, TextAnchor.UpperLeft);
            _history.horizontalOverflow = HorizontalWrapMode.Wrap;
            _budget = UiKit.Label(p, "Presupuesto", new Vector2(1590f, 640f), new Vector2(540f, 40f), string.Empty, 24, UiTheme.Text, TextAnchor.MiddleLeft);
            _value = UiKit.Label(p, "Valor", new Vector2(1590f, 595f), new Vector2(540f, 40f), string.Empty, 24, UiTheme.Text, TextAnchor.MiddleLeft);
            UiKit.Label(p, "Titular", new Vector2(1460f, 530f), new Vector2(240f, 36f), Locale.Get("career.kit_home"), 22, UiTheme.TextDim);
            UiKit.Label(p, "Alternativa", new Vector2(1720f, 530f), new Vector2(240f, 36f), Locale.Get("career.kit_away"), 22, UiTheme.TextDim);
            _home = new KitPreview(p, new Vector2(1460f, 380f));
            _away = new KitPreview(p, new Vector2(1720f, 380f));

            _go = UiKit.Button(p, "Dirigir", new Vector2(960f, 90f), new Vector2(560f, 100f), Locale.Get("career.take_club"), 38, UiTheme.AccentDark);
        }

        /// <summary>Parámetros de la directiva para describir las expectativas.</summary>
        public void Configure(CareerConfig config)
        {
            _config = config ?? new CareerConfig();
        }

        /// <summary>Preselecciona un club.</summary>
        public void Select(int index)
        {
            Chosen = Mathf.Clamp(index, 0, TeamCatalog.Teams.Length - 1);
            _vintage = false;
        }

        /// <inheritdoc />
        public override void Refresh()
        {
            TeamDefinition team = TeamCatalog.AllTeams[Chosen];
            ClubInfo info = team.Club ?? new ClubInfo();
            _crest.Set(team);
            _name.text = team.Name;
            _stars.Set(TeamCatalog.RatingAll(Chosen));
            ClubTier tier = BoardExpectations.ParseTier(info.Tier);
            RebuildShown();
            _filter.Label.text = Locale.Get("career.filter") + ": " + Locale.Get("career.filter." + _filterIndex);
            _counter.text = Mathf.Max(1, _shown.IndexOf(Chosen) + 1) + " / " + _shown.Count;
            _tier.text = Locale.Get("career.tier." + (int)tier);
            int size = PrimeraSize;
            _mode.Label.text = Locale.Get(_vintage ? "career.mode.vintage" : "career.mode.classic");
            _size.Label.text = _vintage ? Locale.Get("career.vintage_split")
                : size == 0 ? Locale.Get("career.primera_all") : CareerText.Format("career.primera_size", size, TeamCatalog.Teams.Length - size);
            int division = CareerSession.PreviewDivisions(_vintage ? TeamCatalog.VintageDatabase : TeamCatalog.Database, size)[Chosen];
            _division.text = size == 0 ? Locale.Get("career.division_single") : Locale.Get(division == 1 ? "career.division_primera" : "career.division_b");
            _division.color = division == 1 || size == 0 ? UiTheme.Accent : UiTheme.Warning;
            _stadium.text = string.IsNullOrEmpty(info.City) ? info.Stadium : info.Stadium + " · " + info.City;
            _capacity.text = CareerText.Format("career.capacity", CareerText.Count(info.Capacity));
            _founded.text = info.Founded > 0 ? CareerText.Format("career.founded", info.Founded) : team.Name;
            _history.text = info.History;
            _budget.text = Locale.Get("career.transfer_budget") + ": " + CareerText.Money(info.TransferBudget);
            _value.text = Locale.Get("career.club_value") + ": " + CareerText.Money(info.ClubValue);
            _home.Set(KitColors.From(team.Home));
            _away.Set(KitColors.From(team.Away));

            int clubsInDivision = _vintage ? (division == 1 ? size : TeamCatalog.AllTeams.Length - size) : TeamCatalog.Teams.Length;
            List<BoardObjective> objectives = BoardExpectations.Generate(tier, clubsInDivision, _config.Board);
            for (int i = 0; i < Objectives; i++)
            {
                BoardObjective o = objectives[i];
                _badge[i].color = BoardText.PriorityColor(o.Priority);
                _priority[i].text = BoardText.Priority(o.Priority);
                _objective[i].text = BoardText.Category(o.Category);
                _target[i].text = BoardText.Target(o, _config);
            }
        }

        private int CurrentIndex()
        {
            int unused = CurrentSize; // asegura el valor inicial
            return _sizeIndex;
        }

        private void RebuildShown()
        {
            _shown.Clear();
            int pool = _vintage ? TeamCatalog.AllTeams.Length : TeamCatalog.Teams.Length; // los clásicos son los primeros de la lista completa
            for (int i = 0; i < pool; i++)
            {
                if (FilterTiers[_filterIndex] == null || TeamCatalog.AllTeams[i].Club?.Tier == FilterTiers[_filterIndex])
                {
                    _shown.Add(i);
                }
            }
        }

        private void Step(int direction)
        {
            RebuildShown();
            if (_shown.Count == 0)
            {
                return;
            }

            int at = _shown.IndexOf(Chosen);
            Chosen = _shown[at < 0 ? 0 : (at + direction + _shown.Count) % _shown.Count];
        }

        /// <inheritdoc />
        public override void Tap(Vector2 p)
        {
            if (_back.Contains(p)) { Host.Request(CareerAction.BackToManager); return; }
            if (_go.Contains(p)) { Host.Request(CareerAction.NewGame); return; }
            if (_prev.Contains(p)) { Step(-1); }
            else if (_next.Contains(p)) { Step(1); }
            else if (_mode.Contains(p))
            {
                _vintage = !_vintage;
                _sizeIndex = -1;
                RebuildShown();
                if (!_shown.Contains(Chosen) && _shown.Count > 0)
                {
                    Chosen = _shown[0];
                }
            }
            else if (!_vintage && _size.Contains(p))
            {
                int[] sizes = Sizes;
                _sizeIndex = (CurrentIndex() + 1) % sizes.Length;
            }
            else if (_filter.Contains(p))
            {
                _filterIndex = (_filterIndex + 1) % FilterTiers.Length;
                RebuildShown();
                if (_shown.Count > 0 && !_shown.Contains(Chosen))
                {
                    Chosen = _shown[0];
                }
            }
            else { return; }

            Refresh();
        }
    }
}
