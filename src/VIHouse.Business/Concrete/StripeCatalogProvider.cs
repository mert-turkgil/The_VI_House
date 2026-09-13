using Microsoft.Extensions.Options;
using Stripe;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;

namespace VIHouse.Business.Concrete;

/// <summary>
/// Mirrors membership plans into Stripe's Products and Prices.
///
/// Built on its own <see cref="StripeClient"/> rather than the static
/// <c>StripeConfiguration.ApiKey</c> that <see cref="StripePaymentProvider"/> sets: the static key
/// is process-wide state, and the catalogue is the one place an admin action reaches Stripe from a
/// request that has nothing to do with checkout. Keeping the client explicit means this class
/// works — or fails with a clear "not configured" — regardless of what else has run in the
/// process.
///
/// Two Stripe rules shape everything here. A Price is immutable, so a change of amount, currency
/// or interval is a new Price plus the retirement of the old one, never an edit. And a Product
/// that has ever had a Price cannot be deleted, only made inactive — which is why the interface
/// has an Archive and no Delete.
/// </summary>
public class StripeCatalogProvider : IPaymentCatalogProvider
{
    /// <summary>Metadata key carrying the local plan id on every mirrored product. This is how an
    /// import recognises a product that already has a row here.</summary>
    public const string PlanIdMetadataKey = "vihouse_plan_id";

    private readonly StripeOptions options;
    private readonly ProductService products;
    private readonly PriceService prices;

    public StripeCatalogProvider(IOptions<StripeOptions> options)
    {
        this.options = options.Value;

        // An empty key still constructs a client; the guard in EnsureConfigured turns the first
        // call into a readable error instead of a 401 from Stripe.
        var client = new StripeClient(string.IsNullOrWhiteSpace(this.options.SecretKey) ? "sk_unset" : this.options.SecretKey);
        products = new ProductService(client);
        prices = new PriceService(client);
    }

    public async Task<CatalogSyncResult> SyncPlanAsync(CatalogPlan plan, CancellationToken ct = default)
    {
        EnsureConfigured();

        var metadata = new Dictionary<string, string>();
        if (plan.LocalPlanId is { } localId)
            metadata[PlanIdMetadataKey] = localId.ToString();

        // --- First sync: one call creates the product and its default price together. ----------
        if (string.IsNullOrWhiteSpace(plan.ProductId))
        {
            var created = await products.CreateAsync(new ProductCreateOptions
            {
                Name = plan.Name,
                Description = NullIfBlank(plan.Description),
                Active = plan.Active,
                Metadata = metadata,
                DefaultPriceData = new ProductDefaultPriceDataOptions
                {
                    Currency = plan.Currency.ToLowerInvariant(),
                    UnitAmount = plan.AmountMinor,
                    Recurring = ToRecurringForProduct(plan.Recurring),
                },
            }, cancellationToken: ct);

            return new CatalogSyncResult(created.Id, created.DefaultPriceId, PriceReplaced: false);
        }

        // --- Subsequent syncs: update the product in place, re-issue the price only if needed. ---
        // Description is sent as an empty string rather than omitted when blank, because omitting
        // it leaves the old description standing at Stripe.
        var product = await products.UpdateAsync(plan.ProductId, new ProductUpdateOptions
        {
            Name = plan.Name,
            Description = plan.Description ?? string.Empty,
            Active = plan.Active,
            Metadata = metadata,
        }, cancellationToken: ct);

        Price? current = null;
        var currentId = plan.PriceId ?? product.DefaultPriceId;
        if (!string.IsNullOrWhiteSpace(currentId))
        {
            try
            {
                current = await prices.GetAsync(currentId, cancellationToken: ct);
            }
            catch (StripeException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // The price id we hold no longer resolves — deleted in test mode, or the ids were
                // copied between accounts. Treat it as "no current price" and issue a fresh one.
                current = null;
            }
        }

        if (current is not null && current.ProductId == plan.ProductId && PriceMatches(current, plan))
        {
            // The price is only ever deactivated by us, on archive — which also clears the
            // product's default. A re-activated plan needs the same price back on sale and back as
            // the default, rather than a duplicate.
            if (!current.Active && plan.Active)
                await prices.UpdateAsync(current.Id, new PriceUpdateOptions { Active = true }, cancellationToken: ct);

            if (product.DefaultPriceId != current.Id)
                await products.UpdateAsync(plan.ProductId, new ProductUpdateOptions { DefaultPrice = current.Id }, cancellationToken: ct);

            return new CatalogSyncResult(product.Id, current.Id, PriceReplaced: false);
        }

        var replacement = await prices.CreateAsync(new PriceCreateOptions
        {
            Product = plan.ProductId,
            Currency = plan.Currency.ToLowerInvariant(),
            UnitAmount = plan.AmountMinor,
            Recurring = ToRecurring(plan.Recurring),
            Active = plan.Active,
            Metadata = metadata,
        }, cancellationToken: ct);

        await products.UpdateAsync(plan.ProductId, new ProductUpdateOptions { DefaultPrice = replacement.Id }, cancellationToken: ct);

        // Retired, not deleted (prices cannot be deleted). Existing subscriptions on the old price
        // keep billing at what their holders agreed to — a price change is for new members.
        if (current is not null && current.Active)
            await prices.UpdateAsync(current.Id, new PriceUpdateOptions { Active = false }, cancellationToken: ct);

        return new CatalogSyncResult(product.Id, replacement.Id, PriceReplaced: current is not null);
    }

