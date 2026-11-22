using UnityEngine;

namespace Football.Game
{
    /// <summary>Ajustes y últimas elecciones del jugador, guardados en el dispositivo.</summary>
    public sealed class GameSettings
    {
        private const string LeftKey = "football.left";
        private const string DebugKey = "football.debug";
        private const string FpsKey = "football.fps";
        private const string HomeKey = "football.home";
        private const string OpponentKey = "football.opponent";
        private const string HomeKitKey = "football.homekit";
        private const string AwayKitKey = "football.awaykit";
        private const string DifficultyKey = "football.difficulty";
        private const string LengthKey = "football.length";
        private const string NamesKey = "football.names";
        private const string SpeedKey = "football.speed";
        private const string ThroughKey = "football.through";
        private const string CrossKey = "football.cross";
        private const string BigButtonsKey = "football.bigbuttons";
        private const string QualityKey = "football.quality";
        private const string MasterKey = "football.vol.master";
        private const string StadiumKey = "football.vol.stadium";
        private const string EffectsKey = "football.vol.effects";
        private const string ResolutionKey = "football.resolution";
        private const string FixedJoystickKey = "football.fixedjoystick";
        private const string OpacityKey = "football.opacity";
        private const string AssistKey = "football.assist";
        private const string CameraKey = "football.camera";
        private const string DeadZoneKey = "football.deadzone";
        private const string VibrationKey = "football.vibration";

        /// <summary>Velocidades del juego: fracción del tiempo real que avanza la simulación.</summary>
        public static readonly float[] Speeds = { 0.6f, 0.75f, 1f };

        /// <summary>Nombres de las velocidades.</summary>
        public static readonly string[] SpeedLabels = { "LENTA", "NORMAL", "RÁPIDA" };

        /// <summary>Controles invertidos para zurdos.</summary>
        public bool LeftHanded;
        /// <summary>Mostrar el panel de depuración.</summary>
        public bool ShowDebug;
        /// <summary>FPS objetivo (30 o 60).</summary>
        public int TargetFps = 30;
        /// <summary>Equipo del jugador (índice en <see cref="TeamCatalog.Teams"/>).</summary>
        public int HomeTeam;
        /// <summary>Equipo rival.</summary>
        public int Opponent = 1;
        /// <summary>Camiseta del jugador: 0 titular, 1 alternativa.</summary>
        public int HomeKit;
        /// <summary>Camiseta del rival: 0 titular, 1 alternativa.</summary>
        public int AwayKit = 1;
        /// <summary>Dificultad elegida (0..4).</summary>
        public int Difficulty = 2;
        /// <summary>Duración elegida (índice en <see cref="TeamCatalog.HalfMinutes"/>).</summary>
        public int Length = 1;
        /// <summary>Qué nombres se muestran sobre los jugadores.</summary>
        public NameMode Names = NameMode.All;
        /// <summary>Velocidad del juego (índice en <see cref="Speeds"/>).</summary>
        public int Speed = 1;
        /// <summary>Mostrar el botón de pase filtrado.</summary>
        public bool ShowThrough;
        /// <summary>Mostrar el botón de centro.</summary>
        public bool ShowCross;
        /// <summary>Botones grandes.</summary>
        public bool BigButtons;
        /// <summary>Perfil gráfico: 0 bajo, 1 medio, 2 alto (ver <see cref="GraphicsQuality"/>).</summary>
        public int Quality = 1;

