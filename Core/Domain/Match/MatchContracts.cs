namespace Football.Core.Domain
{
    // Contrato del partido compartido por el motor en tiempo real (MatchCore), el simulador rápido (MatchSim) y la gestión.
    /// <summary>Estadísticas de un equipo.</summary>
    public sealed class TeamStats
    {
        /// <summary>Goles a favor.</summary>
        public int Goals;
        /// <summary>Tiros.</summary>
        public int Shots;
        /// <summary>Tiros a puerta.</summary>
        public int ShotsOnTarget;
        /// <summary>Paradas del portero.</summary>
        public int Saves;
        /// <summary>Pases intentados.</summary>
        public int Passes;
        /// <summary>Pases completados.</summary>
        public int PassesCompleted;
        /// <summary>Pases interceptados por el rival.</summary>
        public int PassesIntercepted;
        /// <summary>Pases que acabaron fuera de juego o sin llegar a nadie por una interrupción.</summary>
        public int PassesLost;
        /// <summary>Faltas cometidas.</summary>
        public int Fouls;
        /// <summary>Tarjetas amarillas.</summary>
        public int Yellows;
        /// <summary>Tarjetas rojas.</summary>
        public int Reds;
        /// <summary>Córners a favor.</summary>
        public int Corners;
        /// <summary>Fueras de juego del equipo.</summary>
        public int Offsides;
        /// <summary>Pasos de simulación con posesión.</summary>
        public int PossessionTicks;
        /// <summary>Veces que el equipo recuperó o ganó la posesión.</summary>
        public int Possessions;
        /// <summary>Entradas intentadas (de pie o en barrida).</summary>
        public int Tackles;
        /// <summary>Entradas que ganaron el balón.</summary>
        public int TacklesWon;
        /// <summary>Goles esperados acumulados (suma del xG de cada remate).</summary>
        public float Xg;
        /// <summary>Balones recuperados (robos, intercepciones y balones sueltos tras una posesión rival).</summary>
        public int Recoveries;
        /// <summary>Lesiones sufridas.</summary>
        public int Injuries;
        /// <summary>Distancia recorrida por todo el equipo (m).</summary>
        public float Distance;
    }

    /// <summary>Estadísticas de un jugador en el partido (se actualizan sin generar basura).</summary>
    public sealed class PlayerMatchStats
    {
        /// <summary>Goles (sin contar en propia puerta).</summary>
        public int Goals;
        /// <summary>Goles en propia puerta.</summary>
        public int OwnGoals;
        /// <summary>Asistencias (último pase antes del gol de un compañero).</summary>
        public int Assists;
        /// <summary>Remates.</summary>
        public int Shots;
        /// <summary>Remates que iban entre los tres palos al salir del pie.</summary>
        public int ShotsOnTarget;
        /// <summary>Goles esperados acumulados.</summary>
        public float Xg;
        /// <summary>Pases intentados.</summary>
        public int Passes;
        /// <summary>Pases completados.</summary>
        public int PassesCompleted;
        /// <summary>Balones recuperados.</summary>
        public int Recoveries;
        /// <summary>Entradas intentadas.</summary>
        public int Tackles;
        /// <summary>Entradas ganadas.</summary>
        public int TacklesWon;
        /// <summary>Faltas cometidas.</summary>
        public int Fouls;
        /// <summary>Paradas (arqueros).</summary>
        public int Saves;
        /// <summary>Amarillas.</summary>
        public int Yellows;
        /// <summary>Rojas (directa o doble amarilla).</summary>
        public int Reds;
        /// <summary>Se lesionó.</summary>
        public bool Injured;
        /// <summary>Distancia recorrida (m).</summary>
        public float Distance;
    }

    /// <summary>Un remate del partido.</summary>
    public struct ShotRecord
    {
        /// <summary>Equipo que remata.</summary>
        public int Team;
        /// <summary>Índice global del jugador.</summary>
        public int PlayerIndex;
        /// <summary>Identificador del jugador en el modelo de datos.</summary>
        public int PlayerId;
        /// <summary>Minuto de juego.</summary>
        public float Minute;
        /// <summary>Distancia a la portería (m).</summary>
        public float Distance;
        /// <summary>Goles esperados del remate.</summary>
        public float Xg;
        /// <summary>Iba entre los tres palos al salir del pie.</summary>
        public bool OnTarget;
        /// <summary>Terminó en gol.</summary>
        public bool Goal;
    }

    /// <summary>Vista inmutable de un jugador para el partido, con los atributos efectivos ya modificados por forma, moral y fatiga.</summary>
    public sealed class PlayerMatchView
    {
        /// <summary>Identificador del jugador en el modelo de datos.</summary>
        public int Id;
        /// <summary>Nombre para mostrar.</summary>
        public string Name;
        /// <summary>Dorsal.</summary>
        public int Number;
        /// <summary>Posición asignada en la formación.</summary>
        public Position Role;
        /// <summary>Atributos efectivos.</summary>
        public AttributeSet Attributes = new AttributeSet();
    }

    /// <summary>Equipo preparado para jugar un partido.</summary>
    public sealed class MatchTeamSetup
    {
        /// <summary>Nombre del equipo.</summary>
        public string Name;
        /// <summary>Formación.</summary>
        public Formation Formation;
        /// <summary>Estilo de juego.</summary>
        public TeamStyle Style;
        /// <summary>Once jugadores, en el mismo orden que los puestos de la formación (el primero es el portero).</summary>
        public PlayerMatchView[] Players;
        /// <summary>Suplentes (opcional): pueden entrar con <c>MatchEngine.RequestSubstitution</c>.</summary>
        public PlayerMatchView[] Bench;
        /// <summary>Rol de cada puesto de la formación (opcional; <see cref="PlayerDuty.Auto"/> = el del estilo).</summary>
        public PlayerDuty[] Duties;
    }
}
