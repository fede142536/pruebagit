using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Football.Core.Common;
using Football.Core.MatchCore;
using NUnit.Framework;
using Football.Core.Domain;

namespace Football.Core.Tests.MatchCore
{
    public class MatchEngineTests
    {
        internal static MatchEngine NewEngine(ulong seed, int homeStyle = 0, int awayStyle = 0, float rating = 65f, MatchConfig cfg = null)
        {
            cfg = cfg ?? new MatchConfig();
            var rng = new SeededRandom(seed);
            IRandom teams = rng.Fork("teams");
            MatchTeamSetup home = RosterFactory.CreateTeam(teams, "H", rating, Formations.F442, cfg.Styles[homeStyle]);
            MatchTeamSetup away = RosterFactory.CreateTeam(teams, "A", rating, Formations.F433, cfg.Styles[awayStyle]);
            return new MatchEngine(cfg, rng.Fork("engine"), home, away);
        }

        internal static void RunToEnd(MatchEngine e)
        {
            int guard = 0;
            while (!e.IsFinished && guard++ < 100000)
            {
                e.Tick();
            }
        }

        // ---- Determinismo (A1) ----------------------------------------------------------------------------------

        [Test]
        public void SameSeed_GivesTheSameMatch_100Times()
        {
            MatchEngine reference = NewEngine(2024);
            RunToEnd(reference);
            ulong hash = reference.StateHash();
            int goalsHome = reference.Teams[0].Stats.Goals, goalsAway = reference.Teams[1].Stats.Goals;

            for (int i = 0; i < 100; i++)
            {
                MatchEngine e = NewEngine(2024);
                RunToEnd(e);
                Assert.That(e.StateHash(), Is.EqualTo(hash), "ejecución " + i);
                Assert.That(e.Teams[0].Stats.Goals, Is.EqualTo(goalsHome));
                Assert.That(e.Teams[1].Stats.Goals, Is.EqualTo(goalsAway));
            }
        }

        [Test]
        public void SameSeedAndSameInputs_GiveTheSameMatch()
        {
            ulong Play()
            {
                MatchEngine e = NewEngine(77);
                e.SetHuman(0, true);
                for (int t = 0; t < 6000 && !e.IsFinished; t++)
                {
                    var input = new MatchInput { Move = new Vec2((float)Math.Cos(t * 0.01), (float)Math.Sin(t * 0.013)), Sprint = t % 300 < 100 };
                    input.Pass = t % 97 == 0;
                    input.Shoot = t % 211 == 0;
                    input.Tackle = t % 53 == 0;
                    e.SetInput(0, input);
                    e.Tick();
                }

                return e.StateHash();
            }

            Assert.That(Play(), Is.EqualTo(Play()));
        }

        [Test]
        public void DifferentSeeds_GiveDifferentMatches()
        {
            MatchEngine a = NewEngine(1), b = NewEngine(2);
            RunToEnd(a);
            RunToEnd(b);
            Assert.That(a.StateHash(), Is.Not.EqualTo(b.StateHash()));
        }

        // ---- Estructura del partido -----------------------------------------------------------------------------

