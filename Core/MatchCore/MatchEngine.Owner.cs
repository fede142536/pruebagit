using System;
using Football.Core.Common;
using Football.Core.Domain;

namespace Football.Core.MatchCore
{
    public sealed partial class MatchEngine
    {
        private enum OptionKind { Shoot, Pass, Cross, Dribble }

        private enum KickKind { Pass, Shot, Cross, Clearance }

        private struct Option
        {
            public OptionKind Kind;
            public MatchPlayer Target;
            public bool Cautious;
            public Vec2 Point;
            public float Utility;
        }

        private const int MaxOptions = 32;
        private const float NoLaneThreat = -10f;
        private const float OffsideAwarenessBase = 0.5f;
        private const float OffsideAwarenessSkill = 0.25f;
        private const float OppositeThreatRange = 1f;
        private const float PressureRange = 4f;
        private const float PassPressureRange = 6f;
        private const float PassLaneMin = 0.3f;
        private const float PassRecvPressureMin = 0.55f;
        private const float SafeMarginToLine = 1.5f;
        private const float DirectnessWeight = 0.10f;
        private const float RiskBase = 1.5f;
        private const float ShotPowerBase = 0.75f;
        private const float ShotPowerRange = 0.25f;
        private const float ShotMinHeight = 0.3f;
        private const float ShotMaxHeight = 1.9f;
        private const float ShotPressureWeight = 0.5f;
        private const float ShotCurl = 0.25f;
        private const float GoalAngleReference = 0.64f;
        private const float ClearanceDistance = 35f;
        private const float ClearanceSideDistance = 10f;
        private const float CrossErrorSkill = 0.6f;
        private const float CrossSpeedError = 0.06f;
        private const float CrossSpin = 15f;
        private const float ThrowDirectionFallback = 15f;
        private const float DribbleSprintFree = 0.5f;
        private const float DribbleAngleStep = 35f;
        private const float KickDisplay = 0.3f;
        private const float BoxTeammatesForCross = 2f;

        private readonly Option[] _options = new Option[MaxOptions];
        private int _optionCount;
        private bool _offsideExempt;

        private float NearestOpponentDistance(Vec2 pos, int team)
        {
            TeamState opponents = Teams[1 - team];
            float best = float.MaxValue;
            for (int i = 0; i < opponents.Players.Length; i++)
            {
                MatchPlayer o = opponents.Players[i];
                if (o.SentOff)
                {
                    continue;
                }

                float d = Vec2.Distance(o.Pos, pos);
                if (d < best)
                {
                    best = d;
                }
            }

            return best;
        }

        /// <summary>
        /// Margen de tiempo del balón sobre el rival que antes llegaría a cortar el pase: negativo si el balón pasa antes
        /// (pase seguro), positivo si un rival llega antes (pase cortado). Si nadie puede acercarse, devuelve un valor muy negativo.
        /// </summary>
        private float PassLaneSlack(Vec2 origin, Vec2 point, float distance, int team)
        {
            AiConfig ai = Config.Ai;
            float averageBallSpeed = 0.5f * (Ballistics.GroundSpeedForDistance(distance, ai.PassArrivalSpeed, Config.Ball) + ai.PassArrivalSpeed);
            TeamState opponents = Teams[1 - team];
            float slack = NoLaneThreat;
            for (int i = 0; i < opponents.Players.Length; i++)
            {
                MatchPlayer o = opponents.Players[i];
                if (o.SentOff)
                {
                    continue;
                }

                float d = MathUtil.DistancePointSegment(o.Pos, origin, point, out float t);
                if (t <= 0.02f || t >= 0.98f)
                {
                    continue;
                }

                float ballTime = t * distance / averageBallSpeed;
                float opponentTime = ai.InterceptReaction + MathF.Max(0f, d - ai.InterceptReach) / (o.TopSpeed * ai.InterceptRunFactor);
                float margin = ballTime - opponentTime;
                if (margin > slack)
                {
                    slack = margin;
                }
            }

            return slack;
        }

