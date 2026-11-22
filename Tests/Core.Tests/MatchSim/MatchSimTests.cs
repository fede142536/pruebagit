using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Linq;
using Football.Core.Common;
using Football.Core.Domain;
using Football.Core.MatchCore;
using Football.Core.MatchSim;
using NUnit.Framework;

namespace Football.Core.Tests.MatchSim
{
    /// <summary>
    /// Simulador rápido: calibración contra el motor en tiempo real (referencia medida con MatchCore, 200 partidos por caso,
    /// equipos de 65 ± la mitad de la diferencia; BALANCE.md §7), coherencia del informe, determinismo y velocidad.
    /// </summary>
    public sealed class MatchSimTests
    {
        // Referencia de MatchCore: diferencia → (victorias del fuerte, goles del fuerte, goles del débil).
        private static readonly (float gap, double wins, double strongGoals, double weakGoals)[] Reference =
        {
            (0f, 0.36, 1.35, 1.35), (5f, 0.58, 1.94, 1.05), (10f, 0.80, 2.59, 0.67), (20f, 0.97, 4.15, 0.29)
        };

        private const int Matches = 500;

        private static (double wins, double strong, double weak) Simulate(float gap, int matches = Matches)
        {
            var config = new MatchConfig();
            var sim = new MatchSimConfig();
            int wins = 0;
            double strong = 0, weak = 0;
            for (ulong s = 0; s < (ulong)matches; s++)
            {
                var rng = new SeededRandom(77000 + s);
                IRandom teams = rng.Fork("teams");
                MatchTeamSetup home = RosterFactory.CreateTeam(teams, "H", 65f + gap / 2f, Formations.F442, config.Styles[0]);
                MatchTeamSetup away = RosterFactory.CreateTeam(teams, "A", 65f - gap / 2f, Formations.F442, config.Styles[0]);
                MatchReport r = QuickMatchSimulator.Play(home, away, rng.Fork("sim"), sim, config.Report);
                strong += r.HomeGoals;
                weak += r.AwayGoals;
                wins += r.HomeGoals > r.AwayGoals ? 1 : 0;
            }

            return ((double)wins / matches, strong / matches, weak / matches);
        }

        [Test]
        public void FiveHundredMatches_MatchTheRealTimeEngine_AtEveryStrengthGap()
        {
            foreach (var reference in Reference)
            {
                var sim = Simulate(reference.gap);
                string label = $"diferencia {reference.gap}: simulado {sim.wins:P0} ({sim.strong:F2}-{sim.weak:F2}), MatchCore {reference.wins:P0} ({reference.strongGoals:F2}-{reference.weakGoals:F2})";
                TestContext.Out.WriteLine(label);
                Assert.That(sim.wins, Is.EqualTo(reference.wins).Within(0.08), label);
                Assert.That(sim.strong, Is.EqualTo(reference.strongGoals).Within(0.25 + 0.08 * reference.strongGoals), label);
                Assert.That(sim.weak, Is.EqualTo(reference.weakGoals).Within(0.2), label);
            }
        }

        [Test]
        public void BalanceFile_MatchesTheDefaultConfiguration()
        {
            string dir = AppContext.BaseDirectory, path = null;
            while (dir != null && path == null)
            {
                string candidate = Path.Combine(dir, "Data", "balance", "matchsim.json");
                path = File.Exists(candidate) ? candidate : null;
                dir = Path.GetDirectoryName(dir);
            }

            Assert.That(path, Is.Not.Null, "Data/balance/matchsim.json debe existir");
            var options = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };
            MatchSimConfig fromFile = JsonSerializer.Deserialize<MatchSimConfig>(File.ReadAllText(path), options);
            Assert.That(JsonSerializer.Serialize(fromFile, options), Is.EqualTo(JsonSerializer.Serialize(new MatchSimConfig(), options)),
                "regenerar con: dotnet run --project Tools/HeadlessSim -- --dump-sim-config Data/balance/matchsim.json");
        }

