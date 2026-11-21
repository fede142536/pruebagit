using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Football.Core.Common;
using Football.Core.Data;
using Football.Core.MatchCore;
using NUnit.Framework;

namespace Football.Core.Tests.MatchCore
{
    /// <summary>Modo cancha cerrada (3 contra 3): paredes, sin fuera de juego, partidos completos y archivo de balance.</summary>
    public class IndoorTests
    {
        private static MatchEngine NewIndoor(ulong seed, MatchConfig config = null)
        {
            config = config ?? MatchConfig.CreateIndoor();
            var rng = new SeededRandom(seed);
            IRandom teams = rng.Fork("teams");
            MatchTeamSetup home = RosterFactory.CreateTeam(teams, "H", 65f, Formations.Indoor3, config.Styles[(int)(seed % 5)]);
            MatchTeamSetup away = RosterFactory.CreateTeam(teams, "A", 65f, Formations.Indoor3, config.Styles[(int)((seed + 2) % 5)]);
            return new MatchEngine(config, rng.Fork("engine"), home, away);
        }

        [Test]
        public void IndoorMatch_HasThreePlayersPerSide()
        {
            MatchEngine e = NewIndoor(1);
            Assert.That(e.PlayersPerTeam, Is.EqualTo(3));
            Assert.That(e.Players, Has.Length.EqualTo(6));
            Assert.That(e.Teams[1].Players[0].Index, Is.EqualTo(3));
            Assert.That(e.Teams[1].Players[0].IsKeeper, Is.True);
        }

        [Test]
        public void TeamsOfDifferentSize_AreRejected()
        {
            var config = new MatchConfig();
            var rng = new SeededRandom(1);
            MatchTeamSetup eleven = RosterFactory.CreateTeam(rng, "H", 65f, Formations.F442, config.Styles[0]);
            MatchTeamSetup three = RosterFactory.CreateTeam(rng, "A", 65f, Formations.Indoor3, config.Styles[0]);
            Assert.Throws<ArgumentException>(() => new MatchEngine(config, rng, eleven, three));
        }

        [TestCase(0f, 5f, 0f, 20f)]   // hacia la pared lateral
        [TestCase(5f, 8f, 25f, 0f)]   // hacia la pared del fondo, fuera de la portería
        public void BallHittingAWall_BouncesBackAndStaysInPlay(float x, float y, float vx, float vy)
        {
            MatchEngine e = NewIndoor(2);
            e.TestBeginOpenPlay();
            foreach (MatchPlayer p in e.Players)
            {
                e.TestPlace(p, new Vec2(-15f + 2f * p.Index, -9f));
                p.DecisionTimer = 1000f;
            }

            e.TestPlaceBall(new Vec3(x, y, 0.11f), new Vec3(vx, vy, 0f), e.Players[1]);
            float maxX = 0f, maxY = 0f;
            for (int i = 0; i < 120; i++)
            {
                e.Tick();
                maxX = MathF.Max(maxX, MathF.Abs(e.Ball.Pos.X));
                maxY = MathF.Max(maxY, MathF.Abs(e.Ball.Pos.Y));
            }

            Assert.That(e.Phase, Is.EqualTo(MatchPhase.Playing), "no hay saque: la pared devuelve el balón");
            Assert.That(maxY, Is.LessThanOrEqualTo(e.Pitch.HalfWidth));
            Assert.That(maxX, Is.LessThanOrEqualTo(e.Pitch.HalfLength));
        }

        [Test]
        public void BallIntoTheGoalMouth_IsStillAGoal()
        {
            MatchEngine e = NewIndoor(3);
            e.TestBeginOpenPlay();
            foreach (MatchPlayer p in e.Players)
            {
                e.TestPlace(p, new Vec2(-15f + 2f * p.Index, -9f));
                p.DecisionTimer = 1000f;
            }

            float end = e.Teams[0].AttackDirection;
            e.TestPlaceBall(new Vec3(end * 18f, 0.3f, 0.5f), new Vec3(end * 20f, 0f, 0f), e.Teams[0].Players[2]);
            for (int i = 0; i < 30 && e.Phase == MatchPhase.Playing; i++)
            {
                e.Tick();
            }

            Assert.That(e.Teams[0].Stats.Goals, Is.EqualTo(1));
        }

