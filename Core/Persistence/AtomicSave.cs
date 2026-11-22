using System;
using System.Data.Common;
using System.IO;

namespace Football.Core.Persistence
{
    /// <summary>
    /// Guardado atómico: los cambios en la base van en una transacción (todo o nada), y el archivo de la partida se
    /// reemplaza de una vez desde un temporal, con copias de seguridad rotativas. Si algo falla a mitad, la partida
    /// anterior queda intacta.
    /// </summary>
    public static class AtomicSave
    {
        private const string TempSuffix = ".tmp";

        /// <summary>Ejecuta varias escrituras en una sola transacción: si una falla, no queda ninguna.</summary>
        /// <param name="connection">Conexión abierta.</param>
        /// <param name="work">Escrituras (reciben la transacción).</param>
        public static void InTransaction(DbConnection connection, Action<DbTransaction> work)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            using (DbTransaction transaction = connection.BeginTransaction())
            {
                work(transaction);
                transaction.Commit(); // si work lanzó, el Dispose deshace todo
            }
        }

        /// <summary>
        /// Escribe un archivo de forma atómica: primero a un temporal y luego lo pone en su lugar; la versión anterior
        /// pasa a ser la copia 1, la 1 a la 2, y así hasta <paramref name="backups"/>.
        /// </summary>
        /// <param name="path">Archivo final.</param>
        /// <param name="write">Escribe el contenido en la ruta temporal que recibe.</param>
        /// <param name="backups">Copias de seguridad a conservar.</param>
        public static void WriteFile(string path, Action<string> write, int backups = 3)
        {
            string temp = path + TempSuffix;
            if (File.Exists(temp))
            {
                File.Delete(temp); // restos de un guardado anterior que no terminó
            }

            try
            {
                write(temp);
            }
            catch
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }

                throw; // la partida anterior queda como estaba
            }

            if (!File.Exists(temp))
            {
                throw new IOException("El guardado no generó el archivo temporal.");
            }

            if (File.Exists(path))
            {
                RotateBackups(path, backups);
                if (backups > 0)
                {
                    File.Replace(temp, path, BackupName(path, 1));
                    return;
                }

                File.Delete(path);
            }

            File.Move(temp, path);
        }

        /// <summary>Nombre de la copia de seguridad número <paramref name="n"/>.</summary>
        public static string BackupName(string path, int n) => path + ".bak" + n;

        private static void RotateBackups(string path, int backups)
        {
            if (backups <= 0)
            {
                return;
            }

            string oldest = BackupName(path, backups);
            if (File.Exists(oldest))
            {
                File.Delete(oldest);
            }

            for (int n = backups - 1; n >= 1; n--)
            {
                string from = BackupName(path, n);
                if (File.Exists(from))
                {
                    File.Move(from, BackupName(path, n + 1));
                }
            }
        }

        /// <summary>
        /// Guarda una base SQLite abierta en un archivo de partida de forma atómica (con <c>VACUUM INTO</c>, que copia una
        /// foto coherente de la base sin cerrarla).
        /// </summary>
        /// <param name="connection">Conexión abierta a la base en uso.</param>
        /// <param name="path">Archivo de la partida.</param>
        /// <param name="backups">Copias de seguridad.</param>
        public static void SaveSqlite(DbConnection connection, string path, int backups = 3)
        {
            WriteFile(path, temp =>
            {
                using (DbCommand cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "VACUUM INTO @path";
                    DbParameter parameter = cmd.CreateParameter();
                    parameter.ParameterName = "@path";
                    parameter.Value = temp;
                    cmd.Parameters.Add(parameter);
                    cmd.ExecuteNonQuery();
                }
            }, backups);
        }
    }
}
