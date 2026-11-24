using System;
using Football.Core.Common;
using Football.Core.Domain;

namespace Football.Core.MatchCore
{
    /// <summary>Resultado de pedir un cambio.</summary>
    public enum SubstitutionResult
    {
        /// <summary>Aceptado: se hace en el próximo parón (o ya, si el juego está detenido).</summary>
        Accepted,
        /// <summary>Ya se hicieron (o pidieron) los cambios máximos.</summary>
        NoSubstitutionsLeft,
        /// <summary>Se usaron todas las ventanas de cambios (fuera del entretiempo).</summary>
        NoWindowsLeft,
        /// <summary>Ese suplente ya entró o ya está pedido.</summary>
        PlayerUnavailable,
        /// <summary>El jugador que sale no está en el campo (expulsado, ya sustituido o ya pedido).</summary>
        InvalidOutgoing
    }

    // Gestión de la plantilla durante el partido: cambios con las reglas oficiales (5 cambios en 3 ventanas, los del
    // entretiempo no gastan ventana, nadie vuelve a entrar) y formaciones o puestos cambiados en vivo.
    public sealed partial class MatchEngine
    {
        private const int MaxPending = 11;
        private readonly int[] _pendingOut = new int[2 * MaxPending];   // índice global del que sale
        private readonly int[] _pendingIn = new int[2 * MaxPending];    // índice en el banco del que entra
        private readonly int[] _pendingCount = new int[2];
        private readonly bool[] _windowOpen = new bool[2];             // en este parón ya se abrió una ventana
        private bool _halfTimeBreak;
        private float _aiSubTimer;

        /// <summary>Cambios que le quedan a un equipo (descontando los pedidos).</summary>
        public int SubstitutionsLeft(int team) =>
            Config.Rules.MaxSubstitutions - Teams[team].SubstitutionsUsed - _pendingCount[team];

        /// <summary>Ventanas de cambios que le quedan a un equipo (sin contar el entretiempo).</summary>
        public int WindowsLeft(int team) => Config.Rules.MaxSubstitutionWindows - Teams[team].WindowsUsed;

        /// <summary>El partido está en el descanso (los cambios no gastan ventana).</summary>
        public bool InHalfTimeBreak => _halfTimeBreak;

        /// <summary>Cambios pedidos que se harán en el próximo parón.</summary>
        public int PendingSubstitutions(int team) => _pendingCount[team];

        /// <summary>
        /// Pide un cambio: sale <paramref name="outgoing"/> y entra el suplente <paramref name="benchIndex"/>. Con el juego en
        /// marcha se hace en el próximo parón (lateral, saque de arco, córner, falta, gol o descanso); con el juego detenido, ya.
        /// </summary>
        /// <param name="team">Equipo.</param>
        /// <param name="outgoing">Jugador en el campo que sale.</param>
        /// <param name="benchIndex">Índice en <see cref="TeamState.Bench"/> del que entra.</param>
        public SubstitutionResult RequestSubstitution(int team, MatchPlayer outgoing, int benchIndex)
        {
            TeamState t = Teams[team];
            if (outgoing == null || outgoing.Team != team || outgoing.SentOff || t.Players[outgoing.Slot] != outgoing
                || IsPendingOut(team, outgoing.Index))
            {
                return SubstitutionResult.InvalidOutgoing;
            }

            if (benchIndex < 0 || benchIndex >= t.Bench.Length || t.BenchUsed[benchIndex])
            {
                return SubstitutionResult.PlayerUnavailable;
            }

            if (SubstitutionsLeft(team) <= 0)
            {
                return SubstitutionResult.NoSubstitutionsLeft;
            }

            // Una ventana nueva solo se abre si en este parón no hay ya una abierta (y nunca en el descanso).
            bool needsWindow = !_halfTimeBreak && !_windowOpen[team] && _pendingCount[team] == 0;
            if (needsWindow && WindowsLeft(team) <= 0)
            {
                return SubstitutionResult.NoWindowsLeft;
            }

            int k = team * MaxPending + _pendingCount[team];
            _pendingOut[k] = outgoing.Index;
            _pendingIn[k] = benchIndex;
            _pendingCount[team]++;
            t.BenchUsed[benchIndex] = true;
            if (Phase != MatchPhase.Playing || AwaitingSecondHalf)
            {
                ApplySubstitutions(team); // en el descanso el cambio se hace en el acto
            }

            return SubstitutionResult.Accepted;
        }

