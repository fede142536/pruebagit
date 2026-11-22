using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Football.Core.Common;
using Football.Core.Competitions;
using Football.Core.Data;
using Football.Core.Domain;
using Football.Core.Management;

namespace Football.Core.Career
{
    /// <summary>Vista previa del próximo partido de local: rango ideal, semáforo y público esperado.</summary>
    public struct TicketPreview
    {
        /// <summary>Partido (null si no hay partidos de local pendientes).</summary>
        public Fixture Fixture;
        /// <summary>Importancia.</summary>
        public MatchImportance Importance;
        /// <summary>Precio evaluado.</summary>
        public long Price;
        /// <summary>Rango ideal.</summary>
        public long MinIdeal, MaxIdeal;
        /// <summary>Semáforo del precio.</summary>
        public PriceStatus Status;
        /// <summary>Público y recaudación esperados.</summary>
        public AttendanceResult Expected;
    }

    // Club del usuario: Copa Argentina, entradas y público, socios, estadio, directiva, popularidad y medidor de gestión.
    public sealed partial class CareerSession
    {
        private const string CupName = "Copa Argentina ";
        private readonly List<RemodelWork> _completedWorks = new List<RemodelWork>();

        // ------------------------------------------------------------------ Copa Argentina

        /// <summary>Partido de copa.</summary>
        public static bool IsCup(Fixture f) => f.CompetitionId % 10 == (int)TournamentKind.Cup;

        private void StartCup(DateTime firstLeagueDate, ref int nextId)
        {
            int id = CompetitionInfo.IdOf(Data.Season, TournamentKind.Cup);
            Data.Competitions.Add(new CompetitionInfo { Id = id, Name = CupName + Data.Season, Season = Data.Season, Kind = TournamentKind.Cup });
            // Cabezas de serie por reputación: con un número impar de clubes, el mejor pasa directo a la segunda ronda.
            List<int> seeded = Data.Clubs.Select(c => c.Id).OrderByDescending(c => Data.Finances[c].Reputation).ThenBy(c => c).ToList();
            AddCupRound(seeded, 1, firstLeagueDate.AddDays(_cfg.Career.CupFirstOffsetDays), ref nextId);
        }

        private void AddCupRound(List<int> clubs, int round, DateTime date, ref int nextId)
        {
            int id = CompetitionInfo.IdOf(Data.Season, TournamentKind.Cup);
            int byes = clubs.Count % 2;
            for (int i = 0; i < (clubs.Count - byes) / 2; i++)
            {
                int high = clubs[byes + i], low = clubs[clubs.Count - 1 - i];
                Data.Fixtures.Add(new Fixture { Id = nextId++, CompetitionId = id, Round = round, HomeClubId = low, AwayClubId = high, Date = date });
            }
        }

        /// <summary>Cuando termina una ronda de copa, arma la siguiente con los ganadores (y el que pasó directo).</summary>
        private void AdvanceCup()
        {
            int id = CompetitionInfo.IdOf(Data.Season, TournamentKind.Cup);
            List<Fixture> cup = Data.Fixtures.Where(f => f.CompetitionId == id).ToList();
            if (cup.Count == 0)
            {
                return;
            }

            int round = cup.Max(f => f.Round);
            List<Fixture> last = cup.Where(f => f.Round == round).ToList();
            if (!last.All(f => f.IsPlayed))
            {
                return;
            }

            var winners = new List<int>();
            foreach (Fixture f in last)
            {
                winners.Add(Knockout.Winner(f, new SeededRandom(Mix(f.Id * 31L))));
            }

            if (round == 1)
            {
                // El cabeza de serie que no jugó la primera ronda entra ahora.
                winners.AddRange(Data.Clubs.Select(c => c.Id).Where(c => last.All(f => f.HomeClubId != c && f.AwayClubId != c)));
            }

            if (winners.Count == 1)
            {
                if (!Data.News.Concat(_inbox.Chronological).Any(n => n.Key == "news.cup_champion" && n.Args.Length > 1 && n.Args[1] == Num(Data.Season)))
                {
                    _inbox.Post("news.cup_champion", Data.Club(winners[0]).Name, Num(Data.Season));
                    Data.Finances[winners[0]].Reputation = Math.Min(MaxReputation, Data.Finances[winners[0]].Reputation + ReputationStepForQualifying);
                }

                return;
            }

            List<int> ordered = winners.OrderByDescending(c => Data.Finances[c].Reputation).ThenBy(c => c).ToList();
            int nextId = Data.Fixtures.Max(f => f.Id) + 1;
            DateTime date = last.Max(f => f.Date).AddDays(_cfg.Career.CupRoundSpacingDays);
            AddCupRound(ordered, round + 1, date, ref nextId);
        }

