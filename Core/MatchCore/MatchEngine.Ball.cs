using System;
using Football.Core.Common;
using Football.Core.Domain;

namespace Football.Core.MatchCore
{
    public sealed partial class MatchEngine
    {
        private const float ParryLateralSpeed = 5f;
        private const float ParryRetainFactor = 0.35f;
        private const float ParryLift = 3f;
        private const float ParryOverBarChance = 0.4f;
        private const float ParryLiftStep = 1f;
        private const float SaveMinBallSpeed = 5f;
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
        private readonly MatchPlayer[] _lastKickerOfTeam = new MatchPlayer[2];
        private bool _offsideActive;
        private MatchPlayer _beatenKeeper;
        private int _offsideTeam;
        private MatchPlayer _passTarget;
        private readonly bool[] _offsideFlag;
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

        /// <summary>El arquero dentro de su área lleva el balón en las manos: no se le puede quitar ni se le escapa.</summary>
        private bool KeeperHolding(MatchPlayer p) => p.IsKeeper && Pitch.InPenaltyArea(Teams[p.Team].DefendedEnd, p.Pos);

        private void CarryBall(float dt)
        {
            MatchPlayer p = Owner;
            PlayerConfig c = Config.Player;
            if (KeeperHolding(p))
            {
                Ball.Pos = new Vec3(p.Pos.X, p.Pos.Y, c.KeeperHoldHeight);
                Ball.Vel = new Vec3(p.Velocity.X, p.Velocity.Y, 0f);
                Ball.Spin = 0f;
                return;
            }

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
            Vec2 blockerContact = to;

            for (int i = 0; i < Players.Length; i++)
            {
                MatchPlayer p = Players[i];
                if (p.SentOff || p.KickTimer > 0f || p.SlideTimer > 0f || p.RecoverTimer > 0f || p == _beatenKeeper)
                {
                    continue; // un arquero que ya falló la atajada de este tiro no lo vuelve a tocar
                }

                float distance = MathUtil.DistancePointSegment(p.Pos, from, to, out float along);
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
                    blockerContact = from + (to - from) * along;
                }
            }

            if (best != null)
            {
                if (best.IsKeeper || !Miscontrols(best, ballSpeed, from, to))
                {
                    GainPossession(best);
                }
                else
                {
                    HeavyTouch(best);
                }
            }
            else if (blocker != null)
            {
                Deflect(blocker, blockerContact);
            }
        }

        private int _possessionTeam = -1;

        /// <summary>
        /// ¿Se le escapa el balón al controlarlo? Más probable con balones rápidos (cerca del máximo controlable), con poco
        /// FirstTouch y con un rival encima. Es la principal causa de pases no completados entre compañeros.
        /// </summary>
        private bool Miscontrols(MatchPlayer p, float ballSpeed, Vec2 from, Vec2 to)
        {
            PlayerConfig c = Config.Player;
            float firstTouch = p.Skill(AttributeId.FirstTouch);
            float maxControl = c.ControlMaxBallSpeed * (1f + c.ControlSpeedSpread * (2f * firstTouch - 1f));
            float relative = MathUtil.Clamp01(ballSpeed / MathF.Max(maxControl, 0.1f));
            TeamState team = Teams[p.Team];
            float difficulty = team.IsHuman && team.Controlled == p ? team.Difficulty.PlayerErrorScale : team.Difficulty.PassErrorScale;
            float chance = c.MiscontrolChance * relative * relative * (1f - c.MiscontrolSkillWeight * firstTouch) * difficulty;
            if (NearestOpponentDistance(p.Pos, p.Team) < c.MiscontrolPressureRange)
            {
                chance *= 1f + c.MiscontrolPressureWeight;
            }

            return _rng.NextFloat() < chance;
        }

        /// <summary>Mal control: el balón sigue hacia delante, más lento y algo desviado; nadie lo tiene.</summary>
        private void HeavyTouch(MatchPlayer p)
        {
            if (OnTouch(p, true, false))
            {
                return;
            }

            PlayerConfig c = Config.Player;
            Vec2 velocity = Ball.Vel.Horizontal.Rotated(_rng.NextGaussian(0f, c.MiscontrolNoise)) * c.MiscontrolSpeedFactor;
            Ball.Pos = new Vec3(p.Pos.X, p.Pos.Y, 0f);
            Ball.Vel = new Vec3(velocity.X, velocity.Y, 0f);
            p.KickTimer = BlockLockout;
        }

