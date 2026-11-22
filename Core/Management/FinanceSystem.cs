using System;
using System.Collections.Generic;
using Football.Core.Common;
using Football.Core.Domain;

namespace Football.Core.Management
{
    /// <summary>Da la masa salarial semanal de un club (la implementa el sistema que lleva los contratos).</summary>
    public interface IWageBill
    {
        /// <summary>Sueldos semanales del plantel y del cuerpo técnico de un club.</summary>
        /// <param name="clubId">Club.</param>
        long WeeklyWages(int clubId);
    }

    /// <summary>
    /// Economía al estilo PC Fútbol. Escucha el bus: cada semana paga sueldos y mantenimiento y avanza las obras; cada mes
    /// cobra socios, televisión y patrocinio; en cada partido de local, la taquilla según capacidad, precio, prestigio y
    /// rivalidad. Los presupuestos de fichajes y de sueldos están separados y se pueden pasar de uno a otro con comisión.
    /// </summary>
    public sealed class FinanceSystem
    {
        private readonly FinanceConfig _config;
        private readonly IWageBill _wages;
        private readonly EventBus _bus;
        private readonly Dictionary<int, ClubFinances> _finances = new Dictionary<int, ClubFinances>();
        private readonly Dictionary<int, ClubFacilities> _facilities = new Dictionary<int, ClubFacilities>();

        /// <summary>Crea el sistema y lo suscribe al bus.</summary>
        /// <param name="config">Economía.</param>
        /// <param name="wages">Masa salarial de cada club.</param>
        /// <param name="bus">Bus de eventos.</param>
        public FinanceSystem(FinanceConfig config, IWageBill wages, EventBus bus)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _wages = wages ?? throw new ArgumentNullException(nameof(wages));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            bus.Subscribe<WeekPassed>(OnWeek);
            bus.Subscribe<MonthClosed>(OnMonth);
            bus.Subscribe<HomeMatchPlayed>(OnHomeMatch);
        }

        /// <summary>Registra un club.</summary>
        /// <param name="finances">Dinero del club.</param>
        /// <param name="facilities">Instalaciones del club.</param>
        public void Register(ClubFinances finances, ClubFacilities facilities)
        {
            _finances[finances.ClubId] = finances;
            facilities.ClubId = finances.ClubId;
            _facilities[finances.ClubId] = facilities;
        }

        /// <summary>Dinero de un club.</summary>
        public ClubFinances Finances(int clubId) => _finances[clubId];

        /// <summary>Instalaciones de un club.</summary>
        public ClubFacilities Facilities(int clubId) => _facilities[clubId];

        /// <summary>Capacidad actual del estadio.</summary>
        public int Capacity(int clubId) => _config.StadiumCapacity[Math.Min(_facilities[clubId][FacilityKind.Stadium].Level, _config.StadiumCapacity.Length) - 1];

        /// <summary>
        /// Pasa dinero del presupuesto de fichajes al de sueldos: el monto, menos la comisión, se reparte entre las semanas de
        /// la temporada.
        /// </summary>
        /// <param name="clubId">Club.</param>
        /// <param name="amount">Dinero que sale del presupuesto de fichajes.</param>
        /// <param name="date">Fecha (para el registro).</param>
        /// <returns>Falso si no alcanza.</returns>
        public bool MoveToWages(int clubId, long amount, DateTime date)
        {
            ClubFinances f = _finances[clubId];
            if (amount <= 0 || amount > f.TransferBudget)
            {
                return false;
            }

            long commission = (long)(amount * _config.BudgetTransferCommission);
            f.TransferBudget -= amount;
            f.WeeklyWageBudget += (amount - commission) / _config.WeeksPerSeason;
            f.Record(date, FinanceCategory.Commission, -commission);
            return true;
        }

        /// <summary>Pasa presupuesto semanal de sueldos al de fichajes (por toda la temporada, menos la comisión).</summary>
        /// <param name="clubId">Club.</param>
        /// <param name="weeklyAmount">Sueldo semanal que se resigna.</param>
        /// <param name="date">Fecha.</param>
        /// <returns>Falso si no alcanza.</returns>
        public bool MoveToTransfers(int clubId, long weeklyAmount, DateTime date)
        {
            ClubFinances f = _finances[clubId];
            if (weeklyAmount <= 0 || weeklyAmount > f.WeeklyWageBudget)
            {
                return false;
            }

            long season = weeklyAmount * _config.WeeksPerSeason;
            long commission = (long)(season * _config.BudgetTransferCommission);
            f.WeeklyWageBudget -= weeklyAmount;
            f.TransferBudget += season - commission;
            f.Record(date, FinanceCategory.Commission, -commission);
            return true;
        }

