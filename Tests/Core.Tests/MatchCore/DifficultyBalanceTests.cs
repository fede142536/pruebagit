using System.Linq;
using Football.Core.Common;
using Football.Core.Data;
using Football.Core.Domain;
using Football.Core.MatchCore;
using NUnit.Framework;

namespace Football.Core.Tests.MatchCore
{
    /// <summary>Los niveles de dificultad deben ordenarse de verdad y los goles no degenerar. Plantel sintético: el rango de goles es amplio a propósito; la calibración fina se mide con HeadlessSim (BALANCE.md §12).</summary>
    public sealed class DifficultyBalanceTests
    {
        private const int MatchesPerLevel = 40;
        private const int HomeLevel = 1;
        private const float MinGoals = 1.5f, MaxGoals = 4.8f;
        private const float MinWinRateGap = 0.08f;  // cada nivel más fácil del rival debe subir al menos esto las victorias del local

        private static (float homeWins, float goals) Play(int awayLevel)
        {
            var config = new MatchConfig();
            int wins = 0, goals = 0;
            for (int m = 0; m < MatchesPerLevel; m++)
            {
                ulong seed = 900 + (ulong)m;
                TeamDefinition Team(string name) => new TeamDefinition
                {
                    Name = name, Formation = "4-3-3", Style = "Balanced",
                    Players = Enumerable.Range(0, 18).Select(i => new PlayerDefinition
                    {
                        Name = name + i, Position = Slots[i], Rating = 66 + i % 5
                    }).ToArray()
                };

                TeamDefinition a = Team("A"), b = Team("B");
                MatchTeamSetup home = TeamBuilder.Build(a, config, new SeededRandom(seed), SquadSelection.Auto(a, Formations.F433));
                MatchTeamSetup away = TeamBuilder.Build(b, config, new SeededRandom(seed + 1), SquadSelection.Auto(b, Formations.F433));
                var e = new MatchEngine(config, new SeededRandom(seed + 2), home, away, HomeLevel, awayLevel);
                int guard = 0;
                while (!e.IsFinished && guard++ < 60 * 60 * 15)
                {
                    e.Tick();
                }

                wins += e.Teams[0].Stats.Goals > e.Teams[1].Stats.Goals ? 1 : 0;
                goals += e.Teams[0].Stats.Goals + e.Teams[1].Stats.Goals;
            }

            return (wins / (float)MatchesPerLevel, goals / (float)MatchesPerLevel);
        }

        private static readonly string[] Slots =
        {
            "POR", "LI", "DFC", "DFC", "LD", "MC", "MC", "MCO", "EI", "ED", "DC", "POR", "DFC", "MC", "DC", "LI", "LD", "MCD"
        };

        [Test]
        public void AnEasierRival_MeansMoreWinsForTheHome_AndGoalsStayPlayable()
        {
            (float homeWins, float goals) hard = Play(2), even = Play(1), easy = Play(0);
            TestContext.Out.WriteLine($"victorias del local contra rival nivel 2/1/0: {hard.homeWins:P0}/{even.homeWins:P0}/{easy.homeWins:P0}; goles {hard.goals:0.00}/{even.goals:0.00}/{easy.goals:0.00}");
            Assert.That(even.homeWins - hard.homeWins, Is.GreaterThanOrEqualTo(MinWinRateGap), "nivel 1 vs 2");
            Assert.That(easy.homeWins - even.homeWins, Is.GreaterThanOrEqualTo(MinWinRateGap), "nivel 0 vs 1");
            foreach ((float homeWins, float goals) r in new[] { hard, even, easy })
            {
                Assert.That(r.goals, Is.InRange(MinGoals, MaxGoals));
            }
        }
    }
}
