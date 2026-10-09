using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;

namespace VIHouse.Business.Concrete;

public class StripePaymentProvider : IPaymentProvider
{
    private readonly StripeOptions options;
    private readonly SessionService sessionService;
    private readonly Stripe.BillingPortal.SessionService portalService;
    private readonly SubscriptionService subscriptionService;
    private readonly CouponService couponService;
    private readonly EventService eventService;
    private readonly ILogger<StripePaymentProvider> logger;

    /// <summary>
    /// Every service is bound to the one shared <see cref="IStripeClient"/> (a singleton, see
    /// StripeClientFactory) rather than the static StripeConfiguration.ApiKey — the previous
    /// approach re-assigned a process-wide global from a per-request constructor. The client also
    /// carries the network-retry policy; Stripe.net attaches an idempotency key to every POST it
    /// retries, so a request that was sent but never answered cannot be executed twice.
    /// </summary>
    public StripePaymentProvider(IStripeClient client, IOptions<StripeOptions> options, ILogger<StripePaymentProvider> logger)
    {
        this.options = options.Value;
        this.logger = logger;
        sessionService = new SessionService(client);
        portalService = new Stripe.BillingPortal.SessionService(client);
        subscriptionService = new SubscriptionService(client);
        couponService = new CouponService(client);
        eventService = new EventService(client);
    }

    public async Task<CheckoutSessionResult> CreateCheckoutSessionAsync(CreateCheckoutSessionRequest request, CancellationToken ct = default)
    {
        var isSubscription = request.Recurring is not null;

        var createOptions = new SessionCreateOptions
        {
            Mode = isSubscription ? "subscription" : "payment",
            // Customer and CustomerEmail are mutually exclusive at Stripe: a known customer is
            // reused (their saved cards, one billing portal), an unknown buyer is created from the
            // email on the first payment and remembered from the webhook.
            Customer = request.ProviderCustomerId,
            CustomerEmail = request.ProviderCustomerId is null ? request.CustomerEmail : null,
            ClientReferenceId = request.ClientReferenceId,
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            Metadata = new Dictionary<string, string>(request.Metadata),
            LineItems = [BuildLineItem(request)],
        };

        // What Stripe's own page collects on top of the card. Each is opt-in per request, because a
        // session checkout and a membership sign-up want different amounts of ceremony.
        if (request.CollectPhone)
        {
            createOptions.PhoneNumberCollection = new SessionPhoneNumberCollectionOptions { Enabled = true };
        }

        if (request.CollectBillingAddress)
        {
            createOptions.BillingAddressCollection = "required";
        }

        if (request.CollectCompanyDetails)
        {
            // Optional on purpose (brief: a member may be buying personally). TaxIdCollection is
            // Stripe's own "Add tax ID" affordance, which is optional by construction; the company
            // name has to be asked for as a custom field, and is marked optional explicitly.
            createOptions.TaxIdCollection = new SessionTaxIdCollectionOptions { Enabled = true };
            createOptions.CustomFields =
            [
                new SessionCustomFieldOptions
                {
                    Key = CompanyFieldKey,
                    Type = "text",
                    Optional = true,
                    Label = new SessionCustomFieldLabelOptions { Type = "custom", Custom = "Company (optional)" },
                    Text = new SessionCustomFieldTextOptions { MaximumLength = 100 },
                },
            ];
        }

        // Ticket checkouts expire to release the seat hold (CapacityService holds for 15 minutes);
        // Stripe rejects ExpiresAt on subscription sessions, and there's no inventory to free up for
        // a membership anyway, so the window only applies to one-off purchases.
        if (!isSubscription)
        {
            createOptions.ExpiresAt = DateTime.UtcNow.AddMinutes(30);
        }

        // A coupon and AllowPromotionCodes are mutually exclusive at Stripe; this site never sets
        // the latter, so the discount is always ours to decide.
        if (!string.IsNullOrWhiteSpace(request.ProviderCouponId))
        {
            createOptions.Discounts = [new SessionDiscountOptions { Coupon = request.ProviderCouponId }];
        }

        Session session;
        try
        {
            session = await sessionService.CreateAsync(createOptions, cancellationToken: ct);
        }
        catch (StripeException ex) when (createOptions.Customer is not null && ex.StripeError?.Param == "customer")
        {
            // The customer id remembered on the account no longer exists at Stripe (deleted in the
            // dashboard, or a different Stripe account's id). A sale is worth more than the link:
            // open the checkout by email — Stripe creates a fresh customer — and the webhook that
            // completes it writes the new id over the stale one (PaymentTransactionService).
            logger.LogWarning(ex, "Stripe rejected customer {CustomerId}; opening the checkout by email instead.", createOptions.Customer);
            createOptions.Customer = null;
            createOptions.CustomerEmail = request.CustomerEmail;
            session = await sessionService.CreateAsync(createOptions, cancellationToken: ct);
        }

        return new CheckoutSessionResult(session.Id, session.Url, UtcDates.ToOffset(session.ExpiresAt));
    }

