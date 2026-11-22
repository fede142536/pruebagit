using System;
using System.Collections.Generic;
using System.Linq;
using Football.Core.Common;
using Football.Core.Competitions;
using Football.Core.Domain;
using NUnit.Framework;

namespace Football.Core.Tests.Competitions
{
    /// <summary>Calendarios, tablas con desempates, promedios, zonas y eliminatorias.</summary>
    public sealed class CompetitionTests
    {
        private static readonly DateTime Start = new DateTime(2027, 2, 1);

        [TestCase(20)]
        [TestCase(15)]
        [TestCase(4)]
        public void DoubleRoundRobin_HasNoConflicts_AndEveryPairMeetsHomeAndAway(int teams)
        {
            var ids = Enumerable.Range(1, teams).ToList();
            List<Fixture> fixtures = FixtureGenerator.RoundRobin(1, ids, Start, 7, true, new SeededRandom(3));
            int rounds = teams % 2 == 0 ? 2 * (teams - 1) : 2 * teams;
            Assert.That(fixtures.Select(f => f.Round).Distinct().Count(), Is.EqualTo(rounds));
            foreach (var round in fixtures.GroupBy(f => f.Round))
            {
                var playing = round.SelectMany(f => new[] { f.HomeClubId, f.AwayClubId }).ToList();
                Assert.That(playing, Is.Unique, $"fecha {round.Key}: nadie juega dos veces");
                Assert.That(round.Select(f => f.Date).Distinct().Count(), Is.EqualTo(1));
            }

            foreach (int a in ids)
            {
                foreach (int b in ids.Where(b => b > a))
                {
                    Assert.That(fixtures.Count(f => f.HomeClubId == a && f.AwayClubId == b), Is.EqualTo(1), $"{a} local contra {b}");
                    Assert.That(fixtures.Count(f => f.HomeClubId == b && f.AwayClubId == a), Is.EqualTo(1), $"{b} local contra {a}");
                }

                int home = fixtures.Count(f => f.HomeClubId == a);
                Assert.That(home, Is.EqualTo(teams - 1), "mitad de local y mitad de visitante");
            }
        }

        [TestCase(30, 15)]  // Primera División: dos zonas impares de 15 -> 14 de zona + 1 interzonal en 15 fechas
        [TestCase(28, 14)]  // zonas pares de 14: 13 de zona + 1 fecha final de interzonales en 14 fechas
        [TestCase(24, 12)]
        public void TwoZones_EveryClubPlaysItsZoneOnce_PlusOneInterzonal_WithoutConflicts(int teams, int zoneSize)
        {
            var seeded = Enumerable.Range(1, teams).ToList();
            List<Fixture> fixtures = FixtureGenerator.TwoZones(1, seeded, Start, 7, new SeededRandom(5));
            List<List<int>> zones = Groups.Snake(seeded, 2);
            int expectedRounds = zoneSize % 2 == 1 ? zoneSize : zoneSize;
            Assert.That(fixtures.Select(f => f.Round).Max(), Is.EqualTo(expectedRounds), "fechas del torneo");
            Assert.That(fixtures, Has.Count.EqualTo(teams * zoneSize / 2 + (zoneSize % 2 == 0 ? 0 : 0)));
            foreach (var round in fixtures.GroupBy(f => f.Round))
            {
                Assert.That(round.SelectMany(f => new[] { f.HomeClubId, f.AwayClubId }), Is.Unique, $"fecha {round.Key}: nadie juega dos veces");
                Assert.That(round.Select(f => f.Date).Distinct().Count(), Is.EqualTo(1));
            }

            foreach (int club in seeded)
            {
                List<Fixture> mine = fixtures.Where(f => f.HomeClubId == club || f.AwayClubId == club).ToList();
                Assert.That(mine, Has.Count.EqualTo(zoneSize), $"club {club}: partidos del torneo");
                List<int> zone = zones.Single(z => z.Contains(club));
                int[] rivals = mine.Select(f => f.HomeClubId == club ? f.AwayClubId : f.HomeClubId).ToArray();
                Assert.That(rivals, Is.Unique, "nadie se repite");
                Assert.That(zone.Where(c => c != club).All(rivals.Contains), Is.True, $"club {club}: juega contra toda su zona");
                Assert.That(rivals.Count(r => !zone.Contains(r)), Is.EqualTo(1), $"club {club}: un solo cruce interzonal");
                Assert.That(mine.Count(f => f.HomeClubId == club), Is.InRange(zoneSize / 2 - 1, zoneSize / 2 + 2), $"club {club}: localías equilibradas");
            }

            Assert.That(fixtures.Where(f => f.Group == 0).All(f => zones.Single(z => z.Contains(f.HomeClubId)) != zones.Single(z => z.Contains(f.AwayClubId))),
                Is.True, "el grupo 0 son los cruces interzonales");
            Assert.That(fixtures.Where(f => f.Group != 0).All(f => zones[f.Group - 1].Contains(f.HomeClubId) && zones[f.Group - 1].Contains(f.AwayClubId)), Is.True);
        }