        /// <summary>Distancia mínima de los rivales al segmento entre dos puntos (los extremos no cuentan).</summary>
        private float LaneClearance(Vec2 a, Vec2 b, int team)
        {
            TeamState opponents = Teams[1 - team];
            float best = float.MaxValue;
            for (int i = 0; i < opponents.Players.Length; i++)
            {
                MatchPlayer o = opponents.Players[i];
                if (o.SentOff)
                {
                    continue;
                }

                float d = MathUtil.DistancePointSegment(o.Pos, a, b, out float t);
                if (t > 0.02f && t < 0.98f && d < best)
                {
                    best = d;
                }
            }

            return best;
        }

        /// <summary>Valor de tener el balón en un punto para el equipo que ataca en la dirección dada.</summary>
        private float Threat(float attackDirection, Vec2 pos)
        {
            AiConfig ai = Config.Ai;
            float progress = MathUtil.Clamp01(Pitch.Progress(attackDirection, pos.X));
            float lateral = MathF.Abs(pos.Y) / Pitch.HalfWidth;
            float central = 1f - ai.ThreatCentralityLoss * lateral * lateral;
            return ai.ThreatBase + ai.ThreatScale * MathF.Pow(progress, ai.ThreatExponent) * central;
        }

        private float ExpectedGoal(MatchPlayer p, Vec2 origin, float distance, float opponentDistance)
        {
            AiConfig ai = Config.Ai;
            float dir = Teams[p.Team].AttackDirection;
            float goalX = dir * Pitch.HalfLength;
            float dx = MathF.Max(0.5f, MathF.Abs(goalX - origin.X));
            float angle = MathF.Abs(MathF.Atan2(origin.Y + Pitch.GoalHalfWidth, dx) - MathF.Atan2(origin.Y - Pitch.GoalHalfWidth, dx));
            float angleFactor = MathUtil.Clamp01(angle / GoalAngleReference);
            float finishing = 0.5f * (p.Skill(AttributeId.Finishing) + p.Skill(AttributeId.Shooting));
            float block = 0.5f + 0.5f * MathUtil.Clamp01(LaneClearance(origin, Pitch.GoalCenter(dir), p.Team) / 3f);
            float pressure = 0.7f + 0.3f * MathUtil.Clamp01(opponentDistance / PressureRange);
            return ai.XgScale * MathF.Exp(-distance / ai.XgDecay) * angleFactor * (0.6f + 0.8f * finishing) * block * pressure;
        }

        private float PassSuccess(MatchPlayer passer, Vec2 from, Vec2 to, float laneSlack, float receiverOpponentDistance)
        {
            AiConfig ai = Config.Ai;
            float skill = passer.Skill(AttributeId.Passing);
            float distance = Vec2.Distance(from, to);
            float baseChance = ai.PassBase + ai.PassSkillSpread * skill;
            float ratio = distance / ai.PassDistanceScale;
            float distanceFactor = 1f / (1f + ratio * ratio);
            float lane = PassLaneMin + (1f - PassLaneMin) * MathUtil.Clamp01(-laneSlack / ai.PassLaneSlackRange);
            float receiver = PassRecvPressureMin + (1f - PassRecvPressureMin) * MathUtil.Clamp01(receiverOpponentDistance / PassPressureRange);
            return baseChance * distanceFactor * lane * receiver;
        }

        private void AddPassOptions(MatchPlayer p, Vec2 origin, float maxDistance, float noiseSigma)
        {
            AiConfig ai = Config.Ai;
            TeamState team = Teams[p.Team];
            float dir = team.AttackDirection;
            float currentProgress = Pitch.Progress(dir, origin.X);
            for (int i = 0; i < team.Players.Length && _optionCount < MaxOptions; i++)
            {
                MatchPlayer t = team.Players[i];
                if (t == p || t.SentOff)
                {
                    continue;
                }

                float distance = Vec2.Distance(origin, t.Pos);
                if (distance < ai.PassMinDistance || distance > maxDistance)
                {
                    continue;
                }

                Vec2 point = Pitch.Clamp(t.Pos + t.Velocity * ai.PassLeadTime, -SafeMarginToLine);
                bool aware = _rng.NextFloat() < OffsideAwarenessBase + OffsideAwarenessSkill * p.Skill(AttributeId.Decisions);
                if (!_offsideExempt && aware && Rules.IsOffsidePosition(dir, point.X, origin.X, team.OffsideLine))
                {
                    continue;
                }

                float slack = PassLaneSlack(origin, point, distance, p.Team);
                if (slack > 0f)
                {
                    continue;
                }

                float success = PassSuccess(p, origin, point, slack, NearestOpponentDistance(point, p.Team));
                success = MathF.Pow(success, RiskBase - team.Style.Risk);

                float value = Threat(dir, point);
                float lost = Threat(-dir, point);
                float utility = success * value - (1f - success) * ai.LossWeight * lost;
                float gain = Pitch.Progress(dir, point.X) - currentProgress;
                utility += DirectnessWeight * (team.Style.Directness - 0.5f) * gain;
                utility += _rng.NextGaussian(0f, noiseSigma);

                _options[_optionCount++] = new Option { Kind = OptionKind.Pass, Target = t, Point = point, Utility = utility };
            }
        }

