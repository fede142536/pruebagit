using System;
using Football.Core.Common;
using Football.Core.Domain;

namespace Football.Core.MatchCore
{
    public sealed partial class MatchEngine
    {
        private enum PlayState { Attacking, Defending, Loose }

        private const float AnchorMinX = 0.06f;
        private const float AnchorMaxX = 0.94f;
        private const float AnchorWidthUse = 0.95f;
        private const float AttackWidthBase = 0.95f;
        private const float AttackWidthStyle = 0.25f;
        private const float DefendWidthBase = 0.85f;
        private const float DefendWidthStyle = 0.2f;
        private const float LooseShiftFactor = 0.5f;
        private const float PresserLeadTime = 0.25f;
        private const float PressSecondFactor = 0.8f;
        private const float PressSecondIntensity = 0.6f;
        private const float TackleReadyFactor = 1.1f;
        private const float SideMargin = 2f;
        private const float TargetMargin = -0.5f;
        private const float KeeperDiveMargin = 0.4f;
        private const float KeeperDiveWindow = 2f;
        private const float KeeperLateralBand = 1.5f;
        private const float KeeperGoalInset = 0.6f;
        private const float KeeperReactionBase = 0.15f;
        private const float KeeperRunFraction = 0.8f;
        private const float KeeperChaseMaxBallSpeed = 8f;
        private const float InterceptPlayerSpeed = 0.92f;
        private const float SetPieceWalk = 0.75f;
        private const float SetPieceMargin = 0.5f;
        private const float BoxAttackDepth = 7f;
        private const float BoxDefendDepth = 6f;
        private const float BoxStep = 2f;
        private const float PenaltyWait = 2f;
        private const float SetPieceBoxMaxDistance = 28f;

        private readonly bool[] _claimed;
        private const float CelebrationInset = 1.5f;

        private void UpdateTeamPlans()
        {
            for (int t = 0; t < 2; t++)
            {
                TeamState team = Teams[t];
                TeamState opponents = Teams[1 - t];
                int count = 0;
                for (int i = 0; i < opponents.Players.Length; i++)
                {
                    if (!opponents.Players[i].SentOff)
                    {
                        _scratchX[count++] = opponents.Players[i].Pos.X;
                    }
                }

                // Sin fuera de juego (cancha cerrada), la "línea" es el fondo rival: nadie queda adelantado.
                team.OffsideLine = Config.Rules.Offside
                    ? Rules.OffsideLine(team.AttackDirection, _scratchX, count)
                    : team.AttackDirection * Pitch.HalfLength;
                team.Chaser = null;
                team.Presser = null;
                team.Presser2 = null;
                team.Cover = null;
                team.SupportCount = 0;
                for (int i = 0; i < team.Players.Length; i++)
                {
                    team.Players[i].MarkTarget = null;
                }
            }

            int ownerTeam = Owner != null ? Owner.Team : -1;
            for (int t = 0; t < 2; t++)
            {
                PlayState state = ownerTeam == t ? PlayState.Attacking : (ownerTeam == -1 ? PlayState.Loose : PlayState.Defending);
                for (int i = 0; i < Teams[t].Players.Length; i++)
                {
                    MatchPlayer p = Teams[t].Players[i];
                    p.Anchor = ComputeAnchor(p, state);
                }
            }

            if (Owner != null)
            {
                PlanAttack(Teams[ownerTeam]);
                PlanDefense(Teams[1 - ownerTeam]);
            }
            else
            {
                PlanLoose(Teams[0]);
                PlanLoose(Teams[1]);
            }
        }

