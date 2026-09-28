using VIHouse.Business;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
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

using VIHouse.Entities.Referrals;

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
    IProfileRepository profiles,
    ICapacityService capacity,
    IPaymentProvider paymentProvider,
    IOptions<SiteOptions> siteOptions,
    IMembershipService membershipService,
    IAmbassadorService ambassadorService,
    IPaymentTransactionService transactions,
    IOutbox outbox,
    ILogger<PaymentService> logger,
    UserManager<ApplicationUser> userManager) : IPaymentService
{
    /// <summary>How long a seat stays held once the buyer has finished checkout with a delayed
    /// payment method and the bank is confirming. Bank debits settle in days, not minutes.</summary>
    private static readonly TimeSpan ProcessingHold = TimeSpan.FromDays(14);

    /// <summary>The experience's member discount, if the applicant's address belongs to a current
    /// member; 0 otherwise. Read live, so a lapsed membership stops discounting the moment it lapses.</summary>
    private async Task<int> MemberDiscountForAsync(Experience experience, string applicantEmail, CancellationToken ct)
    {
        if (experience.MemberDiscountPercent <= 0) return 0;
        var user = await userManager.FindByEmailAsync(applicantEmail);
        if (user is null) return 0;
        return await membershipService.GetCurrentMembershipAsync(user.Id, ct) is null ? 0 : experience.MemberDiscountPercent;
    }

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
        var memberDiscount = await MemberDiscountForAsync(experience, application.Email, ct);

        var options = availableTicketTypes
            .Where(t => t.SalesStartAt is null || t.SalesStartAt <= now)
            .Where(t => t.SalesEndAt is null || t.SalesEndAt >= now)
            .Select(t => new TicketTypeOption(t.Id, t.Title, t.Description, MemberPricing.Apply(t.PriceMinor, memberDiscount), t.Currency, t.PerksText, t.Inventory <= 0)
            {
                FullPriceMinor = memberDiscount > 0 ? t.PriceMinor : null,
            })
            .ToList();

        return new InvitationLandingInfo(
            true, null, application.FirstName, experience.Title, experience.City, experience.Country,
            experience.StartAtUtc, experience.EndAtUtc, options)
        {
            MemberDiscountPercent = memberDiscount,
        };
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

        // Amount: the member price first (the same figure the invitation page showed), then an
        // optional promo code on top — redeemed atomically (same no-oversell pattern as ticket
        // inventory) before we ever reserve capacity or talk to Stripe.
        var amountMinor = MemberPricing.Apply(ticketType.PriceMinor, await MemberDiscountForAsync(experience, application.Email, ct));
        if (!string.IsNullOrWhiteSpace(promoCode))
        {
            var promoResult = await TryApplyPromoAsync(promoCode.Trim(), application.ExperienceId, application.Email, amountMinor, ct);
            if (promoResult.Error is not null)
                return CheckoutInitiationResult.Fail(promoResult.Error);
            amountMinor = promoResult.AmountMinor;
        }

        // A checkout they finished whose bank is still confirming: the seat is held for it, and a
        // second session would take the money twice. The first resolves on its own.
        if ((await payments.FindAsync(p => p.ApplicationId == application.Id && p.Status == PaymentStatus.Pending, ct)).Count > 0)
            return CheckoutInitiationResult.Fail("Your previous payment is still being confirmed by your bank. We'll email you the moment it lands — there's no need to pay again.");

        // Retry path: a prior attempt on this application left an Active hold (different ticket
        // type, or they bounced back from Stripe) — release it before reserving a fresh one so we
        // never hold two seats for the same applicant.
        var existingHold = await ticketHolds.GetActiveByApplicationAsync(application.Id, ct);
        if (existingHold is not null)
            await capacity.ReleaseAsync(existingHold.Id, ct);

        var hold = await capacity.TryReserveAsync(ticketTypeId, 1, application.Id, invitation.Id, ct);
        if (hold is null)
            return CheckoutInitiationResult.Fail("Sorry — this ticket type just sold out.");

        PaymentTransaction? transaction = null;
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

            // The money record, opened Pending before the provider is asked for anything: it is
            // what the webhook moves, and what "is this paid" is answered from (see PaymentTransaction).
            transaction = await transactions.OpenAsync(PaymentTransactionKind.Experience, user.Id, nameof(Payment), payment.Id, amountMinor, ticketType.Currency, ct);
            payment.TransactionId = transaction.Id;

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
                })
                {
                    // The picture on the cards and the experience page, so the checkout looks like
                    // the thing that was just chosen. Absolute — the provider fetches it itself.
                    ImageUrl = AbsoluteOrNull(ExperienceService.CoverUrl(experience)),
                    ProviderCustomerId = user.ProviderCustomerId,
                    // The approval and booking texts already exist; this is where the number to send
                    // them to comes from when the applicant did not give one on the form.
                    CollectPhone = true,
                }, ct);

            payment.ProviderReference = session.SessionId;
            await payments.SaveChangesAsync(ct);
            await transactions.AttachSessionAsync(transaction.Id, session.SessionId, user.ProviderCustomerId, ct);

            if (application.Status == ApplicationStatus.Approved)
                await applicationService.MarkPaymentPendingAsync(application.Id, ct);

            return CheckoutInitiationResult.Ok(session.Url);
        }
        catch (Exception ex)
        {
            // Stripe call (or anything else) failed after we'd already reserved the seat — give it
            // back rather than leaving a phantom hold nobody will ever complete, and close the
            // money record that never reached the provider.
            logger.LogError(ex, "Could not open a ticket checkout for application {ApplicationId}.", application.Id);
            await capacity.ReleaseAsync(hold.Id, ct);
            if (transaction is not null) await transactions.CancelOpenAsync(transaction.Id, "Checkout could not be opened at the provider.", ct);
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
            // The pending kind comes from the money record, the one thing the provider moves.
            var open = payment.TransactionId is { } tid ? await transactions.GetByIdAsync(tid, ct) : null;
            return new BookingConfirmationInfo(false, null, experience?.Title, experience?.City, payment.AmountMinor, payment.Currency, null)
            {
                AwaitingBank = open?.Status == PaymentTransactionStatus.Processing,
                RequiresAction = open?.Status == PaymentTransactionStatus.RequiresAction,
            };
        }

        var booking = await bookings.GetByIdAsync(payment.BookingId.Value, ct);
        return new BookingConfirmationInfo(true, booking?.BookingReference, experience?.Title, experience?.City, payment.AmountMinor, payment.Currency, payment.UserId);
    }

    /// <summary>A site-relative media path as an absolute URL, or null when there is no image.
    /// An already-absolute URL (an admin pasted one) is left alone.</summary>
    private string? AbsoluteOrNull(string? path) =>
        string.IsNullOrWhiteSpace(path) ? null
        : path.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? path
        : SiteUrls.Absolute(siteOptions.Value.BaseUrl, path);

    /// <summary>
    /// Duplicate delivery is not this service's concern: PaymentWebhookDispatcher records every
    /// event under a unique key before any handler runs and wraps all handlers in one transaction,
    /// so this only has to be correct for an event it sees exactly once. Side effects go through
    /// the outbox, keyed on the outcome, so a retry after a rollback cannot send twice.
    ///
    /// The one rule: a booking exists only after <see cref="PaymentWebhookEventType.CheckoutCompleted"/>,
    /// which the provider only produces once the money is there. A completed checkout with a
    /// delayed payment method is "awaiting payment" — the seat is kept, nothing is granted.
    /// </summary>
    public async Task HandleWebhookEventAsync(PaymentWebhookEvent webhookEvent, CancellationToken ct = default)
    {
        switch (webhookEvent.Type)
        {
            case PaymentWebhookEventType.CheckoutCompleted when webhookEvent.SessionId is not null:
                await HandleCheckoutCompletedAsync(webhookEvent, ct);
                break;
            case PaymentWebhookEventType.CheckoutCompletedAwaitingPayment when webhookEvent.SessionId is not null:
                await HandleCheckoutAwaitingPaymentAsync(webhookEvent, ct);
                break;
            case PaymentWebhookEventType.CheckoutExpired when webhookEvent.SessionId is not null:
                await HandleCheckoutEndedUnpaidAsync(webhookEvent.SessionId, paymentFailed: false, ct);
                break;
            case PaymentWebhookEventType.CheckoutPaymentFailed when webhookEvent.SessionId is not null:
                await HandleCheckoutEndedUnpaidAsync(webhookEvent.SessionId, paymentFailed: true, ct);
                break;
            // A delayed payment that fails or is cancelled at the intent level after the checkout
            // completed. The intent is only known once the checkout completed, so this can never
            // touch a checkout the buyer is still on; the session is found through the money record.
            case PaymentWebhookEventType.PaymentIntentFailed when webhookEvent.PaymentIntentId is not null:
            case PaymentWebhookEventType.PaymentIntentCanceled when webhookEvent.PaymentIntentId is not null:
                if (await SessionForIntentAsync(webhookEvent.PaymentIntentId, PaymentTransactionKind.Experience, ct) is { } failedSession)
                    await HandleCheckoutEndedUnpaidAsync(failedSession, paymentFailed: true, ct);
                break;
            case PaymentWebhookEventType.ChargeRefunded when webhookEvent.PaymentIntentId is not null:
                await HandleRefundAsync(webhookEvent, ct);
                break;
            case PaymentWebhookEventType.DisputeCreated when webhookEvent.PaymentIntentId is not null:
            case PaymentWebhookEventType.DisputeClosed when webhookEvent.PaymentIntentId is not null:
                await HandleDisputeAsync(webhookEvent, ct);
                break;
        }
    }

    private async Task HandleCheckoutCompletedAsync(PaymentWebhookEvent webhookEvent, CancellationToken ct)
    {
        var sessionId = webhookEvent.SessionId!;
        var payment = await payments.GetByProviderReferenceAsync(sessionId, ct);
        if (payment is null || payment.Status is PaymentStatus.Paid or PaymentStatus.Refunded or PaymentStatus.PartiallyRefunded)
            return; // unknown session, or already fulfilled

        var now = DateTimeOffset.UtcNow;
        payment.Status = PaymentStatus.Paid;
        // The provider's figure for what was actually charged, when it sent one.
        if (webhookEvent.AmountMinor is { } charged) payment.AmountMinor = charged;
        payment.UpdatedAt = now;

        var hold = await ticketHolds.GetActiveByApplicationAsync(payment.ApplicationId, ct);
        var quantity = hold?.Quantity ?? 1;
        if (hold is not null)
        {
            await capacity.CommitAsync(hold.Id, ct);
        }
        else if (!await ticketTypes.TryDecrementInventoryAsync(payment.TicketTypeId, quantity, ct))
        {
            // The hold lapsed before the provider's answer arrived (the sweep gave the seat back in
            // the same minute the buyer paid) and the seat has since gone. Money that arrived is
            // honoured — the booking is made and the overshoot is written down for a human, because
            // an oversold seat is something the House can put right with the buyer and a paid-for
            // booking that silently never existed is not.
            logger.LogWarning("Payment {PaymentId} for ticket type {TicketTypeId} completed after its hold lapsed and inventory is exhausted; booked anyway (oversold by {Quantity}).",
                payment.Id, payment.TicketTypeId, quantity);
        }

        var reference = await bookings.GenerateNextReferenceAsync(now.Year % 100, ct);
        var booking = new Booking
        {
            BookingReference = reference,
            UserId = payment.UserId!.Value,
            ExperienceId = payment.ExperienceId,
            TicketTypeId = payment.TicketTypeId,
            ApplicationId = payment.ApplicationId,
            Quantity = quantity,
            AmountMinor = payment.AmountMinor,
            Currency = payment.Currency,
            Status = BookingStatus.Confirmed,
            ConfirmedAt = now,
        };
        await bookings.AddAsync(booking, ct);
        await bookings.SaveChangesAsync(ct);

        payment.BookingId = booking.Id;

        // The invitation this checkout came in on — from the hold when there is one, otherwise the
        // application's latest — is spent now, so it cannot be used to buy the same place twice.
        var invitation = hold?.InvitationId is { } invitationId
            ? await invitations.GetByIdAsync(invitationId, ct)
            : await invitations.GetLatestByApplicationAsync(payment.ApplicationId, ct);
        if (invitation is not null && !invitation.IsUsed)
        {
            invitation.IsUsed = true;
            invitation.UsedAt = now;
        }

        // Money wins: an application that had been put back to Approved (a failure event that was
        // later contradicted by the bank) is walked forward rather than refused.
        if ((await applications.GetByIdAsync(payment.ApplicationId, ct))?.Status == ApplicationStatus.Approved)
            await applicationService.MarkPaymentPendingAsync(payment.ApplicationId, ct);
        await applicationService.MarkPaidAsync(payment.ApplicationId, ct);
        await payments.SaveChangesAsync(ct);

        var confirmedApplication = await applications.GetByIdAsync(payment.ApplicationId, ct);

        // Stripe asked for a number on its own page (CollectPhone). If the applicant never gave one
        // on our form, this is where it arrives — in time for the booking text below.
        if (confirmedApplication is not null
            && string.IsNullOrWhiteSpace(confirmedApplication.Phone)
            && !string.IsNullOrWhiteSpace(webhookEvent.CustomerPhone))
        {
            confirmedApplication.Phone = webhookEvent.CustomerPhone;
            await applications.SaveChangesAsync(ct);
        }

        await ambassadorService.RecordConversionAsync(confirmedApplication?.ReferralCode, ReferralConversionKind.TicketPurchase,
            nameof(Payment), payment.Id, payment.AmountMinor, payment.Currency,
            ReferralTargetKind.Experience, payment.ExperienceId,
            buyerUserId: payment.UserId, buyerEmail: confirmedApplication?.Email, ct: ct);
        var confirmedExperience = await experiences.GetByIdAsync(payment.ExperienceId, ct);

        // The money has landed — this is the moment the account becomes one its owner can use.
        var accountSetupPending = false;
        if (await userManager.FindByIdAsync(payment.UserId!.Value.ToString()) is { } member)
            accountSetupPending = await OpenAccountAsync(member, booking, confirmedApplication, confirmedExperience, ct);

        if (confirmedApplication is not null && confirmedExperience is not null)
        {
            await outbox.EnqueueEmailAsync(
                $"email:BookingConfirmed:Booking:{booking.Id}",
                "BookingConfirmed", confirmedApplication.Email, $"You're confirmed — booking {booking.BookingReference}",
                new BookingConfirmedEmailModel(
                    confirmedApplication.FirstName, booking.BookingReference, confirmedExperience.Title, confirmedExperience.City,
                    confirmedExperience.StartAtUtc, confirmedExperience.EndAtUtc, booking.AmountMinor, booking.Currency)
                {
                    Venue = confirmedExperience.Venue,
                    IsOnline = confirmedExperience.AttendanceMode == ExperienceAttendanceMode.Online,
                    TimeZoneId = confirmedExperience.TimeZoneId,
                    TicketUrl = SiteUrls.Absolute(siteOptions.Value.BaseUrl, SiteUrls.Booking(booking.BookingReference)),
                    ExperienceUrl = SiteUrls.Absolute(siteOptions.Value.BaseUrl, SiteUrls.Experience(confirmedExperience.Slug)),
                    CalendarUrl = SiteUrls.Absolute(siteOptions.Value.BaseUrl,
                        SiteUrls.InCulture(SiteUrls.ExperienceCalendar(confirmedExperience.Slug), confirmedApplication.PreferredCulture)),
                    AccountSetupPending = accountSetupPending,
                },
                confirmedApplication.PreferredCulture ?? SiteCultures.Default,
                nameof(Booking), booking.Id, ct);

            await outbox.EnqueueNotificationAsync(
                $"notification:BookingConfirmed:Booking:{booking.Id}",
                payment.UserId!.Value, NotificationType.Payment,
                "Booking Confirmed", $"You're confirmed for The VI House — {confirmedExperience.City}. Reference {booking.BookingReference}.",
                SiteUrls.AccountBookings, nameof(Booking), booking.Id, ct);
        }
    }

    /// <summary>
    /// The buyer finished checkout with a delayed payment method. Nothing is granted: the payment
    /// row goes to Pending (so the sweep's "abandoned" query no longer sees it), the seat stays
    /// held for as long as a bank debit can take, and the buyer is told what is happening. The
    /// outcome arrives later as CheckoutCompleted or CheckoutPaymentFailed.
    /// </summary>
    private async Task HandleCheckoutAwaitingPaymentAsync(PaymentWebhookEvent webhookEvent, CancellationToken ct)
    {
        var payment = await payments.GetByProviderReferenceAsync(webhookEvent.SessionId!, ct);
        if (payment is null || payment.Status != PaymentStatus.Created) return;

        var now = DateTimeOffset.UtcNow;
        payment.Status = PaymentStatus.Pending;
        payment.UpdatedAt = now;

        var hold = await ticketHolds.GetActiveByApplicationAsync(payment.ApplicationId, ct);
        if (hold is not null)
            await capacity.ExtendAsync(hold.Id, now.Add(ProcessingHold), ct);

        await payments.SaveChangesAsync(ct);

        var application = await applications.GetByIdAsync(payment.ApplicationId, ct);
        var experience = await experiences.GetByIdAsync(payment.ExperienceId, ct);
        if (application is null || experience is null) return;

        var what = $"The VI House — {experience.City}";
        await outbox.EnqueueEmailAsync(
            $"email:PaymentProcessing:Payment:{payment.Id}",
            "PaymentProcessing", application.Email, "We've received your order — payment in progress",
            new PaymentProcessingEmailModel(application.FirstName, what, payment.AmountMinor, payment.Currency,
                SiteUrls.Absolute(siteOptions.Value.BaseUrl, SiteUrls.AccountBookings)),
            application.PreferredCulture ?? SiteCultures.Default,
            nameof(Payment), payment.Id, ct);

        if (payment.UserId is { } userId)
        {
            await outbox.EnqueueNotificationAsync(
                $"notification:PaymentProcessing:Payment:{payment.Id}",
                userId, NotificationType.Payment,
                "Payment In Progress", $"Your bank is confirming your payment for {what}. We'll confirm your place as soon as it lands.",
                SiteUrls.AccountBookings, nameof(Payment), payment.Id, ct);
        }
    }

    /// <summary>
    /// The checkout ended without money — the session expired, or a delayed payment was declined
    /// by the bank. Either way the held place goes back and the approval stands. An "expired" only
    /// ends a checkout nobody finished (Created): a completed one waiting on the bank (Pending)
    /// cannot expire, and a stray expiry must not release a seat the buyer has paid for.
    /// </summary>
    private async Task HandleCheckoutEndedUnpaidAsync(string sessionId, bool paymentFailed, CancellationToken ct)
    {
        var payment = await payments.GetByProviderReferenceAsync(sessionId, ct);
        if (payment is null) return;
        if (payment.Status != PaymentStatus.Created && !(paymentFailed && payment.Status == PaymentStatus.Pending))
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

        var invitation = hold?.InvitationId is { } invitationId
            ? await invitations.GetByIdAsync(invitationId, ct)
            : await invitations.GetLatestByApplicationAsync(payment.ApplicationId, ct);
        var experience = await experiences.GetByIdAsync(payment.ExperienceId, ct);
        if (application is not null && experience is not null && invitation is not null)
        {
            var invitationUrl = SiteUrls.Absolute(siteOptions.Value.BaseUrl, SiteUrls.Invitation(invitation.Code));
            await outbox.EnqueueEmailAsync(
                $"email:PaymentFailed:Payment:{payment.Id}",
                "PaymentFailed", application.Email, "We couldn't complete your payment",
                new PaymentFailedEmailModel(application.FirstName, experience.Title, invitationUrl),
                application.PreferredCulture ?? SiteCultures.Default,
                nameof(Payment), payment.Id, ct);

            // Their seat went back into inventory when the checkout expired, so this is time-sensitive
            // in a way the approval email isn't — worth the second channel.
            await outbox.EnqueueSmsAsync(
                $"sms:PaymentFailed:Payment:{payment.Id}",
                "PaymentFailed", application.Phone,
                $"The VI House: your payment for {experience.City} didn't complete. Your invitation is still open: {invitationUrl}",
                nameof(Payment), payment.Id, ct);
        }

        // In the account as well as the inbox, and worded as the absence it is: nothing was
        // confirmed. Keyed on the payment, so the provider's retries add no second line.
        if (payment.UserId is { } userId)
        {
            var what = experience is null ? "your booking" : $"The VI House — {experience.City}";
            await outbox.EnqueueNotificationAsync(
                $"notification:PaymentNotCompleted:Payment:{payment.Id}",
                userId, NotificationType.Payment,
                paymentFailed ? "Payment Failed" : "Checkout Cancelled",
                paymentFailed
                    ? $"Your bank declined the payment for {what}, so your place wasn't confirmed and nothing was charged. Your invitation is still open."
                    : $"The checkout for {what} closed before it was paid, so your place wasn't confirmed and nothing was charged. Your invitation is still open.",
                SiteUrls.AccountPayments, nameof(Payment), payment.Id, ct);
        }
    }

    /// <summary>The checkout session behind a payment intent, when the intent belongs to one of
    /// this service's transactions and that transaction is not settled.</summary>
    private async Task<string?> SessionForIntentAsync(string paymentIntentId, PaymentTransactionKind kind, CancellationToken ct)
    {
        var transaction = await transactions.GetByPaymentIntentAsync(paymentIntentId, ct);
        return transaction is { ProviderSessionId: { } session } && transaction.Kind == kind
            && transaction.Status is PaymentTransactionStatus.Failed or PaymentTransactionStatus.Canceled
            ? session : null;
    }

    /// <summary>
    /// Money went back to the buyer. Found through the transaction, which learned the payment
    /// intent when the checkout completed. A full refund cancels the booking and returns the seat;
    /// a partial one is recorded on the payment and the booking stands. The ambassador's
    /// commission follows the money: pro rata for a partial refund, all of it for a full one.
    /// </summary>
    private async Task HandleRefundAsync(PaymentWebhookEvent webhookEvent, CancellationToken ct)
    {
        var transaction = await transactions.GetByPaymentIntentAsync(webhookEvent.PaymentIntentId!, ct);
        if (transaction is not { Kind: PaymentTransactionKind.Experience, RelatedEntityType: nameof(Payment) }) return;

        var payment = await payments.GetByIdAsync(transaction.RelatedEntityId, ct);
        if (payment is null || payment.Status == PaymentStatus.Refunded) return;

        var refunded = webhookEvent.AmountRefundedMinor ?? 0;
        var full = refunded >= (webhookEvent.AmountMinor ?? payment.AmountMinor);
        var now = DateTimeOffset.UtcNow;

        payment.Status = full ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;
        payment.RefundStatus = full ? "refunded" : $"partial:{refunded}";
        payment.UpdatedAt = now;

        var booking = payment.BookingId is { } bookingId ? await bookings.GetByIdAsync(bookingId, ct) : null;
        if (full && booking is not null && booking.Status == BookingStatus.Confirmed)
        {
            booking.Status = BookingStatus.Refunded;
            booking.UpdatedAt = now;
            await ticketTypes.IncrementInventoryAsync(booking.TicketTypeId ?? payment.TicketTypeId, booking.Quantity, ct);
        }
        await payments.SaveChangesAsync(ct);

        await ambassadorService.ReverseForRefundAsync(nameof(Payment), payment.Id, refunded, full, ct);

        var application = await applications.GetByIdAsync(payment.ApplicationId, ct);
        var experience = await experiences.GetByIdAsync(payment.ExperienceId, ct);
        if (application is null) return;

        var what = experience is null ? "your experience booking" : $"The VI House — {experience.City}";
        var effect = full && booking is not null
            ? $"Your booking {booking.BookingReference} has been cancelled and the place released."
            : "Your booking is unchanged.";

        // Keyed on the amount as well: a second partial refund is a second piece of news.
        await outbox.EnqueueEmailAsync(
            $"email:PaymentRefunded:Payment:{payment.Id}:{refunded}",
            "PaymentRefunded", application.Email, full ? "Your refund is on its way" : "A partial refund is on its way",
            new PaymentRefundedEmailModel(application.FirstName, what, refunded, payment.Currency, !full, effect),
            application.PreferredCulture ?? SiteCultures.Default,
            nameof(Payment), payment.Id, ct);

        if (payment.UserId is { } userId)
        {
            await outbox.EnqueueNotificationAsync(
                $"notification:PaymentRefunded:Payment:{payment.Id}:{refunded}",
                userId, NotificationType.Payment,
                full ? "Payment Refunded" : "Partial Refund", $"{(full ? "A full" : "A partial")} refund for {what} is on its way back to you. {effect}",
                SiteUrls.AccountBookings, nameof(Payment), payment.Id, ct);
        }
    }

    /// <summary>A dispute is a flag on the transaction, not a state of the booking; a human decides
    /// what to do with the booking, so the House's contact address is told, once per verdict.</summary>
    private async Task HandleDisputeAsync(PaymentWebhookEvent webhookEvent, CancellationToken ct)
    {
        var transaction = await transactions.GetByPaymentIntentAsync(webhookEvent.PaymentIntentId!, ct);
        if (transaction is not { Kind: PaymentTransactionKind.Experience, RelatedEntityType: nameof(Payment) }) return;

        var payment = await payments.GetByIdAsync(transaction.RelatedEntityId, ct);
        if (payment is null) return;

        var opened = webhookEvent.Type == PaymentWebhookEventType.DisputeCreated;
        var verdict = opened ? "opened" : $"closed ({webhookEvent.DisputeStatus ?? "unknown"})";
        if (opened && payment.Status == PaymentStatus.Paid)
        {
            payment.Status = PaymentStatus.Chargeback;
            payment.UpdatedAt = DateTimeOffset.UtcNow;
            await payments.SaveChangesAsync(ct);
        }
        else if (!opened && webhookEvent.DisputeStatus == "won" && payment.Status == PaymentStatus.Chargeback)
        {
            payment.Status = PaymentStatus.Paid;
            payment.UpdatedAt = DateTimeOffset.UtcNow;
            await payments.SaveChangesAsync(ct);
        }

        var contact = siteOptions.Value.ContactEmail;
        if (string.IsNullOrWhiteSpace(contact)) return;

        var application = await applications.GetByIdAsync(payment.ApplicationId, ct);
        var booking = payment.BookingId is { } bookingId ? await bookings.GetByIdAsync(bookingId, ct) : null;
        await outbox.EnqueueEmailAsync(
            $"email:Dispute:Payment:{payment.Id}:{verdict}",
            "ContactMessage", contact, $"Payment dispute {verdict} — booking {booking?.BookingReference ?? "(none)"}",
            new ContactMessageEmailModel("The VI House (system)", application?.Email ?? "unknown", "Payment dispute",
                $"A dispute was {verdict} on the ticket payment by {application?.FirstName} {application?.LastName} ({application?.Email}).\n" +
                $"Booking: {booking?.BookingReference ?? "none"}. Amount: {payment.AmountMinor / 100m:0.00} {payment.Currency}. Provider payment intent: {webhookEvent.PaymentIntentId}.\n" +
                "Nothing was changed on the booking automatically. Respond to the dispute in the provider dashboard and decide about the booking there."),
            SiteCultures.Default,
            nameof(Payment), payment.Id, ct);
    }

    /// <summary>
    /// Payment landed — turn the placeholder created at checkout into an account its owner can use.
    ///
    /// Everything here is deliberately on this side of the payment. The account itself has to exist
    /// earlier (Stripe needs someone to attach the charge to), but the Member role, the Active status
    /// and the only link that lets them choose a password are granted by money arriving, not by
    /// starting a checkout — so an abandoned attempt leaves nothing behind that looks like a member.
    ///
    /// Runs from the webhook (or the same path fed by a server-side read of the session) rather
    /// than the browser redirect, because this is the path that always runs — even when the tab is
    /// closed at the bank's 3-D Secure page.
    /// </summary>
    /// <returns>True when a password-setup email was queued — the booking confirmation then says so.</returns>
    private async Task<bool> OpenAccountAsync(
        ApplicationUser user, Booking booking, Application? application, Experience? experience, CancellationToken ct)
    {
        if (!await userManager.IsInRoleAsync(user, Roles.Member))
            await userManager.AddToRoleAsync(user, Roles.Member);

        if (user.MemberStatus != MemberStatus.Active)
        {
            user.MemberStatus = MemberStatus.Active;
            await userManager.UpdateAsync(user);
        }

        // Never signed in counts too: an account opened before the no-password change carries a random
        // password nobody knows, and a returning buyer who never set one up needs the same way in.
        var needsSetup = !await userManager.HasPasswordAsync(user) || user.LastLoginAt is null;
        if (needsSetup)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            // Same unpadded URL-safe alphabet WebEncoders.Base64UrlEncode produces, which is what the
            // ResetPassword page decodes with — using the framework primitive here would drag
            // ASP.NET Core into the Business layer. MembershipService does the same, for the same reason.
            var encoded = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(token));
            var setupUrl = SiteUrls.Absolute(siteOptions.Value.BaseUrl, SiteUrls.ResetPassword(encoded));

            // The only place the setup link is issued. The success page no longer shows one: it is
            // reachable by anyone holding the session id from the URL, and a password link is not
            // something to hand to whoever has a browser-history entry.
            // Keyed on the booking, not only the user: a second booking by someone who still has not
            // set a password gets a fresh link rather than nothing.
            await outbox.EnqueueEmailAsync(
                $"email:WelcomeSetup:User:{user.Id}:Booking:{booking.Id}",
                "WelcomeSetup", user.Email!, "Set up your VI House account",
                new WelcomeSetupEmailModel(user.FirstName, setupUrl, null)
                {
                    BookingReference = booking.BookingReference,
                    ExperienceTitle = experience is null ? null : $"The VI House — {experience.City}",
                    ValidForHours = 24,
                },
                user.PreferredCulture ?? SiteCultures.Default,
                nameof(ApplicationUser), user.Id, ct);
        }

        // The text carries the booking reference, not the setup link: a password token stays on the
        // one channel we already treat as the account's own, and the reference is the part somebody
        // actually wants on their phone.
        await outbox.EnqueueSmsAsync(
            $"sms:BookingConfirmed:Booking:{booking.Id}",
            "BookingConfirmed", application?.Phone,
            $"The VI House: payment received. Booking {booking.BookingReference}"
                + (experience is null ? "" : $" for {experience.City}")
                + (needsSetup ? ". Your account is open — check your email to set a password." : "."),
            nameof(Booking), booking.Id, ct);

        return needsSetup;
    }

    private async Task<(long AmountMinor, string? Error)> TryApplyPromoAsync(string code, Guid experienceId, string applicantEmail, long baseAmountMinor, CancellationToken ct)
    {
        var promo = await promoCodes.GetByCodeAsync(code.ToUpperInvariant(), ct);
        if (promo is null || !promo.IsActive)
            return (baseAmountMinor, "That promo code isn't valid.");
        if (promo.ExpiresAt is { } expires && expires < DateTimeOffset.UtcNow)
            return (baseAmountMinor, "That promo code has expired.");
        if (promo.Scope != PromoScope.Experiences)
            return (baseAmountMinor, "That promo code is for membership, not experience tickets.");
        if (promo.ExperienceId is { } restrictedTo && restrictedTo != experienceId)
            return (baseAmountMinor, "That promo code doesn't apply to this experience.");
        if (promo.RestrictedToEmail is { } reservedFor && reservedFor != applicantEmail.Trim().ToUpperInvariant())
            return (baseAmountMinor, "That promo code is reserved for a different email address.");

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
            PreferredCulture = application.PreferredCulture,
        };

        // No password at all. The owner chooses one through the setup link emailed when the payment
        // lands (OpenAccountAsync). This used to be a random, never-communicated password, which made
        // HasPasswordAsync true — so the setup email was never sent and the account had no way in.
        var result = await userManager.CreateAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Could not provision member account for {application.Email}: {string.Join("; ", result.Errors.Select(e => e.Description))}");

        // No role and no Active status here on purpose. This account exists only because Stripe needs
        // something to attach the charge to; it is opened for real in OpenAccountAsync, when the
        // payment actually lands. Someone who reaches the card form and walks away leaves a shell
        // behind, not a member.
        application.UserId = user.Id;
        await applications.SaveChangesAsync(ct);

        // The application asked the same questions the account profile holds, so the answers are
        // carried across now rather than asked for again on first sign-in. Guarded because a
        // profile may already exist for an address that was pre-registered by hand.
        if (await profiles.GetByUserIdAsync(user.Id, ct) is null)
        {
            await profiles.AddAsync(new Profile
            {
                UserId = user.Id,
                JobTitle = application.JobTitle,
                AddressLine1 = application.AddressLine1,
                AddressLine2 = application.AddressLine2,
                PostalCode = application.PostalCode,
                About = application.AboutStatement,
                Expectations = application.ExpectationsStatement,
                EarningsBand = application.EarningsBand,
                UpdatedAt = DateTimeOffset.UtcNow,
            }, ct);
            await profiles.SaveChangesAsync(ct);
        }

        return user;
    }
}
