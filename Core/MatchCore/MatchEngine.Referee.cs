using System;
using Football.Core.Common;
using Football.Core.Domain;

namespace Football.Core.MatchCore
{
    public sealed partial class MatchEngine
    {
        private const float PenaltyDelayFactor = 1.8f;
        private const float KickoffOwnHalfLimit = 1f;
        private const float KickoffCircleClearance = 0.5f;
        private const float TakerOffset = 0.8f;
        private const float ThrowInMaxSpeed = 14f;
        private const float ThrowInMaxDistance = 25f;
        private const float KickoffMaxDistance = 20f;
        private const float GoalKickShortDistance = 22f;
        private const float GoalKickLongDistance = 55f;
        private const float CornerTargetDepth = 9f;
        private const float CornerSpin = 25f;
        private const float DirectFreeKickMinDistance = 16f;
        private const float FreeKickCrossDistance = 45f;
        private const float PenaltyPower = 0.9f;
        private const float FreeKickPower = 0.8f;
        private const float OffsideFreeKickClamp = 0f;

        private void PlaceForKickoff(int kickingTeam)
        {
            _ballTeam = kickingTeam;
            Teams[0].LastPasser = Teams[0].LastReceiver = Teams[1].LastPasser = Teams[1].LastReceiver = null;
            Ball.PlaceAt(Vec2.Zero);
            _prevBallPos = Ball.Pos;
            Owner = null;
            _passActive = false;
            _offsideActive = false;
            _beatenKeeper = null;
            _shotLive = false;
            _saveRolled = false;

            float circle = Config.Pitch.CenterCircleRadius;
            for (int t = 0; t < 2; t++)
            {
                TeamState team = Teams[t];
                float dir = team.AttackDirection;
                for (int s = 0; s < team.Players.Length; s++)
                {
                    MatchPlayer p = team.Players[s];
                    if (p.SentOff)
                    {
                        continue;
                    }

                    FormationSlot slot = team.Formation.Slots[s];
                    var a = new Vec2(dir * MathUtil.Lerp(-Pitch.HalfLength, Pitch.HalfLength, slot.X), slot.Y * dir * Pitch.HalfWidth * 0.92f);
                    float limit = (t != kickingTeam && MathF.Abs(a.Y) < circle) ? -(circle + KickoffCircleClearance) : -KickoffOwnHalfLimit;
                    if (dir * a.X > limit)
                    {
                        a.X = dir * limit;
                    }

                    p.Pos = a;
                    p.Heading = dir > 0f ? 0f : MathF.PI;
                    p.Speed = 0f;
                    p.Target = a;
                    p.SpeedFactor = 0f;
                    p.Diving = false;
                    p.WantsTackle = false;
                    p.MarkTarget = null;
                    p.ActionTimer = 0f;
                    p.KickTimer = 0f;
                    p.Action = PlayerAction.Idle;
                }
            }

            TeamState kicking = Teams[kickingTeam];
            _taker = KickoffTaker(kicking);
            _taker.Pos = new Vec2(-kicking.AttackDirection * TakerOffset, 0f);
            _taker.Target = _taker.Pos;

            _restart = new Restart(RestartKind.KickOff, kickingTeam, Vec2.Zero);
            Phase = MatchPhase.SetPiece;
            _phaseTimer = Config.Rules.KickoffDelay;
            _takerTimeout = Config.Rules.TakerTimeout;
            _planTimer = 0f;
            AddEvent(MatchEventType.KickOff, kickingTeam, -1, Vec2.Zero);
        }