        /// <summary>Final de copa: la última ronda (dos clubes).</summary>
        public bool IsCupFinal(Fixture f)
        {
            if (!IsCup(f))
            {
                return false;
            }

            return Data.Fixtures.Count(x => x.CompetitionId == f.CompetitionId && x.Round == f.Round) == 1 && f.Round > 1;
        }

        // ------------------------------------------------------------------ entradas y público

        /// <summary>Importancia de un partido para la hinchada local.</summary>
        public MatchImportance ImportanceOf(Fixture f)
        {
            if (IsCupFinal(f))
            {
                return MatchImportance.CupFinal;
            }

            if (RivalryBetween(f.HomeClubId, f.AwayClubId) != RivalryLevel.None)
            {
                return MatchImportance.Classic;
            }

            if (IsCup(f))
            {
                return MatchImportance.Decisive; // eliminación directa
            }

            int remaining = Data.Fixtures.Count(x => x.CompetitionId == f.CompetitionId && !x.IsPlayed && x.Round >= f.Round
                                                     && (x.HomeClubId == f.HomeClubId || x.AwayClubId == f.HomeClubId));
            if (remaining > _cfg.Career.DecisiveRounds)
            {
                return MatchImportance.Regular;
            }

            List<Standing> table = Table(x => x.CompetitionId == f.CompetitionId);
            int pos = table.FindIndex(s => s.ClubId == f.HomeClubId);
            if (pos < 0)
            {
                return MatchImportance.Regular;
            }

            int maxGain = remaining * 3;
            bool title = table[0].Points - table[pos].Points <= maxGain;
            int qualifying = Math.Min(_cfg.Career.CupQualifiers, table.Count) - 1;
            bool qualification = Math.Abs(table[qualifying].Points - table[pos].Points) <= maxGain;
            bool danger = BuildAverages().Worst(Data.Clubs.Select(c => c.Id), Data.Season).Take(_cfg.Career.RelegatedClubs + 1).Contains(f.HomeClubId);
            return title || qualification || danger ? MatchImportance.Decisive : MatchImportance.Meaningless;
        }

        /// <summary>Precio que cobra el local: el del usuario (nivel o precio fijado a mano) o el normal de la IA.</summary>
        public long TicketPriceFor(Fixture f)
        {
            if (f.HomeClubId != Data.UserClubId)
            {
                return MatchAttendanceCalculator.PriceOf(TicketLevel.Normal, _cfg.Career.Attendance);
            }

            return Data.NextPriceOverride > 0 ? Data.NextPriceOverride : MatchAttendanceCalculator.PriceOf(Data.TicketLevel, _cfg.Career.Attendance);
        }

        /// <summary>Elige el nivel de precio de las entradas (y quita el precio fijado a mano).</summary>
        public void SetTicketLevel(TicketLevel level)
        {
            Data.TicketLevel = level;
            Data.NextPriceOverride = 0;
        }

        /// <summary>Ajuste fino: precio para el próximo partido de local (0 = volver al nivel).</summary>
        public void SetNextTicketPrice(long price) => Data.NextPriceOverride = Math.Max(0, price);

        /// <summary>Racha de un club: puntos de sus últimos partidos de liga sobre el máximo posible (0,5 si no jugó).</summary>
        public float FormOf(int clubId)
        {
            List<Fixture> last = Data.Fixtures.Where(f => f.IsPlayed && !IsCup(f) && Involves(f, clubId)).OrderByDescending(f => f.Date)
                .Take(_cfg.Career.FormMatches).ToList();
            if (last.Count == 0)
            {
                return 0.5f;
            }

            int points = 0;
            foreach (Fixture f in last)
            {
                int gf = f.HomeClubId == clubId ? f.HomeGoals.Value : f.AwayGoals.Value;
                int ga = f.HomeClubId == clubId ? f.AwayGoals.Value : f.HomeGoals.Value;
                points += gf > ga ? 3 : gf == ga ? 1 : 0;
            }

            return points / (3f * last.Count);
        }

