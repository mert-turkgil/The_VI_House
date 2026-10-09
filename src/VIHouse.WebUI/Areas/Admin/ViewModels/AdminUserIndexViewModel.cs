using System.ComponentModel.DataAnnotations;
using VIHouse.DataAccess.Abstract;

namespace VIHouse.WebUI.Areas.Admin.ViewModels;

public class AdminUserIndexViewModel
{
    public string? Query { get; set; }
    public string? Role { get; set; }
    public UserDirectoryPage Page { get; set; } = new([], 0, 1, 25);
}

/// <summary>The name and contact fields an admin may correct on someone's account. The email
/// address is not here on purpose: it is the sign-in, and changing it goes through the owner's own
/// confirm-the-new-address flow.</summary>
public class AdminEditUserViewModel
{
    [Required, StringLength(100)]
    public string FirstName { get; set; } = "";

    [Required, StringLength(100)]
    public string LastName { get; set; } = "";

    [Phone, StringLength(32)]
    public string? Phone { get; set; }

    [StringLength(2, MinimumLength = 2, ErrorMessage = "Country is a two-letter code, e.g. GB.")]
    public string? Country { get; set; }

    [StringLength(100)]
    public string? City { get; set; }
}
