using System;

namespace Football.Core.Common
{
    /// <summary>
    /// Generador xoshiro256** sembrado con SplitMix64. Rápido, sin allocations y reproducible
    /// para una misma semilla dentro de la misma plataforma y build.
    /// </summary>
    public sealed class SeededRandom : IRandom
    {
        private const double DoubleScale = 1.0 / 9007199254740992.0; // 2^-53
        private const float FloatScale = 1f / 16777216f;             // 2^-24
        private const int DoubleShift = 11;                          // 64 - 53 bits
        private const int FloatShift = 40;                           // 64 - 24 bits

        private ulong _s0;
        private ulong _s1;
        private ulong _s2;
        private ulong _s3;

        /// <inheritdoc />
        public ulong Seed { get; }

        /// <summary>Crea un generador con la semilla dada.</summary>
        /// <param name="seed">Semilla; cualquier valor es válido, incluido 0.</param>
        public SeededRandom(ulong seed)
        {
            Seed = seed;
            ulong state = seed;
            _s0 = HashUtil.SplitMix64Next(ref state);
            _s1 = HashUtil.SplitMix64Next(ref state);
            _s2 = HashUtil.SplitMix64Next(ref state);
            _s3 = HashUtil.SplitMix64Next(ref state);
        }

        /// <inheritdoc />
        public ulong NextULong()
        {
            ulong result = RotateLeft(_s1 * 5UL, 7) * 9UL;
            ulong t = _s1 << 17;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = RotateLeft(_s3, 45);
            return result;
        }

        /// <inheritdoc />
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), "El máximo debe ser mayor que el mínimo.");
            }

            ulong range = (ulong)((long)maxExclusive - minInclusive);
            // Rechazo de los valores que crearían sesgo de módulo.
            ulong threshold = (0UL - range) % range;
            while (true)
            {
                ulong r = NextULong();
                if (r >= threshold)
                {
                    return (int)(minInclusive + (long)(r % range));
                }
            }
        }

        /// <inheritdoc />
        public double NextDouble() => (NextULong() >> DoubleShift) * DoubleScale;

        /// <inheritdoc />
        public float NextFloat() => (NextULong() >> FloatShift) * FloatScale;

        /// <inheritdoc />
        public float NextGaussian(float mean, float standardDeviation)
        {
            if (standardDeviation < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(standardDeviation), "La desviación no puede ser negativa.");
            }

            double u1 = 1.0 - NextDouble(); // (0, 1]: evita log(0)
            double u2 = NextDouble();
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            return mean + standardDeviation * (float)z;
        }

        /// <inheritdoc />
        public bool Chance(float probability) => NextFloat() < probability;

        /// <inheritdoc />
        public IRandom Fork(string stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            return new SeededRandom(HashUtil.SplitMix64Mix(Seed ^ HashUtil.Fnv1a64(stream)));
        }

        private static ulong RotateLeft(ulong value, int bits) => (value << bits) | (value >> (64 - bits));
    }
}
