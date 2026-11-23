using System;
using System.Collections.Generic;
using System.Linq;
using Football.Core.Domain;

namespace Football.Core.Management
{
    /// <summary>
    /// Convierte el plantel de la carrera en un equipo listo para cualquiera de los dos motores (en tiempo real o rápido):
    /// atributos efectivos según forma, moral, estado físico y localía; deja afuera a lesionados y sancionados.
    /// </summary>
    public static class MatchPreparation
    {
        /// <summary>Suplentes que lleva un equipo al partido.</summary>
        public const int BenchSize = 9;
        private const char GroupSeparator = ';';
        private const char IdSeparator = ',';

        /// <summary>Alineación elegida por el DT: titulares por puesto y banco, ya resueltos contra el plantel y los lesionados.</summary>
        public sealed class Lineup
        {
            /// <summary>Titulares: el elemento i juega en el puesto i de la formación.</summary>
            public readonly List<Player> Starters = new List<Player>();
            /// <summary>Suplentes, en orden.</summary>
            public readonly List<Player> Bench = new List<Player>();
        }

        /// <summary>Escribe una alineación en texto («id,id,…;id,id,…»: titulares por puesto y banco) para guardarla con la partida.</summary>
        public static string FormatLineup(IEnumerable<int> starters, IEnumerable<int> bench) =>
            string.Join(IdSeparator.ToString(), starters) + GroupSeparator + string.Join(IdSeparator.ToString(), bench);

        /// <summary>Lee una alineación guardada; devuelve false si el texto está vacío o mal formado.</summary>
        public static bool TryParseLineup(string text, out List<int> starters, out List<int> bench)
        {
            starters = new List<int>();
            bench = new List<int>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string[] groups = text.Split(GroupSeparator);
            if (groups.Length != 2)
            {
                return false;
            }

            for (int g = 0; g < 2; g++)
            {
                foreach (string part in groups[g].Split(new[] { IdSeparator }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!int.TryParse(part, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int id))
                    {
                        return false;
                    }

                    (g == 0 ? starters : bench).Add(id);
                }
            }

            return starters.Count > 0;
        }

        /// <summary>Vista de un jugador para el partido con sus atributos efectivos.</summary>
        /// <param name="player">Jugador.</param>
        /// <param name="condition">Su estado.</param>
        /// <param name="role">Posición en la que juega.</param>
        /// <param name="home">Juega de local.</param>
        /// <param name="number">Dorsal.</param>
        /// <param name="c">Parámetros.</param>
        public static PlayerMatchView View(Player player, PlayerCondition condition, Position role, bool home, int number, ManagementConfig c)
        {
            float factor = 1f + c.FormWeight * (condition.Form - 6f) + c.MoraleWeight * (condition.Morale - 50f)
                           - c.FitnessPenalty * (1f - condition.Fitness) + (home ? c.HomeAdvantage : 0f);
            var view = new PlayerMatchView
            {
                Id = player.Id, Name = (player.FirstName + " " + player.LastName).Trim(), Number = number, Role = role,
                Form = condition.Form, Morale = condition.Morale
            };
            foreach (AttributeId id in AttributeSet.All)
            {
                int value = (int)Math.Round(player.Attributes[id] * factor);
                view.Attributes[id] = Math.Max(AttributeScale.Min, Math.Min(AttributeScale.Max, value));
            }

            return view;
        }

        /// <summary>
        /// Arma el equipo: para cada puesto de la formación, el mejor disponible en esa línea (o el mejor que quede); el
        /// resto disponible, al banco.
        /// </summary>
        /// <param name="name">Nombre del club.</param>
        /// <param name="squad">Plantel.</param>
        /// <param name="conditions">Estado de cada jugador (por id).</param>
        /// <param name="formation">Formación.</param>
        /// <param name="style">Estilo.</param>
        /// <param name="home">Juega de local.</param>
        /// <param name="c">Parámetros.</param>
        /// <param name="lineup">Alineación elegida por el DT (texto de <see cref="FormatLineup"/>); vacía o null = automática.</param>
        public static MatchTeamSetup Prepare(string name, IReadOnlyList<Player> squad, IReadOnlyDictionary<int, PlayerCondition> conditions,
            Formation formation, TeamStyle style, bool home, ManagementConfig c, string lineup = null)
        {
            PlayerCondition Cond(Player p) => conditions.TryGetValue(p.Id, out PlayerCondition pc) ? pc : new PlayerCondition { PlayerId = p.Id };
            Lineup chosen = Resolve(name, squad, conditions, formation, lineup);
            var players = new PlayerMatchView[formation.Slots.Length];
            for (int s = 0; s < players.Length; s++)
            {
                players[s] = View(chosen.Starters[s], Cond(chosen.Starters[s]), formation.Slots[s].Role, home, s + 1, c);
            }

            PlayerMatchView[] bench = chosen.Bench.Select((p, i) => View(p, Cond(p), p.Position, home, formation.Slots.Length + 1 + i, c)).ToArray();
            return new MatchTeamSetup { Name = name, Formation = formation, Style = style, Players = players, Bench = bench };
        }