        private void StartSetPiece(Restart restart)
        {
            Owner = null;
            if (_passActive)
            {
                Teams[_passTeam].Stats.PassesLost++;
            }

            _passActive = false;
            _offsideActive = false;
            _beatenKeeper = null;
            _shotLive = false;
            _saveRolled = false;
            _restart = restart;
            _ballTeam = restart.Team; // recibir un saque propio no es una recuperación
            Teams[0].LastPasser = Teams[0].LastReceiver = Teams[1].LastPasser = Teams[1].LastReceiver = null;
            Ball.PlaceAt(restart.Position);
            _prevBallPos = Ball.Pos;
            Phase = MatchPhase.SetPiece;
            _phaseTimer = RestartDelayFor(restart.Kind)
                          * (Teams[restart.Team].Shout == TouchlineShout.WasteTime ? Config.Coach.WasteTimeDelayFactor : 1f);
            _takerTimeout = Config.Rules.TakerTimeout;
            _humanRestartTimer = 0f;

            for (int i = 0; i < Players.Length; i++)
            {
                Players[i].WantsTackle = false;
                Players[i].Diving = false;
                Players[i].MarkTarget = null;
            }

            _taker = ChooseTaker(restart);
            PickThrowSupport(restart);
            TeamState takingTeam = Teams[restart.Team];
            if (takingTeam.IsHuman && _taker != null)
            {
                takingTeam.Controlled = _taker; // la persona controla al que saca (se ve la flecha sobre él)
                takingTeam.KeeperLock = false;
            }

            MatchEventType type = MatchEventType.FreeKick;
            switch (restart.Kind)
            {
                case RestartKind.ThrowIn: type = MatchEventType.ThrowIn; break;
                case RestartKind.GoalKick: type = MatchEventType.GoalKick; break;
                case RestartKind.Corner: type = MatchEventType.Corner; Teams[restart.Team].Stats.Corners++; break;
                case RestartKind.Penalty: type = MatchEventType.Penalty; break;
            }

            AddEvent(type, restart.Team, _taker != null ? _taker.Index : -1, restart.Position);
            UpdateSetPieceTargets();
            _planTimer = Config.Ai.DecisionInterval;
        }

        /// <summary>Saca del medio el último jugador de campo que no esté expulsado (el arquero solo si no queda nadie).</summary>
        private static MatchPlayer KickoffTaker(TeamState team)
        {
            for (int i = team.Players.Length - 1; i > 0; i--)
            {
                if (!team.Players[i].SentOff)
                {
                    return team.Players[i];
                }
            }

            return team.Players[0];
        }

