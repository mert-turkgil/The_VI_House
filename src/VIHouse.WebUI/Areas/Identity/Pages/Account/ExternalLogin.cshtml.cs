// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using VIHouse.DataAccess.Identity;
using VIHouse.WebUI.Helpers;

namespace VIHouse.WebUI.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ExternalLoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ExternalLoginModel> _logger;
        private readonly VIHouse.Business.Abstract.ISecurityAlertService _securityAlerts;

        public ExternalLoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            ILogger<ExternalLoginModel> logger,
            VIHouse.Business.Abstract.ISecurityAlertService securityAlerts)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _securityAlerts = securityAlerts;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ProviderDisplayName { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public string ReturnUrl { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string ErrorMessage { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [EmailAddress]
            public string Email { get; set; }
        }
        

        public IActionResult OnGet() => RedirectToPage("./Login");

        public IActionResult OnPost(string provider, string returnUrl = null)
        {
            // Request a redirect to the external login provider.
            var redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl = ReturnUrls.Safe(Url, returnUrl) });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return new ChallengeResult(provider, properties);
        }

        public async Task<IActionResult> OnGetCallbackAsync(string returnUrl = null, string remoteError = null)
        {
            returnUrl = ReturnUrls.Safe(Url, returnUrl);
            if (remoteError != null)
            {
                ErrorMessage = $"Error from external provider: {remoteError}";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = "Error loading external login information.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            // Sign in the user with this external login provider if the user already has a login.
            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
            if (result.Succeeded)
            {
                _logger.LogInformation("{Name} logged in with {LoginProvider} provider.", info.Principal.Identity.Name, info.LoginProvider);

                if (await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey) is { } signedInUser)
                {
                    signedInUser.LastLoginAt = DateTimeOffset.UtcNow;
                    await _userManager.UpdateAsync(signedInUser);
                    await _securityAlerts.RecordSignInAsync(signedInUser.Id, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
                }

                return LocalRedirect(returnUrl);
            }
            if (result.IsLockedOut)
            {
                return RedirectToPage("./Lockout");
            }
            else
            {
                // Invite-only membership (brief §25): unlike stock Identity scaffolding, an
                // unrecognized external login must never be offered the "create an account" form —
                // that would let any Google account holder self-register. Only a Google account
                // already linked to an existing local account (via Manage/ExternalLogins) can sign
                // in this way; anyone else is rejected here, same as the branches above.
                ErrorMessage = $"No VI House account is linked to this {info.ProviderDisplayName ?? info.LoginProvider} account. Sign in with your email and password, then link it under Security.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }
        }

        public async Task<IActionResult> OnPostConfirmationAsync(string returnUrl = null)
        {
            // Neutralized, not just OnGetCallbackAsync's branch above: this handler is reachable by
            // a direct POST to ?handler=Confirmation using nothing more than an antiforgery token
            // from any page on the site plus a completed Google OAuth round-trip for any Google
            // account — it doesn't depend on OnGetCallbackAsync ever having rendered this page's
            // form. Left unguarded, it would create a brand-new ApplicationUser (no role, no
            // approval) for literally any Google email, bypassing the application-approval funnel.
            returnUrl = ReturnUrls.Safe(Url, returnUrl);
            ErrorMessage = "No VI House account is linked to that account.";
            return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
        }
    }
}