        /// <summary>
        /// Resuelve la alineación del partido. Con una elegida por el DT se respeta puesto por puesto; si un elegido no puede jugar
        /// (lesionado, sancionado o ya no está en el club) lo reemplaza el mejor disponible para ese puesto. Los suplentes elegidos que
        /// no puedan jugar se descartan. Sin alineación elegida (o con texto inválido) es automática: el mejor de cada línea y el resto al banco.
        /// </summary>
        /// <param name="name">Nombre del club (para los mensajes).</param>
        /// <param name="squad">Plantel.</param>
        /// <param name="conditions">Estado de cada jugador (por id).</param>
        /// <param name="formation">Formación.</param>
        /// <param name="lineup">Alineación guardada o null.</param>
        public static Lineup Resolve(string name, IReadOnlyList<Player> squad, IReadOnlyDictionary<int, PlayerCondition> conditions,
            Formation formation, string lineup)
        {
            bool Ok(Player p) => !conditions.TryGetValue(p.Id, out PlayerCondition pc) || pc.Available;
            var available = squad.Where(Ok).OrderByDescending(p => PlayerRatings.Overall(p.Attributes, p.Position)).ToList();
            if (available.Count < formation.Slots.Length)
            {
                throw new InvalidOperationException($"{name}: solo hay {available.Count} jugadores disponibles.");
            }

            var result = new Lineup();
            var byId = squad.ToDictionary(p => p.Id);
            bool custom = TryParseLineup(lineup, out List<int> starterIds, out List<int> benchIds);
            var starters = new Player[formation.Slots.Length];
            var used = new HashSet<int>();
            if (custom)
            {
                for (int s = 0; s < starters.Length && s < starterIds.Count; s++)
                {
                    if (byId.TryGetValue(starterIds[s], out Player p) && Ok(p) && used.Add(p.Id))
                    {
                        starters[s] = p;
                    }
                }

                foreach (int id in benchIds)
                {
                    used.Add(id); // los elegidos para el banco no se usan para tapar huecos de titulares si hay otros
                }
            }

            // Puestos sin elegido: el mejor disponible, primero entre los que el DT no puso en ningún lado.
            for (int s = 0; s < starters.Length; s++)
            {
                if (starters[s] != null)
                {
                    continue;
                }

                Position role = formation.Slots[s].Role;
                PitchLine line = PlayerRatings.LineOf(role);
                bool IsStarter(Player p) => starters.Contains(p);
                List<Player> pool = available.Where(p => !IsStarter(p) && !(custom && used.Contains(p.Id))).ToList();
                if (pool.Count == 0)
                {
                    pool = available.Where(p => !IsStarter(p)).ToList();
                }

                starters[s] = pool.FirstOrDefault(p => p.Position == role)
                              ?? pool.FirstOrDefault(p => PlayerRatings.LineOf(p.Position) == line)
                              ?? pool.FirstOrDefault(p => (role == Position.Goalkeeper) == (p.Position == Position.Goalkeeper) || line != PitchLine.Goalkeeper)
                              ?? pool.First();
            }

            result.Starters.AddRange(starters);
            if (custom)
            {
                foreach (int id in benchIds)
                {
                    if (result.Bench.Count < BenchSize && byId.TryGetValue(id, out Player p) && Ok(p) && !result.Starters.Contains(p) && !result.Bench.Contains(p))
                    {
                        result.Bench.Add(p);
                    }
                }
            }
            else
            {
                result.Bench.AddRange(available.Where(p => !result.Starters.Contains(p)).Take(BenchSize));
            }

            return result;
        }
    }
}
