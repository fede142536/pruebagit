using System;
using System.Globalization;
using Football.Core.Career;
using Football.Core.Domain;
using Football.Core.Management;
using UnityEngine;
using UnityEngine.UI;

namespace Football.Game
{
    /// <summary>
    /// Finanzas y estadio, en cuatro pestañas. RESUMEN: ganancia neta, ingresos y gastos de la temporada, anillo de ingresos
    /// por rubro, valor del club y proyección de la caja al cierre. TRANSACCIONES: los últimos movimientos. PRESUPUESTO:
    /// fichajes y sueldos (con pases entre ambos), cuerpo técnico e instalaciones. ENTRADAS/ESTADIO: nivel de precio y
    /// ajuste fino con vista previa en vivo (rango ideal en verde, amarillo o rojo, ocupación y recaudación esperadas) y
    /// el acceso a la remodelación del estadio. Todo el cálculo lo hace <see cref="CareerSession"/>.
    /// </summary>
    public sealed class CareerFinanceScreen : CareerPage
    {
        private enum Tab { Summary, Transactions, Budget, Tickets }

        private const int TabCount = 4;
        private const int LedgerRows = 14;
        private const int LegendRows = 8;
        private const float PriceStepShare = 0.05f;
        private const float BudgetMoveShare = 0.1f;
        private const int StaffImprovement = 2;
        private const int MaxStaffAbility = 20;
        private static readonly FinanceCategory[] IncomeCategories =
        {
            FinanceCategory.Gate, FinanceCategory.Members, FinanceCategory.Television, FinanceCategory.Sponsors,
            FinanceCategory.Matchday, FinanceCategory.Merchandise, FinanceCategory.Prizes, FinanceCategory.Transfers
        };
        private static readonly Color[] SliceColors =
        {
            new Color(0.22f, 0.88f, 0.78f), new Color(0.3f, 0.6f, 0.95f), new Color(0.65f, 0.45f, 0.95f), new Color(1f, 0.82f, 0.25f),
            new Color(1f, 0.55f, 0.3f), new Color(0.95f, 0.4f, 0.6f), new Color(0.5f, 0.85f, 0.35f), new Color(0.75f, 0.78f, 0.85f)
        };
        private static readonly StaffRole[] ShownStaff = { StaffRole.Manager, StaffRole.Scout, StaffRole.Physio, StaffRole.YouthCoach };

        private readonly UiButton[] _tabs = new UiButton[TabCount];
        private readonly Transform[] _groups = new Transform[TabCount];
        private Tab _tab;
        private CareerConfig _config = new CareerConfig();

        // resumen
        private Text _net, _income, _expenses, _value, _projection;
        private DonutChart _donut;
        private readonly Text[] _legend = new Text[LegendRows];
        private readonly Image[] _legendColor = new Image[LegendRows];
        private readonly long[] _sliceValues = new long[LegendRows];

        // transacciones
        private TextGrid _ledger;

        // presupuesto
        private TextGrid _money;
        private UiButton _toWages, _toTransfers;
        private readonly Text[] _facilityText = new Text[4];
        private readonly UiButton[] _upgrade = new UiButton[4];
        private readonly Text[] _staffText = new Text[4];
        private readonly UiButton[] _hire = new UiButton[4];

        // entradas
        private readonly UiButton[] _levels = new UiButton[4];
        private UiButton _minus, _plus, _apply, _stadiumButton;
        private Text _price, _nextMatch, _importance, _range, _statusText, _attendance, _spectators, _revenue, _stadiumInfo;
        private Image _statusChip;
        private UiBar _attendanceBar;
        private long _chosenPrice;

        /// <summary>Crea la pantalla (oculta).</summary>
        public CareerFinanceScreen(Transform root, ICareerHost host) : base(root, "Finanzas", host)
        {
            for (int i = 0; i < TabCount; i++)
            {
                _tabs[i] = UiKit.Button(Panel, "Pestana" + i, new Vector2(250f + i * 345f, 950f), new Vector2(330f, 64f), Locale.Get("finance.tab." + i), 24, UiTheme.Button);
                _groups[i] = CreateGroup(Panel, "Grupo" + i);
            }

            BuildSummary(_groups[(int)Tab.Summary]);
            BuildLedger(_groups[(int)Tab.Transactions]);
            BuildBudget(_groups[(int)Tab.Budget]);
            BuildTickets(_groups[(int)Tab.Tickets]);
            Select(Tab.Summary);
        }

