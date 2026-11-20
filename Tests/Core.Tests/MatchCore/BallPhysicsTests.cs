using System;
using Football.Core.Common;
using Football.Core.MatchCore;
using NUnit.Framework;

namespace Football.Core.Tests.MatchCore
{
    public class BallPhysicsTests
    {
        private MatchConfig _cfg;
        private Pitch _pitch;
        private float _dt;

        [SetUp]
        public void SetUp()
        {
            _cfg = new MatchConfig();
            _pitch = new Pitch(_cfg.Pitch);
            _dt = _cfg.FixedDeltaTime();
        }

        private BallHit Run(BallState b, float seconds)
        {
            BallHit last = BallHit.None;
            int steps = (int)MathF.Round(seconds / _dt);
            for (int i = 0; i < steps; i++)
            {
                BallHit h = BallPhysics.Step(b, _dt, _cfg.Ball, _pitch);
                if (h != BallHit.None)
                {
                    last = h;
                }
            }

            return last;
        }

        [Test]
        public void FreeFall_WithoutDrag_MatchesAnalyticalFormula()
        {
            _cfg.Ball.AirDrag = 0f;
            var ball = new BallState { Pos = new Vec3(0, 0, 10f) };
            Run(ball, 1.0f);

            float expected = 10f - 0.5f * _cfg.Ball.Gravity * 1f * 1f;
            Assert.That(ball.Pos.Z, Is.EqualTo(expected).Within(expected * 0.01f), "error < 1 %");
            Assert.That(ball.Vel.Z, Is.EqualTo(-_cfg.Ball.Gravity).Within(0.05f));
        }

        [Test]
        public void Projectile_WithoutDrag_LandsWhereTheFormulaSays()
        {
            _cfg.Ball.AirDrag = 0f;
            _cfg.Ball.Restitution = 0f;
            var ball = new BallState();
            float angle = 35f * MathUtil.Deg2Rad;
            Ballistics.Launch(ball, new Vec2(1, 0), 20f, angle, 0f);
            Run(ball, 0.2f); // despega
            while (ball.Pos.Z > 0f)
            {
                BallPhysics.Step(ball, _dt, _cfg.Ball, _pitch);
            }

            float range = 20f * 20f * MathF.Sin(2 * angle) / _cfg.Ball.Gravity;
            Assert.That(ball.Pos.X, Is.EqualTo(range).Within(range * 0.03f));
        }

        [Test]
        public void Drag_ShortensTheFlight()
        {
            var noDrag = new MatchConfig();
            noDrag.Ball.AirDrag = 0f;
            float WithDrag(BallConfig cfg)
            {
                var b = new BallState();
                Ballistics.Launch(b, new Vec2(1, 0), 25f, 30f * MathUtil.Deg2Rad, 0f);
                for (int i = 0; i < 40; i++) BallPhysics.Step(b, _dt, cfg, _pitch);
                while (b.Pos.Z > 0f) BallPhysics.Step(b, _dt, cfg, _pitch);
                return b.Pos.X;
            }

            Assert.That(WithDrag(_cfg.Ball), Is.LessThan(WithDrag(noDrag.Ball) * 0.95f));
        }

        [Test]
        public void Bounce_RebootHeightFollowsRestitutionSquared()
        {
            _cfg.Ball.AirDrag = 0f;
            var ball = new BallState { Pos = new Vec3(0, 0, 10f) };
            float maxAfterBounce = 0f;
            bool bounced = false;
            for (int i = 0; i < 600; i++)
            {
                float vzBefore = ball.Vel.Z;
                BallPhysics.Step(ball, _dt, _cfg.Ball, _pitch);
                if (!bounced && vzBefore < 0 && ball.Vel.Z > 0)
                {
                    bounced = true;
                }

                if (bounced)
                {
                    maxAfterBounce = MathF.Max(maxAfterBounce, ball.Pos.Z);
                    if (ball.Vel.Z < 0) break;
                }
            }

            float expected = 10f * _cfg.Ball.Restitution * _cfg.Ball.Restitution;
            Assert.That(bounced, Is.True);
            Assert.That(maxAfterBounce, Is.EqualTo(expected).Within(expected * 0.05f));
        }

        [Test]
        public void RollingBall_StopsAndNeverReverses()
        {
            var ball = new BallState();
            Ballistics.Launch(ball, new Vec2(1, 0), 15f, 0f, 0f);
            float lastX = 0f;
            for (int i = 0; i < 60 * 30; i++)
            {
                BallPhysics.Step(ball, _dt, _cfg.Ball, _pitch);
                Assert.That(ball.Pos.X, Is.GreaterThanOrEqualTo(lastX));
                lastX = ball.Pos.X;
            }

            Assert.That(ball.Vel.HorizontalLength, Is.Zero);
            // Con fricción constante y proporcional, recorre menos que v²/(2a) sin el término proporcional.
            Assert.That(ball.Pos.X, Is.InRange(25f, 15f * 15f / (2f * _cfg.Ball.RollDeceleration)));
        }