        private int BestOption()
        {
            int best = -1;
            float bestUtility = float.MinValue;
            for (int i = 0; i < _optionCount; i++)
            {
                if (_options[i].Utility > bestUtility)
                {
                    bestUtility = _options[i].Utility;
                    best = i;
                }
            }

            return best;
        }

        private float DecisionNoise(MatchPlayer p) =>
            Config.Ai.DecisionNoise * (1.3f - p.Skill(AttributeId.Decisions)) * Teams[p.Team].Difficulty.DecisionNoiseScale;

        private void OwnerDecide(MatchPlayer p)
        {
            AiConfig ai = Config.Ai;
            TeamState team = Teams[p.Team];
            float dir = team.AttackDirection;
            Vec2 pos = Ball.Pos.Horizontal;
            float opponentDistance = NearestOpponentDistance(pos, p.Team);
            float noise = DecisionNoise(p);
            _optionCount = 0;

            if (Pitch.InPenaltyArea(team.DefendedEnd, pos) && opponentDistance < ai.ClearancePressure)
            {
                KickCross(p, ClearanceTarget(p, pos, dir), false, 0f, KickKind.Clearance);
                return;
            }

            Vec2 goal = Pitch.GoalCenter(dir);
            float goalDistance = Vec2.Distance(pos, goal);
            if (goalDistance <= ai.ShootMaxRange)
            {
                float xg = ExpectedGoal(p, pos, goalDistance, opponentDistance) * (0.75f + 0.5f * team.Style.Risk) * ai.ShotEagerness;
                float utility = xg + (1f - xg) * ai.ShotMissValue + _rng.NextGaussian(0f, noise);
                _options[_optionCount++] = new Option { Kind = OptionKind.Shoot, Point = goal, Utility = utility };
            }

            AddPassOptions(p, pos, ai.PassMaxDistance, noise);
            AddCrossOption(p, pos, noise);
            AddDribbleOptions(p, pos, noise);

            int chosen = BestOption();
            if (chosen < 0)
            {
                return;
            }

            Option o = _options[chosen];
            switch (o.Kind)
            {
                case OptionKind.Shoot:
                    KickShot(p, ShotPowerBase + ShotPowerRange * _rng.NextFloat(), false);
                    break;
                case OptionKind.Pass:
                    KickPass(p, o.Point, o.Target, true, false, float.MaxValue);
                    break;
                case OptionKind.Cross:
                    KickCross(p, o.Point, false, (pos.Y > 0f ? -1f : 1f) * CrossSpin * dir, KickKind.Cross);
                    break;
                default:
                    p.Target = o.Point;
                    p.SpeedFactor = o.Cautious ? Config.Player.JogFraction : 1f;
                    break;
            }
        }

        private Vec2 ClearanceTarget(MatchPlayer p, Vec2 pos, float dir)
        {
            float away = pos.Y > 0f ? -1f : 1f;
            return Pitch.Clamp(new Vec2(pos.X + dir * ClearanceDistance, pos.Y + away * ClearanceSideDistance), -SafeMarginToLine);
        }

