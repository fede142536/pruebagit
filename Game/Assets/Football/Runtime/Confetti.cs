using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Football.Game
{
    /// <summary>
    /// Papelitos que caen sobre las tribunas y la banda al empezar el partido, en los colores de los dos equipos. Son pocas
    /// mallas con muchos papelitos cada una (pocas llamadas de dibujo, importante en el A13) y se apagan al terminar.
    /// </summary>
    public sealed class Confetti
    {
        private const int PiecesPerColor = 60;
        private const float Duration = 7f;
        private const float StartHeight = 14f;
        private const float FallSpeed = 2.2f;
        private const float Size = 0.35f;
        private const float Sway = 0.6f;

        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<Vector3[]> _origins = new List<Vector3[]>();
        private readonly List<Vector3> _vertices = new List<Vector3>(PiecesPerColor * 4);
        private float _time;

        /// <summary>Crea los papelitos (uno por color) en la franja entre las líneas laterales y las tribunas.</summary>
        public Confetti(Transform parent, float halfLength, float halfWidth, Color[] colors, List<Mesh> ownedMeshes)
        {
            var rng = new Football.Core.Common.SeededRandom(11);
            for (int c = 0; c < colors.Length; c++)
            {
                var origins = new Vector3[PiecesPerColor];
                for (int i = 0; i < origins.Length; i++)
                {
                    float side = rng.NextFloat() < 0.5f ? -1f : 1f;
                    float x = (rng.NextFloat() * 2f - 1f) * halfLength;
                    float z = side * (halfWidth + 1f + rng.NextFloat() * 8f);
                    origins[i] = new Vector3(x, StartHeight + rng.NextFloat() * 6f, z);
                }

                var mesh = new Mesh();
                var triangles = new int[PiecesPerColor * 6];
                for (int i = 0; i < PiecesPerColor; i++)
                {
                    int v = i * 4, t = i * 6;
                    triangles[t] = v;
                    triangles[t + 1] = v + 1;
                    triangles[t + 2] = v + 2;
                    triangles[t + 3] = v;
                    triangles[t + 4] = v + 2;
                    triangles[t + 5] = v + 3;
                }

                Fill(origins, 0f);
                mesh.SetVertices(_vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.bounds = new Bounds(Vector3.zero, new Vector3(halfLength * 2f + 40f, 60f, halfWidth * 2f + 40f));
                ownedMeshes.Add(mesh);

                var go = new GameObject("Papelitos");
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = Materials.Get(colors[c]);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                _meshes.Add(mesh);
                _objects.Add(go);
                _origins.Add(origins);
            }
        }

        /// <summary>Anima los papelitos; tras unos segundos se apagan.</summary>
        public void Update(float deltaTime)
        {
            if (_time > Duration)
            {
                return;
            }

            _time += deltaTime;
            bool done = _time > Duration;
            for (int c = 0; c < _meshes.Count; c++)
            {
                if (done)
                {
                    _objects[c].SetActive(false);
                    continue;
                }

                Fill(_origins[c], _time);
                _meshes[c].SetVertices(_vertices);
            }
        }

        private void Fill(Vector3[] origins, float time)
        {
            _vertices.Clear();
            for (int i = 0; i < origins.Length; i++)
            {
                Vector3 o = origins[i];
                float y = Mathf.Max(0.02f, o.y - FallSpeed * time);
                float wobble = Mathf.Sin(time * 3f + i) * Sway;
                var p = new Vector3(o.x + wobble, y, o.z + Mathf.Cos(time * 2.3f + i) * Sway);
                float spin = time * 4f + i;
                var a = new Vector3(Mathf.Cos(spin), Mathf.Sin(spin * 0.7f), Mathf.Sin(spin)) * (Size * 0.5f);
                var b = new Vector3(-Mathf.Sin(spin), 0.3f, Mathf.Cos(spin)) * (Size * 0.5f);
                _vertices.Add(p - a - b);
                _vertices.Add(p - a + b);
                _vertices.Add(p + a + b);
                _vertices.Add(p + a - b);
            }
        }
    }
}
