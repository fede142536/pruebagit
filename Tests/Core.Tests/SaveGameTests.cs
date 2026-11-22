using System;
using System.Data.Common;
using System.IO;
using Football.Core.Persistence;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace Football.Core.Tests
{
    /// <summary>Migraciones de la gestión (v3 a v7) y guardado atómico con copias rotativas.</summary>
    public sealed class SaveGameTests
    {
        private static SqliteConnection Open(string source = ":memory:")
        {
            var db = new SqliteConnection("Data Source=" + source);
            db.Open();
            SqliteConnectionSetup.Apply(db);
            return db;
        }

        private static object Scalar(DbConnection db, string sql)
        {
            using (DbCommand cmd = db.CreateCommand())
            {
                cmd.CommandText = sql;
                return cmd.ExecuteScalar();
            }
        }

        private static void Run(DbConnection db, string sql, DbTransaction tx = null)
        {
            using (DbCommand cmd = db.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }

        [Test]
        public void FromVersion2_ExistingClubsAndPlayers_GetFinancesFacilitiesAndConditions()
        {
            using (SqliteConnection db = Open())
            {
                Migrator.Default.MigrateTo(db, 2);
                Run(db, "INSERT INTO clubs (id, name, short_name, country, primary_color, secondary_color, transfer_budget, wage_budget) VALUES (1, 'Club', 'CLU', 'AR', '#fff', '#000', 5000, 300)");
                Run(db, "INSERT INTO players (id, club_id, first_name, last_name, birth_date, position, potential) VALUES (7, 1, 'Ana', 'Paz', '2001-02-03', 5, 80)");
                Run(db, "INSERT INTO contracts (player_id, weekly_wage, expires_on) VALUES (7, 100, '2028-06-30')");
                Migrator.Default.MigrateToLatest(db);

                Assert.That(Migrator.Default.GetCurrentVersion(db), Is.EqualTo(7));
                Assert.That(Convert.ToInt64(Scalar(db, "SELECT transfer_budget FROM club_finances WHERE club_id = 1")), Is.EqualTo(5000));
                Assert.That(Convert.ToInt64(Scalar(db, "SELECT weekly_wage_budget FROM club_finances WHERE club_id = 1")), Is.EqualTo(300));
                Assert.That(Convert.ToInt64(Scalar(db, "SELECT COUNT(*) FROM facilities WHERE club_id = 1 AND level = 1")), Is.EqualTo(4));
                Assert.That(Convert.ToInt64(Scalar(db, "SELECT release_clause FROM contracts WHERE player_id = 7")), Is.EqualTo(0));
                Assert.That(Convert.ToDouble(Scalar(db, "SELECT fitness FROM player_conditions WHERE player_id = 7")), Is.EqualTo(1.0));
                Assert.That(Scalar(db, "SELECT last_name FROM players WHERE id = 7"), Is.EqualTo("Paz"));
                foreach (string table in new[] { "ledger", "competitions", "fixtures", "rivalries", "staff", "scouting", "news", "records", "hall_of_fame" })
                {
                    Assert.That(Convert.ToInt64(Scalar(db, $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{table}'")), Is.EqualTo(1), table);
                }
            }
        }

        [Test]
        public void NewTables_EnforceTheirRules()
        {
            using (SqliteConnection db = Open())
            {
                Migrator.Default.MigrateToLatest(db);
                Run(db, "INSERT INTO clubs (id, name, short_name, country, primary_color, secondary_color, transfer_budget, wage_budget) VALUES (1, 'A', 'A', 'AR', '#fff', '#000', 0, 0)");
                Assert.Throws<SqliteException>(() => Run(db, "INSERT INTO staff (id, club_id, name, role, ability) VALUES (1, 1, 'X', 0, 25)"), "capacidad 1–20");
                Assert.Throws<SqliteException>(() => Run(db, "INSERT INTO fixtures (id, competition_id, round, date, home_club_id, away_club_id) VALUES (1, 99, 1, '2027-01-01', 1, 1)"));
            }
        }

        [Test]
        public void InTransaction_IsAllOrNothing()
        {
            using (SqliteConnection db = Open())
            {
                Migrator.Default.MigrateToLatest(db);
                Assert.Throws<InvalidOperationException>(() => AtomicSave.InTransaction(db, tx =>
                {
                    Run(db, "INSERT INTO records (key, value, holder_id, season) VALUES ('a', 1, 1, 2027)", tx);
                    throw new InvalidOperationException("falla a mitad del guardado");
                }));
                Assert.That(Convert.ToInt64(Scalar(db, "SELECT COUNT(*) FROM records")), Is.EqualTo(0), "no quedó nada a medias");
                AtomicSave.InTransaction(db, tx => Run(db, "INSERT INTO records (key, value, holder_id, season) VALUES ('a', 1, 1, 2027)", tx));
                Assert.That(Convert.ToInt64(Scalar(db, "SELECT COUNT(*) FROM records")), Is.EqualTo(1));
            }
        }

        [Test]
        public void WriteFile_ReplacesAtOnce_RotatesBackups_AndSurvivesAFailedSave()
        {
            string dir = Path.Combine(Path.GetTempPath(), "football-save-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string path = Path.Combine(dir, "partida.db");
                for (int n = 1; n <= 5; n++)
                {
                    int version = n;
                    AtomicSave.WriteFile(path, temp => File.WriteAllText(temp, "v" + version), backups: 3);
                }

                Assert.That(File.ReadAllText(path), Is.EqualTo("v5"));
                Assert.That(File.ReadAllText(AtomicSave.BackupName(path, 1)), Is.EqualTo("v4"));
                Assert.That(File.ReadAllText(AtomicSave.BackupName(path, 3)), Is.EqualTo("v2"));
                Assert.That(File.Exists(AtomicSave.BackupName(path, 4)), Is.False, "solo 3 copias");

                Assert.Throws<IOException>(() => AtomicSave.WriteFile(path, temp =>
                {
                    File.WriteAllText(temp, "a medias");
                    throw new IOException("se cortó la batería");
                }));
                Assert.That(File.ReadAllText(path), Is.EqualTo("v5"), "la partida anterior quedó intacta");
                Assert.That(File.Exists(path + ".tmp"), Is.False);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Test]
        public void SaveSqlite_WritesAConsistentCopyThatOpensAgain()
        {
            string dir = Path.Combine(Path.GetTempPath(), "football-save-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "carrera.db");
            try
            {
                using (SqliteConnection db = Open())
                {
                    Migrator.Default.MigrateToLatest(db);
                    Run(db, "INSERT INTO records (key, value, holder_id, season) VALUES ('record.season_goals', 31, 9, 2027)");
                    AtomicSave.SaveSqlite(db, path);
                    AtomicSave.SaveSqlite(db, path);
                }

                SqliteConnection.ClearAllPools();
                using (SqliteConnection saved = Open(path))
                {
                    Assert.That(Migrator.Default.GetCurrentVersion(saved), Is.EqualTo(Migrator.Default.LatestVersion));
                    Assert.That(Convert.ToDouble(Scalar(saved, "SELECT value FROM records")), Is.EqualTo(31.0));
                }

                Assert.That(File.Exists(AtomicSave.BackupName(path, 1)), Is.True);
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                Directory.Delete(dir, true);
            }
        }
    }
}
