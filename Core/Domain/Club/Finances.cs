using System;
using System.Collections.Generic;

namespace Football.Core.Domain
{
    /// <summary>Concepto de un movimiento de dinero del club.</summary>
    public enum FinanceCategory
    {
        /// <summary>Taquilla de los partidos de local.</summary>
        Gate,
        /// <summary>Cuotas de socios.</summary>
        Members,
        /// <summary>Derechos de televisión.</summary>
        Television,
        /// <summary>Patrocinadores.</summary>
        Sponsors,
        /// <summary>Sueldos del plantel y del cuerpo técnico.</summary>
        Wages,
        /// <summary>Mantenimiento de las instalaciones.</summary>
        Maintenance,
        /// <summary>Obras de infraestructura.</summary>
        Construction,
        /// <summary>Compras y ventas de jugadores.</summary>
        Transfers,
        /// <summary>Comisión por mover dinero entre presupuestos.</summary>
        Commission,
        /// <summary>Premios por resultados.</summary>
        Prizes
    }

    /// <summary>Un movimiento de dinero (positivo = ingreso).</summary>
    public struct LedgerEntry
    {
        /// <summary>Fecha.</summary>
        public DateTime Date;
        /// <summary>Concepto.</summary>
        public FinanceCategory Category;
        /// <summary>Importe (positivo ingreso, negativo gasto).</summary>
        public long Amount;
    }

    /// <summary>
    /// Dinero de un club al estilo PC Fútbol: caja, presupuesto de fichajes y presupuesto semanal de sueldos, separados
    /// (se puede pasar dinero de uno a otro pagando una comisión), y el registro de movimientos.
    /// </summary>
    public sealed class ClubFinances
    {
        /// <summary>Club.</summary>
        public int ClubId;
        /// <summary>Dinero en caja.</summary>
        public long Cash;
        /// <summary>Presupuesto para fichajes (parte de la caja reservada para comprar jugadores).</summary>
        public long TransferBudget;
        /// <summary>Presupuesto de sueldos por semana (tope de la masa salarial semanal).</summary>
        public long WeeklyWageBudget;
        /// <summary>Socios.</summary>
        public int Members;
        /// <summary>Precio de la entrada.</summary>
        public long TicketPrice;
        /// <summary>Prestigio del club (1–100): mueve público, socios, televisión y patrocinio.</summary>
        public int Reputation = 50;
        /// <summary>Movimientos, del más viejo al más nuevo.</summary>
        public readonly List<LedgerEntry> Ledger = new List<LedgerEntry>();

        /// <summary>Anota un movimiento y actualiza la caja.</summary>
        /// <param name="date">Fecha.</param>
        /// <param name="category">Concepto.</param>
        /// <param name="amount">Importe (positivo ingreso).</param>
        public void Record(DateTime date, FinanceCategory category, long amount)
        {
            Cash += amount;
            Ledger.Add(new LedgerEntry { Date = date, Category = category, Amount = amount });
        }

        /// <summary>Suma de los movimientos de un concepto.</summary>
        /// <param name="category">Concepto.</param>
        public long Total(FinanceCategory category)
        {
            long total = 0;
            foreach (LedgerEntry e in Ledger)
            {
                if (e.Category == category)
                {
                    total += e.Amount;
                }
            }

            return total;
        }
    }

    /// <summary>Instalaciones del club que se mejoran por niveles.</summary>
    public enum FacilityKind
    {
        /// <summary>Estadio: capacidad y taquilla.</summary>
        Stadium,
        /// <summary>Ciudad deportiva: entrenamiento y progresión.</summary>
        TrainingGround,
        /// <summary>Cantera: calidad de los juveniles.</summary>
        YouthAcademy,
        /// <summary>Departamento médico: recuperación de lesiones.</summary>
        Medical
    }

    /// <summary>Una instalación y su obra en curso.</summary>
    public sealed class Facility
    {
        /// <summary>Tipo.</summary>
        public FacilityKind Kind;
        /// <summary>Nivel actual (1–5).</summary>
        public int Level = 1;
        /// <summary>Semanas que faltan para terminar la obra en curso (0 = sin obra).</summary>
        public int WeeksToFinish;
    }

    /// <summary>Las cuatro instalaciones de un club.</summary>
    public sealed class ClubFacilities
    {
        /// <summary>Club.</summary>
        public int ClubId;
        /// <summary>Instalaciones, una por tipo (índice = <see cref="FacilityKind"/>).</summary>
        public readonly Facility[] All =
        {
            new Facility { Kind = FacilityKind.Stadium }, new Facility { Kind = FacilityKind.TrainingGround },
            new Facility { Kind = FacilityKind.YouthAcademy }, new Facility { Kind = FacilityKind.Medical }
        };

        /// <summary>Instalación de un tipo.</summary>
        /// <param name="kind">Tipo.</param>
        public Facility this[FacilityKind kind] => All[(int)kind];
    }

    /// <summary>Pasó una semana del calendario.</summary>
    public struct WeekPassed
    {
        /// <summary>Fecha del último día de la semana.</summary>
        public DateTime Date;
    }

    /// <summary>Terminó un mes del calendario.</summary>
    public struct MonthClosed
    {
        /// <summary>Último día del mes.</summary>
        public DateTime Date;
    }

    /// <summary>Se jugó un partido de local (para la taquilla).</summary>
    public struct HomeMatchPlayed
    {
        /// <summary>Fecha.</summary>
        public DateTime Date;
        /// <summary>Club local.</summary>
        public int ClubId;
        /// <summary>Prestigio del rival (1–100).</summary>
        public int OpponentReputation;
        /// <summary>Rivalidad entre los dos.</summary>
        public RivalryLevel Rivalry;
    }

    /// <summary>Se terminó una obra.</summary>
    public struct FacilityUpgraded
    {
        /// <summary>Club.</summary>
        public int ClubId;
        /// <summary>Instalación.</summary>
        public FacilityKind Kind;
        /// <summary>Nuevo nivel.</summary>
        public int Level;
    }
}
