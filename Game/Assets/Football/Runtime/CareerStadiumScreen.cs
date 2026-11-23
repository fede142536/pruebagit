using System.Collections.Generic;
using Football.Core.Domain;
using Football.Core.Management;
using UnityEngine;
using UnityEngine.UI;

namespace Football.Game
{
    /// <summary>
    /// Remodelación del estadio. A la izquierda, pestañas TRIBUNAS, PARKING, EQUIPAMIENTO y SERVICIOS con tres tarjetas de
    /// nivel (beneficio, costo y semanas); la que está en obra muestra el cartel «EN OBRA» con su barra de avance. Se pueden
    /// marcar varias obras a la vez (una por categoría): la barra de abajo suma la TOTAL REMODELACIÓN y CONTRATAR OBRA las
    /// pide todas. A la derecha, el club, el estadio, los indicadores de CAPACIDAD, PARKING y CAMPO y una vista isométrica
    /// con las tribunas, el estacionamiento y los andamios de las obras (los mismos anillos y andamios que el estadio 3D).
    /// </summary>
    public sealed class CareerStadiumScreen : CareerPage
    {
        private const int Categories = 4;
        private const int TiersPerCategory = 3;
        private const int ScaffoldBars = 6;
        private const float IsoSquash = 0.55f;
        private const float IsoTurn = 45f;
        private const float PitchLength = 230f, PitchWidth = 150f;
        private const float TierDepth = 26f, StandGap = 6f;
        private static readonly Color Construction = new Color(1f, 0.72f, 0.1f);
        private static readonly Color Concrete = new Color(0.55f, 0.58f, 0.64f);
        private static readonly Color Asphalt = new Color(0.22f, 0.23f, 0.26f);
        private static readonly Color Grass = new Color(0.2f, 0.55f, 0.25f);

        private readonly UiButton[] _tabs = new UiButton[Categories];
        private readonly UiButton[] _cards = new UiButton[TiersPerCategory];
        private readonly Text[] _cardTitle = new Text[TiersPerCategory], _cardBenefit = new Text[TiersPerCategory],
            _cardCost = new Text[TiersPerCategory], _cardWeeks = new Text[TiersPerCategory], _cardState = new Text[TiersPerCategory];
        private readonly GameObject[] _sign = new GameObject[TiersPerCategory];
        private readonly UiBar[] _progress = new UiBar[TiersPerCategory];
        private readonly int[] _selected = new int[Categories];
        private readonly List<(RemodelCategory, int)> _order = new List<(RemodelCategory, int)>();
        private readonly UiButton _contract, _back;
        private readonly Text _total, _selection, _clubName, _stadiumName, _capacity, _parking, _pitch;
        private readonly ClubCrest _crest;
        private readonly Image[] _stands = new Image[4];
        private readonly Image[] _ring = new Image[4];
        private readonly Image _parkingLot;
        private readonly Image[] _scaffold = new Image[ScaffoldBars];
        private readonly Image _equipmentCrane, _servicesCrane;
        private RemodelCategory _category;

