using System;
using System.Collections.Generic;
using System.Linq;
using Football.Core.Domain;
using Football.Core.MatchCore;

namespace Football.Core.Career
{
    // Renovaciones negociadas: cada jugador pide sueldo, años y, si juega poco, la seguridad de ser titular; se toma días para responder
    // y puede contraofertar o irse libre.
    public sealed partial class CareerSession
    {
        /// <summary>Negociación de un jugador (la última), o null si no hubo.</summary>
        /// <param name="playerId">Jugador.</param>
        public RenewalTalk TalkOf(int playerId) => Data.Talks.TryGetValue(playerId, out RenewalTalk t) ? t : null;

        /// <summary>Lo que pide un jugador del club del usuario para renovar.</summary>
        /// <param name="p">Jugador.</param>
        public RenewalDemand DemandOf(Player p)
        {
            CareerConfig cc = _cfg.Career;
            int personality = (int)p.Personality;
            double factor = cc.RenewalWageFactor[Math.Min(personality, cc.RenewalWageFactor.Length - 1)];
            long wage = p.Contract == null ? 0 : (long)(p.Contract.WeeklyWage * (1.0 + cc.RenewalRaise) * factor);
            int age = p.AgeOn(Data.Today);
            int years = 1;
            for (int i = 0; i < cc.RenewalYearsByAge.Length; i++)
            {
                if (age <= cc.RenewalYearsByAge[i])
                {
                    years = cc.RenewalYearsWanted[i];
                    break;
                }
            }

            return new RenewalDemand
            {
                Wage = wage, Years = Math.Min(years, MaxRenewalYears(p)), WantsPlayingTime = WantsPlayingTime(p), Role = ExpectedRole(p),
                Flexibility = _cfg.Management.WageFlexibility[Math.Min(personality, _cfg.Management.WageFlexibility.Length - 1)]
            };
        }

        /// <summary>Rol que espera un jugador del club según su nivel en el plantel (los tres mejores, figuras; hasta el puesto 11, titulares; después rotación y suplentes); sube un escalón si reclama minutos.</summary>
        /// <param name="p">Jugador.</param>
        public PlayingTimePromise ExpectedRole(Player p)
        {
            CareerConfig cc = _cfg.Career;
            List<Player> squad = Squad(p.ClubId ?? 0);
            int rank = squad.OrderByDescending(x => RosterFactory.DisplayRating(x.Attributes, x.Position)).ToList().IndexOf(p);
            int value = rank < 0 ? 1 : rank < 3 ? 3 : rank < 11 ? 2 : rank < 18 ? 1 : 0;
            if (WantsPlayingTime(p))
            {
                value = Math.Min(3, value + 1);
            }

            return RoleOf(value);
        }

        private static int RoleValue(PlayingTimePromise r)
        {
            switch (r)
            {
                case PlayingTimePromise.Star: return 3;
                case PlayingTimePromise.Regular: return 2;
                case PlayingTimePromise.Backup: return 0;
                default: return 1; // rotación, juvenil o sin promesa
            }
        }

        private static PlayingTimePromise RoleOf(int value) =>
            value >= 3 ? PlayingTimePromise.Star : value == 2 ? PlayingTimePromise.Regular : value == 1 ? PlayingTimePromise.Rotation : PlayingTimePromise.Backup;

        /// <summary>
        /// Qué tan conforme queda un jugador con una oferta (0 = justo lo que pide; negativo = menos; positivo = más). Mezcla sueldo, años y rol: se puede
        /// compensar una cosa con otra (el mismo sueldo con menos años, o menos sueldo con un rol más importante).
        /// </summary>
        /// <param name="p">Jugador.</param>
        /// <param name="wage">Sueldo semanal ofrecido.</param>
        /// <param name="years">Años ofrecidos.</param>
        /// <param name="role">Rol ofrecido.</param>
        /// <param name="worst">El aspecto que más pesa en contra (para explicar el rechazo).</param>
        public float Satisfaction(Player p, long wage, int years, PlayingTimePromise role, out TalkReason worst)
        {
            CareerConfig cc = _cfg.Career;
            RenewalDemand want = DemandOf(p);
            int k = Math.Min((int)p.Personality, cc.RenewalWageWeight.Length - 1);
            float wageTerm = Math.Max(-1f, Math.Min(cc.RenewalWageBonusCap, want.Wage > 0 ? (float)wage / want.Wage - 1f : 0f));
            float yearsTerm = Math.Max(-1f, Math.Min(1f, (years - want.Years) / cc.RenewalYearsSpan));
            float roleTerm = Math.Max(-1f, Math.Min(1f, (RoleValue(role) - RoleValue(want.Role)) / 2f));
            float roleWeight = cc.RenewalRoleWeight[k] * (want.WantsPlayingTime ? cc.RenewalPlayingTimeBoost : 1f);
            float w = cc.RenewalWageWeight[k] * wageTerm, y = cc.RenewalYearsWeight[k] * yearsTerm, r = roleWeight * roleTerm;
            worst = w <= y && w <= r ? TalkReason.Wage : y <= r ? TalkReason.Years : TalkReason.PlayingTime;
            return w + y + r;
        }

