using System;
using System.Linq;
using Football.Core.Data;
using Football.Core.Domain;
using Football.Core.Management;
using Football.Core.MatchCore;
using NUnit.Framework;

namespace Football.Core.Tests.MatchCore
{
    /// <summary>Los 30 clubes de Primera en <c>Data/teams/argentina.json</c>: esquema, planteles reales, datos del club y rivalidades.</summary>
    public class FirstDivisionFileTests
    {
        private static readonly string[] OriginalFifteen =
        {
            "Boca Juniors", "River Plate", "Racing Club", "Independiente", "San Lorenzo", "Estudiantes de La Plata",
            "Gimnasia y Esgrima La Plata", "Rosario Central", "Newell's Old Boys", "Talleres", "Belgrano", "Vélez Sarsfield", "Lanús",
            "Banfield", "Defensa y Justicia"
        };

        private static readonly string[] NewFifteen =
        {
            "Huracán", "Argentinos Juniors", "Godoy Cruz Antonio Tomba", "Tigre", "Platense", "Unión (Santa Fe)", "Instituto (Córdoba)",
            "Atlético Tucumán", "Central Córdoba (Santiago del Estero)", "Barracas Central", "Sarmiento (Junín)", "Independiente Rivadavia",
            "Deportivo Riestra", "Aldosivi", "San Martín (San Juan)"
        };

        internal static TeamDatabase Load() => Football.Core.Tests.Career.CareerSessionTests.Argentina();

        [Test]
        public void TheFile_HasAllThirtyClubs_TheOriginalFifteenFirst()
        {
            TeamDatabase db = Load();
            Assert.That(db.Teams, Has.Length.EqualTo(30));
            Assert.That(db.Teams.Take(15).Select(t => t.Name), Is.EquivalentTo(OriginalFifteen), "los 15 de antes siguen ahí");
            Assert.That(db.Teams.Skip(15).Select(t => t.Name), Is.EqualTo(NewFifteen));
            Assert.That(db.Teams.Select(t => t.ShortName), Is.Unique);
            Assert.That(db.Teams.Select(t => t.ShortName), Is.All.Matches<string>(s => s.Length == 3 && s == s.ToUpperInvariant()), "sigla de 3 letras");
        }

        [Test]
        public void TheNewClubs_HaveRealSquadsOf20To22_WithNumbers_AgesAndBalancedRatings()
        {
            foreach (TeamDefinition t in Load().Teams.Skip(15))
            {
                Assert.That(t.Players.Length, Is.InRange(20, 22), t.Name);
                Assert.That(t.Players.Any(p => p.Name.Contains("ficticio", StringComparison.OrdinalIgnoreCase)), Is.False, t.Name + ": jugador ficticio");
                Assert.That(t.Players.Select(p => p.Number), Is.All.InRange(1, 99), t.Name + ": dorsal");
                Assert.That(t.Players.Select(p => p.Number), Is.Unique, t.Name + ": dorsales repetidos");
                Assert.That(t.Players.Count(p => p.Position == "POR"), Is.InRange(2, 3), t.Name + ": arqueros");
                Assert.That(t.Players.Count(p => p.Position is "DFC" or "LI" or "LD"), Is.InRange(6, 10), t.Name + ": defensores");
                Assert.That(t.Players.Any(p => p.Position == "LI") && t.Players.Any(p => p.Position == "LD"), Is.True, t.Name + ": laterales");
                Assert.That(t.Players.Any(p => p.Position == "DC"), Is.True, t.Name + ": delantero");
                Assert.That(t.Players.Select(p => p.Rating), Is.All.InRange(62, 78), t.Name + ": medias");
                double xi = t.Players.OrderByDescending(p => p.Rating).Take(11).Average(p => p.Rating);
                Assert.That(xi, Is.InRange(65.0, 73.0), t.Name + ": media del once");
                int[] ages = t.Players.Where(p => p.Age > 0).Select(p => p.Age).ToArray();
                Assert.That(ages.Length, Is.GreaterThanOrEqualTo(10), t.Name + ": edades reales");
                Assert.That(ages, Is.All.InRange(16, 42), t.Name + ": edad");
                Assert.That(TeamBuilder.Validate(t), Is.Null, t.Name);
            }
        }