        private float PositionVsExpectation(int clubId)
        {
            List<Standing> table = AnnualTable();
            int pos = table.FindIndex(s => s.ClubId == clubId) + 1;
            if (pos <= 0 || table.All(s => s.Played == 0))
            {
                return 0f;
            }

            int expected = _cfg.Career.Board.NationalTarget[(int)TierOf(clubId)];
            return MathUtil.Clamp((expected - pos) / PositionScale, -1f, 1f);
        }

        private const float PositionScale = 5f;

        private AttendanceInput AttendanceInputFor(Fixture f, MatchImportance importance, long price)
        {
            StadiumState s = Data.Stadiums[f.HomeClubId];
            bool user = f.HomeClubId == Data.UserClubId;
            return new AttendanceInput
            {
                Capacity = StadiumRemodelingSystem.UsableCapacity(s, _cfg.Career.Stadium), ParkingSpaces = s.ParkingSpaces, Price = price,
                Importance = importance, Reputation = Data.Finances[f.HomeClubId].Reputation, OpponentReputation = Data.Finances[f.AwayClubId].Reputation,
                Form = FormOf(f.HomeClubId), PositionVsExpectation = PositionVsExpectation(f.HomeClubId),
                FanMood = user ? Data.Supporters.Relation / 100f : NeutralMood, SupporterUnrest = user ? Data.Supporters.Unrest : 0f
            };
        }

        private const float NeutralMood = 0.5f;

        /// <summary>Ocupación esperada de un partido (0–1): la usa el estadio 3D para mostrar el público real.</summary>
        public float ExpectedAttendance(Fixture f) =>
            MatchAttendanceCalculator.Compute(AttendanceInputFor(f, ImportanceOf(f), TicketPriceFor(f)), _cfg.Career.Attendance).Ratio;

        /// <summary>Próximo partido de local del usuario sin jugar, o null.</summary>
        public Fixture NextUserHomeFixture() =>
            Data.Fixtures.Where(f => !f.IsPlayed && f.HomeClubId == Data.UserClubId).OrderBy(f => f.Date).FirstOrDefault();

        /// <summary>Vista previa del próximo partido de local con un precio (o el actual si es 0).</summary>
        public TicketPreview PreviewNextHome(long price = 0)
        {
            Fixture f = NextUserHomeFixture();
            if (f == null)
            {
                return new TicketPreview();
            }

            MatchImportance importance = ImportanceOf(f);
            long p = price > 0 ? price : TicketPriceFor(f);
            AttendanceInput input = AttendanceInputFor(f, importance, p);
            (long min, long max) = TicketPricingAdvisor.IdealRange(input, _cfg.Career.Attendance);
            return new TicketPreview
            {
                Fixture = f, Importance = importance, Price = p, MinIdeal = min, MaxIdeal = max,
                Status = TicketPricingAdvisor.Status(p, (min, max), _cfg.Career.Attendance),
                Expected = MatchAttendanceCalculator.Compute(input, _cfg.Career.Attendance)
            };
        }

        // ------------------------------------------------------------------ socios

        /// <summary>Hay un pedido de rebaja de los socios sin responder.</summary>
        public bool SupporterRequestPending => Data.Supporters.PendingFixtureId != 0;

        /// <summary>Unos días antes de cada partido de local, los socios revisan el precio y, si es caro, piden una rebaja.</summary>
        private void ReviewSupporters()
        {
            Fixture f = NextUserHomeFixture();
            if (f == null || (f.Date - Data.Today).TotalDays > _cfg.Career.SupporterReviewDaysBefore)
            {
                return;
            }

            TicketPreview preview = PreviewNextHome();
            if (SupporterRequestSystem.Review(Data.Supporters, f.Id, preview.Price, (preview.MinIdeal, preview.MaxIdeal)))
            {
                _inbox.Post("news.supporters_request", Data.Club(f.AwayClubId).Name, Num(preview.Price), Num(preview.MaxIdeal));
            }
        }

