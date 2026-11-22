using System;
using Football.Core.Common;
using Football.Core.Domain;

namespace Football.Core.MatchCore
{
    /// <summary>
    /// Motor de partido en tiempo real, sin gráficos y con paso fijo. Contiene la física del balón, el movimiento,
    /// las reglas, el árbitro y la IA. La capa Unity solo dibuja un <see cref="MatchSnapshot"/> por paso.
    /// Todo el azar pasa por el <see cref="IRandom"/> inyectado: misma semilla y mismas entradas dan el mismo partido.
    /// </summary>
    public sealed partial class MatchEngine
    {
        /// <summary>Configuración del partido.</summary>
        public readonly MatchConfig Config;
        /// <summary>Geometría del campo.</summary>
        public readonly Pitch Pitch;
        /// <summary>Balón.</summary>
        public readonly BallState Ball = new BallState();
        /// <summary>Equipos: 0 local, 1 visitante.</summary>
        public readonly TeamState[] Teams = new TeamState[2];
        /// <summary>Todos los jugadores: índice = equipo × jugadores por equipo + puesto.</summary>
        public readonly MatchPlayer[] Players;
        /// <summary>Jugadores por equipo (11, o 3 en la cancha cerrada).</summary>
        public readonly int PlayersPerTeam;
        /// <summary>Eventos del partido.</summary>
        public readonly MatchEventLog Events = new MatchEventLog();

        /// <summary>Fase actual.</summary>
        public MatchPhase Phase { get; private set; }
        /// <summary>Parte actual (1 o 2).</summary>
        public int Half { get; private set; } = 1;
        /// <summary>Segundos reales transcurridos en la parte actual.</summary>
        public float HalfElapsed { get; private set; }
        /// <summary>Pasos de simulación ejecutados.</summary>
        public long TickCount { get; private set; }
        /// <summary>Jugador que tiene el balón controlado, o null si está suelto.</summary>
        public MatchPlayer Owner { get; private set; }
        /// <summary>Último jugador que tocó el balón.</summary>
        public MatchPlayer LastToucher { get; private set; }
        /// <summary>Saque pendiente (válido en la fase <see cref="MatchPhase.SetPiece"/>).</summary>
        public Restart CurrentRestart => _restart;

        /// <summary>Terminó el partido.</summary>
        public bool IsFinished => Phase == MatchPhase.Finished;

        /// <summary>Minuto de partido (0 a 90) representado por el tiempo real transcurrido.</summary>
        public float Minute => ((Half - 1) + HalfElapsed / Config.Rules.HalfDuration) * Config.Rules.MinutesPerHalf;

        private readonly IRandom _rng;
        private readonly float _dt;
        private readonly float[] _scratchX;

        private Restart _restart;
        private MatchPlayer _taker;
        private float _phaseTimer;
        private float _takerTimeout;
        private int _kickoffTeam;
        private Vec3 _prevBallPos;
        private float _planTimer;
        private float _adaptTimer;
        private const float DecisionStagger = 0.011f;
        private const float PenaltyShotSeconds = 2f;

        /// <summary>Cambia la mentalidad y la presión de un equipo durante el partido.</summary>
        /// <param name="team">Equipo (0 local, 1 visitante).</param>
        /// <param name="mentality">Mentalidad.</param>
        /// <param name="pressing">Presión.</param>
        public void SetTactics(int team, Mentality mentality, PressLevel pressing)
        {
            TeamState t = Teams[team];
            if (t.Mentality == mentality && t.Pressing == pressing)
            {
                return;
            }

            t.Mentality = mentality;
            t.Pressing = pressing;
            RefreshStyle(t); // mentalidad y presión, sin perder un grito en curso ni la charla del entretiempo
            AddEvent(MatchEventType.TacticsChanged, team, -1, Ball.Pos.Horizontal);
        }

        /// <summary>La IA revisa de vez en cuando su táctica según el marcador y el minuto.</summary>
        private void AdaptTactics(float dt)
        {
            _adaptTimer -= dt;
            if (_adaptTimer > 0f)
            {
                return;
            }

            _adaptTimer = Config.Ai.AdaptInterval;
            for (int t = 0; t < 2; t++)
            {
                TeamState team = Teams[t];
                if (team.IsHuman || !team.AdaptiveTactics)
                {
                    continue;
                }

                int difference = team.Stats.Goals - Teams[1 - t].Stats.Goals;
                Mentality mentality = Tactics.Adaptive(difference, Minute, Config.Ai, out PressLevel pressing);
                SetTactics(t, mentality, pressing);
            }
        }

