using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Football.Core.MatchCore;
using Football.Core.Domain;

namespace Football.Game
{
    /// <summary>Colores de un jugador.</summary>
    public struct PlayerLook
    {
        /// <summary>Camiseta.</summary>
        public Color Shirt;
        /// <summary>Color secundario de la camiseta.</summary>
        public Color Secondary;
        /// <summary>Diseño de la camiseta.</summary>
        public KitPattern Pattern;
        /// <summary>Pantalón corto.</summary>
        public Color Shorts;
        /// <summary>Medias.</summary>
        public Color Socks;
        /// <summary>Piel.</summary>
        public Color Skin;
        /// <summary>Pelo.</summary>
        public Color Hair;
        /// <summary>Botines.</summary>
        public Color Boots;
        /// <summary>Peinado (0 corto, 1 degradé, 2 melena con vincha, 3 rulos, 4 rapado).</summary>
        public int HairStyle;
        /// <summary>Con barba.</summary>
        public bool Beard;
        /// <summary>Es arquero (lleva guantes).</summary>
        public bool Keeper;
        /// <summary>Color de los guantes del arquero.</summary>
        public Color Gloves;
    }

    /// <summary>
    /// Figura humana de bajo coste (mallas redondeadas, unas 9 por jugador) con animación procedural: zancada, rodillas que se
    /// doblan, brazos en oposición, inclinación, patada, barrida y estirada del portero, y cabeza que sigue al balón.
    /// Es un sustituto provisional del modelo definitivo (con animaciones capturadas), que se podrá cambiar sin tocar la lógica.
    /// </summary>
    public sealed class PlayerModel
    {
        private const float StumbleLean = 28f;
        private const float StumbleRoll = 12f;
        private const float StumbleArms = 60f;
        private const float HipHeight = 0.92f;
        private const float ThighLength = 0.44f;
        private const float ShinLength = 0.42f;
        private const float ShoulderHeight = 1.42f;
        private const float UpperArmLength = 0.29f;
        private const float ForearmLength = 0.235f;
        private const float ElbowRest = 14f;
        private const float ElbowRunFactor = 1.3f;
        private const float ElbowStraight = 4f;
        private const float ElbowHold = 70f;
        // Material de cada pieza en el alfa del color del vértice: el shader da brillo a botines y pelo y pliegues a la tela.
        private const float SkinId = 1f;
        private const float FabricId = 0.75f;
        private const float BootId = 0.5f;
        private const float HairId = 0.25f;
        private const int Curls = 9;
        private const float HipOffset = 0.11f;
        private const float ShoulderOffset = 0.26f;
        private const float IdleSpeed = 0.35f;
        private const float MinSwing = 14f;
        private const float MaxSwing = 52f;
        private const float MinCadence = 1.3f;
        private const float MaxCadence = 2.7f;
        private const float SwingResponse = 10f;
        private const float KneeFactor = 1.3f;
        private const float KneeLead = 1.1f;
        private const float ArmFactor = 0.85f;
        private const float ArmSpread = 8f;
        private const float MaxLean = 14f;
        private const float Bob = 0.05f;
        private const float KickDuration = 0.32f;
        private const float TackleLean = -68f;
        private const float ShotDuration = 0.45f;
        private const float ShotWindUp = 0.4f;        // fracción del tiro dedicada al armado
        private const float ShotBackSwing = 45f;
        private const float ShotFollowThrough = 105f;
        private const float ShotKneeBend = 80f;
        private const float ShotLean = 14f;
        private const float ShotArmSwing = 40f;
        private const float PassSwing = 60f;
        private const float PassHipOpen = 30f;
        private const float PassTwist = 18f;
        private const float SlideRoll = 62f;
        private const float SlideDrop = 0.55f;
        private const float LowDiveRoll = 82f;
        private const float HighDiveRoll = 60f;
        private const float LowDiveLift = 0.15f;
        private const float HighDiveLift = 0.85f;
        private const float HighDiveHeight = 2f;
        private const float RushLean = 28f;
        private const float RushArms = 50f;
        private const float ThrowArch = -14f;
        private const float MaxLook = 70f;
        private const float TwoPi = Mathf.PI * 2f;
        private const float Gravity = 9.81f;
        private const float TurnLeanShare = 0.5f; // fracción del ángulo físico real (atan(v·ω/g)), que exagerado queda raro
        private const float MaxTurnLean = 18f;
        private const float TurnLeanResponse = 10f;
        private const float ShadowLiftShrink = 0.9f; // la sombra se achica al despegarse del suelo (estirada, salto)
        private static readonly Vector3 ShadowScale = new Vector3(0.95f, 1f, 0.95f);

