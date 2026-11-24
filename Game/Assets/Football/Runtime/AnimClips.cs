using System.IO;
using UnityEngine;

namespace Football.Game
{
    /// <summary>Una animación ya retargeteada a los 20 huesos del juego (ver <c>Tools/mixamo</c>): rotaciones locales por fotograma y vaivén de la pelvis.</summary>
    public sealed class AnimClip
    {
        /// <summary>Nombre del clip.</summary>
        public string Name;
        /// <summary>Se repite sin cortes (carrera, caminata, reposo).</summary>
        public bool Loop;
        /// <summary>Al terminar deja la última pose mientras el estado que la pidió siga vigente.</summary>
        public bool Hold;
        /// <summary>Velocidad del suelo con la que fue grabada (m/s, ya a la escala del jugador); 0 si no avanza.</summary>
        public float GroundSpeed;
        /// <summary>Velocidad de reproducción base.</summary>
        public float Rate;
        /// <summary>Fotogramas.</summary>
        public int Frames;
        /// <summary>Rotaciones locales: <see cref="AnimClips.BoneCount"/> huesos (sin la raíz) por fotograma.</summary>
        public Quaternion[] Rotations;
        /// <summary>Pelvis (x, y, z) por fotograma: desplazamiento en metros respecto de su posición de reposo.</summary>
        public Vector3[] Pelvis;

        /// <summary>Duración en segundos.</summary>
        public float Length => Loop ? Frames / AnimClips.Fps : (Frames - 1) / AnimClips.Fps;
    }

    /// <summary>
    /// Animaciones de fútbol de <c>Resources/Anim/clips.bytes</c> (las genera <c>Tools/mixamo/import_animations.py</c> a partir de las de Mixamo). Se leen una vez;
    /// si el archivo falta, el juego sigue con la animación procedural de siempre.
    /// </summary>
    public static class AnimClips
    {
        /// <summary>Fotogramas por segundo de los clips.</summary>
        public const float Fps = 30f;
        /// <summary>Huesos animados por fotograma (los 20 del juego menos la raíz).</summary>
        public const int BoneCount = 19;
        private const string Magic = "ANM1";
        private const string ResourceName = "Anim/clips";
        private const float QuatScale = 1f / 32767f;

        private static AnimClip[] _clips;
        private static bool _loaded;

        /// <summary>Hay animaciones cargadas.</summary>
        public static bool Available
        {
            get
            {
                Load();
                return _clips != null && _clips.Length > 0;
            }
        }

        /// <summary>Busca un clip por nombre; null si no existe.</summary>
        public static AnimClip Find(string name)
        {
            Load();
            if (_clips == null)
            {
                return null;
            }

            for (int i = 0; i < _clips.Length; i++)
            {
                if (_clips[i].Name == name)
                {
                    return _clips[i];
                }
            }

            return null;
        }

        private static void Load()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            TextAsset asset = Resources.Load<TextAsset>(ResourceName);
            if (asset == null)
            {
                Debug.LogWarning("Football: no se encontró Resources/" + ResourceName + ".bytes; los jugadores usan la animación procedural.");
                return;
            }

            try
            {
                _clips = Parse(asset.bytes);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Football: no se pudieron leer las animaciones (" + e.Message + "); los jugadores usan la animación procedural.");
                _clips = null;
            }
        }

        /// <summary>Lee el contenido de <c>clips.bytes</c>.</summary>
        public static AnimClip[] Parse(byte[] bytes)
        {
            using (var r = new BinaryReader(new MemoryStream(bytes)))
            {
                if (new string(r.ReadChars(4)) != Magic)
                {
                    throw new InvalidDataException("formato desconocido");
                }

                int count = r.ReadInt32();
                var clips = new AnimClip[count];
                for (int c = 0; c < count; c++)
                {
                    var clip = new AnimClip { Name = r.ReadString(), Loop = r.ReadByte() != 0, Hold = r.ReadByte() != 0, GroundSpeed = r.ReadSingle(), Rate = r.ReadSingle() };
                    clip.Frames = r.ReadInt32();
                    clip.Rotations = new Quaternion[clip.Frames * BoneCount];
                    clip.Pelvis = new Vector3[clip.Frames];
                    for (int f = 0; f < clip.Frames; f++)
                    {
                        clip.Pelvis[f] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                        for (int b = 0; b < BoneCount; b++)
                        {
                            var q = new Quaternion(r.ReadInt16() * QuatScale, r.ReadInt16() * QuatScale, r.ReadInt16() * QuatScale, r.ReadInt16() * QuatScale);
                            clip.Rotations[f * BoneCount + b] = q.normalized;
                        }
                    }

                    clips[c] = clip;
                }

                return clips;
            }
        }
    }
}
