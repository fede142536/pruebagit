using System;
using Football.Core.Common;
using NUnit.Framework;

namespace Football.Core.Tests
{
    public class RandomTests
    {
        [Test]
        public void SplitMix64_MatchesPublishedVectors()
        {
            ulong state = 0;
            Assert.That(HashUtil.SplitMix64Next(ref state), Is.EqualTo(0xE220A8397B1DCDAFUL));
            Assert.That(HashUtil.SplitMix64Next(ref state), Is.EqualTo(0x6E789E6AA1B965F4UL));
            Assert.That(HashUtil.SplitMix64Next(ref state), Is.EqualTo(0x06C45D188009454FUL));
        }

        [Test]
        public void SeededRandom_MatchesReferenceImplementation()
        {
            // Valores calculados con una implementación independiente de xoshiro256** en Python.
            var rng = new SeededRandom(12345);
            ulong[] expected =
            {
                13720838825685603483UL, 2398916695208396998UL, 17770384849984869256UL,
                891717726879801395UL, 10241316046318454344UL
            };
            foreach (ulong value in expected)
            {
                Assert.That(rng.NextULong(), Is.EqualTo(value));
            }
        }

        [Test]
        public void Fnv1a64_IsStable()
        {
            Assert.That(HashUtil.Fnv1a64("abc"), Is.EqualTo(14899682270663221853UL));
        }

        [Test]
        public void SameSeed_ProducesSameSequence()
        {
            var a = new SeededRandom(42);
            var b = new SeededRandom(42);
            for (int i = 0; i < 1000; i++)
            {
                Assert.That(a.NextULong(), Is.EqualTo(b.NextULong()));
            }
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentSequences()
        {
            Assert.That(new SeededRandom(1).NextULong(), Is.Not.EqualTo(new SeededRandom(2).NextULong()));
        }

        [Test]
        public void Fork_MatchesReferenceImplementation()
        {
            IRandom fork = new SeededRandom(12345).Fork("transfers");
            Assert.That(fork.Seed, Is.EqualTo(9479007067598252233UL));
            Assert.That(fork.NextULong(), Is.EqualTo(12729811792715095912UL));
        }

        [Test]
        public void Fork_DoesNotDependOnConsumedRandomness()
        {
            var untouched = new SeededRandom(7);
            var consumed = new SeededRandom(7);
            for (int i = 0; i < 500; i++)
            {
                consumed.NextULong();
            }

            Assert.That(consumed.Fork("matches").NextULong(), Is.EqualTo(untouched.Fork("matches").NextULong()));
        }

        [Test]
        public void Fork_DifferentStreamsAreIndependent()
        {
            var root = new SeededRandom(7);
            Assert.That(root.Fork("a").NextULong(), Is.Not.EqualTo(root.Fork("b").NextULong()));
        }

        [Test]
        public void NextInt_StaysInRangeAndCoversIt()
        {
            var rng = new SeededRandom(3);
            var seen = new bool[6];
            for (int i = 0; i < 5000; i++)
            {
                int v = rng.NextInt(-2, 4);
                Assert.That(v, Is.InRange(-2, 3));
                seen[v + 2] = true;
            }

            Assert.That(seen, Is.All.True);
        }

        [Test]
        public void NextInt_IsUnbiased()
        {
            var rng = new SeededRandom(11);
            var counts = new int[3];
            const int draws = 30000;
            for (int i = 0; i < draws; i++)
            {
                counts[rng.NextInt(0, 3)]++;
            }

            foreach (int c in counts)
            {
                Assert.That(c, Is.EqualTo(draws / 3).Within(draws * 0.02));
            }
        }

        [Test]
        public void NextInt_RejectsEmptyRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SeededRandom(1).NextInt(5, 5));
        }

        [Test]
        public void NextDoubleAndFloat_AreInUnitInterval_WithUniformMean()
        {
            var rng = new SeededRandom(5);
            double sumD = 0, sumF = 0;
            const int n = 20000;
            for (int i = 0; i < n; i++)
            {
                double d = rng.NextDouble();
                float f = rng.NextFloat();
                Assert.That(d, Is.GreaterThanOrEqualTo(0.0).And.LessThan(1.0));
                Assert.That(f, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
                sumD += d;
                sumF += f;
            }

            Assert.That(sumD / n, Is.EqualTo(0.5).Within(0.01));
            Assert.That(sumF / n, Is.EqualTo(0.5).Within(0.01));
        }

        [Test]
        public void NextGaussian_HasRequestedMeanAndDeviation()
        {
            var rng = new SeededRandom(9);
            const int n = 40000;
            double sum = 0, sumSq = 0;
            for (int i = 0; i < n; i++)
            {
                double v = rng.NextGaussian(10f, 2f);
                sum += v;
                sumSq += v * v;
            }

            double mean = sum / n;
            double sd = Math.Sqrt(sumSq / n - mean * mean);
            Assert.That(mean, Is.EqualTo(10.0).Within(0.05));
            Assert.That(sd, Is.EqualTo(2.0).Within(0.05));
        }

        [Test]
        public void Chance_RespectsProbability()
        {
            var rng = new SeededRandom(13);
            int hits = 0;
            const int n = 20000;
            for (int i = 0; i < n; i++)
            {
                if (rng.Chance(0.25f))
                {
                    hits++;
                }
            }

            Assert.That(hits / (double)n, Is.EqualTo(0.25).Within(0.01));
            Assert.That(rng.Chance(0f), Is.False);
            Assert.That(rng.Chance(1f), Is.True);
        }

        [Test]
        public void NextULong_DoesNotAllocate()
        {
            var rng = new SeededRandom(1);
            rng.NextULong();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++)
            {
                rng.NextULong();
                rng.NextFloat();
                rng.NextInt(0, 10);
            }

            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
        }
    }
}
