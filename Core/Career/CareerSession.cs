using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Football.Core.Common;
using Football.Core.Competitions;
using Football.Core.Data;
using Football.Core.Domain;
using Football.Core.Management;
using Football.Core.MatchCore;
using Football.Core.MatchSim;

namespace Football.Core.Career
{
    /// <summary>Qué pasó al avanzar un día.</summary>
    public enum DayOutcome
    {
        /// <summary>Día normal: se puede seguir avanzando.</summary>
        Continue,
        /// <summary>Hoy juega el club del usuario: hay que jugarlo (motor 3D) o simularlo antes de seguir.</summary>
        UserMatchToday,
        /// <summary>Terminó la temporada (descensos, clasificados, retiros y juveniles ya aplicados).</summary>
        SeasonFinished,
        /// <summary>La directiva despidió al DT: la carrera terminó.</summary>
        ManagerSacked
    }

    /// <summary>Parámetros de todos los sistemas de la carrera, leídos de <c>Data/balance/</c>.</summary>
    public sealed class CareerConfigs
    {
        /// <summary>Calendario, pases, descensos y valores iniciales.</summary>
        public CareerConfig Career = new CareerConfig();
        /// <summary>Economía.</summary>
        public FinanceConfig Finance = new FinanceConfig();
        /// <summary>Plantel, mercado, vestuario y cantera.</summary>
        public ManagementConfig Management = new ManagementConfig();
        /// <summary>Simulador rápido.</summary>
        public MatchSimConfig Sim = new MatchSimConfig();
        /// <summary>Notas de los jugadores.</summary>
        public ReportConfig Report = new ReportConfig();
        /// <summary>Partido en tiempo real (estilos de juego).</summary>
        public MatchConfig Match = new MatchConfig();
    }

    /// <summary>Fila de la tabla de promedios.</summary>
    public struct AverageRow
    {
        /// <summary>Club.</summary>
        public int ClubId;
        /// <summary>Puntos por partido en las temporadas que cuentan.</summary>
        public float Average;
    }

    /// <summary>
    /// Orquestador del modo carrera: avanza el calendario día a día (<see cref="SeasonClock"/>) y conecta todos los sistemas
    /// por el bus de eventos. Cada día: recuperación física, partidos de la fecha (simulador rápido para los ajenos; el del
    /// usuario se juega en 3D o se simula), finanzas semanales y mensuales, obras, informes de ojeadores, ofertas de rivales
    /// con el libro de pases abierto, vestuario y buzón. Al terminar la temporada: descensos por promedios, clasificados a la
    /// copa por la tabla anual, retiros (los veteranos pueden pasar al cuerpo técnico) y juveniles nuevos.
    /// Todo el azar sale de la semilla de la partida y de la fecha, así el resultado no depende de cuándo se guardó.
    /// </summary>
    public sealed partial class CareerSession : IWageBill
    {
        private const int MaxDaysToNextMatch = 400;
        private const int DaysPerWeek = 7;
        private const int WeeksPerYear = 52;
        private const int ClubValueFromCash = 3;
        private const int ReputationStepForQualifying = 3;
        private const int ReputationStepForRelegation = 8;
        private const int MinReputation = 1;
        private const int MaxReputation = 100;

        private readonly CareerConfigs _cfg;
        private readonly EventBus _bus = new EventBus();
        private readonly SimClock _simClock;
        private readonly SeasonClock _clock;
        private readonly FinanceSystem _finance;
        private readonly ScoutingSystem _scouting;
        private readonly TransferMarket _market;
        private readonly YouthIntake _youth;
        private readonly DressingRoom _dressing;
        private readonly Inbox _inbox;
        private readonly LivingHistory _history;
        private readonly ReportApplier _applier;
        private readonly Dictionary<int, int> _scoutAssignments = new Dictionary<int, int>();
        private readonly List<PendingOffer> _offers = new List<PendingOffer>();
        private readonly Formation _aiFormation;
        private IRandom _dayRng;

        /// <summary>Estado completo de la partida (lo que se guarda).</summary>
        public CareerData Data { get; }

        /// <summary>Partido del usuario que se juega hoy y todavía no tiene resultado (null si no hay).</summary>
        public Fixture PendingUserFixture { get; private set; }

        /// <summary>Ofertas de rivales por jugadores del usuario, pendientes de respuesta.</summary>
        public IReadOnlyList<PendingOffer> Offers => _offers;

        /// <summary>Buzón del usuario.</summary>
        public Inbox Inbox => _inbox;

        /// <summary>Sistema de finanzas (presupuestos, obras, asistencia).</summary>
        public FinanceSystem Finance => _finance;

        /// <summary>Conocimiento de los ojeadores.</summary>
        public ScoutingSystem Scouting => _scouting;

        /// <summary>Mercado de pases.</summary>
        public TransferMarket Market => _market;

        /// <summary>Historia del club: récords y salón de la fama.</summary>
        public LivingHistory History => _history;

        /// <summary>Jugadores seguidos por un ojeador y días que faltan para su informe.</summary>
        public IReadOnlyDictionary<int, int> ScoutAssignments => _scoutAssignments;

        /// <summary>Arma la sesión sobre un estado nuevo o cargado.</summary>
        /// <param name="data">Estado de la partida.</param>
        /// <param name="configs">Parámetros.</param>
        public CareerSession(CareerData data, CareerConfigs configs)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            _cfg = configs ?? throw new ArgumentNullException(nameof(configs));
            _aiFormation = TeamBuilder.ParseFormation(_cfg.Career.Formation);
            _simClock = new SimClock(data.Today);
            SeedAnnouncedCompetitions();
            // La semana contable cierra cada domingo: depende solo de la fecha, igual en una partida nueva y en una cargada.
            _clock = new SeasonClock(_simClock, _bus, ((int)data.Today.DayOfWeek + DaysPerWeek - 1) % DaysPerWeek);
            _finance = new FinanceSystem(_cfg.Finance, this, _bus);
            foreach (Club club in data.Clubs)
            {
                _finance.Register(data.Finances[club.Id], data.Facilities[club.Id]);
            }

            _scouting = new ScoutingSystem(_cfg.Management);
            foreach ((int clubId, int playerId, float knowledge) in data.Scouting)
            {
                _scouting.Restore(clubId, playerId, knowledge);
            }

            _market = new TransferMarket(_cfg.Management);
            int nextPlayer = data.Players.Count == 0 ? 1 : data.Players.Keys.Max() + 1;
            int nextStaff = data.Staff.Count == 0 ? 1 : data.Staff.Max(s => s.Id) + 1;
            _youth = new YouthIntake(_cfg.Management, _bus, nextPlayer);
            _dressing = new DressingRoom(_cfg.Management, _bus);
            _inbox = new Inbox(data.UserClubId, _bus, () => Data.Today, ClubOfPlayer);
            _inbox.Restore(data.News);
            _history = new LivingHistory(_cfg.Management, _bus, nextStaff);
            foreach (RecordEntry r in data.Records)
            {
                _history.RestoreRecord(r);
            }

            foreach (KeyValuePair<int, int> g in data.CareerGoals)
            {
                _history.RestoreCareerGoals(g.Key, g.Value);
            }

