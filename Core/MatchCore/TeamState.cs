using Football.Core.Common;

namespace Football.Core.MatchCore
{
    /// <summary>Estado de un equipo durante el partido.</summary>
    public sealed class TeamState
    {
        /// <summary>Índice del equipo (0 local, 1 visitante).</summary>
        public readonly int Index;
        /// <summary>Nombre.</summary>
        public readonly string Name;
        /// <summary>Formación (se puede cambiar durante el partido con <see cref="MatchEngine.SetFormation"/>).</summary>
        public Formation Formation;
        /// <summary>Suplentes disponibles al empezar (vista inmutable; los ya usados se marcan en <see cref="BenchUsed"/>).</summary>
        public readonly PlayerMatchView[] Bench;
        /// <summary>Suplente ya ingresado o pedido: no puede volver a usarse.</summary>
        public readonly bool[] BenchUsed;
        /// <summary>Jugadores que salieron sustituidos (no pueden volver a entrar).</summary>
        public readonly System.Collections.Generic.List<string> SubstitutedOut = new System.Collections.Generic.List<string>();
        /// <summary>Cambios ya hechos.</summary>
        public int SubstitutionsUsed;
        /// <summary>Ventanas de cambios ya usadas (fuera del entretiempo).</summary>
        public int WindowsUsed;
        /// <summary>Estilo de juego efectivo (el base con los ajustes tácticos aplicados).</summary>
        public readonly TeamStyle Style;
        /// <summary>Estilo con el que el equipo empezó el partido.</summary>
        public readonly TeamStyle BaseStyle;
        /// <summary>Mentalidad actual.</summary>
        public Mentality Mentality;
        /// <summary>Presión actual.</summary>
        public PressLevel Pressing;
        /// <summary>Si la IA de este equipo adapta la táctica al marcador (no se aplica al equipo humano).</summary>
        public bool AdaptiveTactics = true;
        /// <summary>Dificultad de la IA de este equipo.</summary>
        public readonly DifficultyProfile Difficulty;
        /// <summary>Jugadores, en el orden de los puestos de la formación.</summary>
        public readonly MatchPlayer[] Players;
        /// <summary>Estadísticas.</summary>
        public readonly TeamStats Stats = new TeamStats();

        /// <summary>Dirección de ataque: +1 hacia +X, -1 hacia -X. Se invierte en el descanso.</summary>
        public float AttackDirection;

        /// <summary>El equipo está controlado por una persona.</summary>
        public bool IsHuman;
        /// <summary>Jugador que controla la persona.</summary>
        public MatchPlayer Controlled;
        /// <summary>Última entrada recibida de la persona.</summary>
        public MatchInput Input;
        /// <summary>Tiempo que se guarda un botón de golpeo aún no usado (s).</summary>
        public float KickBufferTimer;
        /// <summary>Tiempo hasta el próximo cambio automático de jugador (s).</summary>
        public float SwitchTimer;
        /// <summary>La persona controla al arquero hasta que lo suelte (o el arquero juegue el balón).</summary>
        public bool KeeperLock;

        /// <summary>Jugador que persigue un balón suelto.</summary>
        public MatchPlayer Chaser;
        /// <summary>Jugador que presiona al poseedor rival.</summary>
        public MatchPlayer Presser;
        /// <summary>Segundo jugador que presiona.</summary>
        public MatchPlayer Presser2;
        /// <summary>Jugador que cubre la espalda del que presiona.</summary>
        public MatchPlayer Cover;
        /// <summary>Jugadores que ofrecen apoyo al poseedor.</summary>
        public readonly MatchPlayer[] Support = new MatchPlayer[8];
        /// <summary>Cuántos de <see cref="Support"/> están en uso.</summary>
        public int SupportCount;
        /// <summary>Punto de intercepción del perseguidor.</summary>
        public Vec2 InterceptPoint;
        /// <summary>Coordenada X de la línea de fuera de juego que enfrenta este equipo al atacar.</summary>
        public float OffsideLine;

        /// <summary>Extremo (+1/-1) cuya portería defiende el equipo.</summary>
        public float DefendedEnd => -AttackDirection;

        /// <summary>Crea el equipo y sus jugadores (uno por puesto de la formación), que se añaden al arreglo global.</summary>
        public TeamState(int index, float attackDirection, MatchTeamSetup setup, DifficultyProfile difficulty,
            PlayerConfig playerConfig, MatchPlayer[] allPlayers)
        {
            Index = index;
            AttackDirection = attackDirection;
            Name = setup.Name;
            Formation = setup.Formation;
            BaseStyle = setup.Style.Clone();
            Style = setup.Style.Clone();
            Difficulty = difficulty;
            Players = new MatchPlayer[setup.Players.Length];
            Bench = setup.Bench ?? new PlayerMatchView[0];
            BenchUsed = new bool[Bench.Length];
            for (int slot = 0; slot < Players.Length; slot++)
            {
                var player = new MatchPlayer(index * Players.Length + slot, index, slot, setup.Players[slot], playerConfig);
                Players[slot] = player;
                allPlayers[player.Index] = player;
            }
        }
    }
}
