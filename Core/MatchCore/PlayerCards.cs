using System;
using System.Collections.Generic;
using Football.Core.Common;
using Football.Core.Data;
using Football.Core.Domain;

namespace Football.Core.MatchCore
{
    /// <summary>Pierna hábil.</summary>
    public enum Foot
    {
        /// <summary>Derecha.</summary>
        Right,
        /// <summary>Izquierda.</summary>
        Left,
        /// <summary>Las dos.</summary>
        Both
    }

    /// <summary>Los seis índices de la carta: ritmo, tiro, pase, regate, defensa y físico (1–99).</summary>
    public struct FaceStats
    {
        /// <summary>RIT: velocidad y aceleración.</summary>
        public int Pace;
        /// <summary>TIR: definición, potencia y temple.</summary>
        public int Shooting;
        /// <summary>PAS: pase, visión y centros.</summary>
        public int Passing;
        /// <summary>REG: regate, control y agilidad.</summary>
        public int Dribbling;
        /// <summary>DEF: entrada, colocación y cabezazo.</summary>
        public int Defending;
        /// <summary>FÍS: fuerza, resistencia y salto.</summary>
        public int Physical;
    }

    /// <summary>Estadísticas acumuladas que muestra la ficha (temporada en la carrera, o el partido en curso).</summary>
    public sealed class CardStats
    {
        /// <summary>Partidos jugados.</summary>
        public int Matches;
        /// <summary>Goles.</summary>
        public int Goals;
        /// <summary>Asistencias.</summary>
        public int Assists;
        /// <summary>Suma de notas (para la nota media).</summary>
        public float RatingSum;
        /// <summary>Partidos con nota.</summary>
        public int Rated;

        /// <summary>Nota media (0 si todavía no tiene).</summary>
        public float AverageRating => Rated > 0 ? RatingSum / Rated : 0f;
    }

    /// <summary>Todo lo que muestra la ficha de un jugador (sin lógica de interfaz).</summary>
    public sealed class PlayerCardInfo
    {
        /// <summary>Nombre completo.</summary>
        public string Name;
        /// <summary>Dorsal (0 si no tiene).</summary>
        public int Number;
        /// <summary>Puesto natural.</summary>
        public Position Position;
        /// <summary>Media general.</summary>
        public int Overall;
        /// <summary>Pierna hábil.</summary>
        public Foot Foot;
        /// <summary>Altura (cm).</summary>
        public int HeightCm;
        /// <summary>Edad.</summary>
        public int Age;
        /// <summary>Seis índices.</summary>
        public FaceStats Face;
        /// <summary>Atributos individuales más destacados para su puesto.</summary>
        public readonly List<KeyValuePair<AttributeId, int>> TopAttributes = new List<KeyValuePair<AttributeId, int>>();
        /// <summary>Puestos donde rinde (el natural primero).</summary>
        public readonly List<Position> Compatible = new List<Position>();
        /// <summary>Estado físico de 0 a 1 (negativo si no se conoce).</summary>
        public float Fitness = -1f;
        /// <summary>Moral de 0 a 100 (negativa si no se conoce).</summary>
        public float Morale = -1f;
        /// <summary>Estadísticas acumuladas (null si no hay).</summary>
        public CardStats Stats;
    }

    /// <summary>
    /// Arma la ficha de un jugador. Los atributos detallados de los jugadores del archivo de equipos se generan siempre igual
    /// para cada jugador (semilla = su nombre), así la ficha muestra lo mismo que después juega en la cancha. Pierna, altura
    /// y edad, que el archivo no trae, salen del mismo modo (estimaciones propias, D-12).
    /// </summary>
    public static class PlayerCards
    {
        private const int TopCount = 6;
        private const int CompatiblePenalty = 4; // hasta «puesto vecino» (ver TeamBuilder.Penalty)
        private const int MinAge = 18, MaxAge = 35;
        private static readonly Position[] AllPositions = (Position[])Enum.GetValues(typeof(Position));

        /// <summary>Azar propio de un jugador (siempre el mismo para el mismo nombre).</summary>
        /// <param name="name">Nombre del jugador.</param>
        public static IRandom RandomFor(string name) => new SeededRandom(HashUtil.Fnv1a64("jugador:" + (name ?? string.Empty)));

        /// <summary>Seis índices a partir de los atributos.</summary>
        public static FaceStats Face(AttributeSet a) => new FaceStats
        {
            Pace = Avg(a[AttributeId.Pace], a[AttributeId.Acceleration]),
            Shooting = Avg(a[AttributeId.Finishing], a[AttributeId.Shooting], a[AttributeId.Composure]),
            Passing = Avg(a[AttributeId.Passing], a[AttributeId.Vision], a[AttributeId.Crossing]),
            Dribbling = Avg(a[AttributeId.Dribbling], a[AttributeId.FirstTouch], a[AttributeId.Agility]),
            Defending = Avg(a[AttributeId.Tackling], a[AttributeId.Positioning], a[AttributeId.Heading]),
            Physical = Avg(a[AttributeId.Strength], a[AttributeId.Stamina], a[AttributeId.Jumping])
        };

