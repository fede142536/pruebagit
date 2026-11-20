using System.Data.Common;

namespace Football.Core.Persistence
{
    /// <summary>Utilidades mínimas de ADO.NET compartidas por migraciones y repositorios.</summary>
    internal static class SqlHelper
    {
        /// <summary>Crea un comando con parámetros nombrados; <c>null</c> se convierte en <c>NULL</c> de SQL.</summary>
        public static DbCommand Command(DbConnection connection, string sql, DbTransaction transaction,
            params (string Name, object Value)[] parameters)
        {
            DbCommand command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = transaction;
            foreach ((string name, object value) in parameters)
            {
                DbParameter p = command.CreateParameter();
                p.ParameterName = name;
                p.Value = value ?? System.DBNull.Value;
                command.Parameters.Add(p);
            }

            return command;
        }

        /// <summary>Ejecuta una sentencia sin resultado.</summary>
        public static int Execute(DbConnection connection, string sql, DbTransaction transaction,
            params (string Name, object Value)[] parameters)
        {
            using (DbCommand command = Command(connection, sql, transaction, parameters))
            {
                return command.ExecuteNonQuery();
            }
        }

        /// <summary>Ejecuta una consulta de un solo valor.</summary>
        public static object Scalar(DbConnection connection, string sql, DbTransaction transaction,
            params (string Name, object Value)[] parameters)
        {
            using (DbCommand command = Command(connection, sql, transaction, parameters))
            {
                return command.ExecuteScalar();
            }
        }
    }
}
