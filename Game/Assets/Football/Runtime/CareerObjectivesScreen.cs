using System;
using System.Collections.Generic;
using System.Globalization;
using Football.Core.Career;
using Football.Core.Domain;
using Football.Core.Management;
using UnityEngine;
using UnityEngine.UI;

namespace Football.Game
{
    /// <summary>Textos y colores de los objetivos de la directiva (los usan la elección de club y la oficina).</summary>
    public static class BoardText
    {
        private static readonly Color[] PriorityColors =
            { UiTheme.Danger, UiTheme.Amber, UiTheme.Amber, UiTheme.TextDim };

        /// <summary>«1 CRÍTICA», «2 ALTA», «3 MEDIA» o «4 BAJA».</summary>
        public static string Priority(ObjectivePriority p) => CareerText.Num((int)p) + " " + Locale.Get("priority." + (int)p);

        /// <summary>Color de la prioridad (rojo para la crítica).</summary>
        public static Color PriorityColor(ObjectivePriority p) => PriorityColors[Mathf.Clamp((int)p - 1, 0, PriorityColors.Length - 1)];

        /// <summary>Nombre del objetivo.</summary>
        public static string Category(ObjectiveCategory c) => Locale.Get("objective." + (int)c);

        /// <summary>Meta concreta («Terminar entre los 3 primeros», «Ocupación media del 85%»…).</summary>
        public static string Target(BoardObjective o, CareerConfig c)
        {
            switch (o.Category)
            {
                case ObjectiveCategory.NationalSuccess:
                    return CareerText.Format("objective.target.0", Mathf.RoundToInt(o.Target));
                case ObjectiveCategory.ContinentalSuccess:
                    return CareerText.Format("objective.target.1", c.CupQualifiers);
                case ObjectiveCategory.YouthDevelopment:
                    return CareerText.Format("objective.target.2", CareerText.Percent(o.Target), c.Board.YouthAge);
                case ObjectiveCategory.Finances:
                    return CareerText.Format("objective.target.3", CareerText.Percent(o.Target));
                default:
                    return CareerText.Format("objective.target.4", CareerText.Percent(o.Target));
            }
        }

        /// <summary>Valor actual del objetivo según la directiva.</summary>
        public static string Current(ObjectiveCategory category, BoardSnapshot s)
        {
            switch (category)
            {
                case ObjectiveCategory.NationalSuccess:
                case ObjectiveCategory.ContinentalSuccess:
                    return s.Position > 0 ? CareerText.Format("objective.now.position", s.Position, s.Clubs) : Locale.Get("objective.now.none");
                case ObjectiveCategory.YouthDevelopment:
                    return CareerText.Format("objective.now.youth", CareerText.Percent(s.YouthMinutesShare));
                case ObjectiveCategory.Finances:
                    return CareerText.Format("objective.now.cash", CareerText.Percent(s.CashRatio));
                default:
                    return s.AverageAttendance < 0f ? Locale.Get("objective.now.none") : CareerText.Format("objective.now.attendance", CareerText.Percent(s.AverageAttendance));
            }
        }

        /// <summary>Texto de un factor del medidor de gestión («Refuerzo Pérez: +4 pts»).</summary>
        public static string Factor(RatingFactor f) =>
            CareerText.Format(f.Key, f.Arg ?? string.Empty) + ": " + (f.Points >= 0f ? "+" : string.Empty)
            + f.Points.ToString("0.#", CultureInfo.InvariantCulture) + " " + Locale.Get("rating.points");
    }

