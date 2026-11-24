using System;
using Football.Core.Common;
using Football.Core.MatchCore;
using NUnit.Framework;
using Football.Core.Domain;

namespace Football.Core.Tests.MatchCore
{
    /// <summary>Duelos uno contra uno: posición, orientación y recortes deciden las entradas.</summary>
    public class DuelTests
    {
        private const float Frozen = 1000f;

        [Test]
        public void TackleChance_IsHigherFacingTheCarrierFromTheFront()
        {
            var c = new RulesConfig();
            float front = Rules.TackleWinChance(c, 0.6f, 0.6f, 1f, 1f, 0f);
            float side = Rules.TackleWinChance(c, 0.6f, 0.6f, 1f, 0f, 0f);
            float behind = Rules.TackleWinChance(c, 0.6f, 0.6f, 1f, -1f, 0f);
            float turnedAway = Rules.TackleWinChance(c, 0.6f, 0.6f, -1f, 1f, 0f);
            Assert.That(front, Is.GreaterThan(side));
            Assert.That(side, Is.GreaterThan(behind));
            Assert.That(front, Is.GreaterThan(turnedAway));
            Assert.That(front - behind, Is.EqualTo(2f * c.TackleFrontWeight).Within(1e-5f));
        }

        [Test]
        public void TackleChance_DropsRightAfterACut_MoreForGoodDribblers()
        {
            var c = new RulesConfig();
            float none = Rules.TackleWinChance(c, 0.6f, 0.6f, 1f, 1f, 0f);
            float fresh = Rules.TackleWinChance(c, 0.6f, 0.6f, 1f, 1f, 1f);
            float fading = Rules.TackleWinChance(c, 0.6f, 0.6f, 1f, 1f, 0.5f);
            Assert.That(fresh, Is.LessThan(fading));
            Assert.That(fading, Is.LessThan(none));

            float weakDribbler = none - Rules.TackleWinChance(c, 0.6f, 0.2f, 1f, 1f, 1f) + c.TackleSkillWeight * 0.4f;
            float strongDribbler = none - fresh;
            Assert.That(strongDribbler, Is.GreaterThan(weakDribbler), "el recorte de un buen regateador deja más atrás");
        }

        [Test]
        public void TackleChance_StaysWithinBounds()
        {
            var c = new RulesConfig();
            Assert.That(Rules.TackleWinChance(c, 1f, 0f, 1f, 1f, 0f), Is.LessThanOrEqualTo(0.85f));
            Assert.That(Rules.TackleWinChance(c, 0f, 1f, -1f, -1f, 1f), Is.GreaterThanOrEqualTo(0.05f));
        }

        [Test]
        public void BehindThreshold_SeparatesFrontFromBehind()
        {
            var c = new RulesConfig();
            Assert.That(Rules.IsFromBehind(c, -0.9f), Is.True);
            Assert.That(Rules.IsFromBehind(c, 0f), Is.False);
            Assert.That(Rules.IsFromBehind(c, 0.9f), Is.False);
        }

        private static MatchEngine Isolated(ulong seed)
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

        [Test]
        public void SharpTurnAtSpeed_CountsAsACut_GentleTurnDoesNot()
        {
            MatchEngine e = Isolated(1);
            MatchPlayer owner = e.Players[9];
            e.TestPlace(owner, Vec2.Zero);
            owner.ActionTimer = Frozen;
            e.TestGiveBall(owner);
            owner.Heading = 0f;
            owner.Speed = 6f;
            owner.Target = new Vec2(30f, 0f);
            owner.SpeedFactor = 1f;
            for (int i = 0; i < 30; i++)
            {
                e.Tick();
            }

            Assert.That(owner.CutTimer, Is.EqualTo(0f), "correr recto no es recortar");

            owner.Target = owner.Pos + new Vec2(0f, 30f); // giro de 90 grados
            float maxCut = 0f;
            for (int i = 0; i < 40; i++)
            {
                e.Tick();
                maxCut = MathF.Max(maxCut, owner.CutTimer);
            }

            Assert.That(maxCut, Is.GreaterThan(0f), "un giro brusco a velocidad es un recorte");
        }

        [Test]
        public void ACutRightBeforeTheTackle_LetsTheCarrierEscapeMoreOften()
        {
            int keptCalm = 0, keptAfterCut = 0;
            for (ulong seed = 0; seed < 300; seed++)
            {
                keptCalm += Tackle(seed, false) ? 1 : 0;
                keptAfterCut += Tackle(seed, true) ? 1 : 0;
            }

            Assert.That(keptAfterCut, Is.GreaterThan(keptCalm + 15), $"sin recorte conserva {keptCalm}, con recorte {keptAfterCut}");
        }

        /// <summary>Un defensor de frente entra al poseedor; devuelve si el poseedor conservó el balón sin falta.</summary>
        private static bool Tackle(ulong seed, bool cut)
        {
            MatchEngine e = Isolated(seed);
            MatchPlayer owner = e.Players[9];
            MatchPlayer defender = e.Players[13];
            e.TestPlace(owner, Vec2.Zero);
            e.TestPlace(defender, new Vec2(1f, 0f));
            owner.Heading = 0f;
            defender.Heading = MathF.PI;
            owner.ActionTimer = Frozen;
            e.TestGiveBall(owner);
            e.Tick(); // registra al poseedor para medir sus giros
            owner.CutTimer = cut ? e.Config.Rules.CutWindow : 0f;
            defender.WantsTackle = true;
            defender.TackleTimer = 0f;
            e.Tick();
            return e.Owner == owner && e.Events.CountOf(MatchEventType.Foul, 1) == 0;
        }

