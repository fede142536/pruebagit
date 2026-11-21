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
            Ball.PlaceAt(restart.Position);
            _prevBallPos = Ball.Pos;
            Phase = MatchPhase.SetPiece;
            _phaseTimer = restart.Kind == RestartKind.Penalty ? Config.Rules.RestartDelay * PenaltyDelayFactor : Config.Rules.RestartDelay;
            _takerTimeout = Config.Rules.TakerTimeout;

            for (int i = 0; i < Players.Length; i++)
            {
                Players[i].WantsTackle = false;
                Players[i].Diving = false;
                Players[i].MarkTarget = null;
            }

            _taker = ChooseTaker(restart);
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

                ExecuteRestart();
            }
        }

        private void ExecuteRestart()
        {
            Phase = MatchPhase.Playing;
            _planTimer = 0f;
            MatchPlayer t = _taker;
            TeamState team = Teams[t.Team];
            Vec2 at = _restart.Position;
            t.ActionTimer = 0f;
            t.SpeedFactor = 0f;

            bool exempt = _restart.Kind != RestartKind.FreeKick && _restart.Kind != RestartKind.Penalty;
            _offsideExempt = exempt;
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
