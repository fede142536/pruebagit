using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace Football.Core.Persistence
{
    /// <summary>
    /// Aplica migraciones versionadas a una base de datos. Cada migración se ejecuta en su propia
    /// transacción: si falla, el esquema queda en la versión anterior.
    /// </summary>
    public sealed class Migrator
    {
        private readonly IReadOnlyList<IMigration> _migrations;

        /// <summary>Crea un migrador.</summary>
        /// <param name="migrations">Migraciones con versiones consecutivas 1..N, sin huecos ni repetidas.</param>
        public Migrator(IReadOnlyList<IMigration> migrations)
        {
            if (migrations == null)
            {
                throw new ArgumentNullException(nameof(migrations));
            }

            for (int i = 0; i < migrations.Count; i++)
            {
                if (migrations[i].Version != i + 1)
                {
                    throw new ArgumentException(
                        $"Las migraciones deben ser consecutivas desde 1; en la posición {i} hay la versión {migrations[i].Version}.",
                        nameof(migrations));
                }
            }

            _migrations = migrations;
        }

        /// <summary>Migrador con el historial real del juego.</summary>
        public static Migrator Default => new Migrator(Migrations.All);

        /// <summary>Última versión de esquema que conoce este migrador.</summary>
        public int LatestVersion => _migrations.Count;

        /// <summary>Versión actual del esquema en la base de datos (0 si está vacía).</summary>
        /// <param name="connection">Conexión abierta.</param>
        public int GetCurrentVersion(DbConnection connection)
        {
            RequireOpen(connection);
            SqlHelper.Execute(connection,
                "CREATE TABLE IF NOT EXISTS schema_version (version INTEGER NOT NULL PRIMARY KEY, description TEXT NOT NULL)",
                null);
            object value = SqlHelper.Scalar(connection, "SELECT COALESCE(MAX(version), 0) FROM schema_version", null);
            return Convert.ToInt32(value);
        }

        /// <summary>Lleva la base de datos a la última versión.</summary>
        /// <param name="connection">Conexión abierta.</param>
        public void MigrateToLatest(DbConnection connection) => MigrateTo(connection, LatestVersion);

        /// <summary>Lleva la base de datos hasta la versión indicada (no se admiten bajadas de versión).</summary>
        /// <param name="connection">Conexión abierta.</param>
        /// <param name="targetVersion">Versión de destino, entre la actual y la última conocida.</param>
        public void MigrateTo(DbConnection connection, int targetVersion)
        {
            if (targetVersion < 0 || targetVersion > LatestVersion)
            {
                throw new ArgumentOutOfRangeException(nameof(targetVersion),
                    $"La versión debe estar entre 0 y {LatestVersion}.");
            }

            int current = GetCurrentVersion(connection);
            if (current > LatestVersion)
            {
                throw new InvalidOperationException(
                    $"La base de datos está en la versión {current}, más nueva que la {LatestVersion} que conoce esta aplicación.");
            }

            if (targetVersion < current)
            {
                throw new InvalidOperationException("No se admite bajar la versión del esquema.");
            }

            for (int version = current + 1; version <= targetVersion; version++)
            {
                Apply(connection, _migrations[version - 1]);
            }
        }

        private static void Apply(DbConnection connection, IMigration migration)
        {
            using (DbTransaction transaction = connection.BeginTransaction())
            {
                foreach (string statement in migration.Statements)
                {
                    SqlHelper.Execute(connection, statement, transaction);
                }

                SqlHelper.Execute(connection,
                    "INSERT INTO schema_version (version, description) VALUES (@version, @description)",
                    transaction, ("@version", migration.Version), ("@description", migration.Description));
                transaction.Commit();
            }
        }

        private static void RequireOpen(DbConnection connection)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (connection.State != ConnectionState.Open)
            {
                throw new InvalidOperationException("La conexión debe estar abierta.");
            }
        }
    }
}
