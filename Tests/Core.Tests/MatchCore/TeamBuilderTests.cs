using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Football.Core.Common;
using Football.Core.Data;
using Football.Core.Domain;
using Football.Core.MatchCore;
using NUnit.Framework;

namespace Football.Core.Tests.MatchCore
{
    public class TeamBuilderTests
    {
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };

        private static string RepoFile(params string[] parts)
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

        private static TeamDatabase LoadArgentina()
        {
            string path = RepoFile("Data", "teams", "argentina.json");
            Assert.That(path, Is.Not.Null, "Data/teams/argentina.json debe existir");
            return JsonSerializer.Deserialize<TeamDatabase>(File.ReadAllText(path), Json);
        }

        [Test]
        public void ArgentinaFile_HasTheSixRequestedClubs()
        {
            TeamDatabase db = LoadArgentina();
            string[] names = db.Teams.Select(t => t.Name).ToArray();
            Assert.That(names, Is.EquivalentTo(new[]
            {
                "Boca Juniors", "River Plate", "Racing Club", "Estudiantes de La Plata", "San Lorenzo", "Independiente"
            }));
        }

        [Test]
        public void ArgentinaFile_EveryTeamIsValid()
        {
            var hex = new Regex("^#[0-9A-Fa-f]{6}$");
            var config = new MatchConfig();
            foreach (TeamDefinition t in LoadArgentina().Teams)
            {
                Assert.That(t.Players.Length, Is.GreaterThanOrEqualTo(18), t.Name + ": plantilla corta");
                Assert.That(t.Players.Count(p => p.Position == "POR"), Is.GreaterThanOrEqualTo(2), t.Name + ": porteros");
                Assert.That(config.Styles.Any(s => s.Name == t.Style), Is.True, t.Name + ": estilo desconocido " + t.Style);
                Assert.That(Formations.All.Any(f => f.Name == t.Formation), Is.True, t.Name + ": formación desconocida");
                Assert.That(t.Players.Select(p => p.Name), Is.Unique, t.Name + ": nombres repetidos");
                foreach (KitDefinition k in new[] { t.Home, t.Away })
                {
                    foreach (string color in new[] { k.Shirt, k.Secondary, k.Shorts, k.Socks })
                    {
                        Assert.That(hex.IsMatch(color), Is.True, $"{t.Name}: color inválido {color}");
                    }

                    Assert.That(new[] { "Solid", "Stripes", "Band", "Sash" }, Does.Contain(k.Pattern));
                }

                foreach (PlayerDefinition p in t.Players)
                {
                    Assert.That(TeamBuilder.IsKnownPosition(p.Position), Is.True, $"{t.Name}/{p.Name}: posición {p.Position}");
                    Assert.That(p.Rating, Is.InRange(40, 95), $"{t.Name}/{p.Name}: media");
                }
            }
        }

        [Test]
        public void UnityCopyOfTheFile_IsUpToDate()
        {
            string source = RepoFile("Data", "teams", "argentina.json");
            string copy = RepoFile("Game", "Assets", "Football", "Resources", "teams.json");
            Assert.That(copy, Is.Not.Null, "falta la copia para Unity: ejecutar Tools/build-core.sh");
            Assert.That(File.ReadAllText(copy), Is.EqualTo(File.ReadAllText(source)), "copia desactualizada: ejecutar Tools/build-core.sh");
        }

        [Test]
        public void PickLineup_PutsAKeeperInGoalAndNeverRepeatsAPlayer()
        {
            foreach (TeamDefinition t in LoadArgentina().Teams)
            {
                Formation f = TeamBuilder.ParseFormation(t.Formation);
                int[] lineup = TeamBuilder.PickLineup(t, f);
                Assert.That(lineup, Has.None.EqualTo(-1), t.Name);
                Assert.That(lineup, Is.Unique, t.Name);
                Assert.That(t.Players[lineup[0]].Position, Is.EqualTo("POR"), t.Name + ": el puesto 0 es el portero");
                for (int s = 1; s < lineup.Length; s++)
                {
                    Assert.That(t.Players[lineup[s]].Position, Is.Not.EqualTo("POR"), t.Name + ": portero jugando de campo");
                }
            }
        }

        [Test]
        public void PickLineup_ChoosesTheBetterPlayerForAPosition()
        {
            var team = new TeamDefinition
            {
                Name = "Prueba",
                Formation = "4-4-2",
                Players = Enumerable.Range(0, 11).Select(i => new PlayerDefinition { Name = "J" + i, Position = "MC", Rating = 60 }).ToArray()
            };
            team.Players[0].Position = "POR";
            team.Players = team.Players.Concat(new[]
            {
                new PlayerDefinition { Name = "Portero malo", Position = "POR", Rating = 50 },
                new PlayerDefinition { Name = "Portero bueno", Position = "POR", Rating = 80 }
            }).ToArray();

            int[] lineup = TeamBuilder.PickLineup(team, Formations.F442);

            Assert.That(team.Players[lineup[0]].Name, Is.EqualTo("Portero bueno"));
        }

