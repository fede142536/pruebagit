using System;
using Football.Core.Common;
using Football.Core.Domain;

namespace Football.Core.MatchCore
{
    public sealed partial class MatchEngine
    {
        private const float DeflectionNoise = 0.6f;
        private const float DeflectionSpeedFactor = 0.45f;
        private const float DeflectionLift = 2f;
        private const float ParryLateralSpeed = 5f;
        private const float ParryRetainFactor = 0.35f;
        private const float ParryLift = 3f;
        private const float ParryOverBarChance = 0.4f;
        private const float ParryOverBarForward = 3f;
        private const float ParryOverBarLift = 8f;
        private const float SaveMinBallSpeed = 5f;
        private const float SaveReachPenalty = 0.25f;
        private const float MinSaveChance = 0.05f;
        private const float MaxSaveChance = 0.95f;
        private const float KeeperHandReach = 0.4f;
        private const float CatchHeight = 2.6f;
        private const float TackleKnockSpeed = 4f;
        private const float TackleKeepChance = 0.55f;
        private const float DispossessedLockout = 0.4f;
        private const float HeavyTouchLockout = 0.3f;
        private const float BlockLockout = 0.25f;
        private const float ActionDisplay = 0.3f;
        private const float NetDepth = 2f;

        private bool _passActive;
        private int _passTeam;
        private MatchPlayer _lastKicker;
        private MatchPlayer _passTarget;
        private readonly bool[] _offsideFlag = new bool[22];
        private bool _shotLive;
        private bool _saveRolled;
        private long _shotStartTick;

        private void UpdateBall(float dt)
        {
            _prevBallPos = Ball.Pos;
            if (Owner != null)
            {
                CarryBall(dt);
                return;
            }

            BallHit hit = BallPhysics.Step(Ball, dt, Config.Ball, Pitch);
            if (hit != BallHit.None)
            {
                AddEvent(MatchEventType.Woodwork, LastToucher != null ? LastToucher.Team : -1,
                    LastToucher != null ? LastToucher.Index : -1, Ball.Pos.Horizontal);
            }

            UpdateKeepers();
            if (Owner == null && Phase == MatchPhase.Playing)
            {
                TryPickup();
            }
        }

        private void CarryBall(float dt)
        {
            MatchPlayer p = Owner;
            PlayerConfig c = Config.Player;
            Vec2 front = p.Pos + Vec2.FromAngle(p.Heading) * c.CarryDistance;
            Vec2 velocity = p.Velocity;
            Ball.Pos = new Vec3(front.X, front.Y, 0f);
            Ball.Vel = new Vec3(velocity.X, velocity.Y, 0f);
            Ball.Spin = 0f;

            // Mal toque: cuanto más rápido corre y menos técnica tiene, más probable.
            float relative = p.Speed / p.TopSpeed;
            float skill = 0.5f * (p.Skill(AttributeId.Dribbling) + p.Skill(AttributeId.FirstTouch));
            float rate = c.HeavyTouchRate * relative * relative * (1f - 0.7f * skill);
            if (_rng.NextFloat() < rate * dt)
            {
                Vec2 push = Vec2.FromAngle(p.Heading) * c.HeavyTouchSpeed;
                Ball.Vel = new Vec3(velocity.X + push.X, velocity.Y + push.Y, 0f);
                Owner = null;
                p.KickTimer = HeavyTouchLockout;
            }
        }

