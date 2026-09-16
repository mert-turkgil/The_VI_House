namespace VIHouse.WebUI.Helpers;

/// <summary>
/// The models of the small building blocks under Views/Emails — a heading, a button, a callout, a
/// code box. Records rather than tuples so a template reads as prose and a missing argument is a
/// compile error in the view, not a swapped label at 2am in someone's inbox.
/// </summary>
public static class EmailParts
{
    /// <summary>Gold eyebrow + serif heading. Tone: "gold" (default), "warning" (a payment that
    /// failed, a decision), "security" (an alert banner).</summary>
    public sealed record Heading(string Eyebrow, string Title, string Tone = "gold");

    /// <summary>A bulletproof button. Secondary draws it as an outlined ghost next to a primary one.</summary>
    public sealed record Button(string Url, string Label, bool Secondary = false);

    /// <summary>A callout box. Tone: "info" (cream), "warning" (amber), "success" (green tint).
    /// Body is plain text — it may come from an admin's free-text note.</summary>
    public sealed record Note(string Body, string Tone = "info", string? Label = null);

    /// <summary>A monospace box for a reference, a code or a fallback link.</summary>
    public sealed record Code(string Value, string? Label = null, string? Href = null);
}
