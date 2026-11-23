using System.Linq;
using Football.Core.Career;
using Football.Core.Domain;
using Football.Core.MatchCore;
using NUnit.Framework;

namespace Football.Core.Tests.Career
{
    /// <summary>La media que muestra la carrera es la del archivo de equipos (la misma de los amistosos).</summary>
    public class CareerLevelTests
    {
        [Test]
        public void TheShownRating_MatchesTheFileRating_OnAverageAndPerPlayer()
        {
            var db = CareerSessionTests.Argentina();
            CareerSession s = CareerSession.NewGame(db, "Boca Juniors", 2027, 3, new CareerConfigs());
            double diff = 0;
            int n = 0, far = 0;
            foreach (Club club in s.Data.Clubs)
            {
                var def = db.Teams.First(t => t.Name == club.Name);
                foreach (Player p in s.Squad(club.Id))
                {
                    var pd = def.Players.First(x => x.Name.Trim() == (p.FirstName + " " + p.LastName).Trim());
                    float shown = RosterFactory.DisplayRating(p.Attributes, p.Position);
                    diff += shown - pd.Rating;
                    n++;
                    far += System.Math.Abs(shown - pd.Rating) > 8f ? 1 : 0;
                }
            }

            Assert.That(diff / n, Is.InRange(-1.0, 1.0), "sin sesgo respecto del archivo");
            Assert.That(far / (double)n, Is.LessThan(0.05), "casi nadie se aparta más de 8 puntos");
        }

        [Test]
        public void VisibleRating_OfTheUsersPlayers_IsOnTheFileScale()
        {
            var db = CareerSessionTests.Argentina();
            CareerSession s = CareerSession.NewGame(db, "Boca Juniors", 2027, 3, new CareerConfigs());
            double avg = s.Squad(s.Data.UserClubId).Average(p => s.VisibleRating(p));
            double file = db.Teams.First(t => t.Name == "Boca Juniors").Players.Average(p => p.Rating);
            Assert.That(avg, Is.EqualTo(file).Within(2.0));
        }
    }
}