        /// <summary>Volumen general (0..4 = 0 %, 25 %, 50 %, 75 %, 100 %).</summary>
        public int MasterVolume = 4;
        /// <summary>Volumen del ambiente de estadio (hinchada).</summary>
        public int StadiumVolume = 3;
        /// <summary>Volumen de los efectos (balón, silbato, red, palo).</summary>
        public int EffectsVolume = 4;
        /// <summary>Escala de resolución: 0 la del perfil gráfico, 1 75 %, 2 100 %.</summary>
        public int Resolution;
        /// <summary>Joystick fijo (siempre en el mismo lugar) en lugar de flotante.</summary>
        public bool FixedJoystick;
        /// <summary>Opacidad de los botones táctiles: 0 40 %, 1 55 %, 2 75 %.</summary>
        public int ButtonOpacity = 1;
        /// <summary>Asistencia de pase: 0 según la dificultad, 1 baja, 2 media, 3 alta.</summary>
        public int PassAssist;
        /// <summary>Cámara: 0 transmisión de TV (fija), 1 dinámica, 2 cercana.</summary>
        public int CameraMode = 1;
        /// <summary>Zona muerta del joystick: índice de <c>ViewTuning.Controls.DeadZones</c> (0 sensible, 1 normal, 2 firme).</summary>
        public int DeadZone = 1;
        /// <summary>Nombres de las zonas muertas.</summary>
        public static readonly string[] DeadZoneLabels = { "SENSIBLE", "NORMAL", "FIRME" };

        /// <summary>Etiquetas de volumen.</summary>
        public static readonly string[] VolumeLabels = { "0 %", "25 %", "50 %", "75 %", "100 %" };
        /// <summary>Etiquetas de escala de resolución.</summary>
        public static readonly string[] ResolutionLabels = { "SEGÚN CALIDAD", "75 %", "100 %" };
        /// <summary>Etiquetas de opacidad.</summary>
        public static readonly string[] OpacityLabels = { "40 %", "55 %", "75 %" };
        /// <summary>Opacidad de cada nivel.</summary>
        public static readonly float[] Opacities = { 0.4f, 0.55f, 0.75f };
        /// <summary>Etiquetas de la asistencia de pase.</summary>
        public static readonly string[] AssistLabels = { "SEGÚN DIFICULTAD", "BAJA", "MEDIA", "ALTA" };
        /// <summary>Valor de la asistencia de cada nivel (el 0 usa la dificultad).</summary>
        public static readonly float[] AssistValues = { -1f, 0.2f, 0.55f, 0.9f };
        /// <summary>Etiquetas de los tipos de cámara.</summary>
        public static readonly string[] CameraLabels = { "TRANSMISIÓN TV", "DINÁMICA", "CERCANA" };

        /// <summary>Volumen de 0 a 1 de un nivel.</summary>
        public static float Volume(int level) => Mathf.Clamp01(level / 4f);

        /// <summary>Nombres de los perfiles gráficos.</summary>
        public static readonly string[] QualityLabels = { "BAJA", "MEDIA", "ALTA" };
        /// <summary>Vibración en goles y palos (solo teléfono).</summary>
        public bool Vibration = true;

        /// <summary>Fracción del tiempo real que avanza la simulación.</summary>
        public float SpeedFactor => Speeds[Mathf.Clamp(Speed, 0, Speeds.Length - 1)];

