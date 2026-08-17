namespace VIHouse.WebUI.Areas.Admin.ViewModels;

public class AdminCustomerListItemViewModel
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = default!;
    public List<string> Roles { get; set; } = [];
    public string? CompanyName { get; set; }
    public int ApplicationCount { get; set; }
    public int BookingCount { get; set; }
}
