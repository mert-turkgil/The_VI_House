using Stripe;
using VIHouse.Business.Options;

namespace VIHouse.Business.Concrete;

/// <summary>
/// Builds the one Stripe client the process shares. Two things live here that used to be implicit:
/// the API key is bound to a client instance rather than the static StripeConfiguration.ApiKey
/// (which a scoped constructor was re-assigning on every request), and network retries are on.
/// Stripe.net adds an idempotency key to every POST it retries, so a request whose response was
/// lost is replayed, not repeated — a checkout session or a coupon is created once.
/// </summary>
public static class StripeClientFactory
{
    public const int MaxNetworkRetries = 2;

    public static IStripeClient Create(StripeOptions options) =>
        new StripeClient(
            string.IsNullOrWhiteSpace(options.SecretKey) ? "sk_unset" : options.SecretKey,
            httpClient: new SystemNetHttpClient(maxNetworkRetries: MaxNetworkRetries));
}
