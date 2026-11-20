using System.Collections.Generic;
using UnityEngine;
using Football.Core.MatchCore;

namespace Football.Game
{
    /// <summary>
    /// Dibuja el partido con formas simples (el arte definitivo se enchufará después sin tocar la lógica).
    /// Convierte las coordenadas del motor a Unity: X→X, Y→Z, Z (altura)→Y. Cámara de transmisión lateral alta.
    /// </summary>
    public sealed class MatchView
    {
        private const float LineWidth = 0.14f;
        private const float LineHeight = 0.02f;
        private const float StripeHeight = 0.01f;
        private const int CircleSegments = 36;
        private const int StripeCount = 14;
        private const float PlayerHeight = 1.8f;
        private const float PlayerWidth = 0.7f;
        private const float BallVisualSize = 0.38f;
        private const float MaxLeanDegrees = 12f;
        private const float CameraHeight = 27f;
        private const float CameraDistance = 38f;
        private const float CameraLookAhead = 3f;
        private const float CameraFollowX = 0.75f;
        private const float CameraMaxX = 30f;
        private const float CameraFollowSpeed = 3.5f;
        private const float CameraFov = 38f;

        private readonly GameObject _root;
        private readonly Camera _camera;
        private readonly Transform[] _players = new Transform[MatchSnapshot.PlayerCount];
        private readonly Transform _ball;
        private readonly Transform _ballShadow;
        private readonly Transform _controlRing;
        private readonly Transform _targetRing;
        private readonly Pitch _pitch;
        private float _cameraX;

        /// <summary>Crea toda la escena del partido.</summary>
        /// <param name="pitch">Geometría del campo.</param>
        /// <param name="homeKit">Color del equipo local.</param>
        /// <param name="awayKit">Color del equipo visitante.</param>
        public MatchView(Pitch pitch, Color homeKit, Color awayKit)
        {
            _pitch = pitch;
            _root = new GameObject("MatchView");
            _camera = CreateCamera();
            BuildPitch();
            BuildGoals();

            Color keeperHome = Color.Lerp(homeKit, Color.yellow, 0.7f);
            Color keeperAway = Color.Lerp(awayKit, Color.green, 0.7f);
            for (int i = 0; i < _players.Length; i++)
            {
                bool home = i < 11;
                bool keeper = i % 11 == 0;
                Color kit = home ? (keeper ? keeperHome : homeKit) : (keeper ? keeperAway : awayKit);
                _players[i] = CreatePlayer("Jugador" + i, kit);
            }

            _ball = Primitive(PrimitiveType.Sphere, "Balon", Color.white, new Vector3(BallVisualSize, BallVisualSize, BallVisualSize));
            _ballShadow = Primitive(PrimitiveType.Cylinder, "SombraBalon", new Color(0.04f, 0.17f, 0.06f), new Vector3(0.5f, 0.005f, 0.5f));
            _controlRing = Primitive(PrimitiveType.Cylinder, "AroControl", new Color(1f, 0.92f, 0.1f), new Vector3(1.5f, 0.01f, 1.5f));
            _targetRing = Primitive(PrimitiveType.Cylinder, "AroDestino", Color.white, new Vector3(1.3f, 0.012f, 1.3f));
            _cameraX = 0f;
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
            cam.backgroundColor = new Color(0.05f, 0.2f, 0.07f);
            cam.fieldOfView = CameraFov;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 250f;
            go.AddComponent<AudioListener>();
            return cam;
        }

        private Transform Primitive(PrimitiveType type, string name, Color color, Vector3 scale)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(_root.transform, false);
            go.transform.localScale = scale;
            Object.Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = Materials.Get(color);
            return go.transform;
        }

