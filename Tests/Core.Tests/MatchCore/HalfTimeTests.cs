using Football.Core.Common;
using Football.Core.MatchCore;
using NUnit.Framework;

namespace Football.Core.Tests.MatchCore
{
    /// <summary>Con el descanso manual el campo queda quieto hasta que se llama a StartSecondHalf.</summary>
    public class HalfTimeTests
    {
        [Test]
        public void ManualHalfTime_FreezesThePitch_UntilTheSecondHalfIsStarted()
        {
            MatchEngine e = MatchEngineTests.NewEngine(31);
            e.ManualHalfTime = true;
            int guard = 0;
            while (!e.AwaitingSecondHalf && guard++ < 60 * 60 * 60)
            {
                e.Tick();
            }

            Assert.That(e.AwaitingSecondHalf, Is.True);
            Assert.That(e.Half, Is.EqualTo(2));
            ulong hash = e.StateHash();
            for (int i = 0; i < 600; i++)
            {
                e.Tick();
            }

            Assert.That(e.StateHash(), Is.EqualTo(hash), "durante el descanso nada se mueve");
            e.StartSecondHalf();
            Assert.That(e.AwaitingSecondHalf, Is.False);
            Assert.That(e.Phase, Is.EqualTo(MatchPhase.SetPiece));
            Assert.That(e.Ball.Pos.Horizontal.Length, Is.LessThan(0.1f), "el balón vuelve al punto central");
        }

        [Test]
        public void DefaultHalfTime_StillMovesOnByItself()
        {
            MatchEngine e = MatchEngineTests.NewEngine(31);
            int guard = 0;
            while (e.Half == 1 && guard++ < 60 * 60 * 60)
            {
                e.Tick();
            }

            Assert.That(e.AwaitingSecondHalf, Is.False);
            Assert.That(e.Half, Is.EqualTo(2));
            Assert.That(e.Phase, Is.EqualTo(MatchPhase.SetPiece));
        }
    }
}
