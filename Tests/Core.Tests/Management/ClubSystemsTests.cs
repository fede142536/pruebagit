using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Football.Core.Common;
using Football.Core.Domain;
using Football.Core.Management;
using NUnit.Framework;

namespace Football.Core.Tests.Management
{
    /// <summary>Scouting, mercado, cantera, vestuario, buzón e historia viva.</summary>
    public sealed class ClubSystemsTests
    {
        private static readonly DateTime Today = new DateTime(2027, 7, 1);

        private static Player NewPlayer(int id, int rating, int age, Position position = Position.CentralMidfielder, int? club = 2,
            PersonalityTrait trait = PersonalityTrait.Balanced, int potential = 0)
        {
            var p = new Player
            {
                Id = id, ClubId = club, FirstName = "J", LastName = id.ToString(), Position = position, BirthDate = Today.AddYears(-age),
                Personality = trait, Potential = potential > 0 ? potential : rating, JoinedOn = Today.AddYears(-2)
            };
            foreach (AttributeId a in AttributeSet.All)
            {
                p.Attributes[a] = rating;
            }

            return p;
        }

        [Test]
        public void Scouting_RangesAlwaysContainTheTruth_AndNarrowWithBetterScouts()
        {
            var c = new ManagementConfig();
            var scouting = new ScoutingSystem(c);
            Player target = NewPlayer(10, 72, 24);
            foreach (AttributeId a in AttributeSet.All)
            {
                var (min, max) = scouting.Range(1, target, a);
                Assert.That(target.Attributes[a], Is.InRange(min, max));
                Assert.That(max - min, Is.GreaterThanOrEqualTo(c.ScoutingMaxWidth - 2), "sin ojear, rango ancho (±1 por redondeo)");
            }

            scouting.Scout(1, 10, 5, 18);
            var good = new ScoutingSystem(c);
            good.Scout(1, 10, 5, 4);
            Assert.That(scouting.Knowledge(1, 10), Is.GreaterThan(good.Knowledge(1, 10)), "el mejor ojeador conoce más en los mismos días");
            var narrow = scouting.Range(1, target, AttributeId.Pace);
            Assert.That(target.Attributes[AttributeId.Pace], Is.InRange(narrow.min, narrow.max));
            Assert.That(narrow.max - narrow.min, Is.LessThan(c.ScoutingMaxWidth - 2));
            scouting.Scout(1, 10, 100, 20);
            Assert.That(scouting.Range(1, target, AttributeId.Pace), Is.EqualTo((72, 72)), "conocido del todo, exacto");
            Assert.That(scouting.Range(2, target, AttributeId.Pace), Is.EqualTo((72, 72)), "los propios se ven exactos");
        }

        [Test]
        public void Market_ValuesByLevelAndAge_AndAnswersOffersByProfileAndClauses()
        {
            var market = new TransferMarket(new ManagementConfig());
            Player star = NewPlayer(1, 78, 26), veteran = NewPlayer(2, 78, 34), kid = NewPlayer(3, 62, 19, potential: 85);
            Assert.That(market.Value(star, Today), Is.GreaterThan(market.Value(veteran, Today)));
            Assert.That(market.Value(kid, Today), Is.GreaterThan(market.Value(NewPlayer(4, 62, 19), Today)), "la proyección vale");

            long seller = market.AskingPrice(star, ClubProfile.YouthSeller, Today), buyer = market.AskingPrice(star, ClubProfile.Buyer, Today);
            Assert.That(seller, Is.GreaterThan(buyer), "el club vendedor pide más");
            var offer = new TransferOffer { PlayerId = 1, Fee = seller };
            Assert.That(market.Evaluate(offer, star, ClubProfile.YouthSeller, Today, out _), Is.EqualTo(OfferResponse.Accepted));
            offer.Fee = (long)(seller * 0.8);
            Assert.That(market.Evaluate(offer, star, ClubProfile.YouthSeller, Today, out long counter), Is.EqualTo(OfferResponse.CounterOffer));
            Assert.That(counter, Is.GreaterThan(offer.Fee));
            offer.SellOnShare = 0.4f;
            Assert.That(market.Evaluate(offer, star, ClubProfile.YouthSeller, Today, out _), Is.EqualTo(OfferResponse.Accepted), "el 40 % de una venta futura compensa");
            offer.SellOnShare = 0f;
            offer.Fee = seller / 3;
            Assert.That(market.Evaluate(offer, star, ClubProfile.YouthSeller, Today, out _), Is.EqualTo(OfferResponse.Rejected));
            star.Contract = new Contract { ReleaseClause = seller / 2 };
            offer.Fee = seller / 4;
            Assert.That(market.Evaluate(offer, star, ClubProfile.YouthSeller, Today, out _), Is.EqualTo(OfferResponse.Rejected), "muy por debajo de la cláusula");
            offer.Fee = seller / 2;
            Assert.That(market.Evaluate(offer, star, ClubProfile.YouthSeller, Today, out _), Is.EqualTo(OfferResponse.Accepted), "pagar la cláusula obliga");
        }

