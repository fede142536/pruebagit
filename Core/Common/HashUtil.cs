namespace Football.Core.Common
{
    /// <summary>
    /// Funciones de hash estables. No se usa <c>string.GetHashCode</c> porque cambia entre ejecuciones y plataformas.
    /// </summary>
    public static class HashUtil
    {
        private const ulong FnvOffsetBasis = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;
        private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;
        private const ulong MixMultiplierA = 0xBF58476D1CE4E5B9UL;
        private const ulong MixMultiplierB = 0x94D049BB133111EBUL;

        /// <summary>Hash FNV-1a de 64 bits sobre los caracteres UTF-16 del texto.</summary>
        /// <param name="text">Texto a procesar.</param>
        /// <returns>Hash estable para el mismo texto en cualquier plataforma.</returns>
        public static ulong Fnv1a64(string text)
        {
            ulong hash = FnvOffsetBasis;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= (byte)(text[i] & 0xFF);
                hash *= FnvPrime;
                hash ^= (byte)(text[i] >> 8);
                hash *= FnvPrime;
            }

            return hash;
        }

        /// <summary>Avanza el estado de SplitMix64 y devuelve el siguiente valor.</summary>
        /// <param name="state">Estado, que se modifica.</param>
        public static ulong SplitMix64Next(ref ulong state)
        {
            state += GoldenGamma;
            return Finalize(state);
        }

        /// <summary>Mezcla un valor de 64 bits con el finalizador de SplitMix64 (buena difusión de bits).</summary>
        /// <param name="value">Valor a mezclar.</param>
        public static ulong SplitMix64Mix(ulong value) => Finalize(value + GoldenGamma);

        private static ulong Finalize(ulong z)
        {
            z = (z ^ (z >> 30)) * MixMultiplierA;
            z = (z ^ (z >> 27)) * MixMultiplierB;
            return z ^ (z >> 31);
        }
    }
}
