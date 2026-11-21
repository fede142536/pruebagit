using System;
using Football.Core.Common;
using Football.Core.MatchCore;
using NUnit.Framework;

namespace Football.Core.Tests.MatchCore
{
    /// <summary>Errores del motor encontrados en la revisión de código; cada prueba impide que vuelvan.</summary>
    public class EngineRegressionTests
    {
        private const float Frozen = 1000f;

        private static MatchEngine Isolated(ulong seed)
        {
            MatchEngine e = MatchEngineTests.NewEngine(seed);
            e.TestBeginOpenPlay();
            for (int i = 0; i < 22; i++)
            {
                MatchPlayer p = e.Players[i];
                e.TestPlace(p, new Vec2(-40f + 4f * (i % 11), i < 11 ? -30f : 30f));
                p.DecisionTimer = Frozen;
                p.OwnerTimer = Frozen;
            }

            return e;
        }

        [Test]
        public void KickOff_IsNeverTakenByASentOffPlayer()
        {
            MatchEngine e = MatchEngineTests.NewEngine(1);
            MatchPlayer last = e.Teams[1].Players[10];
            last.SentOff = true;
            e.TestKickoff(1);
            for (int i = 0; i < 600 && e.Phase != MatchPhase.Playing; i++)
            {
                e.Tick();
            }

            Assert.That(e.Phase, Is.EqualTo(MatchPhase.Playing), "el saque se hace");
            Assert.That(e.LastToucher, Is.Not.Null);
            Assert.That(e.LastToucher.SentOff, Is.False, "sacó un jugador en el campo");
        }

        [Test]
        public void KeeperWithTheBall_NeverCrossesHisOwnGoalLine()
        {
            for (ulong seed = 0; seed < 20; seed++)
            {
                MatchEngine e = Isolated(seed);
                MatchPlayer keeper = e.Teams[0].Players[0]; // defiende −X
                e.TestPlace(keeper, new Vec2(-e.Pitch.HalfLength + 0.6f, 0f));
                keeper.Heading = MathF.PI; // mirando hacia su propia portería
                e.TestGiveBall(keeper);
                keeper.Target = new Vec2(-e.Pitch.HalfLength - 3f, 0f); // intenta caminar hacia atrás
                keeper.SpeedFactor = 1f;
                for (int i = 0; i < 120; i++)
                {
                    e.Tick();
                    keeper.Target = new Vec2(-e.Pitch.HalfLength - 3f, 0f);
                }

                Assert.That(e.Teams[1].Stats.Goals, Is.Zero, "no hay gol en contra por llevar el balón tras la línea");
                Assert.That(keeper.Pos.X, Is.GreaterThan(-e.Pitch.HalfLength));
            }
        }

        [Test]
        public void KeeperHoldingTheBallInHisArea_CannotBeTackled()
        {
            int lost = 0;
            for (ulong seed = 0; seed < 40; seed++)
            {
                MatchEngine e = Isolated(seed);
                MatchPlayer keeper = e.Teams[0].Players[0];
                MatchPlayer striker = e.Teams[1].Players[10];
                e.TestPlace(keeper, new Vec2(-48f, 0f));
                e.TestPlace(striker, new Vec2(-47.2f, 0f));
                striker.Heading = MathF.PI;
                e.TestGiveBall(keeper);
                keeper.ActionTimer = Frozen;
                striker.WantsTackle = true;
                striker.TackleTimer = 0f;
                e.Tick();
                lost += e.Owner != keeper ? 1 : 0;
            }

            Assert.That(lost, Is.Zero);
        }

        [Test]
        public void ShotThatBeatsTheKeeper_IsCreditedToTheShooterNotTheKeeper()
        {
            int goals = 0, wrong = 0;
            for (ulong seed = 0; seed < 60; seed++)
            {
                MatchEngine e = Isolated(seed);
                MatchPlayer shooter = e.Teams[0].Players[10]; // ataca hacia +X
                MatchPlayer keeper = e.Teams[1].Players[0];
                e.TestPlace(shooter, new Vec2(40f, 0f));
                e.TestPlace(keeper, new Vec2(51.5f, 0.3f));
                e.TestKick(shooter, new Vec3(30f, 0f, 1.5f), true);
                for (int i = 0; i < 90 && e.Phase == MatchPhase.Playing; i++)
                {
                    e.Tick();
                }

                for (int k = 0; k < e.Events.Count; k++)
                {
                    MatchEvent ev = e.Events[k];
                    if (ev.Type == MatchEventType.Goal)
                    {
                        goals++;
                        wrong += ev.PlayerIndex != shooter.Index ? 1 : 0;
                    }
                }
            }

            Assert.That(goals, Is.GreaterThan(0), "con el arquero casi en la trayectoria, alguno entra");
            Assert.That(wrong, Is.Zero, "el gol es del que pateó");
        }

        [Test]
        public void BlockedShot_BouncesBackInsteadOfPassingThroughTheDefender()
        {
            MatchEngine e = Isolated(3);
            MatchPlayer shooter = e.Teams[0].Players[9];
            MatchPlayer defender = e.Teams[1].Players[4];
            e.TestPlace(shooter, new Vec2(20f, 0f));
            e.TestPlace(defender, new Vec2(26f, 0f));
            e.TestKick(shooter, new Vec3(30f, 0f, 0f), true);
            for (int i = 0; i < 20; i++)
            {
                e.Tick();
            }

            Assert.That(e.LastToucher, Is.SameAs(defender), "el defensor bloquea el tiro");
            Assert.That(e.Ball.Vel.X, Is.LessThan(0f), "y el balón rebota hacia atrás");
            Assert.That(e.Ball.Pos.X, Is.LessThan(defender.Pos.X + 0.6f));
        }

