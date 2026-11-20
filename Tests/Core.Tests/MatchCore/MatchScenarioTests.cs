using System;
using Football.Core.Common;
using Football.Core.MatchCore;
using NUnit.Framework;

namespace Football.Core.Tests.MatchCore
{
    /// <summary>
    /// Escenarios armados dentro del motor: se aíslan los jugadores (la IA queda congelada) y se coloca solo lo
    /// necesario para comprobar una regla. Los jugadores 0-10 son el local (ataca hacia +X), 11-21 el visitante.
    /// </summary>
    public class MatchScenarioTests
    {
        private const float Frozen = 1000f;

        private static MatchEngine Isolated(ulong seed = 1)
        {
            MatchEngine e = MatchEngineTests.NewEngine(seed);
            e.TestBeginOpenPlay();
            for (int i = 0; i < 22; i++)
            {
                MatchPlayer p = e.Players[i];
                int n = i % 11;
                // Local en la banda y=-30, visitante en y=+30, repartidos a lo largo del campo y fuera de cualquier jugada.
                e.TestPlace(p, new Vec2(-50f + 4f * n, i < 11 ? -30f : 30f));
                p.DecisionTimer = Frozen;
                p.OwnerTimer = Frozen;
            }

            return e;
        }

        private static void TickUntil(MatchEngine e, Func<bool> done, int maxTicks = 600)
        {
            for (int i = 0; i < maxTicks && !done(); i++)
            {
                e.Tick();
            }
        }

        private static int Count(MatchEngine e, MatchEventType type, int team = -1) => e.Events.CountOf(type, team);

        // ---- Gol ------------------------------------------------------------------------------------------------

        [Test]
        public void Goal_IsScoredRestartsWithAKickOffByTheConcedingTeam()
        {
            MatchEngine e = Isolated();
            e.TestPlaceBall(new Vec3(50f, 2f, 1f), new Vec3(25f, 0f, 0f), e.Players[10]);

            TickUntil(e, () => e.Phase != MatchPhase.Playing, 60);

            Assert.That(e.Teams[0].Stats.Goals, Is.EqualTo(1));
            Assert.That(Count(e, MatchEventType.Goal, 0), Is.EqualTo(1));
            Assert.That(e.Phase, Is.EqualTo(MatchPhase.GoalPause));

            TickUntil(e, () => e.Phase == MatchPhase.SetPiece, 400);
            Assert.That(e.CurrentRestart.Kind, Is.EqualTo(RestartKind.KickOff));
            Assert.That(e.CurrentRestart.Team, Is.EqualTo(1), "saca el que recibió el gol");
            Assert.That(e.Ball.Pos.X, Is.Zero.Within(1e-3f));
            Assert.That(e.Ball.Pos.Y, Is.Zero.Within(1e-3f));

            TickUntil(e, () => e.Phase == MatchPhase.Playing, 400);
            Assert.That(e.Phase, Is.EqualTo(MatchPhase.Playing));
        }

        [Test]
        public void Goal_AtTheOtherEnd_CountsForTheOtherTeam()
        {
            MatchEngine e = Isolated();
            e.TestPlaceBall(new Vec3(-50f, -1f, 0.5f), new Vec3(-22f, 0f, 0f), e.Players[15]);

            TickUntil(e, () => e.Phase != MatchPhase.Playing, 60);

            Assert.That(e.Teams[1].Stats.Goals, Is.EqualTo(1));
            Assert.That(e.Teams[0].Stats.Goals, Is.Zero);
        }

        [Test]
        public void OwnGoal_CountsForTheAttackingTeam()
        {
            MatchEngine e = Isolated();
            e.TestPlaceBall(new Vec3(50f, 1f, 0.5f), new Vec3(20f, 0f, 0f), e.Players[14]); // el último en tocar es del visitante
            TickUntil(e, () => e.Phase != MatchPhase.Playing, 60);

            Assert.That(e.Teams[0].Stats.Goals, Is.EqualTo(1));
        }

        [Test]
        public void ShotOverTheBar_IsNotAGoal()
        {
            MatchEngine e = Isolated();
            e.TestPlaceBall(new Vec3(48f, 0f, 3.2f), new Vec3(25f, 0f, 0f), e.Players[10]);
            TickUntil(e, () => e.Phase != MatchPhase.Playing, 60);

            Assert.That(e.Teams[0].Stats.Goals, Is.Zero);
            Assert.That(e.CurrentRestart.Kind, Is.EqualTo(RestartKind.GoalKick));
        }

