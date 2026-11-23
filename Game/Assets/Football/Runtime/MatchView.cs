using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Football.Core.MatchCore;
using Football.Core.Domain;

namespace Football.Game
{
    /// <summary>
    /// Dibuja el partido con figuras simples generadas por código (el arte definitivo se enchufará después sin tocar la
    /// lógica). Convierte las coordenadas del motor a Unity: X→X, Y→Z, Z (altura)→Y. Cámara de transmisión lateral alta.
    /// </summary>
    public sealed class MatchView
    {
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private const float LineWidth = 0.22f; // más ancha que la real (0,12 m) para que se vea de lejos sin antialiasing
        private const float LineHeight = 0.035f; // capas separadas lo suficiente para que no parpadeen de lejos
        private const float StripeHeight = 0.02f;
        private const int CircleSegments = 40;
        private const int StripeCount = 14;
        private const float GrassMargin = 2.5f;
        private static readonly Color TrackColor = new Color(0.07f, 0.3f, 0.78f);   // pista azul vivo
        private static readonly Color GrassColor = new Color(0.13f, 0.55f, 0.17f);  // verde brillante
        private static readonly Color StripeColor = new Color(0.2f, 0.67f, 0.25f);  // franjas de corte bien marcadas
        private const float BallVisualSize = 0.3f;
        private static float CameraHeight => ViewTuning.Current.Camera.Height;
        private static float CameraDistance => ViewTuning.Current.Camera.Distance;
        private static float CameraFollowX => ViewTuning.Current.Camera.FollowX;
        private static float CameraFollowZ => ViewTuning.Current.Camera.FollowZ; // sigue casi del todo a lo ancho: así se ven las dos líneas laterales
        private static float CameraEdgeMargin => ViewTuning.Current.Camera.EdgeMargin; // distancia al fondo donde la cámara deja de avanzar (cancha grande)
        private const float FullPitchWidth = 68f;
        private const float MinCameraScale = 0.45f;
        private const float WallHeight = 1.1f;
        private static float CameraFollowSpeed => ViewTuning.Current.Camera.FollowSpeed;
        private static float CameraFov => ViewTuning.Current.Camera.Fov;
        private const float ArrowHeight = 2.7f;
        private const float ArrowBob = 0.12f;
        private const float ReferenceTopSpeed = 9f;
        private const float BoardDistance = 4f;
        private const float BoardHeight = 0.9f;
        private const float GoalDepth = 1.6f;

        /// <summary>Tonos de piel (jugadores, hinchas y DT).</summary>
        public static readonly Color[] SkinTones =
        {
            new Color(0.96f, 0.80f, 0.69f), new Color(0.87f, 0.67f, 0.51f), new Color(0.76f, 0.57f, 0.42f),
            new Color(0.55f, 0.38f, 0.26f), new Color(0.36f, 0.24f, 0.17f)
        };

        /// <summary>Colores de pelo.</summary>
        public static readonly Color[] HairColors =
        {
            new Color(0.07f, 0.06f, 0.05f), new Color(0.2f, 0.12f, 0.07f), new Color(0.35f, 0.22f, 0.12f),
            new Color(0.78f, 0.63f, 0.32f), new Color(0.5f, 0.24f, 0.1f)
        };

        private readonly GameObject _root;
        private readonly Pitch _pitch;
        private const float NameHeight = 2.15f;

        private readonly PlayerModel[] _players;
        private readonly Vector3[] _heads;
        private readonly bool[] _visible;
        private readonly int _perTeam;
        private readonly float _cameraScale;
        private Camera _camera;
        private Transform _ball;
        private Transform _ballShadow;
        private Transform _controlArrow;
        private Transform _controlRing;
        private Transform _targetRing;
        private Transform _switchRing;
        private Transform _aimShaft, _aimHead;
        private Renderer _aimShaftRenderer, _aimHeadRenderer;
        private readonly Material[] _aimMaterials = new Material[3];
        private bool _setPieceView;
        private float _setPieceX, _setPieceY;
        private const float AimBaseLength = 3f;
        private const float AimChargeLength = 9f;
        private const float AimWidth = 0.35f;
        private static float SetPieceLookAhead => ViewTuning.Current.Camera.SetPieceLookAhead;
        private static float SetPieceZoom => ViewTuning.Current.Camera.SetPieceZoom;
        private TrailRenderer _trail;
        private readonly Transform[] _netBack = new Transform[2];
        private readonly Vector3[] _netRest = new Vector3[2];
        private readonly float[] _netBulge = new float[2];
        private readonly float[] _netVelocity = new float[2];
        private float _shake;
        private float _introTime = float.MaxValue, _introDuration;
        private int _focusPlayer = -1;

        private const float NetBulgeDepth = 0.7f;
        private const float NetSpring = 18f;
        private const float NetDamping = 4f;
        private const float ShakeDecay = 6f;
        private const float ShakeFrequency = 37f;
        private const float IntroHeight = 70f;
        private static float FocusZoom => ViewTuning.Current.Camera.FocusZoom;
        private Confetti _confetti;
        private readonly KitColors _homeKit, _awayKit;
        private readonly StadiumLook _look;
        private readonly int[] _shirtNumbers;
        private const float LodHysteresis = 2f;
        private GrassBurst _grass;
        /// <summary>Cantidad de peinados distintos.</summary>
        public const int HairStyles = 6;
        private const float RunUpDistance = 2.4f;   // el sacador de córner o de saque de arco toma carrera en diagonal
        private const float RunUpSeconds = 0.22f;
        private const float HoldBallHeight = 0.5f;
        private float[] _runUp;
        private float _zoom = 1f;
        private float _lookAheadX;
        private float _lookAheadZ;
        private float _ballSpeed;
        private readonly float _tickRate;

        private const float RingPulseSpeed = 5f;
        private const float RingPulseAmount = 0.12f;
        private const float RingSpinDegrees = 60f;
        private const float TrailMinSpeed = 14f;      // m/s: solo los balones rápidos dejan estela
        private const float TrailSeconds = 0.18f;
        private static float ZoomOut => ViewTuning.Current.Camera.ZoomOut;           // balón largo o en el medio: se ve más campo
        private static float ZoomIn => ViewTuning.Current.Camera.ZoomIn;            // en el último tercio: más cerca para definir
        private static float ZoomLongBallSpeed => ViewTuning.Current.Camera.ZoomLongBallSpeed;
        private static float ZoomSpeed => ViewTuning.Current.Camera.ZoomSpeed;
        private static float LookAheadSeconds => ViewTuning.Current.Camera.LookAheadSeconds;   // la cámara se adelanta hacia donde corre el poseedor
        private static float LookAheadMax => ViewTuning.Current.Camera.LookAheadMax;

        private float _cameraX;
        private float _cameraZ;
        private float _clock;
        private Vector3 _lastBall;

        /// <summary>Errores ocurridos al construir la escena (vacío si todo fue bien).</summary>
        public string BuildErrors { get; private set; } = string.Empty;

        /// <summary>Crea toda la escena del partido.</summary>
        /// <param name="pitch">Geometría del campo.</param>
        /// <param name="homeKit">Equipación del local.</param>
        /// <param name="awayKit">Equipación del visitante.</param>
        /// <param name="playersPerTeam">Jugadores por equipo (11, o 3 en la cancha cerrada).</param>
        /// <param name="tickRate">Pasos por segundo del motor (para medir la velocidad del balón entre instantáneas).</param>
        /// <param name="look">Público (ocupación), anillos de tribuna y obras del estadio; sin indicar, estadio lleno.</param>
        public MatchView(Pitch pitch, KitColors homeKit, KitColors awayKit, int playersPerTeam, int tickRate, StadiumLook? look = null, int[] shirtNumbers = null)
        {
            _look = look ?? StadiumLook.Full;
            _shirtNumbers = shirtNumbers;
            _pitch = pitch;
            _tickRate = tickRate;
            _homeKit = homeKit;
            _awayKit = awayKit;
            _ledColors = new[] { homeKit.Shirt, new Color(0.95f, 0.95f, 0.95f), awayKit.Shirt, new Color(0.1f, 0.6f, 0.95f), homeKit.Secondary };
            _perTeam = playersPerTeam;
            _players = new PlayerModel[2 * playersPerTeam];
            _heads = new Vector3[_players.Length];
            _visible = new bool[_players.Length];
            // En una cancha más chica la cámara se acerca en proporción (con un mínimo para ver todo el ancho).
            _cameraScale = Mathf.Clamp(pitch.Width / FullPitchWidth, MinCameraScale, 1f);
            _root = new GameObject("MatchView");
            Safe("cámara", () => _camera = CreateCamera());
            Safe("campo", BuildPitch);
            Safe("porterías", BuildGoals);
            Safe("estadio", BuildStadium);
            Safe("agrupado", FlushStatic);
            Safe("jugadores", () => BuildPlayers(homeKit, awayKit));
            Safe("balón", BuildBall);
            Safe("indicadores", BuildIndicators);
            Safe("terrones", () => _grass = new GrassBurst(_root.transform));
            Safe("papelitos", () => _confetti = new Confetti(_root.transform, pitch.HalfLength, pitch.HalfWidth,
                new[] { homeKit.Shirt, awayKit.Shirt, Color.white }, _meshes));
        }

