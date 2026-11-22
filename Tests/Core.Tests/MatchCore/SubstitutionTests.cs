using System.Linq;
using Football.Core.Common;
using Football.Core.Data;
using Football.Core.MatchCore;
using NUnit.Framework;

namespace Football.Core.Tests.MatchCore
{
    /// <summary>Cambios con las reglas oficiales y formaciones en vivo.</summary>
    public class SubstitutionTests
    {
        private static readonly string[] Positions =
            { "POR", "LI", "DFC", "DFC", "LD", "MC", "MC", "MCO", "EI", "ED", "DC", "POR", "DFC", "MC", "MC", "DC", "ED", "LD", "MCD", "DC" };

        internal static MatchEngine NewMatch(ulong seed = 1)
        {
            var config = new MatchConfig();
            TeamDefinition Team(string name) => new TeamDefinition
            {
                Name = name, Formation = "4-3-3", Style = "Balanced",
                Players = Positions.Select((p, i) => new PlayerDefinition { Name = name + i, Position = p, Rating = 60 + i % 15 }).ToArray()
            };

            TeamDefinition a = Team("A"), b = Team("B");
            MatchTeamSetup home = TeamBuilder.Build(a, config, new SeededRandom(seed), SquadSelection.Auto(a, Formations.F433));
            MatchTeamSetup away = TeamBuilder.Build(b, config, new SeededRandom(seed + 1), SquadSelection.Auto(b, Formations.F433));
            var e = new MatchEngine(config, new SeededRandom(seed + 2), home, away);
            e.SetHuman(0, true); // la IA del equipo 0 no hace cambios por su cuenta
            return e;
        }

        /// <summary>Lleva el partido a un parón: balón fuera por la banda.</summary>
        private static void Stoppage(MatchEngine e)
        {
            e.TestBeginOpenPlay();
            e.TestPlaceBall(new Vec3(0f, 33.5f, 0f), new Vec3(0f, 6f, 0f), e.Teams[1].Players[5]);
            for (int i = 0; i < 10 && e.Phase == MatchPhase.Playing; i++)
            {
                e.Tick();
            }

            Assert.That(e.Phase, Is.EqualTo(MatchPhase.SetPiece));
        }

        [Test]
        public void Substitution_WaitsForAStoppage_ThenTheNewPlayerComesOnFresh()
        {
            MatchEngine e = NewMatch();
            e.TestBeginOpenPlay();
            MatchPlayer tired = e.Teams[0].Players[7];
            tired.Stamina = 0.3f;
            string benchName = e.Teams[0].Bench[2].Name;
            Assert.That(e.RequestSubstitution(0, tired, 2), Is.EqualTo(SubstitutionResult.Accepted));
            e.Tick();
            Assert.That(e.Teams[0].Players[7], Is.SameAs(tired), "con el juego en marcha, todavía no");

            Stoppage(e);
            MatchPlayer incoming = e.Teams[0].Players[7];
            Assert.That(incoming.Name, Is.EqualTo(benchName));
            Assert.That(incoming.Stamina, Is.EqualTo(1f));
            Assert.That(incoming.Index, Is.EqualTo(tired.Index), "ocupa el mismo lugar");
            Assert.That(incoming.Role, Is.EqualTo(Formations.F433.Slots[7].Role), "juega en el puesto del que sale");
            Assert.That(e.Events.CountOf(MatchEventType.Substitution, 0), Is.EqualTo(1));
            Assert.That(e.SubstitutionsLeft(0), Is.EqualTo(4));
            Assert.That(e.WindowsLeft(0), Is.EqualTo(2));
        }

        [Test]
        public void ASubstitutedPlayer_CannotComeBack_AndABenchPlayerEntersOnlyOnce()
        {
            MatchEngine e = NewMatch();
            Stoppage(e);
            MatchPlayer out1 = e.Teams[0].Players[6];
            Assert.That(e.RequestSubstitution(0, out1, 0), Is.EqualTo(SubstitutionResult.Accepted));
            Assert.That(e.RequestSubstitution(0, e.Teams[0].Players[5], 0), Is.EqualTo(SubstitutionResult.PlayerUnavailable));
            Assert.That(e.RequestSubstitution(0, out1, 1), Is.EqualTo(SubstitutionResult.InvalidOutgoing), "ya no está en el campo");
            Assert.That(e.Teams[0].SubstitutedOut, Does.Contain(out1.Name));
        }

