using System.Collections.Generic;
using Football.Core.Data;
using Football.Core.MatchCore;
using UnityEngine;
using UnityEngine.Rendering;

namespace Football.Game
{
    /// <summary>
    /// Portada estilo consola de POTRERO: el túnel de un estadio de noche, con la salida al campo iluminada al fondo y tres
    /// jugadores de cuerpo entero con camisetas de clubes argentinos. El del medio lleva siempre la del club favorito; los de
    /// los costados van cambiando de club. Es solo decorado: sin motor de partido, liviano para gama baja.
    /// </summary>
    public sealed class TitleStage
    {
        private const int Figures = 3;
        private const float FigureSpacing = 1.35f;
        private const float SideFigureDepth = 0.6f;
        private const float RotateSeconds = 6f;
        private const float FacingCamera = -90f; // en grados del motor: mirando hacia la cámara (−Z de Unity)
        private const float SideTurn = 14f;
        private const float CameraHeight = 1.25f;
        private const float CameraBack = 5.6f;
        private const float LookHeight = 1.0f;
        private const float CameraFov = 36f;
        private const float SwayAmount = 0.35f;
        private const float SwaySpeed = 0.18f;
        private const float BreathSpeed = 0.9f;
        private const float ReferenceTopSpeed = 9f;
        private const float TunnelHalfWidth = 3.4f;
        private const float TunnelHeight = 4.2f;
        private const float TunnelStart = -8f;
        private const float TunnelEnd = 12f;
        private const float LightStep = 2.5f;
        private const float PitchDepth = 40f;

        private static readonly Color Concrete = new Color(0.13f, 0.13f, 0.15f);
        private static readonly Color Floor = new Color(0.07f, 0.075f, 0.085f);
        private static readonly Color Ceiling = new Color(0.05f, 0.05f, 0.06f);
        private static readonly Color Lamp = new Color(1f, 0.96f, 0.84f);
        private static readonly Color Glow = new Color(0.78f, 0.88f, 1f);
        private static readonly Color Grass = new Color(0.16f, 0.46f, 0.2f);
        private static readonly Color Night = new Color(0.015f, 0.02f, 0.04f);

        private readonly Dictionary<Color, MeshMerge> _batches = new Dictionary<Color, MeshMerge>();
        private readonly List<Mesh> _sceneMeshes = new List<Mesh>();
        private readonly List<Mesh>[] _figureMeshes = new List<Mesh>[Figures];
        private readonly PlayerModel[] _models = new PlayerModel[Figures];
        private readonly int[] _clubs = new int[Figures];
        private GameObject _root;
        private Camera _camera;
        private float _timer, _clock;
        private int _favorite, _next;

        /// <summary>La portada está en pantalla.</summary>
        public bool Running => _root != null;

        /// <summary>Clubes que lleva cada figura (izquierda, centro, derecha), índices de <see cref="TeamCatalog.Teams"/>.</summary>
        public int[] Clubs => _clubs;

        /// <summary>Cambió algún club desde el último aviso (las tarjetas del menú se actualizan).</summary>
        public bool ClubsChanged { get; private set; }

        /// <summary>Arma la portada (si no estaba) y viste al del medio con el club favorito.</summary>
        /// <param name="favoriteTeam">Club favorito (índice del catálogo).</param>
        public void Start(int favoriteTeam)
        {
            if (_root != null)
            {
                if (favoriteTeam != _favorite)
                {
                    _favorite = favoriteTeam;
                    Dress(1, _favorite);
                }

                return;
            }

            try
            {
                _favorite = favoriteTeam;
                _root = new GameObject("PortadaPotrero");
                BuildTunnel();
                CreateCamera();
                TeamDefinition[] teams = TeamCatalog.Teams;
                _next = (_favorite + 1) % teams.Length;
                for (int i = 0; i < Figures; i++)
                {
                    _figureMeshes[i] = new List<Mesh>();
                    Dress(i, i == 1 ? _favorite : NextClub());
                }
            }
            catch (System.Exception e)
            {
                // La portada es decorativa: si algo falla, el menú sigue sobre el color liso de la cámara de menús.
                Debug.LogWarning("Football: no se pudo crear la portada (" + e.Message + ")");
                Stop();
            }
        }