        [Test]
        public void BallRollingInsideThePitch_ProducesNoRestartsOrGoals()
        {
            MatchEngine e = Isolated();
            e.TestPlaceBall(new Vec3(0f, 0f, 0f), new Vec3(5f, 3f, 0f), e.Players[5]);
            for (int i = 0; i < 120; i++)
            {
                e.Tick();
            }

            Assert.That(e.Phase, Is.EqualTo(MatchPhase.Playing));
            Assert.That(Count(e, MatchEventType.Goal) + Count(e, MatchEventType.ThrowIn) + Count(e, MatchEventType.GoalKick)
                        + Count(e, MatchEventType.Corner), Is.Zero);
        }

        // ---- Saque de puerta, córner y banda -----------------------------------------------------------------------

        [Test]
        public void GoalKick_AwardedToTheDefenderWhenTheAttackerSendsTheBallOut()
        {
            MatchEngine e = Isolated();
            e.TestPlaceBall(new Vec3(48f, 10f, 0.2f), new Vec3(20f, 0f, 0f), e.Players[10]);
            TickUntil(e, () => e.Phase == MatchPhase.SetPiece, 60);

            Assert.That(e.CurrentRestart.Kind, Is.EqualTo(RestartKind.GoalKick));
            Assert.That(e.CurrentRestart.Team, Is.EqualTo(1));
            Assert.That(e.CurrentRestart.Position.X, Is.EqualTo(52.5f - 5.5f).Within(0.01f));
            Assert.That(Count(e, MatchEventType.GoalKick, 1), Is.EqualTo(1));
        }

        [Test]
        public void Corner_AwardedToTheAttackerWhenTheDefenderSendsTheBallOut()
        {
            MatchEngine e = Isolated();
            e.TestPlaceBall(new Vec3(48f, 10f, 0.2f), new Vec3(20f, 0f, 0f), e.Players[14]);
            TickUntil(e, () => e.Phase == MatchPhase.SetPiece, 60);

            Assert.That(e.CurrentRestart.Kind, Is.EqualTo(RestartKind.Corner));
            Assert.That(e.CurrentRestart.Team, Is.EqualTo(0));
            Assert.That(e.CurrentRestart.Position.X, Is.EqualTo(52.5f).Within(0.01f));
            Assert.That(e.CurrentRestart.Position.Y, Is.EqualTo(34f).Within(0.01f), "córner del lado por el que salió");
            Assert.That(e.Teams[0].Stats.Corners, Is.EqualTo(1));
        }

        [Test]
        public void Corner_IsTakenAsACrossIntoTheBox()
        {
            MatchEngine e = Isolated();
            e.TestPlaceBall(new Vec3(48f, -10f, 0.2f), new Vec3(20f, 0f, 0f), e.Players[14]);
            TickUntil(e, () => e.Phase == MatchPhase.SetPiece, 60);
            TickUntil(e, () => e.Phase == MatchPhase.Playing, 1500);

            Assert.That(e.Phase, Is.EqualTo(MatchPhase.Playing));
            Assert.That(e.Ball.Vel.Z, Is.GreaterThan(0f), "el córner sale por alto");
            Assert.That(e.Ball.Vel.X, Is.LessThan(0f), "hacia el área desde la línea de fondo derecha");
        }

        [Test]
        public void ThrowIn_AwardedToTheOpponentOfTheLastToucher()
        {
            MatchEngine e = Isolated();
            e.TestPlaceBall(new Vec3(10f, 33.8f, 0.2f), new Vec3(0f, 6f, 0f), e.Players[4]);
            TickUntil(e, () => e.Phase == MatchPhase.SetPiece, 60);

            Assert.That(e.CurrentRestart.Kind, Is.EqualTo(RestartKind.ThrowIn));
            Assert.That(e.CurrentRestart.Team, Is.EqualTo(1));
            Assert.That(e.CurrentRestart.Position.Y, Is.EqualTo(34f).Within(0.01f));
            Assert.That(Count(e, MatchEventType.ThrowIn, 1), Is.EqualTo(1));
        }

