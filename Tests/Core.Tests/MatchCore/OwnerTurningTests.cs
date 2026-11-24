using System;
using Football.Core.Common;
using Football.Core.MatchCore;
using NUnit.Framework;

namespace Football.Core.Tests.MatchCore
{
    /// <summary>El poseedor de la computadora no da vueltas al recibir el balón (antes giraba 180° de más y uno de cada cinco daba más de una vuelta).</summary>
    public class OwnerTurningTests
    {
        [Test]
        public void TheOwner_DoesNotSpinInTheFirstSecondAndAHalf()
        {
            double wasted = 0;
            int events = 0, spins = 0;
            for (ulong seed = 1; seed <= 4; seed++)
            {
                MatchEngine e = MatchEngineTests.NewEngine(seed);
                e.TestBeginOpenPlay();
                MatchPlayer last = null;
                float lastHeading = 0f, firstHeading = 0f;
                double turn = 0;
                int ticks = 0;
                for (int i = 0; i < 60 * 480 && !e.IsFinished; i++)
                {
                    e.Tick();
                    MatchPlayer o = e.Owner;
                    if (o != last)
                    {
                        last = o;
                        ticks = 0;
                        turn = 0;
                        if (o != null)
                        {
                            lastHeading = firstHeading = o.Heading;
                        }

                        continue;
                    }

                    if (o == null)
                    {
                        continue;
                    }

                    turn += Math.Abs(MathUtil.AngleDiff(o.Heading, lastHeading));
                    lastHeading = o.Heading;
                    if (++ticks == 90)
                    {
                        double extra = Math.Max(0.0, turn - Math.Abs(MathUtil.AngleDiff(o.Heading, firstHeading)));
                        wasted += extra;
                        events++;
                        spins += turn > 2 * Math.PI ? 1 : 0;
                    }
                }
            }

            Assert.That(events, Is.GreaterThan(100), "hubo posesiones para medir");
            Assert.That(wasted / events * 180 / Math.PI, Is.LessThan(70.0), "giro de más por posesión, en grados");
            Assert.That(spins / (double)events, Is.LessThan(0.02), "casi ninguna posesión da una vuelta entera");
        }
    }
}
