using System;
using Football.Core.Common;
using Football.Core.Domain;
using Football.Core.MatchCore;
using NUnit.Framework;

namespace Football.Core.Tests.MatchCore
{
    /// <summary>Roles tácticos individuales: reglas y efecto medible en el posicionamiento sin balón.</summary>
    public sealed class RoleTests
    {
        [Test]
        public void EachPositionOffersItsOwnDuties()
        {
            Assert.That(Tactics.Allows(Position.LeftBack, PlayerDuty.WingBack), Is.True);
            Assert.That(Tactics.Allows(Position.RightBack, PlayerDuty.Defensive), Is.True);
            Assert.That(Tactics.Allows(Position.CentralMidfielder, PlayerDuty.Playmaker), Is.True);
            Assert.That(Tactics.Allows(Position.DefensiveMidfielder, PlayerDuty.BallWinner), Is.True);
            Assert.That(Tactics.Allows(Position.AttackingMidfielder, PlayerDuty.BoxToBox), Is.True);
            Assert.That(Tactics.Allows(Position.Striker, PlayerDuty.Runner), Is.True);
            Assert.That(Tactics.Allows(Position.CenterBack, PlayerDuty.Runner), Is.False);
            Assert.That(Tactics.Allows(Position.Goalkeeper, PlayerDuty.Playmaker), Is.False);
            Assert.That(Tactics.Allows(Position.LeftWinger, PlayerDuty.Auto), Is.True, "Auto vale para todos");
        }

        [Test]
        public void StylesGiveDefaultDuties_AndAChosenDutyWins()
        {
            var c = new MatchConfig();
            TeamStyle possession = c.Styles[1], counter = c.Styles[2], press = c.Styles[3], defensive = c.Styles[4];
            Assert.That(Tactics.Effective(Position.CentralMidfielder, PlayerDuty.Auto, possession), Is.EqualTo(PlayerDuty.Playmaker));
            Assert.That(Tactics.Effective(Position.Striker, PlayerDuty.Auto, counter), Is.EqualTo(PlayerDuty.Runner));
            Assert.That(Tactics.Effective(Position.LeftBack, PlayerDuty.Auto, press), Is.EqualTo(PlayerDuty.WingBack));
            Assert.That(Tactics.Effective(Position.RightBack, PlayerDuty.Auto, defensive), Is.EqualTo(PlayerDuty.Defensive));
            Assert.That(Tactics.Effective(Position.Striker, PlayerDuty.Runner, defensive), Is.EqualTo(PlayerDuty.Runner), "el elegido manda");
            Assert.That(Tactics.Effective(Position.CenterBack, PlayerDuty.Runner, counter), Is.EqualTo(PlayerDuty.Auto), "un rol inválido se ignora");
        }

        [Test]
        public void SetDuty_ValidatesThePosition()
        {
            MatchEngine e = MatchEngineTests.NewEngine(1);
            int cb = Spot(e, Position.CenterBack), st = Spot(e, Position.Striker);
            Assert.That(e.SetDuty(0, cb, PlayerDuty.Runner), Is.False);
            Assert.That(e.SetDuty(0, 0, PlayerDuty.Auto), Is.False, "el arquero no tiene roles");
            Assert.That(e.SetDuty(0, st, PlayerDuty.TargetMan), Is.True);
            Assert.That(e.DutyOf(e.Teams[0].Players[st]), Is.EqualTo(PlayerDuty.TargetMan));
        }

