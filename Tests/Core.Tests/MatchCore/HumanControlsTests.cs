using System;
using Football.Core.Common;
using Football.Core.MatchCore;
using NUnit.Framework;
using Football.Core.Domain;

namespace Football.Core.Tests.MatchCore
{
    /// <summary>Controles del jugador humano: barrida, presión, despeje y control del arquero.</summary>
    public class HumanControlsTests
    {
        private const float Frozen = 1000f;

        private static MatchEngine Isolated(ulong seed = 1)
        {
            MatchEngine e = MatchEngineTests.NewEngine(seed);
            e.TestBeginOpenPlay();
            for (int i = 0; i < 22; i++)
            {
                MatchPlayer p = e.Players[i];
                e.TestPlace(p, new Vec2(-50f + 4f * (i % 11), i < 11 ? -30f : 30f));
                p.DecisionTimer = Frozen;
                p.OwnerTimer = Frozen;
            }

            return e;
        }

        /// <summary>Rival (visitante) con balón, de frente al defensor local que controla la persona, a cierta distancia.</summary>
        private static MatchEngine Duel(ulong seed, float gap, out MatchPlayer defender, out MatchPlayer owner)
        {
            MatchEngine e = Isolated(seed);
            owner = e.Players[15];
            defender = e.Players[4];
            e.TestPlace(owner, new Vec2(0f, 0f));
            e.TestPlace(defender, new Vec2(gap, 0f));
            defender.Heading = MathF.PI; // mira hacia el rival
            owner.Heading = 0f;          // y el rival viene de frente hacia él
            owner.ActionTimer = Frozen;
            e.TestGiveBall(owner);
            e.SetHuman(0, true);
            e.Tick(); // toma el control del jugador más cercano al balón
            return e;
        }

        [Test]
        public void Slide_ReachesFartherThanAStandingTackle()
        {
            int won = 0, fouls = 0;
            for (ulong seed = 0; seed < 80; seed++)
            {
                MatchEngine e = Duel(seed, 1.6f, out MatchPlayer defender, out MatchPlayer owner);
                Assert.That(e.Teams[0].Controlled, Is.SameAs(defender));
                Assert.That(1.6f, Is.GreaterThan(e.Config.Rules.TackleRange), "a esta distancia una entrada de pie no llega");

                e.SetInput(0, new MatchInput { Slide = true });
                for (int i = 0; i < 40; i++)
                {
                    e.Tick();
                }

                if (e.Events.CountOf(MatchEventType.Foul, 0) > 0)
                {
                    fouls++;
                }
                else if (e.Owner != owner)
                {
                    won++;
                }
            }

            Assert.That(won, Is.GreaterThan(10), "la barrida gana el balón a menudo");
            Assert.That(fouls, Is.GreaterThan(2), "y a veces es falta");
        }

        [Test]
        public void Slide_LocksSteeringAndThenThePlayerGetsUp()
        {
            MatchEngine e = Duel(3, 6f, out MatchPlayer defender, out _);
            e.SetInput(0, new MatchInput { Slide = true, Move = new Vec2(-1f, 0f) });
            e.Tick();
            Assert.That(defender.SlideTimer, Is.GreaterThan(0f));
            float heading = defender.Heading;

            e.SetInput(0, new MatchInput { Move = new Vec2(0f, 1f) }); // intenta girar durante la barrida
            for (int i = 0; i < 10; i++)
            {
                e.Tick();
            }

            Assert.That(defender.Heading, Is.EqualTo(heading).Within(1e-4f), "no gira mientras se desliza");

            int ticks = (int)((e.Config.Rules.SlideDuration + 0.05f) * e.Config.TickRate);
            for (int i = 0; i < ticks; i++)
            {
                e.Tick();
            }

            Assert.That(defender.RecoverTimer, Is.GreaterThan(0f), "queda en el suelo un momento");
            Assert.That(defender.Speed, Is.Zero);

            for (int i = 0; i < 60; i++)
            {
                e.SetInput(0, new MatchInput { Move = new Vec2(0f, 1f) });
                e.Tick();
            }

            Assert.That(defender.Speed, Is.GreaterThan(1f), "se levanta y vuelve a correr");
        }

        [Test]
        public void Press_ChasesTheCarrierAndTacklesWithoutSliding()
        {
            int attempts = 0;
            for (ulong seed = 0; seed < 20; seed++)
            {
                MatchEngine e = Duel(seed, 8f, out MatchPlayer defender, out _);
                float start = Vec2.Distance(defender.Pos, e.Players[15].Pos);
                for (int i = 0; i < 120; i++)
                {
                    e.SetInput(0, new MatchInput { Press = true });
                    e.Tick();
                }

                Assert.That(defender.SlideTimer, Is.LessThanOrEqualTo(0f), "presionar no es barrer");
                if (defender.TackleTimer > 0f)
                {
                    attempts++;
                }

                Assert.That(Vec2.Distance(defender.Pos, e.Players[15].Pos), Is.LessThan(start));
            }

            Assert.That(attempts, Is.GreaterThan(15), "al alcanzarlo entra de pie");
        }

