using System.IO;
using System.Linq;
using Football.Core.Career;
using Football.Core.Domain;
using Football.Core.Management;
using Football.Core.Persistence;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace Football.Core.Tests.Career
{
    /// <summary>El DT arma su once inicial y su banco: se respeta, se reemplaza a los que no pueden jugar y se guarda con la partida.</summary>
    public class CareerLineupTests
    {
        private static CareerSession Start() => CareerSessionTests.NewCareer(5);

        /// <summary>Alineación distinta de la automática: los titulares en orden inverso y el banco con los peores disponibles.</summary>
        private static (int[] starters, int[] bench) Custom(CareerSession s)
        {
            MatchPreparation.Lineup auto = s.UserLineup();
            int[] starters = auto.Starters.Select(p => p.Id).Reverse().ToArray();
            var rest = s.Squad(s.Data.UserClubId).Where(p => !starters.Contains(p.Id)).OrderBy(p => PlayerRatings.Overall(p.Attributes, p.Position)).ToList();
            return (starters, rest.Take(5).Select(p => p.Id).ToArray());
        }

        [Test]
        public void ByDefault_TheLineupIsAutomatic_WithElevenStartersAndABench()
        {
            CareerSession s = Start();
            MatchPreparation.Lineup l = s.UserLineup();
            Assert.That(s.HasCustomLineup, Is.False);
            Assert.That(l.Starters, Has.Count.EqualTo(s.UserFormation().Slots.Length));
            Assert.That(l.Bench, Has.Count.EqualTo(MatchPreparation.BenchSize));
            Assert.That(l.Starters.Concat(l.Bench).Select(p => p.Id).Distinct().Count(), Is.EqualTo(l.Starters.Count + l.Bench.Count));
        }

        [Test]
        public void AChosenLineup_IsUsedByTheMatch_SlotBySlot()
        {
            CareerSession s = Start();
            (int[] starters, int[] bench) = Custom(s);
            Assert.That(s.SetUserLineup(starters, bench), Is.True);
            Assert.That(s.HasCustomLineup, Is.True);
            Assert.That(s.UserLineup().Starters.Select(p => p.Id), Is.EqualTo(starters));
            Assert.That(s.UserLineup().Bench.Select(p => p.Id), Is.EqualTo(bench));

            s.AdvanceToNextMatch();
            s.PrepareUserMatch(out MatchTeamSetup home, out MatchTeamSetup away);
            MatchTeamSetup mine = s.PendingUserFixture.HomeClubId == s.Data.UserClubId ? home : away;
            Assert.That(mine.Players.Select(p => p.Id), Is.EqualTo(starters));
            Assert.That(mine.Bench.Select(p => p.Id), Is.EqualTo(bench));
        }

        [Test]
        public void AnInjuredStarter_IsReplaced_AndAnInjuredSubstituteIsDropped()
        {
            CareerSession s = Start();
            (int[] starters, int[] bench) = Custom(s);
            s.SetUserLineup(starters, bench);
            s.Data.Conditions[starters[3]].InjuryDays = 20;
            s.Data.Conditions[bench[0]].InjuryDays = 20;
            MatchPreparation.Lineup l = s.UserLineup();
            Assert.That(l.Starters.Select(p => p.Id), Does.Not.Contain(starters[3]), "el lesionado no juega");
            Assert.That(l.Starters, Has.Count.EqualTo(starters.Length));
            for (int i = 0; i < starters.Length; i++)
            {
                if (i != 3)
                {
                    Assert.That(l.Starters[i].Id, Is.EqualTo(starters[i]), "los demás siguen en su puesto");
                }
            }

            Assert.That(l.Bench.Select(p => p.Id), Does.Not.Contain(bench[0]));
            Assert.That(l.Bench.Select(p => p.Id), Is.EqualTo(bench.Skip(1)));
        }

        [Test]
        public void InvalidLineups_AreRejected_AndClearingGoesBackToAutomatic()
        {
            CareerSession s = Start();
            (int[] starters, int[] bench) = Custom(s);
            int stranger = s.Data.Players.Values.First(p => p.ClubId != s.Data.UserClubId).Id;
            Assert.That(s.SetUserLineup(starters.Take(10).ToArray(), bench), Is.False, "faltan titulares");
            Assert.That(s.SetUserLineup(starters, bench.Concat(new[] { starters[0] }).ToArray()), Is.False, "un jugador repetido");
            Assert.That(s.SetUserLineup(starters.Take(10).Concat(new[] { stranger }).ToArray(), bench), Is.False, "uno de otro club");
            Assert.That(s.SetUserLineup(starters, s.Squad(s.Data.UserClubId).Select(p => p.Id).Where(id => !starters.Contains(id)).Take(10).ToArray()), Is.False, "banco demasiado largo");
            Assert.That(s.HasCustomLineup, Is.False);
            s.SetUserLineup(starters, bench);
            s.ClearUserLineup();
            Assert.That(s.HasCustomLineup, Is.False);
            Assert.That(MatchPreparation.TryParseLineup("basura;x", out _, out _), Is.False);
        }

        [Test]
        public void TheLineup_SurvivesTheBinaryFileAndSqlite()
        {
            CareerSession s = Start();
            (int[] starters, int[] bench) = Custom(s);
            s.SetUserLineup(starters, bench);
            s.SyncData();
            var buffer = new MemoryStream();
            CareerFile.Write(buffer, s.Data);
            buffer.Position = 0;
            Assert.That(CareerFile.Read(buffer).Tactics.Lineup, Is.EqualTo(s.Data.Tactics.Lineup));

            using (var db = new SqliteConnection("Data Source=:memory:"))
            {
                db.Open();
                SqliteConnectionSetup.Apply(db);
                Migrator.Default.MigrateToLatest(db);
                var repo = new SqlCareerRepository(db);
                repo.Save(s.Data);
                Assert.That(repo.Load().Tactics.Lineup, Is.EqualTo(s.Data.Tactics.Lineup));
            }
        }
    }
}
