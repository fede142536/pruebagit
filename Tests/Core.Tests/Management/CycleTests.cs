using System;
using System.Collections.Generic;
using System.Linq;
using Football.Core.Common;
using Football.Core.Domain;
using Football.Core.Management;
using Football.Core.MatchSim;
using NUnit.Framework;

namespace Football.Core.Tests.Management
{
    /// <summary>Ciclo gestión ↔ partido: estado del jugador → atributos efectivos → partido → informe → estado.</summary>
    public sealed class CycleTests
    {
        private static readonly Position[] Positions =
        {
            Position.Goalkeeper, Position.Goalkeeper, Position.LeftBack, Position.CenterBack, Position.CenterBack, Position.RightBack,
            Position.CenterBack, Position.LeftBack, Position.LeftMidfielder, Position.CentralMidfielder, Position.CentralMidfielder,
            Position.RightMidfielder, Position.DefensiveMidfielder, Position.AttackingMidfielder, Position.Striker, Position.Striker,
            Position.LeftWinger, Position.RightWinger, Position.Striker, Position.CentralMidfielder
        };

        /// <summary>Plantel de 20 jugadores con media parecida a <paramref name="rating"/>.</summary>
        private static List<Player> Squad(int firstId, int rating)
        {
            var squad = new List<Player>();
            for (int i = 0; i < Positions.Length; i++)
            {
                var p = new Player { Id = firstId + i, FirstName = "J", LastName = (firstId + i).ToString(), Position = Positions[i] };
                foreach (AttributeId id in AttributeSet.All)
                {
                    p.Attributes[id] = rating + (i % 5) - 2;
                }

                squad.Add(p);
            }

            return squad;
        }

        private static Dictionary<int, PlayerCondition> Conditions(IEnumerable<Player> squad) =>
            squad.ToDictionary(p => p.Id, p => new PlayerCondition { PlayerId = p.Id });

        [Test]
        public void CareerFile_MatchesTheDefaults() =>
            TestFiles.AssertMatchesDefaults<Football.Core.Career.CareerConfig>("career.json",
                "regenerar con: dotnet run --project Tools/HeadlessSim -- --dump-career-config Data/balance/career.json");

        [Test]
        public void ManagementFile_MatchesTheDefaults() =>
            TestFiles.AssertMatchesDefaults<ManagementConfig>("management.json",
                "regenerar con: dotnet run --project Tools/HeadlessSim -- --dump-management-config Data/balance/management.json");

        [Test]
        public void FormMoraleFitnessAndHome_ChangeTheEffectiveAttributes()
        {
            var c = new ManagementConfig();
            Player p = Squad(1, 70)[14];
            int Pace(PlayerCondition cond, bool home) => MatchPreparation.View(p, cond, p.Position, home, 9, c).Attributes[AttributeId.Pace];
            int normal = Pace(new PlayerCondition(), false);
            Assert.That(Pace(new PlayerCondition { Form = 9f, Morale = 90f }, false), Is.GreaterThan(normal), "en forma y con moral");
            Assert.That(Pace(new PlayerCondition { Fitness = 0.4f }, false), Is.LessThan(normal), "cansado");
            Assert.That(Pace(new PlayerCondition(), true), Is.GreaterThan(normal), "de local");
        }

        [Test]
        public void Prepare_LeavesOutInjuredAndSuspended_AndPutsAKeeperInGoal()
        {
            List<Player> squad = Squad(1, 70);
            Dictionary<int, PlayerCondition> cond = Conditions(squad);
            cond[1].InjuryDays = 10;       // arquero titular lesionado
            cond[15].SuspendedMatches = 1; // un delantero sancionado
            MatchTeamSetup team = MatchPreparation.Prepare("Club", squad, cond, Formations.F442, new TeamStyle(), true, new ManagementConfig());
            Assert.That(team.Players.Select(v => v.Id), Does.Not.Contain(1).And.Not.Contain(15));
            Assert.That(team.Bench.Select(v => v.Id), Does.Not.Contain(1).And.Not.Contain(15));
            Assert.That(team.Players[0].Id, Is.EqualTo(2), "ataja el suplente");
            Assert.That(team.Players.Select(v => v.Id).Concat(team.Bench.Select(v => v.Id)), Is.Unique);
        }

        private static MatchReport FakeReport(PlayerReport player, int goalsFor, int goalsAgainst)
        {
            var report = new MatchReport();
            report.Teams[0].Goals = goalsFor;
            report.Teams[1].Goals = goalsAgainst;
            report.Teams[0].Players.Add(player);
            return report;
        }