        private MatchPlayer ChooseTaker(Restart r)
        {
            TeamState team = Teams[r.Team];
            if (r.Kind == RestartKind.GoalKick)
            {
                return team.Players[0];
            }

            Vec2 goal = Pitch.GoalCenter(team.AttackDirection);
            bool nearGoal = Vec2.Distance(r.Position, goal) < Config.Rules.FreeKickDirectMaxDistance;
            MatchPlayer best = null;
            float bestScore = float.MinValue;
            for (int i = 1; i < team.Players.Length; i++)
            {
                MatchPlayer p = team.Players[i];
                if (p.SentOff)
                {
                    continue;
                }

                float distance = Vec2.Distance(p.Pos, r.Position);
                float score;
                switch (r.Kind)
                {
                    case RestartKind.Corner:
                        score = p.Skill(AttributeId.Crossing) - 0.001f * distance;
                        break;
                    case RestartKind.Penalty:
                        score = p.Skill(AttributeId.Finishing) + p.Skill(AttributeId.Composure);
                        break;
                    case RestartKind.FreeKick when nearGoal:
                        score = p.Skill(AttributeId.Shooting) - 0.001f * distance;
                        break;
                    default:
                        score = -distance;
                        break;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }

            return best ?? team.Players[0];
        }

        private void TickSetPiece(float dt)
        {
            TickTimers(dt);
            _planTimer -= dt;
            if (_planTimer <= 0f)
            {
                _planTimer = Config.Ai.DecisionInterval;
                UpdateSetPieceTargets();
            }

            MovePlayers(dt);

            _phaseTimer -= dt;
            if (_phaseTimer > 0f)
            {
                return;
            }

            _takerTimeout -= dt;
            float distance = Vec2.Distance(_taker.Pos, _restart.Position);
            if (distance <= Config.Rules.TakerReach || _takerTimeout <= 0f)
            {
                if (distance > Config.Rules.TakerReach)
                {
                    _taker.Pos = _restart.Position - new Vec2(Teams[_taker.Team].AttackDirection * TakerOffset, 0f);
                }

                TeamState team = Teams[_taker.Team];
                if (HumanTakes(team))
                {
                    // Saca la persona: apunta con el joystick y elige pase, centro o tiro. Si no hace nada, saca la IA.
                    _humanRestartTimer += dt;
                    MatchInput input = team.Input;
                    if (input.Pass || input.Through || input.Cross || input.Shoot || input.Clear)
                    {
                        ExecuteHumanRestart(team, input);
                        ConsumeKick(team);
                        return;
                    }

                    if (_humanRestartTimer < Config.Rules.HumanRestartTimeout)
                    {
                        return;
                    }
                }

                ExecuteRestart();
            }
        }

        private float _humanRestartTimer;

        /// <summary>El saque está listo y espera a que la persona elija (para mostrar la ayuda en pantalla).</summary>
        public bool AwaitingHumanRestart =>
            Phase == MatchPhase.SetPiece && _taker != null && HumanTakes(Teams[_taker.Team]) && _phaseTimer <= 0f
            && Vec2.Distance(_taker.Pos, _restart.Position) <= Config.Rules.TakerReach;

        private bool HumanTakes(TeamState team) => team.IsHuman && _restart.Kind != RestartKind.Penalty;

        private float RestartDelayFor(RestartKind kind)
        {
            RulesConfig r = Config.Rules;
            switch (kind)
            {
                case RestartKind.ThrowIn: return r.ThrowInDelay;
                case RestartKind.GoalKick: return r.GoalKickDelay;
                case RestartKind.Corner: return r.CornerDelay;
                case RestartKind.Penalty: return r.RestartDelay * PenaltyDelayFactor;
                default: return r.RestartDelay;
            }
        }

        private bool _restartKick;
        private const float NearPostDepth = 2.5f;

        /// <summary>Quién saca ahora (durante un saque), o null.</summary>
        public MatchPlayer Taker => Phase == MatchPhase.SetPiece ? _taker : null;

        private void BeginRestartKick(MatchPlayer t)
        {
            _restartKick = true;
            Phase = MatchPhase.Playing;
            _planTimer = 0f;
            t.ActionTimer = 0f;
            t.SpeedFactor = 0f;
            _offsideExempt = _restart.Kind != RestartKind.FreeKick && _restart.Kind != RestartKind.Penalty;
        }

        /// <summary>
        /// Dirección en la que apunta el saque de la persona: la del joystick, limitada a las direcciones válidas (hacia
        /// adelante o al costado en el saque del medio, hacia adentro en laterales y córners); sin joystick, una por defecto.
        /// </summary>
        /// <param name="team">Equipo de la persona.</param>
        /// <param name="from">Punto del saque.</param>
        /// <param name="direction">Dirección unitaria.</param>
        /// <returns>Falso si no hay un saque de la persona esperando.</returns>
        public bool TryGetRestartAim(int team, out Vec2 from, out Vec2 direction)
        {
            from = _restart.Position;
            direction = Vec2.Zero;
            if (!AwaitingHumanRestart || _taker.Team != team)
            {
                return false;
            }

            direction = RestartAim(Teams[team].Input.Move);
            return true;
        }

        private const float AimDeadZone = 0.2f;

        private Vec2 RestartAim(Vec2 stick)
        {
            RulesConfig r = Config.Rules;
            Vec2 at = _restart.Position;
            TeamState team = Teams[_restart.Team];
            var forward = new Vec2(team.AttackDirection, 0f);
            Vec2 center;
            float limit;
            switch (_restart.Kind)
            {
                case RestartKind.KickOff:
                    center = forward;
                    limit = r.KickoffAimDeg;
                    break;
                case RestartKind.ThrowIn:
                    center = new Vec2(0f, at.Y > 0f ? -1f : 1f);
                    limit = r.ThrowInAimDeg;
                    break;
                case RestartKind.Corner:
                    center = new Vec2(at.X > 0f ? -1f : 1f, at.Y > 0f ? -1f : 1f).Normalized;
                    limit = r.CornerAimDeg;
                    break;
                case RestartKind.GoalKick:
                    center = forward;
                    limit = r.GoalKickAimDeg;
                    break;
                default:
                    center = (Pitch.GoalCenter(team.AttackDirection) - at).Normalized;
                    limit = 180f;
                    break;
            }

            if (stick.Length < AimDeadZone)
            {
                // Sin joystick: córner al punto penal; el resto, al centro del cono.
                return _restart.Kind == RestartKind.Corner ? (Pitch.PenaltySpot(team.AttackDirection) - at).Normalized : center;
            }

            float diff = MathUtil.AngleDiff(stick.Angle, center.Angle);
            float max = limit * MathUtil.Deg2Rad;
            return Vec2.FromAngle(center.Angle + MathUtil.Clamp(diff, -max, max));
        }

        /// <summary>
        /// Saque de la persona, en la dirección apuntada y con la fuerza cargada: PASE raso (asistido hacia el compañero que
        /// está en esa dirección), CENTRO elevado a una distancia según la carga, TIRO al arco en tiros libres o pelotazo
        /// largo en el resto. Nunca hay un pase automático: solo se ejecuta al soltar un botón.
        /// </summary>
        private void ExecuteHumanRestart(TeamState team, MatchInput input)
        {
            MatchPlayer t = _taker;
            RulesConfig r = Config.Rules;
            Vec2 aim = RestartAim(input.Move);
            input.Move = aim; // la asistencia de pase busca compañeros en la dirección permitida
            BeginRestartKick(t);
            Vec2 at = _restart.Position;
            RestartKind kind = _restart.Kind;
            float passScale = input.PassPower > 0f
                ? MathUtil.Lerp(Config.Player.HumanPassPowerMin, Config.Player.HumanPassPowerMax, MathUtil.Clamp01(input.PassPower))
                : 1f;

            if (input.Shoot && kind == RestartKind.FreeKick)
            {
                KickShot(t, input.ShootPower > 0f ? input.ShootPower : FreeKickPower, true, HumanAimY(t, team, input));
            }
            else if (input.Shoot || input.Clear)
            {
                float power = input.Shoot ? MathUtil.Clamp01(input.ShootPower) : 1f;
                Vec2 point = Pitch.Clamp(at + aim * MathUtil.Lerp(r.SetPieceLongMin, r.SetPieceLongMax, power), -SafeMarginToLine);
                if (kind == RestartKind.ThrowIn)
                {
                    KickPass(t, at + aim * ThrowInMaxDistance, null, false, true, ThrowInMaxSpeed);
                }
                else
                {
                    KickCross(t, point, true, 0f, KickKind.Clearance);
                }
            }
            else if (input.Cross)
            {
                float power = input.CrossPower > 0f ? MathUtil.Clamp01(input.CrossPower) : 0.5f;
                float distance = MathUtil.Lerp(r.SetPieceCrossMin, r.SetPieceCrossMax, power);
                if (kind == RestartKind.ThrowIn)
                {
                    KickPass(t, at + aim * MathF.Min(distance, ThrowInMaxDistance), null, false, true, ThrowInMaxSpeed);
                }
                else
                {
                    float spin = kind == RestartKind.Corner ? (at.Y > 0f ? -CornerSpin : CornerSpin) : 0f;
                    KickCross(t, Pitch.Clamp(at + aim * distance, -SafeMarginToLine), true, spin);
                }
            }
            else
            {
                ChooseHumanTarget(t, team, input, input.Through, 0f, out Vec2 point, out MatchPlayer target);
                if (kind == RestartKind.ThrowIn)
                {
                    Vec2 toPoint = point - at;
                    if (toPoint.Length > ThrowInMaxDistance)
                    {
                        point = at + toPoint.Normalized * ThrowInMaxDistance;
                        target = null;
                    }

                    KickPass(t, point, target, false, true, ThrowInMaxSpeed, passScale);
                }
                else
                {
                    KickPass(t, point, target, kind != RestartKind.KickOff, true, float.MaxValue, passScale);
                }
            }

            _offsideExempt = false;
        }

        private void ExecuteRestart()
        {
            MatchPlayer t = _taker;
            TeamState team = Teams[t.Team];
            Vec2 at = _restart.Position;
            BeginRestartKick(t);
            switch (_restart.Kind)
            {
                case RestartKind.KickOff:
                    RestartPass(t, KickoffMaxDistance, float.MaxValue);
                    break;
                case RestartKind.ThrowIn:
                    RestartPass(t, ThrowInMaxDistance, ThrowInMaxSpeed);
                    break;
                case RestartKind.GoalKick:
                    RestartPass(t, _rng.NextFloat() < team.Style.Directness ? GoalKickLongDistance : GoalKickShortDistance, float.MaxValue);
                    break;
                case RestartKind.Corner:
                {
                    float goalX = team.AttackDirection * (Pitch.HalfLength - CornerTargetDepth);
                    var target = new Vec2(goalX, _rng.NextGaussian(0f, Config.Ai.CrossTargetSpread));
                    MatchPlayer shortOption = team.Corners == CornerRoutine.Short ? ShortCornerPlayer(team) : null;
                    if (shortOption != null)
                    {
                        // Córner corto: pase al compañero que se acercó.
                        KickPass(t, shortOption.Pos, shortOption, false, false, Config.Ai.PassMaxSpeed);
                        break;
                    }

                    if (team.Corners == CornerRoutine.NearPost)
                    {
                        float postSide = at.Y >= 0f ? 1f : -1f;
                        target = new Vec2(team.AttackDirection * (Pitch.HalfLength - NearPostDepth), postSide * Pitch.GoalHalfWidth);
                    }
                    else if (team.Corners == CornerRoutine.PenaltySpot)
                    {
                        target = new Vec2(team.AttackDirection * (Pitch.HalfLength - Config.Pitch.PenaltySpotDistance), 0f);
                    }

                    KickCross(t, target, false, at.Y > 0f ? -CornerSpin : CornerSpin);
                    break;
                }

                case RestartKind.FreeKick:
                {
                    float dg = Vec2.Distance(at, Pitch.GoalCenter(team.AttackDirection));
                    if (dg <= Config.Rules.FreeKickDirectMaxDistance && dg >= DirectFreeKickMinDistance)
                    {
                        KickShot(t, FreeKickPower, false);
                    }
                    else if (dg < FreeKickCrossDistance)
                    {
                        RestartPass(t, GoalKickLongDistance, float.MaxValue);
                    }
                    else
                    {
                        RestartPass(t, GoalKickShortDistance, float.MaxValue);
                    }

                    break;
                }

                case RestartKind.Penalty:
                    KickShot(t, PenaltyPower, false);
                    break;
            }

            _offsideExempt = false;
        }

        private void CallOffside(MatchPlayer p)
        {
            Teams[p.Team].Stats.Offsides++;
            AddEvent(MatchEventType.Offside, p.Team, p.Index, p.Pos);
            StartSetPiece(new Restart(RestartKind.FreeKick, 1 - p.Team, Pitch.Clamp(p.Pos, OffsideFreeKickClamp)));
        }
    }
}