        /// <summary>Pierna hábil (determinista por nombre: 74 % diestros, 22 % zurdos, 4 % ambidiestros).</summary>
        public static Foot FootOf(string name)
        {
            float u = RandomFor(name).Fork("pierna").NextFloat();
            return u < 0.74f ? Foot.Right : u < 0.96f ? Foot.Left : Foot.Both;
        }

        /// <summary>Altura estimada según el puesto (arqueros y centrales, más altos).</summary>
        public static int HeightCm(string name, Position position)
        {
            IRandom rng = RandomFor(name).Fork("altura");
            float mean = position == Position.Goalkeeper ? 189f : position == Position.CenterBack ? 186f
                : PlayerRatings.LineOf(position) == PitchLine.Attack ? 178f : 177f;
            return (int)Math.Round(MathUtil.Clamp(rng.NextGaussian(mean, 5f), 162f, 202f));
        }

        /// <summary>Edad estimada (para los jugadores del archivo, que no la traen).</summary>
        public static int AgeOf(string name) => RandomFor(name).Fork("edad").NextInt(MinAge, MaxAge + 1);

        /// <summary>Puestos donde el jugador rinde sin gran penalización (el natural primero).</summary>
        public static List<Position> CompatibleWith(Position natural)
        {
            var result = new List<Position> { natural };
            foreach (Position p in AllPositions)
            {
                if (p != natural && TeamBuilder.Penalty(natural, p) <= CompatiblePenalty)
                {
                    result.Add(p);
                }
            }

            return result;
        }

        /// <summary>Atributos más altos entre los que importan para su puesto.</summary>
        public static void TopAttributes(AttributeSet a, Position position, List<KeyValuePair<AttributeId, int>> into)
        {
            into.Clear();
            bool keeper = position == Position.Goalkeeper;
            foreach (AttributeId id in AttributeSet.All)
            {
                bool keeperAttribute = id >= AttributeId.Reflexes;
                if (keeperAttribute == keeper || (keeper && (id == AttributeId.Composure || id == AttributeId.Positioning)))
                {
                    into.Add(new KeyValuePair<AttributeId, int>(id, a[id]));
                }
            }

            into.Sort((x, y) => y.Value != x.Value ? y.Value.CompareTo(x.Value) : x.Key.CompareTo(y.Key));
            if (into.Count > TopCount)
            {
                into.RemoveRange(TopCount, into.Count - TopCount);
            }
        }

        /// <summary>Ficha de un jugador del archivo de equipos (amistoso: alineación previa).</summary>
        public static PlayerCardInfo FromDefinition(PlayerDefinition def)
        {
            Position position = TeamBuilder.ParsePosition(def.Position);
            var attributes = new AttributeSet();
            RosterFactory.FillAttributes(RandomFor(def.Name), def.Rating, position, attributes);
            PlayerCardInfo card = Fill(def.Name, def.Number, position, attributes, def.Age > 0 ? def.Age : AgeOf(def.Name));
            card.Overall = def.Rating;
            return card;
        }

        /// <summary>Ficha de un suplente del partido (pausa).</summary>
        public static PlayerCardInfo FromView(PlayerMatchView v) => Fill(v.Name, v.Number, v.Role, v.Attributes, AgeOf(v.Name));

        /// <summary>Ficha de un jugador en la cancha (pausa): estado físico actual y estadísticas del partido.</summary>
        public static PlayerCardInfo FromMatch(MatchPlayer p, int age = -1)
        {
            PlayerCardInfo card = Fill(p.Name, p.Number, p.Role, p.Attr, age >= 0 ? age : AgeOf(p.Name));
            card.Fitness = p.Stamina;
            card.Stats = new CardStats { Matches = 1, Goals = p.Stats.Goals, Assists = p.Stats.Assists };
            return card;
        }

        /// <summary>Ficha de un jugador de la carrera.</summary>
        public static PlayerCardInfo FromPlayer(Player p, PlayerCondition condition, CardStats stats, DateTime today)
        {
            string name = (p.FirstName + " " + p.LastName).Trim();
            PlayerCardInfo card = Fill(name, 0, p.Position, p.Attributes, p.AgeOn(today));
            if (condition != null)
            {
                card.Fitness = condition.Fitness;
                card.Morale = condition.Morale;
            }

            card.Stats = stats ?? new CardStats();
            return card;
        }

        private static PlayerCardInfo Fill(string name, int number, Position position, AttributeSet attributes, int age)
        {
            var card = new PlayerCardInfo
            {
                Name = name, Number = number, Position = position, Overall = (int)Math.Round(RosterFactory.DisplayRating(attributes, position)),
                Foot = FootOf(name), HeightCm = HeightCm(name, position), Age = age, Face = Face(attributes)
            };
            TopAttributes(attributes, position, card.TopAttributes);
            card.Compatible.AddRange(CompatibleWith(position));
            return card;
        }

        private static int Avg(params int[] values)
        {
            int sum = 0;
            foreach (int v in values)
            {
                sum += v;
            }

            return (int)Math.Round((float)sum / values.Length);
        }
    }
}
