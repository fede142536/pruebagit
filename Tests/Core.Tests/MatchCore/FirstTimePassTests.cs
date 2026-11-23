using Football.Core.Common;
using Football.Core.MatchCore;
using NUnit.Framework;

namespace Football.Core.Tests.MatchCore
{
    /// <summary>Pase de primera: la orden pedida antes de recibir se ejecuta con el primer toque, sin esperar el instante de control.</summary>
    public sealed class FirstTimePassTests
    {
        private const int MaxTicks = 60 * 6;

        private static (MatchEngine engine, MatchPlayer controlled) Scenario(ulong seed)
        {
            MatchEngine e = SubstitutionTests.NewMatch(seed);
            e.TestBeginOpenPlay();
            MatchPlayer passer = e.Teams[0].Players[6], controlled = e.Teams[0].Players[7];
            foreach (MatchPlayer p in e.Teams[1].Players)
            {
                e.TestPlace(p, new Vec2(35f, p.Index * 2f - 20f)); // el rival lejos: nadie intercepta
            }

            foreach (MatchPlayer p in e.Teams[0].Players)
            {
                if (p != passer && p != controlled)
                {
                    e.TestPlace(p, new Vec2(-30f, p.Index * 2f - 10f));
                }
            }

            e.TestPlace(passer, new Vec2(-14f, 0f));
            e.TestPlace(controlled, new Vec2(0f, 0f));
            controlled.Heading = 0f;
            e.Teams[0].Controlled = controlled;
            e.TestPlaceBall(new Vec3(-5f, 0f, 0.11f), new Vec3(6f, 0f, 0f), passer);
            return (e, controlled);
        }

        /// <summary>Ticks que pasan entre que el jugador controla el balón y lo suelta de nuevo (-1 si nunca lo controló o lo soltó).</summary>
        private static int TicksHolding(MatchEngine e, MatchPlayer p, bool pressBeforeReceiving)
        {
            var pass = new MatchInput { Pass = true }; // sin joystick: el receptor se queda esperando el balón y el pase va al compañero automático
            if (pressBeforeReceiving)
            {
                e.SetInput(0, pass);
            }

            int got = -1;
            for (int i = 0; i < MaxTicks; i++)
            {
                if (got >= 0 && !pressBeforeReceiving && i == got + 1)
                {
                    e.SetInput(0, pass); // pulsa justo después de recibir
                }

                e.Tick();
                if (got < 0 && e.Owner == p)
                {
                    got = i;
                }
                else if (got < 0 && e.LastToucher == p && e.Owner != p && System.Math.Sqrt(e.Ball.Vel.X * e.Ball.Vel.X + e.Ball.Vel.Y * e.Ball.Vel.Y) > 8f)
                {
                    return 0; // lo controló y lo pasó en el mismo paso: de primera
                }
                else if (got >= 0 && e.Owner != p)
                {
                    return i - got;
                }
            }

            return -1;
        }

        [Test]
        public void PressingPassBeforeTheBallArrives_PassesAtTheFirstTouch()
        {
            (MatchEngine e, MatchPlayer c) = Scenario(41);
            int ticks = TicksHolding(e, c, true);
            double seconds = ticks / (double)e.Config.TickRate;
            TestContext.Out.WriteLine("de primera: " + seconds.ToString("0.000") + " s con el balón");
            Assert.That(ticks, Is.GreaterThanOrEqualTo(0), "controló el balón y lo pasó");
            Assert.That(seconds, Is.LessThan(e.Config.Player.ControlDelay), "pasó antes del instante de control normal");
        }

        [Test]
        public void PressingPassOnlyAfterReceiving_WaitsTheNormalControlDelay()
        {
            (MatchEngine e, MatchPlayer c) = Scenario(41);
            int ticks = TicksHolding(e, c, false);
            double seconds = ticks / (double)e.Config.TickRate;
            TestContext.Out.WriteLine("después de recibir: " + seconds.ToString("0.000") + " s con el balón");
            Assert.That(ticks, Is.GreaterThan(0));
            Assert.That(seconds, Is.GreaterThanOrEqualTo(e.Config.Player.ControlDelay * 0.8));
        }

        [Test]
        public void IfTheRivalWinsTheBall_TheEarlyOrderIsDiscarded()
        {
            (MatchEngine e, MatchPlayer c) = Scenario(41);
            e.SetInput(0, new MatchInput { Pass = true });
            MatchPlayer rival = e.Teams[1].Players[8];
            e.TestPlace(rival, new Vec2(-3f, 0f)); // se adelanta y corta el pase
            e.TestPlaceBall(new Vec3(-3.2f, 0f, 0.11f), new Vec3(0.5f, 0f, 0f), e.Teams[0].Players[6]);
            for (int i = 0; i < 30 && e.Owner != rival; i++)
            {
                e.Tick();
            }

            Assert.That(e.Owner, Is.SameAs(rival), "el rival se quedó con el balón");
            Assert.That(e.Teams[0].Input.Pass, Is.False, "la orden de pase se descartó");
        }
    }
}
