using System;
using System.Collections.Generic;
using System.Linq;
using Football.Core.Domain;
using Football.Core.Management;
using Football.Core.MatchCore;

namespace Football.Core.Career
{
    /// <summary>Resultado de subir un juvenil a primera o bajarlo a la reserva.</summary>
    public enum SquadMove
    {
        /// <summary>Hecho.</summary>
        Done,
        /// <summary>No es del plantel del usuario o no existe.</summary>
        NotYours,
        /// <summary>Es menor que la edad mínima para jugar en primera.</summary>
        TooYoung,
        /// <summary>Superó la edad máxima de la reserva.</summary>
        TooOld,
        /// <summary>Todavía no debutó en primera: solo los que ya jugaron pueden bajar.</summary>
        NotDebuted,
        /// <summary>La reserva está completa.</summary>
        ReserveFull,
        /// <summary>Está cedido a otro club.</summary>
        OnLoan,
        /// <summary>Ya está donde se lo quiere mandar.</summary>
        AlreadyThere,
        /// <summary>Sube a primera pero el club ya tiene el cupo de extranjeros.</summary>
        ForeignQuota
    }

    // Equipo de reserva simulado: juveniles que ganan rodaje fuera del primer equipo, suben y bajan, y se desarrollan según minutos, moral y entrenador.
    public sealed partial class CareerSession
    {
        /// <summary>Edad máxima en la reserva.</summary>
        public int MaxReserveAge => _cfg.Career.MaxReserveAge;

        /// <summary>Tamaño máximo de la reserva.</summary>
        public int MaxReserveSize => _cfg.Career.MaxReserveSize;

        /// <summary>Si el jugador está en la reserva de su club.</summary>
        /// <param name="playerId">Jugador.</param>
        public bool IsInReserve(int playerId) => Data.Conditions.TryGetValue(playerId, out PlayerCondition c) && c.InReserve;

        /// <summary>Juveniles de la reserva de un club.</summary>
        /// <param name="clubId">Club.</param>
        public List<Player> ReserveSquad(int clubId) =>
            Data.Players.Values.Where(p => p.ClubId == clubId && IsInReserve(p.Id)).OrderBy(p => p.Id).ToList();

        /// <summary>Todos los jugadores del club, de primera y de la reserva.</summary>
        /// <param name="clubId">Club.</param>
        public List<Player> FullRoster(int clubId) => Data.Players.Values.Where(p => p.ClubId == clubId).OrderBy(p => p.Id).ToList();

        /// <summary>Si ya debutó en primera (solo así puede bajar a la reserva).</summary>
        /// <param name="playerId">Jugador.</param>
        public bool HasDebuted(int playerId) => !Data.Conditions.TryGetValue(playerId, out PlayerCondition c) || c.Debuted;

        /// <summary>Controla si el jugador del usuario puede subir a primera y por qué no.</summary>
        /// <param name="playerId">Jugador.</param>
        public SquadMove CanPromote(int playerId)
        {
            if (!Data.Players.TryGetValue(playerId, out Player p) || p.ClubId != Data.UserClubId || !IsInReserve(playerId))
            {
                return !Data.Players.ContainsKey(playerId) || Data.Players[playerId].ClubId != Data.UserClubId ? SquadMove.NotYours : SquadMove.AlreadyThere;
            }

            if (p.AgeOn(Data.Today) < _cfg.Career.MinFirstTeamAge)
            {
                return SquadMove.TooYoung;
            }

            return WouldBreakForeignQuota(p, Data.UserClubId) ? SquadMove.ForeignQuota : SquadMove.Done;
        }

        /// <summary>Sube a un juvenil de la reserva a primera con contrato profesional (sueldo de profesional y años nuevos).</summary>
        /// <param name="playerId">Jugador.</param>
        public SquadMove Promote(int playerId)
        {
            SquadMove check = CanPromote(playerId);
            if (check != SquadMove.Done)
            {
                return check;
            }

            Player p = Data.Players[playerId];
            GoProfessional(p);
            Data.Conditions[playerId].InReserve = false;
            FixShirtNumbers(Data, Data.UserClubId);
            return SquadMove.Done;
        }

