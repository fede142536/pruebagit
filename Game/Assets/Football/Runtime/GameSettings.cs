using UnityEngine;

namespace Football.Game
{
    /// <summary>Ajustes y últimas elecciones del jugador, guardados en el dispositivo.</summary>
    public sealed class GameSettings
    {
        private const string LeftKey = "football.left";
        private const string DebugKey = "football.debug";
        private const string FpsKey = "football.fps";
        private const string OpponentKey = "football.opponent";
        private const string DifficultyKey = "football.difficulty";
        private const string LengthKey = "football.length";

        /// <summary>Controles invertidos para zurdos.</summary>
        public bool LeftHanded;
        /// <summary>Mostrar el panel de depuración.</summary>
        public bool ShowDebug;
        /// <summary>FPS objetivo (30 o 60).</summary>
        public int TargetFps = 30;
        /// <summary>Rival elegido.</summary>
        public int Opponent;
        /// <summary>Dificultad elegida (0..4).</summary>
        public int Difficulty = 2;
        /// <summary>Duración elegida (índice en <see cref="TeamCatalog.HalfMinutes"/>).</summary>
        public int Length = 1;

        /// <summary>Lee los ajustes guardados.</summary>
        public static GameSettings Load()
        {
            return new GameSettings
            {
                LeftHanded = PlayerPrefs.GetInt(LeftKey, 0) == 1,
                ShowDebug = PlayerPrefs.GetInt(DebugKey, 0) == 1,
                TargetFps = PlayerPrefs.GetInt(FpsKey, 30),
                Opponent = Mathf.Clamp(PlayerPrefs.GetInt(OpponentKey, 0), 0, TeamCatalog.Opponents.Length - 1),
                Difficulty = Mathf.Clamp(PlayerPrefs.GetInt(DifficultyKey, 2), 0, TeamCatalog.DifficultyLabels.Length - 1),
                Length = Mathf.Clamp(PlayerPrefs.GetInt(LengthKey, 1), 0, TeamCatalog.HalfMinutes.Length - 1)
            };
        }

        /// <summary>Guarda los ajustes.</summary>
        public void Save()
        {
            PlayerPrefs.SetInt(LeftKey, LeftHanded ? 1 : 0);
            PlayerPrefs.SetInt(DebugKey, ShowDebug ? 1 : 0);
            PlayerPrefs.SetInt(FpsKey, TargetFps);
            PlayerPrefs.SetInt(OpponentKey, Opponent);
            PlayerPrefs.SetInt(DifficultyKey, Difficulty);
            PlayerPrefs.SetInt(LengthKey, Length);
            PlayerPrefs.Save();
        }
    }
}