        [Test]
        public void TwoZones_SpreadsTheSeededClubsAcrossBothZones_AndFitsInAYear()
        {
            var seeded = Enumerable.Range(1, 30).ToList();
            List<List<int>> zones = Groups.Snake(seeded, 2);
            Assert.That(zones[0].Take(2), Is.EquivalentTo(new[] { 1, 4 }), "serpentina: 1.º y 4.º en la zona A");
            Assert.That(zones[1].Take(2), Is.EquivalentTo(new[] { 2, 3 }));
            List<Fixture> fixtures = FixtureGenerator.TwoZones(1, seeded, Start, 7, new SeededRandom(1));
            // Apertura + 3 semanas + Clausura: cada torneo son 15 fechas, no 29.
            Assert.That((fixtures.Max(f => f.Date) - Start).TotalDays, Is.LessThanOrEqualTo(14 * 7));
            Assert.That(2 * 15 * 7 + 21, Is.LessThan(365), "los dos torneos entran en el año");
            Assert.Throws<ArgumentException>(() => FixtureGenerator.TwoZones(1, new[] { 1, 2, 3 }, Start, 7));
        }

        [Test]
        public void TwoZones_IsDeterministic_ForTheSameSeed()
        {
            var seeded = Enumerable.Range(1, 30).ToList();
            string Key(List<Fixture> l) => string.Join(";", l.Select(f => $"{f.Round}:{f.HomeClubId}-{f.AwayClubId}:{f.Group}"));
            Assert.That(Key(FixtureGenerator.TwoZones(1, seeded, Start, 7, new SeededRandom(9))), Is.EqualTo(Key(FixtureGenerator.TwoZones(1, seeded, Start, 7, new SeededRandom(9)))));
        }

        [Test]
        public void SingleRound_HomeGamesAreBalanced()
        {
            List<Fixture> fixtures = FixtureGenerator.RoundRobin(1, Enumerable.Range(1, 28).ToList(), Start, 7, false);
            foreach (int id in Enumerable.Range(1, 28))
            {
                int home = fixtures.Count(f => f.HomeClubId == id);
                Assert.That(home, Is.InRange(13, 14), "una rueda de 27 fechas: 13 o 14 de local");
            }
        }

        private static Fixture Played(int home, int away, int hg, int ag) =>
            new Fixture { HomeClubId = home, AwayClubId = away, HomeGoals = hg, AwayGoals = ag };

