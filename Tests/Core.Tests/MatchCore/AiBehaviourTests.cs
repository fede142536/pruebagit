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
    }
}
