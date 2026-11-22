using System;
using System.Linq;
using Football.Core.Common;
using Football.Core.Domain;
using Football.Core.Management;
using NUnit.Framework;

namespace Football.Core.Tests.Management
{
    /// <summary>Economía al estilo PC Fútbol: presupuestos separados, flujo semanal y mensual, taquilla e infraestructura.</summary>
    public sealed class FinanceTests
    {
        private sealed class FixedWages : IWageBill
        {
            public long Weekly = 20_000_000;
            public long WeeklyWages(int clubId) => Weekly;
        }

        private static readonly DateTime Start = new DateTime(2027, 1, 1);

        private static (FinanceSystem system, SeasonClock clock, ClubFinances club, FixedWages wages, EventBus bus) NewWorld()
        {
            var bus = new EventBus();
            var wages = new FixedWages();
            var system = new FinanceSystem(new FinanceConfig(), wages, bus);
            var club = new ClubFinances
            {
                ClubId = 1, Cash = 20_000_000_000, TransferBudget = 5_000_000_000, WeeklyWageBudget = 25_000_000, Members = 40000,
                Reputation = 70
            };
            system.Register(club, new ClubFacilities());
            return (system, new SeasonClock(new SimClock(Start), bus), club, wages, bus);
        }

        [Test]
        public void FinanceFile_MatchesTheDefaults() =>
            TestFiles.AssertMatchesDefaults<FinanceConfig>("finance.json",
                "regenerar con: dotnet run --project Tools/HeadlessSim -- --dump-finance-config Data/balance/finance.json");

        [Test]
        public void MovingMoneyBetweenBudgets_CostsTheCommission()
        {
            var w = NewWorld();
            var c = new FinanceConfig();
            long cash = w.club.Cash;
            Assert.That(w.system.MoveToWages(1, 1_040_000_000, Start), Is.True);
            Assert.That(w.club.TransferBudget, Is.EqualTo(5_000_000_000 - 1_040_000_000));
            Assert.That(w.club.WeeklyWageBudget, Is.EqualTo(25_000_000L + 1_040_000_000L * 9 / 10 / c.WeeksPerSeason));
            Assert.That(w.club.Cash, Is.EqualTo(cash - 104_000_000), "la comisión sale de la caja");
            Assert.That(w.system.MoveToWages(1, 99_000_000_000, Start), Is.False, "no alcanza");
            Assert.That(w.system.MoveToTransfers(1, 1_000_000, Start), Is.True);
            Assert.That(w.system.MoveToTransfers(1, long.MaxValue / 1000, Start), Is.False);
        }

        [Test]
        public void EveryWeek_PaysWagesAndMaintenance_EveryMonth_CollectsIncome()
        {
            var w = NewWorld();
            long cash = w.club.Cash;
            w.clock.Advance(7);
            Assert.That(w.club.Total(FinanceCategory.Wages), Is.EqualTo(-w.wages.Weekly));
            Assert.That(w.club.Total(FinanceCategory.Maintenance), Is.LessThan(0));
            Assert.That(w.club.Total(FinanceCategory.Members), Is.EqualTo(0), "el mes todavía no terminó");
            w.clock.Advance(30);
            Assert.That(w.club.Total(FinanceCategory.Members), Is.EqualTo(40000L * new FinanceConfig().MembershipFee));
            Assert.That(w.club.Total(FinanceCategory.Television), Is.GreaterThan(0));
            Assert.That(w.club.Total(FinanceCategory.Sponsors), Is.GreaterThan(0));
            Assert.That(w.club.Total(FinanceCategory.Wages), Is.EqualTo(-5 * w.wages.Weekly), "5 semanas en 37 días");
            Assert.That(w.club.Cash - cash, Is.EqualTo(w.club.Ledger.Sum(e => e.Amount)), "la caja cuadra con los movimientos");
        }

        [Test]
        public void Gate_DependsOnRivalryAndPrice_AndNeverExceedsCapacity()
        {
            var w = NewWorld();
            int normal = w.system.Attendance(1, 50, RivalryLevel.None);
            int classic = w.system.Attendance(1, 50, RivalryLevel.Classic);
            Assert.That(classic, Is.GreaterThan(normal), "el clásico llena más");
            Assert.That(classic, Is.LessThanOrEqualTo(w.system.Capacity(1)));
            w.club.TicketPrice = new FinanceConfig().ReferenceTicketPrice * 3;
            Assert.That(w.system.Attendance(1, 50, RivalryLevel.None), Is.LessThan(normal), "más caro, menos gente");
            w.club.TicketPrice = 0;
            w.bus.Publish(new HomeMatchPlayed { Date = Start, ClubId = 1, OpponentReputation = 80, Rivalry = RivalryLevel.Classic });
            Assert.That(w.club.Total(FinanceCategory.Gate), Is.EqualTo((long)w.system.Attendance(1, 80, RivalryLevel.Classic) * new FinanceConfig().ReferenceTicketPrice));
        }

        [Test]
        public void StadiumUpgrade_IsPaidUpFront_TakesWeeks_AndRaisesCapacity()
        {
            var w = NewWorld();
            int before = w.system.Capacity(1);
            int upgraded = 0;
            w.bus.Subscribe<FacilityUpgraded>(e => upgraded = e.Level);
            long cost = w.system.UpgradeCost(2);
            long cash = w.club.Cash;
            Assert.That(w.system.StartUpgrade(1, FacilityKind.Stadium, Start), Is.True);
            Assert.That(w.club.Cash, Is.EqualTo(cash - cost));
            Assert.That(w.system.StartUpgrade(1, FacilityKind.Stadium, Start), Is.False, "una obra por vez");
            w.clock.Advance(7 * (new FinanceConfig().UpgradeWeeks[0] - 1));
            Assert.That(w.system.Capacity(1), Is.EqualTo(before), "todavía en obra");
            w.clock.Advance(7);
            Assert.That(upgraded, Is.EqualTo(2));
            Assert.That(w.system.Capacity(1), Is.GreaterThan(before));
            w.club.Cash = 0;
            Assert.That(w.system.StartUpgrade(1, FacilityKind.Medical, Start), Is.False, "sin caja no hay obra");
        }

        [Test]
        public void ASeasonOfAMidTableClub_DoesNotGoBankrupt()
        {
            var w = NewWorld();
            for (int week = 0; week < 52; week++)
            {
                if (week % 2 == 0)
                {
                    w.bus.Publish(new HomeMatchPlayed { Date = w.clock.Today, ClubId = 1, OpponentReputation = 60, Rivalry = RivalryLevel.None });
                }

                w.clock.Advance(7);
            }

            Assert.That(w.club.Cash, Is.GreaterThan(0), "un club de prestigio 70 con sueldos razonables termina la temporada con caja");
        }
    }
}
