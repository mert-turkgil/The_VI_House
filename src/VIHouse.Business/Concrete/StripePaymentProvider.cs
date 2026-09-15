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
    private readonly ILogger<StripePaymentProvider> logger;

    public StripePaymentProvider(IOptions<StripeOptions> options, ILogger<StripePaymentProvider> logger)
    {
        this.options = options.Value;
        this.logger = logger;
        StripeConfiguration.ApiKey = this.options.SecretKey;
        sessionService = new SessionService();
        portalService = new Stripe.BillingPortal.SessionService();
        subscriptionService = new SubscriptionService();
        couponService = new CouponService();
    }

    public async Task<CheckoutSessionResult> CreateCheckoutSessionAsync(CreateCheckoutSessionRequest request, CancellationToken ct = default)
    {
        var isSubscription = request.Recurring is not null;

        var createOptions = new SessionCreateOptions
        {
            Mode = isSubscription ? "subscription" : "payment",
            CustomerEmail = request.CustomerEmail,
            ClientReferenceId = request.ClientReferenceId,
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            Metadata = new Dictionary<string, string>(request.Metadata),
            LineItems = [BuildLineItem(request)],
        };

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

        var session = await sessionService.CreateAsync(createOptions, cancellationToken: ct);
        return new CheckoutSessionResult(session.Id, session.Url, ToUtc(session.ExpiresAt));
    }

    private static DateTimeOffset? ToUtc(DateTime? value) =>
        value is null ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));

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
        catch (StripeException ex)
        {
            // Covers "no such session" (e.g. the pending_ placeholder ProviderReference from a
            // checkout that never reached Stripe — see PaymentService.InitiateCheckoutAsync) as
            // well as genuine network/API failures. The caller falls back to local DB fields.
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

    public PaymentWebhookEvent ConstructWebhookEvent(string requestBody, string signatureHeader)
    {
        // Throws StripeException on a bad/missing signature — the caller (WebhooksController) lets
        // that translate to a 400 so Stripe knows delivery failed, rather than swallowing it.
        var stripeEvent = EventUtility.ConstructEvent(requestBody, signatureHeader, options.WebhookSecret);

        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
            case "checkout.session.expired":
            {
                var session = stripeEvent.Data.Object as Session;
                var type = stripeEvent.Type == "checkout.session.completed"
                    ? PaymentWebhookEventType.CheckoutCompleted
                    : PaymentWebhookEventType.CheckoutExpired;

                return new PaymentWebhookEvent(stripeEvent.Id, type, session?.Id)
                {
                    SubscriptionId = session?.SubscriptionId,
                    CustomerId = session?.CustomerId,
                    ClientReferenceId = session?.ClientReferenceId,
                };
            }

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
                    return new PaymentWebhookEvent(stripeEvent.Id, PaymentWebhookEventType.Unhandled, null);
                }

                // The period the invoice covers is on its line items; the invoice-level PeriodEnd
                // is the billing-usage window, which for a licensed subscription trails a cycle
                // behind. The latest line end is the date the member is now paid up to.
                var periodEnd = invoice.Lines?.Data?
                    .Select(l => l.Period?.End)
                    .Where(d => d is not null)
                    .Max();

                return new PaymentWebhookEvent(stripeEvent.Id, PaymentWebhookEventType.SubscriptionRenewed, null)
                {
                    SubscriptionId = subscriptionId,
                    CustomerId = invoice.CustomerId,
                    InvoiceId = invoice.Id,
                    CurrentPeriodEnd = ToUtc(periodEnd),
                };
            }

            case "invoice.payment_failed":
            {
                if (stripeEvent.Data.Object is not Invoice invoice
                    || invoice.Parent?.SubscriptionDetails?.SubscriptionId is not { } subscriptionId)
                {
                    return new PaymentWebhookEvent(stripeEvent.Id, PaymentWebhookEventType.Unhandled, null);
                }

                return new PaymentWebhookEvent(stripeEvent.Id, PaymentWebhookEventType.SubscriptionPaymentFailed, null)
                {
                    SubscriptionId = subscriptionId,
                    CustomerId = invoice.CustomerId,
                    InvoiceId = invoice.Id,
                    HostedInvoiceUrl = invoice.HostedInvoiceUrl,
                    NextPaymentAttempt = ToUtc(invoice.NextPaymentAttempt),
                };
            }

            case "customer.subscription.deleted":
            {
                var subscription = stripeEvent.Data.Object as Subscription;
                return new PaymentWebhookEvent(stripeEvent.Id, PaymentWebhookEventType.SubscriptionCancelled, null)
                {
                    SubscriptionId = subscription?.Id,
                    CustomerId = subscription?.CustomerId,
                };
            }

            default:
                return new PaymentWebhookEvent(stripeEvent.Id, PaymentWebhookEventType.Unhandled, null);
        }
    }
}