        private void Safe(string step, System.Action action)
        {
            try
            {
                action();
            }
            catch (System.Exception e)
            {
                BuildErrors += step + ": " + e.GetType().Name + " - " + e.Message + "\n";
                Debug.LogException(e);
            }
        }

        private Camera CreateCamera()
        {
            // La cámara de los menús se apaga mientras dure el partido (la escena de plantilla ya se apagó al arrancar).
            if (GameRoot.MenuCamera != null)
            {
                GameRoot.MenuCamera.enabled = false;
            }

            var go = new GameObject("CamaraPartido");
            go.transform.SetParent(_root.transform, false);
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.05f, 0.09f); // estadio de noche: sin el cielo plano que distraía
            cam.fieldOfView = CameraFov;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 300f;
            cam.allowHDR = false;
            cam.allowMSAA = true; // el suavizado se activa o no desde Ajustes (GraphicsQuality)
            cam.targetDisplay = 0;
            return cam; // el AudioListener único está en la cámara de los menús
        }

        // Piezas fijas (carteles, gradas, paredes, arcos): se juntan en una malla por color al terminar de construir.
        private readonly Dictionary<Color, MeshMerge> _static = new Dictionary<Color, MeshMerge>();

        /// <summary>Anota una pieza que nunca se mueve; se dibuja junto con las demás del mismo color.</summary>
        private void StaticShape(string name, Mesh mesh, Color color, Vector3 position, Vector3 scale) =>
            StaticShape(name, mesh, color, position, scale, Quaternion.identity);

        private void StaticShape(string name, Mesh mesh, Color color, Vector3 position, Vector3 scale, Quaternion rotation)
        {
            if (!_static.TryGetValue(color, out MeshMerge batch))
            {
                batch = new MeshMerge();
                _static[color] = batch;
            }

            batch.Add(mesh, position, rotation, scale, color);
        }

        /// <summary>Crea un objeto por color con todas las piezas fijas: de unas 40 llamadas de dibujo a menos de 10.</summary>
        private void FlushStatic()
        {
            foreach (KeyValuePair<Color, MeshMerge> batch in _static)
            {
                Mesh mesh = batch.Value.Build();
                _meshes.Add(mesh);
                var go = new GameObject("Fijo");
                go.transform.SetParent(_root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = Materials.Get(batch.Key);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            _static.Clear();
        }

        private Transform Shape(string name, Mesh mesh, Color color, Vector3 position, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Materials.Get(color);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go.transform;
        }

        /// <summary>
        /// Carrera previa del sacador en córners y saques de arco: mientras espera se ve unos pasos atrás, en diagonal
        /// hacia afuera; al patear, recorre ese tramo en un instante. Solo es visual (el motor no cambia).
        /// </summary>
        private Vector2 RunUpOffset(MatchSnapshot previous, MatchSnapshot current, int i, float x, float y, float deltaTime)
        {
            if (_runUp == null || _runUp.Length != _players.Length)
            {
                _runUp = new float[_players.Length];
            }

            var outward = new Vector2(Mathf.Sign(x), Mathf.Sign(y) * 0.7f).normalized;
            bool runUpKick = current.Restart == RestartKind.Corner || current.Restart == RestartKind.GoalKick;
            if (current.Phase == MatchPhase.SetPiece && current.TakerIndex == i && runUpKick)
            {
                _runUp[i] = 1f;
                return outward * RunUpDistance;
            }

            if (_runUp[i] > 0f && current.Phase == MatchPhase.Playing)
            {
                _runUp[i] = Mathf.Max(0f, _runUp[i] - deltaTime / RunUpSeconds);
                return outward * (RunUpDistance * _runUp[i]);
            }

            _runUp[i] = 0f;
            return Vector2.zero;
        }

        private static Vector3 ToUnity(float x, float y, float height) => new Vector3(x, height, y);

        private static float ToEngineX(Vector3 unity) => unity.x;

        private static float ToEngineY(Vector3 unity) => unity.z;

        // ---- Campo y estadio ---------------------------------------------------------------------------------------

        private void BuildPitch()
        {
            float halfL = _pitch.HalfLength;
            float halfW = _pitch.HalfWidth;

            // Pista perimetral azul vivo alrededor del césped (como en los estadios de TV) y una franja de césped hasta las
            // líneas: el límite del campo se lee al instante aunque la cámara esté lejos.
            var apron = new MeshBuilder();
            apron.Quad(-halfL - 12f, -halfW - 10f, halfL + 12f, halfW + 10f, 0f);
            AddMesh("Pista", apron, TrackColor);

            var grass = new MeshBuilder();
            grass.Quad(-halfL - GrassMargin, -halfW - GrassMargin, halfL + GrassMargin, halfW + GrassMargin, StripeHeight * 0.5f);
            AddMesh("Cesped", grass, GrassColor);

            var stripes = new MeshBuilder();
            float band = _pitch.Length / StripeCount;
            for (int i = 0; i < StripeCount; i += 2)
            {
                stripes.Quad(-halfL + i * band, -halfW, -halfL + (i + 1) * band, halfW, StripeHeight);
            }

            AddMesh("Franjas", stripes, StripeColor);

            var lines = new MeshBuilder();
            Rectangle(lines, -halfL, -halfW, halfL, halfW);
            lines.Stroke(new Vector2(0f, -halfW), new Vector2(0f, halfW), LineWidth, LineHeight);
            Circle(lines, Vector2.zero, _pitch.Config.CenterCircleRadius);
            for (int end = -1; end <= 1; end += 2)
            {
                float goalLine = end * halfL;
                float areaX = end * (halfL - _pitch.Config.PenaltyAreaDepth);
                float boxX = end * (halfL - _pitch.Config.GoalAreaDepth);
                Rectangle(lines, Mathf.Min(goalLine, areaX), -_pitch.Config.PenaltyAreaHalfWidth,
                    Mathf.Max(goalLine, areaX), _pitch.Config.PenaltyAreaHalfWidth);
                if (_pitch.Config.GoalAreaDepth > 0f)
                {
                    float boxY = _pitch.Config.GoalAreaHalfWidth;
                    Rectangle(lines, Mathf.Min(goalLine, boxX), -boxY, Mathf.Max(goalLine, boxX), boxY);
                }
                float spotX = end * (halfL - _pitch.Config.PenaltySpotDistance);
                lines.Quad(spotX - 0.15f, -0.15f, spotX + 0.15f, 0.15f, LineHeight);
            }

            lines.Quad(-0.15f, -0.15f, 0.15f, 0.15f, LineHeight);
            AddMesh("Lineas", lines, new Color(0.95f, 0.97f, 0.95f));
        }

        private void BuildStadium()
        {
            if (_pitch.Config.Walls)
            {
                BuildWalls();
            }

            bool walls = _pitch.Config.Walls;
            float boardDistance = walls ? 0f : BoardDistance;
            float halfL = _pitch.HalfLength + boardDistance;
            float halfW = _pitch.HalfWidth + boardDistance;
            if (!walls)
            {
                BuildLedBoards(halfL, halfW); // en la cancha cerrada las paredes hacen de carteles
            }

            // Gradas escalonadas detrás de los carteles (tonos variados para sugerir público).
            Color[] tiers = { new Color(0.32f, 0.12f, 0.14f), new Color(0.14f, 0.2f, 0.38f), new Color(0.3f, 0.3f, 0.34f) }; // tonos de público
            int built = Mathf.Clamp(_look.Tiers, 1, tiers.Length);
            for (int t = 0; t < built; t++)
            {
                float offset = TierOffset(t);
                float height = 2.5f + t * 4f;
                StaticShape("Grada", MeshKit.Box, tiers[t], new Vector3(0f, height * 0.5f, halfW + offset), new Vector3(2f * halfL + 30f, height, TierDepth));
                StaticShape("Grada", MeshKit.Box, tiers[t], new Vector3(-halfL - offset, height * 0.5f, 0f), new Vector3(TierDepth, height, 2f * halfW + 20f));
                StaticShape("Grada", MeshKit.Box, tiers[t], new Vector3(halfL + offset, height * 0.5f, 0f), new Vector3(TierDepth, height, 2f * halfW + 20f));
            }

            if (!walls)
            {
                BuildCrowd(halfL, halfW, built);
                BuildBenchesAndPhotographers(_pitch.HalfLength, _pitch.HalfWidth);
                if (_look.StandsWork)
                {
                    BuildScaffolding(halfL, halfW, built);
                }
            }
        }

        private const float ScaffoldPoleSpacing = 4f;
        private const float ScaffoldLevelHeight = 2.2f;
        private static readonly Color ScaffoldColor = new Color(0.85f, 0.62f, 0.12f);

        /// <summary>
        /// Ampliación de tribunas en obra: andamios (postes y travesaños amarillos) donde va el anillo nuevo, detrás de la
        /// tribuna de enfrente. La misma obra se ve en la vista isométrica de la pantalla del estadio.
        /// </summary>
        private void BuildScaffolding(float halfL, float halfW, int builtTiers)
        {
            float z = halfW + TierOffset(builtTiers);
            float height = 2.5f + builtTiers * 4f;
            float length = 2f * halfL + 30f;
            int poles = (int)(length / ScaffoldPoleSpacing);
            for (int k = 0; k <= poles; k++)
            {
                float x = -length * 0.5f + k * ScaffoldPoleSpacing;
                StaticShape("Andamio", MeshKit.Box, ScaffoldColor, new Vector3(x, height * 0.5f, z - TierDepth * 0.4f), new Vector3(0.15f, height, 0.15f));
                StaticShape("Andamio", MeshKit.Box, ScaffoldColor, new Vector3(x, height * 0.5f, z + TierDepth * 0.4f), new Vector3(0.15f, height, 0.15f));
            }

            for (float y = ScaffoldLevelHeight; y < height; y += ScaffoldLevelHeight)
            {
                StaticShape("Andamio", MeshKit.Box, ScaffoldColor, new Vector3(0f, y, z - TierDepth * 0.4f), new Vector3(length, 0.12f, 0.12f));
                StaticShape("Andamio", MeshKit.Box, ScaffoldColor, new Vector3(0f, y, z + TierDepth * 0.4f), new Vector3(length, 0.12f, 0.12f));
            }
        }

        // ---- Carteles LED ---------------------------------------------------------------------------------------------

        private const int LedSegments = 8;
        private const float GoalGapMargin = 2.5f;
        private const float LedCycleSeconds = 5f;
        private Material _ledMaterial;
        private float _ledClock;
        private Color[] _ledColors;

        /// <summary>
        /// Carteles LED alrededor del campo (laterales y detrás de los arcos): una sola malla con un material propio cuyo color
        /// va cambiando (como las pantallas publicitarias). Se cambia el color del material, no un MaterialPropertyBlock (D-15).
        /// </summary>
        private void BuildLedBoards(float halfL, float halfW)
        {
            var merge = new MeshMerge();
            Color frame = new Color(0.08f, 0.08f, 0.1f);
            for (int i = 0; i < LedSegments; i++)
            {
                float x0 = -halfL + i * (2f * halfL / LedSegments);
                float length = 2f * halfL / LedSegments; // tramos pegados: una sola cinta continua
                for (int side = -1; side <= 1; side += 2)
                {
                    merge.Add(MeshKit.Box, new Vector3(x0 + length * 0.5f, BoardHeight * 0.5f, side * halfW), Quaternion.identity,
                        new Vector3(length, BoardHeight, 0.15f), Color.white);
                }
            }

            // Detrás de cada arco, también continuos de banda a banda (solo se abre el hueco de la red).
            float gap = _pitch.GoalHalfWidth + GoalGapMargin;
            for (int end = -1; end <= 1; end += 2)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float z = side * (gap + halfW) * 0.5f;
                    merge.Add(MeshKit.Box, new Vector3(end * (halfL + 1f), BoardHeight * 0.5f, z), Quaternion.identity,
                        new Vector3(0.15f, BoardHeight, halfW - gap), Color.white);
                }
            }

            Mesh mesh = merge.Build();
            _meshes.Add(mesh);
            var go = new GameObject("CartelesLED");
            go.transform.SetParent(_root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            _ledMaterial = new Material(Materials.Get(Color.white)); // material propio: su color se anima
            renderer.sharedMaterial = _ledMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            StaticShape("MarcoLED", MeshKit.Box, frame, new Vector3(0f, 0.04f, halfW + 0.1f), new Vector3(2f * halfL, 0.08f, 0.05f));
        }

        private void UpdateLedBoards(float deltaTime)
        {
            if (_ledMaterial == null || _ledColors == null)
            {
                return;
            }

            _ledClock += deltaTime;
            float position = _ledClock / LedCycleSeconds;
            int index = (int)position;
            float blend = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((position - index) * 4f - 3f)); // fundido al final de cada aviso
            Color from = _ledColors[index % _ledColors.Length];
            Color to = _ledColors[(index + 1) % _ledColors.Length];
            _ledMaterial.color = Color.Lerp(from, to, blend);
        }

