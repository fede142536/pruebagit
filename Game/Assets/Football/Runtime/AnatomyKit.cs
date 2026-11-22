using System;
using System.Collections.Generic;
using UnityEngine;

namespace Football.Game
{
    /// <summary>
    /// Sección horizontal de una malla esculpida: a la altura <see cref="Y"/>, un óvalo de media anchura <see cref="Rx"/> con
    /// distinta profundidad hacia delante (<see cref="Front"/>, +Z) y hacia atrás (<see cref="Back"/>, −Z). Así se modelan
    /// pecho y espalda, cuádriceps, gemelos o mandíbula sin cajas ni esferas sueltas.
    /// </summary>
    public readonly struct Ring
    {
        /// <summary>Altura de la sección (m).</summary>
        public readonly float Y;
        /// <summary>Media anchura (m).</summary>
        public readonly float Rx;
        /// <summary>Profundidad hacia delante (m).</summary>
        public readonly float Front;
        /// <summary>Profundidad hacia atrás (m).</summary>
        public readonly float Back;

        /// <summary>Crea una sección (radio 0 = polo que cierra la malla).</summary>
        public Ring(float y, float rx, float front, float back)
        {
            Y = y;
            Rx = rx;
            Front = front;
            Back = back;
        }

        /// <summary>Polo: punto del eje que cierra la malla.</summary>
        public static Ring Pole(float y) => new Ring(y, 0f, 0f, 0f);

        /// <summary>Punto de la sección en el ángulo dado (0 = +X, π/2 = delante).</summary>
        public Vector3 Point(float angle, float inflate = 1f, float offset = 0f)
        {
            float s = Mathf.Sin(angle);
            float depth = s >= 0f ? Front : Back;
            return new Vector3(Mathf.Cos(angle) * (Rx * inflate + offset), Y, s * (depth * inflate + offset));
        }
    }

    /// <summary>
    /// Mallas anatómicas continuas (estilizadas, pocas caras): tronco en V, pelvis, muslo con cuádriceps y rótula, pierna con
    /// gemelo y tobillo fino, botín con puntera, lengüeta y suela, brazo con deltoides y bíceps, antebrazo, mano, cuello,
    /// cráneo ovalado con mandíbula, nariz, orejas y cabello. Cada malla se arma una sola vez y la comparten todos los jugadores;
    /// las normales son suaves porque los vértices de cada anillo se comparten. Medidas en metros, en el espacio de la
    /// articulación que la mueve (cuerpo, cadera, rodilla, hombro, codo o cabeza).
    /// </summary>
    public static class AnatomyKit
    {
        private const int Sides = 14;
        private const int PatchRows = 12;
        private const int PatchColumns = 5;
        private const float PatternInflate = 1.035f;
        private const float PatternOffset = 0.003f;

        /// <summary>Tronco en V (espacio del cuerpo, desde el suelo): trapecios, hombros, pectorales, cintura.</summary>
        public static readonly Ring[] TorsoRings =
        {
            Ring.Pole(1.525f), new Ring(1.515f, 0.075f, 0.055f, 0.06f), new Ring(1.485f, 0.165f, 0.085f, 0.095f),
            new Ring(1.44f, 0.215f, 0.11f, 0.105f), new Ring(1.37f, 0.212f, 0.135f, 0.115f), new Ring(1.29f, 0.198f, 0.138f, 0.115f),
            new Ring(1.2f, 0.178f, 0.122f, 0.108f), new Ring(1.1f, 0.158f, 0.108f, 0.1f), new Ring(1.0f, 0.162f, 0.104f, 0.1f),
            Ring.Pole(0.975f)
        };

        private static readonly Ring[] PelvisRings =
        {
            Ring.Pole(1.01f), new Ring(1.0f, 0.166f, 0.108f, 0.104f), new Ring(0.93f, 0.186f, 0.115f, 0.13f),
            new Ring(0.86f, 0.19f, 0.112f, 0.128f), new Ring(0.81f, 0.17f, 0.1f, 0.112f), Ring.Pole(0.79f)
        };

        private static readonly Ring[] ShortLegRings =
        {
            Ring.Pole(0.035f), new Ring(0.025f, 0.09f, 0.09f, 0.092f), new Ring(-0.08f, 0.096f, 0.098f, 0.095f),
            new Ring(-0.165f, 0.1f, 0.1f, 0.097f), Ring.Pole(-0.172f)
        };

