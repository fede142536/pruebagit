using System;
using UnityEngine;

namespace Football.Game
{
    /// <summary>Sección de los ajustes.</summary>
    public enum SettingCategory
    {
        /// <summary>Gráficos y rendimiento.</summary>
        Graphics,
        /// <summary>Audio.</summary>
        Audio,
        /// <summary>Controles y cámara.</summary>
        Controls,
        /// <summary>Reglas del partido.</summary>
        Rules
    }

    /// <summary>Un ajuste: texto a mostrar, cómo pasa al siguiente valor, sección y si se puede cambiar durante el partido.</summary>
    public sealed class SettingItem
    {
        /// <summary>Sección.</summary>
        public SettingCategory Category;
        /// <summary>Se puede cambiar en la pausa sin salir del partido.</summary>
        public bool Live;
        /// <summary>Texto con el valor actual.</summary>
        public Func<GameSettings, string> Text;
        /// <summary>Pasa al siguiente valor.</summary>
        public Action<GameSettings> Next;
    }

    /// <summary>
    /// Catálogo único de ajustes. El menú principal muestra todos por secciones; la pausa del partido, solo los que se pueden
    /// cambiar en vivo (audio, cámara, controles táctiles y calidad gráfica); las reglas del partido quedan bloqueadas.
    /// Los delegados se crean una sola vez al arrancar.
    /// </summary>
    public static class SettingsCatalog
    {
        /// <summary>Nombres de las secciones.</summary>
        public static readonly string[] CategoryLabels = { "GRÁFICOS", "AUDIO", "CONTROLES Y CÁMARA", "REGLAS" };

        /// <summary>Todos los ajustes.</summary>
        public static readonly SettingItem[] Items =
        {
            Item(SettingCategory.Graphics, true, s => "CALIDAD GRÁFICA: " + GameSettings.QualityLabels[s.Quality],
                s => s.Quality = (s.Quality + 1) % GameSettings.QualityLabels.Length),
            Item(SettingCategory.Graphics, true, s => "ESCALA DE RESOLUCIÓN: " + GameSettings.ResolutionLabels[s.Resolution],
                s => s.Resolution = (s.Resolution + 1) % GameSettings.ResolutionLabels.Length),
            Item(SettingCategory.Graphics, true, s => "LÍMITE DE FPS: " + s.TargetFps, s => s.TargetFps = s.TargetFps == 30 ? 60 : 30),
            Item(SettingCategory.Graphics, false, s => "PANEL DE DEPURACIÓN: " + YesNo(s.ShowDebug), s => s.ShowDebug = !s.ShowDebug),

            Item(SettingCategory.Audio, true, s => "VOLUMEN GENERAL: " + GameSettings.VolumeLabels[s.MasterVolume], s => s.MasterVolume = (s.MasterVolume + 1) % 5),
            Item(SettingCategory.Audio, true, s => "AMBIENTE DE ESTADIO: " + GameSettings.VolumeLabels[s.StadiumVolume], s => s.StadiumVolume = (s.StadiumVolume + 1) % 5),
            Item(SettingCategory.Audio, true, s => "MÚSICA DE LOS MENÚS: " + GameSettings.VolumeLabels[s.MusicVolume], s => s.MusicVolume = (s.MusicVolume + 1) % 5),
            Item(SettingCategory.Audio, true, s => "BALÓN, SILBATO Y RED: " + GameSettings.VolumeLabels[s.EffectsVolume], s => s.EffectsVolume = (s.EffectsVolume + 1) % 5),
            Item(SettingCategory.Audio, true, s => "VIBRACIÓN (GOLES Y PALOS): " + YesNo(s.Vibration), s => s.Vibration = !s.Vibration),

            Item(SettingCategory.Controls, true, s => "CÁMARA: " + GameSettings.CameraLabel(s.CameraMode), s => s.CameraMode = (s.CameraMode + 1) % GameSettings.CameraLabelKeys.Length),
            Item(SettingCategory.Controls, true, s => "JOYSTICK: " + (s.FixedJoystick ? "FIJO" : "FLOTANTE"), s => s.FixedJoystick = !s.FixedJoystick),
            Item(SettingCategory.Controls, true, s => "ZONA MUERTA DEL JOYSTICK: " + GameSettings.DeadZoneLabels[s.DeadZone],
                s => s.DeadZone = (s.DeadZone + 1) % GameSettings.DeadZoneLabels.Length),
            Item(SettingCategory.Controls, true, s => "TAMAÑO DE LOS BOTONES: " + (s.BigButtons ? "GRANDE" : "NORMAL"), s => s.BigButtons = !s.BigButtons),
            Item(SettingCategory.Controls, true, s => "OPACIDAD DE LOS BOTONES: " + GameSettings.OpacityLabels[s.ButtonOpacity], s => s.ButtonOpacity = (s.ButtonOpacity + 1) % 3),
            Item(SettingCategory.Controls, true, s => "ASISTENCIA DE PASE: " + GameSettings.AssistLabels[s.PassAssist], s => s.PassAssist = (s.PassAssist + 1) % 4),
            Item(SettingCategory.Controls, true, s => "CONTROLES PARA ZURDOS: " + YesNo(s.LeftHanded), s => s.LeftHanded = !s.LeftHanded),
            Item(SettingCategory.Controls, true, s => "BOTÓN FILTRADO: " + YesNo(s.ShowThrough), s => s.ShowThrough = !s.ShowThrough),
            Item(SettingCategory.Controls, true, s => "BOTÓN CENTRO: " + YesNo(s.ShowCross), s => s.ShowCross = !s.ShowCross),
            Item(SettingCategory.Controls, true, s => "NOMBRES: " + MatchSetupScreen.NameModeLabel(s.Names), s => s.Names = (NameMode)(((int)s.Names + 1) % 3)),

            Item(SettingCategory.Rules, false, s => "MINUTOS POR TIEMPO: " + TeamCatalog.HalfMinutes[s.Length], s => s.Length = (s.Length + 1) % TeamCatalog.HalfMinutes.Length),
            Item(SettingCategory.Rules, false, s => "DIFICULTAD: " + TeamCatalog.DifficultyLabels[s.Difficulty].ToUpperInvariant(),
                s => s.Difficulty = (s.Difficulty + 1) % TeamCatalog.DifficultyLabels.Length),
            Item(SettingCategory.Rules, false, s => "VELOCIDAD DEL JUEGO: " + GameSettings.SpeedLabels[s.Speed], s => s.Speed = (s.Speed + 1) % GameSettings.Speeds.Length)
        };