        private Vec2 ComputeAnchor(MatchPlayer p, PlayState state)
        {
            AiConfig ai = Config.Ai;
            TeamState team = Teams[p.Team];
            TeamStyle style = team.Style;
            FormationSlot slot = SlotFor(team, p, state); // doble dibujo: con balón y sin balón
            float dir = team.AttackDirection;

            float ballProgress = Pitch.Progress(dir, Ball.Pos.X);
            float x = slot.X + (ballProgress - 0.5f) * ai.BallShiftX;
            x += state == PlayState.Attacking ? ai.PossessionAdvance : (state == PlayState.Defending ? -ai.DefendRetreat : 0f);
            x += (style.LineHeight - 0.5f) * ai.LineHeightEffect;
            x += (ballProgress - x) * style.Compactness * ai.CompactnessEffect;
            PlayerDuty duty = Tactics.Effective(p.Role, team.Duties[p.Spot], style);
            x += DutyAdvance(duty, state);
            x = MathUtil.Clamp(x, AnchorMinX, AnchorMaxX);

            float width = state == PlayState.Attacking
                ? AttackWidthBase + AttackWidthStyle * style.Width
                : DefendWidthBase + DefendWidthStyle * style.Width;
            float y = slot.Y * dir * Pitch.HalfWidth * AnchorWidthUse * width * DutyWidth(duty, state);
            y = MathUtil.Clamp(y, -Pitch.HalfWidth + SideMargin, Pitch.HalfWidth - SideMargin);
            y += (Ball.Pos.Y - y) * ai.BallShiftY * (state == PlayState.Loose ? LooseShiftFactor : 1f);

            var anchor = new Vec2(dir * MathUtil.Lerp(-Pitch.HalfLength, Pitch.HalfLength, x), y);
            float pull = DutyBallPull(duty, state);
            return pull > 0f ? Vec2.Lerp(anchor, Ball.Pos.Horizontal, pull) : anchor;
        }

        /// <summary>Cuánto adelanta (o atrasa) el rol su posición, en fracción del largo del campo.</summary>
        private float DutyAdvance(PlayerDuty duty, PlayState state)
        {
            RoleConfig r = Config.Roles;
            bool attacking = state == PlayState.Attacking;
            switch (duty)
            {
                case PlayerDuty.Defensive: return attacking ? -r.DefensiveHold : 0f;
                case PlayerDuty.WingBack: return attacking ? r.WingBackAdvance : 0f;
                case PlayerDuty.BallWinner: return attacking ? -r.BallWinnerHold : 0f;
                case PlayerDuty.BoxToBox: return attacking ? r.BoxToBoxAdvance : 0f;
                case PlayerDuty.TargetMan: return attacking ? -r.TargetManDrop : 0f;
                case PlayerDuty.Runner: return attacking ? r.RunnerAdvance : 0f;
                default: return 0f;
            }
        }

        /// <summary>Factor de apertura del rol (1 = la de su puesto).</summary>
        private float DutyWidth(PlayerDuty duty, PlayState state)
        {
            RoleConfig r = Config.Roles;
            switch (duty)
            {
                case PlayerDuty.Defensive: return r.DefensiveWidth;
                case PlayerDuty.WingBack: return state == PlayState.Attacking ? r.WingBackWidth : 1f;
                case PlayerDuty.TargetMan: return r.TargetManWidth;
                default: return 1f;
            }
        }

        /// <summary>Cuánto se acerca el rol al balón (0 = nada): el recuperador al defender, el organizador al atacar.</summary>
        private float DutyBallPull(PlayerDuty duty, PlayState state)
        {
            RoleConfig r = Config.Roles;
            if (duty == PlayerDuty.BallWinner && state == PlayState.Defending)
            {
                return r.BallWinnerPull;
            }

            return duty == PlayerDuty.Playmaker && state == PlayState.Attacking ? r.PlaymakerPull : 0f;
        }

        private void PlanAttack(TeamState team)
        {
            AiConfig ai = Config.Ai;
            Vec2 ownerPos = Owner.Pos;
            int wanted = Math.Min(ai.SupportCount, team.Support.Length);
            for (int k = 0; k < wanted; k++)
            {
                MatchPlayer best = null;
                float bestDistance = float.MaxValue;
                for (int i = team.FirstOutfield; i < team.Players.Length; i++)
                {
                    MatchPlayer p = team.Players[i];
                    if (p == Owner || p.SentOff || IsInSupport(team, p))
                    {
                        continue;
                    }

                    float d = Vec2.Distance(p.Pos, ownerPos);
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        best = p;
                    }
                }

                if (best == null)
                {
                    break;
                }

                team.Support[team.SupportCount++] = best;
            }
        }

        private static bool IsInSupport(TeamState team, MatchPlayer p)
        {
            for (int i = 0; i < team.SupportCount; i++)
            {
                if (team.Support[i] == p)
                {
                    return true;
                }
            }

            return false;
        }