            foreach (int id in data.HallOfFame)
            {
                _history.RestoreHallOfFame(id);
            }

            _applier = new ReportApplier(_cfg.Management, _bus);
            _bus.Subscribe<MonthClosed>(OnMonthClosed);
            _bus.Subscribe<WeekPassed>(OnWeekPassed);
            _dayRng = DayRandom();
            PendingUserFixture = TodaysUserFixture();
        }

        // ------------------------------------------------------------------ partida nueva

        /// <summary>
        /// Crea una partida nueva con los clubes de un archivo de equipos (por ejemplo <c>Data/teams/argentina.json</c>).
        /// La edad, el potencial y los atributos detallados de cada jugador se generan a partir de su media, con la semilla.
        /// </summary>
        /// <param name="db">Equipos y clásicos.</param>
        /// <param name="userTeam">Nombre del club del usuario.</param>
        /// <param name="season">Temporada inicial (año).</param>
        /// <param name="seed">Semilla de la partida.</param>
        /// <param name="configs">Parámetros.</param>
        /// <param name="tournamentOnly">Modo Torneo: un solo campeonato a una rueda, sin temporadas siguientes.</param>
        /// <param name="manager">Director técnico del usuario (null = uno sin nombre).</param>
        /// <param name="primeraSize">Clubes de Primera (los mejores por su puesto de la tabla anual anterior o, sin dato, por la media del plantel); los demás juegan el Nacional B. 0 o todos los clubes = una sola división.</param>
        public static CareerSession NewGame(TeamDatabase db, string userTeam, int season, int seed, CareerConfigs configs, bool tournamentOnly = false,
            ManagerProfile manager = null, int primeraSize = 0)
        {
            CareerConfig cc = configs.Career;
            var rng = new SeededRandom((ulong)(uint)seed);
            var market = new TransferMarket(configs.Management);
            DateTime start = new DateTime(season, cc.SeasonStartMonth, cc.SeasonStartDay).AddDays(-cc.DaysBetweenRounds);
            var data = new CareerData { Seed = seed, Season = season, Today = start, TournamentOnly = tournamentOnly, Manager = manager ?? new ManagerProfile() };
            data.Supporters.Relation = cc.Supporters.StartingRelation;
            data.Tactics.Formation = cc.Formation;
            int nextPlayer = 1, nextStaff = 1;
            var strength = new Dictionary<int, double>(); // para repartir las divisiones
            for (int t = 0; t < db.Teams.Length; t++)
            {
                TeamDefinition def = db.Teams[t];
                var club = new Club
                {
                    Id = t + 1, Name = def.Name, ShortName = def.ShortName ?? def.Name, Country = "Argentina",
                    PrimaryColor = def.Home.Shirt, SecondaryColor = def.Home.Secondary
                };
                data.Clubs.Add(club);
                if (def.Name == userTeam)
                {
                    data.UserClubId = club.Id;
                }

                IRandom teamRng = rng.Fork("club" + club.Id);
                foreach (PlayerDefinition pd in def.Players)
                {
                    Player p = CreatePlayer(pd, nextPlayer++, club.Id, start, teamRng, cc);
                    long wage = market.WageDemand(p, PlayingTimePromise.Regular);
                    p.Contract = new Contract
                    {
                        WeeklyWage = wage, ExpiresOn = new DateTime(season + teamRng.NextInt(cc.ContractYears[0], cc.ContractYears[1] + 1) - 1, 12, 31),
                        ReleaseClause = (long)(market.Value(p, start) * (double)cc.ReleaseClauseFactor)
                    };
                    data.Players[p.Id] = p;
                    data.Conditions[p.Id] = new PlayerCondition { PlayerId = p.Id };
                }

                float rating = (float)def.Players.OrderByDescending(x => x.Rating).Take(TeamBuilder.MinPlayers).Average(x => x.Rating);
                strength[club.Id] = def.Club != null && def.Club.AnnualRank > 0 ? 1000.0 - def.Club.AnnualRank : rating;
                int reputation = (int)MathUtil.Clamp(50f + (rating - cc.ReputationPivotRating) * cc.ReputationPerRating, MinReputation, MaxReputation);
                long wages = data.Players.Values.Where(p => p.ClubId == club.Id).Sum(p => p.Contract.WeeklyWage);
                // El mercado pide el mismo sueldo en todos lados; un club chico no lo puede pagar. Se ajusta la masa salarial a lo que el club factura.
                double expectedRevenue = (double)cc.RevenuePerReputation * Math.Max(1, reputation - cc.RevenueFloorReputation);
                double wageScale = Math.Min(cc.MaxWageScale, Math.Max(cc.MinWageScale, expectedRevenue * cc.WageToRevenue / WeeksPerYear / Math.Max(1L, wages)));
                foreach (Player p in data.Players.Values.Where(p => p.ClubId == club.Id))
                {
                    p.Contract.WeeklyWage = (long)(p.Contract.WeeklyWage * wageScale);
                }

                wages = data.Players.Values.Where(p => p.ClubId == club.Id).Sum(p => p.Contract.WeeklyWage);
                // Presupuestos del archivo (estimaciones propias por club) o, si faltan, fórmulas por reputación.
                ClubInfo info = def.Club ?? new ClubInfo();
                long transfer = info.TransferBudget > 0 ? info.TransferBudget : (long)(reputation * cc.CashPerReputation * (double)cc.TransferBudgetShare);
                long cash = Math.Max(reputation * cc.CashPerReputation, transfer * 2);
                long weeklyWages = Math.Max(info.AnnualWageBudget / WeeksPerYear, (long)(wages * (double)cc.WageBudgetMargin));
                data.Finances[club.Id] = new ClubFinances
                {
                    ClubId = club.Id, Cash = cash, TransferBudget = transfer, WeeklyWageBudget = weeklyWages,
                    Members = reputation * cc.MembersPerReputation,
                    TicketPrice = MatchAttendanceCalculator.PriceOf(TicketLevel.Normal, cc.Attendance), Reputation = reputation
                };
                ClubTier tier = BoardExpectations.ParseTier(info.Tier);
                data.ClubInfos[club.Id] = new ClubInfo
                {
                    Founded = info.Founded, Stadium = info.Stadium, Capacity = info.Capacity, TransferBudget = transfer,
                    AnnualWageBudget = weeklyWages * WeeksPerYear, ClubValue = info.ClubValue > 0 ? info.ClubValue : cash * ClubValueFromCash,
                    Tier = tier.ToString(), History = info.History
                };
                data.Stadiums[club.Id] = new StadiumState
                {
                    Name = string.IsNullOrEmpty(info.Stadium) ? def.Name : info.Stadium,
                    Capacity = info.Capacity > 0 ? info.Capacity : configs.Finance.StadiumCapacity[0],
                    ParkingSpaces = info.ParkingSpaces > 0 ? info.ParkingSpaces : cc.InitialParking[(int)tier],
                    Pitch = Enum.TryParse(info.Pitch, true, out PitchCondition pitch) ? pitch : (PitchCondition)cc.InitialPitch[(int)tier]
                };
                club.TransferBudget = data.Finances[club.Id].TransferBudget;
                club.WageBudget = data.Finances[club.Id].WeeklyWageBudget;
                data.Facilities[club.Id] = new ClubFacilities { ClubId = club.Id };
                foreach (StaffRole role in new[] { StaffRole.Manager, StaffRole.Scout, StaffRole.Physio, StaffRole.YouthCoach })
                {
                    int ability = (int)MathUtil.Clamp(cc.StaffAbilityPivot + (reputation - 50) / 10 + teamRng.NextInt(-2, 3), 1, 20);
                    data.Staff.Add(new StaffMember
                    {
                        Id = nextStaff++, ClubId = club.Id, Role = role, Ability = ability, WeeklyWage = ability * cc.StaffWagePerAbility,
                        Name = StaffNames[(club.Id * 7 + (int)role) % StaffNames.Length]
                    });
                }
            }

            if (data.UserClubId == 0)
            {
                throw new ArgumentException("El club del usuario no está en el archivo: " + userTeam, nameof(userTeam));
            }

            if (!tournamentOnly && primeraSize > 0 && primeraSize < data.Clubs.Count)
            {
                if (primeraSize < 2 || data.Clubs.Count - primeraSize < cc.MinSecondDivisionClubs)
                {
                    throw new ArgumentException("Con " + primeraSize + " clubes en Primera quedan menos de " + cc.MinSecondDivisionClubs + " en el Nacional B.", nameof(primeraSize));
                }

                int placed = 0;
                foreach (Club c in data.Clubs.OrderByDescending(c => strength[c.Id]).ThenBy(c => c.Id))
                {
                    c.Division = ++placed <= primeraSize ? 1 : 2;
                }

                data.PrimeraSize = primeraSize;
            }

            foreach (RivalryDefinition r in db.Rivalries)
            {
                Club a = data.Clubs.Find(c => c.Name == r.TeamA), b = data.Clubs.Find(c => c.Name == r.TeamB);
                if (a != null && b != null && Enum.TryParse(r.Level, true, out RivalryLevel level))
                {
                    data.Rivalries.Add(new ClubRivalry { ClubA = a.Id, ClubB = b.Id, Level = level, Name = r.Name });
                }
            }

            var session = new CareerSession(data, configs);
            session.StartSeason();
            return session;
        }