        [Test]
        public void Penalty_IsZeroInPositionAndHugeForKeepers()
        {
            Assert.That(TeamBuilder.Penalty(Position.Striker, Position.Striker), Is.Zero);
            Assert.That(TeamBuilder.Penalty(Position.LeftBack, Position.LeftMidfielder), Is.LessThan(TeamBuilder.Penalty(Position.LeftBack, Position.Striker)));
            Assert.That(TeamBuilder.Penalty(Position.Goalkeeper, Position.Striker), Is.GreaterThanOrEqualTo(50));
            Assert.That(TeamBuilder.Penalty(Position.Striker, Position.Goalkeeper), Is.GreaterThanOrEqualTo(50));
        }

        [Test]
        public void DisplayName_UsesShortNameOrSurname()
        {
            Assert.That(TeamBuilder.DisplayName(new PlayerDefinition { Name = "Leandro Paredes" }), Is.EqualTo("Paredes"));
            Assert.That(TeamBuilder.DisplayName(new PlayerDefinition { Name = "Adrián Martínez", ShortName = "Maravilla" }), Is.EqualTo("Maravilla"));
            Assert.That(TeamBuilder.DisplayName(new PlayerDefinition { Name = "Pelé" }), Is.EqualTo("Pelé"));
        }

        [Test]
        public void Build_BetterRatedPlayersGetBetterAttributes()
        {
            static TeamDefinition Uniform(int rating) => new TeamDefinition
            {
                Name = "R" + rating,
                Formation = "4-4-2",
                Players = new[] { "POR", "LI", "DFC", "DFC", "LD", "MI", "MC", "MC", "MD", "DC", "DC" }
                    .Select((p, i) => new PlayerDefinition { Name = "J" + i, Position = p, Rating = rating }).ToArray()
            };

            var config = new MatchConfig();
            MatchTeamSetup weak = TeamBuilder.Build(Uniform(55), config, new SeededRandom(1));
            MatchTeamSetup strong = TeamBuilder.Build(Uniform(80), config, new SeededRandom(1));

            double Avg(MatchTeamSetup s, AttributeId id) => s.Players.Skip(1).Average(p => p.Attributes[id]);
            Assert.That(Avg(strong, AttributeId.Passing), Is.GreaterThan(Avg(weak, AttributeId.Passing) + 15));
            Assert.That(strong.Players[0].Attributes[AttributeId.Reflexes], Is.GreaterThan(weak.Players[0].Attributes[AttributeId.Reflexes]));
            Assert.That(strong.Players.Select(p => p.Number), Is.Unique, "dorsales automáticos sin repetir");
        }

        [Test]
        public void Build_RejectsTeamsWithFewerThanElevenPlayers()
        {
            var team = new TeamDefinition { Name = "Corto", Players = new PlayerDefinition[5] };
            Assert.Throws<ArgumentException>(() => TeamBuilder.Build(team, new MatchConfig(), new SeededRandom(1)));
        }

        [Test]
        public void RealTeams_PlayACompleteMatch()
        {
            TeamDatabase db = LoadArgentina();
            var config = new MatchConfig();
            var rng = new SeededRandom(11);
            MatchTeamSetup boca = TeamBuilder.Build(db.Teams.First(t => t.Name == "Boca Juniors"), config, rng.Fork("a"));
            MatchTeamSetup river = TeamBuilder.Build(db.Teams.First(t => t.Name == "River Plate"), config, rng.Fork("b"));
            Assert.That(boca.Players.Select(p => p.Name), Does.Contain("Paredes"));

            var engine = new MatchEngine(config, rng.Fork("match"), boca, river);
            int guard = 0;
            while (!engine.IsFinished && guard++ < 100000)
            {
                engine.Tick();
            }

            Assert.That(engine.IsFinished, Is.True);
            Assert.That(engine.Teams[0].Name, Is.EqualTo("Boca Juniors"));
        }

        [Test]
        public void LineupRating_DifferentiatesTheClubs()
        {
            float[] ratings = LoadArgentina().Teams.Select(TeamBuilder.LineupRating).ToArray();
            Assert.That(ratings, Has.All.InRange(60f, 85f));
            Assert.That(ratings.Max() - ratings.Min(), Is.GreaterThan(1f), "las medias distinguen a los equipos");
        }
    }
}
