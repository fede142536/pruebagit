using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.IO;
using Football.Core.Domain;

namespace Football.Core.Persistence
{
    /// <summary>Formato de fecha común a todas las tablas.</summary>
    internal static class DateFormat
    {
        public const string Pattern = "yyyy-MM-dd";

        public static string ToText(DateTime date) => date.ToString(Pattern, CultureInfo.InvariantCulture);

        public static DateTime Parse(string text) =>
            DateTime.ParseExact(text, Pattern, CultureInfo.InvariantCulture);
    }

    /// <summary>Base de los repositorios SQL: conexión y validación de identificadores.</summary>
    public abstract class SqlRepositoryBase
    {
        /// <summary>Conexión abierta con el esquema en la última versión.</summary>
        protected DbConnection Connection { get; }

        /// <summary>Crea el repositorio.</summary>
        /// <param name="connection">Conexión abierta, migrada y configurada con <see cref="SqliteConnectionSetup"/>.</param>
        protected SqlRepositoryBase(DbConnection connection)
        {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        /// <summary>Verifica que el identificador sea positivo.</summary>
        /// <param name="id">Identificador a validar.</param>
        protected static void RequireId(int id)
        {
            if (id <= 0)
            {
                throw new ArgumentException("El identificador debe ser positivo.", nameof(id));
            }
        }
    }

    /// <summary>Repositorio de estadios sobre ADO.NET.</summary>
    public sealed class SqlStadiumRepository : SqlRepositoryBase, IStadiumRepository
    {
        /// <summary>Crea el repositorio.</summary>
        /// <param name="connection">Conexión abierta y migrada.</param>
        public SqlStadiumRepository(DbConnection connection) : base(connection) { }

        /// <inheritdoc />
        public void Save(Stadium stadium)
        {
            RequireId(stadium.Id);
            SqlHelper.Execute(Connection,
                @"INSERT INTO stadiums (id, name, capacity) VALUES (@id, @name, @capacity)
                  ON CONFLICT(id) DO UPDATE SET name = excluded.name, capacity = excluded.capacity",
                null, ("@id", stadium.Id), ("@name", stadium.Name), ("@capacity", stadium.Capacity));
        }

        /// <inheritdoc />
        public Stadium Get(int id)
        {
            using (DbCommand cmd = SqlHelper.Command(Connection,
                "SELECT id, name, capacity FROM stadiums WHERE id = @id", null, ("@id", id)))
            using (DbDataReader r = cmd.ExecuteReader())
            {
                return r.Read()
                    ? new Stadium { Id = r.GetInt32(0), Name = r.GetString(1), Capacity = r.GetInt32(2) }
                    : null;
            }
        }
    }

    /// <summary>Repositorio de clubes sobre ADO.NET.</summary>
    public sealed class SqlClubRepository : SqlRepositoryBase, IClubRepository
    {
        private const string SelectColumns =
            "SELECT id, name, short_name, country, primary_color, secondary_color, stadium_id, transfer_budget, wage_budget FROM clubs";

        /// <summary>Crea el repositorio.</summary>
        /// <param name="connection">Conexión abierta y migrada.</param>
        public SqlClubRepository(DbConnection connection) : base(connection) { }

        /// <inheritdoc />
        public void Save(Club club)
        {
            RequireId(club.Id);
            SqlHelper.Execute(Connection,
                @"INSERT INTO clubs (id, name, short_name, country, primary_color, secondary_color, stadium_id, transfer_budget, wage_budget)
                  VALUES (@id, @name, @short, @country, @primary, @secondary, @stadium, @transfer, @wage)
                  ON CONFLICT(id) DO UPDATE SET
                    name = excluded.name, short_name = excluded.short_name, country = excluded.country,
                    primary_color = excluded.primary_color, secondary_color = excluded.secondary_color,
                    stadium_id = excluded.stadium_id, transfer_budget = excluded.transfer_budget,
                    wage_budget = excluded.wage_budget",
                null,
                ("@id", club.Id), ("@name", club.Name), ("@short", club.ShortName), ("@country", club.Country),
                ("@primary", club.PrimaryColor), ("@secondary", club.SecondaryColor), ("@stadium", club.StadiumId),
                ("@transfer", club.TransferBudget), ("@wage", club.WageBudget));
        }

        /// <inheritdoc />
        public Club Get(int id)
        {
            using (DbCommand cmd = SqlHelper.Command(Connection, SelectColumns + " WHERE id = @id", null, ("@id", id)))
            using (DbDataReader r = cmd.ExecuteReader())
            {
                return r.Read() ? Map(r) : null;
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<Club> GetAll()
        {
            var result = new List<Club>();
            using (DbCommand cmd = SqlHelper.Command(Connection, SelectColumns + " ORDER BY id", null))
            using (DbDataReader r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    result.Add(Map(r));
                }
            }

            return result;
        }

        /// <inheritdoc />
        public void Delete(int id)
        {
            SqlHelper.Execute(Connection, "DELETE FROM clubs WHERE id = @id", null, ("@id", id));
        }

        private static Club Map(DbDataReader r) => new Club
        {
            Id = r.GetInt32(0),
            Name = r.GetString(1),
            ShortName = r.GetString(2),
            Country = r.GetString(3),
            PrimaryColor = r.GetString(4),
            SecondaryColor = r.GetString(5),
            StadiumId = r.IsDBNull(6) ? (int?)null : r.GetInt32(6),
            TransferBudget = r.GetInt64(7),
            WageBudget = r.GetInt64(8)
        };
    }

