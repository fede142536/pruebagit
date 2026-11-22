using Football.Core.Common;
using Football.Core.Domain;

namespace Football.Core.MatchCore
{
    // Director técnico en vivo: doble dibujo (con y sin balón), gritos desde el banco, charla del entretiempo y jugadas de
    // pelota parada. Todo cambia datos del equipo que la IA ya usa (estilo, puestos); no hay ramas de «trampa».
    public sealed partial class MatchEngine
    {
        /// <summary>
        /// Formación que el equipo adopta sin balón (por ejemplo, 4-3-3 con balón y 4-4-2 sin él). Debe tener los mismos
        /// puestos que la formación con balón; cada jugador conserva su puesto (índice) en las dos.
        /// </summary>
        /// <param name="team">Equipo.</param>
        /// <param name="formation">Formación sin balón, o null para usar la misma que con balón.</param>
        /// <returns>Falso si la cantidad de puestos no coincide.</returns>
        public bool SetDefensiveFormation(int team, Formation formation)
        {
            TeamState t = Teams[team];
            if (formation != null && formation.Slots.Length != t.Formation.Slots.Length)
            {
                return false;
            }

            t.DefensiveFormation = formation;
            _planTimer = 0f;
            return true;
        }

        /// <summary>Elige las jugadas de pelota parada de un equipo.</summary>
        /// <param name="team">Equipo.</param>
        /// <param name="corners">Jugada de córner a favor.</param>
        /// <param name="marking">Marcaje en contra.</param>
        public void SetSetPieces(int team, CornerRoutine corners, SetPieceMarking marking)
        {
            Teams[team].Corners = corners;
            Teams[team].Marking = marking;
        }

        /// <summary>Grito táctico desde el banco: cambia el estilo durante <see cref="CoachConfig.ShoutSeconds"/>.</summary>
        /// <param name="team">Equipo.</param>
        /// <param name="shout">Grito (<see cref="TouchlineShout.None"/> lo cancela).</param>
        public void Shout(int team, TouchlineShout shout)
        {
            TeamState t = Teams[team];
            t.Shout = shout;
            t.ShoutTimer = shout == TouchlineShout.None ? 0f : Config.Coach.ShoutSeconds;
            RefreshStyle(t);
            AddEvent(MatchEventType.TacticsChanged, team, -1, Ball.Pos.Horizontal);
        }

        /// <summary>
        /// Charla del entretiempo (solo durante el descanso, una vez). El efecto depende del marcador: calmado sirve ganando,
        /// apasionado perdiendo, exigente empatando; con una goleada en contra, la exigencia sale mal.
        /// </summary>
        /// <param name="team">Equipo.</param>
        /// <param name="talk">Tono.</param>
        /// <param name="reaction">Cómo la recibió el plantel.</param>
        /// <returns>Falso si no es el entretiempo o ya se dio la charla.</returns>
        public bool GiveTeamTalk(int team, TeamTalk talk, out TalkReaction reaction)
        {
            reaction = TalkReaction.Neutral;
            TeamState t = Teams[team];
            if (!_halfTimeBreak || t.TalkGiven)
            {
                return false;
            }

            int difference = t.Stats.Goals - Teams[1 - team].Stats.Goals;
            t.TalkBoost = TalkEffect(talk, difference);
            t.TalkGiven = true;
            reaction = t.TalkBoost > 0.2f ? TalkReaction.Positive : t.TalkBoost < -0.2f ? TalkReaction.Negative : TalkReaction.Neutral;
            RefreshStyle(t);
            AddEvent(MatchEventType.TacticsChanged, team, -1, Ball.Pos.Horizontal);
            return true;
        }

        /// <summary>Efecto de una charla según la diferencia de goles (−1 a +1).</summary>
        /// <param name="talk">Tono.</param>
        /// <param name="goalDifference">Goles propios menos ajenos.</param>
        public static float TalkEffect(TeamTalk talk, int goalDifference)
        {
            switch (talk)
            {
                case TeamTalk.Calm:
                    return goalDifference > 0 ? 1f : goalDifference == 0 ? 0.5f : -0.5f;
                case TeamTalk.Passionate:
                    return goalDifference < 0 ? 1f : goalDifference == 0 ? 0.5f : 0f;
                default:
                    return goalDifference == 0 ? 0.8f : goalDifference > 0 ? 0.5f : goalDifference == -1 ? 0.3f : -1f;
            }
        }

        /// <summary>La IA da su charla al empezar el descanso: apasionada si pierde, calmada si gana, exigente si empata.</summary>
        private void AiTeamTalks()
        {
            for (int t = 0; t < 2; t++)
            {
                TeamState team = Teams[t];
                if (team.IsHuman || team.TalkGiven)
                {
                    continue;
                }

                int difference = team.Stats.Goals - Teams[1 - t].Stats.Goals;
                TeamTalk talk = difference < 0 ? TeamTalk.Passionate : difference > 0 ? TeamTalk.Calm : TeamTalk.Demanding;
                GiveTeamTalk(t, talk, out _);
            }
        }

        /// <summary>Los gritos se apagan solos al pasar su tiempo.</summary>
        private void UpdateShouts(float dt)
        {
            for (int t = 0; t < 2; t++)
            {
                TeamState team = Teams[t];
                if (team.Shout == TouchlineShout.None)
                {
                    continue;
                }

                team.ShoutTimer -= dt;
                if (team.ShoutTimer <= 0f)
                {
                    team.Shout = TouchlineShout.None;
                    RefreshStyle(team);
                }
            }
        }