        /// <summary>Parámetros (precios de referencia de las entradas).</summary>
        public void Configure(CareerConfig config)
        {
            _config = config ?? new CareerConfig();
        }

        /// <summary>Abre directamente la pestaña de entradas (desde el pedido de los socios, por ejemplo).</summary>
        public void ShowTickets() => Select(Tab.Tickets);

        private void Select(Tab tab)
        {
            _tab = tab;
            for (int i = 0; i < TabCount; i++)
            {
                _groups[i].gameObject.SetActive(i == (int)tab);
                _tabs[i].Background.color = i == (int)tab ? UiTheme.Selected : UiTheme.Button;
            }

            _chosenPrice = 0;
        }

        /// <inheritdoc />
        public override void Refresh()
        {
            switch (_tab)
            {
                case Tab.Summary: RefreshSummary(); break;
                case Tab.Transactions: RefreshLedger(); break;
                case Tab.Budget: RefreshBudget(); break;
                default: RefreshTickets(); break;
            }
        }

        /// <inheritdoc />
        public override void Tap(Vector2 p)
        {
            for (int i = 0; i < TabCount; i++)
            {
                if (_tabs[i].Contains(p))
                {
                    Select((Tab)i);
                    Refresh();
                    return;
                }
            }

            switch (_tab)
            {
                case Tab.Budget: TapBudget(p); break;
                case Tab.Tickets: TapTickets(p); break;
            }
        }

        // ------------------------------------------------------------------ resumen

        private void BuildSummary(Transform p)
        {
            string[] titles = { "finance.net", "finance.income", "finance.expenses" };
            Text[] values = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                float x = 330f + i * 420f;
                UiTheme.Frame(p, "Tarjeta" + i, new Vector2(x, 800f), new Vector2(400f, 180f));
                UiKit.Label(p, "TarjetaTitulo" + i, new Vector2(x, 850f), new Vector2(360f, 40f), Locale.Get(titles[i]), 22, UiTheme.TextDim);
                values[i] = UiKit.Label(p, "TarjetaValor" + i, new Vector2(x, 785f), new Vector2(380f, 70f), string.Empty, 44, UiTheme.Text);
            }

            _net = values[0];
            _income = values[1];
            _expenses = values[2];

            UiTheme.Frame(p, "Valor", new Vector2(1600f, 845f), new Vector2(560f, 90f));
            _value = UiKit.Label(p, "ValorTexto", new Vector2(1600f, 845f), new Vector2(520f, 60f), string.Empty, 26, UiTheme.Text);
            UiTheme.Frame(p, "Proyeccion", new Vector2(1600f, 745f), new Vector2(560f, 90f));
            _projection = UiKit.Label(p, "ProyeccionTexto", new Vector2(1600f, 745f), new Vector2(520f, 60f), string.Empty, 24, UiTheme.Text);

            UiTheme.Frame(p, "Anillo", new Vector2(960f, 390f), new Vector2(1840f, 520f));
            UiKit.Label(p, "AnilloTitulo", new Vector2(520f, 615f), new Vector2(700f, 40f), Locale.Get("finance.income_split"), 24, UiTheme.Accent);
            _donut = new DonutChart(p, new Vector2(520f, 370f), 380f, LegendRows, new Color(0.12f, 0.15f, 0.2f, 1f));
            for (int i = 0; i < LegendRows; i++)
            {
                float y = 590f - i * 56f;
                _legendColor[i] = UiKit.Panel(p, "LeyendaColor" + i, new Vector2(1000f, y), new Vector2(30f, 30f), SliceColors[i]);
                _legend[i] = UiKit.Label(p, "Leyenda" + i, new Vector2(1380f, y), new Vector2(700f, 44f), string.Empty, 22, UiTheme.Text, TextAnchor.MiddleLeft);
            }
        }