        private void GoProfessional(Player p)
        {
            CareerConfig cc = _cfg.Career;
            long wage = _market.WageDemand(p, PlayingTimePromise.Youngster);
            DateTime end = ContractEnd(cc.PromotedContractYears);
            p.Contract = new Contract
            {
                WeeklyWage = Math.Max(p.Contract?.WeeklyWage ?? 0, wage), ExpiresOn = p.Contract != null && p.Contract.ExpiresOn > end ? p.Contract.ExpiresOn : end,
                ReleaseClause = (long)(_market.Value(p, Data.Today) * (double)cc.ReleaseClauseFactor), Promise = PlayingTimePromise.Youngster
            };
        }

        /// <summary>Controla si el jugador del usuario puede bajar a la reserva y por qué no.</summary>
        /// <param name="playerId">Jugador.</param>
        public SquadMove CanDemote(int playerId)
        {
            if (!Data.Players.TryGetValue(playerId, out Player p) || p.ClubId != Data.UserClubId)
            {
                return SquadMove.NotYours;
            }

            if (IsInReserve(playerId))
            {
                return SquadMove.AlreadyThere;
            }

            if (LoanOf(playerId) != null)
            {
                return SquadMove.OnLoan;
            }

            if (p.AgeOn(Data.Today) > _cfg.Career.MaxReserveAge)
            {
                return SquadMove.TooOld;
            }

            if (!HasDebuted(playerId))
            {
                return SquadMove.NotDebuted;
            }

            return ReserveSquad(Data.UserClubId).Count >= _cfg.Career.MaxReserveSize ? SquadMove.ReserveFull : SquadMove.Done;
        }

        /// <summary>Baja a un jugador de primera a la reserva para que siga ganando rodaje (solo hasta la edad máxima y si ya debutó).</summary>
        /// <param name="playerId">Jugador.</param>
        public SquadMove Demote(int playerId)
        {
            SquadMove check = CanDemote(playerId);
            if (check != SquadMove.Done)
            {
                return check;
            }

            Data.Conditions[playerId].InReserve = true;
            Data.Listings.Remove(playerId);
            _offers.RemoveAll(o => o.Offer.PlayerId == playerId);
            return SquadMove.Done;
        }

        /// <summary>
        /// Potencial que el usuario cree que tiene un juvenil: exacto no se ve. El error depende del entrenador de juveniles (mejor entrenador, menos error);
        /// en los ajenos, también de cuánto se los ojeó.
        /// </summary>
        /// <param name="p">Jugador.</param>
        public int VisiblePotential(Player p)
        {
            StaffMember coach = Data.Staff.Find(s => s.ClubId == Data.UserClubId && s.Role == StaffRole.YouthCoach);
            int ability = coach?.Ability ?? _cfg.Career.StaffAbilityPivot;
            float noise = _cfg.Career.PotentialNoise * (1.05f - Math.Min(1f, (ability - 1) / 19f));
            if (p.ClubId != Data.UserClubId)
            {
                noise *= 1f + (1f - _scouting.Knowledge(Data.UserClubId, p.Id));
            }

            float unit = ((p.Id * 37 + p.Id / 7) % 21 - 10) / 10f; // fijo por jugador
            return (int)Math.Max(1f, Math.Min(AttributeScale.Max, Math.Round(p.Potential + unit * noise)));
        }

        private DeclineSettings DeclineConfig()
        {
            CareerConfig cc = _cfg.Career;
            return new DeclineSettings
            {
                MinAge = cc.DeclineMinAge, OnsetSpread = cc.DeclineOnsetSpread, PointsPerWeek = cc.DeclinePointsPerWeek, Acceleration = cc.DeclineAcceleration,
                SpeedRange = cc.DeclineSpeedRange, WeeklyNoise = cc.DeclineWeeklyNoise, PhysicalShare = cc.DeclinePhysicalShare,
                GoalkeeperFactor = cc.DeclineGoalkeeperFactor, ProfessionalFactor = cc.DeclineProfessionalFactor
            };
        }