        private Transform CreatePlayer(string name, Color kit)
        {
            var root = new GameObject(name);
            root.transform.SetParent(_root.transform, false);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Cuerpo";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, PlayerHeight * 0.5f, 0f);
            body.transform.localScale = new Vector3(PlayerWidth, PlayerHeight * 0.5f, PlayerWidth);
            Object.Destroy(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial = Materials.Get(kit);

            // Una pieza en la parte delantera para ver hacia dónde mira.
            GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Frente";
            nose.transform.SetParent(root.transform, false);
            nose.transform.localPosition = new Vector3(0f, PlayerHeight * 0.75f, PlayerWidth * 0.5f);
            nose.transform.localScale = new Vector3(0.25f, 0.25f, 0.3f);
            Object.Destroy(nose.GetComponent<Collider>());
            nose.GetComponent<Renderer>().sharedMaterial = Materials.Get(Color.white);
            return root.transform;
        }

        private static Vector3 ToUnity(float x, float y, float height) => new Vector3(x, height, y);

        // ---- Campo -----------------------------------------------------------------------------------------------

        private void BuildPitch()
        {
            float halfL = _pitch.HalfLength;
            float halfW = _pitch.HalfWidth;

            var grass = new MeshBuilder();
            grass.Quad(-halfL - 25f, -halfW - 20f, halfL + 25f, halfW + 20f, 0f);
            AddMesh("Cesped", grass, new Color(0.09f, 0.40f, 0.13f));

            var stripes = new MeshBuilder();
            float band = _pitch.Length / StripeCount;
            for (int i = 0; i < StripeCount; i += 2)
            {
                stripes.Quad(-halfL + i * band, -halfW, -halfL + (i + 1) * band, halfW, StripeHeight);
            }

            AddMesh("Franjas", stripes, new Color(0.12f, 0.47f, 0.17f));

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
                lines.Quad(spotX - 0.18f, -0.18f, spotX + 0.18f, 0.18f, LineHeight);
            }

            lines.Quad(-0.18f, -0.18f, 0.18f, 0.18f, LineHeight);
            AddMesh("Lineas", lines, Color.white);
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
            Material material = Materials.Get(color);
            material.SetFloat("_Cull", 0f);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private void BuildGoals()
        {
            float post = 0.14f;
            float halfGoal = _pitch.GoalHalfWidth;
            float height = _pitch.GoalHeight;
            for (int end = -1; end <= 1; end += 2)
            {
                float x = end * _pitch.HalfLength;
                Primitive(PrimitiveType.Cube, "Poste", Color.white, new Vector3(post, height, post)).localPosition = new Vector3(x, height * 0.5f, -halfGoal);
                Primitive(PrimitiveType.Cube, "Poste", Color.white, new Vector3(post, height, post)).localPosition = new Vector3(x, height * 0.5f, halfGoal);
                Primitive(PrimitiveType.Cube, "Larguero", Color.white, new Vector3(post, post, halfGoal * 2f + post)).localPosition = new Vector3(x, height, 0f);
            }
        }

        // ---- Dibujo por fotograma ---------------------------------------------------------------------------------

        /// <summary>Dibuja el estado interpolado entre dos instantáneas.</summary>
        /// <param name="previous">Instantánea anterior.</param>
        /// <param name="current">Instantánea actual.</param>
        /// <param name="alpha">Fracción entre ambas (0..1).</param>
        /// <param name="controlled">Índice del jugador controlado, o -1.</param>
        /// <param name="passTarget">Índice del receptor previsto del pase, o -1.</param>
        /// <param name="topSpeed">Velocidad con la que se calcula la inclinación al correr (m/s).</param>
        /// <param name="deltaTime">Tiempo del fotograma.</param>
        public void Render(MatchSnapshot previous, MatchSnapshot current, float alpha, int controlled, int passTarget, float topSpeed, float deltaTime)
        {
            for (int i = 0; i < _players.Length; i++)
            {
                Transform t = _players[i];
                bool visible = !current.SentOff[i];
                if (t.gameObject.activeSelf != visible)
                {
                    t.gameObject.SetActive(visible);
                }

                if (!visible)
                {
                    continue;
                }

                float x = Mathf.Lerp(previous.PlayerX[i], current.PlayerX[i], alpha);
                float y = Mathf.Lerp(previous.PlayerY[i], current.PlayerY[i], alpha);
                float heading = Mathf.LerpAngle(previous.PlayerHeading[i] * Mathf.Rad2Deg, current.PlayerHeading[i] * Mathf.Rad2Deg, alpha);
                float speed = Mathf.Lerp(previous.PlayerSpeed[i], current.PlayerSpeed[i], alpha);
                float lean = Mathf.Clamp01(speed / topSpeed) * MaxLeanDegrees;
                if (current.PlayerAction[i] == PlayerAction.Tackle)
                {
                    lean = 55f;
                }

                t.localPosition = ToUnity(x, y, 0f);
                t.localRotation = Quaternion.Euler(lean, 90f - heading, 0f);
            }

            float bx = Mathf.Lerp(previous.BallX, current.BallX, alpha);
            float by = Mathf.Lerp(previous.BallY, current.BallY, alpha);
            float bz = Mathf.Lerp(previous.BallZ, current.BallZ, alpha);
            _ball.localPosition = ToUnity(bx, by, bz + BallVisualSize * 0.5f);
            float shadow = 0.5f * Mathf.Clamp(1f - bz * 0.08f, 0.3f, 1f);
            _ballShadow.localScale = new Vector3(shadow, 0.005f, shadow);
            _ballShadow.localPosition = ToUnity(bx, by, 0.015f);

            PlaceRing(_controlRing, previous, current, alpha, controlled);
            PlaceRing(_targetRing, previous, current, alpha, passTarget);

            UpdateCamera(bx, deltaTime);
        }

        private static void PlaceRing(Transform ring, MatchSnapshot previous, MatchSnapshot current, float alpha, int player)
        {
            bool show = player >= 0 && !current.SentOff[player];
            if (ring.gameObject.activeSelf != show)
            {
                ring.gameObject.SetActive(show);
            }

            if (show)
            {
                float x = Mathf.Lerp(previous.PlayerX[player], current.PlayerX[player], alpha);
                float y = Mathf.Lerp(previous.PlayerY[player], current.PlayerY[player], alpha);
                ring.localPosition = ToUnity(x, y, 0.03f);
            }
        }

        private void UpdateCamera(float ballX, float deltaTime)
        {
            float target = Mathf.Clamp(ballX * CameraFollowX, -CameraMaxX, CameraMaxX);
            _cameraX = Mathf.Lerp(_cameraX, target, 1f - Mathf.Exp(-CameraFollowSpeed * deltaTime));
            Transform t = _camera.transform;
            t.localPosition = new Vector3(_cameraX, CameraHeight, -CameraDistance);
            t.LookAt(new Vector3(_cameraX, 0f, CameraLookAhead));
        }

        /// <summary>Convierte una posición del campo a pantalla (para dibujar elementos de la interfaz sobre un jugador).</summary>
        public Vector3 WorldToScreen(float x, float y, float height) => _camera.WorldToScreenPoint(ToUnity(x, y, height));

        /// <summary>Elimina todo lo creado.</summary>
        public void Destroy()
        {
            Object.Destroy(_root);
        }

        // ---- Malla procedural --------------------------------------------------------------------------------------

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
