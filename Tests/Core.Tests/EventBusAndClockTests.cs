using System;
using System.Collections.Generic;
using Football.Core.Common;
using NUnit.Framework;

namespace Football.Core.Tests
{
    public class EventBusAndClockTests
    {
        private struct Goal { public int Minute; }
        private struct Card { public int Minute; }

        [Test]
        public void Publish_DeliversToSubscribersOfThatTypeInOrder()
        {
            var bus = new EventBus();
            var log = new List<string>();
            bus.Subscribe<Goal>(g => log.Add("first:" + g.Minute));
            bus.Subscribe<Goal>(g => log.Add("second:" + g.Minute));
            bus.Subscribe<Card>(c => log.Add("card"));

            bus.Publish(new Goal { Minute = 12 });
            bus.Publish(new Card { Minute = 3 });

            Assert.That(log, Is.EqualTo(new[] { "first:12", "second:12", "card" }));
        }

        [Test]
        public void Unsubscribe_StopsDelivery()
        {
            var bus = new EventBus();
            int calls = 0;
            Action<Goal> handler = _ => calls++;
            bus.Subscribe(handler);
            bus.Publish(new Goal());
            bus.Unsubscribe(handler);
            bus.Publish(new Goal());

            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void Unsubscribe_UnknownHandlerIsIgnored()
        {
            var bus = new EventBus();
            Assert.DoesNotThrow(() => bus.Unsubscribe<Goal>(_ => { }));
        }

        [Test]
        public void Publish_WithoutSubscribersIsSafe()
        {
            Assert.DoesNotThrow(() => new EventBus().Publish(new Goal()));
        }

        [Test]
        public void SubscribingDuringPublish_DoesNotAffectCurrentDelivery()
        {
            var bus = new EventBus();
            int late = 0;
            bus.Subscribe<Goal>(_ => bus.Subscribe<Goal>(__ => late++));
            bus.Publish(new Goal());

            Assert.That(late, Is.Zero);
        }

        [Test]
        public void Publish_DoesNotAllocate()
        {
            var bus = new EventBus();
            int sum = 0;
            bus.Subscribe<Goal>(g => sum += g.Minute);
            var evt = new Goal { Minute = 1 };
            bus.Publish(evt);

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++)
            {
                bus.Publish(evt);
            }

            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
            Assert.That(sum, Is.EqualTo(10001));
        }

        [Test]
        public void SimClock_AdvancesAndNeverGoesBack()
        {
            var clock = new SimClock(new DateTime(2030, 7, 1, 15, 30, 0));
            Assert.That(clock.Today, Is.EqualTo(new DateTime(2030, 7, 1)));
            clock.AdvanceDays(31);
            Assert.That(clock.Today, Is.EqualTo(new DateTime(2030, 8, 1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.AdvanceDays(-1));
        }
    }
}
