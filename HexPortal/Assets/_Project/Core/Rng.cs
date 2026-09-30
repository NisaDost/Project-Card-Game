using System;
using System.Collections.Generic;

namespace HexPortal.Core
{
    /// <summary>Deterministic RNG: xorshift64*, state seeded with SplitMix64.</summary>
    public sealed class Rng
    {
        const ulong NonZeroState = 0x9E3779B97F4A7C15UL;
        ulong state;

        public Rng(ulong seed)
        {
            ulong z = unchecked(seed + 0x9E3779B97F4A7C15UL);
            z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
            z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
            z ^= z >> 31;
            state = z == 0 ? NonZeroState : z;
        }

        Rng(Rng other) { state = other.state; }

        /// <summary>An independent copy that continues with the same sequence.</summary>
        public Rng Clone() => new Rng(this);

        public ulong NextULong()
        {
            state ^= state >> 12;
            state ^= state << 25;
            state ^= state >> 27;
            return unchecked(state * 0x2545F4914F6CDD1DUL);
        }

        /// <summary>Uniform in [0, maxExclusive).</summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return (int)NextBelow((ulong)maxExclusive);
        }

        /// <summary>Uniform in [minInclusive, maxExclusive).</summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return (int)(minInclusive + (long)NextBelow((ulong)((long)maxExclusive - minInclusive)));
        }

        /// <summary>Fisher–Yates.</summary>
        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = NextInt(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        // Unbiased: reject the lowest (2^64 mod range) values, then reduce.
        ulong NextBelow(ulong range)
        {
            ulong threshold = unchecked(0UL - range) % range;
            while (true)
            {
                ulong r = NextULong();
                if (r >= threshold) return r % range;
            }
        }
    }
}
