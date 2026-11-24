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
                    expires_on TEXT NOT NULL)"),

            // ---- Fase 2 en adelante: gestión. Cada migración publicada no se vuelve a editar; los cambios van en una nueva. ----

            new SqlMigration(3, "Cláusulas de contrato, promesa de minutos, personalidad y antigüedad",
                "ALTER TABLE contracts ADD COLUMN release_clause INTEGER NOT NULL DEFAULT 0 CHECK (release_clause >= 0)",
                "ALTER TABLE contracts ADD COLUMN promise INTEGER NOT NULL DEFAULT 0",
                "ALTER TABLE players ADD COLUMN personality INTEGER NOT NULL DEFAULT 0",
                "ALTER TABLE players ADD COLUMN joined_on TEXT"),

            new SqlMigration(4, "Finanzas e infraestructura",
                @"CREATE TABLE club_finances (
                    club_id INTEGER PRIMARY KEY REFERENCES clubs(id) ON DELETE CASCADE,
                    cash INTEGER NOT NULL,
                    transfer_budget INTEGER NOT NULL CHECK (transfer_budget >= 0),
                    weekly_wage_budget INTEGER NOT NULL CHECK (weekly_wage_budget >= 0),
                    members INTEGER NOT NULL DEFAULT 0 CHECK (members >= 0),
                    ticket_price INTEGER NOT NULL DEFAULT 0 CHECK (ticket_price >= 0),
                    reputation INTEGER NOT NULL DEFAULT 50 CHECK (reputation BETWEEN 1 AND 100))",
                @"CREATE TABLE ledger (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    club_id INTEGER NOT NULL REFERENCES clubs(id) ON DELETE CASCADE,
                    date TEXT NOT NULL,
                    category INTEGER NOT NULL,
                    amount INTEGER NOT NULL)",
                "CREATE INDEX ix_ledger_club_date ON ledger(club_id, date)",
                @"CREATE TABLE facilities (
                    club_id INTEGER NOT NULL REFERENCES clubs(id) ON DELETE CASCADE,
                    kind INTEGER NOT NULL,
                    level INTEGER NOT NULL DEFAULT 1 CHECK (level BETWEEN 1 AND 5),
                    weeks_to_finish INTEGER NOT NULL DEFAULT 0 CHECK (weeks_to_finish >= 0),
                    PRIMARY KEY (club_id, kind))",
                // Los clubes que ya existían arrancan con sus presupuestos de la v1 y las instalaciones en nivel 1.
                @"INSERT INTO club_finances (club_id, cash, transfer_budget, weekly_wage_budget)
                    SELECT id, transfer_budget, transfer_budget, wage_budget FROM clubs",
                @"INSERT INTO facilities (club_id, kind) SELECT id, k FROM clubs, (SELECT 0 AS k UNION ALL SELECT 1 UNION ALL SELECT 2 UNION ALL SELECT 3)"),

            new SqlMigration(5, "Competiciones y partidos",
                @"CREATE TABLE competitions (
                    id INTEGER PRIMARY KEY,
                    name TEXT NOT NULL,
                    season INTEGER NOT NULL,
                    kind INTEGER NOT NULL,
                    tiebreakers TEXT NOT NULL DEFAULT '')",
                @"CREATE TABLE fixtures (
                    id INTEGER PRIMARY KEY,
                    competition_id INTEGER NOT NULL REFERENCES competitions(id) ON DELETE CASCADE,
                    round INTEGER NOT NULL,
                    grp INTEGER NOT NULL DEFAULT 0,
                    date TEXT NOT NULL,
                    home_club_id INTEGER NOT NULL REFERENCES clubs(id),
                    away_club_id INTEGER NOT NULL REFERENCES clubs(id),
                    home_goals INTEGER,
                    away_goals INTEGER,
                    home_penalties INTEGER,
                    away_penalties INTEGER,
                    CHECK (home_club_id <> away_club_id))",
                "CREATE INDEX ix_fixtures_competition_round ON fixtures(competition_id, round)",
                @"CREATE TABLE rivalries (
                    club_a INTEGER NOT NULL REFERENCES clubs(id) ON DELETE CASCADE,
                    club_b INTEGER NOT NULL REFERENCES clubs(id) ON DELETE CASCADE,
                    level INTEGER NOT NULL CHECK (level BETWEEN 1 AND 3),
                    name TEXT,
                    PRIMARY KEY (club_a, club_b))"),

            new SqlMigration(6, "Estado del plantel, cuerpo técnico y scouting",
                @"CREATE TABLE player_conditions (
                    player_id INTEGER PRIMARY KEY REFERENCES players(id) ON DELETE CASCADE,
                    form REAL NOT NULL DEFAULT 6,
                    morale REAL NOT NULL DEFAULT 50,
                    fitness REAL NOT NULL DEFAULT 1 CHECK (fitness BETWEEN 0 AND 1),
                    injury_days INTEGER NOT NULL DEFAULT 0,
                    suspended_matches INTEGER NOT NULL DEFAULT 0,
                    accumulated_yellows INTEGER NOT NULL DEFAULT 0,
                    season_minutes REAL NOT NULL DEFAULT 0,
                    promise INTEGER NOT NULL DEFAULT 0,
                    promise_matches INTEGER NOT NULL DEFAULT 0,
                    promise_minutes REAL NOT NULL DEFAULT 0)",
                @"CREATE TABLE staff (
                    id INTEGER PRIMARY KEY,
                    club_id INTEGER REFERENCES clubs(id) ON DELETE SET NULL,
                    name TEXT NOT NULL,
                    role INTEGER NOT NULL,
                    ability INTEGER NOT NULL CHECK (ability BETWEEN 1 AND 20),
                    weekly_wage INTEGER NOT NULL DEFAULT 0,
                    former_player_id INTEGER)",
                @"CREATE TABLE scouting (
                    club_id INTEGER NOT NULL REFERENCES clubs(id) ON DELETE CASCADE,
                    player_id INTEGER NOT NULL REFERENCES players(id) ON DELETE CASCADE,
                    knowledge REAL NOT NULL CHECK (knowledge BETWEEN 0 AND 1),
                    PRIMARY KEY (club_id, player_id))",
                // Todo jugador que ya existía arranca descansado y sin sanciones.
                "INSERT INTO player_conditions (player_id) SELECT id FROM players"),

            new SqlMigration(7, "Buzón de noticias e historia del club",
                @"CREATE TABLE news (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    club_id INTEGER NOT NULL REFERENCES clubs(id) ON DELETE CASCADE,
                    date TEXT NOT NULL,
                    key TEXT NOT NULL,
                    args TEXT NOT NULL DEFAULT '',
                    read INTEGER NOT NULL DEFAULT 0)",
                @"CREATE TABLE records (
                    key TEXT PRIMARY KEY,
                    value REAL NOT NULL,
                    holder_id INTEGER NOT NULL,
                    season INTEGER NOT NULL)",
                @"CREATE TABLE hall_of_fame (
                    player_id INTEGER PRIMARY KEY,
                    inducted_season INTEGER NOT NULL)"),

            new SqlMigration(8, "Estado de la partida del modo carrera (fecha, temporada, club del usuario y semilla)",
                @"CREATE TABLE career_meta (
                    id INTEGER PRIMARY KEY CHECK (id = 1),
                    seed INTEGER NOT NULL,
                    season INTEGER NOT NULL,
                    today TEXT NOT NULL,
                    user_club_id INTEGER NOT NULL REFERENCES clubs(id),
                    tournament_only INTEGER NOT NULL DEFAULT 0,
                    formation TEXT NOT NULL DEFAULT '4-4-2',
                    defensive_formation TEXT NOT NULL DEFAULT '',
                    style TEXT NOT NULL DEFAULT 'Balanced',
                    fullback_duty INTEGER NOT NULL DEFAULT 0,
                    midfield_duty INTEGER NOT NULL DEFAULT 0,
                    striker_duty INTEGER NOT NULL DEFAULT 0)"),

            new SqlMigration(9, "Estadísticas de la temporada de cada jugador (ficha del jugador)",
                @"CREATE TABLE player_season_stats (
                    player_id INTEGER PRIMARY KEY REFERENCES players(id) ON DELETE CASCADE,
                    matches INTEGER NOT NULL DEFAULT 0 CHECK (matches >= 0),
                    goals INTEGER NOT NULL DEFAULT 0 CHECK (goals >= 0),
                    assists INTEGER NOT NULL DEFAULT 0 CHECK (assists >= 0),
                    rating_sum REAL NOT NULL DEFAULT 0,
                    rated INTEGER NOT NULL DEFAULT 0 CHECK (rated >= 0))"),

            new SqlMigration(10, "Modo carrera completo: DT, datos de clubes, estadio y obras, entradas, socios, fichajes y medidor de gestión",
                @"CREATE TABLE manager (
                    id INTEGER PRIMARY KEY CHECK (id = 1),
                    first_name TEXT NOT NULL,
                    last_name TEXT NOT NULL,
                    nationality TEXT NOT NULL,
                    avatar INTEGER NOT NULL,
                    outfit INTEGER NOT NULL,
                    reputation REAL NOT NULL,
                    popularity REAL NOT NULL CHECK (popularity BETWEEN 0 AND 100),
                    sacked INTEGER NOT NULL DEFAULT 0)",
                @"CREATE TABLE club_info (
                    club_id INTEGER PRIMARY KEY REFERENCES clubs(id) ON DELETE CASCADE,
                    founded INTEGER NOT NULL,
                    stadium_name TEXT NOT NULL,
                    club_value INTEGER NOT NULL,
                    tier TEXT NOT NULL,
                    history TEXT NOT NULL DEFAULT '')",
                @"CREATE TABLE club_stadiums (
                    club_id INTEGER PRIMARY KEY REFERENCES clubs(id) ON DELETE CASCADE,
                    name TEXT NOT NULL,
                    capacity INTEGER NOT NULL CHECK (capacity >= 0),
                    parking_spaces INTEGER NOT NULL CHECK (parking_spaces >= 0),
                    parking_level INTEGER NOT NULL DEFAULT 0 CHECK (parking_level BETWEEN 0 AND 3),
                    equipment_level INTEGER NOT NULL DEFAULT 0 CHECK (equipment_level BETWEEN 0 AND 3),
                    services_level INTEGER NOT NULL DEFAULT 0 CHECK (services_level BETWEEN 0 AND 3),
                    pitch INTEGER NOT NULL)",
                @"CREATE TABLE stadium_works (
                    club_id INTEGER NOT NULL REFERENCES clubs(id) ON DELETE CASCADE,
                    category INTEGER NOT NULL,
                    tier INTEGER NOT NULL CHECK (tier BETWEEN 1 AND 3),
                    total_weeks INTEGER NOT NULL CHECK (total_weeks > 0),
                    remaining_weeks INTEGER NOT NULL CHECK (remaining_weeks >= 0),
                    cost INTEGER NOT NULL,
                    PRIMARY KEY (club_id, category))",
                @"CREATE TABLE supporters (
                    id INTEGER PRIMARY KEY CHECK (id = 1),
                    relation REAL NOT NULL,
                    unrest REAL NOT NULL CHECK (unrest BETWEEN 0 AND 100),
                    critical_streak INTEGER NOT NULL,
                    pending_fixture_id INTEGER NOT NULL,
                    pending_price INTEGER NOT NULL,
                    accepted INTEGER NOT NULL,
                    ignored INTEGER NOT NULL)",
                @"CREATE TABLE ticket_history (
                    fixture_id INTEGER PRIMARY KEY,
                    date TEXT NOT NULL,
                    price INTEGER NOT NULL,
                    min_ideal INTEGER NOT NULL,
                    max_ideal INTEGER NOT NULL,
                    attendance REAL NOT NULL CHECK (attendance BETWEEN 0 AND 1),
                    revenue INTEGER NOT NULL)",
                @"CREATE TABLE signings (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    player_id INTEGER NOT NULL,
                    name TEXT NOT NULL,
                    fee INTEGER NOT NULL,
                    date TEXT NOT NULL,
                    matches INTEGER NOT NULL,
                    rating_sum REAL NOT NULL,
                    contributions INTEGER NOT NULL,
                    team_matches INTEGER NOT NULL)",
                @"CREATE TABLE management_history (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    date TEXT NOT NULL,
                    supporters REAL NOT NULL,
                    squad REAL NOT NULL,
                    budget REAL NOT NULL,
                    total REAL NOT NULL)",
                "ALTER TABLE career_meta ADD COLUMN ticket_level INTEGER NOT NULL DEFAULT 1",
                "ALTER TABLE career_meta ADD COLUMN next_price_override INTEGER NOT NULL DEFAULT 0",
                "ALTER TABLE career_meta ADD COLUMN season_start_cash INTEGER NOT NULL DEFAULT 0"),

            new SqlMigration(11, "Alineación elegida por el DT (once inicial y banco)",
                "ALTER TABLE career_meta ADD COLUMN lineup TEXT NOT NULL DEFAULT ''"),

            new SqlMigration(12, "Divisiones: Primera y Nacional B",
                "ALTER TABLE clubs ADD COLUMN division INTEGER NOT NULL DEFAULT 1",
                "ALTER TABLE career_meta ADD COLUMN primera_size INTEGER NOT NULL DEFAULT 0"),

            new SqlMigration(13, "Dorsales de los jugadores",
                "ALTER TABLE players ADD COLUMN shirt_number INTEGER NOT NULL DEFAULT 0"),

            new SqlMigration(14, "Negociaciones de renovación",
                @"CREATE TABLE renewal_talks (
                    player_id INTEGER PRIMARY KEY, offer_wage INTEGER NOT NULL, offer_years INTEGER NOT NULL, offer_promise INTEGER NOT NULL,
                    sent_on TEXT NOT NULL, reply_on TEXT NOT NULL, status INTEGER NOT NULL, reason INTEGER NOT NULL,
                    demand_wage INTEGER NOT NULL, demand_years INTEGER NOT NULL, rounds INTEGER NOT NULL)"),

            new SqlMigration(15, "Nacionalidad de los jugadores",
                "ALTER TABLE players ADD COLUMN nationality TEXT NOT NULL DEFAULT 'ARG'"),

            new SqlMigration(16, "Rol que pide el jugador al renovar",
                "ALTER TABLE renewal_talks ADD COLUMN demand_role INTEGER NOT NULL DEFAULT 2"),

            new SqlMigration(17, "Reserva, debut, desarrollo, declive, retiro anunciado y estadísticas de la reserva",
                "ALTER TABLE player_conditions ADD COLUMN in_reserve INTEGER NOT NULL DEFAULT 0",
                "ALTER TABLE player_conditions ADD COLUMN debuted INTEGER NOT NULL DEFAULT 1",
                "ALTER TABLE player_conditions ADD COLUMN dev_progress REAL NOT NULL DEFAULT 0",
                "ALTER TABLE player_conditions ADD COLUMN dev_minutes_mark REAL NOT NULL DEFAULT 0",
                "ALTER TABLE player_conditions ADD COLUMN retiring INTEGER NOT NULL DEFAULT 0",
                "ALTER TABLE player_conditions ADD COLUMN decline_progress REAL NOT NULL DEFAULT 0",
                @"CREATE TABLE reserve_stats (
                    player_id INTEGER PRIMARY KEY, matches INTEGER NOT NULL, goals INTEGER NOT NULL, assists INTEGER NOT NULL,
                    rating_sum REAL NOT NULL, rated INTEGER NOT NULL)"),

            new SqlMigration(18, "Media al empezar la temporada",
                "ALTER TABLE player_conditions ADD COLUMN season_start_rating INTEGER NOT NULL DEFAULT 0")
        };
    }
}
