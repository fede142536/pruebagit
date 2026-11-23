using System;
using Football.Core.MatchCore;
using NUnit.Framework;

namespace Football.Core.Tests.MatchCore
{
    /// <summary>Regla 7 del proyecto: cero asignaciones por cuadro durante el partido. Se mide en el núcleo (en Unity se mide con el benchmark).</summary>
    public sealed class AllocationTests
    {
        private const int WarmupTicks = 60 * 20;
        private const int MeasuredTicks = 60 * 60;
        private const long BytesPerTickBudget = 64;  // margen para el boxing ocasional de un evento; lo normal es 0

        [Test]
        public void TheEngineAllocatesAlmostNothingPerTick()
        {
            MatchEngine e = SubstitutionTests.NewMatch(31);
            for (int i = 0; i < WarmupTicks; i++)
            {
                e.Tick();
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < MeasuredTicks && !e.IsFinished; i++)
            {
                e.Tick();
            }

            long perTick = (GC.GetAllocatedBytesForCurrentThread() - before) / MeasuredTicks;
            TestContext.Out.WriteLine("bytes por tick: " + perTick);
            Assert.That(perTick, Is.LessThanOrEqualTo(BytesPerTickBudget), "el motor no debería generar basura por tick");
        }

        [Test]
        public void TakingASnapshotAllocatesNothing()
        {
            MatchEngine e = SubstitutionTests.NewMatch(32);
            var snapshot = new MatchSnapshot();
            for (int i = 0; i < 60 * 10; i++)
            {
                e.Tick();
                snapshot.CopyFrom(e);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 6000; i++)
            {
                snapshot.CopyFrom(e);
            }

            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.LessThanOrEqualTo(1024), "CopyFrom reutiliza sus buffers");
        }
    }
}