        private static readonly string[] StaffNames =
        {
            "Carlos Medina", "Jorge Ibáñez", "Raúl Ferreyra", "Néstor Gallo", "Hugo Ledesma", "Oscar Paz", "Daniel Correa",
            "Miguel Arce", "Ricardo Sosa", "Alberto Ruiz", "Sergio Luna", "Mario Benítez", "Pablo Quiroga", "Héctor Vera"
        };

        private static Player CreatePlayer(PlayerDefinition pd, int id, int clubId, DateTime today, IRandom rng, CareerConfig cc)
        {
            string full = (pd.Name ?? string.Empty).Trim();
            int space = full.LastIndexOf(' ');
            int estimated = rng.NextInt(19, 34); // se sortea siempre para que la partida no cambie según el archivo
            int age = pd.Age > 0 ? pd.Age : estimated;
            Position position = TeamBuilder.ParsePosition(pd.Position);
            var p = new Player
            {
                Id = id, ClubId = clubId, FirstName = space > 0 ? full.Substring(0, space) : string.Empty,
                LastName = space > 0 ? full.Substring(space + 1) : full,
                BirthDate = today.AddYears(-age).AddDays(-rng.NextInt(0, 365)), Position = position, JoinedOn = today,
                Personality = (PersonalityTrait)rng.NextInt(0, Enum.GetValues(typeof(PersonalityTrait)).Length)
            };
            RosterFactory.FillAttributes(rng, pd.Rating, position, p.Attributes);
            p.Potential = Math.Min(AttributeScale.Max, pd.Rating + Math.Max(0, 24 - age) * 2);
            return p;
        }

        // ------------------------------------------------------------------ calendario

        /// <summary>
        /// Arma el Apertura y el Clausura de la temporada en curso. Con pocos clubes, todos contra todos; desde
        /// <see cref="CareerConfig.ZoneSplitFrom"/> clubes, dos zonas (cabezas de serie por reputación en serpentina) más los cruces
        /// interzonales, para que la temporada entre en el año. El Clausura repite el Apertura con la localía invertida.
        /// </summary>
        public void StartSeason()
        {
            CareerConfig cc = _cfg.Career;
            int season = Data.Season;
            bool divisions = Data.PrimeraSize > 0 && !Data.TournamentOnly;
            List<int> ids = Data.Clubs.Where(c => !divisions || c.Division == 1).Select(c => c.Id).ToList(); // los de Primera (todos, sin divisiones)
            var first = new DateTime(season, cc.SeasonStartMonth, cc.SeasonStartDay);
            int aperturaId = CompetitionInfo.IdOf(season, TournamentKind.Apertura);
            int clausuraId = CompetitionInfo.IdOf(season, TournamentKind.Clausura);
            Data.Competitions.Add(new CompetitionInfo { Id = aperturaId, Name = "Apertura " + season, Season = season, Kind = TournamentKind.Apertura });
            List<Fixture> apertura = ids.Count >= cc.ZoneSplitFrom
                ? FixtureGenerator.TwoZones(aperturaId, ids.OrderByDescending(c => Data.Finances[c].Reputation).ThenBy(c => c).ToList(), first,
                    cc.DaysBetweenRounds, new SeededRandom(Mix(season)))
                : FixtureGenerator.RoundRobin(aperturaId, ids, first, cc.DaysBetweenRounds, false, new SeededRandom(Mix(season)));
            int nextId = Data.Fixtures.Count == 0 ? 1 : Data.Fixtures.Max(f => f.Id) + 1;
            DateTime clausuraStart = apertura.Max(f => f.Date).AddDays(cc.BreakBetweenTournaments);
            foreach (Fixture f in apertura)
            {
                f.Id = nextId++;
                Data.Fixtures.Add(f);
            }

            Data.SeasonStartCash = Data.Finances[Data.UserClubId].Cash;
            if (Data.TournamentOnly)
            {
                return; // Torneo: una sola rueda
            }

            PostExpiringContractsNews();

            Data.Competitions.Add(new CompetitionInfo { Id = clausuraId, Name = "Clausura " + season, Season = season, Kind = TournamentKind.Clausura });
            StartCup(first, ref nextId);
            if (divisions)
            {
                StartSecondDivision(first, ref nextId);
            }

            foreach (Fixture f in apertura)
            {
                Data.Fixtures.Add(new Fixture
                {
                    Id = nextId++, CompetitionId = clausuraId, Round = f.Round, Group = f.Group, HomeClubId = f.AwayClubId, AwayClubId = f.HomeClubId,
                    Date = clausuraStart.AddDays((f.Round - 1) * cc.DaysBetweenRounds)
                });
            }
        }

        /// <summary>Nacional B: los clubes de segunda juegan todos contra todos, ida y vuelta, desde la primera fecha del año.</summary>
        private void StartSecondDivision(DateTime first, ref int nextId)
        {
            List<int> second = Data.Clubs.Where(c => c.Division == 2).Select(c => c.Id).ToList();
            if (second.Count < 2)
            {
                return;
            }

            int id = CompetitionInfo.IdOf(Data.Season, TournamentKind.NacionalB);
            Data.Competitions.Add(new CompetitionInfo { Id = id, Name = "Nacional B " + Data.Season, Season = Data.Season, Kind = TournamentKind.NacionalB });
            foreach (Fixture f in FixtureGenerator.RoundRobin(id, second, first, _cfg.Career.DaysBetweenRounds, true, new SeededRandom(Mix(Data.Season * 7L + 1))))
            {
                f.Id = nextId++;
                Data.Fixtures.Add(f);
            }
        }

