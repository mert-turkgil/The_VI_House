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
        var createOptions = new SessionCreateOptions
        {
            Mode = "payment",
            CustomerEmail = request.CustomerEmail,
            ClientReferenceId = request.ClientReferenceId,
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            Metadata = new Dictionary<string, string>(request.Metadata),
            ExpiresAt = DateTime.UtcNow.AddMinutes(30), // matches CapacityService's 15-min hold plus buffer
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = request.Currency,
                        UnitAmount = request.AmountMinor,
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = request.ProductName,
                            Description = request.ProductDescription,
                        },
                    },
                },
            ],
        };

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
