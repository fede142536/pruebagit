namespace Football.Core.Common
{
    /// <summary>
    /// Fuente de azar determinista. Todo el azar de la lógica de juego debe pasar por esta interfaz
    /// (prohibido <c>System.Random</c> y <c>UnityEngine.Random</c>).
    /// </summary>
    public interface IRandom
    {
        /// <summary>Semilla con la que se creó este generador (identifica su flujo).</summary>
        ulong Seed { get; }

        /// <summary>Siguiente valor de 64 bits uniforme.</summary>
        ulong NextULong();

        /// <summary>Entero uniforme sin sesgo en <c>[minInclusive, maxExclusive)</c>.</summary>
        /// <param name="minInclusive">Límite inferior incluido.</param>
        /// <param name="maxExclusive">Límite superior excluido; debe ser mayor que el inferior.</param>
        int NextInt(int minInclusive, int maxExclusive);

        /// <summary>Número uniforme en <c>[0, 1)</c> con precisión de 53 bits.</summary>
        double NextDouble();

        /// <summary>Número uniforme en <c>[0, 1)</c> con precisión de 24 bits.</summary>
        float NextFloat();

        /// <summary>Muestra de una distribución normal (Box-Muller).</summary>
        /// <param name="mean">Media.</param>
        /// <param name="standardDeviation">Desviación típica (no negativa).</param>
        float NextGaussian(float mean, float standardDeviation);

        /// <summary>Devuelve <c>true</c> con la probabilidad indicada.</summary>
        /// <param name="probability">Probabilidad en <c>[0, 1]</c>.</param>
        bool Chance(float probability);

        /// <summary>
        /// Crea un flujo independiente y reproducible a partir del nombre. El resultado depende solo de la
        /// semilla y del nombre, no de cuánto azar se haya consumido: añadir llamadas en un subsistema no
        /// altera el azar de los demás.
        /// </summary>
        /// <param name="stream">Nombre del subsistema (p. ej. <c>"transfers"</c>).</param>
        IRandom Fork(string stream);
    }
}
