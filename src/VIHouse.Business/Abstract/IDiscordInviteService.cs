namespace VIHouse.Business.Abstract;

/// <summary>
/// Mints single-use Discord invites for a channel, on demand, through the bot configured in
/// <see cref="Options.DiscordOptions"/>. A generated invite is what makes a community link safe
/// to show to one person: it works once, then it is spent, and it expires on its own.
/// </summary>
public interface IDiscordInviteService
{
    /// <summary>False when no bot token is configured — callers show the static link instead.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// A fresh invite URL for the channel, or null when Discord refuses or is unreachable. Cached
    /// per user and channel for the invite's lifetime, so reloading the page hands back the same
    /// unused link rather than minting a pile of them.
    /// </summary>
    Task<string?> CreateInviteAsync(Guid userId, string channelId, CancellationToken ct = default);
}