        [Test]
        public void ThrowIn_IsPlayedToATeammateWithAGroundBall()
        {
            MatchEngine e = Isolated();
            e.TestPlaceBall(new Vec3(10f, -33.8f, 0.2f), new Vec3(0f, -6f, 0f), e.Players[15]);
            TickUntil(e, () => e.Phase == MatchPhase.SetPiece, 60);
            TickUntil(e, () => e.Phase == MatchPhase.Playing, 1500);

            Assert.That(e.Phase, Is.EqualTo(MatchPhase.Playing));
            Assert.That(e.Ball.Vel.Length, Is.GreaterThan(1f).And.LessThan(16f), "saque de banda: balón lento y raso");
            Assert.That(e.LastToucher.Team, Is.EqualTo(0));
        }

        // ---- Faltas, tarjetas y penalti ----------------------------------------------------------------------------

        /// <summary>Atacante local con balón y defensor visitante pegado a punto de entrar; se repite por semillas hasta que ocurre la falta.</summary>
        private static MatchEngine FoulScenario(ulong seed, Vec2 ownerAt, MatchPlayer fouler = null, int yellowsBefore = 0)
        {
            MatchEngine e = Isolated(seed);
            MatchPlayer owner = e.Players[9];
            MatchPlayer defender = fouler ?? e.Players[13];
            e.TestPlace(owner, ownerAt);
            e.TestPlace(defender, ownerAt + new Vec2(-0.2f, 0.9f));
            owner.ActionTimer = Frozen;
            e.TestGiveBall(owner);
            defender.WantsTackle = true;
            defender.TackleTimer = 0f;
            defender.Yellows = yellowsBefore;
            e.Tick();
            return e;
        }

        [Test]
        public void FoulInsideTheArea_IsAPenaltyAtTheSpot()
        {
            int fouls = 0;
            for (ulong seed = 0; seed < 600 && fouls < 6; seed++)
            {
                MatchEngine e = FoulScenario(seed, new Vec2(44f, 2f));
                if (Count(e, MatchEventType.Foul, 1) == 0)
                {
                    continue;
                }

                fouls++;
                Assert.That(e.Phase, Is.EqualTo(MatchPhase.SetPiece));
                Assert.That(e.CurrentRestart.Kind, Is.EqualTo(RestartKind.Penalty), "seed " + seed);
                Assert.That(e.CurrentRestart.Team, Is.EqualTo(0));
                Assert.That(e.CurrentRestart.Position.X, Is.EqualTo(52.5f - 11f).Within(0.01f));
                Assert.That(Count(e, MatchEventType.Penalty, 0), Is.EqualTo(1));
                Assert.That(e.Teams[1].Stats.Fouls, Is.EqualTo(1));
            }

            Assert.That(fouls, Is.GreaterThanOrEqualTo(6), "debe haber faltas en 600 intentos");
        }

        [Test]
        public void FoulOutsideTheArea_IsAFreeKickWhereItHappened()
        {
            int fouls = 0;
            for (ulong seed = 0; seed < 600 && fouls < 6; seed++)
            {
                MatchEngine e = FoulScenario(seed, new Vec2(20f, 2f));
                if (Count(e, MatchEventType.Foul, 1) == 0)
                {
                    continue;
                }

                fouls++;
                Assert.That(e.CurrentRestart.Kind, Is.EqualTo(RestartKind.FreeKick));
                Assert.That(e.CurrentRestart.Team, Is.EqualTo(0));
                Assert.That(e.CurrentRestart.Position.X, Is.EqualTo(20f).Within(0.01f));
                Assert.That(e.CurrentRestart.Position.Y, Is.EqualTo(2f).Within(0.01f));
                Assert.That(Count(e, MatchEventType.FreeKick, 0), Is.EqualTo(1));
            }

            Assert.That(fouls, Is.GreaterThanOrEqualTo(6));
        }

