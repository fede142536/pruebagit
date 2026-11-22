using System;

namespace Football.Core.Management
{
    /// <summary>Economía del club (<c>Data/balance/finance.json</c>). Importes en pesos de juego (unidades enteras).</summary>
    [Serializable]
    public sealed class FinanceConfig
    {
        /// <summary>Comisión al pasar dinero entre el presupuesto de fichajes y el de sueldos (fracción que se pierde).</summary>
        public float BudgetTransferCommission = 0.1f;
        /// <summary>Semanas de la temporada (para convertir sueldo semanal en dinero de fichajes y al revés).</summary>
        public int WeeksPerSeason = 52;
        /// <summary>Precio de referencia de la entrada.</summary>
        public long ReferenceTicketPrice = 8000;
        /// <summary>Ocupación del estadio con prestigio 50, rival medio y precio de referencia.</summary>
        public float BaseOccupancy = 0.55f;
        /// <summary>Ocupación extra por cada punto de prestigio propio sobre 50.</summary>
        public float OccupancyPerReputation = 0.006f;
        /// <summary>Ocupación extra por cada punto de prestigio del rival sobre 50.</summary>
        public float OccupancyPerOpponentReputation = 0.003f;
        /// <summary>Ocupación extra según la rivalidad (ninguna, rivalidad, derbi, clásico).</summary>
        public float[] RivalryOccupancy = { 0f, 0.1f, 0.2f, 0.35f };
        /// <summary>Elasticidad al precio: ocupación × (precio de referencia / precio)^esto.</summary>
        public float PriceElasticity = 0.8f;
        /// <summary>Capacidad del estadio en cada nivel (1–5).</summary>
        public int[] StadiumCapacity = { 18000, 28000, 40000, 52000, 65000 };
        /// <summary>Cuota mensual de un socio.</summary>
        public long MembershipFee = 6000;
        /// <summary>Televisión: ingreso mensual por punto de prestigio.</summary>
        public long TelevisionPerReputation = 900000;
        /// <summary>Patrocinadores: ingreso mensual por punto de prestigio.</summary>
        public long SponsorPerReputation = 700000;
        /// <summary>Mantenimiento semanal de cada instalación por nivel.</summary>
        public long MaintenancePerLevel = 1500000;
        /// <summary>Costo de subir una instalación al nivel indicado (índice = nivel nuevo − 2).</summary>
        public long[] UpgradeCost = { 1500000000, 3500000000, 7000000000, 12000000000 };
        /// <summary>Semanas de obra para subir al nivel indicado (índice = nivel nuevo − 2).</summary>
        public int[] UpgradeWeeks = { 8, 12, 18, 26 };
        /// <summary>Nivel máximo de una instalación.</summary>
        public int MaxFacilityLevel = 5;
    }
}
