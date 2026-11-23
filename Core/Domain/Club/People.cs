using System;
using System.Collections.Generic;

namespace Football.Core.Domain
{
    /// <summary>Puesto en el cuerpo técnico.</summary>
    public enum StaffRole
    {
        /// <summary>Director técnico.</summary>
        Manager,
        /// <summary>Ayudante de campo (entrenamiento).</summary>
        AssistantCoach,
        /// <summary>Ojeador.</summary>
        Scout,
        /// <summary>Fisioterapeuta / médico.</summary>
        Physio,
        /// <summary>Entrenador de juveniles.</summary>
        YouthCoach,
        /// <summary>Entrenador de arqueros.</summary>
        GoalkeeperCoach
    }

    /// <summary>Miembro del cuerpo técnico, con capacidad de 1 a 20 (escala de Football Manager).</summary>
    public sealed class StaffMember
    {
        /// <summary>Identificador.</summary>
        public int Id;
        /// <summary>Nombre.</summary>
        public string Name;
        /// <summary>Club (null si está libre).</summary>
        public int? ClubId;
        /// <summary>Puesto.</summary>
        public StaffRole Role;
        /// <summary>Capacidad (1–20).</summary>
        public int Ability = 10;
        /// <summary>Sueldo semanal.</summary>
        public long WeeklyWage;
        /// <summary>Si fue jugador, su identificador (exjugadores retirados que pasan al cuerpo técnico).</summary>
        public int? FormerPlayerId;
    }

    /// <summary>Rasgo de personalidad de un jugador.</summary>
    public enum PersonalityTrait
    {
        /// <summary>Sin rasgo marcado.</summary>
        Balanced,
        /// <summary>Profesional: progresa más y su moral es estable.</summary>
        Professional,
        /// <summary>Líder: pesa en el vestuario.</summary>
        Leader,
        /// <summary>Ambicioso: quiere jugar y cobrar más; se va si no progresa.</summary>
        Ambitious,
        /// <summary>Leal: acepta quedarse por menos.</summary>
        Loyal,
        /// <summary>Temperamental: más tarjetas y moral que sube y baja.</summary>
        Temperamental
    }

    /// <summary>Perfil de un club manejado por la IA en el mercado.</summary>
    public enum ClubProfile
    {
        /// <summary>Comprador: paga más por figuras hechas.</summary>
        Buyer,
        /// <summary>Canterano y vendedor: vende caro y apuesta por juveniles.</summary>
        YouthSeller,
        /// <summary>Defensivo: busca defensores y no arriesga en el mercado.</summary>
        Defensive
    }

    /// <summary>Nivel del jugador en la jerarquía del vestuario.</summary>
    public enum DressingRoomTier
    {
        /// <summary>Resto del plantel.</summary>
        Other,
        /// <summary>Influyente.</summary>
        Influential,
        /// <summary>Muy influyente.</summary>
        HighlyInfluential,
        /// <summary>Líder del equipo.</summary>
        TeamLeader
    }

    /// <summary>Oferta por un jugador, con cláusulas.</summary>
    public sealed class TransferOffer
    {
        /// <summary>Jugador.</summary>
        public int PlayerId;
        /// <summary>Club comprador.</summary>
        public int BuyerClubId;
        /// <summary>Club vendedor.</summary>
        public int SellerClubId;
        /// <summary>Monto fijo.</summary>
        public long Fee;
        /// <summary>Porcentaje de una venta futura que se queda el vendedor (0–0,5).</summary>
        public float SellOnShare;
        /// <summary>Monto extra por cada partido jugado hasta un tope (bonus por presencias).</summary>
        public long AppearanceBonus;
        /// <summary>Cláusula de rescisión que tendrá el contrato nuevo (0 = sin cláusula).</summary>
        public long ReleaseClause;
    }

    /// <summary>Respuesta a una oferta.</summary>
    public enum OfferResponse
    {
        /// <summary>Aceptada.</summary>
        Accepted,
        /// <summary>Contraoferta (ver el monto pedido).</summary>
        CounterOffer,
        /// <summary>Rechazada.</summary>
        Rejected
    }

    /// <summary>Motivo por el que una oferta del usuario no se presenta.</summary>
    public enum OfferBlock
    {
        /// <summary>Se puede presentar.</summary>
        None,
        /// <summary>El libro de pases está cerrado.</summary>
        WindowClosed,
        /// <summary>Más de lo que hay en el presupuesto de fichajes.</summary>
        OverBudget,
        /// <summary>El jugador es propio o no tiene club.</summary>
        NotForSale,
        /// <summary>El club ya tiene el cupo de extranjeros: hay que vender uno antes.</summary>
        ForeignQuota
    }

    /// <summary>Noticia del buzón (el texto sale de los archivos de idioma con la clave y los datos).</summary>
    public sealed class NewsItem
    {
        /// <summary>Fecha.</summary>
        public DateTime Date;
        /// <summary>Club al que va dirigida.</summary>
        public int ClubId;
        /// <summary>Clave de la plantilla de texto (por ejemplo, «news.injury»).</summary>
        public string Key;
        /// <summary>Datos que completan la plantilla ({0}, {1}…).</summary>
        public string[] Args;
        /// <summary>Leída.</summary>
        public bool Read;
    }

    /// <summary>Un récord del club o de la liga.</summary>
    public sealed class RecordEntry
    {
        /// <summary>Qué récord es (por ejemplo, «goles en una temporada»).</summary>
        public string Key;
        /// <summary>Valor.</summary>
        public float Value;
        /// <summary>Quién lo tiene (jugador o club).</summary>
        public int HolderId;
        /// <summary>Temporada.</summary>
        public int Season;
    }

    /// <summary>Promesa de minutos incumplida (la publica la gestión).</summary>
    public struct PromiseBroken
    {
        /// <summary>Jugador.</summary>
        public int PlayerId;
        /// <summary>Club.</summary>
        public int ClubId;
    }

    /// <summary>Llegaron juveniles de la cantera.</summary>
    public struct YouthIntakeArrived
    {
        /// <summary>Club.</summary>
        public int ClubId;
        /// <summary>Cuántos.</summary>
        public int Count;
        /// <summary>Potencial del mejor.</summary>
        public int BestPotential;
    }

    /// <summary>Se cerró un traspaso.</summary>
    public struct TransferCompleted
    {
        /// <summary>Jugador.</summary>
        public int PlayerId;
        /// <summary>Comprador.</summary>
        public int BuyerClubId;
        /// <summary>Vendedor.</summary>
        public int SellerClubId;
        /// <summary>Monto.</summary>
        public long Fee;
    }

    /// <summary>Se batió un récord.</summary>
    public struct RecordBroken
    {
        /// <summary>Récord.</summary>
        public string Key;
        /// <summary>Quién.</summary>
        public int HolderId;
        /// <summary>Valor.</summary>
        public float Value;
    }

    /// <summary>Un jugador se retiró (y quizás pasó al cuerpo técnico).</summary>
    public struct PlayerRetired
    {
        /// <summary>Jugador.</summary>
        public int PlayerId;
        /// <summary>Si pasó al cuerpo técnico, su ficha nueva.</summary>
        public int? StaffId;
    }
}
