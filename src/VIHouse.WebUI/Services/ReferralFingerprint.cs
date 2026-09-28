using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using VIHouse.Business.Abstract;
using VIHouse.WebUI.Helpers;

namespace VIHouse.WebUI.Services;

/// <summary>
/// What a referral landing records about the visitor for fraud checks: a keyed hash of the IP
/// address (never the address), the User-Agent, and the signed-in account. The key comes from
/// Referrals:IpHashKey when set — set it in production so the hashes cannot be reversed by
/// hashing every IPv4 address — and otherwise from a fixed per-app string.
/// </summary>
public sealed class ReferralFingerprint(IConfiguration configuration)
{
    private readonly byte[] key = Encoding.UTF8.GetBytes(
        configuration["Referrals:IpHashKey"] is { Length: > 0 } configured ? configured : "VIHouse.ReferralVisits.v1");

    public ReferralVisitor For(HttpContext http)
    {
        var ip = http.Connection.RemoteIpAddress;
        string? ipHash = null;
        if (ip is not null)
        {
            if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
            ipHash = Convert.ToHexStringLower(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(ip.ToString())));
        }

        var userAgent = http.Request.Headers.UserAgent.ToString();
        var userId = http.User.UserId();
        return new ReferralVisitor(ipHash, string.IsNullOrWhiteSpace(userAgent) ? null : userAgent, userId);
    }
}