        private void RefreshSummary()
        {
            SeasonFinanceSummary s = Session.FinanceSummary();
            _net.text = CareerText.SignedMoney(s.Net);
            _net.color = s.Net >= 0 ? StatusColors.Good : StatusColors.Bad;
            _income.text = CareerText.Money(s.Income);
            _expenses.text = CareerText.Money(s.Expenses);
            _value.text = Locale.Get("career.club_value") + ": " + CareerText.Money(Session.InfoOf(Data.UserClubId).ClubValue);
            _projection.text = CareerText.Format("finance.projection", CareerText.Money(s.ProjectedCash));
            _projection.color = s.ProjectedCash >= 0 ? UiTheme.Text : StatusColors.Bad;
            long total = 0;
            for (int i = 0; i < LegendRows; i++)
            {
                _sliceValues[i] = Math.Max(0L, s.ByCategory[(int)IncomeCategories[i]]);
                total += _sliceValues[i];
            }

            _donut.Set(_sliceValues, SliceColors);
            for (int i = 0; i < LegendRows; i++)
            {
                _legend[i].text = Locale.Get("finance.category." + (int)IncomeCategories[i]) + "   " + CareerText.Money(_sliceValues[i])
                                  + (total > 0 ? "   (" + CareerText.Percent(_sliceValues[i] / (float)total) + ")" : string.Empty);
            }
        }

        // ------------------------------------------------------------------ transacciones

        private void BuildLedger(Transform p)
        {
            UiTheme.Frame(p, "Movimientos", new Vector2(960f, 520f), new Vector2(1500f, 820f));
            _ledger = new TextGrid(p, "Libro", new Vector2(260f, 880f), 52f, new[] { 360f, 560f, 1100f, 1530f }, new[] { 200f, 160f, 600f, 300f },
                LedgerRows + 1, 24);
        }