        [Test]
        public void Report_UpdatesFormMoraleFitness_AndHandsOutBansAndInjuries()
        {
            var bus = new EventBus();
            int injuredDays = 0, banned = 0;
            bus.Subscribe<PlayerInjured>(e => injuredDays = e.Days);
            bus.Subscribe<PlayerSuspended>(e => banned = e.Matches);
            var applier = new ReportApplier(new ManagementConfig(), bus);
            var squad = new Dictionary<int, PlayerCondition> { [5] = new PlayerCondition { PlayerId = 5 }, [6] = new PlayerCondition { PlayerId = 6 } };

            var star = new PlayerReport { PlayerId = 5, Minutes = 90f, Rating = 9f, StaminaLeft = 0.6f, Stats = new PlayerMatchStats() };
            applier.Apply(FakeReport(star, 3, 0), 0, squad, 1, new SeededRandom(1));
            Assert.That(squad[5].Form, Is.GreaterThan(6f));
            Assert.That(squad[5].Morale, Is.GreaterThan(50f));
            Assert.That(squad[5].Fitness, Is.EqualTo(0.6f).Within(1e-5));
            Assert.That(squad[5].SeasonMinutes, Is.EqualTo(90f));

            var red = new PlayerReport { PlayerId = 5, Minutes = 40f, Rating = 4f, StaminaLeft = 0.9f, Stats = new PlayerMatchStats { Reds = 1, Injured = true } };
            applier.Apply(FakeReport(red, 0, 2), 0, squad, 1, new SeededRandom(2));
            Assert.That(banned, Is.EqualTo(new ManagementConfig().RedCardBan));
            Assert.That(squad[5].SuspendedMatches, Is.EqualTo(banned));
            Assert.That(injuredDays, Is.InRange(4, 21));
            Assert.That(squad[5].Available, Is.False);

            // Sin jugar, cumple una fecha; descansando, se cura.
            var other = new PlayerReport { PlayerId = 6, Minutes = 90f, Rating = 6f, StaminaLeft = 0.8f, Stats = new PlayerMatchStats() };
            applier.Apply(FakeReport(other, 1, 1), 0, squad, 1, new SeededRandom(3));
            Assert.That(squad[5].SuspendedMatches, Is.EqualTo(banned - 1));
            for (int d = 0; d < 30; d++)
            {
                applier.RestDay(squad.Values);
            }

            Assert.That(squad[5].InjuryDays, Is.EqualTo(0));
            Assert.That(squad[6].Fitness, Is.EqualTo(1f));
        }

        [Test]
        public void YellowCards_Accumulate_IntoABan()
        {
            var applier = new ReportApplier(new ManagementConfig(), new EventBus());
            var squad = new Dictionary<int, PlayerCondition> { [5] = new PlayerCondition { PlayerId = 5 } };
            for (int m = 0; m < new ManagementConfig().YellowLimit; m++)
            {
                var booked = new PlayerReport { PlayerId = 5, Minutes = 90f, Rating = 6f, StaminaLeft = 1f, Stats = new PlayerMatchStats { Yellows = 1 } };
                applier.Apply(FakeReport(booked, 1, 1), 0, squad, 1, new SeededRandom((ulong)m));
            }

            Assert.That(squad[5].SuspendedMatches, Is.EqualTo(1));
            Assert.That(squad[5].AccumulatedYellows, Is.EqualTo(0));
        }

        [Test]
        public void RoundTrip_PreparedTeamsPlay_AndTheReportUpdatesTheCareer()
        {
            var c = new ManagementConfig();
            List<Player> homeSquad = Squad(1, 72), awaySquad = Squad(100, 68);
            Dictionary<int, PlayerCondition> homeCond = Conditions(homeSquad), awayCond = Conditions(awaySquad);
            var applier = new ReportApplier(c, new EventBus());
            var rng = new SeededRandom(42);
            for (int round = 0; round < 10; round++)
            {
                MatchTeamSetup home = MatchPreparation.Prepare("Local", homeSquad, homeCond, Formations.F442, new TeamStyle(), true, c);
                MatchTeamSetup away = MatchPreparation.Prepare("Visita", awaySquad, awayCond, Formations.F433, new TeamStyle(), false, c);
                MatchReport report = QuickMatchSimulator.Play(home, away, rng.Fork("m" + round), new MatchSimConfig(), new ReportConfig());
                applier.Apply(report, 0, homeCond, 1, rng);
                applier.Apply(report, 1, awayCond, 1, rng);
                for (int d = 0; d < 7; d++)
                {
                    applier.RestDay(homeCond.Values.Concat(awayCond.Values));
                }
            }

            Assert.That(homeCond.Values.Sum(x => x.SeasonMinutes), Is.EqualTo(10 * 11 * 90f).Within(1f), "11 en cancha durante 90' en cada partido");
            Assert.That(homeCond.Values.Count(x => x.SeasonMinutes > 0), Is.GreaterThan(11), "con cambios juegan también suplentes");
            Assert.That(homeCond.Values.Any(x => Math.Abs(x.Form - 6f) > 0.01f), Is.True, "las notas mueven la forma");
        }

        [Test]
        public void HomeAdvantage_IsMeasurable()
        {
            var c = new ManagementConfig();
            List<Player> a = Squad(1, 68), b = Squad(100, 68);
            int home = 0, away = 0;
            for (ulong s = 0; s < 600; s++)
            {
                bool aHome = s % 2 == 0;
                MatchTeamSetup h = MatchPreparation.Prepare("H", aHome ? a : b, Conditions(aHome ? a : b), Formations.F442, new TeamStyle(), true, c);
                MatchTeamSetup v = MatchPreparation.Prepare("V", aHome ? b : a, Conditions(aHome ? b : a), Formations.F442, new TeamStyle(), false, c);
                MatchReport r = QuickMatchSimulator.Play(h, v, new SeededRandom(s), new MatchSimConfig(), new ReportConfig());
                home += r.HomeGoals > r.AwayGoals ? 1 : 0;
                away += r.AwayGoals > r.HomeGoals ? 1 : 0;
            }

            TestContext.Out.WriteLine($"local {home}, visitante {away} de 600");
            Assert.That(home, Is.GreaterThan(away * 1.2), "el local gana más");
        }
    }
}
