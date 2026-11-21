using System;
using Football.Core.Common;

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
        /// <summary>Los 22 jugadores: índice = equipo × 11 + puesto.</summary>
        public readonly MatchPlayer[] Players = new MatchPlayer[22];
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
        private readonly float[] _scratchX = new float[11];

        private Restart _restart;
        private MatchPlayer _taker;
        private float _phaseTimer;
        private float _takerTimeout;
        private int _kickoffTeam;
        private Vec3 _prevBallPos;
        private float _planTimer;

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
            Pitch = new Pitch(config.Pitch);
            _dt = config.FixedDeltaTime();
            Teams[0] = new TeamState(0, 1f, home, config.Difficulties[homeDifficulty], config.Player, Players);
            Teams[1] = new TeamState(1, -1f, away, config.Difficulties[awayDifficulty], config.Player, Players);

            for (int i = 0; i < Players.Length; i++)
            {
                Players[i].DecisionTimer = (i % 11) * 0.011f;
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
                if (HalfElapsed >= Config.Rules.HalfDuration)
                {
                    EndHalf();
                }
            }

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
            }

            h = Mix(h, Teams[0].Stats.Goals);
            h = Mix(h, Teams[1].Stats.Goals);
            return Mix(h, (int)TickCount);
        }

        private static ulong Mix(ulong h, int value)
        {
            h ^= (uint)value;
            return h * 1099511628211UL;
        }

        private void AddEvent(MatchEventType type, int team, int playerIndex, Vec2 at)
        {
            Events.Add(new MatchEvent { Type = type, Team = team, PlayerIndex = playerIndex, Minute = Minute, X = at.X, Y = at.Y });
        }

        private void TickPlaying(float dt)
        {
            TickTimers(dt);
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
                }

                p.Pos = Pitch.Clamp(p.Pos, margin);
                FaceTheBall(p, dt);

                if (p.ActionDisplayTimer <= 0f)
                {
                    float relative = p.Speed / p.TopSpeed;
                    p.Action = p.Speed < 0.5f ? PlayerAction.Idle : (relative > 0.85f ? PlayerAction.Sprint : PlayerAction.Run);
                }
            }

            Separate();
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
                    float overlap = (min - dist) * 0.5f;
                    a.Pos -= push * overlap;
                    b.Pos += push * overlap;
                }
            }
        }

        private void EndHalf()
        {
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
