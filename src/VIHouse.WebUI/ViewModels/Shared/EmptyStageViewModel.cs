namespace VIHouse.WebUI.ViewModels.Shared;

/// <summary>
/// The designed empty section shown where a listing has nothing published yet — Experiences,
/// Sessions, Membership and Join. The topic picks the copy (EmptyStage.{Topic}.* resources), the
/// three tiles' icons, the buttons, and the Launch List source its notify-me sign-ups are tagged
/// with ("empty:experiences").
/// </summary>
public record EmptyStageViewModel(string Topic)
{
    public static readonly string[] Topics = ["experiences", "sessions", "membership"];

    public static bool IsTopic(string? topic) => topic is not null && Topics.Contains(topic);

    /// <summary>"experiences" → "Experiences", the resource key segment.</summary>
    public string Key => char.ToUpperInvariant(Topic[0]) + Topic[1..];
}
