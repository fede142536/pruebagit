using Football.Core.Common;
using Football.Core.MatchCore;
using NUnit.Framework;

namespace Football.Core.Tests.MatchCore
{
    /// <summary>Táctica en vivo: mentalidad, presión y adaptación de la IA al marcador.</summary>
    public class TacticsTests
    {
        [Test]
        public void Apply_ShiftsLineRiskDirectnessAndPress_WithinBounds()
        {
            var ai = new AiConfig();
            var baseStyle = new TeamStyle { LineHeight = 0.5f, Risk = 0.5f, Directness = 0.5f, PressIntensity = 0.5f, Width = 0.7f };
            var target = new TeamStyle();

            Tactics.Apply(baseStyle, Mentality.Attacking, PressLevel.High, ai, target);
            Assert.That(target.LineHeight, Is.EqualTo(0.5f + ai.MentalityLineStep).Within(1e-5f));
            Assert.That(target.Risk, Is.GreaterThan(baseStyle.Risk));
            Assert.That(target.Directness, Is.GreaterThan(baseStyle.Directness));
            Assert.That(target.PressIntensity, Is.EqualTo(0.5f + ai.PressLevelStep).Within(1e-5f));
            Assert.That(target.Width, Is.EqualTo(baseStyle.Width), "la amplitud no depende de la mentalidad");

            Tactics.Apply(baseStyle, Mentality.VeryDefensive, PressLevel.Low, ai, target);
            Assert.That(target.LineHeight, Is.LessThan(baseStyle.LineHeight));
            Assert.That(target.PressIntensity, Is.LessThan(baseStyle.PressIntensity));

            var extreme = new TeamStyle { LineHeight = 0.95f, PressIntensity = 0.95f };
            Tactics.Apply(extreme, Mentality.VeryAttacking, PressLevel.High, ai, target);
            Assert.That(target.LineHeight, Is.LessThanOrEqualTo(1f));
            Assert.That(target.PressIntensity, Is.LessThanOrEqualTo(1f));
        }

        [TestCase(0, 70f, Mentality.Balanced)]
        [TestCase(-1, 30f, Mentality.Balanced)]
        [TestCase(-1, 65f, Mentality.Attacking)]
        [TestCase(-2, 85f, Mentality.VeryAttacking)]
        [TestCase(1, 65f, Mentality.Balanced)]
        [TestCase(1, 85f, Mentality.Defensive)]
        public void Adaptive_ReactsToScoreAndMinute(int difference, float minute, Mentality expected)
        {
            Mentality m = Tactics.Adaptive(difference, minute, new AiConfig(), out PressLevel press);
            Assert.That(m, Is.EqualTo(expected));
            Assert.That(press, Is.EqualTo(difference < 0 && minute >= new AiConfig().AdaptStartMinute ? PressLevel.High : PressLevel.Normal));
        }

        [Test]
        public void SetTactics_ChangesOnlyThatTeam_AndNeverTheSharedConfiguration()
        {
            var config = new MatchConfig();
            float configLine = config.Styles[0].LineHeight;
            MatchEngine e = MatchEngineTests.NewEngine(1, cfg: config);

            e.SetTactics(0, Mentality.VeryAttacking, PressLevel.High);

            Assert.That(e.Teams[0].Style.LineHeight, Is.GreaterThan(configLine));
            Assert.That(e.Teams[0].BaseStyle.LineHeight, Is.EqualTo(configLine));
            Assert.That(e.Teams[1].Style.LineHeight, Is.EqualTo(configLine), "el rival usa el mismo estilo pero su propia copia");
            Assert.That(config.Styles[0].LineHeight, Is.EqualTo(configLine), "la configuración compartida no se toca");
            Assert.That(e.Events.CountOf(MatchEventType.TacticsChanged, 0), Is.EqualTo(1));

            e.SetTactics(0, Mentality.VeryAttacking, PressLevel.High);
            Assert.That(e.Events.CountOf(MatchEventType.TacticsChanged, 0), Is.EqualTo(1), "repetir la misma táctica no genera otro aviso");
        }

        [Test]
        public void HumanTeam_KeepsItsTactics_WhileTheAiAdapts()
        {
            MatchEngine e = MatchEngineTests.NewEngine(3);
            e.SetHuman(0, true);
            e.SetTactics(0, Mentality.Defensive, PressLevel.Low);
            for (int i = 0; i < 60 * 60 * 8 && !e.IsFinished; i++)
            {
                e.Tick();
            }

            Assert.That(e.Teams[0].Mentality, Is.EqualTo(Mentality.Defensive));
            Assert.That(e.Teams[0].Pressing, Is.EqualTo(PressLevel.Low));
        }

        /// <summary>
        /// Efecto medible en el campo: con mentalidad muy ofensiva y presión alta el equipo juega más adelantado y recupera el
        /// balón más arriba que con mentalidad muy defensiva y presión baja.
        /// </summary>
        [Test]
        public void Mentality_HasAMeasurableEffectOnTheBlockAndThePress()
        {
            Measure(Mentality.VeryAttacking, PressLevel.High, out float attackingDepth, out float attackingTackles);
            Measure(Mentality.VeryDefensive, PressLevel.Low, out float defensiveDepth, out float defensiveTackles);
            TestContext.Progress.WriteLine($"Bloque: ofensiva {attackingDepth:F3} / defensiva {defensiveDepth:F3}; " +
                                           $"entradas: {attackingTackles:F1} / {defensiveTackles:F1}");

            Assert.That(attackingDepth, Is.GreaterThan(defensiveDepth + 0.05f),
                $"profundidad media del bloque: ofensiva {attackingDepth:F3}, defensiva {defensiveDepth:F3}");
            Assert.That(attackingTackles, Is.GreaterThan(defensiveTackles),
                $"entradas por partido: ofensiva {attackingTackles:F1}, defensiva {defensiveTackles:F1}");
        }

        private static void Measure(Mentality mentality, PressLevel press, out float depth, out float tackles)
        {
            const int matches = 12;
            double sum = 0;
            long samples = 0;
            tackles = 0f;
            for (ulong seed = 0; seed < matches; seed++)
            {
                MatchEngine e = MatchEngineTests.NewEngine(100 + seed);
                e.Teams[0].AdaptiveTactics = false;
                e.Teams[1].AdaptiveTactics = false;
                e.SetTactics(0, mentality, press);
                int guard = 0;
                while (!e.IsFinished && guard++ < 100000)
                {
                    e.Tick();
                    if (e.TickCount % 30 != 0 || e.Phase != MatchPhase.Playing)
                    {
                        continue;
                    }

                    TeamState team = e.Teams[0];
                    for (int i = 1; i < team.Players.Length; i++)
                    {
                        sum += e.Pitch.Progress(team.AttackDirection, team.Players[i].Pos.X);
                        samples++;
                    }
                }

                tackles += e.Teams[0].Stats.Tackles;
            }

            depth = (float)(sum / samples);
            tackles /= matches;
        }
    }
}
