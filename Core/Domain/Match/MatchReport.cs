using System.Collections.Generic;

namespace Football.Core.Domain
{
    /// <summary>Resultado de un jugador en el partido, para la gestión (forma, moral, fatiga, lesiones, sanciones).</summary>
    public sealed class PlayerReport
    {
        /// <summary>Identificador del jugador en el modelo de datos (<see cref="PlayerMatchView.Id"/>).</summary>
        public int PlayerId;
        /// <summary>Nombre.</summary>
        public string Name;
        /// <summary>Dorsal.</summary>
        public int Number;
        /// <summary>Posición en la que terminó (o en la que salió).</summary>
        public Position Role;
        /// <summary>Fue titular.</summary>
        public bool Started;
        /// <summary>Minutos jugados.</summary>
        public float Minutes;
        /// <summary>Resistencia al terminar su participación (0..1).</summary>
        public float StaminaLeft;
        /// <summary>Nota del partido (1–10), o 0 si jugó demasiado poco para calificarlo.</summary>
        public float Rating;
        /// <summary>Estadísticas.</summary>
        public PlayerMatchStats Stats;
    }

    /// <summary>Resultado de un equipo en el partido.</summary>
    public sealed class TeamReport
    {
        /// <summary>Nombre del equipo.</summary>
        public string Name;
        /// <summary>Goles.</summary>
        public int Goals;
        /// <summary>Posesión (0..1).</summary>
        public float Possession;
        /// <summary>Estadísticas del equipo.</summary>
        public TeamStats Stats;
        /// <summary>Jugadores que participaron (titulares y suplentes que entraron).</summary>
        public readonly List<PlayerReport> Players = new List<PlayerReport>();
    }

    /// <summary>
    /// Informe completo de un partido: se genera una sola vez al terminar (los contadores del partido no generan basura) y es
    /// lo que la gestión aplica a la carrera. Lo devuelven tanto el motor en tiempo real como el simulador rápido.
    /// </summary>
    public sealed class MatchReport
    {
        /// <summary>Local (0) y visitante (1).</summary>
        public readonly TeamReport[] Teams = { new TeamReport(), new TeamReport() };
        /// <summary>Todos los remates, en orden, con su xG.</summary>
        public readonly List<ShotRecord> Shots = new List<ShotRecord>();
        /// <summary>Goles del local.</summary>
        public int HomeGoals => Teams[0].Goals;
        /// <summary>Goles del visitante.</summary>
        public int AwayGoals => Teams[1].Goals;
        /// <summary>Lo generó el simulador rápido (<c>MatchSim</c>) en lugar del motor en tiempo real.</summary>
        public bool Simulated;

        /// <summary>Busca a un jugador por su identificador.</summary>
        /// <param name="playerId">Identificador del jugador.</param>
        /// <returns>Su informe, o null si no participó.</returns>
        public PlayerReport FindPlayer(int playerId)
        {
            for (int t = 0; t < Teams.Length; t++)
            {
                foreach (PlayerReport p in Teams[t].Players)
                {
                    if (p.PlayerId == playerId)
                    {
                        return p;
                    }
                }
            }

            return null;
        }

        /// <summary>Calcula la nota de un jugador a partir de sus estadísticas.</summary>
        /// <param name="c">Pesos.</param>
        /// <param name="p">Jugador (con minutos y estadísticas).</param>
        /// <param name="goalsFor">Goles de su equipo.</param>
        /// <param name="goalsAgainst">Goles recibidos por su equipo.</param>
        /// <returns>Nota entre <see cref="ReportConfig.RatingMin"/> y <see cref="ReportConfig.RatingMax"/>, o 0 sin minutos suficientes.</returns>
        public static float Rate(ReportConfig c, PlayerReport p, int goalsFor, int goalsAgainst)
        {
            if (p.Minutes < c.MinMinutesForRating)
            {
                return 0f;
            }

            PlayerMatchStats s = p.Stats;
            float rating = c.RatingBase
                           + c.GoalWeight * s.Goals + c.AssistWeight * s.Assists + c.ShotOnTargetWeight * s.ShotsOnTarget
                           + c.RecoveryWeight * s.Recoveries + c.TackleWonWeight * s.TacklesWon + c.SaveWeight * s.Saves
                           - c.YellowPenalty * s.Yellows - c.RedPenalty * s.Reds - c.OwnGoalPenalty * s.OwnGoals;
            if (s.Passes >= 5)
            {
                rating += c.PassAccuracyWeight * ((float)s.PassesCompleted / s.Passes - c.PassAccuracyReference);
            }

            if (IsDefensive(p.Role))
            {
                rating -= c.GoalConcededPenalty * goalsAgainst;
            }

            rating += goalsFor > goalsAgainst ? c.ResultWeight : goalsFor < goalsAgainst ? -c.ResultWeight : 0f;
            return rating < c.RatingMin ? c.RatingMin : rating > c.RatingMax ? c.RatingMax : rating;
        }

        private static bool IsDefensive(Position role) =>
            role == Position.Goalkeeper || role == Position.CenterBack || role == Position.LeftBack || role == Position.RightBack;
    }
}