    /// <summary>The key Stripe echoes the company name back under. Alphanumeric, as Stripe requires.</summary>
    private const string CompanyFieldKey = "company";

    private static string? CustomFieldValue(Session? session, string key) =>
        session?.CustomFields?.FirstOrDefault(f => f.Key == key)?.Text?.Value is { Length: > 0 } value ? value : null;

    public async Task ExpireCheckoutSessionAsync(string sessionId, CancellationToken ct = default)
    {
        try
        {
            await sessionService.ExpireAsync(sessionId, cancellationToken: ct);
        }
        catch (StripeException ex)
        {
            // Only an open session can be expired; one that has already completed or lapsed comes
            // back as an error, and that outcome is exactly what the caller wanted anyway.
            logger.LogWarning(ex, "Could not expire Stripe checkout session {SessionId}", sessionId);
        }
    }

    public async Task<string> CreateCouponAsync(CouponRequest request, CancellationToken ct = default)
    {
        var coupon = await couponService.CreateAsync(new CouponCreateOptions
        {
            Name = request.Name,
            PercentOff = request.PercentOff,
            AmountOff = request.AmountOffMinor,
            Currency = request.AmountOffMinor is null ? null : request.Currency?.ToLowerInvariant(),
            Duration = request.Forever ? "forever" : "once",
        }, cancellationToken: ct);

        return coupon.Id;
    }

    public async Task<bool> CancelSubscriptionAsync(string subscriptionId, CancellationToken ct = default)
    {
        try
        {
            await subscriptionService.CancelAsync(subscriptionId, cancellationToken: ct);
            return true;
        }
        catch (StripeException ex)
        {
            logger.LogWarning(ex, "Could not cancel Stripe subscription {SubscriptionId}", subscriptionId);
            return false;
        }
    }

