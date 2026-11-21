using UnityEngine;
using UnityEngine.Rendering;
using Football.Core.MatchCore;

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
    }

    /// <summary>
    /// Figura humana de bajo coste (13 piezas) con animación procedural: zancada según la velocidad, rodillas que se
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
        private const float KickSwing = 85f;
        private const float TackleLean = -68f;
        private const float DiveRoll = 72f;
        private const float DiveLift = 0.35f;
        private const float MaxLook = 70f;
        private const float TwoPi = Mathf.PI * 2f;

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

        /// <summary>Construye la figura.</summary>
        /// <param name="parent">Objeto padre.</param>
        /// <param name="name">Nombre del objeto.</param>
        /// <param name="look">Colores.</param>
        /// <param name="phaseSeed">Desfase de la zancada, para que no corran todos al compás.</param>
        public PlayerModel(Transform parent, string name, PlayerLook look, float phaseSeed)
        {
            Root = Pivot(parent, name, Vector3.zero);
            _phase = phaseSeed;

            // Sombra bajo los pies: ancla visualmente al jugador al césped.
            Part(Root, "Sombra", MeshKit.Disc, new Color(0.03f, 0.14f, 0.05f), new Vector3(0f, 0.025f, 0f), new Vector3(0.95f, 1f, 0.95f));

            _body = Pivot(Root, "Cuerpo", Vector3.zero);
            Part(_body, "Tronco", MeshKit.Torso, look.Shirt, new Vector3(0f, ShoulderHeight + 0.03f, 0f), new Vector3(0.44f, TorsoLength, 0.25f));
            Part(_body, "Pantalon", MeshKit.Box, look.Shorts, new Vector3(0f, HipHeight - 0.05f, 0f), new Vector3(0.41f, 0.22f, 0.27f));
            AddPattern(_body, look);

            _head = Pivot(_body, "Cabeza", new Vector3(0f, ShoulderHeight + 0.05f, 0f));
            Part(_head, "Cara", MeshKit.Sphere, look.Skin, new Vector3(0f, 0.15f, 0f), new Vector3(0.22f, 0.26f, 0.24f));
            Part(_head, "Pelo", MeshKit.Sphere, look.Hair, new Vector3(0f, 0.21f, -0.02f), new Vector3(0.235f, 0.17f, 0.25f));

            _legL = Leg(_body, "PiernaIzq", -HipOffset, look, out _kneeL);
            _legR = Leg(_body, "PiernaDer", HipOffset, look, out _kneeR);

            _armL = Pivot(_body, "BrazoIzq", new Vector3(-ShoulderOffset, ShoulderHeight - 0.02f, 0f));
            Part(_armL, "Brazo", MeshKit.Limb, look.Skin, Vector3.zero, new Vector3(0.1f, ArmLength, 0.1f));
            _armR = Pivot(_body, "BrazoDer", new Vector3(ShoulderOffset, ShoulderHeight - 0.02f, 0f));
            Part(_armR, "Brazo", MeshKit.Limb, look.Skin, Vector3.zero, new Vector3(0.1f, ArmLength, 0.1f));
        }

        /// <summary>Rayas, franja o banda de la camiseta: piezas finas que asoman por delante y por detrás del tronco.</summary>
        private static void AddPattern(Transform body, PlayerLook look)
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
                    Transform sash = Part(body, "Banda", MeshKit.Box, look.Secondary, new Vector3(0f, chest, 0f), new Vector3(0.1f, TorsoLength * 1.15f, 0.27f));
                    sash.localRotation = Quaternion.Euler(0f, 0f, 38f);
                    break;
            }
        }

        private static Transform Leg(Transform body, string name, float x, PlayerLook look, out Transform knee)
        {
            Transform hip = Pivot(body, name, new Vector3(x, HipHeight, 0f));
            Part(hip, "Muslo", MeshKit.Limb, look.Skin, Vector3.zero, new Vector3(0.16f, ThighLength, 0.16f));
            knee = Pivot(hip, "Rodilla", new Vector3(0f, -ThighLength, 0f));
            Part(knee, "Media", MeshKit.Limb, look.Socks, Vector3.zero, new Vector3(0.13f, ShinLength, 0.13f));
            Part(knee, "Botin", MeshKit.Box, look.Boots, new Vector3(0f, -ShinLength - 0.02f, 0.05f), new Vector3(0.11f, 0.08f, 0.25f));
            return hip;
        }

        private static Transform Pivot(Transform parent, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        private static Transform Part(Transform parent, string name, Mesh mesh, Color color, Vector3 localPosition, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
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
        public void Pose(Vector3 position, float headingDegrees, float speed, float topSpeed, PlayerAction action, float lookYaw,
            bool diveRight, float deltaTime)
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

            if (action == PlayerAction.Kick && _lastAction != PlayerAction.Kick)
            {
                _kickTimer = KickDuration;
            }

            _lastAction = action;
            float kick = 0f;
            if (_kickTimer > 0f)
            {
                _kickTimer -= deltaTime;
                float u = 1f - Mathf.Clamp01(_kickTimer / KickDuration);
                kick = Mathf.Sin(u * Mathf.PI) * KickSwing;
            }

            float swing = Mathf.Sin(_phase) * _amplitude;
            // Ángulo negativo en X = la pierna va hacia delante.
            _legL.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            _legR.localRotation = Quaternion.Euler(swing - kick, 0f, 0f);
            float kneeL = _amplitude * KneeFactor * Mathf.Max(0f, Mathf.Sin(_phase + KneeLead));
            float kneeR = _amplitude * KneeFactor * Mathf.Max(0f, Mathf.Sin(_phase + Mathf.PI + KneeLead)) + kick * 0.3f;
            _kneeL.localRotation = Quaternion.Euler(kneeL, 0f, 0f);
            _kneeR.localRotation = Quaternion.Euler(kneeR, 0f, 0f);
            _armL.localRotation = Quaternion.Euler(swing * ArmFactor, 0f, -ArmSpread);
            _armR.localRotation = Quaternion.Euler(-swing * ArmFactor, 0f, ArmSpread);

            float lean = relative * MaxLean - kick * 0.1f;
            float roll = 0f;
            float lift = Mathf.Abs(Mathf.Sin(_phase)) * Bob * relative;
            if (action == PlayerAction.Tackle)
            {
                lean = TackleLean;
            }
            else if (action == PlayerAction.Dive)
            {
                roll = diveRight ? -DiveRoll : DiveRoll;
                lift = DiveLift;
                lean = 0f;
            }
            else if (action == PlayerAction.Stumble)
            {
                // Pierde el equilibrio: se inclina hacia delante y se bambolea, con los brazos abiertos.
                lean = StumbleLean;
                roll = Mathf.Sin(_phase * 2f) * StumbleRoll;
                _armL.localRotation = Quaternion.Euler(-30f, 0f, -StumbleArms);
                _armR.localRotation = Quaternion.Euler(-30f, 0f, StumbleArms);
            }
            else if (action == PlayerAction.Celebrate)
            {
                // Festejo: brazos en alto agitándose.
                float wave = Mathf.Sin(_phase * 1.5f) * 15f;
                _armL.localRotation = Quaternion.Euler(-170f + wave, 0f, -20f);
                _armR.localRotation = Quaternion.Euler(-170f - wave, 0f, 20f);
                lean = -5f;
            }

            _body.localPosition = new Vector3(0f, lift, 0f);
            _body.localRotation = Quaternion.Euler(lean, 0f, roll);
            _head.localRotation = Quaternion.Euler(0f, Mathf.Clamp(lookYaw, -MaxLook, MaxLook), 0f);
        }
    }
}
