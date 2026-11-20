using System.Collections.Generic;

namespace Football.Core.Persistence
{
    /// <summary>Migración definida por una lista de sentencias SQL.</summary>
    public sealed class SqlMigration : IMigration
    {
        /// <inheritdoc />
        public int Version { get; }

        /// <inheritdoc />
        public string Description { get; }

        /// <inheritdoc />
        public IReadOnlyList<string> Statements { get; }

        /// <summary>Crea una migración.</summary>
        /// <param name="version">Número de versión.</param>
        /// <param name="description">Descripción breve.</param>
        /// <param name="statements">Sentencias SQL en orden de ejecución.</param>
        public SqlMigration(int version, string description, params string[] statements)
        {
            Version = version;
            Description = description;
            Statements = statements;
        }
    }
}
