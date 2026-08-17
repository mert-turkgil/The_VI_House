using VIHouse.Entities.Applications;
using VIHouse.Entities.Commerce;
using VIHouse.Entities.Users;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

public class AdminCustomerDetailViewModel
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = default!;
    public List<string> Roles { get; set; } = [];
    public Profile? Profile { get; set; }
    public List<Application> Applications { get; set; } = [];
    public List<Booking> Bookings { get; set; } = [];
}
