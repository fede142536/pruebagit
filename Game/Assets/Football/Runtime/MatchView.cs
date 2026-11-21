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
        private const float LineWidth = 0.13f;
        private const float LineHeight = 0.02f;
        private const float StripeHeight = 0.01f;
        private const int CircleSegments = 40;
        private const int StripeCount = 14;
        private const float BallVisualSize = 0.3f;
        private const float CameraHeight = 21f;
        private const float CameraDistance = 30f;
        private const float CameraFollowX = 0.8f;
        private const float CameraFollowZ = 0.35f;
        private const float CameraMaxX = 34f;
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
        private readonly PlayerModel[] _players = new PlayerModel[MatchSnapshot.PlayerCount];
        private Camera _camera;
        private Transform _ball;
        private Transform _ballShadow;
        private Transform _controlArrow;
        private Transform _controlRing;
        private Transform _targetRing;
        private float _cameraX;
        private float _cameraZ;
        private float _clock;
        private Vector3 _lastBall;

        /// <summary>Errores ocurridos al construir la escena (vacío si todo fue bien).</summary>
        public string BuildErrors { get; private set; } = string.Empty;

        /// <summary>Crea toda la escena del partido.</summary>
        /// <param name="pitch">Geometría del campo.</param>
        /// <param name="homeKit">Color del equipo local.</param>
        /// <param name="awayKit">Color del equipo visitante.</param>
        public MatchView(Pitch pitch, Color homeKit, Color awayKit)
        {
            _pitch = pitch;
            _root = new GameObject("MatchView");
            Safe("cámara", () => _camera = CreateCamera());
            Safe("campo", BuildPitch);
            Safe("porterías", BuildGoals);
            Safe("estadio", BuildStadium);
            Safe("jugadores", () => BuildPlayers(homeKit, awayKit));
            Safe("balón", BuildBall);
            Safe("indicadores", BuildIndicators);
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
            // Se apagan las cámaras que traiga la escena de plantilla.
            Camera[] existing = Camera.allCameras;
            for (int i = 0; i < existing.Length; i++)
            {
                existing[i].enabled = false;
            }

            var go = new GameObject("CamaraPartido");
            go.transform.SetParent(_root.transform, false);
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.45f, 0.62f, 0.8f);
            cam.fieldOfView = CameraFov;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 300f;
            go.AddComponent<AudioListener>();
            return cam;
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

        // ---- Campo y estadio ---------------------------------------------------------------------------------------

        private void BuildPitch()
        {
            float halfL = _pitch.HalfLength;
            float halfW = _pitch.HalfWidth;

            var grass = new MeshBuilder();
            grass.Quad(-halfL - 12f, -halfW - 10f, halfL + 12f, halfW + 10f, 0f);
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
                Rectangle(lines, Mathf.Min(goalLine, boxX), -9.16f, Mathf.Max(goalLine, boxX), 9.16f);
                float spotX = end * (halfL - _pitch.Config.PenaltySpotDistance);
                lines.Quad(spotX - 0.15f, -0.15f, spotX + 0.15f, 0.15f, LineHeight);
            }

            lines.Quad(-0.15f, -0.15f, 0.15f, 0.15f, LineHeight);
            AddMesh("Lineas", lines, new Color(0.95f, 0.97f, 0.95f));
        }

        private void BuildStadium()
        {
            float halfL = _pitch.HalfLength + BoardDistance;
            float halfW = _pitch.HalfWidth + BoardDistance;
            Color[] boards = { new Color(0.1f, 0.25f, 0.7f), new Color(0.85f, 0.85f, 0.88f), new Color(0.75f, 0.12f, 0.12f) };

            // Carteles publicitarios alrededor del campo.
            const int segments = 8;
            for (int i = 0; i < segments; i++)
            {
                float x0 = -halfL + i * (2f * halfL / segments);
                float length = 2f * halfL / segments - 0.3f;
                Color c = boards[i % boards.Length];
                Shape("Cartel", MeshKit.Box, c, new Vector3(x0 + length * 0.5f, BoardHeight * 0.5f, -halfW), new Vector3(length, BoardHeight, 0.15f));
                Shape("Cartel", MeshKit.Box, c, new Vector3(x0 + length * 0.5f, BoardHeight * 0.5f, halfW), new Vector3(length, BoardHeight, 0.15f));
            }

            // Gradas escalonadas detrás de los carteles (tonos variados para sugerir público).
            Color[] tiers = { new Color(0.22f, 0.24f, 0.3f), new Color(0.3f, 0.28f, 0.34f), new Color(0.26f, 0.3f, 0.38f) };
            for (int t = 0; t < tiers.Length; t++)
            {
                float offset = 6f + t * 7f;
                float height = 2.5f + t * 4f;
                Shape("Grada", MeshKit.Box, tiers[t], new Vector3(0f, height * 0.5f, halfW + offset), new Vector3(2f * halfL + 30f, height, 7f));
                Shape("Grada", MeshKit.Box, tiers[t], new Vector3(-halfL - offset, height * 0.5f, 0f), new Vector3(7f, height, 2f * halfW + 20f));
                Shape("Grada", MeshKit.Box, tiers[t], new Vector3(halfL + offset, height * 0.5f, 0f), new Vector3(7f, height, 2f * halfW + 20f));
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
            go.AddComponent<MeshFilter>().sharedMesh = builder.ToMesh();
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
                Shape("Red", MeshKit.Box, net, new Vector3(back, height * 0.4f, 0f), new Vector3(0.02f, height * 0.8f, halfGoal * 2f));
            }
        }

        // ---- Jugadores, balón e indicadores -----------------------------------------------------------------------------

        private void BuildPlayers(Color homeKit, Color awayKit)
        {
            for (int i = 0; i < _players.Length; i++)
            {
                bool home = i < 11;
                bool keeper = i % 11 == 0;
                Color kit = home ? homeKit : awayKit;
                if (keeper)
                {
                    kit = home ? new Color(0.95f, 0.85f, 0.1f) : new Color(0.15f, 0.75f, 0.35f);
                }

                float brightness = kit.r * 0.3f + kit.g * 0.59f + kit.b * 0.11f;
                var look = new PlayerLook
                {
                    Shirt = kit,
                    Shorts = brightness > 0.6f ? new Color(0.12f, 0.12f, 0.16f) : new Color(0.95f, 0.95f, 0.95f),
                    Socks = kit,
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
        }

        private void BuildIndicators()
        {
            _controlArrow = Shape("FlechaControl", MeshKit.Arrow, new Color(1f, 0.85f, 0.05f), Vector3.zero, new Vector3(0.55f, 0.6f, 0.55f));
            _controlRing = Shape("AroControl", MeshKit.Disc, new Color(1f, 0.85f, 0.05f), Vector3.zero, new Vector3(1.6f, 1f, 1.6f));
            _targetRing = Shape("AroDestino", MeshKit.Disc, Color.white, Vector3.zero, new Vector3(1.4f, 1f, 1.4f));
        }

        // ---- Dibujo por fotograma ---------------------------------------------------------------------------------

        /// <summary>Dibuja el estado interpolado entre dos instantáneas.</summary>
        /// <param name="previous">Instantánea anterior.</param>
        /// <param name="current">Instantánea actual.</param>
        /// <param name="alpha">Fracción entre ambas (0..1).</param>
        /// <param name="controlled">Índice del jugador controlado, o -1.</param>
        /// <param name="passTarget">Índice del receptor previsto del pase, o -1.</param>
        /// <param name="deltaTime">Tiempo del fotograma.</param>
        public void Render(MatchSnapshot previous, MatchSnapshot current, float alpha, int controlled, int passTarget, float deltaTime)
        {
            _clock += deltaTime;
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
            }

            if (_camera != null)
            {
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
            float follow = 1f - Mathf.Exp(-CameraFollowSpeed * deltaTime);
            _cameraX = Mathf.Lerp(_cameraX, Mathf.Clamp(ballX * CameraFollowX, -CameraMaxX, CameraMaxX), follow);
            _cameraZ = Mathf.Lerp(_cameraZ, ballY * CameraFollowZ, follow);
            Transform t = _camera.transform;
            t.localPosition = new Vector3(_cameraX, CameraHeight, _cameraZ - CameraDistance);
            t.LookAt(new Vector3(_cameraX, 0f, _cameraZ + 2f));
        }

        /// <summary>Elimina todo lo creado.</summary>
        public void Destroy()
        {
            Object.Destroy(_root);
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