        [Test]
        public void TacklingFromBehind_IsAFoulMoreOften()
        {
            int frontFouls = 0, behindFouls = 0;
            for (ulong seed = 0; seed < 400; seed++)
            {
                frontFouls += FoulFrom(seed, true) ? 1 : 0;
                behindFouls += FoulFrom(seed, false) ? 1 : 0;
            }

            Assert.That(behindFouls, Is.GreaterThan(frontFouls), $"de frente {frontFouls} faltas, por detrás {behindFouls}");
        }

        private static bool FoulFrom(ulong seed, bool front)
        {
            MatchEngine e = Isolated(seed);
            MatchPlayer owner = e.Players[9];
            MatchPlayer defender = e.Players[13];
            e.TestPlace(owner, Vec2.Zero);
            e.TestPlace(defender, new Vec2(front ? 1f : -1f, 0f));
            owner.Heading = 0f;
            defender.Heading = front ? MathF.PI : 0f;
            owner.ActionTimer = Frozen;
            e.TestGiveBall(owner);
            defender.WantsTackle = true;
            defender.TackleTimer = 0f;
            e.Tick();
            return e.Events.CountOf(MatchEventType.Foul, 1) > 0;
        }

        /// <summary>Un defensor que llega por detrás no se lanza: primero se pone entre el poseedor y su portería.</summary>
        [Test]
        public void AiPresser_GetsBetweenTheCarrierAndItsGoal()
        {
            int goalSide = 0;
            for (ulong seed = 0; seed < 20; seed++)
            {
                MatchEngine e = MatchEngineTests.NewEngine(seed);
                e.TestBeginOpenPlay();
                MatchPlayer owner = e.Players[9];
                MatchPlayer defender = e.Players[16];
                e.TestPlace(owner, new Vec2(10f, 5f));
                e.TestPlace(defender, new Vec2(4f, 5f)); // por detrás del poseedor, que ataca hacia +X
                owner.ActionTimer = Frozen;
                owner.OwnerTimer = Frozen;
                e.TestGiveBall(owner);
                bool reached = false;
                for (int i = 0; i < 150 && e.Owner == owner && !reached; i++)
                {
                    e.Tick();
                    reached = defender.Pos.X > owner.Pos.X + 1f && MathF.Abs(defender.Pos.Y - owner.Pos.Y) < 1.5f;
                }

                goalSide += reached ? 1 : 0;
            }

            Assert.That(goalSide, Is.GreaterThan(12), "el que presiona se coloca entre el poseedor y su portería");
        }

        /// <summary>
        /// Sensación de juego medible: una persona que mantiene PRESIONAR sobre un rival que conduce (con su IA activa)
        /// le quita el balón la mayoría de las veces en 8 s y sin hacer falta casi nunca (pedido del usuario: «es casi
        /// imposible sacársela»). Antes del ajuste: 48 % de robos y 28 % de faltas.
        /// </summary>
        [Test]
        public void HumanPressing_WinsTheBallMostOfTheTime_WithFewFouls()
        {
            int won = 0, fouls = 0;
            const int n = 120;
            for (ulong seed = 0; seed < n; seed++)
            {
                MatchEngine e = MatchEngineTests.NewEngine(5000 + seed);
                e.TestBeginOpenPlay();
                for (int i = 0; i < 22; i++)
                {
                    e.TestPlace(e.Players[i], new Vec2(i < 11 ? -30f : 30f, -30f + 6f * (i % 11)));
                }

                MatchPlayer carrier = e.Teams[1].Players[6];
                e.TestPlace(carrier, Vec2.Zero);
                e.TestPlace(e.Teams[0].Players[6], new Vec2(-6f, 2f));
                e.TestGiveBall(carrier);
                e.SetHuman(0, true);
                for (int t = 0; t < 480; t++)
                {
                    MatchPlayer c = e.Teams[0].Controlled;
                    Vec2 to = e.Owner != null && c != null ? e.Owner.Pos - c.Pos : Vec2.Zero;
                    e.SetInput(0, new MatchInput { Press = true, Move = to.Normalized, Sprint = to.Length > 3f });
                    e.Tick();
                    if (e.Events.CountOf(MatchEventType.Foul, 0) > 0)
                    {
                        fouls++;
                        break;
                    }

                    if (e.Owner != null && e.Owner.Team == 0)
                    {
                        won++;
                        break;
                    }

                    if (e.Phase != MatchPhase.Playing)
                    {
                        break;
                    }
                }
            }

            Assert.That(won, Is.GreaterThan(n * 50 / 100), $"robos {won} de {n}");
            Assert.That(fouls, Is.LessThan(n * 20 / 100), $"faltas {fouls} de {n}");
        }
    }
}