        /// <summary>
        /// Avanza un día. Si hoy juega el usuario y su partido no tiene resultado, no avanza y devuelve
        /// <see cref="DayOutcome.UserMatchToday"/>.
        /// </summary>
        public DayOutcome AdvanceDay()
        {
            if (Data.Manager.Sacked)
            {
                return DayOutcome.ManagerSacked;
            }

            if (PendingUserFixture != null)
            {
                return DayOutcome.UserMatchToday;
            }

            _clock.Advance(1); // semanas y meses: finanzas, obras, vestuario
            Data.Today = _simClock.Today;
            _dayRng = DayRandom();
            _applier.RestDay(Data.Conditions.Values);
            if (Data.Today.DayOfWeek == DayOfWeek.Monday)
            {
                WeeklyScouting();
                RivalOffers();
            }

            foreach (Fixture f in Data.Fixtures.Where(f => f.Date == Data.Today && !f.IsPlayed).ToList())
            {
                if (Involves(f, Data.UserClubId))
                {
                    continue;
                }

                Play(f, null);
            }

            AdvanceCup();
            AdvancePromotion();
            CheckCompetitionEnds();
            ReviewSupporters();
            if (Data.Manager.Sacked)
            {
                return DayOutcome.ManagerSacked;
            }

            PendingUserFixture = TodaysUserFixture();
            if (PendingUserFixture != null)
            {
                return DayOutcome.UserMatchToday;
            }

            if (SeasonFixtures().All(f => f.IsPlayed))
            {
                if (Data.TournamentOnly)
                {
                    FinishTournament();
                    return DayOutcome.SeasonFinished;
                }

                EndSeason();
                return Data.Manager.Sacked ? DayOutcome.ManagerSacked : DayOutcome.SeasonFinished;
            }

            return DayOutcome.Continue;
        }

        /// <summary>Avanza hasta el próximo partido del usuario (o el fin de temporada).</summary>
        public DayOutcome AdvanceToNextMatch()
        {
            for (int i = 0; i < MaxDaysToNextMatch; i++)
            {
                DayOutcome outcome = AdvanceDay();
                if (outcome != DayOutcome.Continue)
                {
                    return outcome;
                }
            }

            return DayOutcome.Continue;
        }

        /// <summary>Próximo partido del usuario sin jugar (null si no quedan en la temporada).</summary>
        public Fixture NextUserFixture() =>
            Data.Fixtures.Where(f => !f.IsPlayed && Involves(f, Data.UserClubId)).OrderBy(f => f.Date).FirstOrDefault();

        // ------------------------------------------------------------------ partidos

        /// <summary>Equipos listos para el motor 3D del partido de hoy del usuario (local y visitante).</summary>
        /// <param name="home">Local.</param>
        /// <param name="away">Visitante.</param>
        public void PrepareUserMatch(out MatchTeamSetup home, out MatchTeamSetup away)
        {
            Fixture f = PendingUserFixture ?? throw new InvalidOperationException("Hoy no juega el usuario.");
            home = Setup(f.HomeClubId, true);
            away = Setup(f.AwayClubId, false);
        }

        /// <summary>Carga el resultado del partido que el usuario jugó en 3D (el informe del motor en tiempo real).</summary>
        /// <param name="report">Informe de <c>MatchEngine.BuildReport()</c>.</param>
        public void CompleteUserMatch(MatchReport report)
        {
            Fixture f = PendingUserFixture ?? throw new InvalidOperationException("Hoy no juega el usuario.");
            Play(f, report ?? throw new ArgumentNullException(nameof(report)));
            PendingUserFixture = null;
            AdvanceCup();
            AdvancePromotion();
            CheckCompetitionEnds();
        }

        /// <summary>Simula con el simulador rápido el partido de hoy del usuario.</summary>
        public MatchReport SimulateUserMatch()
        {
            Fixture f = PendingUserFixture ?? throw new InvalidOperationException("Hoy no juega el usuario.");
            MatchReport report = Play(f, null);
            PendingUserFixture = null;
            AdvanceCup();
            AdvancePromotion();
            CheckCompetitionEnds();
            return report;
        }

        private MatchReport Play(Fixture f, MatchReport report)
        {
            if (report == null)
            {
                MatchTeamSetup home = Setup(f.HomeClubId, true), away = Setup(f.AwayClubId, false);
                report = QuickMatchSimulator.Play(home, away, _dayRng.Fork("f" + f.Id), _cfg.Sim, _cfg.Report);
            }

            MatchImportance importance = ImportanceOf(f);
            long price = TicketPriceFor(f);
            AttendanceResult crowd = MatchAttendanceCalculator.Compute(AttendanceInputFor(f, importance, price), _cfg.Career.Attendance);
            f.HomeGoals = report.HomeGoals;
            f.AwayGoals = report.AwayGoals;
            float injuryFactor = _cfg.Career.Stadium.InjuryFactor[(int)Data.Stadiums[f.HomeClubId].Pitch];
            for (int side = 0; side < 2; side++)
            {
                int clubId = side == 0 ? f.HomeClubId : f.AwayClubId;
                Dictionary<int, PlayerCondition> squadConditions = SquadConditions(clubId);
                _applier.Apply(report, side, squadConditions, Data.Facilities[clubId][FacilityKind.Medical].Level, _dayRng.Fork("a" + f.Id + side));
                ScaleNewInjuries(report.Teams[side], squadConditions, injuryFactor); // el estado del campo también cuenta
                if (clubId == Data.UserClubId)
                {
                    _dressing.RecordMatch(report.Teams[side]);
                }
            }

            _history.RecordMatch(report, f.HomeClubId, f.AwayClubId, Data.Season);
            AddSeasonStats(report);
            _bus.Publish(new HomeMatchPlayed
            {
                Date = Data.Today, ClubId = f.HomeClubId, OpponentReputation = Data.Finances[f.AwayClubId].Reputation,
                Rivalry = RivalryBetween(f.HomeClubId, f.AwayClubId), Spectators = Math.Max(1, crowd.Spectators), TicketPrice = price
            });
            long extras = StadiumRemodelingSystem.MatchdayExtras(Data.Stadiums[f.HomeClubId], crowd.Spectators, _cfg.Career.Stadium);
            if (extras > 0)
            {
                Data.Finances[f.HomeClubId].Record(Data.Today, FinanceCategory.Matchday, extras);
            }

            AfterMatch(f, report, importance, price, crowd);
            if (Involves(f, Data.UserClubId))
            {
                _inbox.Post("news.result", Data.Club(f.HomeClubId).Name, Num(report.HomeGoals), Num(report.AwayGoals), Data.Club(f.AwayClubId).Name);
            }

            return report;
        }

