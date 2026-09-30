using System;
using System.Collections.Generic;
using System.Linq;
using HexPortal.Core;
using NUnit.Framework;

namespace HexPortal.Tests
{
    // Deterministic RNG used by all Core randomness (core-engine rules).
    public class RngTests
    {
        static ulong[] Take(Rng rng, int n)
        {
            var r = new ulong[n];
            for (int i = 0; i < n; i++) r[i] = rng.NextULong();
            return r;
        }

        [Test]
        public void Rng_SameSeedSameSequence()
        {
            Assert.That(Take(new Rng(42), 100), Is.EqualTo(Take(new Rng(42), 100)));
            Assert.That(Take(new Rng(42), 100), Is.Not.EqualTo(Take(new Rng(43), 100)));

            var a = new Rng(7);
            var b = new Rng(7);
            for (int i = 0; i < 1000; i++) Assert.That(a.NextInt(1000), Is.EqualTo(b.NextInt(1000)));
        }

        [Test]
        public void Rng_NextIntStaysInRange()
        {
            var rng = new Rng(1);
            for (int i = 0; i < 10000; i++)
            {
                Assert.That(rng.NextInt(7), Is.InRange(0, 6));
                Assert.That(rng.NextInt(1), Is.EqualTo(0));
                Assert.That(rng.NextInt(3, 6), Is.InRange(3, 5));
                Assert.That(rng.NextInt(-2, -1), Is.EqualTo(-2));
                Assert.That(rng.NextInt(int.MinValue, int.MaxValue), Is.LessThan(int.MaxValue));
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(-5));
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(5, 5));
        }

        [Test]
        public void Rng_CopyContinuesIdentically()
        {
            var a = new Rng(99);
            Take(a, 17);
            var b = a.Clone();
            Assert.That(Take(b, 50), Is.EqualTo(Take(a, 50)));
        }

        [Test]
        public void Rng_ZeroSeedWorks()
        {
            var values = Take(new Rng(0), 100);
            Assert.That(values, Is.Unique);
            Assert.That(values, Has.None.EqualTo(0UL));
        }

        [Test]
        public void Rng_NextIntIsRoughlyUniform()
        {
            var rng = new Rng(12345);
            var buckets = new int[6];
            for (int i = 0; i < 60000; i++) buckets[rng.NextInt(6)]++;
            foreach (var n in buckets) Assert.That(n, Is.InRange(9500, 10500));
        }

        [Test]
        public void Rng_ShuffleIsDeterministicPermutation()
        {
            var a = Enumerable.Range(0, 20).ToList();
            var b = Enumerable.Range(0, 20).ToList();
            new Rng(5).Shuffle(a);
            new Rng(5).Shuffle(b);
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a, Is.EquivalentTo(Enumerable.Range(0, 20)));
            Assert.That(a, Is.Not.EqualTo(Enumerable.Range(0, 20)));
        }
    }
}
