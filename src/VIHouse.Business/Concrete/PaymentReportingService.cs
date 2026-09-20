using Microsoft.AspNetCore.Identity;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;
using VIHouse.DataAccess.Abstract;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Commerce;
using VIHouse.Entities.Experiences;
using VIHouse.Entities.Membership;
using VIHouse.Entities.Seminars;

namespace VIHouse.Business.Concrete;

public class PaymentReportingService(
    IPaymentTransactionRepository transactions,
    IPaymentRepository payments,
    IBookingRepository bookings,
    IExperienceRepository experiences,
    IMembershipPaymentRepository membershipPayments,
    IPendingJoinRepository pendingJoins,
    IRepository<Membership> memberships,
    IRepository<MembershipPlan> plans,
    ISeminarEnrollmentRepository enrollments,
    ISeminarRepository seminars,
    IWebhookEventRepository events,
    IPaymentProvider paymentProvider,
    UserManager<ApplicationUser> userManager) : IPaymentReportingService
{
    /// <summary>The money arrived on these. A refund is money that arrived and then went back;
    /// gross counts it, net does not.</summary>
    private static readonly PaymentTransactionStatus[] Settled =
        [PaymentTransactionStatus.Succeeded, PaymentTransactionStatus.PartiallyRefunded, PaymentTransactionStatus.Refunded];

    private static readonly PaymentTransactionStatus[] InFlight =
        [PaymentTransactionStatus.Pending, PaymentTransactionStatus.Processing, PaymentTransactionStatus.RequiresAction];

    public async Task<PaymentDashboardStats> GetDashboardStatsAsync(CancellationToken ct = default)
    {
        var all = await transactions.GetAllAsync(ct);
        var now = DateTimeOffset.UtcNow;

        var settled = all.Where(t => Settled.Contains(t.Status)).ToList();
        var collected = settled
            .GroupBy(t => t.Currency)
            .Select(g => new CurrencyMoney(g.Key, g.Sum(t => t.AmountMinor), g.Sum(t => t.AmountRefundedMinor), g.Count()))
            .OrderByDescending(c => c.NetMinor)
            .ToList();

        var thisMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var monthly = new Dictionary<string, List<MonthlyMoneyPoint>>();
        foreach (var currency in collected.Select(c => c.Currency))
        {
            var rows = settled.Where(t => t.Currency == currency).ToList();
            var points = new List<MonthlyMoneyPoint>();
            for (var offset = 11; offset >= 0; offset--)
            {
                var start = thisMonth.AddMonths(-offset);
                var end = start.AddMonths(1);
                // PaidAt is when the provider confirmed the money; CreatedAt is when a checkout was
                // opened, which for a bank transfer can be days earlier and for an abandoned one is
                // not revenue at all. Legacy rows backfilled without PaidAt fall back to CreatedAt.
                var inMonth = rows.Where(t => (t.PaidAt ?? t.CreatedAt) >= start && (t.PaidAt ?? t.CreatedAt) < end);
                points.Add(new MonthlyMoneyPoint(start.ToString("MMM yy"), inMonth.Sum(t => t.AmountMinor - t.AmountRefundedMinor)));
            }
            monthly[currency] = points;
        }

        var currentMembers = (await memberships.FindAsync(m =>
                (m.Status == MembershipStatus.Active || m.Status == MembershipStatus.PastDue) && (m.ExpiresAt == null || m.ExpiresAt > now), ct))
            .Select(m => m.UserId).Distinct().Count();

        return new PaymentDashboardStats
        {
            CountsByStatus = all.GroupBy(t => t.Status).ToDictionary(g => g.Key, g => g.Count()),
            CountsByKind = all.GroupBy(t => t.Kind).ToDictionary(g => g.Key, g => g.Count()),
            CollectedByCurrency = collected,
            CollectedByKindAndCurrency = settled
                .GroupBy(t => (t.Kind, t.Currency))
                .Select(g => new KindCurrencyMoney(g.Key.Kind, g.Key.Currency, g.Count(), g.Sum(t => t.AmountMinor), g.Sum(t => t.AmountRefundedMinor)))
                .OrderBy(k => k.Kind).ThenByDescending(k => k.NetMinor)
                .ToList(),
            OpenByCurrency = all.Where(t => InFlight.Contains(t.Status))
                .GroupBy(t => t.Currency)
                .Select(g => new CurrencyMoney(g.Key, g.Sum(t => t.AmountMinor), 0, g.Count()))
                .ToList(),
            MonthlyNetByCurrency = monthly,
            PrimaryCurrency = collected.FirstOrDefault()?.Currency,
            DisputedCount = all.Count(t => t.Disputed),
            MembershipSales = settled.Count(t => t.Kind == PaymentTransactionKind.Membership),
            MembershipRenewals = settled.Count(t => t.Kind == PaymentTransactionKind.MembershipRenewal),
            CurrentMembers = currentMembers,
        };
    }

    public async Task<PaymentTransactionPage> ListTransactionsAsync(PaymentTransactionQuery query, CancellationToken ct = default)
    {
        var all = await transactions.GetAllAsync(ct);
        var counts = all.GroupBy(t => t.Status).ToDictionary(g => g.Key, g => g.Count());
        var currencies = all.Select(t => t.Currency).Distinct().OrderBy(c => c).ToList();

        IEnumerable<PaymentTransaction> filtered = all;
        if (query.Status is { } status) filtered = filtered.Where(t => t.Status == status);
        if (query.Kind is { } kind) filtered = filtered.Where(t => t.Kind == kind);
        if (!string.IsNullOrWhiteSpace(query.Currency)) filtered = filtered.Where(t => t.Currency.Equals(query.Currency, StringComparison.OrdinalIgnoreCase));

        var rows = await ProjectAsync(filtered.OrderByDescending(t => t.CreatedAt).ToList(), ct);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(r =>
                    Has(r.UserEmail, term) || Has(r.UserName, term) || Has(r.RelatedLabel, term)
                    || Has(r.ProviderSessionId, term) || Has(r.ProviderPaymentIntentId, term) || Has(r.ProviderSubscriptionId, term)
                    || Has(r.ProviderInvoiceId, term) || Has(r.ProviderCustomerId, term) || r.Id.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return new PaymentTransactionPage(rows.Take(query.Take).ToList(), counts, rows.Count, currencies);

        static bool Has(string? value, string term) => value is not null && value.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<PaymentTransactionDetail?> GetTransactionAsync(Guid id, CancellationToken ct = default)
    {
        var transaction = await transactions.GetByIdAsync(id, ct);
        if (transaction is null) return null;

        var row = (await ProjectAsync([transaction], ct)).Single();

        var objectIds = new[] { transaction.ProviderSessionId, transaction.ProviderPaymentIntentId, transaction.ProviderChargeId, transaction.ProviderInvoiceId, transaction.ProviderSubscriptionId }
            .Where(s => !string.IsNullOrEmpty(s)).Select(s => s!).ToList();
        var timeline = objectIds.Count == 0 ? [] : await events.ListByObjectIdsAsync(objectIds, ct);

        var live = transaction.ProviderSessionId is { } sessionId
            ? await paymentProvider.GetPaymentDetailsAsync(sessionId, ct)
            : null;

        return new PaymentTransactionDetail { Transaction = row, Events = timeline, LiveDetails = live };
    }

    public async Task<List<MemberPaymentItem>> ListForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var mine = await transactions.FindAsync(t => t.UserId == userId, ct);
        if (mine.Count == 0) return [];

        var rows = await ProjectAsync(mine.OrderByDescending(t => t.CreatedAt).ToList(), ct);
        var receipts = mine.ToDictionary(t => t.Id, t => t.ProviderReceiptUrl);

        return rows.Select(r => new MemberPaymentItem
        {
            Id = r.Id,
            Kind = r.Kind,
            Status = r.Status,
            What = r.RelatedLabel,
            Link = MemberLink(r),
            AmountMinor = r.AmountMinor,
            AmountRefundedMinor = r.AmountRefundedMinor,
            Currency = r.Currency,
            CreatedAt = r.CreatedAt,
            PaidAt = r.PaidAt,
            RefundedAt = r.RefundedAt,
            ReceiptUrl = receipts.GetValueOrDefault(r.Id),
            // Our own id, shortened: enough for support to find the row, and nothing of the
            // provider's. A failure message is deliberately not carried — the provider's wording
            // is for the operator; the member is told what it means for them in the view.
            Reference = r.Id.ToString("N")[..8].ToUpperInvariant(),
        }).ToList();

        static string? MemberLink(PaymentTransactionListItem r) => r.RelatedEntityType switch
        {
            nameof(Payment) => r.RelatedReference is { } reference ? SiteUrls.Booking(reference) : SiteUrls.AccountBookings,
            nameof(SeminarEnrollment) => r.RelatedReference is { } slug ? SiteUrls.Session(slug) : SiteUrls.AccountSessions,
            nameof(MembershipPayment) or nameof(PendingJoin) or nameof(Membership) => SiteUrls.AccountMembership,
            _ => null,
        };
    }

    public async Task<Guid?> ResolveTransactionIdAsync(Guid rowId, CancellationToken ct = default)
    {
        if (await transactions.GetByIdAsync(rowId, ct) is not null) return rowId;
        if (await payments.GetByIdAsync(rowId, ct) is { TransactionId: { } fromPayment }) return fromPayment;
        if (await membershipPayments.GetByIdAsync(rowId, ct) is { TransactionId: { } fromMembership }) return fromMembership;
        if (await enrollments.GetByIdAsync(rowId, ct) is { TransactionId: { } fromEnrolment }) return fromEnrolment;
        return null;
    }

    /// <summary>Rows to list items: the account, and what was bought, looked up once per batch
    /// rather than once per row.</summary>
    private async Task<List<PaymentTransactionListItem>> ProjectAsync(List<PaymentTransaction> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return [];

        var userIds = rows.Where(t => t.UserId is not null).Select(t => t.UserId!.Value).Distinct().ToList();
        var users = userIds.Count == 0
            ? new Dictionary<Guid, ApplicationUser>()
            : userManager.Users.Where(u => userIds.Contains(u.Id)).ToDictionary(u => u.Id);

        // --- Experience: Payment → Experience city + booking reference
        var paymentIds = rows.Where(t => t.RelatedEntityType == nameof(Payment)).Select(t => t.RelatedEntityId).Distinct().ToList();
        var paymentRows = paymentIds.Count == 0 ? [] : await payments.FindAsync(p => paymentIds.Contains(p.Id), ct);
        var experienceIds = paymentRows.Select(p => p.ExperienceId).Distinct().ToList();
        var experienceRows = experienceIds.Count == 0 ? new Dictionary<Guid, Experience>() : (await experiences.FindAsync(e => experienceIds.Contains(e.Id), ct)).ToDictionary(e => e.Id);
        var bookingIds = paymentRows.Where(p => p.BookingId is not null).Select(p => p.BookingId!.Value).Distinct().ToList();
        var bookingRows = bookingIds.Count == 0 ? new Dictionary<Guid, Booking>() : (await bookings.FindAsync(b => bookingIds.Contains(b.Id), ct)).ToDictionary(b => b.Id);
        var paymentsById = paymentRows.ToDictionary(p => p.Id);

        // --- Session: SeminarEnrollment → seminar title
        var enrolmentIds = rows.Where(t => t.RelatedEntityType == nameof(SeminarEnrollment)).Select(t => t.RelatedEntityId).Distinct().ToList();
        var enrolmentRows = enrolmentIds.Count == 0 ? new Dictionary<Guid, SeminarEnrollment>() : (await enrollments.FindAsync(e => enrolmentIds.Contains(e.Id), ct)).ToDictionary(e => e.Id);
        var seminarIds = enrolmentRows.Values.Select(e => e.SeminarId).Distinct().ToList();
        var seminarRows = seminarIds.Count == 0 ? new Dictionary<Guid, Seminar>() : (await seminars.GetByIdsAsync(seminarIds, ct)).ToDictionary(s => s.Id);

        // --- Membership: MembershipPayment / PendingJoin / Membership → plan name
        var membershipPaymentIds = rows.Where(t => t.RelatedEntityType == nameof(MembershipPayment)).Select(t => t.RelatedEntityId).Distinct().ToList();
        var membershipPaymentRows = membershipPaymentIds.Count == 0 ? new Dictionary<Guid, MembershipPayment>() : (await membershipPayments.FindAsync(p => membershipPaymentIds.Contains(p.Id), ct)).ToDictionary(p => p.Id);
        var joinIds = rows.Where(t => t.RelatedEntityType == nameof(PendingJoin)).Select(t => t.RelatedEntityId).Distinct().ToList();
        var joinRows = joinIds.Count == 0 ? new Dictionary<Guid, PendingJoin>() : (await pendingJoins.FindAsync(j => joinIds.Contains(j.Id), ct)).ToDictionary(j => j.Id);
        var membershipIds = rows.Where(t => t.RelatedEntityType == nameof(Membership)).Select(t => t.RelatedEntityId)
            .Concat(membershipPaymentRows.Values.Where(p => p.MembershipId is not null).Select(p => p.MembershipId!.Value)).Distinct().ToList();
        var membershipRows = membershipIds.Count == 0 ? new Dictionary<Guid, Membership>() : (await memberships.FindAsync(m => membershipIds.Contains(m.Id), ct)).ToDictionary(m => m.Id);
        var planIds = membershipPaymentRows.Values.Select(p => p.PlanId).Concat(joinRows.Values.Select(j => j.PlanId)).Concat(membershipRows.Values.Select(m => m.PlanId)).Distinct().ToList();
        var planRows = planIds.Count == 0 ? new Dictionary<Guid, MembershipPlan>() : (await plans.FindAsync(p => planIds.Contains(p.Id), ct)).ToDictionary(p => p.Id);

        return rows.Select(t =>
        {
            var user = t.UserId is { } uid ? users.GetValueOrDefault(uid) : null;
            var (label, path) = Describe(t);
            return new PaymentTransactionListItem
            {
                Id = t.Id,
                Kind = t.Kind,
                Status = t.Status,
                AmountMinor = t.AmountMinor,
                AmountRefundedMinor = t.AmountRefundedMinor,
                Currency = t.Currency,
                UserId = t.UserId,
                UserEmail = user?.Email ?? (t.RelatedEntityType == nameof(PendingJoin) ? joinRows.GetValueOrDefault(t.RelatedEntityId)?.Email : null),
                UserName = user is null ? null : $"{user.FirstName} {user.LastName}".Trim(),
                RelatedLabel = label,
                RelatedEntityType = t.RelatedEntityType,
                RelatedEntityId = t.RelatedEntityId,
                RelatedAdminPath = path,
                RelatedReference = Reference(t),
                ProviderSessionId = t.ProviderSessionId,
                ProviderPaymentIntentId = t.ProviderPaymentIntentId,
                ProviderSubscriptionId = t.ProviderSubscriptionId,
                ProviderInvoiceId = t.ProviderInvoiceId,
                ProviderCustomerId = t.ProviderCustomerId,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                PaidAt = t.PaidAt,
                RefundedAt = t.RefundedAt,
                FailureCode = t.FailureCode,
                FailureMessage = t.FailureMessage,
                Disputed = t.Disputed,
                LastEventId = t.LastEventId,
            };
        }).ToList();

        // The member-facing handle for what was bought: a booking reference, a session slug.
        string? Reference(PaymentTransaction t) => t.RelatedEntityType switch
        {
            nameof(Payment) => paymentsById.GetValueOrDefault(t.RelatedEntityId)?.BookingId is { } bid
                ? bookingRows.GetValueOrDefault(bid)?.BookingReference : null,
            nameof(SeminarEnrollment) => enrolmentRows.GetValueOrDefault(t.RelatedEntityId) is { } enrolment
                ? seminarRows.GetValueOrDefault(enrolment.SeminarId)?.Slug : null,
            _ => null,
        };

        (string Label, string? Path) Describe(PaymentTransaction t)
        {
            switch (t.RelatedEntityType)
            {
                case nameof(Payment):
                {
                    var payment = paymentsById.GetValueOrDefault(t.RelatedEntityId);
                    var experience = payment is null ? null : experienceRows.GetValueOrDefault(payment.ExperienceId);
                    var booking = payment?.BookingId is { } bid ? bookingRows.GetValueOrDefault(bid) : null;
                    var label = experience is null ? "Experience ticket" : $"The VI House — {experience.City}";
                    if (booking is not null) label += $" · {booking.BookingReference}";
                    return (label, booking is not null ? $"/admin/bookings/{booking.Id}" : experience is not null ? $"/admin/experiences/{experience.Id}" : null);
                }
                case nameof(SeminarEnrollment):
                {
                    var enrolment = enrolmentRows.GetValueOrDefault(t.RelatedEntityId);
                    var seminar = enrolment is null ? null : seminarRows.GetValueOrDefault(enrolment.SeminarId);
                    var title = seminar is null ? "Session" : SeminarContent.Title(seminar, SiteCultures.Default);
                    return ($"Session · {title}", seminar is null ? null : $"/admin/sessions/{seminar.Id}/enrolments");
                }
                case nameof(MembershipPayment):
                {
                    var payment = membershipPaymentRows.GetValueOrDefault(t.RelatedEntityId);
                    var plan = payment is null ? null : planRows.GetValueOrDefault(payment.PlanId);
                    return ($"Membership · {plan?.Name ?? "plan"}", plan is null ? null : $"/admin/plans/{plan.Id}");
                }
                case nameof(PendingJoin):
                {
                    var join = joinRows.GetValueOrDefault(t.RelatedEntityId);
                    var plan = join is null ? null : planRows.GetValueOrDefault(join.PlanId);
                    return ($"Membership (join) · {plan?.Name ?? "plan"}", plan is null ? null : $"/admin/plans/{plan.Id}");
                }
                case nameof(Membership):
                {
                    var membership = membershipRows.GetValueOrDefault(t.RelatedEntityId);
                    var plan = membership is null ? null : planRows.GetValueOrDefault(membership.PlanId);
                    return ($"Renewal · {plan?.Name ?? "membership"}", plan is null ? null : $"/admin/plans/{plan.Id}");
                }
                default:
                    return (t.Kind.ToString(), null);
            }
        }
    }
}
