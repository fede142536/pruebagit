using System;
using System.Collections.Generic;
using Football.Core.Data;
using Football.Core.Domain;
using Football.Core.Management;
using Football.Core.MatchCore;

namespace Football.Core.Career
{
    /// <summary>Competición de una temporada.</summary>
    public enum TournamentKind
    {
        /// <summary>Primera mitad del año.</summary>
        Apertura,
        /// <summary>Segunda mitad del año (mismos cruces con la localía invertida).</summary>
        Clausura,
        /// <summary>Copa Argentina: eliminación directa con todos los clubes.</summary>
        Cup,
        /// <summary>Nacional B (segunda división): todos contra todos, ida y vuelta.</summary>
        NacionalB,
        /// <summary>Promoción entre el tercero del Nacional B y un club de Primera: ida y vuelta.</summary>
        Promotion
    }

    /// <summary>Precio cobrado y público de un partido de local del usuario (historial de entradas).</summary>
    public sealed class TicketRecord
    {
        /// <summary>Partido.</summary>
        public int FixtureId;
        /// <summary>Fecha.</summary>
        public DateTime Date;
        /// <summary>Precio cobrado.</summary>
        public long Price;
        /// <summary>Rango ideal sugerido.</summary>
        public long MinIdeal, MaxIdeal;
        /// <summary>Ocupación (0–1).</summary>
        public float Attendance;
        /// <summary>Recaudación de taquilla.</summary>
        public long Revenue;
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
        /// <summary>
        /// Once inicial y banco elegidos por el DT («id,id,…;id,id,…»: titulares por puesto y suplentes, ids de jugador). Vacío = alineación automática.
        /// Ver <see cref="Football.Core.Management.MatchPreparation.FormatLineup"/>.
        /// </summary>
        public string Lineup = string.Empty;
    }

    /// <summary>Oferta de un club rival por un jugador del usuario, pendiente de respuesta.</summary>
    public sealed class PendingOffer
    {
        /// <summary>Oferta.</summary>
        public TransferOffer Offer;
        /// <summary>Fecha en que llegó.</summary>
        public DateTime Received;
        /// <summary>Venta, préstamo o préstamo con compra obligatoria.</summary>
        public DealKind Kind;
        /// <summary>En un préstamo, fracción del sueldo que paga el club que lo recibe.</summary>
        public float WageShare = 1f;
        /// <summary>En un préstamo con compra obligatoria, el monto de la compra.</summary>
        public long PurchaseFee;
        /// <summary>En un préstamo con compra obligatoria, los minutos que tiene que jugar para que se active.</summary>
        public float MinMinutes;
        /// <summary>El jugador estaba en la lista de transferibles o cedibles.</summary>
        public bool Listed;
        /// <summary>Qué piensa el jugador de irse (solo en los que no estaban en la lista).</summary>
        public PlayerStance Stance = PlayerStance.Open;
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
        /// <summary>
        /// Cantidad de clubes de Primera (los demás juegan el Nacional B, con ascensos y descensos). 0 = todos los clubes juegan una sola división
        /// (sin Nacional B ni descensos de categoría).
        /// </summary>
        public int PrimeraSize;
        /// <summary>Táctica del club del usuario.</summary>
        public UserTactics Tactics = new UserTactics();
        /// <summary>Director técnico del usuario.</summary>
        public ManagerProfile Manager = new ManagerProfile();
        /// <summary>Nivel de precio de las entradas del usuario.</summary>
        public TicketLevel TicketLevel = TicketLevel.Normal;
        /// <summary>Precio fijado a mano para el próximo partido de local (0 = el del nivel).</summary>
        public long NextPriceOverride;
        /// <summary>Caja del usuario al empezar la temporada (para la directiva y el medidor).</summary>
        public long SeasonStartCash;
        /// <summary>Datos institucionales de cada club (fundación, estadio, valor, jerarquía, historia).</summary>
        public readonly Dictionary<int, ClubInfo> ClubInfos = new Dictionary<int, ClubInfo>();
        /// <summary>Estadio de cada club.</summary>
        public readonly Dictionary<int, StadiumState> Stadiums = new Dictionary<int, StadiumState>();
        /// <summary>Relación con los socios del usuario.</summary>
        public SupporterState Supporters = new SupporterState();
        /// <summary>Historial de precios y público de los partidos de local del usuario.</summary>
        public readonly List<TicketRecord> TicketHistory = new List<TicketRecord>();
        /// <summary>Fichajes del DT y su rendimiento.</summary>
        public readonly List<SigningRecord> Signings = new List<SigningRecord>();
        /// <summary>Historial del medidor de gestión.</summary>
        public readonly List<ManagementHistoryEntry> ManagementHistory = new List<ManagementHistoryEntry>();
        /// <summary>Clubes.</summary>
        public readonly List<Club> Clubs = new List<Club>();
        /// <summary>Jugadores por identificador (los retirados quedan sin club).</summary>
        public readonly Dictionary<int, Player> Players = new Dictionary<int, Player>();
        /// <summary>Estado de cada jugador (forma, moral, físico, lesiones, sanciones).</summary>
        public readonly Dictionary<int, PlayerCondition> Conditions = new Dictionary<int, PlayerCondition>();
        /// <summary>Negociaciones de renovación del usuario (por jugador).</summary>
        public readonly Dictionary<int, RenewalTalk> Talks = new Dictionary<int, RenewalTalk>();
        /// <summary>Jugadores del usuario en la lista de transferibles o cedibles.</summary>
        public readonly Dictionary<int, TransferListing> Listings = new Dictionary<int, TransferListing>();
        /// <summary>Préstamos vigentes (jugadores del usuario cedidos y jugadores ajenos que recibió).</summary>
        public readonly List<LoanRecord> Loans = new List<LoanRecord>();
        /// <summary>Cláusulas de recompra vigentes del usuario (jugadores que vendió con la posibilidad de volver a comprarlos).</summary>
        public readonly List<BuybackClause> Buybacks = new List<BuybackClause>();
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

        /// <summary>Estadísticas de la temporada en los partidos de la reserva (simulados), por jugador.</summary>
        public readonly Dictionary<int, CardStats> ReserveStats = new Dictionary<int, CardStats>();
        /// <summary>Salón de la fama.</summary>
        public readonly List<int> HallOfFame = new List<int>();

        /// <summary>Club por identificador (null si no existe).</summary>
        public Club Club(int id) => Clubs.Find(c => c.Id == id);
    }
}