        private void TryPickup()
        {
            PlayerConfig c = Config.Player;
            Vec2 from = _prevBallPos.Horizontal;
            Vec2 to = Ball.Pos.Horizontal;
            if (Ball.Pos.Z > c.ControlMaxHeight)
            {
                return;
            }

            float ballSpeed = Ball.Vel.Length;
            MatchPlayer best = null;
            float bestScore = float.MaxValue;
            MatchPlayer blocker = null;
            float blockerDistance = float.MaxValue;

            for (int i = 0; i < Players.Length; i++)
            {
                MatchPlayer p = Players[i];
                if (p.SentOff || p.KickTimer > 0f || p.SlideTimer > 0f || p.RecoverTimer > 0f)
                {
                    continue;
                }

                float distance = MathUtil.DistancePointSegment(p.Pos, from, to, out _);
                float firstTouch = p.Skill(AttributeId.FirstTouch);
                float radius = p.IsKeeper ? c.ControlRadius + KeeperHandReach : c.ControlRadius;
                if (distance > radius)
                {
                    continue;
                }

                float maxControl = c.ControlMaxBallSpeed * (1f + c.ControlSpeedSpread * (2f * firstTouch - 1f));
                if (ballSpeed <= maxControl)
                {
                    float score = distance - 0.3f * firstTouch;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = p;
                    }
                }
                else if (distance <= c.BlockRadius && distance < blockerDistance)
                {
                    blockerDistance = distance;
                    blocker = p;
                }
            }

            if (best != null)
            {
                GainPossession(best);
            }
            else if (blocker != null)
            {
                Deflect(blocker);
            }
        }

        private void GainPossession(MatchPlayer p)
        {
            if (OnTouch(p))
            {
                return;
            }

            Owner = p;
            Ball.Vel = Vec3.Zero;
            Ball.Spin = 0f;
            p.ActionTimer = Config.Player.ControlDelay;
            p.OwnerTimer = 0f;
            p.Target = p.Pos + Vec2.FromAngle(p.Heading) * Config.Ai.DribbleLookahead;
            p.SpeedFactor = Config.Player.JogFraction;
        }

        private void Deflect(MatchPlayer p)
        {
            if (OnTouch(p))
            {
                return;
            }

            float noise = _rng.NextGaussian(0f, DeflectionNoise);
            Vec2 horizontal = Ball.Vel.Horizontal.Rotated(noise) * DeflectionSpeedFactor;
            Ball.Vel = new Vec3(horizontal.X, horizontal.Y, DeflectionLift * _rng.NextFloat());
            p.KickTimer = BlockLockout;
        }

        /// <summary>Registra un toque de balón. Devuelve true si provoca una interrupción (fuera de juego).</summary>
        private bool OnTouch(MatchPlayer p)
        {
            if (_passActive)
            {
                _passActive = false;
                if (p.Team == _passTeam && p != _lastKicker)
                {
                    Teams[_passTeam].Stats.PassesCompleted++;
                    if (_offsideFlag[p.Index])
                    {
                        CallOffside(p);
                        return true;
                    }
                }
                else if (p.Team != _passTeam)
                {
                    Teams[_passTeam].Stats.PassesIntercepted++;
                }
            }

            LastToucher = p;
            _saveRolled = false;
            _shotLive = false;
            return false;
        }

        private void UpdateKeepers()
        {
            PlayerConfig c = Config.Player;
            for (int t = 0; t < 2; t++)
            {
                MatchPlayer keeper = Teams[t].Players[0];
                if (keeper.SentOff || _saveRolled || Owner != null || LastToucher == null || LastToucher.Team == t)
                {
                    continue;
                }

                float speed = Ball.Vel.Length;
                if (speed < SaveMinBallSpeed)
                {
                    continue;
                }

                var centre = new Vec3(keeper.Pos.X, keeper.Pos.Y, c.KeeperReachHeight);
                float skill = (keeper.Skill(AttributeId.Reflexes) + keeper.Skill(AttributeId.Handling)
                               + keeper.Skill(AttributeId.GoalkeeperPositioning)) / 3f;
                float reach = c.KeeperBaseReach + c.KeeperExtraReach * keeper.Skill(AttributeId.Reflexes);
                float distance = Vec3.Distance(Ball.Pos, centre);
                if (distance <= reach)
                {
                    _saveRolled = true;
                    AttemptSave(keeper, speed, distance / reach, skill);
                }
            }
        }