        private static int Spot(MatchEngine e, Position role)
        {
            FormationSlot[] slots = e.Teams[0].Formation.Slots;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].Role == role)
                {
                    return i;
                }
            }

            throw new InvalidOperationException("sin " + role);
        }

        /// <summary>
        /// En un mismo partido y en los mismos instantes, calcula el ancla de los jugadores del puesto con cada rol: avance
        /// medio hacia el arco rival, apertura media y distancia media al balón. Así la diferencia es solo del rol.
        /// </summary>
        private static (double advance, double width, double toBall)[] Measure(Position role, bool attacking, params PlayerDuty[] duties)
        {
            MatchEngine e = MatchEngineTests.NewEngine(21);
            var result = new (double advance, double width, double toBall)[duties.Length];
            int samples = 0;
            for (int t = 0; t < 4000 && !e.IsFinished; t++)
            {
                e.Tick();
                if (e.Phase != MatchPhase.Playing || (t & 15) != 0)
                {
                    continue;
                }

                float dir = e.Teams[0].AttackDirection;
                Vec2 ball = e.Ball.Pos.Horizontal;
                foreach (MatchPlayer p in e.Teams[0].Players)
                {
                    if (p.Role != role)
                    {
                        continue;
                    }

                    PlayerDuty chosen = e.Teams[0].Duties[p.Spot];
                    for (int d = 0; d < duties.Length; d++)
                    {
                        e.Teams[0].Duties[p.Spot] = duties[d];
                        Vec2 anchor = e.TestAnchor(p, attacking);
                        result[d].advance += dir * anchor.X;
                        result[d].width += Math.Abs(anchor.Y);
                        result[d].toBall += Vec2.Distance(anchor, ball);
                    }

                    e.Teams[0].Duties[p.Spot] = chosen;
                    samples++;
                }
            }

            Assert.That(samples, Is.GreaterThan(100));
            for (int d = 0; d < duties.Length; d++)
            {
                result[d] = (result[d].advance / samples, result[d].width / samples, result[d].toBall / samples);
            }

            return result;
        }

        [Test]
        public void WingBacks_GetForwardAndWide_DefensiveFullbacksStayBack()
        {
            var m = Measure(Position.LeftBack, true, PlayerDuty.Auto, PlayerDuty.WingBack, PlayerDuty.Defensive);
            Assert.That(m[1].advance, Is.GreaterThan(m[0].advance + 5), $"carrilero {m[1].advance:F1} m, normal {m[0].advance:F1} m");
            Assert.That(m[2].advance, Is.LessThan(m[0].advance - 4), $"defensivo {m[2].advance:F1} m");
            Assert.That(m[1].width, Is.GreaterThan(m[0].width), "el carrilero se abre más");
            Assert.That(m[2].width, Is.LessThan(m[0].width), "el defensivo cierra por dentro");
        }

        [Test]
        public void Runners_PlayHigher_TargetMenStayCentral()
        {
            var m = Measure(Position.Striker, true, PlayerDuty.Auto, PlayerDuty.Runner, PlayerDuty.TargetMan);
            Assert.That(m[1].advance, Is.GreaterThan(m[0].advance + 4), $"al espacio {m[1].advance:F1} m, normal {m[0].advance:F1} m");
            Assert.That(m[2].advance, Is.LessThan(m[0].advance), "el pivote baja a recibir");
            Assert.That(m[2].width, Is.LessThan(m[0].width * 0.6), "el pivote juega centrado");
        }

        [Test]
        public void BallWinners_CloseOnTheBall_PlaymakersOfferThemselves()
        {
            var defending = Measure(Position.CentralMidfielder, false, PlayerDuty.Auto, PlayerDuty.BallWinner);
            Assert.That(defending[1].toBall, Is.LessThan(defending[0].toBall * 0.8), "el recuperador se pega al balón al defender");

            var attacking = Measure(Position.CentralMidfielder, true, PlayerDuty.Auto, PlayerDuty.Playmaker, PlayerDuty.BoxToBox, PlayerDuty.BallWinner);
            Assert.That(attacking[1].toBall, Is.LessThan(attacking[0].toBall * 0.85), "el organizador se acerca al poseedor");
            Assert.That(attacking[2].advance, Is.GreaterThan(attacking[0].advance + 5), "el llegador pisa el área");
            Assert.That(attacking[3].advance, Is.LessThan(attacking[0].advance), "el recuperador no se adelanta");
        }
    }
}
