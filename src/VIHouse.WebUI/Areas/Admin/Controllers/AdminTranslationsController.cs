using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Audit;
using VIHouse.WebUI.Areas.Admin.ViewModels;
using VIHouse.WebUI.Localization;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

/// <summary>
/// Every string on the site, editable in four languages.
///
/// Narrower than AdminControllerBase on purpose — this screen can rewrite the terms of service, the
/// navigation and every validation message, so Finance and Support have no business in it.
/// Marketing owns site copy; SuperAdmin owns everything. (There is no ContentEditor role. The
/// narrowing follows AdminUsersController, the only other per-action restriction in the area.)
///
/// This controller's own messages are hard-coded English rather than localized. It edits the files
/// the translations live in: if someone mangles a key, the message telling them so must not itself
/// be mangled.
/// </summary>
[Authorize(Roles = $"{Roles.SuperAdmin},{Roles.Marketing}")]
public class AdminTranslationsController(
    ResxCatalog catalog,
    IAuditLogRepository auditLogs,
    UserManager<ApplicationUser> userManager) : AdminControllerBase
{
    public IActionResult Index(string? section, string? q, bool untranslated)
    {
        var english = catalog.GetAll(ResxCatalog.NeutralSuffix);
        var byCulture = SiteCultures.All.ToDictionary(
            c => c.Name,
            c => catalog.GetAll(ResxCatalog.SuffixFor(c.Name)));

        var rows = new List<TranslationRowViewModel>(english.Count);
        var needsAttention = 0;

        foreach (var (key, source) in english)
        {
            var row = new TranslationRowViewModel
            {
                Key = key,
                Section = SectionOf(key),
                HasPlaceholders = ResxCatalog.PlaceholderSlots(source).Count > 0,
            };

            foreach (var culture in SiteCultures.All)
            {
                var value = byCulture[culture.Name].GetValueOrDefault(key, "");
                row.Values[culture.Name] = value;

                // Identical to the English in a language that is not English: either untranslated,
                // or a word that genuinely is the same. The screen surfaces it; a human decides.
                if (!IsEnglish(culture.Name) && value.Length > 0 && value == source)
                    row.NeedsAttention = true;
            }

            if (row.NeedsAttention) needsAttention++;
            rows.Add(row);
        }

        var filtered = rows.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(section))
            filtered = filtered.Where(r => string.Equals(r.Section, section, StringComparison.OrdinalIgnoreCase));

        if (untranslated)
            filtered = filtered.Where(r => r.NeedsAttention);

        if (!string.IsNullOrWhiteSpace(q))
        {
            // Key and every language, because "where does this German sentence come from" is the
            // question people actually arrive with.
            var needle = q.Trim();
            filtered = filtered.Where(r =>
                Matches(r.Key, needle) || r.Values.Values.Any(v => Matches(v, needle)));
        }

        return View(new AdminTranslationsViewModel
        {
            Rows = [.. filtered],
            Cultures = SiteCultures.All,
            Sections = [.. rows.Select(r => r.Section).Distinct().OrderBy(s => s, StringComparer.OrdinalIgnoreCase)],
            Section = section,
            Query = q,
            UntranslatedOnly = untranslated,
            TotalKeys = rows.Count,
            NeedsAttention = needsAttention,
            IsEditable = catalog.IsAvailable,
            ResourcesPath = catalog.RootPath,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(string key, string culture, string value, string? original, CancellationToken ct)
    {
        if (!catalog.IsAvailable)
            return Json(new { ok = false, error = "The .resx files are not on disk, so nothing can be saved. See Localization:ResourcesPath." });

        if (string.IsNullOrWhiteSpace(key) || !SiteCultures.IsSupported(culture))
            return Json(new { ok = false, error = "Unknown key or language." });

        // A trailing line break is never meaningful in a UI string, and it is easy to add one by
        // accident. Trailing spaces are left alone — those are occasionally deliberate.
        value = value.TrimEnd('\r', '\n');

        var suffix = ResxCatalog.SuffixFor(culture);

        if (!catalog.GetAll(suffix).TryGetValue(key, out var onDisk))
            return Json(new { ok = false, error = $"That key is not in {catalog.FileNameFor(suffix)}." });

        // Someone else saved this cell since the page was loaded. Refusing beats silently discarding
        // their work — both values are shown so whoever is second can merge by hand.
        if (original is not null && onDisk != original)
            return Json(new
            {
                ok = false,
                // The value goes back with the error. Without it the browser keeps posting the
                // baseline it was rendered with, so the second attempt is refused for the same
                // reason as the first and the cell can never be edited again.
                current = onDisk,
                error = "Someone else changed this while you had it open — it now reads “" + onDisk
                        + "”. Your text was not saved; the cell has been updated, so edit it again to overwrite.",
            });

        if (onDisk == value)
            return Json(new { ok = true, unchanged = true });

        if (IsEnglish(culture) && string.IsNullOrWhiteSpace(value))
            return Json(new { ok = false, error = "The English text is the source for every other language and cannot be emptied." });

        if (Validate(key, culture, value) is { } problem)
            return Json(new { ok = false, error = problem });

        if (!await catalog.SaveAsync(suffix, key, value, ct))
            return Json(new { ok = false, error = "The file could not be written." });

        await auditLogs.AddAsync(new AuditLogEntry
        {
            AdminUserId = Guid.Parse(userManager.GetUserId(User)!),
            Action = "TranslationUpdated",
            EntityType = "Translation",
            // The key and the language, not the text. Consistent with the rest of the audit log,
            // which never carries free-text content, and the before/after is recoverable from git.
            DataAfter = JsonSerializer.Serialize(new { Key = key, Culture = culture }),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
        }, ct);
        await auditLogs.SaveChangesAsync(ct);

        return Json(new { ok = true });
    }

    /// <summary>
    /// The four files as a zip.
    ///
    /// Strings edited here diverge from the copies committed to git — the unavoidable cost of
    /// editing them at runtime. This is how the change gets back: download, unzip over
    /// src/VIHouse.WebUI/Resources/, read the diff, commit. Because the writer only ever replaces
    /// the one value it was asked to, that diff is the edits and nothing else.
    /// </summary>
    [Authorize(Roles = Roles.SuperAdmin)]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var buffer = new MemoryStream();

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var suffix in SiteCultures.All.Select(c => ResxCatalog.SuffixFor(c.Name)).Distinct())
            {
                var bytes = await catalog.ReadRawAsync(suffix, ct);
                if (bytes.Length == 0) continue;

                var entry = archive.CreateEntry(catalog.FileNameFor(suffix), CompressionLevel.Optimal);
                await using var stream = entry.Open();
                await stream.WriteAsync(bytes, ct);
            }
        }

        buffer.Position = 0;
        return File(buffer, "application/zip", $"SharedResource-{DateTime.UtcNow:yyyy-MM-dd}.zip");
    }

    /// <summary>
    /// A translation that drops or renumbers a {0} throws FormatException when the page renders — on
    /// the public site, inside IStringLocalizer's indexer, where nothing catches it. So a candidate
    /// is checked against the English holes and then actually formatted before it is allowed to land.
    /// </summary>
    private string? Validate(string key, string culture, string value)
    {
        if (value.Length > 8000)
            return "That is longer than 8000 characters — check for a paste accident.";

        var english = catalog.GetAll(ResxCatalog.NeutralSuffix).GetValueOrDefault(key, "");
        var expected = ResxCatalog.PlaceholderSlots(IsEnglish(culture) ? value : english);
        var actual = ResxCatalog.PlaceholderSlots(value);

        if (!IsEnglish(culture) && !expected.SetEquals(actual))
            return expected.Count == 0
                ? "The English text has no placeholders, so this translation must not add any."
                : "This text must use exactly the same placeholders as the English: "
                    + string.Join(" ", expected.Select(i => "{" + i + "}"));

        // The definitive check — catches "{0", "{a}" and a stray "}" that slot-matching misses.
        try
        {
            var arity = actual.Count == 0 ? 0 : actual.Max() + 1;
            _ = string.Format(
                CultureInfo.InvariantCulture,
                value,
                Enumerable.Range(0, arity).Cast<object>().ToArray());
        }
        catch (FormatException)
        {
            return "The placeholders are malformed — check for an unclosed { or a stray }.";
        }

        return null;
    }

    /// <summary>
    /// Case- and diacritic-insensitive substring match, so a search for "sehir" finds "şehir" and
    /// "uber" finds "über". This screen is searched in four languages by people who are not
    /// necessarily typing on that language's keyboard.
    /// </summary>
    private static bool Matches(string haystack, string needle) =>
        haystack.Length > 0 &&
        CultureInfo.InvariantCulture.CompareInfo.IndexOf(
            FoldDotlessI(haystack), FoldDotlessI(needle),
            CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;

    /// <summary>
    /// IgnoreNonSpace folds ş, ğ, ç, ö and ü because each decomposes to a base letter plus a
    /// combining mark. Turkish ı (U+0131) and İ (U+0130) are separate letters rather than accented
    /// ones, so they do not fold — meaning "Hakkimizda" would not find "Hakkımızda" without this.
    /// On a Turkish team that is most of the searches.
    /// </summary>
    private static string FoldDotlessI(string value) =>
        value.Contains('ı') || value.Contains('İ')
            ? value.Replace('ı', 'i').Replace('İ', 'I')
            : value;

    private static bool IsEnglish(string culture) =>
        culture.StartsWith("en", StringComparison.OrdinalIgnoreCase);

    private static string SectionOf(string key)
    {
        var dot = key.IndexOf('.');
        return dot > 0 ? key[..dot] : "Other";
    }
}
