using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace Football.Game
{
    /// <summary>
    /// Ajustes gráficos que dependen del proyecto. El suavizado de bordes (MSAA) en URP vive en el asset del pipeline; se
    /// cambia por reflexión para no depender en tiempo de compilación del paquete URP (el juego funciona con cualquier
    /// plantilla). En las GPU de teléfono (por mosaicos) el MSAA 2x es barato.
    /// </summary>
    public static class GraphicsQuality
    {
        private const int SamplesOn = 2;
        private const int SamplesOff = 1;

        /// <summary>Activa o desactiva el suavizado de bordes.</summary>
        public static void SetAntialiasing(bool on)
        {
            int samples = on ? SamplesOn : SamplesOff;
            QualitySettings.antiAliasing = on ? SamplesOn : 0; // pipeline integrado
            try
            {
                RenderPipelineAsset pipeline = QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline;
                if (pipeline == null)
                {
                    return;
                }

                PropertyInfo property = pipeline.GetType().GetProperty("msaaSampleCount", BindingFlags.Public | BindingFlags.Instance);
                if (property != null && property.CanWrite)
                {
                    property.SetValue(pipeline, samples);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Football: no se pudo cambiar el suavizado de bordes (" + e.Message + ")");
            }
        }
    }
}
