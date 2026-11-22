using Football.Core.Common;

namespace Football.Core.MatchCore
{
    // Ganchos solo para pruebas (internal, visibles únicamente para el ensamblado de pruebas): permiten armar
    // escenarios concretos sin esperar a que ocurran durante un partido.
    public sealed partial class MatchEngine
    {
        internal void TestBeginOpenPlay()
        {
            Phase = MatchPhase.Playing;
            Owner = null;
            _passActive = false;
            _shotLive = false;
            _saveRolled = false;
            _planTimer = 0f;
        }

        internal void TestPlace(MatchPlayer p, Vec2 position)
        {
            p.Pos = position;
            p.Target = position;
            p.Speed = 0f;
            p.SpeedFactor = 0f;
        }

        internal void TestGiveBall(MatchPlayer p)
        {
            Owner = p;
            LastToucher = p;
            Ball.Pos = new Vec3(p.Pos.X, p.Pos.Y, 0f);
            Ball.Vel = Vec3.Zero;
            _prevBallPos = Ball.Pos;
            p.ActionTimer = 0f;
        }

        internal void TestPlaceBall(Vec3 position, Vec3 velocity, MatchPlayer lastToucher)
        {
            Owner = null;
            Ball.Pos = position;
            Ball.Vel = velocity;
            Ball.Spin = 0f;
            _prevBallPos = position;
            LastToucher = lastToucher;
        }

        internal bool TestPassInFlight => _passActive;

        internal void TestKickoff(int team) => PlaceForKickoff(team);

        internal Vec2 TestRunIntoSpace(MatchPlayer p) => RunIntoSpace(p, Teams[p.Team].AttackDirection);

        /// <summary>El jugador golpea el balón con la velocidad dada, como un tiro o un despeje.</summary>
        internal void TestKick(MatchPlayer p, Vec3 velocity, bool shot)
        {
            Ball.Pos = new Vec3(p.Pos.X, p.Pos.Y, 0.11f);
            _prevBallPos = Ball.Pos;
            Ball.Vel = velocity;
            Ball.Spin = 0f;
            AfterKick(p, shot ? KickKind.Shot : KickKind.Clearance, null);
        }

        internal bool TestParryOverTheBar(MatchPlayer keeper)
        {
            if (!TryParryOverTheBar(Teams[keeper.Team].DefendedEnd, out Vec3 velocity))
            {
                return false;
            }

            Ball.Vel = velocity;
            Ball.Spin = 0f;
            OnTouch(keeper, false);
            keeper.KickTimer = 1f;
            return true;
        }

        internal float TestFatigueError(MatchPlayer p) => FatigueError(p);

        internal float TestFatigueReaction(MatchPlayer p) => FatigueReaction(p);
    }
}
