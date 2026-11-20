using System;
using Football.Core.Common;

namespace Football.Core.MatchCore
{
    public sealed partial class MatchEngine
    {
        private const float HumanMoveDeadZone = 0.1f;
        private const float HumanTargetDistance = 5f;
        private const float HumanMinMove = 0.3f;
        private const float HumanTackleWindow = 0.35f;
        private const float HumanDefaultPower = 0.8f;
        private const float HumanAimDeadZone = 0.3f;
        private const float HumanAimForward = 0.2f;
        private const float HumanAimOverflow = 1f;
        private const float HumanAssistBaseAngle = 40f;
        private const float HumanAssistAngleRange = 40f;
        private const float HumanThroughLead = 0.9f;
        private const float HumanThroughAhead = 5f;
        private const float HumanThroughMinAdvance = 3f;
        private const float HumanLobMinDistance = 25f;
        private const float HumanBlindDistance = 15f;
        private const float HumanCrossProgressFactor = 0.9f;

        /// <summary>Indica si un equipo está controlado por una persona (por defecto la IA controla a ambos).</summary>
        /// <param name="team">Equipo (0 local, 1 visitante).</param>
        /// <param name="isHuman">Verdadero para que lo controle una persona.</param>
        public void SetHuman(int team, bool isHuman)
        {
            Teams[team].IsHuman = isHuman;
            if (!isHuman)
            {
                Teams[team].Controlled = null;
            }
        }

        /// <summary>
        /// Entrega la entrada de la persona para el próximo paso. Los botones son impulsos que se consumen en un solo paso;
        /// el movimiento y el esprint se mantienen hasta la siguiente llamada.
        /// </summary>
        /// <param name="team">Equipo controlado.</param>
        /// <param name="input">Entrada.</param>
        public void SetInput(int team, in MatchInput input)
        {
            MatchInput current = Teams[team].Input;
            current.Move = input.Move;
            current.Sprint = input.Sprint;
            current.ShootPower = input.ShootPower;
            current.Pass |= input.Pass;
            current.Through |= input.Through;
            current.Cross |= input.Cross;
            current.Shoot |= input.Shoot;
            current.Tackle |= input.Tackle;
            current.Switch |= input.Switch;
            Teams[team].Input = current;
        }

        /// <summary>
        /// Compañero al que iría un pase con la dirección dada, para mostrar una vista previa en pantalla.
        /// No modifica el estado ni consume azar.
        /// </summary>
        /// <param name="team">Equipo controlado por la persona.</param>
        /// <param name="stick">Dirección del joystick.</param>
        /// <param name="through">Verdadero para un pase filtrado al espacio.</param>
        /// <returns>El receptor previsto, o null si el equipo no tiene el balón o no hay candidato.</returns>
        public MatchPlayer PreviewPassTarget(int team, Vec2 stick, bool through)
        {
            TeamState t = Teams[team];
            MatchPlayer p = t.Controlled;
            if (p == null || Owner != p)
            {
                return null;
            }

            var input = new MatchInput { Move = stick };
            ChooseHumanTarget(p, t, input, through, 0f, out _, out MatchPlayer target);
            return target;
        }

        private void ClearInputImpulses()
        {
            for (int t = 0; t < 2; t++)
            {
                MatchInput i = Teams[t].Input;
                i.Pass = i.Through = i.Cross = i.Shoot = i.Tackle = i.Switch = false;
                Teams[t].Input = i;
            }
        }

        private MatchPlayer NearestToBall(TeamState team, MatchPlayer exclude)
        {
            Vec2 ball = Ball.Pos.Horizontal;
            bool keeperAllowed = Pitch.InPenaltyArea(team.DefendedEnd, ball);
            MatchPlayer best = null;
            float bestDistance = float.MaxValue;
            for (int i = keeperAllowed ? 0 : 1; i < team.Players.Length; i++)
            {
                MatchPlayer p = team.Players[i];
                if (p.SentOff || p == exclude)
                {
                    continue;
                }

                float d = Vec2.Distance(p.Pos, ball);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = p;
                }
            }

            return best;
        }

        private void UpdateHumanControl(float dt)
        {
            AiConfig ai = Config.Ai;
            for (int t = 0; t < 2; t++)
            {
                TeamState team = Teams[t];
                if (!team.IsHuman)
                {
                    continue;
                }

                team.SwitchTimer -= dt;
                if (Owner != null && Owner.Team == t)
                {
                    team.Controlled = Owner;
                }
                else if (Owner == null && _passActive && _passTeam == t && _passTarget != null && !_passTarget.SentOff)
                {
                    team.Controlled = _passTarget;
                }
                else if (team.Controlled == null || team.Controlled.SentOff)
                {
                    team.Controlled = NearestToBall(team, null);
                    team.SwitchTimer = ai.SwitchCooldown;
                }
                else if (team.Input.Switch)
                {
                    MatchPlayer next = NearestToBall(team, team.Controlled);
                    if (next != null)
                    {
                        team.Controlled = next;
                        team.SwitchTimer = ai.SwitchCooldown;
                    }
                }
                else if (team.SwitchTimer <= 0f)
                {
                    MatchPlayer nearest = NearestToBall(team, null);
                    Vec2 ball = Ball.Pos.Horizontal;
                    if (nearest != null && nearest != team.Controlled
                        && Vec2.Distance(team.Controlled.Pos, ball) - Vec2.Distance(nearest.Pos, ball) > ai.SwitchHysteresis)
                    {
                        team.Controlled = nearest;
                        team.SwitchTimer = ai.SwitchCooldown;
                    }
                }
            }
        }

