namespace Football.Core.Domain
{
    /// <summary>
    /// Atributos de un jugador. Los valores numéricos se guardan en la base de datos: no renumerar,
    /// añadir siempre al final de su grupo con un número nuevo.
    /// </summary>
    public enum AttributeId
    {
        /// <summary>Técnico: pase.</summary>
        Passing = 0,
        /// <summary>Técnico: regate.</summary>
        Dribbling = 1,
        /// <summary>Técnico: disparo.</summary>
        Shooting = 2,
        /// <summary>Técnico: control del primer toque.</summary>
        FirstTouch = 3,
        /// <summary>Técnico: centros.</summary>
        Crossing = 4,
        /// <summary>Técnico: juego aéreo de cabeza.</summary>
        Heading = 5,
        /// <summary>Técnico: entrada y recuperación.</summary>
        Tackling = 6,
        /// <summary>Técnico: definición ante el arco.</summary>
        Finishing = 7,

        /// <summary>Físico: velocidad punta.</summary>
        Pace = 20,
        /// <summary>Físico: aceleración.</summary>
        Acceleration = 21,
        /// <summary>Físico: resistencia.</summary>
        Stamina = 22,
        /// <summary>Físico: fuerza.</summary>
        Strength = 23,
        /// <summary>Físico: agilidad.</summary>
        Agility = 24,
        /// <summary>Físico: salto.</summary>
        Jumping = 25,

        /// <summary>Mental: visión de juego.</summary>
        Vision = 40,
        /// <summary>Mental: compostura bajo presión.</summary>
        Composure = 41,
        /// <summary>Mental: colocación.</summary>
        Positioning = 42,
        /// <summary>Mental: toma de decisiones.</summary>
        Decisions = 43,
        /// <summary>Mental: agresividad.</summary>
        Aggression = 44,
        /// <summary>Mental: despliegue de esfuerzo sin balón.</summary>
        WorkRate = 45,

        /// <summary>Portero: reflejos.</summary>
        Reflexes = 60,
        /// <summary>Portero: blocaje y manejo del balón.</summary>
        Handling = 61,
        /// <summary>Portero: colocación bajo los palos.</summary>
        GoalkeeperPositioning = 62,
        /// <summary>Portero: saque con el pie o la mano.</summary>
        Distribution = 63
    }
}
