using System.Collections.Generic;
using UnityEngine;

namespace Football.Game
{
    /// <summary>
    /// Materiales planos sin iluminación (baratos para móviles de gama baja). Se parte de un material guardado en
    /// <c>Resources</c> —lo crea el script del editor— para que su shader entre en la compilación del juego.
    /// </summary>
    public static class Materials
    {
        private const string TemplateName = "FootballUnlit";
        private static Material _template;
        private static readonly Dictionary<int, Material> Cache = new Dictionary<int, Material>();

        /// <summary>Devuelve un material del color dado (se reutiliza entre llamadas).</summary>
        /// <param name="color">Color.</param>
        public static Material Get(Color color)
        {
            Color32 c = color;
            int key = c.r | (c.g << 8) | (c.b << 16) | (c.a << 24);
            if (Cache.TryGetValue(key, out Material cached))
            {
                return cached;
            }

            var material = new Material(Template());
            material.color = color;
            // Se dibujan ambas caras (las mallas generadas por código no dependen del orden de sus vértices).
            // No se activa el dibujo por instancias: exige variantes de shader que algunas tarjetas manejan mal
            // (posible causa de la pantalla rosa en el PC del usuario); el agrupado SRP de URP ya reduce el coste.
            material.SetFloat("_Cull", 0f);
            Cache[key] = material;
            return material;
        }

        private const string PlayerShaderName = "FootballPlayer";
        private const string CrowdShaderName = "FootballCrowd";
        private const string ShadowPassName = "SRPDefaultUnlit";
        /// <summary>
        /// Cola de dibujo de los jugadores: después de la cancha y del estadio (2000), así la sombra proyectada, que no escribe
        /// profundidad, no queda tapada por el césped que se dibuje más tarde.
        /// </summary>
        public const int PlayerRenderQueue = 2100;
        private static readonly Dictionary<int, Material> CrowdCache = new Dictionary<int, Material>();
        private static Shader _crowdShader;
        private static bool _crowdShaderChecked;
        private static bool _planarShadows = true;
        private static readonly int ShadowId = Shader.PropertyToID("_FootballShadow");
        private static readonly Dictionary<int, Material> PlayerCache = new Dictionary<int, Material>();
        private static Shader _playerShader;
        private static bool _playerShaderChecked;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int RimId = Shader.PropertyToID("_FootballRim");
        private static readonly int DirtId = Shader.PropertyToID("_FootballDirt");
        private static readonly int SweatId = Shader.PropertyToID("_FootballSweat");

        /// <summary>Verdadero si el shader de jugadores funciona en este dispositivo (permite agrupar piezas con color por vértice).</summary>
        public static bool PlayerShaderAvailable => PlayerShader() != null;

        /// <summary>
        /// Material para el cuerpo de los jugadores: luz de borde y suciedad (shader Football/Player).
        /// Si el dispositivo no admite ese shader, devuelve el material plano de <see cref="Get"/>.
        /// </summary>
        /// <param name="color">Color de la pieza.</param>
        public static Material GetPlayer(Color color)
        {
            Shader shader = PlayerShader();
            if (shader == null)
            {
                return Get(color);
            }

            Color32 c = color;
            int key = c.r | (c.g << 8) | (c.b << 16) | (c.a << 24);
            if (PlayerCache.TryGetValue(key, out Material cached))
            {
                return cached;
            }

            var material = new Material(shader);
            material.SetColor(BaseColorId, color);
            material.SetFloat("_Cull", 0f);
            material.renderQueue = PlayerRenderQueue;
            material.SetShaderPassEnabled(ShadowPassName, _planarShadows);
            PlayerCache[key] = material;
            return material;
        }

        /// <summary>
        /// Material del público, los bancos y los fotógrafos: el mismo cuerpo del shader de jugadores pero sin la pasada de sombra
        /// proyectada (<c>Football/Crowd</c>). Si ese shader no está, usa el de jugadores.
        /// </summary>
        /// <param name="color">Color base (blanco: los colores van por vértice).</param>
        public static Material GetCrowd(Color color)
        {
            if (!_crowdShaderChecked)
            {
                _crowdShaderChecked = true;
                Shader crowd = Resources.Load<Shader>(CrowdShaderName);
                _crowdShader = crowd != null && crowd.isSupported ? crowd : null;
            }

            if (_crowdShader == null)
            {
                return GetPlayer(color);
            }

            Color32 c = color;
            int key = c.r | (c.g << 8) | (c.b << 16) | (c.a << 24);
            if (CrowdCache.TryGetValue(key, out Material cached))
            {
                return cached;
            }

            var material = new Material(_crowdShader);
            material.SetColor(BaseColorId, color);
            material.SetFloat("_Cull", 0f);
            CrowdCache[key] = material;
            return material;
        }

