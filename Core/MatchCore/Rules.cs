using System;
using Football.Core.Common;

namespace Football.Core.MatchCore
{
    /// <summary>Qué ha ocurrido con el balón respecto a los límites del campo.</summary>
    public enum BoundaryKind
    {
        /// <summary>Sigue en juego.</summary>
        None,
        /// <summary>Gol.</summary>
        Goal,
        /// <summary>Salió por la línea de banda.</summary>
        Touchline,
        /// <summary>Salió por la línea de fondo, fuera de la portería.</summary>
        GoalLine
    }

    /// <summary>Resultado de comprobar los límites del campo.</summary>
    public struct BoundaryResult
    {
        /// <summary>Qué ocurrió.</summary>
        public BoundaryKind Kind;
        /// <summary>Extremo (+1 o -1) de la línea de fondo cruzada, si corresponde.</summary>
        public float EndSign;
        /// <summary>Punto por el que el balón cruzó la línea.</summary>
        public Vec2 Exit;
    }

    /// <summary>Resultado de una entrada.</summary>
    public enum TackleOutcome
    {
        /// <summary>El defensor falla y no toca el balón.</summary>
        Missed,
        /// <summary>El defensor gana el balón limpiamente.</summary>
        Won,
        /// <summary>El defensor comete falta.</summary>
        Foul
    }

    /// <summary>Tarjeta mostrada por una falta.</summary>
    public enum CardResult
    {
        /// <summary>Sin tarjeta.</summary>
        None,
        /// <summary>Amarilla.</summary>
        Yellow,
        /// <summary>Segunda amarilla: expulsión.</summary>
        SecondYellow,
        /// <summary>Roja directa.</summary>
        Red
    }

    /// <summary>Reglas del fútbol como funciones puras, sin estado: se prueban de forma aislada.</summary>
    public static class Rules
    {
        /// <summary>
        /// Comprueba si el balón se fue del campo o entró en la portería entre dos posiciones consecutivas.
        /// El balón debe haber cruzado la línea por completo (su centro más su radio).
        /// </summary>
        /// <param name="pitch">Campo.</param>
        /// <param name="previous">Posición en el paso anterior.</param>
        /// <param name="current">Posición actual.</param>
        /// <param name="ballRadius">Radio del balón.</param>
        public static BoundaryResult CheckBoundary(Pitch pitch, Vec3 previous, Vec3 current, float ballRadius)
        {
            for (int i = 0; i < 2; i++)
            {
                float end = i == 0 ? 1f : -1f;
                float plane = pitch.HalfLength + ballRadius;
                float before = end * previous.X - plane;
                float after = end * current.X - plane;
                if (after > 0f && before <= 0f)
                {
                    float t = before / (before - after);
                    float y = previous.Y + (current.Y - previous.Y) * t;
                    float z = previous.Z + (current.Z - previous.Z) * t;
                    var exit = new Vec2(end * pitch.HalfLength, y);
                    bool inGoal = MathF.Abs(y) < pitch.GoalHalfWidth && z < pitch.GoalHeight;
                    return new BoundaryResult { Kind = inGoal ? BoundaryKind.Goal : BoundaryKind.GoalLine, EndSign = end, Exit = exit };
                }
            }

            for (int i = 0; i < 2; i++)
            {
                float end = i == 0 ? 1f : -1f;
                if (end * current.X > pitch.HalfLength + ballRadius)
                {
                    // Ya estaba fuera por el fondo desde un paso anterior (p. ej. un balón que rueda detrás de la línea).
                    return new BoundaryResult { Kind = BoundaryKind.GoalLine, EndSign = end, Exit = new Vec2(end * pitch.HalfLength, current.Y) };
                }
            }

            float side = MathF.Sign(current.Y);
            if (MathF.Abs(current.Y) > pitch.HalfWidth + ballRadius)
            {
                float x = MathUtil.Clamp(current.X, -pitch.HalfLength, pitch.HalfLength);
                return new BoundaryResult { Kind = BoundaryKind.Touchline, Exit = new Vec2(x, side * pitch.HalfWidth) };
            }

            return new BoundaryResult { Kind = BoundaryKind.None };
        }

        /// <summary>Saque de banda: lo ejecuta el equipo contrario al último que tocó el balón.</summary>
        /// <param name="pitch">Campo.</param>
        /// <param name="exit">Punto por el que salió.</param>
        /// <param name="lastTouchTeam">Último equipo que tocó el balón.</param>
        /// <param name="cornerClearance">Distancia mínima a la línea de fondo del punto de saque.</param>
        public static Restart ThrowIn(Pitch pitch, Vec2 exit, int lastTouchTeam, float cornerClearance)
        {
            float x = MathUtil.Clamp(exit.X, -pitch.HalfLength + cornerClearance, pitch.HalfLength - cornerClearance);
            return new Restart(RestartKind.ThrowIn, 1 - lastTouchTeam, new Vec2(x, MathF.Sign(exit.Y) * pitch.HalfWidth));
        }

