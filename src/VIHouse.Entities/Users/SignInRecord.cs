using VIHouse.Entities.Common;

namespace VIHouse.Entities.Users;

/// <summary>
/// One row per completed sign-in: where from and with what. Exists so a sign-in from an address
/// the account has not used lately can be told apart from the usual one and reported to the owner
/// (SecurityAlertService). Kept for ninety days, then purged.
/// </summary>
public class SignInRecord : BaseEntity
{
    public Guid UserId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset At { get; set; }
}
