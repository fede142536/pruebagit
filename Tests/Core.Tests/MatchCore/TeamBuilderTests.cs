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
            Assert.That(names, Is.SupersetOf(new[]
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

                    Assert.That(new[] { "Solid", "Stripes", "Band", "Sash", "Chevron", "Halves" }, Does.Contain(k.Pattern));
                }

                foreach (PlayerDefinition p in t.Players)
                {
                    Assert.That(TeamBuilder.IsKnownPosition(p.Position), Is.True, $"{t.Name}/{p.Name}: posición {p.Position}");
                    Assert.That(p.Rating, Is.InRange(40, 95), $"{t.Name}/{p.Name}: media");
                }
            }
        }

        [Test]
        public void ArgentinaFile_PassesTheRuntimeValidation_AndIsReadableOnScreen()
        {
            foreach (TeamDefinition t in LoadArgentina().Teams)
            {
                Assert.That(TeamBuilder.Validate(t), Is.Null);
                Assert.That(t.Players.Select(TeamBuilder.DisplayName), Is.Unique, t.Name + ": dos jugadores se verían con el mismo nombre");
                Assert.That(t.Home.Shirt, Is.Not.EqualTo(t.Away.Shirt), t.Name + ": la camiseta suplente es igual a la titular");
                int[] numbers = t.Players.Where(p => p.Number > 0).Select(p => p.Number).ToArray();
                Assert.That(numbers, Is.Unique, t.Name + ": dorsales repetidos");
                Assert.That(numbers, Is.All.InRange(1, 99), t.Name + ": dorsal fuera de rango");
                foreach (Formation f in new[] { Formations.F442, Formations.F433 })
                {
                    int[] xi = TeamBuilder.PickLineup(t, f);
                    Assert.That(TeamBuilder.ParsePosition(t.Players[xi[0]].Position), Is.EqualTo(Position.Goalkeeper), $"{t.Name} {f.Name}: arquero");
                }
            }
        }

        [Test]
        public void ArgentinaFile_HasTheNineNewClubs_AndTheirClassics()
        {
            TeamDatabase db = LoadArgentina();
            string[] wanted =
            {
                "Vélez Sarsfield", "Defensa y Justicia", "Banfield", "Lanús", "Gimnasia y Esgrima La Plata", "Talleres", "Belgrano",
                "Rosario Central", "Newell's Old Boys"
            };
            foreach (string name in wanted)
            {
                TeamDefinition team = db.Teams.SingleOrDefault(t => t.Name == name);
                Assert.That(team, Is.Not.Null, name);
                Assert.That(team.Home.Shirt, Is.Not.EqualTo(team.Away.Shirt), name + ": titular y alternativa distintas");
            }

            Assert.That(db.Teams.Select(t => t.Name).Distinct().Count(), Is.EqualTo(db.Teams.Length), "sin equipos repetidos");
            Assert.That(db.RivalryBetween("Rosario Central", "Newell's Old Boys"), Is.EqualTo(RivalryLevel.Classic));
            Assert.That(db.RivalryBetween("Belgrano", "Talleres"), Is.EqualTo(RivalryLevel.Classic), "en cualquier orden");
            Assert.That(db.RivalryBetween("Banfield", "Lanús"), Is.EqualTo(RivalryLevel.Classic));
            Assert.That(db.RivalryBetween("Vélez Sarsfield", "Belgrano"), Is.EqualTo(RivalryLevel.None));
            foreach (RivalryDefinition r in db.Rivalries)
            {
                Assert.That(db.Teams.Any(t => t.Name == r.TeamA) && db.Teams.Any(t => t.Name == r.TeamB), Is.True,
                    $"la rivalidad {r.Name} usa equipos del archivo");
                Assert.That(Enum.TryParse(r.Level, true, out RivalryLevel _), Is.True, r.Level);
            }
        }

        [Test]
        public void NineNewClubs_HaveRealSquadsOf20To22_WithShirtNumbers_AndNoPlaceholders()
        {
            TeamDatabase db = LoadArgentina();
            foreach (TeamDefinition t in db.Teams.Skip(6))
            {
                Assert.That(t.Players.Length, Is.InRange(20, 22), t.Name + ": tamaño del plantel");
                Assert.That(t.Players.Any(p => p.Name.Contains("ficticio")), Is.False, t.Name + ": quedan jugadores ficticios");
                Assert.That(t.Players.All(p => p.Number > 0), Is.True, t.Name + ": todos con dorsal");
            }
        }

        [Test]
        public void Validate_RejectsUnplayableTeams()
        {
            var keeperOnly = Enumerable.Range(0, 11).Select(i => new PlayerDefinition { Name = "J" + i, Position = "DC", Rating = 60 }).ToArray();
            Assert.That(TeamBuilder.Validate(null), Is.Not.Null);
            Assert.That(TeamBuilder.Validate(new TeamDefinition { Name = "Sin jugadores" }), Is.Not.Null);
            Assert.That(TeamBuilder.Validate(new TeamDefinition { Name = "Sin arquero", Players = keeperOnly }), Does.Contain("portero"));
            keeperOnly[0].Position = "POR";
            Assert.That(TeamBuilder.Validate(new TeamDefinition { Name = "Bien", Players = keeperOnly }), Is.Null);
            keeperOnly[3].Position = "XX";
            Assert.That(TeamBuilder.Validate(new TeamDefinition { Name = "Posición rara", Players = keeperOnly }), Is.Not.Null);
        }

        [Test]
        public void Build_KeepsShirtNumbersStableAcrossFormations()
        {
            TeamDefinition t = LoadArgentina().Teams[0];
            var config = new MatchConfig();
            MatchTeamSetup a = TeamBuilder.Build(t, config, new SeededRandom(1), SquadSelection.Auto(t, Formations.F442));
            MatchTeamSetup b = TeamBuilder.Build(t, config, new SeededRandom(1), SquadSelection.Auto(t, Formations.F433));
            foreach (PlayerMatchView p in a.Players)
            {
                PlayerMatchView same = b.Players.FirstOrDefault(q => q.Name == p.Name);
                if (same != null)
                {
                    Assert.That(same.Number, Is.EqualTo(p.Number), p.Name + ": el dorsal no depende del puesto");
                }
            }
        }

        [Test]
        public void UnityCopyOfTheBalanceFile_IsUpToDate()
        {
            string source = RepoFile("Data", "balance", "match.json");
            string copy = RepoFile("Game", "Assets", "Football", "Resources", "match.json");
            Assert.That(copy, Is.Not.Null, "falta la copia para Unity: ejecutar Tools/build-core.sh");
            Assert.That(File.ReadAllText(copy), Is.EqualTo(File.ReadAllText(source)), "copia desactualizada: ejecutar Tools/build-core.sh");
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