        private void PlanDefense(TeamState team)
        {
            AiConfig ai = Config.Ai;
            float trigger = ai.PressTrigger * (0.5f + team.Style.PressIntensity) * team.Difficulty.PressScale;
            MatchPlayer first = null, second = null;
            float firstDistance = float.MaxValue, secondDistance = float.MaxValue;
            for (int i = team.FirstOutfield; i < team.Players.Length; i++)
            {
                MatchPlayer p = team.Players[i];
                if (p.SentOff)
                {
                    continue;
                }

                float d = Vec2.Distance(p.Pos, Owner.Pos);
                if (d < firstDistance)
                {
                    second = first;
                    secondDistance = firstDistance;
                    first = p;
                    firstDistance = d;
                }
                else if (d < secondDistance)
                {
                    second = p;
                    secondDistance = d;
                }
            }

            if (first != null && firstDistance <= trigger)
            {
                team.Presser = first;
            }

            if (second != null && team.Style.PressIntensity >= PressSecondIntensity && secondDistance <= trigger * PressSecondFactor)
            {
                team.Presser2 = second;
            }

            team.Cover = !team.HasKeeper ? PickGoalGuard(team) : team.Presser != null ? PickCover(team) : null;

            for (int i = 0; i < _claimed.Length; i++)
            {
                _claimed[i] = false;
            }

            TeamState attackers = Teams[1 - team.Index];
            for (int i = team.FirstOutfield; i < team.Players.Length; i++)
            {
                MatchPlayer p = team.Players[i];
                if (p.SentOff || p == team.Presser || p == team.Presser2 || p == team.Cover)
                {
                    continue;
                }

                MatchPlayer mark = null;
                float best = ai.MarkRadius;
                for (int j = attackers.FirstOutfield; j < attackers.Players.Length; j++)
                {
                    MatchPlayer a = attackers.Players[j];
                    if (a.SentOff || a == Owner || _claimed[a.Index])
                    {
                        continue;
                    }

                    float d = Vec2.Distance(p.Anchor, a.Pos);
                    if (d < best)
                    {
                        best = d;
                        mark = a;
                    }
                }

                if (mark != null)
                {
                    _claimed[mark.Index] = true;
                    p.MarkTarget = mark;
                }
            }
        }

        /// <summary>Punto por detrás del que presiona, en la línea entre el poseedor y la portería propia.</summary>
        private Vec2 CoverPoint(TeamState team)
        {
            Vec2 goal = Pitch.GoalCenter(team.DefendedEnd);
            return Owner.Pos + (goal - Owner.Pos).Normalized * Config.Ai.CoverDepth;
        }

