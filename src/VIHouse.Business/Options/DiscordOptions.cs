namespace VIHouse.Business.Options;

/// <summary>
/// Bound from the "Discord" configuration section. The bot token is a secret (user-secrets in
/// Development, the DISCORD_BOT_TOKEN GitHub Secret in Production, written into
/// appsettings.Production.json by deploy.yml). Leave it empty and every community link falls
/// back to its static URL — the feature is additive, never a precondition.
///
/// Setup: create an application at discord.com/developers, add a bot, invite it to the server
/// with the "Create Instant Invite" and "Manage Roles" permissions, and paste its token here.
/// </summary>
public class DiscordOptions
{
    public string? BotToken { get; set; }

    /// <summary>The server id — needed only to assign a plan's DiscordRoleId on join.</summary>
    public string? GuildId { get; set; }

    /// <summary>How long a generated invite stays valid. Long enough to open the email later that
    /// day; short enough that a forwarded one has usually died.</summary>
    public int InviteMaxAgeSeconds { get; set; } = 86_400;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BotToken);
}
