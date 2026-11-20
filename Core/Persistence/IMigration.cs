using System.Collections.Generic;

namespace Football.Core.Persistence
{
    /// <summary>Paso versionado del esquema de la base de datos.</summary>
    public interface IMigration
    {
        /// <summary>Número de versión: consecutivo, empezando en 1.</summary>
        int Version { get; }

        /// <summary>Descripción breve del cambio.</summary>
        string Description { get; }

        /// <summary>Sentencias SQL a ejecutar, en orden, dentro de una única transacción.</summary>
        IReadOnlyList<string> Statements { get; }
    }
}
