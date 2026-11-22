using System;
using System.Collections.Generic;
using UnityEngine;
using Football.Core.Data;
using Football.Core.MatchCore;
using Football.Core.Domain;

namespace Football.Game
{
    /// <summary>Diseño de la camiseta.</summary>
    public enum KitPattern
    {
        /// <summary>Lisa.</summary>
        Solid,
        /// <summary>Rayas verticales.</summary>
        Stripes,
        /// <summary>Franja horizontal en el pecho.</summary>
        Band,
        /// <summary>Banda diagonal.</summary>
        Sash,
        /// <summary>V en el pecho (como la de Vélez).</summary>
        Chevron,
        /// <summary>Mitades verticales de dos colores (como la de Newell's).</summary>
        Halves
    }

    /// <summary>Colores de una equipación ya convertidos para Unity.</summary>
    public struct KitColors
    {
        /// <summary>Color principal de la camiseta.</summary>
        public Color Shirt;
        /// <summary>Color secundario (rayas, franja o banda).</summary>
        public Color Secondary;
        /// <summary>Pantalón.</summary>
        public Color Shorts;
        /// <summary>Medias.</summary>
        public Color Socks;
        /// <summary>Diseño.</summary>
        public KitPattern Pattern;

        /// <summary>Convierte la equipación del archivo de datos.</summary>
        public static KitColors From(KitDefinition kit)
        {
            KitPattern pattern;
            if (!Enum.TryParse(kit.Pattern, true, out pattern))
            {
                pattern = KitPattern.Solid;
            }

            return new KitColors
            {
                Shirt = Parse(kit.Shirt, Color.white),
                Secondary = Parse(kit.Secondary, Color.black),
                Shorts = Parse(kit.Shorts, Color.white),
                Socks = Parse(kit.Socks, Color.white),
                Pattern = pattern
            };
        }

        private static Color Parse(string hex, Color fallback) => ColorUtility.TryParseHtmlString(hex, out Color c) ? c : fallback;

