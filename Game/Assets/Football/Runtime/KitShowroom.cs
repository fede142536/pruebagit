using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Football.Core.Domain;
using Football.Core.MatchCore;

namespace Football.Game
{
    /// <summary>
    /// Vitrina de la pantalla de equipos (estilo EA FC): un jugador 3D de cuerpo entero con la equipación elegida, girando
    /// despacio. Lo dibuja una cámara propia sobre una textura que se muestra en la interfaz, así no se mezcla con el partido
    /// de fondo del menú. Sin lógica de juego: solo arma la figura y la hace girar.
    /// </summary>
    public sealed class KitShowroom
    {
        private const int TextureWidth = 384;
        private const int TextureHeight = 640;
        private const float SpinDegreesPerSecond = 28f;
        private const float CameraDistance = 4.6f;
        private const float CameraHeight = 1.05f;
        private const float LookHeight = 0.95f;
        private const float CameraFov = 30f;
        private const float CameraFarClip = 9f; // solo ve su propia figura (las vitrinas y el menú están mucho más lejos)
        private const float ShowroomDepth = -400f;
        private const float ShowroomSpacing = 40f;
        private const float ReferenceTopSpeed = 9f;

        private readonly GameObject _root;
        private readonly Camera _camera;
        private readonly RenderTexture _texture;
        private readonly RawImage _image;
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly Color _skin, _hair;
        private PlayerModel _model;
        private float _heading;
        private KitColors _kit;
        private bool _hasKit;

        /// <summary>Crea la vitrina y el recuadro de la interfaz donde se ve.</summary>
        /// <param name="uiParent">Padre de la interfaz (área de diseño de la pantalla).</param>
        /// <param name="center">Centro del recuadro en la interfaz.</param>
        /// <param name="size">Tamaño del recuadro.</param>
        /// <param name="index">Número de vitrina (cada una se coloca en un lugar distinto del mundo).</param>
        /// <param name="startHeading">Orientación inicial (grados), para que las dos no giren sincronizadas.</param>
        public KitShowroom(Transform uiParent, Vector2 center, Vector2 size, int index, float startHeading)
        {
            _root = new GameObject("Vitrina" + index);
            _root.transform.position = new Vector3(index * ShowroomSpacing, ShowroomDepth, 0f);
            _heading = startHeading;
            _skin = MatchView.SkinTones[(index * 3 + 1) % MatchView.SkinTones.Length];
            _hair = MatchView.HairColors[(index * 2) % MatchView.HairColors.Length];

            _texture = new RenderTexture(TextureWidth, TextureHeight, 16) { antiAliasing = 2 };
            _texture.Create();
            var cameraObject = new GameObject("CamaraVitrina");
            cameraObject.transform.SetParent(_root.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, CameraHeight, -CameraDistance);
            cameraObject.transform.LookAt(_root.transform.position + new Vector3(0f, LookHeight, 0f));
            _camera = cameraObject.AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0f, 0f, 0f, 0f); // fondo transparente: se ve el panel de la interfaz
            _camera.fieldOfView = CameraFov;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = CameraFarClip;
            _camera.allowHDR = false;
            _camera.targetTexture = _texture;
            _camera.targetDisplay = 0;
            _camera.enabled = false;

            // Pedestal: un disco claro bajo los pies.
            var pedestal = new GameObject("Pedestal");
            pedestal.transform.SetParent(_root.transform, false);
            pedestal.transform.localScale = new Vector3(1.6f, 1f, 1.6f);
            pedestal.AddComponent<MeshFilter>().sharedMesh = MeshKit.Disc;
            var renderer = pedestal.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Materials.Get(new Color(0.85f, 0.88f, 0.95f, 1f));
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            var imageObject = new GameObject("Jugador3D", typeof(RectTransform));
            imageObject.transform.SetParent(uiParent, false);
            var rect = (RectTransform)imageObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = size;
            _image = imageObject.AddComponent<RawImage>();
            _image.texture = _texture;
            _image.raycastTarget = false;
        }

        /// <summary>Viste a la figura con una equipación (solo la reconstruye si cambió).</summary>
        /// <param name="kit">Equipación.</param>
        public void SetKit(KitColors kit)
        {
            if (_hasKit && _kit.Distance(kit) <= 0f && _kit.Pattern == kit.Pattern)
            {
                return;
            }

            _kit = kit;
            _hasKit = true;
            DestroyModel();
            var look = new PlayerLook
            {
                Shirt = kit.Shirt,
                Secondary = kit.Secondary,
                Pattern = kit.Pattern,
                Shorts = kit.Shorts,
                Socks = kit.Socks,
                Skin = _skin,
                Hair = _hair,
                Boots = new Color(0.08f, 0.08f, 0.08f)
            };
            _model = new PlayerModel(_root.transform, "Figura", look, 0f, 1.02f, _meshes);
        }

        /// <summary>Activa o desactiva la cámara (solo dibuja mientras la pantalla está a la vista).</summary>
        /// <param name="visible">Verdadero para dibujar.</param>
        public void SetVisible(bool visible)
        {
            _camera.enabled = visible;
        }

        /// <summary>Hace girar la figura.</summary>
        /// <param name="deltaTime">Tiempo del fotograma (s).</param>
        public void Update(float deltaTime)
        {
            if (_model == null)
            {
                return;
            }

            _heading += SpinDegreesPerSecond * deltaTime;
            if (_heading >= 360f)
            {
                _heading -= 360f;
            }

            _model.Pose(Vector3.zero, _heading, 0f, ReferenceTopSpeed, PlayerAction.Idle, 0f, false, deltaTime);
        }

        /// <summary>Libera la figura, la cámara y la textura.</summary>
        public void Dispose()
        {
            DestroyModel();
            _texture.Release();
            Object.Destroy(_texture);
            Object.Destroy(_root);
            Object.Destroy(_image.gameObject);
        }

        private void DestroyModel()
        {
            if (_model != null)
            {
                Object.Destroy(_model.Root.gameObject);
                _model = null;
            }

            for (int i = 0; i < _meshes.Count; i++)
            {
                Object.Destroy(_meshes[i]);
            }

            _meshes.Clear();
        }
    }
}