        private static readonly Ring[] ThighRings =
        {
            Ring.Pole(0.03f), new Ring(0.0f, 0.085f, 0.085f, 0.085f), new Ring(-0.08f, 0.089f, 0.103f, 0.086f),
            new Ring(-0.18f, 0.081f, 0.092f, 0.078f), new Ring(-0.3f, 0.068f, 0.076f, 0.065f), new Ring(-0.39f, 0.056f, 0.06f, 0.055f),
            new Ring(-0.43f, 0.052f, 0.064f, 0.05f), Ring.Pole(-0.46f)
        };

        private static readonly Ring[] ShinRings =
        {
            Ring.Pole(0.02f), new Ring(0.0f, 0.052f, 0.055f, 0.05f), new Ring(-0.06f, 0.056f, 0.05f, 0.075f),
            new Ring(-0.14f, 0.054f, 0.048f, 0.071f), new Ring(-0.25f, 0.044f, 0.042f, 0.048f), new Ring(-0.35f, 0.032f, 0.033f, 0.032f),
            new Ring(-0.4f, 0.03f, 0.031f, 0.03f), Ring.Pole(-0.425f)
        };

        // Botín a lo largo de Y (talón en −Y, puntera en +Y); se gira 90° en X para que la puntera mire hacia delante y
        // «Front» quede hacia abajo (suela plana) y «Back» hacia arriba (empeine y caña).
        private static readonly Ring[] BootRings =
        {
            Ring.Pole(-0.078f), new Ring(-0.068f, 0.036f, 0.03f, 0.04f), new Ring(-0.025f, 0.043f, 0.033f, 0.072f),
            new Ring(0.035f, 0.046f, 0.033f, 0.056f), new Ring(0.1f, 0.044f, 0.031f, 0.036f), new Ring(0.15f, 0.039f, 0.029f, 0.026f),
            new Ring(0.185f, 0.027f, 0.023f, 0.016f), Ring.Pole(0.2f)
        };

        private static readonly Ring[] UpperArmRings =
        {
            Ring.Pole(0.05f), new Ring(0.04f, 0.05f, 0.05f, 0.05f), new Ring(0.0f, 0.062f, 0.06f, 0.058f),
            new Ring(-0.06f, 0.055f, 0.058f, 0.05f), new Ring(-0.12f, 0.046f, 0.054f, 0.045f), new Ring(-0.22f, 0.04f, 0.042f, 0.038f),
            new Ring(-0.28f, 0.037f, 0.035f, 0.037f), Ring.Pole(-0.31f)
        };

        private static readonly Ring[] SleeveRings =
        {
            Ring.Pole(0.062f), new Ring(0.052f, 0.064f, 0.064f, 0.064f), new Ring(-0.02f, 0.069f, 0.069f, 0.066f),
            new Ring(-0.12f, 0.066f, 0.068f, 0.063f), Ring.Pole(-0.126f)
        };

        private static readonly Ring[] ForearmRings =
        {
            Ring.Pole(0.025f), new Ring(0.0f, 0.037f, 0.036f, 0.037f), new Ring(-0.05f, 0.041f, 0.04f, 0.038f),
            new Ring(-0.15f, 0.032f, 0.03f, 0.03f), new Ring(-0.235f, 0.024f, 0.02f, 0.02f), Ring.Pole(-0.25f)
        };

        private static readonly Ring[] HandRings =
        {
            Ring.Pole(0.008f), new Ring(-0.01f, 0.03f, 0.016f, 0.016f), new Ring(-0.05f, 0.036f, 0.018f, 0.018f),
            new Ring(-0.09f, 0.03f, 0.015f, 0.014f), Ring.Pole(-0.108f)
        };

        private static readonly Ring[] NeckRings =
        {
            Ring.Pole(1.63f), new Ring(1.61f, 0.05f, 0.052f, 0.05f), new Ring(1.55f, 0.056f, 0.052f, 0.056f),
            new Ring(1.49f, 0.066f, 0.062f, 0.062f), Ring.Pole(1.47f)
        };