        private static SettingItem Item(SettingCategory c, bool live, Func<GameSettings, string> text, Action<GameSettings> next) =>
            new SettingItem { Category = c, Live = live, Text = text, Next = next };

        private static string YesNo(bool v) => v ? "SÍ" : "NO";

        /// <summary>Aplica lo que tiene efecto inmediato fuera del partido (FPS, calidad, volumen general).</summary>
        public static void Apply(GameSettings s)
        {
            Application.targetFrameRate = s.TargetFps;
            GraphicsQuality.Apply(s.Quality, s.Resolution);
            Materials.SetRim(s.Quality > 0); // en calidad BAJA, jugadores sin luz de borde
            Materials.SetLighting(s.Quality > 0); // ni volumen de luz direccional
            Materials.SetPlanarShadows(s.Quality > 0); // la sombra proyectada es de calidad MEDIA y ALTA; en BAJA queda el disco de contacto
            AudioListener.volume = GameSettings.Volume(s.MasterVolume);
            UseImportedBody(s.Quality > 0); // el cuerpo humano importado es de calidad MEDIA y ALTA; en BAJA se arma el procedural, más liviano
        }

        private const string HumanoidResource = "Humanoid/body";
        private static byte[] _humanoidBytes;
        private static bool _humanoidLoaded;

        /// <summary>Instala o retira el cuerpo importado (se usa en los partidos que empiecen después). Si el archivo falta, queda el procedural.</summary>
        private static void UseImportedBody(bool on)
        {
            if (!_humanoidLoaded)
            {
                _humanoidLoaded = true;
                TextAsset asset = Resources.Load<TextAsset>(HumanoidResource);
                _humanoidBytes = asset != null ? asset.bytes : null;
                if (_humanoidBytes == null)
                {
                    Debug.LogWarning("Football: no se encontró Resources/" + HumanoidResource + ".bytes; los jugadores usan el cuerpo procedural.");
                }
            }

            if (!HumanoidBody.Install(on ? _humanoidBytes : null) && on && _humanoidBytes != null)
            {
                Debug.LogWarning("Football: el archivo del cuerpo importado no se pudo leer; los jugadores usan el cuerpo procedural.");
            }
        }
    }
}