        [TestCase(0.2f, 2.3f)]
        [TestCase(0.6f, 1.2f)]
        [TestCase(2f, 1.2f)]
        [TestCase(5f, 0.5f)]
        [TestCase(8f, 1.5f)]
        public void ParryOverTheBar_WhenPossible_AlwaysEndsInACorner(float fromLine, float height)
        {
            MatchEngine e = Isolated(1);
            MatchPlayer keeper = e.Teams[1].Players[0]; // defiende +X
            e.TestPlace(keeper, new Vec2(e.Pitch.HalfLength - fromLine, 0f));
            e.TestPlaceBall(new Vec3(keeper.Pos.X, 0f, height), Vec3.Zero, e.Teams[0].Players[9]);
            Assert.That(e.TestParryOverTheBar(keeper), Is.True, "hay trayectoria posible");
            for (int i = 0; i < 240 && e.Phase == MatchPhase.Playing; i++)
            {
                e.Tick();
            }

            Assert.That(e.Teams[0].Stats.Goals, Is.Zero, "no entra");
            Assert.That(e.CurrentRestart.Kind, Is.EqualTo(RestartKind.Corner), "sale por encima del larguero: córner");
        }

        [Test]
        public void ParryOverTheBar_IsNotAttempted_WhenTheBallIsLowRightOnTheLine()
        {
            MatchEngine e = Isolated(1);
            MatchPlayer keeper = e.Teams[1].Players[0];
            e.TestPlace(keeper, new Vec2(e.Pitch.HalfLength - 0.1f, 0f));
            e.TestPlaceBall(new Vec3(keeper.Pos.X, 0f, 1.0f), Vec3.Zero, e.Teams[0].Players[9]);
            Assert.That(e.TestParryOverTheBar(keeper), Is.False, "imposible sin cruzar la línea por debajo del larguero");
        }

        [Test]
        public void ClearanceToAnOffsideStriker_IsOffside()
        {
            MatchEngine e = Isolated(2);
            MatchPlayer defender = e.Teams[0].Players[3];
            MatchPlayer striker = e.Teams[0].Players[10];
            e.TestPlace(defender, new Vec2(-10f, 0f));
            e.TestPlace(striker, new Vec2(30f, 0f)); // sin rivales por delante salvo el arquero: fuera de juego
            e.TestPlace(e.Teams[1].Players[0], new Vec2(50f, 0f));
            e.TestKick(defender, new Vec3(14f, 0f, 9f), false);
            for (int i = 0; i < 300 && e.Phase == MatchPhase.Playing; i++)
            {
                e.Tick();
                striker.Target = e.Ball.Pos.Horizontal;
                striker.SpeedFactor = 1f;
            }

            Assert.That(e.Events.CountOf(MatchEventType.Offside, 0), Is.EqualTo(1), "un despeje no es un salvoconducto");
        }

        [Test]
        public void PassPressedJustBeforeControllingTheBall_IsNotLost()
        {
            MatchEngine e = Isolated(4);
            MatchPlayer p = e.Teams[0].Players[6];
            e.TestPlace(p, new Vec2(0f, 0f));
            e.TestPlace(e.Teams[0].Players[7], new Vec2(12f, 4f));
            e.TestGiveBall(p);
            e.SetHuman(0, true);
            e.Tick();
            Assert.That(e.Teams[0].Controlled, Is.SameAs(p));
            p.ActionTimer = 0.1f; // aún controlando el balón
            e.SetInput(0, new MatchInput { Pass = true, Move = new Vec2(1f, 0.3f) });
            for (int i = 0; i < 12; i++)
            {
                e.Tick();
                e.SetInput(0, new MatchInput { Move = new Vec2(1f, 0.3f) });
            }

            Assert.That(e.Teams[0].Stats.Passes, Is.EqualTo(1), "el pase pulsado 0,1 s antes se ejecuta");
            for (int i = 0; i < 30; i++)
            {
                e.Tick();
            }

            Assert.That(e.Teams[0].Stats.Passes, Is.EqualTo(1), "y no se repite");
        }

        [Test]
        public void StalePassPress_ExpiresAndDoesNotFireLater()
        {
            MatchEngine e = Isolated(4);
            MatchPlayer p = e.Teams[0].Players[6];
            e.TestPlace(p, new Vec2(0f, 0f));
            e.TestPlace(e.Teams[0].Players[7], new Vec2(12f, 4f));
            e.TestGiveBall(p);
            e.SetHuman(0, true);
            e.Tick();
            p.ActionTimer = 0.5f; // mucho más que el búfer
            e.SetInput(0, new MatchInput { Pass = true });
            for (int i = 0; i < 60; i++)
            {
                e.Tick();
                e.SetInput(0, new MatchInput());
            }

            Assert.That(e.Teams[0].Stats.Passes, Is.Zero);
        }

        [Test]
        public void Statistically_GoalsAreAlmostNeverCreditedToTheConcedingTeam()
        {
            int goals = 0, conceding = 0;
            for (ulong seed = 0; seed < 30; seed++)
            {
                MatchEngine e = MatchEngineTests.NewEngine(500 + seed);
                while (!e.IsFinished)
                {
                    e.Tick();
                }

                for (int k = 0; k < e.Events.Count; k++)
                {
                    MatchEvent ev = e.Events[k];
                    if (ev.Type == MatchEventType.Goal && ev.PlayerIndex >= 0)
                    {
                        goals++;
                        conceding += e.Players[ev.PlayerIndex].Team != ev.Team ? 1 : 0;
                    }
                }
            }

            Assert.That(goals, Is.GreaterThan(30));
            Assert.That(conceding, Is.LessThan(goals / 10), $"goles en contra {conceding} de {goals}");
        }
    }
}
