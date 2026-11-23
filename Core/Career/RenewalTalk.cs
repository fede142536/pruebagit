using System;
using Football.Core.Domain;

namespace Football.Core.Career
{
    /// <summary>Estado de una negociación de renovación.</summary>
    public enum TalkStatus
    {
        /// <summary>El jugador se está tomando su tiempo para responder.</summary>
        Thinking,
        /// <summary>Aceptó: el contrato nuevo quedó firmado.</summary>
        Accepted,
        /// <summary>Contestó con otro pedido (ver lo que exige); se puede mejorar la oferta.</summary>
        Counter,
        /// <summary>No renueva: se irá libre al terminar el contrato.</summary>
        Refused
    }

    /// <summary>Por qué un jugador no acepta una oferta.</summary>
    public enum TalkReason
    {
        /// <summary>Sin objeciones.</summary>
        None,
        /// <summary>Quiere más sueldo.</summary>
        Wage,
        /// <summary>Quiere un contrato más largo.</summary>
        Years,
        /// <summary>Quiere la seguridad de jugar (ser figura o titular).</summary>
        PlayingTime,
        /// <summary>Está descontento con el club.</summary>
        Unhappy
    }

    /// <summary>Lo que pide un jugador para renovar.</summary>
    public struct RenewalDemand
    {
        /// <summary>Sueldo semanal que pide.</summary>
        public long Wage;
        /// <summary>Años mínimos de contrato que pide (contados desde esta temporada).</summary>
        public int Years;
        /// <summary>Exige que se le prometa ser titular (o figura): juega menos de lo que cree merecer.</summary>
        public bool WantsPlayingTime;
        /// <summary>Rol que espera tener en el equipo (figura, titular, rotación o suplente), según su nivel en el plantel.</summary>
        public PlayingTimePromise Role;
        /// <summary>Descuento sobre el sueldo pedido que acepta según su personalidad (los leales se conforman con menos).</summary>
        public float Flexibility;
    }

    /// <summary>Una negociación de renovación entre el usuario y un jugador de su plantel.</summary>
    public sealed class RenewalTalk
    {
        /// <summary>Jugador.</summary>
        public int PlayerId;
        /// <summary>Sueldo semanal ofrecido.</summary>
        public long OfferWage;
        /// <summary>Años ofrecidos (contados desde esta temporada).</summary>
        public int OfferYears;
        /// <summary>Promesa de minutos ofrecida.</summary>
        public PlayingTimePromise OfferPromise;
        /// <summary>Día en que se hizo la última oferta.</summary>
        public DateTime SentOn;
        /// <summary>Día en que el jugador responde.</summary>
        public DateTime ReplyOn;
        /// <summary>Estado.</summary>
        public TalkStatus Status;
        /// <summary>Motivo del último rechazo o contraoferta.</summary>
        public TalkReason Reason;
        /// <summary>Sueldo que pide tras la última respuesta.</summary>
        public long DemandWage;
        /// <summary>Años que pide tras la última respuesta.</summary>
        public int DemandYears;
        /// <summary>Rol que pide tras la última respuesta.</summary>
        public PlayingTimePromise DemandRole = PlayingTimePromise.Regular;
        /// <summary>Rondas de oferta ya hechas.</summary>
        public int Rounds;
    }
}
