using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Football.Core.Domain;
using Football.Core.Management;

namespace Football.Core.Career
{
    /// <summary>Resultado de renovar el contrato de un jugador del usuario o de firmar a un jugador libre.</summary>
    public enum ContractOutcome
    {
        /// <summary>Quedó firmado.</summary>
        Accepted,
        /// <summary>El jugador no es del club, no está libre o no existe.</summary>
        NotAvailable,
        /// <summary>Los años pedidos no sirven: no pasan de lo que le queda, superan el máximo o la edad no los permite.</summary>
        InvalidYears,
        /// <summary>El jugador está descontento y no quiere renovar.</summary>
        Unhappy,
        /// <summary>El sueldo no entra en el presupuesto de sueldos del club.</summary>
        NoBudget,
        /// <summary>El club ya tiene el cupo de extranjeros.</summary>
        ForeignQuota
    }

    // Contratos: vencimientos, renovaciones, jugadores libres.
    public sealed partial class CareerSession
    {
        private const int MonthsInYear = 12, LastDayOfYear = 31;

        /// <summary>Fecha en que vence un contrato de <paramref name="years"/> años firmado en la temporada en curso (31/12 del último año).</summary>
        public DateTime ContractEnd(int years) => new DateTime(Data.Season + years - 1, MonthsInYear, LastDayOfYear);

        /// <summary>Temporadas que le quedan a un contrato, contando la que está en juego (1 = vence al cerrar esta temporada).</summary>
        /// <param name="p">Jugador.</param>
        public int ContractYearsLeft(Player p) => p.Contract == null ? 0 : Math.Max(0, p.Contract.ExpiresOn.Year - Data.Season + 1);

        /// <summary>Máximo de años que firma un jugador al renovar, según la edad (los veteranos solo aceptan contratos cortos).</summary>
        /// <param name="p">Jugador.</param>
        public int MaxRenewalYears(Player p)
        {
            CareerConfig cc = _cfg.Career;
            int age = p.AgeOn(Data.Today);
            int years = cc.MaxContractYears;
            foreach (int limit in cc.RenewalAgeLimits)
            {
                if (age <= limit)
                {
                    return Math.Max(1, years);
                }

                years--;
            }

            return 1;
        }

        /// <summary>Sueldo semanal que pide un jugador del club por renovar (lo que cobra más el aumento de <see cref="CareerConfig.RenewalRaise"/>).</summary>
        /// <param name="p">Jugador.</param>
        public long RenewalWage(Player p) => p.Contract == null ? 0 : (long)(p.Contract.WeeklyWage * (1.0 + _cfg.Career.RenewalRaise));

        /// <summary>Jugadores del usuario cuyo contrato vence al cerrar esta temporada, de mejor a peor media.</summary>
        public List<Player> ExpiringContracts() =>
            Squad(Data.UserClubId).Where(p => ContractYearsLeft(p) <= 1).OrderByDescending(p => PlayerRatings.Overall(p.Attributes, p.Position)).ToList();

        /// <summary>
        /// Renueva el contrato de un jugador del usuario por <paramref name="years"/> años (contados desde esta temporada, máximo
        /// <see cref="CareerConfig.MaxContractYears"/>) con el aumento que pide. Si el jugador está descontento o el sueldo no entra en el presupuesto, no se firma.
        /// </summary>
        /// <param name="playerId">Jugador.</param>
        /// <param name="years">Años del contrato nuevo (más de los que le quedan).</param>
        public ContractOutcome Renew(int playerId, int years)
        {
            if (!Data.Players.TryGetValue(playerId, out Player p) || p.ClubId != Data.UserClubId || p.Contract == null)
            {
                return ContractOutcome.NotAvailable;
            }

            if (years <= ContractYearsLeft(p) || years > MaxRenewalYears(p) || years > _cfg.Career.MaxContractYears)
            {
                return ContractOutcome.InvalidYears;
            }

            if (Data.Conditions.TryGetValue(p.Id, out PlayerCondition c) && c.Morale < _cfg.Career.RenewalMinMorale)
            {
                return ContractOutcome.Unhappy;
            }

            long wage = RenewalWage(p);
            if (WeeklyWages(Data.UserClubId) - p.Contract.WeeklyWage + wage > Data.Finances[Data.UserClubId].WeeklyWageBudget)
            {
                return ContractOutcome.NoBudget;
            }

            p.Contract.WeeklyWage = wage;
            p.Contract.ExpiresOn = ContractEnd(years);
            _inbox.Post("news.contract_renewed", FullName(p), Num(Data.Season + years - 1));
            return ContractOutcome.Accepted;
        }

        // ------------------------------------------------------------------ jugadores libres

        /// <summary>Un jugador sin club que todavía juega (los que ya pasaron la edad de retiro no figuran).</summary>
        /// <param name="p">Jugador.</param>
        public bool IsFreeAgent(Player p) => !p.ClubId.HasValue && p.AgeOn(Data.Today) < _cfg.Management.FreeAgentMaxAge;

        /// <summary>Sueldo semanal que pide un jugador libre para firmar con el club del usuario.</summary>
        /// <param name="p">Jugador libre.</param>
        public long FreeAgentWage(Player p) => (long)(_market.WageDemand(p, PlayingTimePromise.Regular) * (double)_cfg.Career.FreeAgentWageFactor);

