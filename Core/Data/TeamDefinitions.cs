using System;

namespace Football.Core.Data
{
    // Formato de los archivos de equipos (Data/teams/*.json). Son clases con campos públicos para que se puedan leer con
    // JsonUtility en Unity y con System.Text.Json fuera. El usuario puede editarlos a mano para añadir o cambiar jugadores.

    /// <summary>Equipación: colores en formato <c>#RRGGBB</c> y diseño de la camiseta.</summary>
    [Serializable]
    public sealed class KitDefinition
    {
        /// <summary>Color principal de la camiseta.</summary>
        public string Shirt = "#FFFFFF";
        /// <summary>Color secundario (franja, banda o rayas).</summary>
        public string Secondary = "#000000";
        /// <summary>Diseño: <c>Solid</c> (liso), <c>Stripes</c> (rayas verticales), <c>Band</c> (franja horizontal) o <c>Sash</c> (banda diagonal).</summary>
        public string Pattern = "Solid";
        /// <summary>Color del pantalón.</summary>
        public string Shorts = "#FFFFFF";
        /// <summary>Color de las medias.</summary>
        public string Socks = "#FFFFFF";
    }

    /// <summary>Jugador de una plantilla.</summary>
    [Serializable]
    public sealed class PlayerDefinition
    {
        /// <summary>Nombre completo.</summary>
        public string Name;
        /// <summary>Nombre corto que se muestra sobre el jugador (si falta, se usa el apellido).</summary>
        public string ShortName;
        /// <summary>Dorsal.</summary>
        public int Number;
        /// <summary>
        /// Posición natural: POR, DFC, LI, LD, MCD, MC, MCO, MI, MD, EI, ED o DC
        /// (portero, central, laterales, mediocentros, volantes, extremos, delantero centro).
        /// </summary>
        public string Position = "MC";
        /// <summary>Media de 1 a 100.</summary>
        public int Rating = 65;
    }

    /// <summary>Equipo con sus equipaciones, estilo y plantilla.</summary>
    [Serializable]
    public sealed class TeamDefinition
    {
        /// <summary>Nombre completo.</summary>
        public string Name;
        /// <summary>Nombre corto para el marcador (3 o 4 letras).</summary>
        public string ShortName;
        /// <summary>Estilo de juego: nombre de uno de <c>MatchConfig.Styles</c> (Balanced, Possession, Counter Attack, High Press, Defensive).</summary>
        public string Style = "Balanced";
        /// <summary>Formación: <c>4-4-2</c> o <c>4-3-3</c>.</summary>
        public string Formation = "4-4-2";
        /// <summary>Equipación titular.</summary>
        public KitDefinition Home = new KitDefinition();
        /// <summary>Equipación alternativa.</summary>
        public KitDefinition Away = new KitDefinition();
        /// <summary>Plantilla completa (se elige el mejor once para la formación).</summary>
        public PlayerDefinition[] Players = Array.Empty<PlayerDefinition>();
    }

    /// <summary>Archivo con varios equipos.</summary>
    [Serializable]
    public sealed class TeamDatabase
    {
        /// <summary>De dónde salen los datos.</summary>
        public string Source;
        /// <summary>Fecha de la última actualización (AAAA-MM-DD).</summary>
        public string Updated;
        /// <summary>Notas sobre los datos (por ejemplo, cómo se estimaron las medias).</summary>
        public string Notes;
        /// <summary>Equipos.</summary>
        public TeamDefinition[] Teams = Array.Empty<TeamDefinition>();
    }
}
