using System;
using System.Collections.Generic;
using Football.Core.Common;
using Football.Core.Domain;

namespace Football.Core.MatchSim
{
    /// <summary>
    /// Simulador estadístico de partidos (modo «Simular» y temporadas masivas): usa las mismas entidades que el motor en
    /// tiempo real (<see cref="MatchTeamSetup"/>, <see cref="PlayerMatchView"/>, estilo) y devuelve el mismo
    /// <see cref="MatchReport"/>, en microsegundos. No depende de <c>MatchCore</c>: está calibrado contra él con datos
    /// (<see cref="MatchSimConfig"/>). Todo el azar pasa por el <see cref="IRandom"/> recibido.
    /// </summary>
    public static class QuickMatchSimulator
    {
        private const float MatchMinutes = 90f;
        private const int Goalkeeper = 0, Defender = 1, Midfielder = 2, Forward = 3;
        private const float ReferenceAttribute = 65f;

        /// <summary>Juega un partido.</summary>
        /// <param name="home">Local.</param>
        /// <param name="away">Visitante.</param>
        /// <param name="rng">Azar con semilla.</param>
        /// <param name="config">Parámetros.</param>
        /// <param name="report">Pesos de las notas (los mismos que usa el motor en tiempo real).</param>
        /// <returns>Informe del partido (<see cref="MatchReport.Simulated"/> verdadero).</returns>
        public static MatchReport Play(MatchTeamSetup home, MatchTeamSetup away, IRandom rng, MatchSimConfig config, ReportConfig report)
        {
            if (home == null || away == null || rng == null || config == null || report == null)
            {
                throw new ArgumentNullException(home == null ? nameof(home) : away == null ? nameof(away) : rng == null ? nameof(rng) : "config");
            }

            var result = new MatchReport { Simulated = true };
            var sides = new[] { new Side(home, config), new Side(away, config) };
            float strength = 0.5f * (sides[0].Attack - sides[1].Attack) + 0.5f * (sides[0].Defense - sides[1].Defense);
            float possession = MathUtil.Clamp(0.5f + config.PossessionSensitivity * (sides[0].Midfield - sides[1].Midfield)
                                              + 0.15f * (sides[1].Style.Directness - sides[0].Style.Directness), 0.25f, 0.75f);
            // Partido cerrado o abierto: el mismo factor para los dos (media 1).
            float openness = MathF.Exp(rng.NextGaussian(0f, config.Openness) - 0.5f * config.Openness * config.Openness);
            for (int t = 0; t < 2; t++)
            {
                Side side = sides[t];
                float diff = t == 0 ? strength : -strength;
                if (diff > 0f)
                {
                    diff /= 1f + diff / config.StrengthSaturation; // el favorito satura; el débil no
                }

                float goals = config.BaseGoals * openness * MathF.Exp(config.StrengthSensitivity * diff)
                              * config.StyleFactor(side.Style.Name, true) * config.StyleFactor(sides[1 - t].Style.Name, false);
                float shots = config.BaseShots * MathF.Exp(config.StrengthSensitivity * config.ShotShare * diff)
                              * (1f + config.DirectnessShots * (side.Style.Directness - 0.5f));
                float xgPerShot = MathF.Min(config.MaxShotXg, goals / MathF.Max(shots, 0.1f));
                TeamReport team = result.Teams[t];
                team.Name = side.Setup.Name;
                team.Stats = new TeamStats();
                team.Possession = t == 0 ? possession : 1f - possession;
                side.Build(team, rng, config);
                PlayShots(side, team, Poisson(rng, shots), xgPerShot, t, rng, config, result.Shots);
                Discipline(side, team, rng, config);
                Passing(side, team, rng, config);
            }

            result.Shots.Sort((a, b) => a.Minute.CompareTo(b.Minute));
            for (int t = 0; t < 2; t++)
            {
                TeamReport team = result.Teams[t];
                team.Goals = team.Stats.Goals;
                team.Stats.Saves = Math.Max(0, result.Teams[1 - t].Stats.ShotsOnTarget - result.Teams[1 - t].Stats.Goals);
                sides[t].Keeper.Stats.Saves = team.Stats.Saves;
            }

            for (int t = 0; t < 2; t++)
            {
                foreach (PlayerReport p in result.Teams[t].Players)
                {
                    p.Rating = MatchReport.Rate(report, p, result.Teams[t].Goals, result.Teams[1 - t].Goals);
                }
            }

            return result;
        }

