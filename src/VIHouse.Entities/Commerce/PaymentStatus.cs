namespace VIHouse.Entities.Commerce;

public enum PaymentStatus
{
    Created,
    Pending,
    Authorized,
    Paid,
    Failed,
    Cancelled,
    PartiallyRefunded,
    Refunded,
    Chargeback
}
