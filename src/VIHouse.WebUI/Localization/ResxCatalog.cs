using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Options;

namespace VIHouse.WebUI.Localization;

/// <summary>
/// Reads and writes the SharedResource*.resx files directly, so a translation edited in the admin
/// panel is live on the next request instead of at the next deployment.
///
/// Why this exists at all: MSBuild compiles .resx files into the assembly and its satellite DLLs,
/// and ResourceManagerStringLocalizerFactory then caches every resolved key for the life of the
/// process. Editing the XML at runtime is therefore invisible twice over — the running app never
/// reads it, and nothing would re-read it if it did. Reading the files ourselves is the only way to
/// make the Translations screen mean anything.
///
/// Reading and writing deliberately use different mechanisms. Reads go through XDocument, which is
/// lenient and handles entities and the multi-line values correctly. Writes are surgical text
/// replacements, because these files are hand-maintained: 717 of the 721 entries sit on a single
/// line, they are grouped by feature with blank lines rather than sorted, and newer keys were
/// appended at the end. Round-tripping through XDocument.Save or ResXResourceWriter would reformat
/// all 801 lines of all four files on the first save and produce a diff nobody could review.
/// </summary>
public sealed partial class ResxCatalog
{
    /// <summary>The neutral file, SharedResource.resx, which is also the English copy.</summary>
    public const string NeutralSuffix = "";

    private const string BaseName = "SharedResource";

    private readonly string _root;
    private readonly ILogger<ResxCatalog> _logger;
    private readonly ConcurrentDictionary<string, CachedFile> _cache = new();

    /// <summary>
    /// How long a parsed file is trusted before its timestamp is checked again.
    ///
    /// Invalidating on save only fixes the process that did the writing. An IIS overlapped recycle
    /// briefly runs two, and any multi-instance deployment runs more — so without this, "I saved it
    /// and the page still shows the old text" happens intermittently, which is the one failure this
    /// whole feature exists to prevent. Four stat calls every five seconds, regardless of traffic.
    /// It also picks up a git pull or a hand-edit on the server.
    /// </summary>
    private const long StaleCheckMs = 5_000;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly Lazy<bool> _available;