        /// <summary>Crea un partido listo para el saque inicial del equipo local.</summary>
        /// <param name="config">Configuración.</param>
        /// <param name="rng">Fuente de azar con semilla.</param>
        /// <param name="home">Equipo local (ataca hacia +X en la primera parte).</param>
        /// <param name="away">Equipo visitante.</param>
        /// <param name="homeDifficulty">Índice de dificultad de la IA del local.</param>
        /// <param name="awayDifficulty">Índice de dificultad de la IA del visitante.</param>
        public MatchEngine(MatchConfig config, IRandom rng, MatchTeamSetup home, MatchTeamSetup away,
            int homeDifficulty = 2, int awayDifficulty = 2)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            if (config.TickRate <= 0 || config.Rules.HalfDuration <= 0f)
            {
                throw new ArgumentException("TickRate y HalfDuration deben ser positivos.", nameof(config));
            }

            if (homeDifficulty < 0 || homeDifficulty >= config.Difficulties.Length || awayDifficulty < 0 || awayDifficulty >= config.Difficulties.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(homeDifficulty), "Dificultad inexistente en la configuración.");
            }

            if (home == null || away == null || home.Players == null || away.Players == null
                || home.Players.Length != away.Players.Length || home.Players.Length < 1)
            {
                throw new ArgumentException("Los dos equipos deben tener la misma cantidad de jugadores.", nameof(away));
            }

            PlayersPerTeam = home.Players.Length;
            Players = new MatchPlayer[2 * PlayersPerTeam];
            _scratchX = new float[PlayersPerTeam];
            _offsideFlag = new bool[Players.Length];
            _claimed = new bool[Players.Length];
            Pitch = new Pitch(config.Pitch);
            _dt = config.FixedDeltaTime();
            Teams[0] = new TeamState(0, 1f, home, config.Difficulties[homeDifficulty], config.Player, Players);
            Teams[1] = new TeamState(1, -1f, away, config.Difficulties[awayDifficulty], config.Player, Players);

            for (int i = 0; i < Players.Length; i++)
            {
                Players[i].DecisionTimer = (i % PlayersPerTeam) * DecisionStagger;
            }