        private static void PlayShots(Side side, TeamReport team, int shots, float xgPerShot, int teamIndex, IRandom rng, MatchSimConfig c,
            List<ShotRecord> records)
        {
            for (int s = 0; s < shots; s++)
            {
                PlayerReport shooter = side.Pick(rng, c.ShotWeights, AttributeId.Finishing);
                float xg = MathUtil.Clamp(xgPerShot * (0.4f + 1.2f * rng.NextFloat()), 0.01f, c.MaxShotXg);
                bool goal = rng.NextFloat() < xg;
                bool onTarget = goal || rng.NextFloat() < (c.OnTargetShare - xgPerShot) / MathF.Max(1f - xgPerShot, 0.05f);
                float minute = rng.NextFloat() * MatchMinutes;
                shooter.Stats.Shots++;
                shooter.Stats.Xg += xg;
                team.Stats.Shots++;
                team.Stats.Xg += xg;
                if (onTarget)
                {
                    shooter.Stats.ShotsOnTarget++;
                    team.Stats.ShotsOnTarget++;
                }

                if (goal)
                {
                    shooter.Stats.Goals++;
                    team.Stats.Goals++;
                    if (rng.NextFloat() < c.AssistShare)
                    {
                        PlayerReport assistant = side.Pick(rng, c.PassWeights, AttributeId.Passing, shooter);
                        if (assistant != null)
                        {
                            assistant.Stats.Assists++;
                        }
                    }
                }

                records.Add(new ShotRecord
                {
                    Team = teamIndex, PlayerIndex = -1, PlayerId = shooter.PlayerId, Minute = minute, Xg = xg,
                    Distance = 28f - 20f * xg, OnTarget = onTarget, Goal = goal
                });
            }
        }

        private static void Discipline(Side side, TeamReport team, IRandom rng, MatchSimConfig c)
        {
            float aggression = 1f + (side.Style.PressIntensity - 0.5f);
            int fouls = Poisson(rng, c.Fouls * aggression);
            for (int i = 0; i < fouls; i++)
            {
                side.Pick(rng, c.PassWeights, AttributeId.Aggression).Stats.Fouls++;
            }

            team.Stats.Fouls = fouls;
            int yellows = Poisson(rng, c.Yellows * aggression);
            for (int i = 0; i < yellows; i++)
            {
                side.Pick(rng, c.PassWeights, AttributeId.Aggression).Stats.Yellows++;
            }

            team.Stats.Yellows = yellows;
            if (rng.NextFloat() < c.Reds * aggression)
            {
                PlayerReport sent = side.Pick(rng, c.PassWeights, AttributeId.Aggression);
                sent.Stats.Reds++;
                sent.Minutes = Math.Max(1f, sent.Minutes * rng.NextFloat());
                team.Stats.Reds = 1;
            }

            team.Stats.Corners = Poisson(rng, c.Corners);
            if (rng.NextFloat() < c.Injuries)
            {
                PlayerReport hurt = side.Pick(rng, c.PassWeights, AttributeId.Stamina);
                hurt.Stats.Injured = true;
                team.Stats.Injuries = 1;
            }
        }

        private static void Passing(Side side, TeamReport team, IRandom rng, MatchSimConfig c)
        {
            int passes = Poisson(rng, c.BasePasses * 2f * team.Possession);
            float accuracy = MathUtil.Clamp(c.PassAccuracy + c.PassAccuracyPerPoint * (side.Passing - ReferenceAttribute), 0.6f, 0.97f);
            for (int i = 0; i < passes; i++)
            {
                PlayerReport passer = side.Pick(rng, c.PassWeights, AttributeId.Passing);
                passer.Stats.Passes++;
                team.Stats.Passes++;
                if (rng.NextFloat() < accuracy)
                {
                    passer.Stats.PassesCompleted++;
                    team.Stats.PassesCompleted++;
                }
            }

            team.Stats.PassesIntercepted = team.Stats.Passes - team.Stats.PassesCompleted;
            int recoveries = Poisson(rng, c.Recoveries);
            for (int i = 0; i < recoveries; i++)
            {
                side.Pick(rng, c.PassWeights, AttributeId.Tackling).Stats.Recoveries++;
            }

            team.Stats.Recoveries = recoveries;
            foreach (PlayerReport p in team.Players)
            {
                float km = p.Role == Position.Goalkeeper ? c.DistanceKm * 0.3f : c.DistanceKm;
                p.Stats.Distance = km * 1000f * p.Minutes / MatchMinutes;
                team.Stats.Distance += p.Stats.Distance;
            }
        }

