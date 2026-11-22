using System;
using System.Collections.Generic;
using Football.Core.Common;
using Football.Core.Domain;

namespace Football.Core.Management
{
    /// <summary>
    /// Scouting progresivo: de un jugador ajeno solo se ven rangos de atributos, que se estrechan a medida que los ojeadores
    /// lo siguen (más rápido cuanto mejor es el ojeador). El rango siempre contiene el valor real; dónde cae dentro del
    /// rango es fijo para cada jugador y atributo (no cambia de un día a otro).
    /// </summary>
    public sealed class ScoutingSystem
    {
        private readonly ManagementConfig _config;
        private readonly Dictionary<(int club, int player), float> _knowledge = new Dictionary<(int, int), float>();

        /// <summary>Crea el sistema.</summary>
        public ScoutingSystem(ManagementConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>Cuánto conoce un club a un jugador (0 nada, 1 todo).</summary>
        public float Knowledge(int clubId, int playerId) => _knowledge.TryGetValue((clubId, playerId), out float k) ? k : 0f;

        /// <summary>Un ojeador sigue a un jugador durante unos días.</summary>
        /// <param name="clubId">Club del ojeador.</param>
        /// <param name="playerId">Jugador.</param>
        /// <param name="days">Días.</param>
        /// <param name="scoutAbility">Capacidad del ojeador (1–20).</param>
        /// <returns>Conocimiento nuevo.</returns>
        public float Scout(int clubId, int playerId, int days, int scoutAbility)
        {
            float gain = days * _config.ScoutingPerDay * (0.5f + scoutAbility / 20f);
            float k = Math.Min(1f, Knowledge(clubId, playerId) + gain);
            _knowledge[(clubId, playerId)] = k;
            return k;
        }

        /// <summary>Rango visible de un atributo de un jugador ajeno.</summary>
        /// <param name="clubId">Club que mira.</param>
        /// <param name="player">Jugador.</param>
        /// <param name="attribute">Atributo.</param>
        /// <returns>Mínimo y máximo (iguales al valor real con conocimiento completo; los propios se ven exactos).</returns>
        public (int min, int max) Range(int clubId, Player player, AttributeId attribute)
        {
            int real = player.Attributes[attribute];
            if (player.ClubId == clubId)
            {
                return (real, real);
            }

            float width = _config.ScoutingMaxWidth * (1f - Knowledge(clubId, player.Id));
            // Posición fija del valor real dentro del rango (0 = en el borde inferior, 1 = en el superior).
            ulong hash = HashUtil.SplitMix64Mix((ulong)player.Id * 31UL + (ulong)attribute);
            float where = (hash % 1000UL) / 999f;
            int min = (int)Math.Round(real - width * where);
            int max = (int)Math.Round(real + width * (1f - where));
            return (Math.Max(AttributeScale.Min, min), Math.Min(AttributeScale.Max, max));
        }
    }
}
