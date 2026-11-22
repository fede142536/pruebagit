using System;
using System.Collections.Generic;
using Football.Core.Common;
using Football.Core.Domain;

namespace Football.Core.Management
{
    /// <summary>
    /// Aplica un <see cref="MatchReport"/> a la carrera: forma, moral, estado físico, minutos, lesiones (con días de baja
    /// según el departamento médico) y sanciones (roja o amarillas acumuladas). Avisa por el bus de lesiones y sanciones.
    /// </summary>
    public sealed class ReportApplier
    {
        private readonly ManagementConfig _config;
        private readonly EventBus _bus;

        /// <summary>Crea el aplicador.</summary>
        public ReportApplier(ManagementConfig config, EventBus bus)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        }

        /// <summary>Aplica el informe al plantel de un equipo del partido.</summary>
        /// <param name="report">Informe.</param>
        /// <param name="side">0 local, 1 visitante.</param>
        /// <param name="squad">Estado de todos los jugadores del club (por id), incluidos los que no jugaron.</param>
        /// <param name="medicalLevel">Nivel del departamento médico (1–5).</param>
        /// <param name="rng">Azar (días de baja).</param>
        public void Apply(MatchReport report, int side, IDictionary<int, PlayerCondition> squad, int medicalLevel, IRandom rng)
        {
            ManagementConfig c = _config;
            TeamReport team = report.Teams[side];
            int goalsFor = team.Goals, goalsAgainst = report.Teams[1 - side].Goals;
            int result = goalsFor > goalsAgainst ? 0 : goalsFor == goalsAgainst ? 1 : 2;
            var played = new HashSet<int>();
            foreach (PlayerReport p in team.Players)
            {
                if (!squad.TryGetValue(p.PlayerId, out PlayerCondition cond))
                {
                    continue;
                }

                played.Add(p.PlayerId);
                cond.SeasonMinutes += p.Minutes;
                cond.MinutesSincePromise += p.Minutes;
                cond.Fitness = Math.Min(cond.Fitness, p.StaminaLeft);
                if (p.Rating > 0f)
                {
                    cond.Form += c.FormSmoothing * (p.Rating - cond.Form);
                    cond.Morale += c.RatingMorale * (p.Rating - 6f);
                }

                cond.Morale += c.ResultMorale[result];
                cond.Morale = MathUtil.Clamp(cond.Morale, 0f, 100f);
                if (p.Stats.Injured)
                {
                    float saving = 1f - c.MedicalLevelSaving * (Math.Max(1, medicalLevel) - 1);
                    cond.InjuryDays = (int)Math.Ceiling(rng.NextInt(c.InjuryDays[0], c.InjuryDays[1] + 1) * saving);
                    _bus.Publish(new PlayerInjured { PlayerId = p.PlayerId, Days = cond.InjuryDays });
                }

                if (p.Stats.Reds > 0)
                {
                    cond.SuspendedMatches += c.RedCardBan;
                    _bus.Publish(new PlayerSuspended { PlayerId = p.PlayerId, Matches = c.RedCardBan });
                }
                else if (p.Stats.Yellows > 0 && (cond.AccumulatedYellows += p.Stats.Yellows) >= c.YellowLimit)
                {
                    cond.AccumulatedYellows -= c.YellowLimit;
                    cond.SuspendedMatches += 1;
                    _bus.Publish(new PlayerSuspended { PlayerId = p.PlayerId, Matches = 1 });
                }
            }

            foreach (PlayerCondition cond in squad.Values)
            {
                cond.TeamMatchesSincePromise++;
                if (!played.Contains(cond.PlayerId) && cond.SuspendedMatches > 0)
                {
                    cond.SuspendedMatches--; // cumplió una fecha de sanción
                }
            }
        }

        /// <summary>Un día de descanso: recupera estado físico y descuenta días de lesión.</summary>
        public void RestDay(IEnumerable<PlayerCondition> squad)
        {
            foreach (PlayerCondition cond in squad)
            {
                cond.Fitness = Math.Min(1f, cond.Fitness + _config.FitnessRecoveryPerDay);
                if (cond.InjuryDays > 0)
                {
                    cond.InjuryDays--;
                }
            }
        }
    }
}
