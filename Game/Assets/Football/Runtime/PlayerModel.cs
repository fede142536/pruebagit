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
        /// <summary>Peinado (0 corto, 1 degradé, 2 melena con vincha, 3 rulos, 4 rapado, 5 jopo).</summary>
        public int HairStyle;
        /// <summary>Pantalón largo (traje o buzo del DT): las piernas van del color del pantalón.</summary>
        public bool Trousers;
        /// <summary>Manga larga (traje o buzo del DT): los brazos van del color de la camiseta.</summary>
        public bool LongSleeves;
        /// <summary>Con barba (equivale a <see cref="FacialHair"/> = 1 si este no se indica).</summary>
        public bool Beard;
        /// <summary>Vello facial: 0 nada, 1 barba, 2 candado (si es 0 y <see cref="Beard"/> está, barba).</summary>
        public int FacialHair;
        /// <summary>Es arquero (lleva guantes).</summary>
        public bool Keeper;
        /// <summary>Color de los guantes del arquero.</summary>
        public Color Gloves;
    }

    /// <summary>
    /// Figura humana: una sola malla con un <see cref="SkinnedMeshRenderer"/> de 20 huesos (<see cref="BodyBone"/>) armada por
    /// <see cref="SkinnedBodyGeometry"/>, con pesos suaves en codos, rodillas, hombros, cuello e ingle. Animación procedural sin
    /// basura por fotograma: zancada con rodillas, brazos en oposición, giro de pelvis y contragiro del pecho, inclinación en
    /// las curvas repartida entre pelvis y columna, apoyo del pie (suela paralela al césped, puntera que se dobla al despegar y
    /// pelvis que baja para que el pie de apoyo toque el suelo), mirada al balón repartida entre cuello y cabeza con límites
    /// anatómicos, y los gestos de patada, barrida, estirada, achique, saque de banda y festejo. Si el shader de jugadores no
    /// está, la misma malla se dibuja con un material plano por color (submallas).
    /// </summary>
    public sealed class PlayerModel
    {
        private const float StumbleLean = 28f;
        private const float StumbleRoll = 12f;
        private const float StumbleArms = 60f;
        private const float ElbowRest = 14f;
        private const float ElbowRunFactor = 1.3f;
        private const float ElbowStraight = 4f;
        private const float ElbowHold = 70f;
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

        // Reparto de la inclinación entre la raíz (todo el cuerpo desde los pies), la pelvis y la columna.
        private const float LeanRootShare = 0.5f, LeanSpineShare = 0.25f, LeanChestShare = 0.25f;
        private const float BankRootShare = 0.5f, BankPelvisShare = 0.2f, BankSpineShare = 0.3f;
        private const float PelvisSwingYaw = 0.18f;   // la pelvis gira con la zancada…
        private const float ChestCounterYaw = 0.22f;  // …y el pecho contragira
        // Mirada: cuello y cabeza se reparten el giro, cada uno con su tope anatómico.
        private const float NeckLookShare = 0.4f;
        private const float NeckMaxYaw = 35f, HeadMaxYaw = 50f;
        private const float NeckMaxUp = 25f, NeckMaxDown = 20f, HeadMaxUp = 35f, HeadMaxDown = 30f;
        // Apoyo del pie.
        private const float PlantKneeLimit = 35f;     // con la rodilla más doblada que esto el pie está en el aire
        private const float AnkleMin = -35f, AnkleMax = 45f;
        private const float ToePushShare = 0.6f, ToeMaxBend = 40f, ToePushFrom = 10f;
        private const float MaxPelvisDrop = 0.08f;
        private static readonly Vector3 RendererBoundsCenter = new Vector3(0f, 0.9f, 0f);
        private static readonly Vector3 RendererBoundsSize = new Vector3(2.4f, 2.4f, 2.4f);

        /// <summary>Raíz del jugador, a la altura de los pies.</summary>
        public readonly Transform Root;

        private readonly Transform[] _bones = new Transform[SkinnedBodyGeometry.BoneCount];
        private readonly Transform _body, _pelvis, _spine, _chest, _neck, _head;
        private readonly Transform _legL, _legR, _kneeL, _kneeR, _ankleL, _ankleR, _toeL, _toeR;
        private readonly Transform _armL, _armR, _elbowL, _elbowR;
        private readonly Transform _shadow;
        private readonly float _pelvisHeight;
        private readonly float _ankleHeight;
        private float _phase;
        private float _amplitude;
        private float _kickTimer;
        private PlayerAction _lastAction;
        private PlayerAction _strike;
        private float _lastHeading;
        private float _turnRoll;
        private bool _hasHeading;

        /// <summary>Construye la figura.</summary>
        /// <param name="parent">Objeto padre.</param>
        /// <param name="name">Nombre del objeto.</param>
        /// <param name="look">Colores y aspecto.</param>
        /// <param name="phaseSeed">Desfase de la zancada, para que no corran todos al compás.</param>
        /// <param name="build">Contextura: escala de la figura (1 = normal), para que no sean todos iguales.</param>
        /// <param name="ownedMeshes">Lista donde se anotan las mallas creadas, para destruirlas al cerrar la escena.</param>
        public PlayerModel(Transform parent, string name, PlayerLook look, float phaseSeed, float build, List<Mesh> ownedMeshes)
        {
            Root = Pivot(parent, name, Vector3.zero);
            Root.localScale = new Vector3(build, build, build);
            _phase = phaseSeed;

            // Sombra de contacto bajo los pies (malla y material compartidos por todos): ancla la figura al césped.
            _shadow = new GameObject("Sombra").transform;
            _shadow.SetParent(Root, false);
            _shadow.localPosition = new Vector3(0f, 0.025f, 0f);
            _shadow.localScale = ShadowScale;
            _shadow.gameObject.AddComponent<MeshFilter>().sharedMesh = MeshKit.Disc;
            var shadowRenderer = _shadow.gameObject.AddComponent<MeshRenderer>();
            shadowRenderer.sharedMaterial = Materials.Get(new Color(0.03f, 0.14f, 0.05f));
            shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
            shadowRenderer.receiveShadows = false;

            // Esqueleto en reposo (rotaciones identidad): cada hueso, a su altura relativa al padre.
            for (int b = 0; b < _bones.Length; b++)
            {
                int p = SkinnedBodyGeometry.Parents[b];
                Transform owner = p < 0 ? Root : _bones[p];
                Vector3 local = SkinnedBodyGeometry.BindPositions[b] - (p < 0 ? Vector3.zero : SkinnedBodyGeometry.BindPositions[p]);
                _bones[b] = Pivot(owner, SkinnedBodyGeometry.BoneNames[b], local);
            }

            _body = Bone(BodyBone.Root);
            _pelvis = Bone(BodyBone.Pelvis);
            _spine = Bone(BodyBone.Spine);
            _chest = Bone(BodyBone.Chest);
            _neck = Bone(BodyBone.Neck);
            _head = Bone(BodyBone.Head);
            _legL = Bone(BodyBone.HipL);
            _legR = Bone(BodyBone.HipR);
            _kneeL = Bone(BodyBone.KneeL);
            _kneeR = Bone(BodyBone.KneeR);
            _ankleL = Bone(BodyBone.AnkleL);
            _ankleR = Bone(BodyBone.AnkleR);
            _toeL = Bone(BodyBone.ToeL);
            _toeR = Bone(BodyBone.ToeR);
            _armL = Bone(BodyBone.ShoulderL);
            _armR = Bone(BodyBone.ShoulderR);
            _elbowL = Bone(BodyBone.ElbowL);
            _elbowR = Bone(BodyBone.ElbowR);
            _pelvisHeight = SkinnedBodyGeometry.BindPositions[(int)BodyBone.Pelvis].y;
            _ankleHeight = SkinnedBodyGeometry.BindPositions[(int)BodyBone.AnkleL].y;

            SkinnedBodyGeometry geometry = SkinnedBodyGeometry.Build(SpecOf(look));
            Mesh mesh = BuildMesh(geometry, out Material[] materials);
            ownedMeshes?.Add(mesh);
            var go = new GameObject("Malla");
            go.transform.SetParent(Root, false);
            var skin = go.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh;
            skin.bones = _bones;
            skin.rootBone = _body;
            skin.sharedMaterials = materials;
            skin.quality = SkinQuality.Bone2;          // dos huesos por vértice: lo que arma la geometría
            skin.updateWhenOffscreen = false;
            skin.skinnedMotionVectors = false;
            skin.localBounds = new Bounds(RendererBoundsCenter, RendererBoundsSize);
            skin.shadowCastingMode = ShadowCastingMode.Off;
            skin.receiveShadows = false;
        }

        private Transform Bone(BodyBone bone) => _bones[(int)bone];

        private static BodySpec SpecOf(PlayerLook look) => new BodySpec
        {
            Shirt = look.Shirt, Secondary = look.Secondary, Pattern = (int)look.Pattern, Shorts = look.Shorts, Socks = look.Socks,
            Skin = look.Skin, Hair = look.Hair, Boots = look.Boots, Gloves = look.Gloves, HairStyle = look.HairStyle,
            FacialHair = look.FacialHair != 0 ? look.FacialHair : (look.Beard ? 1 : 0),
            Keeper = look.Keeper, Trousers = look.Trousers, LongSleeves = look.LongSleeves
        };

        /// <summary>
        /// Malla con huesos y poses de enlace (traslaciones, porque el reposo no tiene giros). Con el shader de jugadores, un
        /// solo material y el color por vértice; sin él, una submalla por color con el material plano.
        /// </summary>
        private static Mesh BuildMesh(SkinnedBodyGeometry g, out Material[] materials)
        {
            var mesh = new Mesh { name = "Jugador" };
            mesh.SetVertices(g.Vertices);
            mesh.SetColors(g.Colors);
            mesh.SetUVs(0, g.Masks);
            mesh.SetUVs(1, g.Vertices); // reposo: el shader pega pliegues y trama a la tela
            var weights = new BoneWeight[g.Vertices.Count];
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = new BoneWeight
                {
                    boneIndex0 = g.Bone0[i], weight0 = g.Weight0[i], boneIndex1 = g.Bone1[i], weight1 = 1f - g.Weight0[i]
                };
            }

            mesh.boneWeights = weights;
            var bindposes = new Matrix4x4[SkinnedBodyGeometry.BoneCount];
            for (int b = 0; b < bindposes.Length; b++)
            {
                bindposes[b] = Matrix4x4.Translate(-SkinnedBodyGeometry.BindPositions[b]);
            }

            mesh.bindposes = bindposes;
            if (Materials.PlayerShaderAvailable)
            {
                mesh.SetTriangles(g.Triangles, 0);
                materials = new[] { Materials.GetPlayer(Color.white) };
            }
            else
            {
                materials = SplitByColor(mesh, g);
            }

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material[] SplitByColor(Mesh mesh, SkinnedBodyGeometry g)
        {
            var groups = new List<List<int>>();
            var colors = new List<Color>();
            for (int t = 0; t < g.Triangles.Count; t += 3)
            {
                Color c = g.Colors[g.Triangles[t]];
                c.a = 1f;
                int k = colors.IndexOf(c);
                if (k < 0)
                {
                    k = colors.Count;
                    colors.Add(c);
                    groups.Add(new List<int>());
                }

                groups[k].Add(g.Triangles[t]);
                groups[k].Add(g.Triangles[t + 1]);
                groups[k].Add(g.Triangles[t + 2]);
            }

            mesh.subMeshCount = groups.Count;
            var materials = new Material[groups.Count];
            for (int k = 0; k < groups.Count; k++)
            {
                mesh.SetTriangles(groups[k], k);
                materials[k] = Materials.Get(colors[k]);
            }

            return materials;
        }

        private static Transform Pivot(Transform parent, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
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

        /// <summary>Coloca y anima al jugador para este fotograma (sin crear objetos).</summary>
        /// <param name="position">Posición de los pies en coordenadas de Unity.</param>
        /// <param name="headingDegrees">Orientación en grados en el plano del motor (0 = hacia +X).</param>
        /// <param name="speed">Rapidez (m/s).</param>
        /// <param name="topSpeed">Velocidad de referencia para la zancada máxima (m/s).</param>
        /// <param name="action">Acción visible.</param>
        /// <param name="lookYaw">Giro de la mirada hacia el balón (grados, positivo a la derecha).</param>
        /// <param name="diveRight">En una estirada, verdadero si se lanza hacia su derecha.</param>
        /// <param name="deltaTime">Tiempo del fotograma (s).</param>
        /// <param name="diveHeight">Altura del balón en la estirada del arquero (m).</param>
        /// <param name="holdingThrow">Espera para sacar de banda: el balón con las dos manos detrás de la cabeza.</param>
        /// <param name="holdingBall">Arquero con el balón en las manos (tras atajar o descolgar un centro).</param>
        /// <param name="lookPitch">Mirada hacia arriba (positivo) o abajo (negativo), en grados.</param>
        public void Pose(Vector3 position, float headingDegrees, float speed, float topSpeed, PlayerAction action, float lookYaw,
            bool diveRight, float deltaTime, float diveHeight = 0f, bool holdingThrow = false, bool holdingBall = false, float lookPitch = 0f)
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

            // Tronco: la acción (estirada, barrida, tropiezo) inclina todo el cuerpo desde los pies; la inclinación hacia
            // delante y la de la curva se reparten entre raíz, pelvis y columna, y la pelvis gira con la zancada.
            float bank = TurnLean(headingDegrees, speed, deltaTime);
            float stride = swing * PelvisSwingYaw;
            _body.localPosition = new Vector3(0f, lift, 0f);
            _body.localRotation = Quaternion.Euler(lean * LeanRootShare, twist * 0.5f, roll + bank * BankRootShare);
            _pelvis.localRotation = Quaternion.Euler(0f, stride, bank * BankPelvisShare);
            _spine.localRotation = Quaternion.Euler(lean * LeanSpineShare, twist * 0.25f - stride * 0.5f, bank * BankSpineShare);
            _chest.localRotation = Quaternion.Euler(lean * LeanChestShare, twist * 0.25f - swing * ChestCounterYaw, 0f);

            bool grounded = action != PlayerAction.Dive && action != PlayerAction.Slide && action != PlayerAction.Celebrate;
            PlantFeet(legL, kneeL, legR, kneeR, lean, grounded);
            _shadow.localScale = ShadowScale * (1f - Mathf.Clamp(lift, 0f, HighDiveLift) * ShadowLiftShrink);
            Look(lookYaw, lookPitch);
        }

        /// <summary>
        /// Apoyo del pie: con la pierna casi estirada, la suela queda paralela al césped (el tobillo compensa el giro de la
        /// cadera, la rodilla y el tronco); al quedar atrás, la puntera se dobla para despegar. Después, la pelvis sube o
        /// baja lo justo para que el pie más bajo toque el suelo.
        /// </summary>
        private void PlantFeet(float legL, float kneeL, float legR, float kneeR, float lean, bool grounded)
        {
            float trunk = lean * LeanRootShare;
            Foot(_ankleL, _toeL, legL, kneeL, trunk, grounded);
            Foot(_ankleR, _toeR, legR, kneeR, trunk, grounded);
            _pelvis.localPosition = new Vector3(0f, _pelvisHeight, 0f);
            if (!grounded)
            {
                return;
            }

            float scale = Mathf.Max(Root.lossyScale.y, 1e-4f);
            float lowest = Mathf.Min(_ankleL.position.y, _ankleR.position.y) - Root.position.y;
            float drop = Mathf.Clamp(_ankleHeight - lowest / scale, -MaxPelvisDrop, MaxPelvisDrop);
            _pelvis.localPosition = new Vector3(0f, _pelvisHeight + drop, 0f);
        }

        private static void Foot(Transform ankle, Transform toe, float hip, float knee, float trunk, bool grounded)
        {
            float plant = grounded ? Mathf.Clamp01(1f - knee / PlantKneeLimit) : 0f;
            float flat = -(hip + knee + trunk);
            ankle.localRotation = Quaternion.Euler(Mathf.Clamp(flat * plant, AnkleMin, AnkleMax), 0f, 0f);
            float push = grounded ? Mathf.Clamp((hip - ToePushFrom) * ToePushShare, 0f, ToeMaxBend) : 0f;
            toe.localRotation = Quaternion.Euler(-push, 0f, 0f);
        }

        /// <summary>Mirada al balón: el cuello toma una parte del giro y la cabeza el resto, cada uno con su tope.</summary>
        private void Look(float yaw, float pitch)
        {
            yaw = Mathf.Clamp(yaw, -MaxLook, MaxLook);
            float neckYaw = Mathf.Clamp(yaw * NeckLookShare, -NeckMaxYaw, NeckMaxYaw);
            float headYaw = Mathf.Clamp(yaw - neckYaw, -HeadMaxYaw, HeadMaxYaw);
            float neckPitch = Mathf.Clamp(pitch * NeckLookShare, -NeckMaxDown, NeckMaxUp);
            float headPitch = Mathf.Clamp(pitch - neckPitch, -HeadMaxDown, HeadMaxUp);
            _neck.localRotation = Quaternion.Euler(-neckPitch, neckYaw, 0f);
            _head.localRotation = Quaternion.Euler(-headPitch, headYaw, 0f);
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
