using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Football.Core.Career;
using Football.Core.Data;
using Football.Core.Domain;
using NUnit.Framework;

namespace Football.Core.Tests.Career
{
    /// <summary>Orquestador del modo carrera: calendario, partidos, tablas, mercado, ojeadores y fin de temporada.</summary>
    public class CareerSessionTests
    {
        private const string UserTeam = "Rosario Central";

        internal static TeamDatabase Argentina()
        {
            string dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "Data", "teams", "argentina.json")))
            {
                dir = Path.GetDirectoryName(dir);
            }

            Assert.That(dir, Is.Not.Null, "falta Data/teams/argentina.json");
            return JsonSerializer.Deserialize<TeamDatabase>(File.ReadAllText(Path.Combine(dir, "Data", "teams", "argentina.json")),
                new JsonSerializerOptions { IncludeFields = true });
        }

        internal static CareerSession NewCareer(int seed = 7) =>
            CareerSession.NewGame(Argentina(), UserTeam, 2027, seed, new CareerConfigs());

        [Test]
        public void NewGame_CreatesClubsSquadsFinancesStaffAndBothTournaments()
        {
            CareerSession s = NewCareer();
            CareerData d = s.Data;
            Assert.That(d.Clubs, Has.Count.EqualTo(15));
            Assert.That(d.Club(d.UserClubId).Name, Is.EqualTo(UserTeam));
            Assert.That(d.Clubs.All(c => s.Squad(c.Id).Count >= 20), Is.True);
            Assert.That(d.Players.Values.All(p => p.Contract != null && p.Contract.WeeklyWage > 0), Is.True);
            Assert.That(d.Staff.Count(st => st.ClubId == d.UserClubId), Is.EqualTo(4));
            Assert.That(d.Competitions.Select(c => c.Kind), Is.EquivalentTo(new[] { TournamentKind.Apertura, TournamentKind.Clausura }));
            int perTournament = 15 * 14 / 2 + 15 / 2; // 15 clubes: 15 fechas de 7 partidos (uno libre)
            Assert.That(d.Fixtures.Count(f => f.CompetitionId == CompetitionInfo.IdOf(2027, TournamentKind.Apertura)),
                Is.EqualTo(perTournament - 15 / 2), "todos contra todos a una rueda");
            Assert.That(d.Rivalries.Any(r => r.Level == RivalryLevel.Classic), Is.True);
        }

        [Test]
        public void AdvanceToNextMatch_StopsOnTheUsersMatchDay_AndOthersAreAlreadyPlayed()
        {
            CareerSession s = NewCareer();
            Assert.That(s.AdvanceToNextMatch(), Is.EqualTo(DayOutcome.UserMatchToday));
            Fixture mine = s.PendingUserFixture;
            Assert.That(mine.Date, Is.EqualTo(s.Data.Today));
            Assert.That(s.AdvanceDay(), Is.EqualTo(DayOutcome.UserMatchToday), "no avanza sin jugar el partido del usuario");
            Assert.That(s.Data.Fixtures.Where(f => f.Date == s.Data.Today && f != mine).All(f => f.IsPlayed), Is.True);
            s.PrepareUserMatch(out MatchTeamSetup home, out MatchTeamSetup away);
            Assert.That(home.Players, Has.Length.EqualTo(11));
            Assert.That(away.Players, Has.Length.EqualTo(11));
            MatchReport report = s.SimulateUserMatch();
            Assert.That(mine.IsPlayed, Is.True);
            Assert.That(mine.HomeGoals, Is.EqualTo(report.HomeGoals));
            Assert.That(s.Inbox.Items.Any(n => n.Key == "news.result"), Is.True);
            Assert.That(s.TournamentTable().Sum(r => r.Played), Is.EqualTo(2 * s.Data.Fixtures.Count(f => f.IsPlayed)));
        }

        [Test]
        public void CompleteUserMatch_AcceptsTheReportOfTheRealTimeEngine()
        {
            CareerSession s = NewCareer();
            s.AdvanceToNextMatch();
            s.PrepareUserMatch(out MatchTeamSetup home, out MatchTeamSetup away);
            var engine = new Football.Core.MatchCore.MatchEngine(new Football.Core.MatchCore.MatchConfig(),
                new Football.Core.Common.SeededRandom(3), home, away);
            while (!engine.IsFinished)
            {
                engine.Tick();
            }

            MatchReport report = engine.BuildReport();
            Fixture mine = s.PendingUserFixture;
            s.CompleteUserMatch(report);
            Assert.That(mine.HomeGoals, Is.EqualTo(report.HomeGoals));
            Assert.That(s.PendingUserFixture, Is.Null);
            Assert.That(s.Data.Conditions.Values.Any(c => c.SeasonMinutes > 0), Is.True, "el informe actualiza el plantel");
        }

        [Test]
        public void AFullSeason_EndsWithRelegationQualifiersRetirementsAndYouth_ThenTheNextSeasonStarts()
        {
            CareerSession s = NewCareer();
            long cashBefore = s.Data.Finances.Values.Sum(f => f.Cash);
            int playersBefore = s.Data.Players.Count;
            DayOutcome outcome;
            int guard = 0;
            do
            {
                outcome = s.AdvanceToNextMatch();
                if (outcome == DayOutcome.UserMatchToday)
                {
                    s.SimulateUserMatch();
                }
            }
            while (outcome != DayOutcome.SeasonFinished && guard++ < 200);

            Assert.That(outcome, Is.EqualTo(DayOutcome.SeasonFinished));
            Assert.That(s.Data.Season, Is.EqualTo(2028));
            Assert.That(s.Data.Fixtures.Where(f => f.CompetitionId / 10 == 2027).All(f => f.IsPlayed), Is.True);
            Assert.That(s.Data.Fixtures.Any(f => f.CompetitionId / 10 == 2028), Is.True, "calendario de la temporada siguiente");
            string[] keys = s.Inbox.Items.Select(n => n.Key).ToArray();
            Assert.That(keys, Does.Contain("news.qualified"));
            Assert.That(keys.Count(k => k == "news.relegated" || k == "news.user_relegated"), Is.EqualTo(2));
            Assert.That(keys, Does.Contain("news.season_end"));
            Assert.That(s.Data.Players.Count, Is.GreaterThan(playersBefore), "llegaron juveniles");
            Assert.That(s.Data.Finances.Values.Sum(f => f.Cash), Is.Not.EqualTo(cashBefore), "hubo sueldos e ingresos");
            Assert.That(s.Data.Finances.Values.SelectMany(f => f.Ledger).Any(e => e.Category == FinanceCategory.Gate), Is.True);
            Assert.That(s.AveragesTable(), Has.Count.EqualTo(15));
            Assert.That(s.AnnualTable(2027).First().Points, Is.GreaterThan(s.AnnualTable(2027).Last().Points));
        }

        [Test]
        public void SameSeedAndChoices_GiveTheSameSeason()
        {
            string Run()
            {
                CareerSession s = NewCareer(11);
                for (int i = 0; i < 5; i++)
                {
                    s.AdvanceToNextMatch();
                    s.SimulateUserMatch();
                }

                return string.Join(";", s.Data.Fixtures.Where(f => f.IsPlayed).Select(f => f.HomeGoals + "-" + f.AwayGoals));
            }

            Assert.That(Run(), Is.EqualTo(Run()));
        }

        [Test]
        public void Scouts_DeliverAReport_AndRaiseKnowledge()
        {
            CareerSession s = NewCareer();
            Player target = s.Data.Players.Values.First(p => p.ClubId != s.Data.UserClubId);
            Assert.That(s.SendScout(target.Id), Is.True);
            Assert.That(s.SendScout(s.Squad(s.Data.UserClubId)[0].Id), Is.False, "a los propios no se los ojea");
            for (int i = 0; i < 21; i++)
            {
                if (s.AdvanceDay() == DayOutcome.UserMatchToday)
                {
                    s.SimulateUserMatch();
                }
            }

            Assert.That(s.Scouting.Knowledge(s.Data.UserClubId, target.Id), Is.GreaterThan(0f));
            Assert.That(s.Inbox.Items.Any(n => n.Key == "news.scout_report"), Is.True);
        }

        [Test]
        public void Market_ReleaseClauseClosesTheDeal_AndRivalOffersArriveInTheWindow()
        {
            CareerSession s = NewCareer();
            Player cheapest = s.Data.Players.Values.Where(p => p.ClubId != s.Data.UserClubId).OrderBy(p => p.Contract.ReleaseClause).First();
            Assert.That(s.TransferWindowOpen, Is.True, "enero: libro abierto");
            s.Data.Finances[s.Data.UserClubId].TransferBudget = cheapest.Contract.ReleaseClause * 2;
            long budget = s.Data.Finances[s.Data.UserClubId].TransferBudget;
            Assert.That(s.PayReleaseClause(cheapest.Id), Is.True);
            Assert.That(cheapest.ClubId, Is.EqualTo(s.Data.UserClubId));
            Assert.That(s.Data.Finances[s.Data.UserClubId].TransferBudget, Is.LessThan(budget));
            for (int i = 0; i < 35 && s.Offers.Count == 0; i++)
            {
                if (s.AdvanceDay() == DayOutcome.UserMatchToday)
                {
                    s.SimulateUserMatch();
                }
            }

            Assert.That(s.Offers, Is.Not.Empty, "con el libro abierto llegan ofertas por jugadores propios");
            PendingOffer offer = s.Offers[0];
            s.Respond(offer, true);
            Assert.That(s.Data.Players[offer.Offer.PlayerId].ClubId, Is.EqualTo(offer.Offer.BuyerClubId));
        }

        [Test]
        public void TournamentMode_IsASingleRoundRobin_WithAChampion()
        {
            CareerSession s = CareerSession.NewGame(Argentina(), UserTeam, 2027, 5, new CareerConfigs(), tournamentOnly: true);
            Assert.That(s.Data.Competitions, Has.Count.EqualTo(1));
            DayOutcome outcome;
            int guard = 0;
            do
            {
                outcome = s.AdvanceToNextMatch();
                if (outcome == DayOutcome.UserMatchToday)
                {
                    s.SimulateUserMatch();
                }
            }
            while (outcome != DayOutcome.SeasonFinished && guard++ < 100);

            Assert.That(s.TournamentOver, Is.True);
            Assert.That(s.Data.Season, Is.EqualTo(2027), "el torneo no pasa a otra temporada");
            Assert.That(s.Inbox.Items.Count(n => n.Key == "news.champion"), Is.EqualTo(1));
            Assert.That(s.ChampionId(), Is.EqualTo(s.TournamentTable()[0].ClubId));
            s.AdvanceDay();
            Assert.That(s.Inbox.Items.Count(n => n.Key == "news.champion"), Is.EqualTo(1), "el campeón se anuncia una vez");
        }

        [Test]
        public void UserTactics_ShapeTheTeamThatTakesTheField()
        {
            CareerSession s = NewCareer();
            s.Data.Tactics.Formation = "4-3-3";
            s.Data.Tactics.DefensiveFormation = "4-4-2";
            s.Data.Tactics.Style = "HighPress";
            s.Data.Tactics.StrikerDuty = PlayerDuty.Defensive;
            s.AdvanceToNextMatch();
            s.PrepareUserMatch(out MatchTeamSetup home, out MatchTeamSetup away);
            MatchTeamSetup mine = s.PendingUserFixture.HomeClubId == s.Data.UserClubId ? home : away;
            Assert.That(mine.Formation.Name, Is.EqualTo("4-3-3"));
            Assert.That(mine.DefensiveFormation.Name, Is.EqualTo("4-4-2"));
            Assert.That(mine.Style.StrikerDuty, Is.EqualTo(PlayerDuty.Defensive));
        }

        [Test]
        public void Search_FiltersByPositionAgeAndRating_AndHiringStaffRespectsTheWageBudget()
        {
            CareerSession s = NewCareer();
            var found = s.Search(Position.Goalkeeper, 18, 40, 0, 99, long.MaxValue);
            Assert.That(found, Is.Not.Empty);
            Assert.That(found.All(p => p.Position == Position.Goalkeeper && p.ClubId != s.Data.UserClubId), Is.True);
            Assert.That(s.HireStaff(StaffRole.Scout, 20, "Ojeador Top"), Is.False.Or.True);
            s.Data.Finances[s.Data.UserClubId].WeeklyWageBudget = long.MaxValue / 4;
            Assert.That(s.HireStaff(StaffRole.Scout, 20, "Ojeador Top"), Is.True);
            Assert.That(s.Data.Staff.Single(st => st.ClubId == s.Data.UserClubId && st.Role == StaffRole.Scout).Ability, Is.EqualTo(20));
        }
    }
}