        // ---- Público ----------------------------------------------------------------------------------------------------

        private static readonly float[] CrowdSpacing = { 2.2f, 1.4f, 0.95f }; // por perfil gráfico: bajo, medio, alto
        private const int CrowdRowsPerTier = 2;
        private const float CheerHeight = 0.35f;
        private const float CheerFrequency = 11f;
        private Transform _crowd;
        private float _cheerTimer;

        /// <summary>
        /// Público en las tribunas: figuras simples (torso y cabeza) con colores de los dos equipos y ropa variada,
        /// agrupadas en una malla por tribuna. La densidad depende del perfil gráfico y la cantidad de gente, de la ocupación
        /// del partido (<see cref="StadiumLook.Attendance"/>): cada lugar se llena o no según un hash fijo, así el estadio
        /// semivacío queda salpicado de huecos y no siempre vacío del mismo lado.
        /// </summary>
        private void BuildCrowd(float halfL, float halfW, int tiers)
        {
            _crowd = new GameObject("Publico").transform;
            _crowd.SetParent(_root.transform, false);
            float spacing = CrowdSpacing[Mathf.Clamp(GraphicsQuality.Level, 0, CrowdSpacing.Length - 1)];
            // Público multicolor: mayoría con los colores del local, algo del visitante y ropa de todos los días.
            Color[] palette =
            {
                _homeKit.Shirt, _homeKit.Shirt, _homeKit.Secondary, _homeKit.Shirt, _awayKit.Shirt, _awayKit.Secondary,
                new Color(0.92f, 0.92f, 0.92f), new Color(0.15f, 0.15f, 0.18f), new Color(0.55f, 0.4f, 0.3f), new Color(0.85f, 0.2f, 0.2f),
                new Color(0.2f, 0.45f, 0.85f), new Color(0.95f, 0.8f, 0.2f), new Color(0.3f, 0.65f, 0.35f), new Color(0.9f, 0.5f, 0.2f)
            };
            int seed = 0;
            for (int t = 0; t < tiers; t++)
            {
                float offset = TierOffset(t);
                float top = 2.5f + t * 4f;
                // Tribuna lateral (enfrente de la cámara) y las dos cabeceras.
                CrowdBlock(new Vector3(0f, top, halfW + offset), 2f * halfL + 26f, true, spacing, palette, ref seed, _look.Attendance);
                CrowdBlock(new Vector3(-halfL - offset, top, 0f), 2f * halfW + 16f, false, spacing, palette, ref seed, _look.Attendance);
                CrowdBlock(new Vector3(halfL + offset, top, 0f), 2f * halfW + 16f, false, spacing, palette, ref seed, _look.Attendance);
            }
        }

        private const float TierDepth = 5.5f;

        /// <summary>Distancia de cada anillo de tribuna al borde del campo (tribunas cercanas, al estilo de las canchas argentinas).</summary>
        private static float TierOffset(int tier) => 3.5f + tier * 5.5f;

        /// <summary>
        /// Lateral de abajo (el de la cámara): bancos de suplentes techados a los dos lados de la mitad de la cancha, con
        /// suplentes sentados con la camiseta de su equipo, y fotógrafos agachados junto a cada línea de fondo.
        /// </summary>
        private void BuildBenchesAndPhotographers(float halfL, float halfW)
        {
            float z = -(halfW + BoardDistance + 1.6f);
            Color roof = new Color(0.08f, 0.16f, 0.4f), back = new Color(0.12f, 0.13f, 0.16f), seat = new Color(0.75f, 0.75f, 0.78f);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * BenchOffset;
                StaticShape("Banco", MeshKit.Box, back, new Vector3(x, 1f, z - 0.9f), new Vector3(BenchLength, 2f, 0.2f));
                StaticShape("Banco", MeshKit.Box, roof, new Vector3(x, 2.05f, z - 0.2f), new Vector3(BenchLength + 0.4f, 0.12f, 1.8f));
                StaticShape("Banco", MeshKit.Box, back, new Vector3(x - side * BenchLength * 0.5f, 1f, z - 0.2f), new Vector3(0.15f, 2f, 1.6f));
                StaticShape("Banco", MeshKit.Box, seat, new Vector3(x, 0.45f, z - 0.55f), new Vector3(BenchLength - 0.4f, 0.12f, 0.5f));
                Color shirt = side < 0 ? _homeKit.Shirt : _awayKit.Shirt;
                for (int k = 0; k < BenchPlayers; k++)
                {
                    float px = x - BenchLength * 0.4f + k * (BenchLength * 0.8f / (BenchPlayers - 1));
                    StaticShape("Suplente", MeshKit.Capsule, shirt, new Vector3(px, 0.85f, z - 0.55f), new Vector3(0.42f, 0.62f, 0.32f));
                    StaticShape("Suplente", MeshKit.Sphere, SkinTones[(k * 3 + side + 4) % SkinTones.Length], new Vector3(px, 1.3f, z - 0.5f),
                        new Vector3(0.22f, 0.26f, 0.24f));
                }
            }

