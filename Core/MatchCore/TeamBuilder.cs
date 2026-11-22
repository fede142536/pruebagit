using System;
using Football.Core.Common;
using Football.Core.Data;
using Football.Core.Domain;

namespace Football.Core.MatchCore
{
    /// <summary>Convierte un equipo definido en datos (plantilla real o ficticia) en un once listo para jugar.</summary>
    public static class TeamBuilder
    {
        /// <summary>Penalización de media por jugar en un puesto que no es el suyo pero es compatible.</summary>
        private const int NearPenalty = 4;
        /// <summary>Penalización por jugar en un puesto de la misma línea pero distinto.</summary>
        private const int LinePenalty = 9;
        /// <summary>Penalización por jugar fuera de su línea.</summary>
        private const int FarPenalty = 20;
        /// <summary>Penalización por poner a un jugador de campo de portero o al revés.</summary>
        private const int KeeperPenalty = 60;

        /// <summary>Convierte el código de posición del archivo (POR, DFC, LI…) en la posición del motor.</summary>
        /// <param name="code">Código en español.</param>
        public static Position ParsePosition(string code)
        {
            switch ((code ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "POR": return Position.Goalkeeper;
                case "DFC": return Position.CenterBack;
                case "LI": return Position.LeftBack;
                case "LD": return Position.RightBack;
                case "MCD": return Position.DefensiveMidfielder;
                case "MCO": return Position.AttackingMidfielder;
                case "MI": return Position.LeftMidfielder;
                case "MD": return Position.RightMidfielder;
                case "EI": return Position.LeftWinger;
                case "ED": return Position.RightWinger;
                case "DC": return Position.Striker;
                default: return Position.CentralMidfielder;
            }
        }

        /// <summary>Indica si un código de posición del archivo es válido.</summary>
        public static bool IsKnownPosition(string code)
        {
            switch ((code ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "POR": case "DFC": case "LI": case "LD": case "MCD": case "MC":
                case "MCO": case "MI": case "MD": case "EI": case "ED": case "DC":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Formación por nombre (por defecto 4-4-2).</summary>
        public static Formation ParseFormation(string name)
        {
            for (int i = 0; i < Formations.All.Length; i++)
            {
                if (string.Equals(Formations.All[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return Formations.All[i];
                }
            }

            return Formations.F442;
        }

        /// <summary>Estilo por nombre (por defecto el primero, equilibrado).</summary>
        public static TeamStyle ParseStyle(string name, MatchConfig config)
        {
            for (int i = 0; i < config.Styles.Length; i++)
            {
                if (string.Equals(config.Styles[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return config.Styles[i];
                }
            }

            return config.Styles[0];
        }

        /// <summary>Nombre corto para mostrar: el indicado o, si falta, el apellido (última palabra).</summary>
        public static string DisplayName(PlayerDefinition p)
        {
            if (!string.IsNullOrWhiteSpace(p.ShortName))
            {
                return p.ShortName.Trim();
            }

            string full = (p.Name ?? string.Empty).Trim();
            int space = full.LastIndexOf(' ');
            return space >= 0 ? full.Substring(space + 1) : full;
        }

        /// <summary>Jugadores mínimos para formar un once.</summary>
        public const int MinPlayers = 11;

        /// <summary>
        /// Comprueba que un equipo leído de un archivo se pueda jugar: nombre, al menos once jugadores con posición y media
        /// válidas y al menos un portero.
        /// </summary>
        /// <param name="team">Equipo a comprobar.</param>
        /// <returns>null si es válido; si no, el motivo en español.</returns>
        public static string Validate(TeamDefinition team)
        {
            if (team == null || string.IsNullOrWhiteSpace(team.Name))
            {
                return "equipo sin nombre";
            }

            if (team.Players == null || team.Players.Length < MinPlayers)
            {
                return team.Name + ": menos de " + MinPlayers + " jugadores";
            }

            bool keeper = false;
            foreach (PlayerDefinition p in team.Players)
            {
                if (p == null || string.IsNullOrWhiteSpace(p.Name) || !IsKnownPosition(p.Position))
                {
                    return team.Name + ": jugador sin nombre o con posición desconocida";
                }

                if (p.Rating < AttributeScale.Min || p.Rating > AttributeScale.Max)
                {
                    return team.Name + ": media fuera de rango (" + p.Name + ")";
                }

                keeper |= ParsePosition(p.Position) == Position.Goalkeeper;
            }

            return keeper ? null : team.Name + ": sin portero";
        }

        /// <summary>Fracción de la penalización por jugar fuera de puesto que se aplica a la media en el partido.</summary>
        public const float OutOfPositionFactor = 0.5f;

        /// <summary>Media con la que juega un jugador en un puesto (su media menos la penalización si no es su posición).</summary>
        /// <param name="def">Jugador.</param>
        /// <param name="role">Puesto en el que se lo alinea.</param>
        public static float EffectiveRating(PlayerDefinition def, Position role) =>
            def.Rating - Penalty(ParsePosition(def.Position), role) * OutOfPositionFactor;

        /// <summary>Penalización de media al alinear en <paramref name="slot"/> a un jugador cuya posición natural es <paramref name="natural"/>.</summary>
        public static int Penalty(Position natural, Position slot)
        {
            if (natural == slot)
            {
                return 0;
            }

            bool keeperNatural = natural == Position.Goalkeeper;
            bool keeperSlot = slot == Position.Goalkeeper;
            if (keeperNatural || keeperSlot)
            {
                return KeeperPenalty;
            }

            if (AreNear(natural, slot) || AreNear(slot, natural))
            {
                return NearPenalty;
            }

            return Line(natural) == Line(slot) ? LinePenalty : FarPenalty;
        }

        private static bool AreNear(Position a, Position b)
        {
            switch (a)
            {
                case Position.LeftBack: return b == Position.LeftMidfielder;
                case Position.RightBack: return b == Position.RightMidfielder;
                case Position.CenterBack: return b == Position.DefensiveMidfielder;
                case Position.DefensiveMidfielder: return b == Position.CentralMidfielder;
                case Position.CentralMidfielder: return b == Position.AttackingMidfielder;
                case Position.LeftMidfielder: return b == Position.LeftWinger;
                case Position.RightMidfielder: return b == Position.RightWinger;
                case Position.AttackingMidfielder: return b == Position.Striker;
                case Position.LeftWinger: return b == Position.RightWinger || b == Position.Striker;
                case Position.RightWinger: return b == Position.Striker;
                default: return false;
            }
        }

        private static int Line(Position p)
        {
            switch (p)
            {
                case Position.Goalkeeper: return 0;
                case Position.CenterBack:
                case Position.LeftBack:
                case Position.RightBack: return 1;
                case Position.Striker:
                case Position.LeftWinger:
                case Position.RightWinger: return 3;
                default: return 2;
            }
        }

        /// <summary>
        /// Elige el mejor once para la formación del equipo: recorre los puestos del más escaso al más común (portero,
        /// centrales, laterales…) y coloca en cada uno al jugador con mayor media efectiva (media menos la penalización
        /// por puesto). Devuelve, para cada puesto de la formación, el índice del jugador en la plantilla.
        /// </summary>
        public static int[] PickLineup(TeamDefinition team, Formation formation) => PickLineup(team, formation, null);

        /// <summary>Igual que <see cref="PickLineup(TeamDefinition, Formation)"/>, pero eligiendo solo entre los jugadores indicados.</summary>
        /// <param name="team">Equipo.</param>
        /// <param name="formation">Formación.</param>
        /// <param name="candidates">Índices de la plantilla que pueden jugar, o null para todos.</param>
        public static int[] PickLineup(TeamDefinition team, Formation formation, System.Collections.Generic.IReadOnlyList<int> candidates)
        {
            var lineup = new int[formation.Slots.Length];
            var used = new bool[team.Players.Length];
            if (candidates != null)
            {
                for (int i = 0; i < used.Length; i++)
                {
                    used[i] = true;
                }

                for (int i = 0; i < candidates.Count; i++)
                {
                    used[candidates[i]] = false;
                }
            }
            for (int i = 0; i < lineup.Length; i++)
            {
                lineup[i] = -1;
            }

            int[] order = SlotOrder(formation);
            foreach (int slot in order)
            {
                Position role = formation.Slots[slot].Role;
                int best = -1;
                int bestScore = int.MinValue;
                for (int p = 0; p < team.Players.Length; p++)
                {
                    if (used[p])
                    {
                        continue;
                    }

                    int score = team.Players[p].Rating - Penalty(ParsePosition(team.Players[p].Position), role);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = p;
                    }
                }

                if (best >= 0)
                {
                    used[best] = true;
                    lineup[slot] = best;
                }
            }

            return lineup;
        }

        private static int[] SlotOrder(Formation formation)
        {
            var order = new int[formation.Slots.Length];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            Array.Sort(order, (a, b) =>
            {
                int pa = Priority(formation.Slots[a].Role), pb = Priority(formation.Slots[b].Role);
                return pa != pb ? pa.CompareTo(pb) : a.CompareTo(b);
            });
            return order;
        }

        private static int Priority(Position role)
        {
            switch (role)
            {
                case Position.Goalkeeper: return 0;
                case Position.CenterBack: return 1;
                case Position.LeftBack:
                case Position.RightBack: return 2;
                case Position.DefensiveMidfielder: return 3;
                case Position.Striker: return 4;
                case Position.LeftWinger:
                case Position.RightWinger: return 5;
                case Position.LeftMidfielder:
                case Position.RightMidfielder: return 6;
                default: return 7;
            }
        }

        /// <summary>Media del once titular (para comparar equipos en el menú).</summary>
        public static float LineupRating(TeamDefinition team)
        {
            Formation formation = ParseFormation(team.Formation);
            int[] lineup = PickLineup(team, formation);
            float sum = 0f;
            int count = 0;
            for (int i = 0; i < lineup.Length; i++)
            {
                if (lineup[i] >= 0)
                {
                    sum += team.Players[lineup[i]].Rating;
                    count++;
                }
            }

            return count > 0 ? sum / count : 0f;
        }

        /// <summary>Construye el equipo listo para el motor de partido.</summary>
        /// <param name="team">Definición del equipo.</param>
        /// <param name="config">Configuración del partido (para el estilo).</param>
        /// <param name="rng">Se conserva por compatibilidad: la variación individual de atributos ya es fija por jugador.</param>
        public static MatchTeamSetup Build(TeamDefinition team, MatchConfig config, IRandom rng) => Build(team, config, rng, null);

        /// <summary>Construye el equipo con la convocatoria elegida por el usuario (o la automática si es null o no es válida).</summary>
        /// <param name="team">Definición del equipo.</param>
        /// <param name="config">Configuración del partido.</param>
        /// <param name="rng">Se conserva por compatibilidad (los atributos son fijos por jugador, ver <see cref="PlayerCards"/>).</param>
        /// <param name="selection">Formación y titulares elegidos.</param>
        public static MatchTeamSetup Build(TeamDefinition team, MatchConfig config, IRandom rng, SquadSelection selection)
        {
            if (team.Players == null || team.Players.Length < 11)
            {
                throw new ArgumentException($"El equipo {team.Name} necesita al menos 11 jugadores.", nameof(team));
            }

            if (selection == null || !selection.IsValid(team))
            {
                selection = SquadSelection.Auto(team, ParseFormation(team.Formation));
            }

            Formation formation = selection.Formation;
            int[] lineup = selection.Starters.ToArray();
            var players = new PlayerMatchView[formation.Slots.Length];
            for (int slot = 0; slot < players.Length; slot++)
            {
                PlayerDefinition def = team.Players[lineup[slot]];
                Position natural = ParsePosition(def.Position);
                Position role = formation.Slots[slot].Role;
                var view = new PlayerMatchView
                {
                    Id = lineup[slot] + 1,
                    Name = DisplayName(def),
                    // Sin dorsal en el archivo, se usa el orden en la plantilla: no cambia al cambiar de formación o de puesto.
                    Number = def.Number > 0 ? def.Number : lineup[slot] + 1,
                    Role = role
                };

                // Los atributos salen de su posición natural; jugar fuera de puesto le resta algo de media.
                float effective = EffectiveRating(def, role);
                // Variación individual fija por jugador (no por partido): la ficha muestra lo mismo que se juega.
                RosterFactory.FillAttributes(PlayerCards.RandomFor(def.Name), effective, natural == Position.Goalkeeper || role != Position.Goalkeeper ? natural : role, view.Attributes);
                players[slot] = view;
            }

            // Suplentes: con su posición natural y su media (entran al puesto del que sale, con la resistencia llena).
            var bench = new PlayerMatchView[selection.Bench.Count];
            for (int i = 0; i < bench.Length; i++)
            {
                int index = selection.Bench[i];
                PlayerDefinition def = team.Players[index];
                Position natural = ParsePosition(def.Position);
                var view = new PlayerMatchView
                {
                    Id = index + 1,
                    Name = DisplayName(def),
                    Number = def.Number > 0 ? def.Number : index + 1,
                    Role = natural
                };
                RosterFactory.FillAttributes(PlayerCards.RandomFor(def.Name), def.Rating, natural, view.Attributes);
                bench[i] = view;
            }

            return new MatchTeamSetup
            {
                Name = team.Name, Formation = formation, Style = ParseStyle(team.Style, config), Players = players, Bench = bench
            };
        }
    }
}
