using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Linq;
using Football.Core.Common;
using Football.Core.Data;
using Football.Core.MatchCore;
using Football.Core.Domain;

namespace Football.Tools.HeadlessSim
{
    /// <summary>
    /// Simula partidos completos sin gráficos para medir el motor.
    /// Uso: dotnet run --project Tools/HeadlessSim -- --matches 1000 --seed 1 [--config Data/balance/match.json]
    ///      [--difficulty 2] [--rating 65] [--dump-config Data/balance/match.json] [--indoor (cancha cerrada 3 contra 3)]
    /// </summary>
    public static class Program
    {
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };

        private const int MaxTicksPerMatch = 200000;
        private static Formation[] s_formations = Formations.All;

        /// <summary>Punto de entrada: interpreta las opciones y ejecuta la simulación pedida.</summary>
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
            string teamsPath = null;
            bool indoor = false;
            bool printReport = false;
            for (int i = 0; i < args.Length; i++)
            {
                bool needsValue = args[i] != "--verbose" && args[i] != "--indoor" && args[i] != "--report";
                if (needsValue && i + 1 >= args.Length)
                {
                    Console.Error.WriteLine($"Falta el valor de {args[i]}.");
                    return 2;
                }

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
                    case "--indoor": indoor = true; break;
                    case "--report": printReport = true; break;
                    case "--teams": teamsPath = args[++i]; break;
                    case "--ascii": asciiEvery = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
                    default:
                        Console.Error.WriteLine($"Opción desconocida: {args[i]}");
                        return 2;
                }
            }

            MatchConfig config = configPath != null
                ? JsonSerializer.Deserialize<MatchConfig>(File.ReadAllText(configPath), Json)
                : indoor ? MatchConfig.CreateIndoor() : new MatchConfig();
            s_formations = indoor ? Formations.Indoor : Formations.All;

            int levels = config.Difficulties.Length;
            if (difficulty < 0 || difficulty >= levels || awayDifficulty >= levels || matches <= 0)
            {
                Console.Error.WriteLine($"Valores fuera de rango: --difficulty y --away-difficulty van de 0 a {levels - 1}; --matches debe ser positivo.");
                return 2;
            }

            if (dumpPath != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dumpPath)));
                File.WriteAllText(dumpPath, JsonSerializer.Serialize(config, Json) + "\n");
                Console.WriteLine("Configuración escrita en " + dumpPath);
                return 0;
            }

            if (teamsPath != null)
            {
                return League(config, teamsPath, matches, seed, difficulty);
            }

            if (asciiEvery > 0f)
            {
                return Ascii(config, seed, difficulty, rating, asciiEvery);
            }

            return Run(config, matches, seed, difficulty, awayDifficulty < 0 ? difficulty : awayDifficulty, rating, verbose, printReport);
        }

        private static int Run(MatchConfig config, int matches, ulong seed, int difficulty, int awayDifficulty, float rating, bool verbose,
            bool printReport)
        {
            var total = new double[2, Columns];
            double assists = 0, scoredGoals = 0, staminaSum = 0, staminaCount = 0, staminaMin = 0, subsUsed = 0;
            MatchReport last = null;
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
                Formation homeFormation = s_formations[teams.NextInt(0, s_formations.Length)];
                Formation awayFormation = s_formations[teams.NextInt(0, s_formations.Length)];
                MatchTeamSetup home = RosterFactory.CreateTeam(teams, "H", rating + teams.NextGaussian(0, 5), homeFormation, homeStyle);
                MatchTeamSetup away = RosterFactory.CreateTeam(teams, "A", rating + teams.NextGaussian(0, 5), awayFormation, awayStyle);

                try
                {
                    var engine = new MatchEngine(config, rng.Fork("engine"), home, away, difficulty, awayDifficulty);
                    timer.Restart();
                    int guard = 0;
                    while (!engine.IsFinished && guard++ < MaxTicksPerMatch)
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

                    MatchReport report = engine.BuildReport();
                    Accumulate(total, report);
                    last = report;
                    for (int t = 0; t < 2; t++)
                    {
                        staminaMin += engine.Teams[t].Players.Min(p => p.Stamina);
                    }

                    foreach (TeamReport tr in report.Teams)
                    {
                        foreach (PlayerReport pr in tr.Players)
                        {
                            assists += pr.Stats.Assists;
                            scoredGoals += pr.Stats.Goals;
                            if (pr.Started && pr.Minutes >= 1.98f * config.Rules.MinutesPerHalf && pr.Role != Football.Core.Domain.Position.Goalkeeper)
                            {
                                staminaSum += pr.StaminaLeft;
                                staminaCount++;
                            }

                            if (!pr.Started)
                            {
                                subsUsed++;
                            }
                        }
                    }
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

            string[] names = { "Goles", "Tiros", "A puerta", "Paradas", "Pases", "Pases ok", "Interceptados", "Perdidos fuera", "Faltas", "Amarillas", "Rojas", "Córners", "Fueras de juego", "Posesiones", "Entradas", "Entradas ganadas", null, "xG", "Recuperaciones", "Lesiones", "Distancia (km)" };
            Console.WriteLine($"Partidos: {matches}  semilla: {seed}  dificultad: {config.Difficulties[difficulty].Name} (local) vs {config.Difficulties[awayDifficulty].Name} (visitante)  calidad media: {rating}");
            Console.WriteLine($"Excepciones: {failures}  sin terminar: {unfinished}  estados inválidos (NaN/fuera del campo): {outOfBounds}");
            Console.WriteLine($"Tiempo por partido: media {totalMs / ok:F0} ms, máximo {maxMs:F0} ms");
            Console.WriteLine($"Goles por partido: {goals / ok:F2}   victorias local/empate/visitante: {homeWins * 100.0 / ok:F0}% / {draws * 100.0 / ok:F0}% / {awayWins * 100.0 / ok:F0}%");
            Console.WriteLine("Distribución de goles totales (0..9+): " + string.Join(" ", goalHistogram));
            Console.WriteLine($"{"Por equipo y partido",-22}");
            for (int k = 0; k < names.Length; k++)
            {
                if (names[k] == null)
                {
                    continue;
                }

                double v = (total[0, k] + total[1, k]) / (2.0 * ok);
                Console.WriteLine($"  {names[k],-18} {v,8:F2}");
            }

            Console.WriteLine($"  Pases completados  {100.0 * (total[0, 5] + total[1, 5]) / Math.Max(1.0, total[0, 4] + total[1, 4]),7:F1}%");
            double possessions = Math.Max(1.0, total[0, 13] + total[1, 13]);
            Console.WriteLine($"  Duración media de una posesión {(total[0, 16] + total[1, 16]) / possessions / config.TickRate,6:F1} s (tiempo de simulación)");
            Console.WriteLine($"  Pases por posesión             {(total[0, 5] + total[1, 5]) / possessions,6:F2}");
            Console.WriteLine($"  Acierto de las entradas        {100.0 * (total[0, 15] + total[1, 15]) / Math.Max(1.0, total[0, 14] + total[1, 14]),6:F1}%");
            Console.WriteLine($"  Goles / xG                     {(total[0, 0] + total[1, 0]) / Math.Max(0.01, total[0, 17] + total[1, 17]),6:F2}");
            Console.WriteLine($"  Remates al arco                {100.0 * (total[0, 2] + total[1, 2]) / Math.Max(1.0, total[0, 1] + total[1, 1]),6:F1}% de los tiros");
            Console.WriteLine($"  Goles con asistencia           {100.0 * assists / Math.Max(1.0, scoredGoals),6:F1}%");
            Console.WriteLine($"  Resistencia al final           {100.0 * staminaSum / Math.Max(1.0, staminaCount),6:F1}% (jugadores de campo que jugaron todo el partido); la más baja del equipo {100.0 * staminaMin / (2.0 * ok),5:F1}%");
            Console.WriteLine($"  Cambios por equipo             {subsUsed / (2.0 * ok),6:F2}");
            if (printReport && last != null)
            {
                PrintReport(last);
            }

            return failures == 0 && unfinished == 0 && outOfBounds == 0 ? 0 : 1;
        }

        /// <summary>Muestra el once de cada equipo de un archivo y juega todos contra todos (ida y vuelta) varias veces.</summary>
        private static int League(MatchConfig config, string path, int rounds, ulong seed, int difficulty)
        {
            TeamDatabase db = JsonSerializer.Deserialize<TeamDatabase>(File.ReadAllText(path), Json);
            foreach (TeamDefinition t in db.Teams)
            {
                Formation f = TeamBuilder.ParseFormation(t.Formation);
                int[] xi = TeamBuilder.PickLineup(t, f);
                Console.WriteLine($"{t.Name} ({t.Formation}, {t.Style}) media del once {TeamBuilder.LineupRating(t):F1}");
                Console.WriteLine("  " + string.Join(", ", xi.Select((p, s) => $"{f.Slots[s].Role}: {t.Players[p].Name} {t.Players[p].Rating}")));
            }

            int n = db.Teams.Length;
            var points = new int[n];
            var goalsFor = new int[n];
            var goalsAgainst = new int[n];
            var root = new SeededRandom(seed);
            int played = 0;
            for (int r = 0; r < Math.Max(1, rounds); r++)
            {
                for (int h = 0; h < n; h++)
                {
                    for (int a = 0; a < n; a++)
                    {
                        if (h == a) continue;
                        IRandom rng = root.Fork($"{r}-{h}-{a}");
                        var engine = new MatchEngine(config, rng.Fork("engine"),
                            TeamBuilder.Build(db.Teams[h], config, rng.Fork("h")), TeamBuilder.Build(db.Teams[a], config, rng.Fork("a")),
                            difficulty, difficulty);
                        for (int guard = 0; !engine.IsFinished; guard++)
                        {
                            if (guard >= MaxTicksPerMatch)
                            {
                                Console.Error.WriteLine($"Partido sin terminar: {db.Teams[h].Name} - {db.Teams[a].Name}");
                                return 1;
                            }

                            engine.Tick();
                        }

                        int gh = engine.Teams[0].Stats.Goals, ga = engine.Teams[1].Stats.Goals;
                        goalsFor[h] += gh; goalsAgainst[h] += ga; goalsFor[a] += ga; goalsAgainst[a] += gh;
                        if (gh > ga) points[h] += 3; else if (gh < ga) points[a] += 3; else { points[h]++; points[a]++; }
                        played++;
                    }
                }
            }

            Console.WriteLine($"\nTabla tras {played} partidos ({Math.Max(1, rounds)} vueltas de ida y vuelta):");
            foreach (int i in Enumerable.Range(0, n).OrderByDescending(i => points[i]).ThenByDescending(i => goalsFor[i] - goalsAgainst[i]))
            {
                Console.WriteLine($"  {db.Teams[i].Name,-26} {points[i],4} pts  {goalsFor[i],4}-{goalsAgainst[i],-4} media {TeamBuilder.LineupRating(db.Teams[i]):F1}");
            }

            return 0;
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

        private const int Columns = 21;

        private static void Accumulate(double[,] total, MatchReport report)
        {
            for (int t = 0; t < 2; t++)
            {
                TeamStats s = report.Teams[t].Stats;
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
                total[t, 13] += s.Possessions;
                total[t, 14] += s.Tackles;
                total[t, 15] += s.TacklesWon;
                total[t, 16] += s.PossessionTicks;
                total[t, 17] += s.Xg;
                total[t, 18] += s.Recoveries;
                total[t, 19] += s.Injuries;
                total[t, 20] += s.Distance / 1000.0;
            }
        }

        /// <summary>Imprime el informe completo de un partido: equipos, jugadores y remates.</summary>
        private static void PrintReport(MatchReport r)
        {
            Console.WriteLine($"\nInforme del último partido: {r.Teams[0].Name} {r.HomeGoals} - {r.AwayGoals} {r.Teams[1].Name}");
            foreach (TeamReport t in r.Teams)
            {
                TeamStats s = t.Stats;
                Console.WriteLine($"\n{t.Name}: posesión {100 * t.Possession:F0}%  tiros {s.Shots} ({s.ShotsOnTarget} al arco)  xG {s.Xg:F2}  " +
                                  $"pases {s.PassesCompleted}/{s.Passes}  recuperaciones {s.Recoveries}  faltas {s.Fouls}  " +
                                  $"tarjetas {s.Yellows}A {s.Reds}R  lesiones {s.Injuries}  distancia {s.Distance / 1000f:F1} km");
                Console.WriteLine("  #   Jugador               Pos  Min  Nota  G  A  Tir(arco)  xG    Pases   Rec  Entr  Km   Res  Tarj");
                foreach (PlayerReport p in t.Players)
                {
                    PlayerMatchStats ps = p.Stats;
                    string name = p.Name.Length > 20 ? p.Name.Substring(0, 20) : p.Name;
                    string rating = p.Rating > 0f ? p.Rating.ToString("F1") : "  - ";
                    string cards = (ps.Yellows > 0 ? ps.Yellows + "A" : "") + (ps.Reds > 0 ? "R" : "") + (ps.Injured ? " les." : "");
                    Console.WriteLine($"  {p.Number,-3} {name,-20} {Abbrev(p.Role),-4} {p.Minutes,3:F0}  {rating,4}  {ps.Goals}  {ps.Assists}  {ps.Shots,3}({ps.ShotsOnTarget})" +
                                      $"    {ps.Xg,4:F2}  {ps.PassesCompleted,3}/{ps.Passes,-3}  {ps.Recoveries,3}  {ps.TacklesWon,2}/{ps.Tackles,-2} {ps.Distance / 1000f,4:F1}  {100 * p.StaminaLeft,3:F0}%  {cards}");
                }
            }

            Console.WriteLine("\nRemates (minuto, equipo, distancia, xG, al arco, gol):");
            foreach (ShotRecord shot in r.Shots)
            {
                Console.WriteLine($"  {shot.Minute,5:F1}'  {r.Teams[shot.Team].Name,-6} {shot.Distance,5:F1} m  xG {shot.Xg:F2}  {(shot.OnTarget ? "al arco" : "afuera "),-7} {(shot.Goal ? "GOL" : "")}");
            }
        }

        private static string Abbrev(Football.Core.Domain.Position p)
        {
            switch (p)
            {
                case Football.Core.Domain.Position.Goalkeeper: return "ARQ";
                case Football.Core.Domain.Position.CenterBack: return "DFC";
                case Football.Core.Domain.Position.LeftBack: return "LI";
                case Football.Core.Domain.Position.RightBack: return "LD";
                case Football.Core.Domain.Position.DefensiveMidfielder: return "MCD";
                case Football.Core.Domain.Position.CentralMidfielder: return "MC";
                case Football.Core.Domain.Position.AttackingMidfielder: return "MCO";
                case Football.Core.Domain.Position.LeftMidfielder: return "MI";
                case Football.Core.Domain.Position.RightMidfielder: return "MD";
                case Football.Core.Domain.Position.LeftWinger: return "EI";
                case Football.Core.Domain.Position.RightWinger: return "ED";
                default: return "DC";
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
