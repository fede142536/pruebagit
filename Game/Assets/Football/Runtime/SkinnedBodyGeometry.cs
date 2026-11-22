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

    /// <summary>Lo que define el aspecto de un jugador para armar su malla (colores, peinado, vello facial y ropa).</summary>
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
    }

    /// <summary>
    /// Malla única del jugador para un <c>SkinnedMeshRenderer</c> de 20 huesos, armada sin Unity (solo listas): tronco con
    /// ruedo de la camiseta sobre el pantalón, pantalón con botamanga abierta, mangas con borde, medias con puño enrollado y
    /// franja, botines con suela, lengüeta y tapones, cara con arco superciliar, nariz, pómulos, mentón y orejas, pelo
    /// volumétrico y barba o candado. Cada vértice lleva hasta dos huesos con pesos suaves en codos, rodillas, hombros,
    /// cuello e ingle (las piezas se solapan en las articulaciones y los pesos repartidos las mantienen cerradas al doblar).
    /// En el color del vértice va el material (alfa: 1 piel, 0,75 tela, 0,5 botín, 0,25 pelo) y en la UV dos máscaras para
    /// el shader: x = sudor (piel), y = pliegues (tela). Pose de reposo con rotaciones identidad: cada pose de enlace es una
    /// traslación.
    /// </summary>
    public sealed class SkinnedBodyGeometry
    {
        /// <summary>Cantidad de huesos.</summary>
        public const int BoneCount = 20;
        /// <summary>Escala de la cabeza (figura de unas 7,5 cabezas).</summary>
        public const float HeadScale = 0.8f;
        /// <summary>Material piel (alfa del color del vértice).</summary>
        public const float SkinId = 1f;
        /// <summary>Material tela.</summary>
        public const float FabricId = 0.75f;
        /// <summary>Material botín.</summary>
        public const float BootId = 0.5f;
        /// <summary>Material pelo.</summary>
        public const float HairId = 0.25f;

        /// <summary>Padre de cada hueso (−1 para la raíz).</summary>
        public static readonly int[] Parents = { -1, 0, 1, 2, 3, 4, 3, 6, 7, 3, 9, 10, 1, 12, 13, 14, 1, 16, 17, 18 };

        /// <summary>Posición de cada hueso en reposo, en el espacio del jugador (pies en el origen, mirando a +Z).</summary>
        public static readonly Vector3[] BindPositions =
        {
            new Vector3(0f, 0f, 0f), new Vector3(0f, 0.95f, 0f), new Vector3(0f, 1.1f, 0f), new Vector3(0f, 1.28f, 0f),
            new Vector3(0f, 1.45f, 0f), new Vector3(0f, 1.53f, 0f),
            new Vector3(-0.26f, 1.4f, 0f), new Vector3(-0.26f, 1.11f, 0f), new Vector3(-0.26f, 0.875f, 0f),
            new Vector3(0.26f, 1.4f, 0f), new Vector3(0.26f, 1.11f, 0f), new Vector3(0.26f, 0.875f, 0f),
            new Vector3(-0.11f, 0.92f, 0f), new Vector3(-0.11f, 0.48f, 0f), new Vector3(-0.11f, 0.06f, 0f), new Vector3(-0.11f, 0.025f, 0.11f),
            new Vector3(0.11f, 0.92f, 0f), new Vector3(0.11f, 0.48f, 0f), new Vector3(0.11f, 0.06f, 0f), new Vector3(0.11f, 0.025f, 0.11f)
        };

        /// <summary>Nombres de los huesos (los del pedido: Pelvis, Spine, Chest… Toe_R).</summary>
        public static readonly string[] BoneNames =
        {
            "Root", "Pelvis", "Spine", "Chest", "Neck", "Head", "Shoulder_L", "Elbow_L", "Wrist_L", "Shoulder_R", "Elbow_R", "Wrist_R",
            "Hip_L", "Knee_L", "Ankle_L", "Toe_L", "Hip_R", "Knee_R", "Ankle_R", "Toe_R"
        };

        // Radio de mezcla de cada hueso con su padre (m) y dirección del segmento (hacia el hijo).
        private static readonly float[] BlendRadius = { 0f, 0f, 0.07f, 0.08f, 0.035f, 0.02f, 0.09f, 0.05f, 0.03f, 0.09f, 0.05f, 0.03f, 0.07f, 0.06f, 0.03f, 0.04f, 0.07f, 0.06f, 0.03f, 0.04f };
        private static readonly int[] BlendChild = { -1, 2, 3, 4, 5, -1, 7, 8, -1, 10, 11, -1, 13, 14, 15, -1, 17, 18, 19, -1 };
        private static readonly Vector3 Up = new Vector3(0f, 1f, 0f), Down = new Vector3(0f, -1f, 0f), Forward = new Vector3(0f, 0f, 1f);
        private static readonly Vector3[] Axis = { Up, Up, Up, Up, Up, Up, Down, Down, Down, Down, Down, Down, Down, Down, Down, Forward, Down, Down, Down, Forward };
        private const int TrunkBone = -1; // la pieza del tronco toma el hueso según la altura
        private const float ShoulderBlendFrom = 0.13f, ShoulderBlendTo = 0.23f, ShoulderBlendHeight = 1.3f, ShoulderBlendMax = 0.8f;
        private const float GroinTop = 0.88f, GroinBottom = 0.8f, GroinInner = 0.03f, GroinOuter = 0.09f, GroinMax = 0.6f;

        private const int TorsoSides = 14, LimbSides = 9, ArmSides = 8, HeadSides = 12;
        private const int PatchRows = 6, PatchColumns = 2;
        private const float PatternInflate = 1.035f, PatternOffset = 0.003f;
        private static readonly Color EyeColor = new Color(0.07f, 0.05f, 0.04f);
        private static readonly Color MouthColor = new Color(0.45f, 0.22f, 0.2f);
        private const float SoleLighten = 0.65f;

        // ---- perfiles (en metros; tronco desde el suelo, el resto en el espacio de su articulación) ----

        private static readonly Ring[] Torso =
        {
            Ring.Pole(1.525f), new Ring(1.515f, 0.075f, 0.055f, 0.06f), new Ring(1.485f, 0.165f, 0.085f, 0.095f),
            new Ring(1.44f, 0.215f, 0.11f, 0.105f), new Ring(1.37f, 0.212f, 0.135f, 0.115f), new Ring(1.29f, 0.198f, 0.138f, 0.115f),
            new Ring(1.2f, 0.178f, 0.122f, 0.108f), new Ring(1.1f, 0.158f, 0.108f, 0.1f), new Ring(1.02f, 0.162f, 0.106f, 0.101f),
            new Ring(0.985f, 0.176f, 0.12f, 0.115f), new Ring(0.975f, 0.168f, 0.11f, 0.106f), Ring.Pole(0.97f) // ruedo sobre el pantalón
        };

        private static readonly Ring[] Pelvis =
        {
            Ring.Pole(1.01f), new Ring(1.0f, 0.166f, 0.108f, 0.104f), new Ring(0.93f, 0.186f, 0.115f, 0.13f),
            new Ring(0.86f, 0.19f, 0.112f, 0.128f), new Ring(0.81f, 0.17f, 0.1f, 0.112f), Ring.Pole(0.79f)
        };

        private static readonly Ring[] ShortLeg =
        {
            Ring.Pole(0.035f), new Ring(0.025f, 0.09f, 0.09f, 0.092f), new Ring(-0.08f, 0.096f, 0.098f, 0.095f),
            new Ring(-0.16f, 0.102f, 0.102f, 0.099f), new Ring(-0.172f, 0.108f, 0.108f, 0.104f), new Ring(-0.178f, 0.097f, 0.097f, 0.094f),
            Ring.Pole(-0.18f) // botamanga abierta
        };

        private static readonly Ring[] Thigh =
        {
            Ring.Pole(-0.11f), new Ring(-0.12f, 0.084f, 0.096f, 0.081f), new Ring(-0.18f, 0.081f, 0.092f, 0.078f), new Ring(-0.3f, 0.068f, 0.076f, 0.065f),
            new Ring(-0.39f, 0.056f, 0.06f, 0.055f), new Ring(-0.43f, 0.052f, 0.064f, 0.05f), Ring.Pole(-0.46f)
        };

        private static readonly Ring[] Shin =
        {
            Ring.Pole(0.02f), new Ring(0.0f, 0.052f, 0.055f, 0.05f), new Ring(-0.06f, 0.056f, 0.05f, 0.075f), new Ring(-0.1f, 0.055f, 0.049f, 0.073f),
            Ring.Pole(-0.105f)
        };

        private static readonly Ring[] SockProfile =
        {
            Ring.Pole(-0.066f), new Ring(-0.07f, 0.059f, 0.054f, 0.079f), new Ring(-0.14f, 0.057f, 0.051f, 0.075f),
            new Ring(-0.25f, 0.047f, 0.045f, 0.051f), new Ring(-0.35f, 0.034f, 0.035f, 0.034f), new Ring(-0.405f, 0.032f, 0.033f, 0.032f),
            Ring.Pole(-0.41f)
        };

        private static readonly Ring[] SockCuff = // borde superior enrollado de la media
        {
            Ring.Pole(-0.058f), new Ring(-0.06f, 0.064f, 0.06f, 0.085f), new Ring(-0.082f, 0.064f, 0.059f, 0.084f), Ring.Pole(-0.084f)
        };

        private static readonly Ring[] SockBand =
        {
            Ring.Pole(-0.096f), new Ring(-0.098f, 0.062f, 0.057f, 0.083f), new Ring(-0.122f, 0.061f, 0.055f, 0.081f), Ring.Pole(-0.124f)
        };

        // Botín a lo largo de Y (talón en −Y, puntera en +Y); se gira 90° en X para que la puntera mire hacia delante.
        private static readonly Ring[] Boot =
        {
            Ring.Pole(-0.078f), new Ring(-0.068f, 0.036f, 0.03f, 0.04f), new Ring(-0.025f, 0.043f, 0.033f, 0.072f),
            new Ring(0.035f, 0.046f, 0.033f, 0.056f), new Ring(0.1f, 0.044f, 0.031f, 0.036f), new Ring(0.15f, 0.039f, 0.029f, 0.026f),
            new Ring(0.185f, 0.027f, 0.023f, 0.016f), Ring.Pole(0.2f)
        };

        private static readonly Ring[] UpperArm =
        {
            Ring.Pole(-0.095f), new Ring(-0.1f, 0.048f, 0.055f, 0.046f), new Ring(-0.16f, 0.043f, 0.05f, 0.042f),
            new Ring(-0.22f, 0.04f, 0.042f, 0.038f), new Ring(-0.28f, 0.037f, 0.035f, 0.037f), Ring.Pole(-0.31f)
        };

        private static readonly Ring[] Sleeve =
        {
            Ring.Pole(0.062f), new Ring(0.052f, 0.064f, 0.064f, 0.064f), new Ring(-0.02f, 0.069f, 0.069f, 0.066f),
            new Ring(-0.115f, 0.066f, 0.068f, 0.063f), new Ring(-0.126f, 0.071f, 0.073f, 0.068f), new Ring(-0.131f, 0.062f, 0.063f, 0.06f),
            Ring.Pole(-0.133f) // borde de la manga
        };

        private static readonly Ring[] LongSleeveProfile =
        {
            Ring.Pole(0.062f), new Ring(0.052f, 0.064f, 0.064f, 0.064f), new Ring(-0.02f, 0.069f, 0.069f, 0.066f),
            new Ring(-0.15f, 0.058f, 0.062f, 0.055f), new Ring(-0.29f, 0.046f, 0.046f, 0.046f), Ring.Pole(-0.31f)
        };

        private static readonly Ring[] Forearm =
        {
            Ring.Pole(0.025f), new Ring(0.0f, 0.037f, 0.036f, 0.037f), new Ring(-0.05f, 0.041f, 0.04f, 0.038f),
            new Ring(-0.15f, 0.032f, 0.03f, 0.03f), new Ring(-0.235f, 0.024f, 0.02f, 0.02f), Ring.Pole(-0.25f)
        };

        private static readonly Ring[] Hand =
        {
            Ring.Pole(0.008f), new Ring(-0.01f, 0.03f, 0.016f, 0.016f), new Ring(-0.05f, 0.036f, 0.018f, 0.018f),
            new Ring(-0.09f, 0.03f, 0.015f, 0.014f), Ring.Pole(-0.108f)
        };

        private static readonly Ring[] Neck =
        {
            Ring.Pole(1.63f), new Ring(1.61f, 0.05f, 0.052f, 0.05f), new Ring(1.55f, 0.056f, 0.052f, 0.056f),
            new Ring(1.49f, 0.066f, 0.062f, 0.062f), Ring.Pole(1.47f)
        };

        private static readonly Ring[] Skull =
        {
            Ring.Pole(0.275f), new Ring(0.262f, 0.05f, 0.05f, 0.052f), new Ring(0.232f, 0.09f, 0.1f, 0.104f),
            new Ring(0.18f, 0.104f, 0.115f, 0.117f), new Ring(0.12f, 0.1f, 0.115f, 0.11f), new Ring(0.07f, 0.086f, 0.11f, 0.094f),
            new Ring(0.032f, 0.062f, 0.096f, 0.06f), new Ring(0.012f, 0.036f, 0.07f, 0.028f), Ring.Pole(0.0f)
        };

        private static readonly Ring[] Nose =
        {
            Ring.Pole(0.158f), new Ring(0.145f, 0.011f, 0.01f, 0.004f), new Ring(0.115f, 0.016f, 0.026f, 0.006f), Ring.Pole(0.104f)
        };

        private static readonly Ring[] HairCap =
        {
            Ring.Pole(0.29f), new Ring(0.279f, 0.056f, 0.056f, 0.058f), new Ring(0.248f, 0.097f, 0.106f, 0.11f),
            new Ring(0.2f, 0.112f, 0.11f, 0.124f), new Ring(0.16f, 0.112f, 0.075f, 0.122f), new Ring(0.12f, 0.104f, 0.02f, 0.115f),
            Ring.Pole(0.11f)
        };

        // ---- salida ----

        /// <summary>Posiciones (espacio del jugador, en reposo).</summary>
        public readonly List<Vector3> Vertices = new List<Vector3>();
        /// <summary>Color por vértice (alfa = material).</summary>
        public readonly List<Color> Colors = new List<Color>();
        /// <summary>Máscaras para el shader: x sudor, y pliegues.</summary>
        public readonly List<Vector2> Masks = new List<Vector2>();
        /// <summary>Triángulos.</summary>
        public readonly List<int> Triangles = new List<int>();
        /// <summary>Hueso principal de cada vértice.</summary>
        public readonly List<int> Bone0 = new List<int>();
        /// <summary>Segundo hueso de cada vértice.</summary>
        public readonly List<int> Bone1 = new List<int>();
        /// <summary>Peso del hueso principal (el segundo pesa 1 − este).</summary>
        public readonly List<float> Weight0 = new List<float>();

        private readonly List<Vector3> _local = new List<Vector3>();
        private readonly List<int> _localTris = new List<int>();

        /// <summary>Cantidad de triángulos.</summary>
        public int TriangleCount => Triangles.Count / 3;

        /// <summary>Arma la malla de un jugador.</summary>
        public static SkinnedBodyGeometry Build(BodySpec spec)
        {
            var g = new SkinnedBodyGeometry();
            g.BuildTrunk(spec);
            for (int side = -1; side <= 1; side += 2)
            {
                g.BuildLeg(spec, side);
                g.BuildArm(spec, side);
            }

            g.BuildHead(spec);
            return g;
        }

        // ------------------------------------------------------------------ partes

        private void BuildTrunk(BodySpec s)
        {
            Color shirt = Tag(s.Shirt, FabricId), shorts = Tag(s.Shorts, FabricId);
            Sculpt(Torso, TorsoSides, Place.Identity, shirt, TrunkBone, FoldWaist, true, true);
            if (s.Pattern != 0)
            {
                BuildPattern(s.Pattern, Tag(s.Secondary, FabricId));
            }

            Sculpt(Pelvis, TorsoSides, Place.Identity, shorts, TrunkBone, FoldGroin, false, true);
            Sculpt(Neck, LimbSides, Place.Identity, Tag(s.Skin, SkinId), TrunkBone, SweatNeck, false, false);
        }

        private void BuildLeg(BodySpec s, int side)
        {
            int hip = side < 0 ? (int)BodyBone.HipL : (int)BodyBone.HipR;
            int knee = hip + 1, ankle = hip + 2, toe = hip + 3;
            Color skin = Tag(s.Skin, SkinId), shorts = Tag(s.Shorts, FabricId), socks = Tag(s.Socks, FabricId);
            Color leg = s.Trousers ? shorts : skin;
            Sculpt(ShortLeg, LimbSides, At(hip), shorts, hip, FoldGroin, false, true);
            Sculpt(Thigh, LimbSides, At(hip), leg, hip, s.Trousers ? FoldKnee : SweatLimb, false, false);
            Sculpt(Shin, LimbSides, At(knee), leg, knee, s.Trousers ? FoldKnee : SweatLimb, false, false);
            Sculpt(SockProfile, LimbSides, At(knee), socks, knee, FoldAnkle, false, false);
            Sculpt(SockCuff, LimbSides, At(knee), s.Trousers ? leg : socks, knee, FoldAnkle, false, false);
            if (!s.Trousers)
            {
                Sculpt(SockBand, LimbSides, At(knee), Tag(SockStripe(s), FabricId), knee, FoldAnkle, false, false);
            }

            Color boot = Tag(s.Boots, BootId), sole = Tag(Color.Lerp(s.Boots, Color.white, SoleLighten), BootId);
            Place b = At(ankle).Then(new Vector3(0f, -0.025f, 0.045f), new Vector3(90f, 0f, 0f), Vector3.one);
            Sculpt(Boot, LimbSides, b, boot, ankle, NoMask, true, true);
            Box(At(ankle).Then(new Vector3(0f, -0.06f, 0.055f), Vector3.zero, new Vector3(0.072f, 0.01f, 0.26f)), sole, ankle);
            Box(At(ankle).Then(new Vector3(0f, 0.005f, 0.075f), new Vector3(-25f, 0f, 0f), new Vector3(0.042f, 0.045f, 0.012f)), boot, ankle);
            // Tapones: dos en la planta, uno al costado de cada lado y uno en el talón.
            for (int i = 0; i < Studs.Length; i++)
            {
                Box(At(ankle).Then(Studs[i], Vector3.zero, StudSize), sole, ankle);
            }
        }

        private static readonly Vector3[] Studs =
        {
            new Vector3(-0.02f, -0.07f, 0.14f), new Vector3(0.02f, -0.07f, 0.14f), new Vector3(0f, -0.07f, 0.07f), new Vector3(0f, -0.07f, -0.045f)
        };

        private static readonly Vector3 StudSize = new Vector3(0.014f, 0.012f, 0.014f);

        private void BuildArm(BodySpec s, int side)
        {
            int shoulder = side < 0 ? (int)BodyBone.ShoulderL : (int)BodyBone.ShoulderR;
            int elbow = shoulder + 1, wrist = shoulder + 2;
            Color skin = Tag(s.Skin, SkinId), shirt = Tag(s.Shirt, FabricId);
            Color arm = s.LongSleeves ? shirt : skin;
            Sculpt(s.LongSleeves ? LongSleeveProfile : Sleeve, LimbSides, At(shoulder), shirt, shoulder, FoldElbow, false, true);
            if (!s.LongSleeves)
            {
                Sculpt(UpperArm, ArmSides, At(shoulder), arm, shoulder, SweatLimb, false, false);
            }

            Sculpt(Forearm, ArmSides, At(elbow), arm, elbow, s.LongSleeves ? FoldElbow : SweatLimb, false, false);
            Color hand = s.Keeper ? Tag(s.Gloves, FabricId) : skin;
            float glove = s.Keeper ? 1.3f : 1f;
            Sculpt(Hand, ArmSides, At(wrist).Then(Vector3.zero, Vector3.zero, new Vector3(glove, glove, glove)), hand, wrist, NoMask, true, true);
            Ellipsoid(At(wrist).Then(new Vector3(0f, -0.035f, 0.024f), Vector3.zero, new Vector3(0.012f, 0.026f, 0.012f) * glove), hand, wrist, 2, 5);
        }

        private void BuildHead(BodySpec s)
        {
            const int head = (int)BodyBone.Head;
            Color skin = Tag(s.Skin, SkinId), hair = Tag(s.Hair, HairId);
            Sculpt(Skull, HeadSides, HeadAt(Vector3.zero, Vector3.zero, Vector3.one), skin, head, SweatForehead, true, true);
            Sculpt(Nose, 6, HeadAt(new Vector3(0f, 0f, 0.1f), Vector3.zero, Vector3.one), skin, head, NoMask, true, true);
            // Arco superciliar, pómulos y mentón: el relieve que da la luz de los focos.
            Ellipsoid(HeadAt(new Vector3(0f, 0.183f, 0.093f), Vector3.zero, new Vector3(0.078f, 0.016f, 0.026f)), skin, head, 2, 8);
            Ellipsoid(HeadAt(new Vector3(0f, 0.028f, 0.083f), Vector3.zero, new Vector3(0.034f, 0.024f, 0.028f)), skin, head, 2, 6);
            for (int side = -1; side <= 1; side += 2)
            {
                Ellipsoid(HeadAt(new Vector3(side * 0.06f, 0.13f, 0.085f), Vector3.zero, new Vector3(0.028f, 0.02f, 0.024f)), skin, head, 2, 6);
                Ellipsoid(HeadAt(new Vector3(side * 0.104f, 0.14f, -0.008f), new Vector3(0f, side * 15f, 0f), new Vector3(0.016f, 0.03f, 0.022f)), skin, head, 3, 6);
                Box(HeadAt(new Vector3(side * 0.036f, 0.158f, 0.104f), Vector3.zero, new Vector3(0.02f, 0.013f, 0.008f)), Tag(EyeColor, SkinId), head);
                Box(HeadAt(new Vector3(side * 0.037f, 0.18f, 0.111f), new Vector3(0f, 0f, side * -8f), new Vector3(0.038f, 0.008f, 0.01f)), hair, head);
            }

            Box(HeadAt(new Vector3(0f, 0.075f, 0.104f), Vector3.zero, new Vector3(0.04f, 0.007f, 0.01f)), Tag(MouthColor, SkinId), head);
            BuildHair(s, hair);
            BuildFacialHair(s, hair);
        }

        private const int Curls = 6;

        private void BuildHair(BodySpec s, Color hair)
        {
            const int head = (int)BodyBone.Head;
            switch (s.HairStyle)
            {
                case 1: // degradé: casquete pegado y volumen arriba
                    Sculpt(HairCap, HeadSides, HeadAt(Vector3.zero, Vector3.zero, new Vector3(0.985f, 0.97f, 0.985f)), hair, head, NoMask, true, false);
                    Ellipsoid(HeadAt(new Vector3(0f, 0.255f, 0.012f), Vector3.zero, new Vector3(0.08f, 0.033f, 0.092f)), hair, head, 2, 8);
                    break;
                case 2: // melena con vincha
                    Sculpt(HairCap, HeadSides, HeadAt(Vector3.zero, Vector3.zero, new Vector3(1.04f, 1.02f, 1.04f)), hair, head, NoMask, true, false);
                    Ellipsoid(HeadAt(new Vector3(0f, 0.1f, -0.1f), Vector3.zero, new Vector3(0.1f, 0.12f, 0.045f)), hair, head, 3, 8);
                    Sculpt(Band(0.118f, 0.124f), HeadSides, HeadAt(new Vector3(0f, 0.215f, 0.004f), Vector3.zero, Vector3.one), Tag(Color.white, FabricId),
                        head, NoMask, false, false);
                    break;
                case 3: // rulos: casquete y bucles repartidos
                    Sculpt(HairCap, HeadSides, HeadAt(Vector3.zero, Vector3.zero, new Vector3(1.03f, 1.02f, 1.03f)), hair, head, NoMask, true, false);
                    for (int i = 0; i < Curls; i++)
                    {
                        float a = i * Mathf.PI * 2f / (Curls - 1);
                        Vector3 at = i == 0 ? new Vector3(0f, 0.29f, 0f) : new Vector3(Mathf.Cos(a) * 0.075f, 0.262f, Mathf.Sin(a) * 0.08f);
                        Ellipsoid(HeadAt(at, Vector3.zero, new Vector3(0.035f, 0.035f, 0.035f)), hair, head, 2, 5);
                    }

                    break;
                case 4: // rapado
                    Sculpt(HairCap, HeadSides, HeadAt(Vector3.zero, Vector3.zero, new Vector3(0.975f, 0.96f, 0.975f)), hair, head, NoMask, true, false);
                    break;
                case 5: // jopo: costados cortos y volumen hacia arriba y adelante
                    Sculpt(HairCap, HeadSides, HeadAt(Vector3.zero, Vector3.zero, new Vector3(0.98f, 0.97f, 0.98f)), hair, head, NoMask, true, false);
                    Ellipsoid(HeadAt(new Vector3(0f, 0.27f, 0.05f), new Vector3(-30f, 0f, 0f), new Vector3(0.07f, 0.045f, 0.075f)), hair, head, 3, 8);
                    break;
                default: // corto
                    Sculpt(HairCap, HeadSides, HeadAt(Vector3.zero, Vector3.zero, Vector3.one), hair, head, NoMask, true, false);
                    Ellipsoid(HeadAt(new Vector3(0f, 0.25f, 0.02f), Vector3.zero, new Vector3(0.085f, 0.028f, 0.09f)), hair, head, 2, 8);
                    break;
            }
        }

        private void BuildFacialHair(BodySpec s, Color hair)
        {
            const int head = (int)BodyBone.Head;
            if (s.FacialHair == 0)
            {
                return;
            }

            if (s.FacialHair == 1) // barba: cubre la mandíbula
            {
                Ellipsoid(HeadAt(new Vector3(0f, 0.055f, 0.035f), Vector3.zero, new Vector3(0.092f, 0.05f, 0.1f)), hair, head, 3, 8);
            }
            else // candado: mentón y comisuras
            {
                Ellipsoid(HeadAt(new Vector3(0f, 0.035f, 0.092f), Vector3.zero, new Vector3(0.03f, 0.03f, 0.022f)), hair, head, 2, 6);
            }

            Ellipsoid(HeadAt(new Vector3(0f, 0.086f, 0.112f), Vector3.zero, new Vector3(0.026f, 0.007f, 0.008f)), hair, head, 2, 5);
        }

        private static Ring[] Band(float rx, float depth) => new[]
        {
            Ring.Pole(0.016f), new Ring(0.015f, rx, depth, depth), new Ring(-0.015f, rx, depth, depth), Ring.Pole(-0.016f)
        };

        private static Color SockStripe(BodySpec s)
        {
            Color c = s.Secondary;
            float diff = Mathf.Abs(c.r - s.Socks.r) + Mathf.Abs(c.g - s.Socks.g) + Mathf.Abs(c.b - s.Socks.b);
            return diff > 0.3f ? c : (s.Socks.grayscale > 0.5f ? new Color(0.1f, 0.1f, 0.12f) : Color.white);
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

        private void Strip(float yTop, float yBottom, Func<float, float> centre, float halfWidth, Color color)
        {
            for (int side = 0; side < 2; side++)
            {
                bool front = side == 0;
                BeginShell();
                for (int row = 0; row <= PatchRows; row++)
                {
                    float y = Mathf.Lerp(yTop, yBottom, row / (float)PatchRows);
                    Ring r = Sample(Torso, y);
                    for (int col = 0; col <= PatchColumns; col++)
                    {
                        float x = centre(y) + Mathf.Lerp(-halfWidth, halfWidth, col / (float)PatchColumns);
                        float a = Mathf.Acos(Mathf.Clamp(x / Mathf.Max(r.Rx, 0.001f), -1f, 1f));
                        _local.Add(r.Point(front ? a : -a, PatternInflate, PatternOffset));
                    }
                }

                Grid(PatchRows, PatchColumns);
                EndShell(Place.Identity, color, TrunkBone, FoldWaist, Orientation.Axis);
            }
        }

        private void BandAround(float yTop, float yBottom, Color color)
        {
            BeginShell();
            int columns = TorsoSides;
            int rows = PatchRows / 2;
            for (int row = 0; row <= rows; row++)
            {
                Ring r = Sample(Torso, Mathf.Lerp(yTop, yBottom, row / (float)rows));
                for (int col = 0; col <= columns; col++)
                {
                    _local.Add(r.Point(col * Mathf.PI * 2f / columns, PatternInflate, PatternOffset));
                }
            }

            Grid(rows, columns);
            EndShell(Place.Identity, color, TrunkBone, FoldWaist, Orientation.Axis);
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

        // ------------------------------------------------------------------ máscaras del shader (x sudor, y pliegues)

        private delegate Vector2 MaskFn(Vector3 local, Vector3 model);

        private static readonly MaskFn NoMask = (l, m) => Vector2.zero;
        private static readonly MaskFn SweatLimb = (l, m) => new Vector2(0.35f, 0f);
        private static readonly MaskFn SweatNeck = (l, m) => new Vector2(0.8f, 0f);
        private static readonly MaskFn SweatForehead = (l, m) => new Vector2(Smooth01(0.12f, 0.22f, l.y) * Smooth01(0f, 0.08f, l.z) * 0.9f + 0.1f, 0f);
        private static readonly MaskFn FoldWaist = (l, m) => new Vector2(0f, 0.25f + 0.75f * Smooth01(1.15f, 0.99f, m.y) + 0.5f * Smooth01(1.34f, 1.42f, m.y) * Smooth01(0.14f, 0.2f, Mathf.Abs(m.x)));
        private static readonly MaskFn FoldGroin = (l, m) => new Vector2(0f, 0.6f + 0.4f * Smooth01(0.9f, 0.78f, m.y));
        private static readonly MaskFn FoldElbow = (l, m) => new Vector2(0f, 0.4f + 0.6f * Smooth01(-0.06f, -0.12f, l.y));
        private static readonly MaskFn FoldKnee = (l, m) => new Vector2(0f, 0.3f + 0.7f * (1f - Mathf.Clamp01(Mathf.Abs(m.y - 0.48f) / 0.1f)));
        private static readonly MaskFn FoldAnkle = (l, m) => new Vector2(0f, 0.3f + 0.7f * Smooth01(-0.3f, -0.4f, l.y));

        /// <summary>Transición suave entre dos bordes (0 en <paramref name="e0"/>, 1 en <paramref name="e1"/>; acepta bordes invertidos).</summary>
        public static float Smooth01(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        // ------------------------------------------------------------------ pesos

        /// <summary>Huesos y peso de un vértice: mezcla suave con el padre o el hijo cerca de cada articulación.</summary>
        private void Weigh(int bone, Vector3 p, out int b0, out int b1, out float w0)
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
                    float wc = Smooth01(-rc, rc, tc);
                    b0 = child;
                    b1 = bone;
                    w0 = wc;
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

        // ------------------------------------------------------------------ armado de cáscaras

        private enum Orientation { Axis, Centroid }

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

        private static Place HeadAt(Vector3 position, Vector3 euler, Vector3 scale) => new Place
        {
            Origin = BindPositions[(int)BodyBone.Head], Position = position * HeadScale, Euler = euler, Scale = scale * HeadScale
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

        /// <summary>Malla esculpida por secciones; las tapas que quedan dentro de otra pieza se omiten (ahorran triángulos).</summary>
        private void Sculpt(Ring[] rings, int sides, Place place, Color color, int bone, MaskFn mask, bool capTop, bool capBottom)
        {
            BeginShell();
            int ringCount = rings.Length - 2;
            for (int r = 0; r < ringCount; r++)
            {
                for (int i = 0; i < sides; i++)
                {
                    _local.Add(rings[r + 1].Point(i * Mathf.PI * 2f / sides));
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

            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                if (capTop)
                {
                    Tri(top, j, i);
                }

                if (capBottom)
                {
                    Tri(bottom, (ringCount - 1) * sides + i, (ringCount - 1) * sides + j);
                }

                for (int r = 0; r < ringCount - 1; r++)
                {
                    int a = r * sides;
                    Quad(a + i, a + j, a + sides + j, a + sides + i);
                }
            }

            EndShell(place, color, bone, mask, Orientation.Axis);
        }

        /// <summary>Elipsoide de pocas caras (rasgos de la cara, pelo, pulgar).</summary>
        private void Ellipsoid(Place place, Color color, int bone, int rings, int sides)
        {
            var profile = new Ring[rings + 2];
            profile[0] = Ring.Pole(1f);
            profile[rings + 1] = Ring.Pole(-1f);
            for (int i = 1; i <= rings; i++)
            {
                float y = 1f - 2f * i / (rings + 1);
                float r = Mathf.Sqrt(1f - y * y);
                profile[i] = new Ring(y, r, r, r);
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

            EndShell(place, color, bone, NoMask, Orientation.Centroid);
        }

        /// <summary>Caja de caras planas (24 vértices): suela, lengüeta, tapones, ojos, cejas y boca.</summary>
        private void Box(Place place, Color color, int bone)
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

            EndShell(place, color, bone, NoMask, Orientation.Centroid);
        }

        /// <summary>Orienta las caras hacia fuera (en el espacio de la pieza), las lleva al espacio del jugador y les pone pesos.</summary>
        private void EndShell(Place place, Color color, int bone, MaskFn mask, Orientation orientation)
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
                Vertices.Add(model);
                Colors.Add(color);
                Masks.Add(mask(_local[i], model));
                Weigh(bone, model, out int b0, out int b1, out float w0);
                Bone0.Add(b0);
                Bone1.Add(b1);
                Weight0.Add(w0);
            }

            for (int i = 0; i < _localTris.Count; i++)
            {
                Triangles.Add(offset + _localTris[i]);
            }
        }

        private static Color Tag(Color color, float material)
        {
            color.a = material;
            return color;
        }
    }
}
