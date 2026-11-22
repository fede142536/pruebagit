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
    /// flechas para recorrer los clubes; en el centro, las cinco expectativas de la directiva ordenadas por prioridad
    /// («1 CRÍTICA» … «4 BAJA»); a la derecha, historia y fundación, presupuesto de fichajes, valor del club, camisetas
    /// titular y alternativa, y el estadio con su capacidad. Los datos salen de <see cref="ClubInfo"/> del archivo de equipos.
    /// </summary>
    public sealed class CareerClubSelectScreen : CareerPage
    {
        private const int Objectives = 5;
        private const float CrestSize = 250f;
        private const float RowStep = 132f;

        private readonly UiButton _back, _prev, _next, _go;
        private readonly ClubCrest _crest;
        private readonly StarRating _stars;
        private readonly FlagView _flag;
        private readonly Text _name, _league, _tier, _founded, _history, _budget, _value, _stadium, _capacity;
        private readonly Text[] _priority = new Text[Objectives], _objective = new Text[Objectives], _target = new Text[Objectives];
        private readonly Image[] _badge = new Image[Objectives];
        private readonly KitPreview _home, _away;
        private CareerConfig _config = new CareerConfig();

        /// <summary>Club elegido (índice de <see cref="TeamCatalog.Teams"/>).</summary>
        public int Chosen { get; private set; }

        /// <summary>Crea la pantalla (oculta).</summary>
        public CareerClubSelectScreen(Transform root, ICareerHost host) : base(root, "ElegirClub", host)
        {
            Transform p = Panel;
            _back = UiKit.Button(p, "Volver", new Vector2(160f, 1020f), new Vector2(220f, 70f), Locale.Get("career.back"), 28, UiTheme.Button);
            UiKit.Label(p, "Titulo", new Vector2(760f, 1020f), new Vector2(900f, 80f), Locale.Get("career.pick_club"), 50, UiTheme.Text, TextAnchor.MiddleLeft);

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
            _tier = UiKit.Label(p, "Jerarquia", new Vector2(330f, 340f), new Vector2(540f, 40f), string.Empty, 24, UiTheme.Gold);
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
        }

        /// <inheritdoc />
        public override void Refresh()
        {
            TeamDefinition team = TeamCatalog.Teams[Chosen];
            ClubInfo info = team.Club ?? new ClubInfo();
            _crest.Set(team);
            _name.text = team.Name;
            _stars.Set(TeamCatalog.Rating(Chosen));
            ClubTier tier = BoardExpectations.ParseTier(info.Tier);
            _tier.text = Locale.Get("career.tier." + (int)tier);
            _stadium.text = info.Stadium;
            _capacity.text = CareerText.Format("career.capacity", CareerText.Count(info.Capacity));
            _founded.text = info.Founded > 0 ? CareerText.Format("career.founded", info.Founded) : team.Name;
            _history.text = info.History;
            _budget.text = Locale.Get("career.transfer_budget") + ": " + CareerText.Money(info.TransferBudget);
            _value.text = Locale.Get("career.club_value") + ": " + CareerText.Money(info.ClubValue);
            _home.Set(KitColors.From(team.Home));
            _away.Set(KitColors.From(team.Away));

            List<BoardObjective> objectives = BoardExpectations.Generate(tier, TeamCatalog.Teams.Length, _config.Board);
            for (int i = 0; i < Objectives; i++)
            {
                BoardObjective o = objectives[i];
                _badge[i].color = BoardText.PriorityColor(o.Priority);
                _priority[i].text = BoardText.Priority(o.Priority);
                _objective[i].text = BoardText.Category(o.Category);
                _target[i].text = BoardText.Target(o, _config);
            }
        }

        /// <inheritdoc />
        public override void Tap(Vector2 p)
        {
            int count = TeamCatalog.Teams.Length;
            if (_back.Contains(p)) { Host.Request(CareerAction.BackToManager); return; }
            if (_go.Contains(p)) { Host.Request(CareerAction.NewGame); return; }
            if (_prev.Contains(p)) { Chosen = (Chosen + count - 1) % count; }
            else if (_next.Contains(p)) { Chosen = (Chosen + 1) % count; }
            else { return; }

            Refresh();
        }
    }
}
