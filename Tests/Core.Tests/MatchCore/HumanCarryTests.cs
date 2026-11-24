using Football.Core.Common;
using Football.Core.MatchCore;
using NUnit.Framework;

namespace Football.Core.Tests.MatchCore
{
    /// <summary>La pelota del jugador que controla la persona no se escapa sola cuando no hay rival cerca.</summary>
    public class HumanCarryTests
    {
        [Test]
        public void HumanCarrier_SprintingWithoutRivalsNearby_NeverLosesTheBallByItself()
        {
            int lost = 0;
            for (ulong seed = 1; seed <= 20; seed++)
            {
                MatchEngine e = MatchEngineTests.NewEngine(900 + seed);
                e.TestBeginOpenPlay();
                for (int i = 0; i < 22; i++)
                {
                    e.TestPlace(e.Players[i], new Vec2(i < 11 ? -45f : 45f, -30f + 5f * (i % 11)));
                }

                MatchPlayer carrier = e.Teams[0].Players[9];
                e.TestPlace(carrier, new Vec2(-25f, 0f));
                e.TestGiveBall(carrier);
                e.SetHuman(0, true);
                for (int t = 0; t < 60 * 8; t++) // 8 s corriendo a toda velocidad por la cancha vacía
                {
                    e.SetInput(0, new MatchInput { Move = new Vec2(0f, 1f), Sprint = true });
                    e.Tick();
                    MatchPlayer c = e.Teams[0].Controlled;
                    if (c != null && c.Pos.Y > 30f)
                    {
                        e.TestPlace(c, new Vec2(c.Pos.X, -30f)); // vuelve a empezar sin que los rivales se acerquen
                    }

                    if (e.Owner != carrier)
                    {
                        lost++;
                        break;
                    }
                }
            }

            Assert.That(lost, Is.EqualTo(0), "la persona no pidió soltarla");
        }
    }
}
