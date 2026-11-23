using System;
using System.Collections.Generic;
using System.Data.Common;
using Football.Core.Career;
using Football.Core.Domain;
using Football.Core.Management;
using Football.Core.MatchCore;

namespace Football.Core.Persistence
{
    /// <summary>
    /// Guarda y lee una partida completa del modo carrera sobre todas las tablas del esquema (v1 a v8): clubes, jugadores,
    /// atributos, contratos con cláusula y promesa, finanzas y libro contable, instalaciones, torneos y partidos, clásicos,
    /// estado de cada jugador, cuerpo técnico, ojeadores, buzón, récords, salón de la fama y estado de la partida.
    /// El guardado es todo o nada (<see cref="AtomicSave.InTransaction"/>): si algo falla, la base queda como estaba. Para
    /// pasarlo a un archivo sin riesgo de dejarlo a medias, usar después <see cref="AtomicSave.SaveSqlite"/>.
    /// </summary>
    public sealed partial class SqlCareerRepository : SqlRepositoryBase
    {
        private const string CareerGoalsPrefix = "goals.";
        private const char ArgsSeparator = '\u001f';

        private static readonly string[] TablesChildrenFirst =
        {
            "career_meta", "management_history", "signings", "ticket_history", "supporters", "stadium_works", "club_stadiums", "club_info", "manager",
            "player_season_stats", "news", "records", "hall_of_fame", "scouting", "staff", "player_conditions", "rivalries", "fixtures",
            "competitions", "facilities", "ledger", "club_finances", "contracts", "player_attributes", "players", "clubs"
        };

        /// <summary>Crea el repositorio.</summary>
        /// <param name="connection">Conexión abierta, migrada a la última versión y configurada con <see cref="SqliteConnectionSetup"/>.</param>
        public SqlCareerRepository(DbConnection connection) : base(connection) { }

        /// <summary>Hay una partida guardada en la base.</summary>
        public bool HasCareer() => Convert.ToInt64(SqlHelper.Scalar(Connection, "SELECT COUNT(*) FROM career_meta", null)) > 0;

        /// <summary>Reemplaza la partida guardada por la indicada, en una sola transacción.</summary>
        /// <param name="data">Estado de la partida (llamar antes a <see cref="CareerSession.SyncData"/>).</param>
        public void Save(CareerData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            AtomicSave.InTransaction(Connection, tx =>
            {
                foreach (string table in TablesChildrenFirst)
                {
                    SqlHelper.Execute(Connection, "DELETE FROM " + table, tx);
                }

                SaveClubs(data, tx);
                SavePlayers(data, tx);
                SaveCompetitions(data, tx);
                SavePeople(data, tx);
                SaveHistory(data, tx);
                SaveClubManagement(data, tx);
                UserTactics t = data.Tactics ?? new UserTactics();
                SqlHelper.Execute(Connection,
                    @"INSERT INTO career_meta (id, seed, season, today, user_club_id, tournament_only, formation, defensive_formation, style,
                        fullback_duty, midfield_duty, striker_duty, ticket_level, next_price_override, season_start_cash, lineup)
                      VALUES (1, @seed, @season, @today, @user, @tournament, @formation, @defensive, @style, @fb, @mid, @st, @level, @override, @startcash, @lineup)", tx,
                    ("@lineup", t.Lineup ?? string.Empty),
                    ("@level", (int)data.TicketLevel), ("@override", data.NextPriceOverride), ("@startcash", data.SeasonStartCash),
                    ("@seed", data.Seed), ("@season", data.Season), ("@today", DateFormat.ToText(data.Today)), ("@user", data.UserClubId),
                    ("@tournament", data.TournamentOnly ? 1 : 0), ("@formation", t.Formation ?? string.Empty),
                    ("@defensive", t.DefensiveFormation ?? string.Empty), ("@style", t.Style ?? string.Empty), ("@fb", (int)t.FullbackDuty),
                    ("@mid", (int)t.MidfieldDuty), ("@st", (int)t.StrikerDuty));
            });
        }

