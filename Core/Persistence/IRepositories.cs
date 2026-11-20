using System.Collections.Generic;
using Football.Core.Domain;

namespace Football.Core.Persistence
{
    /// <summary>Acceso a estadios. El dominio no conoce SQL: depende solo de esta interfaz.</summary>
    public interface IStadiumRepository
    {
        /// <summary>Inserta o actualiza un estadio. El <c>Id</c> debe ser positivo.</summary>
        /// <param name="stadium">Estadio a guardar.</param>
        void Save(Stadium stadium);

        /// <summary>Obtiene un estadio, o <c>null</c> si no existe.</summary>
        /// <param name="id">Identificador.</param>
        Stadium Get(int id);
    }

    /// <summary>Acceso a clubes.</summary>
    public interface IClubRepository
    {
        /// <summary>Inserta o actualiza un club. El <c>Id</c> debe ser positivo.</summary>
        /// <param name="club">Club a guardar.</param>
        void Save(Club club);

        /// <summary>Obtiene un club, o <c>null</c> si no existe.</summary>
        /// <param name="id">Identificador.</param>
        Club Get(int id);

        /// <summary>Todos los clubes, ordenados por identificador.</summary>
        IReadOnlyList<Club> GetAll();

        /// <summary>Elimina un club; sus jugadores quedan sin club.</summary>
        /// <param name="id">Identificador.</param>
        void Delete(int id);
    }

    /// <summary>Acceso a jugadores, incluidos sus atributos y contrato.</summary>
    public interface IPlayerRepository
    {
        /// <summary>Inserta o actualiza un jugador con sus atributos y contrato. El <c>Id</c> debe ser positivo.</summary>
        /// <param name="player">Jugador a guardar.</param>
        void Save(Player player);

        /// <summary>Obtiene un jugador, o <c>null</c> si no existe.</summary>
        /// <param name="id">Identificador.</param>
        Player Get(int id);

        /// <summary>Jugadores de un club, ordenados por identificador.</summary>
        /// <param name="clubId">Club.</param>
        IReadOnlyList<Player> GetByClub(int clubId);

        /// <summary>Elimina un jugador junto con sus atributos y contrato.</summary>
        /// <param name="id">Identificador.</param>
        void Delete(int id);
    }
}
