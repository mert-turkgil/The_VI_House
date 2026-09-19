using Microsoft.AspNetCore.Identity;
using VIHouse.Entities.Users;

namespace VIHouse.DataAccess.Identity;

/// <summary>
/// Lives in DataAccess (not Entities) so VIHouse.Entities stays free of any Identity/EF package
/// dependency. Guid-keyed, matching every other entity's primary key type.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;

    /// <summary>ISO 3166-1 alpha-2 country code (brief §186).</summary>
    public string Country { get; set; } = default!;
    public string? City { get; set; }

    public MemberStatus MemberStatus { get; set; } = MemberStatus.PendingApplication;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>The provider's customer record for this person, learned from the first payment
    /// and handed to every later checkout — so a member is one customer at Stripe with one billing
    /// portal and one payment-method list, not a new customer per purchase.</summary>
    public string? ProviderCustomerId { get; set; }
}