        /// <summary>Lee la partida guardada (null si no hay).</summary>
        public CareerData Load()
        {
            if (!HasCareer())
            {
                return null;
            }

            var data = new CareerData();
            Read(@"SELECT seed, season, today, user_club_id, tournament_only, formation, defensive_formation, style, fullback_duty,
                     midfield_duty, striker_duty, ticket_level, next_price_override, season_start_cash, lineup FROM career_meta", r =>
            {
                data.Seed = r.GetInt32(0);
                data.Season = r.GetInt32(1);
                data.Today = DateFormat.Parse(r.GetString(2));
                data.UserClubId = r.GetInt32(3);
                data.TournamentOnly = r.GetInt32(4) != 0;
                data.Tactics = new UserTactics
                {
                    Formation = r.GetString(5), DefensiveFormation = r.GetString(6), Style = r.GetString(7),
                    FullbackDuty = (PlayerDuty)r.GetInt32(8), MidfieldDuty = (PlayerDuty)r.GetInt32(9), StrikerDuty = (PlayerDuty)r.GetInt32(10),
                    Lineup = r.GetString(14)
                };
                data.TicketLevel = (TicketLevel)r.GetInt32(11);
                data.NextPriceOverride = r.GetInt64(12);
                data.SeasonStartCash = r.GetInt64(13);
            });
            LoadClubs(data);
            LoadPlayers(data);
            LoadCompetitions(data);
            LoadPeople(data);
            LoadHistory(data);
            LoadClubManagement(data);
            return data;
        }

        // ------------------------------------------------------------------ clubes y finanzas

        private void SaveClubs(CareerData data, DbTransaction tx)
        {
            foreach (Club c in data.Clubs)
            {
                SqlHelper.Execute(Connection,
                    @"INSERT INTO clubs (id, name, short_name, country, primary_color, secondary_color, stadium_id, transfer_budget, wage_budget)
                      VALUES (@id, @name, @short, @country, @primary, @secondary, @stadium, @transfer, @wage)", tx,
                    ("@id", c.Id), ("@name", c.Name), ("@short", c.ShortName ?? c.Name), ("@country", c.Country ?? string.Empty),
                    ("@primary", c.PrimaryColor ?? string.Empty), ("@secondary", c.SecondaryColor ?? string.Empty), ("@stadium", c.StadiumId),
                    ("@transfer", c.TransferBudget), ("@wage", c.WageBudget));
                ClubFinances f = data.Finances[c.Id];
                SqlHelper.Execute(Connection,
                    @"INSERT INTO club_finances (club_id, cash, transfer_budget, weekly_wage_budget, members, ticket_price, reputation)
                      VALUES (@id, @cash, @transfer, @wages, @members, @ticket, @reputation)", tx,
                    ("@id", c.Id), ("@cash", f.Cash), ("@transfer", Math.Max(0, f.TransferBudget)), ("@wages", Math.Max(0, f.WeeklyWageBudget)),
                    ("@members", Math.Max(0, f.Members)), ("@ticket", Math.Max(0, f.TicketPrice)), ("@reputation", Math.Max(1, Math.Min(100, f.Reputation))));
                foreach (LedgerEntry e in f.Ledger)
                {
                    SqlHelper.Execute(Connection, "INSERT INTO ledger (club_id, date, category, amount) VALUES (@id, @date, @category, @amount)", tx,
                        ("@id", c.Id), ("@date", DateFormat.ToText(e.Date)), ("@category", (int)e.Category), ("@amount", e.Amount));
                }

                foreach (Facility facility in data.Facilities[c.Id].All)
                {
                    SqlHelper.Execute(Connection, "INSERT INTO facilities (club_id, kind, level, weeks_to_finish) VALUES (@id, @kind, @level, @weeks)", tx,
                        ("@id", c.Id), ("@kind", (int)facility.Kind), ("@level", facility.Level), ("@weeks", facility.WeeksToFinish));
                }
            }

            foreach (ClubRivalry r in data.Rivalries)
            {
                SqlHelper.Execute(Connection, "INSERT INTO rivalries (club_a, club_b, level, name) VALUES (@a, @b, @level, @name)", tx,
                    ("@a", r.ClubA), ("@b", r.ClubB), ("@level", (int)r.Level), ("@name", r.Name));
            }
        }

