#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Football.Game.Editor
{
    /// <summary>Menú para generar el archivo .apk que se instala en el teléfono Android.</summary>
    public static class BuildAndroid
    {
        private const string ApkPath = "Builds/FootballGame.apk";
        // Fuera de Assets/Football: ACTUALIZAR_JUEGO.bat sincroniza esa carpeta con /MIR y borraría la escena.
        private const string SceneFolder = "Assets/FootballGenerated";
        private const string ScenePath = SceneFolder + "/Main.unity";

        /// <summary>Configura el proyecto para Android y genera el .apk.</summary>
        [MenuItem("Football/Construir APK para Android")]
        public static void Build()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUtility.DisplayDialog("Football",
                    "Primero cambia la plataforma a Android: menú File > Build Profiles (o Build Settings), elige Android y pulsa \"Switch Platform\". Después vuelve a ejecutar esta opción.",
                    "Entendido");
                return;
            }

            FootballProjectSetup.EnsureMaterial();

            PlayerSettings.companyName = "Personal";
            PlayerSettings.productName = "Football Game";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.personal.footballgame");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            EditorUserBuildSettings.buildAppBundle = false;

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled && File.Exists(s.path)).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                scenes = new[] { CreateEmptyScene() };
            }

            Directory.CreateDirectory("Builds");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            });

            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log("Football: APK generado en " + Path.GetFullPath(ApkPath) + " (" + (report.summary.totalSize / 1048576) + " MB)");
                EditorUtility.RevealInFinder(ApkPath);
            }
            else
            {
                Debug.LogError("Football: falló la compilación (" + report.summary.result + "). Revisa los errores de la consola.");
            }
        }

        private static string CreateEmptyScene()
        {
            if (!AssetDatabase.IsValidFolder(SceneFolder))
            {
                AssetDatabase.CreateFolder("Assets", "FootballGenerated");
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            return ScenePath;
        }
    }
}
#endif
