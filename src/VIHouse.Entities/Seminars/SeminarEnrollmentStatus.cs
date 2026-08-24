namespace VIHouse.Entities.Seminars;

public enum SeminarEnrollmentStatus
{
    /// <summary>Checkout started, money not yet confirmed. Grants nothing.</summary>
    Pending,

    /// <summary>Paid, comped, or covered by membership. The only status that opens the content.</summary>
    Confirmed,

    /// <summary>Checkout expired or was abandoned. Kept rather than deleted so the row can be
    /// revived on a retry, and so the attempt stays visible to support.</summary>
    Cancelled,
}