        /// <summary>Lee los ajustes guardados.</summary>
        public static GameSettings Load()
        {
            int teams = TeamCatalog.Teams.Length;
            var s = new GameSettings
            {
                LeftHanded = PlayerPrefs.GetInt(LeftKey, 0) == 1,
                ShowDebug = PlayerPrefs.GetInt(DebugKey, 0) == 1,
                TargetFps = PlayerPrefs.GetInt(FpsKey, 30),
                HomeTeam = Mathf.Clamp(PlayerPrefs.GetInt(HomeKey, 0), 0, teams - 1),
                Opponent = Mathf.Clamp(PlayerPrefs.GetInt(OpponentKey, 1), 0, teams - 1),
                HomeKit = Mathf.Clamp(PlayerPrefs.GetInt(HomeKitKey, 0), 0, 1),
                AwayKit = Mathf.Clamp(PlayerPrefs.GetInt(AwayKitKey, 1), 0, 1),
                Difficulty = Mathf.Clamp(PlayerPrefs.GetInt(DifficultyKey, 2), 0, TeamCatalog.DifficultyLabels.Length - 1),
                Length = Mathf.Clamp(PlayerPrefs.GetInt(LengthKey, 1), 0, TeamCatalog.HalfMinutes.Length - 1),
                Names = (NameMode)Mathf.Clamp(PlayerPrefs.GetInt(NamesKey, 0), 0, 2),
                Speed = Mathf.Clamp(PlayerPrefs.GetInt(SpeedKey, 1), 0, Speeds.Length - 1),
                ShowThrough = PlayerPrefs.GetInt(ThroughKey, 0) == 1,
                ShowCross = PlayerPrefs.GetInt(CrossKey, 0) == 1,
                BigButtons = PlayerPrefs.GetInt(BigButtonsKey, 0) == 1,
                Quality = Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, 1), 0, QualityLabels.Length - 1),
                MasterVolume = Mathf.Clamp(PlayerPrefs.GetInt(MasterKey, 4), 0, 4),
                StadiumVolume = Mathf.Clamp(PlayerPrefs.GetInt(StadiumKey, 3), 0, 4),
                EffectsVolume = Mathf.Clamp(PlayerPrefs.GetInt(EffectsKey, 4), 0, 4),
                Resolution = Mathf.Clamp(PlayerPrefs.GetInt(ResolutionKey, 0), 0, 2),
                FixedJoystick = PlayerPrefs.GetInt(FixedJoystickKey, 0) == 1,
                ButtonOpacity = Mathf.Clamp(PlayerPrefs.GetInt(OpacityKey, 1), 0, 2),
                PassAssist = Mathf.Clamp(PlayerPrefs.GetInt(AssistKey, 0), 0, 3),
                CameraMode = Mathf.Clamp(PlayerPrefs.GetInt(CameraKey, 1), 0, 2),
                DeadZone = Mathf.Clamp(PlayerPrefs.GetInt(DeadZoneKey, 1), 0, DeadZoneLabels.Length - 1),
                Vibration = PlayerPrefs.GetInt(VibrationKey, 1) == 1
            };

            if (s.Opponent == s.HomeTeam)
            {
                s.Opponent = (s.HomeTeam + 1) % teams;
            }

            return s;
        }

        /// <summary>Guarda los ajustes.</summary>
        public void Save()
        {
            PlayerPrefs.SetInt(LeftKey, LeftHanded ? 1 : 0);
            PlayerPrefs.SetInt(DebugKey, ShowDebug ? 1 : 0);
            PlayerPrefs.SetInt(FpsKey, TargetFps);
            PlayerPrefs.SetInt(HomeKey, HomeTeam);
            PlayerPrefs.SetInt(OpponentKey, Opponent);
            PlayerPrefs.SetInt(HomeKitKey, HomeKit);
            PlayerPrefs.SetInt(AwayKitKey, AwayKit);
            PlayerPrefs.SetInt(DifficultyKey, Difficulty);
            PlayerPrefs.SetInt(LengthKey, Length);
            PlayerPrefs.SetInt(NamesKey, (int)Names);
            PlayerPrefs.SetInt(SpeedKey, Speed);
            PlayerPrefs.SetInt(ThroughKey, ShowThrough ? 1 : 0);
            PlayerPrefs.SetInt(CrossKey, ShowCross ? 1 : 0);
            PlayerPrefs.SetInt(BigButtonsKey, BigButtons ? 1 : 0);
            PlayerPrefs.SetInt(QualityKey, Quality);
            PlayerPrefs.SetInt(MasterKey, MasterVolume);
            PlayerPrefs.SetInt(StadiumKey, StadiumVolume);
            PlayerPrefs.SetInt(EffectsKey, EffectsVolume);
            PlayerPrefs.SetInt(ResolutionKey, Resolution);
            PlayerPrefs.SetInt(FixedJoystickKey, FixedJoystick ? 1 : 0);
            PlayerPrefs.SetInt(OpacityKey, ButtonOpacity);
            PlayerPrefs.SetInt(AssistKey, PassAssist);
            PlayerPrefs.SetInt(CameraKey, CameraMode);
            PlayerPrefs.SetInt(DeadZoneKey, DeadZone);
            PlayerPrefs.SetInt(VibrationKey, Vibration ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
