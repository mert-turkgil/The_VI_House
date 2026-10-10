using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using VIHouse.Business;
using VIHouse.Business.Abstract;
using VIHouse.Business.Concrete;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Referrals;
using VIHouse.WebUI.Areas.Admin.ViewModels;
using VIHouse.WebUI.Helpers;
using VIHouse.WebUI.ViewModels.Ambassador;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

/// <summary>
/// Influencers (code name: ambassadors). Marketing invites them and keeps their public profile;
/// Finance keeps the legal name, billing address and bank details they are paid against, and records
/// payouts. Everyone in the section sees the numbers.
/// </summary>
[Authorize(Roles = AdminSections.RolesFor.Ambassadors)]
[Route("admin/ambassadors")]
public class AdminAmbassadorsController(
    IAmbassadorService ambassadorService,
    IJournalService journalService,
    UserManager<ApplicationUser> userManager,
    IEmailService emailService,
    IUserDirectory directory,
    IOptions<SiteOptions> siteOptions,
    IOptions<SecurityOptions> security,
    IStringLocalizer<SharedResource> loc) : AdminControllerBase
{
    /// <summary>The public referral link. Site:BaseUrl when configured (the host visitors use),
    /// otherwise whatever this request came in on — right locally, right enough elsewhere.</summary>
    private string ReferralUrlFor(string code) => SiteUrls.Absolute(BaseUrl, SiteUrls.Referral(code));

    private string BaseUrl =>
        string.IsNullOrWhiteSpace(siteOptions.Value.BaseUrl) ? $"{Request.Scheme}://{Request.Host}" : siteOptions.Value.BaseUrl;

    private bool CanEditProfile => User.IsInRole(Roles.SuperAdmin) || User.IsInRole(Roles.Marketing);
    private bool CanSettle => User.IsInRole(Roles.SuperAdmin) || User.IsInRole(Roles.Finance);

    private async Task<AdminAmbassadorEditViewModel> BuildEditModelAsync(Ambassador ambassador, AdminAmbassadorEditViewModel? form, CancellationToken ct)
    {
        var user = ambassador.UserId is { } userId ? await userManager.FindByIdAsync(userId.ToString()) : null;
        var model = form ?? AdminAmbassadorEditViewModel.FromEntity(ambassador, user?.Email);
        model.Ambassador = ambassador;
        model.AccountName = user is null ? null : $"{user.FirstName} {user.LastName}".Trim();
        model.Id = ambassador.Id;
        model.Code = ambassador.Code;
        model.Email = user?.Email ?? ambassador.InviteEmail;
        model.UserId = ambassador.UserId;
        model.CreatedAt = ambassador.CreatedAt;
        model.ReferralUrl = ReferralUrlFor(ambassador.Code);
        model.QrSvg = QrSvg.Render(model.ReferralUrl);
        model.Stats = await ambassadorService.GetStatsAsync(ambassador.Id, ct);
        model.Conversions = await ambassadorService.GetConversionsAsync(ambassador.Id, 30, ct);
        model.VisitSources = await ambassadorService.GetVisitSourcesAsync(ambassador.Id, ct);
        model.Payouts = await ambassadorService.GetPayoutsAsync(ambassador.Id, ct);
        model.Withdrawals = await ambassadorService.GetWithdrawalsAsync(ambassador.Id, ct);
        model.Signals = await ambassadorService.GetFraudSignalsAsync(ambassador.Id, ct);
        model.Posts = ambassador.UserId is { } authorId ? await journalService.GetForAuthorAsync(authorId, ct) : [];
        model.Missing = ambassador.MissingRequirements();
        model.PhotoUrl = ambassador.PhotoStorageKey is { } key ? SiteUrls.InfluencerPhoto(ambassador.Id, key) : null;
        model.CanSettle = CanSettle;
        model.CanEditProfile = CanEditProfile;
        model.ProfileForm ??= InfluencerProfileForm.From(ambassador);
        model.PayoutForm ??= InfluencerPayoutForm.From(ambassador);
        model.Links = new ReferralLinksViewModel
        {
            Code = ambassador.Code,
            BaseUrl = BaseUrl,
            Targets = await ambassadorService.GetLinkTargetsAsync(ct),
            Stats = model.Stats.Targets,
            Style = "admin",
        };
        return model;
    }

    private string Message(string? key, object[]? args) =>
        string.IsNullOrWhiteSpace(key) ? loc["Admin.Msg.CheckFields"].Value : loc[key, args ?? []].Value;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var ambassadors = await ambassadorService.GetAllAsync(ct);
        return View(ambassadors.OrderBy(a => a.Name).ToList());
    }

    [HttpGet("new")]
    [Authorize(Roles = AdminSections.RolesFor.Marketing)]
    public IActionResult Create() => View(new AdminAmbassadorCreateViewModel());

    [HttpPost("new")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AdminSections.RolesFor.Marketing)]
    [RequestSizeLimit(16 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 16 * 1024 * 1024)]
    public async Task<IActionResult> Create(AdminAmbassadorCreateViewModel form, CancellationToken ct)
    {
        // Every problem at once, before anything is saved: the same rules the service applies.
        var profile = form.Profile.ToInput();
        var payout = form.Payout.ToInput();
        InfluencerFormErrors.Apply(ModelState, nameof(form.Profile), InfluencerValidation.Profile(profile), loc);
        InfluencerFormErrors.Apply(ModelState, nameof(form.Payout), InfluencerValidation.Payout(payout), loc);
        if (form.Photo is { Length: > 0 } picked && !InfluencerValidation.IsPhoto(picked.FileName, picked.Length))
            ModelState.AddModelError(nameof(form.Photo), loc["Influencer.Error.Photo"].Value);
        if (!ModelState.IsValid)
        {
            form.Profile.Padded();
            return View(form);
        }

        var (adminId, ip) = CurrentActor();
        await using var stream = form.Photo is { Length: > 0 } file ? file.OpenReadStream() : null;
        var photo = stream is null ? null : new MediaUpload(form.Photo!.FileName, form.Photo.ContentType, form.Photo.Length, stream);
        var result = await ambassadorService.InviteAsync(
            new AmbassadorInvite(form.Email.Trim(), form.Name.Trim(), form.Code.Trim().ToUpperInvariant(), form.CommissionPercent, form.Culture,
                profile, payout),
            photo, adminId, ip, ct);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, Message(result.Error, result.ErrorArgs));
            if (result.UserId is { } existingUserId) ViewData["ExistingUserId"] = existingUserId;
            form.Profile.Padded();
            return View(form);
        }

        // The link itself is never shown here — it only exists in the email.
        Status(result.Error is not null
            ? Message(result.Error, result.ErrorArgs)
            : loc["Admin.Ambassadors.Msg.InviteSent", form.Email.Trim(), result.Ambassador!.Code].Value, isError: result.Error is not null);
        return RedirectToAction(nameof(Edit), new { id = result.Ambassador!.Id });
    }

    /// <summary>
    /// The other door on the same page: someone who already has an account. Search by name or
    /// email, pick them, set the code and the rate.
    /// </summary>
    [HttpGet("new/member")]
    [Authorize(Roles = AdminSections.RolesFor.Marketing)]
    public async Task<IActionResult> CreateForMember(string? q, Guid? userId, CancellationToken ct)
    {
        var model = new AdminAmbassadorMemberViewModel { Query = q?.Trim() };
        if (userId is { } id && await userManager.FindByIdAsync(id.ToString()) is { } user)
        {
            await DescribeMemberAsync(model, user, ct);
            model.Name = string.Join(" ", new[] { user.FirstName, user.LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
            model.Code = SuggestCode(user);
        }
        else if (!string.IsNullOrWhiteSpace(model.Query))
        {
            model.Results = (await directory.SearchAsync(model.Query, null, 1, 8, ct)).Rows;
        }
        return View(model);
    }

    [HttpPost("new/member")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AdminSections.RolesFor.Marketing)]
    public async Task<IActionResult> CreateForMember(AdminAmbassadorMemberViewModel form, CancellationToken ct)
    {
        if (form.UserId is not { } id || await userManager.FindByIdAsync(id.ToString()) is not { Email: not null } user) return NotFound();
        await DescribeMemberAsync(form, user, ct);
        if (security.Value.IsProtected(user.Email))
            ModelState.AddModelError(string.Empty, loc["Admin.Users.Msg.Protected", user.Email].Value);
        if (!ModelState.IsValid || form.ExistingAmbassadorId is not null) return View(form);

        var (adminId, ip) = CurrentActor();
        var result = await ambassadorService.CreateForUserAsync(
            user.Id, form.Name.Trim(), form.Code.Trim().ToUpperInvariant(), form.CommissionPercent, adminId, ip, ct);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, Message(result.Error, result.ErrorArgs));
            return View(form);
        }

        Status(loc["Admin.Users.Msg.ReferralCreated", result.Ambassador!.Code].Value);
        return RedirectToAction(nameof(Edit), new { id = result.Ambassador.Id });
    }

    private async Task DescribeMemberAsync(AdminAmbassadorMemberViewModel model, ApplicationUser user, CancellationToken ct)
    {
        model.UserId = user.Id;
        model.MemberEmail = user.Email;
        model.MemberName = string.Join(" ", new[] { user.FirstName, user.LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
        model.ExistingAmbassadorId = (await ambassadorService.GetByUserIdAsync(user.Id, ct))?.Id;
    }

    /// <summary>A starting point for the code — first name, upper-cased, letters only — that the
    /// admin can overwrite. "VI-" prefixes are the convention from the brief (§47: VI-ANTON).</summary>
    private static string SuggestCode(ApplicationUser user)
    {
        var stem = new string((user.FirstName ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return stem.Length == 0 ? "" : $"VI-{stem}";
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var ambassador = await ambassadorService.GetByIdAsync(id, ct);
        if (ambassador is null) return NotFound();

        return View(await BuildEditModelAsync(ambassador, null, ct));
    }

    /// <summary>
    /// Emails the influencer their link. Admins were copying "/r/CODE" out of the panel and
    /// pasting it into a mail by hand; this sends the absolute URL, the code and a personal
    /// note, from the House, to the address on the account.
    /// </summary>
    [HttpPost("{id:guid}/send-link")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AdminSections.RolesFor.Marketing)]
    public async Task<IActionResult> SendLink(Guid id, string? note, CancellationToken ct)
    {
        var ambassador = await ambassadorService.GetByIdAsync(id, ct);
        if (ambassador is null) return NotFound();

        if (ambassador.Status == AmbassadorStatus.Pending)
        {
            Status(loc["Admin.Ambassadors.Msg.NotAcceptedYet"].Value, isError: true);
            return RedirectToAction(nameof(Edit), new { id });
        }

        var user = ambassador.UserId is { } userId ? await userManager.FindByIdAsync(userId.ToString()) : null;
        if (user?.Email is null)
        {
            Status(loc["Admin.Ambassadors.Msg.NoEmail"].Value, isError: true);
            return RedirectToAction(nameof(Edit), new { id });
        }

        var referralUrl = ReferralUrlFor(ambassador.Code);
        var sent = await emailService.SendAsync(
            "AmbassadorLink", user.Email, "Your VI House referral link",
            new AmbassadorLinkEmailModel(ambassador.Name, referralUrl, ambassador.Code, ambassador.CommissionPercent,
                SiteUrls.Absolute(BaseUrl, SiteUrls.InCulture(SiteUrls.Influencer, user.PreferredCulture)),
                string.IsNullOrWhiteSpace(note) ? null : note.Trim()),
            user.PreferredCulture ?? SiteCultures.Default,
            nameof(Ambassador), ambassador.Id, ct);

        Status(sent
            ? loc["Admin.Ambassadors.Msg.LinkSent", user.Email].Value
            : loc["Admin.Ambassadors.Msg.LinkNotSent", user.Email, referralUrl].Value, isError: !sent);
        return RedirectToAction(nameof(Edit), new { id });
    }

    /// <summary>New link and expiry for a pending invitation; the old link stops working. The
    /// address and language can be corrected here — the fix for a mistyped email.</summary>
    [HttpPost("{id:guid}/resend-invite")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AdminSections.RolesFor.Marketing)]
    public async Task<IActionResult> ResendInvite(Guid id, string? email, string? culture, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(email) && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email.Trim()))
        {
            Status(loc["Admin.Ambassadors.Msg.NotAnEmail", email].Value, isError: true);
            return RedirectToAction(nameof(Edit), new { id });
        }

        var (adminId, ip) = CurrentActor();
        var result = await ambassadorService.ResendInviteAsync(id, email, culture, adminId, ip, ct);
        Status(!result.Success || result.Error is not null
            ? Message(result.Error, result.ErrorArgs)
            : loc["Admin.Ambassadors.Msg.InviteResent", result.Ambassador!.InviteEmail ?? ""].Value, isError: !result.Success || result.Error is not null);
        return RedirectToAction(nameof(Edit), new { id });
    }

    /// <summary>Cancels a pending invitation and frees the code.</summary>
    [HttpPost("{id:guid}/withdraw-invite")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AdminSections.RolesFor.Marketing)]
    public async Task<IActionResult> WithdrawInvite(Guid id, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        if (!await ambassadorService.WithdrawInviteAsync(id, adminId, ip, ct))
        {
            Status(loc["Admin.Ambassadors.Msg.OnlyPendingWithdrawn"].Value, isError: true);
            return RedirectToAction(nameof(Edit), new { id });
        }
        Status(loc["Admin.Ambassadors.Msg.Withdrawn"].Value);
        return RedirectToAction(nameof(Index));
    }

    // --- Profile (Marketing) ---------------------------------------------------------------------

    [HttpPost("{id:guid}/profile")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AdminSections.RolesFor.Marketing)]
    public async Task<IActionResult> SaveProfile(Guid id, [Bind(Prefix = "ProfileForm")] InfluencerProfileForm profile, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var result = ModelState.IsValid
            ? await ambassadorService.UpdateProfileAsync(id, profile.ToInput(), adminId, ip, ct)
            : InfluencerSaveResult.Fail();
        if (result.Success)
        {
            Status(loc["Admin.Influencer.Msg.ProfileSaved"].Value);
            return RedirectToAction(nameof(Edit), null, new { id }, "profile");
        }

        var ambassador = await ambassadorService.GetByIdAsync(id, ct);
        if (ambassador is null) return NotFound();
        InfluencerFormErrors.Apply(ModelState, "ProfileForm", result.Errors, loc);
        var model = await BuildEditModelAsync(ambassador, null, ct);
        model.ProfileForm = profile.Padded();
        return View(nameof(Edit), model);
    }

    [HttpPost("{id:guid}/photo")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AdminSections.RolesFor.Marketing)]
    [RequestSizeLimit(16 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 16 * 1024 * 1024)]
    public async Task<IActionResult> UploadPhoto(Guid id, IFormFile? photo, CancellationToken ct)
    {
        var result = InfluencerSaveResult.Fail("Influencer.Error.Photo");
        if (photo is { Length: > 0 })
        {
            var (adminId, ip) = CurrentActor();
            await using var stream = photo.OpenReadStream();
            result = await ambassadorService.SetPhotoAsync(id, new MediaUpload(photo.FileName, photo.ContentType, photo.Length, stream), adminId, ip, ct);
        }
        Status(result.Success ? loc["Admin.Influencer.Msg.PhotoSaved"].Value : loc[result.Errors[0]].Value, isError: !result.Success);
        return RedirectToAction(nameof(Edit), null, new { id }, "profile");
    }

    [HttpPost("{id:guid}/photo/remove")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AdminSections.RolesFor.Marketing)]
    public async Task<IActionResult> RemovePhoto(Guid id, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var result = await ambassadorService.RemovePhotoAsync(id, adminId, ip, ct);
        Status(result.Success ? loc["Admin.Influencer.Msg.PhotoRemoved"].Value : loc[result.Errors[0]].Value, isError: !result.Success);
        return RedirectToAction(nameof(Edit), null, new { id }, "profile");
    }

    // --- Legal identity and bank (Finance) -------------------------------------------------------

    /// <summary>The only way bank details change after the invitation: Finance, audited (the IBAN
    /// masked in the log), with the change date shown to whoever pays next.</summary>
    [HttpPost("{id:guid}/payout-details")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AdminSections.RolesFor.Money)]
    public async Task<IActionResult> SavePayoutDetails(Guid id, [Bind(Prefix = "PayoutForm")] InfluencerPayoutForm payout, CancellationToken ct)
    {
        var (adminId, ip) = CurrentActor();
        var result = ModelState.IsValid
            ? await ambassadorService.UpdatePayoutIdentityAsync(id, payout.ToInput(), adminId, ip, ct)
            : InfluencerSaveResult.Fail();
        if (result.Success)
        {
            Status(loc["Admin.Influencer.Msg.PayoutDetailsSaved"].Value);
            return RedirectToAction(nameof(Edit), null, new { id }, "payout-details");
        }

        var ambassador = await ambassadorService.GetByIdAsync(id, ct);
        if (ambassador is null) return NotFound();
        InfluencerFormErrors.Apply(ModelState, "PayoutForm", result.Errors, loc);
        var model = await BuildEditModelAsync(ambassador, null, ct);
        model.PayoutForm = payout;
        model.EditPayoutDetails = true;
        return View(nameof(Edit), model);
    }

    // --- Money -----------------------------------------------------------------------------------

    /// <summary>
    /// Records that everything owed in one currency has been paid. The form carries the balance
    /// the admin saw; if it has moved since, nothing is recorded (see MarkCommissionPaidAsync).
    /// Recording only — the money itself is sent outside the site (bank transfer). An open
    /// withdrawal request in that currency is settled by the same payout.
    /// </summary>
    [HttpPost("{id:guid}/payouts")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AdminSections.RolesFor.Money)]
    public async Task<IActionResult> RecordPayout(Guid id, string currency, long expectedOwedMinor, string? reference, string? note, bool confirmed, CancellationToken ct)
    {
        if (!confirmed)
        {
            Status(loc["Admin.Ambassadors.Msg.ConfirmTransfer"].Value, isError: true);
            return RedirectToAction(nameof(Edit), new { id });
        }

        var (adminId, ip) = CurrentActor();
        var result = await ambassadorService.MarkCommissionPaidAsync(id, currency ?? "", expectedOwedMinor, reference, note, adminId, ip, ct);
        Status(result.Success
            ? (result.Payout!.Reference is null
                ? loc["Admin.Ambassadors.Msg.PayoutRecorded", MoneyFormatter.Format(result.Payout.AmountMinor, result.Payout.Currency)].Value
                : loc["Admin.Ambassadors.Msg.PayoutRecordedRef", MoneyFormatter.Format(result.Payout.AmountMinor, result.Payout.Currency), result.Payout.Reference].Value)
            : Message(result.Error, result.ErrorArgs), isError: !result.Success);
        return RedirectToAction(nameof(Edit), new { id });
    }

    /// <summary>Strikes one ledger line's commission out — a self-referral, or anything else the
    /// House will not pay for. The line stays on the timeline, marked, with the reason.</summary>
    [HttpPost("{id:guid}/conversions/{conversionId:guid}/void")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AdminSections.RolesFor.Money)]
    public async Task<IActionResult> VoidConversion(Guid id, Guid conversionId, string? reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            Status(loc["Admin.Ambassadors.Msg.ReasonNeeded"].Value, isError: true);
            return RedirectToAction(nameof(Edit), new { id });
        }

        var (adminId, ip) = CurrentActor();
        var done = await ambassadorService.VoidConversionAsync(id, conversionId, reason.Trim(), adminId, ip, ct);
        Status(loc[done ? "Admin.Ambassadors.Msg.CommissionWithdrawn" : "Admin.Ambassadors.Msg.NothingToWithdraw"].Value, isError: !done);
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost("{id:guid}")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = AdminSections.RolesFor.Marketing)]
    public async Task<IActionResult> Edit(Guid id, AdminAmbassadorEditViewModel form, CancellationToken ct)
    {
        form.Id = id;
        if (!ModelState.IsValid)
        {
            var ambassador = await ambassadorService.GetByIdAsync(id, ct);
            if (ambassador is null) return NotFound();
            return View(await BuildEditModelAsync(ambassador, form, ct));
        }

        var (adminId, ip) = CurrentActor();
        await ambassadorService.UpdateAsync(form.ToEntity(), adminId, ip, ct);

        Status(loc["Admin.Msg.ChangesSaved"].Value);
        return RedirectToAction(nameof(Edit), new { id });
    }
}
