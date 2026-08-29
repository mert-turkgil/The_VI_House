namespace VIHouse.Business.Abstract;

/// <summary>
/// Transport only — one HTTP call to the gateway, no logging and no retries. The counterpart to
/// IEmailSender; callers use ISmsService instead, which is the piece that writes the audit row and
/// swallows the failure.
/// </summary>
public interface ISmsSender
{
    /// <summary>
    /// Whether a gateway is actually configured. Separated from a failed send so the admin screens can
    /// say "no SMS gateway is set up" rather than "the text message failed" — very different problems.
    /// </summary>
    bool IsConfigured { get; }

    /// <param name="toPhoneE164">Already normalised — see PhoneNumber.TryNormalise.</param>
    Task SendAsync(string toPhoneE164, string body, CancellationToken ct = default);
}