        [Test]
        public void Press_FromBehind_FirstGetsGoalSideInsteadOfTacklingTheBack()
        {
            MatchEngine e = Duel(5, 3f, out MatchPlayer defender, out MatchPlayer owner);
            owner.Heading = MathF.PI; // el rival se va hacia la portería local (−X): el defensor queda detrás
            bool tackledFromBehind = false, gotGoalSide = false;
            for (int i = 0; i < 150 && !gotGoalSide; i++)
            {
                Vec2 toOwner = (owner.Pos - defender.Pos).Normalized;
                bool behind = Rules.IsFromBehind(e.Config.Rules, -Vec2.Dot(Vec2.FromAngle(owner.Heading), toOwner));
                e.SetInput(0, new MatchInput { Press = true });
                e.Tick();
                tackledFromBehind |= behind && defender.TackleTimer > 0f && e.Events.CountOf(MatchEventType.Foul, 0) == 0
                                     && defender.TackleTimer >= e.Config.Rules.TackleCooldown - 0.02f;
                // Lo rodea hasta quedar entre el rival y la portería, o le gana el balón entrando de costado.
                gotGoalSide = defender.Pos.X < owner.Pos.X - 1f || e.Owner == defender;
            }

            Assert.That(tackledFromBehind, Is.False, "presionar no entra por la espalda");
            Assert.That(gotGoalSide, Is.True, "rodea al rival (y le entra de costado o queda entre él y la portería)");
        }

        [Test]
        public void WithoutPressing_TheDefenderNeverTacklesOnItsOwn()
        {
            MatchEngine e = Duel(1, 1f, out MatchPlayer defender, out _);
            for (int i = 0; i < 120; i++)
            {
                e.SetInput(0, new MatchInput());
                e.Tick();
            }

            Assert.That(defender.TackleTimer, Is.LessThanOrEqualTo(0f));
        }

        [Test]
        public void Clear_KicksTheBallHighAndUpfield()
        {
            MatchEngine e = Isolated();
            MatchPlayer p = e.Players[2];
            e.TestPlace(p, new Vec2(-40f, 5f));
            e.TestGiveBall(p);
            e.SetHuman(0, true);
            e.SetInput(0, new MatchInput { Clear = true });
            e.Tick();

            Assert.That(e.Owner, Is.Null);
            Assert.That(e.Ball.Vel.X, Is.GreaterThan(5f), "hacia el campo rival");
            Assert.That(e.Ball.Vel.Z, Is.GreaterThan(5f), "por alto");
            Assert.That(e.Teams[0].Stats.Passes, Is.Zero, "un despeje no cuenta como pase");
        }

        [Test]
        public void KeeperButton_GivesControlOfTheKeeperUntilSwitch()
        {
            MatchEngine e = Isolated();
            e.TestPlaceBall(new Vec3(-20f, 0f, 0f), Vec3.Zero, e.Players[15]);
            e.TestPlace(e.Players[4], new Vec2(-19f, 2f));
            e.SetHuman(0, true);
            e.Tick();
            Assert.That(e.Teams[0].Controlled, Is.SameAs(e.Players[4]));

            e.SetInput(0, new MatchInput { Keeper = true });
            e.Tick();
            Assert.That(e.Teams[0].Controlled, Is.SameAs(e.Players[0]));

            MatchPlayer keeper = e.Players[0];
            Vec2 before = keeper.Pos;
            for (int i = 0; i < 90; i++)
            {
                e.SetInput(0, new MatchInput { Move = new Vec2(1f, 0f), Sprint = true });
                e.Tick();
            }

            Assert.That(e.Teams[0].Controlled, Is.SameAs(keeper), "sigue controlando al arquero aunque otro esté más cerca");
            Assert.That(keeper.Pos.X, Is.GreaterThan(before.X + 3f), "el arquero sale");

            e.SetInput(0, new MatchInput { Switch = true });
            e.Tick();
            Assert.That(e.Teams[0].Controlled, Is.Not.SameAs(keeper));
        }

        [Test]
        public void KeeperWithTheBall_CanClear()
        {
            MatchEngine e = Isolated();
            MatchPlayer keeper = e.Players[0];
            e.TestPlace(keeper, new Vec2(-50f, 0f));
            e.TestGiveBall(keeper);
            e.SetHuman(0, true);
            e.Tick();
            e.SetInput(0, new MatchInput { Clear = true });
            e.Tick();

            Assert.That(e.Owner, Is.Null);
            Assert.That(e.Ball.Vel.X, Is.GreaterThan(5f));
            Assert.That(e.Teams[0].KeeperLock, Is.False);
        }
    }
}