        [Test]
        public void ATackleThatWinsTheBallOrMisses_IsNotAFoul()
        {
            int clean = 0;
            for (ulong seed = 0; seed < 100; seed++)
            {
                MatchEngine e = FoulScenario(seed, new Vec2(20f, 2f));
                if (Count(e, MatchEventType.Foul) == 0)
                {
                    clean++;
                    Assert.That(e.Phase, Is.EqualTo(MatchPhase.Playing), "sin falta el juego sigue");
                    Assert.That(Count(e, MatchEventType.FreeKick), Is.Zero);
                }
            }

            Assert.That(clean, Is.GreaterThan(50));
        }

        [Test]
        public void ThePenalty_IsTakenByTheFouledTeamAsAShot_WithEveryoneElseOutsideTheArea()
        {
            for (ulong seed = 0; seed < 600; seed++)
            {
                MatchEngine e = FoulScenario(seed, new Vec2(44f, 2f));
                if (Count(e, MatchEventType.Penalty) == 0)
                {
                    continue;
                }

                TickUntil(e, () => Count(e, MatchEventType.Shot) > 0, 900);

                Assert.That(Count(e, MatchEventType.Shot, 0), Is.EqualTo(1), "el penalti lo tira el equipo que lo recibió");
                MatchEvent shot = e.Events[FindEvent(e, MatchEventType.Shot)];
                Assert.That(shot.X, Is.EqualTo(41.5f).Within(0.5f), "se tira desde el punto de penalti");
                for (int i = 0; i < 22; i++)
                {
                    MatchPlayer p = e.Players[i];
                    if (p.IsKeeper || p.Index == shot.PlayerIndex)
                    {
                        continue;
                    }

                    Assert.That(e.Pitch.InPenaltyArea(1f, p.Pos), Is.False, $"jugador {i} dentro del área durante el penalti");
                }

                return;
            }

            Assert.Fail("no se produjo ninguna falta para comprobar el penalti");
        }

        private static int FindEvent(MatchEngine e, MatchEventType type)
        {
            for (int i = 0; i < e.Events.Count; i++)
            {
                if (e.Events[i].Type == type)
                {
                    return i;
                }
            }

            return -1;
        }

