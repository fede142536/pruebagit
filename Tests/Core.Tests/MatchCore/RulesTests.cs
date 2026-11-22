using System;
using Football.Core.Common;
using Football.Core.MatchCore;
using NUnit.Framework;
using Football.Core.Domain;

namespace Football.Core.Tests.MatchCore
{
    public class RulesTests
    {
        private readonly MatchConfig _cfg = new MatchConfig();
        private Pitch _pitch;
        private const float R = 0.11f;

        [SetUp]
        public void SetUp() => _pitch = new Pitch(_cfg.Pitch);

        // ---- Gol -----------------------------------------------------------------------------------------------

        [TestCase(51f, 0f, 1f, 53f, 0f, 1f, 1f)]       // centro de la portería derecha
        [TestCase(-51f, 3f, 0.5f, -53f, 3f, 0.5f, -1f)] // portería izquierda, cerca del poste
        [TestCase(52f, -3.5f, 2.3f, 53f, -3.5f, 2.3f, 1f)] // rozando el poste y el larguero por dentro
        public void Goal_IsDetectedWhenTheBallCrossesTheLineBetweenThePostsUnderTheBar(
            float px, float py, float pz, float cx, float cy, float cz, float end)
        {
            BoundaryResult r = Rules.CheckBoundary(_pitch, new Vec3(px, py, pz), new Vec3(cx, cy, cz), R);
            Assert.That(r.Kind, Is.EqualTo(BoundaryKind.Goal));
            Assert.That(r.EndSign, Is.EqualTo(end));
        }

        [TestCase(51f, 0f, 2.6f, 53f, 0f, 2.6f)]   // por encima del larguero
        [TestCase(51f, 4.0f, 1f, 53f, 4.0f, 1f)]   // por fuera del poste
        [TestCase(51f, -5f, 0.3f, 53f, -5f, 0.3f)] // muy desviado
        public void BallCrossingTheEndLineOutsideTheGoal_IsNotAGoal(
            float px, float py, float pz, float cx, float cy, float cz)
        {
            BoundaryResult r = Rules.CheckBoundary(_pitch, new Vec3(px, py, pz), new Vec3(cx, cy, cz), R);
            Assert.That(r.Kind, Is.EqualTo(BoundaryKind.GoalLine));
        }

        [Test]
        public void BallInsideThePitch_IsStillInPlay()
        {
            Assert.That(Rules.CheckBoundary(_pitch, new Vec3(10, 5, 0), new Vec3(11, 5, 0), R).Kind, Is.EqualTo(BoundaryKind.None));
            Assert.That(Rules.CheckBoundary(_pitch, new Vec3(52.4f, 0, 1), new Vec3(52.55f, 0, 1), R).Kind, Is.EqualTo(BoundaryKind.None),
                "el balón debe cruzar la línea por completo (centro + radio)");
        }

        // ---- Banda ---------------------------------------------------------------------------------------------

        [TestCase(20f, 33f, 34.2f, 0, 1, 20f, 34f)]
        [TestCase(-10f, -33f, -34.2f, 1, 0, -10f, -34f)]
        [TestCase(0f, 33.5f, 34.3f, 1, 0, 0f, 34f)]
        public void ThrowIn_GoesToTheOpponentOfTheLastToucher(float x, float py, float cy, int lastTouch, int expectedTeam, float rx, float ry)
        {
            BoundaryResult b = Rules.CheckBoundary(_pitch, new Vec3(x, py, 0), new Vec3(x, cy, 0), R);
            Assert.That(b.Kind, Is.EqualTo(BoundaryKind.Touchline));

            Restart r = Rules.ThrowIn(_pitch, b.Exit, lastTouch, 1f);
            Assert.That(r.Kind, Is.EqualTo(RestartKind.ThrowIn));
            Assert.That(r.Team, Is.EqualTo(expectedTeam));
            Assert.That(r.Position.X, Is.EqualTo(rx).Within(0.01f));
            Assert.That(r.Position.Y, Is.EqualTo(ry).Within(0.01f));
        }

        [Test]
        public void BallInsideTheTouchline_IsNotAThrowIn()
        {
            Assert.That(Rules.CheckBoundary(_pitch, new Vec3(0, 33.8f, 0), new Vec3(0, 34.05f, 0), R).Kind, Is.EqualTo(BoundaryKind.None));
            Assert.That(Rules.CheckBoundary(_pitch, new Vec3(0, -20f, 0), new Vec3(0, -20.1f, 0), R).Kind, Is.EqualTo(BoundaryKind.None));
        }