        // Equipo que tenía el balón por última vez, para contar recuperaciones (un saque lo asigna a quien saca).
        private int _ballTeam = -1;

        private void CountPossessionChange(MatchPlayer p)
        {
            int team = p.Team;
            if (team != _possessionTeam)
            {
                _possessionTeam = team;
                Teams[team].Stats.Possessions++;
            }

            if (_ballTeam >= 0 && _ballTeam != team)
            {
                p.Stats.Recoveries++;
                Teams[team].Stats.Recoveries++;
                TeamState losers = Teams[_ballTeam];
                losers.LastPasser = losers.LastReceiver = null; // perder el balón anula la asistencia pendiente
            }

            _ballTeam = team;
        }

        private void GainPossession(MatchPlayer p)
        {
            if (OnTouch(p, true))
            {
                return;
            }

            CountPossessionChange(p);
            Owner = p;
            _holder = null;
            _boxOwner = null;
            Ball.Vel = Vec3.Zero;
            Ball.Spin = 0f;
            p.ActionTimer = Config.Player.ControlDelay;
            p.OwnerTimer = 0f;
            // El arquero se queda con el balón en las manos; los demás arrancan hacia delante sin salirse del campo.
            p.Target = p.IsKeeper ? p.Pos : Pitch.Clamp(p.Pos + Vec2.FromAngle(p.Heading) * Config.Ai.DribbleLookahead, -SafeMarginToLine);
            p.SpeedFactor = Config.Player.JogFraction;
        }

        /// <summary>
        /// Un balón demasiado rápido para controlarlo golpea el cuerpo del jugador: rebota según el punto de contacto, pierde
        /// velocidad y, si es el arquero, sale hacia fuera de su portería.
        /// </summary>
        private void Deflect(MatchPlayer p, Vec2 contact)
        {
            if (OnTouch(p, false, false))
            {
                return;
            }

            PlayerConfig c = Config.Player;
            Vec2 velocity = Ball.Vel.Horizontal;
            Vec2 normal = contact - p.Pos;
            normal = normal.LengthSquared > 1e-6f ? normal.Normalized : -velocity.Normalized;
            float into = Vec2.Dot(velocity, normal);
            if (into < 0f)
            {
                velocity -= normal * (2f * into); // reflexión sobre el cuerpo
            }

            velocity = velocity.Rotated(_rng.NextGaussian(0f, c.DeflectionNoise)) * c.DeflectionSpeedFactor;
            if (p.IsKeeper)
            {
                float end = Teams[p.Team].DefendedEnd;
                velocity = new Vec2(-end * MathF.Abs(velocity.X), velocity.Y);
            }

            Ball.Pos = new Vec3(contact.X, contact.Y, Ball.Pos.Z);
            Ball.Vel = new Vec3(velocity.X, velocity.Y, c.DeflectionLift * _rng.NextFloat());
            p.KickTimer = BlockLockout;
        }