        private void AddSeasonStats(MatchReport report)
        {
            foreach (TeamReport team in report.Teams)
            {
                foreach (PlayerReport p in team.Players)
                {
                    if (p.Minutes <= 0f || !Data.Players.ContainsKey(p.PlayerId))
                    {
                        continue;
                    }

                    if (!Data.SeasonStats.TryGetValue(p.PlayerId, out CardStats s))
                    {
                        s = new CardStats();
                        Data.SeasonStats[p.PlayerId] = s;
                    }

                    s.Matches++;
                    s.Goals += p.Stats.Goals;
                    s.Assists += p.Stats.Assists;
                    if (p.Rating > 0f)
                    {
                        s.RatingSum += p.Rating;
                        s.Rated++;
                    }
                }
            }
        }

        /// <summary>Ficha de un jugador: carta, atributos, puestos, estado y estadísticas de la temporada.</summary>
        /// <param name="playerId">Jugador.</param>
        public PlayerCardInfo Card(int playerId)
        {
            Player p = Data.Players[playerId];
            Data.Conditions.TryGetValue(playerId, out PlayerCondition c);
            Data.SeasonStats.TryGetValue(playerId, out CardStats s);
            return PlayerCards.FromPlayer(p, c, s, Data.Today);
        }

        private MatchTeamSetup Setup(int clubId, bool home)
        {
            Club club = Data.Club(clubId);
            List<Player> squad = Squad(clubId);
            bool user = clubId == Data.UserClubId;
            Formation formation = user ? UserFormation() : _aiFormation;
            MatchTeamSetup setup = MatchPreparation.Prepare(club.Name, squad, Data.Conditions, formation, user ? UserStyle() : new TeamStyle(), home,
                _cfg.Management, user ? Data.Tactics.Lineup : null);
            if (user && !string.IsNullOrEmpty(Data.Tactics.DefensiveFormation))
            {
                Formation defensive = TeamBuilder.ParseFormation(Data.Tactics.DefensiveFormation);
                setup.DefensiveFormation = defensive.Slots.Length == formation.Slots.Length ? defensive : null;
            }

            int other = PendingOrNextOpponent(clubId);
            setup.Rivalry = other > 0 ? RivalryBetween(clubId, other) : RivalryLevel.None;
            return setup;
        }

        /// <summary>
        /// Alineación que saldría hoy: la elegida por el DT (con los lesionados y sancionados reemplazados) o la automática.
        /// Sirve para dibujar el once y el banco en la pantalla de alineación.
        /// </summary>
        public MatchPreparation.Lineup UserLineup() =>
            MatchPreparation.Resolve(Data.Club(Data.UserClubId).Name, Squad(Data.UserClubId), Data.Conditions, UserFormation(), Data.Tactics.Lineup);

        /// <summary>Si el DT eligió su alineación (false = la arma la computadora).</summary>
        public bool HasCustomLineup => !string.IsNullOrWhiteSpace(Data.Tactics.Lineup);

        /// <summary>
        /// Guarda la alineación elegida: un titular por puesto de la formación y el banco. Rechaza ids que no sean del plantel, repetidos o
        /// una cantidad de titulares distinta de los puestos.
        /// </summary>
        /// <param name="starterIds">Ids de los titulares, uno por puesto.</param>
        /// <param name="benchIds">Ids de los suplentes (hasta <see cref="MatchPreparation.BenchSize"/>).</param>
        /// <returns>Verdadero si quedó guardada.</returns>
        public bool SetUserLineup(IReadOnlyList<int> starterIds, IReadOnlyList<int> benchIds)
        {
            var squad = new HashSet<int>(Squad(Data.UserClubId).Select(p => p.Id));
            var all = starterIds.Concat(benchIds).ToList();
            if (starterIds.Count != UserFormation().Slots.Length || benchIds.Count > MatchPreparation.BenchSize
                || all.Any(id => !squad.Contains(id)) || all.Distinct().Count() != all.Count)
            {
                return false;
            }

            Data.Tactics.Lineup = MatchPreparation.FormatLineup(starterIds, benchIds);
            return true;
        }

        /// <summary>Vuelve a la alineación automática.</summary>
        public void ClearUserLineup() => Data.Tactics.Lineup = string.Empty;

        /// <summary>Formación con balón del usuario.</summary>
        public Formation UserFormation() => TeamBuilder.ParseFormation(Data.Tactics.Formation);

        /// <summary>Estilo del usuario con los roles por línea aplicados.</summary>
        public TeamStyle UserStyle()
        {
            TeamStyle style = TeamBuilder.ParseStyle(Data.Tactics.Style, _cfg.Match).Clone();
            style.FullbackDuty = Data.Tactics.FullbackDuty;
            style.MidfieldDuty = Data.Tactics.MidfieldDuty;
            style.StrikerDuty = Data.Tactics.StrikerDuty;
            return style;
        }

        private int PendingOrNextOpponent(int clubId)
        {
            Fixture f = Data.Fixtures.Where(x => !x.IsPlayed && Involves(x, clubId)).OrderBy(x => x.Date).FirstOrDefault();
            return f == null ? 0 : f.HomeClubId == clubId ? f.AwayClubId : f.HomeClubId;
        }

        // ------------------------------------------------------------------ tablas

        /// <summary>Torneo en juego (el Apertura hasta que termina, después el Clausura).</summary>
        public CompetitionInfo CurrentTournament()
        {
            if (UserDivision == 2)
            {
                return Data.Competitions.Find(c => c.Id == CompetitionInfo.IdOf(Data.Season, TournamentKind.NacionalB));
            }

            CompetitionInfo apertura = Data.Competitions.Find(c => c.Id == CompetitionInfo.IdOf(Data.Season, TournamentKind.Apertura));
            bool aperturaOver = Data.Fixtures.Where(f => f.CompetitionId == apertura.Id).All(f => f.IsPlayed);
            CompetitionInfo clausura = Data.Competitions.Find(c => c.Id == CompetitionInfo.IdOf(Data.Season, TournamentKind.Clausura));
            return aperturaOver && clausura != null ? clausura : apertura;
        }

        /// <summary>Tabla del torneo en juego (desempate argentino).</summary>
        public List<Standing> TournamentTable() => Table(f => f.CompetitionId == CurrentTournament().Id);

        /// <summary>Tabla anual: Apertura y Clausura de la temporada en curso sumados.</summary>
        public List<Standing> AnnualTable() => AnnualTable(Data.Season);

        /// <summary>Tabla anual de una temporada (la en curso o una ya jugada).</summary>
        /// <param name="season">Temporada (año).</param>
        public List<Standing> AnnualTable(int season) => AnnualTable(season, UserDivision);

        /// <summary>Tabla anual de una división: Apertura y Clausura sumados (Primera) o el Nacional B (segunda).</summary>
        /// <param name="season">Temporada (año).</param>
        /// <param name="division">1 = Primera, 2 = Nacional B.</param>
        public List<Standing> AnnualTable(int season, int division) =>
            Table(f => f.CompetitionId / 10 == season && (division == 2 ? KindOf(f) == TournamentKind.NacionalB : IsFirstDivisionLeague(f)));

        /// <summary>División del club del usuario (1 si no hay divisiones).</summary>
        public int UserDivision => Data.PrimeraSize > 0 && !Data.TournamentOnly ? Data.Club(Data.UserClubId).Division : 1;

