using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Football.Core.Common;
using Football.Core.Data;

namespace Football.Core.MatchCore
{
    /// <summary>Grupo de la convocatoria.</summary>
    public enum SquadGroup
    {
        /// <summary>Titular (en un puesto de la formación).</summary>
        Starter,
        /// <summary>Suplente convocado.</summary>
        Bench,
        /// <summary>No convocado.</summary>
        Reserve
    }

    /// <summary>
    /// Convocatoria de un equipo para un partido: formación, titulares (uno por puesto), suplentes y reservas no convocados.
    /// Todas las listas guardan índices de <see cref="TeamDefinition.Players"/>.
    /// </summary>
    public sealed class SquadSelection
    {
        /// <summary>Suplentes convocados por defecto.</summary>
        public const int DefaultBenchSize = 9;

        private const char Separator = '|';

        /// <summary>Formación.</summary>
        public Formation Formation { get; private set; }

        /// <summary>Titulares: el elemento i juega en el puesto i de la formación.</summary>
        public readonly List<int> Starters = new List<int>();

        /// <summary>Suplentes convocados.</summary>
        public readonly List<int> Bench = new List<int>();

        /// <summary>Jugadores no convocados.</summary>
        public readonly List<int> Reserves = new List<int>();

        /// <summary>Convocatoria automática: el mejor once para la formación, los mejores restantes al banco (con un portero suplente si hay).</summary>
        /// <param name="team">Equipo.</param>
        /// <param name="formation">Formación.</param>
        /// <param name="benchSize">Número de suplentes.</param>
        public static SquadSelection Auto(TeamDefinition team, Formation formation, int benchSize = DefaultBenchSize)
        {
            var s = new SquadSelection { Formation = formation };
            s.Starters.AddRange(TeamBuilder.PickLineup(team, formation));
            var rest = new List<int>();
            for (int i = 0; i < team.Players.Length; i++)
            {
                if (!s.Starters.Contains(i))
                {
                    rest.Add(i);
                }
            }

            rest.Sort((a, b) => team.Players[b].Rating.CompareTo(team.Players[a].Rating));
            int keeper = rest.FindIndex(i => TeamBuilder.ParsePosition(team.Players[i].Position) == Domain.Position.Goalkeeper);
            if (keeper >= 0 && benchSize > 0)
            {
                s.Bench.Add(rest[keeper]);
                rest.RemoveAt(keeper);
            }

            while (s.Bench.Count < benchSize && rest.Count > 0)
            {
                s.Bench.Add(rest[0]);
                rest.RemoveAt(0);
            }

            s.Reserves.AddRange(rest);
            return s;
        }

        /// <summary>Lista de un grupo.</summary>
        public List<int> Group(SquadGroup group) =>
            group == SquadGroup.Starter ? Starters : (group == SquadGroup.Bench ? Bench : Reserves);

        /// <summary>Intercambia dos jugadores (de cualquier grupo y posición).</summary>
        public void Swap(SquadGroup groupA, int indexA, SquadGroup groupB, int indexB)
        {
            List<int> a = Group(groupA);
            List<int> b = Group(groupB);
            int temp = a[indexA];
            a[indexA] = b[indexB];
            b[indexB] = temp;
        }

        /// <summary>Cambia la formación manteniendo a los mismos once titulares, recolocados en los puestos que mejor les van.</summary>
        public void ChangeFormation(TeamDefinition team, Formation formation)
        {
            int[] relocated = TeamBuilder.PickLineup(team, formation, Starters);
            Formation = formation;
            Starters.Clear();
            Starters.AddRange(relocated);
        }

        /// <summary>Media efectiva del titular de un puesto (su media menos la penalización si juega fuera de su posición).</summary>
        public int EffectiveRating(TeamDefinition team, int slot)
        {
            PlayerDefinition p = team.Players[Starters[slot]];
            return (int)MathF.Round(TeamBuilder.EffectiveRating(p, Formation.Slots[slot].Role));
        }

        /// <summary>Indica si la convocatoria es coherente con el equipo: once titulares y cada jugador en un solo grupo.</summary>
        public bool IsValid(TeamDefinition team)
        {
            if (Formation == null || Starters.Count != Formation.Slots.Length)
            {
                return false;
            }

            int total = team.Players.Length;
            if (Starters.Count + Bench.Count + Reserves.Count != total)
            {
                return false;
            }

            var seen = new bool[total];
            foreach (List<int> list in new[] { Starters, Bench, Reserves })
            {
                foreach (int i in list)
                {
                    if (i < 0 || i >= total || seen[i])
                    {
                        return false;
                    }

                    seen[i] = true;
                }
            }

            return true;
        }

        /// <summary>
        /// Texto compacto para guardar la convocatoria (formación|titulares|suplentes|huella de la plantilla). La huella hace que
        /// una convocatoria guardada se descarte si la plantilla cambió (los índices ya no señalarían a los mismos jugadores).
        /// </summary>
        /// <param name="team">Equipo al que pertenece la convocatoria.</param>
        public string Serialize(TeamDefinition team)
        {
            var sb = new StringBuilder();
            sb.Append(Formation.Name).Append(Separator);
            AppendList(sb, Starters);
            sb.Append(Separator);
            AppendList(sb, Bench);
            sb.Append(Separator).Append(Fingerprint(team));
            return sb.ToString();
        }

        /// <summary>Huella de la plantilla: cambia si se agrega, quita, reordena o cambia de posición un jugador.</summary>
        /// <param name="team">Equipo.</param>
        public static string Fingerprint(TeamDefinition team)
        {
            var sb = new StringBuilder();
            foreach (PlayerDefinition p in team.Players)
            {
                sb.Append(p.Name).Append('/').Append(p.Position).Append(';');
            }

            return HashUtil.Fnv1a64(sb.ToString()).ToString("x16", CultureInfo.InvariantCulture);
        }

        private static void AppendList(StringBuilder sb, List<int> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                sb.Append(list[i].ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <summary>Recupera una convocatoria guardada; devuelve null si el texto no corresponde a esta plantilla.</summary>
        public static SquadSelection Deserialize(TeamDefinition team, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            string[] parts = text.Split(Separator);
            if (parts.Length != 4 || parts[3] != Fingerprint(team))
            {
                return null;
            }

            Formation formation = null;
            foreach (Formation f in Formations.All)
            {
                if (f.Name == parts[0])
                {
                    formation = f;
                }
            }

            if (formation == null)
            {
                return null;
            }

            var s = new SquadSelection { Formation = formation };
            if (!ParseList(parts[1], s.Starters) || !ParseList(parts[2], s.Bench))
            {
                return null;
            }

            for (int i = 0; i < team.Players.Length; i++)
            {
                if (!s.Starters.Contains(i) && !s.Bench.Contains(i))
                {
                    s.Reserves.Add(i);
                }
            }

            return s.IsValid(team) ? s : null;
        }

        private static bool ParseList(string text, List<int> into)
        {
            if (text.Length == 0)
            {
                return true;
            }

            foreach (string item in text.Split(','))
            {
                if (!int.TryParse(item, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                {
                    return false;
                }

                into.Add(value);
            }

            return true;
        }
    }
}
