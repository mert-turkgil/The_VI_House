using VIHouse.Entities.Common;

namespace VIHouse.Entities.Commerce;

/// <summary>
/// The one money record every flow shares. A ticket purchase, a session purchase, a membership
/// checkout and a membership renewal are different things to fulfil, but the same thing to pay
/// for — and "what state is this payment in, according to the provider" needs one answer, in one
/// place, for the admin, the member and the ledger to agree.
///
/// This row says what happened to the <em>money</em>. The fulfilment rows (Booking, Membership,
/// SeminarEnrollment) say what the member <em>got</em>, and they point here through their
/// TransactionId. A transaction becomes Succeeded only from a provider event or a server-side
/// provider read that shows the payment settled — never from a browser landing on a success URL,
/// and never because the row was created.
/// </summary>
public class PaymentTransaction : BaseEntity
{
    public PaymentTransactionKind Kind { get; set; }

    /// <summary>Null until an account exists — a /join checkout creates the account only when the
    /// payment lands.</summary>
    public Guid? UserId { get; set; }

    /// <summary>The fulfilment row this pays for: "Payment", "MembershipPayment", "SeminarEnrollment",
    /// "PendingJoin". Kept as a name + id rather than three nullable FKs so the shape does not grow
    /// a column per new kind.</summary>
    public string RelatedEntityType { get; set; } = default!;
    public Guid RelatedEntityId { get; set; }

    public PaymentTransactionStatus Status { get; set; } = PaymentTransactionStatus.Pending;

    /// <summary>Minor units, in <see cref="Currency"/>. Set from the provider's own figure once an
    /// event carries one; the local price is only what the checkout was opened with.</summary>
    public long AmountMinor { get; set; }
    public string Currency { get; set; } = "GBP";
    public long AmountRefundedMinor { get; set; }

    // --- The provider's identifiers, each unique where present. ---------------------------------
    public string? ProviderCustomerId { get; set; }
    public string? ProviderSessionId { get; set; }
    public string? ProviderPaymentIntentId { get; set; }
    public string? ProviderChargeId { get; set; }
    public string? ProviderSubscriptionId { get; set; }
    public string? ProviderInvoiceId { get; set; }

    // --- Why and when. ---------------------------------------------------------------------------
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? FailedAt { get; set; }
    public DateTimeOffset? CanceledAt { get; set; }
    public DateTimeOffset? ExpiredAt { get; set; }
    public DateTimeOffset? RefundedAt { get; set; }

    /// <summary>A chargeback is open on this payment. A flag rather than a status: the money is
    /// still recorded as taken until the dispute is lost, and the status keeps saying so.</summary>
    public bool Disputed { get; set; }
    public DateTimeOffset? DisputedAt { get; set; }

    /// <summary>The provider event that last moved this row — the trail from a status back to
    /// the WebhookEvents row that caused it.</summary>
    public string? LastEventId { get; set; }

    public byte[]? RowVersion { get; set; }
}

public enum PaymentTransactionKind
{
    Experience,
    Session,
    Membership,
    MembershipRenewal,
}

/// <summary>
/// The internal payment state. Deliberately not Stripe's vocabulary: Stripe has a status per
/// object (session, payment intent, invoice, charge), this is the one answer the business needs.
/// The mapping from provider signals to these states, and the allowed transitions, live in
/// VIHouse.Business.Concrete.PaymentTransactionStateMachine and are documented in
/// docs/PAYMENT_ARCHITECTURE.md.
/// </summary>
public enum PaymentTransactionStatus
{
    /// <summary>Checkout opened; nothing paid yet. The hold or seat is kept while this lasts.</summary>
    Pending,
    /// <summary>The buyer finished checkout with a delayed payment method; the money is on its way
    /// and may still fail. Nothing is fulfilled.</summary>
    Processing,
    /// <summary>The provider needs the buyer to do something (3-D Secure, a bank confirmation)
    /// before it can collect. Nothing is fulfilled.</summary>
    RequiresAction,
    /// <summary>The provider confirmed the money. The only state that fulfils.</summary>
    Succeeded,
    /// <summary>The provider tried and could not collect. A retry — a new checkout, or the
    /// provider retrying an invoice — can still succeed.</summary>
    Failed,
    /// <summary>Stopped before any money moved: the buyer backed out, or the House cancelled.</summary>
    Canceled,
    /// <summary>Everything collected was returned.</summary>
    Refunded,
    /// <summary>Some of it was returned; the rest stands.</summary>
    PartiallyRefunded,
    /// <summary>The provider's checkout window closed with no payment attempt.</summary>
    Expired,
}