        /// <summary>
        /// Sin arquero (3 contra 3 de potrero) el que cubre es el «cierre»: el jugador más cercano al arco propio que no
        /// presiona. Nadie usa las manos, así que su trabajo es tapar la línea de remate entre el balón y el arco.
        /// </summary>
        private MatchPlayer PickGoalGuard(TeamState team)
        {
            Vec2 goal = Pitch.GoalCenter(team.DefendedEnd);
            MatchPlayer best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < team.Players.Length; i++)
            {
                MatchPlayer p = team.Players[i];
                if (p.SentOff || p == team.Presser || p == team.Presser2 || p == team.SecondPresser)
                {
                    continue;
                }

                float d = Vec2.Distance(p.Pos, goal);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = p;
                }
            }

            return best;
        }

        /// <summary>Punto del cierre sin arquero: sobre la línea balón-arco, más adelantado cuanto más lejos está el balón.</summary>
        private Vec2 GoalCoverPoint(TeamState team)
        {
            AiConfig ai = Config.Ai;
            Vec2 goal = Pitch.GoalCenter(team.DefendedEnd);
            Vec2 ball = Ball.Pos.Horizontal;
            float depth = MathUtil.Clamp(Vec2.Distance(ball, goal) * ai.GoalCoverShare, ai.GoalCoverMinDepth, ai.GoalCoverMaxDepth);
            return goal + (ball - goal).Normalized * depth;
        }

        /// <summary>Elige al compañero más cercano al punto de cobertura (si hay uno a distancia razonable).</summary>
        private MatchPlayer PickCover(TeamState team)
        {
            Vec2 point = CoverPoint(team);
            MatchPlayer best = null;
            float bestDistance = Config.Ai.CoverRadius;
            for (int i = team.FirstOutfield; i < team.Players.Length; i++)
            {
                MatchPlayer p = team.Players[i];
                if (p.SentOff || p == team.Presser || p == team.Presser2)
                {
                    continue;
                }

                float d = Vec2.Distance(p.Pos, point);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = p;
                }
            }

            return best;
        }

        /// <summary>
        /// Punto de contención: entre el poseedor (un instante por delante) y la portería propia. Así el defensor le cierra el
        /// camino en lugar de perseguirlo por detrás.
        /// </summary>
        private Vec2 ContainPoint(MatchPlayer defender, float lead)
        {
            Vec2 carrier = Owner.Pos + Owner.Velocity * lead;
            Vec2 goal = Pitch.GoalCenter(Teams[defender.Team].DefendedEnd);
            Vec2 goalDir = (goal - carrier).Normalized;
            Vec2 contain = carrier + goalDir * Config.Ai.ContainDistance;

            // Si está detrás del poseedor y casi en su línea, lo rodea por el costado en lugar de chocar contra su espalda.
            Vec2 rel = defender.Pos - carrier;
            float along = Vec2.Dot(rel, goalDir);
            var side = new Vec2(-goalDir.Y, goalDir.X);
            float lateral = Vec2.Dot(rel, side);
            if (along < 0f && MathF.Abs(lateral) < Config.Ai.ContainDistance)
            {
                float sign = lateral >= 0f ? 1f : -1f;
                return carrier + side * (sign * Config.Ai.ContainDistance) + goalDir * Config.Ai.ContainDistance;
            }

            return contain;
        }

        /// <summary>La IA solo entra de frente o de costado y si la jugada lo merece; un equipo que presiona mucho arriesga más.</summary>
        private bool AiShouldTackle(MatchPlayer p, TeamState team)
        {
            AiConfig ai = Config.Ai;
            if (Vec2.Distance(p.Pos, Owner.Pos) > ai.TackleEngageDistance)
            {
                return false;
            }

            float threshold = ai.TackleChanceThreshold - ai.TackleThresholdPressSpread * 2f * (team.Style.PressIntensity - 0.5f);
            float chance = ContextTackleChance(p, Owner, out bool fromBehind);
            return !fromBehind && chance >= threshold; // por detrás no entra: es falta fácil y casi nunca gana el balón
        }

        private void PlanLoose(TeamState team)
        {
            AiConfig ai = Config.Ai;
            bool ballInOwnArea = Pitch.InPenaltyArea(team.DefendedEnd, Ball.Pos.Horizontal) && Ball.Vel.HorizontalLength < KeeperChaseMaxBallSpeed;
            MatchPlayer best = null;
            float bestTime = float.MaxValue;
            Vec2 bestPoint = Ball.Pos.Horizontal;
            for (int i = ballInOwnArea ? 0 : 1; i < team.Players.Length; i++)
            {
                MatchPlayer p = team.Players[i];
                if (p.SentOff)
                {
                    continue;
                }

                float time = InterceptTime(p, out Vec2 point);
                if (_passActive && _passTeam == team.Index && p == _passTarget)
                {
                    time -= ai.ReceiverBonus;
                }

                if (time < bestTime)
                {
                    bestTime = time;
                    best = p;
                    bestPoint = point;
                }
            }

            team.Chaser = best;
            team.InterceptPoint = bestPoint;
        }

        private float InterceptTime(MatchPlayer p, out Vec2 point)
        {
            AiConfig ai = Config.Ai;
            Vec2 start = Ball.Pos.Horizontal;
            Vec2 velocity = Ball.Vel.Horizontal;
            float speed = velocity.Length;
            Vec2 direction = velocity.Normalized;
            float decel = Config.Ball.RollDeceleration;
            float stopTime = speed / decel;
            float runSpeed = p.TopSpeed * InterceptPlayerSpeed;
            point = start;

            for (float t = ai.InterceptStep; t <= ai.InterceptHorizon; t += ai.InterceptStep)
            {
                float tt = MathF.Min(t, stopTime);
                point = start + direction * (speed * tt - 0.5f * decel * tt * tt);
                if (Vec2.Distance(p.Pos, point) / runSpeed <= t)
                {
                    return t;
                }
            }

            return ai.InterceptHorizon + Vec2.Distance(p.Pos, point) / runSpeed;
        }

        private void RunPlayerAi(float dt)
        {
            float interval = Config.Ai.DecisionInterval;
            for (int i = 0; i < Players.Length; i++)
            {
                MatchPlayer p = Players[i];
                if (p.SentOff)
                {
                    continue;
                }

                TeamState team = Teams[p.Team];
                if (team.IsHuman && team.Controlled == p)
                {
                    continue;
                }

                if (Owner == p)
                {
                    p.OwnerTimer -= dt;
                    if (p.ActionTimer <= 0f && p.OwnerTimer <= 0f)
                    {
                        p.OwnerTimer = Config.Ai.OwnerDecisionInterval * team.Difficulty.ReactionScale * FatigueReaction(p);
                        OwnerDecide(p);
                    }

                    continue;
                }

                p.DecisionTimer -= dt;
                bool urgent = p.IsKeeper && _shotLive;
                if (p.DecisionTimer > 0f && !urgent)
                {
                    continue;
                }

                float jitter = 0.9f + 0.02f * (p.Index % PlayersPerTeam);
                p.DecisionTimer = interval * team.Difficulty.ReactionScale * jitter * FatigueReaction(p);
                DecideOffBall(p, team);
            }
        }

        private void DecideOffBall(MatchPlayer p, TeamState team)
        {
            AiConfig ai = Config.Ai;
            PlayerConfig pc = Config.Player;
            p.WantsTackle = false;
            if (p.IsKeeper)
            {
                DecideKeeper(p, team);
                return;
            }

            float dir = team.AttackDirection;
            float run = 0.5f * (1f + pc.JogFraction);
            Vec2 target = p.Anchor;
            float speed = pc.JogFraction;
            int ownerTeam = Owner != null ? Owner.Team : -1;
            bool positional = true; // false si el jugador tiene una tarea urgente (presionar, apoyar, perseguir)

            if (ownerTeam == p.Team)
            {
                if (IsInSupport(team, p))
                {
                    target = SupportPosition(p, team);
                    speed = run;
                    positional = false;
                }
                else if (IsAttackingRole(p.Role) && Pitch.Progress(dir, Ball.Pos.X) > 0.35f)
                {
                    target = RunIntoSpace(p, dir); // se desmarca hacia el hueco más libre
                    speed = run;
                    positional = false;
                }

                float limit = team.OffsideLine - dir * ai.OffsideMargin;
                if (dir * target.X > dir * limit && dir * target.X > dir * Ball.Pos.X)
                {
                    target.X = limit;
                }
            }
            else if (ownerTeam >= 0)
            {
                if (p == team.Presser || p == team.Presser2 || p == team.SecondPresser)
                {
                    // Contiene a distancia y solo se lanza a por el balón cuando la entrada tiene buenas opciones.
                    float distance = Vec2.Distance(p.Pos, Owner.Pos);
                    speed = distance > ai.PressSprintDistance ? 1f : run;
                    p.WantsTackle = AiShouldTackle(p, team);
                    target = p.WantsTackle || p == team.Presser2
                        ? Owner.Pos + Owner.Velocity * PresserLeadTime
                        : ContainPoint(p, PresserLeadTime);
                    positional = false;
                }
                else if (p == team.Cover)
                {
                    target = team.HasKeeper ? CoverPoint(team) : GoalCoverPoint(team);
                    speed = run;
                    positional = false;
                }
                else if (p.MarkTarget != null)
                {
                    Vec2 mark = p.MarkTarget.Pos;
                    Vec2 goal = Pitch.GoalCenter(team.DefendedEnd);
                    Vec2 goalSide = mark + (goal - mark).Normalized * ai.MarkDistance;
                    target = Vec2.Lerp(p.Anchor, goalSide, ai.MarkBlend);
                }
            }
            else if (p == team.Chaser)
            {
                target = team.InterceptPoint;
                speed = 1f;
                positional = false;
            }
            else if (ownerTeam < 0 && !team.HasKeeper && team.DefendedEnd * Ball.Pos.X > 0f && p == PickGoalGuard(team))
            {
                target = GoalCoverPoint(team); // balón suelto en campo propio: el cierre no deja el arco vacío
                speed = run;
                positional = false;
            }

            target = Pitch.Clamp(target, TargetMargin);
            if (positional)
            {
                // Un jugador que solo mantiene su posición no corre sin parar: si ya está cerca se queda quieto y,
                // si está a poca distancia, camina.
                float away = Vec2.Distance(p.Pos, target);
                if (away < ai.HoldRadius)
                {
                    target = p.Pos;
                    speed = 0f;
                }
                else if (away < ai.WalkDistance)
                {
                    speed = MathF.Min(speed, ai.WalkFraction);
                }
            }

            p.Target = target;
            p.SpeedFactor = speed;
        }

        private static bool IsAttackingRole(Position role) =>
            role == Position.Striker || role == Position.LeftWinger || role == Position.RightWinger || role == Position.AttackingMidfielder;

        private Vec2 SupportPosition(MatchPlayer p, TeamState team)
        {
            AiConfig ai = Config.Ai;
            Vec2 ownerPos = Owner.Pos;
            float dir = team.AttackDirection;
            Vec2 best = p.Anchor;
            float bestScore = float.MinValue;
            for (int k = 0; k < ai.SupportCandidates; k++)
            {
                Vec2 candidate = k == 0
                    ? p.Anchor
                    : p.Anchor + Vec2.FromAngle(k * 2f * MathF.PI / ai.SupportCandidates) * ai.SupportSearchRadius;
                candidate = Pitch.Clamp(candidate, TargetMargin);

                float open = MathF.Min(10f, NearestOpponentDistance(candidate, p.Team));
                float away = Vec2.Distance(candidate, p.Anchor);
                float fromOwner = Vec2.Distance(candidate, ownerPos);
                float score = open - 0.15f * away - MathF.Abs(fromOwner - ai.SupportDistance) * 0.2f
                              + 0.08f * team.Style.Directness * dir * (candidate.X - ownerPos.X);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>
        /// Desmarque de un delantero: entre correr recto, abrirse a un lado o al otro, elige el espacio más alejado de los
        /// rivales (con algo de preferencia por el recto). Sin asignaciones de memoria.
        /// </summary>
        private Vec2 RunIntoSpace(MatchPlayer p, float dir)
        {
            AiConfig ai = Config.Ai;
            Vec2 best = p.Anchor + new Vec2(dir * ai.RunAhead, 0f);
            float bestScore = float.MinValue;
            for (int k = -1; k <= 1; k++)
            {
                Vec2 candidate = Pitch.Clamp(p.Anchor + new Vec2(dir * ai.RunAhead, k * ai.RunWidth), -SideMargin);
                float score = NearestOpponentDistance(candidate, p.Team) + (k == 0 ? ai.RunStraightBias : 0f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private Vec2 KeeperAnchor(MatchPlayer keeper, TeamState team)
        {
            AiConfig ai = Config.Ai;
            Vec2 goal = Pitch.GoalCenter(team.DefendedEnd);
            Vec2 toBall = Ball.Pos.Horizontal - goal;
            float depth = MathUtil.Clamp(ai.KeeperMinDepth + ai.KeeperDepthPerMeter * toBall.Length, ai.KeeperMinDepth, ai.KeeperMaxDepth);
            return goal + toBall.Normalized * depth;
        }

        /// <summary>
        /// Un rival con el balón cerca de la portería sin ningún defensor de campo entre él y el arco: el arquero debe salir.
        /// </summary>
        private bool OneOnOne(TeamState team)
        {
            if (Owner == null || Owner.Team == team.Index)
            {
                return false;
            }

            AiConfig ai = Config.Ai;
            Vec2 goal = Pitch.GoalCenter(team.DefendedEnd);
            float toGoal = Vec2.Distance(Owner.Pos, goal);
            if (toGoal > ai.KeeperRushDistance)
            {
                return false;
            }

            for (int i = team.FirstOutfield; i < team.Players.Length; i++)
            {
                MatchPlayer d = team.Players[i];
                if (d.SentOff)
                {
                    continue;
                }

                float lane = MathUtil.DistancePointSegment(d.Pos, Owner.Pos, goal, out float along);
                if (along > 0.05f && lane < ai.KeeperRushLaneWidth)
                {
                    return false;
                }
            }

            return true;
        }

        private void DecideKeeper(MatchPlayer keeper, TeamState team)
        {
            PlayerConfig pc = Config.Player;
            float end = team.DefendedEnd;
            keeper.Diving = false;

            bool opponentBall = Owner == null && LastToucher != null && LastToucher.Team != team.Index;
            if (opponentBall && Ball.Vel.X * end > pc.ShotMinSpeed * 0.3f && Ball.Vel.HorizontalLength >= pc.ShotMinSpeed * 0.7f)
            {
                float reaction = KeeperReactionBase * team.Difficulty.ReactionScale * (1.3f - keeper.Skill(AttributeId.Reflexes));
                if ((TickCount - _shotStartTick) * _dt >= reaction || !_shotLive)
                {
                    float t = (end * Pitch.HalfLength - Ball.Pos.X) / Ball.Vel.X;
                    if (t > 0f && t < KeeperDiveWindow)
                    {
                        float yAt = Ball.Pos.Y + Ball.Vel.Y * t;
                        if (MathF.Abs(yAt) < Pitch.GoalHalfWidth + KeeperLateralBand)
                        {
                            float reach = Pitch.GoalHalfWidth + KeeperDiveMargin;
                            float aimY = MathUtil.Clamp(yAt, -reach, reach);
                            bool committed = keeper.Action == PlayerAction.Dive && keeper.ActionDisplayTimer > 0f;
                            if (committed && MathF.Abs(aimY - keeper.Target.Y) < pc.KeeperDiveRetarget)
                            {
                                aimY = keeper.Target.Y; // ya está en el aire: no se corrige por pequeñas variaciones de la trayectoria
                            }

                            keeper.Target = new Vec2(end * (Pitch.HalfLength - KeeperGoalInset), aimY);
                            keeper.Diving = true;
                            keeper.Action = PlayerAction.Dive;
                            keeper.DiveHeight = MathF.Max(0f, Ball.Pos.Z + Ball.Vel.Z * t - 0.5f * Config.Ball.Gravity * t * t);
                            keeper.ActionDisplayTimer = KickDisplay;
                            return;
                        }
                    }
                }
            }

            if (Owner == null && team.Chaser == keeper)
            {
                keeper.Target = team.InterceptPoint;
                keeper.SpeedFactor = 1f;
                return;
            }

            if (OneOnOne(team))
            {
                // Mano a mano: sale a achicar el ángulo y, si llega, le disputa el balón.
                AiConfig ai = Config.Ai;
                Vec2 goal = Pitch.GoalCenter(end);
                keeper.Target = Owner.Pos + (goal - Owner.Pos).Normalized * ai.KeeperRushStop;
                keeper.SpeedFactor = 1f;
                keeper.WantsTackle = Vec2.Distance(keeper.Pos, Owner.Pos) <= Config.Rules.TackleRange * TackleReadyFactor;
                keeper.Action = PlayerAction.Rush;
                keeper.ActionDisplayTimer = Config.Ai.DecisionInterval;
                return;
            }

            keeper.Target = KeeperAnchor(keeper, team);
            keeper.SpeedFactor = KeeperRunFraction;
        }

        /// <summary>Posiciones de todos los jugadores durante un saque (o la pausa tras un gol).</summary>
        private void UpdateSetPieceTargets()
        {
            UpdateSetPieceTargetsCore();
            MatchPlayer c = Celebrator;
            if (c != null && !c.SentOff)
            {
                // Festejo: el goleador corre al banderín del córner más cercano.
                float sx = c.Pos.X >= 0f ? 1f : -1f, sy = c.Pos.Y >= 0f ? 1f : -1f;
                c.Target = new Vec2(sx * (Pitch.HalfLength - CelebrationInset), sy * (Pitch.HalfWidth - CelebrationInset));
                c.SpeedFactor = 1f;
                c.Action = PlayerAction.Celebrate;
                c.ActionDisplayTimer = Config.Ai.DecisionInterval * 2f;
            }
        }

        private MatchPlayer _throwSupportA, _throwSupportB;
        private const float ThrowSupportAlong = 9f;
        private const float ThrowSupportInward = 8f;

        /// <summary>Los dos compañeros de campo más cercanos al lateral (sin el que saca ni el arquero).</summary>
        private void PickThrowSupport(Restart r)
        {
            _throwSupportA = _throwSupportB = null;
            if (r.Kind != RestartKind.ThrowIn)
            {
                return;
            }

            float bestA = float.MaxValue, bestB = float.MaxValue;
            TeamState team = Teams[r.Team];
            for (int i = team.FirstOutfield; i < team.Players.Length; i++)
            {
                MatchPlayer p = team.Players[i];
                if (p.SentOff || p == _taker)
                {
                    continue;
                }

                float d = Vec2.Distance(p.Pos, r.Position);
                if (d < bestA)
                {
                    _throwSupportB = _throwSupportA;
                    bestB = bestA;
                    _throwSupportA = p;
                    bestA = d;
                }
                else if (d < bestB)
                {
                    _throwSupportB = p;
                    bestB = d;
                }
            }
        }

        private void UpdateSetPieceTargetsCore()
        {
            Restart r = _restart;
            float distance = Config.Rules.SetPieceDistance + SetPieceMargin;

            for (int i = 0; i < Players.Length; i++)
            {
                MatchPlayer p = Players[i];
                if (p.SentOff)
                {
                    continue;
                }

                TeamState team = Teams[p.Team];
                bool taking = p.Team == r.Team;
                float dir = team.AttackDirection;
                p.WantsTackle = false;
                p.Diving = false;
                p.SpeedFactor = SetPieceWalk;

                if (p == _taker)
                {
                    p.Target = r.Position - new Vec2(dir * TakerOffset, 0f);
                    continue;
                }

                if (taking && r.Kind == RestartKind.ThrowIn && (p == _throwSupportA || p == _throwSupportB))
                {
                    // Lateral: los dos más cercanos se acercan a pedirla, uno hacia adentro y otro a lo largo de la banda.
                    float inward = r.Position.Y > 0f ? -1f : 1f;
                    Vec2 offer = p == _throwSupportA
                        ? new Vec2(dir * ThrowSupportAlong * 0.3f, inward * ThrowSupportInward)
                        : new Vec2(dir * ThrowSupportAlong, inward * ThrowSupportInward * 0.4f);
                    p.Target = Pitch.Clamp(r.Position + offer, TargetMargin);
                    p.SpeedFactor = 1f;
                    continue;
                }

                if (p.IsKeeper)
                {
                    p.Target = r.Kind == RestartKind.Penalty && !taking
                        ? Pitch.GoalCenter(team.DefendedEnd) + new Vec2(-team.DefendedEnd * KeeperGoalInset * 0.5f, 0f)
                        : KeeperAnchor(p, team);
                    continue;
                }

                Vec2 target = ComputeAnchor(p, taking ? PlayState.Attacking : PlayState.Defending);

                bool boxPlay = r.Kind == RestartKind.Corner
                               || (r.Kind == RestartKind.FreeKick
                                   && Vec2.Distance(r.Position, Pitch.GoalCenter(Teams[r.Team].AttackDirection)) < SetPieceBoxMaxDistance);
                if (boxPlay && p.Slot >= 2)
                {
                    float goalSign = Teams[r.Team].AttackDirection;
                    target = taking
                        ? AttackingBoxSpot(team, p, r, goalSign)
                        : DefendingBoxSpot(team, p, Teams[r.Team], goalSign);
                }

                if (r.Kind == RestartKind.Penalty)
                {
                    float goalSign = Teams[r.Team].AttackDirection;
                    float edge = Pitch.HalfLength - Config.Pitch.PenaltyAreaDepth - PenaltyWait;
                    target = new Vec2(goalSign * edge, (p.Slot - 5) * 4.5f);
                }

                if (r.Kind == RestartKind.KickOff && dir * target.X > -KickoffOwnHalfLimit)
                {
                    target.X = -dir * KickoffOwnHalfLimit;
                }

                if (r.Kind == RestartKind.KickOff && taking && p != _taker)
                {
                    // Compañeros del ejecutante: fuera del círculo central, en su campo, listos para recibir el pase atrás.
                    float clear = Config.Pitch.CenterCircleRadius + KickoffCircleClearance;
                    if (target.Length < clear)
                    {
                        target = new Vec2(-dir * clear, target.Y);
                    }
                }

                if (!taking && r.Kind != RestartKind.Penalty)
                {
                    Vec2 fromBall = target - r.Position;
                    if (fromBall.Length < distance)
                    {
                        Vec2 push = fromBall.Normalized;
                        if (push.LengthSquared < MathUtil.Epsilon)
                        {
                            push = new Vec2(-dir, 0f);
                        }

                        target = r.Position + push * distance;
                    }
                }

                p.Target = Pitch.Clamp(target, TargetMargin);
            }
        }
    }
}