        [Test]
        public void ThrowIn_NeverStartsInsideTheCornerArea()
        {
            Restart r = Rules.ThrowIn(_pitch, new Vec2(52.4f, 34f), 0, 1f);
            Assert.That(r.Position.X, Is.EqualTo(51.5f).Within(0.01f));
        }

        // ---- Saque de puerta y córner ---------------------------------------------------------------------------

        [TestCase(1f, 1, 1, 1, 0, 1f, 34f)]   // defiende el equipo 1; tocó el equipo 1 -> córner del equipo 0 en la esquina de la banda y>0
        [TestCase(1f, -1f, 1, 1, 0, 1f, -34f)]
        [TestCase(-1f, 1, 0, 0, 1, -1f, 34f)]
        public void CornerKick_WhenTheDefenderTouchedLast(float end, float side, int defending, int lastTouch, int expectedTeam, float endSign, float expectedY)
        {
            Restart r = Rules.GoalLineOut(_pitch, end, new Vec2(end * 52.5f, side * 10f), lastTouch, defending);
            Assert.That(r.Kind, Is.EqualTo(RestartKind.Corner));
            Assert.That(r.Team, Is.EqualTo(expectedTeam));
            Assert.That(r.Position.X, Is.EqualTo(end * 52.5f).Within(0.01f));
            Assert.That(r.Position.Y, Is.EqualTo(expectedY).Within(0.01f));
        }

        [TestCase(1f, 1, 0)]
        [TestCase(-1f, 0, 1)]
        [TestCase(1f, 0, 0)]
        public void GoalKick_WhenTheAttackerTouchedLast(float end, int defending, int lastTouch)
        {
            if (lastTouch == defending)
            {
                lastTouch = 1 - defending;
            }

            Restart r = Rules.GoalLineOut(_pitch, end, new Vec2(end * 52.5f, 12f), lastTouch, defending);
            Assert.That(r.Kind, Is.EqualTo(RestartKind.GoalKick));
            Assert.That(r.Team, Is.EqualTo(defending));
            Assert.That(r.Position.X, Is.EqualTo(end * (52.5f - 5.5f)).Within(0.01f));
        }

        // ---- Falta y penalti ------------------------------------------------------------------------------------

        [TestCase(40f, 5f, 1f)]    // dentro del área derecha
        [TestCase(-45f, -15f, -1f)]
        [TestCase(36f, 20.16f, 1f)] // sobre la línea del área cuenta como dentro
        public void FoulInsideTheOffendersArea_IsAPenalty(float x, float y, float end)
        {
            Restart r = Rules.Foul(_pitch, new Vec2(x, y), 0, end);
            Assert.That(r.Kind, Is.EqualTo(RestartKind.Penalty));
            Assert.That(r.Position.X, Is.EqualTo(end * (52.5f - 11f)).Within(0.01f));
            Assert.That(r.Position.Y, Is.Zero);
            Assert.That(r.Team, Is.EqualTo(0));
        }

        [TestCase(30f, 5f, 1f)]    // fuera del área
        [TestCase(40f, 22f, 1f)]   // a la altura del área pero fuera por el lateral
        [TestCase(40f, 5f, -1f)]   // en el área rival del infractor: no es penalti para él
        public void FoulOutsideTheOffendersArea_IsAFreeKickWhereItHappened(float x, float y, float end)
        {
            Restart r = Rules.Foul(_pitch, new Vec2(x, y), 1, end);
            Assert.That(r.Kind, Is.EqualTo(RestartKind.FreeKick));
            Assert.That(r.Team, Is.EqualTo(1));
            Assert.That(r.Position.X, Is.EqualTo(x).Within(0.01f));
            Assert.That(r.Position.Y, Is.EqualTo(y).Within(0.01f));
        }

        // ---- Entradas y tarjetas --------------------------------------------------------------------------------

        [Test]
        public void ResolveTackle_FollowsTheRolls()
        {
            Assert.That(Rules.ResolveTackle(0.01f, 0.9f, 0.1f, 0.5f), Is.EqualTo(TackleOutcome.Foul));
            Assert.That(Rules.ResolveTackle(0.5f, 0.1f, 0.1f, 0.5f), Is.EqualTo(TackleOutcome.Won));
            Assert.That(Rules.ResolveTackle(0.5f, 0.9f, 0.1f, 0.5f), Is.EqualTo(TackleOutcome.Missed));
        }