        private static TournamentKind KindOf(Fixture f) => (TournamentKind)(f.CompetitionId % 10);

        private static bool IsFirstDivisionLeague(Fixture f) => KindOf(f) == TournamentKind.Apertura || KindOf(f) == TournamentKind.Clausura;

        /// <summary>Partido de una liga (Primera o Nacional B): cuenta para los promedios; la copa y la promoción no.</summary>
        private static bool IsLeague(Fixture f) => IsFirstDivisionLeague(f) || KindOf(f) == TournamentKind.NacionalB;

        /// <summary>Clubes de Primera esta temporada (todos si no hay divisiones).</summary>
        private List<int> PrimeraIds() => Data.Clubs.Where(c => Data.PrimeraSize == 0 || c.Division == 1).Select(c => c.Id).ToList();

        /// <summary>Tabla de promedios: puntos por partido de las últimas temporadas (la en curso incluida).</summary>
        public List<AverageRow> AveragesTable()
        {
            AveragesTable averages = BuildAverages();
            return Data.Clubs.Where(c => Data.PrimeraSize == 0 || c.Division == 1).Select(c => new AverageRow { ClubId = c.Id, Average = averages.Average(c.Id, Data.Season) })
                .OrderByDescending(r => r.Average).ToList();
        }

        private List<Standing> Table(Func<Fixture, bool> filter)
        {
            // Los clubes de la tabla son los que juegan esas fechas (en dos divisiones, cada una tiene la suya).
            List<Fixture> all = Data.Fixtures.Where(filter).ToList();
            IEnumerable<int> members = all.SelectMany(f => new[] { f.HomeClubId, f.AwayClubId }).Distinct();
            var table = new LeagueTable(all.Count == 0 ? Data.Clubs.Select(c => c.Id) : members, LeagueTable.ArgentineOrder);
            foreach (Fixture f in all.Where(f => f.IsPlayed))
            {
                table.Add(f);
            }

            return table.Ranked();
        }

        private AveragesTable BuildAverages()
        {
            var averages = new AveragesTable(_cfg.Career.AveragesSeasons);
            foreach (int season in Data.Competitions.Select(c => c.Season).Distinct())
            {
                foreach (Standing s in Table(f => f.CompetitionId / 10 == season && IsLeague(f)).Where(s => s.Played > 0))
                {
                    averages.Record(s.ClubId, season, s.Points, s.Played);
                }
            }

            return averages;
        }

        // ------------------------------------------------------------------ fin de temporada

        /// <summary>El torneo (modo Torneo) ya terminó: no quedan partidos.</summary>
        public bool TournamentOver => Data.TournamentOnly && SeasonFixtures().All(f => f.IsPlayed);

        /// <summary>Campeón del torneo en juego o del último terminado (el primero de la tabla).</summary>
        public int ChampionId() => TournamentTable().First().ClubId;

        private bool _championAnnounced;

        private void FinishTournament()
        {
            if (_championAnnounced || Data.News.Concat(_inbox.Chronological).Any(n => n.Key == "news.champion"))
            {
                return;
            }

            _championAnnounced = true;
            _inbox.Post("news.champion", Data.Club(ChampionId()).Name);
        }

        private void EndSeason()
        {
            CareerConfig cc = _cfg.Career;
            ReviewSeasonWithTheBoard();
            if (HasDivisions)
            {
                ApplyDivisionChanges();
            }
            else
            {
                List<int> ids = Data.Clubs.Select(c => c.Id).ToList();
                foreach (int clubId in BuildAverages().Worst(ids, Data.Season).Take(cc.RelegatedClubs))
                {
                    ClubFinances fin = Data.Finances[clubId];
                    fin.Reputation = Math.Max(MinReputation, fin.Reputation - ReputationStepForRelegation);
                    _inbox.Post(clubId == Data.UserClubId ? "news.user_relegated" : "news.relegated", Data.Club(clubId).Name);
                }
            }

            foreach (Standing s in AnnualTable(Data.Season, 1).Take(cc.CupQualifiers))
            {
                ClubFinances fin = Data.Finances[s.ClubId];
                fin.Reputation = Math.Min(MaxReputation, fin.Reputation + ReputationStepForQualifying);
                _inbox.Post("news.qualified", Data.Club(s.ClubId).Name);
            }

            List<Player> active = Data.Players.Values.Where(p => p.ClubId.HasValue).ToList();
            List<Player> retired = _history.SeasonEnd(active, Data.Today, _dayRng.Fork("retiros"), out List<StaffMember> newStaff);
            foreach (Player p in retired)
            {
                foreach (StaffMember s in newStaff.Where(s => s.FormerPlayerId == p.Id))
                {
                    s.ClubId = p.ClubId;
                    s.WeeklyWage = s.Ability * cc.StaffWagePerAbility;
                    Data.Staff.Add(s);
                }

                p.ClubId = null;
                p.Contract = null;
            }

            ProcessExpiredContracts();

            foreach (Club club in Data.Clubs)
            {
                StaffMember coach = Data.Staff.Find(s => s.ClubId == club.Id && s.Role == StaffRole.YouthCoach);
                foreach (Player p in _youth.Generate(club.Id, Data.Facilities[club.Id][FacilityKind.YouthAcademy].Level,
                             coach?.Ability ?? cc.StaffAbilityPivot, Data.Today, _dayRng.Fork("cantera" + club.Id)))
                {
                    p.Contract = new Contract
                    {
                        WeeklyWage = _market.WageDemand(p, PlayingTimePromise.Youngster), ExpiresOn = ContractEnd(cc.YouthContractYears + 1) // el contrato corre desde la temporada que viene
                    };
                    Data.Players[p.Id] = p;
                    Data.Conditions[p.Id] = new PlayerCondition { PlayerId = p.Id };
                }
            }

            ReleaseSurplusPlayers();
            RefillAiSquads();

            foreach (PlayerCondition c in Data.Conditions.Values)
            {
                c.SeasonMinutes = 0f;
            }

            Data.SeasonStats.Clear();

            _inbox.Post("news.season_end", Data.Season.ToString(CultureInfo.InvariantCulture));
            Data.Season++;
            StartSeason();
        }

        /// <summary>
        /// Los contratos no vencen, así que sin esto los planteles crecerían con cada camada de juveniles. Los clubes de la computadora
        /// sueltan al excedente sobre <see cref="CareerConfig.MaxAiSquadSize"/> (los de menor nivel, sin dejar menos arqueros que
        /// <see cref="CareerConfig.MinAiGoalkeepers"/>); pasan a ser jugadores libres. El club del usuario no se toca.
        /// </summary>
        private void ReleaseSurplusPlayers()
        {
            CareerConfig cc = _cfg.Career;
            foreach (Club club in Data.Clubs.Where(c => c.Id != Data.UserClubId))
            {
                List<Player> squad = Squad(club.Id);
                int goalkeepers = squad.Count(p => p.Position == Position.Goalkeeper);
                foreach (Player p in squad.OrderBy(x => PlayerRatings.Overall(x.Attributes, x.Position)).ThenBy(x => x.Id))
                {
                    if (squad.Count <= cc.MaxAiSquadSize)
                    {
                        break;
                    }

                    if (p.Position == Position.Goalkeeper)
                    {
                        if (goalkeepers <= cc.MinAiGoalkeepers)
                        {
                            continue;
                        }

                        goalkeepers--;
                    }

                    p.ClubId = null;
                    p.Contract = null;
                    squad.Remove(p);
                }
            }
        }