        /// <summary>
        /// Balón fuera por la línea de fondo: córner si lo tocó por última vez el equipo que defiende esa portería,
        /// saque de puerta del defensor en caso contrario.
        /// </summary>
        /// <param name="pitch">Campo.</param>
        /// <param name="endSign">Extremo cruzado.</param>
        /// <param name="exit">Punto por el que salió.</param>
        /// <param name="lastTouchTeam">Último equipo que tocó el balón.</param>
        /// <param name="defendingTeam">Equipo que defiende ese extremo.</param>
        public static Restart GoalLineOut(Pitch pitch, float endSign, Vec2 exit, int lastTouchTeam, int defendingTeam)
        {
            if (lastTouchTeam == defendingTeam)
            {
                float side = exit.Y >= 0f ? 1f : -1f;
                return new Restart(RestartKind.Corner, 1 - defendingTeam, new Vec2(endSign * pitch.HalfLength, side * pitch.HalfWidth));
            }

            return new Restart(RestartKind.GoalKick, defendingTeam,
                new Vec2(endSign * (pitch.HalfLength - pitch.Config.GoalAreaDepth), 0f));
        }

        /// <summary>Penalti si la falta fue dentro del área del infractor; tiro libre en el lugar de la falta en otro caso.</summary>
        /// <param name="pitch">Campo.</param>
        /// <param name="foulPosition">Lugar de la falta.</param>
        /// <param name="fouledTeam">Equipo que recibió la falta.</param>
        /// <param name="foulingEndSign">Extremo que defiende el equipo infractor.</param>
        public static Restart Foul(Pitch pitch, Vec2 foulPosition, int fouledTeam, float foulingEndSign)
        {
            if (pitch.InPenaltyArea(foulingEndSign, foulPosition))
            {
                return new Restart(RestartKind.Penalty, fouledTeam, pitch.PenaltySpot(foulingEndSign));
            }

            return new Restart(RestartKind.FreeKick, fouledTeam, pitch.Clamp(foulPosition, 0f));
        }

        /// <summary>
        /// Coordenada X de la línea de fuera de juego: la del penúltimo rival (el último suele ser el portero) medida
        /// en la dirección de ataque.
        /// </summary>
        /// <param name="attackDirection">Dirección de ataque del equipo (+1 o -1).</param>
        /// <param name="opponentsX">Coordenadas X de los rivales.</param>
        /// <param name="count">Cuántos rivales se consideran (los que siguen en el campo).</param>
        public static float OffsideLine(float attackDirection, float[] opponentsX, int count)
        {
            float last = float.MinValue;
            float secondLast = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                float v = attackDirection * opponentsX[i];
                if (v > last)
                {
                    secondLast = last;
                    last = v;
                }
                else if (v > secondLast)
                {
                    secondLast = v;
                }
            }

            if (count < 2)
            {
                return attackDirection * 1000f;
            }

            return attackDirection * secondLast;
        }

        /// <summary>Indica si un jugador está en posición de fuera de juego (estar a la par no lo es).</summary>
        /// <param name="attackDirection">Dirección de ataque del equipo.</param>
        /// <param name="playerX">Coordenada X del jugador.</param>
        /// <param name="ballX">Coordenada X del balón.</param>
        /// <param name="offsideLine">Línea de fuera de juego (ver <see cref="OffsideLine"/>).</param>
        public static bool IsOffsidePosition(float attackDirection, float playerX, float ballX, float offsideLine)
        {
            float p = attackDirection * playerX;
            return p > 0f && p > attackDirection * ballX && p > attackDirection * offsideLine;
        }

        /// <summary>Probabilidad de ganar el balón en una entrada.</summary>
        /// <param name="c">Reglas.</param>
        /// <param name="tackling">Habilidad de entrada del defensor (0..1).</param>
        /// <param name="dribbling">Habilidad de regate del poseedor (0..1).</param>
        public static float TackleWinChance(RulesConfig c, float tackling, float dribbling) =>
            MathUtil.Clamp(c.TackleWinBase + c.TackleSkillWeight * (tackling - dribbling), 0.05f, 0.85f);

        /// <summary>Probabilidad de que una entrada sea falta.</summary>
        /// <param name="c">Reglas.</param>
        /// <param name="tackling">Habilidad de entrada del defensor (0..1).</param>
        /// <param name="aggression">Agresividad del defensor (0..1).</param>
        public static float FoulChance(RulesConfig c, float tackling, float aggression) =>
            c.FoulBase * (0.6f + 0.8f * aggression) * (1.4f - 0.8f * tackling);

        /// <summary>Resuelve una entrada a partir de dos tiradas uniformes en [0, 1).</summary>
        /// <param name="foulRoll">Tirada para la falta.</param>
        /// <param name="winRoll">Tirada para ganar el balón.</param>
        /// <param name="foulChance">Probabilidad de falta.</param>
        /// <param name="winChance">Probabilidad de ganar el balón si no hay falta.</param>
        public static TackleOutcome ResolveTackle(float foulRoll, float winRoll, float foulChance, float winChance)
        {
            if (foulRoll < foulChance)
            {
                return TackleOutcome.Foul;
            }

            return winRoll < winChance ? TackleOutcome.Won : TackleOutcome.Missed;
        }

        /// <summary>Decide la tarjeta de una falta.</summary>
        /// <param name="roll">Tirada uniforme en [0, 1).</param>
        /// <param name="aggression">Agresividad del infractor (0..1).</param>
        /// <param name="previousYellows">Amarillas que ya tenía.</param>
        /// <param name="c">Reglas.</param>
        public static CardResult DecideCard(float roll, float aggression, int previousYellows, RulesConfig c)
        {
            if (roll < c.RedBase)
            {
                return CardResult.Red;
            }

            float yellow = c.YellowBase + c.YellowAggressionSpread * (aggression - 0.5f);
            if (roll < c.RedBase + yellow)
            {
                return previousYellows >= 1 ? CardResult.SecondYellow : CardResult.Yellow;
            }

            return CardResult.None;
        }
    }
}
