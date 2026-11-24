using System;
using Football.Core.MatchCore;
using NUnit.Framework;

namespace Football.Core.Tests.MatchCore
{
    /// <summary>El motor no genera basura durante el juego (regla: cero asignaciones por fotograma): lo que sube en el celular viene de la capa Unity.</summary>
    public class AllocationTests
    {
        [Test]
        public void TickAndSnapshotCopy_AllocateAlmostNothing()
        {
            MatchEngine e = MatchEngineTests.NewEngine(3);
            e.TestBeginOpenPlay();
            var snap = new MatchSnapshot();
            for (int i = 0; i < 600; i++)
            {
                e.Tick();
                snap.CopyFrom(e); // calentamiento
            }

            const int ticks = 60 * 30;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < ticks; i++)
            {
                e.Tick();
                snap.CopyFrom(e);
            }

            double perTick = (GC.GetAllocatedBytesForCurrentThread() - before) / (double)ticks;
            Assert.That(perTick, Is.LessThan(64.0), "bytes por paso del motor y copia de la instantánea");
        }
    }
}
