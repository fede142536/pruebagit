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
        private const int BenchSize = 9;

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
        public static MatchTeamSetup Prepare(string name, IReadOnlyList<Player> squad, IReadOnlyDictionary<int, PlayerCondition> conditions,
            Formation formation, TeamStyle style, bool home, ManagementConfig c)
        {
            PlayerCondition Cond(Player p) => conditions.TryGetValue(p.Id, out PlayerCondition pc) ? pc : new PlayerCondition { PlayerId = p.Id };
            var available = squad.Where(p => Cond(p).Available).OrderByDescending(p => PlayerRatings.Overall(p.Attributes, p.Position)).ToList();
            if (available.Count < formation.Slots.Length)
            {
                throw new InvalidOperationException($"{name}: solo hay {available.Count} jugadores disponibles.");
            }

            var players = new PlayerMatchView[formation.Slots.Length];
            for (int s = 0; s < formation.Slots.Length; s++)
            {
                Position role = formation.Slots[s].Role;
                PitchLine line = PlayerRatings.LineOf(role);
                Player pick = available.FirstOrDefault(p => p.Position == role)
                              ?? available.FirstOrDefault(p => PlayerRatings.LineOf(p.Position) == line)
                              ?? available.First(p => (role == Position.Goalkeeper) == (p.Position == Position.Goalkeeper) || line != PitchLine.Goalkeeper);
                available.Remove(pick);
                players[s] = View(pick, Cond(pick), role, home, s + 1, c);
            }

            PlayerMatchView[] bench = available.Take(BenchSize)
                .Select((p, i) => View(p, Cond(p), p.Position, home, formation.Slots.Length + 1 + i, c)).ToArray();
            return new MatchTeamSetup { Name = name, Formation = formation, Style = style, Players = players, Bench = bench };
        }
    }
}
