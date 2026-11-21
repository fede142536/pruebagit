using System;
using Football.Core.Common;
using Football.Core.MatchCore;
using NUnit.Framework;

namespace Football.Core.Tests.MatchCore
{
    /// <summary>Comportamientos de la IA: arquero que achica, desmarques al espacio y cambio automático de jugador.</summary>
    public class AiBehaviourTests
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
        public void KeeperRushesOut_WhenAStrikerIsThroughOnGoal()
        {
            MatchEngine e = Isolated(1);
            MatchPlayer striker = e.Teams[0].Players[10]; // ataca hacia +X
            MatchPlayer keeper = e.Teams[1].Players[0];
            e.TestPlace(striker, new Vec2(41f, 2f));
            e.TestPlace(keeper, new Vec2(51.5f, 0f));
            striker.ActionTimer = Frozen;
            e.TestGiveBall(striker);
            keeper.DecisionTimer = 0f;
            for (int i = 0; i < 90; i++)
            {
                e.Tick();
            }

            Assert.That(keeper.Pos.X, Is.LessThan(48.5f), "el arquero sale de la línea hacia el delantero");
        }

        [Test]
        public void KeeperStays_WhenADefenderIsBetweenTheStrikerAndGoal()
        {
            MatchEngine e = Isolated(1);
            MatchPlayer striker = e.Teams[0].Players[10];
            MatchPlayer keeper = e.Teams[1].Players[0];
            MatchPlayer defender = e.Teams[1].Players[3];
            e.TestPlace(striker, new Vec2(41f, 2f));
            e.TestPlace(defender, new Vec2(45f, 1.5f));
            defender.DecisionTimer = Frozen;
            e.TestPlace(keeper, new Vec2(51.5f, 0f));
            striker.ActionTimer = Frozen;
            e.TestGiveBall(striker);
            keeper.DecisionTimer = 0f;
            for (int i = 0; i < 40; i++)
            {
                e.Tick();
                defender.Target = defender.Pos;
            }

            Assert.That(keeper.Pos.X, Is.GreaterThan(48.5f), "con un defensor cubriendo, el arquero no se lanza");
        }

        [Test]
        public void LooseBall_PassesControlToTheTeammateWhoGetsThereFirst()
        {
            MatchEngine e = Isolated(2);
            e.SetHuman(0, true);
            MatchPlayer far = e.Teams[0].Players[6];
            MatchPlayer near = e.Teams[0].Players[7];
            e.TestPlace(far, new Vec2(-20f, 0f));
            e.TestPlace(near, new Vec2(5f, 10f));
            e.TestGiveBall(far);
            e.Tick();
            Assert.That(e.Teams[0].Controlled, Is.SameAs(far));

            // Balón largo y suelto hacia donde está el otro compañero.
            e.TestPlaceBall(new Vec3(0f, 6f, 0f), new Vec3(4f, 3f, 0f), e.Teams[1].Players[5]);
            for (int i = 0; i < 90; i++)
            {
                e.Tick();
            }

            Assert.That(e.Teams[0].Controlled, Is.SameAs(near));
        }

        [Test]
        public void PreviewSwitchTarget_IsWhoTheSwitchButtonSelects()
        {
            MatchEngine e = Isolated(3);
            e.SetHuman(0, true);
            MatchPlayer carrier = e.Teams[1].Players[8];
            e.TestPlace(carrier, new Vec2(0f, 0f));
            carrier.ActionTimer = Frozen;
            e.TestGiveBall(carrier);
            e.TestPlace(e.Teams[0].Players[5], new Vec2(-3f, 0f));
            e.TestPlace(e.Teams[0].Players[6], new Vec2(-8f, 2f));
            e.Tick();
            MatchPlayer preview = e.PreviewSwitchTarget(0);
            Assert.That(preview, Is.Not.Null);
            Assert.That(preview, Is.Not.SameAs(e.Teams[0].Controlled));

            e.SetInput(0, new MatchInput { Switch = true });
            e.Tick();
            Assert.That(e.Teams[0].Controlled, Is.SameAs(preview));
        }

