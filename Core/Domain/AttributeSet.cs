using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Football.Core.Domain
{
    /// <summary>Conjunto completo de atributos de un jugador, acotados a <see cref="AttributeScale"/>.</summary>
    public sealed class AttributeSet
    {
        private static readonly AttributeId[] AllIds = (AttributeId[])Enum.GetValues(typeof(AttributeId));
        private static readonly ReadOnlyCollection<AttributeId> AllReadOnly = Array.AsReadOnly(AllIds);
        private static readonly int[] IndexById = BuildIndex();

        private readonly int[] _values = new int[AllIds.Length];

        /// <summary>Todos los identificadores de atributo, en orden estable (solo lectura).</summary>
        public static IReadOnlyList<AttributeId> All => AllReadOnly;

        /// <summary>Indica si un identificador numérico corresponde a un atributo existente.</summary>
        /// <param name="id">Identificador estable del atributo.</param>
        public static bool IsDefined(int id) => id >= 0 && id < IndexById.Length && IndexById[id] >= 0;

        /// <summary>Crea un conjunto con todos los atributos en el mínimo de la escala.</summary>
        public AttributeSet()
        {
            for (int i = 0; i < _values.Length; i++)
            {
                _values[i] = AttributeScale.Min;
            }
        }

        /// <summary>Crea un conjunto con todos los atributos al mismo valor.</summary>
        /// <param name="value">Valor inicial; se acota a la escala.</param>
        public AttributeSet(int value) : this()
        {
            for (int i = 0; i < _values.Length; i++)
            {
                _values[i] = Clamp(value);
            }
        }

        /// <summary>Valor de un atributo. Al asignar, el valor se acota a la escala.</summary>
        /// <param name="id">Atributo.</param>
        public int this[AttributeId id]
        {
            get => _values[Slot(id)];
            set => _values[Slot(id)] = Clamp(value);
        }

        /// <summary>Copia independiente del conjunto.</summary>
        public AttributeSet Clone()
        {
            var copy = new AttributeSet();
            Array.Copy(_values, copy._values, _values.Length);
            return copy;
        }

        private static int Slot(AttributeId id)
        {
            int raw = (int)id;
            if (!IsDefined(raw))
            {
                throw new ArgumentOutOfRangeException(nameof(id), raw, "Atributo inexistente.");
            }

            return IndexById[raw];
        }

        private static int Clamp(int value) =>
            value < AttributeScale.Min ? AttributeScale.Min :
            value > AttributeScale.Max ? AttributeScale.Max : value;

        private static int[] BuildIndex()
        {
            int max = 0;
            foreach (AttributeId id in AllIds)
            {
                if ((int)id > max)
                {
                    max = (int)id;
                }
            }

            var index = new int[max + 1];
            for (int i = 0; i < index.Length; i++)
            {
                index[i] = -1; // huecos: identificadores que no son atributos
            }

            for (int i = 0; i < AllIds.Length; i++)
            {
                index[(int)AllIds[i]] = i;
            }

            return index;
        }
    }
}
