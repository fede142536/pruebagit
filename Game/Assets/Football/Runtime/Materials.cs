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
            Cache[key] = material;
            return material;
        }

        private static Material Template()
        {
            if (_template != null)
            {
                return _template;
            }

            _template = Resources.Load<Material>(TemplateName);
            if (_template == null)
            {
                Debug.LogWarning("Falta Resources/" + TemplateName + ".mat: ejecuta el menú Football > Preparar proyecto.");
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
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
