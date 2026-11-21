using System.Linq;
using Football.Core.Common;
using Football.Core.Data;
using Football.Core.MatchCore;
using NUnit.Framework;

namespace Football.Core.Tests.MatchCore
{
    public class SquadSelectionTests
    {
        private static TeamDefinition Team(int size = 24)
        {
            string[] positions = { "POR", "LI", "DFC", "DFC", "LD", "MI", "MC", "MC", "MD", "DC", "DC", "POR", "DFC", "MCD", "MCO", "EI", "ED", "DC", "LD", "LI", "MC", "POR", "DFC", "MC" };
            return new TeamDefinition
            {
                Name = "Prueba",
                Formation = "4-4-2",
                Players = Enumerable.Range(0, size).Select(i => new PlayerDefinition
                {
                    Name = "Jugador " + i, Position = positions[i % positions.Length], Rating = 60 + (i * 7) % 20
                }).ToArray()
            };
        }

        [Test]
        public void Auto_Picks11StartersAFullBenchAndTheRestAsReserves()
        {
            TeamDefinition t = Team();
            SquadSelection s = SquadSelection.Auto(t, Formations.F442);

            Assert.That(s.Starters, Has.Count.EqualTo(11));
            Assert.That(s.Bench, Has.Count.EqualTo(SquadSelection.DefaultBenchSize));
            Assert.That(s.Reserves, Has.Count.EqualTo(t.Players.Length - 11 - SquadSelection.DefaultBenchSize));
            Assert.That(s.IsValid(t), Is.True);
            Assert.That(s.Starters.Concat(s.Bench).Concat(s.Reserves), Is.EquivalentTo(Enumerable.Range(0, t.Players.Length)));
        }

        [Test]
        public void Auto_KeepsABackupKeeperOnTheBench()
        {
            TeamDefinition t = Team();
            SquadSelection s = SquadSelection.Auto(t, Formations.F433);
            Assert.That(s.Bench.Any(i => t.Players[i].Position == "POR"), Is.True);
        }

        [Test]
        public void Auto_WithAShortSquad_HasNoReserves()
        {
            TeamDefinition t = Team(15);
            SquadSelection s = SquadSelection.Auto(t, Formations.F442);
            Assert.That(s.Bench, Has.Count.EqualTo(4));
            Assert.That(s.Reserves, Is.Empty);
            Assert.That(s.IsValid(t), Is.True);
        }

        [Test]
        public void Swap_MovesPlayersBetweenGroups()
        {
            TeamDefinition t = Team();
            SquadSelection s = SquadSelection.Auto(t, Formations.F442);
            int starter = s.Starters[9], reserve = s.Reserves[0];

            s.Swap(SquadGroup.Starter, 9, SquadGroup.Reserve, 0);

            Assert.That(s.Starters[9], Is.EqualTo(reserve));
            Assert.That(s.Reserves[0], Is.EqualTo(starter));
            Assert.That(s.IsValid(t), Is.True);
        }

        [Test]
        public void ChangeFormation_KeepsTheSameElevenPlayers()
        {
            TeamDefinition t = Team();
            SquadSelection s = SquadSelection.Auto(t, Formations.F442);
            int[] before = s.Starters.OrderBy(i => i).ToArray();

            s.ChangeFormation(t, Formations.F352);

            Assert.That(s.Formation, Is.SameAs(Formations.F352));
            Assert.That(s.Starters.OrderBy(i => i).ToArray(), Is.EqualTo(before));
            Assert.That(t.Players[s.Starters[0]].Position, Is.EqualTo("POR"), "el portero sigue en el arco");
            Assert.That(s.IsValid(t), Is.True);
        }

        [Test]
        public void EffectiveRating_PenalisesPlayingOutOfPosition()
        {
            TeamDefinition t = Team();
            SquadSelection s = SquadSelection.Auto(t, Formations.F442);
            int keeperSlot = 0, strikerSlot = 9;
            int natural = s.EffectiveRating(t, strikerSlot);
            s.Swap(SquadGroup.Starter, keeperSlot, SquadGroup.Starter, strikerSlot);

            Assert.That(s.EffectiveRating(t, strikerSlot), Is.LessThan(t.Players[s.Starters[strikerSlot]].Rating), "un portero de delantero rinde menos");
            Assert.That(natural, Is.EqualTo(t.Players[s.Starters[keeperSlot]].Rating).Or.LessThanOrEqualTo(80));
        }