        [Test]
        public void FiveSubstitutions_InThreeWindows_AreTheMaximum()
        {
            MatchEngine e = NewMatch();
            // Ventana 1: dos cambios en el mismo parón.
            Stoppage(e);
            Assert.That(e.RequestSubstitution(0, e.Teams[0].Players[5], 0), Is.EqualTo(SubstitutionResult.Accepted));
            Assert.That(e.RequestSubstitution(0, e.Teams[0].Players[6], 1), Is.EqualTo(SubstitutionResult.Accepted));
            Assert.That(e.WindowsLeft(0), Is.EqualTo(2), "varios cambios en un parón gastan una sola ventana");
            // Ventanas 2 y 3.
            Stoppage(e);
            Assert.That(e.RequestSubstitution(0, e.Teams[0].Players[7], 2), Is.EqualTo(SubstitutionResult.Accepted));
            Stoppage(e);
            Assert.That(e.RequestSubstitution(0, e.Teams[0].Players[8], 3), Is.EqualTo(SubstitutionResult.Accepted));
            Assert.That(e.WindowsLeft(0), Is.EqualTo(0));
            // Sin ventanas, un nuevo parón no permite más cambios aunque quede cupo.
            Stoppage(e);
            Assert.That(e.SubstitutionsLeft(0), Is.EqualTo(1));
            Assert.That(e.RequestSubstitution(0, e.Teams[0].Players[9], 4), Is.EqualTo(SubstitutionResult.NoWindowsLeft));
        }

        [Test]
        public void HalfTimeSubstitutions_DoNotUseAWindow_ButCountTowardsTheFive()
        {
            MatchEngine e = NewMatch();
            while (e.Half == 1)
            {
                e.Tick();
            }

            Assert.That(e.InHalfTimeBreak, Is.True);
            Assert.That(e.RequestSubstitution(0, e.Teams[0].Players[5], 0), Is.EqualTo(SubstitutionResult.Accepted));
            Assert.That(e.RequestSubstitution(0, e.Teams[0].Players[6], 1), Is.EqualTo(SubstitutionResult.Accepted));
            Assert.That(e.RequestSubstitution(0, e.Teams[0].Players[7], 2), Is.EqualTo(SubstitutionResult.Accepted));
            Assert.That(e.WindowsLeft(0), Is.EqualTo(3), "el entretiempo no gasta ventana");
            Assert.That(e.SubstitutionsLeft(0), Is.EqualTo(2));
            Assert.That(e.RequestSubstitution(0, e.Teams[0].Players[8], 3), Is.EqualTo(SubstitutionResult.Accepted));
            Assert.That(e.RequestSubstitution(0, e.Teams[0].Players[9], 4), Is.EqualTo(SubstitutionResult.Accepted));
            Assert.That(e.RequestSubstitution(0, e.Teams[0].Players[10], 5), Is.EqualTo(SubstitutionResult.NoSubstitutionsLeft));
        }

        [Test]
        public void AiTeams_SubstituteTiredPlayers_WithinTheRules()
        {
            int teamsThatSubbed = 0;
            for (ulong seed = 0; seed < 10; seed++)
            {
                MatchEngine e = NewMatch(seed + 20);
                e.SetHuman(0, false);
                while (!e.IsFinished)
                {
                    e.Tick();
                }

                for (int t = 0; t < 2; t++)
                {
                    Assert.That(e.Teams[t].SubstitutionsUsed, Is.LessThanOrEqualTo(5));
                    Assert.That(e.Teams[t].WindowsUsed, Is.LessThanOrEqualTo(3));
                    teamsThatSubbed += e.Teams[t].SubstitutionsUsed > 0 ? 1 : 0;
                }
            }

            Assert.That(teamsThatSubbed, Is.GreaterThan(5), "la IA refresca a los cansados en el segundo tiempo");
        }

        [Test]
        public void ChangingFormationLive_UpdatesRolesAndTheAiZones()
        {
            MatchEngine e = NewMatch();
            e.TestBeginOpenPlay();
            Assert.That(e.SetFormation(0, Formations.F4312), Is.True);
            for (int i = 0; i < 11; i++)
            {
                Assert.That(e.Teams[0].Players[i].Role, Is.EqualTo(Formations.F4312.Slots[i].Role));
            }

            Assert.That(e.SetFormation(0, Formations.Indoor3), Is.False, "un dibujo de otra cantidad de jugadores no vale");
        }

        [Test]
        public void SwappingPositions_ExchangesRoles_ButNeverTheKeeper()
        {
            MatchEngine e = NewMatch();
            MatchPlayer winger = e.Teams[0].Players[8];
            MatchPlayer mid = e.Teams[0].Players[6];
            var wingerRole = winger.Role;
            var midRole = mid.Role;
            Assert.That(e.SwapPositions(winger, mid), Is.True);
            Assert.That(winger.Role, Is.EqualTo(midRole));
            Assert.That(mid.Role, Is.EqualTo(wingerRole));
            Assert.That(e.SwapPositions(e.Teams[0].Players[0], mid), Is.False);
        }
    }
}