        /// <summary>Partido de la reserva (simulado): los que jugaron suman partido, y al azar goles, asistencias y nota según su media y su moral.</summary>
        private void SimulateReserveMatch(int clubId, List<Player> reserve, Dictionary<int, float> minutes, Common.IRandom rng)
        {
            CareerConfig cc = _cfg.Career;
            if (reserve.Count == 0 || !rng.Chance(cc.ReserveMatchChance))
            {
                return;
            }

            foreach (Player p in reserve.Where(x => minutes.TryGetValue(x.Id, out float m) && m > 0f))
            {
                if (!Data.ReserveStats.TryGetValue(p.Id, out CardStats st))
                {
                    st = new CardStats();
                    Data.ReserveStats[p.Id] = st;
                }

                float overall = PlayerRatings.Overall(p.Attributes, p.Position);
                int line = (int)PlayerRatings.LineOf(p.Position);
                float scale = overall / ReserveReferenceRating;
                st.Matches++;
                if (rng.Chance(Math.Min(1f, cc.ReserveGoalChance[line] * scale)))
                {
                    st.Goals++;
                }

                if (rng.Chance(Math.Min(1f, cc.ReserveGoalChance[line] * cc.ReserveAssistShare * scale)))
                {
                    st.Assists++;
                }

                float mood = (Data.Conditions[p.Id].Morale - ReserveNeutralMorale) / ReserveMoraleScale;
                float mark = Math.Max(ReserveMinRating, Math.Min(ReserveMaxRating, ReserveBaseRating + (overall - ReserveReferenceRating) / ReserveRatingScale + mood + (rng.NextFloat() - 0.5f)));
                st.RatingSum += mark;
                st.Rated++;
                PlayerCondition cond = Data.Conditions[p.Id];
                cond.Form += _cfg.Management.FormSmoothing * (mark - cond.Form); // la reserva también cuenta para el rendimiento
            }
        }

        private const float ReserveReferenceRating = 60f, ReserveNeutralMorale = 50f, ReserveMoraleScale = 100f, ReserveBaseRating = 6.3f;
        private const float ReserveRatingScale = 20f, ReserveMinRating = 4f, ReserveMaxRating = 9.5f;

        private DevelopmentSettings DevSettings()
        {
            CareerConfig cc = _cfg.Career;
            return new DevelopmentSettings
            {
                MaxAge = cc.DevMaxAge, PointsPerWeek = cc.DevPointsPerWeek, AgeFactor = cc.DevAgeFactor, FullMinutes = cc.DevFullMinutes, IdleShare = cc.DevIdleShare,
                MoraleRange = cc.DevMoraleRange, CoachRange = cc.DevCoachRange, SlowGap = cc.DevSlowGap
            };
        }

        /// <summary>Semana de los juveniles: la reserva (simulada) juega, su moral se mueve y todos los jóvenes se desarrollan.</summary>
        internal void YouthWeek()
        {
            CareerConfig cc = _cfg.Career;
            DevelopmentSettings dev = DevSettings();
            DeclineSettings decline = DeclineConfig();
            foreach (Club club in Data.Clubs)
            {
                Common.IRandom rng = _dayRng.Fork("desarrollo" + club.Id + "-" + Data.Today.Ticks);
                StaffMember coach = Data.Staff.Find(s => s.ClubId == club.Id && s.Role == StaffRole.YouthCoach);
                int ability = coach?.Ability ?? cc.StaffAbilityPivot;
                var reserveMinutes = new Dictionary<int, float>();
                List<Player> reserve = ReserveSquad(club.Id).Where(p => Data.Conditions[p.Id].Available)
                    .OrderByDescending(p => PlayerRatings.Overall(p.Attributes, p.Position)).ThenBy(p => p.Id).ToList();
                for (int i = 0; i < reserve.Count; i++)
                {
                    reserveMinutes[reserve[i].Id] = i < cc.ReserveStarters ? cc.ReserveStarterMinutes : i < cc.ReserveStarters + cc.ReserveBench ? cc.ReserveBenchMinutes : 0f;
                }

                SimulateReserveMatch(club.Id, reserve, reserveMinutes, rng);
                foreach (Player p in FullRoster(club.Id))
                {
                    PlayerCondition cond = Data.Conditions[p.Id];
                    reserveMinutes.TryGetValue(p.Id, out float fromReserve);
                    if (cond.InReserve)
                    {
                        float target = fromReserve > 0f ? cc.ReservePlayingMorale : cc.ReserveIdleMorale;
                        cond.Morale += (target - cond.Morale) * cc.ReserveMoraleStep;
                    }

                    float week = Math.Max(0f, cond.SeasonMinutes - cond.DevMinutesMark) + fromReserve;
                    cond.DevMinutesMark = cond.SeasonMinutes;
                    int age = p.AgeOn(Data.Today);
                    float overall = PlayerRatings.Overall(p.Attributes, p.Position);
                    float performance = YouthDevelopment.PerformanceIndex(cond.Form, overall, cc.ExpectedRatingBase, cc.ExpectedRatingPerOverall, cc.ExpectedRatingPivot, cc.PerformanceScale);
                    bool declining = age >= cc.DeclineMinAge && age >= YouthDevelopment.OnsetAge(decline, p.Id);
                    if (age >= cc.DeclineMinAge)
                    {
                        float loss = YouthDevelopment.WeeklyDecline(decline, p.Id, age, p.Position, p.Personality == PersonalityTrait.Professional, rng)
                                     * YouthDevelopment.PerformanceFactor(cc.DeclinePerformanceRange, performance); // rendir bien frena el declive; rendir mal lo acelera
                        YouthDevelopment.ApplyDecline(p, cond, loss, cc.DeclinePhysicalShare, rng);
                    }

                    if (age > cc.DevMaxAge)
                    {
                        if (!declining)
                        {
                            DriftInPrime(p, cond, performance, rng); // en su madurez, sube o baja un poco según cómo rinde
                        }

                        continue;
                    }

                    float points = YouthDevelopment.WeeklyPoints(dev, age, overall, p.Potential, week, cond.Morale, ability)
                                   * YouthDevelopment.PerformanceFactor(cc.GrowthPerformanceRange, performance); // rendir bien acelera el crecimiento
                    YouthDevelopment.Apply(p, cond, points, rng);
                }
            }
        }

