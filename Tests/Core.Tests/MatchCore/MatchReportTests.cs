using System.Linq;
using Football.Core.Common;
using Football.Core.MatchCore;
using NUnit.Framework;
using Football.Core.Domain;

namespace Football.Core.Tests.MatchCore
{
    /// <summary>Informe del partido: coherencia entre jugadores, equipo y remates; tiros al arco sin el sesgo de los desvíos.</summary>
    public sealed class MatchReportTests
    {
        private static MatchReport Play(ulong seed)
        {
            MatchEngine e = MatchEngineTests.NewEngine(seed, (int)(seed % 5), (int)((seed / 3) % 5));
            MatchEngineTests.RunToEnd(e);
            Assert.That(e.IsFinished, Is.True);
            return e.BuildReport();
        }

        [Test]
        public void PlayerStats_AddUpToTheTeamStats()
        {
            for (ulong seed = 200; seed < 230; seed++)
            {
                MatchReport r = Play(seed);
                for (int t = 0; t < 2; t++)
                {
                    TeamReport team = r.Teams[t];
                    TeamReport rival = r.Teams[1 - t];
                    int goals = team.Players.Sum(p => p.Stats.Goals) + rival.Players.Sum(p => p.Stats.OwnGoals);
                    Assert.That(goals, Is.LessThanOrEqualTo(team.Goals), "goles acreditados");
                    Assert.That(goals, Is.GreaterThanOrEqualTo(team.Goals - 1), "como mucho un gol sin autor claro");
                    Assert.That(team.Players.Sum(p => p.Stats.Assists), Is.LessThanOrEqualTo(goals), "asistencias");
                    Assert.That(team.Players.Sum(p => p.Stats.Shots), Is.EqualTo(team.Stats.Shots), "remates");
                    Assert.That(team.Players.Sum(p => p.Stats.ShotsOnTarget), Is.EqualTo(team.Stats.ShotsOnTarget), "remates al arco");
                    Assert.That(team.Players.Sum(p => p.Stats.Passes), Is.EqualTo(team.Stats.Passes), "pases");
                    Assert.That(team.Players.Sum(p => p.Stats.PassesCompleted), Is.EqualTo(team.Stats.PassesCompleted), "pases completados");
                    Assert.That(team.Players.Sum(p => p.Stats.Recoveries), Is.EqualTo(team.Stats.Recoveries), "recuperaciones");
                    Assert.That(team.Players.Sum(p => p.Stats.Yellows), Is.EqualTo(team.Stats.Yellows), "amarillas");
                    Assert.That(team.Players.Sum(p => p.Stats.Xg), Is.EqualTo(team.Stats.Xg).Within(1e-3), "xG");
                    Assert.That(team.Players.Sum(p => p.Stats.Distance), Is.EqualTo(team.Stats.Distance).Within(team.Stats.Distance * 0.001f), "distancia");
                    Assert.That(r.Shots.Count(s => s.Team == t), Is.EqualTo(team.Stats.Shots), "lista de remates");
                    Assert.That(team.Stats.ShotsOnTarget, Is.LessThanOrEqualTo(team.Stats.Shots));
                }
            }
        }

        [Test]
        public void EveryPlayerHasMinutesAndARatingInRange()
        {
            MatchReport r = Play(7);
            foreach (TeamReport team in r.Teams)
            {
                Assert.That(team.Players.Count, Is.GreaterThanOrEqualTo(11));
                Assert.That(team.Possession, Is.InRange(0f, 1f));
                foreach (PlayerReport p in team.Players)
                {
                    Assert.That(p.Minutes, Is.InRange(0f, 90.01f), p.Name);
                    if (p.Minutes >= new MatchConfig().Report.MinMinutesForRating)
                    {
                        Assert.That(p.Rating, Is.InRange(3f, 10f), p.Name);
                    }

                    Assert.That(r.FindPlayer(p.PlayerId), Is.Not.Null);
                }
            }

            Assert.That(r.Teams[0].Possession + r.Teams[1].Possession, Is.EqualTo(1f).Within(1e-4));
        }

        [Test]
        public void ShotOnTarget_CountsEvenIfADefenderBlocksIt()
        {
            MatchEngine e = MatchEngineTests.NewEngine(11);
            e.TestBeginOpenPlay();
            for (int i = 0; i < 22; i++)
            {
                e.TestPlace(e.Players[i], new Vec2(0f, -30f + 2.8f * i));
            }

            MatchPlayer shooter = e.Teams[0].Players[9];
            float dir = e.Teams[0].AttackDirection;
            e.TestPlace(shooter, new Vec2(dir * 35f, 0f));
            e.TestPlace(e.Teams[1].Players[4], new Vec2(dir * 41f, 0f)); // en el camino del balón
            e.TestKick(shooter, new Vec3(dir * 25f, 0f, 1.5f), true);
            Assert.That(e.Teams[0].Stats.ShotsOnTarget, Is.EqualTo(1), "va al arco al salir del pie, aunque luego lo tapen");
            Assert.That(e.Shot(0).OnTarget, Is.True);

            MatchEngine wide = MatchEngineTests.NewEngine(12);
            wide.TestBeginOpenPlay();
            MatchPlayer other = wide.Teams[0].Players[9];
            wide.TestPlace(other, new Vec2(dir * 35f, 0f));
            wide.TestKick(other, new Vec3(dir * 25f, 9f, 1.5f), true);
            Assert.That(wide.Teams[0].Stats.ShotsOnTarget, Is.EqualTo(0), "un remate desviado no cuenta");
        }

        [Test]
        public void XgOverManyMatches_MatchesTheGoals()
        {
            double goals = 0, xg = 0;
            for (ulong seed = 300; seed < 360; seed++)
            {
                MatchReport r = Play(seed);
                goals += r.HomeGoals + r.AwayGoals;
                xg += r.Teams[0].Stats.Xg + r.Teams[1].Stats.Xg;
            }

            Assert.That(goals / xg, Is.InRange(0.75, 1.3), $"goles {goals} / xG {xg:F1}: el xG del informe está calibrado");
        }
    }
}