        private void ApplyHumanInput(float dt)
        {
            for (int t = 0; t < 2; t++)
            {
                TeamState team = Teams[t];
                MatchPlayer p = team.Controlled;
                if (!team.IsHuman || p == null || p.SentOff)
                {
                    continue;
                }

                MatchInput input = team.Input;
                float length = input.Move.Length;
                if (length > HumanMoveDeadZone)
                {
                    p.Target = p.Pos + input.Move / length * HumanTargetDistance;
                    p.SpeedFactor = input.Sprint ? 1f : Config.Player.JogFraction * MathUtil.Clamp(length, HumanMinMove, 1f);
                }
                else
                {
                    p.Target = p.Pos;
                    p.SpeedFactor = 0f;
                }

                if (Owner == p)
                {
                    if (p.ActionTimer <= 0f)
                    {
                        HumanKick(p, team, input);
                    }
                }
                else
                {
                    p.TackleWindow -= dt;
                    if (input.Tackle)
                    {
                        p.TackleWindow = HumanTackleWindow;
                    }

                    p.WantsTackle = p.TackleWindow > 0f;
                }
            }
        }

        private void HumanKick(MatchPlayer p, TeamState team, MatchInput input)
        {
            if (input.Shoot)
            {
                float power = input.ShootPower > 0f ? input.ShootPower : HumanDefaultPower;
                KickShot(p, power, true, HumanAimY(p, team, input));
            }
            else if (input.Pass)
            {
                HumanPass(p, team, input, false);
            }
            else if (input.Through)
            {
                HumanPass(p, team, input, true);
            }
            else if (input.Cross)
            {
                HumanCross(p, team, input);
            }
        }

        private float HumanAimY(MatchPlayer p, TeamState team, MatchInput input)
        {
            Vec2 origin = Ball.Pos.Horizontal;
            float dir = team.AttackDirection;
            if (input.Move.Length > HumanAimDeadZone && input.Move.X * dir > HumanAimForward)
            {
                float dx = MathF.Abs(Pitch.HalfLength * dir - origin.X);
                float y = origin.Y + input.Move.Y / (input.Move.X * dir) * dx;
                float limit = Pitch.GoalHalfWidth + HumanAimOverflow;
                return MathUtil.Clamp(y, -limit, limit);
            }

            return float.NaN;
        }

        private bool ChooseHumanTarget(MatchPlayer p, TeamState team, MatchInput input, bool through, float minDistance,
            out Vec2 point, out MatchPlayer target)
        {
            AiConfig ai = Config.Ai;
            Vec2 origin = Ball.Pos.Horizontal;
            float dir = team.AttackDirection;
            Vec2 stick = input.Move.Length > HumanMoveDeadZone ? input.Move.Normalized : Vec2.FromAngle(p.Heading);
            float assist = team.Difficulty.PassAssist;
            float maxAngle = (HumanAssistBaseAngle + HumanAssistAngleRange * assist) * MathUtil.Deg2Rad;

            point = origin + stick * HumanBlindDistance;
            target = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < team.Players.Length; i++)
            {
                MatchPlayer t = team.Players[i];
                if (t == p || t.SentOff)
                {
                    continue;
                }

                Vec2 v = t.Pos - origin;
                float d = v.Length;
                if (d < MathF.Max(ai.PassMinDistance, minDistance) || d > ai.PassMaxDistance)
                {
                    continue;
                }

                float cos = Vec2.Dot(stick, v / d);
                if (MathF.Acos(MathUtil.Clamp(cos, -1f, 1f)) > maxAngle)
                {
                    continue;
                }

                float advance = dir * (t.Pos.X - origin.X);
                Vec2 candidate;
                float score = 2f * cos - d / 50f + 0.3f * MathUtil.Clamp01(NearestOpponentDistance(t.Pos, p.Team) / 6f);
                if (through)
                {
                    if (advance < HumanThroughMinAdvance)
                    {
                        continue;
                    }

                    candidate = t.Pos + t.Velocity * HumanThroughLead + new Vec2(dir * HumanThroughAhead, 0f);
                    score += 0.5f * MathUtil.Clamp01(advance / 30f);
                }
                else
                {
                    candidate = t.Pos + t.Velocity * ai.PassLeadTime;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    target = t;
                    point = Pitch.Clamp(candidate, -SafeMarginToLine);
                }
            }

            if (target != null)
            {
                Vec2 toTarget = point - origin;
                Vec2 blended = Vec2.Lerp(stick, toTarget.Normalized, assist).Normalized;
                point = origin + blended * toTarget.Length;
            }

            return target != null;
        }

        private void HumanPass(MatchPlayer p, TeamState team, MatchInput input, bool through)
        {
            ChooseHumanTarget(p, team, input, through, 0f, out Vec2 point, out MatchPlayer target);
            KickPass(p, point, target, true, true, float.MaxValue);
        }

        private void HumanCross(MatchPlayer p, TeamState team, MatchInput input)
        {
            AiConfig ai = Config.Ai;
            Vec2 origin = Ball.Pos.Horizontal;
            float dir = team.AttackDirection;
            float spin = (origin.Y > 0f ? -1f : 1f) * CrossSpin * dir;
            if (Pitch.Progress(dir, origin.X) >= ai.CrossMinProgress * HumanCrossProgressFactor)
            {
                var target = new Vec2(dir * (Pitch.HalfLength - 9f), _rng.NextGaussian(0f, ai.CrossTargetSpread));
                KickCross(p, target, true, spin, KickKind.Cross);
                return;
            }

            ChooseHumanTarget(p, team, input, false, HumanLobMinDistance, out Vec2 point, out _);
            KickCross(p, point, true, 0f, KickKind.Cross);
        }
    }
}
