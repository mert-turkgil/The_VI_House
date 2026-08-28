namespace VIHouse.WebUI.ViewModels.Shared;

/// <summary>
/// Backs Views/Shared/_CoverImage.cshtml — the single place a cover photograph is rendered.
///
/// The srcset logic is the interesting part. Images fetched by ClientApp/scripts/fetch-media.mjs are
/// written as "{path}-800.jpg" and "{path}-1600.jpg", so when a URL matches that convention this can
/// offer the browser both widths and let it pick. When it does not — an admin has pasted some other
/// path, which the free-text CoverImageUrl field allows — it degrades to a plain single-source
/// image rather than inventing URLs that would 404.
/// </summary>
public record CoverImageViewModel
{
    /// <summary>App-relative path, e.g. "/img/experiences/izmir-...-1600.jpg". Null renders the crest fallback.</summary>
    public string? Url { get; init; }

    /// <summary>Null (the default) means decorative — see the partial's comment on when that is right.</summary>
    public string? Alt { get; init; }

    /// <summary>Maps to a .cover--{Ratio} class: "4x3", "16x9", "21x9", "hero", "square".</summary>
    public string Ratio { get; init; } = "4x3";

    /// <summary>The sizes attribute. Defaults to a card-sized guess; heroes should pass "100vw".</summary>
    public string Sizes { get; init; } = "(min-width: 960px) 33vw, (min-width: 560px) 50vw, 100vw";

    /// <summary>True for an above-the-fold hero: skips lazy-loading and hints high priority.</summary>
    public bool Eager { get; init; }

    /// <summary>Widths the fetch script produces. Kept in step with media-manifest.json's "widths".</summary>
    private static readonly int[] AvailableWidths = [800, 1600];

    public string Src => Url ?? string.Empty;

    /// <summary>
    /// "…-800.jpg 800w, …-1600.jpg 1600w" when the URL follows the fetch script's naming convention;
    /// empty otherwise. An empty srcset attribute is ignored by browsers, so the plain src wins —
    /// which is exactly the wanted behaviour for an arbitrary admin-supplied URL.
    /// </summary>
    public string SrcSet
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Url)) return string.Empty;

            var marker = Url.LastIndexOf('-');
            var dot = Url.LastIndexOf('.');
            if (marker < 0 || dot < marker) return string.Empty;

            var widthPart = Url[(marker + 1)..dot];
            if (!int.TryParse(widthPart, out var width) || !AvailableWidths.Contains(width))
                return string.Empty;

            var stem = Url[..marker];
            var extension = Url[dot..];

            return string.Join(", ", AvailableWidths.Select(w => $"{stem}-{w}{extension} {w}w"));
        }
    }

    /// <summary>
    /// Intrinsic dimensions matching the CSS aspect ratio. These exist to reserve layout space, not
    /// to describe the file — the CSS sizes the element, and a mismatch between these numbers and
    /// the real pixels is harmless as long as the *ratio* is right.
    /// </summary>
    public int Width => 1600;

    public int Height => Ratio switch
    {
        "16x9" => 900,
        "21x9" => 686,
        "square" => 1600,
        "hero" => 800,
        _ => 1200, // 4x3
    };
}