        [Test]
        public void Wages_DependOnLevelPromiseAndPersonality()
        {
            var market = new TransferMarket(new ManagementConfig());
            Player loyal = NewPlayer(1, 70, 27, trait: PersonalityTrait.Loyal), ambitious = NewPlayer(2, 70, 27, trait: PersonalityTrait.Ambitious);
            long demand = market.WageDemand(loyal, PlayingTimePromise.Regular);
            Assert.That(market.WageDemand(loyal, PlayingTimePromise.Star), Is.GreaterThan(demand), "prometerle ser figura cuesta más");
            long low = (long)(demand * 0.85);
            Assert.That(market.AcceptsWage(loyal, PlayingTimePromise.Regular, low), Is.True, "el leal acepta un poco menos");
            Assert.That(market.AcceptsWage(ambitious, PlayingTimePromise.Regular, low), Is.False, "el ambicioso no");
        }

        [Test]
        public void RivalProfiles_TargetDifferentPlayers()
        {
            var pool = new[] { NewPlayer(1, 80, 29), NewPlayer(2, 60, 18, potential: 88), NewPlayer(3, 74, 27, Position.CenterBack) };
            Assert.That(TransferMarket.AiTarget(ClubProfile.Buyer, pool, Today).Id, Is.EqualTo(1));
            Assert.That(TransferMarket.AiTarget(ClubProfile.YouthSeller, pool, Today).Id, Is.EqualTo(2));
            Assert.That(TransferMarket.AiTarget(ClubProfile.Defensive, pool, Today).Id, Is.EqualTo(3));
        }

        [Test]
        public void YouthIntake_BetterAcademiesProduceBetterProspects()
        {
            var c = new ManagementConfig();
            double Mean(int level, int coach)
            {
                var intake = new YouthIntake(c, new EventBus(), 10000);
                var rng = new SeededRandom(7);
                return Enumerable.Range(0, 40).SelectMany(_ => intake.Generate(1, level, coach, Today, rng)).Average(p => p.Potential);
            }

            Assert.That(Mean(5, 18), Is.GreaterThan(Mean(1, 5) + 15));
            var gen = new YouthIntake(c, new EventBus(), 500);
            List<Player> players = gen.Generate(3, 3, 10, Today, new SeededRandom(1));
            Assert.That(players.Count, Is.InRange(c.YouthIntakeSize[0], c.YouthIntakeSize[1]));
            Assert.That(players.All(p => p.AgeOn(Today) >= 15 && p.AgeOn(Today) <= 17 && p.ClubId == 3), Is.True);
            Assert.That(players.Select(p => p.Id), Is.Unique);
        }

        [Test]
        public void DressingRoom_HierarchyChemistryPromisesAndLeaders()
        {
            var c = new ManagementConfig();
            var bus = new EventBus();
            int broken = 0;
            bus.Subscribe<PromiseBroken>(_ => broken++);
            var room = new DressingRoom(c, bus);
            var squad = Enumerable.Range(1, 15).Select(i => NewPlayer(i, 65, 20 + i % 10)).ToList();
            squad[3].Personality = PersonalityTrait.Leader;
            squad[3].BirthDate = Today.AddYears(-33);
            Dictionary<int, DressingRoomTier> tiers = DressingRoom.Hierarchy(squad, Today);
            Assert.That(tiers[squad[3].Id], Is.EqualTo(DressingRoomTier.TeamLeader));
            Assert.That(tiers.Values.Count(t => t == DressingRoomTier.TeamLeader), Is.EqualTo(1));

            var lineup = squad.Take(11).Select(p => p.Id).ToList();
            Assert.That(room.Chemistry(lineup), Is.EqualTo(0f));
            var team = new TeamReport();
            team.Players.AddRange(lineup.Select(id => new PlayerReport { PlayerId = id, Minutes = 90f, Stats = new PlayerMatchStats() }));
            for (int m = 0; m < 30; m++)
            {
                room.RecordMatch(team);
            }

            Assert.That(room.Chemistry(lineup), Is.EqualTo(1f).Within(1e-5), "habituales: química completa");
            Assert.That(room.ChemistryBonus(lineup), Is.EqualTo(c.ChemistryBonus).Within(1e-5));

            var cond = squad.ToDictionary(p => p.Id, p => new PlayerCondition { PlayerId = p.Id, Morale = 50f });
            cond[squad[14].Id].Promise = PlayingTimePromise.Star;
            cond[squad[14].Id].TeamMatchesSincePromise = 10;
            cond[squad[14].Id].MinutesSincePromise = 90f; // jugó 1 de 10
            foreach (Player leader in squad.Where(p => tiers[p.Id] >= DressingRoomTier.HighlyInfluential))
            {
                cond[leader.Id].Morale = 90f;
            }

            room.MonthlyReview(2, squad, cond, Today);
            Assert.That(broken, Is.EqualTo(1));
            Player follower = squad.First(p => tiers[p.Id] == DressingRoomTier.Other && p != squad[14]);
            Assert.That(cond[follower.Id].Morale, Is.GreaterThan(50f), "los líderes contentos levantan al resto");
            Assert.That(cond[squad[14].Id].Morale, Is.LessThan(cond[follower.Id].Morale - 5f), "la promesa incumplida le baja la moral");
        }