        /// <summary>Responde el pedido de los socios: aceptar la rebaja al rango ideal o mantener el precio.</summary>
        /// <param name="accept">Verdadero para rebajar.</param>
        public void AnswerSupporters(bool accept)
        {
            if (!SupporterRequestPending)
            {
                return;
            }

            if (accept)
            {
                Data.NextPriceOverride = SupporterRequestSystem.Accept(Data.Supporters, _cfg.Career.Supporters);
                _inbox.Post("news.supporters_thanks");
            }
            else
            {
                SupporterRequestSystem.Keep(Data.Supporters, _cfg.Career.Supporters);
                _inbox.Post("news.supporters_ignored");
            }
        }

        // ------------------------------------------------------------------ después de cada partido

        private void ScaleNewInjuries(TeamReport team, Dictionary<int, PlayerCondition> squad, float factor)
        {
            foreach (PlayerReport p in team.Players)
            {
                if (p.Stats.Injured && squad.TryGetValue(p.PlayerId, out PlayerCondition c))
                {
                    c.InjuryDays = Math.Max(1, (int)Math.Round(c.InjuryDays * factor));
                }
            }
        }

        private void AfterMatch(Fixture f, MatchReport report, MatchImportance importance, long price, AttendanceResult crowd)
        {
            if (!Involves(f, Data.UserClubId))
            {
                return;
            }

            bool home = f.HomeClubId == Data.UserClubId;
            int side = home ? 0 : 1;
            if (home)
            {
                AttendanceInput input = AttendanceInputFor(f, importance, price);
                (long min, long max) = TicketPricingAdvisor.IdealRange(input, _cfg.Career.Attendance);
                Data.TicketHistory.Add(new TicketRecord
                {
                    FixtureId = f.Id, Date = Data.Today, Price = price, MinIdeal = min, MaxIdeal = max, Attendance = crowd.Ratio, Revenue = crowd.Revenue
                });
                SupporterOutcome outcome = SupporterRequestSystem.AfterHomeMatch(Data.Supporters, price, (min, max), importance, FormOf(f.HomeClubId),
                    _cfg.Career.Supporters);
                if (outcome == SupporterOutcome.Repudiation)
                {
                    _inbox.Post("news.supporters_repudiation", Num(price));
                }
                else if (outcome == SupporterOutcome.DemandSacking)
                {
                    _inbox.Post("news.supporters_demand_exit");
                    Data.Manager.Popularity = Math.Max(0f, Data.Manager.Popularity - _cfg.Career.Supporters.SackingPressure);
                }

                Data.NextPriceOverride = 0; // el precio a mano vale para un partido
            }

            bool classic = RivalryBetween(f.HomeClubId, f.AwayClubId) == RivalryLevel.Classic;
            Data.Manager.Popularity = BoardExpectations.AfterMatch(Data.Manager.Popularity, report.Teams[side].Goals, report.Teams[1 - side].Goals, classic,
                _cfg.Career.Board);
            UpdateSignings(report.Teams[side]);
            ManagementRating rating = Rating();
            Data.ManagementHistory.Add(new ManagementHistoryEntry
            {
                Date = Data.Today, Supporters = rating.Supporters, Squad = rating.Squad, Budget = rating.Budget, Total = rating.Total
            });
            CheckSack();
        }

        private void UpdateSignings(TeamReport team)
        {
            foreach (SigningRecord s in Data.Signings)
            {
                if (Data.Players.TryGetValue(s.PlayerId, out Player p) && p.ClubId != Data.UserClubId)
                {
                    continue; // ya no está en el club
                }

                s.TeamMatches++;
                PlayerReport r = team.Players.Find(x => x.PlayerId == s.PlayerId);
                if (r == null || r.Minutes <= 0f)
                {
                    continue;
                }

                s.Matches++;
                s.RatingSum += r.Rating > 0f ? r.Rating : _cfg.Career.Rating.SigningExpectedRating;
                s.Contributions += r.Stats.Goals + r.Stats.Assists;
            }
        }

        private void CheckSack()
        {
            if (Data.Manager.Sacked || !BoardExpectations.ShouldFire(Data.Manager.Popularity, _cfg.Career.Board))
            {
                return;
            }

            Data.Manager.Sacked = true;
            _inbox.Post("news.manager_sacked", Data.Club(Data.UserClubId).Name);
        }

        // ------------------------------------------------------------------ estadio

        /// <summary>Estadio del club del usuario.</summary>
        public StadiumState UserStadium => Data.Stadiums[Data.UserClubId];