        private void AttemptSave(MatchPlayer keeper, float speed, float edgeFraction, float skill)
        {
            PlayerConfig c = Config.Player;
            float chance = c.KeeperSaveBase + c.KeeperSaveSkillSpread * (skill - 0.5f) * 2f
                           - c.KeeperSaveSpeedPenalty * MathF.Max(0f, speed - c.KeeperSaveSpeedRef)
                           - SaveReachPenalty * edgeFraction;
            chance = MathUtil.Clamp(chance, MinSaveChance, MaxSaveChance);
            if (_rng.NextFloat() >= chance)
            {
                return;
            }

            if (_shotLive)
            {
                Teams[keeper.Team].Stats.Saves++;
                Teams[1 - keeper.Team].Stats.ShotsOnTarget++;
            }

            AddEvent(MatchEventType.Save, keeper.Team, keeper.Index, Ball.Pos.Horizontal);
            keeper.Action = PlayerAction.Dive;
            keeper.ActionDisplayTimer = ActionDisplay;

            float handling = keeper.Skill(AttributeId.Handling);
            if (speed <= c.KeeperCatchMaxSpeed * (0.8f + 0.4f * handling) && Ball.Pos.Z < CatchHeight)
            {
                GainPossession(keeper);
                return;
            }

            float end = Teams[keeper.Team].DefendedEnd;
            if (_rng.NextFloat() < ParryOverBarChance)
            {
                // Desvía por encima del larguero: el balón sale por la línea de fondo y es córner.
                Ball.Vel = new Vec3(end * ParryOverBarForward, 0f, ParryOverBarLift);
                Ball.Spin = 0f;
                OnTouch(keeper);
                keeper.KickTimer = BlockLockout;
                return;
            }

            float lateral = _rng.NextGaussian(0f, ParryLateralSpeed);
            Ball.Vel = new Vec3(-end * speed * ParryRetainFactor, lateral, ParryLift * _rng.NextFloat());
            Ball.Spin = 0f;
            OnTouch(keeper);
            keeper.KickTimer = BlockLockout;
        }

        private void ResolveTackles()
        {
            MatchPlayer owner = Owner;
            if (owner == null)
            {
                return;
            }

            RulesConfig rules = Config.Rules;
            for (int i = 0; i < Players.Length; i++)
            {
                MatchPlayer p = Players[i];
                if (p.Team == owner.Team || p.SentOff || !p.WantsTackle || p.TackleTimer > 0f)
                {
                    continue;
                }

                if (Vec2.Distance(p.Pos, owner.Pos) > rules.TackleRange)
                {
                    continue;
                }

                p.TackleTimer = rules.TackleCooldown;
                p.WantsTackle = false;
                p.TackleWindow = 0f;
                p.Action = PlayerAction.Tackle;
                p.ActionDisplayTimer = ActionDisplay;

                float tackling = p.Skill(AttributeId.Tackling);
                float foulChance = Rules.FoulChance(rules, tackling, p.Skill(AttributeId.Aggression));
                float winChance = Rules.TackleWinChance(rules, tackling, owner.Skill(AttributeId.Dribbling));
                TackleOutcome outcome = Rules.ResolveTackle(_rng.NextFloat(), _rng.NextFloat(), foulChance, winChance);

                if (outcome == TackleOutcome.Foul)
                {
                    CommitFoul(p, owner);
                    return;
                }

                if (outcome == TackleOutcome.Won)
                {
                    WinBall(p, owner);
                    return;
                }
            }
        }

        /// <summary>Empieza una barrida en la dirección en la que mira el jugador.</summary>
        private void StartSlide(MatchPlayer p)
        {
            if (p.SlideTimer > 0f || p.RecoverTimer > 0f || Owner == p)
            {
                return;
            }

            p.SlideTimer = Config.Rules.SlideDuration;
            p.SlideResolved = false;
            p.Speed = MathF.Max(p.Speed, Config.Rules.SlideSpeed);
            p.WantsTackle = false;
            p.Action = PlayerAction.Tackle;
            p.ActionDisplayTimer = Config.Rules.SlideDuration + Config.Rules.SlideRecovery;
        }