        /// <summary>Raíz del jugador, a la altura de los pies.</summary>
        public readonly Transform Root;

        private readonly Transform _body;
        private readonly Transform _head;
        private readonly Transform _legL, _legR, _kneeL, _kneeR;
        private readonly Transform _armL, _armR, _elbowL, _elbowR;
        private float _phase;
        private float _amplitude;
        private float _kickTimer;
        private PlayerAction _lastAction;
        private PlayerAction _strike;
        private readonly Transform _shadow;
        private float _lastHeading;
        private float _turnRoll;
        private bool _hasHeading;

        // Piezas agrupadas por articulación (una malla con color por vértice por cada una): ~9 objetos por jugador en lugar
        // de 13-16. Solo con el shader de jugadores, que multiplica por el color del vértice; si no está, piezas sueltas.
        private readonly bool _merge = Materials.PlayerShaderAvailable;
        private readonly Dictionary<Transform, MeshMerge> _batches = new Dictionary<Transform, MeshMerge>();
        private readonly List<Mesh> _ownedMeshes;

        /// <summary>Construye la figura.</summary>
        /// <param name="parent">Objeto padre.</param>
        /// <param name="name">Nombre del objeto.</param>
        /// <param name="look">Colores.</param>
        /// <param name="phaseSeed">Desfase de la zancada, para que no corran todos al compás.</param>
        /// <param name="build">Contextura: escala de la figura (1 = normal), para que no sean todos iguales.</param>
        /// <param name="ownedMeshes">Lista donde se anotan las mallas creadas, para destruirlas al cerrar la escena.</param>
        public PlayerModel(Transform parent, string name, PlayerLook look, float phaseSeed, float build, List<Mesh> ownedMeshes)
        {
            _ownedMeshes = ownedMeshes;
            Root = Pivot(parent, name, Vector3.zero);
            Root.localScale = new Vector3(build, build, build);
            _phase = phaseSeed;

            // Sombra bajo los pies: ancla visualmente al jugador al césped. Material plano (no lleva borde ni suciedad).
            _shadow = Part(Root, "Sombra", MeshKit.Disc, new Color(0.03f, 0.14f, 0.05f), new Vector3(0f, 0.025f, 0f), ShadowScale, false);

            _body = Pivot(Root, "Cuerpo", Vector3.zero);
            // Tronco, pelvis y cuello: mallas continuas esculpidas (AnatomyKit), sin primitivas sueltas.
            Part(_body, "Tronco", AnatomyKit.Torso, Tag(look.Shirt, FabricId), Vector3.zero, Vector3.one);
            Mesh pattern = AnatomyKit.Pattern(look.Pattern);
            if (pattern != null)
            {
                Part(_body, "Diseno", pattern, Tag(look.Secondary, FabricId), Vector3.zero, Vector3.one);
            }

            Part(_body, "Pantalon", AnatomyKit.Pelvis, Tag(look.Shorts, FabricId), Vector3.zero, Vector3.one);
            Part(_body, "Cuello", AnatomyKit.Neck, Tag(look.Skin, SkinId), Vector3.zero, Vector3.one);

            _head = Pivot(_body, "Cabeza", new Vector3(0f, ShoulderHeight + 0.08f, 0f));
            Color skin = Tag(look.Skin, SkinId);
            Part(_head, "Craneo", AnatomyKit.Skull, skin, Vector3.zero, Vector3.one);
            Part(_head, "Nariz", AnatomyKit.Nose, skin, new Vector3(0f, 0f, 0.1f), Vector3.one);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(_head, "Oreja", MeshKit.Sphere, skin, new Vector3(side * 0.103f, 0.14f, -0.008f), new Vector3(0.024f, 0.058f, 0.04f));
                Part(_head, "Ojo", MeshKit.Sphere, Tag(EyeColor, SkinId), new Vector3(side * 0.036f, 0.158f, 0.103f), new Vector3(0.02f, 0.014f, 0.01f));
            }