        private void LoadClubs(CareerData data)
        {
            Read("SELECT id, name, short_name, country, primary_color, secondary_color, stadium_id, transfer_budget, wage_budget FROM clubs ORDER BY id",
                r => data.Clubs.Add(new Club
                {
                    Id = r.GetInt32(0), Name = r.GetString(1), ShortName = r.GetString(2), Country = r.GetString(3), PrimaryColor = r.GetString(4),
                    SecondaryColor = r.GetString(5), StadiumId = r.IsDBNull(6) ? (int?)null : r.GetInt32(6), TransferBudget = r.GetInt64(7),
                    WageBudget = r.GetInt64(8)
                }));
            Read("SELECT club_id, cash, transfer_budget, weekly_wage_budget, members, ticket_price, reputation FROM club_finances",
                r => data.Finances[r.GetInt32(0)] = new ClubFinances
                {
                    ClubId = r.GetInt32(0), Cash = r.GetInt64(1), TransferBudget = r.GetInt64(2), WeeklyWageBudget = r.GetInt64(3),
                    Members = r.GetInt32(4), TicketPrice = r.GetInt64(5), Reputation = r.GetInt32(6)
                });
            Read("SELECT club_id, date, category, amount FROM ledger ORDER BY id",
                r => data.Finances[r.GetInt32(0)].Ledger.Add(new LedgerEntry
                {
                    Date = DateFormat.Parse(r.GetString(1)), Category = (FinanceCategory)r.GetInt32(2), Amount = r.GetInt64(3)
                }));
            foreach (Club c in data.Clubs)
            {
                data.Facilities[c.Id] = new ClubFacilities { ClubId = c.Id };
            }

            Read("SELECT club_id, kind, level, weeks_to_finish FROM facilities", r =>
            {
                Facility f = data.Facilities[r.GetInt32(0)][(FacilityKind)r.GetInt32(1)];
                f.Level = r.GetInt32(2);
                f.WeeksToFinish = r.GetInt32(3);
            });
            Read("SELECT club_a, club_b, level, name FROM rivalries", r => data.Rivalries.Add(new ClubRivalry
            {
                ClubA = r.GetInt32(0), ClubB = r.GetInt32(1), Level = (RivalryLevel)r.GetInt32(2), Name = r.IsDBNull(3) ? null : r.GetString(3)
            }));
        }

        // ------------------------------------------------------------------ jugadores

