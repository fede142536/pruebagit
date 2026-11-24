using System;
using Football.Core.Domain;

namespace Football.Core.Career
{
    /// <summary>Tipo de operación de una oferta.</summary>
    public enum DealKind
    {
        /// <summary>Venta definitiva.</summary>
        Sale,
        /// <summary>Préstamo hasta el final de la temporada; el jugador vuelve.</summary>
        Loan,
        /// <summary>Préstamo con compra obligatoria: si juega los minutos pedidos, el club se lo queda al final por el monto fijado.</summary>
        LoanObligation
    }

    /// <summary>Qué piensa un jugador de una oferta de otro club.</summary>
    public enum PlayerStance
    {
        /// <summary>Quiere irse.</summary>
        WantsOut,
        /// <summary>Se quedaría si le mejoran el contrato.</summary>
        Open,
        /// <summary>Quiere quedarse.</summary>
        WantsStay
    }

    /// <summary>Resultado de aceptar una oferta.</summary>
    public enum DealOutcome
    {
        /// <summary>Hecho.</summary>
        Done,
        /// <summary>El libro de pases está cerrado.</summary>
        WindowClosed,
        /// <summary>El club no acepta la cláusula de recompra pedida (pide más).</summary>
        BuybackTooLow,
        /// <summary>La oferta ya no vale (el jugador se fue o ya tiene otro trato).</summary>
        Expired,
        /// <summary>No alcanza el presupuesto de fichajes o de sueldos.</summary>
        NoBudget,
        /// <summary>El club rechazó el pedido.</summary>
        Rejected,
        /// <summary>El jugador no aceptaría quedarse (quiere irse).</summary>
        WontStay,
        /// <summary>El club ya tiene el cupo de extranjeros.</summary>
        ForeignQuota
    }

    /// <summary>Un jugador del usuario puesto en la lista de transferibles o cedibles, con el precio que se pide.</summary>
    public sealed class TransferListing
    {
        /// <summary>Jugador.</summary>
        public int PlayerId;
        /// <summary>Precio pedido por una venta.</summary>
        public long AskingPrice;
        /// <summary>Se acepta venderlo.</summary>
        public bool ForSale;
        /// <summary>Se acepta cederlo a préstamo.</summary>
        public bool ForLoan;
        /// <summary>Fecha en que se lo puso en la lista.</summary>
        public DateTime Date;
    }

    /// <summary>Préstamo vigente de un jugador (del usuario o al usuario).</summary>
    public sealed class LoanRecord
    {
        /// <summary>Jugador.</summary>
        public int PlayerId;
        /// <summary>Club dueño del jugador.</summary>
        public int OwnerClubId;
        /// <summary>Club que lo tiene prestado.</summary>
        public int BorrowerClubId;
        /// <summary>Fecha en que empezó.</summary>
        public DateTime Start;
        /// <summary>Fracción del sueldo que paga el club que lo recibe (0,5–1).</summary>
        public float BorrowerWageShare = 1f;
        /// <summary>Hay compra obligatoria al final si juega los minutos pedidos.</summary>
        public bool Obligation;
        /// <summary>Monto de la compra obligatoria.</summary>
        public long PurchaseFee;
        /// <summary>Minutos que tiene que jugar en la temporada para que se active la compra.</summary>
        public float MinMinutes;
    }

    /// <summary>Cláusula de recompra: el club que vendió puede volver a comprar al jugador por un monto fijo hasta una fecha.</summary>
    public sealed class BuybackClause
    {
        /// <summary>Jugador.</summary>
        public int PlayerId;
        /// <summary>Club que vendió y puede recomprar.</summary>
        public int ClubId;
        /// <summary>Club que lo tiene ahora.</summary>
        public int HolderClubId;
        /// <summary>Monto de la recompra.</summary>
        public long Fee;
        /// <summary>Último día para ejercerla.</summary>
        public DateTime Expires;
    }

    /// <summary>La conversación con el jugador sobre una oferta.</summary>
    public sealed class PlayerTalk
    {
        /// <summary>Qué quiere el jugador.</summary>
        public PlayerStance Stance;
        /// <summary>Sueldo semanal nuevo con el que se quedaría (solo si <see cref="PlayerStance.Open"/>).</summary>
        public long KeepWage;
        /// <summary>Años de contrato con los que se quedaría.</summary>
        public int KeepYears;
        /// <summary>La oferta es por un jugador que el usuario puso en la lista: no hay conversación (ya sabe que lo dejaste libre).</summary>
        public bool Listed;
    }
}
