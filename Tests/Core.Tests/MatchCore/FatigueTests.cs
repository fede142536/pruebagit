using Football.Core.Common;
using Football.Core.Domain;
using Football.Core.MatchCore;
using NUnit.Framework;

namespace Football.Core.Tests.MatchCore
{
    /// <summary>Fatiga dinámica: velocidad, aceleración, precisión, reacción y lesiones leves desde el minuto 70.</summary>
    public sealed class FatigueTests
    {
        private static MatchPlayer NewPlayer(PlayerConfig c)
        {
            var view = new PlayerMatchView { Id = 1, Name = "X", Number = 8, Role = Position.CentralMidfielder };
            foreach (AttributeId id in System.Enum.GetValues(typeof(AttributeId)))
            {
                view.Attributes[id] = 12;
            }

            return new MatchPlayer(1, 0, 1, view, c);
        }

        /// <summary>Distancia que recorre en 2 s arrancando parado, con la resistencia dada.</summary>
        private static float Sprint(float stamina, bool injured = false)
        {
            var c = new PlayerConfig();
            MatchPlayer p = NewPlayer(c);
            p.Stamina = stamina;
            p.Injured = injured;
            p.Target = new Vec2(100f, 0f);
            p.SpeedFactor = 1f;
            const float dt = 1f / 60f;
            for (int i = 0; i < 120; i++)
            {
                PlayerMotion.Step(p, dt, c);
                p.Stamina = stamina; // se mantiene fija para comparar
            }

            return p.Pos.X;
        }

        [Test]
        public void TiredPlayers_AccelerateAndRunSlower()
        {
            float fresh = Sprint(1f), tired = Sprint(0.3f), injured = Sprint(1f, true);
            Assert.That(tired, Is.LessThan(fresh * 0.95f), $"descansado {fresh:F1} m, cansado {tired:F1} m");
            Assert.That(injured, Is.LessThan(fresh * 0.85f), $"lesionado {injured:F1} m");
        }

        [Test]
        public void TiredPlayers_KickWorseAndReactLater()
        {
            MatchEngine e = MatchEngineTests.NewEngine(1);
            MatchPlayer p = e.Players[5];
            p.Stamina = 1f;
            Assert.That(e.TestFatigueError(p), Is.EqualTo(1f).Within(1e-6));
            Assert.That(e.TestFatigueReaction(p), Is.EqualTo(1f).Within(1e-6));
            p.Stamina = 0.2f;
            Assert.That(e.TestFatigueError(p), Is.GreaterThan(1.3f), "más error de pase y tiro");
            Assert.That(e.TestFatigueReaction(p), Is.GreaterThan(1.3f), "decide más tarde");
        }

        /// <summary>Lesiones de un partido manteniendo a todos con la resistencia dada desde el minuto 60.</summary>
        private static int Injuries(ulong seed, float stamina, out bool beforeStart)
        {
            var config = new MatchConfig();
            config.Player.InjuryBaseRate = 0.002f; // más alta que la real para medir con pocos partidos
            var rng = new SeededRandom(seed);
            IRandom teams = rng.Fork("teams");
            var e = new MatchEngine(config, rng.Fork("engine"),
                RosterFactory.CreateTeam(teams, "H", 65f, Formations.F442, config.Styles[0]),
                RosterFactory.CreateTeam(teams, "A", 65f, Formations.F442, config.Styles[0]));
            beforeStart = false;
            int guard = 0;
            while (!e.IsFinished && guard++ < 100000)
            {
                if (e.Minute >= 60f)
                {
                    foreach (MatchPlayer p in e.Players)
                    {
                        p.Stamina = stamina;
                    }
                }

                e.Tick();
                if (e.Minute < config.Player.InjuryStartMinute && e.Teams[0].Stats.Injuries + e.Teams[1].Stats.Injuries > 0)
                {
                    beforeStart = true;
                }
            }

            return e.Teams[0].Stats.Injuries + e.Teams[1].Stats.Injuries;
        }

        [Test]
        public void Injuries_OnlyFromMinute70_AndMuchLikelierWhenTired()
        {
            int fresh = 0, tired = 0;
            for (ulong seed = 0; seed < 12; seed++)
            {
                fresh += Injuries(seed, 1f, out bool early1);
                tired += Injuries(seed, 0.1f, out bool early2);
                Assert.That(early1 || early2, Is.False, "ninguna lesión antes del minuto 70");
            }

            Assert.That(tired, Is.GreaterThan(3 * System.Math.Max(1, fresh)), $"descansados {fresh}, cansados {tired}");
        }

        [Test]
        public void Ai_SubstitutesAnInjuredPlayerFirst()
        {
            MatchEngine e = SubstitutionTests.NewMatch(5);
            int guard = 0;
            while (e.Minute < 62f && guard++ < 100000)
            {
                e.Tick();
            }

            MatchPlayer hurt = e.Teams[1].Players[7];
            foreach (MatchPlayer p in e.Teams[1].Players)
            {
                p.Stamina = 0.2f; // todos cansados: aun así sale primero el lesionado
            }

            hurt.Stamina = 0.95f;
            e.TestInjure(hurt); // el mismo camino que una lesión del partido (el árbitro para el juego)
            while (!e.IsFinished && e.Teams[1].Departed.Count == 0 && guard++ < 100000)
            {
                e.Tick();
            }

            Assert.That(e.Teams[1].Departed.Count, Is.GreaterThan(0), "la IA hizo un cambio");
            Assert.That(e.Teams[1].Departed[0], Is.SameAs(hurt), "el primero en salir es el lesionado");
        }
    }
}
