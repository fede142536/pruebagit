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
        /// <summary>Peinado (0 corto, 1 rapado, 2 largo, 3 cresta, 4 calvo).</summary>
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
        private const float TorsoLength = 0.52f;
        private const float ArmLength = 0.6f;
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
        private readonly Transform _armL, _armR;
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
            Part(_body, "Tronco", MeshKit.Torso, look.Shirt, new Vector3(0f, ShoulderHeight + 0.03f, 0f), new Vector3(0.44f, TorsoLength, 0.25f));
            Part(_body, "Pantalon", MeshKit.Capsule, look.Shorts, new Vector3(0f, HipHeight - 0.04f, 0f), new Vector3(0.42f, 0.26f, 0.28f));
            Part(_body, "Cuello", MeshKit.Capsule, look.Skin, new Vector3(0f, ShoulderHeight + 0.06f, 0f), new Vector3(0.11f, 0.12f, 0.11f));
            AddPattern(_body, look);

            _head = Pivot(_body, "Cabeza", new Vector3(0f, ShoulderHeight + 0.08f, 0f));
            Part(_head, "Cara", MeshKit.Sphere, look.Skin, new Vector3(0f, 0.13f, 0f), new Vector3(0.21f, 0.25f, 0.23f));
            Part(_head, "Nariz", MeshKit.Sphere, look.Skin, new Vector3(0f, 0.12f, 0.115f), new Vector3(0.04f, 0.05f, 0.05f));
            AddHair(_head, look);

            _legL = Leg(_body, "PiernaIzq", -HipOffset, look, out _kneeL);
            _legR = Leg(_body, "PiernaDer", HipOffset, look, out _kneeR);

            _armL = Arm(_body, "BrazoIzq", -ShoulderOffset, look);
            _armR = Arm(_body, "BrazoDer", ShoulderOffset, look);
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

        /// <summary>Rayas, franja o banda de la camiseta: piezas finas que asoman por delante y por detrás del tronco.</summary>
        private void AddPattern(Transform body, PlayerLook look)
        {
            const float chest = ShoulderHeight - TorsoLength * 0.42f;
            switch (look.Pattern)
            {
                case KitPattern.Stripes:
                    for (int i = -1; i <= 1; i++)
                    {
                        Part(body, "Raya", MeshKit.Box, look.Secondary, new Vector3(i * 0.12f, chest, 0f), new Vector3(0.06f, TorsoLength * 0.92f, 0.27f));
                    }

                    break;
                case KitPattern.Band:
                    Part(body, "Franja", MeshKit.Box, look.Secondary, new Vector3(0f, chest + 0.04f, 0f), new Vector3(0.46f, 0.12f, 0.28f));
                    break;
                case KitPattern.Sash:
                    Part(body, "Banda", MeshKit.Box, look.Secondary, new Vector3(0f, chest, 0f), new Vector3(0.1f, TorsoLength * 1.15f, 0.27f),
                        true, Quaternion.Euler(0f, 0f, 38f));
                    break;
                case KitPattern.Chevron:
                    // Dos brazos de la V que bajan desde los hombros hasta el centro del pecho.
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Part(body, "V", MeshKit.Box, look.Secondary, new Vector3(side * 0.1f, chest + 0.1f, 0f),
                            new Vector3(0.08f, TorsoLength * 0.55f, 0.27f), true, Quaternion.Euler(0f, 0f, side * 35f));
                    }

                    break;
                case KitPattern.Halves:
                    Part(body, "Mitad", MeshKit.Box, look.Secondary, new Vector3(0.115f, chest, 0f), new Vector3(0.23f, TorsoLength * 0.92f, 0.27f));
                    break;
            }
        }

        private Transform Leg(Transform body, string name, float x, PlayerLook look, out Transform knee)
        {
            Transform hip = Pivot(body, name, new Vector3(x, HipHeight, 0f));
            // Muslo con la pierna del short, rodilla, pantorrilla con la media hasta debajo de la rodilla y botín redondeado.
            Part(hip, "Muslo", MeshKit.Limb, look.Skin, Vector3.zero, new Vector3(0.17f, ThighLength, 0.17f));
            Part(hip, "PiernaShort", MeshKit.Limb, look.Shorts, Vector3.zero, new Vector3(0.19f, ThighLength * 0.42f, 0.19f));
            knee = Pivot(hip, "Rodilla", new Vector3(0f, -ThighLength, 0f));
            Part(knee, "Pantorrilla", MeshKit.Limb, look.Skin, Vector3.zero, new Vector3(0.14f, ShinLength, 0.15f));
            Part(knee, "Media", MeshKit.Limb, look.Socks, new Vector3(0f, -0.07f, 0f), new Vector3(0.145f, ShinLength * 0.86f, 0.155f));
            Part(knee, "Botin", MeshKit.Capsule, look.Boots, new Vector3(0f, -ShinLength - 0.01f, 0.05f), new Vector3(0.11f, 0.08f, 0.26f),
                true, Quaternion.Euler(90f, 0f, 0f));
            return hip;
        }

        /// <summary>Brazo: manga de la camiseta, brazo y mano; el arquero lleva guantes.</summary>
        private Transform Arm(Transform body, string name, float x, PlayerLook look)
        {
            Transform shoulder = Pivot(body, name, new Vector3(x, ShoulderHeight - 0.02f, 0f));
            Part(shoulder, "Brazo", MeshKit.Limb, look.Skin, Vector3.zero, new Vector3(0.095f, ArmLength, 0.095f));
            Part(shoulder, "Manga", MeshKit.Limb, look.Shirt, new Vector3(0f, 0.02f, 0f), new Vector3(0.12f, ArmLength * 0.36f, 0.12f));
            Color hand = look.Keeper ? look.Gloves : look.Skin;
            Vector3 handSize = look.Keeper ? new Vector3(0.11f, 0.13f, 0.08f) : new Vector3(0.075f, 0.09f, 0.06f);
            Part(shoulder, "Mano", MeshKit.Sphere, hand, new Vector3(0f, -ArmLength - 0.03f, 0f), handSize);
            return shoulder;
        }

        /// <summary>Peinado y barba según el aspecto (determinista: cada jugador siempre igual).</summary>
        private void AddHair(Transform head, PlayerLook look)
        {
            switch (look.HairStyle)
            {
                case 1: // rapado: una capa fina
                    Part(head, "Pelo", MeshKit.Sphere, look.Hair, new Vector3(0f, 0.17f, -0.01f), new Vector3(0.218f, 0.2f, 0.238f));
                    break;
                case 2: // largo: cubre la nuca
                    Part(head, "Pelo", MeshKit.Sphere, look.Hair, new Vector3(0f, 0.18f, -0.03f), new Vector3(0.245f, 0.22f, 0.26f));
                    Part(head, "Melena", MeshKit.Capsule, look.Hair, new Vector3(0f, 0.04f, -0.09f), new Vector3(0.22f, 0.2f, 0.09f));
                    break;
                case 3: // cresta
                    Part(head, "Pelo", MeshKit.Sphere, look.Hair, new Vector3(0f, 0.18f, -0.01f), new Vector3(0.22f, 0.18f, 0.24f));
                    Part(head, "Cresta", MeshKit.Capsule, look.Hair, new Vector3(0f, 0.27f, 0f), new Vector3(0.06f, 0.08f, 0.22f),
                        true, Quaternion.Euler(90f, 0f, 0f));
                    break;
                case 4: // calvo
                    break;
                default: // corto
                    Part(head, "Pelo", MeshKit.Sphere, look.Hair, new Vector3(0f, 0.2f, -0.02f), new Vector3(0.235f, 0.17f, 0.25f));
                    break;
            }

            if (look.Beard)
            {
                Part(head, "Barba", MeshKit.Sphere, look.Hair, new Vector3(0f, 0.05f, 0.03f), new Vector3(0.19f, 0.12f, 0.2f));
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
