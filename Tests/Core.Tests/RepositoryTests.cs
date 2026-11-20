using System;
using System.Data.Common;
using Football.Core.Domain;
using Football.Core.Persistence;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace Football.Core.Tests
{
    public class RepositoryTests
    {
        private DbConnection _db;
        private IStadiumRepository _stadiums;
        private IClubRepository _clubs;
        private IPlayerRepository _players;

        [SetUp]
        public void SetUp()
        {
            _db = new SqliteConnection("Data Source=:memory:");
            _db.Open();
            SqliteConnectionSetup.Apply(_db);
            Migrator.Default.MigrateToLatest(_db);
            _stadiums = new SqlStadiumRepository(_db);
            _clubs = new SqlClubRepository(_db);
            _players = new SqlPlayerRepository(_db);
        }

        [TearDown]
        public void TearDown() => _db.Dispose();

        private static Club NewClub(int id, int? stadiumId = null) => new Club
        {
            Id = id, Name = "Club " + id, ShortName = "C" + id, Country = "Ficticia",
            PrimaryColor = "#112233", SecondaryColor = "#FFFFFF", StadiumId = stadiumId,
            TransferBudget = 5_000_000_00L, WageBudget = 1_200_000_00L
        };

        private static Player NewPlayer(int id, int? clubId)
        {
            var p = new Player
            {
                Id = id, ClubId = clubId, FirstName = "Nombre" + id, LastName = "Apellido" + id,
                BirthDate = new DateTime(1998, 3, 14), Position = Position.Striker, Potential = 85,
                Contract = new Contract { WeeklyWage = 25_000_00L, ExpiresOn = new DateTime(2032, 6, 30) }
            };
            int value = 10;
            foreach (AttributeId a in AttributeSet.All)
            {
                p.Attributes[a] = value++;
            }

            return p;
        }

        [Test]
        public void Stadium_RoundTripsAndUpdates()
        {
            _stadiums.Save(new Stadium { Id = 1, Name = "Estadio Uno", Capacity = 30000 });
            _stadiums.Save(new Stadium { Id = 1, Name = "Estadio Uno", Capacity = 35000 });

            Stadium loaded = _stadiums.Get(1);
            Assert.That(loaded.Capacity, Is.EqualTo(35000));
            Assert.That(_stadiums.Get(2), Is.Null);
        }

        [Test]
        public void Club_RoundTripsIncludingNullableStadium()
        {
            _stadiums.Save(new Stadium { Id = 1, Name = "E", Capacity = 100 });
            _clubs.Save(NewClub(1, 1));
            _clubs.Save(NewClub(2));

            Club c1 = _clubs.Get(1);
            Assert.That(c1.Name, Is.EqualTo("Club 1"));
            Assert.That(c1.StadiumId, Is.EqualTo(1));
            Assert.That(c1.TransferBudget, Is.EqualTo(5_000_000_00L));
            Assert.That(_clubs.Get(2).StadiumId, Is.Null);
            Assert.That(_clubs.GetAll(), Has.Count.EqualTo(2));
            Assert.That(_clubs.Get(99), Is.Null);
        }

        [Test]
        public void Player_RoundTripsWithAttributesAndContract()
        {
            _clubs.Save(NewClub(1));
            Player original = NewPlayer(7, 1);
            _players.Save(original);

            Player loaded = _players.Get(7);

            Assert.That(loaded.FirstName, Is.EqualTo("Nombre7"));
            Assert.That(loaded.BirthDate, Is.EqualTo(new DateTime(1998, 3, 14)));
            Assert.That(loaded.Position, Is.EqualTo(Position.Striker));
            Assert.That(loaded.ClubId, Is.EqualTo(1));
            Assert.That(loaded.Contract.WeeklyWage, Is.EqualTo(25_000_00L));
            Assert.That(loaded.Contract.ExpiresOn, Is.EqualTo(new DateTime(2032, 6, 30)));
            foreach (AttributeId a in AttributeSet.All)
            {
                Assert.That(loaded.Attributes[a], Is.EqualTo(original.Attributes[a]), a.ToString());
            }
        }

        [Test]
        public void Player_UpdateReplacesAttributesAndRemovesContract()
        {
            _clubs.Save(NewClub(1));
            Player p = NewPlayer(7, 1);
            _players.Save(p);

            p.Attributes[AttributeId.Pace] = 77;
            p.Contract = null;
            p.ClubId = null;
            _players.Save(p);

            Player loaded = _players.Get(7);
            Assert.That(loaded.Attributes[AttributeId.Pace], Is.EqualTo(77));
            Assert.That(loaded.Contract, Is.Null);
            Assert.That(loaded.ClubId, Is.Null);
        }

        [Test]
        public void GetByClub_ReturnsOnlyThatClubsPlayers()
        {
            _clubs.Save(NewClub(1));
            _clubs.Save(NewClub(2));
            _players.Save(NewPlayer(1, 1));
            _players.Save(NewPlayer(2, 1));
            _players.Save(NewPlayer(3, 2));
            _players.Save(NewPlayer(4, null));

            Assert.That(_players.GetByClub(1), Has.Count.EqualTo(2));
            Assert.That(_players.GetByClub(2), Has.Count.EqualTo(1));
        }

        [Test]
        public void DeletingClub_LeavesItsPlayersWithoutClub()
        {
            _clubs.Save(NewClub(1));
            _players.Save(NewPlayer(1, 1));

            _clubs.Delete(1);

            Assert.That(_clubs.Get(1), Is.Null);
            Assert.That(_players.Get(1).ClubId, Is.Null);
        }

        [Test]
        public void DeletingPlayer_RemovesAttributesAndContract()
        {
            _clubs.Save(NewClub(1));
            _players.Save(NewPlayer(1, 1));

            _players.Delete(1);

            Assert.That(_players.Get(1), Is.Null);
            using (DbCommand cmd = _db.CreateCommand())
            {
                cmd.CommandText = "SELECT (SELECT COUNT(*) FROM player_attributes) + (SELECT COUNT(*) FROM contracts)";
                Assert.That(Convert.ToInt32(cmd.ExecuteScalar()), Is.Zero);
            }
        }

        [Test]
        public void NonPositiveIds_AreRejected()
        {
            Assert.Throws<ArgumentException>(() => _clubs.Save(NewClub(0)));
            Assert.Throws<ArgumentException>(() => _players.Save(NewPlayer(-1, null)));
        }

        [Test]
        public void Player_WithUnknownClub_IsRejectedByForeignKey()
        {
            Assert.Catch<DbException>(() => _players.Save(NewPlayer(1, 404)));
            Assert.That(_players.Get(1), Is.Null, "un guardado fallido no debe dejar datos a medias");
        }
    }
}