            AddHair(_head, look);

            _legL = Leg(_body, "PiernaIzq", -HipOffset, look, out _kneeL);
            _legR = Leg(_body, "PiernaDer", HipOffset, look, out _kneeR);

            _armL = Arm(_body, "BrazoIzq", -ShoulderOffset, look, out _elbowL);
            _armR = Arm(_body, "BrazoDer", ShoulderOffset, look, out _elbowR);
            FlushBatches();
        }

        /// <summary>Crea un objeto por articulación con todas sus piezas juntas.</summary>
        private void FlushBatches()
        {
            foreach (KeyValuePair<Transform, MeshMerge> batch in _batches)
            {
                Mesh mesh = batch.Value.Build();
                _ownedMeshes?.Add(mesh);
                var go = new GameObject("Piezas");
                go.transform.SetParent(batch.Key, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = Materials.GetPlayer(Color.white); // un solo material para todos los jugadores
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            _batches.Clear();
        }

        /// <summary>Color con el material de la pieza en el alfa (lo lee el shader de jugadores).</summary>
        private static Color Tag(Color color, float material)
        {
            color.a = material;
            return color;
        }

        private static readonly Color EyeColor = new Color(0.07f, 0.05f, 0.04f);

        /// <summary>Pierna: muslo con cuádriceps y el pantalón encima; pierna con gemelo, media y botín con suela y lengüeta.</summary>
        private Transform Leg(Transform body, string name, float x, PlayerLook look, out Transform knee)
        {
            Transform hip = Pivot(body, name, new Vector3(x, HipHeight, 0f));
            Part(hip, "Muslo", AnatomyKit.Thigh, Tag(look.Skin, SkinId), Vector3.zero, Vector3.one);
            Part(hip, "PiernaShort", AnatomyKit.ShortLeg, Tag(look.Shorts, FabricId), Vector3.zero, Vector3.one);
            knee = Pivot(hip, "Rodilla", new Vector3(0f, -ThighLength, 0f));
            Part(knee, "Pierna", AnatomyKit.Shin, Tag(look.Skin, SkinId), Vector3.zero, Vector3.one);
            Part(knee, "Media", AnatomyKit.Sock, Tag(look.Socks, FabricId), Vector3.zero, Vector3.one);
            Color boot = Tag(look.Boots, BootId);
            Part(knee, "Botin", AnatomyKit.Boot, boot, new Vector3(0f, -ShinLength - 0.025f, 0.045f), Vector3.one, true,
                Quaternion.Euler(90f, 0f, 0f));
            Part(knee, "Suela", MeshKit.Box, Tag(Color.Lerp(look.Boots, Color.white, SoleLighten), BootId),
                new Vector3(0f, -ShinLength - 0.06f, 0.055f), new Vector3(0.072f, 0.01f, 0.26f));
            Part(knee, "Lengueta", MeshKit.Box, boot, new Vector3(0f, -ShinLength + 0.005f, 0.075f), new Vector3(0.042f, 0.045f, 0.012f),
                true, Quaternion.Euler(-25f, 0f, 0f));
            return hip;
        }

        private const float SoleLighten = 0.65f;

        /// <summary>Brazo: deltoides y bíceps con la manga encima, codo, antebrazo y mano con pulgar; el arquero lleva guantes.</summary>
        private Transform Arm(Transform body, string name, float x, PlayerLook look, out Transform elbow)
        {
            Transform shoulder = Pivot(body, name, new Vector3(x, ShoulderHeight - 0.02f, 0f));
            Part(shoulder, "Brazo", AnatomyKit.UpperArm, Tag(look.Skin, SkinId), Vector3.zero, Vector3.one);
            Part(shoulder, "Manga", AnatomyKit.Sleeve, Tag(look.Shirt, FabricId), Vector3.zero, Vector3.one);
            elbow = Pivot(shoulder, "Codo", new Vector3(0f, -UpperArmLength, 0f));
            Part(elbow, "Antebrazo", AnatomyKit.Forearm, Tag(look.Skin, SkinId), Vector3.zero, Vector3.one);
            Color hand = look.Keeper ? Tag(look.Gloves, FabricId) : Tag(look.Skin, SkinId);
            Vector3 handScale = look.Keeper ? new Vector3(1.35f, 1.25f, 1.5f) : Vector3.one;
            Part(elbow, "Mano", AnatomyKit.Hand, hand, new Vector3(0f, -ForearmLength, 0f), handScale);
            Part(elbow, "Pulgar", MeshKit.Sphere, hand, new Vector3(0f, -ForearmLength - 0.035f, 0.024f), new Vector3(0.018f, 0.045f, 0.018f) * handScale.y);
            return shoulder;
        }

        /// <summary>Peinado y barba según el aspecto (determinista: cada jugador siempre igual).</summary>
        private void AddHair(Transform head, PlayerLook look)
        {
            Color hair = Tag(look.Hair, HairId);
            switch (look.HairStyle)
            {
                case 1: // degradé: casquete pegado y volumen arriba
                    Part(head, "Pelo", AnatomyKit.HairCap, hair, Vector3.zero, new Vector3(0.985f, 0.97f, 0.985f));
                    Part(head, "Volumen", MeshKit.Sphere, hair, new Vector3(0f, 0.255f, 0.012f), new Vector3(0.16f, 0.065f, 0.185f));
                    break;
                case 2: // melena con vincha
                    Part(head, "Pelo", AnatomyKit.HairCap, hair, Vector3.zero, new Vector3(1.04f, 1.02f, 1.04f));
                    Part(head, "Melena", MeshKit.Capsule, hair, new Vector3(0f, 0.1f, -0.1f), new Vector3(0.2f, 0.22f, 0.085f));
                    Part(head, "Vincha", MeshKit.Cylinder, Tag(Color.white, FabricId), new Vector3(0f, 0.215f, 0.004f), new Vector3(0.236f, 0.03f, 0.248f));
                    break;
                case 3: // rulos: casquete y bucles repartidos sobre la cabeza
                    Part(head, "Pelo", AnatomyKit.HairCap, hair, Vector3.zero, new Vector3(1.03f, 1.02f, 1.03f));
                    for (int i = 0; i < Curls; i++)
                    {
                        float a = i * Mathf.PI * 2f / (Curls - 1);
                        Vector3 at = i == 0 ? new Vector3(0f, 0.29f, 0f) : new Vector3(Mathf.Cos(a) * 0.075f, 0.262f, Mathf.Sin(a) * 0.08f);
                        Part(head, "Rulo", MeshKit.Sphere, hair, at, Vector3.one * 0.07f);
                    }

                    break;
                case 4: // rapado: una capa fina
                    Part(head, "Pelo", AnatomyKit.HairCap, hair, Vector3.zero, new Vector3(0.975f, 0.96f, 0.975f));
                    break;
                default: // corto
                    Part(head, "Pelo", AnatomyKit.HairCap, hair, Vector3.zero, Vector3.one);
                    Part(head, "Volumen", MeshKit.Sphere, hair, new Vector3(0f, 0.25f, 0.02f), new Vector3(0.17f, 0.055f, 0.18f));
                    break;
            }

            if (look.Beard)
            {
                Part(head, "Barba", MeshKit.Sphere, hair, new Vector3(0f, 0.055f, 0.035f), new Vector3(0.185f, 0.1f, 0.2f));
                Part(head, "Bigote", MeshKit.Capsule, hair, new Vector3(0f, 0.085f, 0.112f), new Vector3(0.05f, 0.012f, 0.014f),
                    true, Quaternion.Euler(0f, 0f, 90f));
            }
        }

        private static Transform Pivot(Transform parent, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        private Transform Part(Transform parent, string name, Mesh mesh, Color color, Vector3 localPosition, Vector3 scale,
            bool body = true, Quaternion? rotation = null)
        {
            Quaternion localRotation = rotation ?? Quaternion.identity;
            if (body && _merge)
            {
                if (!_batches.TryGetValue(parent, out MeshMerge batch))
                {
                    batch = new MeshMerge();
                    _batches[parent] = batch;
                }

                batch.Add(mesh, localPosition, localRotation, scale, color);
                return null;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            go.transform.localRotation = localRotation;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            color.a = 1f; // el alfa solo marca el material en el shader de jugadores
            renderer.sharedMaterial = Materials.Get(color);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go.transform;
        }

        /// <summary>Muestra u oculta al jugador (por ejemplo, si lo expulsan).</summary>
        public void SetVisible(bool visible)
        {
            if (Root.gameObject.activeSelf != visible)
            {
                Root.gameObject.SetActive(visible);
            }
        }

        /// <summary>Coloca y anima al jugador para este fotograma.</summary>
        /// <param name="position">Posición de los pies en coordenadas de Unity.</param>
        /// <param name="headingDegrees">Orientación en grados en el plano del motor (0 = hacia +X).</param>
        /// <param name="speed">Rapidez (m/s).</param>
        /// <param name="topSpeed">Velocidad de referencia para la zancada máxima (m/s).</param>
        /// <param name="action">Acción visible.</param>
        /// <param name="lookYaw">Giro de la cabeza hacia el balón (grados, positivo a la derecha).</param>
        /// <param name="diveRight">En una estirada, verdadero si se lanza hacia su derecha.</param>
        /// <param name="deltaTime">Tiempo del fotograma (s).</param>
        /// <param name="diveHeight">Altura del balón en la estirada del arquero (m).</param>
        /// <param name="holdingThrow">Espera para sacar de banda: el balón con las dos manos detrás de la cabeza.</param>
        /// <param name="holdingBall">Arquero con el balón en las manos (tras atajar o descolgar un centro).</param>
        public void Pose(Vector3 position, float headingDegrees, float speed, float topSpeed, PlayerAction action, float lookYaw,
            bool diveRight, float deltaTime, float diveHeight = 0f, bool holdingThrow = false, bool holdingBall = false)
        {
            Root.localPosition = position;
            Root.localRotation = Quaternion.Euler(0f, 90f - headingDegrees, 0f);

            float relative = Mathf.Clamp01(speed / topSpeed);
            float target = speed < IdleSpeed ? 0f : Mathf.Lerp(MinSwing, MaxSwing, relative);
            _amplitude = Mathf.Lerp(_amplitude, target, 1f - Mathf.Exp(-SwingResponse * deltaTime));
            _phase += deltaTime * Mathf.Lerp(MinCadence, MaxCadence, relative) * TwoPi;
            if (_phase > TwoPi)
            {
                _phase -= TwoPi;
            }

            bool strike = action == PlayerAction.Kick || action == PlayerAction.Pass || action == PlayerAction.Shoot;
            if (action != _lastAction && (strike || action == PlayerAction.ThrowIn))
            {
                _kickTimer = action == PlayerAction.Shoot ? ShotDuration : KickDuration;
                _strike = action;
            }

            _lastAction = action;
            float u = 0f; // avance del golpeo (0 = empieza, 1 = terminó)
            if (_kickTimer > 0f)
            {
                _kickTimer -= deltaTime;
                u = 1f - Mathf.Clamp01(_kickTimer / (_strike == PlayerAction.Shoot ? ShotDuration : KickDuration));
            }

            // Zancada de base (curvas suaves: seno de la fase).
            float swing = Mathf.Sin(_phase) * _amplitude;
            float legL = -swing, legR = swing;
            float kneeL = _amplitude * KneeFactor * Mathf.Max(0f, Mathf.Sin(_phase + KneeLead));
            float kneeR = _amplitude * KneeFactor * Mathf.Max(0f, Mathf.Sin(_phase + Mathf.PI + KneeLead));
            float armL = swing * ArmFactor, armR = -swing * ArmFactor;
            float armSpreadL = -ArmSpread, armSpreadR = ArmSpread;
            float legOut = 0f; // apertura de la pierna que golpea (pase con el interior)
            float twist = 0f;  // giro de cadera
            float lean = relative * MaxLean;
            float roll = 0f;
            float lift = Mathf.Abs(Mathf.Sin(_phase)) * Bob * relative;

            if (_kickTimer > 0f && _strike != PlayerAction.ThrowIn)
            {
                if (_strike == PlayerAction.Shoot)
                {
                    // Tiro: armado amplio hacia atrás, golpe y acompañamiento; el torso se inclina sobre el balón.
                    float back = u < ShotWindUp ? Mathf.SmoothStep(0f, 1f, u / ShotWindUp) : 1f - Mathf.SmoothStep(0f, 1f, (u - ShotWindUp) / (1f - ShotWindUp));
                    float through = u < ShotWindUp ? 0f : Mathf.SmoothStep(0f, 1f, (u - ShotWindUp) / (1f - ShotWindUp));
                    legR = ShotBackSwing * back - ShotFollowThrough * through;
                    kneeR = ShotKneeBend * back;
                    legL = -8f;
                    lean = ShotLean * Mathf.Sin(u * Mathf.PI);
                    armL = -ShotArmSwing * through;
                    armSpreadL = -40f;
                    armSpreadR = 35f;
                }
                else
                {
                    // Pase: la cadera se abre y la pierna golpea con el interior (más corto que el tiro).
                    float hit = Mathf.Sin(u * Mathf.PI);
                    legR = -PassSwing * hit + 15f * (1f - u);
                    kneeR = 20f * hit;
                    legOut = _strike == PlayerAction.Pass ? PassHipOpen * hit : 0f;
                    twist = _strike == PlayerAction.Pass ? PassTwist * hit : 0f;
                    lean = 6f;
                    armSpreadL = -25f;
                }
            }

            switch (action)
            {
                case PlayerAction.Tackle:
                    lean = TackleLean * 0.4f; // entrada de pie: se agacha y estira la pierna
                    legR = -55f;
                    break;
                case PlayerAction.Slide:
                    // Barrida: deslizamiento rasante de costado, una pierna extendida al frente y la otra doblada.
                    roll = SlideRoll;
                    lean = 0f;
                    lift = -SlideDrop;
                    legR = -85f;
                    kneeR = 0f;
                    legL = -30f;
                    kneeL = 70f;
                    armL = -20f;
                    armSpreadL = -70f;
                    armR = 10f;
                    break;
                case PlayerAction.Dive:
                {
                    // Estirada hacia el palo según la altura del balón: abajo, a ras del piso; arriba, en el aire.
                    float high = Mathf.Clamp01(diveHeight / HighDiveHeight);
                    roll = (diveRight ? -1f : 1f) * Mathf.Lerp(LowDiveRoll, HighDiveRoll, high);
                    lift = Mathf.Lerp(LowDiveLift, HighDiveLift, high);
                    lean = 0f;
                    float reach = Mathf.Lerp(-120f, -175f, high);
                    armL = reach;
                    armR = reach;
                    armSpreadL = -15f;
                    armSpreadR = 15f;
                    legL = 15f;
                    legR = -10f;
                    break;
                }

                case PlayerAction.Rush:
                    // Achique: agachado, brazos abiertos para tapar el ángulo.
                    lean = RushLean;
                    lift = -0.08f;
                    armSpreadL = -RushArms;
                    armSpreadR = RushArms;
                    armL = -20f;
                    armR = -20f;
                    kneeL += 25f;
                    kneeR += 25f;
                    break;
                case PlayerAction.Stumble:
                    lean = StumbleLean;
                    roll = Mathf.Sin(_phase * 2f) * StumbleRoll;
                    armL = -30f;
                    armR = -30f;
                    armSpreadL = -StumbleArms;
                    armSpreadR = StumbleArms;
                    break;
                case PlayerAction.Celebrate:
                {
                    float wave = Mathf.Sin(_phase * 1.5f) * 15f;
                    armL = -170f + wave;
                    armR = -170f - wave;
                    armSpreadL = -20f;
                    armSpreadR = 20f;
                    lean = -5f;
                    break;
                }
            }

            if (holdingThrow || (_kickTimer > 0f && _strike == PlayerAction.ThrowIn))
            {
                // Saque de banda: balón con las dos manos por detrás de la nuca, espalda arqueada; al sacar, los brazos
                // pasan por encima de la cabeza hacia delante.
                float release = holdingThrow ? 0f : Mathf.SmoothStep(0f, 1f, u);
                armL = Mathf.Lerp(-200f, -110f, release);
                armR = armL;
                armSpreadL = -12f;
                armSpreadR = 12f;
                lean = Mathf.Lerp(ThrowArch, 10f, release);
                legL = -10f;
                legR = 8f;
            }
            else if (holdingBall)
            {
                // Arquero con el balón: lo sujeta contra el pecho con los dos brazos hacia delante.
                armL = -70f;
                armR = -70f;
                armSpreadL = -18f;
                armSpreadR = 18f;
            }

            _legL.localRotation = Quaternion.Euler(legL, 0f, 0f);
            _legR.localRotation = Quaternion.Euler(legR, -legOut, 0f);
            _kneeL.localRotation = Quaternion.Euler(kneeL, 0f, 0f);
            _kneeR.localRotation = Quaternion.Euler(kneeR, 0f, 0f);
            _armL.localRotation = Quaternion.Euler(armL, 0f, armSpreadL);
            _armR.localRotation = Quaternion.Euler(armR, 0f, armSpreadR);
            // Codo: algo doblado en reposo y más al correr; estirado al lanzarse, festejar o sacar de banda.
            float elbow = ElbowRest + _amplitude * ElbowRunFactor;
            if (action == PlayerAction.Dive || action == PlayerAction.Celebrate || holdingThrow || (_kickTimer > 0f && _strike == PlayerAction.ThrowIn))
            {
                elbow = ElbowStraight;
            }
            else if (holdingBall)
            {
                elbow = ElbowHold;
            }

            _elbowL.localRotation = Quaternion.Euler(-elbow, 0f, 0f);
            _elbowR.localRotation = Quaternion.Euler(-elbow, 0f, 0f);

            roll += TurnLean(headingDegrees, speed, deltaTime);
            _body.localPosition = new Vector3(0f, lift, 0f);
            _body.localRotation = Quaternion.Euler(lean, twist, roll);
            _shadow.localScale = ShadowScale * (1f - Mathf.Clamp(lift, 0f, HighDiveLift) * ShadowLiftShrink);
            _head.localRotation = Quaternion.Euler(0f, Mathf.Clamp(lookYaw, -MaxLook, MaxLook), 0f);
        }

        /// <summary>
        /// Inclinación hacia dentro de la curva, como un corredor real: proporcional a velocidad × giro,
        /// suavizada para que no tiemble con los cambios bruscos de orientación.
        /// </summary>
        private float TurnLean(float headingDegrees, float speed, float deltaTime)
        {
            if (!_hasHeading || deltaTime <= 0f)
            {
                _hasHeading = true;
                _lastHeading = headingDegrees;
                return _turnRoll;
            }

            float turnRate = Mathf.DeltaAngle(_lastHeading, headingDegrees) * Mathf.Deg2Rad / deltaTime;
            _lastHeading = headingDegrees;
            float target = Mathf.Atan(speed * turnRate / Gravity) * Mathf.Rad2Deg * TurnLeanShare;
            target = Mathf.Clamp(target, -MaxTurnLean, MaxTurnLean);
            _turnRoll = Mathf.Lerp(_turnRoll, target, 1f - Mathf.Exp(-TurnLeanResponse * deltaTime));
            return _turnRoll;
        }
    }
}
