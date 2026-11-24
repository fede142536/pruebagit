using Football.Core.Common;
using Football.Core.MatchCore;
using NUnit.Framework;

namespace Football.Core.Tests.MatchCore
{
    /// <summary>El arquero se compromete con su estirada y sostiene el balón contra el pecho.</summary>
    public class KeeperDiveTests
    {
        [Test]
        public void Keeper_DoesNotRetargetInTheAir_ForSmallChanges()
        {
            int dives = 0, nervous = 0;
            for (ulong seed = 1; seed <= 12; seed++)
            {
                MatchEngine e = MatchEngineTests.NewEngine(500 + seed);
                float[] lastY = new float[2] { float.NaN, float.NaN };
                int[] changes = new int[2];
                for (int t = 0; t < 60 * 420; t++)
                {
                    e.Tick();
                    for (int k = 0; k < 2; k++)
                    {
                        MatchPlayer keeper = e.Teams[k].Players[0];
                        if (keeper.Diving)
                        {
                            if (float.IsNaN(lastY[k]))
                            {
                                dives++;
                                changes[k] = 0;
                            }
                            else if (System.MathF.Abs(keeper.Target.Y - lastY[k]) > 0.01f)
                            {
                                changes[k]++;
                                if (changes[k] == 3)
                                {
                                    nervous++; // cambió de rumbo tres veces en una misma estirada
                                }
                            }

                            lastY[k] = keeper.Target.Y;
                        }
                        else
                        {
                            lastY[k] = float.NaN;
                        }
                    }
                }
            }

            TestContext.Out.WriteLine($"estiradas {dives}, con tres correcciones o más: {nervous}");
            Assert.That(dives, Is.GreaterThan(5));
            Assert.That(nervous, Is.LessThanOrEqualTo(dives / 10), "el arquero no se corrige una y otra vez en el aire");
        }

        [Test]
        public void KeeperHoldingTheBall_HasItInFrontOfTheChest()
        {
            MatchEngine e = MatchEngineTests.NewEngine(77);
            e.TestBeginOpenPlay();
            MatchPlayer keeper = e.Teams[0].Players[0];
            e.TestPlace(keeper, new Vec2(-48f, 0f));
            keeper.Heading = 0f;
            e.TestGiveBall(keeper);
            keeper.OwnerTimer = 1e6f; // no decide nada: solo se mira dónde queda el balón
            e.Tick();
            Assert.That(e.Owner, Is.SameAs(keeper));
            Assert.That(e.Ball.Pos.Z, Is.EqualTo(e.Config.Player.KeeperHoldHeight).Within(0.01f));
            Assert.That(Vec2.Distance(e.Ball.Pos.Horizontal, keeper.Pos), Is.EqualTo(e.Config.Player.KeeperHoldForward).Within(0.15f));
        }
    }
}