        private void SlideStep(MatchPlayer p, float dt)
        {
            p.Speed = MathF.Max(0f, p.Speed - Config.Rules.SlideDeceleration * dt);
            p.Pos += Vec2.FromAngle(p.Heading) * (p.Speed * dt);
        }

        /// <summary>Resuelve las barridas en curso: contra el poseedor rival o contra un balón suelto al alcance.</summary>
        private void ResolveSlides()
        {
            RulesConfig rules = Config.Rules;
            for (int i = 0; i < Players.Length; i++)
            {
                MatchPlayer p = Players[i];
                if (p.SentOff || p.SlideTimer <= 0f || p.SlideResolved)
                {
                    continue;
                }

                MatchPlayer owner = Owner;
                if (owner != null && owner.Team != p.Team)
                {
                    if (Vec2.Distance(p.Pos, owner.Pos) > rules.SlideReach)
                    {
                        continue;
                    }

                    p.SlideResolved = true;
                    bool fromBehind = Vec2.Dot(Vec2.FromAngle(p.Heading), Vec2.FromAngle(owner.Heading)) > 0.5f;
                    float foul = Rules.FoulChance(rules, p.Skill(AttributeId.Tackling), p.Skill(AttributeId.Aggression))
                                 * rules.SlideFoulMultiplier * (fromBehind ? rules.BehindFoulMultiplier : 1f);
                    float win = MathUtil.Clamp(Rules.TackleWinChance(rules, p.Skill(AttributeId.Tackling), owner.Skill(AttributeId.Dribbling))
                                               + rules.SlideWinBonus, 0f, 0.95f);
                    TackleOutcome outcome = Rules.ResolveTackle(_rng.NextFloat(), _rng.NextFloat(), foul, win);
                    if (outcome == TackleOutcome.Foul)
                    {
                        CommitFoul(p, owner);
                        return;
                    }

                    if (outcome == TackleOutcome.Won)
                    {
                        Owner = null;
                        owner.KickTimer = DispossessedLockout;
                        KnockBall(p);
                        return;
                    }

                    continue;
                }

                if (owner == null && Ball.Pos.Z < CatchHeight * 0.2f && Vec2.Distance(p.Pos, Ball.Pos.Horizontal) <= rules.SlideReach * 0.8f)
                {
                    p.SlideResolved = true;
                    KnockBall(p);
                    if (Phase != MatchPhase.Playing)
                    {
                        return;
                    }
                }
            }
        }

        /// <summary>El jugador que barre toca el balón y lo manda hacia donde se desliza.</summary>
        private void KnockBall(MatchPlayer p)
        {
            if (OnTouch(p))
            {
                return;
            }

            Vec2 direction = Vec2.FromAngle(p.Heading).Rotated(_rng.NextGaussian(0f, DeflectionNoise * 0.5f));
            float speed = Config.Rules.SlideKnockSpeed;
            Ball.Vel = new Vec3(direction.X * speed, direction.Y * speed, 0f);
            Ball.Pos = new Vec3(Ball.Pos.X, Ball.Pos.Y, 0f);
            p.KickTimer = BlockLockout;
        }

        private void WinBall(MatchPlayer tackler, MatchPlayer victim)
        {
            Owner = null;
            victim.KickTimer = DispossessedLockout;
            if (_rng.NextFloat() < TackleKeepChance)
            {
                GainPossession(tackler);
                return;
            }

            if (OnTouch(tackler))
            {
                return;
            }

            Vec2 away = (victim.Pos - tackler.Pos).Normalized;
            if (away.LengthSquared < MathUtil.Epsilon)
            {
                away = new Vec2(1f, 0f);
            }

            Vec2 direction = away.Rotated(_rng.NextGaussian(0f, DeflectionNoise));
            Ball.Vel = new Vec3(direction.X * TackleKnockSpeed, direction.Y * TackleKnockSpeed, 0f);
            Ball.Pos = new Vec3(Ball.Pos.X, Ball.Pos.Y, 0f);
        }