    public ResxCatalog(IOptions<ResxOptions> options, IHostEnvironment environment, ILogger<ResxCatalog> logger)
    {
        var configured = options.Value.ResourcesPath;
        _root = Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "Resources")
            : configured);
        _logger = logger;

        // Computed once. This is consulted on every localizer creation, which is per request, and a
        // File.Exists syscall on that path would be a silly thing to pay for repeatedly.
        _available = new Lazy<bool>(() =>
        {
            var exists = File.Exists(PathFor(NeutralSuffix));
            if (!exists)
                logger.LogWarning(
                    "No {File} under {Root} — falling back to the compiled resources. Translations cannot be edited.",
                    $"{BaseName}.resx", _root);
            return exists;
        });
    }

    /// <summary>
    /// False when the .resx files are not on disk — which is the default for a published app, since
    /// they are build inputs rather than content. The factory falls back to the framework's
    /// resource-manager localizer in that case, so the site reads correctly either way.
    /// </summary>
    public bool IsAvailable => _available.Value;

    public string RootPath => _root;

    public bool TryGet(string suffix, string key, out string value) =>
        GetAll(suffix).TryGetValue(key, out value!);

    /// <summary>Every key/value in one file, cached — see StaleCheckMs for how it is refreshed.</summary>
    public IReadOnlyDictionary<string, string> GetAll(string suffix)
    {
        var cached = _cache.GetOrAdd(suffix, LoadCached);

        var now = Environment.TickCount64;
        if (now - Interlocked.Read(ref cached.CheckedAt) <= StaleCheckMs)
            return cached.Values;

        // Losing this race just means two threads stat the same file, which is harmless.
        Interlocked.Exchange(ref cached.CheckedAt, now);

        if (StampOf(suffix) == cached.Stamp)
            return cached.Values;

        var reloaded = LoadCached(suffix);
        _cache[suffix] = reloaded;
        return reloaded.Values;
    }

    private sealed class CachedFile
    {
        public required Dictionary<string, string> Values { get; init; }

        /// <summary>The file's last-write time when it was parsed.</summary>
        public required long Stamp { get; init; }

        public long CheckedAt;
    }

    private CachedFile LoadCached(string suffix) => new()
    {
        Values = Load(suffix),
        Stamp = StampOf(suffix),
        CheckedAt = Environment.TickCount64,
    };

    private long StampOf(string suffix)
    {
        var path = PathFor(suffix);
        return File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0;
    }

    private Dictionary<string, string> Load(string suffix)
    {
        var path = PathFor(suffix);
        if (!File.Exists(path)) return [];

        try
        {
            // PreserveWhitespace matters: the four Legal.* bodies are multi-line and carry
            // xml:space="preserve", and without it their blank lines would be collapsed on read.
            var document = XDocument.Parse(File.ReadAllText(path, Encoding.UTF8), LoadOptions.PreserveWhitespace);

            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var element in document.Root?.Elements("data") ?? [])
            {
                if (element.Attribute("name")?.Value is not { Length: > 0 } name) continue;

                // Normalised to LF explicitly rather than trusting the parser to have done it.
                // Views/Legal/Show.cshtml splits its body on a blank line to build paragraphs, and
                // the compiled resources it used to read always delivered LF. XML line-end
                // normalisation means a CRLF file arrives here as LF anyway - but if that ever
                // stopped being true, the four legal pages would silently collapse into one
                // unformatted block in all four languages, on public pages, with nothing logged.
                var value = element.Element("value")?.Value ?? "";
                result[name] = value.Replace("\r\n", "\n", StringComparison.Ordinal)
                                    .Replace("\r", "\n", StringComparison.Ordinal);
            }

            return result;
        }
        catch (Exception ex)
        {
            // A malformed file must not take the whole site down to raw key names. Log it loudly and
            // behave as though this culture has no translations, which falls back to English.
            _logger.LogError(ex, "Could not parse {Path}", path);
            return [];
        }
    }

    private string PathFor(string suffix) =>
        Path.Combine(_root, suffix.Length == 0 ? $"{BaseName}.resx" : $"{BaseName}.{suffix}.resx");

    // --- Writing --------------------------------------------------------------------------------

    /// <summary>
    /// Replaces one key's value in one file, leaving every other byte of it alone.
    /// </summary>
    public async Task<bool> SaveAsync(string suffix, string key, string value, CancellationToken ct = default)
    {
        var path = PathFor(suffix);
        if (!IsContained(path) || !File.Exists(path)) return false;

        // Serialised because two admins saving different keys in the same file would otherwise
        // read-modify-write over each other, and the loser's edit would vanish with no error.
        await _writeLock.WaitAsync(ct);
        try
        {
            var raw = await File.ReadAllTextAsync(path, Encoding.UTF8, ct);
            var match = EntryRegex(key).Match(raw);
            if (!match.Success) return false;

            var replacement = Escape(NormaliseNewlines(value, DetectNewline(raw)));
            var updated = string.Concat(
                raw.AsSpan(0, match.Groups[2].Index),
                replacement,
                raw.AsSpan(match.Groups[2].Index + match.Groups[2].Length));

            await WriteAtomicAsync(path, updated, ct);
            _cache.TryRemove(suffix, out _); // the whole point — the next lookup re-reads this file
            return true;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Adds a key to every file at once, appended before &lt;/root&gt; — which is where the existing
    /// files have grown. All four must stay in step: a key present in one and absent from another is
    /// how a language silently starts rendering English.
    /// </summary>
    public async Task<bool> AddKeyAsync(string key, IReadOnlyDictionary<string, string> valueBySuffix, CancellationToken ct = default)
    {
        await _writeLock.WaitAsync(ct);
        try
        {
            var paths = valueBySuffix.Keys.Select(s => (Suffix: s, Path: PathFor(s))).ToList();
            if (paths.Any(p => !IsContained(p.Path) || !File.Exists(p.Path))) return false;

            // Read every file and build every replacement before writing any of them, so a key that
            // already exists somewhere leaves all four untouched rather than half-updated.
            var pending = new List<(string Suffix, string Path, string Content)>();
            foreach (var (suffix, path) in paths)
            {
                var raw = await File.ReadAllTextAsync(path, Encoding.UTF8, ct);
                if (EntryRegex(key).IsMatch(raw)) return false;

                var closing = raw.LastIndexOf("</root>", StringComparison.Ordinal);
                if (closing < 0) return false;

                var newline = DetectNewline(raw);
                var value = Escape(NormaliseNewlines(valueBySuffix[suffix], newline));
                var entry = $"  <data name=\"{Escape(key)}\" xml:space=\"preserve\"><value>{value}</value></data>{newline}";

                pending.Add((suffix, path, raw.Insert(closing, entry)));
            }

            foreach (var (suffix, path, content) in pending)
            {
                await WriteAtomicAsync(path, content, ct);
                _cache.TryRemove(suffix, out _);
            }

            return true;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>The raw bytes of one file, for the "download all four" export.</summary>
    public Task<byte[]> ReadRawAsync(string suffix, CancellationToken ct = default)
    {
        var path = PathFor(suffix);
        return IsContained(path) && File.Exists(path)
            ? File.ReadAllBytesAsync(path, ct)
            : Task.FromResult(Array.Empty<byte>());
    }

    public string FileNameFor(string suffix) =>
        suffix.Length == 0 ? $"{BaseName}.resx" : $"{BaseName}.{suffix}.resx";

    /// <summary>
    /// Which file a site culture reads. The site's cultures are four-letter (de-DE) while the resx
    /// suffixes are two (de), and English has no suffix at all because the neutral file *is* the
    /// English copy. ResxStringLocalizer walks the same mapping, so it lives here rather than being
    /// spelled out twice and drifting.
    /// </summary>
    public static string SuffixFor(string cultureName)
    {
        var language = new System.Globalization.CultureInfo(cultureName).TwoLetterISOLanguageName;
        return string.Equals(language, "en", StringComparison.OrdinalIgnoreCase) ? NeutralSuffix : language;
    }

    /// <summary>
    /// The {0}, {1} … slots a value uses. A translation that drops or renumbers one throws
    /// FormatException when the page renders, on the public site, so saves are checked against this.
    /// </summary>
    public static SortedSet<int> PlaceholderSlots(string value)
    {
        var slots = new SortedSet<int>();
        foreach (Match m in PlaceholderRegex().Matches(value))
            if (int.TryParse(m.Groups[1].Value, out var index)) slots.Add(index);
        return slots;
    }

    private static async Task WriteAtomicAsync(string path, string content, CancellationToken ct)
    {
        // Temp-then-replace: a process killed mid-write must not leave a half-written .resx behind,
        // because that file is read by every request on the site.
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), ct);
        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>Same posture as LocalMediaStorage: a resolved path that escapes the root is refused.</summary>
    private bool IsContained(string candidate) =>
        Path.GetFullPath(candidate).StartsWith(_root, StringComparison.OrdinalIgnoreCase);

    private static string DetectNewline(string raw) =>
        raw.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    private static string NormaliseNewlines(string value, string newline) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal)
             .Replace("\r", "\n", StringComparison.Ordinal)
             .Replace("\n", newline, StringComparison.Ordinal);

    // Element content only, so quotes are left alone — but the key goes into an attribute, where
    // this is still correct because no key contains a quote.
    private static string Escape(string value) =>
        value.Replace("&", "&amp;", StringComparison.Ordinal)
             .Replace("<", "&lt;", StringComparison.Ordinal)
             .Replace(">", "&gt;", StringComparison.Ordinal);

    // Singleline so the four multi-line Legal.* bodies are matched whole rather than to end-of-line.
    private static Regex EntryRegex(string key) => new(
        @"(<data\s+name=""" + Regex.Escape(key) + @"""[^>]*>\s*<value>)(.*?)(</value>)",
        RegexOptions.Singleline | RegexOptions.CultureInvariant);

    [GeneratedRegex(@"\{(\d+)(?:[,:][^}]*)?\}")]
    private static partial Regex PlaceholderRegex();
}
