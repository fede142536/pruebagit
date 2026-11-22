using System.Collections.Generic;
using UnityEngine;

namespace Football.Game
{
    /// <summary>
    /// Mallas simples generadas por código y compartidas por todos los jugadores (pocas caras, aptas para móviles de
    /// gama baja). Las extremidades tienen el pivote arriba (en la cadera o el hombro) para poder girarlas al animar.
    /// </summary>
    public static class MeshKit
    {
        private const int SphereSegments = 10;
        private const int SphereRings = 7;
        private const int DiscSides = 14;

        private static Mesh _limb;
        private static Mesh _torso;
        private static Mesh _box;
        private static Mesh _sphere;
        private static Mesh _arrow;
        private static Mesh _disc;

        /// <summary>
        /// Extremidad: torno redondeado (cápsula que se afina hacia abajo, como un muslo o un antebrazo) de 1 m que cuelga
        /// desde su pivote (y = 0) hasta y = -1. Sus extremos son redondos para que las articulaciones no muestren aristas.
        /// </summary>
        public static Mesh Limb => _limb != null ? _limb : (_limb = Lathe(new[]
        {
            new Vector2(0.04f, 0f), new Vector2(0.01f, 0.3f), new Vector2(-0.05f, 0.47f), new Vector2(-0.25f, 0.5f),
            new Vector2(-0.6f, 0.44f), new Vector2(-0.92f, 0.38f), new Vector2(-0.99f, 0.28f), new Vector2(-1.02f, 0f)
        }));

        /// <summary>Tronco atlético: hombros anchos y redondeados, pecho y cintura más estrecha, de y = 0 a y = -1.</summary>
        public static Mesh Torso => _torso != null ? _torso : (_torso = Lathe(new[]
        {
            new Vector2(0.05f, 0f), new Vector2(0.02f, 0.3f), new Vector2(-0.06f, 0.48f), new Vector2(-0.2f, 0.5f),
            new Vector2(-0.5f, 0.46f), new Vector2(-0.85f, 0.41f), new Vector2(-1f, 0.4f), new Vector2(-1.03f, 0f)
        }));

        private static Mesh _capsule;

        /// <summary>Cápsula centrada de alto 1 y diámetro 1 (pantalón, cuello, guantes, botines).</summary>
        public static Mesh Capsule => _capsule != null ? _capsule : (_capsule = Lathe(new[]
        {
            new Vector2(0.5f, 0f), new Vector2(0.45f, 0.3f), new Vector2(0.32f, 0.47f), new Vector2(0.1f, 0.5f),
            new Vector2(-0.1f, 0.5f), new Vector2(-0.32f, 0.47f), new Vector2(-0.45f, 0.3f), new Vector2(-0.5f, 0f)
        }));

        private static Mesh _cylinder;

        /// <summary>Cilindro centrado de alto 1 y diámetro 1 (postes y travesaño).</summary>
        public static Mesh Cylinder => _cylinder != null ? _cylinder : (_cylinder = Lathe(new[]
        {
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-0.5f, 0.5f), new Vector2(-0.5f, 0f)
        }));

        private const int LatheSides = 10;

        /// <summary>
        /// Sólido de revolución alrededor del eje Y a partir de un perfil (y, radio), de arriba abajo; el primer y el último
        /// punto deben tener radio 0 (cierran la figura).
        /// </summary>
        private static Mesh Lathe(Vector2[] profile)
        {
            var s = new Solid();
            int top = s.Add(new Vector3(0f, profile[0].x, 0f));
            int bottom = s.Add(new Vector3(0f, profile[profile.Length - 1].x, 0f));
            int ringCount = profile.Length - 2;
            var rings = new int[ringCount, LatheSides];
            for (int r = 0; r < ringCount; r++)
            {
                Vector2 point = profile[r + 1];
                for (int i = 0; i < LatheSides; i++)
                {
                    float a = i * Mathf.PI * 2f / LatheSides;
                    rings[r, i] = s.Add(new Vector3(Mathf.Cos(a) * point.y, point.x, Mathf.Sin(a) * point.y));
                }
            }

            for (int i = 0; i < LatheSides; i++)
            {
                int j = (i + 1) % LatheSides;
                s.Tri(top, rings[0, j], rings[0, i]);
                s.Tri(bottom, rings[ringCount - 1, i], rings[ringCount - 1, j]);
                for (int r = 0; r < ringCount - 1; r++)
                {
                    s.Quad(rings[r, i], rings[r, j], rings[r + 1, j], rings[r + 1, i]);
                }
            }

            float middle = (profile[0].x + profile[profile.Length - 1].x) * 0.5f;
            return s.Build(new Vector3(0f, middle, 0f));
        }

        /// <summary>Cubo unitario centrado.</summary>
        public static Mesh Box => _box != null ? _box : (_box = BuildBox());

        /// <summary>Esfera de diámetro 1 centrada, de pocas caras.</summary>
        public static Mesh Sphere => _sphere != null ? _sphere : (_sphere = BuildSphere());

        /// <summary>Flecha (pirámide) que apunta hacia abajo, de altura 1 centrada.</summary>
        public static Mesh Arrow => _arrow != null ? _arrow : (_arrow = BuildArrow());

        /// <summary>Disco plano de diámetro 1 (para sombras y aros).</summary>
        public static Mesh Disc => _disc != null ? _disc : (_disc = BuildDisc());

