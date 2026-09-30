using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using VIHouse.Business.Options;
using VIHouse.Entities.Journal;

namespace VIHouse.Business.Concrete;

/// <summary>
/// Reads and writes <see cref="JournalPostMedia.Captions"/>, the per-language gallery captions.
///
/// The fallback matches <see cref="TranslationLookup"/> on purpose: the reader's exact culture, then
/// the same language in another region, then the default culture, then whatever exists. A photograph
/// captioned only in English therefore shows that English caption to a German reader, just as a
/// post written only in English does.
/// </summary>
public static class JournalMediaCaptions
{
    /// <summary>Same limit as the admin input. Room for a sentence or two, but not an essay.</summary>
    public const int MaxLength = 300;

    /// <summary>Every stored caption, keyed by culture. Empty rather than null for a photograph
    /// without one, and for a column holding something that is not a JSON object.</summary>
    public static IReadOnlyDictionary<string, string> Read(JournalPostMedia media)
    {
        if (string.IsNullOrWhiteSpace(media.Captions)) return Empty;

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(media.Captions);
            return parsed is null ? Empty : new Dictionary<string, string>(parsed, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return Empty;
        }
    }

    /// <summary>The caption to show a reader in <paramref name="culture"/>, or null if there is none
    /// in any language.</summary>
    public static string? Resolve(JournalPostMedia media, string? culture)
    {
        var captions = Read(media);
        if (captions.Count == 0) return null;

        var wanted = SiteCultures.Normalise(culture);
        if (captions.TryGetValue(wanted, out var exact)) return exact;

        var language = wanted.Split('-')[0];
        var sameLanguage = captions.FirstOrDefault(c => c.Key.Split('-')[0].Equals(language, StringComparison.OrdinalIgnoreCase));
        if (sameLanguage.Value is not null) return sameLanguage.Value;

        if (captions.TryGetValue(SiteCultures.Default, out var fallback)) return fallback;

        return captions.Values.First();
    }

    /// <summary>
    /// Stores <paramref name="captions"/>, keeping only supported cultures with non-blank text.
    /// Blank means "no caption in this language", so it inherits instead of showing an empty line.
    /// </summary>
    public static void Write(JournalPostMedia media, IReadOnlyDictionary<string, string?> captions)
    {
        var kept = captions
            .Where(c => SiteCultures.IsSupported(c.Key) && !string.IsNullOrWhiteSpace(c.Value))
            .ToDictionary(
                c => SiteCultures.Normalise(c.Key),
                c => Text.Clip(c.Value!.Trim(), MaxLength)!);

        media.Captions = kept.Count == 0 ? null : JsonSerializer.Serialize(kept, Json);
    }

    // Non-ASCII letters (ü, ş, õ) are stored as themselves rather than as escapes, so the column
    // reads as it was written.
    private static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };

    private static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>();
}