        /// <summary>Recalcula el estilo efectivo: base + mentalidad y presión + grito + charla.</summary>
        private void RefreshStyle(TeamState t)
        {
            Tactics.Apply(t.BaseStyle, t.Mentality, t.Pressing, Config.Ai, t.Style);
            CoachConfig c = Config.Coach;
            TeamStyle s = t.Style;
            float step = c.ShoutStep;
            switch (t.Shout)
            {
                case TouchlineShout.TakeOn:
                    s.Risk = MathUtil.Clamp01(s.Risk + step);
                    s.Width = MathUtil.Clamp01(s.Width + step * 0.5f);
                    break;
                case TouchlineShout.ShortPassing:
                    s.Directness = MathUtil.Clamp01(s.Directness - step);
                    s.Risk = MathUtil.Clamp01(s.Risk - step * 0.5f);
                    s.Compactness = MathUtil.Clamp01(s.Compactness + step * 0.3f);
                    break;
                case TouchlineShout.WasteTime:
                    s.Directness = MathUtil.Clamp01(s.Directness - step);
                    s.Risk = MathUtil.Clamp01(s.Risk - step);
                    s.LineHeight = MathUtil.Clamp01(s.LineHeight - step * 0.5f);
                    break;
                case TouchlineShout.CounterPress:
                    s.PressIntensity = MathUtil.Clamp01(s.PressIntensity + step);
                    s.LineHeight = MathUtil.Clamp01(s.LineHeight + step * 0.3f);
                    break;
                case TouchlineShout.FindStriker:
                    s.Directness = MathUtil.Clamp01(s.Directness + step);
                    s.Risk = MathUtil.Clamp01(s.Risk + step * 0.3f);
                    break;
            }

            s.PressIntensity = MathUtil.Clamp01(s.PressIntensity + c.TalkPressStep * t.TalkBoost);
        }

        /// <summary>Multiplicador del error de golpeo por la charla del entretiempo (menos de 1 = más preciso).</summary>
        private float TalkError(MatchPlayer p) => 1f - Config.Coach.TalkErrorStep * Teams[p.Team].TalkBoost;

        /// <summary>Puesto en el que se coloca el jugador: con balón, el de la formación; sin balón, el del doble dibujo.</summary>
        private FormationSlot SlotFor(TeamState team, MatchPlayer p, PlayState state)
        {
            Formation f = state != PlayState.Attacking && team.DefensiveFormation != null ? team.DefensiveFormation : team.Formation;
            return f.Slots[p.Spot];
        }

        /// <summary>El jugador que sale a recibir el córner corto: el primero de campo (desde el puesto 5) que no saca.</summary>
        private MatchPlayer ShortCornerPlayer(TeamState team)
        {
            for (int i = 5; i < team.Players.Length; i++)
            {
                MatchPlayer p = team.Players[i];
                if (!p.SentOff && p != _taker)
                {
                    return p;
                }
            }

            return null;
        }

        /// <summary>Lugar de un atacante en el área según la jugada de córner (o tiro libre cercano) de su equipo.</summary>
        private Vec2 AttackingBoxSpot(TeamState team, MatchPlayer p, Restart r, float goalSign)
        {
            float goalX = goalSign * Pitch.HalfLength;
            bool corner = r.Kind == RestartKind.Corner;
            CornerRoutine routine = corner ? team.Corners : CornerRoutine.Mixed;
            float postSide = r.Position.Y >= 0f ? 1f : -1f;
            switch (routine)
            {
                case CornerRoutine.NearPost:
                    return new Vec2(goalX - goalSign * (BoxAttackDepth + BoxStep * (p.Slot % 3)),
                        postSide * (Pitch.GoalHalfWidth + ((p.Slot % 3) - 1) * NearPostSpread));
                case CornerRoutine.PenaltySpot:
                    return new Vec2(goalX - goalSign * (Config.Pitch.PenaltySpotDistance + ((p.Slot % 3) - 1) * SpotSpread),
                        ((p.Slot * 7) % 5 - 2) * SpotSpread);
                case CornerRoutine.Short when p == ShortCornerPlayer(team):
                    return Pitch.Clamp(r.Position + new Vec2(-goalSign * ShortCornerBack, -postSide * ShortCornerInward), TargetMargin);
                default:
                    return new Vec2(goalSign * (Pitch.HalfLength - BoxAttackDepth - BoxStep * (p.Slot % 3)), ((p.Slot * 7) % 9 - 4) * 3.2f);
            }
        }

        /// <summary>Lugar de un defensor en su área: por zonas (sectores fijos) o marcando a su rival del mismo puesto.</summary>
        private Vec2 DefendingBoxSpot(TeamState team, MatchPlayer p, TeamState attackers, float goalSign)
        {
            if (team.Marking == SetPieceMarking.ManToMan && p.Slot < attackers.Players.Length)
            {
                MatchPlayer rival = attackers.Players[p.Slot];
                if (!rival.SentOff && rival != _taker)
                {
                    Vec2 goal = Pitch.GoalCenter(team.DefendedEnd);
                    return rival.Pos + (goal - rival.Pos).Normalized * ManMarkDistance;
                }
            }

            return new Vec2(goalSign * (Pitch.HalfLength - BoxDefendDepth - BoxStep * (p.Slot % 3)), ((p.Slot * 5) % 9 - 4) * 2.8f);
        }

        private const float NearPostSpread = 1.6f;
        private const float SpotSpread = 1.6f;
        private const float ShortCornerBack = 4f;
        private const float ShortCornerInward = 6f;
        private const float ManMarkDistance = 1f;
    }
}
