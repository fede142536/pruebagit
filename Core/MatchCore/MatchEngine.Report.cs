using System;
using Football.Core.Common;
using Football.Core.Domain;

namespace Football.Core.MatchCore
{
    // Estadísticas que necesitan algo más que un contador: remates (xG y si iban al arco), goles, asistencias e informe final.
    public sealed partial class MatchEngine
    {
        private const int MaxShots = 256;
        private const float OnTargetProbeSeconds = 4f;
        private const float OnTargetMinSpeed = 0.5f;

        private readonly ShotRecord[] _shots = new ShotRecord[MaxShots];
        private int _shotCount;
        private readonly BallState _shotProbe = new BallState();
        private bool _shotOnTargetCounted;
        private float _keeperYAtShot;

        /// <summary>Remates registrados hasta ahora.</summary>
        public int ShotCount => _shotCount;

        /// <summary>Remate por posición (0..<see cref="ShotCount"/> - 1).</summary>
        /// <param name="index">Índice.</param>
        public ShotRecord Shot(int index) => _shots[index];

        /// <summary>
        /// Anota un remate recién pateado: su xG (el mismo modelo con el que decide la IA) y si iba entre los tres palos,
        /// calculado con la trayectoria al salir del pie (antes se contaba solo si el balón seguía «vivo» al llegar, y un
        /// desvío de un defensor lo borraba).
        /// </summary>
        private void RecordShot(MatchPlayer p)
        {
            TeamState team = Teams[p.Team];
            Vec2 origin = Ball.Pos.Horizontal;
            float distance = Vec2.Distance(origin, Pitch.GoalCenter(team.AttackDirection));
            float xg = MathF.Min(Config.Report.XgMax,
                Config.Report.XgCalibration * ExpectedGoal(p, origin, distance, NearestOpponentDistance(origin, p.Team)));
            bool onTarget = TrajectoryOnTarget(team.AttackDirection);
            _keeperYAtShot = GoalGuardY(1 - p.Team); // para medir lo colocado del remate
            p.Stats.Shots++;
            p.Stats.Xg += xg;
            team.Stats.Xg += xg;
            _shotOnTargetCounted = false;
            if (onTarget)
            {
                CountShotOnTarget();
            }

            if (_shotCount < MaxShots)
            {
                _shots[_shotCount++] = new ShotRecord
                {
                    Team = p.Team, PlayerIndex = p.Index, PlayerId = p.PlayerId, Minute = Minute, Distance = distance, Xg = xg,
                    OnTarget = onTarget
                };
            }
        }

        /// <summary>
        /// Altura lateral de quien defiende el arco: el arquero o, sin arquero (potrero), el jugador más cercano a la portería.
        /// </summary>
        private float GoalGuardY(int team)
        {
            TeamState t = Teams[team];
            if (t.HasKeeper)
            {
                return t.Players[0].Pos.Y;
            }

            Vec2 goal = Pitch.GoalCenter(t.DefendedEnd);
            float best = float.MaxValue, y = 0f;
            for (int i = 0; i < t.Players.Length; i++)
            {
                MatchPlayer p = t.Players[i];
                float d = Vec2.Distance(p.Pos, goal);
                if (!p.SentOff && d < best)
                {
                    best = d;
                    y = p.Pos.Y;
                }
            }

            return y;
        }

        /// <summary>Cuenta el remate en curso como al arco (una sola vez por remate).</summary>
        private void CountShotOnTarget()
        {
            if (_shotOnTargetCounted || _lastKicker == null)
            {
                return;
            }

            _shotOnTargetCounted = true;
            _lastKicker.Stats.ShotsOnTarget++;
            Teams[_lastKicker.Team].Stats.ShotsOnTarget++;
            if (_shotCount > 0)
            {
                _shots[_shotCount - 1].OnTarget = true;
            }
        }

        /// <summary>Simula el vuelo del balón sin jugadores: ¿cruza la línea de gol entre los palos y bajo el larguero?</summary>
        private bool TrajectoryOnTarget(float attackDirection)
        {
            _shotProbe.Pos = Ball.Pos;
            _shotProbe.Vel = Ball.Vel;
            _shotProbe.Spin = Ball.Spin;
            float dt = _dt;
            int steps = (int)(OnTargetProbeSeconds / dt);
            for (int i = 0; i < steps; i++)
            {
                if (BallPhysics.Step(_shotProbe, dt, Config.Ball, Pitch) != BallHit.None)
                {
                    return false; // al palo o al larguero: no cuenta como remate al arco
                }

                if (attackDirection * _shotProbe.Pos.X >= Pitch.HalfLength)
                {
                    return MathF.Abs(_shotProbe.Pos.Y) < Pitch.GoalHalfWidth && _shotProbe.Pos.Z < Pitch.GoalHeight;
                }

                if (_shotProbe.Vel.HorizontalLength < OnTargetMinSpeed)
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>Acredita el gol al autor (o en propia puerta) y la asistencia al último pase completado de un compañero.</summary>
        private void CreditGoal(int scoring, int scorerIndex)
        {
            TeamState team = Teams[scoring];
            if (scorerIndex < 0)
            {
                return;
            }

            MatchPlayer scorer = Players[scorerIndex];
            if (scorer.Team != scoring)
            {
                scorer.Stats.OwnGoals++;
                return;
            }

            scorer.Stats.Goals++;
            if (team.LastReceiver == scorer && team.LastPasser != null && team.LastPasser != scorer)
            {
                team.LastPasser.Stats.Assists++;
            }

            team.LastPasser = team.LastReceiver = null;
        }

        /// <summary>
        /// Arma el informe del partido. Genera objetos nuevos: llamarlo una vez, al terminar, nunca dentro del bucle de juego.
        /// </summary>
        /// <returns>Informe con equipos, jugadores (incluidos los que salieron), remates y notas.</returns>
        public MatchReport BuildReport()
        {
            var report = new MatchReport();
            int totalTicks = Math.Max(1, Teams[0].Stats.PossessionTicks + Teams[1].Stats.PossessionTicks);
            for (int t = 0; t < 2; t++)
            {
                TeamState team = Teams[t];
                TeamReport tr = report.Teams[t];
                tr.Name = team.Name;
                tr.Goals = team.Stats.Goals;
                tr.Stats = team.Stats;
                tr.Possession = (float)team.Stats.PossessionTicks / totalTicks;
                for (int i = 0; i < team.Players.Length; i++)
                {
                    tr.Players.Add(PlayerEntry(team.Players[i]));
                }

                foreach (MatchPlayer gone in team.Departed)
                {
                    tr.Players.Add(PlayerEntry(gone));
                }
            }

            for (int t = 0; t < 2; t++)
            {
                foreach (PlayerReport p in report.Teams[t].Players)
                {
                    p.Rating = MatchReport.Rate(Config.Report, p, report.Teams[t].Goals, report.Teams[1 - t].Goals);
                }
            }

            for (int i = 0; i < _shotCount; i++)
            {
                report.Shots.Add(_shots[i]);
            }

            return report;
        }

        private PlayerReport PlayerEntry(MatchPlayer p)
        {
            float end = p.MinuteOff >= 0f ? p.MinuteOff : PlayedMinute;
            return new PlayerReport
            {
                PlayerId = p.PlayerId,
                Name = p.Name,
                Number = p.Number,
                Role = p.Role,
                Started = p.MinuteOn <= 0f,
                Minutes = Math.Max(0f, end - p.MinuteOn),
                StaminaLeft = p.Stamina,
                Stats = p.Stats
            };
        }
    }
}
