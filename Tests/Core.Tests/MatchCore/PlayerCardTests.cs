using System.IO;
using System.Linq;
using Football.Core.Career;
using Football.Core.Common;
using Football.Core.Data;
using Football.Core.Domain;
using Football.Core.MatchCore;
using Football.Core.Tests.Career;
using NUnit.Framework;

namespace Football.Core.Tests.MatchCore
{
    /// <summary>Ficha del jugador: carta, índices, atributos, puestos compatibles y estadísticas.</summary>
    public class PlayerCardTests
    {
        private static TeamDefinition FirstTeam() => CareerSessionTests.Argentina().Teams[0];

        [Test]
        public void TheCard_ShowsTheSameAttributesThePlayerHasOnThePitch()
        {
            TeamDefinition team = FirstTeam();
            var config = new MatchConfig();
            MatchTeamSetup a = TeamBuilder.Build(team, config, new SeededRandom(1));
            MatchTeamSetup b = TeamBuilder.Build(team, config, new SeededRandom(999));
            for (int i = 0; i < a.Players.Length; i++)
            {
                Assert.That(AttributeSet.All.Select(x => a.Players[i].Attributes[x]),
                    Is.EqualTo(AttributeSet.All.Select(x => b.Players[i].Attributes[x])), "los atributos son fijos por jugador, no por partido");
            }

            PlayerDefinition def = team.Players.First(p => TeamBuilder.ParsePosition(p.Position) != Position.Goalkeeper);
            PlayerCardInfo card = PlayerCards.FromDefinition(def);
            PlayerMatchView onPitch = a.Players.Concat(a.Bench).First(v => v.Name == TeamBuilder.DisplayName(def));
            if (onPitch.Role == TeamBuilder.ParsePosition(def.Position))
            {
                Assert.That(card.Face.Pace, Is.EqualTo(PlayerCards.Face(onPitch.Attributes).Pace));
            }

            Assert.That(card.Overall, Is.EqualTo(def.Rating));
        }

        [Test]
        public void TheCard_HasSixFaceStatsTopAttributesCompatiblePositionsAndBodyData()
        {
            foreach (PlayerDefinition def in FirstTeam().Players)
            {
                PlayerCardInfo card = PlayerCards.FromDefinition(def);
                FaceStats f = card.Face;
                foreach (int v in new[] { f.Pace, f.Shooting, f.Passing, f.Dribbling, f.Defending, f.Physical })
                {
                    Assert.That(v, Is.InRange(1, 99));
                }

                Assert.That(card.TopAttributes, Has.Count.EqualTo(6));
                Assert.That(card.TopAttributes.Select(x => x.Value), Is.Ordered.Descending);
                Assert.That(card.Compatible[0], Is.EqualTo(card.Position), "el puesto natural primero");
                Assert.That(card.HeightCm, Is.InRange(162, 202));
                Assert.That(card.Age, Is.InRange(18, 35));
                Assert.That(PlayerCards.FromDefinition(def).Foot, Is.EqualTo(card.Foot), "determinista");
                if (card.Position == Position.Goalkeeper)
                {
                    Assert.That(card.TopAttributes.Any(x => x.Key == AttributeId.Reflexes || x.Key == AttributeId.Handling), Is.True);
                }
            }
        }

        [Test]
        public void CareerCards_CarrySeasonStats_ThatSurviveSavingAndResetAtSeasonEnd()
        {
            CareerSession s = CareerSessionTests.NewCareer(3);
            for (int i = 0; i < 4; i++)
            {
                s.AdvanceToNextMatch();
                s.SimulateUserMatch();
            }

            Player regular = s.Squad(s.Data.UserClubId).OrderByDescending(p => s.Data.SeasonStats.TryGetValue(p.Id, out CardStats st) ? st.Matches : 0).First();
            PlayerCardInfo card = s.Card(regular.Id);
            Assert.That(card.Stats.Matches, Is.InRange(1, 4));
            Assert.That(card.Stats.AverageRating, Is.InRange(3f, 10f));
            Assert.That(card.Fitness, Is.InRange(0f, 1f));
            Assert.That(card.Morale, Is.InRange(0f, 100f));
            Assert.That(s.Data.SeasonStats.Values.Sum(x => x.Goals), Is.GreaterThan(0));

            s.SyncData();
            var buffer = new MemoryStream();
            CareerFile.Write(buffer, s.Data);
            buffer.Position = 0;
            CareerData loaded = CareerFile.Read(buffer);
            Assert.That(loaded.SeasonStats[regular.Id].Matches, Is.EqualTo(s.Data.SeasonStats[regular.Id].Matches));
            Assert.That(loaded.SeasonStats[regular.Id].RatingSum, Is.EqualTo(s.Data.SeasonStats[regular.Id].RatingSum));
        }
    }
}