        /// <summary>Crea la pantalla (oculta).</summary>
        public CareerStadiumScreen(Transform root, ICareerHost host) : base(root, "Estadio", host)
        {
            Transform p = Panel;
            UiTheme.Frame(p, "Obras", new Vector2(510f, 545f), new Vector2(960f, 830f));
            for (int i = 0; i < Categories; i++)
            {
                _tabs[i] = UiKit.Button(p, "Categoria" + i, new Vector2(150f + i * 240f, 905f), new Vector2(228f, 64f), Locale.Get("stadium.tab." + i), 22, UiTheme.Button);
            }

            for (int t = 0; t < TiersPerCategory; t++)
            {
                float x = 205f + t * 305f;
                _cards[t] = UiKit.Button(p, "Nivel" + t, new Vector2(x, 625f), new Vector2(290f, 420f), string.Empty, 10, UiTheme.Button);
                _cardTitle[t] = UiKit.Label(p, "NivelTitulo" + t, new Vector2(x, 800f), new Vector2(270f, 40f), string.Empty, 26, UiTheme.Accent);
                _cardBenefit[t] = UiKit.Label(p, "NivelBeneficio" + t, new Vector2(x, 730f), new Vector2(260f, 80f), string.Empty, 22, UiTheme.Text);
                _cardBenefit[t].horizontalOverflow = HorizontalWrapMode.Wrap;
                _cardCost[t] = UiKit.Label(p, "NivelCosto" + t, new Vector2(x, 655f), new Vector2(270f, 40f), string.Empty, 26, UiTheme.Gold);
                _cardWeeks[t] = UiKit.Label(p, "NivelSemanas" + t, new Vector2(x, 612f), new Vector2(270f, 36f), string.Empty, 20, UiTheme.TextDim);
                _cardState[t] = UiKit.Label(p, "NivelEstado" + t, new Vector2(x, 450f), new Vector2(270f, 36f), string.Empty, 20, UiTheme.Text);
                _sign[t] = CreateGroup(p, "EnObra" + t).gameObject;
                BuildSign(_sign[t].transform, new Vector2(x, 540f));
                _progress[t] = new UiBar(_sign[t].transform, "Avance" + t, new Vector2(x, 495f), new Vector2(240f, 16f));
            }

            _selection = UiKit.Label(p, "Seleccion", new Vector2(510f, 330f), new Vector2(900f, 90f), string.Empty, 20, UiTheme.TextDim, TextAnchor.UpperLeft);
            _selection.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.Panel(p, "Barra", new Vector2(510f, 190f), new Vector2(940f, 120f), new Color(0f, 0f, 0f, 0.35f));
            _total = UiKit.Label(p, "Total", new Vector2(250f, 190f), new Vector2(440f, 90f), string.Empty, 24, UiTheme.Gold);
            _contract = UiKit.Button(p, "Contratar", new Vector2(640f, 190f), new Vector2(300f, 84f), Locale.Get("stadium.contract"), 26, UiTheme.AccentDark);
            _back = UiKit.Button(p, "Volver", new Vector2(880f, 190f), new Vector2(150f, 84f), Locale.Get("career.back"), 24, UiTheme.Button);

            UiTheme.Frame(p, "Vista", new Vector2(1450f, 545f), new Vector2(860f, 830f));
            _crest = new ClubCrest(p, new Vector2(1090f, 900f), 90f);
            _clubName = UiKit.Label(p, "Club", new Vector2(1480f, 920f), new Vector2(660f, 44f), string.Empty, 28, UiTheme.Text, TextAnchor.MiddleLeft);
            _stadiumName = UiKit.Label(p, "Nombre", new Vector2(1480f, 878f), new Vector2(660f, 36f), string.Empty, 22, UiTheme.TextDim, TextAnchor.MiddleLeft);
            _capacity = Indicator(p, 0, "stadium.ind.capacity");
            _parking = Indicator(p, 1, "stadium.ind.parking");
            _pitch = Indicator(p, 2, "stadium.ind.pitch");

            // Vista isométrica: un grupo aplastado en Y y, adentro, otro girado 45°.
            Transform squash = Pivot(p, "Iso", new Vector2(1450f, 400f));
            squash.localScale = new Vector3(1f, IsoSquash, 1f);
            Transform iso = Pivot(squash, "IsoGiro", Vector2.zero);
            iso.localRotation = Quaternion.Euler(0f, 0f, IsoTurn);
            _parkingLot = UiKit.Panel(iso, "Parking", Vector2.zero, Vector2.one, Asphalt);
            for (int i = 0; i < _stands.Length; i++)
            {
                _ring[i] = UiKit.Panel(iso, "Anillo" + i, Vector2.zero, Vector2.one, Color.Lerp(Concrete, Color.black, 0.25f));
                _stands[i] = UiKit.Panel(iso, "Tribuna" + i, Vector2.zero, Vector2.one, Concrete);
            }

            UiKit.Panel(iso, "Campo", Vector2.zero, new Vector2(PitchLength, PitchWidth), Grass);
            UiKit.Panel(iso, "LineaMedia", Vector2.zero, new Vector2(3f, PitchWidth), Color.white);
            UiKit.Panel(iso, "Circulo", Vector2.zero, new Vector2(36f, 36f), new Color(1f, 1f, 1f, 0.6f), true);
            for (int i = 0; i < ScaffoldBars; i++)
            {
                _scaffold[i] = UiKit.Panel(iso, "Andamio" + i, Vector2.zero, new Vector2(6f, 40f), Construction);
                _scaffold[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, 35f);
            }

            _equipmentCrane = UiKit.Panel(iso, "GruaEquipamiento", Vector2.zero, new Vector2(18f, 18f), Construction);
            _servicesCrane = UiKit.Panel(iso, "GruaServicios", Vector2.zero, new Vector2(18f, 18f), Construction);
        }

        private Text Indicator(Transform p, int i, string key)
        {
            float x = 1185f + i * 265f;
            UiKit.Panel(p, "Indicador" + i, new Vector2(x, 790f), new Vector2(250f, 100f), UiTheme.Button);
            UiKit.Label(p, "IndicadorTitulo" + i, new Vector2(x, 815f), new Vector2(240f, 34f), Locale.Get(key), 18, UiTheme.TextDim);
            return UiKit.Label(p, "IndicadorValor" + i, new Vector2(x, 772f), new Vector2(240f, 44f), string.Empty, 26, UiTheme.Text);
        }

