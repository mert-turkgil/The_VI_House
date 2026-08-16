namespace VIHouse.Entities.Commerce;

/// <summary>
/// Stripe is the only Phase 1 value, but the field exists precisely so a second provider (or
/// manual bank transfer, brief §150) can be added later without a schema change — the Payment
/// Service Layer abstraction (VIHouse.Business.Abstract.IPaymentProvider) is what actually keeps
/// business logic provider-independent (brief §29).
/// </summary>
public enum PaymentProviderType
{
    Stripe
}
