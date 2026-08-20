using Ganss.Xss;

namespace VIHouse.Business.Concrete;

/// <summary>
/// Cleans and normalises the HTML that CKEditor produces for a <see cref="Entities.Journal.JournalPost"/>
/// body before it is stored.
///
/// Journal content is admin-authored, never user-generated, so this is defence in depth rather than
/// the primary control — it means a hijacked admin session (or a paste from a compromised source)
/// can't turn a published article into a script-injection vector for every reader. Sanitising on
/// write rather than on read keeps the cost off the hot path and means the database only ever holds
/// content that is already safe to render.
/// </summary>
public static class JournalHtml
{
    // Built once: HtmlSanitizer is thread-safe for Sanitize() and configuring it per call would
    // rebuild the whole allow-list on every save.
    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();

        // The default allow-list is already close to what the editor's toolbar can emit; these are
        // the extras the configured plugins produce (source editing lets an admin hand-write them).
        sanitizer.AllowedAttributes.Add("class");
        sanitizer.AllowedAttributes.Add("target");
        sanitizer.AllowedAttributes.Add("rel");

        // Only http/https/mailto links survive — this is what blocks `javascript:` hrefs, the most
        // likely way a pasted link turns into script execution.
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("https");
        sanitizer.AllowedSchemes.Add("mailto");

        return sanitizer;
    }

    /// <summary>Sanitises editor HTML, converting legacy plain-text bodies to paragraphs first.</summary>
    public static string Sanitize(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return string.Empty;

        return Sanitizer.Sanitize(EnsureHtml(body));
    }

    /// <summary>
    /// Posts written before the rich text editor existed are stored as plain text with
    /// blank-line-separated paragraphs. Rendering those verbatim through Html.Raw would collapse
    /// every paragraph into one run-on block, so they're wrapped on the way through. Content that
    /// already contains markup is returned untouched.
    /// </summary>
    public static string EnsureHtml(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return string.Empty;
        if (body.Contains('<')) return body;

        var paragraphs = body
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => $"<p>{System.Net.WebUtility.HtmlEncode(p).Replace("\n", "<br />")}</p>");

        return string.Concat(paragraphs);
    }
}
