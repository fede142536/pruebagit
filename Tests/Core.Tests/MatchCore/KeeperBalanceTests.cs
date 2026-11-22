using Football.Core.Common;
using Football.Core.Domain;
using Football.Core.MatchCore;
using NUnit.Framework;

namespace Football.Core.Tests.MatchCore
{
    /// <summary>Equilibrio del arquero: ataja los remates lejanos y concede los potentes de cerca y colocados.</summary>
    public sealed class KeeperBalanceTests
    {
        /// <summary>Remate a ras del suelo desde la distancia dada, que pasa a <paramref name="offset"/> m del arquero.</summary>
        private static bool Scores(ulong seed, float distance, float speed, float offset)
        {
            MatchEngine e = MatchEngineTests.NewEngine(seed);
            e.TestBeginOpenPlay();
            for (int i = 0; i < 22; i++)
            {
                e.TestPlace(e.Players[i], new Vec2(-30f, -30f + 2.8f * i)); // todos lejos de la jugada
            }

            MatchPlayer keeper = e.Teams[1].Players[0];
            float goalX = e.Pitch.HalfLength;
            e.TestPlace(keeper, new Vec2(goalX - 1f, 0f));
            MatchPlayer shooter = e.Teams[0].Players[9];
            e.TestPlace(shooter, new Vec2(goalX - distance, 0f));
            var direction = new Vec2(distance, offset).Normalized;
            e.TestKick(shooter, new Vec3(direction.X * speed, direction.Y * speed, 0.3f), true);
            for (int i = 0; i < 150 && e.Phase == MatchPhase.Playing; i++)
            {
                e.Tick();
            }

            return e.Teams[0].Stats.Goals > 0;
        }

        private static int Goals(float distance, float speed, float offset)
        {
            int goals = 0;
            for (ulong seed = 0; seed < 200; seed++)
            {
                goals += Scores(seed, distance, speed, offset) ? 1 : 0;
            }

            return goals;
        }

        [Test]
        public void PowerfulPlacedShotFromClose_GoesInMoreThanTheSameShotFromFar()
        {
            int close = Goals(9f, 26f, 2.4f), far = Goals(28f, 26f, 2.4f);
            
            Assert.That(close, Is.GreaterThan(far + 40), $"de cerca {close}/200, de lejos {far}/200");
        }

        [Test]
        public void CentralShot_IsSavedMoreThanAShotNextToThePost()
        {
            int central = Goals(14f, 24f, 0.3f), post = Goals(14f, 24f, 3.2f);
            
            Assert.That(post, Is.GreaterThan(central + 30), $"al palo {post}/200, al centro {central}/200");
        }
    }
}
