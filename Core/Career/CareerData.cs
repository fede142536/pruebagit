using System;
using System.Collections.Generic;
using Football.Core.Domain;
using Football.Core.MatchCore;

namespace Football.Core.Career
{
    /// <summary>Torneo de una temporada (Apertura o Clausura).</summary>
    public enum TournamentKind
    {
        /// <summary>Primera mitad del año.</summary>
        Apertura,
        /// <summary>Segunda mitad del año (mismos cruces con la localía invertida).</summary>
        Clausura
    }

    /// <summary>Competición guardada (tabla <c>competitions</c>).</summary>
    public sealed class CompetitionInfo
    {
        /// <summary>Identificador (temporada × 10 + torneo).</summary>
        public int Id;
        /// <summary>Nombre visible.</summary>
        public string Name;
        /// <summary>Temporada (año).</summary>
        public int Season;
        /// <summary>Torneo.</summary>
        public TournamentKind Kind;

        /// <summary>Identificador de un torneo de una temporada.</summary>
        public static int IdOf(int season, TournamentKind kind) => season * 10 + (int)kind;
    }

    /// <summary>Clásico entre dos clubes (tabla <c>rivalries</c>).</summary>
    public sealed class ClubRivalry
    {
        /// <summary>Un club.</summary>
        public int ClubA;
        /// <summary>El otro club.</summary>
        public int ClubB;
        /// <summary>Intensidad.</summary>
        public RivalryLevel Level;
        /// <summary>Nombre del clásico.</summary>
        public string Name;
    }

    /// <summary>Táctica que el usuario eligió para su club (la usan el simulador rápido y el partido 3D).</summary>
    public sealed class UserTactics
    {
        /// <summary>Formación con balón (por ejemplo «4-3-3»).</summary>
        public string Formation = "4-4-2";
        /// <summary>Formación sin balón (vacío = la misma); debe tener los mismos puestos.</summary>
        public string DefensiveFormation = string.Empty;
        /// <summary>Estilo de juego (nombre de <c>MatchConfig.Styles</c>).</summary>
        public string Style = "Balanced";
        /// <summary>Rol de los laterales.</summary>
        public PlayerDuty FullbackDuty = PlayerDuty.Auto;
        /// <summary>Rol de los mediocampistas.</summary>
        public PlayerDuty MidfieldDuty = PlayerDuty.Auto;
        /// <summary>Rol de los delanteros.</summary>
        public PlayerDuty StrikerDuty = PlayerDuty.Auto;
    }

    /// <summary>Oferta de un club rival por un jugador del usuario, pendiente de respuesta.</summary>
    public sealed class PendingOffer
    {
        /// <summary>Oferta.</summary>
        public TransferOffer Offer;
        /// <summary>Fecha en que llegó.</summary>
        public DateTime Received;
    }

    /// <summary>
    /// Todo el estado de una partida del modo carrera, sin lógica: lo arma <see cref="CareerSession"/>, lo guarda y lo lee
    /// <c>SqlCareerRepository</c>. Un único modelo de jugador y club para gestión, simulación rápida y partido 3D.
    /// </summary>
    public sealed class CareerData
    {
        /// <summary>Semilla de la partida (todo el azar sale de ella y de la fecha).</summary>
        public int Seed;
        /// <summary>Temporada en curso (año).</summary>
        public int Season;
        /// <summary>Fecha actual.</summary>
        public DateTime Today;
        /// <summary>Club que dirige el usuario.</summary>
        public int UserClubId;
        /// <summary>Modo Torneo: un solo campeonato, sin mercado ni finanzas que gestionar.</summary>
        public bool TournamentOnly;
        /// <summary>Táctica del club del usuario.</summary>
        public UserTactics Tactics = new UserTactics();
        /// <summary>Clubes.</summary>
        public readonly List<Club> Clubs = new List<Club>();
        /// <summary>Jugadores por identificador (los retirados quedan sin club).</summary>
        public readonly Dictionary<int, Player> Players = new Dictionary<int, Player>();
        /// <summary>Estado de cada jugador (forma, moral, físico, lesiones, sanciones).</summary>
        public readonly Dictionary<int, PlayerCondition> Conditions = new Dictionary<int, PlayerCondition>();
        /// <summary>Dinero de cada club.</summary>
        public readonly Dictionary<int, ClubFinances> Finances = new Dictionary<int, ClubFinances>();
        /// <summary>Instalaciones de cada club.</summary>
        public readonly Dictionary<int, ClubFacilities> Facilities = new Dictionary<int, ClubFacilities>();
        /// <summary>Cuerpo técnico de todos los clubes.</summary>
        public readonly List<StaffMember> Staff = new List<StaffMember>();
        /// <summary>Torneos (de esta temporada y de las anteriores, para los promedios).</summary>
        public readonly List<CompetitionInfo> Competitions = new List<CompetitionInfo>();
        /// <summary>Partidos de todos los torneos.</summary>
        public readonly List<Fixture> Fixtures = new List<Fixture>();
        /// <summary>Clásicos.</summary>
        public readonly List<ClubRivalry> Rivalries = new List<ClubRivalry>();
        /// <summary>Buzón del usuario, en orden de llegada.</summary>
        public readonly List<NewsItem> News = new List<NewsItem>();
        /// <summary>Lo que cada club sabe de cada jugador ajeno.</summary>
        public readonly List<(int ClubId, int PlayerId, float Knowledge)> Scouting = new List<(int, int, float)>();
        /// <summary>Récords vigentes.</summary>
        public readonly List<RecordEntry> Records = new List<RecordEntry>();
        /// <summary>Goles de carrera de cada jugador.</summary>
        public readonly Dictionary<int, int> CareerGoals = new Dictionary<int, int>();
        /// <summary>Estadísticas de la temporada en curso de cada jugador (partidos, goles, asistencias, notas).</summary>
        public readonly Dictionary<int, CardStats> SeasonStats = new Dictionary<int, CardStats>();
        /// <summary>Salón de la fama.</summary>
        public readonly List<int> HallOfFame = new List<int>();

        /// <summary>Club por identificador (null si no existe).</summary>
        public Club Club(int id) => Clubs.Find(c => c.Id == id);
    }
}
