using System;
using System.Collections.Generic;
using Football.Core.Common;
using Football.Core.Domain;

namespace Football.Core.Management
{
    /// <summary>
    /// Juveniles de la cantera (regens): cada año llegan unos pocos chicos de 16 o 17 años, con potencial según el nivel
    /// de la cantera y la capacidad del entrenador de juveniles. Nombres ficticios (sin jugadores reales).
    /// </summary>
    public sealed class YouthIntake
    {
        private static readonly string[] FirstNames =
        {
            "Tomás", "Bautista", "Thiago", "Valentín", "Benjamín", "Joaquín", "Lautaro", "Santino", "Mateo", "Facundo", "Agustín",
            "Franco", "Lucas", "Ignacio", "Nahuel", "Bruno", "Gonzalo", "Ramiro", "Ezequiel", "Julián"
        };

        private static readonly string[] LastNames =
        {
            "Arrieta", "Benítez", "Corvalán", "Durán", "Echeverría", "Funes", "Galeano", "Herrera", "Ibarra", "Juárez", "Ledesma",
            "Maidana", "Nieva", "Ocampo", "Paredes", "Quiroga", "Rivas", "Sosa", "Toledo", "Vera", "Zárate", "Montiel", "Pereyra"
        };

        private static readonly Position[] Positions =
        {
            Position.Goalkeeper, Position.CenterBack, Position.LeftBack, Position.RightBack, Position.DefensiveMidfielder,
            Position.CentralMidfielder, Position.AttackingMidfielder, Position.LeftWinger, Position.RightWinger, Position.Striker
        };

        private readonly ManagementConfig _config;
        private readonly EventBus _bus;
        private int _nextId;

        /// <summary>Crea el generador.</summary>
        /// <param name="config">Parámetros.</param>
        /// <param name="bus">Bus de eventos.</param>
        /// <param name="firstPlayerId">Primer identificador libre para los juveniles.</param>
        public YouthIntake(ManagementConfig config, EventBus bus, int firstPlayerId)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _nextId = firstPlayerId;
        }

        /// <summary>Genera la camada anual de un club.</summary>
        /// <param name="clubId">Club.</param>
        /// <param name="academyLevel">Nivel de la cantera (1–5).</param>
        /// <param name="youthCoachAbility">Capacidad del entrenador de juveniles (1–20).</param>
        /// <param name="today">Fecha.</param>
        /// <param name="rng">Azar.</param>
        public List<Player> Generate(int clubId, int academyLevel, int youthCoachAbility, DateTime today, IRandom rng)
        {
            ManagementConfig c = _config;
            int count = rng.NextInt(c.YouthIntakeSize[0], c.YouthIntakeSize[1] + 1);
            float mean = c.YouthPotentialBase + c.YouthPotentialPerLevel * (academyLevel - 1) + c.YouthPotentialPerCoach * (youthCoachAbility - 10);
            var intake = new List<Player>();
            int best = 0;
            for (int i = 0; i < count; i++)
            {
                int potential = (int)Math.Round(Math.Max(35f, Math.Min(95f, rng.NextGaussian(mean, 8f))));
                Position position = Positions[rng.NextInt(0, Positions.Length)];
                var player = new Player
                {
                    Id = _nextId++, ClubId = clubId, FirstName = FirstNames[rng.NextInt(0, FirstNames.Length)],
                    LastName = LastNames[rng.NextInt(0, LastNames.Length)], BirthDate = today.AddYears(-rng.NextInt(16, 18)).AddDays(-rng.NextInt(0, 365)),
                    Position = position, Potential = potential, JoinedOn = today,
                    Personality = (PersonalityTrait)rng.NextInt(0, Enum.GetValues(typeof(PersonalityTrait)).Length)
                };
                float current = potential * c.YouthCurrentShare;
                foreach (AttributeId id in AttributeSet.All)
                {
                    player.Attributes[id] = (int)Math.Round(Math.Max(AttributeScale.Min, Math.Min(AttributeScale.Max, rng.NextGaussian(current, 5f))));
                }

                best = Math.Max(best, potential);
                intake.Add(player);
            }

            _bus.Publish(new YouthIntakeArrived { ClubId = clubId, Count = count, BestPotential = best });
            return intake;
        }
    }
}
