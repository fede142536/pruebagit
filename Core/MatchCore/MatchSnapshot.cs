namespace Football.Core.MatchCore
{
    /// <summary>
    /// Copia del estado visible del partido con buffers reutilizables. La capa de presentación guarda dos
    /// instantáneas consecutivas e interpola entre ellas al dibujar.
    /// </summary>
    public sealed class MatchSnapshot
    {
        /// <summary>Máximo de jugadores (11 contra 11); los buffers tienen este tamaño.</summary>
        public const int PlayerCount = 22;

        /// <summary>Jugadores que hay en este partido (22, o 6 en la cancha cerrada).</summary>
        public int Count;

        /// <summary>Posición X de cada jugador.</summary>
        public readonly float[] PlayerX = new float[PlayerCount];
        /// <summary>Posición Y de cada jugador.</summary>
        public readonly float[] PlayerY = new float[PlayerCount];
        /// <summary>Orientación (radianes) de cada jugador.</summary>
        public readonly float[] PlayerHeading = new float[PlayerCount];
        /// <summary>Rapidez (m/s) de cada jugador.</summary>
        public readonly float[] PlayerSpeed = new float[PlayerCount];
        /// <summary>Acción visible de cada jugador.</summary>
        public readonly PlayerAction[] PlayerAction = new PlayerAction[PlayerCount];
        /// <summary>Expulsados.</summary>
        public readonly bool[] SentOff = new bool[PlayerCount];

        /// <summary>Posición X del balón.</summary>
        public float BallX;
        /// <summary>Posición Y del balón.</summary>
        public float BallY;
        /// <summary>Altura del balón.</summary>
        public float BallZ;
        /// <summary>Índice del jugador con el balón, o -1.</summary>
        public int OwnerIndex;
        /// <summary>Jugador controlado por la persona en el equipo local, o -1.</summary>
        public int ControlledHome;
        /// <summary>Jugador controlado por la persona en el equipo visitante, o -1.</summary>
        public int ControlledAway;
        /// <summary>Goles del local.</summary>
        public int HomeGoals;
        /// <summary>Goles del visitante.</summary>
        public int AwayGoals;
        /// <summary>Minuto de partido.</summary>
        public float Minute;
        /// <summary>Parte (1 o 2).</summary>
        public int Half;
        /// <summary>Fase del partido.</summary>
        public MatchPhase Phase;
        /// <summary>Número de paso de simulación.</summary>
        public long Tick;

        /// <summary>Copia el estado actual del motor sin generar basura.</summary>
        /// <param name="engine">Motor de origen.</param>
        public void CopyFrom(MatchEngine engine)
        {
            Count = engine.Players.Length;
            for (int i = 0; i < Count; i++)
            {
                MatchPlayer p = engine.Players[i];
                PlayerX[i] = p.Pos.X;
                PlayerY[i] = p.Pos.Y;
                PlayerHeading[i] = p.Heading;
                PlayerSpeed[i] = p.Speed;
                PlayerAction[i] = p.Action;
                SentOff[i] = p.SentOff;
            }

            BallX = engine.Ball.Pos.X;
            BallY = engine.Ball.Pos.Y;
            BallZ = engine.Ball.Pos.Z;
            OwnerIndex = engine.Owner != null ? engine.Owner.Index : -1;
            ControlledHome = engine.Teams[0].Controlled != null ? engine.Teams[0].Controlled.Index : -1;
            ControlledAway = engine.Teams[1].Controlled != null ? engine.Teams[1].Controlled.Index : -1;
            HomeGoals = engine.Teams[0].Stats.Goals;
            AwayGoals = engine.Teams[1].Stats.Goals;
            Minute = engine.Minute;
            Half = engine.Half;
            Phase = engine.Phase;
            Tick = engine.TickCount;
        }
    }
}
