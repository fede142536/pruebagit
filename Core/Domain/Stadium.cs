namespace Football.Core.Domain
{
    /// <summary>Estadio de un club.</summary>
    public sealed class Stadium
    {
        /// <summary>Identificador único (positivo).</summary>
        public int Id { get; set; }

        /// <summary>Nombre del estadio.</summary>
        public string Name { get; set; }

        /// <summary>Capacidad en espectadores.</summary>
        public int Capacity { get; set; }
    }
}
