using Football.Core.Common;
using Football.Core.MatchCore;
using NUnit.Framework;

namespace Football.Core.Tests.MatchCore
{
    /// <summary>Segunda marca y buffer de las acciones defensivas de la persona.</summary>
    public sealed class SecondPressTests
    {
        private const int OneSecond = 60;

        /// <summary>Rival con el balón en el centro; el jugador de la persona cerca y dos compañeros a distancias distintas.</summary>
        private static MatchEngine Setup(ulong seed, out MatchPlayer controlled, out MatchPlayer nearMate)
        {
            MatchEngine e = MatchEngineTests.NewEngine(seed);
            e.TestBeginOpenPlay();
            for (int i = 0; i < 22; i++)
            {
                e.TestPlace(e.Players[i], new Vec2(i < 11 ? -40f : 40f, -30f + 6f * (i % 11)));
            }

            MatchPlayer carrier = e.Teams[1].Players[9];
            e.TestPlace(carrier, Vec2.Zero);
            e.TestGiveBall(carrier);
            controlled = e.Teams[0].Players[6];
            nearMate = e.Teams[0].Players[7];
            e.TestPlace(controlled, new Vec2(-5f, 0f));
            e.TestPlace(nearMate, new Vec2(-14f, 8f));
            e.SetHuman(0, true);
            e.Tick(); // el control pasa al más cercano al balón
            return e;
        }

        [Test]
        public void SecondPress_SendsTheNearestOtherTeammateToTheCarrier()
        {
            MatchEngine e = Setup(1, out MatchPlayer controlled, out MatchPlayer mate);
            Assert.That(e.Teams[0].Controlled, Is.SameAs(controlled));
            float before = Vec2.Distance(mate.Pos, e.Owner.Pos);
            for (int t = 0; t < OneSecond; t++)
            {
                e.SetInput(0, new MatchInput { SecondPress = true });
                e.Tick();
                if (e.Owner == null || e.Owner.Team != 1)
                {
                    break;
                }
            }

            Assert.That(e.Teams[0].SecondPresser, Is.SameAs(mate), "sale el compañero más cercano, no el controlado");
            if (e.Owner != null)
            {
                Assert.That(Vec2.Distance(mate.Pos, e.Owner.Pos), Is.LessThan(before - 3f), "se acerca al poseedor");
            }
        }

        [Test]
        public void SecondPress_StopsWhenReleased_AndIsIgnoredWithTheBall()
        {
            MatchEngine e = Setup(2, out _, out _);
            e.SetInput(0, new MatchInput { SecondPress = true });
            e.Tick();
            Assert.That(e.Teams[0].SecondPresser, Is.Not.Null);
            e.SetInput(0, new MatchInput());
            e.Tick();
            Assert.That(e.Teams[0].SecondPresser, Is.Null, "al soltar el botón vuelve a su puesto");

            e.TestGiveBall(e.Teams[0].Players[6]);
            e.SetInput(0, new MatchInput { SecondPress = true });
            e.Tick();
            Assert.That(e.Teams[0].SecondPresser, Is.Null, "con el balón propio no hay a quién presionar");
        }

        [Test]
        public void SecondPress_IgnoresTeammatesBeyondTheConfiguredDistance()
        {
            MatchEngine e = Setup(3, out _, out MatchPlayer mate);
            e.TestPlace(mate, new Vec2(-40f, 8f));
            e.SetInput(0, new MatchInput { SecondPress = true });
            e.Tick();
            Assert.That(e.Teams[0].SecondPresser, Is.Null, $"nadie a menos de {e.Config.Player.SecondPressMaxDistance} m");
        }

        [Test]
        public void SecondPress_CostsExtraStamina()
        {
            float withPress = StaminaAfterPressing(true);
            float without = StaminaAfterPressing(false);
            Assert.That(withPress, Is.LessThan(without), "presionar por la segunda marca cansa más");
        }

        private static float StaminaAfterPressing(bool press)
        {
            MatchEngine e = Setup(4, out _, out MatchPlayer mate);
            for (int t = 0; t < 3 * OneSecond; t++)
            {
                e.SetInput(0, new MatchInput { SecondPress = press });
                e.Tick();
            }

            return mate.Stamina;
        }

        [Test]
        public void SlidePressedWhileGettingUp_IsKeptForAMoment()
        {
            Assert.That(SlidesAfterRecovering(0.1f), Is.True, "levantándose 0,1 s: la barrida guardada se ejecuta");
            Assert.That(SlidesAfterRecovering(0.4f), Is.False, "pasado el buffer, la barrida se descarta");
        }

        private static bool SlidesAfterRecovering(float recover)
        {
            MatchEngine e = Setup(5, out MatchPlayer controlled, out _);
            controlled.RecoverTimer = recover;
            e.SetInput(0, new MatchInput { Slide = true });
            for (int t = 0; t < OneSecond; t++)
            {
                e.Tick();
                if (controlled.SlideTimer > 0f)
                {
                    return true;
                }

                e.SetInput(0, new MatchInput());
            }

            return false;
        }
    }
}
