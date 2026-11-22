using System;
using Football.Core.Common;

namespace Football.Core.Management
{
    /// <summary>Parámetros de la relación con los socios (en <c>career.json</c>).</summary>
    [Serializable]
    public sealed class SupporterConfig
    {
        /// <summary>Relación inicial (0–100).</summary>
        public float StartingRelation = 60f;
        /// <summary>Relación ganada al aceptar la rebaja que piden.</summary>
        public float AcceptRelation = 8f;
        /// <summary>Disgusto que se quita al aceptar la rebaja.</summary>
        public float AcceptUnrest = 12f;
        /// <summary>Disgusto por ignorar el pedido.</summary>
        public float IgnoreUnrest = 10f;
        /// <summary>Relación perdida al ignorar el pedido.</summary>
        public float IgnoreRelation = 6f;
        /// <summary>Disgusto por partido jugado con el precio sobre el ideal, por cada 10 % de exceso.</summary>
        public float UnrestPerTenPercent = 4f;
        /// <summary>Multiplicador del disgusto en partidos de poca trascendencia o con mala racha.</summary>
        public float LowStakesFactor = 1.6f;
        /// <summary>Racha por debajo de la cual el equipo «viene mal» (puntos sobre 15).</summary>
        public float BadFormThreshold = 0.35f;
        /// <summary>Disgusto que se calma por partido con el precio dentro del ideal.</summary>
        public float CalmPerMatch = 6f;
        /// <summary>Relación ganada por partido con el precio dentro del ideal.</summary>
        public float RelationPerFairMatch = 1.5f;
        /// <summary>Umbral crítico de disgusto.</summary>
        public float CriticalUnrest = 75f;
        /// <summary>Partidos seguidos sobre el umbral para que pidan la salida del DT.</summary>
        public int SustainedMatches = 3;
        /// <summary>Popularidad que pierde el DT cuando los socios piden su salida.</summary>
        public float SackingPressure = 10f;
    }

    /// <summary>Estado de la relación con los socios (se guarda con la partida).</summary>
    public sealed class SupporterState
    {
        /// <summary>Relación con los socios (0–100).</summary>
        public float Relation = 60f;
        /// <summary>Disgusto de los socios (0–100).</summary>
        public float Unrest;
        /// <summary>Partidos seguidos con el disgusto sobre el umbral crítico.</summary>
        public int CriticalStreak;
        /// <summary>Partido por el que hay un pedido de rebaja sin responder (0 = ninguno).</summary>
        public int PendingFixtureId;
        /// <summary>Precio que piden para ese partido.</summary>
        public long PendingSuggestedPrice;
        /// <summary>Pedidos aceptados.</summary>
        public int RequestsAccepted;
        /// <summary>Pedidos ignorados.</summary>
        public int RequestsIgnored;
    }

    /// <summary>Qué pasó con los socios tras un partido de local.</summary>
    public enum SupporterOutcome
    {
        /// <summary>Nada especial.</summary>
        None,
        /// <summary>Mensajes de repudio por el precio.</summary>
        Repudiation,
        /// <summary>Piden la salida del DT (disgusto crítico sostenido).</summary>
        DemandSacking
    }

    /// <summary>
    /// Pedidos y presión de los socios por el precio de las entradas: si el precio supera el máximo ideal, la agrupación de
    /// socios pide una rebaja antes del partido. Aceptarla mejora la relación; ignorarla o seguir subiendo hace crecer el
    /// disgusto, que baja la asistencia y, si se sostiene sobre el umbral crítico, termina pidiendo la salida del DT.
    /// </summary>
    public static class SupporterRequestSystem
    {
        /// <summary>Revisa el precio antes de un partido de local; si supera el ideal, deja un pedido pendiente.</summary>
        /// <returns>Verdadero si se creó un pedido nuevo (hay que avisar en el buzón).</returns>
        public static bool Review(SupporterState s, int fixtureId, long price, (long Min, long Max) range)
        {
            if (price <= range.Max || s.PendingFixtureId == fixtureId)
            {
                return false;
            }

            s.PendingFixtureId = fixtureId;
            s.PendingSuggestedPrice = range.Max;
            return true;
        }

        /// <summary>Acepta la rebaja pedida: devuelve el precio nuevo.</summary>
        public static long Accept(SupporterState s, SupporterConfig c)
        {
            long price = s.PendingSuggestedPrice;
            s.Relation = MathUtil.Clamp(s.Relation + c.AcceptRelation, 0f, 100f);
            s.Unrest = MathUtil.Clamp(s.Unrest - c.AcceptUnrest, 0f, 100f);
            s.RequestsAccepted++;
            s.PendingFixtureId = 0;
            return price;
        }

        /// <summary>Mantiene el precio pese al pedido.</summary>
        public static void Keep(SupporterState s, SupporterConfig c)
        {
            s.Relation = MathUtil.Clamp(s.Relation - c.IgnoreRelation, 0f, 100f);
            s.Unrest = MathUtil.Clamp(s.Unrest + c.IgnoreUnrest, 0f, 100f);
            s.RequestsIgnored++;
            s.PendingFixtureId = 0;
        }

        /// <summary>Actualiza el disgusto después de un partido de local con el precio que se cobró.</summary>
        /// <param name="s">Estado.</param>
        /// <param name="price">Precio cobrado.</param>
        /// <param name="range">Rango ideal del partido.</param>
        /// <param name="importance">Importancia del partido.</param>
        /// <param name="form">Racha (0–1).</param>
        /// <param name="c">Parámetros.</param>
        public static SupporterOutcome AfterHomeMatch(SupporterState s, long price, (long Min, long Max) range, MatchImportance importance, float form,
            SupporterConfig c)
        {
            if (s.PendingFixtureId != 0)
            {
                Keep(s, c); // se jugó sin responder: cuenta como ignorado
            }

            if (price > range.Max)
            {
                float excess = (float)(price - range.Max) / Math.Max(1L, range.Max);
                bool lowStakes = importance == MatchImportance.Meaningless || importance == MatchImportance.Regular || form < c.BadFormThreshold;
                float gain = c.UnrestPerTenPercent * excess * 10f * (lowStakes ? c.LowStakesFactor : 1f);
                s.Unrest = MathUtil.Clamp(s.Unrest + gain, 0f, 100f);
                s.Relation = MathUtil.Clamp(s.Relation - gain * 0.5f, 0f, 100f);
            }
            else
            {
                s.Unrest = MathUtil.Clamp(s.Unrest - c.CalmPerMatch, 0f, 100f);
                s.Relation = MathUtil.Clamp(s.Relation + c.RelationPerFairMatch, 0f, 100f);
            }

            if (s.Unrest <= c.CriticalUnrest)
            {
                s.CriticalStreak = 0;
                return SupporterOutcome.None;
            }

            s.CriticalStreak++;
            return s.CriticalStreak >= c.SustainedMatches ? SupporterOutcome.DemandSacking : SupporterOutcome.Repudiation;
        }
    }
}