        /// <summary>Cartel de obra: rayas amarillas y negras con la leyenda «EN OBRA».</summary>
        private static void BuildSign(Transform p, Vector2 c)
        {
            UiKit.Panel(p, "Cartel", c, new Vector2(240f, 56f), Construction);
            for (int i = 0; i < 4; i++)
            {
                Image stripe = UiKit.Panel(p, "Raya" + i, c + new Vector2(-105f + i * 70f, 0f), new Vector2(14f, 64f), Color.black);
                stripe.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 35f);
            }

            UiKit.Panel(p, "CartelTexto", c, new Vector2(130f, 40f), Construction);
            UiKit.Label(p, "Leyenda", c, new Vector2(130f, 40f), Locale.Get("stadium.under_construction"), 22, Color.black);
        }

        private static Transform Pivot(Transform parent, string name, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = position;
            return go.transform;
        }

        /// <inheritdoc />
        public override void SetVisible(bool visible)
        {
            if (visible)
            {
                System.Array.Clear(_selected, 0, _selected.Length);
            }

            base.SetVisible(visible);
        }

        /// <inheritdoc />
        public override void Refresh()
        {
            StadiumState s = Session.UserStadium;
            for (int i = 0; i < Categories; i++)
            {
                bool busy = s.ActiveWork((RemodelCategory)i) != null;
                _tabs[i].Background.color = i == (int)_category ? UiTheme.Selected : _selected[i] > 0 ? UiTheme.AccentDark : UiTheme.Button;
                _tabs[i].Label.text = Locale.Get("stadium.tab." + i) + (busy ? " *" : string.Empty);
            }

            RemodelWork active = s.ActiveWork(_category);
            for (int t = 0; t < TiersPerCategory; t++)
            {
                int tier = t + 1;
                (long cost, int minWeeks, int maxWeeks) = Session.WorkInfo(_category, tier);
                _cardTitle[t].text = CareerText.Format("stadium.tier", tier);
                _cardBenefit[t].text = Benefit(_category, tier);
                _cardCost[t].text = CareerText.Money(cost);
                _cardWeeks[t].text = minWeeks == maxWeeks ? CareerText.Format("stadium.weeks", minWeeks) : CareerText.Format("stadium.weeks_range", minWeeks, maxWeeks);
                bool building = active != null && active.Tier == tier;
                bool done = _category != RemodelCategory.Stands && s.LevelOf(_category) >= tier;
                bool selected = _selected[(int)_category] == tier;
                _sign[t].SetActive(building);
                if (building)
                {
                    _progress[t].Set(active.Progress, Construction);
                }

                _cardState[t].text = building ? CareerText.Format("stadium.weeks_left", active.RemainingWeeks)
                    : done ? Locale.Get("stadium.done")
                    : active != null ? Locale.Get("stadium.busy")
                    : selected ? Locale.Get("stadium.selected") : Locale.Get("stadium.available");
                _cards[t].Background.color = selected ? UiTheme.Selected : done || active != null ? UiTheme.Panel : UiTheme.Button;
            }

            _order.Clear();
            string list = string.Empty;
            for (int i = 0; i < Categories; i++)
            {
                if (_selected[i] > 0)
                {
                    _order.Add(((RemodelCategory)i, _selected[i]));
                    list += (list.Length > 0 ? "  ·  " : string.Empty) + Locale.Get("stadium.tab." + i) + " " + CareerText.Num(_selected[i]);
                }
            }

            _selection.text = list.Length > 0 ? Locale.Get("stadium.selection") + ": " + list : Locale.Get("stadium.pick_hint");
            _total.text = Locale.Get("stadium.total") + "\n" + CareerText.Money(Session.QuoteWorks(_order));
            _contract.SetVisible(_order.Count > 0);
            RefreshView(s);
        }

        private string Benefit(RemodelCategory category, int tier)
        {
            int amount = Session.WorkBenefit(category, tier);
            switch (category)
            {
                case RemodelCategory.Stands: return CareerText.Format("stadium.benefit.seats", CareerText.Count(amount));
                case RemodelCategory.Parking: return CareerText.Format("stadium.benefit.parking", CareerText.Count(amount));
                default: return Locale.Get("stadium.benefit." + (int)category + "." + tier);
            }
        }

