using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using VIHouse.Business;
using VIHouse.Business.Abstract;
using VIHouse.Business.Concrete;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Journal;
using VIHouse.Entities.Referrals;
using VIHouse.WebUI.Helpers;
using VIHouse.WebUI.ViewModels.Ambassador;

namespace VIHouse.WebUI.Controllers;

/// <summary>
/// The influencer's own area (/influencer) — the ambassador dashboard grown into a small account:
/// an overview with their links, their earnings and withdrawal requests, their journal articles and
/// the public profile they keep themselves. Security lives on the shared account pages.
///
/// Everything starts from the signed-in account's own influencer row and goes no further: posts
/// are theirs by author id, requests and ledger lines by influencer id, and anything else is a 404
/// — never a 403, which would confirm that it exists. Aggregates only, never who was referred
/// (brief §49).
/// </summary>
[Authorize(Roles = Roles.Ambassador)]
[Route("influencer")]
public class InfluencerController(
    IAmbassadorService ambassadorService,
    IJournalService journalService,
    IOptions<SiteOptions> siteOptions,
    IStringLocalizer<SharedResource> loc) : Controller
{
    private const long UploadLimit = 16 * 1024 * 1024;

    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    private Task<Ambassador?> CurrentAsync(CancellationToken ct) => ambassadorService.GetByUserIdAsync(User.RequiredUserId(), ct);

    private void Status(string message, bool isError = false)
    {
        TempData["StatusMessage"] = message;
        if (isError) TempData["StatusIsError"] = true;
    }

    // --- Overview --------------------------------------------------------------------------------

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var influencer = await CurrentAsync(ct);
        if (influencer is null) return NotFound();

        var stats = await ambassadorService.GetStatsAsync(influencer.Id, ct);

        // Site:BaseUrl is the host visitors use; the request host is right locally and right enough
        // anywhere the setting is missing. The same rule the admin page and the emails apply, so the
        // link an influencer copies here is byte-for-byte the one they were emailed.
        var baseUrl = string.IsNullOrWhiteSpace(siteOptions.Value.BaseUrl) ? $"{Request.Scheme}://{Request.Host}" : siteOptions.Value.BaseUrl;

        ViewData["Title"] = loc["Influencer.Nav.Overview"].Value;
        return View(new InfluencerOverviewViewModel
        {
            Influencer = influencer,
            Stats = stats,
            Missing = influencer.MissingRequirements(),
            Terms = influencer.TermsAcceptedAt is null ? InfluencerTerms.Lines(loc, influencer.CommissionPercent) : null,
            Recent = await ambassadorService.GetConversionsAsync(influencer.Id, 8, ct),
            Links = new ReferralLinksViewModel
            {
                Code = influencer.Code,
                BaseUrl = baseUrl,
                Targets = await ambassadorService.GetLinkTargetsAsync(ct),
                Stats = stats.Targets,
                Style = "site",
            },
        });
    }

    /// <summary>For an influencer made from an existing account, who never saw the invitation page.
    /// The text stored is rebuilt here, in the reader's language, exactly as the card showed it.</summary>
    [HttpPost("terms")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptTerms(bool accept, CancellationToken ct)
    {
        var influencer = await CurrentAsync(ct);
        if (influencer is null) return NotFound();

        if (!accept)
        {
            Status(loc["AmbassadorInvite.Error.Terms"].Value, isError: true);
            return RedirectToAction(nameof(Index));
        }

        var terms = string.Join("\n", InfluencerTerms.Lines(loc, influencer.CommissionPercent));
        await ambassadorService.AcceptTermsAsync(influencer.Id, User.RequiredUserId(), terms, Ip, ct);
        Status(loc["Influencer.Msg.TermsAccepted"].Value);
        return RedirectToAction(nameof(Index));
    }

    // --- Earnings and withdrawals ------------------------------------------------------------------

    [HttpGet("earnings")]
    public async Task<IActionResult> Earnings(CancellationToken ct)
    {
        var influencer = await CurrentAsync(ct);
        if (influencer is null) return NotFound();

        var stats = await ambassadorService.GetStatsAsync(influencer.Id, ct);
        ViewData["Title"] = loc["Influencer.Nav.Earnings"].Value;
        return View(new InfluencerEarningsViewModel
        {
            Influencer = influencer,
            Balances = stats.Balances,
            Withdrawals = await ambassadorService.GetWithdrawalsAsync(influencer.Id, ct),
            Payouts = await ambassadorService.GetPayoutsAsync(influencer.Id, ct),
            Ledger = await ambassadorService.GetConversionsAsync(influencer.Id, 100, ct),
            Titles = stats.Targets.ToDictionary(t => (t.Kind, t.Id), t => t.Title),
            MinimumMinor = ambassadorService.MinimumWithdrawalMinor,
            ProfileComplete = influencer.MissingRequirements().Count == 0,
        });
    }

    [HttpPost("earnings/withdraw")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestWithdrawal(string currency, string? note, CancellationToken ct)
    {
        var influencer = await CurrentAsync(ct);
        if (influencer is null) return NotFound();

        var result = await ambassadorService.RequestWithdrawalAsync(influencer.Id, currency, note, User.RequiredUserId(), Ip, ct);
        Status(result.Success
            ? loc["Influencer.Msg.WithdrawalRequested", MoneyFormatter.Format(result.Request!.RequestedMinor, result.Request.Currency)].Value
            : loc[result.Error!, result.ErrorArgs].Value, isError: !result.Success);
        return RedirectToAction(nameof(Earnings));
    }

    [HttpPost("earnings/withdrawals/{requestId:guid}/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelWithdrawal(Guid requestId, CancellationToken ct)
    {
        var influencer = await CurrentAsync(ct);
        if (influencer is null) return NotFound();

        if (!await ambassadorService.CancelWithdrawalAsync(influencer.Id, requestId, User.RequiredUserId(), Ip, ct)) return NotFound();
        Status(loc["Influencer.Msg.WithdrawalCancelled"].Value);
        return RedirectToAction(nameof(Earnings));
    }

    // --- Journal -----------------------------------------------------------------------------------

    [HttpGet("journal")]
    public async Task<IActionResult> Journal(CancellationToken ct)
    {
        ViewData["Title"] = loc["Influencer.Nav.Journal"].Value;
        return View(new InfluencerJournalViewModel { Posts = await journalService.GetForAuthorAsync(User.RequiredUserId(), ct) });
    }

    [HttpGet("journal/new")]
    public IActionResult NewArticle()
    {
        ViewData["Title"] = loc["Influencer.Journal.New"].Value;
        return View(new InfluencerStartArticleViewModel());
    }

    [HttpPost("journal/new")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NewArticle(InfluencerStartArticleViewModel form, CancellationToken ct)
    {
        ViewData["Title"] = loc["Influencer.Journal.New"].Value;
        var influencer = await CurrentAsync(ct);
        if (influencer is null) return NotFound();
        if (!ModelState.IsValid) return View(form);

        var result = await journalService.StartForAuthorAsync(User.RequiredUserId(), influencer.Name, form.Category, form.Title, form.Excerpt, Ip, ct);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, loc[result.Error!].Value);
            return View(form);
        }
        return RedirectToAction(nameof(Write), new { id = result.PostId });
    }

    [HttpGet("journal/{id:guid}")]
    public async Task<IActionResult> Write(Guid id, CancellationToken ct)
    {
        var post = await journalService.GetOwnAsync(id, User.RequiredUserId(), ct);
        if (post is null) return NotFound();
        return View(BuildWriter(post, null));
    }

    /// <summary>Save, or save and hand to the editors (<paramref name="intent"/> = "submit").</summary>
    [HttpPost("journal/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Write(Guid id, InfluencerArticleForm form, string? intent, CancellationToken ct)
    {
        var userId = User.RequiredUserId();
        var post = await journalService.GetOwnAsync(id, userId, ct);
        if (post is null) return NotFound();

        var submit = intent == "submit";
        var result = ModelState.IsValid
            ? await journalService.SaveForAuthorAsync(id, userId, form.ToDraft(), submit, Ip, ct)
            : JournalSaveResult.Fail("Influencer.Journal.Error.Title");
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, loc[result.Error!].Value);
            return View(BuildWriter(await journalService.GetOwnAsync(id, userId, ct) ?? post, form));
        }

        Status(loc[submit ? "Influencer.Journal.Msg.Submitted" : "Influencer.Journal.Msg.Saved"].Value);
        return submit ? RedirectToAction(nameof(Journal)) : RedirectToAction(nameof(Write), new { id });
    }

    [HttpPost("journal/{id:guid}/withdraw")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WithdrawSubmission(Guid id, CancellationToken ct)
    {
        var result = await journalService.WithdrawSubmissionAsync(id, User.RequiredUserId(), Ip, ct);
        if (!result.Success) return NotFound();
        Status(loc["Influencer.Journal.Msg.Withdrawn"].Value);
        return RedirectToAction(nameof(Write), new { id });
    }

    /// <summary>The editor's image upload (CKEditor's SimpleUploadAdapter): <c>{ url }</c> or
    /// <c>{ error: { message } }</c>. Still images only, into a post the author may still change.</summary>
    [HttpPost("journal/{id:guid}/upload-inline")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(UploadLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimit)]
    public async Task<IActionResult> UploadInline(Guid id, IFormFile? upload, CancellationToken ct)
    {
        var userId = User.RequiredUserId();
        if (await journalService.GetEditableForAuthorAsync(id, userId, ct) is null) return NotFound();
        if (upload is null || upload.Length == 0 || !InfluencerValidation.IsPhoto(upload.FileName, upload.Length))
            return Json(new { error = new { message = loc["Influencer.Journal.Error.Image"].Value } });

        await using var stream = upload.OpenReadStream();
        var result = await journalService.AddMediaAsync(id, new MediaUpload(upload.FileName, upload.ContentType, upload.Length, stream),
            title: null, isInline: true, userId, Ip, ct);
        return result is { Success: true, Media: not null }
            ? Json(new { url = JournalService.MediaUrl(result.Media.Id) })
            : Json(new { error = new { message = loc[result.Error ?? "Influencer.Journal.Error.Image"].Value } });
    }

    [HttpPost("journal/{id:guid}/cover")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(UploadLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimit)]
    public async Task<IActionResult> UploadCover(Guid id, IFormFile? cover, CancellationToken ct)
    {
        var userId = User.RequiredUserId();
        if (await journalService.GetEditableForAuthorAsync(id, userId, ct) is null) return NotFound();
        if (cover is null || cover.Length == 0 || !InfluencerValidation.IsPhoto(cover.FileName, cover.Length))
        {
            Status(loc["Influencer.Journal.Error.Image"].Value, isError: true);
            return RedirectToAction(nameof(Write), null, new { id }, "cover");
        }

        await using var stream = cover.OpenReadStream();
        var result = await journalService.ReplaceCoverAsync(id, new MediaUpload(cover.FileName, cover.ContentType, cover.Length, stream), userId, Ip, ct);
        Status(result.Success ? loc["Influencer.Journal.Msg.CoverSaved"].Value : loc[result.Error!].Value, isError: !result.Success);
        return RedirectToAction(nameof(Write), null, new { id }, "cover");
    }

    [HttpPost("journal/{id:guid}/cover/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveCover(Guid id, CancellationToken ct)
    {
        var userId = User.RequiredUserId();
        if (await journalService.GetEditableForAuthorAsync(id, userId, ct) is null) return NotFound();
        await journalService.RemoveCoverAsync(id, userId, Ip, ct);
        Status(loc["Influencer.Journal.Msg.CoverRemoved"].Value);
        return RedirectToAction(nameof(Write), null, new { id }, "cover");
    }

    private InfluencerWriteViewModel BuildWriter(JournalPost post, InfluencerArticleForm? form)
    {
        var english = JournalContent.Find(post, SiteCultures.Default);
        ViewData["Title"] = english?.Title ?? loc["Influencer.Nav.Journal"].Value;
        return new InfluencerWriteViewModel
        {
            Post = post,
            Form = form ?? new InfluencerArticleForm
            {
                Title = english?.Title ?? "",
                Excerpt = english?.Excerpt,
                Body = english?.Body,
                Category = post.Category,
                CoverImageAlt = post.CoverImageAlt,
            },
            Editable = JournalService.IsEditableByAuthor(post.Status),
            CoverUrl = post.CoverMediaId is { } cover ? JournalService.MediaUrl(cover) : null,
            PreviewUrl = Url.Action("Details", "Journal", new { slug = post.Slug }) ?? SiteUrls.JournalPost(post.Slug),
            UpdatedAtMs = (post.UpdatedAt ?? post.CreatedAt).ToUnixTimeMilliseconds(),
        };
    }

    // --- Profile -----------------------------------------------------------------------------------

    [HttpGet("profile")]
    public async Task<IActionResult> Profile(CancellationToken ct)
    {
        var influencer = await CurrentAsync(ct);
        if (influencer is null) return NotFound();
        return View(BuildProfile(influencer, null));
    }

    [HttpPost("profile")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile([Bind(Prefix = "Form")] InfluencerProfileForm form, CancellationToken ct)
    {
        var influencer = await CurrentAsync(ct);
        if (influencer is null) return NotFound();

        var result = ModelState.IsValid
            ? await ambassadorService.UpdateProfileAsync(influencer.Id, form.ToInput(), User.RequiredUserId(), Ip, ct)
            : InfluencerSaveResult.Fail();
        if (!result.Success)
        {
            InfluencerFormErrors.Apply(ModelState, "Form", result.Errors, loc);
            return View(BuildProfile(influencer, form.Padded()));
        }

        Status(loc["Influencer.Msg.ProfileSaved"].Value);
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost("profile/photo")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(UploadLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimit)]
    public async Task<IActionResult> UploadPhoto(IFormFile? photo, CancellationToken ct)
    {
        var influencer = await CurrentAsync(ct);
        if (influencer is null) return NotFound();

        var result = InfluencerSaveResult.Fail("Influencer.Error.Photo");
        if (photo is { Length: > 0 })
        {
            await using var stream = photo.OpenReadStream();
            result = await ambassadorService.SetPhotoAsync(influencer.Id, new MediaUpload(photo.FileName, photo.ContentType, photo.Length, stream),
                User.RequiredUserId(), Ip, ct);
        }
        Status(result.Success ? loc["Influencer.Msg.PhotoSaved"].Value : loc[result.Errors[0]].Value, isError: !result.Success);
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost("profile/photo/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemovePhoto(CancellationToken ct)
    {
        var influencer = await CurrentAsync(ct);
        if (influencer is null) return NotFound();
        await ambassadorService.RemovePhotoAsync(influencer.Id, User.RequiredUserId(), Ip, ct);
        Status(loc["Influencer.Msg.PhotoRemoved"].Value);
        return RedirectToAction(nameof(Profile));
    }

    private InfluencerProfileViewModel BuildProfile(Ambassador influencer, InfluencerProfileForm? form)
    {
        ViewData["Title"] = loc["Influencer.Nav.Profile"].Value;
        return new InfluencerProfileViewModel
        {
            Influencer = influencer,
            Form = form ?? InfluencerProfileForm.From(influencer),
            PhotoUrl = influencer.PhotoStorageKey is { } key ? SiteUrls.InfluencerPhoto(influencer.Id, key) : null,
            Missing = influencer.MissingRequirements(),
        };
    }
}