        private void SavePlayers(CareerData data, DbTransaction tx)
        {
            foreach (Player p in data.Players.Values)
            {
                SqlHelper.Execute(Connection,
                    @"INSERT INTO players (id, club_id, first_name, last_name, birth_date, position, potential, personality, joined_on)
                      VALUES (@id, @club, @first, @last, @birth, @position, @potential, @personality, @joined)", tx,
                    ("@id", p.Id), ("@club", p.ClubId), ("@first", p.FirstName ?? string.Empty), ("@last", p.LastName ?? string.Empty),
                    ("@birth", DateFormat.ToText(p.BirthDate)), ("@position", (int)p.Position), ("@potential", p.Potential),
                    ("@personality", (int)p.Personality), ("@joined", DateFormat.ToText(p.JoinedOn)));
                foreach (AttributeId attribute in AttributeSet.All)
                {
                    SqlHelper.Execute(Connection, "INSERT INTO player_attributes (player_id, attribute_id, value) VALUES (@id, @a, @v)", tx,
                        ("@id", p.Id), ("@a", (int)attribute), ("@v", p.Attributes[attribute]));
                }

                if (p.Contract != null)
                {
                    SqlHelper.Execute(Connection,
                        @"INSERT INTO contracts (player_id, weekly_wage, expires_on, release_clause, promise)
                          VALUES (@id, @wage, @expires, @clause, @promise)", tx,
                        ("@id", p.Id), ("@wage", p.Contract.WeeklyWage), ("@expires", DateFormat.ToText(p.Contract.ExpiresOn)),
                        ("@clause", Math.Max(0, p.Contract.ReleaseClause)), ("@promise", (int)p.Contract.Promise));
                }

                if (data.Conditions.TryGetValue(p.Id, out PlayerCondition c))
                {
                    SqlHelper.Execute(Connection,
                        @"INSERT INTO player_conditions (player_id, form, morale, fitness, injury_days, suspended_matches, accumulated_yellows,
                            season_minutes, promise, promise_matches, promise_minutes)
                          VALUES (@id, @form, @morale, @fitness, @injury, @suspended, @yellows, @minutes, @promise, @pmatches, @pminutes)", tx,
                        ("@id", p.Id), ("@form", (double)c.Form), ("@morale", (double)c.Morale), ("@fitness", (double)Math.Max(0f, Math.Min(1f, c.Fitness))),
                        ("@injury", c.InjuryDays), ("@suspended", c.SuspendedMatches), ("@yellows", c.AccumulatedYellows),
                        ("@minutes", (double)c.SeasonMinutes), ("@promise", (int)c.Promise), ("@pmatches", c.TeamMatchesSincePromise),
                        ("@pminutes", (double)c.MinutesSincePromise));
                }
            }
        }

        private void LoadPlayers(CareerData data)
        {
            Read("SELECT id, club_id, first_name, last_name, birth_date, position, potential, personality, joined_on FROM players ORDER BY id",
                r => data.Players[r.GetInt32(0)] = new Player
                {
                    Id = r.GetInt32(0), ClubId = r.IsDBNull(1) ? (int?)null : r.GetInt32(1), FirstName = r.GetString(2), LastName = r.GetString(3),
                    BirthDate = DateFormat.Parse(r.GetString(4)), Position = (Position)r.GetInt32(5), Potential = r.GetInt32(6),
                    Personality = (PersonalityTrait)r.GetInt32(7), JoinedOn = r.IsDBNull(8) ? DateTime.MinValue : DateFormat.Parse(r.GetString(8))
                });
            Read("SELECT player_id, attribute_id, value FROM player_attributes",
                r => data.Players[r.GetInt32(0)].Attributes[(AttributeId)r.GetInt32(1)] = r.GetInt32(2));
            Read("SELECT player_id, weekly_wage, expires_on, release_clause, promise FROM contracts",
                r => data.Players[r.GetInt32(0)].Contract = new Contract
                {
                    WeeklyWage = r.GetInt64(1), ExpiresOn = DateFormat.Parse(r.GetString(2)), ReleaseClause = r.GetInt64(3),
                    Promise = (PlayingTimePromise)r.GetInt32(4)
                });
            Read(@"SELECT player_id, form, morale, fitness, injury_days, suspended_matches, accumulated_yellows, season_minutes, promise,
                     promise_matches, promise_minutes FROM player_conditions",
                r => data.Conditions[r.GetInt32(0)] = new PlayerCondition
                {
                    PlayerId = r.GetInt32(0), Form = (float)r.GetDouble(1), Morale = (float)r.GetDouble(2), Fitness = (float)r.GetDouble(3),
                    InjuryDays = r.GetInt32(4), SuspendedMatches = r.GetInt32(5), AccumulatedYellows = r.GetInt32(6),
                    SeasonMinutes = (float)r.GetDouble(7), Promise = (PlayingTimePromise)r.GetInt32(8), TeamMatchesSincePromise = r.GetInt32(9),
                    MinutesSincePromise = (float)r.GetDouble(10)
                });
        }

        // ------------------------------------------------------------------ torneos

        private void SaveCompetitions(CareerData data, DbTransaction tx)
        {
            foreach (CompetitionInfo c in data.Competitions)
            {
                SqlHelper.Execute(Connection, "INSERT INTO competitions (id, name, season, kind) VALUES (@id, @name, @season, @kind)", tx,
                    ("@id", c.Id), ("@name", c.Name), ("@season", c.Season), ("@kind", (int)c.Kind));
            }

            foreach (Fixture f in data.Fixtures)
            {
                SqlHelper.Execute(Connection,
                    @"INSERT INTO fixtures (id, competition_id, round, grp, date, home_club_id, away_club_id, home_goals, away_goals,
                        home_penalties, away_penalties)
                      VALUES (@id, @competition, @round, @group, @date, @home, @away, @hg, @ag, @hp, @ap)", tx,
                    ("@id", f.Id), ("@competition", f.CompetitionId), ("@round", f.Round), ("@group", f.Group), ("@date", DateFormat.ToText(f.Date)),
                    ("@home", f.HomeClubId), ("@away", f.AwayClubId), ("@hg", f.HomeGoals), ("@ag", f.AwayGoals), ("@hp", f.HomePenalties),
                    ("@ap", f.AwayPenalties));
            }
        }

        private void LoadCompetitions(CareerData data)
        {
            Read("SELECT id, name, season, kind FROM competitions ORDER BY id", r => data.Competitions.Add(new CompetitionInfo
            {
                Id = r.GetInt32(0), Name = r.GetString(1), Season = r.GetInt32(2), Kind = (TournamentKind)r.GetInt32(3)
            }));
            Read(@"SELECT id, competition_id, round, grp, date, home_club_id, away_club_id, home_goals, away_goals, home_penalties, away_penalties
                   FROM fixtures ORDER BY id", r => data.Fixtures.Add(new Fixture
            {
                Id = r.GetInt32(0), CompetitionId = r.GetInt32(1), Round = r.GetInt32(2), Group = r.GetInt32(3), Date = DateFormat.Parse(r.GetString(4)),
                HomeClubId = r.GetInt32(5), AwayClubId = r.GetInt32(6), HomeGoals = NullableInt(r, 7), AwayGoals = NullableInt(r, 8),
                HomePenalties = NullableInt(r, 9), AwayPenalties = NullableInt(r, 10)
            }));
        }

        // ------------------------------------------------------------------ cuerpo técnico, ojeadores y buzón

        private void SavePeople(CareerData data, DbTransaction tx)
        {
            foreach (StaffMember s in data.Staff)
            {
                SqlHelper.Execute(Connection,
                    @"INSERT INTO staff (id, club_id, name, role, ability, weekly_wage, former_player_id)
                      VALUES (@id, @club, @name, @role, @ability, @wage, @former)", tx,
                    ("@id", s.Id), ("@club", s.ClubId), ("@name", s.Name ?? string.Empty), ("@role", (int)s.Role),
                    ("@ability", Math.Max(1, Math.Min(20, s.Ability))), ("@wage", s.WeeklyWage), ("@former", s.FormerPlayerId));
            }

            foreach ((int clubId, int playerId, float knowledge) in data.Scouting)
            {
                SqlHelper.Execute(Connection, "INSERT INTO scouting (club_id, player_id, knowledge) VALUES (@club, @player, @k)", tx,
                    ("@club", clubId), ("@player", playerId), ("@k", (double)Math.Max(0f, Math.Min(1f, knowledge))));
            }

            foreach (NewsItem n in data.News)
            {
                SqlHelper.Execute(Connection, "INSERT INTO news (club_id, date, key, args, read) VALUES (@club, @date, @key, @args, @read)", tx,
                    ("@club", n.ClubId), ("@date", DateFormat.ToText(n.Date)), ("@key", n.Key),
                    ("@args", string.Join(ArgsSeparator.ToString(), n.Args ?? Array.Empty<string>())), ("@read", n.Read ? 1 : 0));
            }
        }

        private void LoadPeople(CareerData data)
        {
            Read("SELECT id, club_id, name, role, ability, weekly_wage, former_player_id FROM staff ORDER BY id", r => data.Staff.Add(new StaffMember
            {
                Id = r.GetInt32(0), ClubId = NullableInt(r, 1), Name = r.GetString(2), Role = (StaffRole)r.GetInt32(3), Ability = r.GetInt32(4),
                WeeklyWage = r.GetInt64(5), FormerPlayerId = NullableInt(r, 6)
            }));
            Read("SELECT club_id, player_id, knowledge FROM scouting", r => data.Scouting.Add((r.GetInt32(0), r.GetInt32(1), (float)r.GetDouble(2))));
            Read("SELECT club_id, date, key, args, read FROM news ORDER BY id", r =>
            {
                string args = r.GetString(3);
                data.News.Add(new NewsItem
                {
                    ClubId = r.GetInt32(0), Date = DateFormat.Parse(r.GetString(1)), Key = r.GetString(2),
                    Args = args.Length == 0 ? Array.Empty<string>() : args.Split(ArgsSeparator), Read = r.GetInt32(4) != 0
                });
            });
        }

        // ------------------------------------------------------------------ historia

        private void SaveHistory(CareerData data, DbTransaction tx)
        {
            foreach (RecordEntry rec in data.Records)
            {
                InsertRecord(tx, rec.Key, rec.Value, rec.HolderId, rec.Season);
            }

            // Los goles de carrera de cada jugador van en la misma tabla, con su propio prefijo (no hay una tabla aparte).
            foreach (KeyValuePair<int, int> g in data.CareerGoals)
            {
                InsertRecord(tx, CareerGoalsPrefix + g.Key, g.Value, g.Key, data.Season);
            }

            foreach (KeyValuePair<int, CardStats> st in data.SeasonStats)
            {
                SqlHelper.Execute(Connection,
                    "INSERT INTO player_season_stats (player_id, matches, goals, assists, rating_sum, rated) VALUES (@id, @m, @g, @a, @r, @n)", tx,
                    ("@id", st.Key), ("@m", st.Value.Matches), ("@g", st.Value.Goals), ("@a", st.Value.Assists), ("@r", (double)st.Value.RatingSum),
                    ("@n", st.Value.Rated));
            }

            foreach (int id in data.HallOfFame)
            {
                SqlHelper.Execute(Connection, "INSERT INTO hall_of_fame (player_id, inducted_season) VALUES (@id, @season)", tx,
                    ("@id", id), ("@season", data.Season));
            }
        }

        private void InsertRecord(DbTransaction tx, string key, float value, int holder, int season) =>
            SqlHelper.Execute(Connection, "INSERT INTO records (key, value, holder_id, season) VALUES (@key, @value, @holder, @season)", tx,
                ("@key", key), ("@value", (double)value), ("@holder", holder), ("@season", season));

        private void LoadHistory(CareerData data)
        {
            Read("SELECT key, value, holder_id, season FROM records", r =>
            {
                string key = r.GetString(0);
                if (key.StartsWith(CareerGoalsPrefix, StringComparison.Ordinal))
                {
                    data.CareerGoals[r.GetInt32(2)] = (int)Math.Round(r.GetDouble(1));
                    return;
                }

                data.Records.Add(new RecordEntry { Key = key, Value = (float)r.GetDouble(1), HolderId = r.GetInt32(2), Season = r.GetInt32(3) });
            });
            Read("SELECT player_id FROM hall_of_fame ORDER BY inducted_season, player_id", r => data.HallOfFame.Add(r.GetInt32(0)));
            Read("SELECT player_id, matches, goals, assists, rating_sum, rated FROM player_season_stats", r => data.SeasonStats[r.GetInt32(0)] = new CardStats
            {
                Matches = r.GetInt32(1), Goals = r.GetInt32(2), Assists = r.GetInt32(3), RatingSum = (float)r.GetDouble(4), Rated = r.GetInt32(5)
            });
        }

        // ------------------------------------------------------------------ auxiliares

        private void Read(string sql, Action<DbDataReader> row)
        {
            using (DbCommand cmd = SqlHelper.Command(Connection, sql, null))
            using (DbDataReader r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    row(r);
                }
            }
        }

        private static int? NullableInt(DbDataReader r, int column) => r.IsDBNull(column) ? (int?)null : r.GetInt32(column);
    }
}