        [Test]
        public void GroundSpeedForDistance_MakesTheBallArriveWithTheRequestedSpeed()
        {
            const float distance = 25f, arrival = 7f;
            var ball = new BallState();
            Ballistics.Launch(ball, new Vec2(1, 0), Ballistics.GroundSpeedForDistance(distance, arrival, _cfg.Ball), 0f, 0f);
            while (ball.Pos.X < distance)
            {
                BallPhysics.Step(ball, _dt, _cfg.Ball, _pitch);
            }

            Assert.That(ball.Vel.HorizontalLength, Is.EqualTo(arrival).Within(0.8f));
        }

        [Test]
        public void LobSpeed_LandsNearTheTarget()
        {
            const float distance = 30f;
            float angle = _cfg.Ai.LobLoftDeg * MathUtil.Deg2Rad;
            var ball = new BallState();
            Ballistics.Launch(ball, new Vec2(1, 0), Ballistics.LobSpeed(distance, angle, _cfg.Ball), angle, 0f);
            Run(ball, 0.3f);
            while (ball.Pos.Z > 0f)
            {
                BallPhysics.Step(ball, _dt, _cfg.Ball, _pitch);
            }

            Assert.That(ball.Pos.X, Is.EqualTo(distance).Within(distance * 0.12f));
        }

        [TestCase(30f, 1f)]
        [TestCase(-30f, -1f)]
        public void Spin_CurvesTheBallInTheExpectedDirection(float spin, float expectedSign)
        {
            var ball = new BallState();
            Ballistics.Launch(ball, new Vec2(1, 0), 25f, 12f * MathUtil.Deg2Rad, spin);
            Run(ball, 1.0f);

            Assert.That(MathF.Sign(ball.Pos.Y), Is.EqualTo(expectedSign));
            Assert.That(MathF.Abs(ball.Pos.Y), Is.GreaterThan(0.5f));
        }

        [Test]
        public void WithoutSpin_TheBallDoesNotDrift()
        {
            var ball = new BallState();
            Ballistics.Launch(ball, new Vec2(1, 0), 25f, 12f * MathUtil.Deg2Rad, 0f);
            Run(ball, 1.0f);

            Assert.That(ball.Pos.Y, Is.Zero.Within(1e-4f));
        }

        [Test]
        public void Spin_DecaysOnTheGround()
        {
            var ball = new BallState { Spin = 40f };
            Run(ball, 2f);
            Assert.That(ball.Spin, Is.LessThan(0.1f));
        }

        [Test]
        public void ShotAtThePost_BouncesBack()
        {
            var ball = new BallState { Pos = new Vec3(_pitch.HalfLength - 3f, _pitch.GoalHalfWidth, 1f) };
            ball.Vel = new Vec3(25f, 0f, 0f);

            BallHit hit = Run(ball, 0.3f);

            Assert.That(hit, Is.EqualTo(BallHit.Post));
            Assert.That(ball.Vel.X, Is.LessThan(0f), "rebota hacia el campo");
        }

        [Test]
        public void ShotUnderTheBar_BouncesDown()
        {
            var ball = new BallState { Pos = new Vec3(_pitch.HalfLength - 3f, 0f, _pitch.GoalHeight - 0.05f) };
            ball.Vel = new Vec3(25f, 0f, 0f);

            BallHit hit = Run(ball, 0.3f);

            Assert.That(hit, Is.EqualTo(BallHit.Crossbar));
            Assert.That(ball.Vel.X, Is.LessThan(0f));
        }

        [Test]
        public void ShotInTheMiddleOfTheGoal_DoesNotTouchWoodwork()
        {
            var ball = new BallState { Pos = new Vec3(_pitch.HalfLength - 3f, 0f, 1f) };
            ball.Vel = new Vec3(25f, 0f, 0f);

            Assert.That(Run(ball, 0.3f), Is.EqualTo(BallHit.None));
            Assert.That(ball.Pos.X, Is.GreaterThan(_pitch.HalfLength));
        }

        [Test]
        public void Step_DoesNotAllocate()
        {
            var ball = new BallState();
            Ballistics.Launch(ball, new Vec2(1, 0.2f), 25f, 20f * MathUtil.Deg2Rad, 10f);
            BallPhysics.Step(ball, _dt, _cfg.Ball, _pitch);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 2000; i++)
            {
                BallPhysics.Step(ball, _dt, _cfg.Ball, _pitch);
            }

            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
        }
    }
}
