using System.Collections.Generic;
using Football.Core.Domain;

namespace Football.Core.Persistence
{
    /// <summary>
    /// Historial completo de migraciones del esquema. Regla: una migración publicada nunca se edita;
    /// los cambios se añaden como una versión nueva al final.
    /// </summary>
    public static class Migrations
    {
        /// <summary>Todas las migraciones, en orden de versión.</summary>
        public static readonly IReadOnlyList<IMigration> All = new IMigration[]
        {
            new SqlMigration(1, "Esquema inicial: estadios, clubes, jugadores y atributos",
                @"CREATE TABLE stadiums (
                    id INTEGER PRIMARY KEY,
                    name TEXT NOT NULL,
                    capacity INTEGER NOT NULL CHECK (capacity > 0))",
                @"CREATE TABLE clubs (
                    id INTEGER PRIMARY KEY,
                    name TEXT NOT NULL,
                    short_name TEXT NOT NULL,
                    country TEXT NOT NULL,
                    primary_color TEXT NOT NULL,
                    secondary_color TEXT NOT NULL,
                    stadium_id INTEGER REFERENCES stadiums(id) ON DELETE SET NULL,
                    transfer_budget INTEGER NOT NULL,
                    wage_budget INTEGER NOT NULL)",
                @"CREATE TABLE players (
                    id INTEGER PRIMARY KEY,
                    club_id INTEGER REFERENCES clubs(id) ON DELETE SET NULL,
                    first_name TEXT NOT NULL,
                    last_name TEXT NOT NULL,
                    birth_date TEXT NOT NULL,
                    position INTEGER NOT NULL,
                    potential INTEGER NOT NULL)",
                // Migración publicada: los límites van escritos tal cual (1..100) para que no cambien si cambia la escala.
                @"CREATE TABLE player_attributes (
                    player_id INTEGER NOT NULL REFERENCES players(id) ON DELETE CASCADE,
                    attribute_id INTEGER NOT NULL,
                    value INTEGER NOT NULL CHECK (value BETWEEN 1 AND 100),
                    PRIMARY KEY (player_id, attribute_id))",
                "CREATE INDEX ix_players_club ON players(club_id)"),

            new SqlMigration(2, "Contratos de jugadores",
                @"CREATE TABLE contracts (
                    player_id INTEGER PRIMARY KEY REFERENCES players(id) ON DELETE CASCADE,
                    weekly_wage INTEGER NOT NULL CHECK (weekly_wage >= 0),
                    expires_on TEXT NOT NULL)")
        };
    }
}