    /// <summary>
    /// Oficina y objetivos: arriba, el escudo, el DT y su popularidad (con el umbral de despido); a la izquierda, seis
    /// mosaicos (Resumen, Desarrollo juvenil, Exposición de marca, Finanzas, Éxito nacional y Éxito continental) con su
    /// prioridad y avance; a la derecha, el detalle del mosaico elegido. El Resumen muestra el medidor de gestión en tres
    /// barras (hinchas, plantel y fichajes, presupuesto) con cada factor y sus puntos.
    /// </summary>
    public sealed class CareerObjectivesScreen : CareerPage
    {
        private const int Tiles = 6;
        private const int FactorRows = 9;
        private const int SummaryRows = 5;
        private static readonly ObjectiveCategory[] TileCategory =
        {
            ObjectiveCategory.NationalSuccess, ObjectiveCategory.YouthDevelopment, ObjectiveCategory.BrandExposure,
            ObjectiveCategory.Finances, ObjectiveCategory.NationalSuccess, ObjectiveCategory.ContinentalSuccess
        };

        private readonly ClubCrest _crest;
        private readonly AvatarView _avatar;
        private readonly Text _manager, _club, _popularity;
        private readonly UiBar _popularityBar;
        private readonly UiButton[] _tiles = new UiButton[Tiles];
        private readonly Text[] _tilePriority = new Text[Tiles], _tileValue = new Text[Tiles];
        private readonly UiBar[] _tileBars = new UiBar[Tiles];
        private readonly Text _detailTitle, _detailPriority, _detailTarget, _detailNow, _detailHint, _detailPercent;
        private readonly UiBar _detailBar;
        private readonly GameObject _summary, _single;
        private readonly Text _ratingTotal;
        private readonly UiBar[] _pillarBars = new UiBar[3];
        private readonly Text[] _pillarText = new Text[3];
        private readonly Text[] _factors = new Text[FactorRows];
        private readonly Text[] _summaryLines = new Text[SummaryRows];
        private readonly List<RatingFactor> _sorted = new List<RatingFactor>();
        private CareerConfig _config = new CareerConfig();
        private int _tile;

