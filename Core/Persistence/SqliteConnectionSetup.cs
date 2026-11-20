using System.Data.Common;

namespace Football.Core.Persistence
{
    /// <summary>Configuración que SQLite exige por conexión.</summary>
    public static class SqliteConnectionSetup
    {
        /// <summary>
        /// Activa las claves foráneas (SQLite las ignora por defecto). Debe llamarse justo después de abrir
        /// cada conexión, antes de usar los repositorios.
        /// </summary>
        /// <param name="connection">Conexión abierta.</param>
        public static void Apply(DbConnection connection)
        {
            SqlHelper.Execute(connection, "PRAGMA foreign_keys = ON", null);
        }
    }
}