            PlaceForKickoff(0);
        }

        /// <summary>Avanza la simulación un paso fijo.</summary>
        public void Tick()
        {
            if (Phase == MatchPhase.Finished)
            {
                return;
            }

            switch (Phase)
            {
                case MatchPhase.Playing:
                    TickPlaying(_dt);
                    break;
                case MatchPhase.SetPiece:
                    TickSetPiece(_dt);
                    break;
                case MatchPhase.GoalPause:
                    TickGoalPause(_dt);
                    break;
            }

            if (Phase != MatchPhase.Finished)
            {
                HalfElapsed += _dt;
                if (HalfElapsed >= Config.Rules.HalfDuration && !PenaltyPending())
                {
                    EndHalf();
                }
            }

            UpdateSubstitutionWindows();
            UpdateShouts(_dt); // los gritos se gastan con el reloj del partido, también en saques y goles
            TickCount++;
            ClearInputImpulses();
        }

        /// <summary>Hash del estado completo, para comprobar el determinismo.</summary>
        public ulong StateHash()
        {
            ulong h = 14695981039346656037UL;
            h = Mix(h, BitConverter.SingleToInt32Bits(Ball.Pos.X));
            h = Mix(h, BitConverter.SingleToInt32Bits(Ball.Pos.Y));
            h = Mix(h, BitConverter.SingleToInt32Bits(Ball.Pos.Z));
            for (int i = 0; i < Players.Length; i++)
            {
                h = Mix(h, BitConverter.SingleToInt32Bits(Players[i].Pos.X));
                h = Mix(h, BitConverter.SingleToInt32Bits(Players[i].Pos.Y));
                h = Mix(h, BitConverter.SingleToInt32Bits(Players[i].Heading));
                h = Mix(h, BitConverter.SingleToInt32Bits(Players[i].Speed));
                h = Mix(h, BitConverter.SingleToInt32Bits(Players[i].Stamina));
            }

            h = Mix(h, BitConverter.SingleToInt32Bits(Ball.Vel.X));
            h = Mix(h, BitConverter.SingleToInt32Bits(Ball.Vel.Y));
            h = Mix(h, BitConverter.SingleToInt32Bits(Ball.Vel.Z));
            h = Mix(h, Owner != null ? Owner.Index : -1);

            for (int t = 0; t < 2; t++)
            {
                TeamStats s = Teams[t].Stats;
                h = Mix(h, s.Goals);
                h = Mix(h, s.Shots);
                h = Mix(h, s.Passes);
                h = Mix(h, s.Fouls);
                h = Mix(h, s.Yellows + 16 * s.Reds);
                h = Mix(h, s.PossessionTicks);
            }

            h = Mix(h, Events.Count);
            h = Mix(h, (int)Phase);
            return Mix(h, (int)TickCount);
        }

        private static ulong Mix(ulong h, int value)
        {
            h ^= (uint)value;
            return h * 1099511628211UL;
        }

        /// <summary>Un penal ya pitado se lanza aunque se cumpla el tiempo, y se espera a que el tiro termine.</summary>
        private bool PenaltyPending()
        {
            if (_restart.Kind != RestartKind.Penalty)
            {
                return false;
            }

            return Phase == MatchPhase.SetPiece
                   || (Phase == MatchPhase.Playing && _shotLive && TickCount - _shotStartTick < Config.TickRate * PenaltyShotSeconds);
        }

        private void AddEvent(MatchEventType type, int team, int playerIndex, Vec2 at)
        {
            Events.Add(new MatchEvent { Type = type, Team = team, PlayerIndex = playerIndex, Minute = Minute, X = at.X, Y = at.Y });
        }

        private void TickPlaying(float dt)
        {
            TickTimers(dt);
            AdaptTactics(dt);
            AiSubstitutions(dt);
            CheckInjuries();
            StopForInjury();
            if (Phase != MatchPhase.Playing)
            {
                return;
            }

            UpdateHumanControl(dt);
            _planTimer -= dt;
            if (_planTimer <= 0f)
            {
                _planTimer = Config.Ai.DecisionInterval;
                UpdateTeamPlans();
            }

            RunPlayerAi(dt);
            ApplyHumanInput(dt);
            MovePlayers(dt);

            UpdateBall(dt);
            if (Phase != MatchPhase.Playing)
            {
                return;
            }

            BounceOffWalls();
            TrackDribbleCut(dt);
            ResolveTackles();
            if (Phase != MatchPhase.Playing)
            {
                return;
            }

            ResolveSlides();
            if (Phase != MatchPhase.Playing)
            {
                return;
            }

            CheckBoundaries();
            CountPossession();
        }

        private void TickTimers(float dt)
        {
            for (int i = 0; i < Players.Length; i++)
            {
                MatchPlayer p = Players[i];
                p.ActionTimer -= dt;
                p.KickTimer -= dt;
                p.TackleTimer -= dt;
                p.ActionDisplayTimer -= dt;
                if (p.SlideTimer > 0f)
                {
                    p.SlideTimer -= dt;
                    if (p.SlideTimer <= 0f)
                    {
                        p.RecoverTimer = Config.Rules.SlideRecovery;
                    }
                }
                else
                {
                    p.RecoverTimer -= dt;
                }

                if (p.StumbleTimer > 0f)
                {
                    p.StumbleTimer -= dt;
                }
            }
        }

        private void CountPossession()
        {
            MatchPlayer holder = Owner ?? LastToucher;
            if (holder != null)
            {
                Teams[holder.Team].Stats.PossessionTicks++;
            }
        }

        private void MovePlayers(float dt)
        {
            float margin = Config.Pitch.PlayerMargin;
            for (int i = 0; i < Players.Length; i++)
            {
                MatchPlayer p = Players[i];
                if (p.SentOff)
                {
                    continue;
                }

                if (p.SlideTimer > 0f)
                {
                    SlideStep(p, dt);
                }
                else if (p.RecoverTimer > 0f)
                {
                    p.Speed = 0f; // levantándose tras la barrida
                }
                else
                {
                    PlayerMotion.Step(p, dt, Config.Player);
                    if (p == Teams[p.Team].SecondPresser)
                    {
                        // Salir a presionar por la segunda marca cansa más que correr sin más.
                        p.Stamina = MathUtil.Clamp01(p.Stamina - Config.Player.SecondPressStaminaDrain * dt);
                    }
                }

                float run = p.Speed * dt;
                p.Stats.Distance += run;
                Teams[p.Team].Stats.Distance += run;
                p.Pos = Pitch.Clamp(p.Pos, margin);
                KeepKeeperInFrontOfHisLine(p);
                FaceTheBall(p, dt);

                if (p.StumbleTimer > 0f)
                {
                    p.Speed = MathF.Min(p.Speed, p.TopSpeed * Config.Player.StumbleSpeedFactor);
                    p.Action = PlayerAction.Stumble;
                }
                else if (p.ActionDisplayTimer <= 0f)
                {
                    float relative = p.Speed / p.TopSpeed;
                    p.Action = p.Speed < 0.5f ? PlayerAction.Idle : (relative > 0.85f ? PlayerAction.Sprint : PlayerAction.Run);
                }
            }

            Separate();
            if (Owner != null)
            {
                KeepKeeperInFrontOfHisLine(Owner); // la separación también podría empujarlo detrás de la línea
            }
        }

        /// <summary>El arquero con el balón nunca cruza su propia línea de gol (sería gol en contra).</summary>
        private void KeepKeeperInFrontOfHisLine(MatchPlayer p)
        {
            if (Owner != p || !p.IsKeeper)
            {
                return;
            }

            float end = Teams[p.Team].DefendedEnd;
            float limit = Pitch.HalfLength - Config.Player.KeeperLineInset;
            if (end * p.Pos.X > limit)
            {
                p.Pos = new Vec2(end * limit, p.Pos.Y);
            }
        }

        /// <summary>Un jugador parado gira poco a poco hacia el balón, como haría un jugador real atento a la jugada.</summary>
        private void FaceTheBall(MatchPlayer p, float dt)
        {
            if (p.Speed > 0.3f || Owner == p)
            {
                return;
            }

            Vec2 toBall = Ball.Pos.Horizontal - p.Pos;
            if (toBall.LengthSquared < 1f)
            {
                return;
            }

            float maxTurn = Config.Player.TurnRate * p.TurnScale * Config.Ai.IdleTurnFactor * dt;
            p.Heading += MathUtil.Clamp(MathUtil.AngleDiff(toBall.Angle, p.Heading), -maxTurn, maxTurn);
        }

        private void Separate()
        {
            float min = Config.Player.SeparationDistance;
            float minSq = min * min;
            for (int i = 0; i < Players.Length; i++)
            {
                MatchPlayer a = Players[i];
                if (a.SentOff)
                {
                    continue;
                }

                for (int j = i + 1; j < Players.Length; j++)
                {
                    MatchPlayer b = Players[j];
                    if (b.SentOff)
                    {
                        continue;
                    }

                    Vec2 delta = b.Pos - a.Pos;
                    float distSq = delta.LengthSquared;
                    if (distSq >= minSq)
                    {
                        continue;
                    }

                    float dist = MathF.Sqrt(distSq);
                    Vec2 push = dist > MathUtil.Epsilon ? delta / dist : new Vec2(1f, 0f);
                    float overlap = min - dist;
                    if (a.Team == b.Team || Phase != MatchPhase.Playing)
                    {
                        a.Pos -= push * (overlap * 0.5f);
                        b.Pos += push * (overlap * 0.5f);
                        continue;
                    }

                    // Choque entre rivales: el más fuerte desplaza más al otro y, si el golpe es fuerte, el débil trastabilla.
                    float sa = a.Skill(AttributeId.Strength), sb = b.Skill(AttributeId.Strength);
                    float shareA = MathUtil.Clamp(0.5f + 0.5f * (sb - sa), 0.2f, 0.8f);
                    a.Pos -= push * (overlap * shareA);
                    b.Pos += push * (overlap * (1f - shareA));
                    float impact = MathF.Abs(Vec2.Dot(a.Velocity - b.Velocity, push));
                    if (impact > Config.Player.StumbleMinImpact && a.StumbleTimer <= 0f && b.StumbleTimer <= 0f)
                    {
                        Collide(a, b, sa, sb);
                    }
                }
            }
        }

        /// <summary>Hombro con hombro: pierde el equilibrio el más débil (con algo de azar) y puede soltar el balón.</summary>
        private void Collide(MatchPlayer a, MatchPlayer b, float strengthA, float strengthB)
        {
            PlayerConfig c = Config.Player;
            float advantageA = c.StumbleStrengthWeight * (strengthA - strengthB) + _rng.NextGaussian(0f, 0.5f);
            MatchPlayer loser = advantageA >= 0f ? b : a;
            loser.StumbleTimer = c.StumbleDuration;
            loser.Speed *= c.StumbleSpeedFactor;
            if (Owner == loser && _rng.NextFloat() < c.StumbleLoseBall)
            {
                // El balón se le escapa hacia delante (no es falta: fue un choque legal).
                Owner = null;
                Vec2 dir = Vec2.FromAngle(loser.Heading);
                Ball.Vel = new Vec3(dir.X * c.HeavyTouchSpeed, dir.Y * c.HeavyTouchSpeed, 0f);
                loser.KickTimer = c.StumbleDuration;
            }
        }

        private void EndHalf()
        {
            _halfTimeBreak = Half == 1; // en el descanso los cambios no gastan ventana
            if (_halfTimeBreak)
            {
                AiTeamTalks(); // la persona da la suya desde la pausa mientras dura el descanso
            }
            AddEvent(Half == 1 ? MatchEventType.HalfTime : MatchEventType.FullTime, -1, -1, Vec2.Zero);
            if (Half == 2)
            {
                Phase = MatchPhase.Finished;
                Owner = null;
                return;
            }

            Half = 2;
            HalfElapsed = 0f;
            Teams[0].AttackDirection = -1f;
            Teams[1].AttackDirection = 1f;
            PlaceForKickoff(1);
        }
    }
}
