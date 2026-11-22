using System;
using System.IO;
using System.Linq;
using Football.Core.Career;
using Football.Core.Domain;
using Football.Core.Persistence;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace Football.Core.Tests.Career
{
    /// <summary>Guardado y carga de una partida completa del modo carrera sobre todas las tablas (v1–v8).</summary>
    public class CareerRepositoryTests
    {
        private static SqliteConnection OpenMigrated()
        {
            var db = new SqliteConnection("Data Source=:memory:");
            db.Open();
            SqliteConnectionSetup.Apply(db);
            Migrator.Default.MigrateToLatest(db);
            return db;
        }

        private static void PlayMatches(CareerSession s, int matches)
        {
            for (int i = 0; i < matches; i++)
            {
                s.AdvanceToNextMatch();
                s.SimulateUserMatch();
            }
        }

        private static string Results(CareerSession s) =>
            string.Join(";", s.Data.Fixtures.Where(f => f.IsPlayed).OrderBy(f => f.Id).Select(f => f.Id + ":" + f.HomeGoals + "-" + f.AwayGoals));

        [Test]
        public void SaveAndLoad_RoundTripsEveryTable()
        {
            CareerSession s = CareerSessionTests.NewCareer();
            PlayMatches(s, 4);
            s.SendScout(s.Data.Players.Values.First(p => p.ClubId != s.Data.UserClubId).Id);
            s.Data.Players.Values.First().Contract.Promise = PlayingTimePromise.Star;
            s.SyncData();
            using (SqliteConnection db = OpenMigrated())
            {
                var repo = new SqlCareerRepository(db);
                Assert.That(repo.HasCareer(), Is.False);
                repo.Save(s.Data);
                Assert.That(repo.HasCareer(), Is.True);
                CareerData loaded = repo.Load();
                CareerData d = s.Data;
                Assert.That(loaded.Today, Is.EqualTo(d.Today));
                Assert.That(loaded.Season, Is.EqualTo(d.Season));
                Assert.That(loaded.UserClubId, Is.EqualTo(d.UserClubId));
                Assert.That(loaded.Clubs.Select(c => c.Name), Is.EqualTo(d.Clubs.Select(c => c.Name)));
                Assert.That(loaded.Players.Count, Is.EqualTo(d.Players.Count));
                Player a = d.Players.Values.First(), b = loaded.Players[a.Id];
                Assert.That(AttributeSet.All.Select(x => b.Attributes[x]), Is.EqualTo(AttributeSet.All.Select(x => a.Attributes[x])));
                Assert.That(b.Contract.ReleaseClause, Is.EqualTo(a.Contract.ReleaseClause));
                Assert.That(b.Contract.Promise, Is.EqualTo(PlayingTimePromise.Star));
                Assert.That(b.Personality, Is.EqualTo(a.Personality));
                Assert.That(loaded.Conditions[a.Id].Morale, Is.EqualTo(d.Conditions[a.Id].Morale));
                Assert.That(loaded.Finances[d.UserClubId].Cash, Is.EqualTo(d.Finances[d.UserClubId].Cash));
                Assert.That(loaded.Finances[d.UserClubId].Ledger.Count, Is.EqualTo(d.Finances[d.UserClubId].Ledger.Count));
                Assert.That(loaded.Fixtures.Count(f => f.IsPlayed), Is.EqualTo(d.Fixtures.Count(f => f.IsPlayed)));
                Assert.That(loaded.Competitions.Count, Is.EqualTo(d.Competitions.Count));
                Assert.That(loaded.Rivalries.Count, Is.EqualTo(d.Rivalries.Count));
                Assert.That(loaded.Staff.Count, Is.EqualTo(d.Staff.Count));
                Assert.That(loaded.News.Select(n => n.Key + string.Join(",", n.Args)), Is.EqualTo(d.News.Select(n => n.Key + string.Join(",", n.Args))));
                Assert.That(loaded.Records.Count, Is.EqualTo(d.Records.Count));
                Assert.That(loaded.CareerGoals, Is.EquivalentTo(d.CareerGoals));
            }
        }

        [Test]
        public void ALoadedCareer_ContinuesExactlyLikeOneThatWasNeverSaved()
        {
            CareerSession straight = CareerSessionTests.NewCareer(21);
            PlayMatches(straight, 8);

            CareerSession first = CareerSessionTests.NewCareer(21);
            PlayMatches(first, 4);
            first.SyncData();
            CareerData loaded;
            using (SqliteConnection db = OpenMigrated())
            {
                var repo = new SqlCareerRepository(db);
                repo.Save(first.Data);
                loaded = repo.Load();
            }

            var resumed = new CareerSession(loaded, new CareerConfigs());
            PlayMatches(resumed, 4);
            Assert.That(Results(resumed), Is.EqualTo(Results(straight)));
            Assert.That(resumed.Data.Finances[resumed.Data.UserClubId].Cash, Is.EqualTo(straight.Data.Finances[straight.Data.UserClubId].Cash));
        }

        [Test]
        public void TheBinaryCareerFile_ResumesExactlyLikeSqlite_AndRejectsForeignFiles()
        {
            CareerSession straight = CareerSessionTests.NewCareer(33);
            PlayMatches(straight, 6);

            CareerSession first = CareerSessionTests.NewCareer(33);
            first.Data.Tactics.Formation = "4-3-3";
            straight.Data.Tactics.Formation = "4-3-3";
            PlayMatches(first, 3);
            first.SyncData();
            var buffer = new MemoryStream();
            CareerFile.Write(buffer, first.Data);
            buffer.Position = 0;
            CareerData loaded = CareerFile.Read(buffer);
            Assert.That(loaded.Tactics.Formation, Is.EqualTo("4-3-3"));
            var resumed = new CareerSession(loaded, new CareerConfigs());
            PlayMatches(resumed, 3);

            CareerSession again = CareerSessionTests.NewCareer(33);
            again.Data.Tactics.Formation = "4-3-3";
            PlayMatches(again, 6);
            Assert.That(Results(resumed), Is.EqualTo(Results(again)));
            Assert.Throws<InvalidDataException>(() => CareerFile.Read(new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 })));
        }

        [Test]
        public void SavingToAFile_IsAtomic_AndKeepsABackup()
        {
            string dir = Path.Combine(Path.GetTempPath(), "potrero-career-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "carrera.db");
            try
            {
                CareerSession s = CareerSessionTests.NewCareer();
                for (int round = 0; round < 2; round++)
                {
                    PlayMatches(s, 1);
                    s.SyncData();
                    using (SqliteConnection db = OpenMigrated())
                    {
                        new SqlCareerRepository(db).Save(s.Data);
                        AtomicSave.SaveSqlite(db, path);
                    }
                }

                Assert.That(File.Exists(path), Is.True);
                Assert.That(File.Exists(AtomicSave.BackupName(path, 1)), Is.True, "la versión anterior queda como respaldo");
                using (var db = new SqliteConnection("Data Source=" + path))
                {
                    db.Open();
                    CareerData loaded = new SqlCareerRepository(db).Load();
                    Assert.That(loaded.Fixtures.Count(f => f.IsPlayed), Is.EqualTo(s.Data.Fixtures.Count(f => f.IsPlayed)));
                }
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                Directory.Delete(dir, true);
            }
        }
    }
}