        /// <summary>Libera la portada y devuelve la pantalla a la cámara de los menús.</summary>
        public void Stop()
        {
            for (int i = 0; i < Figures; i++)
            {
                DestroyFigure(i);
            }

            for (int i = 0; i < _sceneMeshes.Count; i++)
            {
                Object.Destroy(_sceneMeshes[i]);
            }

            _sceneMeshes.Clear();
            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
            }

            _camera = null;
            if (GameRoot.MenuCamera != null)
            {
                GameRoot.MenuCamera.enabled = true;
            }
        }

        /// <summary>Anima las figuras, mueve la cámara y rota los clubes de los costados.</summary>
        /// <param name="deltaTime">Tiempo del fotograma (s).</param>
        public void Update(float deltaTime)
        {
            ClubsChanged = false;
            if (_root == null)
            {
                return;
            }

            _clock += deltaTime;
            _timer += deltaTime;
            if (_timer >= RotateSeconds)
            {
                _timer = 0f;
                int side = ((int)(_clock / RotateSeconds) & 1) == 0 ? 0 : 2; // se turnan izquierda y derecha
                Dress(side, NextClub());
            }

            for (int i = 0; i < Figures; i++)
            {
                if (_models[i] == null)
                {
                    continue;
                }

                float sway = Mathf.Sin(_clock * BreathSpeed + i) * 3f; // pequeño balanceo, como quien espera salir a la cancha
                float heading = FacingCamera + (i - 1) * -SideTurn + sway;
                _models[i].Pose(FigurePosition(i), heading, 0f, ReferenceTopSpeed, PlayerAction.Idle, 0f, false, deltaTime);
            }

            float x = Mathf.Sin(_clock * SwaySpeed) * SwayAmount;
            _camera.transform.localPosition = new Vector3(x, CameraHeight, -CameraBack);
            _camera.transform.LookAt(new Vector3(x * 0.3f, LookHeight, 0f));
        }

        private static Vector3 FigurePosition(int i) => new Vector3((i - 1) * FigureSpacing, 0f, i == 1 ? 0f : SideFigureDepth);

        private int NextClub()
        {
            int count = TeamCatalog.Teams.Length;
            for (int tries = 0; tries < count; tries++)
            {
                int club = _next;
                _next = (_next + 1) % count;
                if (club != _favorite && club != _clubs[0] && club != _clubs[2])
                {
                    return club;
                }
            }

            return _next;
        }

        private void Dress(int slot, int club)
        {
            DestroyFigure(slot);
            TeamDefinition team = TeamCatalog.Teams[club];
            KitColors kit = KitColors.From(team.Home);
            var look = new PlayerLook
            {
                Shirt = kit.Shirt,
                Secondary = kit.Secondary,
                Pattern = kit.Pattern,
                Shorts = kit.Shorts,
                Socks = kit.Socks,
                Skin = MatchView.SkinTones[(club * 3 + slot) % MatchView.SkinTones.Length],
                Hair = MatchView.HairColors[(club + slot * 2) % MatchView.HairColors.Length],
                Boots = new Color(0.06f, 0.06f, 0.07f),
                HairStyle = (club + slot) % 5,
                Beard = ((club + slot) & 1) == 0
            };
            _models[slot] = new PlayerModel(_root.transform, "Figura" + slot, look, slot * 0.37f, 0.98f + 0.03f * slot, _figureMeshes[slot]);
            _clubs[slot] = club;
            ClubsChanged = true;
        }

        private void DestroyFigure(int slot)
        {
            if (_models[slot] != null)
            {
                Object.Destroy(_models[slot].Root.gameObject);
                _models[slot] = null;
            }

            List<Mesh> meshes = _figureMeshes[slot];
            for (int i = 0; meshes != null && i < meshes.Count; i++)
            {
                Object.Destroy(meshes[i]);
            }

            meshes?.Clear();
        }

