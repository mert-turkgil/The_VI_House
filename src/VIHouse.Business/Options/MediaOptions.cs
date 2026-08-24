namespace VIHouse.Business.Options;

/// <summary>Where uploaded seminar assets live on disk. Non-secret, environment-specific.</summary>
public class MediaOptions
{
    /// <summary>
    /// Absolute path to the media root. Left blank in Development, where Program.cs falls back to
    /// &lt;ContentRoot&gt;/App_Media.
    ///
    /// In Production this must point *outside* the deployed application directory, for the same
    /// reason DataProtection:KeysPath does: publishing over the app would otherwise delete every
    /// recording an admin has ever uploaded.
    ///
    /// It is deliberately not under wwwroot and is never mapped into the static-file pipeline —
    /// seminar assets are streamed by SeminarsController after an enrolment check, so a priced,
    /// members-only session's video cannot be fetched by anyone the link reaches.
    /// </summary>
    public string RootPath { get; set; } = "";
}
