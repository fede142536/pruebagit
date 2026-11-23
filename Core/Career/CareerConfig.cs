using Football.Core.Management;

namespace Football.Core.Career
{
    /// <summary>
    /// Parámetros del modo carrera que no son de economía ni de plantel (esos están en <c>finance.json</c> y
    /// <c>management.json</c>): calendario, ventanas de pases, descensos, clasificación a copas, ojeadores y valores iniciales de
    /// los clubes. Se guarda en <c>Data/balance/career.json</c> (HeadlessSim <c>--dump-career-config</c>).
    /// </summary>
    public sealed class CareerConfig
    {
        /// <summary>Mes de la primera jornada de cada temporada.</summary>
        public int SeasonStartMonth = 1;
        /// <summary>Día de la primera jornada de cada temporada.</summary>
        public int SeasonStartDay = 25;
        /// <summary>Días entre jornadas.</summary>
        public int DaysBetweenRounds = 7;
        /// <summary>Días de descanso entre el Apertura y el Clausura.</summary>
        public int BreakBetweenTournaments = 21;
        /// <summary>Meses con el libro de pases abierto (1 = enero).</summary>
        public int[] TransferWindowMonths = { 1, 2, 7 };
        /// <summary>Probabilidad semanal de que un rival ofrezca por un jugador del usuario con el libro abierto.</summary>
        public float RivalOfferChancePerWeek = 0.3f;
        /// <summary>Fracción del valor de mercado que ofrece un rival (antes de negociar).</summary>
        public float RivalOfferShare = 0.85f;
        /// <summary>Clubes que descienden por promedios al terminar la temporada.</summary>
        public int RelegatedClubs = 2;
        /// <summary>Ingreso anual estimado por punto de reputación sobre el piso (para ajustar la masa salarial inicial al tamaño del club).</summary>
        public long RevenuePerReputation = 330_000_000;
        /// <summary>Reputación por debajo de la cual el ingreso estimado es el mínimo.</summary>
        public int RevenueFloorReputation = 30;
        /// <summary>Qué parte del ingreso estimado se va en sueldos al empezar.</summary>
        public float WageToRevenue = 0.8f;
        /// <summary>Límites del ajuste de sueldos iniciales (1 = tal cual los pide el mercado).</summary>
        public float MinWageScale = 0.4f, MaxWageScale = 1.5f;
        /// <summary>Tope de plantel de los clubes que maneja la computadora: al cerrar la temporada sueltan al excedente (los de menor nivel).</summary>
        public int MaxAiSquadSize = 32;
        /// <summary>Arqueros que un club de la computadora nunca baja al soltar el excedente.</summary>
        public int MinAiGoalkeepers = 3;
        /// <summary>Clubes que se clasifican a la copa internacional por la tabla anual.</summary>
        public int CupQualifiers = 4;
        /// <summary>Temporadas que cuenta la tabla de promedios.</summary>
        public int AveragesSeasons = 3;
        /// <summary>Días que tarda un ojeador en entregar su informe.</summary>
        public int ScoutDays = 14;
        /// <summary>Formación con la que juega la IA de cada club.</summary>
        public string Formation = "4-4-2";
        /// <summary>Media del plantel que corresponde a una reputación de 50.</summary>
        public float ReputationPivotRating = 66f;
        /// <summary>Puntos de reputación por punto de media del plantel.</summary>
        public float ReputationPerRating = 4f;
        /// <summary>Dinero en caja inicial por punto de reputación.</summary>
        public long CashPerReputation = 40_000_000;
        /// <summary>Fracción de la caja que se reserva para fichajes al empezar.</summary>
        public float TransferBudgetShare = 0.3f;
        /// <summary>Margen del presupuesto de sueldos sobre la masa salarial inicial.</summary>
        public float WageBudgetMargin = 1.1f;
        /// <summary>Socios por punto de reputación.</summary>
        public int MembersPerReputation = 900;
        /// <summary>Años de contrato iniciales (mínimo y máximo).</summary>
        public int[] ContractYears = { 1, 4 };
        /// <summary>Cláusula de rescisión inicial, en veces el valor de mercado.</summary>
        public float ReleaseClauseFactor = 2.5f;
        /// <summary>Capacidad (1–20) del cuerpo técnico inicial de un club de reputación 50.</summary>
        public int StaffAbilityPivot = 10;
        /// <summary>Sueldo semanal de un integrante del cuerpo técnico por punto de capacidad.</summary>
        public long StaffWagePerAbility = 150_000;
        /// <summary>Plazas de estacionamiento iniciales por jerarquía (grande, media, desarrollo).</summary>
        public int[] InitialParking = { 3000, 1500, 600 };
        /// <summary>Estado inicial del campo por jerarquía (2 bueno, 1 regular).</summary>
        public int[] InitialPitch = { 2, 1, 1 };
        /// <summary>Días desde la primera fecha hasta la primera ronda de la Copa Argentina.</summary>
        public int CupFirstOffsetDays = 10;
        /// <summary>Días entre rondas de la copa.</summary>
        public int CupRoundSpacingDays = 35;
        /// <summary>Fechas finales en las que un partido puede ser «decisivo» o «sin nada en juego».</summary>
        public int DecisiveRounds = 3;
        /// <summary>Partidos que cuenta la racha.</summary>
        public int FormMatches = 5;
        /// <summary>Cantidad de clubes desde la que cada torneo se juega en dos zonas con cruces interzonales (por debajo, todos contra todos).</summary>
        public int ZoneSplitFrom = 20;
        /// <summary>Días antes de un partido de local en que los socios revisan el precio.</summary>
        public int SupporterReviewDaysBefore = 3;
        /// <summary>Probabilidad de lluvia de cada mes (enero a diciembre; otoño e invierno más lluviosos).</summary>
        public float[] RainChanceByMonth = { 0.25f, 0.25f, 0.3f, 0.3f, 0.25f, 0.2f, 0.2f, 0.2f, 0.25f, 0.3f, 0.3f, 0.25f };
        /// <summary>Probabilidad de cielo nublado cuando no llueve.</summary>
        public float CloudChance = 0.35f;
        /// <summary>Temperatura media de cada mes (°C, clima templado del Río de la Plata).</summary>
        public float[] MeanTemperatureByMonth = { 25f, 24f, 22f, 18f, 14f, 11f, 10f, 12f, 14f, 17f, 21f, 24f };
        /// <summary>Desvío de la temperatura del día respecto de la media (°C).</summary>
        public float TemperatureSpread = 4f;
        /// <summary>Grados menos cuando llueve.</summary>
        public float RainCooling = 3f;
        /// <summary>Asistencia y precios de entradas.</summary>
        public AttendanceConfig Attendance = new AttendanceConfig();
        /// <summary>Directiva y popularidad del DT.</summary>
        public BoardConfig Board = new BoardConfig();
        /// <summary>Socios.</summary>
        public SupporterConfig Supporters = new SupporterConfig();
        /// <summary>Remodelación del estadio.</summary>
        public StadiumConfig Stadium = new StadiumConfig();
        /// <summary>Medidor de gestión.</summary>
        public RatingConfig Rating = new RatingConfig();
    }
}
