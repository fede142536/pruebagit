using System;

namespace Football.Game
{
    /// <summary>
    /// Píxeles del atlas de decales de las camisetas (sin Unity, para poder probarlos): 256×256 de un solo canal (R8, 64 KB) en una
    /// grilla de 4×4 celdas de 64 px. Celdas 0–9: los dígitos; celdas 10–15: seis siluetas de escudo o patrocinador. Cada celda
    /// guarda cobertura (255 = tinta) con los bordes suavizados; el shader la endurece con un umbral. La fila 0 es la de abajo.
    /// </summary>
    public static class PlayerAtlasData
    {
        /// <summary>Lado del atlas en píxeles.</summary>
        public const int Size = 256;
        /// <summary>Lado de cada celda en píxeles.</summary>
        public const int Cell = 64;
        /// <summary>Celdas por fila.</summary>
        public const int Columns = Size / Cell;
        /// <summary>Primera celda de escudos.</summary>
        public const int FirstCrest = 10;
        /// <summary>Cantidad de variantes de escudo.</summary>
        public const int CrestCount = 6;

        private const int GlyphWidth = 5, GlyphHeight = 7, GlyphScale = 8;

        // Dígitos 5×7, de arriba hacia abajo.
        private static readonly string[][] Digits =
        {
            new[] { "01110", "10001", "10011", "10101", "11001", "10001", "01110" },
            new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" },
            new[] { "01110", "10001", "00001", "00010", "00100", "01000", "11111" },
            new[] { "11110", "00001", "00001", "01110", "00001", "00001", "11110" },
            new[] { "00010", "00110", "01010", "10010", "11111", "00010", "00010" },
            new[] { "11111", "10000", "11110", "00001", "00001", "10001", "01110" },
            new[] { "00110", "01000", "10000", "11110", "10001", "10001", "01110" },
            new[] { "11111", "00001", "00010", "00100", "01000", "01000", "01000" },
            new[] { "01110", "10001", "10001", "01110", "10001", "10001", "01110" },
            new[] { "01110", "10001", "10001", "01111", "00001", "00010", "01100" }
        };

        /// <summary>Arma los píxeles del atlas.</summary>
        public static byte[] Build()
        {
            var pixels = new byte[Size * Size];
            for (int d = 0; d < 10; d++)
            {
                DrawDigit(pixels, d);
            }

            for (int c = 0; c < CrestCount; c++)
            {
                DrawCrest(pixels, FirstCrest + c, c);
            }

            for (int cell = 0; cell < Columns * Columns; cell++)
            {
                Blur(pixels, cell);
                Blur(pixels, cell);
            }

            return pixels;
        }

        /// <summary>Cobertura (0–255) de un punto de una celda, con (0, 0) abajo a la izquierda.</summary>
        public static byte Sample(byte[] pixels, int cell, int x, int y) => pixels[(cell / Columns * Cell + y) * Size + cell % Columns * Cell + x];

        private static void Put(byte[] pixels, int cell, int x, int y, byte value)
        {
            pixels[(cell / Columns * Cell + y) * Size + cell % Columns * Cell + x] = value;
        }

        private static void DrawDigit(byte[] pixels, int digit)
        {
            int ox = (Cell - GlyphWidth * GlyphScale) / 2, oy = (Cell - GlyphHeight * GlyphScale) / 2;
            for (int row = 0; row < GlyphHeight; row++)
            {
                for (int col = 0; col < GlyphWidth; col++)
                {
                    if (Digits[digit][row][col] != '1')
                    {
                        continue;
                    }

                    for (int dy = 0; dy < GlyphScale; dy++)
                    {
                        for (int dx = 0; dx < GlyphScale; dx++)
                        {
                            Put(pixels, digit, ox + col * GlyphScale + dx, oy + (GlyphHeight - 1 - row) * GlyphScale + dy, 255);
                        }
                    }
                }
            }
        }

        // Siluetas en coordenadas u (−1..1 de izquierda a derecha) y v (0 abajo, 1 arriba).
        private static bool Inside(int variant, float u, float v)
        {
            float au = Math.Abs(u);
            switch (variant)
            {
                case 0: return Shield(au, v);                                                 // escudo clásico
                case 1: return Shield(au, v) && !(v > 0.5f && v < 0.64f);                     // escudo con franja
                case 2: { float r = (float)Math.Sqrt(u * u + (v - 0.5f) * (v - 0.5f) * 4f); return r < 0.92f && r > 0.5f || r < 0.28f; } // insignia redonda
                case 3: return Shield(au, v) && au > 0.16f;                                   // escudo con tajo central
                case 4: return au + Math.Abs(v - 0.5f) * 1.5f < 0.95f;                        // rombo
                default: return Math.Abs(v - 0.5f) < 0.2f && au < 0.92f;                      // barra de patrocinador
            }
        }

        private static bool Shield(float au, float v)
        {
            if (v < 0.04f || v > 0.96f)
            {
                return false;
            }

            float half = v > 0.45f ? 0.84f : 0.84f * (float)Math.Sqrt((v - 0.04f) / 0.41f);
            return au < half;
        }

        private static void DrawCrest(byte[] pixels, int cell, int variant)
        {
            for (int y = 0; y < Cell; y++)
            {
                for (int x = 0; x < Cell; x++)
                {
                    float u = (x + 0.5f) / Cell * 2f - 1f, v = (y + 0.5f) / Cell;
                    if (Inside(variant, u, v))
                    {
                        Put(pixels, cell, x, y, 255);
                    }
                }
            }
        }

        private static void Blur(byte[] pixels, int cell)
        {
            var copy = new byte[Cell * Cell];
            for (int y = 0; y < Cell; y++)
            {
                for (int x = 0; x < Cell; x++)
                {
                    copy[y * Cell + x] = Sample(pixels, cell, x, y);
                }
            }

            for (int y = 0; y < Cell; y++)
            {
                for (int x = 0; x < Cell; x++)
                {
                    int sum = 0;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int sx = Math.Min(Cell - 1, Math.Max(0, x + dx)), sy = Math.Min(Cell - 1, Math.Max(0, y + dy));
                            sum += copy[sy * Cell + sx];
                        }
                    }

                    Put(pixels, cell, x, y, (byte)(sum / 9));
                }
            }
        }
    }
}