        /// <summary>
        /// Registra un toque de balón. Devuelve true si provoca una interrupción (fuera de juego).
        /// </summary>
        /// <param name="p">Jugador que toca el balón.</param>
        /// <param name="deliberate">Juega el balón a propósito (control, entrada). Un rebote o una parada no lo es: según el
        /// reglamento, tras ellos sigue valiendo la posición de fuera de juego del último envío.</param>
        /// <param name="clean">El jugador se quedó con el balón. Un mal control o un rebote en un compañero no completan el pase.</param>
        private bool OnTouch(MatchPlayer p, bool deliberate, bool clean = true)
        {
            if (_offsideActive)
            {
                if (p.Team == _offsideTeam && p != _lastKicker && _offsideFlag[p.Index])
                {
                    _offsideActive = false;
                    _passActive = false;
                    CallOffside(p);
                    return true;
                }

                if (p.Team == _offsideTeam || deliberate)
                {
                    _offsideActive = false;
                }
            }

            if (_passActive)
            {
                _passActive = false;
                if (p.Team == _passTeam && p != _lastKicker && clean)
                {
                    Teams[_passTeam].Stats.PassesCompleted++;
                    if (_lastKicker != null && _lastKicker.Team == p.Team)
                    {
                        _lastKicker.Stats.PassesCompleted++;
                        Teams[p.Team].LastPasser = _lastKicker;
                        Teams[p.Team].LastReceiver = p;
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
            _beatenKeeper = null;
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
                float time = ShotDistanceForKeeper() / MathF.Max(speed, 1f);
                float comfort = MathUtil.Clamp01((time - c.KeeperComfortTime) / c.KeeperComfortRange);
                float reach = (c.KeeperBaseReach + c.KeeperExtraReach * keeper.Skill(AttributeId.Reflexes)) * (1f + c.KeeperComfortReach * comfort);
                float distance = Vec3.Distance(Ball.Pos, centre);
                if (distance <= reach)
                {
                    _saveRolled = true;
                    // Lo lejos del cuerpo se mide contra todo el cuerpo (del suelo a las manos en alto), no contra un punto:
                    // un remate rasante directo al arquero es un tiro «al cuerpo», no uno colocado.
                    // Se mide por dónde va a pasar el balón al cruzar la posición del arquero (al tirar la atajada el balón
                    // todavía viene de frente, en el borde de su alcance, y esa distancia no dice nada de la colocación).
                    float lateral = Vec2.Distance(Ball.Pos.Horizontal, keeper.Pos);
                    if (MathF.Abs(Ball.Vel.X) > 0.1f)
                    {
                        float until = (keeper.Pos.X - Ball.Pos.X) / Ball.Vel.X;
                        if (until > 0f)
                        {
                            lateral = MathF.Abs(Ball.Pos.Y + Ball.Vel.Y * until - keeper.Pos.Y);
                        }
                    }

                    float above = MathF.Max(0f, Ball.Pos.Z - 2f * c.KeeperReachHeight);
                    float fromBody = MathF.Sqrt(lateral * lateral + above * above);
                    // Colocación: cuánto tuvo que desplazarse desde donde estaba al patear (la estirada lo acerca al balón,
                    // pero un remate al palo sigue siendo más difícil que uno al centro).
                    float placement = _shotLive ? MathF.Abs(Ball.Pos.Y - _keeperYAtShot) / MathF.Max(Pitch.GoalHalfWidth, 0.1f) : 0f;
                    float edge = MathUtil.Clamp01(MathF.Max(fromBody / reach, placement));
                    AttemptSave(keeper, speed, edge, skill, time);
                }
            }
        }

        /// <summary>Distancia desde la que se pateó el remate en curso (si no hay remate, un valor medio).</summary>
        private float ShotDistanceForKeeper()
        {
            const float NoShotDistance = 15f;
            return _shotLive && _shotCount > 0 ? _shots[_shotCount - 1].Distance : NoShotDistance;
        }

        /// <summary>
        /// Tirada de atajada según el tiempo que tuvo el arquero (distancia del remate ÷ velocidad): con poco tiempo
        /// (mano a mano, remate potente en el área) pesan la urgencia y lo lejos del cuerpo que pasa (pegado al palo); con
        /// mucho tiempo (remate lejano o lento) llega cómodo. También cuentan su nivel y la velocidad del balón.
        /// </summary>
        private void AttemptSave(MatchPlayer keeper, float speed, float edgeFraction, float skill, float time)
        {
            PlayerConfig c = Config.Player;
            float urgency = MathUtil.Clamp01(c.KeeperReactionTime / MathF.Max(time, 0.01f));
            float comfort = MathUtil.Clamp01((time - c.KeeperComfortTime) / c.KeeperComfortRange);
            float chance = c.KeeperSaveBase + c.KeeperSaveSkillSpread * (skill - 0.5f) * 2f
                           - c.KeeperSaveSpeedPenalty * MathF.Max(0f, speed - c.KeeperSaveSpeedRef)
                           - c.KeeperSaveEdgePenalty * edgeFraction * urgency
                           - c.KeeperUrgencyPenalty * urgency * (c.KeeperUrgencyBodyShare + (1f - c.KeeperUrgencyBodyShare) * edgeFraction)
                           + c.KeeperComfortBonus * comfort;
            chance = MathUtil.Clamp(chance, MinSaveChance, MaxSaveChance);
            if (_rng.NextFloat() >= chance)
            {
                _beatenKeeper = keeper;
                return;
            }

            if (_shotLive)
            {
                Teams[keeper.Team].Stats.Saves++;
                keeper.Stats.Saves++;
                CountShotOnTarget(); // si salió desviado y lo ataja igual, también fue al arco
                AddEvent(MatchEventType.Save, keeper.Team, keeper.Index, Ball.Pos.Horizontal);
            }
            keeper.Action = PlayerAction.Dive;
            keeper.ActionDisplayTimer = ActionDisplay;
            keeper.DiveHeight = Ball.Pos.Z;

            float handling = keeper.Skill(AttributeId.Handling);
            if (speed <= c.KeeperCatchMaxSpeed * (0.8f + 0.4f * handling) && Ball.Pos.Z < CatchHeight)
            {
                GainPossession(keeper);
                return;
            }

            float end = Teams[keeper.Team].DefendedEnd;
            if (_rng.NextFloat() < ParryOverBarChance && TryParryOverTheBar(end, out Vec3 overTheBar))
            {
                // Desvía por encima del larguero: el balón sale por la línea de fondo y es córner.
                Ball.Vel = overTheBar;
                Ball.Spin = 0f;
                OnTouch(keeper, false);
                keeper.KickTimer = BlockLockout;
                return;
            }

            float lateral = _rng.NextGaussian(0f, ParryLateralSpeed);
            Ball.Vel = new Vec3(-end * speed * ParryRetainFactor, lateral, ParryLift * _rng.NextFloat());
            Ball.Spin = 0f;
            OnTouch(keeper, false);
            keeper.KickTimer = BlockLockout;
        }

        /// <summary>
        /// Busca un manotazo que pase el balón por encima del larguero: debe estar por encima del travesaño mientras cruza la
        /// línea y caer detrás de ella. Devuelve false si desde donde está el balón no es posible (por ejemplo, pegado a la
        /// línea y a media altura): entonces el arquero lo desvía hacia el campo.
        /// </summary>
        private bool TryParryOverTheBar(float end, out Vec3 velocity)
        {
            BallConfig b = Config.Ball;
            PlayerConfig c = Config.Player;
            float g = b.Gravity;
            float toLine = Pitch.HalfLength - end * Ball.Pos.X;
            float contact = b.Radius + Config.Pitch.PostRadius;
            float height = Config.Pitch.GoalHeight + contact + c.ParryBarMargin - Ball.Pos.Z; // altura a superar
            for (float lift = c.ParryOverBarLift; lift <= c.ParryOverBarMaxLift; lift += ParryLiftStep)
            {
                float disc = lift * lift - 2f * g * height;
                if (disc < 0f)
                {
                    continue;
                }

                float root = MathF.Sqrt(disc);
                float up = height <= 0f ? 0f : (lift - root) / g;   // momento en que supera la altura del larguero
                float down = (lift + root) / g;                    // momento en que vuelve a bajar de ella
                float fastest = (toLine + contact + c.ParryLineMargin) / down; // debe pasar la línea antes de bajar
                float slowest = up > 0f ? MathF.Max(0f, toLine - contact) / up : float.MaxValue; // y no llegar antes de subir
                if (fastest <= slowest)
                {
                    float forward = MathF.Min(MathF.Max(c.ParryOverBarForward, fastest), slowest);
                    velocity = new Vec3(end * forward, 0f, lift);
                    return true;
                }
            }

            velocity = Vec3.Zero;
            return false;
        }

        private MatchPlayer _cutTracked;

        /// <summary>Detecta los recortes del poseedor: un giro brusco a velocidad deja atrás por un instante al defensor.</summary>
        private void TrackDribbleCut(float dt)
        {
            MatchPlayer o = Owner;
            RulesConfig rules = Config.Rules;
            if (o == null)
            {
                _cutTracked = null;
                return;
            }

            if (o != _cutTracked)
            {
                _cutTracked = o;
                o.HeadingSample = o.Heading;
                o.CutSampleTimer = rules.CutSampleInterval;
                o.CutTimer = 0f;
                return;
            }

            o.CutTimer = MathF.Max(0f, o.CutTimer - dt);
            o.CutSampleTimer -= dt;
            if (o.CutSampleTimer > 0f)
            {
                return;
            }

            float turn = MathF.Abs(MathUtil.AngleDiff(o.Heading, o.HeadingSample));
            if (turn >= rules.CutAngleDeg * MathUtil.Deg2Rad && o.Speed >= rules.CutMinSpeed)
            {
                o.CutTimer = rules.CutWindow;
            }

            o.HeadingSample = o.Heading;
            o.CutSampleTimer = rules.CutSampleInterval;
        }

        /// <summary>Probabilidad de que <paramref name="tackler"/> le gane el balón a <paramref name="owner"/> desde donde está ahora.</summary>
        private float ContextTackleChance(MatchPlayer tackler, MatchPlayer owner, out bool fromBehind) =>
            ContextTackleChance(tackler, owner, out fromBehind, out _);

        private float ContextTackleChance(MatchPlayer tackler, MatchPlayer owner, out bool fromBehind, out bool fromFront)
        {
            RulesConfig rules = Config.Rules;
            Vec2 toOwner = (owner.Pos - tackler.Pos).Normalized;
            float facing = Vec2.Dot(Vec2.FromAngle(tackler.Heading), toOwner);
            float front = -Vec2.Dot(Vec2.FromAngle(owner.Heading), toOwner);
            float cut = rules.CutWindow > 0f ? owner.CutTimer / rules.CutWindow : 0f;
            fromBehind = Rules.IsFromBehind(rules, front);
            fromFront = front >= rules.FrontThreshold && facing >= rules.FrontThreshold;
            return Rules.TackleWinChance(rules, tackler.Skill(AttributeId.Tackling), owner.Skill(AttributeId.Dribbling), facing, front, cut);
        }

        private void ResolveTackles()
        {
            MatchPlayer owner = Owner;
            if (owner == null || KeeperHolding(owner))
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

                bool human = Teams[p.Team].IsHuman && Teams[p.Team].Controlled == p;
                p.TackleTimer = human ? rules.HumanTackleCooldown : rules.TackleCooldown;
                p.WantsTackle = false;
                p.TackleWindow = 0f;
                p.Action = PlayerAction.Tackle;
                p.ActionDisplayTimer = ActionDisplay;

                Teams[p.Team].Stats.Tackles++;
                p.Stats.Tackles++;
                float tackling = p.Skill(AttributeId.Tackling);
                float winChance = ContextTackleChance(p, owner, out bool behind, out bool front) + (human ? rules.HumanTackleBonus : 0f);
                // De frente y mirando al balón es una entrada limpia casi siempre; por detrás, falta fácil.
                float foulChance = Rules.FoulChance(rules, tackling, p.Skill(AttributeId.Aggression))
                                   * (behind ? rules.StandingBehindFoulMultiplier : front ? rules.FrontTackleFoulMultiplier : 1f);
                TackleOutcome outcome = Rules.ResolveTackle(_rng.NextFloat(), _rng.NextFloat(), foulChance, winChance);

                if (outcome == TackleOutcome.Foul)
                {
                    CommitFoul(p, owner);
                    return;
                }

                if (outcome == TackleOutcome.Won)
                {
                    Teams[p.Team].Stats.TacklesWon++;
                    p.Stats.TacklesWon++;
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
            p.Action = PlayerAction.Slide;
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
                if (owner != null && owner.Team != p.Team && !KeeperHolding(owner))
                {
                    if (Vec2.Distance(p.Pos, owner.Pos) > rules.SlideReach)
                    {
                        continue;
                    }

                    p.SlideResolved = true;
                    Teams[p.Team].Stats.Tackles++;
                    p.Stats.Tackles++;
                    float win = MathUtil.Clamp(ContextTackleChance(p, owner, out bool fromBehind) + rules.SlideWinBonus, 0f, 0.95f);
                    float foul = Rules.FoulChance(rules, p.Skill(AttributeId.Tackling), p.Skill(AttributeId.Aggression))
                                 * rules.SlideFoulMultiplier * (fromBehind ? rules.BehindFoulMultiplier : 1f);
                    TackleOutcome outcome = Rules.ResolveTackle(_rng.NextFloat(), _rng.NextFloat(), foul, win);
                    if (outcome == TackleOutcome.Foul)
                    {
                        CommitFoul(p, owner);
                        return;
                    }

                    if (outcome == TackleOutcome.Won)
                    {
                        Teams[p.Team].Stats.TacklesWon++;
                        p.Stats.TacklesWon++;
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
            if (OnTouch(p, true))
            {
                return;
            }

            Vec2 direction = Vec2.FromAngle(p.Heading).Rotated(_rng.NextGaussian(0f, Config.Player.DeflectionNoise * 0.5f));
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

            if (OnTouch(tackler, true))
            {
                return;
            }

            Vec2 away = (victim.Pos - tackler.Pos).Normalized;
            if (away.LengthSquared < MathUtil.Epsilon)
            {
                away = new Vec2(1f, 0f);
            }

            Vec2 direction = away.Rotated(_rng.NextGaussian(0f, Config.Player.DeflectionNoise));
            Ball.Vel = new Vec3(direction.X * TackleKnockSpeed, direction.Y * TackleKnockSpeed, 0f);
            Ball.Pos = new Vec3(Ball.Pos.X, Ball.Pos.Y, 0f);
        }

        private void CommitFoul(MatchPlayer fouler, MatchPlayer victim)
        {
            TeamState team = Teams[fouler.Team];
            team.Stats.Fouls++;
            fouler.Stats.Fouls++;
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
                fouler.Stats.Yellows++;
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
            p.MinuteOff = Minute;
            p.Stats.Reds++;
            Teams[p.Team].Stats.Reds++;
            AddEvent(MatchEventType.RedCard, p.Team, p.Index, at);
            if (Teams[p.Team].Controlled == p)
            {
                Teams[p.Team].Controlled = null;
            }
        }

        /// <summary>
        /// Cancha cerrada: el balón rebota en las paredes de las bandas y del fondo; solo sale por la boca de la portería
        /// (gol). Se comprueba antes de los límites, así nunca hay saques de banda, de esquina ni de puerta.
        /// </summary>
        private void BounceOffWalls()
        {
            PitchConfig c = Config.Pitch;
            if (!c.Walls)
            {
                return;
            }

            float r = Config.Ball.Radius;
            Vec3 pos = Ball.Pos;
            Vec3 vel = Ball.Vel;
            bool bounced = false;
            float sideLimit = Pitch.HalfWidth - r;
            if (MathF.Abs(pos.Y) > sideLimit)
            {
                float sign = MathF.Sign(pos.Y);
                pos = new Vec3(pos.X, sign * sideLimit, pos.Z);
                if (vel.Y * sign > 0f)
                {
                    vel = new Vec3(vel.X, -vel.Y * c.WallRestitution, vel.Z);
                }

                bounced = true;
            }

            float endLimit = Pitch.HalfLength - r;
            bool inMouth = MathF.Abs(pos.Y) < Pitch.GoalHalfWidth - r && pos.Z < Pitch.GoalHeight - r;
            if (MathF.Abs(pos.X) > endLimit && !inMouth)
            {
                float sign = MathF.Sign(pos.X);
                pos = new Vec3(sign * endLimit, pos.Y, pos.Z);
                if (vel.X * sign > 0f)
                {
                    vel = new Vec3(-vel.X * c.WallRestitution, vel.Y, vel.Z);
                }

                bounced = true;
            }

            if (!bounced)
            {
                return;
            }

            Ball.Pos = pos;
            Ball.Vel = vel;
            if (Owner != null)
            {
                // Conduciendo contra la pared: el balón queda pegado a ella y el jugador lo sigue teniendo.
                Ball.Vel = Vec3.Zero;
            }

            _prevBallPos = pos; // el rebote no cuenta como cruce de línea
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

        /// <summary>
        /// Autor del gol: el último que tocó el balón si es del equipo que marca. Si lo último fue un rebote o una parada de un
        /// rival, el gol es del último atacante que pateó; solo es en contra si el rival lo metió con un golpeo propio.
        /// </summary>
        private int ScorerIndex(int scoring)
        {
            MatchPlayer last = LastToucher;
            if (last == null)
            {
                return -1;
            }

            if (last.Team == scoring || last == _lastKicker)
            {
                return last.Index;
            }

            MatchPlayer attacker = _lastKickerOfTeam[scoring];
            return attacker != null ? attacker.Index : last.Index;
        }

        private void ScoreGoal(float endSign, Vec2 at)
        {
            int scoring = Teams[0].AttackDirection == endSign ? 0 : 1;
            Teams[scoring].Stats.Goals++;
            if (_shotLive)
            {
                CountShotOnTarget();
                if (_shotCount > 0)
                {
                    _shots[_shotCount - 1].Goal = true;
                }
            }

            _offsideActive = false;
            int scorer = ScorerIndex(scoring);
            CreditGoal(scoring, scorer);
            _celebrator = scorer >= 0 && Players[scorer].Team == scoring ? Players[scorer] : null;
            AddEvent(MatchEventType.Goal, scoring, scorer, at);
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

        private MatchPlayer _celebrator;

        /// <summary>Jugador que festeja el último gol (corre hacia el banderín más cercano durante la pausa), o null.</summary>
        public MatchPlayer Celebrator => Phase == MatchPhase.GoalPause ? _celebrator : null;

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