        /// <summary>Contrata una obra en el estadio del usuario.</summary>
        public RemodelResult ContractWork(RemodelCategory category, int tier) =>
            StadiumRemodelingSystem.Contract(UserStadium, Data.Finances[Data.UserClubId], category, tier, Data.Today,
                new SeededRandom(Mix(Data.Today.Ticks / TimeSpan.TicksPerDay * 7 + (int)category * 3 + tier)), _cfg.Career.Stadium);

        /// <summary>Costo total de varias obras seleccionadas.</summary>
        public long QuoteWorks(IEnumerable<(RemodelCategory, int)> works) => StadiumRemodelingSystem.Quote(works, _cfg.Career.Stadium);

        /// <summary>Costo y duración (mínima y máxima) de una obra.</summary>
        public (long Cost, int MinWeeks, int MaxWeeks) WorkInfo(RemodelCategory category, int tier)
        {
            StadiumConfig c = _cfg.Career.Stadium;
            int k = StadiumRemodelingSystem.Index(category, tier);
            return (c.Cost[k], c.WeeksMin[k], c.WeeksMax[k]);
        }

        /// <summary>Beneficio de una obra (asientos o plazas que suma), para mostrarlo.</summary>
        public int WorkBenefit(RemodelCategory category, int tier) =>
            category == RemodelCategory.Stands ? _cfg.Career.Stadium.StandSeats[tier - 1]
            : category == RemodelCategory.Parking ? _cfg.Career.Stadium.ParkingSpaces[tier - 1] : tier;

        private void OnWeekPassed(WeekPassed e)
        {
            foreach (KeyValuePair<int, StadiumState> pair in Data.Stadiums)
            {
                StadiumRemodelingSystem.AdvanceWeek(pair.Value, _cfg.Career.Stadium, _completedWorks);
                if (pair.Key != Data.UserClubId)
                {
                    continue;
                }

                foreach (RemodelWork w in _completedWorks)
                {
                    _inbox.Post("news.stadium_work_done", "stadium.category." + (int)w.Category, Num(w.Tier));
                }
            }
        }

        // ------------------------------------------------------------------ directiva, popularidad y medidor

        /// <summary>Datos institucionales de un club.</summary>
        public ClubInfo InfoOf(int clubId) => Data.ClubInfos.TryGetValue(clubId, out ClubInfo i) ? i : new ClubInfo();

        /// <summary>Jerarquía de un club.</summary>
        public ClubTier TierOf(int clubId) => BoardExpectations.ParseTier(InfoOf(clubId).Tier);

        /// <summary>Objetivos de la directiva para la temporada.</summary>
        public List<BoardObjective> Objectives() => BoardExpectations.Generate(TierOf(Data.UserClubId), Data.Clubs.Count, _cfg.Career.Board);

        /// <summary>Datos con los que la directiva mide los objetivos.</summary>
        public BoardSnapshot BoardSnapshot()
        {
            List<Standing> table = AnnualTable();
            List<Player> squad = Squad(Data.UserClubId);
            float total = squad.Sum(p => Data.Conditions[p.Id].SeasonMinutes);
            float youth = squad.Where(p => p.AgeOn(Data.Today) <= _cfg.Career.Board.YouthAge).Sum(p => Data.Conditions[p.Id].SeasonMinutes);
            List<TicketRecord> tickets = SeasonTickets();
            return new BoardSnapshot
            {
                Position = table.All(s => s.Played == 0) ? 0 : table.FindIndex(s => s.ClubId == Data.UserClubId) + 1, Clubs = table.Count,
                QualifyingPlaces = _cfg.Career.CupQualifiers, YouthMinutesShare = total > 0f ? youth / total : 0f,
                CashRatio = (float)Data.Finances[Data.UserClubId].Cash / Math.Max(1L, Data.SeasonStartCash),
                AverageAttendance = tickets.Count == 0 ? -1f : tickets.Average(t => t.Attendance)
            };
        }

        private List<TicketRecord> SeasonTickets() => Data.TicketHistory.Where(t => t.Date >= SeasonStartDate).ToList();

        private DateTime SeasonStartDate => new DateTime(Data.Season, 1, 1);

