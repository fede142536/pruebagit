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
        private const string VibrationKey = "football.vibration";
        private const string SoundKey = "football.sound";

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

        /// <summary>Nombres de los perfiles gráficos.</summary>
        public static readonly string[] QualityLabels = { "BAJA", "MEDIA", "ALTA" };
        /// <summary>Vibración en goles y palos (solo teléfono).</summary>
        public bool Vibration = true;
        /// <summary>Sonido (hinchada, silbato, golpeos).</summary>
        public bool Sound = true;

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
                Vibration = PlayerPrefs.GetInt(VibrationKey, 1) == 1,
                Sound = PlayerPrefs.GetInt(SoundKey, 1) == 1
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
            PlayerPrefs.SetInt(VibrationKey, Vibration ? 1 : 0);
            PlayerPrefs.SetInt(SoundKey, Sound ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