        [Test]
        public void Match_HasTwoHalves_SwapsSidesAndEndsAround90Minutes()
        {
            MatchEngine e = NewEngine(5);
            Assert.That(e.Teams[0].AttackDirection, Is.EqualTo(1f));
            Assert.That(e.Phase, Is.EqualTo(MatchPhase.SetPiece));
            Assert.That(e.CurrentRestart.Kind, Is.EqualTo(RestartKind.KickOff));
            Assert.That(e.CurrentRestart.Team, Is.EqualTo(0));

            bool sawSecondHalfKickoff = false;
            int guard = 0;
            while (!e.IsFinished && guard++ < 100000)
            {
                e.Tick();
                if (e.Half == 2 && e.HalfElapsed < 0.1f && e.Phase == MatchPhase.SetPiece)
                {
                    sawSecondHalfKickoff = e.CurrentRestart.Team == 1 && e.CurrentRestart.Kind == RestartKind.KickOff;
                }
            }

            Assert.That(e.IsFinished, Is.True);
            Assert.That(sawSecondHalfKickoff, Is.True, "la segunda parte la saca el visitante");
            Assert.That(e.Half, Is.EqualTo(2));
            Assert.That(e.Teams[0].AttackDirection, Is.EqualTo(-1f), "los equipos cambian de lado en el descanso");
            Assert.That(e.Minute, Is.InRange(90f + e.AddedMinutes - 0.5f, 90f + e.AddedMinutes + 3.5f), "el partido termina en el primer parón después del tiempo agregado");
            Assert.That(e.Events.CountOf(MatchEventType.HalfTime), Is.EqualTo(1));
            Assert.That(e.Events.CountOf(MatchEventType.FullTime), Is.EqualTo(1));
            Assert.That(e.Events.CountOf(MatchEventType.KickOff), Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void FinishedMatch_IgnoresFurtherTicks()
        {
            MatchEngine e = NewEngine(5);
            RunToEnd(e);
            ulong hash = e.StateHash();
            long ticks = e.TickCount;
            e.Tick();
            Assert.That(e.StateHash(), Is.EqualTo(hash));
            Assert.That(e.TickCount, Is.EqualTo(ticks));
        }

        // ---- Invariantes, memoria y rendimiento (A4, A5, A6) -------------------------------------------------------

        [Test]
        public void ManyMatches_NeverProduceInvalidState()
        {
            for (ulong seed = 100; seed < 120; seed++)
            {
                MatchEngine e = NewEngine(seed, (int)(seed % 5), (int)((seed / 5) % 5));
                float limitX = e.Pitch.HalfLength + e.Config.Pitch.PlayerMargin + 0.5f;
                float limitY = e.Pitch.HalfWidth + e.Config.Pitch.PlayerMargin + 0.5f;
                int guard = 0;
                while (!e.IsFinished && guard++ < 100000)
                {
                    e.Tick();
                    if (guard % 30 != 0)
                    {
                        continue;
                    }

                    foreach (MatchPlayer p in e.Players)
                    {
                        Assert.That(float.IsFinite(p.Pos.X) && float.IsFinite(p.Pos.Y), Is.True, "posición finita");
                        Assert.That(Math.Abs(p.Pos.X), Is.LessThanOrEqualTo(limitX));
                        Assert.That(Math.Abs(p.Pos.Y), Is.LessThanOrEqualTo(limitY));
                        Assert.That(p.Stamina, Is.InRange(0f, 1f));
                    }

                    Assert.That(float.IsFinite(e.Ball.Pos.X) && float.IsFinite(e.Ball.Pos.Y) && float.IsFinite(e.Ball.Pos.Z), Is.True);
                    Assert.That(e.Ball.Pos.Z, Is.GreaterThanOrEqualTo(0f));
                }

                Assert.That(e.IsFinished, Is.True, "seed " + seed);
            }
        }

        private const double TargetGoals = 2.5;
        private const double GoalsStandardError = 0.15;

        [Test]
        public void HundredMatches_FinishWithPlausibleGoalsAndStats()
        {
            double goals = 0, shots = 0, passes = 0, completed = 0;
            const int n = 100;
            for (ulong seed = 1000; seed < 1000 + n; seed++)
            {
                MatchEngine e = NewEngine(seed, (int)(seed % 5), (int)((seed / 3) % 5));
                RunToEnd(e);
                Assert.That(e.IsFinished, Is.True);
                for (int t = 0; t < 2; t++)
                {
                    goals += e.Teams[t].Stats.Goals;
                    shots += e.Teams[t].Stats.Shots;
                    passes += e.Teams[t].Stats.Passes;
                    completed += e.Teams[t].Stats.PassesCompleted;
                }
            }

            // Objetivo medido en Docs/BALANCE.md (1.000 partidos): ≈2,5 goles y ≈85 % de acierto de pase (real: 82–87 %). Con 100 partidos el
            // error típico de la media de goles es ≈0,15: se admite ±3 errores típicos, así un cambio que mueva el
            // equilibrio de verdad hace fallar la prueba, y el ruido de la semilla no.
            Assert.That(goals / n, Is.InRange(TargetGoals - 3 * GoalsStandardError, TargetGoals + 3 * GoalsStandardError), "goles por partido (A4)");
            Assert.That(shots / n, Is.InRange(8.0, 14.0), "tiros por partido (ambos equipos)");
            Assert.That(completed / passes, Is.InRange(0.82, 0.89), "acierto de pase (BALANCE.md §1.1: 82–87 % real)");
        }

        [Test]
        public void Tick_DoesNotAllocateInSteadyState()
        {
            MatchEngine e = NewEngine(31);
            for (int i = 0; i < 4000; i++)
            {
                e.Tick(); // calentamiento: crece el registro de eventos y se compila el JIT
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 6000; i++)
            {
                e.Tick();
            }

            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero, "bytes asignados en 6000 pasos (A5)");
        }

        [Test]
        public void Snapshot_CopiesTheStateWithoutAllocating()
        {
            MatchEngine e = NewEngine(31);
            var snap = new MatchSnapshot();
            for (int i = 0; i < 600; i++)
            {
                e.Tick();
            }

            snap.CopyFrom(e);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++)
            {
                snap.CopyFrom(e);
            }

            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
            Assert.That(snap.PlayerX[3], Is.EqualTo(e.Players[3].Pos.X));
            Assert.That(snap.BallX, Is.EqualTo(e.Ball.Pos.X));
            Assert.That(snap.Tick, Is.EqualTo(e.TickCount));
        }