        /// <summary>Cráneo ovalado con pómulos, mandíbula y mentón (espacio de la cabeza).</summary>
        private static readonly Ring[] SkullRings =
        {
            Ring.Pole(0.275f), new Ring(0.262f, 0.05f, 0.05f, 0.052f), new Ring(0.232f, 0.09f, 0.1f, 0.104f),
            new Ring(0.18f, 0.104f, 0.115f, 0.117f), new Ring(0.12f, 0.1f, 0.115f, 0.11f), new Ring(0.07f, 0.086f, 0.11f, 0.094f),
            new Ring(0.032f, 0.062f, 0.096f, 0.06f), new Ring(0.012f, 0.036f, 0.07f, 0.028f), Ring.Pole(0.0f)
        };

        private static readonly Ring[] NoseRings =
        {
            Ring.Pole(0.158f), new Ring(0.145f, 0.011f, 0.01f, 0.004f), new Ring(0.115f, 0.016f, 0.026f, 0.006f), Ring.Pole(0.104f)
        };

        /// <summary>Casquete de pelo: cubre la nuca y deja libre la frente (espacio de la cabeza).</summary>
        private static readonly Ring[] HairCapRings =
        {
            Ring.Pole(0.29f), new Ring(0.279f, 0.056f, 0.056f, 0.058f), new Ring(0.248f, 0.097f, 0.106f, 0.11f),
            new Ring(0.2f, 0.112f, 0.11f, 0.124f), new Ring(0.16f, 0.112f, 0.075f, 0.122f), new Ring(0.12f, 0.104f, 0.02f, 0.115f),
            Ring.Pole(0.11f)
        };

        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        /// <summary>Tronco en V.</summary>
        public static Mesh Torso => Get("torso", () => Sculpt(TorsoRings, Sides + 6));
        /// <summary>Pelvis con el pantalón (cintura, cadera y glúteos).</summary>
        public static Mesh Pelvis => Get("pelvis", () => Sculpt(PelvisRings, Sides + 4));
        /// <summary>Pierna del pantalón, con el ruedo algo abierto que cae sobre el muslo (cadera).</summary>
        public static Mesh ShortLeg => Get("shortleg", () => Sculpt(ShortLegRings, Sides));
        /// <summary>Muslo con cuádriceps y rótula (cadera).</summary>
        public static Mesh Thigh => Get("thigh", () => Sculpt(ThighRings, Sides));
        /// <summary>Pierna con gemelo y tobillo fino (rodilla).</summary>
        public static Mesh Shin => Get("shin", () => Sculpt(ShinRings, Sides));
        /// <summary>Media: la pierna apenas más gruesa, de debajo de la rodilla al tobillo (rodilla).</summary>
        public static Mesh Sock => Get("sock", () => Sculpt(Slice(ShinRings, -0.07f, -0.405f), Sides, 1.06f, 0.002f));
        /// <summary>Franja de la media, abajo de la rodilla (en el color secundario de la equipación).</summary>
        public static Mesh SockBand => Get("sockband", () => Sculpt(Slice(ShinRings, -0.085f, -0.125f), Sides, 1.09f, 0.003f));
        /// <summary>Botín estilizado con puntera, caña y suela plana (girado en el modelo).</summary>
        public static Mesh Boot => Get("boot", () => Sculpt(BootRings, Sides));
        /// <summary>Brazo con deltoides y bíceps (hombro).</summary>
        public static Mesh UpperArm => Get("upperarm", () => Sculpt(UpperArmRings, Sides));
        /// <summary>Manga corta de la camiseta (hombro).</summary>
        public static Mesh Sleeve => Get("sleeve", () => Sculpt(SleeveRings, Sides));
        /// <summary>Antebrazo hasta la muñeca (codo).</summary>
        public static Mesh Forearm => Get("forearm", () => Sculpt(ForearmRings, Sides));
        /// <summary>Mano aplanada (muñeca).</summary>
        public static Mesh Hand => Get("hand", () => Sculpt(HandRings, 10));
        /// <summary>Cuello (cuerpo).</summary>
        public static Mesh Neck => Get("neck", () => Sculpt(NeckRings, Sides));
        /// <summary>Cráneo con mandíbula (cabeza).</summary>
        public static Mesh Skull => Get("skull", () => Sculpt(SkullRings, Sides + 4));
        /// <summary>Nariz (cabeza).</summary>
        public static Mesh Nose => Get("nose", () => Sculpt(NoseRings, 8));
        /// <summary>Casquete de pelo (cabeza).</summary>
        public static Mesh HairCap => Get("haircap", () => Sculpt(HairCapRings, Sides + 4));

