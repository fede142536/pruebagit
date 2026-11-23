using System;

namespace Football.Core.Domain
{
    /// <summary>
    /// Jugador. Es la misma entidad para la gestión, la simulación rápida y el partido 3D.
    /// </summary>
    public sealed class Player
    {

        /// <summary>Identificador único (positivo). Lo asigna quien crea el mundo, no la base de datos.</summary>
        public int Id { get; set; }

        /// <summary>Club actual, o <c>null</c> si es agente libre.</summary>
        public int? ClubId { get; set; }

        /// <summary>Nombre de pila.</summary>
        public string FirstName { get; set; }

        /// <summary>Apellido.</summary>
        public string LastName { get; set; }

        /// <summary>Fecha de nacimiento.</summary>
        public DateTime BirthDate { get; set; }

        /// <summary>Posición principal.</summary>
        public Position Position { get; set; }

        /// <summary>Potencial oculto: techo de atributos al que puede aspirar.</summary>
        public int Potential { get; set; }

        /// <summary>Atributos actuales.</summary>
        public AttributeSet Attributes { get; set; } = new AttributeSet();

        /// <summary>Contrato vigente, o <c>null</c> si no tiene.</summary>
        public Contract Contract { get; set; }

        /// <summary>Nacionalidad (código de tres letras); «ARG» por defecto.</summary>
        public string Nationality { get; set; } = "ARG";

        /// <summary>Dorsal en su club (1–99); 0 = todavía sin asignar.</summary>
        public int ShirtNumber { get; set; }

        /// <summary>Rasgo de personalidad.</summary>
        public PersonalityTrait Personality { get; set; }

        /// <summary>Fecha en que llegó al club (para la jerarquía del vestuario).</summary>
        public DateTime JoinedOn { get; set; }

        /// <summary>Edad en años cumplidos en la fecha indicada.</summary>
        /// <param name="on">Fecha de referencia (normalmente <c>SimClock.Today</c>).</param>
        public int AgeOn(DateTime on)
        {
            int age = on.Year - BirthDate.Year;
            if (on < BirthDate.AddYears(age))
            {
                age--;
            }

            return age;
        }
    }
}
