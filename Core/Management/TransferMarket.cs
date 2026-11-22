using System;
using Football.Core.Domain;

namespace Football.Core.Management
{
    /// <summary>
    /// Mercado de pases: valor de mercado, respuesta a ofertas con cláusulas (según el perfil del club vendedor) y pedido
    /// de sueldo del jugador (según su nivel, su personalidad y la promesa de minutos). Determinista: misma oferta, misma
    /// respuesta.
    /// </summary>
    public sealed class TransferMarket
    {
        private readonly ManagementConfig _config;

        /// <summary>Crea el mercado.</summary>
        public TransferMarket(ManagementConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>Valor de mercado de un jugador.</summary>
        /// <param name="player">Jugador.</param>
        /// <param name="today">Fecha (para la edad).</param>
        public long Value(Player player, DateTime today)
        {
            ManagementConfig c = _config;
            float rating = PlayerRatings.Overall(player.Attributes, player.Position);
            int age = player.AgeOn(today);
            double value = c.BaseValue * Math.Exp(c.ValuePerRating * (rating - 60f))
                           * Math.Max(0.15, 1.0 - c.ValuePerYearFromPeak * Math.Abs(age - c.PeakValueAge));
            if (age < c.PeakValueAge && player.Potential > rating)
            {
                value *= 1.0 + 0.03 * (player.Potential - rating); // los jóvenes con proyección valen más
            }

            return (long)value;
        }

        /// <summary>Lo que pide el vendedor por un jugador según su perfil (una cláusula de rescisión manda si existe).</summary>
        public long AskingPrice(Player player, ClubProfile seller, DateTime today)
        {
            long clause = player.Contract?.ReleaseClause ?? 0;
            long ask = (long)(Value(player, today) * _config.AskFactor[(int)seller]);
            return clause > 0 ? Math.Min(ask, clause) : ask;
        }

        /// <summary>Responde una oferta: acepta, pide una cifra (contraoferta) o la rechaza.</summary>
        /// <param name="offer">Oferta.</param>
        /// <param name="player">Jugador.</param>
        /// <param name="seller">Perfil del vendedor.</param>
        /// <param name="today">Fecha.</param>
        /// <param name="counter">En una contraoferta, el monto que pide.</param>
        public OfferResponse Evaluate(TransferOffer offer, Player player, ClubProfile seller, DateTime today, out long counter)
        {
            long clause = player.Contract?.ReleaseClause ?? 0;
            counter = 0;
            if (clause > 0 && offer.Fee >= clause)
            {
                return OfferResponse.Accepted; // pagar la cláusula obliga a vender
            }

            // El porcentaje de venta futura y el bonus por presencias abaratan el pedido.
            float discount = 1f - _config.SellOnDiscount * (offer.SellOnShare * 10f);
            long ask = (long)(AskingPrice(player, seller, today) * Math.Max(0.5f, discount)) - offer.AppearanceBonus * 10;
            if (offer.Fee >= ask)
            {
                return OfferResponse.Accepted;
            }

            if (offer.Fee < ask * _config.RejectBelow)
            {
                return OfferResponse.Rejected;
            }

            counter = ask;
            return OfferResponse.CounterOffer;
        }

        /// <summary>
        /// A quién quiere fichar un club de la IA según su perfil: el comprador, al mejor; el canterano, al joven con más
        /// proyección; el defensivo, al mejor defensor o arquero.
        /// </summary>
        /// <param name="profile">Perfil del club.</param>
        /// <param name="candidates">Jugadores disponibles.</param>
        /// <param name="today">Fecha.</param>
        /// <returns>El elegido, o null si no hay candidatos.</returns>
        public static Player AiTarget(ClubProfile profile, System.Collections.Generic.IEnumerable<Player> candidates, DateTime today)
        {
            Player best = null;
            float bestScore = float.MinValue;
            foreach (Player p in candidates)
            {
                float overall = PlayerRatings.Overall(p.Attributes, p.Position);
                PitchLine line = PlayerRatings.LineOf(p.Position);
                float score = profile == ClubProfile.Buyer ? overall
                    : profile == ClubProfile.YouthSeller ? p.Potential - 2f * Math.Max(0, p.AgeOn(today) - 21)
                    : overall + (line == PitchLine.Defence || line == PitchLine.Goalkeeper ? 10f : -10f);
                if (score > bestScore || (score == bestScore && best != null && p.Id < best.Id))
                {
                    best = p;
                    bestScore = score;
                }
            }

            return best;
        }

        /// <summary>Sueldo semanal que pide el jugador para firmar.</summary>
        /// <param name="player">Jugador.</param>
        /// <param name="promise">Promesa de minutos ofrecida.</param>
        public long WageDemand(Player player, PlayingTimePromise promise)
        {
            float rating = PlayerRatings.Overall(player.Attributes, player.Position);
            double wage = _config.BaseWage * Math.Exp(_config.WagePerRating * (rating - 60f)) * _config.PromiseWageFactor[(int)promise];
            return (long)wage;
        }

        /// <summary>Si el jugador acepta un sueldo (puede rebajar su pedido según su personalidad).</summary>
        public bool AcceptsWage(Player player, PlayingTimePromise promise, long offeredWeekly) =>
            offeredWeekly >= WageDemand(player, promise) * (1f - _config.WageFlexibility[(int)player.Personality]);
    }
}