        private void RefreshView(StadiumState s)
        {
            Club club = Data.Club(Data.UserClubId);
            _crest.Set(TeamOf(club));
            _clubName.text = club.Name;
            _stadiumName.text = s.Name;
            _capacity.text = CareerText.Count(s.Capacity);
            _parking.text = CareerText.Count(s.ParkingSpaces);
            _pitch.text = Locale.Get("pitch." + (int)s.Pitch);
            _pitch.color = StatusColors.For((int)s.Pitch / 4f);

            // Tribunas: tantos anillos como el estadio 3D; la de enfrente lleva andamios si se amplía.
            int tiers = StadiumLook.TiersFor(s.Capacity);
            float depth = tiers * TierDepth;
            float halfL = PitchLength * 0.5f + StandGap, halfW = PitchWidth * 0.5f + StandGap;
            Place(_stands[0], new Vector2(0f, halfW + depth * 0.5f), new Vector2(PitchLength + 2f * depth, depth));
            Place(_stands[1], new Vector2(0f, -halfW - depth * 0.5f), new Vector2(PitchLength + 2f * depth, depth));
            Place(_stands[2], new Vector2(-halfL - depth * 0.5f, 0f), new Vector2(depth, PitchWidth));
            Place(_stands[3], new Vector2(halfL + depth * 0.5f, 0f), new Vector2(depth, PitchWidth));
            for (int i = 0; i < _ring.Length; i++)
            {
                RectTransform st = _stands[i].rectTransform;
                Place(_ring[i], st.anchoredPosition, st.sizeDelta * 0.55f);
            }

            float lot = 60f + 40f * s.ParkingLevel;
            Place(_parkingLot, new Vector2(halfL + depth + lot * 0.5f + StandGap, 0f), new Vector2(lot, PitchWidth + depth));

            bool stands = s.ActiveWork(RemodelCategory.Stands) != null;
            for (int i = 0; i < ScaffoldBars; i++)
            {
                _scaffold[i].gameObject.SetActive(stands);
                Place(_scaffold[i], new Vector2(-PitchLength * 0.4f + i * PitchLength * 0.16f, halfW + depth + 12f), new Vector2(6f, 34f));
            }

            _equipmentCrane.gameObject.SetActive(s.ActiveWork(RemodelCategory.Equipment) != null);
            Place(_equipmentCrane, new Vector2(-halfL - depth - 14f, halfW + depth), new Vector2(18f, 18f));
            _servicesCrane.gameObject.SetActive(s.ActiveWork(RemodelCategory.Services) != null || s.ActiveWork(RemodelCategory.Parking) != null);
            Place(_servicesCrane, new Vector2(halfL + depth + 14f, -halfW - depth), new Vector2(18f, 18f));
        }

        private static void Place(Image image, Vector2 center, Vector2 size)
        {
            image.rectTransform.anchoredPosition = center;
            image.rectTransform.sizeDelta = size;
        }

        /// <inheritdoc />
        public override void Tap(Vector2 p)
        {
            if (_back.Contains(p))
            {
                Host.Open(CareerSection.Finance);
                return;
            }

            for (int i = 0; i < Categories; i++)
            {
                if (_tabs[i].Contains(p))
                {
                    _category = (RemodelCategory)i;
                    Refresh();
                    return;
                }
            }

            StadiumState s = Session.UserStadium;
            for (int t = 0; t < TiersPerCategory; t++)
            {
                if (!_cards[t].Contains(p))
                {
                    continue;
                }

                int tier = t + 1;
                if (s.ActiveWork(_category) != null)
                {
                    Host.SetStatus(Locale.Get("stadium.result." + (int)RemodelResult.CategoryBusy));
                }
                else if (!StadiumRemodelingSystem.IsAvailable(s, _category, tier))
                {
                    Host.SetStatus(Locale.Get("stadium.result." + (int)RemodelResult.InvalidTier));
                }
                else
                {
                    int c = (int)_category;
                    _selected[c] = _selected[c] == tier ? 0 : tier; // una obra por categoría
                }

                Refresh();
                return;
            }

            if (_contract.Contains(p))
            {
                Contract();
            }
        }

        private void Contract()
        {
            int ok = 0;
            RemodelResult last = RemodelResult.Ok;
            for (int i = 0; i < Categories; i++)
            {
                if (_selected[i] == 0)
                {
                    continue;
                }

                RemodelResult r = Session.ContractWork((RemodelCategory)i, _selected[i]);
                if (r == RemodelResult.Ok)
                {
                    ok++;
                    _selected[i] = 0;
                }
                else
                {
                    last = r;
                }
            }

            Host.SetStatus(last == RemodelResult.Ok ? CareerText.Format("stadium.contracted", ok)
                : CareerText.Format("stadium.partial", ok) + " " + Locale.Get("stadium.result." + (int)last));
            Refresh();
        }
    }
}
