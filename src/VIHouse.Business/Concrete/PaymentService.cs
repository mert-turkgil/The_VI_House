using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Applications;
using VIHouse.Entities.Commerce;
using VIHouse.Entities.Experiences;
using VIHouse.Entities.Notifications;
using VIHouse.Entities.Users;

namespace VIHouse.Business.Concrete;

public class PaymentService(
    IInvitationRepository invitations,
    IApplicationRepository applications,
    IApplicationService applicationService,
    IExperienceRepository experiences,
    ITicketTypeRepository ticketTypes,
    IPromoCodeRepository promoCodes,
    ITicketHoldRepository ticketHolds,
    IPaymentRepository payments,
    IBookingRepository bookings,
    IWebhookEventRepository webhookEvents,
    ICapacityService capacity,
    IPaymentProvider paymentProvider,
    IEmailService emailService,
    ISmsService smsService,
    INotificationService notificationService,
    IOptions<SiteOptions> siteOptions,
    UserManager<ApplicationUser> userManager) : IPaymentService
{
    public async Task<InvitationLandingInfo> GetInvitationLandingAsync(string invitationCode, CancellationToken ct = default)
    {
        var invitation = await invitations.GetByCodeAsync(invitationCode, ct);
        if (invitation is null)
            return InvitationLandingInfo.Invalid("This invitation link isn't valid.");
        if (invitation.IsUsed)
            return InvitationLandingInfo.Invalid("This invitation has already been used.");
        if (invitation.ExpiresAt < DateTimeOffset.UtcNow)
            return InvitationLandingInfo.Invalid("This invitation has expired — contact us for a new one.");

        var application = await applications.GetByIdAsync(invitation.ApplicationId, ct);
        if (application is null || application.Status is not (ApplicationStatus.Approved or ApplicationStatus.PaymentPending))
            return InvitationLandingInfo.Invalid("This application isn't eligible for payment right now.");

        var experience = await experiences.GetByIdAsync(application.ExperienceId, ct);
        if (experience is null)
            return InvitationLandingInfo.Invalid("This experience is no longer available.");

        var availableTicketTypes = await ticketTypes.GetByExperienceAsync(application.ExperienceId, ct);
        var now = DateTimeOffset.UtcNow;

        var options = availableTicketTypes
            .Where(t => t.SalesStartAt is null || t.SalesStartAt <= now)
            .Where(t => t.SalesEndAt is null || t.SalesEndAt >= now)
            .Select(t => new TicketTypeOption(t.Id, t.Title, t.Description, t.PriceMinor, t.Currency, t.PerksText, t.Inventory <= 0))
            .ToList();

        return new InvitationLandingInfo(
            true, null, application.FirstName, experience.Title, experience.City, experience.Country,
            experience.StartAtUtc, experience.EndAtUtc, options);
    }

    public async Task<CheckoutInitiationResult> InitiateCheckoutAsync(
        string invitationCode, Guid ticketTypeId, string? promoCode, string successUrl, string cancelUrl, CancellationToken ct = default)
    {
        var invitation = await invitations.GetByCodeAsync(invitationCode, ct);
        if (invitation is null)
            return CheckoutInitiationResult.Fail("This invitation link isn't valid.");
        if (invitation.IsUsed)
            return CheckoutInitiationResult.Fail("This invitation has already been used.");
        if (invitation.ExpiresAt < DateTimeOffset.UtcNow)
            return CheckoutInitiationResult.Fail("This invitation has expired — contact us for a new one.");

        var application = await applications.GetByIdAsync(invitation.ApplicationId, ct);
        if (application is null || application.Status is not (ApplicationStatus.Approved or ApplicationStatus.PaymentPending))
            return CheckoutInitiationResult.Fail("This application isn't eligible for payment right now.");

        var ticketType = await ticketTypes.GetByIdAsync(ticketTypeId, ct);
        if (ticketType is null || ticketType.ExperienceId != application.ExperienceId)
            return CheckoutInitiationResult.Fail("That ticket type isn't available for this experience.");

        var now = DateTimeOffset.UtcNow;
        if (ticketType.SalesStartAt is { } startsAt && now < startsAt)
            return CheckoutInitiationResult.Fail("Sales for this ticket type haven't opened yet.");
        if (ticketType.SalesEndAt is { } endsAt && now > endsAt)
            return CheckoutInitiationResult.Fail("Sales for this ticket type have closed.");

        var experience = await experiences.GetByIdAsync(application.ExperienceId, ct);
        if (experience is null)
            return CheckoutInitiationResult.Fail("This experience is no longer available.");

        // Amount, with an optional promo code applied — redeemed atomically (same no-oversell
        // pattern as ticket inventory) before we ever reserve capacity or talk to Stripe.
        var amountMinor = ticketType.PriceMinor;
        if (!string.IsNullOrWhiteSpace(promoCode))
        {
            var promoResult = await TryApplyPromoAsync(promoCode.Trim(), application.ExperienceId, amountMinor, ct);
            if (promoResult.Error is not null)
                return CheckoutInitiationResult.Fail(promoResult.Error);
            amountMinor = promoResult.AmountMinor;
        }

        // Retry path: a prior attempt on this application left an Active hold (different ticket
        // type, or they bounced back from Stripe) — release it before reserving a fresh one so we
        // never hold two seats for the same applicant.
        var existingHold = await ticketHolds.GetActiveByApplicationAsync(application.Id, ct);
        if (existingHold is not null)
            await capacity.ReleaseAsync(existingHold.Id, ct);

        var hold = await capacity.TryReserveAsync(ticketTypeId, 1, application.Id, invitation.Id, ct);
        if (hold is null)
            return CheckoutInitiationResult.Fail("Sorry — this ticket type just sold out.");

        try
        {
            var user = await ProvisionMemberAccountAsync(application, ct);

            var payment = new Payment
            {
                ApplicationId = application.Id,
                UserId = user.Id,
                ExperienceId = application.ExperienceId,
                TicketTypeId = ticketTypeId,
                AmountMinor = amountMinor,
                Currency = ticketType.Currency,
                Status = PaymentStatus.Created,
            };
            payment.ProviderReference = $"pending_{payment.Id:N}"; // placeholder, unique — replaced once Stripe returns a session id
            await payments.AddAsync(payment, ct);
            await payments.SaveChangesAsync(ct);

            var session = await paymentProvider.CreateCheckoutSessionAsync(new CreateCheckoutSessionRequest(
                CustomerEmail: application.Email,
                ProductName: $"The VI House — {experience.City} ({ticketType.Title})",
                ProductDescription: experience.ShortSummary,
                AmountMinor: amountMinor,
                Currency: ticketType.Currency,
                SuccessUrl: successUrl,
                CancelUrl: cancelUrl,
                ClientReferenceId: payment.Id.ToString(),
                Metadata: new Dictionary<string, string>
                {
                    ["paymentId"] = payment.Id.ToString(),
                    ["applicationId"] = application.Id.ToString(),
                    ["ticketHoldId"] = hold.Id.ToString(),
                }), ct);

            payment.ProviderReference = session.SessionId;
            await payments.SaveChangesAsync(ct);

            if (application.Status == ApplicationStatus.Approved)
                await applicationService.MarkPaymentPendingAsync(application.Id, ct);

            return CheckoutInitiationResult.Ok(session.Url);
        }
        catch (Exception)
        {
            // Stripe call (or anything else) failed after we'd already reserved the seat — give it
            // back rather than leaving a phantom hold nobody will ever complete.
            await capacity.ReleaseAsync(hold.Id, ct);
            return CheckoutInitiationResult.Fail("We couldn't reach the payment provider — please try again in a moment.");
        }
    }

    public async Task<BookingConfirmationInfo?> GetBookingConfirmationBySessionAsync(string sessionId, CancellationToken ct = default)
    {
        var payment = await payments.GetByProviderReferenceAsync(sessionId, ct);
        if (payment is null) return null;

        var experience = await experiences.GetByIdAsync(payment.ExperienceId, ct);

        if (payment.Status != PaymentStatus.Paid || payment.BookingId is null)
        {
            // Webhook hasn't landed yet — this is a normal race, not an error (brief §32: the
            // browser redirect is informational only, never authoritative on its own).
            return new BookingConfirmationInfo(false, null, experience?.Title, experience?.City, payment.AmountMinor, payment.Currency, null);
        }

        var booking = await bookings.GetByIdAsync(payment.BookingId.Value, ct);
        return new BookingConfirmationInfo(true, booking?.BookingReference, experience?.Title, experience?.City, payment.AmountMinor, payment.Currency, payment.UserId);
    }

    public async Task HandleWebhookEventAsync(PaymentWebhookEvent webhookEvent, CancellationToken ct = default)
    {
        if (await webhookEvents.HasBeenProcessedAsync(webhookEvent.EventId, ct))
            return; // duplicate delivery — Stripe retries are expected, this must be a safe no-op

        if (webhookEvent.Type == PaymentWebhookEventType.CheckoutCompleted && webhookEvent.SessionId is not null)
            await HandleCheckoutCompletedAsync(webhookEvent.SessionId, ct);
        else if (webhookEvent.Type == PaymentWebhookEventType.CheckoutExpired && webhookEvent.SessionId is not null)
            await HandleCheckoutExpiredAsync(webhookEvent.SessionId, ct);

        await webhookEvents.MarkProcessedAsync(webhookEvent.EventId, ct);
        await webhookEvents.SaveChangesAsync(ct);
    }

    private async Task HandleCheckoutCompletedAsync(string sessionId, CancellationToken ct)
    {
        var payment = await payments.GetByProviderReferenceAsync(sessionId, ct);
        if (payment is null || payment.Status == PaymentStatus.Paid)
            return; // unknown session, or already handled by an earlier delivery of this event

        payment.Status = PaymentStatus.Paid;
        payment.UpdatedAt = DateTimeOffset.UtcNow;

        var hold = await ticketHolds.GetActiveByApplicationAsync(payment.ApplicationId, ct);
        if (hold is not null)
            await capacity.CommitAsync(hold.Id, ct);

        var reference = await bookings.GenerateNextReferenceAsync(DateTimeOffset.UtcNow.Year % 100, ct);
        var booking = new Booking
        {
            BookingReference = reference,
            UserId = payment.UserId!.Value,
            ExperienceId = payment.ExperienceId,
            TicketTypeId = payment.TicketTypeId,
            ApplicationId = payment.ApplicationId,
            Quantity = hold?.Quantity ?? 1,
            AmountMinor = payment.AmountMinor,
            Currency = payment.Currency,
            Status = BookingStatus.Confirmed,
            ConfirmedAt = DateTimeOffset.UtcNow,
        };
        await bookings.AddAsync(booking, ct);
        await bookings.SaveChangesAsync(ct);

        payment.BookingId = booking.Id;

        var invitation = hold?.InvitationId is { } invitationId ? await invitations.GetByIdAsync(invitationId, ct) : null;
        if (invitation is not null)
        {
            invitation.IsUsed = true;
            invitation.UsedAt = DateTimeOffset.UtcNow;
        }

        await applicationService.MarkPaidAsync(payment.ApplicationId, ct);
        await payments.SaveChangesAsync(ct);

        var confirmedApplication = await applications.GetByIdAsync(payment.ApplicationId, ct);
        var confirmedExperience = await experiences.GetByIdAsync(payment.ExperienceId, ct);

        // The money has landed — this is the moment the account becomes one its owner can use.
        if (await userManager.FindByIdAsync(payment.UserId!.Value.ToString()) is { } member)
            await OpenAccountAsync(member, booking, confirmedApplication, confirmedExperience, ct);

        if (confirmedApplication is not null && confirmedExperience is not null)
        {
            await emailService.SendAsync(
                "BookingConfirmed", confirmedApplication.Email, $"You're confirmed — booking {booking.BookingReference}",
                new BookingConfirmedEmailModel(
                    confirmedApplication.FirstName, booking.BookingReference, confirmedExperience.Title, confirmedExperience.City,
                    confirmedExperience.StartAtUtc, confirmedExperience.EndAtUtc, booking.AmountMinor, booking.Currency),
                nameof(Booking), booking.Id, ct);

            await notificationService.CreateForUserAsync(
                payment.UserId!.Value, NotificationType.Payment,
                "Booking Confirmed", $"You're confirmed for The VI House — {confirmedExperience.City}. Reference {booking.BookingReference}.",
                "/account/bookings", ct);
        }
    }

    private async Task HandleCheckoutExpiredAsync(string sessionId, CancellationToken ct)
    {
        var payment = await payments.GetByProviderReferenceAsync(sessionId, ct);
        if (payment is null || payment.Status == PaymentStatus.Paid)
            return;

        payment.Status = PaymentStatus.Cancelled;
        payment.UpdatedAt = DateTimeOffset.UtcNow;

        var hold = await ticketHolds.GetActiveByApplicationAsync(payment.ApplicationId, ct);
        if (hold is not null)
            await capacity.ReleaseAsync(hold.Id, ct);

        var application = await applications.GetByIdAsync(payment.ApplicationId, ct);
        if (application?.Status == ApplicationStatus.PaymentPending)
            await applicationService.RevertToApprovedAsync(payment.ApplicationId, ct);

        await payments.SaveChangesAsync(ct);

        var invitation = hold?.InvitationId is { } invitationId ? await invitations.GetByIdAsync(invitationId, ct) : null;
        var experience = await experiences.GetByIdAsync(payment.ExperienceId, ct);
        if (application is not null && experience is not null && invitation is not null)
        {
            var invitationUrl = $"{siteOptions.Value.BaseUrl.TrimEnd('/')}/invitation/{invitation.Code}";
            await emailService.SendAsync(
                "PaymentFailed", application.Email, "We couldn't complete your payment",
                new PaymentFailedEmailModel(application.FirstName, experience.Title, invitationUrl),
                nameof(Payment), payment.Id, ct);

            // Their seat went back into inventory when the checkout expired, so this is time-sensitive
            // in a way the approval email isn't — worth the second channel.
            await smsService.SendAsync(
                "PaymentFailed", application.Phone,
                $"The VI House: your payment for {experience.City} didn't complete. Your invitation is still open: {invitationUrl}",
                nameof(Payment), payment.Id, ct);
        }
    }

    /// <summary>
    /// Payment landed — turn the placeholder created at checkout into an account its owner can use.
    ///
    /// Everything here is deliberately on this side of the payment. The account itself has to exist
    /// earlier (Stripe needs someone to attach the charge to), but the Member role, the Active status
    /// and the only link that lets them choose a password are granted by money arriving, not by
    /// starting a checkout — so an abandoned attempt leaves nothing behind that looks like a member.
    ///
    /// Runs from the webhook rather than the browser redirect because this is the path that always
    /// runs, even when the tab is closed at the bank's 3-D Secure page.
    /// </summary>
    private async Task OpenAccountAsync(
        ApplicationUser user, Booking booking, Application? application, Experience? experience, CancellationToken ct)
    {
        if (!await userManager.IsInRoleAsync(user, Roles.Member))
            await userManager.AddToRoleAsync(user, Roles.Member);

        if (user.MemberStatus != MemberStatus.Active)
        {
            user.MemberStatus = MemberStatus.Active;
            await userManager.UpdateAsync(user);
        }

        if (!await userManager.HasPasswordAsync(user))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            // Same unpadded URL-safe alphabet WebEncoders.Base64UrlEncode produces, which is what the
            // ResetPassword page decodes with — using the framework primitive here would drag
            // ASP.NET Core into the Business layer. MembershipService does the same, for the same reason.
            var encoded = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(token));
            var setupUrl = $"{siteOptions.Value.BaseUrl.TrimEnd('/')}/Identity/Account/ResetPassword?code={encoded}";

            // The success page shows this link too, but that tab is easily lost — closed at the bank,
            // opened on a phone that then rang. Without this email a paid-up member's only way in is
            // to work out for themselves that they should use "forgot password" on an account they
            // never knowingly created.
            await emailService.SendAsync(
                "WelcomeSetup", user.Email!, "Set up your VI House account",
                new WelcomeSetupEmailModel(user.FirstName, setupUrl, null),
                nameof(ApplicationUser), user.Id, ct);
        }

        // The text carries the booking reference, not the setup link: a password token stays on the
        // one channel we already treat as the account's own, and the reference is the part somebody
        // actually wants on their phone.
        await smsService.SendAsync(
            "BookingConfirmed", application?.Phone,
            $"The VI House: payment received. Booking {booking.BookingReference}"
                + (experience is null ? "" : $" for {experience.City}")
                + ". Your account is open — check your email to set a password.",
            nameof(Booking), booking.Id, ct);
    }

    private async Task<(long AmountMinor, string? Error)> TryApplyPromoAsync(string code, Guid experienceId, long baseAmountMinor, CancellationToken ct)
    {
        var promo = await promoCodes.GetByCodeAsync(code, ct);
        if (promo is null || !promo.IsActive)
            return (baseAmountMinor, "That promo code isn't valid.");
        if (promo.ExpiresAt is { } expires && expires < DateTimeOffset.UtcNow)
            return (baseAmountMinor, "That promo code has expired.");
        if (promo.ExperienceId is { } restrictedTo && restrictedTo != experienceId)
            return (baseAmountMinor, "That promo code doesn't apply to this experience.");

        var redeemed = await promoCodes.TryRedeemAsync(promo.Id, ct);
        if (!redeemed)
            return (baseAmountMinor, "That promo code has reached its redemption limit.");

        var discounted = promo.Type == PromoCodeType.Percentage
            ? baseAmountMinor - baseAmountMinor * promo.Value / 100
            : baseAmountMinor - promo.Value;

        return (Math.Max(0, discounted), null);
    }

    private async Task<ApplicationUser> ProvisionMemberAccountAsync(Application application, CancellationToken ct)
    {
        var existing = await userManager.FindByEmailAsync(application.Email);
        if (existing is not null)
            return existing;

        var user = new ApplicationUser
        {
            UserName = application.Email,
            Email = application.Email,
            EmailConfirmed = true, // already vetted through the application review — no separate confirmation email loop
            FirstName = application.FirstName,
            LastName = application.LastName,
            Country = application.Country,
            City = application.City,
            MemberStatus = MemberStatus.PendingApplication,
        };

        // Random, never-communicated password — the member sets their own via the password-reset
        // link shown on the checkout success page (CheckoutController.Success), not emailed here.
        var temporaryPassword = RandomNumberGenerator.GetString(
            "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789!#%", 24);

        var result = await userManager.CreateAsync(user, temporaryPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Could not provision member account for {application.Email}: {string.Join("; ", result.Errors.Select(e => e.Description))}");

        // No role and no Active status here on purpose. This account exists only because Stripe needs
        // something to attach the charge to; it is opened for real in OpenAccountAsync, when the
        // payment actually lands. Someone who reaches the card form and walks away leaves a shell
        // behind, not a member.
        application.UserId = user.Id;
        await applications.SaveChangesAsync(ct);

        return user;
    }
}