        [Test]
        public void Simulation_TakesMicroseconds()
        {
            Simulate(5f, 50); // calentamiento
            var timer = Stopwatch.StartNew();
            Simulate(5f, 2000);
            double microseconds = timer.Elapsed.TotalMilliseconds * 1000.0 / 2000;
            TestContext.Out.WriteLine($"{microseconds:F0} µs por partido (incluye armar los equipos)");
            Assert.That(microseconds, Is.LessThan(1000), "menos de 1 ms por partido");
        }

        [Test]
        public void Report_IsConsistent_AndDeterministic()
        {
            var config = new MatchConfig();
            for (ulong s = 0; s < 40; s++)
            {
                var rng = new SeededRandom(500 + s);
                IRandom teams = rng.Fork("teams");
                MatchTeamSetup home = RosterFactory.CreateTeam(teams, "H", 70f, Formations.F433, config.Styles[(int)(s % 5)]);
                MatchTeamSetup away = RosterFactory.CreateTeam(teams, "A", 64f, Formations.F4231, config.Styles[(int)((s + 2) % 5)]);
                home.Bench = RosterFactory.CreateTeam(teams, "HB", 62f, Formations.F442, config.Styles[0]).Players.Take(7).ToArray();
                MatchReport a = QuickMatchSimulator.Play(home, away, new SeededRandom(9 + s), new MatchSimConfig(), config.Report);
                MatchReport b = QuickMatchSimulator.Play(home, away, new SeededRandom(9 + s), new MatchSimConfig(), config.Report);
                Assert.That(a.Simulated, Is.True);
                Assert.That(b.HomeGoals, Is.EqualTo(a.HomeGoals));
                Assert.That(b.AwayGoals, Is.EqualTo(a.AwayGoals));
                Assert.That(b.Shots.Count, Is.EqualTo(a.Shots.Count), "misma semilla, mismo partido");
                for (int t = 0; t < 2; t++)
                {
                    TeamReport team = a.Teams[t];
                    Assert.That(team.Players.Sum(p => p.Stats.Goals), Is.EqualTo(team.Goals));
                    Assert.That(team.Players.Sum(p => p.Stats.Assists), Is.LessThanOrEqualTo(team.Goals));
                    Assert.That(team.Players.Sum(p => p.Stats.Shots), Is.EqualTo(team.Stats.Shots));
                    Assert.That(team.Players.Sum(p => p.Stats.Passes), Is.EqualTo(team.Stats.Passes));
                    Assert.That(team.Stats.ShotsOnTarget, Is.InRange(team.Goals, team.Stats.Shots));
                    Assert.That(a.Shots.Count(x => x.Team == t), Is.EqualTo(team.Stats.Shots));
                    Assert.That(team.Players.Where(p => p.Started).Count(), Is.EqualTo(11));
                    Assert.That(team.Players.All(p => p.Minutes >= 0f && p.Minutes <= 90f), Is.True);
                }

                Assert.That(a.Teams[0].Players.Count(p => !p.Started), Is.EqualTo(3), "con suplentes, hace los cambios");
                Assert.That(a.Teams[0].Possession + a.Teams[1].Possession, Is.EqualTo(1f).Within(1e-5));
            }
        }

        [Test]
        public void BetterTeam_KeepsMoreOfTheBall_AndShootsMore()
        {
            var config = new MatchConfig();
            double possession = 0, shotsStrong = 0, shotsWeak = 0;
            for (ulong s = 0; s < 200; s++)
            {
                var rng = new SeededRandom(3000 + s);
                IRandom teams = rng.Fork("teams");
                MatchReport r = QuickMatchSimulator.Play(RosterFactory.CreateTeam(teams, "H", 75f, Formations.F442, config.Styles[0]),
                    RosterFactory.CreateTeam(teams, "A", 60f, Formations.F442, config.Styles[0]), rng, new MatchSimConfig(), config.Report);
                possession += r.Teams[0].Possession;
                shotsStrong += r.Teams[0].Stats.Shots;
                shotsWeak += r.Teams[1].Stats.Shots;
            }

            Assert.That(possession / 200, Is.GreaterThan(0.55));
            Assert.That(shotsStrong, Is.GreaterThan(shotsWeak * 1.3));
        }
    }
}