        [Test]
        public void TheNewClubs_HaveCompleteClubData_AndDistinctKits()
        {
            foreach (TeamDefinition t in Load().Teams.Skip(15))
            {
                ClubInfo c = t.Club;
                Assert.That(c, Is.Not.Null, t.Name);
                Assert.That(c.Founded, Is.InRange(1880, 1935), t.Name + ": fundación");
                Assert.That(c.Stadium, Is.Not.Empty, t.Name);
                Assert.That(c.City, Is.Not.Empty, t.Name);
                Assert.That(c.Capacity, Is.InRange(8000, 60000), t.Name + ": capacidad");
                Assert.That(c.ParkingSpaces, Is.InRange(100, 3000), t.Name + ": parking");
                Assert.That(Enum.TryParse(c.Pitch, out PitchCondition _), Is.True, t.Name + ": césped " + c.Pitch);
                Assert.That(new[] { "Mid", "Development" }, Does.Contain(c.Tier), t.Name + ": jerarquía");
                Assert.That(c.TransferBudget, Is.InRange(1_000_000, 8_000_000), t.Name);
                Assert.That(c.AnnualWageBudget, Is.GreaterThan(c.TransferBudget), t.Name + ": sueldos");
                Assert.That(c.ClubValue, Is.GreaterThan(c.AnnualWageBudget), t.Name + ": valor");
                Assert.That(c.History, Is.Not.Empty, t.Name);
                Assert.That(t.Home.Shirt, Is.Not.EqualTo(t.Away.Shirt), t.Name + ": titular y alternativa");
            }
        }

        [Test]
        public void TheBudgets_FollowTheHierarchy_AcrossTheWholeLeague()
        {
            TeamDefinition[] all = Load().Teams;
            double Avg(string tier, Func<ClubInfo, long> f) => all.Where(t => t.Club.Tier == tier).Average(t => f(t.Club));
            Assert.That(Avg("Big", c => c.TransferBudget), Is.GreaterThan(Avg("Mid", c => c.TransferBudget)));
            Assert.That(Avg("Mid", c => c.TransferBudget), Is.GreaterThan(Avg("Development", c => c.TransferBudget)));
            Assert.That(Avg("Big", c => c.ClubValue), Is.GreaterThan(Avg("Development", c => c.ClubValue) * 2));
            // Las medias del once también siguen la jerarquía (un grande supera a uno en desarrollo).
            double Xi(TeamDefinition t) => t.Players.OrderByDescending(p => p.Rating).Take(11).Average(p => p.Rating);
            Assert.That(all.Where(t => t.Club.Tier == "Big").Average(Xi), Is.GreaterThan(all.Where(t => t.Club.Tier == "Development").Average(Xi)));
        }

        [Test]
        public void TheRivalries_IncludeTheNewClubs_AndAllPointToRealTeams()
        {
            TeamDatabase db = Load();
            Assert.That(db.RivalryBetween("Huracán", "San Lorenzo"), Is.Not.EqualTo(RivalryLevel.None));
            Assert.That(db.RivalryBetween("Argentinos Juniors", "Platense"), Is.Not.EqualTo(RivalryLevel.None));
            Assert.That(db.RivalryBetween("Godoy Cruz Antonio Tomba", "Independiente Rivadavia"), Is.EqualTo(RivalryLevel.Classic));
            Assert.That(db.RivalryBetween("Tigre", "Platense"), Is.EqualTo(RivalryLevel.Classic));
            Assert.That(NewFifteen.Count(n => db.Rivalries.Any(r => r.TeamA == n || r.TeamB == n)), Is.GreaterThanOrEqualTo(12), "casi todos los nuevos tienen rival histórico");
            foreach (RivalryDefinition r in db.Rivalries)
            {
                Assert.That(db.Teams.Any(t => t.Name == r.TeamA) && db.Teams.Any(t => t.Name == r.TeamB), Is.True, r.Name);
                Assert.That(Enum.TryParse(r.Level, true, out RivalryLevel _), Is.True, r.Level);
            }
        }
    }
}
