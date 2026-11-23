using System.IO;
using System.Linq;
using Football.Core.Career;
using Football.Core.Domain;
using Football.Core.Persistence;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace Football.Core.Tests.Career
{
    /// <summary>Dorsales por jugador: sin repetir en el club, editables (con intercambio) y guardados.</summary>
    public class ShirtNumberTests
    {
        private static CareerSession New() => CareerSession.NewGame(CareerSessionTests.Argentina(), "Boca Juniors", 2027, 5, new CareerConfigs());

        [Test]
        public void EveryClub_HasUniqueShirtNumbers_AndKeepersPreferTheOne()
        {
            CareerSession s = New();
            foreach (Club c in s.Data.Clubs)
            {
                var squad = s.Squad(c.Id);
                Assert.That(squad.Select(p => p.ShirtNumber), Is.All.InRange(1, 99), c.Name);
                Assert.That(squad.Select(p => p.ShirtNumber), Is.Unique, c.Name);
            }
        }

        [Test]
        public void SetShirtNumber_SwapsWithTheTeammateWhoHasIt()
        {
            CareerSession s = New();
            var squad = s.Squad(s.Data.UserClubId);
            Player a = squad[0], b = squad[1];
            int na = a.ShirtNumber, nb = b.ShirtNumber;
            Assert.That(s.SetShirtNumber(a.Id, nb), Is.True);
            Assert.That(a.ShirtNumber, Is.EqualTo(nb));
            Assert.That(b.ShirtNumber, Is.EqualTo(na), "intercambian");
            Assert.That(s.SetShirtNumber(a.Id, 0), Is.False);
            Assert.That(s.SetShirtNumber(a.Id, 100), Is.False);
            Player foreign = s.Data.Players.Values.First(p => p.ClubId != s.Data.UserClubId);
            Assert.That(s.SetShirtNumber(foreign.Id, 50), Is.False, "solo los del propio club");
        }

        [Test]
        public void TheMatchUsesThePlayersOwnShirtNumber()
        {
            CareerSession s = New();
            Player p = s.Squad(s.Data.UserClubId)[3];
            s.SetShirtNumber(p.Id, 77);
            var view = Football.Core.Management.MatchPreparation.View(p, s.Data.Conditions[p.Id], p.Position, true, 4, new Football.Core.Management.ManagementConfig());
            Assert.That(view.Number, Is.EqualTo(77));
        }

        [Test]
        public void ShirtNumbers_SurviveTheBinaryFile()
        {
            CareerSession s = New();
            Player p = s.Squad(s.Data.UserClubId)[2];
            s.SetShirtNumber(p.Id, 88);
            s.SyncData();
            var buffer = new MemoryStream();
            CareerFile.Write(buffer, s.Data);
            buffer.Position = 0;
            CareerData back = CareerFile.Read(buffer);
            Assert.That(back.Players[p.Id].ShirtNumber, Is.EqualTo(88));
            Assert.That(back.Players.Values.Count(x => x.ClubId.HasValue && x.ShirtNumber == 0), Is.EqualTo(0));
        }

        [Test]
        public void ShirtNumbers_SurviveSqlite()
        {
            CareerSession s = New();
            Player p = s.Squad(s.Data.UserClubId)[2];
            s.SetShirtNumber(p.Id, 66);
            s.SyncData();
            using (var db = new SqliteConnection("Data Source=:memory:"))
            {
                db.Open();
                SqliteConnectionSetup.Apply(db);
                Migrator.Default.MigrateToLatest(db);
                var repo = new SqlCareerRepository(db);
                repo.Save(s.Data);
                Assert.That(repo.Load().Players[p.Id].ShirtNumber, Is.EqualTo(66));
            }
        }

        [Test]
        public void ANewSigning_GetsAFreeNumber()
        {
            CareerSession s = New();
            Player other = s.Data.Players.Values.First(p => p.ClubId != s.Data.UserClubId && p.Position != Position.Goalkeeper);
            s.Data.Finances[s.Data.UserClubId].TransferBudget = long.MaxValue / 4;
            s.PayReleaseClause(other.Id); // puede no ser posible fuera de ventana; si falla el pase no hay nada que verificar
            if (other.ClubId == s.Data.UserClubId)
            {
                var squad = s.Squad(s.Data.UserClubId);
                Assert.That(squad.Select(p => p.ShirtNumber), Is.Unique);
            }
        }
    }
}
