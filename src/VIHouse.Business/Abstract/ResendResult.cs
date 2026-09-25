namespace VIHouse.Business.Abstract;

/// <summary>What happened when an admin pressed Resend on a failed email or text.</summary>
public record ResendResult(bool Sent, string Message)
{
    public static ResendResult Ok(string message) => new(true, message);
    public static ResendResult Refused(string message) => new(false, message);
}