    /// <summary>
    /// A mirrored plan sells by its Stripe Price id, so the sale lands under the named product in
    /// the dashboard and reporting; anything else — a ticket, a seminar, a plan whose sync has not
    /// landed yet — is priced inline, which is what every checkout did before the catalogue existed.
    /// Requiring the mirror would have turned a Stripe outage during an admin edit into "nobody can
    /// buy this plan", so the inline path stays as the fallback.
    /// </summary>
    private static SessionLineItemOptions BuildLineItem(CreateCheckoutSessionRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.ProviderPriceId))
        {
            return new SessionLineItemOptions { Quantity = 1, Price = request.ProviderPriceId };
        }

        return new SessionLineItemOptions
        {
            Quantity = 1,
            PriceData = new SessionLineItemPriceDataOptions
            {
                Currency = request.Currency,
                UnitAmount = request.AmountMinor,
                Recurring = request.Recurring switch
                {
                    RecurringInterval.Monthly => new SessionLineItemPriceDataRecurringOptions { Interval = "month" },
                    RecurringInterval.Annual => new SessionLineItemPriceDataRecurringOptions { Interval = "year" },
                    _ => null,
                },
                ProductData = new SessionLineItemPriceDataProductDataOptions
                {
                    Name = request.ProductName,
                    Description = request.ProductDescription,
                    // Stripe fetches this itself, so it has to be reachable without a session —
                    // hence a cover image rather than anything behind the member gate.
                    Images = string.IsNullOrWhiteSpace(request.ImageUrl) ? null : [request.ImageUrl],
                },
            },
        };
    }

    public async Task<PaymentProviderDetails?> GetPaymentDetailsAsync(string providerReference, CancellationToken ct = default)
    {
        try
        {
            var session = await sessionService.GetAsync(providerReference, new SessionGetOptions
            {
                Expand = ["payment_intent.latest_charge"],
            }, cancellationToken: ct);

            var charge = session.PaymentIntent?.LatestCharge;
            var card = charge?.PaymentMethodDetails?.Card;

            return new PaymentProviderDetails(
                session.Status, session.PaymentStatus, session.PaymentIntent?.Status, charge?.Status,
                charge?.AmountCaptured, card?.Brand, card?.Last4, charge?.ReceiptUrl,
                charge?.Refunded, charge?.AmountRefunded, charge?.Disputed);
        }
        catch (Exception ex) when (ex is StripeException or HttpRequestException
                                   || ex is TaskCanceledException && !ct.IsCancellationRequested)
        {
            // Covers "no such session" (e.g. the pending_ placeholder ProviderReference from a
            // checkout that never reached Stripe — see PaymentService.InitiateCheckoutAsync) as
            // well as genuine network/API failures. Stripe.net reports an unreachable host or a
            // timeout as HttpRequestException / TaskCanceledException rather than StripeException,
            // which used to turn the admin payment page into a 500 whenever Stripe could not be
            // reached. The caller falls back to local DB fields.
            logger.LogWarning(ex, "Could not fetch live Stripe details for {ProviderReference}", providerReference);
            return null;
        }
    }

    public async Task<string?> CreateBillingPortalUrlAsync(string providerCustomerId, string returnUrl, CancellationToken ct = default)
    {
        try
        {
            var session = await portalService.CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer = providerCustomerId,
                ReturnUrl = returnUrl,
            }, cancellationToken: ct);

            return session.Url;
        }
        catch (StripeException ex)
        {
            // The usual cause is the portal not having been configured in the Stripe dashboard yet
            // (Settings → Billing → Customer portal); the member page simply omits the button.
            logger.LogWarning(ex, "Could not open a Stripe billing portal session for {CustomerId}", providerCustomerId);
            return null;
        }
    }

    public async Task<PaymentWebhookEvent?> ReadCheckoutSessionAsync(string sessionId, CancellationToken ct = default)
    {
        try
        {
            var session = await sessionService.GetAsync(sessionId, cancellationToken: ct);

            // The same three outcomes the webhook delivers, read straight from the provider with the
            // secret key — never from anything the browser carried. Synthetic event ids keyed on the
            // session and the outcome: a paid read and a paid webhook are different events that
            // reach the same state, and the state machine makes the second a no-op.
            switch (session.Status)
            {
                case "complete" when session.PaymentStatus is "paid" or "no_payment_required":
                    return MapCheckoutSession($"reconcile_{session.Id}_paid", PaymentWebhookEventType.CheckoutCompleted, session, "reconcile.checkout.paid");
                case "complete" when session.PaymentStatus == "unpaid":
                    return MapCheckoutSession($"reconcile_{session.Id}_unpaid", PaymentWebhookEventType.CheckoutCompletedAwaitingPayment, session, "reconcile.checkout.unpaid");
                case "expired":
                    return MapCheckoutSession($"reconcile_{session.Id}_expired", PaymentWebhookEventType.CheckoutExpired, session, "reconcile.checkout.expired");
                default:
                    return null; // still open — nothing to say yet
            }
        }
        catch (StripeException ex)
        {
            // Same reasons as GetPaymentDetailsAsync: a placeholder reference that never reached
            // Stripe, or the API being unreachable. The caller keeps waiting for the webhook.
            logger.LogWarning(ex, "Could not read Stripe checkout session {SessionId} for reconciliation", sessionId);
            return null;
        }
    }

    /// <summary>The one place a Checkout Session becomes our event — the webhook and the live
    /// read above must agree on every field, or reconciliation would provision differently.</summary>
    private static PaymentWebhookEvent MapCheckoutSession(string eventId, PaymentWebhookEventType type, Session? session, string rawType = "", bool liveMode = false)
        => new(eventId, type, session?.Id)
        {
            RawType = rawType,
            ObjectId = session?.Id,
            LiveMode = liveMode,
            PaymentState = session?.PaymentStatus switch
            {
                "paid" => CheckoutPaymentState.Paid,
                "unpaid" => CheckoutPaymentState.Unpaid,
                "no_payment_required" => CheckoutPaymentState.NoPaymentRequired,
                _ => CheckoutPaymentState.Unknown,
            },
            AmountMinor = session?.AmountTotal,
            Currency = session?.Currency?.ToUpperInvariant(),
            PaymentIntentId = session?.PaymentIntentId,
            SubscriptionId = session?.SubscriptionId,
            CustomerId = session?.CustomerId,
            ClientReferenceId = session?.ClientReferenceId,
            // Present only when the session asked for them; absent on every other event.
            CustomerPhone = session?.CustomerDetails?.Phone,
            CompanyName = CustomFieldValue(session, CompanyFieldKey),
            TaxId = session?.CustomerDetails?.TaxIds?.FirstOrDefault()?.Value,
        };

    public PaymentWebhookEvent ConstructWebhookEvent(string requestBody, string signatureHeader)
    {
        // Throws StripeException on a bad/missing signature — the caller (WebhooksController) lets
        // that translate to a 400 so Stripe knows delivery failed, rather than swallowing it.
        // The signature (HMAC over the raw body, 5-minute tolerance) is what proves the event is
        // Stripe's. The API version the endpoint was registered under is not: a Stripe.net upgrade
        // or a dashboard change would otherwise turn every delivery into a 400 and stop money
        // landing. A mismatch is logged so it gets fixed, not enforced.
        var stripeEvent = EventUtility.ConstructEvent(requestBody, signatureHeader, options.WebhookSecret, throwOnApiVersionMismatch: false);
        if (stripeEvent.ApiVersion != StripeConfiguration.ApiVersion)
            logger.LogWarning("Stripe event {EventId} was built with API version {EventVersion}; this build expects {SdkVersion}.", stripeEvent.Id, stripeEvent.ApiVersion, StripeConfiguration.ApiVersion);
        return MapEvent(stripeEvent);
    }

    public async Task<PaymentWebhookEvent?> FetchWebhookEventAsync(string eventId, CancellationToken ct = default)
    {
        try
        {
            var stripeEvent = await eventService.GetAsync(eventId, cancellationToken: ct);
            return stripeEvent is null ? null : MapEvent(stripeEvent);
        }
        catch (StripeException ex)
        {
            logger.LogWarning(ex, "Could not fetch Stripe event {EventId}.", eventId);
            return null;
        }
    }

    /// <summary>
    /// Every event type the site reacts to or records. The rule that matters most is in the first
    /// case: a completed checkout is only <see cref="PaymentWebhookEventType.CheckoutCompleted"/>
    /// when the provider says the money is there. With a delayed payment method (bank transfer,
    /// direct debit) the session completes as "unpaid" and the outcome arrives days later as
    /// async_payment_succeeded / async_payment_failed — treating the first event as paid would
    /// hand out a ticket, a membership or a seat for money that may never arrive.
    /// </summary>
    private static PaymentWebhookEvent MapEvent(Event stripeEvent)
    {
        var id = stripeEvent.Id;
        var raw = stripeEvent.Type;
        var live = stripeEvent.Livemode;

        PaymentWebhookEvent Plain(PaymentWebhookEventType type) =>
            new(id, type, null) { RawType = raw, ObjectId = (stripeEvent.Data?.Object as IHasId)?.Id, LiveMode = live };

        switch (raw)
        {
            case "checkout.session.completed":
            {
                var session = stripeEvent.Data.Object as Session;
                var type = session?.PaymentStatus == "unpaid"
                    ? PaymentWebhookEventType.CheckoutCompletedAwaitingPayment
                    : PaymentWebhookEventType.CheckoutCompleted;
                return MapCheckoutSession(id, type, session, raw, live);
            }

            case "checkout.session.async_payment_succeeded":
                return MapCheckoutSession(id, PaymentWebhookEventType.CheckoutCompleted, stripeEvent.Data.Object as Session, raw, live);

            case "checkout.session.async_payment_failed":
                return MapCheckoutSession(id, PaymentWebhookEventType.CheckoutPaymentFailed, stripeEvent.Data.Object as Session, raw, live);

            case "checkout.session.expired":
                return MapCheckoutSession(id, PaymentWebhookEventType.CheckoutExpired, stripeEvent.Data.Object as Session, raw, live);

            case "invoice.paid":
            {
                // The first invoice of a subscription is paid inside checkout and already handled
                // by checkout.session.completed, so that one is excluded. Everything else that
                // resolves to a subscription — the regular cycle, a plan change through the portal,
                // an open invoice paid by hand — is money for a further period.
                if (stripeEvent.Data.Object is not Invoice invoice
                    || invoice.BillingReason == "subscription_create"
                    || invoice.Parent?.SubscriptionDetails?.SubscriptionId is not { } subscriptionId)
                {
                    return Plain(PaymentWebhookEventType.Unhandled);
                }

                // The period the invoice covers is on its line items; the invoice-level PeriodEnd
                // is the billing-usage window, which for a licensed subscription trails a cycle
                // behind. The latest line end is the date the member is now paid up to.
                var periodEnd = invoice.Lines?.Data?
                    .Select(l => l.Period?.End)
                    .Where(d => d is not null)
                    .Max();

                return new PaymentWebhookEvent(id, PaymentWebhookEventType.SubscriptionRenewed, null)
                {
                    RawType = raw,
                    ObjectId = invoice.Id,
                    LiveMode = live,
                    SubscriptionId = subscriptionId,
                    CustomerId = invoice.CustomerId,
                    InvoiceId = invoice.Id,
                    // What was actually charged — a coupon or a proration makes this differ from
                    // the plan's list price, and the ledger must say what the member paid.
                    AmountMinor = invoice.AmountPaid,
                    Currency = invoice.Currency?.ToUpperInvariant(),
                    CurrentPeriodEnd = UtcDates.ToOffset(periodEnd),
                    // The member's copy of this charge, on Stripe's own pages.
                    ReceiptUrl = invoice.HostedInvoiceUrl,
                };
            }

            case "invoice.payment_failed":
            case "invoice.payment_action_required":
            {
                if (stripeEvent.Data.Object is not Invoice invoice
                    || invoice.Parent?.SubscriptionDetails?.SubscriptionId is not { } subscriptionId)
                {
                    return Plain(PaymentWebhookEventType.Unhandled);
                }

                var type = raw == "invoice.payment_failed"
                    ? PaymentWebhookEventType.SubscriptionPaymentFailed
                    : PaymentWebhookEventType.InvoicePaymentActionRequired;

                return new PaymentWebhookEvent(id, type, null)
                {
                    RawType = raw,
                    ObjectId = invoice.Id,
                    LiveMode = live,
                    SubscriptionId = subscriptionId,
                    CustomerId = invoice.CustomerId,
                    InvoiceId = invoice.Id,
                    AmountMinor = invoice.AmountDue,
                    Currency = invoice.Currency?.ToUpperInvariant(),
                    HostedInvoiceUrl = invoice.HostedInvoiceUrl,
                    NextPaymentAttempt = UtcDates.ToOffset(invoice.NextPaymentAttempt),
                };
            }

            case "customer.subscription.deleted":
            case "customer.subscription.updated":
            {
                var subscription = stripeEvent.Data.Object as Subscription;
                var type = raw == "customer.subscription.deleted"
                    ? PaymentWebhookEventType.SubscriptionCancelled
                    : PaymentWebhookEventType.SubscriptionUpdated;
                DateTime? periodEnd = subscription?.Items?.Data is { Count: > 0 } items
                    ? items.Max(i => i.CurrentPeriodEnd)
                    : null;
                return new PaymentWebhookEvent(id, type, null)
                {
                    RawType = raw,
                    ObjectId = subscription?.Id,
                    LiveMode = live,
                    SubscriptionId = subscription?.Id,
                    CustomerId = subscription?.CustomerId,
                    CancelAtPeriodEnd = subscription?.CancelAtPeriodEnd,
                    CurrentPeriodEnd = UtcDates.ToOffset(periodEnd),
                    SubscriptionStatus = subscription?.Status,
                };
            }

            case "charge.succeeded":
            {
                // Recorded for one field: the receipt the buyer can open. Fulfilment is decided by
                // the Checkout events, which carry the session the order is keyed on.
                var settled = stripeEvent.Data.Object as Charge;
                return new PaymentWebhookEvent(id, PaymentWebhookEventType.ChargeSucceeded, null)
                {
                    RawType = raw,
                    ObjectId = settled?.Id,
                    LiveMode = live,
                    ChargeId = settled?.Id,
                    PaymentIntentId = settled?.PaymentIntentId,
                    CustomerId = settled?.CustomerId,
                    Currency = settled?.Currency?.ToUpperInvariant(),
                    ReceiptUrl = settled?.ReceiptUrl,
                };
            }

            case "charge.refunded":
            {
                var charge = stripeEvent.Data.Object as Charge;
                return new PaymentWebhookEvent(id, PaymentWebhookEventType.ChargeRefunded, null)
                {
                    RawType = raw,
                    ObjectId = charge?.Id,
                    LiveMode = live,
                    ChargeId = charge?.Id,
                    PaymentIntentId = charge?.PaymentIntentId,
                    CustomerId = charge?.CustomerId,
                    AmountMinor = charge?.Amount,
                    AmountRefundedMinor = charge?.AmountRefunded,
                    Currency = charge?.Currency?.ToUpperInvariant(),
                    ReceiptUrl = charge?.ReceiptUrl,
                };
            }

            case "charge.dispute.created":
            case "charge.dispute.closed":
            {
                var dispute = stripeEvent.Data.Object as Dispute;
                var type = raw == "charge.dispute.created" ? PaymentWebhookEventType.DisputeCreated : PaymentWebhookEventType.DisputeClosed;
                return new PaymentWebhookEvent(id, type, null)
                {
                    RawType = raw,
                    ObjectId = dispute?.Id,
                    LiveMode = live,
                    ChargeId = dispute?.ChargeId,
                    PaymentIntentId = dispute?.PaymentIntentId,
                    AmountMinor = dispute?.Amount,
                    Currency = dispute?.Currency?.ToUpperInvariant(),
                    // On closed: "won" (the money stays) or "lost" (it went back to the buyer).
                    DisputeStatus = dispute?.Status,
                };
            }

            case "payment_intent.processing":
            case "payment_intent.requires_action":
            case "payment_intent.payment_failed":
            case "payment_intent.canceled":
            {
                var intent = stripeEvent.Data.Object as PaymentIntent;
                var type = raw switch
                {
                    "payment_intent.processing" => PaymentWebhookEventType.PaymentIntentProcessing,
                    "payment_intent.requires_action" => PaymentWebhookEventType.PaymentIntentRequiresAction,
                    "payment_intent.payment_failed" => PaymentWebhookEventType.PaymentIntentFailed,
                    _ => PaymentWebhookEventType.PaymentIntentCanceled,
                };
                return new PaymentWebhookEvent(id, type, null)
                {
                    RawType = raw,
                    ObjectId = intent?.Id,
                    LiveMode = live,
                    PaymentIntentId = intent?.Id,
                    CustomerId = intent?.CustomerId,
                    AmountMinor = intent?.Amount,
                    Currency = intent?.Currency?.ToUpperInvariant(),
                };
            }

            // payment_intent.succeeded is deliberately not a fulfilment trigger: for a Checkout
            // integration the money signal is checkout.session.completed (paid) or
            // async_payment_succeeded, both of which carry the session the order is keyed on. The
            // intent event arrives alongside (in no guaranteed order) and would only add a second
            // path to the same outcome. It is recorded, and the intent's own status is what an
            // admin reads live on the payment page.
            default:
                return Plain(PaymentWebhookEventType.Unhandled);
        }
    }
}