        /// <summary>Crea la pantalla (oculta).</summary>
        public CareerObjectivesScreen(Transform root, ICareerHost host) : base(root, "Oficina", host)
        {
            Transform p = Panel;
            UiTheme.Frame(p, "Cabecera", new Vector2(960f, 915f), new Vector2(1840f, 120f));
            _crest = new ClubCrest(p, new Vector2(100f, 915f), 96f);
            _avatar = new AvatarView(p, new Vector2(215f, 915f), 96f);
            _manager = UiKit.Label(p, "DT", new Vector2(620f, 935f), new Vector2(760f, 50f), string.Empty, 32, UiTheme.Text, TextAnchor.MiddleLeft);
            _club = UiKit.Label(p, "Club", new Vector2(620f, 890f), new Vector2(760f, 40f), string.Empty, 22, UiTheme.TextDim, TextAnchor.MiddleLeft);
            _popularity = UiKit.Label(p, "Popularidad", new Vector2(1500f, 940f), new Vector2(700f, 40f), string.Empty, 24, UiTheme.Text, TextAnchor.MiddleLeft);
            _popularityBar = new UiBar(p, "PopularidadBarra", new Vector2(1500f, 893f), new Vector2(700f, 22f));

            for (int i = 0; i < Tiles; i++)
            {
                Vector2 c = new Vector2(230f + (i % 2) * 400f, 715f - (i / 2) * 250f);
                _tiles[i] = UiKit.Button(p, "Mosaico" + i, c, new Vector2(380f, 230f), string.Empty, 10, UiTheme.Button);
                UiKit.Label(p, "MosaicoTitulo" + i, c + new Vector2(0f, 70f), new Vector2(350f, 40f), Locale.Get("office.tile." + i), 24, UiTheme.Text);
                _tilePriority[i] = UiKit.Label(p, "MosaicoPrioridad" + i, c + new Vector2(0f, 22f), new Vector2(350f, 36f), string.Empty, 20, UiTheme.TextDim);
                _tileBars[i] = new UiBar(p, "MosaicoBarra" + i, c + new Vector2(0f, -30f), new Vector2(320f, 18f));
                _tileValue[i] = UiKit.Label(p, "MosaicoValor" + i, c + new Vector2(0f, -72f), new Vector2(350f, 36f), string.Empty, 22, UiTheme.Accent);
            }

            UiTheme.Frame(p, "Detalle", new Vector2(1365f, 465f), new Vector2(1070f, 730f));
            _detailTitle = UiKit.Label(p, "DetalleTitulo", new Vector2(1365f, 790f), new Vector2(1000f, 50f), string.Empty, 32, UiTheme.Accent);

            _single = CreateGroup(p, "DetalleObjetivo").gameObject;
            Transform s = _single.transform;
            _detailPriority = UiKit.Label(s, "Prioridad", new Vector2(1365f, 720f), new Vector2(1000f, 40f), string.Empty, 26, UiTheme.Text);
            _detailTarget = UiKit.Label(s, "Meta", new Vector2(1365f, 640f), new Vector2(1000f, 44f), string.Empty, 28, UiTheme.Text);
            _detailNow = UiKit.Label(s, "Actual", new Vector2(1365f, 580f), new Vector2(1000f, 40f), string.Empty, 24, UiTheme.TextDim);
            _detailBar = new UiBar(s, "Avance", new Vector2(1365f, 500f), new Vector2(900f, 36f));
            _detailPercent = UiKit.Label(s, "Porcentaje", new Vector2(1365f, 445f), new Vector2(400f, 40f), string.Empty, 30, UiTheme.Text);
            _detailHint = UiKit.Label(s, "Consejo", new Vector2(1365f, 300f), new Vector2(900f, 160f), string.Empty, 22, UiTheme.TextDim, TextAnchor.UpperLeft);
            _detailHint.horizontalOverflow = HorizontalWrapMode.Wrap;

            _summary = CreateGroup(p, "DetalleResumen").gameObject;
            Transform r = _summary.transform;
            _ratingTotal = UiKit.Label(r, "Medidor", new Vector2(1365f, 735f), new Vector2(1000f, 44f), string.Empty, 28, UiTheme.Text);
            for (int i = 0; i < 3; i++)
            {
                float y = 670f - i * 70f;
                _pillarText[i] = UiKit.Label(r, "Pilar" + i, new Vector2(1060f, y), new Vector2(380f, 40f), string.Empty, 22, UiTheme.Text, TextAnchor.MiddleLeft);
                _pillarBars[i] = new UiBar(r, "PilarBarra" + i, new Vector2(1560f, y), new Vector2(560f, 26f));
            }

            for (int i = 0; i < FactorRows; i++)
            {
                _factors[i] = UiKit.Label(r, "Factor" + i, new Vector2(1130f, 440f - i * 34f), new Vector2(520f, 32f), string.Empty, 18, UiTheme.TextDim, TextAnchor.MiddleLeft);
            }

            for (int i = 0; i < SummaryRows; i++)
            {
                _summaryLines[i] = UiKit.Label(r, "Objetivo" + i, new Vector2(1640f, 440f - i * 56f), new Vector2(480f, 50f), string.Empty, 18, UiTheme.Text, TextAnchor.MiddleLeft);
            }
        }

        /// <summary>Parámetros (umbral de despido y textos de las metas).</summary>
        public void Configure(CareerConfig config)
        {
            _config = config ?? new CareerConfig();
        }

