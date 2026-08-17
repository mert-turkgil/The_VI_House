using VIHouse.Entities.Commerce;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

public class AdminPaymentListItemViewModel
{
    public Guid Id { get; set; }
    public string? CustomerEmail { get; set; }
    public string ExperienceLabel { get; set; } = default!;
    public PaymentStatus Status { get; set; }
    public long AmountMinor { get; set; }
    public string Currency { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
}
