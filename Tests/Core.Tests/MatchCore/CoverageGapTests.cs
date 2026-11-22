using System;
using Football.Core.Common;
using Football.Core.Domain;
using Football.Core.MatchCore;
using NUnit.Framework;

namespace Football.Core.Tests.MatchCore
{
    /// <summary>
    /// Pruebas añadidas tras la revisión de la batería con mutaciones: cada una mata un cambio del código que antes pasaba
    /// desapercibido (por ejemplo, acreditar los goles del segundo tiempo al equipo equivocado).
    /// </summary>
    public class CoverageGapTests
    {
        private const float Frozen = 1000f;

        private static void Freeze(MatchEngine e)
        {
            e.TestBeginOpenPlay();
            for (int i = 0; i < 22; i++)
            {
                MatchPlayer p = e.Players[i];
                e.TestPlace(p, new Vec2(-40f + 4f * (i % 11), i < 11 ? -30f : 30f));
                p.DecisionTimer = Frozen;
                p.OwnerTimer = Frozen;
            }
        }

        private static MatchEngine InSecondHalf(ulong seed)
        {
            MatchEngine e = MatchEngineTests.NewEngine(seed);
            while (e.Half < 2)
            {
                e.Tick();
            }

            return e;
        }

        // ---- Segundo tiempo: los equipos cambiaron de lado -----------------------------------------------------------

        [TestCase(1)]
        [TestCase(2)]
        public void GoalAtEachEnd_IsCreditedToTheAttackingTeam_InBothHalves(int half)
        {
            MatchEngine e = half == 1 ? MatchEngineTests.NewEngine(7) : InSecondHalf(7);
            Freeze(e);
            int goalsBefore = e.Teams[0].Stats.Goals;
            float end = e.Teams[0].AttackDirection; // portería que ataca el local en esta parte
            MatchPlayer home = e.Teams[0].Players[9];
            e.TestPlaceBall(new Vec3(end * 51.5f, 0f, 0.5f), new Vec3(end * 20f, 0f, 0f), home);
            for (int i = 0; i < 10 && e.Phase == MatchPhase.Playing; i++)
            {
                e.Tick();
            }

            Assert.That(e.Teams[0].Stats.Goals, Is.EqualTo(goalsBefore + 1), $"parte {half}: gol del local");
        }

        [TestCase(1, true, RestartKind.GoalKick, 1)]
        [TestCase(2, true, RestartKind.GoalKick, 1)]
        [TestCase(1, false, RestartKind.Corner, 0)]
        [TestCase(2, false, RestartKind.Corner, 0)]
        public void BallOverTheEndLine_GivesTheRightRestartToTheRightTeam(int half, bool lastTouchByAttacker, RestartKind expected, int team)
        {
            MatchEngine e = half == 1 ? MatchEngineTests.NewEngine(8) : InSecondHalf(8);
            Freeze(e);
            float end = e.Teams[0].AttackDirection; // fondo que defiende el visitante
            MatchPlayer toucher = lastTouchByAttacker ? e.Teams[0].Players[9] : e.Teams[1].Players[4];
            e.TestPlaceBall(new Vec3(end * 51.5f, 15f, 0.2f), new Vec3(end * 15f, 0f, 0f), toucher);
            for (int i = 0; i < 20 && e.Phase == MatchPhase.Playing; i++)
            {
                e.Tick();
            }

            Assert.That(e.CurrentRestart.Kind, Is.EqualTo(expected));
            Assert.That(e.CurrentRestart.Team, Is.EqualTo(team));
        }

        [Test]
        public void BallAlreadyBeyondTheEndLine_IsOut()
        {
            var pitch = new Pitch(new PitchConfig());
            BoundaryResult r = Rules.CheckBoundary(pitch, new Vec3(53f, 10f, 0f), new Vec3(53.2f, 10f, 0f), 0.11f);
            Assert.That(r.Kind, Is.EqualTo(BoundaryKind.GoalLine));
        }

        // ---- Táctica: la IA se adapta de verdad durante el partido ---------------------------------------------------

        [Test]
        public void AiTeamLosingAfterMinute60_SwitchesToAttackingWithHighPress()
        {
            MatchEngine e = MatchEngineTests.NewEngine(9);
            e.Teams[0].Stats.Goals = 6; // el visitante va perdiendo (margen amplio: no debe empatar antes del 60)
            while (e.Minute < 62f && !e.IsFinished)
            {
                e.Tick();
            }

            Assert.That(e.Teams[1].Mentality, Is.EqualTo(Mentality.Attacking));
            Assert.That(e.Teams[1].Pressing, Is.EqualTo(PressLevel.High));
            Assert.That(e.Events.CountOf(MatchEventType.TacticsChanged, 1), Is.GreaterThanOrEqualTo(1));
            Assert.That(e.Teams[1].Style.LineHeight, Is.GreaterThan(e.Teams[1].BaseStyle.LineHeight), "el cambio llega al estilo efectivo");
        }

