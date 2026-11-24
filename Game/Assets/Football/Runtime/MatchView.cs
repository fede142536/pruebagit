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
        private Rain _rain;
        private readonly KitColors[] _hostKits;   // camiseta titular y suplente del dueño de casa: los colores de todo el público
        private readonly int[] _shirtNumbers;
        private const float LodHysteresis = 2f;
        private GrassBurst _grass;
        /// <summary>Cantidad de peinados distintos.</summary>
        public const int HairStyles = 6;
        private const float RunUpDistance = 2.4f;   // el sacador de córner o de saque de arco toma carrera en diagonal
        private const float RunUpSeconds = 0.22f;
        private const float HoldBallHeight = 0.5f;
        private const float HoldThrowMaxSpeed = 0.6f;  // el que saca de banda solo levanta el balón cuando ya está quieto
        private const float DiveSideMinDegrees = 8f;   // por debajo, la estirada es casi de frente: se tira hacia donde está el balón
        private readonly bool[] _diveLatched = new bool[MatchSnapshot.PlayerCount], _diveRight = new bool[MatchSnapshot.PlayerCount];
        private readonly float[] _diveFacing = new float[MatchSnapshot.PlayerCount];
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
        /// <param name="shirtNumbers">Dorsales por jugador (opcional).</param>
        /// <param name="hostKits">Camisetas titular y suplente del dueño de casa (el público solo lleva esos colores); sin indicar, la que viste en el partido.</param>
        /// <param name="hostIsSecondTeam">El dueño de casa es el segundo equipo (el usuario juega de visitante en el modo carrera): la tribuna y los carteles llevan sus colores.</param>
        public MatchView(Pitch pitch, KitColors homeKit, KitColors awayKit, int playersPerTeam, int tickRate, StadiumLook? look = null, int[] shirtNumbers = null,
            bool hostIsSecondTeam = false, KitColors[] hostKits = null)
        {
            _hostKits = hostKits != null && hostKits.Length > 0 ? hostKits : null;
            _hostIsSecondTeam = hostIsSecondTeam;
            _look = look ?? StadiumLook.Full;
            _shirtNumbers = shirtNumbers;
            _pitch = pitch;
            _tickRate = tickRate;
            _homeKit = homeKit;
            _awayKit = awayKit;
            KitColors host = hostIsSecondTeam ? awayKit : homeKit, guest = hostIsSecondTeam ? homeKit : awayKit;
            _ledColors = new[] { host.Shirt, new Color(0.95f, 0.95f, 0.95f), guest.Shirt, new Color(0.1f, 0.6f, 0.95f), host.Secondary };
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
            Safe("lluvia", () =>
            {
                if (_look.Weather == MatchWeather.Rain)
                {
                    _rain = new Rain(_root.transform, _meshes);
                }
            });
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
        private Dictionary<Color, MeshMerge> _static = new Dictionary<Color, MeshMerge>();

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
        private void FlushStatic() => FlushStatic(_root.transform);

        private void FlushStatic(Transform parent)
        {
            foreach (KeyValuePair<Color, MeshMerge> batch in _static)
            {
                Mesh mesh = batch.Value.Build();
                _meshes.Add(mesh);
                var go = new GameObject("Fijo");
                go.transform.SetParent(parent, false);
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

        private const float RainDarken = 0.78f, RainStripeFade = 0.5f, CloudDarken = 0.93f;

        /// <summary>Césped según el clima: mojado y más oscuro con lluvia (las franjas casi no se distinguen), algo apagado con el cielo nublado.</summary>
        private Color WetTint(Color color)
        {
            switch (_look.Weather)
            {
                case MatchWeather.Rain:
                    Color wet = color * RainDarken;
                    wet.a = 1f;
                    return color == StripeColor ? Color.Lerp(wet, GrassColor * RainDarken, RainStripeFade) : wet;
                case MatchWeather.Cloudy:
                    Color dull = color * CloudDarken;
                    dull.a = 1f;
                    return dull;
                default:
                    return color;
            }
        }

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
            AddMesh("Cesped", grass, WetTint(GrassColor));

            var stripes = new MeshBuilder();
            float band = _pitch.Length / StripeCount;
            for (int i = 0; i < StripeCount; i += 2)
            {
                stripes.Quad(-halfL + i * band, -halfW, -halfL + (i + 1) * band, halfW, StripeHeight);
            }

            AddMesh("Franjas", stripes, WetTint(StripeColor));

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

            // Gradas escalonadas detrás de los carteles: cada anillo son escalones que suben (cemento en tonos por anillo), con pasillos oscuros,
            // un muro al fondo y, en los estadios de dos anillos o más, techo sobre los últimos escalones.
            Color[] tiers = { new Color(0.36f, 0.37f, 0.41f), new Color(0.3f, 0.32f, 0.38f), new Color(0.41f, 0.41f, 0.44f), new Color(0.33f, 0.34f, 0.4f) };
            int built = Mathf.Clamp(_look.Tiers, 1, tiers.Length);
            BuildStands(halfL, halfW, built, tiers);
            BuildFloodlights(halfL, halfW, StandFront + built * StepsPerTier * StepDepth + TowerGap);

            if (!walls)
            {
                BuildCrowd(halfL, halfW, built);
                BuildBenchesAndPhotographers(_pitch.HalfLength, _pitch.HalfWidth);
                BuildNearStand(halfL, halfW, built, tiers);
                if (_look.StandsWork)
                {
                    BuildScaffolding(halfL, halfW, built);
                }
            }
        }

        private const int StepsPerTier = 5;
        private const float StepDepth = 1.1f;          // profundidad de cada escalón
        private const float StepRise = 0.55f;          // cuánto sube cada escalón
        private const float StandFront = 0.75f;        // distancia entre los carteles y el primer escalón
        private const float StandBaseHeight = 0.9f;
        private const float AisleSpacing = 18f, AisleWidth = 1.4f;   // pasillos (vomitorios) que cortan las filas de público
        private const int RoofSteps = 3;               // el techo cubre los últimos escalones
        private const float RoofClearance = 3.4f, RoofThickness = 0.25f, RoofOverhang = 1.4f, RoofColumnSpacing = 18f;
        private const int RoofMinTiers = 2;
        private const float WallExtraHeight = 2.5f, WallThickness = 0.5f;
        private const float LateralExtra = 30f, EndExtra = 20f;          // cuánto sobresale cada tribuna del largo/ancho del campo
        private const float TowerHeight = 34f, TowerPole = 0.7f, TowerOutX = 2f, TowerOutZ = 2f, TowerGap = 3f;  // las torres quedan detrás de las gradas
        private static readonly Color WallColor = new Color(0.16f, 0.17f, 0.2f), AisleColor = new Color(0.07f, 0.07f, 0.09f);
        private static readonly Color RoofColor = new Color(0.82f, 0.83f, 0.86f), ColumnColor = new Color(0.55f, 0.56f, 0.6f);
        private static readonly Color LampFrame = new Color(0.1f, 0.1f, 0.12f), LampLight = new Color(0.97f, 0.96f, 0.84f);

        /// <summary>Distancia (m) desde los carteles al centro del escalón <paramref name="k"/>.</summary>
        private static float StepOffset(int k) => StandFront + (k + 0.5f) * StepDepth;

        /// <summary>Altura (m) de la parte de arriba del escalón <paramref name="k"/>.</summary>
        private static float StepTop(int k) => StandBaseHeight + (k + 1) * StepRise;

        /// <summary>Verdadero si la posición <paramref name="along"/> (desde el centro de una fila de largo <paramref name="length"/>) cae sobre un pasillo.</summary>
        private static bool OnAisle(float along, float length)
        {
            float d = Mathf.Repeat(along + length * 0.5f, AisleSpacing);
            return d < AisleWidth * 0.5f || d > AisleSpacing - AisleWidth * 0.5f;
        }

        /// <summary>Gradas con escalones en el lateral de enfrente y las dos cabeceras.</summary>
        private void BuildStands(float halfL, float halfW, int built, Color[] tiers)
        {
            int steps = built * StepsPerTier;
            float lateralLen = 2f * halfL + LateralExtra, endLen = 2f * halfW + EndExtra;
            float crowdLateral = 2f * halfL + CrowdLateralExtra, crowdEnd = 2f * halfW + CrowdEndExtra;
            for (int k = 0; k < steps; k++)
            {
                float off = StepOffset(k), top = StepTop(k);
                Color c = tiers[k / StepsPerTier];
                if (k % 2 == 1)
                {
                    c = Color.Lerp(c, Color.white, 0.07f); // franjas: se distinguen los escalones
                }

                StaticShape("Escalon", MeshKit.Box, c, new Vector3(0f, top * 0.5f, halfW + off), new Vector3(lateralLen, top, StepDepth));
                StaticShape("Escalon", MeshKit.Box, c, new Vector3(-halfL - off, top * 0.5f, 0f), new Vector3(StepDepth, top, endLen));
                StaticShape("Escalon", MeshKit.Box, c, new Vector3(halfL + off, top * 0.5f, 0f), new Vector3(StepDepth, top, endLen));
                for (float a = -crowdLateral * 0.5f + AisleSpacing; a < crowdLateral * 0.5f - AisleWidth; a += AisleSpacing)
                {
                    StaticShape("Pasillo", MeshKit.Box, AisleColor, new Vector3(a, top + 0.02f, halfW + off), new Vector3(AisleWidth, 0.04f, StepDepth));
                }

                for (float a = -crowdEnd * 0.5f + AisleSpacing; a < crowdEnd * 0.5f - AisleWidth; a += AisleSpacing)
                {
                    StaticShape("Pasillo", MeshKit.Box, AisleColor, new Vector3(-halfL - off, top + 0.02f, a), new Vector3(StepDepth, 0.04f, AisleWidth));
                    StaticShape("Pasillo", MeshKit.Box, AisleColor, new Vector3(halfL + off, top + 0.02f, a), new Vector3(StepDepth, 0.04f, AisleWidth));
                }
            }

            float wallOff = StandFront + steps * StepDepth + WallThickness * 0.5f;
            float wallHeight = StepTop(steps - 1) + WallExtraHeight;
            StaticShape("Muro", MeshKit.Box, WallColor, new Vector3(0f, wallHeight * 0.5f, halfW + wallOff), new Vector3(lateralLen, wallHeight, WallThickness));
            StaticShape("Muro", MeshKit.Box, WallColor, new Vector3(-halfL - wallOff, wallHeight * 0.5f, 0f), new Vector3(WallThickness, wallHeight, endLen));
            StaticShape("Muro", MeshKit.Box, WallColor, new Vector3(halfL + wallOff, wallHeight * 0.5f, 0f), new Vector3(WallThickness, wallHeight, endLen));

            if (built >= RoofMinTiers)
            {
                BuildRoofs(halfL, halfW, steps, lateralLen, endLen);
            }
        }

        private const float NearStandGap = 5f;   // los bancos y el túnel quedan delante de la tribuna cercana
        private Transform _nearStand;

        /// <summary>
        /// Tribuna del lateral de la cámara de transmisión (detrás de los bancos y del túnel) con su público. Solo se ve durante las presentaciones y la introducción de TV,
        /// cuando la cámara mira hacia ese lateral; en el partido se oculta para que no tape la cámara de juego.
        /// </summary>
        private void BuildNearStand(float halfL, float halfW, int built, Color[] tiers)
        {
            _nearStand = new GameObject("TribunaCercana").transform;
            _nearStand.SetParent(_root.transform, false);
            Dictionary<Color, MeshMerge> outer = _static;
            _static = new Dictionary<Color, MeshMerge>();
            int steps = built * StepsPerTier;
            float lateralLen = 2f * halfL + LateralExtra;
            for (int k = 0; k < steps; k++)
            {
                float off = NearStandGap + StepOffset(k), top = StepTop(k);
                Color c = tiers[k / StepsPerTier];
                if (k % 2 == 1)
                {
                    c = Color.Lerp(c, Color.white, 0.07f);
                }

                StaticShape("Escalon", MeshKit.Box, c, new Vector3(0f, top * 0.5f, -(halfW + off)), new Vector3(lateralLen, top, StepDepth));
            }

            float wallOff = NearStandGap + StandFront + steps * StepDepth + WallThickness * 0.5f;
            float wallHeight = StepTop(steps - 1) + WallExtraHeight;
            StaticShape("Muro", MeshKit.Box, WallColor, new Vector3(0f, wallHeight * 0.5f, -(halfW + wallOff)), new Vector3(lateralLen, wallHeight, WallThickness));
            FlushStatic(_nearStand);
            _static = outer;

            float spacing = CrowdSpacing[Mathf.Clamp(GraphicsQuality.Level, 0, CrowdSpacing.Length - 1)];
            KitColors host = _hostIsSecondTeam ? _awayKit : _homeKit;
            KitColors first = _hostKits != null ? _hostKits[0] : host, second = _hostKits != null && _hostKits.Length > 1 ? _hostKits[1] : first;
            Color[] mixed = { first.Shirt, first.Shirt, first.Shirt, second.Shirt, second.Shirt, first.Secondary, second.Secondary, first.Shirt };
            var crowd = new MeshMerge();
            int seed = NearStandSeed;
            float crowdLength = 2f * halfL + CrowdLateralExtra;
            for (int k = 0; k < steps; k++)
            {
                CrowdRow(crowd, new Vector3(0f, StepTop(k), -(halfW + NearStandGap + StepOffset(k))), crowdLength, true, spacing, mixed, ref seed, _look.Attendance);
            }

            FinishCrowd(crowd, _nearStand);
            _nearStand.gameObject.SetActive(false);
        }

        private const int NearStandSeed = 100000;

        /// <summary>Techo sobre los últimos escalones del lateral y las cabeceras, con columnas en los pasillos.</summary>
        private void BuildRoofs(float halfL, float halfW, int steps, float lateralLen, float endLen)
        {
            float frontOff = StandFront + (steps - RoofSteps) * StepDepth - RoofOverhang;      // borde de adelante del techo
            float backOff = StandFront + steps * StepDepth + WallThickness;
            float depth = backOff - frontOff, centerOff = (frontOff + backOff) * 0.5f;
            float y = StepTop(steps - 1) + RoofClearance;
            StaticShape("Techo", MeshKit.Box, RoofColor, new Vector3(0f, y, halfW + centerOff), new Vector3(lateralLen, RoofThickness, depth));
            StaticShape("Techo", MeshKit.Box, RoofColor, new Vector3(-halfL - centerOff, y, 0f), new Vector3(depth, RoofThickness, endLen));
            StaticShape("Techo", MeshKit.Box, RoofColor, new Vector3(halfL + centerOff, y, 0f), new Vector3(depth, RoofThickness, endLen));
            for (float a = -lateralLen * 0.5f + RoofColumnSpacing; a < lateralLen * 0.5f - 1f; a += RoofColumnSpacing)
            {
                StaticShape("Columna", MeshKit.Box, ColumnColor, new Vector3(a, y * 0.5f, halfW + frontOff + 0.4f), new Vector3(0.35f, y, 0.35f));
            }

            for (float a = -endLen * 0.5f + RoofColumnSpacing; a < endLen * 0.5f - 1f; a += RoofColumnSpacing)
            {
                StaticShape("Columna", MeshKit.Box, ColumnColor, new Vector3(-halfL - frontOff - 0.4f, y * 0.5f, a), new Vector3(0.35f, y, 0.35f));
                StaticShape("Columna", MeshKit.Box, ColumnColor, new Vector3(halfL + frontOff + 0.4f, y * 0.5f, a), new Vector3(0.35f, y, 0.35f));
            }
        }

        /// <summary>Cuatro torres de reflectores en las esquinas (postes y paneles de luz apuntando al centro del campo).</summary>
        private void BuildFloodlights(float halfL, float halfW, float standDepth)
        {
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    var foot = new Vector3(sx * (halfL + standDepth + TowerOutX), 0f, sz * (halfW + standDepth + TowerOutZ));
                    StaticShape("Torre", MeshKit.Box, ColumnColor, foot + new Vector3(0f, TowerHeight * 0.5f, 0f), new Vector3(TowerPole, TowerHeight, TowerPole));
                    Vector3 toCenter = new Vector3(-foot.x, 0f, -foot.z);
                    Quaternion face = Quaternion.LookRotation(toCenter.normalized, Vector3.up);
                    Vector3 head = foot + new Vector3(0f, TowerHeight + 1.2f, 0f);
                    StaticShape("Reflector", MeshKit.Box, LampFrame, head, new Vector3(6.4f, 3.4f, 0.5f), face);
                    for (int row = 0; row < 3; row++)
                    {
                        for (int col = 0; col < 5; col++)
                        {
                            Vector3 local = new Vector3((col - 2) * 1.2f, (row - 1) * 1.0f, 0.3f);
                            StaticShape("Lampara", MeshKit.Box, LampLight, head + face * local, new Vector3(0.85f, 0.7f, 0.12f), face);
                        }
                    }
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
            int next = builtTiers * StepsPerTier;                       // el anillo nuevo va detrás del último
            float z = halfW + StepOffset(next + StepsPerTier / 2);
            float height = StepTop(next + StepsPerTier - 1);
            float length = 2f * halfL + LateralExtra;
            float scaffoldDepth = StepsPerTier * StepDepth;
            int poles = (int)(length / ScaffoldPoleSpacing);
            for (int k = 0; k <= poles; k++)
            {
                float x = -length * 0.5f + k * ScaffoldPoleSpacing;
                StaticShape("Andamio", MeshKit.Box, ScaffoldColor, new Vector3(x, height * 0.5f, z - scaffoldDepth * 0.4f), new Vector3(0.15f, height, 0.15f));
                StaticShape("Andamio", MeshKit.Box, ScaffoldColor, new Vector3(x, height * 0.5f, z + scaffoldDepth * 0.4f), new Vector3(0.15f, height, 0.15f));
            }

            for (float y = ScaffoldLevelHeight; y < height; y += ScaffoldLevelHeight)
            {
                StaticShape("Andamio", MeshKit.Box, ScaffoldColor, new Vector3(0f, y, z - scaffoldDepth * 0.4f), new Vector3(length, 0.12f, 0.12f));
                StaticShape("Andamio", MeshKit.Box, ScaffoldColor, new Vector3(0f, y, z + scaffoldDepth * 0.4f), new Vector3(length, 0.12f, 0.12f));
            }
        }

        // ---- Carteles LED ---------------------------------------------------------------------------------------------

        private const int LedSegments = 8;
        private const float GoalGapMargin = 2.5f;
        private const float LedCycleSeconds = 5f;
        private Material _ledMaterial;
        private float _ledClock;
        private Color[] _ledColors;
        private readonly bool _hostIsSecondTeam;

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
                    if (side < 0 && Mathf.Abs(x0 + length * 0.5f) < TunnelHalfWidth + length * 0.5f)
                    {
                        continue; // hueco de los carteles frente al túnel: por ahí salen los jugadores
                    }

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

        private const float CheerHeight = 0.35f;
        private const float CheerFrequency = 11f;
        private const float CrowdLateralExtra = 26f, CrowdEndExtra = 16f;
        private Transform _crowd;
        private float _cheerTimer;
        private static readonly float[] CrowdSpacing = { 2.4f, 1.6f, 1.1f }; // por perfil gráfico: bajo, medio, alto

        /// <summary>
        /// Público en las gradas: una fila de figuras simples (torso y cabeza) sobre cada escalón, agrupadas en una malla por tribuna.
        /// Todos son del local: la cabecera de la izquierda es la «popular» (casi todos con la camiseta titular), la de la derecha lleva más la suplente
        /// y el lateral es mezclado. La densidad depende del perfil gráfico y la ocupación (<see cref="StadiumLook.Attendance"/>): cada lugar se
        /// llena o no según un hash fijo. Los pasillos quedan libres.
        /// </summary>
        private void BuildCrowd(float halfL, float halfW, int tiers)
        {
            _crowd = new GameObject("Publico").transform;
            _crowd.SetParent(_root.transform, false);
            float spacing = CrowdSpacing[Mathf.Clamp(GraphicsQuality.Level, 0, CrowdSpacing.Length - 1)];
            // Todo el público es del local, con las camisetas titular y suplente de su club (más los detalles de cada una).
            KitColors host = _hostIsSecondTeam ? _awayKit : _homeKit;
            KitColors first = _hostKits != null ? _hostKits[0] : host, second = _hostKits != null && _hostKits.Length > 1 ? _hostKits[1] : first;
            Color[] mixed =
            {
                first.Shirt, first.Shirt, first.Shirt, second.Shirt, second.Shirt, first.Secondary, second.Secondary, first.Shirt
            };
            Color[] homeEnd = { first.Shirt, first.Shirt, first.Shirt, first.Shirt, first.Shirt, second.Shirt, first.Secondary, first.Shirt };
            Color[] awayEnd = { first.Shirt, second.Shirt, second.Shirt, first.Shirt, second.Shirt, second.Shirt, second.Secondary, first.Shirt };
            MeshMerge lateral = new MeshMerge(), left = new MeshMerge(), right = new MeshMerge();
            int seed = 0;
            int steps = tiers * StepsPerTier;
            float lateralLen = 2f * halfL + CrowdLateralExtra, endLen = 2f * halfW + CrowdEndExtra;
            for (int k = 0; k < steps; k++)
            {
                float off = StepOffset(k), top = StepTop(k);
                CrowdRow(lateral, new Vector3(0f, top, halfW + off), lateralLen, true, spacing, mixed, ref seed, _look.Attendance);
                CrowdRow(left, new Vector3(-halfL - off, top, 0f), endLen, false, spacing, homeEnd, ref seed, _look.Attendance);
                CrowdRow(right, new Vector3(halfL + off, top, 0f), endLen, false, spacing, awayEnd, ref seed, _look.Attendance);
            }

            FinishCrowd(lateral);
            FinishCrowd(left);
            FinishCrowd(right);
        }

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

            // Túnel de vestuarios: en el centro de la banda, entre los dos bancos (un hueco oscuro con techo).
            StaticShape("Tunel", MeshKit.Box, new Color(0.02f, 0.02f, 0.03f), new Vector3(0f, 1.25f, z - 1.2f), new Vector3(TunnelHalfWidth * 2f, 2.5f, 0.4f));
            StaticShape("Tunel", MeshKit.Box, roof, new Vector3(0f, 2.7f, z - 0.5f), new Vector3(TunnelHalfWidth * 2f + 0.6f, 0.25f, 1.8f));
            StaticShape("Tunel", MeshKit.Box, back, new Vector3(-TunnelHalfWidth - 0.2f, 1.3f, z - 0.5f), new Vector3(0.25f, 2.6f, 1.8f));
            StaticShape("Tunel", MeshKit.Box, back, new Vector3(TunnelHalfWidth + 0.2f, 1.3f, z - 0.5f), new Vector3(0.25f, 2.6f, 1.8f));

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

        private void CrowdRow(MeshMerge merge, Vector3 center, float length, bool alongX, float spacing, Color[] palette, ref int seed, float fill)
        {
            bool merged = Materials.PlayerShaderAvailable;
            int count = (int)(length / spacing);
            for (int k = 0; k < count; k++)
            {
                seed++;
                if (((uint)seed * SeatHash >> 8) / SeatHashRange >= fill)
                {
                    continue; // lugar vacío
                }

                float along = -length * 0.5f + (k + 0.5f) * spacing + ((seed * 7) % 5 - 2) * 0.08f;
                if (OnAisle(along, length))
                {
                    continue; // pasillo
                }

                Vector3 p = alongX ? new Vector3(along, 0f, 0f) : new Vector3(0f, 0f, along);
                Color shirt = palette[(seed * 13) % palette.Length];
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

        private void FinishCrowd(MeshMerge merge) => FinishCrowd(merge, _crowd);

        private void FinishCrowd(MeshMerge merge, Transform parent)
        {
            if (!Materials.PlayerShaderAvailable || merge.IsEmpty)
            {
                return;
            }

            Mesh mesh = merge.Build();
            _meshes.Add(mesh);
            var go = new GameObject("Hinchas");
            go.transform.SetParent(parent, false);
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
        // ------------------------------------------------------------------ introducción de TV

        private const float IntroRowInset = 10f;          // las filas de la foto quedan a esta distancia de la banda de los bancos
        private const float IntroRowGap = 1.35f;          // separación entre jugadores de la fila
        private const float IntroTeamCenterX = 9f;        // el local, a un lado del centro; el visitante, al otro
        private const float IntroAerialSeconds = 2f;      // plano aéreo antes de que salga el primero
        private const float IntroHostDelay = 3f;          // el local sale este tiempo después del visitante
        private const float IntroGap = 0.28f;             // entre un jugador y el siguiente al salir del túnel
        private const float IntroWalkSpeed = 3.1f;        // m/s al salir del túnel (trote suave)
        private const float IntroPhotoSeconds = 2.6f;     // cada equipo posa para la foto
        private const float IntroRunSpeed = 4.4f;         // m/s hacia su posición de saque
        private const float IntroRunJitter = 0.7f;        // no arrancan todos a la vez
        private const float IntroEndPause = 0.4f;
        private const float IntroPhotoDistance = 14f;     // la cámara de la foto, a esta distancia de la fila
        private const float IntroPhotoHeight = 1.9f;
        private const float IntroWideHeight = 7f, IntroWideDistance = 25f; // la toma general abarca las dos filas
        private const float IntroCameraResponse = 2.6f;
        private bool _teamIntro;
        private float _teamIntroTime;
        private float[] _tiSlotX, _tiSlotY, _tiStart, _tiArrive, _tiKickX, _tiKickY, _tiRunStart, _tiRunSeconds;
        private float _tiPhotoFirst, _tiPhotoSecond, _tiRunBegin, _tiEnd;
        private int _tiFirstTeam, _tiSecondTeam;

        /// <summary>Terminó la introducción (ya están en sus posiciones de saque).</summary>
        public bool TeamIntroDone { get; private set; }

        /// <summary>Equipo (índice del motor) que se está presentando ahora en la placa de TV, o -1; cambia a lo largo de la secuencia.</summary>
        public int IntroTeamShown { get; private set; } = -1;

        /// <summary>Hay una introducción en curso.</summary>
        public bool TeamIntroRunning => _teamIntro;

        /// <summary>
        /// Empieza la introducción de TV: el visitante y después el local salen del túnel en fila y se ubican en dos filas frente a la cámara, cada equipo
        /// posa para la foto y por último todos corren a su posición de saque. Es solo visual: el motor espera.
        /// </summary>
        /// <param name="kickoff">Posiciones de saque (donde terminan los jugadores).</param>
        /// <param name="hostIsSecondTeam">El dueño de casa es el segundo equipo del motor.</param>
        public void BeginTeamIntro(MatchSnapshot kickoff, bool hostIsSecondTeam)
        {
            int n = _players.Length;
            _tiSlotX = new float[n]; _tiSlotY = new float[n]; _tiStart = new float[n]; _tiArrive = new float[n];
            _tiKickX = new float[n]; _tiKickY = new float[n]; _tiRunStart = new float[n]; _tiRunSeconds = new float[n];
            int host = hostIsSecondTeam ? 1 : 0;
            _tiFirstTeam = 1 - host;   // sale primero el visitante
            _tiSecondTeam = host;
            float rowY = -(_pitch.HalfWidth - IntroRowInset);
            Vector2 mouth = TunnelMouth;
            float lastArrive = 0f;
            for (int i = 0; i < n; i++)
            {
                int team = i < _perTeam ? 0 : 1;
                int k = i % _perTeam;
                bool isHost = team == host;
                float centerX = (isHost ? 1f : -1f) * IntroTeamCenterX;
                _tiSlotX[i] = centerX + (k - (_perTeam - 1) * 0.5f) * IntroRowGap;
                _tiSlotY[i] = rowY;
                float teamStart = IntroAerialSeconds + (isHost ? IntroHostDelay : 0f);
                _tiStart[i] = teamStart + k * IntroGap;
                float dx = _tiSlotX[i] - mouth.x, dy = _tiSlotY[i] - mouth.y;
                _tiArrive[i] = _tiStart[i] + Mathf.Sqrt(dx * dx + dy * dy) / IntroWalkSpeed;
                lastArrive = Mathf.Max(lastArrive, _tiArrive[i]);
                _tiKickX[i] = kickoff.PlayerX[i];
                _tiKickY[i] = kickoff.PlayerY[i];
            }

            _tiPhotoFirst = lastArrive + 0.4f;
            _tiPhotoSecond = _tiPhotoFirst + IntroPhotoSeconds;
            _tiRunBegin = _tiPhotoSecond + IntroPhotoSeconds;
            float end = _tiRunBegin;
            for (int i = 0; i < n; i++)
            {
                float jitter = ((i * 37) % 11) / 10f;
                _tiRunStart[i] = _tiRunBegin + IntroRunJitter * jitter;
                float dx = _tiKickX[i] - _tiSlotX[i], dy = _tiKickY[i] - _tiSlotY[i];
                _tiRunSeconds[i] = Mathf.Sqrt(dx * dx + dy * dy) / IntroRunSpeed;
                end = Mathf.Max(end, _tiRunStart[i] + _tiRunSeconds[i]);
            }

            _tiEnd = end + IntroEndPause;
            _teamIntro = true;
            TeamIntroDone = false;
            IntroTeamShown = -1;
            _teamIntroTime = 0f;
            _camReady = false;
        }

        /// <summary>Corta la introducción (la persona la saltó o terminó): los jugadores quedan donde dice el motor.</summary>
        public void EndTeamIntro()
        {
            _teamIntro = false;
            TeamIntroDone = false;
            IntroTeamShown = -1;
            _camReady = false; // la transmisión retoma desde su toma
        }

        private bool IntroPose(int i, out float x, out float y, out float heading, out float speed)
        {
            float t = _teamIntroTime;
            Vector2 mouth = TunnelMouth;
            x = mouth.x; y = mouth.y; heading = 0f; speed = 0f;
            if (t < _tiStart[i])
            {
                return false; // todavía en el túnel
            }

            float faceCamera = 90f; // hacia +Y del motor: la cámara está del lado del campo, mirando al túnel
            if (t < _tiArrive[i])
            {
                float u = Mathf.Clamp01((t - _tiStart[i]) / Mathf.Max(_tiArrive[i] - _tiStart[i], 1e-3f));
                float jitter = (((i * 13) % 5) - 2) * TunnelHalfWidth * 0.2f;
                x = Mathf.Lerp(mouth.x + jitter, _tiSlotX[i], u);
                y = Mathf.Lerp(mouth.y, _tiSlotY[i], u);
                heading = Mathf.Atan2(_tiSlotY[i] - mouth.y, _tiSlotX[i] - mouth.x - jitter) * Mathf.Rad2Deg;
                speed = IntroWalkSpeed;
                return true;
            }

            if (t < _tiRunStart[i])
            {
                x = _tiSlotX[i]; y = _tiSlotY[i]; heading = faceCamera; speed = 0f; // posan mirando a cámara
                return true;
            }

            float run = (t - _tiRunStart[i]) / Mathf.Max(_tiRunSeconds[i], 1e-3f);
            if (run >= 1f)
            {
                x = _tiKickX[i]; y = _tiKickY[i];
                heading = Mathf.Atan2(0f - _tiKickY[i], 0f - _tiKickX[i]) * Mathf.Rad2Deg;
                return true;
            }

            float e = run * run * (3f - 2f * run);
            x = Mathf.Lerp(_tiSlotX[i], _tiKickX[i], e);
            y = Mathf.Lerp(_tiSlotY[i], _tiKickY[i], e);
            heading = Mathf.Atan2(_tiKickY[i] - _tiSlotY[i], _tiKickX[i] - _tiSlotX[i]) * Mathf.Rad2Deg;
            speed = IntroRunSpeed;
            return true;
        }

        /// <summary>Cámara de la introducción según el momento: aérea, general sobre las filas, foto de cada equipo y vuelta a la transmisión.</summary>
        private void IntroCamera(float deltaTime, out Vector3 position, out Vector3 lookAt)
        {
            float t = _teamIntroTime;
            float rowY = -(_pitch.HalfWidth - IntroRowInset);
            float s = _cameraScale;
            Vector3 targetPos, targetLook;
            IntroTeamShown = -1;
            if (t < IntroAerialSeconds)
            {
                targetPos = new Vector3(0f, IntroHeight * 0.55f * s, -IntroHeight * 0.45f * s);
                targetLook = new Vector3(0f, 0f, -_pitch.HalfWidth * 0.5f);
            }
            else if (t < _tiPhotoFirst)
            {
                targetPos = new Vector3(0f, IntroWideHeight * s, rowY + IntroWideDistance * s);
                targetLook = new Vector3(0f, 1.1f, rowY - 1f);
                IntroTeamShown = t < IntroAerialSeconds + IntroHostDelay ? _tiFirstTeam : _tiSecondTeam;
            }
            else if (t < _tiRunBegin)
            {
                bool first = t < _tiPhotoSecond;
                float cx = TeamCenterX(first ? _tiFirstTeam : _tiSecondTeam);
                targetPos = new Vector3(cx, IntroPhotoHeight, rowY + IntroPhotoDistance * s);
                targetLook = new Vector3(cx, 1.2f, rowY);
                IntroTeamShown = first ? _tiFirstTeam : _tiSecondTeam;
            }
            else
            {
                CameraTuning c = ViewTuning.Current.Camera;
                targetPos = new Vector3(0f, c.Height * s, -c.Distance * s);
                targetLook = new Vector3(0f, 0f, 2f);
            }

            float blend = 1f - Mathf.Exp(-IntroCameraResponse * deltaTime);
            _camPosition = _camReady ? Vector3.Lerp(_camPosition, targetPos, blend) : targetPos;
            _camLook = _camReady ? Vector3.Lerp(_camLook, targetLook, blend) : targetLook;
            _camReady = true;
            position = _camPosition;
            lookAt = _camLook;
        }

        private float TeamCenterX(int team)
        {
            bool isHost = team == (_tiSecondTeam);
            return (isHost ? 1f : -1f) * IntroTeamCenterX;
        }

        // ------------------------------------------------------------------ salida al vestuario

        private const float WalkInsetFromLine = 4f;       // los dos equipos se saludan a esta distancia de la banda de los bancos
        private const float GreetRowGap = 0.9f;           // separación entre jugadores de la fila del saludo
        private const float GreetHalfGap = 2f;            // mitad de la distancia entre las dos filas
        private const float GreetEndSeconds = 9f;         // el saludo termina a los 9 s y empiezan a irse
        private const float TunnelHalfWidth = 2.2f;
        private bool _walkOff;
        private float _walkOffTime;
        private float[] _woStartX, _woStartY, _woGreetX, _woGreetY, _woArrive, _woLeave, _woSpeed, _woFace;
        private bool[] _woGone;

        /// <summary>Todos los jugadores ya entraron al túnel.</summary>
        public bool WalkOffDone { get; private set; }

        /// <summary>Boca del túnel de vestuarios (engine X, Y): en el medio de la banda de los bancos, detrás de los carteles.</summary>
        private Vector2 TunnelMouth => new Vector2(0f, -(_pitch.HalfWidth + BoardDistance + 1.3f));

        /// <summary>
        /// Empieza la salida del campo al terminar el partido: cada jugador deja de correr y camina hacia la banda de los bancos, los dos equipos se
        /// saludan enfrentados en dos filas y después van entrando al túnel de a uno (no todos a la vez, y con ritmos distintos). Es solo visual:
        /// el motor ya terminó el partido.
        /// </summary>
        /// <param name="final">Posiciones al final del partido.</param>
        public void BeginWalkOff(MatchSnapshot final)
        {
            int n = _players.Length;
            _woStartX = new float[n]; _woStartY = new float[n]; _woGreetX = new float[n]; _woGreetY = new float[n];
            _woArrive = new float[n]; _woLeave = new float[n]; _woSpeed = new float[n]; _woFace = new float[n];
            _woGone = new bool[n];
            float rowY = -(_pitch.HalfWidth - WalkInsetFromLine);
            for (int i = 0; i < n; i++)
            {
                bool home = i < _perTeam;
                int k = i % _perTeam;
                _woStartX[i] = final.PlayerX[i];
                _woStartY[i] = final.PlayerY[i];
                // Dos filas enfrentadas junto a la banda, cada jugador frente a su par (el arquero también): el del local mira al visitante.
                _woGreetX[i] = (home ? -1f : 1f) * GreetHalfGap;
                _woGreetY[i] = rowY + (k - (_perTeam - 1) * 0.5f) * GreetRowGap * (_perTeam > 5 ? 1f : 2.5f);
                _woFace[i] = home ? 0f : 180f; // grados: hacia +X (local) o hacia −X (visitante)
                float distance = Mathf.Sqrt((_woGreetX[i] - _woStartX[i]) * (_woGreetX[i] - _woStartX[i]) + (_woGreetY[i] - _woStartY[i]) * (_woGreetY[i] - _woStartY[i]));
                float jitter = ((i * 37) % 11) / 10f;                       // 0..1 fijo por jugador
                float toGreet = 2.6f + 0.6f * jitter;                        // m/s: pasan del trote a la caminata
                _woArrive[i] = 0.8f + 0.12f * k + distance / toGreet;
                _woLeave[i] = GreetEndSeconds + (home ? 0f : 1.5f) + 0.55f * k + 1.2f * jitter; // se van escalonados, primero el local
                _woSpeed[i] = 1.9f + 0.7f * (1f - jitter) + (k == 0 ? -0.3f : 0f);              // los arqueros, un poco más lentos
            }

            _walkOff = true;
            WalkOffDone = false;
            _walkOffTime = 0f;
        }

        /// <summary>Corta la salida (por ejemplo al volver al menú).</summary>
        public void EndWalkOff()
        {
            _walkOff = false;
            WalkOffDone = false;
        }

        /// <summary>Posición, rumbo y velocidad de un jugador durante la salida; devuelve falso cuando ya entró al túnel.</summary>
        private bool WalkOffPose(int i, out float x, out float y, out float heading, out float speed)
        {
            float t = _walkOffTime;
            Vector2 tunnel = TunnelMouth;
            x = _woStartX[i]; y = _woStartY[i]; heading = 0f; speed = 0f;
            if (t < _woArrive[i])
            {
                float u = Mathf.Clamp01(t / _woArrive[i]);
                float ease = u * u * (3f - 2f * u); // arranca frenando la carrera y llega caminando
                x = Mathf.Lerp(_woStartX[i], _woGreetX[i], ease);
                y = Mathf.Lerp(_woStartY[i], _woGreetY[i], ease);
                heading = Mathf.Atan2(_woGreetY[i] - _woStartY[i], _woGreetX[i] - _woStartX[i]) * Mathf.Rad2Deg;
                speed = Mathf.Lerp(3.2f, 1.4f, u);
                return true;
            }

            if (t < _woLeave[i])
            {
                x = _woGreetX[i]; y = _woGreetY[i]; heading = _woFace[i]; speed = 0f; // saludo
                return true;
            }

            float gx = _woGreetX[i], gy = _woGreetY[i];
            float dx = tunnel.x - gx, dy = tunnel.y - gy;
            float length = Mathf.Sqrt(dx * dx + dy * dy);
            float travelled = (t - _woLeave[i]) * _woSpeed[i];
            if (travelled >= length)
            {
                return false;
            }

            float w = travelled / length;
            // Se acercan a la boca del túnel por el centro de la fila (sin cruzar el banco): un leve desvío lateral por jugador.
            x = Mathf.Lerp(gx, tunnel.x + (((i * 13) % 5) - 2) * TunnelHalfWidth * 0.2f, w);
            y = Mathf.Lerp(gy, tunnel.y, w);
            heading = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            speed = _woSpeed[i];
            return true;
        }

        public void Render(MatchSnapshot previous, MatchSnapshot current, float alpha, int controlled, int passTarget, float deltaTime,
            int switchTarget = -1)
        {
            _clock += deltaTime;
            if (_walkOff)
            {
                _walkOffTime += deltaTime;
            }

            if (_teamIntro)
            {
                _teamIntroTime += deltaTime;
                if (_teamIntroTime >= _tiEnd)
                {
                    TeamIntroDone = true;
                }
            }

            _confetti?.Update(deltaTime);
            _grass?.Update(deltaTime);
            _rain?.Update(deltaTime, new Vector2(Mathf.Lerp(previous.BallX, current.BallX, alpha), Mathf.Lerp(previous.BallY, current.BallY, alpha)));
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
                PlayerAction walkAction = current.PlayerAction[i];
                if (_teamIntro)
                {
                    if (!IntroPose(i, out x, out y, out heading, out speed))
                    {
                        model.SetVisible(false);
                        _visible[i] = false;
                        continue;
                    }

                    walkAction = PlayerAction.Idle;
                }

                if (_walkOff)
                {
                    if (_woGone[i] || !WalkOffPose(i, out x, out y, out heading, out speed))
                    {
                        _woGone[i] = true;
                        model.SetVisible(false);
                        _visible[i] = false;
                        continue;
                    }

                    walkAction = PlayerAction.Idle;
                }

                // Ángulo hacia el balón relativo a la orientación (positivo = a la izquierda en el plano del motor).
                float toBall = Mathf.Atan2(by - y, bx - x) * Mathf.Rad2Deg;
                float relative = Mathf.DeltaAngle(heading, toBall);
                PlayerAction action = walkAction;
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

                bool diveRight = relative < 0f;
                if (action == PlayerAction.Dive)
                {
                    // La estirada se decide al lanzarse y no cambia en el aire: el cuerpo mira hacia donde miraba y se tira hacia el lado del salto.
                    if (!_diveLatched[i])
                    {
                        _diveLatched[i] = true;
                        float toBallStart = Mathf.Atan2(by - y, bx - x) * Mathf.Rad2Deg;
                        _diveFacing[i] = toBallStart; // el cuerpo mira al balón (la animación ya se tira de costado)
                        float side = Mathf.DeltaAngle(toBallStart, heading); // hacia dónde se mueve respecto de donde mira el balón
                        _diveRight[i] = Mathf.Abs(side) > DiveSideMinDegrees ? side < 0f : relative < 0f;
                    }

                    heading = _diveFacing[i];
                    diveRight = _diveRight[i];
                    relative = 0f;
                }
                else
                {
                    _diveLatched[i] = false;
                }

                // En el lateral, el que saca camina al balón con los brazos normales y solo cuando llega a la línea y se queda quieto levanta el balón.
                bool holdingThrow = current.Phase == MatchPhase.SetPiece && current.TakerIndex == i && current.Restart == RestartKind.ThrowIn && speed < HoldThrowMaxSpeed;
                bool holdingBall = current.OwnerIndex == i && i % _perTeam == 0 && current.BallZ > HoldBallHeight;
                float ballDistance = Mathf.Max(MinLookDistance, Mathf.Sqrt((bx - x) * (bx - x) + (by - y) * (by - y)));
                float lookPitch = Mathf.Atan2(bz - EyeHeight, ballDistance) * Mathf.Rad2Deg;
                UpdateDetail(model, ToUnity(x, y, 0f));
                model.Pose(ToUnity(x, y, 0f), heading, speed, ReferenceTopSpeed, action, -relative, diveRight, deltaTime,
                    current.DiveHeight[i], holdingThrow, holdingBall, lookPitch);
                _heads[i] = ToUnity(x, y, NameHeight);
            }

            if (_walkOff && !WalkOffDone)
            {
                bool all = true;
                for (int i = 0; i < _woGone.Length && all; i++)
                {
                    all = _woGone[i] || _players[i] == null;
                }

                WalkOffDone = all;
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
            if (_teamIntro)
            {
                IntroCamera(deltaTime, out position, out lookAt);
            }
            else if (!float.IsNaN(_presentationX))
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

            if (_nearStand != null)
            {
                bool wantNear = _teamIntro || _introTime < _introDuration; // la tribuna del lateral de la cámara solo se ve mientras la cámara mira hacia ahí
                if (_nearStand.gameObject.activeSelf != wantNear)
                {
                    _nearStand.gameObject.SetActive(wantNear);
                }
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
