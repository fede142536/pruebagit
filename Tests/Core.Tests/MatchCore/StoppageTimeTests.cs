using System.Linq;
using Football.Core.MatchCore;
using NUnit.Framework;

namespace Football.Core.Tests.MatchCore
{
    /// <summary>Tiempo agregado: 45 minutos por parte (también en el 3 contra 3) y minutos extra según lo que pasó en la parte.</summary>
    public class StoppageTimeTests
    {
        [Test]
        public void Indoor3v3_PlaysTwoHalvesOf45Minutes_LikeTheBigPitch()
        {
            Assert.That(MatchConfig.CreateIndoor().Rules.MinutesPerHalf, Is.EqualTo(45f));
            Assert.That(new MatchConfig().Rules.MinutesPerHalf, Is.EqualTo(45f));
        }

        [Test]
        public void EachHalf_AnnouncesAtLeastOneMinute_AndNeverMoreThanTheMaximum()
        {
            for (ulong seed = 1; seed <= 6; seed++)
            {
                MatchEngine e = MatchEngineTests.NewEngine(seed);
                int firstHalfAdded = -1, secondHalfAdded = -1;
                int guard = 0;
                while (!e.IsFinished && guard++ < 200000)
                {
                    e.Tick();
                    if (e.Half == 1 && e.AddedMinutes > 0) firstHalfAdded = e.AddedMinutes;
                    if (e.Half == 2 && e.AddedMinutes > 0) secondHalfAdded = e.AddedMinutes;
                }

                RulesConfig r = e.Config.Rules;
                Assert.That(firstHalfAdded, Is.InRange(r.MinAddedMinutes, r.MaxAddedMinutes), "semilla " + seed + ": primera parte");
                Assert.That(secondHalfAdded, Is.InRange(r.MinAddedMinutes, r.MaxAddedMinutes), "semilla " + seed + ": segunda parte");
                Assert.That(e.FirstHalfAdded, Is.EqualTo(firstHalfAdded));
            }
        }

        [Test]
        public void TheClockKeepsCountingPastTheRegulationTime_AndTheMatchEndsAfterTheAddedMinutes()
        {
            MatchEngine e = MatchEngineTests.NewEngine(5);
            float maxFirst = 0f;
            while (!e.IsFinished)
            {
                e.Tick();
                if (e.Half == 1) maxFirst = System.Math.Max(maxFirst, e.Minute);
            }

            float perHalf = e.Config.Rules.MinutesPerHalf;
            Assert.That(maxFirst, Is.GreaterThan(perHalf), "la primera parte pasó los 45 antes del descanso");
            Assert.That(maxFirst, Is.LessThanOrEqualTo(perHalf + e.FirstHalfAdded + 0.05f));
            Assert.That(e.Minute, Is.GreaterThan(2f * perHalf), "el partido terminó pasados los 90");
            Assert.That(e.Minute, Is.LessThanOrEqualTo(2f * perHalf + e.AddedMinutes + 0.05f));
            Assert.That(e.PlayedMinute, Is.EqualTo(2f * perHalf + e.FirstHalfAdded + e.AddedMinutes).Within(0.1f), "minutos jugados en total");
        }

        [Test]
        public void MoreHappeningsInTheHalf_MeanMoreAddedTime()
        {
            // Con la misma duración, una parte con muchos hechos suma más que una tranquila (se compara el total de dos grupos de semillas).
            int Added(MatchEngine e)
            {
                int guard = 0;
                while (!e.IsFinished && guard++ < 200000) e.Tick();
                return e.FirstHalfAdded + e.AddedMinutes;
            }

            var calm = new MatchConfig();
            calm.Rules.StoppagePerGoal = calm.Rules.StoppagePerSubstitution = calm.Rules.StoppagePerInjury = 0f;
            calm.Rules.StoppagePerRedCard = calm.Rules.StoppagePerYellowCard = calm.Rules.StoppagePerPenalty = 0f;
            var busy = new MatchConfig();
            busy.Rules.StoppagePerGoal = busy.Rules.StoppagePerYellowCard = 2f;
            int calmTotal = 0, busyTotal = 0;
            for (ulong seed = 1; seed <= 5; seed++)
            {
                calmTotal += Added(MatchEngineTests.NewEngine(seed, cfg: calm));
                busyTotal += Added(MatchEngineTests.NewEngine(seed, cfg: busy));
            }

            Assert.That(calmTotal, Is.EqualTo(10 * calm.Rules.MinAddedMinutes), "sin hechos, el mínimo en cada parte");
            Assert.That(busyTotal, Is.GreaterThan(calmTotal));
        }

        [Test]
        public void EventsRecordTheirHalf_SoTheLabelCanBeWritten()
        {
            MatchEngine e = MatchEngineTests.NewEngine(9);
            MatchEngineTests.RunToEnd(e);
            Assert.That(e.Events.Count, Is.GreaterThan(0));
            for (int i = 0; i < e.Events.Count; i++)
            {
                Assert.That(e.Events[i].Half, Is.InRange(1, 2));
            }
        }

        [Test]
        public void TheEngine_IsStillDeterministic_WithAddedTime()
        {
            MatchEngine a = MatchEngineTests.NewEngine(31), b = MatchEngineTests.NewEngine(31);
            MatchEngineTests.RunToEnd(a);
            MatchEngineTests.RunToEnd(b);
            Assert.That(a.StateHash(), Is.EqualTo(b.StateHash()));
            Assert.That(a.AddedMinutes, Is.EqualTo(b.AddedMinutes));
            Assert.That(a.FirstHalfAdded, Is.EqualTo(b.FirstHalfAdded));
        }
    }
}