        // ------------------------------------------------------------------ ojeadores y mercado

        /// <summary>Manda al ojeador del club a seguir a un jugador ajeno; el informe llega en <see cref="CareerConfig.ScoutDays"/> días.</summary>
        /// <param name="playerId">Jugador.</param>
        /// <returns>Falso si el jugador es propio o ya se lo sigue.</returns>
        public bool SendScout(int playerId)
        {
            if (!Data.Players.TryGetValue(playerId, out Player p) || p.ClubId == Data.UserClubId || _scoutAssignments.ContainsKey(playerId))
            {
                return false;
            }

            _scoutAssignments[playerId] = _cfg.Career.ScoutDays;
            return true;
        }

        private void WeeklyScouting()
        {
            const int week = 7;
            int ability = Data.Staff.Find(s => s.ClubId == Data.UserClubId && s.Role == StaffRole.Scout)?.Ability ?? 1;
            foreach (int playerId in _scoutAssignments.Keys.ToList())
            {
                int days = Math.Min(week, _scoutAssignments[playerId]);
                _scouting.Scout(Data.UserClubId, playerId, days, ability);
                _scoutAssignments[playerId] -= days;
                if (_scoutAssignments[playerId] <= 0)
                {
                    _scoutAssignments.Remove(playerId);
                    Player p = Data.Players[playerId];
                    _inbox.Post("news.scout_report", FullName(p), Num((int)Math.Round(_scouting.Knowledge(Data.UserClubId, playerId) * 100f)));
                }
            }
        }

        /// <summary>Hoy está abierto el libro de pases.</summary>
        public bool TransferWindowOpen => Array.IndexOf(_cfg.Career.TransferWindowMonths, Data.Today.Month) >= 0;

        private void RivalOffers()
        {
            if (!TransferWindowOpen || !_dayRng.Chance(_cfg.Career.RivalOfferChancePerWeek))
            {
                return;
            }

            double share = _cfg.Career.RivalOfferShare;
            long cheapest = Squad(Data.UserClubId).Select(p => (long)(_market.Value(p, Data.Today) * share)).DefaultIfEmpty(long.MaxValue).Min();
            // Solo ofertan los rivales que pueden pagar al menos a uno de los jugadores del usuario.
            List<Club> others = Data.Clubs.Where(c => c.Id != Data.UserClubId && Data.Finances[c.Id].TransferBudget >= cheapest).ToList();
            if (others.Count == 0)
            {
                return;
            }

            Club buyer = others[_dayRng.NextInt(0, others.Count)];
            long budget = Data.Finances[buyer.Id].TransferBudget;
            // El rival elige según su perfil, pero solo entre los que puede pagar y por los que no hay otra oferta.
            IEnumerable<Player> affordable = Squad(Data.UserClubId)
                .Where(p => _market.Value(p, Data.Today) * share <= budget && _offers.All(o => o.Offer.PlayerId != p.Id));
            Player target = TransferMarket.AiTarget(ProfileOf(buyer.Id), affordable, Data.Today);
            if (target == null)
            {
                return;
            }

            long fee = (long)(_market.Value(target, Data.Today) * share);

            _offers.Add(new PendingOffer
            {
                Received = Data.Today,
                Offer = new TransferOffer { PlayerId = target.Id, BuyerClubId = buyer.Id, SellerClubId = Data.UserClubId, Fee = fee }
            });
            _inbox.Post("news.offer_received", buyer.Name, FullName(target), Num(fee));
        }

        /// <summary>Acepta o rechaza una oferta recibida.</summary>
        /// <param name="offer">Oferta pendiente.</param>
        /// <param name="accept">Verdadero para vender.</param>
        public void Respond(PendingOffer offer, bool accept)
        {
            if (!_offers.Remove(offer))
            {
                return;
            }

            if (accept)
            {
                Transfer(offer.Offer);
            }
        }

        /// <summary>
        /// Oferta del usuario por un jugador de otro club. El club vendedor responde según su perfil (comprador, vendedor de
        /// juveniles, defensivo); con «aceptada», el pase se cierra en el momento si alcanza el presupuesto de fichajes.
        /// </summary>
        /// <param name="playerId">Jugador.</param>
        /// <param name="fee">Monto ofrecido.</param>
        /// <param name="counter">Contraoferta del vendedor (si la hay).</param>
        public OfferResponse MakeOffer(int playerId, long fee, out long counter)
        {
            counter = 0;
            Player p = Data.Players[playerId];
            if (!p.ClubId.HasValue || p.ClubId == Data.UserClubId || !TransferWindowOpen || fee > Data.Finances[Data.UserClubId].TransferBudget)
            {
                return OfferResponse.Rejected;
            }

            var offer = new TransferOffer { PlayerId = playerId, BuyerClubId = Data.UserClubId, SellerClubId = p.ClubId.Value, Fee = fee };
            OfferResponse response = _market.Evaluate(offer, p, ProfileOf(p.ClubId.Value), Data.Today, out counter);
            if (response == OfferResponse.Accepted)
            {
                Transfer(offer);
            }

            return response;
        }

        /// <summary>Paga la cláusula de rescisión: el pase se cierra sin negociar.</summary>
        /// <param name="playerId">Jugador.</param>
        /// <returns>Falso si no hay cláusula, el libro está cerrado o no alcanza el presupuesto.</returns>
        public bool PayReleaseClause(int playerId)
        {
            Player p = Data.Players[playerId];
            long clause = p.Contract?.ReleaseClause ?? 0;
            if (clause <= 0 || !p.ClubId.HasValue || p.ClubId == Data.UserClubId || !TransferWindowOpen
                || clause > Data.Finances[Data.UserClubId].TransferBudget)
            {
                return false;
            }

            Transfer(new TransferOffer { PlayerId = playerId, BuyerClubId = Data.UserClubId, SellerClubId = p.ClubId.Value, Fee = clause });
            return true;
        }

        private void Transfer(TransferOffer offer)
        {
            Player p = Data.Players[offer.PlayerId];
            ClubFinances buyer = Data.Finances[offer.BuyerClubId], seller = Data.Finances[offer.SellerClubId];
            buyer.TransferBudget = Math.Max(0, buyer.TransferBudget - offer.Fee);
            buyer.Record(Data.Today, FinanceCategory.Transfers, -offer.Fee);
            seller.TransferBudget += offer.Fee;
            seller.Record(Data.Today, FinanceCategory.Transfers, offer.Fee);
            p.ClubId = offer.BuyerClubId;
            p.JoinedOn = Data.Today;
            if (offer.BuyerClubId == Data.UserClubId)
            {
                Data.Signings.Add(new SigningRecord { PlayerId = p.Id, Name = FullName(p), Fee = offer.Fee, Date = Data.Today });
            }
            if (p.Contract != null)
            {
                p.Contract.ReleaseClause = (long)(offer.Fee * (double)_cfg.Career.ReleaseClauseFactor);
            }

            _bus.Publish(new TransferCompleted { PlayerId = p.Id, BuyerClubId = offer.BuyerClubId, SellerClubId = offer.SellerClubId, Fee = offer.Fee });
        }