    public async Task ArchivePlanAsync(string productId, string? priceId, CancellationToken ct = default)
    {
        EnsureConfigured();

        // Stripe refuses to archive a price while it is its product's default price, so the
        // product is retired first with its default cleared (an empty string unsets it), and the
        // price follows. Order matters; the reverse fails with "cannot be archived because it is
        // the default price of its product".
        await products.UpdateAsync(productId, new ProductUpdateOptions { Active = false, DefaultPrice = "" }, cancellationToken: ct);

        if (!string.IsNullOrWhiteSpace(priceId))
            await prices.UpdateAsync(priceId, new PriceUpdateOptions { Active = false }, cancellationToken: ct);
    }

    public async Task<List<CatalogPlan>> ListPlansAsync(CancellationToken ct = default)
    {
        EnsureConfigured();

        var result = new List<CatalogPlan>();
        var listOptions = new ProductListOptions { Active = true, Limit = 100 };
        listOptions.AddExpand("data.default_price");

        await foreach (var product in products.ListAutoPagingAsync(listOptions, cancellationToken: ct))
        {
            var price = product.DefaultPrice;
            if (price is null || !price.Active || price.UnitAmount is null || price.BillingScheme != "per_unit")
                continue;

            RecurringInterval? recurring;
            if (price.Recurring is null)
                recurring = null;
            else if (price.Recurring.IntervalCount == 1 && price.Recurring.Interval == "month")
                recurring = RecurringInterval.Monthly;
            else if (price.Recurring.IntervalCount == 1 && price.Recurring.Interval == "year")
                recurring = RecurringInterval.Annual;
            else
                continue; // weekly, daily, every-N — not a shape a plan can hold

            Guid? localId = product.Metadata is not null
                && product.Metadata.TryGetValue(PlanIdMetadataKey, out var raw)
                && Guid.TryParse(raw, out var parsed)
                ? parsed
                : null;

            result.Add(new CatalogPlan(
                product.Id, price.Id, localId, product.Name, product.Description,
                price.UnitAmount.Value, price.Currency.ToUpperInvariant(), recurring, product.Active));
        }

        return result;
    }

    // --- Helpers --------------------------------------------------------------------------------

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(options.SecretKey))
            throw new InvalidOperationException("Stripe is not configured: Stripe:SecretKey is empty.");
    }

    private static bool PriceMatches(Price price, CatalogPlan plan)
    {
        if (price.UnitAmount != plan.AmountMinor) return false;
        if (!string.Equals(price.Currency, plan.Currency, StringComparison.OrdinalIgnoreCase)) return false;
        if (price.BillingScheme != "per_unit") return false;

        return plan.Recurring switch
        {
            null => price.Recurring is null,
            RecurringInterval.Monthly => price.Recurring is { Interval: "month", IntervalCount: 1 },
            RecurringInterval.Annual => price.Recurring is { Interval: "year", IntervalCount: 1 },
            _ => false,
        };
    }

    private static PriceRecurringOptions? ToRecurring(RecurringInterval? interval) => interval switch
    {
        RecurringInterval.Monthly => new PriceRecurringOptions { Interval = "month", IntervalCount = 1 },
        RecurringInterval.Annual => new PriceRecurringOptions { Interval = "year", IntervalCount = 1 },
        _ => null,
    };

    private static ProductDefaultPriceDataRecurringOptions? ToRecurringForProduct(RecurringInterval? interval) => interval switch
    {
        RecurringInterval.Monthly => new ProductDefaultPriceDataRecurringOptions { Interval = "month", IntervalCount = 1 },
        RecurringInterval.Annual => new ProductDefaultPriceDataRecurringOptions { Interval = "year", IntervalCount = 1 },
        _ => null,
    };

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