        [Test]
        public void Serialize_RoundTrips()
        {
            TeamDefinition t = Team();
            SquadSelection s = SquadSelection.Auto(t, Formations.F4231);
            s.Swap(SquadGroup.Starter, 3, SquadGroup.Bench, 2);

            SquadSelection copy = SquadSelection.Deserialize(t, s.Serialize(t));

            Assert.That(copy, Is.Not.Null);
            Assert.That(copy.Formation, Is.SameAs(Formations.F4231));
            Assert.That(copy.Starters, Is.EqualTo(s.Starters));
            Assert.That(copy.Bench, Is.EqualTo(s.Bench));
            Assert.That(copy.Reserves.OrderBy(i => i), Is.EqualTo(s.Reserves.OrderBy(i => i)));
        }

        [Test]
        public void Deserialize_RejectsASelectionSavedForADifferentRoster()
        {
            TeamDefinition t = Team();
            string saved = SquadSelection.Auto(t, Formations.F442).Serialize(t);
            TeamDefinition changed = Team();
            PlayerDefinition first = changed.Players[0];
            changed.Players[0] = changed.Players[1];
            changed.Players[1] = first; // mismos jugadores en otro orden: los índices guardados ya no valen

            Assert.That(SquadSelection.Deserialize(changed, saved), Is.Null);
            Assert.That(SquadSelection.Deserialize(t, saved), Is.Not.Null);
        }

        [Test]
        public void EffectiveRating_MatchesWhatTheEngineUses()
        {
            TeamDefinition t = Team();
            SquadSelection s = SquadSelection.Auto(t, Formations.F442);
            for (int slot = 0; slot < s.Starters.Count; slot++)
            {
                float engine = TeamBuilder.EffectiveRating(t.Players[s.Starters[slot]], Formations.F442.Slots[slot].Role);
                Assert.That(s.EffectiveRating(t, slot), Is.EqualTo((int)System.MathF.Round(engine)));
            }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("9-9-9|1,2|3")]
        [TestCase("4-4-2|1,2,3|4")]
        [TestCase("4-4-2|0,1,2,3,4,5,6,7,8,9,99|10")]
        [TestCase("4-4-2|0,0,2,3,4,5,6,7,8,9,10|11")]
        [TestCase("basura")]
        public void Deserialize_RejectsInvalidText(string text)
        {
            Assert.That(SquadSelection.Deserialize(Team(), text), Is.Null);
        }

        [Test]
        public void Build_UsesTheChosenStarters()
        {
            TeamDefinition t = Team();
            SquadSelection s = SquadSelection.Auto(t, Formations.F442);
            s.Swap(SquadGroup.Starter, 10, SquadGroup.Reserve, 0);
            string chosen = TeamBuilder.DisplayName(t.Players[s.Starters[10]]);

            MatchTeamSetup setup = TeamBuilder.Build(t, new MatchConfig(), new SeededRandom(1), s);

            Assert.That(setup.Players[10].Name, Is.EqualTo(chosen));
            Assert.That(setup.Formation, Is.SameAs(Formations.F442));
        }

        [Test]
        public void Build_FallsBackToAutoWhenTheSelectionIsInvalid()
        {
            TeamDefinition t = Team();
            SquadSelection s = SquadSelection.Auto(t, Formations.F442);
            s.Starters.RemoveAt(0);

            MatchTeamSetup setup = TeamBuilder.Build(t, new MatchConfig(), new SeededRandom(1), s);

            Assert.That(setup.Players, Has.Length.EqualTo(11));
        }

        [Test]
        public void EveryFormation_PlaysACompleteMatch()
        {
            var config = new MatchConfig();
            foreach (Formation f in Formations.All)
            {
                Assert.That(f.Slots, Has.Length.EqualTo(11), f.Name);
                Assert.That(f.Slots[0].Role, Is.EqualTo(Football.Core.Domain.Position.Goalkeeper), f.Name);
                TeamDefinition t = Team();
                var engine = new MatchEngine(config, new SeededRandom(5),
                    TeamBuilder.Build(t, config, new SeededRandom(1), SquadSelection.Auto(t, f)),
                    TeamBuilder.Build(t, config, new SeededRandom(2), SquadSelection.Auto(t, Formations.F442)));
                int guard = 0;
                while (!engine.IsFinished && guard++ < 100000)
                {
                    engine.Tick();
                }

                Assert.That(engine.IsFinished, Is.True, f.Name);
            }
        }
    }
}
