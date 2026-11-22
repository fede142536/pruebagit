namespace Football.Core.Domain
{
    /// <summary>Mentalidad del equipo: cuánto arriesga y cuánto se adelanta.</summary>
    public enum Mentality
    {
        /// <summary>Muy defensiva: bloque bajo, sin riesgos.</summary>
        VeryDefensive = -2,
        /// <summary>Defensiva.</summary>
        Defensive = -1,
        /// <summary>Equilibrada: el estilo del equipo tal cual.</summary>
        Balanced = 0,
        /// <summary>Ofensiva.</summary>
        Attacking = 1,
        /// <summary>Muy ofensiva: bloque alto, juego directo y arriesgado.</summary>
        VeryAttacking = 2
    }

    /// <summary>Intensidad de la presión sin balón.</summary>
    public enum PressLevel
    {
        /// <summary>Presión baja: se espera replegado.</summary>
        Low = -1,
        /// <summary>La presión propia del estilo.</summary>
        Normal = 0,
        /// <summary>Presión alta: se va a buscar el balón arriba.</summary>
        High = 1
    }

    /// <summary>
    /// Rol individual de un jugador según su puesto: cambia dónde se coloca sin balón. <see cref="Auto"/> usa el rol que el
    /// estilo del equipo da a ese puesto (o ninguno).
    /// </summary>
    public enum PlayerDuty
    {
        /// <summary>El que marque el estilo del equipo.</summary>
        Auto = 0,
        /// <summary>Lateral defensivo: no pasa al ataque, cierra por dentro.</summary>
        Defensive = 1,
        /// <summary>Lateral carrilero: sube por la banda, muy abierto.</summary>
        WingBack = 2,
        /// <summary>Mediocampista recuperador: se pega al balón en defensa y no se adelanta.</summary>
        BallWinner = 3,
        /// <summary>Mediocampista organizador: se acerca al poseedor para recibir y distribuir.</summary>
        Playmaker = 4,
        /// <summary>Mediocampista llegador: pisa el área en ataque.</summary>
        BoxToBox = 5,
        /// <summary>Delantero pivote: se queda centrado y baja a recibir de espaldas.</summary>
        TargetMan = 6,
        /// <summary>Delantero al espacio: juega en la última línea, pegado al fuera de juego.</summary>
        Runner = 7
    }
}
