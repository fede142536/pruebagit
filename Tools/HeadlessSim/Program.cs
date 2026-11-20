using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Football.Core.Common;
using Football.Core.MatchCore;

namespace Football.Tools.HeadlessSim
{
    /// <summary>
    /// Simula partidos completos sin gráficos para medir el motor.
    /// Uso: dotnet run --project Tools/HeadlessSim -- --matches 1000 --seed 1 [--config Data/balance/match.json]
    ///      [--difficulty 2] [--rating 65] [--dump-config Data/balance/match.json]
    /// </summary>
    public static class Program
    {
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };

        public static int Main(string[] args)
        {
            int matches = 100;
            ulong seed = 1;
            int difficulty = 2;
            int awayDifficulty = -1;
            float rating = 65f;
            string configPath = null;
            string dumpPath = null;
            bool verbose = false;
            float asciiEvery = 0f;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--matches": matches = int.Parse(args[++i]); break;
                    case "--seed": seed = ulong.Parse(args[++i]); break;
                    case "--difficulty": difficulty = int.Parse(args[++i]); break;
                    case "--away-difficulty": awayDifficulty = int.Parse(args[++i]); break;
                    case "--rating": rating = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
                    case "--config": configPath = args[++i]; break;
                    case "--dump-config": dumpPath = args[++i]; break;
                    case "--verbose": verbose = true; break;
                    case "--ascii": asciiEvery = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
                }
            }

            MatchConfig config = configPath != null
                ? JsonSerializer.Deserialize<MatchConfig>(File.ReadAllText(configPath), Json)
                : new MatchConfig();

            if (dumpPath != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dumpPath)));
                File.WriteAllText(dumpPath, JsonSerializer.Serialize(config, Json) + "\n");
                Console.WriteLine("Configuración escrita en " + dumpPath);
                return 0;
            }

            if (asciiEvery > 0f)
            {
                return Ascii(config, seed, difficulty, rating, asciiEvery);
            }

            return Run(config, matches, seed, difficulty, awayDifficulty < 0 ? difficulty : awayDifficulty, rating, verbose);
        }

        private static int Run(MatchConfig config, int matches, ulong seed, int difficulty, int awayDifficulty, float rating, bool verbose)
        {
            var total = new double[2, 14];
            int failures = 0, unfinished = 0, homeWins = 0, draws = 0, awayWins = 0, outOfBounds = 0;
            double goals = 0, maxMs = 0, totalMs = 0;
            var goalHistogram = new int[10];
            var root = new SeededRandom(seed);
            var timer = new Stopwatch();

            for (int m = 0; m < matches; m++)
            {
                IRandom rng = root.Fork("match" + m);
                IRandom teams = rng.Fork("teams");
                TeamStyle homeStyle = config.Styles[teams.NextInt(0, config.Styles.Length)];
                TeamStyle awayStyle = config.Styles[teams.NextInt(0, config.Styles.Length)];
                Formation homeFormation = Formations.All[teams.NextInt(0, Formations.All.Length)];
                Formation awayFormation = Formations.All[teams.NextInt(0, Formations.All.Length)];
                MatchTeamSetup home = RosterFactory.CreateTeam(teams, "H", rating + teams.NextGaussian(0, 5), homeFormation, homeStyle);
                MatchTeamSetup away = RosterFactory.CreateTeam(teams, "A", rating + teams.NextGaussian(0, 5), awayFormation, awayStyle);

                try
                {
                    var engine = new MatchEngine(config, rng.Fork("engine"), home, away, difficulty, awayDifficulty);
                    timer.Restart();
                    int guard = 0;
                    while (!engine.IsFinished && guard++ < 200000)
                    {
                        engine.Tick();
                        if ((guard & 63) == 0)
                        {
                            outOfBounds += CountInvalid(engine);
                        }
                    }

                    timer.Stop();
                    totalMs += timer.Elapsed.TotalMilliseconds;
                    maxMs = Math.Max(maxMs, timer.Elapsed.TotalMilliseconds);
                    if (!engine.IsFinished)
                    {
                        unfinished++;
                    }

                    Accumulate(total, engine);
                    int g0 = engine.Teams[0].Stats.Goals, g1 = engine.Teams[1].Stats.Goals;
                    goals += g0 + g1;
                    goalHistogram[Math.Min(9, g0 + g1)]++;
                    if (g0 > g1) homeWins++; else if (g0 == g1) draws++; else awayWins++;
                    if (verbose)
                    {
                        Console.WriteLine($"#{m} {homeStyle.Name} {g0}-{g1} {awayStyle.Name}  ({timer.Elapsed.TotalMilliseconds:F0} ms)");
                    }
                }
                catch (Exception e)
                {
                    failures++;
                    Console.WriteLine($"Partido {m}: EXCEPCIÓN {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
                }
            }

            int ok = matches - failures;
            if (ok == 0)
            {
                return 1;
            }

            string[] names = { "Goles", "Tiros", "A puerta", "Paradas", "Pases", "Pases ok", "Interceptados", "Perdidos fuera", "Faltas", "Amarillas", "Rojas", "Córners", "Fueras de juego" };
            Console.WriteLine($"Partidos: {matches}  semilla: {seed}  dificultad: {config.Difficulties[difficulty].Name} (local) vs {config.Difficulties[awayDifficulty].Name} (visitante)  calidad media: {rating}");
            Console.WriteLine($"Excepciones: {failures}  sin terminar: {unfinished}  estados inválidos (NaN/fuera del campo): {outOfBounds}");
            Console.WriteLine($"Tiempo por partido: media {totalMs / ok:F0} ms, máximo {maxMs:F0} ms");
            Console.WriteLine($"Goles por partido: {goals / ok:F2}   victorias local/empate/visitante: {homeWins * 100.0 / ok:F0}% / {draws * 100.0 / ok:F0}% / {awayWins * 100.0 / ok:F0}%");
            Console.WriteLine("Distribución de goles totales (0..9+): " + string.Join(" ", goalHistogram));
            Console.WriteLine($"{"Por equipo y partido",-22}");
            for (int k = 0; k < names.Length; k++)
            {
                double v = (total[0, k] + total[1, k]) / (2.0 * ok);
                Console.WriteLine($"  {names[k],-18} {v,8:F2}");
            }

            Console.WriteLine($"  Pases completados  {100.0 * (total[0, 5] + total[1, 5]) / Math.Max(1.0, total[0, 4] + total[1, 4]),7:F1}%");
            return failures == 0 && unfinished == 0 && outOfBounds == 0 ? 0 : 1;
        }

        /// <summary>Dibuja el campo en texto cada cierto número de segundos reales del primer partido.</summary>
        private static int Ascii(MatchConfig config, ulong seed, int difficulty, float rating, float every)
        {
            IRandom rng = new SeededRandom(seed).Fork("match0");
            IRandom teams = rng.Fork("teams");
            MatchTeamSetup home = RosterFactory.CreateTeam(teams, "H", rating, Formations.F442, config.Styles[0]);
            MatchTeamSetup away = RosterFactory.CreateTeam(teams, "A", rating, Formations.F433, config.Styles[3]);
            var engine = new MatchEngine(config, rng.Fork("engine"), home, away, difficulty, difficulty);
            int stride = (int)(every * config.TickRate);
            while (!engine.IsFinished)
            {
                engine.Tick();
                if (engine.TickCount % stride == 0)
                {
                    Console.WriteLine(Frame(engine));
                }
            }

            return 0;
        }

        private static string Frame(MatchEngine e)
        {
            const int cols = 70, rows = 23;
            var grid = new char[rows, cols];
            for (int r = 0; r < rows; r++) for (int c = 0; c < cols; c++) grid[r, c] = r == 0 || r == rows - 1 ? '-' : (c == cols / 2 ? ':' : ' ');
            void Put(float x, float y, char ch)
            {
                int c = (int)((x + e.Pitch.HalfLength) / e.Pitch.Length * (cols - 1));
                int r = (int)((y + e.Pitch.HalfWidth) / e.Pitch.Width * (rows - 1));
                if (c >= 0 && c < cols && r >= 0 && r < rows) grid[r, c] = ch;
            }

            foreach (MatchPlayer p in e.Players)
            {
                if (p.SentOff) continue;
                char ch = p.Team == 0 ? (p.IsKeeper ? 'g' : 'h') : (p.IsKeeper ? 'k' : 'a');
                if (e.Owner == p) ch = char.ToUpper(ch);
                Put(p.Pos.X, p.Pos.Y, ch);
            }

            Put(e.Ball.Pos.X, e.Ball.Pos.Y, e.Ball.Pos.Z > 1f ? '^' : 'O');
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"== min {e.Minute:F1} parte {e.Half}  {e.Teams[0].Stats.Goals}-{e.Teams[1].Stats.Goals}  fase {e.Phase} ball z={e.Ball.Pos.Z:F1} v={e.Ball.Vel.Length:F1}  (h ataca {(e.Teams[0].AttackDirection > 0 ? "→" : "←")})");
            for (int r = 0; r < rows; r++) { for (int c = 0; c < cols; c++) sb.Append(grid[r, c]); sb.AppendLine(); }
            return sb.ToString();
        }

        private static void Accumulate(double[,] total, MatchEngine e)
        {
            for (int t = 0; t < 2; t++)
            {
                TeamStats s = e.Teams[t].Stats;
                total[t, 0] += s.Goals;
                total[t, 1] += s.Shots;
                total[t, 2] += s.ShotsOnTarget;
                total[t, 3] += s.Saves;
                total[t, 4] += s.Passes;
                total[t, 5] += s.PassesCompleted;
                total[t, 6] += s.PassesIntercepted;
                total[t, 7] += s.PassesLost;
                total[t, 8] += s.Fouls;
                total[t, 9] += s.Yellows;
                total[t, 10] += s.Reds;
                total[t, 11] += s.Corners;
                total[t, 12] += s.Offsides;
            }
        }

        private static int CountInvalid(MatchEngine e)
        {
            int bad = 0;
            float marginX = e.Pitch.HalfLength + e.Config.Pitch.PlayerMargin + 1f;
            float marginY = e.Pitch.HalfWidth + e.Config.Pitch.PlayerMargin + 1f;
            foreach (MatchPlayer p in e.Players)
            {
                if (float.IsNaN(p.Pos.X) || float.IsNaN(p.Pos.Y) || Math.Abs(p.Pos.X) > marginX || Math.Abs(p.Pos.Y) > marginY)
                {
                    bad++;
                }
            }

            BallState b = e.Ball;
            if (float.IsNaN(b.Pos.X) || float.IsNaN(b.Pos.Y) || float.IsNaN(b.Pos.Z) || b.Pos.Z < -0.01f || b.Pos.Z > 60f)
            {
                bad++;
            }

            return bad;
        }
    }
}