        [TestCase(-1, 59.9f, Mentality.Balanced)]
        [TestCase(-1, 60f, Mentality.Attacking)]
        [TestCase(-1, 79.9f, Mentality.Attacking)]
        [TestCase(-1, 80f, Mentality.VeryAttacking)]
        [TestCase(1, 79.9f, Mentality.Balanced)]
        [TestCase(1, 80f, Mentality.Defensive)]
        public void Adaptive_BoundariesAreInclusive(int difference, float minute, Mentality expected)
        {
            Assert.That(Tactics.Adaptive(difference, minute, new AiConfig(), out _), Is.EqualTo(expected));
        }

        [Test]
        public void Apply_KeepsEveryFieldWithinZeroAndOne_AndCopiesTheRest()
        {
            var ai = new AiConfig();
            var target = new TeamStyle();
            foreach (float v in new[] { 0.02f, 0.98f })
            {
                var b = new TeamStyle { Name = "X", LineHeight = v, Risk = v, Directness = v, PressIntensity = v, Width = 0.3f, Compactness = 0.7f };
                foreach (Mentality m in new[] { Mentality.VeryDefensive, Mentality.VeryAttacking })
                {
                    foreach (PressLevel p in new[] { PressLevel.Low, PressLevel.High })
                    {
                        Tactics.Apply(b, m, p, ai, target);
                        foreach (float f in new[] { target.LineHeight, target.Risk, target.Directness, target.PressIntensity })
                        {
                            Assert.That(f, Is.InRange(0f, 1f));
                        }

                        Assert.That(target.Name, Is.EqualTo("X"));
                        Assert.That(target.Width, Is.EqualTo(0.3f));
                        Assert.That(target.Compactness, Is.EqualTo(0.7f));
                    }
                }
            }
        }

        // ---- Barridas: más falta que de pie, y más aún por detrás ----------------------------------------------------

        private static bool SlideFoul(ulong seed, bool slide, bool front)
        {
            MatchEngine e = MatchEngineTests.NewEngine(seed);
            Freeze(e);
            MatchPlayer owner = e.Players[9];
            MatchPlayer defender = e.Players[13];
            e.TestPlace(owner, Vec2.Zero);
            e.TestPlace(defender, new Vec2(front ? 1f : -1f, 0f));
            owner.Heading = 0f;
            defender.Heading = front ? MathF.PI : 0f;
            owner.ActionTimer = Frozen;
            e.TestGiveBall(owner);
            if (slide)
            {
                defender.SlideTimer = e.Config.Rules.SlideDuration;
                defender.SlideResolved = false;
            }
            else
            {
                defender.WantsTackle = true;
                defender.TackleTimer = 0f;
            }

            e.Tick();
            return e.Events.CountOf(MatchEventType.Foul, 1) > 0;
        }

        [Test]
        public void SlideTackle_IsAFoulMoreOftenThanAStandingOne_AndMoreStillFromBehind()
        {
            int standing = 0, slideFront = 0, slideBehind = 0;
            for (ulong seed = 0; seed < 400; seed++)
            {
                standing += SlideFoul(seed, false, true) ? 1 : 0;
                slideFront += SlideFoul(seed, true, true) ? 1 : 0;
                slideBehind += SlideFoul(seed, true, false) ? 1 : 0;
            }

            Assert.That(slideFront, Is.GreaterThan(standing), $"de pie {standing}, barrida {slideFront}");
            Assert.That(slideBehind, Is.GreaterThan(slideFront), $"barrida de frente {slideFront}, por detrás {slideBehind}");
        }

        // ---- Arquero ---------------------------------------------------------------------------------------------------

        /// <summary>Tiro raso al centro de la portería visitante con el arquero en su sitio; devuelve si fue gol.</summary>
        private static bool ShotAtKeeper(ulong seed, int keeperSkill, float speed, out MatchEngine e)
        {
            e = MatchEngineTests.NewEngine(seed);
            Freeze(e);
            MatchPlayer keeper = e.Teams[1].Players[0];
            foreach (AttributeId id in new[] { AttributeId.Reflexes, AttributeId.Handling, AttributeId.GoalkeeperPositioning })
            {
                keeper.Attr[id] = keeperSkill;
            }

            e.TestPlace(keeper, new Vec2(e.Pitch.HalfLength - 1f, 1.5f));
            MatchPlayer shooter = e.Teams[0].Players[9];
            e.TestPlace(shooter, new Vec2(e.Pitch.HalfLength - 16f, 0f));
            e.TestKick(shooter, new Vec3(speed, 0f, 0.8f), true);
            for (int i = 0; i < 90 && e.Phase == MatchPhase.Playing; i++)
            {
                e.Tick();
            }

            return e.Teams[0].Stats.Goals > 0;
        }