        /// <summary>Diferencia de color entre dos camisetas (0 = iguales), para evitar que se confundan.</summary>
        public float Distance(KitColors other)
        {
            float dr = Shirt.r - other.Shirt.r, dg = Shirt.g - other.Shirt.g, db = Shirt.b - other.Shirt.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db);
        }
    }

    /// <summary>
    /// Equipos disponibles. Se leen de <c>Resources/teams.json</c> (copia de <c>Data/teams/argentina.json</c>); si el archivo
    /// falta o está mal, se usan dos equipos ficticios para que el juego siga funcionando.
    /// </summary>
    public static class TeamCatalog
    {
        /// <summary>Por debajo de esta diferencia de color, las camisetas se consideran demasiado parecidas.</summary>
        public const float KitClashDistance = 0.35f;

        private const string ResourceName = "teams";
        private static TeamDefinition[] _teams;
        private static float[] _ratings;

        /// <summary>Nombres en español de las dificultades de <c>MatchConfig.Difficulties</c>, en el mismo orden.</summary>
        public static readonly string[] DifficultyLabels = { "Principiante", "Amateur", "Profesional", "Mundial", "Leyenda" };

        /// <summary>Nombres de las mentalidades, de muy defensiva (índice 0) a muy ofensiva (índice 4).</summary>
        public static readonly string[] MentalityLabels = { "MUY DEFENSIVA", "DEFENSIVA", "EQUILIBRADA", "OFENSIVA", "MUY OFENSIVA" };

        /// <summary>Nombres de los niveles de presión, de baja (índice 0) a alta (índice 2).</summary>
        public static readonly string[] PressLabels = { "BAJA", "NORMAL", "ALTA" };

        /// <summary>Opciones de duración de cada parte, en minutos reales.</summary>
        public static readonly int[] HalfMinutes = { 2, 3, 5 };

        /// <summary>Motivo por el que no se pudo leer el archivo de equipos, o null.</summary>
        public static string LoadError { get; private set; }

        private static TeamDatabase _database;

        /// <summary>Equipos válidos y clásicos (para el modo carrera y el torneo).</summary>
        public static TeamDatabase Database
        {
            get
            {
                TeamDefinition[] teams = Teams; // carga el archivo (y los clásicos) si hace falta
                if (_database == null)
                {
                    _database = new TeamDatabase { Teams = teams, Rivalries = Array.Empty<RivalryDefinition>() };
                }

                return _database;
            }
        }

        /// <summary>Equipos disponibles.</summary>
        public static TeamDefinition[] Teams
        {
            get
            {
                if (_teams == null)
                {
                    Load();
                }

                return _teams;
            }
        }

        /// <summary>Media del once titular de un equipo.</summary>
        public static float Rating(int index)
        {
            if (_ratings == null)
            {
                _ratings = new float[Teams.Length];
                for (int i = 0; i < _ratings.Length; i++)
                {
                    _ratings[i] = TeamBuilder.LineupRating(Teams[i]);
                }
            }

            return _ratings[index];
        }

        /// <summary>Nombre del estilo en español.</summary>
        public static string StyleLabel(string style)
        {
            switch (style)
            {
                case "Possession": return "Posesión";
                case "Counter Attack": return "Contraataque";
                case "High Press": return "Presión alta";
                case "Defensive": return "Defensivo";
                default: return "Equilibrado";
            }
        }

        /// <summary>Nombre para el marcador: el completo si es corto, si no la abreviatura.</summary>
        public static string ScoreboardName(TeamDefinition team) =>
            team.Name.Length <= 15 || string.IsNullOrEmpty(team.ShortName) ? team.Name.ToUpperInvariant() : team.ShortName.ToUpperInvariant();

        private static void Load()
        {
            TextAsset asset = Resources.Load<TextAsset>(ResourceName);
            if (asset != null)
            {
                try
                {
                    TeamDatabase db = JsonUtility.FromJson<TeamDatabase>(asset.text);
                    var valid = new List<TeamDefinition>();
                    if (db != null && db.Teams != null)
                    {
                        foreach (TeamDefinition team in db.Teams)
                        {
                            string problem = TeamBuilder.Validate(team);
                            if (problem == null)
                            {
                                valid.Add(team);
                            }
                            else
                            {
                                // Un equipo mal cargado se descarta (y se avisa) en lugar de romper el partido.
                                LoadError = (LoadError == null ? string.Empty : LoadError + "; ") + problem;
                                Debug.LogWarning("Football: equipo descartado: " + problem);
                            }
                        }
                    }

                    if (valid.Count >= 2)
                    {
                        _teams = valid.ToArray();
                        _database = new TeamDatabase { Teams = _teams, Rivalries = db.Rivalries ?? Array.Empty<RivalryDefinition>() };
                        return;
                    }

                    LoadError = "el archivo de equipos no tiene al menos dos equipos válidos" + (LoadError == null ? string.Empty : " (" + LoadError + ")");
                }
                catch (Exception e)
                {
                    LoadError = e.Message;
                    Debug.LogException(e);
                }
            }
            else
            {
                LoadError = "no se encontró Resources/teams.json";
            }

            _teams = new[] { Fictional("Halcones del Sur", "#C62828", "Possession"), Fictional("Real Montaña", "#F5F5F5", "Defensive") };
        }

        private static TeamDefinition Fictional(string name, string color, string style)
        {
            string[] positions = { "POR", "LI", "DFC", "DFC", "LD", "MI", "MC", "MC", "MD", "DC", "DC", "POR", "DFC", "MC", "DC" };
            var players = new PlayerDefinition[positions.Length];
            for (int i = 0; i < players.Length; i++)
            {
                players[i] = new PlayerDefinition { Name = name + " " + (i + 1), ShortName = "Jugador " + (i + 1), Position = positions[i], Rating = 64 };
            }

            return new TeamDefinition
            {
                Name = name,
                ShortName = name.Substring(0, 3).ToUpperInvariant(),
                Style = style,
                Formation = "4-4-2",
                Home = new KitDefinition { Shirt = color, Shorts = "#FFFFFF", Socks = color },
                Away = new KitDefinition { Shirt = "#202020", Shorts = "#202020", Socks = "#202020" },
                Players = players
            };
        }
    }
}