        [Test]
        public void IndoorMatches_FinishWithoutThrowInsCornersOrOffsides_AndPlentyOfGoals()
        {
            double goals = 0;
            const int n = 40;
            for (ulong seed = 0; seed < n; seed++)
            {
                MatchEngine e = NewIndoor(100 + seed);
                int guard = 0;
                while (!e.IsFinished && guard++ < 100000)
                {
                    e.Tick();
                    foreach (MatchPlayer p in e.Players)
                    {
                        Assert.That(float.IsFinite(p.Pos.X) && float.IsFinite(p.Pos.Y), Is.True);
                    }
                }

                Assert.That(e.IsFinished, Is.True);
                for (int t = 0; t < 2; t++)
                {
                    goals += e.Teams[t].Stats.Goals;
                    Assert.That(e.Events.CountOf(MatchEventType.ThrowIn, t), Is.Zero);
                    Assert.That(e.Events.CountOf(MatchEventType.Corner, t), Is.Zero);
                    Assert.That(e.Events.CountOf(MatchEventType.GoalKick, t), Is.Zero);
                    Assert.That(e.Teams[t].Stats.Offsides, Is.Zero);
                }
            }

            Assert.That(goals / n, Is.InRange(4.0, 9.0), "goles por partido en 3 contra 3");
        }

        [Test]
        public void IndoorBalanceFile_MatchesTheIndoorConfiguration()
        {
            var options = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };
            string dir = AppContext.BaseDirectory;
            string path = null;
            while (dir != null && path == null)
            {
                string candidate = Path.Combine(dir, "Data", "balance", "match_indoor.json");
                path = File.Exists(candidate) ? candidate : null;
                dir = Path.GetDirectoryName(dir);
            }

            Assert.That(path, Is.Not.Null, "falta Data/balance/match_indoor.json (HeadlessSim --indoor --dump-config)");
            var fromFile = JsonSerializer.Deserialize<MatchConfig>(File.ReadAllText(path), options);
            Assert.That(JsonSerializer.Serialize(fromFile, options), Is.EqualTo(JsonSerializer.Serialize(MatchConfig.CreateIndoor(), options)));
        }

        [Test]
        public void RealSquad_PicksKeeperAndTwoOutfieldPlayers_AndPlaysIndoor()
        {
            string[] positions = { "POR", "DFC", "DFC", "LI", "LD", "MC", "MC", "MCO", "EI", "ED", "DC", "DC", "POR" };
            TeamDefinition Team(string name) => new TeamDefinition
            {
                Name = name,
                Formation = "4-4-2",
                Style = "Balanced",
                Players = positions.Select((pos, i) => new PlayerDefinition { Name = name + i, Position = pos, Rating = 60 + i }).ToArray()
            };

            TeamDefinition a = Team("A"), b = Team("B");
            MatchConfig config = MatchConfig.CreateIndoor();
            MatchTeamSetup home = TeamBuilder.Build(a, config, new SeededRandom(1), SquadSelection.Auto(a, Formations.Indoor3, 0));
            MatchTeamSetup away = TeamBuilder.Build(b, config, new SeededRandom(2), SquadSelection.Auto(b, Formations.Indoor3, 0));
            Assert.That(home.Players, Has.Length.EqualTo(3));
            Assert.That(home.Players[0].Role, Is.EqualTo(Football.Core.Domain.Position.Goalkeeper));

            var e = new MatchEngine(config, new SeededRandom(3), home, away);
            e.SetHuman(0, true);
            while (!e.IsFinished)
            {
                e.SetInput(0, new MatchInput { Move = new Vec2(1f, 0f), Press = true, Pass = e.TickCount % 90 == 0 });
                e.Tick();
            }

            Assert.That(e.IsFinished, Is.True);
        }

        [Test]
        public void UnityCopyOfTheIndoorBalanceFile_IsUpToDate()
        {
            string Find(params string[] parts)
            {
                string dir = AppContext.BaseDirectory;
                while (dir != null)
                {
                    string candidate = Path.Combine(new[] { dir }.Concat(parts).ToArray());
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }

                    dir = Path.GetDirectoryName(dir);
                }

                return null;
            }

            string source = Find("Data", "balance", "match_indoor.json");
            string copy = Find("Game", "Assets", "Football", "Resources", "match_indoor.json");
            Assert.That(copy, Is.Not.Null, "falta la copia para Unity: ejecutar Tools/build-core.sh");
            Assert.That(File.ReadAllText(copy), Is.EqualTo(File.ReadAllText(source)));
        }
    }
}