        [Test]
        public void BetterKeeper_ConcedesLess()
        {
            int poor = 0, good = 0;
            for (ulong seed = 0; seed < 300; seed++)
            {
                poor += ShotAtKeeper(seed, 15, 24f, out _) ? 1 : 0;
                good += ShotAtKeeper(seed, 95, 24f, out _) ? 1 : 0;
            }

            Assert.That(good, Is.LessThan(poor - 20), $"arquero malo encaja {poor}, bueno {good} de 300");
        }

        [Test]
        public void SlowShotAtTheKeeper_IsUsuallyCaught()
        {
            int caught = 0;
            for (ulong seed = 0; seed < 100; seed++)
            {
                ShotAtKeeper(seed, 70, 12f, out MatchEngine e);
                caught += e.Owner == e.Teams[1].Players[0] ? 1 : 0;
            }

            Assert.That(caught, Is.GreaterThan(60), $"atrapados {caught} de 100");
        }

        // ---- Estadísticas coherentes con los eventos -----------------------------------------------------------------

        [Test]
        public void MatchStats_AreConsistentWithTheEventLog()
        {
            for (ulong seed = 0; seed < 25; seed++)
            {
                MatchEngine e = MatchEngineTests.NewEngine(700 + seed, (int)(seed % 5), (int)((seed + 2) % 5));
                long playing = 0;
                while (!e.IsFinished)
                {
                    e.Tick();
                    playing += e.Phase == MatchPhase.Playing ? 1 : 0;
                }

                for (int t = 0; t < 2; t++)
                {
                    TeamStats s = e.Teams[t].Stats;
                    TeamStats o = e.Teams[1 - t].Stats;
                    Assert.That(e.Events.CountOf(MatchEventType.Goal, t), Is.EqualTo(s.Goals), "eventos de gol = goles");
                    Assert.That(e.Events.CountOf(MatchEventType.Save, t), Is.EqualTo(s.Saves), "eventos de parada = paradas");
                    Assert.That(e.Events.CountOf(MatchEventType.Shot, t), Is.EqualTo(s.Shots), "eventos de tiro = tiros");
                    Assert.That(s.Shots, Is.GreaterThanOrEqualTo(s.ShotsOnTarget - s.Goals), "a puerta sin contar goles ≤ tiros");
                    Assert.That(s.Saves, Is.LessThanOrEqualTo(o.ShotsOnTarget), "cada parada es un tiro a puerta del rival");
                    Assert.That(s.PassesCompleted + s.PassesIntercepted, Is.LessThanOrEqualTo(s.Passes));
                    Assert.That(s.TacklesWon, Is.LessThanOrEqualTo(s.Tackles));
                    Assert.That(s.Possessions, Is.GreaterThan(0));
                    Assert.That(s.Corners, Is.EqualTo(e.Events.CountOf(MatchEventType.Corner, t)));
                }

                long possession = e.Teams[0].Stats.PossessionTicks + e.Teams[1].Stats.PossessionTicks;
                Assert.That(possession, Is.LessThanOrEqualTo(playing), "no hay más posesión que juego");
                Assert.That(possession, Is.GreaterThan(playing / 2), "casi siempre alguien tiene el balón");
            }
        }

        // ---- Dificultad -------------------------------------------------------------------------------------------------

        [Test]
        public void HigherDifficulty_WinsMoreOften_ButNotAlways()
        {
            var config = new MatchConfig();
            int legendWins = 0, beginnerWins = 0, beginnerGoals = 0;
            const int n = 60;
            for (ulong seed = 0; seed < n; seed++)
            {
                var rng = new SeededRandom(900 + seed);
                IRandom teams = rng.Fork("teams");
                MatchTeamSetup a = RosterFactory.CreateTeam(teams, "A", 65f, Formations.F442, config.Styles[0]);
                MatchTeamSetup b = RosterFactory.CreateTeam(teams, "B", 65f, Formations.F442, config.Styles[0]);
                bool legendHome = seed % 2 == 0; // se alternan los lados
                var e = new MatchEngine(config, rng.Fork("engine"), a, b, legendHome ? 4 : 0, legendHome ? 0 : 4);
                while (!e.IsFinished)
                {
                    e.Tick();
                }

                int legend = legendHome ? 0 : 1;
                int diff = e.Teams[legend].Stats.Goals - e.Teams[1 - legend].Stats.Goals;
                legendWins += diff > 0 ? 1 : 0;
                beginnerGoals += e.Teams[1 - legend].Stats.Goals;
                beginnerWins += diff < 0 ? 1 : 0;
            }

            Assert.That(legendWins, Is.GreaterThan(n * 6 / 10), $"Leyenda gana {legendWins} de {n}");
            // «Pero no es invencible»: ganar los 60 es posible, así que se pide que el Principiante también haga goles.
            Assert.That(beginnerGoals, Is.GreaterThan(0), "el Principiante también marca");
            Assert.That(beginnerWins, Is.LessThan(legendWins / 3));
        }
    }
}