        [Test]
        public void TackleAndFoulChances_FollowSkill()
        {
            Assert.That(Rules.TackleWinChance(_cfg.Rules, 0.9f, 0.3f), Is.GreaterThan(Rules.TackleWinChance(_cfg.Rules, 0.3f, 0.9f)));
            Assert.That(Rules.TackleWinChance(_cfg.Rules, 1f, 0f), Is.LessThanOrEqualTo(0.85f));
            Assert.That(Rules.TackleWinChance(_cfg.Rules, 0f, 1f), Is.GreaterThanOrEqualTo(0.05f));
            Assert.That(Rules.FoulChance(_cfg.Rules, 0.2f, 0.9f), Is.GreaterThan(Rules.FoulChance(_cfg.Rules, 0.9f, 0.2f)));
        }

        [Test]
        public void DecideCard_ShowsYellowRedAndSecondYellow()
        {
            RulesConfig c = _cfg.Rules;
            Assert.That(Rules.DecideCard(0.0f, 0.5f, 0, c), Is.EqualTo(CardResult.Red));
            Assert.That(Rules.DecideCard(c.RedBase + 0.01f, 0.5f, 0, c), Is.EqualTo(CardResult.Yellow));
            Assert.That(Rules.DecideCard(c.RedBase + 0.01f, 0.5f, 1, c), Is.EqualTo(CardResult.SecondYellow));
            Assert.That(Rules.DecideCard(0.99f, 0.5f, 0, c), Is.EqualTo(CardResult.None));
            Assert.That(Rules.DecideCard(0.99f, 0.5f, 1, c), Is.EqualTo(CardResult.None), "sin falta grave no hay segunda amarilla");
        }

        [Test]
        public void AggressivePlayers_AreBookedMoreEasily()
        {
            RulesConfig c = _cfg.Rules;
            float roll = c.RedBase + c.YellowBase + 0.05f;
            Assert.That(Rules.DecideCard(roll, 0.95f, 0, c), Is.EqualTo(CardResult.Yellow));
            Assert.That(Rules.DecideCard(roll, 0.05f, 0, c), Is.EqualTo(CardResult.None));
        }

        // ---- Fuera de juego -------------------------------------------------------------------------------------

        [Test]
        public void OffsideLine_IsTheSecondLastOpponent()
        {
            float[] xs = { 50f, 12f, 10f, 15f, 30f };
            Assert.That(Rules.OffsideLine(1f, xs, 5), Is.EqualTo(30f), "el último es el portero (50), el penúltimo el de 30");
            float[] ys = { -50f, -12f, -10f, -15f, -30f };
            Assert.That(Rules.OffsideLine(-1f, ys, 5), Is.EqualTo(-30f));
        }

        [TestCase(1f, 40f, 20f, 30f, true)]   // más adelantado que balón y penúltimo defensor
        [TestCase(-1f, -40f, -20f, -30f, true)]
        [TestCase(1f, 45f, 10f, 44.9f, true)]
        public void Offside_WhenAheadOfTheBallAndTheSecondLastDefender(float dir, float player, float ball, float line, bool expected)
        {
            Assert.That(Rules.IsOffsidePosition(dir, player, ball, line), Is.EqualTo(expected));
        }

        [TestCase(1f, 30f, 20f, 30f)]    // a la par del penúltimo defensor: no es fuera de juego
        [TestCase(1f, 25f, 20f, 30f)]    // por detrás de la línea
        [TestCase(1f, 40f, 45f, 30f)]    // por detrás del balón
        [TestCase(1f, -10f, -20f, -30f)] // en su propio campo
        [TestCase(-1f, 10f, 20f, 30f)]   // en su propio campo (ataca hacia -X)
        public void NotOffside_WhenLevelBehindTheBallOrInOwnHalf(float dir, float player, float ball, float line)
        {
            Assert.That(Rules.IsOffsidePosition(dir, player, ball, line), Is.False);
        }

        [Test]
        public void OffsideLine_WithTooFewOpponents_IsFarAway()
        {
            Assert.That(Rules.OffsideLine(1f, new float[] { 10f }, 1), Is.EqualTo(1000f));
        }
    }
}