        private sealed class Solid
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<int> Triangles = new List<int>();

            public int Add(Vector3 v)
            {
                Vertices.Add(v);
                return Vertices.Count - 1;
            }

            public void Tri(int a, int b, int c)
            {
                Triangles.Add(a);
                Triangles.Add(b);
                Triangles.Add(c);
            }

            public void Quad(int a, int b, int c, int d)
            {
                Tri(a, b, c);
                Tri(a, c, d);
            }

            /// <summary>
            /// Crea la malla orientando todas las caras hacia fuera (las figuras son convexas): así son visibles aunque el
            /// material descarte las caras traseras, sin depender de escribir cada triángulo en el orden correcto.
            /// </summary>
            public Mesh Build(Vector3 centre)
            {
                for (int i = 0; i < Triangles.Count; i += 3)
                {
                    Vector3 a = Vertices[Triangles[i]], b = Vertices[Triangles[i + 1]], c = Vertices[Triangles[i + 2]];
                    Vector3 normal = Vector3.Cross(b - a, c - a);
                    Vector3 middle = (a + b + c) / 3f;
                    if (Vector3.Dot(normal, middle - centre) < 0f)
                    {
                        int swap = Triangles[i + 1];
                        Triangles[i + 1] = Triangles[i + 2];
                        Triangles[i + 2] = swap;
                    }
                }

                var mesh = new Mesh();
                mesh.SetVertices(Vertices);
                mesh.SetTriangles(Triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        private static Mesh BuildBox()
        {
            var s = new Solid();
            var v = new int[8];
            for (int i = 0; i < 8; i++)
            {
                v[i] = s.Add(new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, (i & 4) == 0 ? -0.5f : 0.5f));
            }

            s.Quad(v[0], v[1], v[3], v[2]);
            s.Quad(v[4], v[5], v[7], v[6]);
            s.Quad(v[0], v[1], v[5], v[4]);
            s.Quad(v[2], v[3], v[7], v[6]);
            s.Quad(v[0], v[2], v[6], v[4]);
            s.Quad(v[1], v[3], v[7], v[5]);
            return s.Build(Vector3.zero);
        }

        private static Mesh BuildSphere()
        {
            var s = new Solid();
            int top = s.Add(new Vector3(0f, 0.5f, 0f));
            int bottom = s.Add(new Vector3(0f, -0.5f, 0f));
            var rings = new int[SphereRings - 1, SphereSegments];
            for (int r = 1; r < SphereRings; r++)
            {
                float polar = r * Mathf.PI / SphereRings;
                float y = 0.5f * Mathf.Cos(polar);
                float radius = 0.5f * Mathf.Sin(polar);
                for (int i = 0; i < SphereSegments; i++)
                {
                    float a = i * Mathf.PI * 2f / SphereSegments;
                    rings[r - 1, i] = s.Add(new Vector3(Mathf.Cos(a) * radius, y, Mathf.Sin(a) * radius));
                }
            }

            for (int i = 0; i < SphereSegments; i++)
            {
                int j = (i + 1) % SphereSegments;
                s.Tri(top, rings[0, i], rings[0, j]);
                s.Tri(bottom, rings[SphereRings - 2, i], rings[SphereRings - 2, j]);
                for (int r = 0; r < SphereRings - 2; r++)
                {
                    s.Quad(rings[r, i], rings[r, j], rings[r + 1, j], rings[r + 1, i]);
                }
            }

            return s.Build(Vector3.zero);
        }

        private static Mesh BuildArrow()
        {
            var s = new Solid();
            int tip = s.Add(new Vector3(0f, -0.5f, 0f));
            int a = s.Add(new Vector3(-0.5f, 0.5f, -0.5f));
            int b = s.Add(new Vector3(0.5f, 0.5f, -0.5f));
            int c = s.Add(new Vector3(0.5f, 0.5f, 0.5f));
            int d = s.Add(new Vector3(-0.5f, 0.5f, 0.5f));
            s.Tri(tip, a, b);
            s.Tri(tip, b, c);
            s.Tri(tip, c, d);
            s.Tri(tip, d, a);
            s.Quad(a, b, c, d);
            return s.Build(new Vector3(0f, 0.25f, 0f));
        }

        private static Mesh BuildDisc()
        {
            // Disco con un mínimo de grosor para poder orientar sus caras como un sólido.
            var s = new Solid();
            int top = s.Add(new Vector3(0f, 0.005f, 0f));
            int bottom = s.Add(new Vector3(0f, -0.005f, 0f));
            var upper = new int[DiscSides];
            var lower = new int[DiscSides];
            for (int i = 0; i < DiscSides; i++)
            {
                float a = i * Mathf.PI * 2f / DiscSides;
                upper[i] = s.Add(new Vector3(Mathf.Cos(a) * 0.5f, 0.005f, Mathf.Sin(a) * 0.5f));
                lower[i] = s.Add(new Vector3(Mathf.Cos(a) * 0.5f, -0.005f, Mathf.Sin(a) * 0.5f));
            }

            for (int i = 0; i < DiscSides; i++)
            {
                int j = (i + 1) % DiscSides;
                s.Tri(top, upper[i], upper[j]);
                s.Tri(bottom, lower[i], lower[j]);
                s.Quad(upper[i], upper[j], lower[j], lower[i]);
            }

            return s.Build(Vector3.zero);
        }
    }
}
