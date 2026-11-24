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
    public readonly struct BodyRing
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
        public BodyRing(float y, float rx, float front, float back)
        {
            Y = y;
            Rx = rx;
            Front = front;
            Back = back;
        }

        /// <summary>Polo: punto del eje que cierra la malla.</summary>
        public static BodyRing Pole(float y) => new BodyRing(y, 0f, 0f, 0f);

        /// <summary>Punto de la sección en el ángulo dado (0 = +X, π/2 = delante).</summary>
        public Vector3 Point(float angle, float inflate = 1f, float offset = 0f)
        {
            float s = Mathf.Sin(angle);
            float depth = s >= 0f ? Front : Back;
            return new Vector3(Mathf.Cos(angle) * (Rx * inflate + offset), Y, s * (depth * inflate + offset));
        }
    }

    /// <summary>Huesos del esqueleto del jugador (20): el orden es el de <c>SkinnedMeshRenderer.bones</c>.</summary>
    public enum BodyBone
    {
        /// <summary>Raíz a la altura de los pies: lleva la inclinación de todo el cuerpo (estiradas, barridas).</summary>
        Root,
        /// <summary>Pelvis.</summary>
        Pelvis,
        /// <summary>Columna lumbar.</summary>
        Spine,
        /// <summary>Pecho.</summary>
        Chest,
        /// <summary>Cuello.</summary>
        Neck,
        /// <summary>Cabeza.</summary>
        Head,
        /// <summary>Hombro izquierdo.</summary>
        ShoulderL,
        /// <summary>Codo izquierdo.</summary>
        ElbowL,
        /// <summary>Muñeca izquierda.</summary>
        WristL,
        /// <summary>Hombro derecho.</summary>
        ShoulderR,
        /// <summary>Codo derecho.</summary>
        ElbowR,
        /// <summary>Muñeca derecha.</summary>
        WristR,
        /// <summary>Cadera izquierda.</summary>
        HipL,
        /// <summary>Rodilla izquierda.</summary>
        KneeL,
        /// <summary>Tobillo izquierdo.</summary>
        AnkleL,
        /// <summary>Puntera izquierda.</summary>
        ToeL,
        /// <summary>Cadera derecha.</summary>
        HipR,
        /// <summary>Rodilla derecha.</summary>
        KneeR,
        /// <summary>Tobillo derecho.</summary>
        AnkleR,
        /// <summary>Puntera derecha.</summary>
        ToeR
    }

    /// <summary>Lo que define el aspecto de un jugador para armar su malla (colores, peinado, vello facial, ropa y decales).</summary>
    public struct BodySpec
    {
        /// <summary>Camiseta.</summary>
        public Color Shirt;
        /// <summary>Color secundario (diseño y franja de las medias).</summary>
        public Color Secondary;
        /// <summary>Diseño de la camiseta (índice de <see cref="KitPattern"/>; 0 lisa).</summary>
        public int Pattern;
        /// <summary>Pantalón.</summary>
        public Color Shorts;
        /// <summary>Medias.</summary>
        public Color Socks;
        /// <summary>Piel.</summary>
        public Color Skin;
        /// <summary>Pelo, cejas y barba.</summary>
        public Color Hair;
        /// <summary>Botines.</summary>
        public Color Boots;
        /// <summary>Guantes del arquero.</summary>
        public Color Gloves;
        /// <summary>Peinado (0 corto, 1 degradé, 2 melena con vincha, 3 rulos, 4 rapado, 5 jopo).</summary>
        public int HairStyle;
        /// <summary>Vello facial (0 nada, 1 barba, 2 candado).</summary>
        public int FacialHair;
        /// <summary>Arquero (guantes).</summary>
        public bool Keeper;
        /// <summary>Pantalón largo.</summary>
        public bool Trousers;
        /// <summary>Manga larga.</summary>
        public bool LongSleeves;
        /// <summary>Dorsal de la espalda (1–99); 0 o menos = sin dorsal.</summary>
        public int Number;
        /// <summary>Escudo del pecho: variante del atlas (0–<see cref="PlayerAtlasData.CrestCount"/> − 1); negativo = sin escudo.</summary>
        public int Crest;
    }

    /// <summary>
    /// Malla única del jugador para un <c>SkinnedMeshRenderer</c> de 20 huesos, armada sin Unity (solo listas). Cada pierna y
    /// cada brazo es **una sola superficie continua** de la cadera al tobillo y del hombro a la muñeca, con tres anillos de
    /// soporte en cada flexión (sobre, en y bajo la articulación) y pesos de dos huesos que se interpolan con suavidad; la ropa
    /// (short, medias, mangas) son capas por encima. El tronco, el cuello y la cabeza llevan oclusión ambiental pintada en el color de
    /// los vértices (sin costo de GPU). La camiseta lleva proyección plana para el dorsal y el escudo (los lee el shader del atlas
    /// <see cref="PlayerAtlasData"/>). La cara tiene cuencas oculares, arco superciliar, pómulos, labios y mentón esculpidos y
    /// ojos, iris y labios en el color de los vértices; el pelo y la barba son mechones volumétricos. Hay dos niveles de detalle
    /// (<c>lod</c> 0 completo, 1 simplificado, mismo esqueleto). En el alfa del color va el material (1 piel, 0,75 tela, 0,5 botín,
    /// 0,25 pelo); UV0 = máscaras (x sudor, y pliegues); UV2 = proyección del decal; UV3 = dorsal y escudo; UV4 = color de la tinta.
    /// </summary>
    public sealed class SkinnedBodyGeometry
    {
        /// <summary>Cantidad de huesos.</summary>
        public const int BoneCount = 20;
        /// <summary>Escala de la cabeza (figura de unas 7,6 cabezas).</summary>
        public const float HeadScale = 0.8f;
        /// <summary>Material piel (alfa del color del vértice).</summary>
        public const float SkinId = 1f;
        /// <summary>Material tela.</summary>
        public const float FabricId = 0.75f;
        /// <summary>Material botín.</summary>
        public const float BootId = 0.5f;
        /// <summary>Material pelo.</summary>
        public const float HairId = 0.25f;
        /// <summary>Oscurecimiento máximo que puede sumar la oclusión ambiental (0,25 = 25 %).</summary>
        public const float MaxOcclusion = 0.25f;

        /// <summary>Padre de cada hueso (−1 para la raíz).</summary>
        public static readonly int[] Parents = { -1, 0, 1, 2, 3, 4, 3, 6, 7, 3, 9, 10, 1, 12, 13, 14, 1, 16, 17, 18 };

        /// <summary>Posición de cada hueso en reposo, en el espacio del jugador (pies en el origen, mirando a +Z).</summary>
        private static readonly Vector3[] ProceduralBind =
        {
            new Vector3(0f, 0f, 0f), new Vector3(0f, 0.95f, 0f), new Vector3(0f, 1.1f, 0f), new Vector3(0f, 1.28f, 0f),
            new Vector3(0f, 1.45f, 0f), new Vector3(0f, 1.53f, 0f),
            new Vector3(-0.235f, 1.4f, 0f), new Vector3(-0.235f, 1.11f, 0f), new Vector3(-0.235f, 0.875f, 0f),
            new Vector3(0.235f, 1.4f, 0f), new Vector3(0.235f, 1.11f, 0f), new Vector3(0.235f, 0.875f, 0f),
            new Vector3(-0.11f, 0.92f, 0f), new Vector3(-0.11f, 0.48f, 0f), new Vector3(-0.11f, 0.06f, 0f), new Vector3(-0.11f, 0.025f, 0.11f),
            new Vector3(0.11f, 0.92f, 0f), new Vector3(0.11f, 0.48f, 0f), new Vector3(0.11f, 0.06f, 0f), new Vector3(0.11f, 0.025f, 0.11f)
        };

        /// <summary>
        /// Posición de cada hueso en reposo, en el espacio del jugador: la del cuerpo procedural o, si hay un cuerpo importado instalado
        /// (<see cref="HumanoidBody.Install"/>), la de ese esqueleto.
        /// </summary>
        public static Vector3[] BindPositions { get; private set; } = ProceduralBind;

        private static HumanoidBody _humanoid;

        /// <summary>Cambia entre el cuerpo importado (instalado con <see cref="HumanoidBody.Install"/>) y el procedural (null).</summary>
        /// <param name="body">Cuerpo importado o null.</param>
        public static void UseHumanoid(HumanoidBody body)
        {
            _humanoid = body;
            BindPositions = body != null ? body.Bones : ProceduralBind;
        }

        /// <summary>Hay un cuerpo importado en uso.</summary>
        public static bool UsesHumanoid => _humanoid != null;

        /// <summary>Nombres de los huesos (los del pedido: Pelvis, Spine, Chest… Toe_R).</summary>
        public static readonly string[] BoneNames =
        {
            "Root", "Pelvis", "Spine", "Chest", "Neck", "Head", "Shoulder_L", "Elbow_L", "Wrist_L", "Shoulder_R", "Elbow_R", "Wrist_R",
            "Hip_L", "Knee_L", "Ankle_L", "Toe_L", "Hip_R", "Knee_R", "Ankle_R", "Toe_R"
        };

        // Radio de mezcla de cada hueso con su padre (m) y dirección del segmento (hacia el hijo).
        private static readonly float[] BlendRadius = { 0f, 0f, 0.07f, 0.08f, 0.035f, 0.02f, 0.09f, 0.06f, 0.03f, 0.09f, 0.06f, 0.03f, 0.07f, 0.06f, 0.03f, 0.04f, 0.07f, 0.06f, 0.03f, 0.04f };
        private static readonly int[] BlendChild = { -1, 2, 3, 4, 5, -1, 7, 8, -1, 10, 11, -1, 13, 14, 15, -1, 17, 18, 19, -1 };
        private static readonly Vector3 Up = new Vector3(0f, 1f, 0f), Down = new Vector3(0f, -1f, 0f), Forward = new Vector3(0f, 0f, 1f);
        private static readonly Vector3[] Axis = { Up, Up, Up, Up, Up, Up, Down, Down, Down, Down, Down, Down, Down, Down, Down, Forward, Down, Down, Down, Forward };
        private static readonly int[] ArmChainL = { (int)BodyBone.ShoulderL, (int)BodyBone.ElbowL, (int)BodyBone.WristL };
        private static readonly int[] ArmChainR = { (int)BodyBone.ShoulderR, (int)BodyBone.ElbowR, (int)BodyBone.WristR };
        private static readonly int[] LegChainL = { (int)BodyBone.HipL, (int)BodyBone.KneeL, (int)BodyBone.AnkleL };
        private static readonly int[] LegChainR = { (int)BodyBone.HipR, (int)BodyBone.KneeR, (int)BodyBone.AnkleR };
        private const int TrunkBone = -1; // la pieza del tronco toma el hueso según la altura
        private const float ShoulderBlendFrom = 0.13f, ShoulderBlendTo = 0.23f, ShoulderBlendHeight = 1.3f, ShoulderBlendMax = 0.8f;
        private const float GroinTop = 0.88f, GroinBottom = 0.8f, GroinInner = 0.03f, GroinOuter = 0.09f, GroinMax = 0.6f;
        private const float ToeBlendFrom = 0.07f, ToeBlendTo = 0.15f;

        private const float PatternInflate = 1.035f, PatternOffset = 0.003f;
        private static readonly Color Sclera = new Color(0.93f, 0.93f, 0.9f), Iris = new Color(0.16f, 0.1f, 0.06f), LipColor = new Color(0.55f, 0.27f, 0.25f);
        private static readonly Color MouthLine = new Color(0.32f, 0.15f, 0.14f);
        private const float SoleLighten = 0.65f;

        // Decales: parche del dorsal (centro, ancho, alto) en la espalda y del escudo en el pecho izquierdo del jugador.
        private const float BackPatchY = 1.285f, BackPatchW = 0.26f, BackPatchH = 0.2f;
        private const float CrestPatchX = 0.095f, CrestPatchY = 1.34f, CrestPatchW = 0.075f, CrestPatchH = 0.09f;
        private const float DecalFlagRamp = 0.04f;
        private const float InkDark = 0.07f, InkLight = 0.96f, InkContrast = 0.35f;

        // ---- perfiles (en metros; tronco desde el suelo, el resto en el espacio de su articulación) ----

        private static readonly BodyRing[] Torso =
        {
            BodyRing.Pole(1.525f), new BodyRing(1.515f, 0.07f, 0.052f, 0.056f), new BodyRing(1.5f, 0.115f, 0.068f, 0.078f),
            new BodyRing(1.47f, 0.178f, 0.092f, 0.098f), new BodyRing(1.42f, 0.208f, 0.118f, 0.108f), new BodyRing(1.36f, 0.2f, 0.138f, 0.118f),
            new BodyRing(1.29f, 0.186f, 0.142f, 0.116f), new BodyRing(1.2f, 0.166f, 0.124f, 0.106f), new BodyRing(1.1f, 0.146f, 0.106f, 0.098f),
            new BodyRing(1.02f, 0.15f, 0.104f, 0.1f),
            new BodyRing(0.985f, 0.176f, 0.12f, 0.115f), new BodyRing(0.975f, 0.168f, 0.11f, 0.106f), BodyRing.Pole(0.97f) // ruedo sobre el pantalón
        };

        private static readonly BodyRing[] TorsoLow =
        {
            BodyRing.Pole(1.525f), new BodyRing(1.47f, 0.178f, 0.092f, 0.098f), new BodyRing(1.36f, 0.2f, 0.138f, 0.118f), new BodyRing(1.29f, 0.186f, 0.142f, 0.116f),
            new BodyRing(1.2f, 0.166f, 0.124f, 0.106f), new BodyRing(1.02f, 0.15f, 0.104f, 0.1f), new BodyRing(0.98f, 0.172f, 0.115f, 0.11f), BodyRing.Pole(0.97f)
        };

        // Cuello de la camiseta: aro de color alrededor de la base del cuello.
        private static readonly BodyRing[] Collar =
        {
            BodyRing.Pole(1.548f), new BodyRing(1.543f, 0.064f, 0.06f, 0.068f), new BodyRing(1.51f, 0.082f, 0.074f, 0.088f), BodyRing.Pole(1.495f)
        };

        private static readonly BodyRing[] Pelvis =
        {
            BodyRing.Pole(1.01f), new BodyRing(1.0f, 0.166f, 0.108f, 0.104f), new BodyRing(0.93f, 0.186f, 0.115f, 0.13f),
            new BodyRing(0.86f, 0.19f, 0.112f, 0.128f), new BodyRing(0.81f, 0.17f, 0.1f, 0.112f), BodyRing.Pole(0.79f)
        };

        private static readonly BodyRing[] PelvisLow =
        {
            BodyRing.Pole(1.01f), new BodyRing(0.93f, 0.186f, 0.115f, 0.13f), new BodyRing(0.82f, 0.18f, 0.105f, 0.12f), BodyRing.Pole(0.79f)
        };

        private static readonly BodyRing[] ShortLeg =
        {
            BodyRing.Pole(0.035f), new BodyRing(0.025f, 0.09f, 0.09f, 0.092f), new BodyRing(-0.08f, 0.096f, 0.098f, 0.095f),
            new BodyRing(-0.16f, 0.102f, 0.102f, 0.099f), new BodyRing(-0.172f, 0.108f, 0.108f, 0.104f), new BodyRing(-0.178f, 0.097f, 0.097f, 0.094f),
            BodyRing.Pole(-0.18f) // botamanga abierta
        };

        private static readonly BodyRing[] ShortLegLow =
        {
            BodyRing.Pole(0.035f), new BodyRing(0.0f, 0.095f, 0.095f, 0.094f), new BodyRing(-0.17f, 0.106f, 0.106f, 0.102f), BodyRing.Pole(-0.18f)
        };

        // Pierna de la cadera al tobillo, una sola superficie. Anillos de soporte de la rodilla (y = −0,44): −0,38, −0,44 y −0,50.
        private static readonly BodyRing[] LegSkin =
        {
            BodyRing.Pole(-0.10f), new BodyRing(-0.18f, 0.09f, 0.1f, 0.088f), new BodyRing(-0.22f, 0.086f, 0.094f, 0.082f),
            new BodyRing(-0.30f, 0.076f, 0.085f, 0.074f), new BodyRing(-0.38f, 0.06f, 0.066f, 0.06f), new BodyRing(-0.44f, 0.054f, 0.066f, 0.052f),
            new BodyRing(-0.50f, 0.06f, 0.054f, 0.082f), new BodyRing(-0.58f, 0.058f, 0.05f, 0.08f), new BodyRing(-0.69f, 0.045f, 0.042f, 0.05f),
            new BodyRing(-0.79f, 0.032f, 0.033f, 0.032f), new BodyRing(-0.84f, 0.03f, 0.031f, 0.03f), BodyRing.Pole(-0.865f)
        };

        private static readonly BodyRing[] LegSkinLow =
        {
            BodyRing.Pole(-0.10f), new BodyRing(-0.14f, 0.09f, 0.1f, 0.088f), new BodyRing(-0.30f, 0.076f, 0.085f, 0.074f), new BodyRing(-0.38f, 0.06f, 0.066f, 0.06f),
            new BodyRing(-0.44f, 0.054f, 0.066f, 0.052f), new BodyRing(-0.50f, 0.06f, 0.054f, 0.082f), new BodyRing(-0.69f, 0.045f, 0.042f, 0.05f),
            new BodyRing(-0.84f, 0.03f, 0.031f, 0.03f), BodyRing.Pole(-0.865f)
        };

        private static readonly BodyRing[] Sock =
        {
            BodyRing.Pole(-0.506f), new BodyRing(-0.51f, 0.063f, 0.058f, 0.086f), new BodyRing(-0.58f, 0.061f, 0.054f, 0.084f), new BodyRing(-0.69f, 0.049f, 0.046f, 0.054f),
            new BodyRing(-0.79f, 0.034f, 0.035f, 0.034f), new BodyRing(-0.845f, 0.032f, 0.033f, 0.032f), BodyRing.Pole(-0.85f)
        };

        private static readonly BodyRing[] SockLow =
        {
            BodyRing.Pole(-0.506f), new BodyRing(-0.51f, 0.063f, 0.058f, 0.086f), new BodyRing(-0.69f, 0.049f, 0.046f, 0.054f), new BodyRing(-0.845f, 0.032f, 0.033f, 0.032f), BodyRing.Pole(-0.85f)
        };

        private static readonly BodyRing[] SockCuff = // borde superior enrollado de la media
        {
            BodyRing.Pole(-0.498f), new BodyRing(-0.5f, 0.068f, 0.064f, 0.092f), new BodyRing(-0.522f, 0.068f, 0.063f, 0.091f), BodyRing.Pole(-0.524f)
        };

        private static readonly BodyRing[] SockBand =
        {
            BodyRing.Pole(-0.536f), new BodyRing(-0.538f, 0.066f, 0.061f, 0.09f), new BodyRing(-0.562f, 0.065f, 0.059f, 0.088f), BodyRing.Pole(-0.564f)
        };

        // Botín a lo largo de Y (talón en −Y, puntera en +Y); se gira 90° en X para que la puntera mire hacia delante.
        private static readonly BodyRing[] Boot =
        {
            BodyRing.Pole(-0.078f), new BodyRing(-0.068f, 0.036f, 0.03f, 0.04f), new BodyRing(-0.025f, 0.043f, 0.033f, 0.072f),
            new BodyRing(0.035f, 0.046f, 0.033f, 0.056f), new BodyRing(0.1f, 0.044f, 0.031f, 0.036f), new BodyRing(0.15f, 0.039f, 0.029f, 0.026f),
            new BodyRing(0.185f, 0.027f, 0.023f, 0.016f), BodyRing.Pole(0.2f)
        };

        private static readonly BodyRing[] BootLow =
        {
            BodyRing.Pole(-0.078f), new BodyRing(-0.05f, 0.04f, 0.032f, 0.056f), new BodyRing(0.035f, 0.046f, 0.033f, 0.056f), new BodyRing(0.15f, 0.039f, 0.029f, 0.026f), BodyRing.Pole(0.2f)
        };

        // Brazo del hombro a la muñeca, una sola superficie. Anillos de soporte del codo (y = −0,29): −0,24, −0,29 y −0,34.
        private static readonly BodyRing[] ArmSkin =
        {
            BodyRing.Pole(-0.095f), new BodyRing(-0.1f, 0.052f, 0.058f, 0.05f), new BodyRing(-0.2f, 0.045f, 0.048f, 0.042f),
            new BodyRing(-0.24f, 0.04f, 0.042f, 0.039f), new BodyRing(-0.29f, 0.038f, 0.037f, 0.038f), new BodyRing(-0.34f, 0.044f, 0.043f, 0.04f),
            new BodyRing(-0.44f, 0.034f, 0.032f, 0.032f), new BodyRing(-0.525f, 0.024f, 0.02f, 0.02f), BodyRing.Pole(-0.54f)
        };

        private static readonly BodyRing[] ArmSkinLow =
        {
            BodyRing.Pole(-0.095f), new BodyRing(-0.12f, 0.05f, 0.056f, 0.048f), new BodyRing(-0.24f, 0.04f, 0.042f, 0.039f), new BodyRing(-0.29f, 0.038f, 0.037f, 0.038f),
            new BodyRing(-0.34f, 0.044f, 0.043f, 0.04f), new BodyRing(-0.525f, 0.024f, 0.02f, 0.02f), BodyRing.Pole(-0.54f)
        };

        private static readonly BodyRing[] Sleeve =
        {
            BodyRing.Pole(0.07f), new BodyRing(0.06f, 0.04f, 0.04f, 0.04f), new BodyRing(0.04f, 0.056f, 0.056f, 0.054f), new BodyRing(0.0f, 0.062f, 0.063f, 0.06f),
            new BodyRing(-0.06f, 0.06f, 0.062f, 0.057f), new BodyRing(-0.118f, 0.057f, 0.06f, 0.054f), new BodyRing(-0.128f, 0.062f, 0.065f, 0.059f),
            BodyRing.Pole(-0.133f) // borde de la manga (vuelo y dobladillo)
        };

        private static readonly BodyRing[] SleeveLow =
        {
            BodyRing.Pole(0.07f), new BodyRing(0.04f, 0.056f, 0.056f, 0.054f), new BodyRing(-0.02f, 0.062f, 0.063f, 0.06f), new BodyRing(-0.125f, 0.059f, 0.062f, 0.056f), BodyRing.Pole(-0.133f)
        };

        private static readonly BodyRing[] LongSleeve =
        {
            BodyRing.Pole(0.062f), new BodyRing(0.052f, 0.064f, 0.064f, 0.064f), new BodyRing(-0.02f, 0.069f, 0.069f, 0.066f), new BodyRing(-0.12f, 0.062f, 0.064f, 0.058f),
            new BodyRing(-0.2f, 0.05f, 0.052f, 0.048f), new BodyRing(-0.24f, 0.047f, 0.048f, 0.046f), new BodyRing(-0.29f, 0.046f, 0.046f, 0.046f),
            new BodyRing(-0.34f, 0.047f, 0.047f, 0.046f), new BodyRing(-0.44f, 0.04f, 0.04f, 0.04f), new BodyRing(-0.52f, 0.034f, 0.034f, 0.034f), BodyRing.Pole(-0.53f)
        };

        private static readonly BodyRing[] LongSleeveLow =
        {
            BodyRing.Pole(0.062f), new BodyRing(-0.02f, 0.069f, 0.069f, 0.066f), new BodyRing(-0.24f, 0.047f, 0.048f, 0.046f), new BodyRing(-0.34f, 0.047f, 0.047f, 0.046f),
            new BodyRing(-0.52f, 0.034f, 0.034f, 0.034f), BodyRing.Pole(-0.53f)
        };

        private static readonly BodyRing[] Hand =
        {
            BodyRing.Pole(0.008f), new BodyRing(-0.01f, 0.03f, 0.016f, 0.016f), new BodyRing(-0.05f, 0.036f, 0.018f, 0.018f),
            new BodyRing(-0.09f, 0.03f, 0.015f, 0.014f), BodyRing.Pole(-0.108f)
        };

        private static readonly BodyRing[] HandLow = { BodyRing.Pole(0.008f), new BodyRing(-0.05f, 0.035f, 0.018f, 0.018f), BodyRing.Pole(-0.108f) };

        private static readonly BodyRing[] Neck =
        {
            BodyRing.Pole(1.63f), new BodyRing(1.61f, 0.05f, 0.052f, 0.05f), new BodyRing(1.55f, 0.056f, 0.052f, 0.056f),
            new BodyRing(1.49f, 0.066f, 0.062f, 0.062f), BodyRing.Pole(1.47f)
        };

        private static readonly BodyRing[] Skull =
        {
            BodyRing.Pole(0.275f), new BodyRing(0.262f, 0.05f, 0.05f, 0.052f), new BodyRing(0.232f, 0.09f, 0.1f, 0.104f),
            new BodyRing(0.18f, 0.104f, 0.115f, 0.117f), new BodyRing(0.12f, 0.1f, 0.115f, 0.11f), new BodyRing(0.07f, 0.086f, 0.11f, 0.094f),
            new BodyRing(0.032f, 0.062f, 0.096f, 0.06f), new BodyRing(0.012f, 0.036f, 0.07f, 0.028f), BodyRing.Pole(0.0f)
        };

        // Alturas de los anillos de la cara (cuencas 0,16 y 0,148, arco 0,185, labios 0,085 / 0,075 / 0,065, mentón 0,032 y 0,012).
        private static readonly float[] FaceRows =
            { 0.262f, 0.22f, 0.2f, 0.185f, 0.172f, 0.16f, 0.148f, 0.12f, 0.1f, 0.085f, 0.075f, 0.065f, 0.05f, 0.032f, 0.012f };
        private static readonly float[] FaceRowsLow = { 0.262f, 0.22f, 0.17f, 0.12f, 0.07f, 0.03f };

        private static readonly BodyRing[] Nose =
        {
            BodyRing.Pole(0.158f), new BodyRing(0.145f, 0.011f, 0.01f, 0.004f), new BodyRing(0.115f, 0.016f, 0.026f, 0.006f), BodyRing.Pole(0.104f)
        };

        private static readonly BodyRing[] HairCap =
        {
            BodyRing.Pole(0.29f), new BodyRing(0.279f, 0.056f, 0.056f, 0.058f), new BodyRing(0.248f, 0.097f, 0.106f, 0.11f),
            new BodyRing(0.2f, 0.112f, 0.11f, 0.124f), new BodyRing(0.16f, 0.112f, 0.075f, 0.122f), new BodyRing(0.12f, 0.104f, 0.02f, 0.115f),
            BodyRing.Pole(0.11f)
        };

        private static readonly BodyRing[] HairCapLow = { BodyRing.Pole(0.29f), new BodyRing(0.248f, 0.097f, 0.106f, 0.11f), new BodyRing(0.16f, 0.112f, 0.075f, 0.122f), BodyRing.Pole(0.11f) };

        // ---- oclusión ambiental pintada (centro, radios, fuerza); los marcados se espejan en x ----

        private struct Occluder
        {
            public Vector3 Centre, Radii;
            public float Strength;
            public bool Mirror;

            public Occluder(float x, float y, float z, float rx, float ry, float rz, float strength, bool mirror)
            {
                Centre = new Vector3(x, y, z);
                Radii = new Vector3(rx, ry, rz);
                Strength = strength;
                Mirror = mirror;
            }
        }

        private static readonly Occluder[] Occluders =
        {
            new Occluder(0f, 1.52f, 0.05f, 0.08f, 0.06f, 0.08f, 0.22f, false),     // bajo el mentón y la base del cuello
            new Occluder(0.2f, 1.3f, 0f, 0.06f, 0.08f, 0.09f, 0.25f, true),        // axilas
            new Occluder(0f, 0.85f, 0f, 0.1f, 0.08f, 0.1f, 0.25f, false),          // entrepierna
            new Occluder(0.19f, 1.285f, 0f, 0.05f, 0.05f, 0.07f, 0.18f, true),     // borde interno de las mangas
            new Occluder(0f, 0.96f, 0f, 0.3f, 0.04f, 0.3f, 0.2f, false),           // bajo el ruedo de la camiseta
            new Occluder(0.11f, 0.7f, 0f, 0.12f, 0.05f, 0.12f, 0.22f, true),       // bajo el ruedo del short
            new Occluder(0.11f, 0.42f, 0f, 0.1f, 0.05f, 0.1f, 0.2f, true),         // pliegue superior de las medias
            new Occluder(0.11f, 0.48f, -0.05f, 0.07f, 0.05f, 0.05f, 0.15f, true),  // hueco detrás de la rodilla
        };

        // ---- salida ----

        /// <summary>Nivel de detalle con el que se armó.</summary>
        public int Lod { get; }
        /// <summary>Posiciones (espacio del jugador, en reposo).</summary>
        public readonly List<Vector3> Vertices = new List<Vector3>();
        /// <summary>Color por vértice con la oclusión ambiental aplicada (alfa = material).</summary>
        public readonly List<Color> Colors = new List<Color>();
        /// <summary>Color por vértice sin oclusión (para el material plano de respaldo).</summary>
        public readonly List<Color> BaseColors = new List<Color>();
        /// <summary>Máscaras para el shader: x sudor, y pliegues.</summary>
        public readonly List<Vector2> Masks = new List<Vector2>();
        /// <summary>Proyección del decal (x, y = coordenadas del parche; z = −1 dorsal, +1 escudo, 0 ninguno).</summary>
        public readonly List<Vector3> DecalUv = new List<Vector3>();
        /// <summary>Contenido del decal: x = decena (−1 si no hay), y = unidad (−1 si no hay dorsal), z = variante de escudo (−1 si no hay).</summary>
        public readonly List<Vector3> DecalData = new List<Vector3>();
        /// <summary>Color de la tinta del decal.</summary>
        public readonly List<Vector3> DecalInk = new List<Vector3>();
        /// <summary>Triángulos.</summary>
        public readonly List<int> Triangles = new List<int>();
        /// <summary>Hueso principal de cada vértice.</summary>
        public readonly List<int> Bone0 = new List<int>();
        /// <summary>Segundo hueso de cada vértice.</summary>
        public readonly List<int> Bone1 = new List<int>();
        /// <summary>Peso del hueso principal (el segundo pesa 1 − este).</summary>
        public readonly List<float> Weight0 = new List<float>();
        /// <summary>Cada vértice pertenece a un tramo continuo de brazo o pierna (para medir costuras y deformación).</summary>
        public readonly List<int> Group = new List<int>();

        private readonly List<Vector3> _local = new List<Vector3>();
        private readonly List<int> _localTris = new List<int>();
        private readonly bool _low;
        private BodySpec _spec;
        private Vector3 _ink;
        private int _tens, _units;
        private int _group;

        private SkinnedBodyGeometry(int lod)
        {
            Lod = lod;
            _low = lod > 0;
        }

        /// <summary>Cantidad de triángulos.</summary>
        public int TriangleCount => Triangles.Count / 3;

        private int TorsoSides => _low ? 9 : 14;
        private int LimbSides => _low ? 7 : 8;
        private int ArmSides => _low ? 6 : 8;
        private int HeadSides => _low ? 10 : 12;

        /// <summary>Arma la malla de un jugador.</summary>
        /// <param name="spec">Aspecto.</param>
        /// <param name="lod">0 = detalle completo (~2.500 triángulos), 1 = simplificado (~800–900).</param>
        public static SkinnedBodyGeometry Build(BodySpec spec, int lod = 0)
        {
            var g = new SkinnedBodyGeometry(lod) { _spec = spec };
            g.PrepareDecals();
            if (_humanoid != null)
            {
                g.BuildImportedBody(_humanoid.Lods[Math.Min(lod, _humanoid.Lods.Length - 1)]);
            }
            else
            {
                g.BuildTrunk();
                for (int side = -1; side <= 1; side += 2)
                {
                    g.BuildLeg(side);
                    g.BuildArm(side);
                }
            }

            g.BuildHead();
            return g;
        }

        private void PrepareDecals()
        {
            int n = _spec.Number;
            _tens = n >= 10 ? (n / 10 > 9 ? 9 : n / 10) : -1;
            _units = n > 0 ? n % 10 : -1;
            // Tinta: el color secundario si contrasta con la camiseta; si no, blanco u oscuro según la luminosidad.
            Color shirt = _spec.Shirt, second = _spec.Secondary;
            float diff = Mathf.Abs(shirt.grayscale - second.grayscale);
            Color ink = diff >= InkContrast ? second : (shirt.grayscale > 0.5f ? new Color(InkDark, InkDark, InkDark) : new Color(InkLight, InkLight, InkLight));
            _ink = new Vector3(ink.r, ink.g, ink.b);
        }

        // ------------------------------------------------------------------ partes

        private void BuildTrunk()
        {
            Color shirt = Tag(_spec.Shirt, FabricId), shorts = Tag(_spec.Shorts, FabricId);
            _group = 0;
            Sculpt(_low ? TorsoLow : Torso, TorsoSides, null, Place.Identity, shirt, Bind.Auto, FoldWaist, true, true, DecalMode.Torso);
            if (_spec.Pattern != 0 && !_low)
            {
                BuildPattern(_spec.Pattern, Tag(_spec.Secondary, FabricId));
            }

            Sculpt(_low ? PelvisLow : Pelvis, TorsoSides, null, Place.Identity, shorts, Bind.Auto, FoldGroin, false, true);
            Sculpt(Neck, _low ? 6 : 8, null, Place.Identity, Tag(_spec.Skin, SkinId), Bind.Auto, SweatNeck, false, false);
            if (!_low)
            {
                Sculpt(Collar, 10, null, Place.Identity, Tag(_spec.Secondary, FabricId), Bind.Auto, NoMask, false, true);
            }
        }

        // ---- cuerpo importado (Tools/ImportHumanoid) ----

        private const float NeckBackShift = -0.02f; // el cuello del modelo importado está un poco más atrás que el procedural
        private const int ArmBoneFirst = (int)BodyBone.ShoulderL, ArmBoneLast = (int)BodyBone.WristR;
        private const float PatternStripeHalf = 0.04f, PatternStripeStep = 0.13f, PatternBandLow = 1.28f, PatternBandHigh = 1.42f;
        private const float PatternSashRise = 0.75f, PatternSashHalf = 0.045f, PatternSashCentreY = 1.29f, PatternChevronHalf = 0.035f, PatternChevronY = 1.32f;

        /// <summary>
        /// Cuerpo del cuello para abajo a partir del modelo importado: cada vértice se pinta según su región (piel, camiseta con su diseño, short, media, botín),
        /// con los guantes del arquero, el pantalón largo y la manga larga cuando corresponde. El cuello y el cuello de la camiseta son piezas procedurales.
        /// </summary>
        private void BuildImportedBody(HumanoidBody.Lod lod)
        {
            _group = 0;
            Color skin = Tag(_spec.Skin, SkinId), shirt = Tag(_spec.Shirt, FabricId), trim = Tag(_spec.Secondary, FabricId);
            Color shorts = Tag(_spec.Shorts, FabricId), socks = Tag(_spec.Socks, FabricId), stripe = Tag(SockStripe(), FabricId);
            Color boot = Tag(_spec.Boots, BootId), glove = Tag(_spec.Gloves, FabricId);
            int offset = Vertices.Count;
            for (int i = 0; i < lod.Positions.Length; i++)
            {
                Vector3 p = lod.Positions[i];
                int b0 = lod.Bone0[i];
                bool arm = b0 >= ArmBoneFirst && b0 <= ArmBoneLast;
                bool hand = b0 == (int)BodyBone.WristL || b0 == (int)BodyBone.WristR;
                BodyRegion region = (BodyRegion)lod.Region[i];
                Color color;
                DecalMode decal = DecalMode.None;
                switch (region)
                {
                    case BodyRegion.Shirt:
                        color = _spec.Pattern != 0 && InShirtPattern(_spec.Pattern, p, arm) ? trim : shirt;
                        decal = arm ? DecalMode.None : DecalMode.Torso;
                        break;
                    case BodyRegion.Shorts: color = shorts; break;
                    case BodyRegion.Sock: color = _spec.Trousers ? shorts : socks; break;
                    case BodyRegion.Stripe: color = _spec.Trousers ? shorts : stripe; break;
                    case BodyRegion.Boot: color = boot; break;
                    default:
                        color = skin;
                        if (_spec.Keeper && hand)
                        {
                            color = glove;
                        }
                        else if (_spec.LongSleeves && arm && !hand)
                        {
                            color = shirt;
                        }
                        else if (_spec.Trousers && !arm && b0 >= (int)BodyBone.HipL)
                        {
                            color = shorts;
                        }

                        break;
                }

                bool isSkin = color.a == SkinId;
                Vertices.Add(p);
                BaseColors.Add(color);
                Colors.Add(color);
                Masks.Add(isSkin ? new Vector2(0.35f, 0f) : Vector2.zero);
                Bone0.Add(b0);
                Bone1.Add(lod.Bone1[i]);
                Weight0.Add(lod.Weight0[i] / 255f);
                Group.Add(_group);
                AddDecal(decal, p);
            }

            for (int i = 0; i < lod.Triangles.Length; i++)
            {
                Triangles.Add(offset + lod.Triangles[i]);
            }

            // Cuello (procedural, une el cuerpo con la cabeza) y cuello de la camiseta.
            Place neckAt = Place.Identity.Then(new Vector3(0f, 0f, NeckBackShift), Vector3.zero, Vector3.one);
            Sculpt(Neck, _low ? 6 : 8, null, neckAt, skin, Bind.Single((int)BodyBone.Neck), SweatNeck, false, false);
            if (!_low)
            {
                Sculpt(Collar, 10, null, neckAt, trim, Bind.Single((int)BodyBone.Chest), NoMask, false, true);
            }
        }

        /// <summary>Si un punto de la camiseta cae sobre el diseño (rayas, franja, banda cruzada, V o mitades) y entonces va en el color secundario.</summary>
        private static bool InShirtPattern(int pattern, Vector3 p, bool sleeve)
        {
            switch (pattern)
            {
                case 1: // rayas verticales
                {
                    double k = Math.Round(p.x / PatternStripeStep); // tres rayas: en el centro y a cada lado
                    return !sleeve && Math.Abs(k) <= 1 && Math.Abs(p.x - k * PatternStripeStep) < PatternStripeHalf;
                }
                case 2: // franja horizontal
                    return !sleeve && p.y > PatternBandLow && p.y < PatternBandHigh;
                case 3: // banda cruzada
                    return !sleeve && Mathf.Abs((p.y - PatternSashCentreY) * PatternSashRise - p.x) < PatternSashHalf;
                case 4: // V
                    return !sleeve && p.y < PatternChevronY && Mathf.Abs(Mathf.Abs(p.x) - (PatternChevronY - p.y) * 1.1f) < PatternChevronHalf;
                case 5: // mitades
                    return !sleeve && p.x > 0f;
                case 6: // mangas en contraste (el cuello ya va en el color secundario)
                    return sleeve;
                default:
                    return false;
            }
        }

        private void BuildLeg(int side)
        {
            int hip = side < 0 ? (int)BodyBone.HipL : (int)BodyBone.HipR;
            int ankle = hip + 2;
            int[] chain = side < 0 ? LegChainL : LegChainR;
            Color skin = Tag(_spec.Skin, SkinId), shorts = Tag(_spec.Shorts, FabricId), socks = Tag(_spec.Socks, FabricId);
            Color leg = _spec.Trousers ? shorts : skin;
            Place at = At(hip);
            _group = side < 0 ? 1 : 2;
            Sculpt(_low ? LegSkinLow : LegSkin, LimbSides, null, at, leg, Bind.Joints(chain), _spec.Trousers ? FoldKnee : SweatLimb, false, false);
            _group = 0;
            Sculpt(_low ? ShortLegLow : ShortLeg, LimbSides, null, at, shorts, Bind.Single(hip), FoldGroin, false, true);
            if (!_spec.Trousers)
            {
                Sculpt(_low ? SockLow : Sock, LimbSides, null, at, socks, Bind.Joints(chain), FoldAnkle, false, false);
                if (!_low)
                {
                    Sculpt(SockCuff, LimbSides, null, at, socks, Bind.Joints(chain), FoldAnkle, false, false);
                    Sculpt(SockBand, LimbSides, null, at, Tag(SockStripe(), FabricId), Bind.Joints(chain), FoldAnkle, false, false);
                }
            }

            Color boot = Tag(_spec.Boots, BootId), sole = Tag(Color.Lerp(_spec.Boots, Color.white, SoleLighten), BootId);
            Place b = At(ankle).Then(new Vector3(0f, -0.025f, 0.045f), new Vector3(90f, 0f, 0f), Vector3.one);
            Bind boots = Bind.Boot(ankle);
            Sculpt(_low ? BootLow : Boot, _low ? 6 : 8, null, b, boot, boots, NoMask, true, true);
            if (_low)
            {
                return;
            }

            Box(At(ankle).Then(new Vector3(0f, -0.06f, 0.055f), Vector3.zero, new Vector3(0.072f, 0.01f, 0.26f)), sole, boots);
            Box(At(ankle).Then(new Vector3(0f, 0.005f, 0.075f), new Vector3(-25f, 0f, 0f), new Vector3(0.042f, 0.045f, 0.012f)), boot, boots);
            for (int i = 0; i < Studs.Length; i++)
            {
                Box(At(ankle).Then(Studs[i], Vector3.zero, StudSize), sole, boots);
            }
        }

        private static readonly Vector3[] Studs =
        {
            new Vector3(-0.02f, -0.07f, 0.14f), new Vector3(0.02f, -0.07f, 0.14f), new Vector3(0f, -0.07f, -0.045f)
        };

        private static readonly Vector3 StudSize = new Vector3(0.014f, 0.012f, 0.014f);

        private void BuildArm(int side)
        {
            int shoulder = side < 0 ? (int)BodyBone.ShoulderL : (int)BodyBone.ShoulderR;
            int wrist = shoulder + 2;
            int[] chain = side < 0 ? ArmChainL : ArmChainR;
            Color skin = Tag(_spec.Skin, SkinId), shirt = Tag(_spec.Shirt, FabricId);
            Color arm = _spec.LongSleeves ? shirt : skin;
            Place at = At(shoulder);
            _group = side < 0 ? 3 : 4;
            Sculpt(_low ? ArmSkinLow : ArmSkin, ArmSides, null, at, arm, Bind.Joints(chain), _spec.LongSleeves ? FoldElbowFull : SweatLimb, false, false);
            _group = 0;
            if (_spec.LongSleeves)
            {
                Sculpt(_low ? LongSleeveLow : LongSleeve, LimbSides, null, at, shirt, Bind.Joints(chain), FoldElbowFull, false, true);
            }
            else
            {
                Sculpt(_low ? SleeveLow : Sleeve, LimbSides, null, at, shirt, Bind.Single(shoulder), FoldSleeve, false, true);
            }

            Color hand = _spec.Keeper ? Tag(_spec.Gloves, FabricId) : skin;
            float glove = _spec.Keeper ? 1.3f : 1f;
            Sculpt(_low ? HandLow : Hand, _low ? 5 : 6, null, At(wrist).Then(Vector3.zero, Vector3.zero, new Vector3(glove, glove, glove)), hand, Bind.Single(wrist), NoMask, true, true);
            if (!_low)
            {
                Ellipsoid(At(wrist).Then(new Vector3(0f, -0.035f, 0.024f), Vector3.zero, new Vector3(0.012f, 0.026f, 0.012f) * glove), hand, Bind.Single(wrist), 2, 5);
            }
        }

        private void BuildHead()
        {
            const int head = (int)BodyBone.Head;
            Bind bind = Bind.Single(head);
            Color skin = Tag(_spec.Skin, SkinId), hair = Tag(_spec.Hair, HairId);
            float[] rows = _low ? FaceRowsLow : FaceRows;
            var rings = new BodyRing[rows.Length + 2];
            rings[0] = BodyRing.Pole(Skull[0].Y);
            for (int i = 0; i < rows.Length; i++)
            {
                rings[i + 1] = Sample(Skull, rows[i]);
            }

            rings[rows.Length + 1] = BodyRing.Pole(0f);
            Sculpt(rings, HeadSides, _low ? null : FaceAngles(), HeadAt(Vector3.zero, Vector3.zero, Vector3.one), skin, bind, SweatForehead, true, true,
                DecalMode.None, _low ? null : (Func<Vector3, Vector3>)FaceWarp, _low ? null : (Func<Vector3, Color, Color>)FaceTint);
            if (!_low)
            {
                Sculpt(Nose, 6, null, HeadAt(new Vector3(0f, 0f, 0.1f), Vector3.zero, Vector3.one), skin, bind, NoMask, true, true);
                for (int side = -1; side <= 1; side += 2)
                {
                    Ellipsoid(HeadAt(new Vector3(side * 0.104f, 0.14f, -0.008f), new Vector3(0f, side * 15f, 0f), new Vector3(0.016f, 0.03f, 0.022f)), skin, bind, 2, 5);
                }
            }

            BuildHair(hair);
            BuildFacialHair(hair);
        }

        private static float[] _faceAngles;

        /// <summary>Columnas de la cabeza: densas al frente (cada 7°, donde van ojos y labios) y espaciadas atrás.</summary>
        private static float[] FaceAngles()
        {
            if (_faceAngles != null)
            {
                return _faceAngles;
            }

            const int frontColumns = 13, backSteps = 4;
            const float step = 7f * Mathf.Deg2Rad;
            var list = new List<float>();
            float start = Mathf.PI * 0.5f - 6f * step; // el frente es +Z (90°)
            for (int i = 0; i < frontColumns; i++)
            {
                list.Add(start + i * step);
            }

            float end = start + (frontColumns - 1) * step;
            float restStep = (start + Mathf.PI * 2f - end) / backSteps;
            for (int i = 1; i < backSteps; i++)
            {
                list.Add(end + i * restStep);
            }

            _faceAngles = list.ToArray();
            return _faceAngles;
        }

        // Relieve de la cara (espacio de la cabeza, sin escala): cuencas hundidas, arco superciliar, pómulos, labios y mentón.
        private static Vector3 FaceWarp(Vector3 v)
        {
            if (v.z <= 0.02f)
            {
                return v;
            }

            float depth = 0f;
            for (int side = -1; side <= 1; side += 2)
            {
                float ex = (v.x - side * 0.036f) / 0.022f, ey = (v.y - 0.158f) / 0.017f;
                depth -= 0.0075f * Mathf.Exp(-(ex * ex + ey * ey) * 1.2f);                       // cuenca ocular
                float cx = (v.x - side * 0.062f) / 0.026f, cy = (v.y - 0.128f) / 0.022f;
                depth += 0.004f * Mathf.Exp(-(cx * cx + cy * cy));                              // pómulo
            }

            float by = (v.y - 0.186f) / 0.011f, bx = v.x / 0.06f;
            depth += 0.0065f * Mathf.Exp(-by * by) * Mathf.Exp(-bx * bx);                      // arco superciliar
            float lx = v.x / 0.024f;
            float up = (v.y - 0.085f) / 0.0055f, line = (v.y - 0.075f) / 0.0045f, low = (v.y - 0.065f) / 0.006f;
            depth += (0.0035f * Mathf.Exp(-up * up) + 0.0045f * Mathf.Exp(-low * low) - 0.003f * Mathf.Exp(-line * line)) * Mathf.Exp(-lx * lx); // labios
            float chy = (v.y - 0.022f) / 0.02f, chx = v.x / 0.03f;
            depth += 0.005f * Mathf.Exp(-(chy * chy + chx * chx));                              // mentón
            var n = new Vector3(v.x, (v.y - 0.13f) * 0.6f, v.z);
            float len = Mathf.Max(1e-4f, n.magnitude);
            return v + n * (depth / len);
        }

        // Esclerótica, iris, cejas y labios en el color de los vértices de la cara.
        private static Color FaceTint(Vector3 l, Color baseColor)
        {
            if (l.z <= 0.03f)
            {
                return baseColor;
            }

            Color c = baseColor;
            for (int side = -1; side <= 1; side += 2)
            {
                float ex = (l.x - side * 0.036f) / 0.02f, ey = (l.y - 0.158f) / 0.0135f;
                float e = ex * ex + ey * ey;
                if (e < 1f)
                {
                    c = Color.Lerp(c, new Color(Sclera.r, Sclera.g, Sclera.b, c.a), (1f - Smooth01(0.45f, 1f, e)) * 0.95f);
                }

                float ix = (l.x - side * 0.036f) / 0.0085f, iy = (l.y - 0.158f) / 0.0085f;
                float i2 = ix * ix + iy * iy;
                if (i2 < 1f)
                {
                    c = Color.Lerp(c, new Color(Iris.r, Iris.g, Iris.b, c.a), 1f - Smooth01(0.35f, 1f, i2));
                }
            }

            float lx = l.x / 0.026f;
            float u1 = (l.y - 0.085f) / 0.0075f, u2 = (l.y - 0.065f) / 0.0085f, u3 = (l.y - 0.075f) / 0.0045f;
            float lip = Mathf.Max(1f - Smooth01(0.3f, 1f, lx * lx + u1 * u1), 1f - Smooth01(0.3f, 1f, lx * lx + u2 * u2));
            c = Color.Lerp(c, new Color(LipColor.r, LipColor.g, LipColor.b, c.a), lip * 0.8f);
            float line = 1f - Smooth01(0.2f, 1f, lx * lx + u3 * u3);
            return Color.Lerp(c, new Color(MouthLine.r, MouthLine.g, MouthLine.b, c.a), line * 0.6f);
        }

        // ------------------------------------------------------------------ pelo y barba (mechones)

        private void BuildHair(Color hair)
        {
            const int head = (int)BodyBone.Head;
            Bind bind = Bind.Single(head);
            BodyRing[] cap = _low ? HairCapLow : HairCap;
            Vector3 capScale;
            switch (_spec.HairStyle)
            {
                case 1: capScale = new Vector3(0.985f, 0.97f, 0.985f); break;
                case 2: capScale = new Vector3(1.04f, 1.02f, 1.04f); break;
                case 3: capScale = new Vector3(1.03f, 1.02f, 1.03f); break;
                case 4: capScale = new Vector3(0.975f, 0.96f, 0.975f); break;
                case 5: capScale = new Vector3(0.98f, 0.97f, 0.98f); break;
                default: capScale = Vector3.one; break;
            }

            Sculpt(cap, HeadSides, null, HeadAt(Vector3.zero, Vector3.zero, capScale), hair, bind, NoMask, true, false);
            int low = _low ? 1 : 0;
            switch (_spec.HairStyle)
            {
                case 1: // degradé: copete corto peinado hacia delante
                    for (int i = -2; i <= 2; i += 1 + low)
                    {
                        Lock(new Vector3(i * 0.032f, 0.262f, -0.02f), new Vector3(i * 0.022f, 0.287f, 0.085f), 0.045f, 0.03f, 0.018f, hair);
                    }

                    break;
                case 2: // melena con vincha: mechones largos sobre la nuca y los costados
                    Sculpt(Band(0.118f, 0.124f), HeadSides, null, HeadAt(new Vector3(0f, 0.215f, 0.004f), Vector3.zero, Vector3.one), Tag(Color.white, FabricId), bind, NoMask, false, false);
                    for (int i = -3; i <= 3; i += 1 + low)
                    {
                        float x = i * 0.028f;
                        Lock(new Vector3(x, 0.17f, -0.1f), new Vector3(x * 1.1f, -0.035f - Mathf.Abs(i) * 0.012f, -0.118f), 0.042f, 0.03f, 0.02f, hair);
                    }

                    if (!_low)
                    {
                        Lock(new Vector3(-0.1f, 0.16f, -0.02f), new Vector3(-0.108f, 0.04f, -0.05f), 0.04f, 0.026f, 0.018f, hair);
                        Lock(new Vector3(0.1f, 0.16f, -0.02f), new Vector3(0.108f, 0.04f, -0.05f), 0.04f, 0.026f, 0.018f, hair);
                    }

                    break;
                case 3: // rulos: mechones cortos que salen en todas direcciones desde la coronilla
                    for (int i = 0; i < (_low ? 4 : 7); i++)
                    {
                        float a = i * Mathf.PI * 2f / (_low ? 4 : 7);
                        var root = new Vector3(Mathf.Cos(a) * 0.06f, 0.262f, Mathf.Sin(a) * 0.065f);
                        Lock(root, root + new Vector3(Mathf.Cos(a) * 0.04f, 0.035f, Mathf.Sin(a) * 0.04f), 0.05f, 0.036f, 0.03f, hair);
                    }

                    break;
                case 4: // rapado: solo el casquete
                    break;
                case 5: // jopo: flequillo alto hacia arriba y adelante
                    for (int i = -2; i <= 2; i += 1 + low)
                    {
                        Lock(new Vector3(i * 0.028f, 0.255f, 0.01f), new Vector3(i * 0.018f, 0.305f, 0.095f), 0.05f, 0.032f, 0.02f, hair);
                    }

                    break;
                default: // corto: flequillo sobre la frente y mechones en la nuca
                    for (int i = -2; i <= 2; i += 1 + low)
                    {
                        Lock(new Vector3(i * 0.03f, 0.225f, 0.075f), new Vector3(i * 0.033f, 0.19f, 0.123f), 0.032f, 0.016f, 0.012f, hair);
                    }

                    if (!_low)
                    {
                        for (int i = -1; i <= 1; i++)
                        {
                            Lock(new Vector3(i * 0.045f, 0.13f, -0.1f), new Vector3(i * 0.05f, 0.075f, -0.117f), 0.04f, 0.026f, 0.016f, hair);
                        }

                        Lock(new Vector3(0f, 0.255f, 0.02f), new Vector3(0f, 0.292f, 0.03f), 0.06f, 0.036f, 0.018f, hair);
                    }

                    break;
            }
        }

        private void BuildFacialHair(Color hair)
        {
            if (_spec.FacialHair == 0 || _low)
            {
                return;
            }

            if (_spec.FacialHair == 1) // barba: mechones a lo largo de la mandíbula y el mentón
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Lock(new Vector3(side * 0.088f, 0.1f, 0f), new Vector3(side * 0.078f, 0.025f, 0.045f), 0.036f, 0.026f, 0.014f, hair);
                    Lock(new Vector3(side * 0.066f, 0.07f, 0.07f), new Vector3(side * 0.056f, 0.008f, 0.1f), 0.034f, 0.024f, 0.014f, hair);
                    Lock(new Vector3(side * 0.03f, 0.05f, 0.09f), new Vector3(side * 0.026f, -0.012f, 0.108f), 0.032f, 0.022f, 0.014f, hair);
                }

                Lock(new Vector3(0f, 0.04f, 0.095f), new Vector3(0f, -0.02f, 0.108f), 0.04f, 0.026f, 0.014f, hair);
            }
            else // candado: mentón
            {
                Lock(new Vector3(0f, 0.045f, 0.094f), new Vector3(0f, -0.005f, 0.108f), 0.03f, 0.02f, 0.012f, hair);
            }

            // Bigote: un mechón horizontal bajo la nariz.
            LockWide(new Vector3(-0.026f, 0.089f, 0.108f), new Vector3(0.026f, 0.089f, 0.108f), 0.012f, 0.012f, 0.009f, hair);
        }

        private static BodyRing[] Band(float rx, float depth) => new[]
        {
            BodyRing.Pole(0.016f), new BodyRing(0.015f, rx, depth, depth), new BodyRing(-0.015f, rx, depth, depth), BodyRing.Pole(-0.016f)
        };

        private Color SockStripe()
        {
            Color c = _spec.Secondary;
            float diff = Mathf.Abs(c.r - _spec.Socks.r) + Mathf.Abs(c.g - _spec.Socks.g) + Mathf.Abs(c.b - _spec.Socks.b);
            return diff > 0.3f ? c : (_spec.Socks.grayscale > 0.5f ? new Color(0.1f, 0.1f, 0.12f) : Color.white);
        }

        /// <summary>Mechón: placa volumétrica que se afina de la raíz a la punta (espacio de la cabeza).</summary>
        private void Lock(Vector3 root, Vector3 tip, float rootWidth, float tipWidth, float thickness, Color color)
        {
            Vector3 axis = tip - root;
            Vector3 reference = Mathf.Abs(axis.y) > Mathf.Abs(axis.z) * 1.5f && Mathf.Abs(axis.y) > Mathf.Abs(axis.x) ? Forward : Up;
            Vector3 side = Vector3.Cross(axis, reference);
            side = side / Mathf.Max(1e-5f, side.magnitude);
            TaperedBox(root, tip, side, rootWidth, tipWidth, thickness, color);
        }

        private void LockWide(Vector3 root, Vector3 tip, float rootWidth, float tipWidth, float thickness, Color color)
        {
            TaperedBox(root, tip, Up, rootWidth, tipWidth, thickness, color);
        }

        private void TaperedBox(Vector3 a, Vector3 b, Vector3 side, float w0, float w1, float thick, Color color)
        {
            Vector3 axis = b - a;
            Vector3 normal = Vector3.Cross(axis, side);
            normal = normal / Mathf.Max(1e-5f, normal.magnitude);
            BeginShell();
            for (int e = 0; e < 2; e++)
            {
                Vector3 c = e == 0 ? a : b;
                float w = (e == 0 ? w0 : w1) * 0.5f, t = thick * (e == 0 ? 0.5f : 0.3f);
                _local.Add(c - side * w - normal * t);
                _local.Add(c + side * w - normal * t);
                _local.Add(c + side * w + normal * t);
                _local.Add(c - side * w + normal * t);
            }

            for (int k = 0; k < 4; k++)
            {
                int n = (k + 1) % 4;
                Quad(k, n, 4 + n, 4 + k);
            }

            Quad(4, 5, 6, 7); // la punta; la raíz queda dentro del casquete
            EndShell(HeadAt(Vector3.zero, Vector3.zero, Vector3.one), color, Bind.Single((int)BodyBone.Head), NoMask, Orientation.Centroid);
        }

        // ------------------------------------------------------------------ diseño de la camiseta

        private void BuildPattern(int pattern, Color color)
        {
            const float top = 1.47f, bottom = 1.005f, chest = 1.3f;
            switch (pattern)
            {
                case 1: // rayas
                    for (int i = -1; i <= 1; i++)
                    {
                        float c = i * 0.115f;
                        Strip(top, bottom, y => c, 0.03f, color);
                    }

                    break;
                case 2: // franja horizontal
                    BandAround(chest + 0.07f, chest - 0.05f, color);
                    break;
                case 3: // banda cruzada
                    Strip(top - 0.02f, bottom, y => (y - 1.24f) * 0.75f, 0.045f, color);
                    break;
                case 4: // V
                    for (int side = -1; side <= 1; side += 2)
                    {
                        int sd = side;
                        Strip(top - 0.03f, chest - 0.02f, y => sd * (y - (chest - 0.02f)) * 1.1f, 0.03f, color);
                    }

                    break;
                case 5: // mitades
                    Strip(top, bottom, y => 0.15f, 0.15f, color);
                    break;
            }
        }

        private const int PatchRows = 6, PatchColumns = 2;

        private void Strip(float yTop, float yBottom, Func<float, float> centre, float halfWidth, Color color)
        {
            for (int side = 0; side < 2; side++)
            {
                bool front = side == 0;
                BeginShell();
                for (int row = 0; row <= PatchRows; row++)
                {
                    float y = Mathf.Lerp(yTop, yBottom, row / (float)PatchRows);
                    BodyRing r = Sample(Torso, y);
                    for (int col = 0; col <= PatchColumns; col++)
                    {
                        float x = centre(y) + Mathf.Lerp(-halfWidth, halfWidth, col / (float)PatchColumns);
                        float a = Mathf.Acos(Mathf.Clamp(x / Mathf.Max(r.Rx, 0.001f), -1f, 1f));
                        _local.Add(r.Point(front ? a : -a, PatternInflate, PatternOffset));
                    }
                }

                Grid(PatchRows, PatchColumns);
                EndShell(Place.Identity, color, Bind.Auto, FoldWaist, Orientation.Axis, DecalMode.Torso);
            }
        }

        private void BandAround(float yTop, float yBottom, Color color)
        {
            BeginShell();
            int columns = TorsoSides;
            int rows = PatchRows / 2;
            for (int row = 0; row <= rows; row++)
            {
                BodyRing r = Sample(Torso, Mathf.Lerp(yTop, yBottom, row / (float)rows));
                for (int col = 0; col <= columns; col++)
                {
                    _local.Add(r.Point(col * Mathf.PI * 2f / columns, PatternInflate, PatternOffset));
                }
            }

            Grid(rows, columns);
            EndShell(Place.Identity, color, Bind.Auto, FoldWaist, Orientation.Axis, DecalMode.Torso);
        }

        private void Grid(int rows, int columns)
        {
            int stride = columns + 1;
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++)
                {
                    int a = row * stride + col;
                    Quad(a, a + 1, a + stride + 1, a + stride);
                }
            }
        }

        /// <summary>Sección interpolada a una altura dada (entre los anillos que no son polos).</summary>
        public static BodyRing Sample(BodyRing[] rings, float y)
        {
            for (int i = 1; i < rings.Length - 2; i++)
            {
                BodyRing a = rings[i], c = rings[i + 1];
                if (y <= a.Y && y >= c.Y)
                {
                    float t = (a.Y - y) / Mathf.Max(a.Y - c.Y, 1e-5f);
                    return new BodyRing(y, Mathf.Lerp(a.Rx, c.Rx, t), Mathf.Lerp(a.Front, c.Front, t), Mathf.Lerp(a.Back, c.Back, t));
                }
            }

            BodyRing edge = y > rings[1].Y ? rings[1] : rings[rings.Length - 2];
            return new BodyRing(y, edge.Rx, edge.Front, edge.Back);
        }

        // ------------------------------------------------------------------ máscaras del shader (x sudor, y pliegues)

        private delegate Vector2 MaskFn(Vector3 local, Vector3 model);

        private static readonly MaskFn NoMask = (l, m) => Vector2.zero;
        private static readonly MaskFn SweatLimb = (l, m) => new Vector2(0.35f, 0f);
        private static readonly MaskFn SweatNeck = (l, m) => new Vector2(0.8f, 0f);
        private static readonly MaskFn SweatForehead = (l, m) => new Vector2(Smooth01(0.12f, 0.22f, l.y) * Smooth01(0f, 0.08f, l.z) * 0.9f + 0.1f, 0f);
        private static readonly MaskFn FoldWaist = (l, m) => new Vector2(0f, 0.25f + 0.75f * Smooth01(1.15f, 0.99f, m.y) + 0.5f * Smooth01(1.34f, 1.42f, m.y) * Smooth01(0.14f, 0.2f, Mathf.Abs(m.x)));
        private static readonly MaskFn FoldGroin = (l, m) => new Vector2(0f, 0.6f + 0.4f * Smooth01(0.9f, 0.78f, m.y));
        private static readonly MaskFn FoldElbowFull = (l, m) => new Vector2(0f, 0.3f + 0.7f * (1f - Mathf.Clamp01(Mathf.Abs(m.y - 1.11f) / 0.1f)));
        private static readonly MaskFn FoldSleeve = (l, m) => new Vector2(0f, 0.4f + 0.6f * Smooth01(1.33f, 1.27f, m.y));
        private static readonly MaskFn FoldKnee = (l, m) => new Vector2(0f, 0.3f + 0.7f * (1f - Mathf.Clamp01(Mathf.Abs(m.y - 0.48f) / 0.1f)));
        private static readonly MaskFn FoldAnkle = (l, m) => new Vector2(0f, 0.3f + 0.7f * Smooth01(0.18f, 0.08f, m.y));

        /// <summary>Transición suave entre dos bordes (0 en <paramref name="e0"/>, 1 en <paramref name="e1"/>; acepta bordes invertidos).</summary>
        public static float Smooth01(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        // ------------------------------------------------------------------ pesos

        /// <summary>Cómo se pesa cada vértice de una pieza: un hueso con mezcla hacia su padre, una cadena de huesos o el botín.</summary>
        private struct Bind
        {
            public int Bone;
            public int[] Chain;
            public bool IsBoot;

            public static Bind Auto => new Bind { Bone = TrunkBone };

            public static Bind Single(int bone) => new Bind { Bone = bone };

            public static Bind Joints(int[] chain) => new Bind { Bone = chain[0], Chain = chain };

            public static Bind Boot(int ankle) => new Bind { Bone = ankle, IsBoot = true };
        }

        private void Weigh(Bind bind, Vector3 p, out int b0, out int b1, out float w0)
        {
            if (bind.Chain != null)
            {
                WeighChain(bind.Chain, p, out b0, out b1, out w0);
                return;
            }

            if (bind.IsBoot)
            {
                // El botín sigue al tobillo y su puntera, a medida que avanza, al hueso del pie (se dobla al despegar).
                int toe = bind.Bone + 1;
                float wt = Smooth01(ToeBlendFrom, ToeBlendTo, p.z);
                b0 = wt > 0f ? toe : bind.Bone;
                b1 = bind.Bone;
                w0 = wt > 0f ? wt : 1f;
                if (wt >= 1f)
                {
                    b1 = toe;
                }

                return;
            }

            WeighSingle(bind.Bone, p, out b0, out b1, out w0);
        }

        /// <summary>Cadena cadera–rodilla–tobillo u hombro–codo–muñeca: cada articulación mezcla con suavidad los dos huesos que une.</summary>
        private static void WeighChain(int[] chain, Vector3 p, out int b0, out int b1, out float w0)
        {
            b0 = chain[0];
            b1 = chain[0];
            w0 = 1f;
            for (int i = 1; i < chain.Length; i++)
            {
                float r = BlendRadius[chain[i]];
                float below = BindPositions[chain[i]].y - p.y; // cuánto por debajo de la articulación está el vértice
                float w = Smooth01(-r, r, below);
                if (w >= 1f)
                {
                    b0 = chain[i];
                    b1 = chain[i];
                    continue;
                }

                if (w > 0f)
                {
                    b0 = chain[i];
                    b1 = chain[i - 1];
                    w0 = w;
                }

                return;
            }
        }

        /// <summary>Huesos y peso de un vértice: mezcla suave con el padre o el hijo cerca de cada articulación.</summary>
        private static void WeighSingle(int bone, Vector3 p, out int b0, out int b1, out float w0)
        {
            if (bone < 0)
            {
                bone = TrunkBoneAt(p.y);
            }

            b0 = bone;
            b1 = bone;
            w0 = 1f;
            float r = BlendRadius[bone];
            float t = Vector3.Dot(p - BindPositions[bone], Axis[bone]);
            int child = BlendChild[bone];
            if (r > 0f && t < r)
            {
                w0 = Smooth01(-r, r, t);
                b1 = Parents[bone];
            }
            else if (child >= 0 && BlendRadius[child] > 0f)
            {
                float rc = BlendRadius[child];
                float tc = Vector3.Dot(p - BindPositions[child], Axis[child]);
                if (tc > -rc)
                {
                    b0 = child;
                    b1 = bone;
                    w0 = Smooth01(-rc, rc, tc);
                }
            }

            // Hombros: el borde del pecho acompaña al brazo (deltoides); ingle: la pelvis acompaña al muslo.
            float ax = Mathf.Abs(p.x);
            if (bone == (int)BodyBone.Chest && p.y > ShoulderBlendHeight && ax > ShoulderBlendFrom)
            {
                b0 = bone;
                b1 = p.x < 0f ? (int)BodyBone.ShoulderL : (int)BodyBone.ShoulderR;
                w0 = 1f - Smooth01(ShoulderBlendFrom, ShoulderBlendTo, ax) * ShoulderBlendMax;
            }
            else if (bone == (int)BodyBone.Pelvis && p.y < GroinTop && ax > GroinInner)
            {
                b0 = bone;
                b1 = p.x < 0f ? (int)BodyBone.HipL : (int)BodyBone.HipR;
                w0 = 1f - Smooth01(GroinTop, GroinBottom, p.y) * Smooth01(GroinInner, GroinOuter, ax) * GroinMax;
            }

            if (b1 == b0)
            {
                w0 = 1f;
            }
        }

        /// <summary>Hueso del tronco según la altura: el límite es la mitad entre articulaciones (así los pesos son continuos).</summary>
        private static int TrunkBoneAt(float y)
        {
            for (int b = (int)BodyBone.Pelvis; b < (int)BodyBone.Head; b++)
            {
                if (y < (BindPositions[b].y + BindPositions[b + 1].y) * 0.5f)
                {
                    return b;
                }
            }

            return (int)BodyBone.Head;
        }

        // ------------------------------------------------------------------ oclusión ambiental

        /// <summary>Factor de luz ambiente (1 = abierto, hasta 1 − <see cref="MaxOcclusion"/> en rincones) de un punto del cuerpo.</summary>
        public static float Occlusion(Vector3 p)
        {
            float ao = 1f;
            for (int i = 0; i < Occluders.Length; i++)
            {
                Occluder o = Occluders[i];
                for (int s = 0; s < (o.Mirror ? 2 : 1); s++)
                {
                    float cx = s == 0 ? o.Centre.x : -o.Centre.x;
                    float dx = (p.x - cx) / o.Radii.x, dy = (p.y - o.Centre.y) / o.Radii.y, dz = (p.z - o.Centre.z) / o.Radii.z;
                    float d = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
                    if (d < 1f)
                    {
                        ao *= 1f - o.Strength * (1f - Smooth01(0f, 1f, d));
                    }
                }
            }

            return Mathf.Max(1f - MaxOcclusion, ao);
        }

        // ------------------------------------------------------------------ armado de cáscaras

        private enum Orientation { Axis, Centroid }

        private enum DecalMode { None, Torso }

        /// <summary>Lugar de una pieza: traslación del hueso más posición, giro (Euler en grados, orden de Unity) y escala locales.</summary>
        private struct Place
        {
            public Vector3 Origin, Position, Euler, Scale;

            public static Place Identity => new Place { Scale = Vector3.one };

            public Place Then(Vector3 position, Vector3 euler, Vector3 scale) =>
                new Place { Origin = Origin, Position = Position + position, Euler = euler, Scale = Vector3.Scale(Scale, scale) };

            public Vector3 Apply(Vector3 v) => Origin + Position + Rotate(Euler, Vector3.Scale(v, Scale));
        }

        private static Place At(int bone) => new Place { Origin = BindPositions[bone], Scale = Vector3.one };

        private static float HeadFactor => _humanoid != null ? HumanoidBody.HeadSizeFactor : 1f;

        private static Place HeadAt(Vector3 position, Vector3 euler, Vector3 scale) => new Place
        {
            Origin = _humanoid != null ? HumanoidBody.HeadOrigin : BindPositions[(int)BodyBone.Head],
            Position = position * HeadScale * HeadFactor, Euler = euler, Scale = scale * HeadScale * HeadFactor
        };

        /// <summary>Giro con el orden de Unity (Z, luego X, luego Y).</summary>
        private static Vector3 Rotate(Vector3 euler, Vector3 v)
        {
            if (euler.x == 0f && euler.y == 0f && euler.z == 0f)
            {
                return v;
            }

            float z = euler.z * Mathf.Deg2Rad, x = euler.x * Mathf.Deg2Rad, y = euler.y * Mathf.Deg2Rad;
            float cz = Mathf.Cos(z), sz = Mathf.Sin(z);
            v = new Vector3(v.x * cz - v.y * sz, v.x * sz + v.y * cz, v.z);
            float cx = Mathf.Cos(x), sx = Mathf.Sin(x);
            v = new Vector3(v.x, v.y * cx - v.z * sx, v.y * sx + v.z * cx);
            float cy = Mathf.Cos(y), sy = Mathf.Sin(y);
            return new Vector3(v.x * cy + v.z * sy, v.y, -v.x * sy + v.z * cy);
        }

        private void BeginShell()
        {
            _local.Clear();
            _localTris.Clear();
        }

        private void Tri(int a, int b, int c)
        {
            _localTris.Add(a);
            _localTris.Add(b);
            _localTris.Add(c);
        }

        private void Quad(int a, int b, int c, int d)
        {
            Tri(a, b, c);
            Tri(a, c, d);
        }

        /// <summary>Malla esculpida por secciones (con columnas uniformes o en los ángulos dados); las tapas que quedan dentro de otra pieza se omiten.</summary>
        private void Sculpt(BodyRing[] rings, int sides, float[] angles, Place place, Color color, Bind bind, MaskFn mask, bool capTop, bool capBottom,
            DecalMode decal = DecalMode.None, Func<Vector3, Vector3> warp = null, Func<Vector3, Color, Color> tint = null)
        {
            BeginShell();
            int columns = angles != null ? angles.Length : sides;
            int ringCount = rings.Length - 2;
            for (int r = 0; r < ringCount; r++)
            {
                for (int i = 0; i < columns; i++)
                {
                    float a = angles != null ? angles[i] : i * Mathf.PI * 2f / sides;
                    Vector3 v = rings[r + 1].Point(a);
                    _local.Add(warp != null ? warp(v) : v);
                }
            }

            int top = -1, bottom = -1;
            if (capTop)
            {
                top = _local.Count;
                _local.Add(new Vector3(0f, rings[0].Y, 0f));
            }

            if (capBottom)
            {
                bottom = _local.Count;
                _local.Add(new Vector3(0f, rings[rings.Length - 1].Y, 0f));
            }

            for (int i = 0; i < columns; i++)
            {
                int j = (i + 1) % columns;
                if (capTop)
                {
                    Tri(top, j, i);
                }

                if (capBottom)
                {
                    Tri(bottom, (ringCount - 1) * columns + i, (ringCount - 1) * columns + j);
                }

                for (int r = 0; r < ringCount - 1; r++)
                {
                    int a = r * columns;
                    Quad(a + i, a + j, a + columns + j, a + columns + i);
                }
            }

            EndShell(place, color, bind, mask, Orientation.Axis, decal, tint);
        }

        /// <summary>Elipsoide de pocas caras (orejas, pulgar).</summary>
        private void Ellipsoid(Place place, Color color, Bind bind, int rings, int sides)
        {
            var profile = new BodyRing[rings + 2];
            profile[0] = BodyRing.Pole(1f);
            profile[rings + 1] = BodyRing.Pole(-1f);
            for (int i = 1; i <= rings; i++)
            {
                float y = 1f - 2f * i / (rings + 1);
                float r = Mathf.Sqrt(1f - y * y);
                profile[i] = new BodyRing(y, r, r, r);
            }

            BeginShell();
            for (int r = 1; r <= rings; r++)
            {
                for (int i = 0; i < sides; i++)
                {
                    _local.Add(profile[r].Point(i * Mathf.PI * 2f / sides));
                }
            }

            int top = _local.Count;
            _local.Add(new Vector3(0f, 1f, 0f));
            int bottom = _local.Count;
            _local.Add(new Vector3(0f, -1f, 0f));
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                Tri(top, j, i);
                Tri(bottom, (rings - 1) * sides + i, (rings - 1) * sides + j);
                for (int r = 0; r < rings - 1; r++)
                {
                    int a = r * sides;
                    Quad(a + i, a + j, a + sides + j, a + sides + i);
                }
            }

            EndShell(place, color, bind, NoMask, Orientation.Centroid);
        }

        /// <summary>Caja de caras planas (24 vértices): suela, lengüeta y tapones.</summary>
        private void Box(Place place, Color color, Bind bind)
        {
            BeginShell();
            for (int axis = 0; axis < 3; axis++)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    int start = _local.Count;
                    for (int k = 0; k < 4; k++)
                    {
                        float u = (k == 1 || k == 2) ? 0.5f : -0.5f, w = k >= 2 ? 0.5f : -0.5f;
                        float n = sign * 0.5f;
                        _local.Add(axis == 0 ? new Vector3(n, u, w) : axis == 1 ? new Vector3(u, n, w) : new Vector3(u, w, n));
                    }

                    Quad(start, start + 1, start + 2, start + 3);
                }
            }

            EndShell(place, color, bind, NoMask, Orientation.Centroid);
        }

        /// <summary>Orienta las caras hacia fuera (en el espacio de la pieza), las lleva al espacio del jugador, les pone pesos, oclusión y decal.</summary>
        private void EndShell(Place place, Color color, Bind bind, MaskFn mask, Orientation orientation, DecalMode decal = DecalMode.None,
            Func<Vector3, Color, Color> tint = null)
        {
            Vector3 centroid = Vector3.zero;
            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < _local.Count; i++)
            {
                centroid += _local[i];
                minY = Mathf.Min(minY, _local[i].y);
                maxY = Mathf.Max(maxY, _local[i].y);
            }

            centroid /= Mathf.Max(1, _local.Count);
            float centreY = (minY + maxY) * 0.5f;
            for (int i = 0; i < _localTris.Count; i += 3)
            {
                Vector3 a = _local[_localTris[i]], b = _local[_localTris[i + 1]], c = _local[_localTris[i + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                Vector3 middle = (a + b + c) / 3f;
                Vector3 outward = orientation == Orientation.Centroid ? middle - centroid : new Vector3(middle.x, 0f, middle.z);
                if (outward.sqrMagnitude < 1e-8f)
                {
                    outward = new Vector3(0f, Mathf.Sign(middle.y - centreY), 0f);
                }

                if (Vector3.Dot(normal, outward) < 0f)
                {
                    int swap = _localTris[i + 1];
                    _localTris[i + 1] = _localTris[i + 2];
                    _localTris[i + 2] = swap;
                }
            }

            int offset = Vertices.Count;
            for (int i = 0; i < _local.Count; i++)
            {
                Vector3 model = place.Apply(_local[i]);
                Color tinted = tint != null ? tint(_local[i], color) : color;
                float ao = Occlusion(model);
                Vertices.Add(model);
                BaseColors.Add(tinted);
                Colors.Add(new Color(tinted.r * ao, tinted.g * ao, tinted.b * ao, tinted.a));
                Masks.Add(mask(_local[i], model));
                Weigh(bind, model, out int b0, out int b1, out float w0);
                Bone0.Add(b0);
                Bone1.Add(b1);
                Weight0.Add(w0);
                Group.Add(_group);
                AddDecal(decal, model);
            }

            for (int i = 0; i < _localTris.Count; i++)
            {
                Triangles.Add(offset + _localTris[i]);
            }
        }

        /// <summary>Proyección plana de los decales sobre la camiseta: dorsal en la espalda y escudo en el pecho izquierdo.</summary>
        private void AddDecal(DecalMode decal, Vector3 p)
        {
            if (decal == DecalMode.None)
            {
                DecalUv.Add(Vector3.zero);
                DecalData.Add(new Vector3(-1f, -1f, -1f));
                DecalInk.Add(Vector3.zero);
                return;
            }

            // Unity usa ejes de mano izquierda: quien mira la espalda (desde −Z, hacia +Z) tiene su derecha en +X, y quien mira el frente (desde +Z,
            // hacia −Z) la tiene en −X. El dorsal y el escudo crecen hacia la derecha de quien los mira; con los signos al revés salían en espejo.
            float flag = Mathf.Clamp(p.z / DecalFlagRamp, -1f, 1f);
            float shift = _humanoid != null ? HumanoidBody.DecalShiftY : 0f;
            Vector3 uv;
            if (flag < 0f)
            {
                uv = new Vector3(0.5f + p.x / BackPatchW, (p.y - (BackPatchY + shift - BackPatchH * 0.5f)) / BackPatchH, flag);
            }
            else
            {
                uv = new Vector3(0.5f - (p.x - CrestPatchX) / CrestPatchW, (p.y - (CrestPatchY + shift - CrestPatchH * 0.5f)) / CrestPatchH, flag);
            }

            DecalUv.Add(uv);
            DecalData.Add(new Vector3(_tens, _units, _spec.Crest));
            DecalInk.Add(_ink);
        }

        private static Color Tag(Color color, float material)
        {
            color.a = material;
            return color;
        }
    }
}
