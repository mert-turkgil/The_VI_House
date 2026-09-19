using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VIHouse.Business.Abstract;
using VIHouse.Entities.Commerce;
using VIHouse.WebUI.Areas.Admin.ViewModels;

namespace VIHouse.WebUI.Areas.Admin.Controllers;

/// <summary>
/// The unified payments screen: every transaction — experience ticket, session seat, membership,
/// renewal — from the one table the webhook moves, with its internal state, the provider's ids,
/// the account and the thing bought. Read-only: refunds and disputes are actioned in the Stripe
/// dashboard and arrive here as events. Money roles only (RolesFor.Money).
/// </summary>
[Authorize(Roles = AdminSections.RolesFor.Money)]
[Route("admin/payments")]
public class AdminPaymentsController(IPaymentReportingService reporting) : AdminControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(PaymentTransactionStatus? status, PaymentTransactionKind? kind, string? currency, string? q, CancellationToken ct)
    {
        var query = new PaymentTransactionQuery(status, kind, currency, q);
        var page = await reporting.ListTransactionsAsync(query, ct);
        return View(new AdminPaymentsIndexViewModel(page, query));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        // Older links (application, booking and user pages) carry a Payment / MembershipPayment
        // row id rather than the transaction's; both land on the same page.
        var transactionId = await reporting.ResolveTransactionIdAsync(id, ct);
        if (transactionId is null) return NotFound();

        var detail = await reporting.GetTransactionAsync(transactionId.Value, ct);
        if (detail is null) return NotFound();

        return View(detail);
    }
}
