using Football.Core.Domain;

namespace Football.Core.MatchCore
{
    /// <summary>Puesto de una formación en el campo de ataque normalizado.</summary>
    public struct FormationSlot
    {
        /// <summary>Posición asignada.</summary>
        public Position Role;
        /// <summary>Avance a lo largo del campo: 0 en la línea de gol propia, 1 en la rival.</summary>
        public float X;
        /// <summary>Posición lateral de -1 a 1; positivo es la izquierda del equipo según su dirección de ataque.</summary>
        public float Y;

        /// <summary>Crea un puesto.</summary>
        public FormationSlot(Position role, float x, float y)
        {
            Role = role;
            X = x;
            Y = y;
        }
    }

    /// <summary>Formación táctica: once puestos, el primero es siempre el portero.</summary>
    public sealed class Formation
    {
        /// <summary>Nombre, p. ej. "4-4-2".</summary>
        public readonly string Name;
        /// <summary>Puestos.</summary>
        public readonly FormationSlot[] Slots;

        /// <summary>Crea una formación.</summary>
        public Formation(string name, FormationSlot[] slots)
        {
            Name = name;
            Slots = slots;
        }
    }

    /// <summary>Formaciones disponibles.</summary>
    public static class Formations
    {
        /// <summary>4-4-2 clásico.</summary>
        public static readonly Formation F442 = new Formation("4-4-2", new[]
        {
            new FormationSlot(Position.Goalkeeper, 0.03f, 0f),
            new FormationSlot(Position.LeftBack, 0.20f, 0.80f),
            new FormationSlot(Position.CenterBack, 0.17f, 0.28f),
            new FormationSlot(Position.CenterBack, 0.17f, -0.28f),
            new FormationSlot(Position.RightBack, 0.20f, -0.80f),
            new FormationSlot(Position.LeftMidfielder, 0.46f, 0.80f),
            new FormationSlot(Position.CentralMidfielder, 0.40f, 0.25f),
            new FormationSlot(Position.CentralMidfielder, 0.40f, -0.25f),
            new FormationSlot(Position.RightMidfielder, 0.46f, -0.80f),
            new FormationSlot(Position.Striker, 0.64f, 0.20f),
            new FormationSlot(Position.Striker, 0.64f, -0.20f)
        });

        /// <summary>4-3-3 con extremos.</summary>
        public static readonly Formation F433 = new Formation("4-3-3", new[]
        {
            new FormationSlot(Position.Goalkeeper, 0.03f, 0f),
            new FormationSlot(Position.LeftBack, 0.21f, 0.80f),
            new FormationSlot(Position.CenterBack, 0.17f, 0.28f),
            new FormationSlot(Position.CenterBack, 0.17f, -0.28f),
            new FormationSlot(Position.RightBack, 0.21f, -0.80f),
            new FormationSlot(Position.DefensiveMidfielder, 0.34f, 0f),
            new FormationSlot(Position.CentralMidfielder, 0.46f, 0.32f),
            new FormationSlot(Position.CentralMidfielder, 0.46f, -0.32f),
            new FormationSlot(Position.LeftWinger, 0.66f, 0.75f),
            new FormationSlot(Position.Striker, 0.70f, 0f),
            new FormationSlot(Position.RightWinger, 0.66f, -0.75f)
        });

        /// <summary>4-2-3-1: doble pivote, mediapunta y dos extremos.</summary>
        public static readonly Formation F4231 = new Formation("4-2-3-1", new[]
        {
            new FormationSlot(Position.Goalkeeper, 0.03f, 0f),
            new FormationSlot(Position.LeftBack, 0.21f, 0.80f),
            new FormationSlot(Position.CenterBack, 0.17f, 0.28f),
            new FormationSlot(Position.CenterBack, 0.17f, -0.28f),
            new FormationSlot(Position.RightBack, 0.21f, -0.80f),
            new FormationSlot(Position.DefensiveMidfielder, 0.34f, 0.22f),
            new FormationSlot(Position.DefensiveMidfielder, 0.34f, -0.22f),
            new FormationSlot(Position.LeftWinger, 0.56f, 0.72f),
            new FormationSlot(Position.AttackingMidfielder, 0.54f, 0f),
            new FormationSlot(Position.RightWinger, 0.56f, -0.72f),
            new FormationSlot(Position.Striker, 0.70f, 0f)
        });

        /// <summary>3-5-2: tres centrales, carrileros y dos delanteros.</summary>
        public static readonly Formation F352 = new Formation("3-5-2", new[]
        {
            new FormationSlot(Position.Goalkeeper, 0.03f, 0f),
            new FormationSlot(Position.CenterBack, 0.17f, 0.45f),
            new FormationSlot(Position.CenterBack, 0.15f, 0f),
            new FormationSlot(Position.CenterBack, 0.17f, -0.45f),
            new FormationSlot(Position.LeftMidfielder, 0.40f, 0.85f),
            new FormationSlot(Position.DefensiveMidfielder, 0.33f, 0f),
            new FormationSlot(Position.CentralMidfielder, 0.44f, 0.30f),
            new FormationSlot(Position.CentralMidfielder, 0.44f, -0.30f),
            new FormationSlot(Position.RightMidfielder, 0.40f, -0.85f),
            new FormationSlot(Position.Striker, 0.66f, 0.18f),
            new FormationSlot(Position.Striker, 0.66f, -0.18f)
        });

        /// <summary>Todas las formaciones.</summary>
        public static readonly Formation[] All = { F442, F433, F4231, F352 };
    }
}
