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
