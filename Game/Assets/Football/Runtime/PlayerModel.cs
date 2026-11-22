using System.Collections.Generic;
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
            }
        }

        private Transform Leg(Transform body, string name, float x, PlayerLook look, out Transform knee)
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

            roll += TurnLean(headingDegrees, speed, deltaTime);
            _body.localPosition = new Vector3(0f, lift, 0f);
            _body.localRotation = Quaternion.Euler(lean, 0f, roll);
            _shadow.localScale = ShadowScale * (1f - Mathf.Min(lift, DiveLift) * ShadowLiftShrink);
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