        /// <summary>Cancela los cambios pedidos que aún no se hicieron (el suplente vuelve a estar disponible).</summary>
        public void CancelPendingSubstitutions(int team)
        {
            for (int i = 0; i < _pendingCount[team]; i++)
            {
                Teams[team].BenchUsed[_pendingIn[team * MaxPending + i]] = false;
            }

            _pendingCount[team] = 0;
        }

        /// <summary>Suplente que entrará por un jugador de campo con el cambio ya pedido (null si no hay cambio pedido para él).</summary>
        /// <param name="team">Equipo.</param>
        /// <param name="outgoingIndex">Índice global del jugador que sale.</param>
        public PlayerMatchView PendingIncoming(int team, int outgoingIndex)
        {
            for (int i = 0; i < _pendingCount[team]; i++)
            {
                if (_pendingOut[team * MaxPending + i] == outgoingIndex)
                {
                    return Teams[team].Bench[_pendingIn[team * MaxPending + i]];
                }
            }

            return null;
        }

        /// <summary>Indica si un suplente del banco ya fue pedido y entrará en el próximo parón.</summary>
        /// <param name="team">Equipo.</param>
        /// <param name="benchIndex">Posición en el banco.</param>
        public bool IsPendingIn(int team, int benchIndex)
        {
            for (int i = 0; i < _pendingCount[team]; i++)
            {
                if (_pendingIn[team * MaxPending + i] == benchIndex)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Indica si un jugador de campo ya tiene pedido su cambio (sale en el próximo parón).</summary>
        /// <param name="team">Equipo.</param>
        /// <param name="playerIndex">Índice global del jugador.</param>
        public bool IsPendingOut(int team, int playerIndex)
        {
            for (int i = 0; i < _pendingCount[team]; i++)
            {
                if (_pendingOut[team * MaxPending + i] == playerIndex)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Cambia la formación en vivo: cada jugador conserva su puesto táctico (índice) y toma el rol del nuevo dibujo; la IA
        /// recalcula enseguida sus zonas base. Debe tener la misma cantidad de puestos.
        /// </summary>
        public bool SetFormation(int team, Formation formation)
        {
            TeamState t = Teams[team];
            if (formation == null || formation.Slots.Length != t.Players.Length)
            {
                return false;
            }

            t.Formation = formation;
            for (int i = 0; i < t.Players.Length; i++)
            {
                MatchPlayer p = t.Players[i];
                p.Role = formation.Slots[p.Spot].Role;
            }

            _planTimer = 0f;
            AddEvent(MatchEventType.TacticsChanged, team, -1, Ball.Pos.Horizontal);
            return true;
        }

        /// <summary>Elige el rol de un puesto de la formación (vale para quien ocupe el puesto, también tras un cambio).</summary>
        /// <param name="team">Equipo.</param>
        /// <param name="spot">Puesto (índice en la formación; el 0 es el arquero y no tiene roles).</param>
        /// <param name="duty">Rol; <see cref="PlayerDuty.Auto"/> vuelve al del estilo.</param>
        /// <returns>Falso si el puesto no existe o no admite ese rol.</returns>
        public bool SetDuty(int team, int spot, PlayerDuty duty)
        {
            TeamState t = Teams[team];
            if (spot <= 0 || spot >= t.Duties.Length || !Tactics.Allows(t.Formation.Slots[spot].Role, duty))
            {
                return false;
            }

            t.Duties[spot] = duty;
            _planTimer = 0f; // se recolocan en el próximo paso
            return true;
        }

        /// <summary>Rol con el que juega ahora un jugador (el elegido o, si no hay, el que le da el estilo).</summary>
        /// <param name="p">Jugador.</param>
        public PlayerDuty DutyOf(MatchPlayer p) => Tactics.Effective(p.Role, Teams[p.Team].Duties[p.Spot], Teams[p.Team].Style);

        /// <summary>
        /// Intercambia los puestos tácticos de dos jugadores de campo del mismo equipo (por ejemplo, un extremo pasa a
        /// mediapunta). El arquero no se intercambia.
        /// </summary>
        public bool SwapPositions(MatchPlayer a, MatchPlayer b)
        {
            if (a == null || b == null || a == b || a.Team != b.Team || a.Spot == 0 || b.Spot == 0)
            {
                return false;
            }

            Formation f = Teams[a.Team].Formation;
            int spot = a.Spot;
            a.Spot = b.Spot;
            b.Spot = spot;
            a.Role = f.Slots[a.Spot].Role;
            b.Role = f.Slots[b.Spot].Role;
            _planTimer = 0f;
            return true;
        }

        /// <summary>Hace los cambios pedidos (con el juego detenido).</summary>
        private void ApplySubstitutions(int team)
        {
            int count = _pendingCount[team];
            if (count == 0)
            {
                return;
            }

            TeamState t = Teams[team];
            if (!_halfTimeBreak && !_windowOpen[team])
            {
                t.WindowsUsed++;
                _windowOpen[team] = true;
            }

            for (int i = 0; i < count; i++)
            {
                int k = team * MaxPending + i;
                MatchPlayer old = Players[_pendingOut[k]];
                PlayerMatchView view = t.Bench[_pendingIn[k]];
                var incoming = new MatchPlayer(old.Index, team, old.Slot, view, Config.Player)
                {
                    Spot = old.Spot,
                    Pos = old.Pos,
                    Heading = old.Heading,
                    Target = old.Pos,
                    Anchor = old.Anchor,
                    Yellows = 0
                };
                incoming.Role = t.Formation.Slots[incoming.Spot].Role; // juega en el puesto del que sale, con resistencia llena
                Replace(old, incoming);
                t.SubstitutedOut.Add(old.Name);
                old.MinuteOff = PlayedMinute;
                incoming.MinuteOn = PlayedMinute;
                t.Departed.Add(old);
                t.SubstitutionsUsed++;
                AddEvent(MatchEventType.Substitution, team, incoming.Index, incoming.Pos);
            }

            _pendingCount[team] = 0;
            _planTimer = 0f;
        }

        private void Replace(MatchPlayer old, MatchPlayer incoming)
        {
            TeamState t = Teams[old.Team];
            Players[old.Index] = incoming;
            t.Players[old.Slot] = incoming;
            if (t.Controlled == old) t.Controlled = incoming;
            if (t.LastPasser == old || t.LastReceiver == old) t.LastPasser = t.LastReceiver = null;
            if (t.Presser == old) t.Presser = null;
            if (t.Presser2 == old) t.Presser2 = null;
            if (t.SecondPresser == old) t.SecondPresser = null;
            if (t.Cover == old) t.Cover = null;
            if (t.Chaser == old) t.Chaser = null;
            if (_taker == old) _taker = incoming;
            if (Owner == old) Owner = null;
            for (int i = 0; i < Players.Length; i++)
            {
                if (Players[i].MarkTarget == old)
                {
                    Players[i].MarkTarget = null;
                }
            }
        }

        /// <summary>En cada parón se hacen los cambios pendientes; al volver a jugar, la ventana se cierra.</summary>
        private void UpdateSubstitutionWindows()
        {
            bool stopped = Phase == MatchPhase.SetPiece || Phase == MatchPhase.GoalPause;
            for (int t = 0; t < 2; t++)
            {
                if (stopped)
                {
                    ApplySubstitutions(t);
                }
                else if (Phase == MatchPhase.Playing)
                {
                    _windowOpen[t] = false;
                }
            }

            if (Phase == MatchPhase.Playing)
            {
                _halfTimeBreak = false;
            }
        }

        private float _nextInjuryCheck;
        private bool _injuryStop;

        /// <summary>Lesión leve: el jugador sigue más lento, se anota y el árbitro parará el juego.</summary>
        private void Injure(MatchPlayer p)
        {
            p.Injured = true;
            p.Stats.Injured = true;
            Teams[p.Team].Stats.Injuries++;
            AddEvent(MatchEventType.Injury, p.Team, p.Index, p.Pos);
            _injuryStop = true;
        }

        /// <summary>
        /// Lesiones leves por cansancio: desde <see cref="PlayerConfig.InjuryStartMinute"/>, una tirada por jugador de campo y
        /// minuto de juego, con probabilidad que crece con el cuadrado del cansancio. El lesionado sigue más lento hasta que lo
        /// cambien (la IA lo cambia en cuanto puede; la persona, desde PLANTEL Y TÁCTICA).
        /// </summary>
        private void CheckInjuries()
        {
            PlayerConfig c = Config.Player;
            if (Minute < _nextInjuryCheck)
            {
                return;
            }

            _nextInjuryCheck = MathF.Floor(Minute) + 1f;
            if (Minute < c.InjuryStartMinute)
            {
                return;
            }

            for (int i = 0; i < Players.Length; i++)
            {
                MatchPlayer p = Players[i];
                if (p.SentOff || p.Injured || p.IsKeeper)
                {
                    continue;
                }

                float fatigue = p.Fatigue;
                float chance = c.InjuryBaseRate * (1f + c.InjuryFatigueWeight * fatigue * fatigue);
                if (_rng.NextFloat() < chance)
                {
                    Injure(p);
                }
            }
        }

        /// <summary>
        /// Tras una lesión, el árbitro detiene el juego en cuanto no hay un remate en curso y lo reanuda con balón a tierra
        /// para el equipo que tenía la posesión (en un área, para el que la defiende). Así hay un parón para hacer el cambio.
        /// </summary>
        private void StopForInjury()
        {
            if (!_injuryStop || Phase != MatchPhase.Playing || _shotLive)
            {
                return;
            }

            _injuryStop = false;
            Vec2 at = Pitch.Clamp(Ball.Pos.Horizontal, InjuryRestartMargin);
            int team = _ballTeam >= 0 ? _ballTeam : 0;
            for (int t = 0; t < 2; t++)
            {
                if (Pitch.InPenaltyArea(Teams[t].DefendedEnd, at))
                {
                    team = t;
                }
            }

            StartSetPiece(new Restart(RestartKind.FreeKick, team, at));
        }

        private const float InjuryRestartMargin = 1f;

        /// <summary>
        /// Cambios de la IA: desde cierto minuto, saca al jugador de campo más cansado (o al lesionado) y mete al suplente
        /// mejor valorado de su misma zona (o el mejor disponible), respetando cupos y ventanas.
        /// </summary>
        private void AiSubstitutions(float dt)
        {
            _aiSubTimer -= dt;
            if (_aiSubTimer > 0f)
            {
                return;
            }

            _aiSubTimer = Config.Ai.AdaptInterval;
            RulesConfig r = Config.Rules;
            if (Minute < r.AiSubstitutionMinute)
            {
                return;
            }

            for (int team = 0; team < 2; team++)
            {
                TeamState t = Teams[team];
                if (t.IsHuman || SubstitutionsLeft(team) <= 0 || (WindowsLeft(team) <= 0 && !_windowOpen[team]))
                {
                    continue;
                }

                if (_pendingCount[team] > 0)
                {
                    continue; // un cambio por vez: espera a que se haga el pedido
                }

                MatchPlayer tired = null;
                for (int i = t.FirstOutfield; i < t.Players.Length; i++)
                {
                    MatchPlayer p = t.Players[i];
                    if (p.SentOff || IsPendingOut(team, p.Index))
                    {
                        continue;
                    }

                    if (p.Injured)
                    {
                        tired = p; // un lesionado sale antes que nadie
                        break;
                    }

                    if (tired == null || p.Stamina < tired.Stamina)
                    {
                        tired = p;
                    }
                }

                bool exhausted = tired != null && (tired.Injured || tired.Stamina < r.AiSubstitutionStamina);
                bool rotation = Minute >= r.AiRotationMinute && t.SubstitutionsUsed < r.AiRotationSubstitutions;
                int bench = tired != null && (exhausted || rotation) ? BestBenchFor(t, tired.Role) : -1;
                if (bench >= 0)
                {
                    RequestSubstitution(team, tired, bench);
                }
            }
        }

        private static int BestBenchFor(TeamState t, Position role)
        {
            int best = -1;
            int bestScore = int.MinValue;
            for (int i = 0; i < t.Bench.Length; i++)
            {
                if (t.BenchUsed[i] || t.Bench[i].Role == Position.Goalkeeper)
                {
                    continue;
                }

                int score = (t.Bench[i].Role == role ? 1000 : 0) + t.Bench[i].Attributes[AttributeId.Passing]
                            + t.Bench[i].Attributes[AttributeId.Pace];
                if (score > bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }

            return best;
        }
    }
}
