using Football.Core.Common;
using Football.Core.Domain;

namespace Football.Core.MatchCore
{
    /// <summary>Genera equipos de prueba con atributos coherentes con su posición (arte y datos reales vendrán de la base de datos).</summary>
    public static class RosterFactory
    {
        private const float AttributeNoise = 5f;
        private const int IrrelevantKeeperAttribute = 10;

        /// <summary>Crea un equipo listo para jugar.</summary>
        /// <param name="rng">Fuente de azar.</param>
        /// <param name="name">Nombre del equipo.</param>
        /// <param name="rating">Calidad media de 1 a 100.</param>
        /// <param name="formation">Formación.</param>
        /// <param name="style">Estilo de juego.</param>
        public static MatchTeamSetup CreateTeam(IRandom rng, string name, float rating, Formation formation, TeamStyle style)
        {
            var players = new PlayerMatchView[formation.Slots.Length];
            for (int i = 0; i < players.Length; i++)
            {
                Position role = formation.Slots[i].Role;
                var view = new PlayerMatchView { Id = i + 1, Name = name + " " + (i + 1), Number = i + 1, Role = role };
                foreach (AttributeId id in AttributeSet.All)
                {
                    float value = rating + Offset(role, id) + rng.NextGaussian(0f, AttributeNoise);
                    view.Attributes[id] = (int)MathUtil.Clamp(value, AttributeScale.Min, AttributeScale.Max);
                }

                if (role != Position.Goalkeeper)
                {
                    foreach (AttributeId id in new[] { AttributeId.Reflexes, AttributeId.Handling, AttributeId.GoalkeeperPositioning, AttributeId.Distribution })
                    {
                        view.Attributes[id] = IrrelevantKeeperAttribute;
                    }
                }

                players[i] = view;
            }

            return new MatchTeamSetup { Name = name, Formation = formation, Style = style, Players = players };
        }

        private static float Offset(Position role, AttributeId id)
        {
            switch (role)
            {
                case Position.Goalkeeper:
                    switch (id)
                    {
                        case AttributeId.Reflexes: return 20f;
                        case AttributeId.Handling: return 18f;
                        case AttributeId.GoalkeeperPositioning: return 18f;
                        case AttributeId.Distribution: return 10f;
                        case AttributeId.Pace: return -15f;
                        case AttributeId.Shooting:
                        case AttributeId.Finishing:
                        case AttributeId.Dribbling: return -30f;
                        default: return -5f;
                    }
                case Position.CenterBack:
                case Position.LeftBack:
                case Position.RightBack:
                    switch (id)
                    {
                        case AttributeId.Tackling: return 14f;
                        case AttributeId.Heading: return 12f;
                        case AttributeId.Positioning: return 12f;
                        case AttributeId.Strength: return 8f;
                        case AttributeId.Aggression: return 6f;
                        case AttributeId.Crossing: return role == Position.CenterBack ? -8f : 4f;
                        case AttributeId.Pace: return role == Position.CenterBack ? 0f : 6f;
                        case AttributeId.Shooting: return -12f;
                        case AttributeId.Finishing: return -18f;
                        case AttributeId.Dribbling: return -8f;
                        default: return 0f;
                    }
                case Position.DefensiveMidfielder:
                case Position.CentralMidfielder:
                case Position.AttackingMidfielder:
                case Position.LeftMidfielder:
                case Position.RightMidfielder:
                    switch (id)
                    {
                        case AttributeId.Passing: return 14f;
                        case AttributeId.Vision: return 12f;
                        case AttributeId.Stamina: return 10f;
                        case AttributeId.WorkRate: return 10f;
                        case AttributeId.Dribbling: return 4f;
                        case AttributeId.Tackling: return role == Position.DefensiveMidfielder ? 10f : 2f;
                        case AttributeId.Shooting: return role == Position.AttackingMidfielder ? 6f : -2f;
                        case AttributeId.Crossing: return role == Position.LeftMidfielder || role == Position.RightMidfielder ? 8f : 0f;
                        case AttributeId.Finishing: return -6f;
                        default: return 0f;
                    }
                default:
                    switch (id)
                    {
                        case AttributeId.Shooting: return 14f;
                        case AttributeId.Finishing: return 16f;
                        case AttributeId.Dribbling: return 10f;
                        case AttributeId.Pace: return 10f;
                        case AttributeId.Acceleration: return 8f;
                        case AttributeId.Composure: return 6f;
                        case AttributeId.Heading: return role == Position.Striker ? 8f : 0f;
                        case AttributeId.Crossing: return role == Position.Striker ? -4f : 8f;
                        case AttributeId.Tackling: return -18f;
                        case AttributeId.Positioning: return 4f;
                        default: return 0f;
                    }
            }
        }
    }
}
