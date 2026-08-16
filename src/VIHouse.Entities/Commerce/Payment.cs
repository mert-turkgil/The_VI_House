using VIHouse.Entities.Common;

namespace VIHouse.Entities.Commerce;

/// <summary>
/// Never stores card data — Stripe Checkout/Elements hold that. ProviderReference is the Stripe
/// Checkout Session ID; BookingId stays null until the webhook confirms payment and a Booking is
/// created (brief §32: the browser success redirect is never trusted on its own).
/// </summary>
public class Payment : BaseEntity
{
    public Guid? BookingId { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid? UserId { get; set; }
    public Guid ExperienceId { get; set; }
    public Guid TicketTypeId { get; set; }

    public PaymentProviderType Provider { get; set; } = PaymentProviderType.Stripe;
    public long AmountMinor { get; set; }
    public string Currency { get; set; } = "GBP";
    public PaymentStatus Status { get; set; } = PaymentStatus.Created;
    public string? PaymentMethod { get; set; }

    public string ProviderReference { get; set; } = default!;
    public string? InvoiceReference { get; set; }
    public string? RefundStatus { get; set; }
}