        private void CreateCamera()
        {
            if (GameRoot.MenuCamera != null)
            {
                GameRoot.MenuCamera.enabled = false; // una sola cámara dibuja la pantalla
            }

            var go = new GameObject("CamaraPortada");
            go.transform.SetParent(_root.transform, false);
            _camera = go.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Night;
            _camera.fieldOfView = CameraFov;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = PitchDepth + TunnelEnd + 10f;
            _camera.allowHDR = false;
            _camera.allowMSAA = true;
            _camera.targetDisplay = 0;
        }

        /// <summary>Túnel de hormigón con tiras de luz, la salida iluminada y el césped del estadio al fondo.</summary>
        private void BuildTunnel()
        {
            float length = TunnelEnd - TunnelStart;
            float middle = (TunnelEnd + TunnelStart) * 0.5f;
            Piece(Floor, new Vector3(0f, -0.05f, middle), new Vector3(TunnelHalfWidth * 2f, 0.1f, length));
            Piece(Ceiling, new Vector3(0f, TunnelHeight, middle), new Vector3(TunnelHalfWidth * 2f, 0.1f, length));
            for (int side = -1; side <= 1; side += 2)
            {
                Piece(Concrete, new Vector3(side * TunnelHalfWidth, TunnelHeight * 0.5f, middle), new Vector3(0.1f, TunnelHeight, length));
                // Zócalo con la línea de color de la marca y tiras de luz verticales en las paredes.
                Piece(UiTheme.Accent, new Vector3(side * (TunnelHalfWidth - 0.06f), 0.35f, middle), new Vector3(0.02f, 0.06f, length));
                for (float z = TunnelStart + LightStep; z < TunnelEnd; z += LightStep * 2f)
                {
                    Piece(Lamp, new Vector3(side * (TunnelHalfWidth - 0.06f), TunnelHeight * 0.55f, z), new Vector3(0.02f, 1.6f, 0.12f));
                }
            }

            for (float z = TunnelStart + LightStep * 0.5f; z < TunnelEnd; z += LightStep)
            {
                Piece(Lamp, new Vector3(0f, TunnelHeight - 0.06f, z), new Vector3(2.2f, 0.03f, 0.25f));
            }

            // Salida al campo: marco iluminado, césped y torres de focos recortadas contra la noche.
            Piece(Glow, new Vector3(0f, TunnelHeight + 0.6f, TunnelEnd + 0.1f), new Vector3(TunnelHalfWidth * 2f + 2f, 1.2f, 0.1f));
            Piece(Grass, new Vector3(0f, -0.06f, TunnelEnd + PitchDepth * 0.5f), new Vector3(60f, 0.1f, PitchDepth));
            Piece(new Color(0.2f, 0.55f, 0.24f), new Vector3(0f, -0.05f, TunnelEnd + PitchDepth * 0.5f), new Vector3(60f, 0.1f, 3f));
            for (int side = -1; side <= 1; side += 2)
            {
                Piece(new Color(0.03f, 0.035f, 0.05f), new Vector3(side * 14f, 9f, TunnelEnd + 30f), new Vector3(0.6f, 18f, 0.6f));
                Piece(Lamp, new Vector3(side * 14f, 18.5f, TunnelEnd + 30f), new Vector3(4f, 2f, 0.3f));
            }

            Piece(new Color(0.05f, 0.06f, 0.09f), new Vector3(0f, 6f, TunnelEnd + PitchDepth), new Vector3(70f, 12f, 0.5f)); // tribuna
            foreach (KeyValuePair<Color, MeshMerge> batch in _batches)
            {
                Mesh mesh = batch.Value.Build();
                _sceneMeshes.Add(mesh);
                var go = new GameObject("Tunel");
                go.transform.SetParent(_root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = Materials.Get(batch.Key);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            _batches.Clear();
        }

        private void Piece(Color color, Vector3 position, Vector3 scale)
        {
            if (!_batches.TryGetValue(color, out MeshMerge batch))
            {
                batch = new MeshMerge();
                _batches[color] = batch;
            }

            batch.Add(MeshKit.Box, position, Quaternion.identity, scale, color);
        }
    }
}