        [Test]
        public void Inbox_CollectsTheClubNews_AndFormatsThemFromTheLanguageFiles()
        {
            var bus = new EventBus();
            var inbox = new Inbox(2, bus, () => Today, playerId => playerId < 100 ? 2 : 3);
            bus.Publish(new PlayerInjured { PlayerId = 5, Days = 12 });
            bus.Publish(new PlayerInjured { PlayerId = 150, Days = 3 }); // de otro club: no interesa
            bus.Publish(new FacilityUpgraded { ClubId = 2, Kind = FacilityKind.Stadium, Level = 3 });
            bus.Publish(new TransferCompleted { PlayerId = 7, BuyerClubId = 9, SellerClubId = 2, Fee = 1000 });
            Assert.That(inbox.Items.Count, Is.EqualTo(3));
            Assert.That(inbox.Unread, Is.EqualTo(3));
            Assert.That(inbox.Items[0].Key, Is.EqualTo("news.transfer_out"), "la más nueva primero");

            foreach (string lang in new[] { "es", "en" })
            {
                string dir = AppContext.BaseDirectory, path = null;
                while (dir != null && path == null)
                {
                    string candidate = Path.Combine(dir, "Data", "locale", "news." + lang + ".json");
                    path = File.Exists(candidate) ? candidate : null;
                    dir = Path.GetDirectoryName(dir);
                }

                Assert.That(path, Is.Not.Null, "Data/locale/news." + lang + ".json");
                var templates = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
                foreach (NewsItem item in inbox.Items)
                {
                    string text = Inbox.Format(item, templates);
                    Assert.That(text, Is.Not.EqualTo(item.Key), lang + ": falta el texto de " + item.Key);
                    Assert.That(text, Does.Not.Contain("{"), lang + ": quedaron datos sin completar");
                }

                foreach (string key in new[] { "news.injury", "news.suspension", "news.promise_broken", "news.facility_upgraded", "news.youth_intake",
                             "news.transfer_in", "news.transfer_out", "news.record", "news.retired", "news.retired_to_staff" })
                {
                    Assert.That(templates.ContainsKey(key), Is.True, lang + ": " + key);
                }
            }
        }

        [Test]
        public void LivingHistory_KeepsRecords_AndRetiredLeadersBecomeCoaches()
        {
            var bus = new EventBus();
            var records = new List<string>();
            bus.Subscribe<RecordBroken>(e => records.Add(e.Key));
            var history = new LivingHistory(new ManagementConfig(), bus, 900);
            var report = new MatchReport();
            report.Teams[0].Goals = 5;
            report.Teams[0].Players.Add(new PlayerReport { PlayerId = 9, Stats = new PlayerMatchStats { Goals = 3 } });
            history.RecordMatch(report, 1, 2, 2027);
            Assert.That(history.Record(LivingHistory.BiggestWin).Value, Is.EqualTo(5));
            Assert.That(history.Record(LivingHistory.SeasonGoals).HolderId, Is.EqualTo(9));
            Assert.That(records, Does.Contain(LivingHistory.SeasonGoals));

            var veterans = new[]
            {
                NewPlayer(9, 80, 39, trait: PersonalityTrait.Leader), NewPlayer(10, 70, 39, trait: PersonalityTrait.Temperamental),
                NewPlayer(11, 70, 25)
            };
            List<Player> retired = history.SeasonEnd(veterans, Today, new SeededRandom(1), out List<StaffMember> staff);
            Assert.That(retired.Select(p => p.Id), Is.EquivalentTo(new[] { 9, 10 }), "a los 39 se retiran; a los 25, no");
            Assert.That(staff.Count, Is.EqualTo(1), "solo el líder pasa al cuerpo técnico");
            Assert.That(staff[0].FormerPlayerId, Is.EqualTo(9));
            Assert.That(staff[0].Role, Is.EqualTo(StaffRole.Manager));
            Assert.That(staff[0].Ability, Is.InRange(1, 20));
        }
    }
}
