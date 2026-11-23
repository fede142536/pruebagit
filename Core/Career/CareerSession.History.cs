using System;
using System.Collections.Generic;
using System.Linq;
using Football.Core.Common;
using Football.Core.Competitions;
using Football.Core.Domain;

namespace Football.Core.Career
{
    /// <summary>Resultado de una competición para el club del usuario (para el historial y el cartel de campeón).</summary>
    public sealed class CompetitionRecord
    {
        /// <summary>Identificador de la competición.</summary>
        public int CompetitionId;
        /// <summary>Temporada.</summary>
        public int Season;
        /// <summary>Torneo.</summary>
        public TournamentKind Kind;
        /// <summary>Nombre visible («Apertura 2026»).</summary>
        public string Name;
        /// <summary>Ya terminó (todos los partidos jugados; en la copa, la final).</summary>
        public bool Finished;
        /// <summary>Campeón (0 si no terminó).</summary>
        public int ChampionClubId;
        /// <summary>El campeón es el club del usuario.</summary>
        public bool UserChampion;
        /// <summary>Posición del usuario en la tabla de la competición (1 = primero); 0 en la copa.</summary>
        public int UserPosition;
        /// <summary>Partidos que jugó el usuario.</summary>
        public int UserPlayed;
        /// <summary>Copa: última ronda en la que jugó el usuario (0 si no jugó).</summary>
        public int UserLastCupRound;
        /// <summary>Copa: cantidad de partidos de esa ronda (1 = final, 2 = semifinales, 4 = cuartos…).</summary>
        public int UserLastCupRoundSize;
        /// <summary>Copa: el usuario quedó eliminado (perdió su último partido).</summary>
        public bool UserEliminated;
    }

    // Historial de competiciones, calendario del usuario y cruces de la Copa Argentina.
    public sealed partial class CareerSession
    {
        private readonly HashSet<int> _announcedCompetitions = new HashSet<int>();
        private readonly List<CompetitionRecord> _ceremonies = new List<CompetitionRecord>();

        /// <summary>Competiciones que terminaron desde la última vez que se pidieron (para mostrar el cartel de campeón o el final del torneo). Las vacía.</summary>
        public List<CompetitionRecord> TakeCeremonies()
        {
            var taken = new List<CompetitionRecord>(_ceremonies);
            _ceremonies.Clear();
            return taken;
        }

        /// <summary>Hay una ceremonia sin mostrar.</summary>
        public bool HasCeremony => _ceremonies.Count > 0;

        private void SeedAnnouncedCompetitions()
        {
            foreach (CompetitionInfo c in Data.Competitions)
            {
                if (RecordOf(c).Finished)
                {
                    _announcedCompetitions.Add(c.Id); // las que ya habían terminado al cargar la partida no se vuelven a anunciar
                }
            }
        }

        /// <summary>Busca competiciones recién terminadas: las anota para mostrarlas, publica la noticia y, si ganó el usuario, la noticia propia.</summary>
        private void CheckCompetitionEnds()
        {
            foreach (CompetitionInfo c in Data.Competitions.OrderBy(x => x.Id).ToList())
            {
                if (_announcedCompetitions.Contains(c.Id))
                {
                    continue;
                }

                CompetitionRecord record = RecordOf(c);
                if (!record.Finished)
                {
                    continue;
                }

                _announcedCompetitions.Add(c.Id);
                _ceremonies.Add(record);
                if (record.UserChampion)
                {
                    _inbox.Post("news.user_champion", c.Name);
                }
                else if (c.Kind != TournamentKind.Cup && !Data.TournamentOnly)
                {
                    _inbox.Post("news.competition_champion", c.Name, Data.Club(record.ChampionClubId).Name);
                }
            }
        }

        /// <summary>Todas las competiciones que se armaron (de la más reciente a la más antigua) con el resultado del usuario.</summary>
        public List<CompetitionRecord> CompetitionHistory() =>
            Data.Competitions.OrderByDescending(c => c.Season).ThenBy(c => c.Kind == TournamentKind.Apertura ? 0 : c.Kind == TournamentKind.Cup ? 1 : 2)
                .Select(RecordOf).ToList();

        private CompetitionRecord RecordOf(CompetitionInfo c)
        {
            var record = new CompetitionRecord { CompetitionId = c.Id, Season = c.Season, Kind = c.Kind, Name = c.Name };
            List<Fixture> fixtures = Data.Fixtures.Where(f => f.CompetitionId == c.Id).ToList();
            List<Fixture> mine = fixtures.Where(f => Involves(f, Data.UserClubId)).OrderBy(f => f.Date).ToList();
            record.UserPlayed = mine.Count(f => f.IsPlayed);
            if (c.Kind == TournamentKind.Cup)
            {
                FillCupRecord(record, fixtures, mine);
                record.UserChampion = record.Finished && record.ChampionClubId == Data.UserClubId;
                return record;
            }

            record.Finished = fixtures.Count > 0 && fixtures.All(f => f.IsPlayed);
            List<Standing> table = Table(f => f.CompetitionId == c.Id);
            if (record.UserPlayed > 0)
            {
                record.UserPosition = table.FindIndex(s => s.ClubId == Data.UserClubId) + 1;
            }

            if (record.Finished)
            {
                record.ChampionClubId = table[0].ClubId;
                record.UserChampion = record.ChampionClubId == Data.UserClubId;
            }

            return record;
        }

