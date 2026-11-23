using Football.Core.MatchCore;
using NUnit.Framework;

namespace Football.Core.Tests.MatchCore
{
    /// <summary>Primer toque y cabezazo: acciones de presentación que el motor publica al ganar la posesión.</summary>
    public sealed class TouchActionTests
    {
        [Test]
        public void ReceivingTheBall_ShowsAFirstTouch()
        {
            MatchEngine e = SubstitutionTests.NewMatch(11);
            int controls = 0, headers = 0;
            for (int i = 0; i < 60 * 240 && !e.IsFinished; i++)
            {
                e.Tick();
                foreach (MatchPlayer p in e.Players)
                {
                    controls += p.Action == PlayerAction.Control ? 1 : 0;
                    headers += p.Action == PlayerAction.Header ? 1 : 0;
                }
            }

            Assert.That(controls, Is.GreaterThan(0), "en cuatro minutos alguien controla el balón");
            TestContext.Out.WriteLine("controles=" + controls + " cabezazos=" + headers);
        }

        [Test]
        public void TheKeeperNeverShowsAFirstTouch()
        {
            MatchEngine e = SubstitutionTests.NewMatch(12);
            for (int i = 0; i < 60 * 240 && !e.IsFinished; i++)
            {
                e.Tick();
                foreach (MatchPlayer p in e.Players)
                {
                    if (p.IsKeeper)
                    {
                        Assert.That(p.Action, Is.Not.EqualTo(PlayerAction.Control).And.Not.EqualTo(PlayerAction.Header));
                    }
                }
            }
        }
    }
}
