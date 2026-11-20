#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Football.Game.Editor
{
    /// <summary>
    /// Crea el material base del juego la primera vez que se abre el proyecto. Tiene que existir como archivo dentro de
    /// <c>Resources</c> para que su shader se incluya al compilar el juego (si no, los objetos salen rosados en el móvil).
    /// </summary>
    [InitializeOnLoad]
    public static class FootballProjectSetup
    {
        private const string ResourcesFolder = "Assets/Football/Resources";
        private const string MaterialPath = ResourcesFolder + "/FootballUnlit.mat";

        static FootballProjectSetup()
        {
            EditorApplication.delayCall += EnsureMaterial;
        }

        /// <summary>Crea el material si no existe.</summary>
        [MenuItem("Football/Preparar proyecto")]
        public static void EnsureMaterial()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null)
            {
                return;
            }

            Shader shader = null;
            if (GraphicsSettings.currentRenderPipeline != null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                Debug.LogError("No se encontró un shader sin iluminación para crear el material del juego.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                AssetDatabase.CreateFolder("Assets/Football", "Resources");
            }

            AssetDatabase.CreateAsset(new Material(shader), MaterialPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Football: material base creado en " + MaterialPath);
        }
    }
}
#endif