        [Test]
        public void Table_AppliesTheConfiguredTiebreakers()
        {
            // 1 y 2 empatan en puntos y diferencia de gol; 1 le ganó a 2. 3 tiene más goles a favor que ambos.
            var games = new[]
            {
                Played(1, 2, 1, 0), Played(2, 3, 2, 0), Played(3, 1, 3, 1), Played(1, 4, 2, 0), Played(2, 4, 1, 0), Played(3, 4, 0, 2)
            };
            var table = new LeagueTable(new[] { 1, 2, 3, 4 });
            foreach (Fixture f in games)
            {
                table.Add(f);
            }

            Assert.That(table.Row(1).Points, Is.EqualTo(6));
            Assert.That(table.Row(2).Points, Is.EqualTo(6));
            List<int> argentine = table.Ranked().Select(r => r.ClubId).ToList();
            Assert.That(argentine.Take(2).ToArray(), Is.EqualTo(new[] { 2, 1 }), "mismos puntos: decide la diferencia de gol (2: +2, 1: +1)");

            var h2hFirst = new LeagueTable(new[] { 1, 2, 3, 4 }, new[] { Tiebreaker.Points, Tiebreaker.HeadToHeadPoints, Tiebreaker.GoalsFor });
            foreach (Fixture f in games)
            {
                h2hFirst.Add(f);
            }

            Assert.That(h2hFirst.Ranked()[0].ClubId, Is.EqualTo(1), "con el resultado entre ellos primero, gana 1 (le ganó a 2)");
            Assert.That(table.Ranked().Sum(r => r.Points), Is.EqualTo(games.Sum(g => g.HomeGoals == g.AwayGoals ? 2 : 3)));
        }

        [Test]
        public void Averages_UseUpToThreeSeasons_AndPromotedClubsOnlyTheirOwn()
        {
            var averages = new AveragesTable();
            averages.Record(1, 2024, 60, 38);
            averages.Record(1, 2025, 40, 38);
            averages.Record(1, 2026, 50, 38);
            averages.Record(1, 2023, 10, 38); // fuera de las tres últimas
            averages.Record(2, 2026, 30, 38); // recién ascendido
            Assert.That(averages.Average(1, 2026), Is.EqualTo(150f / 114f).Within(1e-5));
            Assert.That(averages.Average(2, 2026), Is.EqualTo(30f / 38f).Within(1e-5));
            Assert.That(averages.Worst(new[] { 1, 2 }, 2026)[0], Is.EqualTo(2));
        }

        [Test]
        public void Knockout_DrawGoesToPenalties_AndZonesCrossLikeTheLeagueCup()
        {
            var rng = new SeededRandom(5);
            Fixture draw = Played(7, 8, 1, 1);
            int winner = Knockout.Winner(draw, rng);
            Assert.That(winner, Is.AnyOf(7, 8));
            Assert.That(draw.HomePenalties, Is.Not.Null);
            Assert.That(draw.HomePenalties, Is.Not.EqualTo(draw.AwayPenalties));
            Assert.That(winner, Is.EqualTo(draw.HomePenalties > draw.AwayPenalties ? 7 : 8));
            Assert.That(Knockout.Winner(Played(7, 8, 0, 2), rng), Is.EqualTo(8));

            List<List<int>> zones = Groups.Snake(Enumerable.Range(1, 8).ToList(), 2);
            Assert.That(zones[0], Is.EqualTo(new[] { 1, 4, 5, 8 }));
            Assert.That(zones[1], Is.EqualTo(new[] { 2, 3, 6, 7 }));
            List<int> playoffs = Knockout.CrossZones(new[] { 10, 11, 12, 13 }, new[] { 20, 21, 22, 23 }, 4);
            Assert.That(playoffs, Is.EqualTo(new[] { 10, 23, 11, 22, 21, 12, 20, 13 }), "1A-4B, 2A-3B, 2B-3A, 1B-4A");
            List<Fixture> quarter = Knockout.Pair(9, playoffs, 1, Start);
            Assert.That(quarter.Count, Is.EqualTo(4));
            Assert.That(quarter[0].HomeClubId, Is.EqualTo(10), "1.º de la zona A, de local");
            Assert.That(quarter[0].AwayClubId, Is.EqualTo(23), "contra el 4.º de la zona B");
            Assert.That(quarter.SelectMany(f => new[] { f.HomeClubId, f.AwayClubId }), Is.Unique);
        }
    }
}