        private void AddCrossOption(MatchPlayer p, Vec2 pos, float noiseSigma)
        {
            AiConfig ai = Config.Ai;
            TeamState team = Teams[p.Team];
            float dir = team.AttackDirection;
            bool wide = MathF.Abs(pos.Y) > Pitch.HalfWidth - ai.CrossWideDistance;
            if (!wide || Pitch.Progress(dir, pos.X) < ai.CrossMinProgress || _optionCount >= MaxOptions)
            {
                return;
            }

            int inBox = 0;
            for (int i = 0; i < team.Players.Length; i++)
            {
                MatchPlayer t = team.Players[i];
                if (t != p && !t.SentOff && Pitch.InPenaltyArea(dir, t.Pos))
                {
                    inBox++;
                }
            }

            if (inBox == 0)
            {
                return;
            }

            float skill = p.Skill(AttributeId.Crossing);
            float success = ai.CrossBase + 0.4f * skill * MathF.Min(1f, inBox / BoxTeammatesForCross);
            success = MathF.Pow(success, RiskBase - team.Style.Risk);
            float lost = Threat(-dir, pos);
            float utility = success * ai.CrossBoxValue - (1f - success) * ai.LossWeight * lost + _rng.NextGaussian(0f, noiseSigma);
            var target = new Vec2(dir * (Pitch.HalfLength - 9f), _rng.NextGaussian(0f, ai.CrossTargetSpread));
            _options[_optionCount++] = new Option { Kind = OptionKind.Cross, Point = target, Utility = utility };
        }

        private void AddDribbleOptions(MatchPlayer p, Vec2 pos, float noiseSigma)
        {
            AiConfig ai = Config.Ai;
            TeamState team = Teams[p.Team];
            float dir = team.AttackDirection;
            Vec2 toGoal = (Pitch.GoalCenter(dir) - pos).Normalized;
            float skill = p.Skill(AttributeId.Dribbling);
            float lostHere = Threat(-dir, pos);
            for (int k = -2; k <= 2 && _optionCount < MaxOptions; k++)
            {
                Vec2 d = toGoal.Rotated(k * DribbleAngleStep * MathUtil.Deg2Rad);
                Vec2 end = pos + d * ai.DribbleLookahead;
                Vec2 inside = Pitch.Clamp(end, -SafeMarginToLine);
                if (Vec2.Distance(inside, end) > 0.5f)
                {
                    continue;
                }

                float free = MathF.Min(ai.DribbleSafeDistance, LaneClearance(pos, end, p.Team));
                float success = (ai.DribbleBase + ai.DribbleSkillSpread * skill) * (0.35f + 0.65f * MathUtil.Clamp01(free / ai.DribbleSafeDistance));
                float utility = success * Threat(dir, end) - (1f - success) * ai.LossWeight * lostHere
                                + ai.DribbleBias + _rng.NextGaussian(0f, noiseSigma);
                // Con rivales cerca se conduce al trote para no perder el control.
                bool cautious = free < ai.DribbleSafeDistance * DribbleSprintFree;
                _options[_optionCount++] = new Option { Kind = OptionKind.Dribble, Point = end, Cautious = cautious, Utility = utility };
            }
        }

        private void RestartPass(MatchPlayer p, float maxDistance, float maxSpeed)
        {
            Vec2 origin = Ball.Pos.Horizontal;
            _optionCount = 0;
            AddPassOptions(p, origin, maxDistance, DecisionNoise(p));
            int chosen = BestOption();
            if (chosen >= 0)
            {
                Option o = _options[chosen];
                KickPass(p, o.Point, o.Target, true, false, maxSpeed);
                return;
            }

            // Sin candidatos (todos fuera de rango o en fuera de juego): al compañero más cercano.
            TeamState team = Teams[p.Team];
            MatchPlayer nearest = null;
            float best = float.MaxValue;
            for (int i = 0; i < team.Players.Length; i++)
            {
                MatchPlayer t = team.Players[i];
                float d = Vec2.Distance(origin, t.Pos);
                if (t != p && !t.SentOff && d < best)
                {
                    best = d;
                    nearest = t;
                }
            }

            Vec2 point = nearest != null ? nearest.Pos : origin + new Vec2(team.AttackDirection * ThrowDirectionFallback, 0f);
            KickPass(p, point, nearest, false, false, maxSpeed);
        }

