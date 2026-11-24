using System;
using Football.Core.Common;
using Football.Core.MatchCore;
using NUnit.Framework;

namespace Football.Core.Tests.MatchCore
{
    /// <summary>El que saca se da vuelta hacia donde va a patear, y en el lateral el gesto empieza antes de que el balón salga.</summary>
    public class RestartFacingTests
    {
        [Test]
        public void GoalKicksAndThrowIns_AreTakenFacingTheField_AndTheThrowStartsBeforeTheBallLeaves()
        {
            int goalKicks = 0, throwIns = 0, badFacing = 0, earlyRelease = 0;
            for (ulong seed = 1; seed <= 12; seed++)
            {
                MatchEngine e = MatchEngineTests.NewEngine(700 + seed);
                MatchPlayer taker = null;
                RestartKind kind = RestartKind.KickOff;
                float windStart = -1f, minute = 0f;
                for (int t = 0; t < 60 * 600 && !e.IsFinished; t++)
                {
                    bool wasSetPiece = e.Phase == MatchPhase.SetPiece;
                    MatchPlayer current = e.Taker;
                    if (current != null)
                    {
                        taker = current;
                        kind = e.CurrentRestart.Kind;
                        if (kind == RestartKind.ThrowIn && taker.Action == PlayerAction.ThrowIn && windStart < 0f)
                        {
                            windStart = t / 60f; // empieza el gesto
                        }
                    }

                    e.Tick();
                    minute = t / 60f;
                    if (wasSetPiece && e.Phase == MatchPhase.Playing && taker != null && (kind == RestartKind.GoalKick || kind == RestartKind.ThrowIn))
                    {
                        // El saque acaba de salir.
                        if (kind == RestartKind.GoalKick)
                        {
                            goalKicks++;
                            Vec2 ahead = new Vec2(e.Teams[taker.Team].AttackDirection, 0f);
                            if (MathF.Abs(MathUtil.AngleDiff(ahead.Angle, taker.Heading)) > 1.2f)
                            {
                                badFacing++;
                                TestContext.Out.WriteLine($"arco: heading {taker.Heading:F2} dir {ahead.Angle:F2} keeper {taker.IsKeeper} pos {taker.Pos}");
                            }
                        }
                        else
                        {
                            throwIns++;
                            float inward = taker.Pos.Y > 0f ? -MathF.PI / 2f : MathF.PI / 2f;
                            if (MathF.Abs(MathUtil.AngleDiff(inward, taker.Heading)) > 1.0f)
                            {
                                badFacing++;
                                TestContext.Out.WriteLine($"lateral: heading {taker.Heading:F2} inward {inward:F2} pos {taker.Pos}");
                            }

                            if (windStart < 0f || minute - windStart < e.Config.Rules.ThrowInReleaseDelay - 0.1f)
                            {
                                earlyRelease++;
                            }
                        }

                        windStart = -1f;
                        taker = null;
                    }
                }
            }

            TestContext.Out.WriteLine($"saques de arco {goalKicks}, laterales {throwIns}, mal orientados {badFacing}, balón antes del gesto {earlyRelease}");
            Assert.That(goalKicks + throwIns, Is.GreaterThan(20));
            Assert.That(badFacing, Is.EqualTo(0), "el que saca mira hacia donde va a patear");
            Assert.That(earlyRelease, Is.EqualTo(0), "en el lateral, el balón sale después de empezar el gesto");
        }
    }
}
