using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Football.Game
{
    /// <summary>
    /// Lluvia: unas cuantas gotas alargadas que caen alrededor del balón. Una sola malla (dos planos cruzados por gota) que se
    /// actualiza sin asignar memoria; el movimiento sale del tiempo, sin azar de juego (es solo decoración).
    /// </summary>
    public sealed class Rain
    {
        private const int Drops = 380;
        private const float Height = 24f;
        private const float FallSpeed = 26f;
        private const float StreakLength = 0.9f;
        private const float StreakWidth = 0.035f;
        private const float Slant = 0.06f;               // la lluvia cae algo inclinada
        private const float HalfExtentX = 38f, HalfExtentZ = 26f;
        private static readonly Color StreakColor = new Color(0.74f, 0.82f, 0.94f);

        private readonly Mesh _mesh;
        private readonly Vector3[] _vertices = new Vector3[Drops * 8];
        private readonly Vector2[] _origin = new Vector2[Drops];
        private readonly float[] _phase = new float[Drops];
        private readonly GameObject _object;
        private float _time;

        /// <summary>Crea la lluvia (dibujada sin sombras).</summary>
        /// <param name="parent">Padre en la escena.</param>
        /// <param name="ownedMeshes">Lista donde se anota la malla para destruirla al terminar el partido.</param>
        public Rain(Transform parent, List<Mesh> ownedMeshes)
        {
            var rng = new Football.Core.Common.SeededRandom(23);
            var triangles = new int[Drops * 12];
            for (int i = 0; i < Drops; i++)
            {
                _origin[i] = new Vector2((rng.NextFloat() * 2f - 1f) * HalfExtentX, (rng.NextFloat() * 2f - 1f) * HalfExtentZ);
                _phase[i] = rng.NextFloat() * Height;
                for (int q = 0; q < 2; q++)
                {
                    int v = i * 8 + q * 4, t = i * 12 + q * 6;
                    triangles[t] = v;
                    triangles[t + 1] = v + 1;
                    triangles[t + 2] = v + 2;
                    triangles[t + 3] = v;
                    triangles[t + 4] = v + 2;
                    triangles[t + 5] = v + 3;
                }
            }

            _mesh = new Mesh();
            _mesh.MarkDynamic();
            Fill(Vector2.zero);
            _mesh.vertices = _vertices;
            _mesh.triangles = triangles;
            _mesh.bounds = new Bounds(new Vector3(0f, Height * 0.5f, 0f), new Vector3(HalfExtentX * 6f, Height + 4f, HalfExtentZ * 6f));
            ownedMeshes.Add(_mesh);

            _object = new GameObject("Lluvia");
            _object.transform.SetParent(parent, false);
            _object.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = _object.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Materials.Get(StreakColor);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>Hace caer las gotas.</summary>
        /// <param name="deltaTime">Tiempo del fotograma (s).</param>
        /// <param name="center">Punto del campo (x, z en el mundo de Unity) alrededor del cual llueve.</param>
        public void Update(float deltaTime, Vector2 center)
        {
            _time += deltaTime;
            Fill(center);
            _mesh.SetVertices(_vertices);
        }

        private void Fill(Vector2 center)
        {
            for (int i = 0; i < Drops; i++)
            {
                // La gota se repite dentro de una caja que sigue al balón: al salir por un borde reaparece por el opuesto.
                float x = center.x + Wrap(_origin[i].x - center.x, HalfExtentX);
                float z = center.y + Wrap(_origin[i].y - center.y, HalfExtentZ);
                float y = Height - Mathf.Repeat(_phase[i] + _time * FallSpeed, Height);
                float top = y + StreakLength, shift = Slant * StreakLength;
                int v = i * 8;
                _vertices[v] = new Vector3(x - StreakWidth, y, z);
                _vertices[v + 1] = new Vector3(x + StreakWidth, y, z);
                _vertices[v + 2] = new Vector3(x + StreakWidth + shift, top, z);
                _vertices[v + 3] = new Vector3(x - StreakWidth + shift, top, z);
                _vertices[v + 4] = new Vector3(x, y, z - StreakWidth);
                _vertices[v + 5] = new Vector3(x, y, z + StreakWidth);
                _vertices[v + 6] = new Vector3(x + shift, top, z + StreakWidth);
                _vertices[v + 7] = new Vector3(x + shift, top, z - StreakWidth);
            }
        }

        private static float Wrap(float offset, float half) => Mathf.Repeat(offset + half, 2f * half) - half;
    }
}