        [Test]
        public void Forward_RunsIntoTheFreeSpace()
        {
            MatchEngine e = Isolated(4);
            MatchPlayer forward = e.Teams[0].Players[10]; // ataca hacia +X; +Y es su izquierda
            forward.Anchor = new Vec2(20f, 0f);
            for (int i = 1; i < 11; i++)
            {
                e.TestPlace(e.Teams[1].Players[i], new Vec2(29f + i * 0.3f, -9f)); // rivales cerrando el lado derecho
            }

            Assert.That(e.TestRunIntoSpace(forward).Y, Is.GreaterThan(0f), "se abre hacia el lado libre");

            for (int i = 1; i < 11; i++)
            {
                e.TestPlace(e.Teams[1].Players[i], new Vec2(29f + i * 0.3f, 9f)); // ahora cierran el izquierdo
            }

            Assert.That(e.TestRunIntoSpace(forward).Y, Is.LessThan(0f));
        }

        [Test]
        public void ShoulderToShoulder_TheWeakerPlayerStumbles_MostOfTheTime()
        {
            int weakerStumbled = 0, strongerStumbled = 0;
            for (ulong seed = 0; seed < 100; seed++)
            {
                MatchEngine e = Isolated(seed);
                MatchPlayer strong = e.Teams[0].Players[9];
                MatchPlayer weak = e.Teams[1].Players[9];
                strong.Attr[Football.Core.Domain.AttributeId.Strength] = 95;
                weak.Attr[Football.Core.Domain.AttributeId.Strength] = 25;
                e.TestPlace(strong, new Vec2(0f, -0.6f));
                e.TestPlace(weak, new Vec2(0f, 0.6f));
                strong.Heading = MathF.PI / 2f; // corren uno contra el otro
                weak.Heading = -MathF.PI / 2f;
                strong.Speed = 5f;
                weak.Speed = 5f;
                strong.Target = new Vec2(0f, 10f);
                weak.Target = new Vec2(0f, -10f);
                strong.SpeedFactor = weak.SpeedFactor = 1f;
                for (int t = 0; t < 30 && weak.StumbleTimer <= 0f && strong.StumbleTimer <= 0f; t++)
                {
                    e.Tick();
                    strong.Target = new Vec2(0f, 10f);
                    weak.Target = new Vec2(0f, -10f);
                }

                weakerStumbled += weak.StumbleTimer > 0f ? 1 : 0;
                strongerStumbled += strong.StumbleTimer > 0f ? 1 : 0;
            }

            Assert.That(weakerStumbled + strongerStumbled, Is.EqualTo(100), "en cada choque fuerte alguien trastabilla");
            Assert.That(weakerStumbled, Is.GreaterThan(80), $"el débil cae {weakerStumbled} veces, el fuerte {strongerStumbled}");
        }

        [Test]
        public void Scorer_RunsToTheCornerFlagToCelebrate()
        {
            MatchEngine e = Isolated(5);
            MatchPlayer shooter = e.Teams[0].Players[10];
            e.TestPlace(shooter, new Vec2(40f, 1f));
            e.TestPlace(e.Teams[1].Players[0], new Vec2(30f, 30f));
            e.TestKick(shooter, new Vec3(30f, -2f, 0.5f), true);
            for (int i = 0; i < 120 && e.Phase == MatchPhase.Playing; i++)
            {
                e.Tick();
            }

            Assert.That(e.Phase, Is.EqualTo(MatchPhase.GoalPause), $"balón en {e.Ball.Pos.X:F1},{e.Ball.Pos.Y:F1}");
            Assert.That(e.Celebrator, Is.SameAs(shooter));
            float before = Vec2.Distance(shooter.Pos, new Vec2(e.Pitch.HalfLength, e.Pitch.HalfWidth));
            for (int i = 0; i < 60; i++)
            {
                e.Tick();
            }

            Assert.That(Vec2.Distance(shooter.Pos, new Vec2(e.Pitch.HalfLength, e.Pitch.HalfWidth)), Is.LessThan(before - 2f), "corre al banderín");
            Assert.That(shooter.Action, Is.EqualTo(PlayerAction.Celebrate));
        }
    }
}
