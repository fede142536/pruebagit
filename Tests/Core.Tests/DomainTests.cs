using System;
using Football.Core.Domain;
using NUnit.Framework;

namespace Football.Core.Tests
{
    public class DomainTests
    {
        [Test]
        public void AttributeSet_StartsAtMinimum()
        {
            var set = new AttributeSet();
            foreach (AttributeId id in AttributeSet.All)
            {
                Assert.That(set[id], Is.EqualTo(AttributeScale.Min));
            }
        }

        [Test]
        public void AttributeSet_ClampsToScale()
        {
            var set = new AttributeSet();
            set[AttributeId.Pace] = 500;
            set[AttributeId.Passing] = -3;
            Assert.That(set[AttributeId.Pace], Is.EqualTo(AttributeScale.Max));
            Assert.That(set[AttributeId.Passing], Is.EqualTo(AttributeScale.Min));
            Assert.That(new AttributeSet(1000)[AttributeId.Vision], Is.EqualTo(AttributeScale.Max));
        }

        [Test]
        public void AttributeSet_CoversEveryAttributeIndependently()
        {
            var set = new AttributeSet();
            int value = 2;
            foreach (AttributeId id in AttributeSet.All)
            {
                set[id] = value++;
            }

            value = 2;
            foreach (AttributeId id in AttributeSet.All)
            {
                Assert.That(set[id], Is.EqualTo(value++), id.ToString());
            }
        }

        [Test]
        public void AttributeSet_CloneIsIndependent()
        {
            var original = new AttributeSet(50);
            AttributeSet copy = original.Clone();
            copy[AttributeId.Pace] = 99;
            Assert.That(original[AttributeId.Pace], Is.EqualTo(50));
        }

        [Test]
        public void AttributeIds_AreUnique()
        {
            Assert.That(AttributeSet.All, Is.Unique);
        }

        [TestCase("2030-07-01", 2000, 7, 1, 30)]
        [TestCase("2030-06-30", 2000, 7, 1, 29)]
        [TestCase("2030-07-02", 2000, 7, 1, 30)]
        public void Player_AgeOn_CountsCompletedYears(string on, int y, int m, int d, int expected)
        {
            var player = new Player { BirthDate = new DateTime(y, m, d) };
            Assert.That(player.AgeOn(DateTime.Parse(on)), Is.EqualTo(expected));
        }

        [Test]
        public void Fixture_IsPlayedOnlyWithBothGoals()
        {
            Assert.That(new Fixture().IsPlayed, Is.False);
            Assert.That(new Fixture { HomeGoals = 1 }.IsPlayed, Is.False);
            Assert.That(new Fixture { HomeGoals = 1, AwayGoals = 0 }.IsPlayed, Is.True);
        }
    }
}