            Color vest = new Color(0.95f, 0.75f, 0.1f), camera = new Color(0.05f, 0.05f, 0.06f);
            for (int end = -1; end <= 1; end += 2)
            {
                for (int k = 0; k < PhotographersPerEnd; k++)
                {
                    float pz = -halfW * (0.25f + 0.2f * k);
                    float px = end * (halfL + 2.6f);
                    StaticShape("Fotografo", MeshKit.Capsule, vest, new Vector3(px, 0.45f, pz), new Vector3(0.45f, 0.7f, 0.4f));
                    StaticShape("Fotografo", MeshKit.Sphere, SkinTones[(k + 2) % SkinTones.Length], new Vector3(px, 0.95f, pz), new Vector3(0.22f, 0.25f, 0.22f));
                    StaticShape("Camara", MeshKit.Box, camera, new Vector3(px - end * 0.3f, 0.95f, pz), new Vector3(0.3f, 0.18f, 0.18f));
                    StaticShape("Teleobjetivo", MeshKit.Cylinder, camera, new Vector3(px - end * 0.55f, 0.95f, pz), new Vector3(0.12f, 0.35f, 0.12f),
                        Quaternion.Euler(0f, 0f, 90f));
                }
            }
        }

        private const float BenchOffset = 9f;
        private const float BenchLength = 6f;
        private const int BenchPlayers = 5;
        private const int PhotographersPerEnd = 3;

        private const uint SeatHash = 2654435761u;
        private const float SeatHashRange = 16777216f; // 2^24: los 24 bits altos del hash

        private void CrowdBlock(Vector3 center, float length, bool alongX, float spacing, Color[] palette, ref int seed, float fill)
        {
            bool merged = Materials.PlayerShaderAvailable;
            var merge = new MeshMerge();
            int count = (int)(length / spacing);
            for (int row = 0; row < CrowdRowsPerTier; row++)
            {
                float depth = (row - 0.5f) * 2.4f;
                for (int k = 0; k < count; k++)
                {
                    seed++;
                    if (((uint)seed * SeatHash >> 8) / SeatHashRange >= fill)
                    {
                        continue; // lugar vacío
                    }

                    float along = -length * 0.5f + (k + 0.5f) * spacing + ((seed * 7) % 5 - 2) * 0.08f;
                    Vector3 p = alongX ? new Vector3(along, 0f, depth) : new Vector3(depth, 0f, along);
                    Color shirt = palette[(seed * 13 + row * 5) % palette.Length];
                    Color skin = SkinTones[(seed * 3) % SkinTones.Length];
                    Vector3 body = center + p + new Vector3(0f, 0.38f, 0f);
                    Vector3 head = center + p + new Vector3(0f, 0.86f, 0f);
                    if (merged)
                    {
                        merge.Add(MeshKit.Box, body, Quaternion.identity, new Vector3(0.45f, 0.62f, 0.32f), shirt);
                        merge.Add(MeshKit.Box, head, Quaternion.identity, new Vector3(0.24f, 0.26f, 0.24f), skin);
                    }
                    else
                    {
                        StaticShape("Hincha", MeshKit.Box, shirt, body, new Vector3(0.45f, 0.62f, 0.32f));
                    }
                }
            }

            if (!merged)
            {
                return;
            }

            Mesh mesh = merge.Build();
            _meshes.Add(mesh);
            var go = new GameObject("Hinchas");
            go.transform.SetParent(_crowd, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Materials.GetCrowd(Color.white); // colores por vértice, sin sombra proyectada
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>La hinchada salta (por ejemplo, tras un gol).</summary>
        /// <param name="seconds">Duración.</param>
        public void Cheer(float seconds)
        {
            _cheerTimer = seconds;
        }

        private void UpdateCrowd(float deltaTime)
        {
            if (_crowd == null)
            {
                return;
            }

            _cheerTimer = Mathf.Max(0f, _cheerTimer - deltaTime);
            float jump = _cheerTimer > 0f ? Mathf.Abs(Mathf.Sin(_cheerTimer * CheerFrequency)) * CheerHeight : 0f;
            _crowd.localPosition = new Vector3(0f, jump, 0f);
        }

        /// <summary>Cancha cerrada: tableros en el borde del campo (en el fondo, a los lados de la portería).</summary>
        private void BuildWalls()
        {
            float halfL = _pitch.HalfLength;
            float halfW = _pitch.HalfWidth;
            const float thickness = 0.2f;
            Color wall = new Color(0.85f, 0.87f, 0.9f);
            Color trim = new Color(0.1f, 0.25f, 0.7f);
            for (int side = -1; side <= 1; side += 2)
            {
                float z = side * (halfW + thickness * 0.5f);
                StaticShape("Pared", MeshKit.Box, wall, new Vector3(0f, WallHeight * 0.5f, z), new Vector3(2f * halfL + 2f * thickness, WallHeight, thickness));
                StaticShape("Franja", MeshKit.Box, trim, new Vector3(0f, WallHeight + 0.03f, z), new Vector3(2f * halfL + 2f * thickness, 0.06f, thickness + 0.02f));
            }

            float sideLength = halfW - _pitch.GoalHalfWidth;
            for (int end = -1; end <= 1; end += 2)
            {
                float x = end * (halfL + thickness * 0.5f);
                for (int side = -1; side <= 1; side += 2)
                {
                    float z = side * (_pitch.GoalHalfWidth + sideLength * 0.5f);
                    StaticShape("Pared", MeshKit.Box, wall, new Vector3(x, WallHeight * 0.5f, z), new Vector3(thickness, WallHeight, sideLength));
                    StaticShape("Franja", MeshKit.Box, trim, new Vector3(x, WallHeight + 0.03f, z), new Vector3(thickness + 0.02f, 0.06f, sideLength));
                }
            }
        }

        private static void Rectangle(MeshBuilder b, float x0, float y0, float x1, float y1)
        {
            b.Stroke(new Vector2(x0, y0), new Vector2(x1, y0), LineWidth, LineHeight);
            b.Stroke(new Vector2(x1, y0), new Vector2(x1, y1), LineWidth, LineHeight);
            b.Stroke(new Vector2(x1, y1), new Vector2(x0, y1), LineWidth, LineHeight);
            b.Stroke(new Vector2(x0, y1), new Vector2(x0, y0), LineWidth, LineHeight);
        }

        private static void Circle(MeshBuilder b, Vector2 center, float radius)
        {
            Vector2 previous = center + new Vector2(radius, 0f);
            for (int i = 1; i <= CircleSegments; i++)
            {
                float a = i * Mathf.PI * 2f / CircleSegments;
                Vector2 next = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                b.Stroke(previous, next, LineWidth, LineHeight);
                previous = next;
            }
        }

        private void AddMesh(string name, MeshBuilder builder, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            Mesh mesh = builder.ToMesh();
            _meshes.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Materials.Get(color);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void BuildGoals()
        {
            const float post = 0.12f;
            float halfGoal = _pitch.GoalHalfWidth;
            float height = _pitch.GoalHeight;
            Color white = Color.white;
            Color net = new Color(0.75f, 0.78f, 0.8f);
            for (int end = -1; end <= 1; end += 2)
            {
                float x = end * _pitch.HalfLength;
                float back = x + end * GoalDepth;
                // Postes y travesaño cilíndricos (con brillo por la luz de borde del material).
                StaticShape("Poste", MeshKit.Cylinder, white, new Vector3(x, height * 0.5f, -halfGoal), new Vector3(post, height, post));
                StaticShape("Poste", MeshKit.Cylinder, white, new Vector3(x, height * 0.5f, halfGoal), new Vector3(post, height, post));
                StaticShape("Larguero", MeshKit.Cylinder, white, new Vector3(x, height, 0f), new Vector3(post, halfGoal * 2f + post, post),
                    Quaternion.Euler(90f, 0f, 0f));
                // Armazón de la red.
                StaticShape("Red", MeshKit.Box, net, new Vector3(back, height * 0.4f, -halfGoal), new Vector3(0.05f, height * 0.8f, 0.05f));
                StaticShape("Red", MeshKit.Box, net, new Vector3(back, height * 0.4f, halfGoal), new Vector3(0.05f, height * 0.8f, 0.05f));
                StaticShape("Red", MeshKit.Box, net, new Vector3((x + back) * 0.5f, height, -halfGoal), new Vector3(GoalDepth, 0.04f, 0.04f));
                StaticShape("Red", MeshKit.Box, net, new Vector3((x + back) * 0.5f, height, halfGoal), new Vector3(GoalDepth, 0.04f, 0.04f));
                StaticShape("Red", MeshKit.Box, net, new Vector3((x + back) * 0.5f, height * 0.995f, 0f), new Vector3(GoalDepth, 0.02f, halfGoal * 2f));
                Transform backNet = Shape("Red", MeshKit.Box, net, new Vector3(back, height * 0.4f, 0f), new Vector3(0.02f, height * 0.8f, halfGoal * 2f));
                int k = end < 0 ? 0 : 1;
                _netBack[k] = backNet;
                _netRest[k] = backNet.localPosition;
            }
        }

        // ---- Jugadores, balón e indicadores -----------------------------------------------------------------------------

        private const int LargePitchTeamSize = 7;          // desde este tamaño de equipo (11 contra 11) se agrandan los jugadores
        private const float LargePitchPlayerScale = 1.22f;
        private const float TeamMarkScale = 1.05f;
        private const float TeamMarkHeight = 0.03f;
        private static readonly Color HomeMarkColor = new Color(0.1f, 0.85f, 0.95f);
        private static readonly Color AwayMarkColor = new Color(0.95f, 0.3f, 0.2f);
        private const float KeeperBuild = 1.05f;
        private const float MinBuild = 0.94f;
        private const float BuildRange = 0.1f;
        private const int BuildStep = 7;
        private const int BuildSteps = 11;
        private const float DirtFullMinute = 90f;
        private const float MaxDirt = 0.85f;

        private void BuildPlayers(KitColors homeKit, KitColors awayKit)
        {
            Materials.SetDirt(0f); // partido nuevo, ropa limpia
            Materials.SetSweat(0f);
            for (int i = 0; i < _players.Length; i++)
            {
                bool home = i < _perTeam;
                bool keeper = i % _perTeam == 0;
                KitColors kit = home ? homeKit : awayKit;
                if (keeper)
                {
                    // Los porteros visten distinto que los jugadores de campo de ambos equipos.
                    Color gk = home ? new Color(0.55f, 0.85f, 0.15f) : new Color(0.95f, 0.55f, 0.1f);
                    kit = new KitColors { Shirt = gk, Secondary = gk, Shorts = new Color(0.1f, 0.1f, 0.1f), Socks = gk, Pattern = KitPattern.Solid };
                }

                var look = new PlayerLook
                {
                    Shirt = kit.Shirt,
                    Secondary = kit.Secondary,
                    Pattern = kit.Pattern,
                    Shorts = kit.Shorts,
                    Socks = kit.Socks,
                    Skin = SkinTones[(i * 7 + 3) % SkinTones.Length],
                    Hair = HairColors[(i * 5 + 1) % HairColors.Length],
                    Boots = new Color(0.08f, 0.08f, 0.08f),
                    // Aspecto determinista: cada jugador tiene siempre el mismo peinado y barba.
                    HairStyle = (i * 13 + 5) % HairStyles,
                    FacialHair = FacialHairFor(i),
                    Number = _shirtNumbers != null && i < _shirtNumbers.Length ? _shirtNumbers[i] : 0,
                    Crest = (int)kit.Pattern % PlayerAtlasData.CrestCount,
                    Keeper = keeper,
                    Gloves = home ? new Color(0.95f, 0.9f, 0.2f) : new Color(0.25f, 0.8f, 0.95f)
                };
                // Contextura: los arqueros, algo más altos; el resto varía un poco (determinista, sin azar).
                float build = keeper ? KeeperBuild : MinBuild + (i * BuildStep % BuildSteps) * BuildRange / (BuildSteps - 1);
                // En la cancha grande los jugadores se ven chicos: se agrandan un poco y cada uno lleva una marca de su equipo en el piso
                // (celeste el tuyo, rojo el rival), para distinguirlos aunque las camisetas se parezcan.
                if (_perTeam >= LargePitchTeamSize)
                {
                    build *= LargePitchPlayerScale;
                }

                _players[i] = new PlayerModel(_root.transform, "Jugador" + i, look, i * 0.7f, build, _meshes);
                Transform mark = Shape("MarcaEquipo", MeshKit.Disc, home ? HomeMarkColor : AwayMarkColor, Vector3.zero, new Vector3(TeamMarkScale, 1f, TeamMarkScale));
                mark.SetParent(_players[i].Root, false);
                mark.localPosition = new Vector3(0f, TeamMarkHeight, 0f);
            }
        }

        private const uint FacialHairHash = 2654435761u;
        private const float EyeHeight = 1.65f;      // altura de los ojos para mirar el balón arriba o abajo
        private const float MinLookDistance = 0.5f;
        private const int FacialHairBuckets = 10, BeardBuckets = 2, GoateeBuckets = 2; // de cada 10: 2 con barba y 2 con candado

        /// <summary>Barba o candado de un jugador, elegido por hash (siempre el mismo para el mismo lugar).</summary>
        private static int FacialHairFor(int index)
        {
            int bucket = (int)(((uint)(index + 1) * FacialHairHash >> 16) % FacialHairBuckets);
            return bucket < BeardBuckets ? 1 : bucket < BeardBuckets + GoateeBuckets ? 2 : 0;
        }

        /// <summary>LOD de dos niveles: pasados 24 m de la cámara activa (con 2 m de histéresis) el jugador se dibuja con la malla simplificada.</summary>
        private void UpdateDetail(PlayerModel model, Vector3 position)
        {
            if (_camera == null)
            {
                return;
            }

            float far = PlayerModel.FarDistance;
            float limit = model.IsFar ? far - LodHysteresis : far;
            model.SetFar((position - _camera.transform.position).sqrMagnitude > limit * limit);
        }

        private void BuildBall()
        {
            _ball = Shape("Balon", MeshKit.Sphere, Color.white, Vector3.zero, new Vector3(BallVisualSize, BallVisualSize, BallVisualSize));
            // Unos parches oscuros para que se vea girar.
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                var patch = Shape("Parche", MeshKit.Sphere, new Color(0.1f, 0.1f, 0.12f), Vector3.zero, new Vector3(0.35f, 0.35f, 0.35f));
                patch.SetParent(_ball, false);
                patch.localPosition = new Vector3(Mathf.Cos(a) * 0.36f, (i % 2 == 0 ? 0.15f : -0.15f), Mathf.Sin(a) * 0.36f);
            }

            _ballShadow = Shape("SombraBalon", MeshKit.Disc, new Color(0.03f, 0.14f, 0.05f), Vector3.zero, new Vector3(0.5f, 1f, 0.5f));

            // Estela blanca sutil cuando el balón va muy rápido (tiros y pases fuertes).
            _trail = _ball.gameObject.AddComponent<TrailRenderer>();
            _trail.time = TrailSeconds;
            _trail.startWidth = BallVisualSize * 0.8f;
            _trail.endWidth = 0f;
            _trail.sharedMaterial = Materials.Get(new Color(0.95f, 0.95f, 0.95f));
            _trail.shadowCastingMode = ShadowCastingMode.Off;
            _trail.receiveShadows = false;
            _trail.emitting = false;
        }

        private void BuildIndicators()
        {
            _controlArrow = Shape("FlechaControl", MeshKit.Arrow, new Color(1f, 0.85f, 0.05f), Vector3.zero, new Vector3(0.55f, 0.6f, 0.55f));
            _controlRing = Shape("AroControl", MeshKit.Disc, new Color(1f, 0.85f, 0.05f), Vector3.zero, new Vector3(1.6f, 1f, 1.6f));
            _targetRing = Shape("AroDestino", MeshKit.Disc, Color.white, Vector3.zero, new Vector3(1.4f, 1f, 1.4f));
            // Aro tenue: el jugador que tomarías al pulsar CAMBIAR.
            _switchRing = Shape("AroCambio", MeshKit.Disc, new Color(0.55f, 0.5f, 0.2f), Vector3.zero, new Vector3(1.3f, 1f, 1.3f));

            // Flecha de apuntado en el césped para los saques (verde → amarillo → rojo según la carga).
            _aimMaterials[0] = Materials.Get(new Color(0.3f, 0.9f, 0.35f));
            _aimMaterials[1] = Materials.Get(new Color(1f, 0.85f, 0.2f));
            _aimMaterials[2] = Materials.Get(new Color(0.95f, 0.3f, 0.2f));
            _aimShaft = Shape("FlechaSaque", MeshKit.Box, Color.white, Vector3.zero, Vector3.one);
            _aimHead = Shape("PuntaSaque", MeshKit.Box, Color.white, Vector3.zero, Vector3.one);
            _aimShaftRenderer = _aimShaft.GetComponent<Renderer>();
            _aimHeadRenderer = _aimHead.GetComponent<Renderer>();
            _aimShaft.gameObject.SetActive(false);
            _aimHead.gameObject.SetActive(false);
        }

        // ---- Dibujo por fotograma ---------------------------------------------------------------------------------

        /// <summary>Dibuja el estado interpolado entre dos instantáneas.</summary>
        /// <param name="previous">Instantánea anterior.</param>
        /// <param name="current">Instantánea actual.</param>
        /// <param name="alpha">Fracción entre ambas (0..1).</param>
        /// <param name="controlled">Índice del jugador controlado, o -1.</param>
        /// <param name="passTarget">Índice del receptor previsto del pase, o -1.</param>
        /// <param name="deltaTime">Tiempo del fotograma.</param>
        /// <param name="switchTarget">Jugador que tomarías con CAMBIAR, o -1.</param>
        public void Render(MatchSnapshot previous, MatchSnapshot current, float alpha, int controlled, int passTarget, float deltaTime,
            int switchTarget = -1)
        {
            _clock += deltaTime;
            _confetti?.Update(deltaTime);
            _grass?.Update(deltaTime);
            UpdateLedBoards(deltaTime);
            UpdateCrowd(deltaTime);
            AnimateNets(deltaTime);
            float bx = Mathf.Lerp(previous.BallX, current.BallX, alpha);
            float by = Mathf.Lerp(previous.BallY, current.BallY, alpha);
            float bz = Mathf.Lerp(previous.BallZ, current.BallZ, alpha);

            // La ropa se va ensuciando a lo largo del partido.
            Materials.SetDirt(current.Minute / DirtFullMinute * MaxDirt);
            Materials.SetSweat(current.Minute / DirtFullMinute);
            for (int i = 0; i < _players.Length; i++)
            {
                PlayerModel model = _players[i];
                if (model == null)
                {
                    continue;
                }

                bool visible = !current.SentOff[i];
                model.SetVisible(visible);
                _visible[i] = visible;
                if (!visible)
                {
                    continue;
                }

                float x = Mathf.Lerp(previous.PlayerX[i], current.PlayerX[i], alpha);
                float y = Mathf.Lerp(previous.PlayerY[i], current.PlayerY[i], alpha);
                float heading = Mathf.LerpAngle(previous.PlayerHeading[i] * Mathf.Rad2Deg, current.PlayerHeading[i] * Mathf.Rad2Deg, alpha);
                float speed = Mathf.Lerp(previous.PlayerSpeed[i], current.PlayerSpeed[i], alpha);

                // Ángulo hacia el balón relativo a la orientación (positivo = a la izquierda en el plano del motor).
                float toBall = Mathf.Atan2(by - y, bx - x) * Mathf.Rad2Deg;
                float relative = Mathf.DeltaAngle(heading, toBall);
                PlayerAction action = current.PlayerAction[i];
                Vector2 runUp = RunUpOffset(previous, current, i, x, y, deltaTime);
                if (runUp.sqrMagnitude > 0f)
                {
                    x += runUp.x;
                    y += runUp.y;
                    speed = Mathf.Max(speed, RunUpDistance / RunUpSeconds * 0.5f); // zancada de carrera
                }

                if (action == PlayerAction.Slide && previous.PlayerAction[i] != PlayerAction.Slide && _grass != null)
                {
                    float rad = heading * Mathf.Deg2Rad;
                    _grass.Emit(ToUnity(x, y, 0.05f), new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)));
                }

                bool holdingThrow = current.Phase == MatchPhase.SetPiece && current.TakerIndex == i && current.Restart == RestartKind.ThrowIn;
                bool holdingBall = current.OwnerIndex == i && i % _perTeam == 0 && current.BallZ > HoldBallHeight;
                float ballDistance = Mathf.Max(MinLookDistance, Mathf.Sqrt((bx - x) * (bx - x) + (by - y) * (by - y)));
                float lookPitch = Mathf.Atan2(bz - EyeHeight, ballDistance) * Mathf.Rad2Deg;
                UpdateDetail(model, ToUnity(x, y, 0f));
                model.Pose(ToUnity(x, y, 0f), heading, speed, ReferenceTopSpeed, action, -relative, relative < 0f, deltaTime,
                    current.DiveHeight[i], holdingThrow, holdingBall, lookPitch);
                _heads[i] = ToUnity(x, y, NameHeight);
            }

            if (_ball != null)
            {
                Vector3 ballPosition = ToUnity(bx, by, bz + BallVisualSize * 0.5f);
                RollBall(ballPosition);
                _ball.localPosition = ballPosition;
                float shadow = 0.5f * Mathf.Clamp(1f - bz * 0.08f, 0.3f, 1f);
                _ballShadow.localScale = new Vector3(shadow, 1f, shadow);
                _ballShadow.localPosition = ToUnity(bx, by, 0.03f);
            }

            if (_controlArrow != null)
            {
                PlaceIndicator(_controlArrow, previous, current, alpha, controlled, ArrowHeight + Mathf.Sin(_clock * 5f) * ArrowBob);
                PlaceIndicator(_controlRing, previous, current, alpha, controlled, 0.035f);
                PlaceIndicator(_targetRing, previous, current, alpha, passTarget, 0.04f);
                PlaceIndicator(_switchRing, previous, current, alpha, switchTarget != controlled ? switchTarget : -1, 0.03f);
                // El aro del jugador controlado late y gira despacio para encontrarlo de un vistazo.
                float pulse = 1.6f * (1f + RingPulseAmount * Mathf.Sin(_clock * RingPulseSpeed));
                _controlRing.localScale = new Vector3(pulse, 1f, pulse);
                _controlRing.localRotation = Quaternion.Euler(0f, _clock * RingSpinDegrees, 0f);
            }

            if (_trail != null)
            {
                float vx = (current.BallX - previous.BallX) * _tickRate;
                float vy = (current.BallY - previous.BallY) * _tickRate;
                _ballSpeed = Mathf.Sqrt(vx * vx + vy * vy);
                _trail.emitting = _ballSpeed > TrailMinSpeed;
            }

            if (_camera != null)
            {
                _cameraTarget = controlled >= 0 && controlled < _players.Length && _visible[controlled] ? controlled : -1;
                if (_cameraTarget >= 0)
                {
                    _cameraTargetHeading = Mathf.LerpAngle(previous.PlayerHeading[controlled] * Mathf.Rad2Deg,
                        current.PlayerHeading[controlled] * Mathf.Rad2Deg, alpha);
                    _cameraTargetSpeed = current.PlayerSpeed[controlled];
                }

                UpdateLookAhead(current, deltaTime);
                UpdateCamera(bx, by, deltaTime);
            }
        }

        private void RollBall(Vector3 position)
        {
            Vector3 moved = position - _lastBall;
            _lastBall = position;
            moved.y = 0f;
            float distance = moved.magnitude;
            if (distance < 0.001f || distance > 5f)
            {
                return;
            }

            Vector3 axis = Vector3.Cross(Vector3.up, moved / distance);
            float degrees = distance / (BallVisualSize * 0.5f) * Mathf.Rad2Deg;
            _ball.localRotation = Quaternion.AngleAxis(degrees, axis) * _ball.localRotation;
        }

        private static void PlaceIndicator(Transform indicator, MatchSnapshot previous, MatchSnapshot current, float alpha, int player, float height)
        {
            bool show = player >= 0 && !current.SentOff[player];
            if (indicator.gameObject.activeSelf != show)
            {
                indicator.gameObject.SetActive(show);
            }

            if (show)
            {
                float x = Mathf.Lerp(previous.PlayerX[player], current.PlayerX[player], alpha);
                float y = Mathf.Lerp(previous.PlayerY[player], current.PlayerY[player], alpha);
                indicator.localPosition = ToUnity(x, y, height);
            }
        }

        private void UpdateCamera(float ballX, float ballY, float deltaTime)
        {
            if (float.IsNaN(ballX) || float.IsNaN(ballY) || float.IsInfinity(ballX) || float.IsInfinity(ballY))
            {
                return; // nunca mover la cámara a una posición inválida
            }

            float follow = 1f - Mathf.Exp(-CameraFollowSpeed * deltaTime);
            if (_focusPlayer >= 0 && _focusPlayer < _heads.Length && _visible[_focusPlayer])
            {
                // Cinemática de festejo: la cámara se acerca al goleador y lo sigue.
                ballX = ToEngineX(_heads[_focusPlayer]);
                ballY = ToEngineY(_heads[_focusPlayer]);
            }

            if (_setPieceView)
            {
                // Saque de la persona: la cámara mira entre el balón y hacia donde apunta, un poco más abierta.
                ballX = _setPieceX;
                ballY = _setPieceY;
            }

            float maxX = Mathf.Max(0f, _pitch.HalfLength - CameraEdgeMargin * _cameraScale);
            bool broadcastLike = _cameraMode != CameraTactical || _replayAngle != NoReplay;
            float aheadX = broadcastLike ? _lookAheadX : 0f; // la panorámica táctica no se adelanta: muestra el bloque entero
            float aheadZ = broadcastLike ? _lookAheadZ : 0f;
            _cameraX = Mathf.Lerp(_cameraX, Mathf.Clamp((ballX + aheadX) * CameraFollowX, -maxX, maxX), follow);
            _cameraZ = Mathf.Lerp(_cameraZ, (ballY + aheadZ) * CameraFollowZ, follow);
            float zoom = _focusPlayer >= 0 ? FocusZoom : _setPieceView ? SetPieceZoom : broadcastLike ? _zoom : 1f;
            float scale = _cameraScale * zoom;
            CameraTuning c = ViewTuning.Current.Camera;
            Transform t = _camera.transform;
            Vector3 position, lookAt;
            if (!float.IsNaN(_presentationX))
            {
                // Presentación táctica: toma panorámica alta desde el lateral sobre la mitad del equipo que se presenta.
                position = new Vector3(_presentationX, PresentationHeight * _cameraScale, -PresentationDistance * _cameraScale);
                lookAt = new Vector3(_presentationX, 0f, PresentationLookAhead * _cameraScale);
                float blend = 1f - Mathf.Exp(-c.TransitionSpeed * deltaTime);
                _camPosition = _camReady ? Vector3.Lerp(_camPosition, position, blend) : position;
                _camLook = _camReady ? Vector3.Lerp(_camLook, lookAt, blend) : lookAt;
                _camReady = true;
                position = _camPosition;
                lookAt = _camLook;
            }
            else if (_replayAngle == ReplayBehindGoal)
            {
                // Repetición: a ras del césped, detrás del arco donde entró el gol, mirando hacia el balón (corte seco).
                position = new Vector3(_replayGoalEnd * (_pitch.HalfLength + c.GoalCamBehind), c.GoalCamHeight, 0f);
                lookAt = ToUnity(ballX, ballY, GoalCamLookHeight);
                _camPosition = position;
                _camLook = lookAt;
            }
            else
            {
                CameraPose(scale, c, out position, out lookAt, deltaTime);
                if (!_camReady || _replayAngle != NoReplay)
                {
                    _camPosition = position; // primer fotograma o repetición: sin arrastre
                    _camLook = lookAt;
                    _camReady = true;
                }

                float blend = 1f - Mathf.Exp(-c.TransitionSpeed * deltaTime);
                _camPosition = Vector3.Lerp(_camPosition, position, blend);
                _camLook = Vector3.Lerp(_camLook, lookAt, blend);
                position = _camPosition;
                lookAt = _camLook;
            }

            if (_introTime < _introDuration)
            {
                // Presentación: el estadio desde arriba y la cámara baja hasta su sitio de transmisión.
                _introTime += deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_introTime / _introDuration));
                position = Vector3.Lerp(new Vector3(0f, IntroHeight * _cameraScale, -IntroHeight * 0.35f * _cameraScale), position, u);
                lookAt = Vector3.Lerp(Vector3.zero, lookAt, u);
            }

            if (_shake > 0.001f)
            {
                _shake *= Mathf.Exp(-ShakeDecay * deltaTime);
                position += new Vector3(Mathf.Sin(_clock * ShakeFrequency), Mathf.Sin(_clock * ShakeFrequency * 1.3f + 1f), 0f) * _shake;
            }

            t.localPosition = position;
            t.LookAt(lookAt);
        }

        /// <summary>
        /// Flecha de apuntado de un saque sobre el césped, desde el balón hacia la dirección elegida; su largo y color
        /// muestran la carga. La cámara se abre y mira hacia donde apunta para ver a los compañeros.
        /// </summary>
        /// <param name="show">Mostrar u ocultar.</param>
        /// <param name="x">Balón, X del motor.</param>
        /// <param name="y">Balón, Y del motor.</param>
        /// <param name="dirX">Dirección, X del motor.</param>
        /// <param name="dirY">Dirección, Y del motor.</param>
        /// <param name="charge">Carga de 0 a 1.</param>
        public void SetAim(bool show, float x, float y, float dirX, float dirY, float charge)
        {
            _setPieceView = show;
            if (_aimShaft == null)
            {
                return;
            }

            if (_aimShaft.gameObject.activeSelf != show)
            {
                _aimShaft.gameObject.SetActive(show);
                _aimHead.gameObject.SetActive(show);
            }

            if (!show)
            {
                return;
            }

            float length = AimBaseLength + AimChargeLength * Mathf.Clamp01(charge);
            float yaw = 90f - Mathf.Atan2(dirY, dirX) * Mathf.Rad2Deg;
            var rotation = Quaternion.Euler(0f, yaw, 0f);
            _aimShaft.localRotation = rotation;
            _aimShaft.localScale = new Vector3(AimWidth, 0.02f, length);
            _aimShaft.localPosition = ToUnity(x + dirX * length * 0.5f, y + dirY * length * 0.5f, 0.05f);
            _aimHead.localRotation = rotation * Quaternion.Euler(0f, 45f, 0f);
            _aimHead.localScale = new Vector3(AimWidth * 2.2f, 0.02f, AimWidth * 2.2f);
            _aimHead.localPosition = ToUnity(x + dirX * length, y + dirY * length, 0.05f);
            Material m = _aimMaterials[charge < 0.34f ? 0 : charge < 0.67f ? 1 : 2];
            _aimShaftRenderer.sharedMaterial = m;
            _aimHeadRenderer.sharedMaterial = m;
            _setPieceX = x + dirX * SetPieceLookAhead;
            _setPieceY = y + dirY * SetPieceLookAhead;
        }

        private const int CameraBroadcast = 0;
        private const int CameraTactical = 1;
        private const int CameraLowSideline = 2;
        private const int CameraPro = 3;
        private const int NoReplay = -1;
        private const int ReplayBroadcast = 0;
        private const int ReplayBehindGoal = 1;
        private const float ProMinSpeed = 1.2f;
        private const float GoalCamLookHeight = 0.6f;
        private int _cameraMode = CameraBroadcast;
        private int _replayAngle = NoReplay;
        private float _replayGoalEnd = 1f;
        private int _cameraTarget = -1;
        private float _cameraTargetHeading, _cameraTargetSpeed;
        private float _proYaw = float.NaN;
        private Vector3 _camPosition, _camLook;
        private bool _camReady;

        /// <summary>
        /// Tipo de cámara: 0 transmisión de TV (zoom dinámico y anticipación), 1 táctica panorámica, 2 lateral baja,
        /// 3 profesional (detrás del jugador controlado). El cambio se hace con un paso suave, sin saltos.
        /// </summary>
        /// <param name="mode">Modo.</param>
        public void SetCameraMode(int mode)
        {
            _cameraMode = Mathf.Clamp(mode, CameraBroadcast, CameraPro);
        }

        /// <summary>Ángulo de la repetición: -1 sin repetición, 0 el de la transmisión, 1 a ras del césped detrás del arco.</summary>
        /// <param name="angle">Ángulo.</param>
        /// <param name="goalEnd">Arco donde entró el gol (+1 o -1 en X del motor).</param>
        public void SetReplayAngle(int angle, float goalEnd)
        {
            _replayAngle = angle;
            _replayGoalEnd = goalEnd < 0f ? -1f : 1f;
        }

        /// <summary>La cámara salta de golpe a la posición que le toca (sin arrastre): al terminar una repetición, por ejemplo.</summary>
        public void SnapCamera()
        {
            _camReady = false;
        }

        /// <summary>Pose de la cámara del modo elegido (el paso suave entre modos se aplica después).</summary>
        private void CameraPose(float scale, CameraTuning c, out Vector3 position, out Vector3 lookAt, float deltaTime)
        {
            int mode = _replayAngle != NoReplay ? CameraBroadcast : _cameraMode;
            if (mode == CameraPro && _cameraTarget >= 0 && _focusPlayer < 0 && !_setPieceView)
            {
                Vector3 player = _heads[_cameraTarget];
                player.y = 0f;
                if (_cameraTargetSpeed > ProMinSpeed || float.IsNaN(_proYaw))
                {
                    float wanted = 90f - _cameraTargetHeading; // del ángulo del motor (X, Y) al giro de Unity
                    _proYaw = float.IsNaN(_proYaw) ? wanted : Mathf.LerpAngle(_proYaw, wanted, 1f - Mathf.Exp(-c.ProTurnSpeed * deltaTime));
                }

                Vector3 forward = Quaternion.Euler(0f, _proYaw, 0f) * Vector3.forward;
                position = player - forward * c.ProBehind + Vector3.up * c.ProHeight;
                lookAt = player + forward * c.ProLookAhead;
                return;
            }

            float height = CameraHeight, distance = CameraDistance;
            if (mode == CameraTactical)
            {
                height = c.TacticalHeight;
                distance = c.TacticalDistance;
            }
            else if (mode == CameraLowSideline)
            {
                height = c.LowHeight;
                distance = c.LowDistance;
            }

            position = new Vector3(_cameraX, height * scale, _cameraZ - distance * scale);
            lookAt = new Vector3(_cameraX, 0f, _cameraZ + 2f);
        }

        /// <summary>Empieza la presentación: plano aéreo del estadio que baja hasta la cámara de transmisión.</summary>
        /// <param name="seconds">Duración.</param>
        public void PlayIntro(float seconds)
        {
            _introDuration = seconds;
            _introTime = 0f;
        }

        /// <summary>Termina la presentación ya (si la persona la salta).</summary>
        public void SkipIntro()
        {
            _introTime = float.MaxValue;
        }

        /// <summary>La cámara sigue de cerca a un jugador (festejo de gol); -1 vuelve al balón.</summary>
        public void Focus(int player)
        {
            _focusPlayer = player;
        }

        /// <summary>Temblor de cámara sutil (balón al palo).</summary>
        /// <param name="amount">Amplitud en metros.</param>
        public void Shake(float amount)
        {
            _shake = Mathf.Max(_shake, amount);
        }

        /// <summary>La red de la portería indicada se infla hacia atrás y vuelve, como al recibir el balón.</summary>
        /// <param name="endSign">Extremo de la portería (+1 o -1 en X del motor).</param>
        public void BulgeNet(float endSign)
        {
            _netBulge[endSign < 0f ? 0 : 1] = 1f;
        }

        private void AnimateNets(float deltaTime)
        {
            for (int k = 0; k < 2; k++)
            {
                if (_netBack[k] == null)
                {
                    continue;
                }

                // Muelle amortiguado: se estira y vuelve con un pequeño rebote.
                _netVelocity[k] += (-NetSpring * _netBulge[k] - NetDamping * _netVelocity[k]) * deltaTime;
                _netBulge[k] += _netVelocity[k] * deltaTime;
                float end = k == 0 ? -1f : 1f;
                _netBack[k].localPosition = _netRest[k] + new Vector3(end * NetBulgeDepth * Mathf.Max(0f, _netBulge[k]), 0f, 0f);
            }
        }

        /// <summary>
        /// Cámara dinámica: se aleja con balones largos o en el medio del campo (para ver opciones de pase), se acerca en el
        /// último tercio (para definir) y se adelanta hacia donde corre el jugador con el balón.
        /// </summary>
        private void UpdateLookAhead(MatchSnapshot s, float deltaTime)
        {
            float targetX = 0f, targetZ = 0f;
            if (s.OwnerIndex >= 0 && s.OwnerIndex < _players.Length)
            {
                float heading = s.PlayerHeading[s.OwnerIndex];
                float run = s.PlayerSpeed[s.OwnerIndex] * LookAheadSeconds;
                targetX = Mathf.Clamp(Mathf.Cos(heading) * run, -LookAheadMax, LookAheadMax);
                targetZ = Mathf.Clamp(Mathf.Sin(heading) * run, -LookAheadMax, LookAheadMax);
            }

            float blend = 1f - Mathf.Exp(-ZoomSpeed * deltaTime);
            _lookAheadX = Mathf.Lerp(_lookAheadX, targetX, blend);
            _lookAheadZ = Mathf.Lerp(_lookAheadZ, targetZ, blend);

            float progress = Mathf.Abs(s.BallX) / Mathf.Max(1f, _pitch.HalfLength); // 0 en el medio, 1 en un arco
            bool longBall = s.OwnerIndex < 0 && _ballSpeed > ZoomLongBallSpeed;
            float zoom = longBall ? ZoomOut : Mathf.Lerp(ZoomOut, ZoomIn, Mathf.Clamp01((progress - 0.35f) / 0.4f));
            _zoom = Mathf.Lerp(_zoom, zoom, blend);
        }

        /// <summary>
        /// Posición en pantalla (píxeles) a los pies de un jugador, para poner su nombre debajo (así no choca con la flecha
        /// del jugador controlado); falso si no está visible.
        /// </summary>
        public bool FeetOnScreen(int player, out Vector3 screen)
        {
            screen = default;
            if (_camera == null || !_visible[player])
            {
                return false;
            }

            Vector3 head = _heads[player];
            screen = _camera.WorldToScreenPoint(new Vector3(head.x, 0f, head.z));
            return screen.z > 0f;
        }

        /// <summary>Posición en pantalla (píxeles) sobre la cabeza de un jugador; falso si no está visible.</summary>
        public bool HeadOnScreen(int player, out Vector3 screen)
        {
            screen = default;
            if (_camera == null || !_visible[player])
            {
                return false;
            }

            screen = _camera.WorldToScreenPoint(_heads[player]);
            return screen.z > 0f;
        }

        private const float PresentationHeight = 30f;
        private const float PresentationDistance = 34f;
        private const float PresentationLookAhead = 4f;
        private const float TacticsBoardHeight = 0.03f;
        private const float TacticsLineWidth = 0.18f;
        private const float TacticsBoardMargin = 3f;
        private static readonly Color TacticsBoardColor = new Color(0.04f, 0.16f, 0.1f);
        private static readonly Color TacticsLineColor = new Color(0.92f, 0.96f, 1f);
        private float _presentationX = float.NaN;
        private GameObject _board;
        private readonly List<Mesh> _boardMeshes = new List<Mesh>();

        /// <summary>Toma panorámica de la presentación táctica centrada en una X del motor (NaN la quita).</summary>
        /// <param name="engineX">Centro de la mitad del equipo que se presenta.</param>
        public void SetPresentationShot(float engineX)
        {
            _presentationX = engineX;
            if (float.IsNaN(engineX))
            {
                _camReady = false; // la transmisión retoma desde su propia toma, sin arrastre
            }
        }

        /// <summary>
        /// Pizarra táctica sobre el césped: un tablero oscuro bajo la mitad del equipo con líneas blancas que unen a los
        /// jugadores de cada línea (defensa, medio, ataque), como en los gráficos de la televisión. Reemplaza la anterior.
        /// </summary>
        /// <param name="minX">Extremo de la pizarra (X del motor).</param>
        /// <param name="maxX">Otro extremo.</param>
        /// <param name="players">Jugadores (X e Y del motor).</param>
        /// <param name="lineOf">Línea de cada jugador (0 arquero, 1 defensa, 2 medio, 3 ataque).</param>
        public void ShowFormationBoard(float minX, float maxX, Vector2[] players, int[] lineOf)
        {
            HideFormationBoard();
            _board = new GameObject("Pizarra");
            _board.transform.SetParent(_root.transform, false);
            var board = new MeshMerge();
            var lines = new MeshMerge();
            float centerX = (minX + maxX) * 0.5f, length = Mathf.Abs(maxX - minX);
            board.Add(MeshKit.Box, ToUnity(centerX, 0f, TacticsBoardHeight), Quaternion.identity,
                new Vector3(length, 0.02f, _pitch.Width - TacticsBoardMargin), TacticsBoardColor);
            for (int line = 1; line <= 3; line++)
            {
                for (int i = 0; i < players.Length; i++)
                {
                    if (lineOf[i] != line)
                    {
                        continue;
                    }

                    // Une a cada jugador con el siguiente de su línea (de un costado al otro).
                    int next = -1;
                    for (int j = 0; j < players.Length; j++)
                    {
                        if (lineOf[j] == line && players[j].y > players[i].y && (next < 0 || players[j].y < players[next].y))
                        {
                            next = j;
                        }
                    }

                    if (next >= 0)
                    {
                        Vector3 a = ToUnity(players[i].x, players[i].y, TacticsBoardHeight * 2f), b = ToUnity(players[next].x, players[next].y, TacticsBoardHeight * 2f);
                        Vector3 d = b - a;
                        lines.Add(MeshKit.Box, (a + b) * 0.5f, Quaternion.LookRotation(d.normalized, Vector3.up), new Vector3(TacticsLineWidth, 0.02f, d.magnitude),
                            TacticsLineColor);
                    }

                    lines.Add(MeshKit.Disc, ToUnity(players[i].x, players[i].y, TacticsBoardHeight * 2.5f), Quaternion.identity, new Vector3(1.6f, 1f, 1.6f), TacticsLineColor);
                }
            }

            AddBoardPart(board, TacticsBoardColor);
            AddBoardPart(lines, TacticsLineColor);
        }

        private void AddBoardPart(MeshMerge merge, Color color)
        {
            if (merge.IsEmpty)
            {
                return;
            }

            Mesh mesh = merge.Build();
            _boardMeshes.Add(mesh);
            var go = new GameObject("PizarraParte");
            go.transform.SetParent(_board.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Materials.Get(color);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>Quita la pizarra táctica.</summary>
        public void HideFormationBoard()
        {
            if (_board != null)
            {
                Object.Destroy(_board);
                _board = null;
            }

            for (int i = 0; i < _boardMeshes.Count; i++)
            {
                Object.Destroy(_boardMeshes[i]);
            }

            _boardMeshes.Clear();
        }

        /// <summary>Elimina todo lo creado.</summary>
        public void Destroy()
        {
            HideFormationBoard();
            Object.Destroy(_root);
            for (int i = 0; i < _meshes.Count; i++)
            {
                Object.Destroy(_meshes[i]); // las mallas creadas por código no se liberan solas con el objeto
            }

            _meshes.Clear();
            if (_ledMaterial != null)
            {
                Object.Destroy(_ledMaterial);
                _ledMaterial = null;
            }
            if (GameRoot.MenuCamera != null)
            {
                GameRoot.MenuCamera.enabled = true;
            }
        }

        // ---- Malla procedural del campo ----------------------------------------------------------------------------

        private sealed class MeshBuilder
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<int> _triangles = new List<int>();

            public void Quad(float x0, float z0, float x1, float z1, float y)
            {
                int i = _vertices.Count;
                _vertices.Add(new Vector3(x0, y, z0));
                _vertices.Add(new Vector3(x0, y, z1));
                _vertices.Add(new Vector3(x1, y, z1));
                _vertices.Add(new Vector3(x1, y, z0));
                _triangles.Add(i);
                _triangles.Add(i + 1);
                _triangles.Add(i + 2);
                _triangles.Add(i);
                _triangles.Add(i + 2);
                _triangles.Add(i + 3);
            }

            public void Stroke(Vector2 a, Vector2 b, float width, float y)
            {
                Vector2 d = (b - a).normalized;
                Vector2 n = new Vector2(-d.y, d.x) * (width * 0.5f);
                Vector2 e = d * (width * 0.5f);
                Vector2 p0 = a - e + n, p1 = a - e - n, p2 = b + e - n, p3 = b + e + n;
                int i = _vertices.Count;
                _vertices.Add(new Vector3(p0.x, y, p0.y));
                _vertices.Add(new Vector3(p1.x, y, p1.y));
                _vertices.Add(new Vector3(p2.x, y, p2.y));
                _vertices.Add(new Vector3(p3.x, y, p3.y));
                // Sentido horario visto desde arriba (la cara visible con el descarte de caras traseras).
                _triangles.Add(i);
                _triangles.Add(i + 3);
                _triangles.Add(i + 2);
                _triangles.Add(i);
                _triangles.Add(i + 2);
                _triangles.Add(i + 1);
            }

            public Mesh ToMesh()
            {
                var mesh = new Mesh();
                mesh.SetVertices(_vertices);
                mesh.SetTriangles(_triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