        /// <summary>
        /// El jugador es de los mejores del plantel pero juega poco: para renovar exige que se le asegure un lugar (figura o titular). Solo cuenta
        /// con un mínimo de partidos jugados en la temporada.
        /// </summary>
        /// <param name="p">Jugador.</param>
        public bool WantsPlayingTime(Player p)
        {
            CareerConfig cc = _cfg.Career;
            List<Player> squad = Squad(p.ClubId ?? 0);
            if (squad.Count == 0 || !Data.Conditions.TryGetValue(p.Id, out PlayerCondition mine))
            {
                return false;
            }

            float top = squad.Max(x => Data.Conditions.TryGetValue(x.Id, out PlayerCondition c) ? c.SeasonMinutes : 0f);
            if (top < cc.RenewalMinSeasonMinutes)
            {
                return false; // todavía se jugó muy poco
            }

            int rank = squad.OrderByDescending(x => RosterFactory.DisplayRating(x.Attributes, x.Position)).ToList().IndexOf(p);
            return rank < cc.RenewalStarterRank && mine.SeasonMinutes < top * cc.RenewalMinutesShare;
        }

        /// <summary>
        /// Hace una oferta de renovación. El jugador no responde en el momento: se toma unos días (según su personalidad) y contesta aceptando,
        /// pidiendo otra cosa o negándose. Una oferta nueva reemplaza a la anterior.
        /// </summary>
        /// <param name="playerId">Jugador.</param>
        /// <param name="weeklyWage">Sueldo semanal ofrecido.</param>
        /// <param name="years">Años del contrato nuevo (más de los que le quedan, dentro del máximo).</param>
        /// <param name="promise">Promesa de minutos.</param>
        public ContractOutcome SendRenewalOffer(int playerId, long weeklyWage, int years, PlayingTimePromise promise)
        {
            if (!Data.Players.TryGetValue(playerId, out Player p) || p.ClubId != Data.UserClubId || p.Contract == null)
            {
                return ContractOutcome.NotAvailable;
            }

            if (years <= ContractYearsLeft(p) || years > MaxRenewalYears(p) || years > _cfg.Career.MaxContractYears)
            {
                return ContractOutcome.InvalidYears;
            }

            RenewalTalk previous = TalkOf(playerId);
            if (previous != null && previous.Status == TalkStatus.Refused)
            {
                return ContractOutcome.Unhappy; // ya dijo que no
            }

            if (previous != null && previous.Status == TalkStatus.Thinking)
            {
                return ContractOutcome.NotAvailable; // todavía está pensando la oferta anterior
            }

            if (WeeklyWages(Data.UserClubId) - p.Contract.WeeklyWage + weeklyWage > Data.Finances[Data.UserClubId].WeeklyWageBudget)
            {
                return ContractOutcome.NoBudget;
            }

            int rounds = previous?.Rounds ?? 0;
            Data.Talks[playerId] = new RenewalTalk
            {
                PlayerId = playerId, OfferWage = weeklyWage, OfferYears = years, OfferPromise = promise, SentOn = Data.Today,
                ReplyOn = Data.Today.AddDays(ThinkDays(p, rounds)), Status = TalkStatus.Thinking, Rounds = rounds + 1,
                DemandWage = previous?.DemandWage ?? 0, DemandYears = previous?.DemandYears ?? 0
            };
            return ContractOutcome.Accepted;
        }