        private void FillCupRecord(CompetitionRecord record, List<Fixture> fixtures, List<Fixture> mine)
        {
            if (fixtures.Count == 0)
            {
                return;
            }

            int lastRound = fixtures.Max(f => f.Round);
            List<Fixture> last = fixtures.Where(f => f.Round == lastRound).ToList();
            if (last.Count == 1 && lastRound > 1 && last[0].IsPlayed)
            {
                record.Finished = true;
                record.ChampionClubId = Knockout.Winner(last[0], new SeededRandom(Mix(last[0].Id * 31L)));
            }

            Fixture lastMine = mine.LastOrDefault(f => f.IsPlayed);
            if (lastMine != null)
            {
                record.UserLastCupRound = lastMine.Round;
                record.UserLastCupRoundSize = fixtures.Count(f => f.Round == lastMine.Round);
                record.UserEliminated = Knockout.Winner(lastMine, new SeededRandom(Mix(lastMine.Id * 31L))) != Data.UserClubId;
            }
        }

        /// <summary>Partidos del club del usuario en una temporada (todos los torneos y la copa), en orden de fecha.</summary>
        /// <param name="season">Temporada (año).</param>
        public List<Fixture> UserFixtures(int season) =>
            Data.Fixtures.Where(f => f.CompetitionId / 10 == season && Involves(f, Data.UserClubId)).OrderBy(f => f.Date).ThenBy(f => f.Id).ToList();

        /// <summary>Temporadas con partidos armados, de la más reciente a la más antigua.</summary>
        public List<int> SeasonsWithFixtures() => Data.Competitions.Select(c => c.Season).Distinct().OrderByDescending(s => s).ToList();

        /// <summary>Nombre de la competición de un partido («Apertura 2026», «Copa Argentina 2026»).</summary>
        public string CompetitionName(Fixture f) => Data.Competitions.Find(c => c.Id == f.CompetitionId)?.Name ?? string.Empty;

        // ------------------------------------------------------------------ cruces de la copa

        /// <summary>Rondas de la Copa Argentina armadas hasta ahora en una temporada (0 si no hay copa).</summary>
        public int CupRounds(int season)
        {
            int id = CompetitionInfo.IdOf(season, TournamentKind.Cup);
            return Data.Fixtures.Where(f => f.CompetitionId == id).Select(f => f.Round).DefaultIfEmpty(0).Max();
        }

        /// <summary>Cruces de una ronda de la copa, en orden de armado.</summary>
        /// <param name="season">Temporada.</param>
        /// <param name="round">Ronda (desde 1).</param>
        public List<Fixture> CupFixtures(int season, int round)
        {
            int id = CompetitionInfo.IdOf(season, TournamentKind.Cup);
            return Data.Fixtures.Where(f => f.CompetitionId == id && f.Round == round).OrderBy(f => f.Id).ToList();
        }

        /// <summary>Ronda de la copa que corresponde mostrar: la del próximo partido del usuario o, si ya no tiene, la última en la que jugó (o la ronda más avanzada).</summary>
        /// <param name="season">Temporada.</param>
        public int UserCupRound(int season)
        {
            int id = CompetitionInfo.IdOf(season, TournamentKind.Cup);
            List<Fixture> mine = Data.Fixtures.Where(f => f.CompetitionId == id && Involves(f, Data.UserClubId)).OrderBy(f => f.Round).ToList();
            Fixture next = mine.FirstOrDefault(f => !f.IsPlayed);
            if (next != null)
            {
                return next.Round;
            }

            return mine.Count > 0 ? mine.Last().Round : Math.Max(1, CupRounds(season));
        }

        /// <summary>Clubes que pasaron directo en una ronda de la copa (sin partido), para completar los cruces. Vacío si no hubo.</summary>
        /// <param name="season">Temporada.</param>
        /// <param name="round">Ronda.</param>
        public List<int> CupByesOf(int season, int round)
        {
            int id = CompetitionInfo.IdOf(season, TournamentKind.Cup);
            List<Fixture> cup = Data.Fixtures.Where(f => f.CompetitionId == id).ToList();
            return cup.Count == 0 || round > cup.Max(f => f.Round) ? new List<int>() : CupByes(cup, round);
        }

        /// <summary>Ganador de un cruce de copa ya jugado (define por penales si hubo empate); 0 si no se jugó.</summary>
        public int CupWinner(Fixture f) => f.IsPlayed ? Knockout.Winner(f, new SeededRandom(Mix(f.Id * 31L))) : 0;
    }
}
