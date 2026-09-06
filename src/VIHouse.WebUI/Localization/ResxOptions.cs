namespace VIHouse.WebUI.Localization;

/// <summary>
/// Bound from the "Localization" configuration section.
///
/// Empty means "the Resources folder under the content root", which is right for Development and
/// for a deployment that is happy for edits to be replaced by whatever git holds at the next
/// release. Point it somewhere outside the application directory — the same reasoning as
/// <c>Media:RootPath</c> and <c>DataProtection:KeysPath</c> — when translations edited in
/// Production need to survive a redeploy.
/// </summary>
public class ResxOptions
{
    public string ResourcesPath { get; set; } = "";
}
