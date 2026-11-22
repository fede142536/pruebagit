using System.Collections.Generic;
using UnityEngine;

namespace Football.Game
{
    /// <summary>
    /// Junta varias piezas (malla + posición, giro, escala y color) en una sola malla con color por vértice.
    /// Menos objetos que dibujar = menos llamadas de dibujo, sin <c>MaterialPropertyBlock</c> (D-15).
    /// Se usa solo al construir la escena, nunca durante el partido.
    /// </summary>
    public sealed class MeshMerge
    {
        private static readonly List<Vector3> ReadVertices = new List<Vector3>();
        private static readonly List<Vector3> ReadNormals = new List<Vector3>();
        private static readonly List<int> ReadTriangles = new List<int>();

        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<int> _triangles = new List<int>();

        /// <summary>Verdadero si todavía no tiene piezas.</summary>
        public bool IsEmpty => _vertices.Count == 0;

        /// <summary>Añade una pieza.</summary>
        /// <param name="mesh">Malla de la pieza (debe ser legible: las de <see cref="MeshKit"/> lo son).</param>
        /// <param name="position">Posición local.</param>
        /// <param name="rotation">Giro local.</param>
        /// <param name="scale">Escala local.</param>
        /// <param name="color">Color de todos sus vértices.</param>
        public void Add(Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale, Color color)
        {
            mesh.GetVertices(ReadVertices);
            mesh.GetNormals(ReadNormals);
            mesh.GetTriangles(ReadTriangles, 0);
            int offset = _vertices.Count;
            bool hasNormals = ReadNormals.Count == ReadVertices.Count;
            for (int i = 0; i < ReadVertices.Count; i++)
            {
                _vertices.Add(rotation * Vector3.Scale(ReadVertices[i], scale) + position);
                // Con escala no uniforme la normal se transforma con la escala inversa.
                Vector3 n = hasNormals ? ReadNormals[i] : Vector3.up;
                n = new Vector3(n.x / scale.x, n.y / scale.y, n.z / scale.z);
                _normals.Add((rotation * n).normalized);
                _colors.Add(color);
            }

            for (int i = 0; i < ReadTriangles.Count; i++)
            {
                _triangles.Add(ReadTriangles[i] + offset);
            }
        }

        /// <summary>Crea la malla combinada.</summary>
        /// <returns>Una malla nueva (quien la pide debe destruirla al terminar).</returns>
        public Mesh Build()
        {
            var mesh = new Mesh();
            if (_vertices.Count > ushort.MaxValue)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetColors(_colors);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