        private void CommitFoul(MatchPlayer fouler, MatchPlayer victim)
        {
            TeamState team = Teams[fouler.Team];
            team.Stats.Fouls++;
            Vec2 at = victim.Pos;
            AddEvent(MatchEventType.Foul, fouler.Team, fouler.Index, at);

            CardResult card = Rules.DecideCard(_rng.NextFloat(), fouler.Skill(AttributeId.Aggression), fouler.Yellows, Config.Rules);
            if (fouler.IsKeeper && (card == CardResult.Red || card == CardResult.SecondYellow))
            {
                card = CardResult.Yellow; // Sin sustitutos en esta fase: el portero nunca se expulsa.
            }

            if (card == CardResult.Yellow || card == CardResult.SecondYellow)
            {
                fouler.Yellows++;
                team.Stats.Yellows++;
                AddEvent(MatchEventType.YellowCard, fouler.Team, fouler.Index, at);
            }

            if (card == CardResult.Red || card == CardResult.SecondYellow)
            {
                SendOff(fouler, at);
            }

            Restart restart = Rules.Foul(Pitch, at, victim.Team, team.DefendedEnd);
            StartSetPiece(restart);
        }

        private void SendOff(MatchPlayer p, Vec2 at)
        {
            p.SentOff = true;
            Teams[p.Team].Stats.Reds++;
            AddEvent(MatchEventType.RedCard, p.Team, p.Index, at);
            if (Teams[p.Team].Controlled == p)
            {
                Teams[p.Team].Controlled = null;
            }
        }

        private void CheckBoundaries()
        {
            BoundaryResult r = Rules.CheckBoundary(Pitch, _prevBallPos, Ball.Pos, Config.Ball.Radius);
            if (r.Kind == BoundaryKind.None)
            {
                return;
            }

            int lastTeam = LastToucher != null ? LastToucher.Team : 0;
            switch (r.Kind)
            {
                case BoundaryKind.Goal:
                    ScoreGoal(r.EndSign, r.Exit);
                    break;
                case BoundaryKind.Touchline:
                    StartSetPiece(Rules.ThrowIn(Pitch, r.Exit, lastTeam, 1f));
                    break;
                case BoundaryKind.GoalLine:
                    int defending = Teams[0].AttackDirection == -r.EndSign ? 0 : 1;
                    StartSetPiece(Rules.GoalLineOut(Pitch, r.EndSign, r.Exit, lastTeam, defending));
                    break;
            }
        }

        private void ScoreGoal(float endSign, Vec2 at)
        {
            int scoring = Teams[0].AttackDirection == endSign ? 0 : 1;
            Teams[scoring].Stats.Goals++;
            if (_shotLive)
            {
                Teams[scoring].Stats.ShotsOnTarget++;
            }

            AddEvent(MatchEventType.Goal, scoring, LastToucher != null ? LastToucher.Index : -1, at);
            Owner = null;
            _passActive = false;
            _shotLive = false;
            Phase = MatchPhase.GoalPause;
            _phaseTimer = Config.Rules.GoalPauseDuration;
            _kickoffTeam = 1 - scoring;
            _taker = null;
            _restart = new Restart(RestartKind.KickOff, _kickoffTeam, Vec2.Zero);
            _planTimer = 0f;
        }

        private void TickGoalPause(float dt)
        {
            TickTimers(dt);
            _planTimer -= dt;
            if (_planTimer <= 0f)
            {
                _planTimer = Config.Ai.DecisionInterval;
                UpdateSetPieceTargets();
            }

            MovePlayers(dt);
            BallPhysics.Step(Ball, dt, Config.Ball, Pitch);
            float limit = Pitch.HalfLength + NetDepth;
            if (MathF.Abs(Ball.Pos.X) > limit)
            {
                Ball.Pos.X = MathF.Sign(Ball.Pos.X) * limit;
                Ball.Vel.X *= -0.1f;
            }

            _phaseTimer -= dt;
            if (_phaseTimer <= 0f)
            {
                PlaceForKickoff(_kickoffTeam);
            }
        }
    }
}