        private void KickPass(MatchPlayer p, Vec2 point, MatchPlayer target, bool allowLob, bool human, float maxSpeed)
        {
            AiConfig ai = Config.Ai;
            Vec2 origin = Ball.Pos.Horizontal;
            Vec2 delta = point - origin;
            float distance = delta.Length;
            float skill = p.Skill(AttributeId.Passing);
            DifficultyProfile diff = Teams[p.Team].Difficulty;
            float pressure = 1f + 0.6f * (1f - MathUtil.Clamp01(NearestOpponentDistance(origin, p.Team) / PressureRange));
            float errorScale = human ? diff.PlayerErrorScale : diff.PassErrorScale;
            float sigma = ai.PassAngleErrorDeg * MathUtil.Deg2Rad * (1f - 0.8f * skill) * (1f + distance / 40f) * pressure * errorScale;
            float angle = delta.Angle + _rng.NextGaussian(0f, sigma);

            float loft = 0f;
            float speed;
            if (allowLob && distance >= ai.LobPassDistance)
            {
                loft = ai.LobLoftDeg * MathUtil.Deg2Rad;
                speed = Ballistics.LobSpeed(distance, loft, Config.Ball);
            }
            else
            {
                speed = MathUtil.Clamp(Ballistics.GroundSpeedForDistance(distance, ai.PassArrivalSpeed, Config.Ball), ai.PassMinSpeed, ai.PassMaxSpeed);
            }

            speed = MathF.Min(speed, maxSpeed);
            speed *= 1f + _rng.NextGaussian(0f, ai.PassSpeedError * (1f - 0.5f * skill) * errorScale);
            Ballistics.Launch(Ball, Vec2.FromAngle(angle), speed, loft, 0f);
            AfterKick(p, KickKind.Pass, target);
        }

        private void KickShot(MatchPlayer p, float power, bool human, float aimY = float.NaN)
        {
            AiConfig ai = Config.Ai;
            TeamState team = Teams[p.Team];
            Vec2 origin = Ball.Pos.Horizontal;
            Vec2 goal = Pitch.GoalCenter(team.AttackDirection);
            float distance = Vec2.Distance(origin, goal);
            MatchPlayer keeper = Teams[1 - p.Team].Players[0];

            if (float.IsNaN(aimY))
            {
                float side = MathF.Abs(keeper.Pos.Y) < 0.5f ? (_rng.NextFloat() < 0.5f ? -1f : 1f) : (keeper.Pos.Y > 0f ? -1f : 1f);
                aimY = side * (Pitch.GoalHalfWidth - ai.ShotCornerMargin);
            }

            float aimHeight = MathUtil.Lerp(ShotMinHeight, ShotMaxHeight, _rng.NextFloat());
            var aim = new Vec2(goal.X, aimY);
            Vec2 delta = aim - origin;

            float finishing = 0.5f * (p.Skill(AttributeId.Finishing) + p.Skill(AttributeId.Composure));
            DifficultyProfile diff = team.Difficulty;
            float errorScale = human ? diff.PlayerErrorScale : diff.ShotErrorScale;
            float pressure = 1f + ShotPressureWeight * (1f - MathUtil.Clamp01(NearestOpponentDistance(origin, p.Team) / PressureRange));
            float sigma = ai.ShotAngleErrorDeg * MathUtil.Deg2Rad * (1f - 0.7f * finishing) * pressure * errorScale * (1f + distance / 40f);
            float angle = delta.Angle + _rng.NextGaussian(0f, sigma);

            float speed = MathUtil.Lerp(ai.ShotSpeedMin, ai.ShotSpeedMax, MathUtil.Clamp01(power * (0.55f + 0.45f * p.Skill(AttributeId.Shooting))));
            float flight = distance / MathF.Max(speed, 1f);
            float loft = MathF.Atan2(aimHeight + 0.5f * Config.Ball.Gravity * flight * flight, MathF.Max(distance, 1f));
            loft += _rng.NextGaussian(0f, ai.ShotLoftErrorDeg * MathUtil.Deg2Rad * (1f - 0.5f * finishing) * errorScale);
            loft = MathUtil.Clamp(loft, 0f, 60f * MathUtil.Deg2Rad);

            float spin = _rng.NextGaussian(0f, ai.ShotMaxSpin * ShotCurl);
            Ballistics.Launch(Ball, Vec2.FromAngle(angle), speed, loft, spin);
            AfterKick(p, KickKind.Shot, null);
        }

