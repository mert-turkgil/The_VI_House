using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace VIHouse.WebUI.Areas.Identity.Pages.Account;

/// <summary>
/// Self-registration is disabled. Membership is granted through the application-approval funnel
/// (brief §25) — /apply and /join — never by someone finding this URL.
///
/// This file exists rather than being deleted, and that is the whole point. Deleting a scaffolded
/// Identity page does NOT remove the route: AddDefaultUI() supplies compiled Razor Pages for the
/// entire Identity UI, and a local page is only an *override* of one. Remove the override and the
/// framework's own Register page takes over — still reachable, still calling CreateAsync, and now
/// unstyled and unrate-limited as well. Verified: after deleting these files the route kept
/// answering 200 with the package's page.
///
/// So the override stays and refuses. NotFound rather than a redirect, so the endpoint gives away
/// nothing about whether registration exists elsewhere.
/// </summary>
[AllowAnonymous]
public class RegisterModel : PageModel
{
    public IActionResult OnGet() => NotFound();

    public IActionResult OnPost() => NotFound();
}