        [Test]
        public void Yellow_IsShownToTheFoulerAndTwoYellowsMeanSendOff()
        {
            int yellows = 0, secondYellows = 0;
            for (ulong seed = 0; seed < 1500 && (yellows < 3 || secondYellows < 3); seed++)
            {
                MatchEngine e = FoulScenario(seed, new Vec2(20f, 2f), yellowsBefore: seed % 2 == 0 ? 0 : 1);
                MatchPlayer fouler = e.Players[13];
                int yellowEvents = Count(e, MatchEventType.YellowCard, 1);
                if (yellowEvents == 0)
                {
                    continue;
                }

                Assert.That(Count(e, MatchEventType.Foul, 1), Is.EqualTo(1), "la tarjeta acompaña a una falta");
                if (seed % 2 == 0 && Count(e, MatchEventType.RedCard) == 0)
                {
                    yellows++;
                    Assert.That(fouler.Yellows, Is.EqualTo(1));
                    Assert.That(fouler.SentOff, Is.False, "una amarilla no expulsa");
                }
                else if (seed % 2 == 1)
                {
                    secondYellows++;
                    Assert.That(fouler.Yellows, Is.EqualTo(2));
                    Assert.That(fouler.SentOff, Is.True, "la segunda amarilla expulsa");
                    Assert.That(Count(e, MatchEventType.RedCard, 1), Is.EqualTo(1));
                    Assert.That(e.Teams[1].Stats.Reds, Is.EqualTo(1));
                }
            }

            Assert.That(yellows, Is.GreaterThanOrEqualTo(3));
            Assert.That(secondYellows, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void SentOffPlayer_NoLongerTakesPartAndTheTeamPlaysWithTen()
        {
            for (ulong seed = 0; seed < 3000; seed++)
            {
                MatchEngine e = FoulScenario(seed, new Vec2(20f, 2f), yellowsBefore: 1);
                if (!e.Players[13].SentOff)
                {
                    continue;
                }

                TickUntil(e, () => e.Phase == MatchPhase.Playing, 1500);
                Vec2 where = e.Players[13].Pos;
                for (int i = 0; i < 300; i++)
                {
                    e.Tick();
                }

                Assert.That(e.Players[13].Pos.X, Is.EqualTo(where.X).Within(1e-4f), "el expulsado no se mueve");
                Assert.That(e.Owner, Is.Not.SameAs(e.Players[13]));
                return;
            }

            Assert.Fail("no hubo expulsión en las semillas probadas");
        }

        [Test]
        public void TheKeeper_IsNeverSentOff()
        {
            int fouls = 0;
            for (ulong seed = 0; seed < 4000 && fouls < 40; seed++)
            {
                MatchEngine e = Isolated(seed);
                MatchPlayer owner = e.Players[9];
                MatchPlayer keeper = e.Players[11];
                e.TestPlace(owner, new Vec2(44f, 2f));
                e.TestPlace(keeper, new Vec2(44.2f, 2.9f));
                owner.ActionTimer = Frozen;
                e.TestGiveBall(owner);
                keeper.WantsTackle = true;
                keeper.TackleTimer = 0f;
                keeper.Yellows = 1;
                e.Tick();
                if (Count(e, MatchEventType.Foul) > 0)
                {
                    fouls++;
                    Assert.That(keeper.SentOff, Is.False);
                }
            }

            Assert.That(fouls, Is.GreaterThan(10));
        }

        // ---- Fuera de juego ---------------------------------------------------------------------------------------

        /// <summary>Pase del jugador local 8 desde (10,0) a un compañero adelantado, con la defensa visitante en x = 20.</summary>
        private static MatchEngine PassScenario(Vec2 receiverAt, float? secondDefenderX = null)
        {
            MatchEngine e = Isolated(9);
            for (int i = 0; i < 11; i++)
            {
                e.TestPlace(e.Players[i], new Vec2(-50f + 4f * i, -30f)); // todos detrás del balón
            }

            for (int i = 11; i < 22; i++)
            {
                e.TestPlace(e.Players[i], new Vec2(20f, i % 2 == 0 ? 25f : -25f)); // defensa en x = 20, lejos del carril
            }

            e.TestPlace(e.Players[11], new Vec2(50f, 0f)); // portero visitante
            if (secondDefenderX.HasValue)
            {
                e.TestPlace(e.Players[16], new Vec2(secondDefenderX.Value, -25f));
            }

            MatchPlayer passer = e.Players[8];
            MatchPlayer receiver = e.Players[9];
            e.TestPlace(passer, new Vec2(10f, 0f));
            e.TestPlace(receiver, receiverAt);
            receiver.DecisionTimer = 0f;
            e.TestGiveBall(passer);

            e.SetHuman(0, true);
            e.SetInput(0, new MatchInput { Move = new Vec2(1f, 0f), Pass = true });
            e.Tick(); // se ejecuta el pase
            // El receptor pasa a ser el jugador controlado: se suelta el joystick para que espere el balón.
            e.SetInput(0, new MatchInput());
            return e;
        }

        [Test]
        public void PassToAPlayerBeyondTheLastDefenders_IsOffside()
        {
            MatchEngine e = PassScenario(new Vec2(40f, 0f));
            TickUntil(e, () => Count(e, MatchEventType.Offside) > 0, 600);

            Assert.That(Count(e, MatchEventType.Offside, 0), Is.EqualTo(1));
            Assert.That(e.Teams[0].Stats.Offsides, Is.EqualTo(1));
            Assert.That(e.Phase, Is.EqualTo(MatchPhase.SetPiece));
            Assert.That(e.CurrentRestart.Kind, Is.EqualTo(RestartKind.FreeKick));
            Assert.That(e.CurrentRestart.Team, Is.EqualTo(1), "tiro libre para el equipo que defiende");
            Assert.That(e.CurrentRestart.Position.X, Is.GreaterThan(30f), "en el lugar del receptor");
        }

        [Test]
        public void PassToAPlayerBehindTheDefensiveLine_IsNotOffside()
        {
            MatchEngine e = PassScenario(new Vec2(15f, 0f));
            TickUntil(e, () => e.Owner == e.Players[9], 600);

            Assert.That(Count(e, MatchEventType.Offside), Is.Zero);
            Assert.That(e.Owner, Is.SameAs(e.Players[9]), "el receptor controla el balón");
            Assert.That(e.Teams[0].Stats.PassesCompleted, Is.EqualTo(1));
        }

        [Test]
        public void PassBeyondTheBallButLevelWithTheSecondLastDefender_IsNotOffside()
        {
            // El penúltimo defensor está en x = 45: el receptor en x = 40 sigue habilitado.
            MatchEngine e = PassScenario(new Vec2(40f, 0f), secondDefenderX: 45f);
            TickUntil(e, () => e.Owner == e.Players[9] || Count(e, MatchEventType.Offside) > 0, 600);

            Assert.That(Count(e, MatchEventType.Offside), Is.Zero);
            Assert.That(e.Owner, Is.SameAs(e.Players[9]));
        }

        [Test]
        public void PassInterceptedByADefender_IsNotOffsideEvenIfTheReceiverWasAhead()
        {
            MatchEngine e = PassScenario(new Vec2(40f, 0f));
            e.TestPlace(e.Players[12], new Vec2(25f, 0.3f)); // un defensor en el carril, antes del receptor
            e.Players[12].DecisionTimer = 0f;
            TickUntil(e, () => e.Owner != null && e.Owner.Team == 1, 600);

            Assert.That(Count(e, MatchEventType.Offside), Is.Zero);
            Assert.That(e.Teams[0].Stats.PassesIntercepted, Is.EqualTo(1));
        }

        [Test]
        public void KickOffAndThrowIns_AreNeverOffside()
        {
            MatchEngine e = MatchEngineTests.NewEngine(4);
            for (int i = 0; i < 60 * 40; i++)
            {
                e.Tick();
            }

            // En 40 s hay saque inicial, saques de banda y puertas; si algún receptor adelantado hubiera sido sancionado
            // en ellos, el contador de fueras de juego del arranque (tras un saque) lo mostraría con el balón parado.
            for (int i = 0; i < e.Events.Count; i++)
            {
                if (e.Events[i].Type == MatchEventType.Offside && i > 0)
                {
                    MatchEventType before = e.Events[i - 1].Type;
                    Assert.That(before, Is.Not.EqualTo(MatchEventType.KickOff));
                    Assert.That(before, Is.Not.EqualTo(MatchEventType.ThrowIn));
                    Assert.That(before, Is.Not.EqualTo(MatchEventType.GoalKick));
                }
            }
        }

        // ---- Control del jugador humano ---------------------------------------------------------------------------

        [Test]
        public void HumanMovement_MovesTheControlledPlayerInTheStickDirection()
        {
            MatchEngine e = Isolated();
            e.SetHuman(0, true);
            e.Tick();
            MatchPlayer p = e.Teams[0].Controlled;
            Assert.That(p, Is.Not.Null);
            e.TestPlace(p, new Vec2(0f, 0f));

            for (int i = 0; i < 90; i++)
            {
                e.SetInput(0, new MatchInput { Move = new Vec2(0f, 1f), Sprint = true });
                e.Tick();
            }

            Assert.That(p.Pos.Y, Is.GreaterThan(5f));
            Assert.That(Math.Abs(p.Pos.X), Is.LessThan(1f));
            Assert.That(p.Speed, Is.GreaterThan(5f));
        }

        [Test]
        public void ReleasingTheStick_StopsThePlayer()
        {
            MatchEngine e = Isolated();
            e.SetHuman(0, true);
            e.Tick();
            MatchPlayer p = e.Teams[0].Controlled;
            e.TestPlace(p, Vec2.Zero);
            for (int i = 0; i < 60; i++)
            {
                e.SetInput(0, new MatchInput { Move = new Vec2(1f, 0f), Sprint = true });
                e.Tick();
            }

            for (int i = 0; i < 60; i++)
            {
                e.SetInput(0, new MatchInput());
                e.Tick();
            }

            Assert.That(p.Speed, Is.LessThan(0.1f));
        }

        [Test]
        public void HumanShot_KicksTheBallTowardsTheGoalAndCountsAsAShot()
        {
            MatchEngine e = Isolated();
            MatchPlayer shooter = e.Players[10];
            e.TestPlace(shooter, new Vec2(38f, 0f));
            e.TestGiveBall(shooter);
            e.SetHuman(0, true);
            e.SetInput(0, new MatchInput { Shoot = true, ShootPower = 1f, Move = new Vec2(1f, 0f) });
            e.Tick();

            Assert.That(Count(e, MatchEventType.Shot, 0), Is.EqualTo(1));
            Assert.That(e.Teams[0].Stats.Shots, Is.EqualTo(1));
            Assert.That(e.Ball.Vel.X, Is.GreaterThan(15f));
            Assert.That(e.Owner, Is.Null);
        }

        [Test]
        public void HumanPass_GoesToTheTeammateInTheStickDirection()
        {
            MatchEngine e = Isolated();
            for (int i = 0; i < 11; i++)
            {
                e.TestPlace(e.Players[i], new Vec2(-50f + 4f * i, -30f));
            }

            MatchPlayer passer = e.Players[8];
            MatchPlayer ahead = e.Players[9];
            e.TestPlace(passer, new Vec2(0f, 0f));
            e.TestPlace(ahead, new Vec2(15f, 8f));
            e.TestGiveBall(passer);
            e.SetHuman(0, true);
            e.SetInput(0, new MatchInput { Pass = true, Move = new Vec2(1f, 0.4f) });
            e.Tick();

            Assert.That(e.TestPassInFlight, Is.True);
            Assert.That(e.Teams[0].Stats.Passes, Is.EqualTo(1));
            Assert.That(e.Ball.Vel.X, Is.GreaterThan(3f));
            Assert.That(e.Ball.Vel.Y, Is.GreaterThan(0f), "el pase sale hacia el compañero, no recto");
        }

        [Test]
        public void ButtonImpulses_AreConsumedInASingleTick()
        {
            MatchEngine e = Isolated();
            MatchPlayer shooter = e.Players[10];
            e.TestPlace(shooter, new Vec2(38f, 0f));
            e.TestGiveBall(shooter);
            e.SetHuman(0, true);
            e.SetInput(0, new MatchInput { Shoot = true, ShootPower = 1f });
            e.Tick();
            e.TestGiveBall(shooter); // vuelve a tener el balón; sin nueva pulsación no debe tirar
            e.Tick();

            Assert.That(Count(e, MatchEventType.Shot, 0), Is.EqualTo(1));
        }

        [Test]
        public void SwitchButton_ChangesTheControlledPlayerWhenDefending()
        {
            MatchEngine e = Isolated();
            e.TestPlaceBall(new Vec3(-20f, 0f, 0f), Vec3.Zero, e.Players[15]);
            e.TestPlace(e.Players[4], new Vec2(-19f, 3f));
            e.TestPlace(e.Players[5], new Vec2(-15f, 3f));
            e.SetHuman(0, true);
            e.Tick();
            MatchPlayer first = e.Teams[0].Controlled;
            Assert.That(first, Is.SameAs(e.Players[4]), "al inicio controla al más cercano al balón");

            e.SetInput(0, new MatchInput { Switch = true });
            e.Tick();

            Assert.That(e.Teams[0].Controlled, Is.Not.SameAs(first));
        }

        [Test]
        public void HumanTackle_ChallengesTheBallCarrier()
        {
            int tackles = 0;
            for (ulong seed = 0; seed < 40; seed++)
            {
                MatchEngine e = Isolated(seed);
                MatchPlayer owner = e.Players[15]; // visitante con balón
                MatchPlayer defender = e.Players[4];
                e.TestPlace(owner, new Vec2(0f, 0f));
                e.TestPlace(defender, new Vec2(-0.8f, 0.4f));
                owner.ActionTimer = Frozen;
                e.TestGiveBall(owner);
                e.SetHuman(0, true);
                e.Tick(); // el humano toma al jugador más cercano al balón
                Assert.That(e.Teams[0].Controlled, Is.SameAs(defender));

                e.SetInput(0, new MatchInput { Tackle = true });
                e.Tick();
                if (defender.TackleTimer > 0f)
                {
                    tackles++;
                }
            }

            Assert.That(tackles, Is.EqualTo(40), "pulsar entrada con el poseedor al alcance siempre intenta la entrada");
        }

        [Test]
        public void SetHuman_False_ReturnsTheTeamToTheAi()
        {
            MatchEngine e = Isolated();
            e.SetHuman(0, true);
            e.Tick();
            Assert.That(e.Teams[0].Controlled, Is.Not.Null);
            e.SetHuman(0, false);
            Assert.That(e.Teams[0].Controlled, Is.Null);
            Assert.That(e.Teams[0].IsHuman, Is.False);
        }
    }
}
