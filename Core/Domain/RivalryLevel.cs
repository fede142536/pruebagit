namespace Football.Core.Domain
{
    /// <summary>
    /// Intensidad de la rivalidad entre dos clubes. La usan la gestión (público, presión) y el partido (tarjetas).
    /// </summary>
    public enum RivalryLevel
    {
        /// <summary>Sin rivalidad especial.</summary>
        None = 0,
        /// <summary>Rivalidad (regional o histórica).</summary>
        Rivalry = 1,
        /// <summary>Derbi de la misma ciudad o zona.</summary>
        Derby = 2,
        /// <summary>Clásico: el partido más caliente del calendario de ambos.</summary>
        Classic = 3
    }
}
