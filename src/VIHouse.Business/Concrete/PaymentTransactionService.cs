using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using VIHouse.Business.Abstract;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Commerce;
using VIHouse.Entities.Membership;

namespace VIHouse.Business.Concrete;

public class PaymentTransactionService(
    IPaymentTransactionRepository transactions,
    IRepository<Membership> memberships,
    UserManager<ApplicationUser> userManager,
    ILogger<PaymentTransactionService> logger) : IPaymentTransactionService
{
    public async Task<PaymentTransaction> OpenAsync(PaymentTransactionKind kind, Guid? userId, string relatedEntityType, Guid relatedEntityId,
        long amountMinor, string currency, CancellationToken ct = default)
    {
        var transaction = new PaymentTransaction
        {
            Kind = kind,
            UserId = userId,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
            AmountMinor = amountMinor,
            Currency = currency.ToUpperInvariant(),
            Status = PaymentTransactionStatus.Pending,
        };
        await transactions.AddAsync(transaction, ct);
        await transactions.SaveChangesAsync(ct);
        return transaction;
    }

    public async Task AttachSessionAsync(Guid transactionId, string providerSessionId, string? providerCustomerId, CancellationToken ct = default)
    {
        var transaction = await transactions.GetByIdAsync(transactionId, ct)
            ?? throw new InvalidOperationException($"Payment transaction {transactionId} not found.");
        transaction.ProviderSessionId = providerSessionId;
        transaction.ProviderCustomerId ??= providerCustomerId;
        transaction.UpdatedAt = DateTimeOffset.UtcNow;
        await transactions.SaveChangesAsync(ct);
    }

    public async Task CancelOpenAsync(Guid transactionId, string reason, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (!await transactions.TryTransitionAsync(transactionId, [PaymentTransactionStatus.Pending], PaymentTransactionStatus.Canceled, now, null, ct))
            return;

        var transaction = await transactions.GetByIdAsync(transactionId, ct);
        if (transaction is null) return;
        transaction.FailureMessage = Text.Clip(reason, 1000);
        await transactions.SaveChangesAsync(ct);
    }

    public async Task AssignUserAsync(Guid transactionId, Guid userId, string? providerCustomerId, CancellationToken ct = default)
    {
        var transaction = await transactions.GetByIdAsync(transactionId, ct);
        if (transaction is null) return;
        transaction.UserId ??= userId;
        transaction.ProviderCustomerId ??= providerCustomerId;
        transaction.UpdatedAt = DateTimeOffset.UtcNow;
        await transactions.SaveChangesAsync(ct);
        await RememberCustomerAsync(userId, providerCustomerId ?? transaction.ProviderCustomerId);
    }

    public Task<PaymentTransaction?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        transactions.GetByIdAsync(id, ct);

    public Task<PaymentTransaction?> GetBySessionAsync(string providerSessionId, CancellationToken ct = default) =>
        transactions.GetBySessionAsync(providerSessionId, ct);

    public Task<PaymentTransaction?> GetByInvoiceAsync(string providerInvoiceId, CancellationToken ct = default) =>
        transactions.GetByInvoiceAsync(providerInvoiceId, ct);

    public Task<PaymentTransaction?> GetByPaymentIntentAsync(string providerPaymentIntentId, CancellationToken ct = default) =>
        transactions.GetByPaymentIntentAsync(providerPaymentIntentId, ct);

    public async Task<PaymentTransaction?> GetLatestOpenForAsync(string relatedEntityType, Guid relatedEntityId, CancellationToken ct = default)
    {
        var latest = await transactions.GetLatestForRelatedAsync(relatedEntityType, relatedEntityId, ct);
        return latest is { Status: PaymentTransactionStatus.Pending } ? latest : null;
    }

    public async Task<PaymentTransactionEventResult> ApplyAsync(PaymentWebhookEvent e, CancellationToken ct = default)
    {
        // Disputes: a flag, not a state.
        if (e.Type is PaymentWebhookEventType.DisputeCreated or PaymentWebhookEventType.DisputeClosed)
            return await FlagDisputeAsync(e, ct);

        // A settled charge: one field (the buyer's receipt), no state change.
        if (e.Type is PaymentWebhookEventType.ChargeSucceeded)
            return await RecordReceiptAsync(e, ct);

        var target = PaymentTransactionStateMachine.TargetFor(e);
        if (target is null)
            return new PaymentTransactionEventResult(PaymentTransactionEventOutcome.NotApplicable, null, null, null);

        var transaction = await FindOrOpenAsync(e, ct);
        if (transaction is null)
        {
            logger.LogWarning("Payment event {EventId} ({Type}) matches no transaction (session {Session}, intent {Intent}, invoice {Invoice}).",
                e.EventId, e.RawType, e.SessionId, e.PaymentIntentId, e.InvoiceId);
            return new PaymentTransactionEventResult(PaymentTransactionEventOutcome.NoTransaction, null, null, null);
        }

        var from = transaction.Status;
        if (from == target.Value && target != PaymentTransactionStatus.PartiallyRefunded)
            return new PaymentTransactionEventResult(PaymentTransactionEventOutcome.AlreadyApplied, transaction, from, target);

        if (!PaymentTransactionStateMachine.CanMove(from, target.Value))
        {
            logger.LogWarning("Payment event {EventId} ({Type}) asks transaction {TransactionId} to move {From} → {To}, which is not allowed; ignored.",
                e.EventId, e.RawType, transaction.Id, from, target);
            return new PaymentTransactionEventResult(PaymentTransactionEventOutcome.IllegalTransition, transaction, from, target);
        }

        var now = DateTimeOffset.UtcNow;
        var moved = await transactions.TryTransitionAsync(transaction.Id, PaymentTransactionStateMachine.SourcesOf(target.Value), target.Value, now, e.EventId, ct);
        if (!moved)
        {
            // Lost a race with another event for the same row; it is wherever that event put it.
            return new PaymentTransactionEventResult(PaymentTransactionEventOutcome.AlreadyApplied, transaction, from, target);
        }

        // The ids and figures the event carries — the provider's, which beat whatever the checkout
        // was opened with. Written after the atomic move, on the reloaded row.
        transaction.ProviderPaymentIntentId ??= e.PaymentIntentId;
        transaction.ProviderChargeId ??= e.ChargeId;
        transaction.ProviderCustomerId ??= e.CustomerId;
        transaction.ProviderSubscriptionId ??= e.SubscriptionId;
        transaction.ProviderInvoiceId ??= e.InvoiceId;
        transaction.ProviderReceiptUrl ??= e.ReceiptUrl;
        if (e.AmountMinor is { } amount && e.Type is not PaymentWebhookEventType.ChargeRefunded) transaction.AmountMinor = amount;
        if (e.Currency is { Length: 3 } currency) transaction.Currency = currency;
        // Stripe's amount_refunded is cumulative and events are not ordered: a late delivery of an
        // earlier, smaller refund must not wind the figure back.
        if (e.AmountRefundedMinor is { } refunded) transaction.AmountRefundedMinor = Math.Max(transaction.AmountRefundedMinor, refunded);
        if (target is PaymentTransactionStatus.Failed)
        {
            transaction.FailureCode = e.RawType;
            transaction.FailureMessage = e.NextPaymentAttempt is { } next
                ? $"The provider could not collect the payment; it will try again on {next:d MMM yyyy}."
                : "The provider could not collect the payment.";
        }
        transaction.UpdatedAt = now;
        await transactions.SaveChangesAsync(ct);

        if (transaction.UserId is { } userId)
            await RememberCustomerAsync(userId, e.CustomerId);

        logger.LogInformation("Transaction {TransactionId} ({Kind}) {From} → {To} on {EventId} ({Type}).",
            transaction.Id, transaction.Kind, from, target, e.EventId, e.RawType);
        return new PaymentTransactionEventResult(PaymentTransactionEventOutcome.Applied, transaction, from, target);
    }

    /// <summary>
    /// Which transaction an event is about. Checkout events carry the session id every transaction
    /// was opened with. Invoice events (renewals) name an invoice we have not seen before, so a
    /// MembershipRenewal transaction is opened for it on first sight — keyed on the invoice id,
    /// which is unique, so a second event about the same invoice finds the same row. Refunds and
    /// payment-intent events name the intent, learned when the checkout completed.
    /// </summary>
    private async Task<PaymentTransaction?> FindOrOpenAsync(PaymentWebhookEvent e, CancellationToken ct)
    {
        if (e.SessionId is { } sessionId)
            return await transactions.GetBySessionAsync(sessionId, ct);

        if (e.InvoiceId is { } invoiceId)
        {
            var existing = await transactions.GetByInvoiceAsync(invoiceId, ct);
            if (existing is not null) return existing;

            var membership = e.SubscriptionId is { } subscriptionId
                ? (await memberships.FindAsync(m => m.ProviderSubscriptionId == subscriptionId, ct)).OrderByDescending(m => m.StartAt).FirstOrDefault()
                : null;
            if (membership is null)
                logger.LogWarning("Invoice {InvoiceId} ({EventId}) names subscription {SubscriptionId}, which no membership holds; recording the money without an owner.",
                    invoiceId, e.EventId, e.SubscriptionId);

            var opened = new PaymentTransaction
            {
                Kind = PaymentTransactionKind.MembershipRenewal,
                UserId = membership?.UserId,
                RelatedEntityType = nameof(Membership),
                RelatedEntityId = membership?.Id ?? Guid.Empty,
                AmountMinor = e.AmountMinor ?? 0,
                Currency = e.Currency ?? "GBP",
                Status = PaymentTransactionStatus.Pending,
                ProviderInvoiceId = invoiceId,
                ProviderSubscriptionId = e.SubscriptionId,
                ProviderCustomerId = e.CustomerId,
            };
            await transactions.AddAsync(opened, ct);
            await transactions.SaveChangesAsync(ct);
            return opened;
        }

        if (e.PaymentIntentId is { } intentId)
            return await transactions.GetByPaymentIntentAsync(intentId, ct);

        return null;
    }

    /// <summary>The provider's hosted receipt for a charge, kept so the member can open their own
    /// copy from their account. Matched on the payment intent, which the checkout captured.</summary>
    private async Task<PaymentTransactionEventResult> RecordReceiptAsync(PaymentWebhookEvent e, CancellationToken ct)
    {
        var transaction = e.PaymentIntentId is { } intentId ? await transactions.GetByPaymentIntentAsync(intentId, ct) : null;
        if (transaction is null || e.ReceiptUrl is null)
            return new PaymentTransactionEventResult(PaymentTransactionEventOutcome.NoTransaction, null, null, null);

        if (transaction.ProviderReceiptUrl == e.ReceiptUrl)
            return new PaymentTransactionEventResult(PaymentTransactionEventOutcome.AlreadyApplied, transaction, transaction.Status, transaction.Status);

        transaction.ProviderReceiptUrl = e.ReceiptUrl;
        transaction.ProviderChargeId ??= e.ChargeId;
        transaction.UpdatedAt = DateTimeOffset.UtcNow;
        await transactions.SaveChangesAsync(ct);
        return new PaymentTransactionEventResult(PaymentTransactionEventOutcome.Applied, transaction, transaction.Status, transaction.Status);
    }

    private async Task<PaymentTransactionEventResult> FlagDisputeAsync(PaymentWebhookEvent e, CancellationToken ct)
    {
        var transaction = e.PaymentIntentId is { } intentId ? await transactions.GetByPaymentIntentAsync(intentId, ct) : null;
        if (transaction is null)
            return new PaymentTransactionEventResult(PaymentTransactionEventOutcome.NoTransaction, null, null, null);

        var now = DateTimeOffset.UtcNow;
        transaction.Disputed = e.Type == PaymentWebhookEventType.DisputeCreated;
        transaction.DisputedAt = transaction.Disputed ? now : transaction.DisputedAt;
        transaction.ProviderChargeId ??= e.ChargeId;
        transaction.LastEventId = e.EventId;
        transaction.UpdatedAt = now;
        await transactions.SaveChangesAsync(ct);
        logger.LogWarning("Transaction {TransactionId} dispute {State} on {EventId}.", transaction.Id, transaction.Disputed ? "opened" : "closed", e.EventId);
        return new PaymentTransactionEventResult(PaymentTransactionEventOutcome.Applied, transaction, transaction.Status, transaction.Status);
    }

    /// <summary>
    /// One customer per person at the provider. The id on a provider event is the provider's own
    /// word, so it is written even over a different id already on the account — that happens when
    /// the stored one went stale (deleted at the provider) and the checkout fell back to opening by
    /// email; see StripePaymentProvider.CreateCheckoutSessionAsync.
    /// </summary>
    private async Task RememberCustomerAsync(Guid userId, string? providerCustomerId)
    {
        if (string.IsNullOrWhiteSpace(providerCustomerId)) return;
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.ProviderCustomerId == providerCustomerId) return;

        // The id is unique across accounts. If another account already holds it — two people
        // sharing a provider customer, or an id reused by hand — the link is left alone and the
        // fact is written down. Never thrown: this is bookkeeping, and a payment that has landed
        // must not be rolled back (and retried for ever) over which account the customer hangs off.
        var owner = userManager.Users.FirstOrDefault(u => u.ProviderCustomerId == providerCustomerId);
        if (owner is not null && owner.Id != userId)
        {
            logger.LogWarning("Customer {CustomerId} is already linked to account {OwnerId}; leaving account {UserId} unlinked.", providerCustomerId, owner.Id, userId);
            return;
        }

        if (user.ProviderCustomerId is not null)
            logger.LogWarning("Account {UserId} paid as customer {New}; replacing the stored customer {Old}.", userId, providerCustomerId, user.ProviderCustomerId);
        user.ProviderCustomerId = providerCustomerId;

        var updated = await userManager.UpdateAsync(user);
        if (!updated.Succeeded)
            logger.LogWarning("Could not link customer {CustomerId} to account {UserId}: {Errors}", providerCustomerId, userId, string.Join(" ", updated.Errors.Select(e => e.Description)));
    }
}