        /// <summary>Número de sucesos de Poisson con media <paramref name="mean"/> (Knuth; medias pequeñas).</summary>
        private static int Poisson(IRandom rng, float mean)
        {
            double limit = Math.Exp(-Math.Max(0f, mean));
            int k = 0;
            double product = rng.NextDouble();
            while (product > limit && k < 200)
            {
                k++;
                product *= rng.NextDouble();
            }

            return k;
        }

        private static int Line(Position p)
        {
            switch (p)
            {
                case Position.Goalkeeper: return Goalkeeper;
                case Position.CenterBack:
                case Position.LeftBack:
                case Position.RightBack: return Defender;
                case Position.Striker:
                case Position.LeftWinger:
                case Position.RightWinger: return Forward;
                default: return Midfielder;
            }
        }

        /// <summary>Valoración de un jugador en su línea (media de los atributos que importan en ella).</summary>
        private static float LineScore(PlayerMatchView p)
        {
            AttributeSet a = p.Attributes;
            switch (Line(p.Role))
            {
                case Goalkeeper:
                    return (a[AttributeId.Reflexes] + a[AttributeId.Handling] + a[AttributeId.GoalkeeperPositioning]) / 3f;
                case Defender:
                    return (a[AttributeId.Tackling] + a[AttributeId.Positioning] + a[AttributeId.Strength] + a[AttributeId.Heading] + a[AttributeId.Pace]) / 5f;
                case Forward:
                    return (a[AttributeId.Finishing] + a[AttributeId.Shooting] + a[AttributeId.Dribbling] + a[AttributeId.Pace] + a[AttributeId.Composure]) / 5f;
                default:
                    return (a[AttributeId.Passing] + a[AttributeId.Vision] + a[AttributeId.FirstTouch] + a[AttributeId.Decisions] + a[AttributeId.Dribbling]) / 5f;
            }
        }

        /// <summary>Un equipo preparado para simular: fuerzas por línea y los jugadores que participan.</summary>
        private sealed class Side
        {
            public readonly MatchTeamSetup Setup;
            public readonly TeamStyle Style;
            public readonly float Attack, Defense, Midfield, Passing;
            public PlayerReport Keeper;
            private readonly List<PlayerReport> _players = new List<PlayerReport>();
            private readonly List<PlayerMatchView> _views = new List<PlayerMatchView>();

            public Side(MatchTeamSetup setup, MatchSimConfig c)
            {
                Setup = setup;
                Style = setup.Style ?? new TeamStyle();
                float[] sum = new float[4];
                int[] count = new int[4];
                float passing = 0f;
                foreach (PlayerMatchView p in setup.Players)
                {
                    int line = Line(p.Role);
                    sum[line] += LineScore(p);
                    count[line]++;
                    passing += p.Attributes[AttributeId.Passing];
                }

                float Mean(int line) => count[line] > 0 ? sum[line] / count[line] : ReferenceAttribute;
                Attack = 0.65f * Mean(Forward) + 0.35f * Mean(Midfielder);
                Defense = 0.6f * Mean(Defender) + 0.25f * Mean(Goalkeeper) + 0.15f * Mean(Midfielder);
                Midfield = Mean(Midfielder);
                Passing = setup.Players.Length > 0 ? passing / setup.Players.Length : ReferenceAttribute;
            }