        /// <summary>
        /// Dibujo de la camiseta como una «piel» apenas por encima del tronco (rayas, franja, banda, V o mitades): sigue la forma
        /// del pecho y la espalda, sin cajas que asomen.
        /// </summary>
        /// <param name="pattern">Diseño (liso devuelve null).</param>
        public static Mesh Pattern(KitPattern pattern)
        {
            if (pattern == KitPattern.Solid)
            {
                return null;
            }

            return Get("pattern" + (int)pattern, () => BuildPattern(pattern));
        }

        private static Mesh BuildPattern(KitPattern pattern)
        {
            var b = new Builder();
            const float top = 1.47f, bottom = 1.005f, chest = 1.3f;
            switch (pattern)
            {
                case KitPattern.Stripes:
                    for (int i = -1; i <= 1; i++)
                    {
                        float c = i * 0.115f;
                        Strip(b, top, bottom, y => c, 0.03f);
                    }

                    break;
                case KitPattern.Band:
                    BandAround(b, chest + 0.07f, chest - 0.05f);
                    break;
                case KitPattern.Sash:
                    Strip(b, top - 0.02f, bottom, y => (y - 1.24f) * 0.75f, 0.045f); // del hombro izquierdo a la cadera derecha
                    break;
                case KitPattern.Chevron:
                    for (int side = -1; side <= 1; side += 2)
                    {
                        int s = side;
                        Strip(b, top - 0.03f, chest - 0.02f, y => s * (y - (chest - 0.02f)) * 1.1f, 0.03f);
                    }

                    break;
                case KitPattern.Halves:
                    Strip(b, top, bottom, y => 0.15f, 0.15f);
                    break;
            }

            return b.Build();
        }

        /// <summary>Franja vertical (o inclinada) por delante y por detrás, centrada en x = centre(y).</summary>
        private static void Strip(Builder b, float yTop, float yBottom, Func<float, float> centre, float halfWidth)
        {
            for (int side = 0; side < 2; side++)
            {
                bool front = side == 0;
                int start = b.Count;
                for (int row = 0; row <= PatchRows; row++)
                {
                    float y = Mathf.Lerp(yTop, yBottom, row / (float)PatchRows);
                    Ring r = Sample(TorsoRings, y);
                    for (int col = 0; col <= PatchColumns; col++)
                    {
                        float x = centre(y) + Mathf.Lerp(-halfWidth, halfWidth, col / (float)PatchColumns);
                        float a = Mathf.Acos(Mathf.Clamp(x / Mathf.Max(r.Rx, 0.001f), -1f, 1f));
                        b.Add(r.Point(front ? a : -a, PatternInflate, PatternOffset));
                    }
                }

                Grid(b, start, PatchRows, PatchColumns);
            }
        }

        /// <summary>Franja horizontal que da toda la vuelta al pecho.</summary>
        private static void BandAround(Builder b, float yTop, float yBottom)
        {
            int start = b.Count;
            int columns = Sides + 6;
            for (int row = 0; row <= PatchRows / 2; row++)
            {
                Ring r = Sample(TorsoRings, Mathf.Lerp(yTop, yBottom, row / (float)(PatchRows / 2)));
                for (int col = 0; col <= columns; col++)
                {
                    b.Add(r.Point(col * Mathf.PI * 2f / columns, PatternInflate, PatternOffset));
                }
            }

            Grid(b, start, PatchRows / 2, columns);
        }

