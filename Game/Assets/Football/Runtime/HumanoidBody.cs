using System;
using System.IO;
using UnityEngine;

namespace Football.Game
{
    /// <summary>Región de un vértice del cuerpo importado: decide con qué color del club o del jugador se pinta.</summary>
    public enum BodyRegion : byte
    {
        /// <summary>Piel.</summary>
        Skin = 0,
        /// <summary>Camiseta (torso y mangas).</summary>
        Shirt = 1,
        /// <summary>Pantalón corto.</summary>
        Shorts = 2,
        /// <summary>Media.</summary>
        Sock = 3,
        /// <summary>Franja de la media.</summary>
        Stripe = 4,
        /// <summary>Botín.</summary>
        Boot = 5
    }

    /// <summary>
    /// Cuerpo humano del cuello para abajo, convertido por <c>Tools/ImportHumanoid</c> a partir del modelo CC0 de Quaternius
    /// («Universal Base Characters»): pose A, pesos sobre los 20 huesos del juego y una región por vértice. Dos niveles de detalle.
    /// Es opcional: si el archivo no está (o la calidad es baja) el juego arma el cuerpo con sus anillos procedurales.
    /// Esta clase no depende de Unity más que por <c>Vector3</c>, así que también la lee la herramienta SkinCheck.
    /// </summary>
    public sealed class HumanoidBody
    {
        /// <summary>Un nivel de detalle.</summary>
        public sealed class Lod
        {
            /// <summary>Posiciones en pose A (m, pies en el origen, mirando a +Z).</summary>
            public Vector3[] Positions;
            /// <summary>Hueso principal de cada vértice.</summary>
            public byte[] Bone0;
            /// <summary>Segundo hueso.</summary>
            public byte[] Bone1;
            /// <summary>Peso del hueso principal, de 0 a 255.</summary>
            public byte[] Weight0;
            /// <summary>Región (<see cref="BodyRegion"/>).</summary>
            public byte[] Region;
            /// <summary>Triángulos (índices de a tres).</summary>
            public ushort[] Triangles;
        }

        private const string Magic = "HUM1";
        private const int BoneCount = 20;
        /// <summary>Donde apoya la cabeza armada por el juego: la base de la mandíbula, sobre el corte del cuello (m).</summary>
        public static readonly Vector3 HeadOrigin = new Vector3(0f, 1.572f, -0.005f);
        /// <summary>Cuánto más grande que la cabeza procedural es la cabeza que va sobre este cuerpo.</summary>
        public const float HeadSizeFactor = 1.06f;
        /// <summary>Desplazamiento vertical de los decales (dorsal y escudo) respecto del cuerpo procedural.</summary>
        public const float DecalShiftY = 0.06f;

        /// <summary>Posición de reposo de los 20 huesos del juego.</summary>
        public Vector3[] Bones;
        /// <summary>Niveles de detalle (0 completo, 1 simplificado).</summary>
        public Lod[] Lods;

        /// <summary>Cuerpo instalado (null si no hay archivo o está desactivado).</summary>
        public static HumanoidBody Current { get; private set; }

        /// <summary>Instala el cuerpo importado a partir del contenido del archivo; con null vuelve al cuerpo procedural.</summary>
        /// <param name="bytes">Contenido de <c>body.bytes</c>.</param>
        /// <returns>Verdadero si quedó instalado.</returns>
        public static bool Install(byte[] bytes)
        {
            Current = bytes == null ? null : Parse(bytes);
            SkinnedBodyGeometry.UseHumanoid(Current);
            return Current != null;
        }

        /// <summary>Lee el archivo de <c>Tools/ImportHumanoid</c>; devuelve null si el formato no es el esperado.</summary>
        public static HumanoidBody Parse(byte[] bytes)
        {
            try
            {
                using (var r = new BinaryReader(new MemoryStream(bytes)))
                {
                    if (new string(r.ReadChars(4)) != Magic)
                    {
                        return null;
                    }

                    int lodCount = r.ReadByte();
                    var body = new HumanoidBody { Bones = new Vector3[BoneCount], Lods = new Lod[lodCount] };
                    for (int b = 0; b < BoneCount; b++)
                    {
                        body.Bones[b] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    }

                    for (int l = 0; l < lodCount; l++)
                    {
                        int vertices = (int)r.ReadUInt32(), triangles = (int)r.ReadUInt32();
                        var lod = new Lod { Positions = new Vector3[vertices] };
                        for (int v = 0; v < vertices; v++)
                        {
                            lod.Positions[v] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                        }

                        lod.Bone0 = r.ReadBytes(vertices);
                        lod.Bone1 = r.ReadBytes(vertices);
                        lod.Weight0 = r.ReadBytes(vertices);
                        lod.Region = r.ReadBytes(vertices);
                        lod.Triangles = new ushort[triangles * 3];
                        for (int t = 0; t < lod.Triangles.Length; t++)
                        {
                            lod.Triangles[t] = r.ReadUInt16();
                        }

                        body.Lods[l] = lod;
                    }

                    return body;
                }
            }
            catch (Exception)
            {
                return null; // archivo corto o dañado: el juego usa el cuerpo procedural
            }
        }
    }
}