        /// <summary>Días que tarda un jugador en contestar: entre el mínimo y el máximo de la configuración, más los ambiciosos y menos los leales.</summary>
        private int ThinkDays(Player p, int round)
        {
            CareerConfig cc = _cfg.Career;
            double spread = new Common.SeededRandom(Mix(p.Id * 131L + Data.Season * 7L + round)).NextFloat();
            double trait = p.Personality == PersonalityTrait.Ambitious ? 0.3 : p.Personality == PersonalityTrait.Loyal ? -0.3 : 0.0;
            double t = Math.Max(0.0, Math.Min(1.0, spread * 0.7 + 0.15 + trait));
            return (int)Math.Round(cc.RenewalThinkDays[0] + t * (cc.RenewalThinkDays[1] - cc.RenewalThinkDays[0]));
        }

        /// <summary>Resuelve las negociaciones cuyo día de respuesta llegó (se llama cada día).</summary>
        private void ResolveTalks()
        {
            foreach (RenewalTalk talk in Data.Talks.Values.Where(t => t.Status == TalkStatus.Thinking && t.ReplyOn <= Data.Today).ToList())
            {
                if (!Data.Players.TryGetValue(talk.PlayerId, out Player p) || p.ClubId != Data.UserClubId || p.Contract == null)
                {
                    Data.Talks.Remove(talk.PlayerId);
                    continue;
                }

                Reply(talk, p);
            }
        }

        private void Reply(RenewalTalk talk, Player p)
        {
            RenewalDemand want = DemandOf(p);
            talk.DemandWage = want.Wage;
            talk.DemandYears = want.Years;
            talk.DemandRole = want.Role;
            TalkReason reason = TalkReason.None;
            bool unhappy = Data.Conditions.TryGetValue(p.Id, out PlayerCondition c) && c.Morale < _cfg.Career.RenewalMinMorale;
            if (unhappy)
            {
                reason = TalkReason.Unhappy;
            }
            else
            {
                float satisfaction = Satisfaction(p, talk.OfferWage, talk.OfferYears, talk.OfferPromise, out TalkReason worst);
                if (satisfaction < -want.Flexibility)
                {
                    reason = worst;
                }
            }

            talk.Reason = reason;
            if (reason == TalkReason.None)
            {
                Sign(p, talk.OfferWage, talk.OfferYears, talk.OfferPromise);
                talk.Status = TalkStatus.Accepted;
            }
            else if (reason == TalkReason.Unhappy || talk.Rounds >= _cfg.Career.RenewalMaxRounds)
            {
                talk.Status = TalkStatus.Refused;
                _inbox.Post("news.renewal_refused", FullName(p));
            }
            else
            {
                talk.Status = TalkStatus.Counter;
                _inbox.Post("news.renewal_counter", FullName(p), Num(want.Wage), Num(want.Years));
            }
        }

        private void Sign(Player p, long wage, int years, PlayingTimePromise role)
        {
            p.Contract.WeeklyWage = wage;
            p.Contract.ExpiresOn = ContractEnd(years);
            p.Contract.Promise = role;
            _inbox.Post("news.contract_renewed", FullName(p), Num(Data.Season + years - 1));
        }

        /// <summary>
        /// Acepta lo que pidió el jugador en su última respuesta (sueldo, años y rol): se firma en el acto, sin esperar. El usuario también puede
        /// hacer otra oferta, con más o menos de lo que pide.
        /// </summary>
        /// <param name="playerId">Jugador.</param>
        public ContractOutcome AcceptCounter(int playerId)
        {
            RenewalTalk talk = TalkOf(playerId);
            if (talk == null || talk.Status != TalkStatus.Counter || !Data.Players.TryGetValue(playerId, out Player p) || p.ClubId != Data.UserClubId || p.Contract == null)
            {
                return ContractOutcome.NotAvailable;
            }

            int years = Math.Max(talk.DemandYears, ContractYearsLeft(p) + 1);
            if (years > MaxRenewalYears(p) || years > _cfg.Career.MaxContractYears)
            {
                return ContractOutcome.InvalidYears;
            }

            if (WeeklyWages(Data.UserClubId) - p.Contract.WeeklyWage + talk.DemandWage > Data.Finances[Data.UserClubId].WeeklyWageBudget)
            {
                return ContractOutcome.NoBudget;
            }

            Sign(p, talk.DemandWage, years, talk.DemandRole);
            talk.Status = TalkStatus.Accepted;
            talk.OfferWage = talk.DemandWage;
            talk.OfferYears = years;
            talk.OfferPromise = talk.DemandRole;
            return ContractOutcome.Accepted;
        }
    }
}
