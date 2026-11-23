using System.Collections.Generic;
using System.Linq;
using Football.Core.Domain;

namespace Football.Core.Career
{
    // Dorsales: cada jugador lleva el suyo (editable por el usuario en su club); no se repiten dentro de un plantel.
    public sealed partial class CareerSession
    {
        /// <summary>Dorsal más alto que se puede elegir.</summary>
        public const int MaxShirtNumber = 99;

        /// <summary>
        /// Asigna dorsales sin repetir en un club: respeta los que ya tiene cada jugador y a quienes no tienen (o repiten) les da el primero libre;
        /// los arqueros prefieren el 1, el 12 y el 13.
        /// </summary>
        /// <param name="data">Datos de la carrera.</param>
        /// <param name="clubId">Club.</param>
        internal static void FixShirtNumbers(CareerData data, int clubId)
        {
            List<Player> squad = data.Players.Values.Where(p => p.ClubId == clubId).OrderBy(p => p.Id).ToList();
            var used = new HashSet<int>();
            var pending = new List<Player>();
            foreach (Player p in squad)
            {
                if (p.ShirtNumber >= 1 && p.ShirtNumber <= MaxShirtNumber && used.Add(p.ShirtNumber))
                {
                    continue;
                }

                p.ShirtNumber = 0;
                pending.Add(p);
            }

            foreach (Player p in pending.OrderBy(p => p.Position == Position.Goalkeeper ? 0 : 1).ThenBy(p => p.Id))
            {
                int[] preferred = p.Position == Position.Goalkeeper ? new[] { 1, 12, 13 } : new int[0];
                int number = preferred.FirstOrDefault(n => !used.Contains(n));
                if (number == 0)
                {
                    number = Enumerable.Range(1, MaxShirtNumber).FirstOrDefault(n => !used.Contains(n) && !(p.Position != Position.Goalkeeper && n == 1));
                    if (number == 0)
                    {
                        number = Enumerable.Range(1, MaxShirtNumber).First(n => !used.Contains(n));
                    }
                }

                p.ShirtNumber = number;
                used.Add(number);
            }
        }

        /// <summary>
        /// Cambia el dorsal de un jugador del club del usuario. Si otro compañero lo lleva, intercambian dorsales.
        /// </summary>
        /// <param name="playerId">Jugador.</param>
        /// <param name="number">Dorsal pedido (1 a <see cref="MaxShirtNumber"/>).</param>
        /// <returns>Falso si el jugador no es del club o el número no es válido.</returns>
        public bool SetShirtNumber(int playerId, int number)
        {
            if (number < 1 || number > MaxShirtNumber || !Data.Players.TryGetValue(playerId, out Player p) || p.ClubId != Data.UserClubId)
            {
                return false;
            }

            Player holder = Data.Players.Values.FirstOrDefault(x => x.ClubId == Data.UserClubId && x.Id != p.Id && x.ShirtNumber == number);
            if (holder != null)
            {
                holder.ShirtNumber = p.ShirtNumber;
            }

            p.ShirtNumber = number;
            return true;
        }
    }
}
