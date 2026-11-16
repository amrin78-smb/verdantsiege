using System;

namespace VerdantSiege.Utilities
{
    /// <summary>
    /// Small deterministic PRNG (xorshift128+ variant). Identical sequences on every platform and runtime
    /// (System.Random's algorithm is not guaranteed across .NET versions/IL2CPP), so a supplied seed always
    /// replays the same encounter. Value type state; copy semantics are intentional for snapshots.
    /// </summary>
    [Serializable]
    public struct SeededRandom
    {
        private ulong _s0;
        private ulong _s1;

        public SeededRandom(int seed)
        {
            // SplitMix64 to expand the seed into two non-zero state words.
            ulong z = unchecked((ulong)seed + 0x9E3779B97F4A7C15UL);
            _s0 = SplitMix(ref z);
            _s1 = SplitMix(ref z);
            if (_s0 == 0 && _s1 == 0) _s1 = 1;
        }

        private static ulong SplitMix(ref ulong z)
        {
            z = unchecked(z + 0x9E3779B97F4A7C15UL);
            ulong r = z;
            r = unchecked((r ^ (r >> 30)) * 0xBF58476D1CE4E5B9UL);
            r = unchecked((r ^ (r >> 27)) * 0x94D049BB133111EBUL);
            return r ^ (r >> 31);
        }

        public ulong NextULong()
        {
            ulong s1 = _s0;
            ulong s0 = _s1;
            _s0 = s0;
            s1 ^= s1 << 23;
            _s1 = s1 ^ s0 ^ (s1 >> 17) ^ (s0 >> 26);
            return unchecked(_s1 + s0);
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float NextFloat() => (NextULong() >> 40) * (1f / (1UL << 24));

        /// <summary>Uniform float in [min, max).</summary>
        public float Range(float min, float max) => min + (max - min) * NextFloat();

        /// <summary>Uniform int in [min, maxExclusive).</summary>
        public int Range(int min, int maxExclusive)
        {
            if (maxExclusive <= min) return min;
            return min + (int)(NextULong() % (ulong)(maxExclusive - min));
        }
    }
}