        private void RefreshLedger()
        {
            _ledger.Clear();
            _ledger.SetRow(0, Locale.Get("finance.col.date"), string.Empty, Locale.Get("finance.col.category"), Locale.Get("finance.col.amount"));
            var ledger = Data.Finances[Data.UserClubId].Ledger;
            int row = 1;
            for (int i = ledger.Count - 1; i >= 0 && row <= LedgerRows; i--, row++)
            {
                LedgerEntry e = ledger[i];
                _ledger.SetRow(row, e.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), string.Empty,
                    Locale.Get("finance.category." + (int)e.Category), CareerText.SignedMoney(e.Amount));
                _ledger.SetColor(row, 3, e.Amount >= 0 ? StatusColors.Good : StatusColors.Bad);
            }
        }

        // ------------------------------------------------------------------ presupuesto

        private void BuildBudget(Transform p)
        {
            UiTheme.Frame(p, "Dinero", new Vector2(480f, 520f), new Vector2(880f, 820f));
            UiKit.Label(p, "DineroTitulo", new Vector2(480f, 880f), new Vector2(840f, 50f), Locale.Get("career.finances"), 30, UiTheme.Accent);
            _money = new TextGrid(p, "Dinero", new Vector2(80f, 810f), 70f, new[] { 330f, 760f }, new[] { 500f, 300f }, 6, 26);
            _toWages = UiKit.Button(p, "ASueldos", new Vector2(270f, 250f), new Vector2(360f, 80f), Locale.Get("career.to_wages"), 26, UiTheme.Button);
            _toTransfers = UiKit.Button(p, "AFichajes", new Vector2(690f, 250f), new Vector2(360f, 80f), Locale.Get("career.to_transfers"), 26, UiTheme.Button);

            UiTheme.Frame(p, "Obras", new Vector2(1420f, 720f), new Vector2(920f, 420f));
            UiKit.Label(p, "ObrasTitulo", new Vector2(1420f, 895f), new Vector2(880f, 50f), Locale.Get("career.facilities"), 30, UiTheme.Accent);
            for (int i = 0; i < 4; i++)
            {
                float y = 820f - i * 85f;
                _facilityText[i] = UiKit.Label(p, "Obra" + i, new Vector2(1240f, y), new Vector2(520f, 60f), string.Empty, 22, UiTheme.Text, TextAnchor.MiddleLeft);
                _upgrade[i] = UiKit.Button(p, "Ampliar" + i, new Vector2(1700f, y), new Vector2(320f, 64f), string.Empty, 20, UiTheme.Button);
            }

            UiTheme.Frame(p, "CuerpoTecnico", new Vector2(1420f, 290f), new Vector2(920f, 420f));
            UiKit.Label(p, "CuerpoTitulo", new Vector2(1420f, 465f), new Vector2(880f, 50f), Locale.Get("career.staff"), 30, UiTheme.Accent);
            for (int i = 0; i < 4; i++)
            {
                float y = 390f - i * 85f;
                _staffText[i] = UiKit.Label(p, "Staff" + i, new Vector2(1240f, y), new Vector2(520f, 60f), string.Empty, 20, UiTheme.Text, TextAnchor.MiddleLeft);
                _hire[i] = UiKit.Button(p, "Contratar" + i, new Vector2(1700f, y), new Vector2(320f, 64f), Locale.Get("career.hire"), 20, UiTheme.Button);
            }
        }

        private void RefreshBudget()
        {
            CareerData d = Data;
            ClubFinances f = d.Finances[d.UserClubId];
            _money.Clear();
            _money.SetRow(0, Locale.Get("career.cash"), CareerText.Money(f.Cash));
            _money.SetRow(1, Locale.Get("career.transfer_budget"), CareerText.Money(f.TransferBudget));
            _money.SetRow(2, Locale.Get("career.wage_budget"), CareerText.Money(f.WeeklyWageBudget));
            _money.SetRow(3, Locale.Get("career.wage_bill"), CareerText.Money(Session.WeeklyWages(d.UserClubId)));
            _money.SetRow(4, Locale.Get("career.reputation"), CareerText.Num(f.Reputation));
            _money.SetRow(5, Locale.Get("career.members"), CareerText.Count(f.Members));
            ClubFacilities facilities = d.Facilities[d.UserClubId];
            for (int i = 0; i < 4; i++)
            {
                Facility fac = facilities.All[i];
                _facilityText[i].text = Locale.Get("career.facility." + i) + "  ·  " + CareerText.Format("career.level", fac.Level)
                                        + (fac.WeeksToFinish > 0 ? "  ·  " + CareerText.Format("career.building", fac.WeeksToFinish) : string.Empty);
                _upgrade[i].Label.text = CareerText.Format("career.upgrade", CareerText.Money(Session.Finance.UpgradeCost(fac.Level + 1)));
            }

            for (int i = 0; i < ShownStaff.Length; i++)
            {
                StaffMember s = d.Staff.Find(x => x.ClubId == d.UserClubId && x.Role == ShownStaff[i]);
                _staffText[i].text = Locale.Get("career.staff." + (int)ShownStaff[i]) + ": "
                                     + (s == null ? "-" : s.Name + " (" + s.Ability + ") " + CareerText.Money(s.WeeklyWage));
            }
        }

        private void TapBudget(Vector2 p)
        {
            CareerData d = Data;
            ClubFinances f = d.Finances[d.UserClubId];
            if (_toWages.Contains(p))
            {
                bool ok = Session.Finance.MoveToWages(d.UserClubId, (long)(f.TransferBudget * (double)BudgetMoveShare), d.Today);
                Host.SetStatus(Locale.Get(ok ? "career.moved" : "career.move_failed"));
            }
            else if (_toTransfers.Contains(p))
            {
                long spare = f.WeeklyWageBudget - Session.WeeklyWages(d.UserClubId);
                bool ok = spare > 0 && Session.Finance.MoveToTransfers(d.UserClubId, (long)(spare * (double)BudgetMoveShare) + 1, d.Today);
                Host.SetStatus(Locale.Get(ok ? "career.moved" : "career.move_failed"));
            }

            for (int i = 0; i < 4; i++)
            {
                if (_upgrade[i].Contains(p))
                {
                    Host.SetStatus(Locale.Get(Session.Finance.StartUpgrade(d.UserClubId, (FacilityKind)i, d.Today) ? "career.upgrade_started" : "career.upgrade_failed"));
                }

                if (_hire[i].Contains(p))
                {
                    StaffMember s = d.Staff.Find(x => x.ClubId == d.UserClubId && x.Role == ShownStaff[i]);
                    int ability = Math.Min(MaxStaffAbility, (s?.Ability ?? 0) + StaffImprovement);
                    string name = s?.Name ?? Locale.Get("career.staff." + (int)ShownStaff[i]);
                    Host.SetStatus(Locale.Get(Session.HireStaff(ShownStaff[i], ability, name) ? "career.hired" : "career.hire_failed"));
                }
            }

            RefreshBudget();
        }

        // ------------------------------------------------------------------ entradas y estadio

        private void BuildTickets(Transform p)
        {
            UiTheme.Frame(p, "Precio", new Vector2(560f, 520f), new Vector2(1060f, 820f));
            UiKit.Label(p, "PrecioTitulo", new Vector2(560f, 880f), new Vector2(1000f, 44f), Locale.Get("tickets.title"), 28, UiTheme.Accent);
            for (int i = 0; i < _levels.Length; i++)
            {
                _levels[i] = UiKit.Button(p, "Nivel" + i, new Vector2(170f + i * 260f, 800f), new Vector2(240f, 84f), string.Empty, 20, UiTheme.Button);
            }

            _minus = UiKit.Button(p, "Menos", new Vector2(250f, 680f), new Vector2(110f, 90f), "-", 48, UiTheme.Button);
            _price = UiKit.Label(p, "PrecioElegido", new Vector2(560f, 680f), new Vector2(460f, 90f), string.Empty, 52, UiTheme.Text);
            _plus = UiKit.Button(p, "Mas", new Vector2(870f, 680f), new Vector2(110f, 90f), "+", 48, UiTheme.Button);

            _nextMatch = UiKit.Label(p, "Proximo", new Vector2(560f, 590f), new Vector2(1000f, 40f), string.Empty, 24, UiTheme.Text);
            _importance = UiKit.Label(p, "Importancia", new Vector2(560f, 548f), new Vector2(1000f, 36f), string.Empty, 21, UiTheme.TextDim);
            _statusChip = UiKit.Panel(p, "Estado", new Vector2(560f, 480f), new Vector2(900f, 64f), StatusColors.Good);
            _range = UiKit.Label(p, "Rango", new Vector2(560f, 488f), new Vector2(880f, 36f), string.Empty, 24, Color.black);
            _statusText = UiKit.Label(p, "EstadoTexto", new Vector2(560f, 462f), new Vector2(880f, 28f), string.Empty, 18, Color.black);
            _attendance = UiKit.Label(p, "Ocupacion", new Vector2(560f, 395f), new Vector2(900f, 40f), string.Empty, 26, UiTheme.Text);
            _attendanceBar = new UiBar(p, "OcupacionBarra", new Vector2(560f, 350f), new Vector2(900f, 30f));
            _spectators = UiKit.Label(p, "Espectadores", new Vector2(560f, 295f), new Vector2(900f, 36f), string.Empty, 22, UiTheme.TextDim);
            _revenue = UiKit.Label(p, "Recaudacion", new Vector2(560f, 250f), new Vector2(900f, 40f), string.Empty, 26, UiTheme.Gold);
            _apply = UiKit.Button(p, "Aplicar", new Vector2(560f, 160f), new Vector2(560f, 84f), Locale.Get("tickets.apply"), 28, UiTheme.AccentDark);

            UiTheme.Frame(p, "EstadioResumen", new Vector2(1500f, 520f), new Vector2(760f, 820f));
            UiKit.Label(p, "EstadioTitulo", new Vector2(1500f, 880f), new Vector2(720f, 44f), Locale.Get("stadium.title"), 28, UiTheme.Accent);
            _stadiumInfo = UiKit.Label(p, "EstadioTexto", new Vector2(1500f, 560f), new Vector2(680f, 560f), string.Empty, 24, UiTheme.Text, TextAnchor.UpperLeft);
            _stadiumInfo.horizontalOverflow = HorizontalWrapMode.Wrap;
            _stadiumButton = UiKit.Button(p, "Remodelar", new Vector2(1500f, 160f), new Vector2(560f, 84f), Locale.Get("stadium.open"), 28, UiTheme.AccentDark);
        }

        private void RefreshTickets()
        {
            for (int i = 0; i < _levels.Length; i++)
            {
                long levelPrice = MatchAttendanceCalculator.PriceOf((TicketLevel)i, _config.Attendance);
                _levels[i].Label.text = Locale.Get("tickets.level." + i) + "\n" + CareerText.Price(levelPrice);
                _levels[i].Background.color = (int)Data.TicketLevel == i && Data.NextPriceOverride == 0 ? UiTheme.Selected : UiTheme.Button;
            }

            TicketPreview preview = Session.PreviewNextHome(_chosenPrice);
            if (_chosenPrice <= 0)
            {
                _chosenPrice = preview.Price > 0 ? preview.Price : MatchAttendanceCalculator.PriceOf(Data.TicketLevel, _config.Attendance);
            }

            _price.text = CareerText.Price(_chosenPrice);
            bool hasMatch = preview.Fixture != null;
            _statusChip.gameObject.SetActive(hasMatch);
            _attendanceBar.SetVisible(hasMatch);
            if (!hasMatch)
            {
                _nextMatch.text = Locale.Get("tickets.no_home_match");
                _importance.text = _range.text = _statusText.text = _attendance.text = _spectators.text = _revenue.text = string.Empty;
            }
            else
            {
                Fixture f = preview.Fixture;
                _nextMatch.text = CareerText.Format("tickets.next", Data.Club(f.AwayClubId).Name, f.Date.ToString("dd/MM", CultureInfo.InvariantCulture));
                _importance.text = Locale.Get("importance." + (int)preview.Importance);
                _range.text = CareerText.Format("tickets.range", CareerText.Price(preview.MinIdeal), CareerText.Price(preview.MaxIdeal));
                _statusText.text = Locale.Get("price.status." + (int)preview.Status);
                _statusChip.color = preview.Status == PriceStatus.Ideal ? StatusColors.Good : preview.Status == PriceStatus.Elevated ? StatusColors.Fair : StatusColors.Bad;
                _attendance.text = CareerText.Format("tickets.attendance", CareerText.Percent(preview.Expected.Ratio));
                _attendanceBar.Set(preview.Expected.Ratio, StatusColors.For(preview.Expected.Ratio));
                _spectators.text = CareerText.Format("tickets.spectators", CareerText.Count(preview.Expected.Spectators));
                _revenue.text = CareerText.Format("tickets.revenue", CareerText.Money(preview.Expected.Revenue));
            }

            StadiumState s = Session.UserStadium;
            string works = string.Empty;
            foreach (RemodelWork w in s.Works)
            {
                works += "\n" + Locale.Get("stadium.category." + (int)w.Category) + " " + CareerText.Num(w.Tier) + ": "
                         + CareerText.Format("stadium.weeks_left", w.RemainingWeeks);
            }

            _stadiumInfo.text = s.Name + "\n" + CareerText.Format("career.capacity", CareerText.Count(s.Capacity)) + "\n"
                                + CareerText.Format("stadium.parking_spaces", CareerText.Count(s.ParkingSpaces)) + "\n"
                                + Locale.Get("stadium.pitch") + ": " + Locale.Get("pitch." + (int)s.Pitch) + "\n"
                                + CareerText.Format("stadium.levels", s.EquipmentLevel, s.ServicesLevel)
                                + (works.Length > 0 ? "\n\n" + Locale.Get("stadium.in_progress") + works : string.Empty);
        }

        private void TapTickets(Vector2 p)
        {
            long step = (long)(_config.Attendance.ReferencePrice * (double)PriceStepShare);
            for (int i = 0; i < _levels.Length; i++)
            {
                if (_levels[i].Contains(p))
                {
                    Session.SetTicketLevel((TicketLevel)i);
                    _chosenPrice = MatchAttendanceCalculator.PriceOf((TicketLevel)i, _config.Attendance);
                    Host.SetStatus(CareerText.Format("tickets.level_set", Locale.Get("tickets.level." + i)));
                }
            }

            if (_minus.Contains(p)) { _chosenPrice = Math.Max(step, _chosenPrice - step); }
            else if (_plus.Contains(p)) { _chosenPrice += step; }
            else if (_apply.Contains(p))
            {
                Session.SetNextTicketPrice(_chosenPrice);
                Host.SetStatus(CareerText.Format("tickets.applied", CareerText.Price(_chosenPrice)));
            }
            else if (_stadiumButton.Contains(p))
            {
                Host.Open(CareerSection.Stadium);
                return;
            }

            RefreshTickets();
        }
    }
}
