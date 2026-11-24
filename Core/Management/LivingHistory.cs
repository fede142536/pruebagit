using System;
using System.Collections.Generic;
using System.Linq;
using Football.Core.Common;
using Football.Core.Domain;

namespace Football.Core.Management
{
    /// <summary>
    /// Historia viva: récords (goles en una temporada, mayor goleada, máximos goleadores históricos), salón de la fama y
    /// retiros. Los retirados con alma de técnico (líderes o profesionales) pasan al cuerpo técnico y pueden dirigir rivales.
    /// </summary>
    public sealed class LivingHistory
    {
        /// <summary>Clave del récord de goles en una temporada.</summary>
        public const string SeasonGoals = "record.season_goals";
        /// <summary>Clave del récord de goles en la historia del club.</summary>
        public const string CareerGoals = "record.career_goals";
        /// <summary>Clave del récord de mayor goleada.</summary>
        public const string BiggestWin = "record.biggest_win";

        private readonly ManagementConfig _config;
        private readonly EventBus _bus;
        private readonly Dictionary<string, RecordEntry> _records = new Dictionary<string, RecordEntry>();
        private readonly Dictionary<(int season, int player), int> _seasonGoals = new Dictionary<(int, int), int>();
        private readonly Dictionary<int, int> _careerGoals = new Dictionary<int, int>();
        private readonly List<int> _hallOfFame = new List<int>();
        private int _nextStaffId;

        /// <summary>Crea la historia.</summary>
        /// <param name="config">Parámetros.</param>
        /// <param name="bus">Bus.</param>
        /// <param name="firstStaffId">Primer identificador libre para fichas de cuerpo técnico.</param>
        public LivingHistory(ManagementConfig config, EventBus bus, int firstStaffId)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _nextStaffId = firstStaffId;
        }

        /// <summary>Récord actual de una clave (null si todavía no hay).</summary>
        public RecordEntry Record(string key) => _records.TryGetValue(key, out RecordEntry r) ? r : null;

        /// <summary>Miembros del salón de la fama (identificadores de jugadores).</summary>
        public IReadOnlyList<int> HallOfFame => _hallOfFame;

        /// <summary>Todos los récords vigentes (para guardarlos).</summary>
        public IEnumerable<RecordEntry> Records => _records.Values;

        /// <summary>Goles de carrera de cada jugador (para guardarlos).</summary>
        public IReadOnlyDictionary<int, int> AllCareerGoals => _careerGoals;

        /// <summary>Carga un récord guardado.</summary>
        /// <param name="record">Récord.</param>
        public void RestoreRecord(RecordEntry record) => _records[record.Key] = record;

        /// <summary>Carga los goles de carrera guardados de un jugador.</summary>
        public void RestoreCareerGoals(int playerId, int goals) => _careerGoals[playerId] = goals;

        /// <summary>Carga un miembro del salón de la fama.</summary>
        public void RestoreHallOfFame(int playerId)
        {
            if (!_hallOfFame.Contains(playerId))
            {
                _hallOfFame.Add(playerId);
            }
        }

        /// <summary>Goles de un jugador en toda su carrera registrada.</summary>
        public int CareerGoalsOf(int playerId) => _careerGoals.TryGetValue(playerId, out int g) ? g : 0;

        /// <summary>Suma un partido a la historia (goles y goleadas).</summary>
        public void RecordMatch(MatchReport report, int homeClubId, int awayClubId, int season)
        {
            int margin = Math.Abs(report.HomeGoals - report.AwayGoals);
            int winner = report.HomeGoals > report.AwayGoals ? homeClubId : awayClubId;
            if (margin > 0)
            {
                Beat(BiggestWin, margin, winner, season);
            }

            foreach (TeamReport team in report.Teams)
            {
                foreach (PlayerReport p in team.Players.Where(x => x.Stats.Goals > 0))
                {
                    var key = (season, p.PlayerId);
                    _seasonGoals[key] = (_seasonGoals.TryGetValue(key, out int g) ? g : 0) + p.Stats.Goals;
                    _careerGoals[p.PlayerId] = CareerGoalsOf(p.PlayerId) + p.Stats.Goals;
                    Beat(SeasonGoals, _seasonGoals[key], p.PlayerId, season);
                    Beat(CareerGoals, _careerGoals[p.PlayerId], p.PlayerId, season);
                }
            }
        }

        private void Beat(string key, float value, int holder, int season)
        {
            RecordEntry current = Record(key);
            if (current != null && value <= current.Value)
            {
                return;
            }

            bool announce = current == null || current.HolderId != holder;
            _records[key] = new RecordEntry { Key = key, Value = value, HolderId = holder, Season = season };
            if (announce)
            {
                _bus.Publish(new RecordBroken { Key = key, HolderId = holder, Value = value });
            }
        }

        /// <summary>Sortea si un jugador se retira esta temporada: desde <see cref="ManagementConfig.RetirementAge"/>, más probable cuanto mayor.</summary>
        /// <param name="p">Jugador.</param>
        /// <param name="today">Fecha.</param>
        /// <param name="rng">Azar.</param>
        public bool ShouldRetire(Player p, DateTime today, IRandom rng)
        {
            int over = p.AgeOn(today) - _config.RetirementAge;
            return over >= 0 && rng.Chance(Math.Min(MaxRetirementChance, _config.RetirementChancePerYear * (over + 1)));
        }

        private const float MaxRetirementChance = 0.95f;

        /// <summary>
        /// Fin de temporada: los veteranos pueden retirarse (más probable cuanto mayores). Los líderes y profesionales pasan
        /// al cuerpo técnico (técnicos con capacidad según su nivel); los grandes goleadores entran al salón de la fama.
        /// </summary>
        /// <param name="players">Jugadores a revisar.</param>
        /// <param name="today">Fecha.</param>
        /// <param name="rng">Azar.</param>
        /// <param name="newStaff">Fichas de cuerpo técnico creadas.</param>
        /// <returns>Jugadores que se retiraron.</returns>
        /// <param name="decide">Si se pasa, decide quién se retira en lugar del azar (para los retiros anunciados al empezar la temporada).</param>
        public List<Player> SeasonEnd(IEnumerable<Player> players, DateTime today, IRandom rng, out List<StaffMember> newStaff, Func<Player, bool> decide = null)
        {
            newStaff = new List<StaffMember>();
            var retired = new List<Player>();
            foreach (Player p in players.OrderBy(x => x.Id))
            {
                if (!(decide != null ? decide(p) : ShouldRetire(p, today, rng)))
                {
                    continue;
                }

                retired.Add(p);
                int? staffId = null;
                if (p.Personality == PersonalityTrait.Leader || p.Personality == PersonalityTrait.Professional)
                {
                    float level = PlayerRatings.Overall(p.Attributes, p.Position);
                    var staff = new StaffMember
                    {
                        Id = _nextStaffId++, Name = (p.FirstName + " " + p.LastName).Trim(), Role = StaffRole.Manager,
                        Ability = (int)Math.Max(1, Math.Min(20, Math.Round(level / 5f) + (p.Personality == PersonalityTrait.Leader ? 2 : 0))),
                        FormerPlayerId = p.Id
                    };
                    newStaff.Add(staff);
                    staffId = staff.Id;
                }

                if (CareerGoalsOf(p.Id) >= _config.HallOfFameGoals)
                {
                    _hallOfFame.Add(p.Id);
                }

                _bus.Publish(new PlayerRetired { PlayerId = p.Id, StaffId = staffId });
            }

            return retired;
        }
    }
}