        /// <summary>Medidor integral de gestión con sus tres pilares y el detalle de factores.</summary>
        public ManagementRating Rating()
        {
            ClubFinances fin = Data.Finances[Data.UserClubId];
            List<TicketRecord> tickets = SeasonTickets();
            List<Player> squad = Squad(Data.UserClubId);
            float maxMinutes = squad.Count == 0 ? 0f : squad.Max(p => Data.Conditions[p.Id].SeasonMinutes);
            DateTime start = SeasonStartDate;
            var inputs = new ManagementInputs
            {
                SupporterRelation = Data.Supporters.Relation,
                AveragePriceExcess = tickets.Count == 0 ? 0f : tickets.Average(t => Math.Max(0f, (float)(t.Price - t.MaxIdeal) / Math.Max(1L, t.MaxIdeal))),
                RequestsAccepted = Data.Supporters.RequestsAccepted, RequestsIgnored = Data.Supporters.RequestsIgnored,
                AverageOccupancy = tickets.Count == 0 ? -1f : tickets.Average(t => t.Attendance),
                ServicesLevel = UserStadium.ServicesLevel, ParkingLevel = UserStadium.ParkingLevel,
                AverageMorale = squad.Count == 0 ? 50f : squad.Average(p => Data.Conditions[p.Id].Morale),
                RotationShare = squad.Count == 0 || maxMinutes <= 0f ? 0.6f
                    : (float)squad.Count(p => Data.Conditions[p.Id].SeasonMinutes >= maxMinutes * RotationMinutesShare) / Math.Min(squad.Count, RotationSquad),
                BrokenPromises = Data.News.Concat(_inbox.Chronological).Count(n => n.Key == "news.promise_broken" && n.Date >= start),
                ClubValue = InfoOf(Data.UserClubId).ClubValue, Cash = fin.Cash, StartCash = Math.Max(1L, Data.SeasonStartCash),
                WageBill = WeeklyWages(Data.UserClubId), WageBudget = Math.Max(1L, fin.WeeklyWageBudget),
                TransferSpend = -fin.Ledger.Where(e => e.Date >= start && e.Category == FinanceCategory.Transfers && e.Amount < 0).Sum(e => e.Amount),
                WorksSpend = -fin.Ledger.Where(e => e.Date >= start && e.Category == FinanceCategory.Construction).Sum(e => e.Amount)
            };
            inputs.RotationShare = Math.Min(1f, inputs.RotationShare);
            foreach (Fixture f in Data.Fixtures.Where(f => f.IsPlayed && f.Date >= start && Involves(f, Data.UserClubId)
                                                          && RivalryBetween(f.HomeClubId, f.AwayClubId) == RivalryLevel.Classic))
            {
                int gf = f.HomeClubId == Data.UserClubId ? f.HomeGoals.Value : f.AwayGoals.Value;
                int ga = f.HomeClubId == Data.UserClubId ? f.AwayGoals.Value : f.HomeGoals.Value;
                inputs.ClassicsWon += gf > ga ? 1 : 0;
                inputs.ClassicsLost += gf < ga ? 1 : 0;
            }

            inputs.Signings.AddRange(Data.Signings);
            return ManagementRatingSystem.Compute(inputs, _cfg.Career.Rating);
        }

        private const float RotationMinutesShare = 0.2f;
        private const int RotationSquad = 18;

        private void MonthlyClubReview(DateTime date)
        {
            foreach (KeyValuePair<int, StadiumState> pair in Data.Stadiums)
            {
                if (pair.Value.ServicesLevel >= 2)
                {
                    ClubFinances f = Data.Finances[pair.Key];
                    f.Record(date, FinanceCategory.Merchandise, f.Reputation * _cfg.Career.Stadium.MerchPerReputation);
                }
            }

            if (Data.TournamentOnly)
            {
                return;
            }

            float ratio = (float)Data.Finances[Data.UserClubId].Cash / Math.Max(1L, Data.SeasonStartCash);
            Data.Manager.Popularity = BoardExpectations.AfterMonth(Data.Manager.Popularity, ratio, _cfg.Career.Board);
            CheckSack();
        }

        private void ReviewSeasonWithTheBoard()
        {
            Data.Manager.Popularity = BoardExpectations.AfterSeason(Data.Manager.Popularity, Objectives(), BoardSnapshot(), _cfg.Career.Board);
            _inbox.Post("news.board_review", Num((long)Math.Round(Data.Manager.Popularity)));
            CheckSack();
        }

        private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
