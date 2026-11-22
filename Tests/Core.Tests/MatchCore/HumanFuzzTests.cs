using System;
using Football.Core.Common;
using Football.Core.MatchCore;
using NUnit.Framework;
using Football.Core.Domain;

namespace Football.Core.Tests.MatchCore
{
    /// <summary>
    /// Partidos completos con una persona que pulsa botones al azar: el motor no debe lanzar excepciones ni quedarse atascado
    /// (los partidos IA contra IA de la simulación masiva no recorren el código del control humano).
    /// </summary>
    public class HumanFuzzTests
    {
        [Test]
        public void RandomHumanInput_NeverThrowsNorGetsStuck()
        {
            for (ulong seed = 0; seed < FuzzMatches; seed++)
            {
                MatchEngine e = MatchEngineTests.NewEngine(3000 + seed, (int)(seed % 5), (int)((seed + 1) % 5));
                e.SetHuman(0, true);
                var buttons = new SeededRandom(seed);
                var move = new Vec2(1f, 0f);
                int samePhaseTicks = 0;
                MatchPhase lastPhase = e.Phase;
                long guard = 0;
                while (!e.IsFinished)
                {
                    if (buttons.NextFloat() < 0.05f)
                    {
                        move = Vec2.FromAngle(buttons.NextFloat() * 6.2831f) * buttons.NextFloat();
                    }

                    var input = new MatchInput
                    {
                        Move = move,
                        Sprint = buttons.NextFloat() < 0.3f,
                        Press = buttons.NextFloat() < 0.3f,
                        Pass = buttons.NextFloat() < 0.02f,
                        Through = buttons.NextFloat() < 0.01f,
                        Cross = buttons.NextFloat() < 0.01f,
                        Shoot = buttons.NextFloat() < 0.01f,
                        ShootPower = buttons.NextFloat(),
                        Clear = buttons.NextFloat() < 0.01f,
                        Slide = buttons.NextFloat() < 0.01f,
                        Tackle = buttons.NextFloat() < 0.01f,
                        Switch = buttons.NextFloat() < 0.01f,
                        Keeper = buttons.NextFloat() < 0.005f
                    };
                    e.SetInput(0, input);
                    try
                    {
                        e.Tick();
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail($"semilla {seed}, minuto {e.Minute:F1}, fase {e.Phase}: {ex}");
                    }

                    samePhaseTicks = e.Phase == lastPhase && e.Phase != MatchPhase.Playing ? samePhaseTicks + 1 : 0;
                    lastPhase = e.Phase;
                    Assert.That(samePhaseTicks, Is.LessThan(e.Config.TickRate * MaxStoppedSeconds),
                        $"semilla {seed}: el juego quedó detenido en {e.Phase} en el minuto {e.Minute:F1} (saque {e.CurrentRestart.Kind})");
                    Assert.That(++guard, Is.LessThan(200000L));
                }
            }
        }

        private const int FuzzMatches = 60;
        private const float MaxStoppedSeconds = 15f;
    }
}
