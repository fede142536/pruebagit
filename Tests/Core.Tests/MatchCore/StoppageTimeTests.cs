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
            float tolerance = e.Config.Rules.EndPlayHardMinutes + 0.05f; // la jugada en curso se termina de jugar
            Assert.That(maxFirst, Is.GreaterThanOrEqualTo(perHalf + e.FirstHalfAdded - 0.05f), "se juega todo el tiempo agregado");
            Assert.That(maxFirst, Is.LessThanOrEqualTo(perHalf + e.FirstHalfAdded + tolerance));
            Assert.That(e.Minute, Is.GreaterThan(2f * perHalf), "el partido terminó pasados los 90");
            Assert.That(e.Minute, Is.GreaterThanOrEqualTo(2f * perHalf + e.AddedMinutes - 0.05f));
            Assert.That(e.Minute, Is.LessThanOrEqualTo(2f * perHalf + e.AddedMinutes + tolerance));
            Assert.That(e.PlayedMinute, Is.GreaterThanOrEqualTo(2f * perHalf + e.FirstHalfAdded + e.AddedMinutes - 0.1f), "minutos jugados en total");
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

        [Test]
        public void TheClockDoesNotRun_WhileTheKickoffIsPending_AndStartsWithTheFirstTouch()
        {
            MatchEngine e = MatchEngineTests.NewEngine(11);
            e.SetHuman(0, true); // la persona saca del medio: el motor espera su pase o el tiempo límite
            int waited = 0;
            for (int t = 0; t < 3000 && e.HalfElapsed <= 0f; t++)
            {
                e.Tick();
                if (e.Phase == MatchPhase.SetPiece && e.CurrentRestart.Kind == RestartKind.KickOff)
                {
                    waited++;
                    Assert.That(e.HalfElapsed, Is.EqualTo(0f), "el reloj no corre antes del puntapié inicial");
                }
            }

            Assert.That(waited, Is.GreaterThan(e.Config.TickRate), "hubo espera real antes del saque");
            Assert.That(e.HalfElapsed, Is.GreaterThan(0f), "después del saque el reloj corre");
        }

        [Test]
        public void AfterTheAddedTime_TheHalfEndsOnlyWhenThePlayHasFinished()
        {
            int halves = 0, goalEnds = 0, safetyEnds = 0;
            for (ulong seed = 1; seed <= 14; seed++)
            {
                MatchEngine e = MatchEngineTests.NewEngine(seed);
                int lastHalf = 1;
                bool lastFinished = false;
                float perMinute = e.Config.Rules.HalfDuration / e.Config.Rules.MinutesPerHalf;
                int guard = 0;
                while (!e.IsFinished && guard++ < 200000)
                {
                    e.Tick();
                    bool ended = e.Half != lastHalf || (e.IsFinished && !lastFinished);
                    lastHalf = e.Half;
                    lastFinished = e.IsFinished;
                    if (!ended)
                    {
                        continue;
                    }

                    halves++;
                    Assert.That(e.LastHalfEndReason, Is.Not.EqualTo(HalfEndReason.None), "semilla " + seed);
                    goalEnds += e.LastHalfEndReason == HalfEndReason.Goal ? 1 : 0;
                    safetyEnds += e.LastHalfEndReason == HalfEndReason.Safety ? 1 : 0;
                }
            }

            Assert.That(halves, Is.EqualTo(28));
            Assert.That(safetyEnds, Is.LessThanOrEqualTo(2), "casi siempre la parte termina con el balón parado, no por el seguro");
            Assert.That(goalEnds, Is.LessThanOrEqualTo(halves), "los goles en el descuento cierran la parte después de su pausa");
        }
    }
}