        /// <summary>Empieza a subir de nivel una instalación (se paga al empezar y tarda unas semanas).</summary>
        /// <param name="clubId">Club.</param>
        /// <param name="kind">Instalación.</param>
        /// <param name="date">Fecha.</param>
        /// <returns>Falso si ya hay obra, está al máximo o no alcanza la caja.</returns>
        public bool StartUpgrade(int clubId, FacilityKind kind, DateTime date)
        {
            Facility facility = _facilities[clubId][kind];
            if (facility.WeeksToFinish > 0 || facility.Level >= _config.MaxFacilityLevel)
            {
                return false;
            }

            long cost = UpgradeCost(facility.Level + 1);
            ClubFinances f = _finances[clubId];
            if (f.Cash < cost)
            {
                return false;
            }

            f.Record(date, FinanceCategory.Construction, -cost);
            facility.WeeksToFinish = _config.UpgradeWeeks[facility.Level - 1];
            return true;
        }

        /// <summary>Costo de llevar una instalación al nivel indicado.</summary>
        /// <param name="level">Nivel nuevo (2–5).</param>
        public long UpgradeCost(int level) => _config.UpgradeCost[Math.Max(0, Math.Min(level - 2, _config.UpgradeCost.Length - 1))];

        /// <summary>Público esperado en un partido de local (nunca más que la capacidad).</summary>
        /// <param name="clubId">Club local.</param>
        /// <param name="opponentReputation">Prestigio del rival.</param>
        /// <param name="rivalry">Rivalidad.</param>
        public int Attendance(int clubId, int opponentReputation, RivalryLevel rivalry)
        {
            ClubFinances f = _finances[clubId];
            FinanceConfig c = _config;
            float occupancy = c.BaseOccupancy + c.OccupancyPerReputation * (f.Reputation - 50)
                              + c.OccupancyPerOpponentReputation * (opponentReputation - 50)
                              + c.RivalryOccupancy[Math.Min((int)rivalry, c.RivalryOccupancy.Length - 1)];
            long price = f.TicketPrice > 0 ? f.TicketPrice : c.ReferenceTicketPrice;
            occupancy *= (float)Math.Pow((double)c.ReferenceTicketPrice / price, c.PriceElasticity);
            return (int)(Capacity(clubId) * MathUtil.Clamp01(occupancy));
        }

        private void OnHomeMatch(HomeMatchPlayed e)
        {
            if (!_finances.TryGetValue(e.ClubId, out ClubFinances f))
            {
                return;
            }

            long price = f.TicketPrice > 0 ? f.TicketPrice : _config.ReferenceTicketPrice;
            f.Record(e.Date, FinanceCategory.Gate, Attendance(e.ClubId, e.OpponentReputation, e.Rivalry) * price);
        }

        private void OnWeek(WeekPassed e)
        {
            foreach (KeyValuePair<int, ClubFinances> pair in _finances)
            {
                ClubFinances f = pair.Value;
                f.Record(e.Date, FinanceCategory.Wages, -_wages.WeeklyWages(pair.Key));
                ClubFacilities facilities = _facilities[pair.Key];
                long levels = 0;
                foreach (Facility facility in facilities.All)
                {
                    levels += facility.Level;
                    if (facility.WeeksToFinish > 0 && --facility.WeeksToFinish == 0)
                    {
                        facility.Level++;
                        _bus.Publish(new FacilityUpgraded { ClubId = pair.Key, Kind = facility.Kind, Level = facility.Level });
                    }
                }

                f.Record(e.Date, FinanceCategory.Maintenance, -levels * _config.MaintenancePerLevel);
            }
        }

        private void OnMonth(MonthClosed e)
        {
            foreach (ClubFinances f in _finances.Values)
            {
                f.Record(e.Date, FinanceCategory.Members, f.Members * _config.MembershipFee);
                f.Record(e.Date, FinanceCategory.Television, f.Reputation * _config.TelevisionPerReputation);
                f.Record(e.Date, FinanceCategory.Sponsors, f.Reputation * _config.SponsorPerReputation);
            }
        }
    }
}
