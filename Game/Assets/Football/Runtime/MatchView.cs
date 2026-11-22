using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Football.Core.MatchCore;

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
        private const float BallVisualSize = 0.3f;
        private const float CameraHeight = 21f;
        private const float CameraDistance = 30f;
        private const float CameraFollowX = 0.8f;
        private const float CameraFollowZ = 0.85f; // sigue casi del todo a lo ancho: así se ven las dos líneas laterales
        private const float CameraEdgeMargin = 18.5f; // distancia al fondo donde la cámara deja de avanzar (cancha grande)
        private const float FullPitchWidth = 68f;
        private const float MinCameraScale = 0.45f;
        private const float WallHeight = 1.1f;
        private const float CameraFollowSpeed = 3f;
        private const float CameraFov = 40f;
        private const float ArrowHeight = 2.7f;
        private const float ArrowBob = 0.12f;
        private const float ReferenceTopSpeed = 9f;
        private const float BoardDistance = 4f;
        private const float BoardHeight = 0.9f;
        private const float GoalDepth = 1.6f;

        private static readonly Color[] SkinTones =
        {
            new Color(0.96f, 0.80f, 0.69f), new Color(0.87f, 0.67f, 0.51f), new Color(0.76f, 0.57f, 0.42f),
            new Color(0.55f, 0.38f, 0.26f), new Color(0.36f, 0.24f, 0.17f)
        };

        private static readonly Color[] HairColors =
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
        private const float SetPieceLookAhead = 12f;
        private const float SetPieceZoom = 1.25f;
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
        private const float FocusZoom = 0.55f;
        private Confetti _confetti;
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
        private const float ZoomOut = 1.15f;           // balón largo o en el medio: se ve más campo
        private const float ZoomIn = 0.88f;            // en el último tercio: más cerca para definir
        private const float ZoomLongBallSpeed = 15f;
        private const float ZoomSpeed = 1.5f;
        private const float LookAheadSeconds = 0.7f;   // la cámara se adelanta hacia donde corre el poseedor
        private const float LookAheadMax = 6f;

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
        public MatchView(Pitch pitch, KitColors homeKit, KitColors awayKit, int playersPerTeam, int tickRate)
        {
            _pitch = pitch;
            _tickRate = tickRate;
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
            Safe("jugadores", () => BuildPlayers(homeKit, awayKit));
            Safe("balón", BuildBall);
            Safe("indicadores", BuildIndicators);
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
            return cam; // el AudioListener único está en la cámara de los menús
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

        private static Vector3 ToUnity(float x, float y, float height) => new Vector3(x, height, y);

        private static float ToEngineX(Vector3 unity) => unity.x;

        private static float ToEngineY(Vector3 unity) => unity.z;

        // ---- Campo y estadio ---------------------------------------------------------------------------------------

        private void BuildPitch()
        {
            float halfL = _pitch.HalfLength;
            float halfW = _pitch.HalfWidth;

            // Fuera del campo, un verde más oscuro: el límite se distingue aunque la línea quede lejos.
            var apron = new MeshBuilder();
            apron.Quad(-halfL - 12f, -halfW - 10f, halfL + 12f, halfW + 10f, 0f);
            AddMesh("Contorno", apron, new Color(0.07f, 0.27f, 0.1f));

            var grass = new MeshBuilder();
            grass.Quad(-halfL, -halfW, halfL, halfW, StripeHeight * 0.5f);
            AddMesh("Cesped", grass, new Color(0.11f, 0.42f, 0.15f));

            var stripes = new MeshBuilder();
            float band = _pitch.Length / StripeCount;
            for (int i = 0; i < StripeCount; i += 2)
            {
                stripes.Quad(-halfL + i * band, -halfW, -halfL + (i + 1) * band, halfW, StripeHeight);
            }

            AddMesh("Franjas", stripes, new Color(0.14f, 0.5f, 0.19f));

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
            Color[] boards = { new Color(0.1f, 0.25f, 0.7f), new Color(0.85f, 0.85f, 0.88f), new Color(0.75f, 0.12f, 0.12f) };

            // Carteles publicitarios alrededor del campo.
            const int segments = 8;
            for (int i = 0; i < (walls ? 0 : segments); i++) // en la cancha cerrada las paredes hacen de carteles
            {
                float x0 = -halfL + i * (2f * halfL / segments);
                float length = 2f * halfL / segments - 0.3f;
                Color c = boards[i % boards.Length];
                Shape("Cartel", MeshKit.Box, c, new Vector3(x0 + length * 0.5f, BoardHeight * 0.5f, -halfW), new Vector3(length, BoardHeight, 0.15f));
                Shape("Cartel", MeshKit.Box, c, new Vector3(x0 + length * 0.5f, BoardHeight * 0.5f, halfW), new Vector3(length, BoardHeight, 0.15f));
            }

            // Gradas escalonadas detrás de los carteles (tonos variados para sugerir público).
            Color[] tiers = { new Color(0.32f, 0.12f, 0.14f), new Color(0.14f, 0.2f, 0.38f), new Color(0.3f, 0.3f, 0.34f) }; // tonos de público
            for (int t = 0; t < tiers.Length; t++)
            {
                float offset = 6f + t * 7f;
                float height = 2.5f + t * 4f;
                Shape("Grada", MeshKit.Box, tiers[t], new Vector3(0f, height * 0.5f, halfW + offset), new Vector3(2f * halfL + 30f, height, 7f));
                Shape("Grada", MeshKit.Box, tiers[t], new Vector3(-halfL - offset, height * 0.5f, 0f), new Vector3(7f, height, 2f * halfW + 20f));
                Shape("Grada", MeshKit.Box, tiers[t], new Vector3(halfL + offset, height * 0.5f, 0f), new Vector3(7f, height, 2f * halfW + 20f));
            }
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
                Shape("Pared", MeshKit.Box, wall, new Vector3(0f, WallHeight * 0.5f, z), new Vector3(2f * halfL + 2f * thickness, WallHeight, thickness));
                Shape("Franja", MeshKit.Box, trim, new Vector3(0f, WallHeight + 0.03f, z), new Vector3(2f * halfL + 2f * thickness, 0.06f, thickness + 0.02f));
            }

            float sideLength = halfW - _pitch.GoalHalfWidth;
            for (int end = -1; end <= 1; end += 2)
            {
                float x = end * (halfL + thickness * 0.5f);
                for (int side = -1; side <= 1; side += 2)
                {
                    float z = side * (_pitch.GoalHalfWidth + sideLength * 0.5f);
                    Shape("Pared", MeshKit.Box, wall, new Vector3(x, WallHeight * 0.5f, z), new Vector3(thickness, WallHeight, sideLength));
                    Shape("Franja", MeshKit.Box, trim, new Vector3(x, WallHeight + 0.03f, z), new Vector3(thickness + 0.02f, 0.06f, sideLength));
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
                Shape("Poste", MeshKit.Box, white, new Vector3(x, height * 0.5f, -halfGoal), new Vector3(post, height, post));
                Shape("Poste", MeshKit.Box, white, new Vector3(x, height * 0.5f, halfGoal), new Vector3(post, height, post));
                Shape("Larguero", MeshKit.Box, white, new Vector3(x, height, 0f), new Vector3(post, post, halfGoal * 2f + post));
                // Armazón de la red.
                Shape("Red", MeshKit.Box, net, new Vector3(back, height * 0.4f, -halfGoal), new Vector3(0.05f, height * 0.8f, 0.05f));
                Shape("Red", MeshKit.Box, net, new Vector3(back, height * 0.4f, halfGoal), new Vector3(0.05f, height * 0.8f, 0.05f));
                Shape("Red", MeshKit.Box, net, new Vector3((x + back) * 0.5f, height, -halfGoal), new Vector3(GoalDepth, 0.04f, 0.04f));
                Shape("Red", MeshKit.Box, net, new Vector3((x + back) * 0.5f, height, halfGoal), new Vector3(GoalDepth, 0.04f, 0.04f));
                Shape("Red", MeshKit.Box, net, new Vector3((x + back) * 0.5f, height * 0.995f, 0f), new Vector3(GoalDepth, 0.02f, halfGoal * 2f));
                Transform backNet = Shape("Red", MeshKit.Box, net, new Vector3(back, height * 0.4f, 0f), new Vector3(0.02f, height * 0.8f, halfGoal * 2f));
                int k = end < 0 ? 0 : 1;
                _netBack[k] = backNet;
                _netRest[k] = backNet.localPosition;
            }
        }

        // ---- Jugadores, balón e indicadores -----------------------------------------------------------------------------

        private void BuildPlayers(KitColors homeKit, KitColors awayKit)
        {
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
                    Boots = new Color(0.08f, 0.08f, 0.08f)
                };
                _players[i] = new PlayerModel(_root.transform, "Jugador" + i, look, i * 0.7f);
            }
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
            AnimateNets(deltaTime);
            float bx = Mathf.Lerp(previous.BallX, current.BallX, alpha);
            float by = Mathf.Lerp(previous.BallY, current.BallY, alpha);
            float bz = Mathf.Lerp(previous.BallZ, current.BallZ, alpha);

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
                model.Pose(ToUnity(x, y, 0f), heading, speed, ReferenceTopSpeed, current.PlayerAction[i], -relative, relative < 0f, deltaTime);
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
            float aheadX = _cameraMode == 0 ? 0f : _lookAheadX; // la transmisión de TV no se adelanta
            float aheadZ = _cameraMode == 0 ? 0f : _lookAheadZ;
            _cameraX = Mathf.Lerp(_cameraX, Mathf.Clamp((ballX + aheadX) * CameraFollowX, -maxX, maxX), follow);
            _cameraZ = Mathf.Lerp(_cameraZ, (ballY + aheadZ) * CameraFollowZ, follow);
            float baseZoom = _cameraMode == 0 ? 1f : _cameraMode == 2 ? _zoom * CloseZoom : _zoom;
            float zoom = _focusPlayer >= 0 ? FocusZoom : _setPieceView ? SetPieceZoom : baseZoom;
            float scale = _cameraScale * zoom;
            Transform t = _camera.transform;
            var position = new Vector3(_cameraX, CameraHeight * scale, _cameraZ - CameraDistance * scale);
            var lookAt = new Vector3(_cameraX, 0f, _cameraZ + 2f);
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

        private int _cameraMode = 1;
        private const float CloseZoom = 0.75f;

        /// <summary>Tipo de cámara: 0 transmisión de TV (fija, sin zoom ni anticipación), 1 dinámica, 2 cercana.</summary>
        public void SetCameraMode(int mode)
        {
            _cameraMode = mode;
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

        /// <summary>Elimina todo lo creado.</summary>
        public void Destroy()
        {
            Object.Destroy(_root);
            for (int i = 0; i < _meshes.Count; i++)
            {
                Object.Destroy(_meshes[i]); // las mallas creadas por código no se liberan solas con el objeto
            }

            _meshes.Clear();
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
