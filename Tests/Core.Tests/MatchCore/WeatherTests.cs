using Football.Core.Common;
using Football.Core.Domain;
using Football.Core.MatchCore;
using NUnit.Framework;

namespace Football.Core.Tests.MatchCore
{
    /// <summary>Con lluvia el balón rueda más lejos y los controles fallan más; con sol o nublado el juego es el mismo.</summary>
    public class WeatherTests
    {
        private static float RollDistance(MatchWeather weather)
        {
            MatchConfig cfg = new MatchConfig();
            cfg.ApplyWeather(weather);
            MatchEngine e = MatchEngineTests.NewEngine(5, cfg: cfg);
            e.TestBeginOpenPlay();
            for (int i = 0; i < 22; i++)
            {
                e.TestPlace(e.Players[i], new Vec2(i < 11 ? -50f : 50f, 30f));
            }

            e.TestPlaceBall(new Vec3(-20f, 0f, 0.11f), new Vec3(14f, 0f, 0f), null);
            for (int t = 0; t < 60 * 6; t++)
            {
                e.Tick();
            }

            return e.Ball.Pos.X - -20f;
        }

        [Test]
        public void Rain_MakesTheBallRollFarther()
        {
            float dry = RollDistance(MatchWeather.Sunny), wet = RollDistance(MatchWeather.Rain);
            Assert.That(wet, Is.GreaterThan(dry * 1.1f));
            Assert.That(RollDistance(MatchWeather.Cloudy), Is.EqualTo(dry).Within(0.001f));
        }

        [Test]
        public void Rain_RaisesMiscontrol_AndLeavesOtherWeatherUntouched()
        {
            var baseline = new MatchConfig();
            var wet = new MatchConfig();
            wet.ApplyWeather(MatchWeather.Rain);
            var cloudy = new MatchConfig();
            cloudy.ApplyWeather(MatchWeather.Cloudy);
            Assert.That(wet.Player.MiscontrolChance, Is.GreaterThan(baseline.Player.MiscontrolChance));
            Assert.That(cloudy.Player.MiscontrolChance, Is.EqualTo(baseline.Player.MiscontrolChance));
        }
    }
}