        /// <summary>
        /// Enciende o apaga la sombra proyectada de los jugadores (calidad MEDIA y ALTA). Apagada, la pasada no se dibuja y el disco de
        /// contacto de cada jugador hace de sombra.
        /// </summary>
        /// <param name="enabled">Verdadero para encenderla.</param>
        public static void SetPlanarShadows(bool enabled)
        {
            _planarShadows = enabled;
            Shader.SetGlobalFloat(ShadowId, enabled ? 1f : 0f);
            foreach (Material m in PlayerCache.Values)
            {
                m.SetShaderPassEnabled(ShadowPassName, enabled);
            }
        }

        /// <summary>La sombra proyectada está encendida (los jugadores ocultan entonces su disco de contacto).</summary>
        public static bool PlanarShadowsEnabled => _planarShadows && PlayerShaderAvailable;

        /// <summary>Enciende o apaga la luz de borde de los jugadores (se apaga en calidad BAJA).</summary>
        /// <param name="enabled">Verdadero para encenderla.</param>
        public static void SetRim(bool enabled)
        {
            Shader.SetGlobalFloat(RimId, enabled ? 1f : 0f);
        }

        private static readonly int LightDirId = Shader.PropertyToID("_FootballLightDir");
        private static readonly int LightAmountId = Shader.PropertyToID("_FootballLightAmount");

        /// <summary>Luz direccional de los focos sobre jugadores y público (volumen sin luces reales).</summary>
        /// <param name="enabled">Falso en calidad baja (colores planos).</param>
        public static void SetLighting(bool enabled)
        {
            Vector3 toLight = new Vector3(-0.35f, 0.85f, -0.4f).normalized; // focos altos, desde el lado de la cámara
            Shader.SetGlobalVector(LightDirId, new Vector4(toLight.x, toLight.y, toLight.z, 0f));
            Shader.SetGlobalFloat(LightAmountId, enabled ? 1f : 0f);
        }

        /// <summary>Cuánta suciedad muestran botines y medias (0 limpio, 1 muy sucio).</summary>
        /// <param name="amount">Cantidad entre 0 y 1.</param>
        public static void SetDirt(float amount)
        {
            Shader.SetGlobalFloat(DirtId, Mathf.Clamp01(amount));
        }

        /// <summary>Cuánto brilla el sudor en la piel de los jugadores (0 al empezar, 1 al final).</summary>
        /// <param name="amount">Cantidad entre 0 y 1.</param>
        public static void SetSweat(float amount)
        {
            Shader.SetGlobalFloat(SweatId, Mathf.Clamp01(amount));
        }

        private static Shader PlayerShader()
        {
            if (_playerShaderChecked)
            {
                return _playerShader;
            }

            _playerShaderChecked = true;
            Shader shader = Resources.Load<Shader>(PlayerShaderName);
            if (shader == null || !shader.isSupported)
            {
                Debug.LogWarning("El shader de jugadores no está disponible o no es compatible; se usa el material plano.");
                return null;
            }

            _playerShader = shader;
            PlayerAtlas.Publish();
            Shader.SetGlobalFloat(ShadowId, _planarShadows ? 1f : 0f);
            return _playerShader;
        }

        private static Material Template()
        {
            if (_template != null)
            {
                return _template;
            }

            _template = Resources.Load<Material>(TemplateName);
            if (_template != null && (_template.shader == null || !_template.shader.isSupported))
            {
                Debug.LogWarning("El shader del material base no es compatible con esta tarjeta de video; se usa uno de reserva.");
                _template = null;
            }

            if (_template == null)
            {
                Debug.LogWarning("Se crea un material base de reserva (falta Resources/" + TemplateName + ".mat o no es compatible).");
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null || !shader.isSupported)
                {
                    shader = Shader.Find("Unlit/Color");
                }

                if (shader == null)
                {
                    shader = Shader.Find("Sprites/Default");
                }

                _template = new Material(shader);
            }

            return _template;
        }
    }
}
