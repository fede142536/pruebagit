namespace Football.Core.Domain
{
    /// <summary>Club de fútbol. Los importes de dinero se expresan en unidades menores de moneda (centavos).</summary>
    public sealed class Club
    {
        /// <summary>Identificador único (positivo).</summary>
        public int Id { get; set; }

        /// <summary>Nombre completo.</summary>
        public string Name { get; set; }

        /// <summary>Nombre corto para marcadores y tablas.</summary>
        public string ShortName { get; set; }

        /// <summary>País o liga de origen.</summary>
        public string Country { get; set; }

        /// <summary>Color principal, en formato <c>#RRGGBB</c>.</summary>
        public string PrimaryColor { get; set; }

        /// <summary>Color secundario, en formato <c>#RRGGBB</c>.</summary>
        public string SecondaryColor { get; set; }

        /// <summary>Estadio, o <c>null</c> si aún no tiene.</summary>
        public int? StadiumId { get; set; }

        /// <summary>Presupuesto de fichajes disponible.</summary>
        public long TransferBudget { get; set; }

        /// <summary>Presupuesto de salarios disponible.</summary>
        public long WageBudget { get; set; }
    }
}
