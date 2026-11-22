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

    /// <summary>Grito táctico del director técnico desde el banco: cambia el juego del equipo durante un rato.</summary>
    public enum TouchlineShout
    {
        /// <summary>Ninguno.</summary>
        None = 0,
        /// <summary>«Encarar más»: más riesgo y amplitud, más regate.</summary>
        TakeOn = 1,
        /// <summary>«Tocar en corto»: juego paciente, sin pelotazos.</summary>
        ShortPassing = 2,
        /// <summary>«Perder tiempo»: sin riesgos, bloque más bajo y saques más lentos.</summary>
        WasteTime = 3,
        /// <summary>«Presión tras pérdida»: presión intensa y línea más alta.</summary>
        CounterPress = 4,
        /// <summary>«Buscar al 9»: juego directo hacia el delantero.</summary>
        FindStriker = 5
    }

    /// <summary>Tono de la charla del entretiempo.</summary>
    public enum TeamTalk
    {
        /// <summary>Calmado: ordena al equipo (funciona si se va ganando o empatando).</summary>
        Calm = 0,
        /// <summary>Apasionado: enciende al equipo (funciona si se va perdiendo).</summary>
        Passionate = 1,
        /// <summary>Exigente: pide más (funciona empatando; con una goleada en contra puede salir mal).</summary>
        Demanding = 2
    }

    /// <summary>Cómo recibió el plantel la charla del entretiempo.</summary>
    public enum TalkReaction
    {
        /// <summary>Mal: juega más nervioso.</summary>
        Negative = -1,
        /// <summary>Sin efecto apreciable.</summary>
        Neutral = 0,
        /// <summary>Bien: juega más concentrado e intenso.</summary>
        Positive = 1
    }

    /// <summary>Jugada ensayada en los córners a favor.</summary>
    public enum CornerRoutine
    {
        /// <summary>Repartidos por el área, centro al segundo palo.</summary>
        Mixed = 0,
        /// <summary>Carga al primer palo.</summary>
        NearPost = 1,
        /// <summary>Al punto penal.</summary>
        PenaltySpot = 2,
        /// <summary>Jugada corta con un compañero que se acerca.</summary>
        Short = 3
    }

    /// <summary>Cómo defiende el equipo las pelotas paradas en su área.</summary>
    public enum SetPieceMarking
    {
        /// <summary>Zonal: cada uno cubre un sector del área.</summary>
        Zonal = 0,
        /// <summary>Individual: cada uno sigue a un rival.</summary>
        ManToMan = 1
    }
}