        private static void Grid(Builder b, int start, int rows, int columns)
        {
            int stride = columns + 1;
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    int a = start + row * stride + col;
                    b.Quad(a, a + 1, a + stride + 1, a + stride);
                }
            }
        }

        /// <summary>Sección interpolada a una altura dada (entre los anillos que no son polos).</summary>
        public static Ring Sample(Ring[] rings, float y)
        {
            for (int i = 1; i < rings.Length - 2; i++)
            {
                Ring a = rings[i], c = rings[i + 1];
                if (y <= a.Y && y >= c.Y)
                {
                    float t = (a.Y - y) / Mathf.Max(a.Y - c.Y, 1e-5f);
                    return new Ring(y, Mathf.Lerp(a.Rx, c.Rx, t), Mathf.Lerp(a.Front, c.Front, t), Mathf.Lerp(a.Back, c.Back, t));
                }
            }

            Ring edge = y > rings[1].Y ? rings[1] : rings[rings.Length - 2];
            return new Ring(y, edge.Rx, edge.Front, edge.Back);
        }

        /// <summary>Recorta un perfil entre dos alturas y lo cierra con polos.</summary>
        private static Ring[] Slice(Ring[] rings, float yTop, float yBottom)
        {
            var list = new List<Ring> { Ring.Pole(yTop + 0.004f), Sample(rings, yTop) };
            for (int i = 1; i < rings.Length - 1; i++)
            {
                if (rings[i].Y < yTop && rings[i].Y > yBottom)
                {
                    list.Add(rings[i]);
                }
            }

            list.Add(Sample(rings, yBottom));
            list.Add(Ring.Pole(yBottom - 0.004f));
            return list.ToArray();
        }

        /// <summary>
        /// Malla esculpida a partir de secciones (de arriba abajo; la primera y la última son polos). Los vértices de cada
        /// anillo se comparten, así las normales salen suaves.
        /// </summary>
        public static Mesh Sculpt(Ring[] rings, int sides, float inflate = 1f, float offset = 0f)
        {
            var b = new Builder();
            int top = b.Add(new Vector3(0f, rings[0].Y, 0f));
            int ringCount = rings.Length - 2;
            int first = b.Count;
            for (int r = 0; r < ringCount; r++)
            {
                for (int i = 0; i < sides; i++)
                {
                    b.Add(rings[r + 1].Point(i * Mathf.PI * 2f / sides, inflate, offset));
                }
            }

            int bottom = b.Add(new Vector3(0f, rings[rings.Length - 1].Y, 0f));
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                b.Tri(top, first + j, first + i);
                b.Tri(bottom, first + (ringCount - 1) * sides + i, first + (ringCount - 1) * sides + j);
                for (int r = 0; r < ringCount - 1; r++)
                {
                    int a = first + r * sides;
                    b.Quad(a + i, a + j, a + sides + j, a + sides + i);
                }
            }

            return b.Build();
        }

        private static Mesh Get(string key, Func<Mesh> create)
        {
            if (!Cache.TryGetValue(key, out Mesh mesh) || mesh == null)
            {
                mesh = create();
                mesh.name = key;
                Cache[key] = mesh;
            }

            return mesh;
        }

        /// <summary>Acumula vértices y triángulos; al cerrar, orienta cada cara hacia fuera del eje vertical.</summary>
        private sealed class Builder
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<int> _triangles = new List<int>();

            public int Count => _vertices.Count;

            public int Add(Vector3 v)
            {
                _vertices.Add(v);
                return _vertices.Count - 1;
            }

            public void Tri(int a, int b, int c)
            {
                _triangles.Add(a);
                _triangles.Add(b);
                _triangles.Add(c);
            }

            public void Quad(int a, int b, int c, int d)
            {
                Tri(a, b, c);
                Tri(a, c, d);
            }

            public Mesh Build()
            {
                float min = float.MaxValue, max = float.MinValue;
                for (int i = 0; i < _vertices.Count; i++)
                {
                    min = Mathf.Min(min, _vertices[i].y);
                    max = Mathf.Max(max, _vertices[i].y);
                }

                float centreY = (min + max) * 0.5f;
                for (int i = 0; i < _triangles.Count; i += 3)
                {
                    Vector3 a = _vertices[_triangles[i]], b = _vertices[_triangles[i + 1]], c = _vertices[_triangles[i + 2]];
                    Vector3 normal = Vector3.Cross(b - a, c - a);
                    Vector3 middle = (a + b + c) / 3f;
                    // Hacia fuera = alejándose del eje a la misma altura (las secciones rodean el eje). En los polos, la cara
                    // casi horizontal se orienta hacia arriba o abajo según de qué lado del centro de la malla está.
                    Vector3 outward = new Vector3(middle.x, 0f, middle.z);
                    if (outward.sqrMagnitude < 1e-6f)
                    {
                        outward = Vector3.up * Mathf.Sign(middle.y - centreY);
                    }

                    if (Vector3.Dot(normal, outward) < 0f)
                    {
                        int swap = _triangles[i + 1];
                        _triangles[i + 1] = _triangles[i + 2];
                        _triangles[i + 2] = swap;
                    }
                }

                var mesh = new Mesh();
                mesh.SetVertices(_vertices);
                mesh.SetTriangles(_triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