        [Test]
        public void FullMatch_RunsInUnderTwoSeconds()
        {
            MatchEngine warm = NewEngine(3);
            RunToEnd(warm);

            var timer = Stopwatch.StartNew();
            MatchEngine e = NewEngine(4);
            RunToEnd(e);
            timer.Stop();

            Assert.That(timer.Elapsed.TotalSeconds, Is.LessThan(2.0), "un partido de 90' en menos de 2 s (A6)");
        }

        // ---- Configuración en datos -----------------------------------------------------------------------------

        [Test]
        public void BalanceFile_MatchesTheDefaultConfiguration()
        {
            string dir = AppContext.BaseDirectory;
            string path = null;
            while (dir != null)
            {
                string candidate = Path.Combine(dir, "Data", "balance", "match.json");
                if (File.Exists(candidate))
                {
                    path = candidate;
                    break;
                }

                dir = Path.GetDirectoryName(dir);
            }

            Assert.That(path, Is.Not.Null, "Data/balance/match.json debe existir");
            var options = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };
            MatchConfig fromFile = JsonSerializer.Deserialize<MatchConfig>(File.ReadAllText(path), options);
            Assert.That(JsonSerializer.Serialize(fromFile, options), Is.EqualTo(JsonSerializer.Serialize(new MatchConfig(), options)),
                "regenerar con: dotnet run --project Tools/HeadlessSim -- --dump-config Data/balance/match.json");
        }

        [Test]
        public void Config_HasFiveDifficultiesAndFiveStyles()
        {
            var cfg = new MatchConfig();
            Assert.That(cfg.Difficulties, Has.Length.EqualTo(5));
            Assert.That(cfg.Styles, Has.Length.EqualTo(5));
            Assert.That(System.Linq.Enumerable.Select(cfg.Styles, st => st.Name), Is.Unique);
            for (int i = 1; i < cfg.Difficulties.Length; i++)
            {
                Assert.That(cfg.Difficulties[i].PassErrorScale, Is.LessThan(cfg.Difficulties[i - 1].PassErrorScale),
                    "cada nivel ejecuta mejor que el anterior");
                Assert.That(cfg.Difficulties[i].DecisionNoiseScale, Is.LessThan(cfg.Difficulties[i - 1].DecisionNoiseScale));
            }
        }

        // ---- Estilos distintos ----------------------------------------------------------------------------------

        [Test]
        public void HighPressTeams_PlayHigherThanDefensiveTeams()
        {
            double highLine = AverageLine(styleIndex: 3);
            double deepLine = AverageLine(styleIndex: 4);
            Assert.That(highLine, Is.GreaterThan(deepLine + 0.03), $"altura media: presión alta {highLine:F3}, defensivo {deepLine:F3}");
        }

        private static double AverageLine(int styleIndex)
        {
            double sum = 0;
            long samples = 0;
            for (ulong seed = 500; seed < 510; seed++)
            {
                MatchEngine e = NewEngine(seed, homeStyle: styleIndex, awayStyle: 0);
                int guard = 0;
                while (!e.IsFinished && guard++ < 100000)
                {
                    e.Tick();
                    if (guard % 20 != 0 || e.Phase != MatchPhase.Playing)
                    {
                        continue;
                    }

                    TeamState team = e.Teams[0];
                    for (int i = 1; i < 11; i++)
                    {
                        sum += e.Pitch.Progress(team.AttackDirection, team.Players[i].Pos.X);
                        samples++;
                    }
                }
            }

            return sum / samples;
        }
    }
}
