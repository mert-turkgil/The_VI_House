using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace VIHouse.Business.Concrete;

/// <summary>
/// Turns stored article HTML into what a reader's browser gets.
///
/// Only one transformation today: an &lt;oembed&gt; — the inert marker CKEditor stores for a video —
/// becomes a click-to-play facade. The player itself is never in the page until someone asks for it,
/// which keeps the article fast (an embedded YouTube player is roughly a megabyte of script) and
/// means a reader who never touches the video is never handed to Google's player.
///
/// Deliberately a read-side transformation. The database keeps the canonical, editor-shaped markup:
/// baking the facade in on write would mean every change to how a video looks needed a data
/// migration, and would put presentation into content the editor has to round-trip.
/// </summary>
public static class ArticleHtml
{
    private static readonly HtmlParser Parser = new();

    /// <param name="playLabel">Accessible name for the play button, already localised by the caller
    /// — this layer has no localiser and should not grow one to write four words.</param>
    public static string RenderForDisplay(string? html, string playLabel)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        // Same bail-out as EditorHtml.NormaliseEmbeds: almost no article has a video, and parsing
        // every one of them to find that out would be the most expensive thing this method does.
        if (!html.Contains("<oembed", StringComparison.OrdinalIgnoreCase)) return html;

        var document = Parser.ParseDocument($"<body>{html}</body>");

        foreach (var embed in document.Body!.QuerySelectorAll("oembed").ToList())
        {
            // Validated on write (EditorHtml) and again here. The second check is not redundant: it
            // is what guarantees the id going into the player URL below came from a pattern match
            // rather than from whatever the column happens to contain.
            if (!YouTubeUrl.TryParseId(embed.GetAttribute("url"), out var id))
            {
                embed.Remove();
                continue;
            }

            var target = embed.ParentElement is IHtmlElement { LocalName: "figure" } figure ? figure : (IElement)embed;
            target.OuterHtml = Facade(document, id, playLabel);
        }

        return document.Body.InnerHtml;
    }

    /// <summary>
    /// The poster is fetched from Google's image CDN — the one third-party request an article with
    /// a video makes before anyone clicks. It carries no cookies and no referrer; swapping it for a
    /// locally hosted still is a change to this one line if that request is unwelcome.
    ///
    /// Built through the document rather than by concatenating strings, so the label is escaped by
    /// the DOM rather than by remembering to escape it.
    /// </summary>
    private static string Facade(IDocument document, string id, string playLabel)
    {
        var wrapper = document.CreateElement("div");
        wrapper.ClassName = "video-embed";
        wrapper.SetAttribute("data-video-id", id);

        var button = document.CreateElement("button");
        button.SetAttribute("type", "button");
        button.ClassName = "video-embed__play";
        button.SetAttribute("aria-label", playLabel);

        var poster = document.CreateElement("img");
        poster.ClassName = "video-embed__poster";
        poster.SetAttribute("src", YouTubeUrl.ThumbnailUrl(id));
        // Decorative: the button beside it carries the accessible name, and "video thumbnail" read
        // aloud tells a screen reader user nothing they cannot already tell.
        poster.SetAttribute("alt", string.Empty);
        poster.SetAttribute("loading", "lazy");
        poster.SetAttribute("decoding", "async");
        poster.SetAttribute("referrerpolicy", "no-referrer");

        var icon = document.CreateElement("span");
        icon.ClassName = "video-embed__icon";
        icon.SetAttribute("aria-hidden", "true");

        button.AppendChild(poster);
        button.AppendChild(icon);
        wrapper.AppendChild(button);

        return wrapper.OuterHtml;
    }
}