    /// <summary>Repositorio de jugadores sobre ADO.NET.</summary>
    public sealed class SqlPlayerRepository : SqlRepositoryBase, IPlayerRepository
    {
        private static Position ReadPosition(int value, int playerId)
        {
            if (!Enum.IsDefined(typeof(Position), value))
            {
                throw new InvalidDataException($"El jugador {playerId} tiene una posición desconocida ({value}).");
            }

            return (Position)value;
        }

        /// <summary>Crea el repositorio.</summary>
        /// <param name="connection">Conexión abierta y migrada.</param>
        public SqlPlayerRepository(DbConnection connection) : base(connection) { }

        /// <inheritdoc />
        public void Save(Player player)
        {
            RequireId(player.Id);
            using (DbTransaction tx = Connection.BeginTransaction())
            {
                SqlHelper.Execute(Connection,
                    @"INSERT INTO players (id, club_id, first_name, last_name, birth_date, position, potential)
                      VALUES (@id, @club, @first, @last, @birth, @position, @potential)
                      ON CONFLICT(id) DO UPDATE SET
                        club_id = excluded.club_id, first_name = excluded.first_name, last_name = excluded.last_name,
                        birth_date = excluded.birth_date, position = excluded.position, potential = excluded.potential",
                    tx,
                    ("@id", player.Id), ("@club", player.ClubId), ("@first", player.FirstName),
                    ("@last", player.LastName), ("@birth", DateFormat.ToText(player.BirthDate)),
                    ("@position", (int)player.Position), ("@potential", player.Potential));

                SqlHelper.Execute(Connection, "DELETE FROM player_attributes WHERE player_id = @id", tx, ("@id", player.Id));
                foreach (AttributeId attribute in AttributeSet.All)
                {
                    SqlHelper.Execute(Connection,
                        "INSERT INTO player_attributes (player_id, attribute_id, value) VALUES (@id, @attribute, @value)",
                        tx, ("@id", player.Id), ("@attribute", (int)attribute), ("@value", player.Attributes[attribute]));
                }

                if (player.Contract == null)
                {
                    SqlHelper.Execute(Connection, "DELETE FROM contracts WHERE player_id = @id", tx, ("@id", player.Id));
                }
                else
                {
                    SqlHelper.Execute(Connection,
                        @"INSERT INTO contracts (player_id, weekly_wage, expires_on) VALUES (@id, @wage, @expires)
                          ON CONFLICT(player_id) DO UPDATE SET weekly_wage = excluded.weekly_wage, expires_on = excluded.expires_on",
                        tx, ("@id", player.Id), ("@wage", player.Contract.WeeklyWage),
                        ("@expires", DateFormat.ToText(player.Contract.ExpiresOn)));
                }

                tx.Commit();
            }
        }

        /// <inheritdoc />
        public Player Get(int id)
        {
            Player player;
            using (DbCommand cmd = SqlHelper.Command(Connection,
                "SELECT id, club_id, first_name, last_name, birth_date, position, potential FROM players WHERE id = @id",
                null, ("@id", id)))
            using (DbDataReader r = cmd.ExecuteReader())
            {
                if (!r.Read())
                {
                    return null;
                }

                player = new Player
                {
                    Id = r.GetInt32(0),
                    ClubId = r.IsDBNull(1) ? (int?)null : r.GetInt32(1),
                    FirstName = r.GetString(2),
                    LastName = r.GetString(3),
                    BirthDate = DateFormat.Parse(r.GetString(4)),
                    Position = ReadPosition(r.GetInt32(5), id),
                    Potential = r.GetInt32(6)
                };
            }

            using (DbCommand cmd = SqlHelper.Command(Connection,
                "SELECT attribute_id, value FROM player_attributes WHERE player_id = @id", null, ("@id", id)))
            using (DbDataReader r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    int attribute = r.GetInt32(0);
                    if (!AttributeSet.IsDefined(attribute))
                    {
                        throw new InvalidDataException($"El jugador {id} tiene un atributo desconocido ({attribute}).");
                    }

                    player.Attributes[(AttributeId)attribute] = r.GetInt32(1);
                }
            }

            using (DbCommand cmd = SqlHelper.Command(Connection,
                "SELECT weekly_wage, expires_on FROM contracts WHERE player_id = @id", null, ("@id", id)))
            using (DbDataReader r = cmd.ExecuteReader())
            {
                if (r.Read())
                {
                    player.Contract = new Contract { WeeklyWage = r.GetInt64(0), ExpiresOn = DateFormat.Parse(r.GetString(1)) };
                }
            }

            return player;
        }

        /// <inheritdoc />
        public IReadOnlyList<Player> GetByClub(int clubId)
        {
            var ids = new List<int>();
            using (DbCommand cmd = SqlHelper.Command(Connection,
                "SELECT id FROM players WHERE club_id = @club ORDER BY id", null, ("@club", clubId)))
            using (DbDataReader r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    ids.Add(r.GetInt32(0));
                }
            }

            // Una consulta por jugador: suficiente para plantillas (~30). Si resulta lento, se optimiza con medición.
            var players = new List<Player>(ids.Count);
            foreach (int id in ids)
            {
                players.Add(Get(id));
            }

            return players;
        }

        /// <inheritdoc />
        public void Delete(int id)
        {
            SqlHelper.Execute(Connection, "DELETE FROM players WHERE id = @id", null, ("@id", id));
        }
    }
}