        /// <summary>
        /// Al cerrar la temporada, los de la reserva que superan la edad máxima firman contrato de primera (en el club del usuario siempre; en los de la
        /// computadora, si tienen nivel) o se van libres.
        /// </summary>
        internal void AgeOutReserve()
        {
            foreach (Club club in Data.Clubs)
            {
                foreach (Player p in ReserveSquad(club.Id).Where(x => x.AgeOn(Data.Today) > _cfg.Career.MaxReserveAge))
                {
                    if (club.Id == Data.UserClubId || WorthPromoting(club.Id, p))
                    {
                        GoProfessional(p);
                        Data.Conditions[p.Id].InReserve = false;
                        _inbox.Post("news.reserve_ageout", FullName(p), Num(p.AgeOn(Data.Today)));
                    }
                    else
                    {
                        p.ClubId = null;
                        p.Contract = null;
                        Data.Conditions[p.Id].InReserve = false;
                        if (club.Id == Data.UserClubId)
                        {
                            _inbox.Post("news.reserve_released", FullName(p));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Jugadores en su madurez (pasado el crecimiento y antes del declive): con buen rendimiento mejoran de a poco (hasta un margen sobre su potencial) y con mal
        /// rendimiento empeoran de a poco. Los puntos son chicos para que el cambio de una temporada no sea brusco.
        /// </summary>
        private void DriftInPrime(Player p, PlayerCondition cond, float performance, Common.IRandom rng)
        {
            CareerConfig cc = _cfg.Career;
            float points = cc.DriftPointsPerWeek * Math.Abs(performance);
            if (performance > 0f)
            {
                YouthDevelopment.Apply(p, cond, points, rng, cc.DriftOverPotential);
            }
            else if (performance < 0f)
            {
                YouthDevelopment.ApplyDecline(p, cond, points, cc.DriftPhysicalShare, rng);
            }
        }

        /// <summary>Cuánto subió (o bajó) la media del jugador desde que empezó la temporada (0 si no hay registro).</summary>
        /// <param name="p">Jugador.</param>
        public int RatingChange(Player p) =>
            Data.Conditions.TryGetValue(p.Id, out PlayerCondition c) && c.SeasonStartRating > 0
                ? (int)Math.Round(PlayerRatings.Overall(p.Attributes, p.Position)) - c.SeasonStartRating
                : 0;

        /// <summary>Si un canterano de un club de la computadora está para jugar en primera (cerca del nivel de sus titulares).</summary>
        private bool WorthPromoting(int clubId, Player p)
        {
            List<float> firsts = Squad(clubId).Select(x => PlayerRatings.Overall(x.Attributes, x.Position)).OrderByDescending(x => x).ToList();
            if (firsts.Count == 0)
            {
                return true;
            }

            float benchmark = firsts[Math.Min(firsts.Count - 1, _cfg.Career.AiPromoteRank)];
            return PlayerRatings.Overall(p.Attributes, p.Position) >= benchmark - _cfg.Career.AiPromoteMargin;
        }

        /// <summary>Los clubes de la computadora suben a primera a los canteranos que ya están al nivel, a partir de la edad mínima.</summary>
        private void PromoteAiYouth()
        {
            foreach (Club club in Data.Clubs.Where(c => c.Id != Data.UserClubId))
            {
                foreach (Player p in ReserveSquad(club.Id).Where(x => x.AgeOn(Data.Today) >= _cfg.Career.MinFirstTeamAge && WorthPromoting(club.Id, x)))
                {
                    GoProfessional(p);
                    Data.Conditions[p.Id].InReserve = false;
                }
            }
        }

        /// <summary>Si una reserva supera el tamaño máximo, se suelta a los de menor potencial.</summary>
        private void TrimReserves()
        {
            foreach (Club club in Data.Clubs)
            {
                List<Player> reserve = ReserveSquad(club.Id).OrderBy(p => p.Potential).ThenBy(p => p.Id).ToList();
                int max = club.Id == Data.UserClubId ? _cfg.Career.MaxReserveSize : _cfg.Career.MaxAiReserveSize;
                for (int i = 0; reserve.Count - i > max; i++)
                {
                    Player drop = reserve[i];
                    drop.ClubId = null;
                    drop.Contract = null;
                    Data.Conditions[drop.Id].InReserve = false;
                    if (club.Id == Data.UserClubId)
                    {
                        _inbox.Post("news.reserve_released", FullName(drop));
                    }
                }
            }
        }

        /// <summary>
        /// Al empezar la temporada se sortea quién se retira al terminarla (desde la edad de retiro, más probable cuanto mayor). El del club del usuario
        /// lo anuncia en el buzón: esa es su última temporada.
        /// </summary>
        internal void AnnounceRetirements()
        {
            foreach (Player p in Data.Players.Values.Where(x => x.ClubId.HasValue).OrderBy(x => x.Id))
            {
                if (!Data.Conditions.TryGetValue(p.Id, out PlayerCondition cond))
                {
                    continue;
                }

                cond.RetiringAtEnd = _history.ShouldRetire(p, Data.Today, new Common.SeededRandom(Mix(p.Id * RetirementSalt + Data.Season)));
                if (cond.RetiringAtEnd && p.ClubId == Data.UserClubId)
                {
                    _inbox.Post("news.last_season", FullName(p), Num(p.AgeOn(Data.Today)));
                }
            }
        }

        private const long RetirementSalt = 7919L;

        /// <summary>Si el jugador anunció que esta es su última temporada.</summary>
        /// <param name="playerId">Jugador.</param>
        public bool IsRetiringThisSeason(int playerId) => Data.Conditions.TryGetValue(playerId, out PlayerCondition c) && c.RetiringAtEnd;

        /// <summary>Estadísticas de la temporada en los partidos de la reserva (null si todavía no jugó).</summary>
        /// <param name="playerId">Jugador.</param>
        public CardStats ReserveStatsOf(int playerId) => Data.ReserveStats.TryGetValue(playerId, out CardStats s) ? s : null;

        /// <summary>Estadísticas de la temporada en los partidos de primera (null si todavía no jugó).</summary>
        /// <param name="playerId">Jugador.</param>
        public CardStats FirstTeamStatsOf(int playerId) => Data.SeasonStats.TryGetValue(playerId, out CardStats s) ? s : null;

        /// <summary>
        /// Plantel con el que se juega un partido: el de primera y, si por lesiones, sanciones o bajas no alcanzan los jugadores disponibles
        /// (<see cref="CareerConfig.EmergencyCallUpTarget"/>), los mejores de la reserva a partir de la edad mínima, como hace cualquier club con sus juveniles.
        /// Siguen en la reserva; al jugar, debutan.
        /// </summary>
        /// <param name="clubId">Club.</param>
        public List<Player> MatchSquad(int clubId)
        {
            List<Player> squad = Squad(clubId);
            int available = squad.Count(p => !Data.Conditions.TryGetValue(p.Id, out PlayerCondition c) || c.Available);
            if (available >= _cfg.Career.EmergencyCallUpTarget)
            {
                return squad;
            }

            foreach (Player kid in ReserveSquad(clubId).Where(k => k.AgeOn(Data.Today) >= _cfg.Career.MinFirstTeamAge && Data.Conditions[k.Id].Available)
                         .OrderByDescending(k => PlayerRatings.Overall(k.Attributes, k.Position)).ThenBy(k => k.Id))
            {
                if (available >= _cfg.Career.EmergencyCallUpTarget)
                {
                    break;
                }

                squad.Add(kid);
                available++;
            }

            return squad;
        }
    }
}
