using VIHouse.Entities.Seminars;

namespace VIHouse.DataAccess.Abstract;

public interface ISeminarEnrollmentRepository : IRepository<SeminarEnrollment>
{
    /// <summary>The row for one person on one seminar, whatever its status. There is at most one —
    /// see the unique index in SeminarEnrollmentConfiguration.</summary>
    Task<SeminarEnrollment?> GetForUserAsync(Guid seminarId, Guid userId, CancellationToken ct = default);

    /// <summary>Webhook lookup: matches the payment provider's checkout session id.</summary>
    Task<SeminarEnrollment?> GetByProviderReferenceAsync(string providerReference, CancellationToken ct = default);

    /// <summary>Everyone confirmed on a seminar, newest first — the admin attendee list.</summary>
    Task<List<SeminarEnrollment>> GetForSeminarAsync(Guid seminarId, CancellationToken ct = default);

    /// <summary>Confirmed enrolments for one member, for their account page.</summary>
    Task<List<SeminarEnrollment>> GetConfirmedForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Seats taken. Counts Pending alongside Confirmed so an in-flight checkout holds its
    /// place for as long as it lives, rather than letting a capacity-1 session sell twice.</summary>
    Task<int> CountTakenAsync(Guid seminarId, CancellationToken ct = default);
}
