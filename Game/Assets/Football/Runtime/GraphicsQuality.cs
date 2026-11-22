using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace Football.Game
{
    /// <summary>
    /// Perfiles gráficos. En URP la resolución de render y el suavizado (MSAA) viven en el asset del pipeline; se cambian por
    /// reflexión para no depender en tiempo de compilación del paquete URP (el juego funciona con cualquier plantilla).
    /// Bajo: 70 % de resolución sin suavizado (teléfonos de gama baja como el A13). Medio: 85 % y MSAA 2x. Alto: 100 % y 4x.
    /// </summary>
    public static class GraphicsQuality
    {
        private static readonly float[] RenderScale = { 0.7f, 0.85f, 1f };
        private static readonly int[] MsaaSamples = { 1, 2, 4 };

        private static readonly float[] ResolutionOverride = { -1f, 0.75f, 1f };

        /// <summary>Aplica un perfil (0 bajo, 1 medio, 2 alto) y, si se eligió, una escala de resolución propia.</summary>
        /// <param name="level">Perfil.</param>
        /// <param name="resolution">0 la del perfil, 1 75 %, 2 100 %.</param>
        public static void Apply(int level, int resolution = 0)
        {
            level = Mathf.Clamp(level, 0, RenderScale.Length - 1);
            int samples = MsaaSamples[level];
            QualitySettings.antiAliasing = samples > 1 ? samples : 0; // pipeline integrado
            try
            {
                RenderPipelineAsset pipeline = QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline;
                if (pipeline == null)
                {
                    return;
                }

                SetProperty(pipeline, "msaaSampleCount", samples);
                float scale = ResolutionOverride[Mathf.Clamp(resolution, 0, ResolutionOverride.Length - 1)];
                SetProperty(pipeline, "renderScale", scale > 0f ? scale : RenderScale[level]);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Football: no se pudo aplicar el perfil gráfico (" + e.Message + ")");
            }
        }

        private static void SetProperty(object target, string name, object value)
        {
            PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property != null && property.CanWrite)
            {
                property.SetValue(target, value);
            }
        }
    }
}
