using Football.Core.Common;
using Football.Core.MatchCore;
using NUnit.Framework;

namespace Football.Core.Tests.MatchCore
{
    /// <summary>La IA prefiere pasarle al compañero que está solo.</summary>
    public class FreeTeammateTests
    {
        private static (float rivalDistance, int passes) Measure(float weight)
        {
            float sum = 0f;
            int passes = 0;
            for (ulong seed = 1; seed <= 4; seed++)
            {
                MatchConfig cfg = new MatchConfig();
                cfg.Ai.FreeTeammateWeight = weight;
                MatchEngine e = MatchEngineTests.NewEngine(300 + seed, cfg: cfg);
                bool wasInFlight = false;
                for (int t = 0; t < 60 * 360; t++)
                {
                    e.Tick();
                    bool flying = e.TestPassInFlight && e.TestPassTarget != null;
                    if (flying && !wasInFlight)
                    {
                        MatchPlayer target = e.TestPassTarget;
                        sum += e.TestNearestOpponentDistance(target.Pos, target.Team);
                        passes++;
                    }

                    wasInFlight = flying;
                }
            }

            return (sum / passes, passes);
        }

        [Test]
        public void Ai_PassesToTeammatesWithMoreSpace_ThanWithoutThePreference()
        {
            (float without, int n0) = Measure(0f);
            (float with, int n1) = Measure(new MatchConfig().Ai.FreeTeammateWeight);
            TestContext.Out.WriteLine($"rival más cercano al receptor: sin preferencia {without:F2} m ({n0} pases), con {with:F2} m ({n1} pases)");
            Assert.That(with, Is.GreaterThan(without + 0.1f));
        }
    }
}