        /// <inheritdoc />
        public override void Refresh()
        {
            CareerData d = Data;
            Club club = d.Club(d.UserClubId);
            _crest.Set(TeamOf(club));
            _avatar.Set(d.Manager.AvatarIndex, true);
            _manager.text = Locale.Get("manager.dt") + " " + (d.Manager.FullName.Length > 0 ? d.Manager.FullName : Locale.Get("manager.unnamed"));
            _club.text = club.Name + "  ·  " + Locale.Get("career.tier." + (int)Session.TierOf(d.UserClubId)) + "  ·  " + Locale.Get("nation." + d.Manager.Nationality);
            float popularity = d.Manager.Popularity;
            _popularity.text = CareerText.Format("office.popularity", Mathf.RoundToInt(popularity), Mathf.RoundToInt(_config.Board.FireThreshold));
            _popularityBar.Set(popularity / 100f, popularity < _config.Board.FireThreshold * 1.4f ? StatusColors.Bad : StatusColors.For(popularity / 100f));

            List<BoardObjective> objectives = Session.Objectives();
            BoardSnapshot snapshot = Session.BoardSnapshot();
            ManagementRating rating = Session.Rating();
            for (int i = 0; i < Tiles; i++)
            {
                _tiles[i].Background.color = i == _tile ? UiTheme.Selected : UiTheme.Button;
                float progress;
                if (i == 0)
                {
                    progress = rating.Total / 100f;
                    _tilePriority[i].text = Locale.Get("office.rating");
                }
                else
                {
                    BoardObjective o = objectives.Find(x => x.Category == TileCategory[i]);
                    progress = BoardExpectations.Progress(o, snapshot);
                    _tilePriority[i].text = BoardText.Priority(o.Priority);
                    _tilePriority[i].color = BoardText.PriorityColor(o.Priority);
                }

                _tileBars[i].Set(progress, StatusColors.For(progress));
                _tileValue[i].text = CareerText.Percent(progress);
            }

            _detailTitle.text = Locale.Get("office.tile." + _tile);
            _summary.SetActive(_tile == 0);
            _single.SetActive(_tile != 0);
            if (_tile == 0)
            {
                RefreshSummary(rating, objectives, snapshot);
            }
            else
            {
                BoardObjective o = objectives.Find(x => x.Category == TileCategory[_tile]);
                float progress = BoardExpectations.Progress(o, snapshot);
                _detailPriority.text = Locale.Get("office.priority") + ": " + BoardText.Priority(o.Priority);
                _detailPriority.color = BoardText.PriorityColor(o.Priority);
                _detailTarget.text = BoardText.Target(o, _config);
                _detailNow.text = BoardText.Current(o.Category, snapshot);
                _detailBar.Set(progress, StatusColors.For(progress));
                _detailPercent.text = CareerText.Percent(progress);
                _detailHint.text = Locale.Get("objective.hint." + (int)o.Category);
            }
        }

        private void RefreshSummary(ManagementRating rating, List<BoardObjective> objectives, BoardSnapshot snapshot)
        {
            _ratingTotal.text = CareerText.Format("rating.total", Mathf.RoundToInt(rating.Total));
            float[] values = { rating.Supporters, rating.Squad, rating.Budget };
            for (int i = 0; i < 3; i++)
            {
                _pillarText[i].text = CareerText.Format("rating.pillar." + i, Mathf.RoundToInt(values[i]), CareerText.Percent(_config.Rating.PillarWeight[i]));
                _pillarBars[i].Set(values[i] / 100f, StatusColors.For(values[i] / 100f));
            }

            _sorted.Clear();
            _sorted.AddRange(rating.Factors);
            _sorted.Sort((a, b) => Math.Abs(b.Points).CompareTo(Math.Abs(a.Points)));
            for (int i = 0; i < FactorRows; i++)
            {
                _factors[i].text = i < _sorted.Count ? BoardText.Factor(_sorted[i]) : string.Empty;
                _factors[i].color = i < _sorted.Count && _sorted[i].Points < 0f ? StatusColors.Bad : UiTheme.TextDim;
            }

            for (int i = 0; i < SummaryRows; i++)
            {
                if (i >= objectives.Count)
                {
                    _summaryLines[i].text = string.Empty;
                    continue;
                }

                BoardObjective o = objectives[i];
                _summaryLines[i].text = BoardText.Priority(o.Priority) + "  " + BoardText.Category(o.Category) + "  "
                                        + CareerText.Percent(BoardExpectations.Progress(o, snapshot));
                _summaryLines[i].color = BoardText.PriorityColor(o.Priority);
            }
        }

        /// <inheritdoc />
        public override void Tap(Vector2 p)
        {
            for (int i = 0; i < Tiles; i++)
            {
                if (_tiles[i].Contains(p))
                {
                    _tile = i;
                    Refresh();
                    return;
                }
            }
        }
    }
}
