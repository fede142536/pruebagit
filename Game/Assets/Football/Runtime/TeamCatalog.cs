using UnityEngine;
using Football.Core.MatchCore;

namespace Football.Game
{
    /// <summary>Equipo rival de la lista del menú. Todos los nombres son ficticios.</summary>
    public sealed class OpponentInfo
    {
        /// <summary>Nombre para mostrar.</summary>
        public string Name;
        /// <summary>Índice del estilo en <c>MatchConfig.Styles</c>.</summary>
        public int StyleIndex;
        /// <summary>Calidad media (1..100).</summary>
        public float Rating;
        /// <summary>Formación.</summary>
        public Formation Formation;
        /// <summary>Color de la camiseta.</summary>
        public Color Kit;
    }

    /// <summary>Datos de los equipos disponibles. Más adelante vendrán de la base de datos de la carrera.</summary>
    public static class TeamCatalog
    {
        /// <summary>Nombre del equipo del jugador.</summary>
        public const string PlayerTeamName = "Tu Equipo";

        /// <summary>Calidad del equipo del jugador.</summary>
        public const float PlayerRating = 66f;

        /// <summary>Color de la camiseta del jugador.</summary>
        public static readonly Color PlayerKit = new Color(0.15f, 0.45f, 0.95f);

        /// <summary>Rivales disponibles, con estilos de juego distintos.</summary>
        public static readonly OpponentInfo[] Opponents =
        {
            new OpponentInfo { Name = "Halcones del Sur", StyleIndex = 1, Rating = 62f, Formation = Formations.F433, Kit = new Color(0.85f, 0.15f, 0.15f) },
            new OpponentInfo { Name = "Real Montaña", StyleIndex = 4, Rating = 68f, Formation = Formations.F442, Kit = new Color(0.95f, 0.95f, 0.95f) },
            new OpponentInfo { Name = "Deportivo Costa", StyleIndex = 2, Rating = 66f, Formation = Formations.F442, Kit = new Color(0.95f, 0.75f, 0.1f) },
            new OpponentInfo { Name = "Unión Valle", StyleIndex = 3, Rating = 70f, Formation = Formations.F433, Kit = new Color(0.2f, 0.7f, 0.3f) },
            new OpponentInfo { Name = "Racing Delta", StyleIndex = 0, Rating = 64f, Formation = Formations.F442, Kit = new Color(0.6f, 0.2f, 0.7f) },
            new OpponentInfo { Name = "Sporting Lago", StyleIndex = 1, Rating = 72f, Formation = Formations.F433, Kit = new Color(0.1f, 0.75f, 0.8f) }
        };

        /// <summary>Nombres en español de los estilos de <c>MatchConfig.Styles</c>, en el mismo orden.</summary>
        public static readonly string[] StyleLabels = { "Equilibrado", "Posesión", "Contraataque", "Presión alta", "Defensivo" };

        /// <summary>Nombres en español de las dificultades de <c>MatchConfig.Difficulties</c>, en el mismo orden.</summary>
        public static readonly string[] DifficultyLabels = { "Principiante", "Amateur", "Profesional", "Mundial", "Leyenda" };

        /// <summary>Opciones de duración de cada parte, en minutos reales.</summary>
        public static readonly int[] HalfMinutes = { 2, 3, 5 };
    }
}