        private void KickCross(MatchPlayer p, Vec2 target, bool human, float spin, KickKind kind = KickKind.Cross)
        {
            AiConfig ai = Config.Ai;
            Vec2 origin = Ball.Pos.Horizontal;
            Vec2 delta = target - origin;
            float distance = delta.Length;
            float skill = p.Skill(AttributeId.Crossing);
            DifficultyProfile diff = Teams[p.Team].Difficulty;
            float errorScale = human ? diff.PlayerErrorScale : diff.PassErrorScale;
            float sigma = ai.PassAngleErrorDeg * MathUtil.Deg2Rad * (1f - CrossErrorSkill * skill) * (1f + distance / 30f) * errorScale;
            float angle = delta.Angle + _rng.NextGaussian(0f, sigma);
            float loft = ai.CrossLoftDeg * MathUtil.Deg2Rad;
            float speed = Ballistics.LobSpeed(distance, loft, Config.Ball);
            speed *= 1f + _rng.NextGaussian(0f, CrossSpeedError * (1f - CrossErrorSkill * skill) * errorScale);
            Ballistics.Launch(Ball, Vec2.FromAngle(angle), speed, loft, spin);

            MatchPlayer receiver = null;
            if (kind == KickKind.Cross)
            {
                float best = float.MaxValue;
                TeamState team = Teams[p.Team];
                for (int i = 0; i < team.Players.Length; i++)
                {
                    MatchPlayer t = team.Players[i];
                    float d = Vec2.Distance(t.Pos, target);
                    if (t != p && !t.SentOff && d < best)
                    {
                        best = d;
                        receiver = t;
                    }
                }
            }

            AfterKick(p, kind, receiver);
        }

        private void AfterKick(MatchPlayer p, KickKind kind, MatchPlayer target)
        {
            TeamState team = Teams[p.Team];
            Owner = null;
            LastToucher = p;
            _lastKicker = p;
            _saveRolled = false;
            p.KickTimer = Config.Player.KickCooldown;
            p.Action = PlayerAction.Kick;
            p.ActionDisplayTimer = KickDisplay;
            p.SpeedFactor = 0f;

            if (p.IsKeeper)
            {
                team.KeeperLock = false; // tras jugar el balón, el control vuelve a los jugadores de campo
            }

            _shotLive = kind == KickKind.Shot;
            _passActive = kind == KickKind.Pass || kind == KickKind.Cross;
            _passTeam = p.Team;
            _passTarget = _passActive ? target : null;

            if (kind == KickKind.Shot)
            {
                team.Stats.Shots++;
                _shotStartTick = TickCount;
                AddEvent(MatchEventType.Shot, p.Team, p.Index, Ball.Pos.Horizontal);
            }
            else if (_passActive)
            {
                team.Stats.Passes++;
                ComputeOffsideFlags(p);
            }
        }

        private void ComputeOffsideFlags(MatchPlayer kicker)
        {
            for (int i = 0; i < _offsideFlag.Length; i++)
            {
                _offsideFlag[i] = false;
            }

            if (_offsideExempt)
            {
                return;
            }

            TeamState team = Teams[kicker.Team];
            TeamState opponents = Teams[1 - kicker.Team];
            int count = 0;
            for (int i = 0; i < opponents.Players.Length; i++)
            {
                if (!opponents.Players[i].SentOff)
                {
                    _scratchX[count++] = opponents.Players[i].Pos.X;
                }
            }

            float line = Rules.OffsideLine(team.AttackDirection, _scratchX, count);
            for (int i = 0; i < team.Players.Length; i++)
            {
                MatchPlayer t = team.Players[i];
                _offsideFlag[t.Index] = t != kicker && !t.SentOff
                                        && Rules.IsOffsidePosition(team.AttackDirection, t.Pos.X, Ball.Pos.X, line);
            }
        }
    }
}