        /// <summary>
        /// Búsqueda de jugadores ajenos por puesto, edad y media visible (la que ven los ojeadores: el centro del rango).
        /// </summary>
        /// <param name="position">Puesto (null = cualquiera).</param>
        /// <param name="minAge">Edad mínima.</param>
        /// <param name="maxAge">Edad máxima.</param>
        /// <param name="minRating">Media visible mínima.</param>
        /// <param name="maxRating">Media visible máxima.</param>
        /// <param name="maxValue">Valor de mercado máximo.</param>
        /// <param name="name">Texto a buscar en el nombre o el apellido (sin importar mayúsculas ni tildes); vacío = todos.</param>
        public List<Player> Search(Position? position, int minAge, int maxAge, int minRating, int maxRating, long maxValue, string name = null)
        {
            string needle = Normalize(name);
            return Data.Players.Values
                .Where(p => (p.ClubId.HasValue && p.ClubId != Data.UserClubId || IsFreeAgent(p)) && (!position.HasValue || p.Position == position.Value))
                .Where(p => needle.Length == 0 || Normalize(p.FirstName + " " + p.LastName).Contains(needle))
                .Where(p => p.AgeOn(Data.Today) >= minAge && p.AgeOn(Data.Today) <= maxAge)
                .Where(p => VisibleRating(p) >= minRating && VisibleRating(p) <= maxRating && _market.Value(p, Data.Today) <= maxValue)
                .OrderByDescending(VisibleRating).ToList();
        }

        /// <summary>Media que ve el usuario: exacta para los propios, aproximada (según los ojeadores) para los ajenos.</summary>
        public int VisibleRating(Player p)
        {
            int real = (int)Math.Round(PlayerRatings.Overall(p.Attributes, p.Position));
            if (p.ClubId == Data.UserClubId)
            {
                return real;
            }

            (int min, int max) = _scouting.Range(Data.UserClubId, p, AttributeId.Finishing);
            float width = (max - min) * (1f - _scouting.Knowledge(Data.UserClubId, p.Id));
            return (int)Math.Round(real + width * ((p.Id * 37 % 11) / 10f - 0.5f) * 0.5f);
        }

        // ------------------------------------------------------------------ club

        /// <summary>Contrata a un integrante del cuerpo técnico (reemplaza al del mismo rol).</summary>
        /// <param name="role">Rol.</param>
        /// <param name="ability">Capacidad (1–20).</param>
        /// <param name="name">Nombre.</param>
        /// <returns>Falso si el sueldo no entra en el presupuesto semanal.</returns>
        public bool HireStaff(StaffRole role, int ability, string name)
        {
            ability = Math.Max(1, Math.Min(20, ability));
            long wage = ability * _cfg.Career.StaffWagePerAbility;
            StaffMember current = Data.Staff.Find(s => s.ClubId == Data.UserClubId && s.Role == role);
            long bill = WeeklyWages(Data.UserClubId) - (current?.WeeklyWage ?? 0) + wage;
            if (bill > Data.Finances[Data.UserClubId].WeeklyWageBudget)
            {
                return false;
            }

            if (current != null)
            {
                current.ClubId = null;
            }

            int id = Data.Staff.Count == 0 ? 1 : Data.Staff.Max(s => s.Id) + 1;
            Data.Staff.Add(new StaffMember { Id = id, ClubId = Data.UserClubId, Role = role, Ability = ability, WeeklyWage = wage, Name = name });
            return true;
        }

        /// <summary>Plantel actual de un club.</summary>
        public List<Player> Squad(int clubId) => Data.Players.Values.Where(p => p.ClubId == clubId).OrderBy(p => p.Id).ToList();

        /// <inheritdoc />
        public long WeeklyWages(int clubId) =>
            Data.Players.Values.Where(p => p.ClubId == clubId && p.Contract != null).Sum(p => p.Contract.WeeklyWage)
            + Data.Staff.Where(s => s.ClubId == clubId).Sum(s => s.WeeklyWage);

        /// <summary>Perfil de mercado de un club (comprador, vendedor de juveniles o defensivo), fijo por club.</summary>
        public static ClubProfile ProfileOf(int clubId) => (ClubProfile)(clubId % Enum.GetValues(typeof(ClubProfile)).Length);

        /// <summary>Clásico entre dos clubes.</summary>
        public RivalryLevel RivalryBetween(int a, int b) =>
            Data.Rivalries.Find(r => (r.ClubA == a && r.ClubB == b) || (r.ClubA == b && r.ClubB == a))?.Level ?? RivalryLevel.None;

        /// <summary>Copia en <see cref="Data"/> el estado de los sistemas que viven en memoria (buzón, ojeadores, historia).</summary>
        public void SyncData()
        {
            Data.News.Clear();
            Data.News.AddRange(_inbox.Chronological);
            Data.Scouting.Clear();
            Data.Scouting.AddRange(_scouting.Entries);
            Data.Records.Clear();
            Data.Records.AddRange(_history.Records);
            Data.CareerGoals.Clear();
            foreach (KeyValuePair<int, int> g in _history.AllCareerGoals)
            {
                Data.CareerGoals[g.Key] = g.Value;
            }

            Data.HallOfFame.Clear();
            Data.HallOfFame.AddRange(_history.HallOfFame);
            foreach (Club club in Data.Clubs)
            {
                club.TransferBudget = Data.Finances[club.Id].TransferBudget;
                club.WageBudget = Data.Finances[club.Id].WeeklyWageBudget;
            }
        }

        // ------------------------------------------------------------------ auxiliares

        private void OnMonthClosed(MonthClosed e)
        {
            MonthlyClubReview(e.Date);
            _dressing.MonthlyReview(Data.UserClubId, Squad(Data.UserClubId), SquadConditions(Data.UserClubId), e.Date);
        }

        private Dictionary<int, PlayerCondition> SquadConditions(int clubId) =>
            Data.Players.Values.Where(p => p.ClubId == clubId).ToDictionary(p => p.Id, p => Data.Conditions[p.Id]);

        private IEnumerable<Fixture> SeasonFixtures() => Data.Fixtures.Where(f => f.CompetitionId / 10 == Data.Season);

        private Fixture TodaysUserFixture() =>
            Data.Fixtures.FirstOrDefault(f => f.Date == Data.Today && !f.IsPlayed && Involves(f, Data.UserClubId));

        private static bool Involves(Fixture f, int clubId) => f.HomeClubId == clubId || f.AwayClubId == clubId;

        private int? ClubOfPlayer(int playerId) => Data.Players.TryGetValue(playerId, out Player p) ? p.ClubId : null;

        private IRandom DayRandom() => new SeededRandom(Mix(Data.Today.Ticks / TimeSpan.TicksPerDay));

        private ulong Mix(long value) => HashUtil.SplitMix64Mix((ulong)(uint)Data.Seed << 32 ^ (ulong)value);

        private static string FullName(Player p) => (p.FirstName + " " + p.LastName).Trim();

        private static string Num(long value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