        /// <summary>Firma a un jugador libre (sin pagar transferencia) por <paramref name="years"/> años.</summary>
        /// <param name="playerId">Jugador libre.</param>
        /// <param name="years">Años de contrato (hasta <see cref="CareerConfig.MaxContractYears"/>).</param>
        public ContractOutcome SignFreeAgent(int playerId, int years)
        {
            if (!Data.Players.TryGetValue(playerId, out Player p) || !IsFreeAgent(p))
            {
                return ContractOutcome.NotAvailable;
            }

            if (years < 1 || years > _cfg.Career.MaxContractYears)
            {
                return ContractOutcome.InvalidYears;
            }

            if (WouldBreakForeignQuota(p, Data.UserClubId))
            {
                return ContractOutcome.ForeignQuota;
            }

            long wage = FreeAgentWage(p);
            if (WeeklyWages(Data.UserClubId) + wage > Data.Finances[Data.UserClubId].WeeklyWageBudget)
            {
                return ContractOutcome.NoBudget;
            }

            SignPlayer(p, Data.UserClubId, wage, years);
            Data.Signings.Add(new SigningRecord { PlayerId = p.Id, Name = FullName(p), Fee = 0, Date = Data.Today });
            _inbox.Post("news.free_agent_signed", FullName(p));
            return ContractOutcome.Accepted;
        }

        private void SignPlayer(Player p, int clubId, long wage, int years)
        {
            p.ClubId = clubId;
            p.JoinedOn = Data.Today;
            p.Contract = new Contract
            {
                WeeklyWage = wage, ExpiresOn = ContractEnd(years), Promise = PlayingTimePromise.Regular,
                ReleaseClause = (long)(_market.Value(p, Data.Today) * (double)_cfg.Career.ReleaseClauseFactor)
            };
            p.ShirtNumber = 0;
            FixShirtNumbers(Data, clubId);
        }

        // ------------------------------------------------------------------ cierre de temporada

        /// <summary>
        /// Contratos que vencen al cerrar la temporada: los clubes de la computadora renuevan a casi todos (los de peor nivel del plantel tienen más
        /// chances de irse) y el resto queda libre; los del usuario que no renovó se van libres.
        /// </summary>
        private void ProcessExpiredContracts()
        {
            CareerConfig cc = _cfg.Career;
            foreach (Club club in Data.Clubs)
            {
                List<Player> expiring = Squad(club.Id).Where(p => p.Contract != null && p.Contract.ExpiresOn.Year <= Data.Season).ToList();
                if (expiring.Count == 0)
                {
                    continue;
                }

                Common.IRandom rng = _dayRng.Fork("contratos" + club.Id);
                List<Player> squad = Squad(club.Id).OrderByDescending(x => PlayerRatings.Overall(x.Attributes, x.Position)).ToList();
                foreach (Player p in expiring)
                {
                    if (club.Id == Data.UserClubId)
                    {
                        Release(p);
                        _inbox.Post("news.contract_expired", FullName(p));
                        continue;
                    }

                    // Cuanto más abajo del plantel, menos chances de renovar; los arqueros y los jóvenes se renuevan casi siempre.
                    float rankShare = squad.IndexOf(p) / (float)Math.Max(1, squad.Count - 1);
                    bool keep = rng.Chance(Math.Max(0.1f, cc.AiRenewalChance * (1.15f - rankShare)))
                                || p.Position == Position.Goalkeeper && squad.Count(x => x.Position == Position.Goalkeeper) <= cc.MinAiGoalkeepers;
                    if (!keep)
                    {
                        Release(p);
                        continue;
                    }

                    p.Contract.WeeklyWage = (long)(p.Contract.WeeklyWage * (1.0 + cc.RenewalRaise));
                    p.Contract.ExpiresOn = ContractEnd(rng.NextInt(1, cc.MaxContractYears + 1) + 1); // el contrato nuevo cuenta desde la temporada que viene
                }
            }
        }

        private static void Release(Player p)
        {
            p.ClubId = null;
            p.Contract = null;
        }

        /// <summary>Los clubes de la computadora que quedaron cortos de plantel firman a los mejores jugadores libres (arqueros primero si faltan).</summary>
        private void RefillAiSquads()
        {
            CareerConfig cc = _cfg.Career;
            List<Player> free = Data.Players.Values.Where(IsFreeAgent).OrderByDescending(p => PlayerRatings.Overall(p.Attributes, p.Position)).ThenBy(p => p.Id).ToList();
            foreach (Club club in Data.Clubs.Where(c => c.Id != Data.UserClubId).OrderBy(c => c.Id))
            {
                List<Player> squad = Squad(club.Id);
                while (squad.Count < cc.MinAiSquadSize && free.Count > 0)
                {
                    bool needKeeper = squad.Count(p => p.Position == Position.Goalkeeper) < cc.MinAiGoalkeepers;
                    bool quotaFull = squad.Count(IsForeign) >= cc.MaxForeignersPerClub;
                    List<Player> pool = quotaFull ? free.Where(p => !IsForeign(p)).ToList() : free;
                    if (pool.Count == 0)
                    {
                        break;
                    }

                    Player pick = needKeeper ? pool.FirstOrDefault(p => p.Position == Position.Goalkeeper) ?? pool[0] : pool.FirstOrDefault(p => p.Position != Position.Goalkeeper) ?? pool[0];
                    free.Remove(pick);
                    SignPlayer(pick, club.Id, (long)(_market.WageDemand(pick, PlayingTimePromise.Regular) * (double)cc.FreeAgentWageFactor),
                        2 + pick.Id % cc.FreeAgentContractYears); // 1 a 3 temporadas de la que viene (se firma antes de pasar de año)
                    squad.Add(pick);
                }
            }
        }

        private void PostExpiringContractsNews()
        {
            int count = ExpiringContracts().Count;
            if (count > 0)
            {
                _inbox.Post("news.contracts_expiring", Num(count));
            }
        }

        /// <summary>Minúsculas y sin tildes, para comparar nombres al buscar.</summary>
        public static string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string decomposed = text.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder(decomposed.Length);
            foreach (char c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }
    }
}
