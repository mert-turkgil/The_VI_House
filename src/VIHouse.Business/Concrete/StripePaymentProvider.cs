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
    private readonly ILogger<StripePaymentProvider> logger;

    public StripePaymentProvider(IOptions<StripeOptions> options, ILogger<StripePaymentProvider> logger)
    {
        this.options = options.Value;
        this.logger = logger;
        StripeConfiguration.ApiKey = this.options.SecretKey;
        sessionService = new SessionService();
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
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = request.Currency,
                        UnitAmount = request.AmountMinor,
                        // An inline recurring price rather than a pre-created Price object: plans are
                        // admin-editable data here, so requiring someone to mirror every price change
                        // in the Stripe dashboard would guarantee the two drift apart.
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
                },
            ],
        };

        // Ticket checkouts expire to release the seat hold (CapacityService holds for 15 minutes);
        // Stripe rejects ExpiresAt on subscription sessions, and there's no inventory to free up for
        // a membership anyway, so the window only applies to one-off purchases.
        if (!isSubscription)
        {
            createOptions.ExpiresAt = DateTime.UtcNow.AddMinutes(30);
        }

        var session = await sessionService.CreateAsync(createOptions, cancellationToken: ct);
        return new CheckoutSessionResult(session.Id, session.Url);
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

    public PaymentWebhookEvent ConstructWebhookEvent(string requestBody, string signatureHeader)
    {
        // Throws StripeException on a bad/missing signature — the caller (WebhooksController) lets
        // that translate to a 400 so Stripe knows delivery failed, rather than swallowing it.
        var stripeEvent = EventUtility.ConstructEvent(requestBody, signatureHeader, options.WebhookSecret);

        var type = stripeEvent.Type switch
        {
            "checkout.session.completed" => PaymentWebhookEventType.CheckoutCompleted,
            "checkout.session.expired" => PaymentWebhookEventType.CheckoutExpired,
            _ => PaymentWebhookEventType.Unhandled,
        };

        var sessionId = stripeEvent.Data.Object is Session session ? session.Id : null;
        return new PaymentWebhookEvent(stripeEvent.Id, type, sessionId);
    }
}
