using System;
using System.Collections.Generic;
using System.Data.Common;
using Football.Core.Persistence;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace Football.Core.Tests
{
    public class MigrationTests
    {
        private DbConnection _db;

        [SetUp]
        public void SetUp()
        {
            _db = new SqliteConnection("Data Source=:memory:");
            _db.Open();
            SqliteConnectionSetup.Apply(_db);
        }

        [TearDown]
        public void TearDown() => _db.Dispose();

        private long Count(string sql)
        {
            using (DbCommand cmd = _db.CreateCommand())
            {
                cmd.CommandText = sql;
                return Convert.ToInt64(cmd.ExecuteScalar());
            }
        }

        private void Run(string sql)
        {
            using (DbCommand cmd = _db.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }

        [Test]
        public void EmptyDatabase_IsVersionZero()
        {
            Assert.That(Migrator.Default.GetCurrentVersion(_db), Is.Zero);
        }

        [Test]
        public void MigrateToLatest_CreatesAllTables()
        {
            var migrator = Migrator.Default;
            migrator.MigrateToLatest(_db);

            Assert.That(migrator.GetCurrentVersion(_db), Is.EqualTo(migrator.LatestVersion));
            foreach (string table in new[] { "stadiums", "clubs", "players", "player_attributes", "contracts" })
            {
                Assert.That(Count($"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{table}'"), Is.EqualTo(1), table);
            }
        }

        [Test]
        public void MigrateToLatest_IsIdempotent()
        {
            Migrator.Default.MigrateToLatest(_db);
            Assert.DoesNotThrow(() => Migrator.Default.MigrateToLatest(_db));
            Assert.That(Count("SELECT COUNT(*) FROM schema_version"), Is.EqualTo(Migrator.Default.LatestVersion));
        }

        [Test]
        public void EveryVersion_MigratesToLatest_PreservingData()
        {
            // Para cada versión anterior: se crea la BD en esa versión, se insertan datos y se migra a la última.
            int latest = Migrator.Default.LatestVersion;
            for (int from = 1; from < latest; from++)
            {
                using (var db = new SqliteConnection("Data Source=:memory:"))
                {
                    db.Open();
                    SqliteConnectionSetup.Apply(db);
                    Migrator.Default.MigrateTo(db, from);

                    using (DbCommand cmd = db.CreateCommand())
                    {
                        cmd.CommandText = @"INSERT INTO players (id, first_name, last_name, birth_date, position, potential)
                                            VALUES (1, 'Ana', 'Pérez', '2000-01-01', 5, 80)";
                        cmd.ExecuteNonQuery();
                    }

                    Migrator.Default.MigrateToLatest(db);

                    using (DbCommand cmd = db.CreateCommand())
                    {
                        cmd.CommandText = "SELECT last_name FROM players WHERE id = 1";
                        Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Pérez"), $"datos perdidos al migrar desde v{from}");
                    }
                }
            }
        }

        [Test]
        public void DatabaseNewerThanApp_IsRejected()
        {
            Migrator.Default.MigrateToLatest(_db);
            Run("INSERT INTO schema_version (version, description) VALUES (999, 'futura')");

            Assert.Throws<InvalidOperationException>(() => Migrator.Default.MigrateToLatest(_db));
        }

        [Test]
        public void FailingMigration_RollsBackAndKeepsPreviousVersion()
        {
            var migrations = new List<IMigration>
            {
                new SqlMigration(1, "ok", "CREATE TABLE a (id INTEGER PRIMARY KEY)"),
                new SqlMigration(2, "rota", "CREATE TABLE b (id INTEGER PRIMARY KEY)", "THIS IS NOT SQL")
            };
            var migrator = new Migrator(migrations);

            Assert.Catch<DbException>(() => migrator.MigrateToLatest(_db));

            Assert.That(migrator.GetCurrentVersion(_db), Is.EqualTo(1));
            Assert.That(Count("SELECT COUNT(*) FROM sqlite_master WHERE name='b'"), Is.Zero, "la tabla de la migración fallida debe revertirse");
        }

        [Test]
        public void Downgrade_IsRejected()
        {
            Migrator.Default.MigrateToLatest(_db);
            Assert.Throws<InvalidOperationException>(() => Migrator.Default.MigrateTo(_db, 1));
        }

        [Test]
        public void Migrator_RejectsGapsInVersions()
        {
            var bad = new List<IMigration> { new SqlMigration(1, "a", "SELECT 1"), new SqlMigration(3, "c", "SELECT 1") };
            Assert.Throws<ArgumentException>(() => new Migrator(bad));
        }

        [Test]
        public void ClosedConnection_IsRejected()
        {
            using (var closed = new SqliteConnection("Data Source=:memory:"))
            {
                Assert.Throws<InvalidOperationException>(() => Migrator.Default.GetCurrentVersion(closed));
            }
        }

        [Test]
        public void AttributeValuesOutsideScale_AreRejectedByTheDatabase()
        {
            Migrator.Default.MigrateToLatest(_db);
            Run("INSERT INTO players (id, first_name, last_name, birth_date, position, potential) VALUES (1,'a','b','2000-01-01',0,50)");
            Assert.Catch<DbException>(() => Run("INSERT INTO player_attributes VALUES (1, 0, 101)"));
        }
    }
}
