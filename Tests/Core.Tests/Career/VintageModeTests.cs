using System.Linq;
using Football.Core.Career;
using Football.Core.Domain;
using NUnit.Framework;

namespace Football.Core.Tests.Career
{
    /// <summary>«Fútbol vintage»: Primera de 20 + Nacional de 29 (49 clubes), sin tocar el modo clásico de 30.</summary>
    public class VintageModeTests
    {
        private static CareerSession NewVintage(string club = "Boca Juniors", int seed = 4) =>
            CareerSession.NewGame(CareerSessionTests.Vintage(), club, 2027, seed, new CareerConfigs(), false, null, CareerSession.VintageFirstDivision);

        [Test]
        public void TheClassicPool_HasThirtyClubs_AndVintageHasFortyNine()
        {
            Assert.That(CareerSessionTests.Argentina().Teams, Has.Length.EqualTo(30));
            Assert.That(CareerSessionTests.Vintage().Teams, Has.Length.EqualTo(49));
            Assert.That(CareerSessionTests.Argentina().Teams.Any(t => t.VintageOnly), Is.False);
        }

        [Test]
        public void TheVintageFile_HasValidUniqueClubs_WithPlayableSquads()
        {
            var teams = CareerSessionTests.Vintage().Teams;
            Assert.That(teams.Select(t => t.Name), Is.Unique);
            Assert.That(teams.Select(t => t.ShortName), Is.Unique, "la tabla compacta muestra la sigla");
            foreach (var t in teams.Where(t => t.VintageOnly))
            {
                Assert.That(Football.Core.MatchCore.TeamBuilder.Validate(t), Is.Null, t.Name);
                Assert.That(t.Players.Select(p => p.Number), Is.Unique, t.Name);
                Assert.That(t.Players.Select(Football.Core.MatchCore.TeamBuilder.DisplayName), Is.Unique, t.Name);
            }
        }

        [Test]
        public void Primera_IsTheTwentyBestOfTheAnnualTable_AndNacionalHasTwentyNine()
        {
            CareerSession s = NewVintage();
            Assert.That(s.Data.Clubs.Count(c => c.Division == 1), Is.EqualTo(20));
            Assert.That(s.Data.Clubs.Count(c => c.Division == 2), Is.EqualTo(29));
            string[] first = s.Data.Clubs.Where(c => c.Division == 1).Select(c => c.Name).ToArray();
            Assert.That(first, Does.Contain("Boca Juniors").And.Contain("Tigre").And.Contain("Gimnasia y Esgrima (Mendoza)"), "puestos 1, 20 y 17");
            Assert.That(first, Does.Not.Contain("Talleres"), "el 21º juega el Nacional");
            Assert.That(first, Does.Not.Contain("Godoy Cruz Antonio Tomba"));
        }

        [Test]
        public void Nacional_PlaysTwoZones_AndEveryoneMeetsTheirZoneRivals()
        {
            CareerSession s = NewVintage();
            var fixtures = s.Data.Fixtures.Where(f => f.CompetitionId == CompetitionInfo.IdOf(2027, TournamentKind.NacionalB)).ToList();
            Assert.That(fixtures.Select(f => f.Group).Distinct().OrderBy(g => g), Is.EqualTo(new[] { 0, 1, 2 }), "zonas 1 y 2 más cruces");
            Assert.That(fixtures.SelectMany(f => new[] { f.HomeClubId, f.AwayClubId }).Distinct().Count(), Is.EqualTo(29));
            Assert.That(fixtures.Select(f => f.Date).Max().Year, Is.EqualTo(2027), "la temporada entra en el año");
        }

        [Test]
        public void ThreeSeasonsAsASmallClub_KeepTheLeagueHealthy()
        {
            CareerSession s = NewVintage("Almirante Brown", 9);
            Assert.That(s.Data.Club(s.Data.UserClubId).Division, Is.EqualTo(2));
            for (int season = 0; season < 3; season++)
            {
                DayOutcome o;
                int guard = 0;
                do
                {
                    s.Data.Manager.Popularity = 100f;
                    o = s.AdvanceToNextMatch();
                    if (o == DayOutcome.UserMatchToday)
                    {
                        s.SimulateUserMatch();
                    }
                }
                while (o != DayOutcome.SeasonFinished && guard++ < 500);
                Assert.That(o, Is.EqualTo(DayOutcome.SeasonFinished));
                s.AdvanceDay(); // abre la temporada siguiente
                Assert.That(s.Data.Clubs.Count(c => c.Division == 1), Is.EqualTo(20));
                Assert.That(s.Data.Fixtures.Count(f => f.CompetitionId == CompetitionInfo.IdOf(s.Data.Season, TournamentKind.NacionalB)), Is.GreaterThan(0));
            }

            long broke = s.Data.Finances.Values.Count(f => f.Cash < -50_000_000L);
            Assert.That(broke, Is.LessThan(10), "pocos clubes en un pozo de deuda irrecuperable");
        }

        [Test]
        public void ASeasonInVintage_ChangesTwoClubsEachWay()
        {
            CareerSession s = NewVintage("Rosario Central");
            DayOutcome o;
            int guard = 0;
            do
            {
                s.Data.Manager.Popularity = 100f;
                o = s.AdvanceToNextMatch();
                if (o == DayOutcome.UserMatchToday)
                {
                    s.SimulateUserMatch();
                }
            }
            while (o != DayOutcome.SeasonFinished && guard++ < 500);
            Assert.That(o, Is.EqualTo(DayOutcome.SeasonFinished));
            Assert.That(s.Data.Clubs.Count(c => c.Division == 1), Is.EqualTo(20), "siguen siendo 20 en Primera");
            Assert.That(s.Data.Clubs.Count(c => c.Division == 2), Is.EqualTo(29));
        }
    }
}
