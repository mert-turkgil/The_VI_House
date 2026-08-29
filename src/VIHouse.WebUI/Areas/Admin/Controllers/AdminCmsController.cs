using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Audit;
using VIHouse.Entities.Content;
using VIHouse.Entities.Seminars;
using VIHouse.WebUI.Areas.Admin.ViewModels;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

/// <summary>
/// Admin CRUD over ContentPage/ContentBlock — the homepage's own words (brief §13, §15, §21, §57).
///
/// This screen used to expose the storage: a "Section Key" text box and a textarea of raw JSON. It
/// was editable only by someone who already knew the shape of every section, and a stray comma
/// silently blanked a section on the live homepage, because HomeController.ParseJsonList catches the
/// JsonException and returns an empty list. Now each known section renders as a form — labelled
/// fields and repeatable rows — and the JSON is assembled here.
///
/// The storage did not change. Everything still goes into ContentBlock.ExtraJson in exactly the
/// shape Views/Home reads, so nothing on the public side knows this screen was rewritten.
///
/// English-only: CMS content stays outside the four-language scope (see Program.cs).
/// </summary>
public class AdminCmsController(
    IContentService contentService,
    IRepository<MediaAsset> assets,
    IMediaStorage mediaStorage,
    IAuditLogRepository auditLogs,
    UserManager<ApplicationUser> userManager,
    IStringLocalizer<SharedResource> loc) : AdminControllerBase
{
    /// <summary>Matches HomeController.ParseJsonList, which is what reads these blobs back.</summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        // Written to be read: an admin opening the raw panel should see something legible, and the
        // camelCase matches what the site's view models bind.
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var pages = await contentService.GetAllPagesAsync(ct);
        return View(pages);
    }

    public async Task<IActionResult> Edit(string id, CancellationToken ct)
    {
        var page = await contentService.GetPageWithBlocksAsync(id, ct);
        if (page is null) return NotFound();

        return View(await BuildPageModelAsync(page, ct));
    }

    /// <summary>
    /// Saves one section. The rows arrive as <c>Rows[0][title]</c> and are read straight off the
    /// form rather than through a bound model, because the fields differ per section and the schema
    /// — not the request — decides which ones are kept.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSection(AdminContentBlockFormModel form, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return RedirectToAction(nameof(Edit), new { id = form.PageSlug });

        var schema = ContentSectionSchema.For(form.SectionKey);
        string? extraJson;

        if (schema?.Rows is not null)
        {
            extraJson = BuildRowsJson(schema.Rows);
        }
        else
        {
            // No schema: the raw editor is the only way in, so what was typed is what gets stored —
            // after checking it parses. Saving invalid JSON here would blank the section on the
            // homepage without any error reaching anyone.
            extraJson = string.IsNullOrWhiteSpace(form.ExtraJson) ? null : form.ExtraJson.Trim();
            if (extraJson is not null && !IsValidJson(extraJson))
            {
                TempData["StatusMessage"] = "That JSON could not be parsed, so nothing was saved. Check for a stray comma or a missing quote.";
                return RedirectToAction(nameof(Edit), new { id = form.PageSlug });
            }
        }

        var (adminId, ip) = CurrentActor();
        await contentService.UpdateBlockAsync(new ContentBlock
        {
            Id = form.Id,
            SectionKey = form.SectionKey,
            SortOrder = form.SortOrder,
            Heading = Trimmed(form.Heading),
            Subheading = Trimmed(form.Subheading),
            BodyText = Trimmed(form.BodyText),
            ImageUrl = Trimmed(form.ImageUrl),
            CtaLabel = Trimmed(form.CtaLabel),
            CtaUrl = Trimmed(form.CtaUrl),
            ExtraJson = extraJson,
        }, adminId, ip, ct);

        TempData["StatusMessage"] = $"{schema?.Title ?? form.SectionKey} saved.";
        return RedirectToAction(nameof(Edit), new { id = form.PageSlug });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddBlock(AdminContentBlockFormModel form, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var (adminId, ip) = CurrentActor();
            await contentService.AddBlockAsync(form.PageId, new ContentBlock
            {
                SectionKey = form.SectionKey,
                SortOrder = form.SortOrder,
                Heading = Trimmed(form.Heading),
            }, adminId, ip, ct);
            TempData["StatusMessage"] = $"\"{form.SectionKey}\" added.";
        }

        return RedirectToAction(nameof(Edit), new { id = form.PageSlug });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveBlock(Guid pageId, Guid blockId, string pageSlug, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        await contentService.RemoveBlockAsync(pageId, blockId, adminId, ip, ct);
        TempData["StatusMessage"] = "Section removed.";
        return RedirectToAction(nameof(Edit), new { id = pageSlug });
    }

    // --- The shared image library --------------------------------------------------------------------

    /// <summary>
    /// Uploads an image for use in any section. Stored through IMediaStorage and served by
    /// MediaController, never written into wwwroot — see MediaAsset for why that distinction is not
    /// cosmetic.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MediaPolicy.MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MediaPolicy.MaxUploadBytes)]
    public async Task<IActionResult> UploadAsset(string pageSlug, IFormFile? file, string? title, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            TempData["StatusMessage"] = loc["Seminar.Error.MediaEmpty"].Value;
            return RedirectToAction(nameof(Edit), new { id = pageSlug });
        }

        if (MediaPolicy.Classify(file.FileName) is not (SeminarMediaKind.Image or SeminarMediaKind.Animation))
        {
            TempData["StatusMessage"] = "Content images take a photograph or a GIF — JPEG, PNG, WebP, AVIF or GIF.";
            return RedirectToAction(nameof(Edit), new { id = pageSlug });
        }

        await using var stream = file.OpenReadStream();
        var saved = await mediaStorage.SaveAsync(
            new MediaUpload(file.FileName, file.ContentType, file.Length, stream), "content", ct);

        if (!saved.Success)
        {
            TempData["StatusMessage"] = loc[saved.Error ?? "Seminar.Error.MediaFailed"].Value;
            return RedirectToAction(nameof(Edit), new { id = pageSlug });
        }

        var asset = new MediaAsset
        {
            StorageKey = saved.StorageKey!,
            Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
            ContentType = saved.ContentType!,
            SizeBytes = saved.SizeBytes,
            OriginalFileName = file.FileName,
        };
        await assets.AddAsync(asset, ct);

        var (adminId, ip) = CurrentActor();
        await LogAsync("MediaAssetUploaded", asset.Id, adminId, ip, before: null,
            after: new { asset.StorageKey, file.FileName, saved.SizeBytes }, ct);

        try
        {
            await assets.SaveChangesAsync(ct);
        }
        catch
        {
            // The bytes are on disk already; a failed save would leave a file nothing references.
            await mediaStorage.DeleteAsync(saved.StorageKey!, ct);
            throw;
        }

        TempData["StatusMessage"] = $"\"{file.FileName}\" uploaded. Copy its address into a section's image field.";
        return RedirectToAction(nameof(Edit), new { id = pageSlug });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAsset(Guid id, string pageSlug, CancellationToken ct)
    {
        var asset = await assets.GetByIdAsync(id, ct);
        if (asset is null)
        {
            TempData["StatusMessage"] = "That image no longer exists.";
            return RedirectToAction(nameof(Edit), new { id = pageSlug });
        }

        var storageKey = asset.StorageKey;
        var (adminId, ip) = CurrentActor();

        assets.Remove(asset);
        await LogAsync("MediaAssetDeleted", asset.Id, adminId, ip,
            before: new { asset.StorageKey, asset.OriginalFileName }, after: null, ct);
        await assets.SaveChangesAsync(ct);

        // Database first, file second — the same ordering every other delete on the site uses.
        await mediaStorage.DeleteAsync(storageKey, ct);

        // No automatic check of whether a section still points at it: a URL inside a JSON blob is not
        // something a query can find, so this one is on the admin. Hence the warning on the button.
        TempData["StatusMessage"] = "Image deleted. Any section still pointing at it will show a broken image.";
        return RedirectToAction(nameof(Edit), new { id = pageSlug });
    }

    // --- Helpers ---------------------------------------------------------------------------------------

    private async Task<AdminContentPageViewModel> BuildPageModelAsync(ContentPage page, CancellationToken ct)
    {
        var sections = page.Blocks
            .OrderBy(b => b.SortOrder)
            .Select(BuildSection)
            .ToList();

        return new AdminContentPageViewModel
        {
            PageId = page.Id,
            Slug = page.Slug,
            Title = page.Title,
            Sections = sections,
            Assets = (await assets.GetAllAsync(ct)).OrderByDescending(a => a.CreatedAt).ToList(),
        };
    }

    private static AdminContentSectionViewModel BuildSection(ContentBlock block)
    {
        var schema = ContentSectionSchema.For(block.SectionKey);
        var model = new AdminContentSectionViewModel
        {
            Id = block.Id,
            SectionKey = block.SectionKey,
            SortOrder = block.SortOrder,
            Heading = block.Heading,
            Subheading = block.Subheading,
            BodyText = block.BodyText,
            CtaLabel = block.CtaLabel,
            CtaUrl = block.CtaUrl,
            ImageUrl = block.ImageUrl,
            ExtraJson = block.ExtraJson,
            Schema = schema,
        };

        if (schema?.Rows is null || string.IsNullOrWhiteSpace(block.ExtraJson))
            return model;

        try
        {
            // Read as a generic array rather than into a typed list, so a row carrying a field this
            // panel does not know about is shown rather than quietly dropped on the next save.
            if (JsonNode.Parse(block.ExtraJson) is JsonArray array)
            {
                foreach (var item in array.OfType<JsonObject>())
                {
                    var row = new AdminContentRowViewModel();
                    foreach (var field in schema.Rows.Fields)
                        row.Values[field] = FindProperty(item, field);

                    model.Rows.Add(row);
                }
            }
        }
        catch (JsonException ex)
        {
            // Shown to the admin instead of an empty editor that would overwrite live content with
            // nothing the moment they pressed Save.
            model.ParseError = ex.Message;
        }

        return model;
    }

    /// <summary>Case-insensitive lookup, because the stored blobs were hand-written and the read side
    /// deserialises case-insensitively too.</summary>
    private static string? FindProperty(JsonObject item, string field) =>
        item.FirstOrDefault(p => string.Equals(p.Key, field, StringComparison.OrdinalIgnoreCase))
            .Value?.ToString();

    /// <summary>
    /// Turns the posted <c>Rows[i][field]</c> values back into the array the homepage reads.
    ///
    /// A row whose first field is empty is dropped. That is how a row is deleted — clear its first
    /// field — and it is also what makes the always-present blank row at the bottom of the form work
    /// as "add one" without any JavaScript.
    /// </summary>
    private string? BuildRowsJson(ContentRowSchema schema)
    {
        var rows = new JsonArray();

        for (var index = 0; ; index++)
        {
            var values = schema.Fields
                .ToDictionary(field => field, field => Request.Form[$"Rows[{index}][{field}]"].ToString());

            // Nothing posted at this index at all: the end of the form.
            if (values.Values.All(string.IsNullOrEmpty) && !Request.Form.ContainsKey($"Rows[{index}][{schema.Fields[0]}]"))
                break;

            if (string.IsNullOrWhiteSpace(values[schema.Fields[0]]))
                continue;

            var row = new JsonObject();
            foreach (var (field, value) in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    row[field] = value.Trim();
            }

            rows.Add(row);
        }

        return rows.ToJsonString(JsonOptions);
    }

    private static bool IsValidJson(string candidate)
    {
        try
        {
            JsonNode.Parse(candidate);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private (Guid AdminId, string? IpAddress) CurrentActor() =>
        (Guid.Parse(userManager.GetUserId(User)!), HttpContext.Connection.RemoteIpAddress?.ToString());

    private Task LogAsync(string action, Guid entityId, Guid adminUserId, string? ipAddress, object? before, object? after, CancellationToken ct) =>
        auditLogs.AddAsync(new AuditLogEntry
        {
            AdminUserId = adminUserId,
            Action = action,
            EntityType = nameof(MediaAsset),
            EntityId = entityId,
            DataBefore = before is null ? null : JsonSerializer.Serialize(before),
            DataAfter = after is null ? null : JsonSerializer.Serialize(after),
            IpAddress = ipAddress,
        }, ct);
}