            /// <summary>Crea los informes de los titulares y de los suplentes que entran.</summary>
            public void Build(TeamReport team, IRandom rng, MatchSimConfig c)
            {
                foreach (PlayerMatchView view in Setup.Players)
                {
                    PlayerReport p = Entry(view, true, MatchMinutes, c);
                    team.Players.Add(p);
                    _players.Add(p);
                    _views.Add(view);
                    if (view.Role == Position.Goalkeeper && Keeper == null)
                    {
                        Keeper = p;
                    }
                }

                PlayerMatchView[] bench = Setup.Bench ?? Array.Empty<PlayerMatchView>();
                int subs = Math.Min(c.Substitutions, bench.Length);
                for (int s = 0; s < subs; s++)
                {
                    // Sale el jugador de campo más cansado que siga entero; entra un suplente de la misma línea si hay.
                    PlayerReport tired = null;
                    for (int i = 0; i < Setup.Players.Length; i++)
                    {
                        PlayerReport p = _players[i];
                        if (p.Role != Position.Goalkeeper && p.Minutes >= MatchMinutes && (tired == null || p.StaminaLeft < tired.StaminaLeft))
                        {
                            tired = p;
                        }
                    }

                    if (tired == null)
                    {
                        break;
                    }

                    float minute = c.SubstitutionFrom + (c.SubstitutionTo - c.SubstitutionFrom) * rng.NextFloat();
                    PlayerMatchView incoming = BestFor(bench, Line(tired.Role), s);
                    tired.Minutes = minute;
                    PlayerReport sub = Entry(incoming, false, MatchMinutes - minute, c);
                    sub.Role = tired.Role;
                    team.Players.Add(sub);
                    _players.Add(sub);
                    _views.Add(incoming);
                }
            }

            private readonly HashSet<PlayerMatchView> _used = new HashSet<PlayerMatchView>();

            private PlayerMatchView BestFor(PlayerMatchView[] bench, int line, int fallback)
            {
                PlayerMatchView best = null;
                foreach (PlayerMatchView b in bench)
                {
                    if (!_used.Contains(b) && Line(b.Role) == line && (best == null || LineScore(b) > LineScore(best)))
                    {
                        best = b;
                    }
                }

                if (best == null)
                {
                    foreach (PlayerMatchView b in bench)
                    {
                        if (!_used.Contains(b) && b.Role != Position.Goalkeeper)
                        {
                            best = b;
                            break;
                        }
                    }
                }

                best = best ?? bench[fallback % bench.Length];
                _used.Add(best);
                return best;
            }

            private static PlayerReport Entry(PlayerMatchView view, bool started, float minutes, MatchSimConfig c)
            {
                float stamina = view.Role == Position.Goalkeeper
                    ? 0.95f
                    : MathUtil.Clamp01(c.StaminaLeft + c.StaminaPerPoint * (view.Attributes[AttributeId.Stamina] - ReferenceAttribute)
                                       + (1f - minutes / MatchMinutes) * (1f - c.StaminaLeft));
                return new PlayerReport
                {
                    PlayerId = view.Id, Name = view.Name, Number = view.Number, Role = view.Role, Started = started, Minutes = minutes,
                    StaminaLeft = stamina, Stats = new PlayerMatchStats()
                };
            }

            /// <summary>Elige un jugador al azar con peso por línea, por un atributo y por minutos jugados.</summary>
            public PlayerReport Pick(IRandom rng, float[] lineWeights, AttributeId attribute, PlayerReport exclude = null)
            {
                float total = 0f;
                for (int i = 0; i < _players.Count; i++)
                {
                    total += Weight(i, lineWeights, attribute, exclude);
                }

                if (total <= 0f)
                {
                    return exclude == null ? _players[0] : null;
                }

                float roll = rng.NextFloat() * total;
                for (int i = 0; i < _players.Count; i++)
                {
                    roll -= Weight(i, lineWeights, attribute, exclude);
                    if (roll <= 0f)
                    {
                        return _players[i];
                    }
                }

                return _players[_players.Count - 1];
            }

            private float Weight(int i, float[] lineWeights, AttributeId attribute, PlayerReport exclude)
            {
                PlayerReport p = _players[i];
                if (p == exclude)
                {
                    return 0f;
                }

                float skill = _views[i].Attributes[attribute] / ReferenceAttribute;
                return lineWeights[Line(p.Role)] * skill * skill * (p.Minutes / MatchMinutes);
            }
        }
    }
}
