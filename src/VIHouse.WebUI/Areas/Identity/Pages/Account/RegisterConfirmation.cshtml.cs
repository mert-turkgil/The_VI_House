using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace VIHouse.WebUI.Areas.Identity.Pages.Account;

/// <summary>
/// The confirmation step of a registration flow that no longer exists. Overridden to refuse for the
/// same reason as <see cref="RegisterModel"/>: deleting the file would hand the route back to the
/// Identity UI package's own page rather than removing it.
/// </summary>
[AllowAnonymous]
public class RegisterConfirmationModel : PageModel
{
    public IActionResult OnGet() => NotFound();
}
