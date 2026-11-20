namespace Football.Core.Domain
{
    /// <summary>Posición de un jugador en el campo. Los valores se guardan en la base de datos: no renumerar.</summary>
    public enum Position
    {
        /// <summary>Portero.</summary>
        Goalkeeper = 0,
        /// <summary>Defensa central.</summary>
        CenterBack = 1,
        /// <summary>Lateral izquierdo.</summary>
        LeftBack = 2,
        /// <summary>Lateral derecho.</summary>
        RightBack = 3,
        /// <summary>Mediocentro defensivo.</summary>
        DefensiveMidfielder = 4,
        /// <summary>Mediocentro.</summary>
        CentralMidfielder = 5,
        /// <summary>Mediapunta.</summary>
        AttackingMidfielder = 6,
        /// <summary>Volante izquierdo.</summary>
        LeftMidfielder = 7,
        /// <summary>Volante derecho.</summary>
        RightMidfielder = 8,
        /// <summary>Extremo izquierdo.</summary>
        LeftWinger = 9,
        /// <summary>Extremo derecho.</summary>
        RightWinger = 10,
        /// <summary>Delantero centro.</summary>
        Striker = 11
    }
}
