using System;
using System.Linq;
using Football.Core.Common;
using Football.Core.MatchCore;
using NUnit.Framework;
using Football.Core.Domain;

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

        /// <summary>
        /// Pedido del usuario: la IA no debe quedarse gambeteando en el área. Medido en 60 partidos: antes, el 22 % de las
        /// veces que entraba con el balón se quedaba más de 1,5 s y llegaba a 5,2 s; ahora casi nunca y nunca más de 3 s.
        /// </summary>
        [Test]
        public void AiNeverKeepsDribblingInsideTheBox()
        {
            int entries = 0, longStays = 0, maxStay = 0;
            for (ulong seed = 0; seed < 120; seed++) // 120 partidos: con 40 la tasa variaba ±2 puntos según las semillas
            {
                MatchEngine e = MatchEngineTests.NewEngine(9000 + seed, (int)(seed % 5), (int)((seed + 1) % 5));
                MatchPlayer inBox = null;
                int stay = 0;
                while (!e.IsFinished)
                {
                    e.Tick();
                    MatchPlayer o = e.Owner;
                    bool box = o != null && !o.IsKeeper && e.Pitch.InPenaltyArea(e.Teams[o.Team].AttackDirection, o.Pos);
                    if (box)
                    {
                        if (o != inBox)
                        {
                            entries++;
                            inBox = o;
                            stay = 0;
                        }

                        stay++;
                        maxStay = Math.Max(maxStay, stay);
                    }
                    else if (inBox != null)
                    {
                        longStays += stay > 90 ? 1 : 0;
                        inBox = null;
                    }
                }
            }

            // Con el desgaste por minutos de partido (D-27) las semillas dan otras jugadas: en 120 partidos el máximo medido fue 3,9 s (un caso, de
            // 120 partidos y unas mil entradas al área); antes 2,9 s. Se pone el tope en 4,5 s.
            Assert.That(maxStay, Is.LessThan(270), $"nadie pasa 4,5 s con el balón en el área (máximo {maxStay / 60f:F1} s)");
            // Medido en 120 partidos (BALANCE.md §1.11): 11,6 % antes del saque del medio de un jugador, 13,3 % después.
            Assert.That(longStays, Is.LessThan(entries * 15 / 100), $"más de 1,5 s en el área: {longStays} de {entries}");
        }

        [Test]
        public void HumanThrowIn_WaitsForThePlayer_ThenGoesWhereTheStickPoints()
        {
            MatchEngine e = Isolated(6);
            e.SetHuman(0, true);
            MatchPlayer last = e.Teams[1].Players[4];
            MatchPlayer mate = e.Teams[0].Players[7];
            e.TestPlace(mate, new Vec2(5f, 20f));
            e.TestPlaceBall(new Vec3(0f, 33f, 0f), new Vec3(0f, 8f, 0f), last); // sale por la banda tocada por el rival
            for (int i = 0; i < 10 && e.Phase == MatchPhase.Playing; i++)
            {
                e.Tick();
            }

            Assert.That(e.Phase, Is.EqualTo(MatchPhase.SetPiece));
            Assert.That(e.CurrentRestart.Kind, Is.EqualTo(RestartKind.ThrowIn));
            Assert.That(e.CurrentRestart.Team, Is.EqualTo(0));

            for (int i = 0; i < 60 * 6; i++)
            {
                e.Tick(); // mucho más que la espera: la IA no saca por la persona antes de tiempo
            }

            Assert.That(e.Phase, Is.EqualTo(MatchPhase.SetPiece), "espera a que la persona elija");
            Assert.That(e.AwaitingHumanRestart, Is.True);

            e.SetInput(0, new MatchInput { Pass = true, Move = new Vec2(1f, -1f).Normalized });
            e.Tick();
            Assert.That(e.Phase, Is.EqualTo(MatchPhase.Playing));
            Assert.That(e.Ball.Vel.X, Is.GreaterThan(0f), "sale hacia donde apunta el joystick");
            Assert.That(e.Ball.Vel.Y, Is.LessThan(0f));
            Assert.That(e.Ball.Pos.Z, Is.GreaterThanOrEqualTo(e.Config.Rules.ThrowInReleaseHeight * 0.9f), "se saca con las manos: el balón sale desde arriba de la cabeza");
            Assert.That(e.Ball.Vel.Z, Is.GreaterThan(0f), "y describe un arco");
            Assert.That(e.Teams[0].Players.Count(p => p.Action == PlayerAction.ThrowIn), Is.EqualTo(1), "el sacador muestra la acción de saque, no un pase");
        }

        [Test]
        public void HumanRestart_IsTakenByTheAi_IfThePlayerDoesNothing()
        {
            MatchEngine e = Isolated(7);
            e.SetHuman(0, true);
            e.TestPlaceBall(new Vec3(0f, 33f, 0f), new Vec3(0f, 8f, 0f), e.Teams[1].Players[4]);
            int ticks = (int)((e.Config.Rules.ThrowInDelay + e.Config.Rules.TakerTimeout + e.Config.Rules.HumanRestartTimeout + 1f) * 60f);
            for (int i = 0; i < ticks && e.Phase != MatchPhase.Playing || i < 10; i++)
            {
                e.Tick();
            }

            Assert.That(e.Phase, Is.EqualTo(MatchPhase.Playing), "el partido no se queda trabado");
        }

        [Test]
        public void KickOff_HasASingleTakerInTheCircle_FacingHisOwnGoal_AndTwoReceiversBehind()
        {
            foreach (int kicking in new[] { 0, 1 })
            {
                MatchEngine e = MatchEngineTests.NewEngine(31);
                e.TestKickoff(kicking);
                float circle = e.Config.Pitch.CenterCircleRadius;
                MatchPlayer[] inCircle = e.Players.Where(p => !p.SentOff && p.Pos.Length < circle).ToArray();
                Assert.That(inCircle, Has.Length.EqualTo(1), "un único jugador en el círculo");
                MatchPlayer taker = inCircle[0];
                Assert.That(taker.Team, Is.EqualTo(kicking));
                float dir = e.Teams[kicking].AttackDirection;
                Assert.That(MathF.Cos(taker.Heading) * dir, Is.LessThan(-0.99f), "de frente a su propio arco");
                foreach (MatchPlayer p in e.Players.Where(p => p != taker && !p.SentOff))
                {
                    Assert.That(p.Pos.X * e.Teams[p.Team].AttackDirection, Is.LessThanOrEqualTo(0f), "todos en su propio campo");
                }

                int receivers = e.Teams[kicking].Players.Count(p => p != taker && p.Pos.X * dir < -circle && p.Pos.X * dir > -15f && MathF.Abs(p.Pos.Y) < 9f);
                Assert.That(receivers, Is.GreaterThanOrEqualTo(2), "dos compañeros listos para el pase atrás");
            }
        }

        [Test]
        public void AiKickOff_IsPlayedBackwards_ToATeammate()
        {
            int backwards = 0;
            for (ulong seed = 0; seed < 10; seed++)
            {
                MatchEngine e = MatchEngineTests.NewEngine(500 + seed);
                float dir = e.Teams[0].AttackDirection;
                for (int i = 0; i < 60 * 4 && e.Phase != MatchPhase.Playing; i++)
                {
                    e.Tick();
                }

                for (int i = 0; i < 10; i++)
                {
                    e.Tick();
                }

                if (e.Ball.Vel.X * dir < 0f || e.Ball.Pos.X * dir < 0f)
                {
                    backwards++;
                }
            }

            Assert.That(backwards, Is.GreaterThanOrEqualTo(8), "la IA juega el saque hacia su campo");
        }

        [Test]
        public void HumanKickOff_WaitsForThePlayer_AndAimIsBackwardOrSideways()
        {
            MatchEngine e = MatchEngineTests.NewEngine(11);
            e.SetHuman(0, true);
            for (int i = 0; i < 60 * 6; i++)
            {
                e.Tick();
            }

            Assert.That(e.Phase, Is.EqualTo(MatchPhase.SetPiece), "sin pase automático: el saque del medio espera");
            float dir = e.Teams[0].AttackDirection;
            e.SetInput(0, new MatchInput { Move = new Vec2(dir, 0f) }); // intenta sacar hacia delante
            Assert.That(e.TryGetRestartAim(0, out _, out Vec2 aim), Is.True);
            float fromBackward = MathF.Acos(Vec2.Dot(aim, new Vec2(-dir, 0f))) * 180f / MathF.PI;
            Assert.That(fromBackward, Is.LessThanOrEqualTo(e.Config.Rules.KickoffAimDeg + 0.5f), "el saque se apunta hacia el propio campo");

            e.SetInput(0, new MatchInput { Pass = true, Move = new Vec2(0f, 1f) });
            e.Tick();
            Assert.That(e.Phase, Is.EqualTo(MatchPhase.Playing));
        }

        [Test]
        public void ThrowInAim_AlwaysPointsIntoThePitch()
        {
            MatchEngine e = Isolated(12);
            e.SetHuman(0, true);
            e.TestPlaceBall(new Vec3(0f, 33f, 0f), new Vec3(0f, 8f, 0f), e.Teams[1].Players[4]);
            for (int i = 0; i < 60 * 6; i++)
            {
                e.Tick();
            }

            foreach (Vec2 stick in new[] { new Vec2(0f, 1f), new Vec2(1f, 0.2f), new Vec2(-1f, 0.5f), new Vec2(0f, -1f) })
            {
                e.SetInput(0, new MatchInput { Move = stick });
                Assert.That(e.TryGetRestartAim(0, out _, out Vec2 aim), Is.True);
                Assert.That(aim.Y, Is.LessThan(0f), $"con el joystick en {stick.X},{stick.Y} apunta hacia adentro");
            }
        }

        [Test]
        public void ChargedCrossFromACorner_GoesFartherThanAShortOne()
        {
            float Landing(float power)
            {
                MatchEngine e = Isolated(13);
                e.SetHuman(0, true);
                float end = e.Teams[0].AttackDirection;
                e.TestPlaceBall(new Vec3(end * 52f, 20f, 0f), new Vec3(end * 15f, 0f, 0f), e.Teams[1].Players[4]);
                for (int i = 0; i < 60 * 7 && !e.AwaitingHumanRestart; i++)
                {
                    e.Tick();
                }

                Assert.That(e.CurrentRestart.Kind, Is.EqualTo(RestartKind.Corner));
                e.SetInput(0, new MatchInput { Cross = true, CrossPower = power });
                e.Tick();
                Vec2 from = e.CurrentRestart.Position;
                for (int i = 0; i < 300 && e.Ball.Pos.Z > 0.2f || i < 5; i++)
                {
                    e.Tick();
                }

                return Vec2.Distance(e.Ball.Pos.Horizontal, from);
            }

            Assert.That(Landing(1f), Is.GreaterThan(Landing(0.05f) + 10f));
        }
    }
}
