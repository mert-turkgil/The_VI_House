// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using VIHouse.DataAccess.Identity;

namespace VIHouse.WebUI.Areas.Identity.Pages.Account.Manage
{
    /// <summary>
    /// Account › Security › Sign-in providers. One card per provider the House supports — Google,
    /// Apple, LinkedIn — linked, ready to link, or not switched on yet (no keys configured in this
    /// environment). Linking is the only way a provider can ever sign someone in: ExternalLogin
    /// never creates an account, it only opens one that has linked that provider here.
    /// </summary>
    public class ExternalLoginsModel : PageModel
    {
        /// <summary>The providers shown, in this order, whether or not they are configured.</summary>
        public static readonly string[] Providers = ["Google", "Apple", "LinkedIn"];

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IStringLocalizer<SharedResource> _loc;

        public ExternalLoginsModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IUserStore<ApplicationUser> userStore,
            IStringLocalizer<SharedResource> loc)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _userStore = userStore;
            _loc = loc;
        }

        /// <param name="Name">The authentication scheme, e.g. "Apple".</param>
        /// <param name="Login">The linked login, or null when not linked.</param>
        /// <param name="Available">Whether the provider is configured in this environment.</param>
        public record ProviderCard(string Name, UserLoginInfo Login, bool Available);

        public IList<ProviderCard> Cards { get; set; }

        /// <summary>
        /// False while removing the link would lock the account out: no password and no other
        /// provider to sign in with.
        /// </summary>
        public bool ShowRemoveButton { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var logins = await _userManager.GetLoginsAsync(user);
            var schemes = (await _signInManager.GetExternalAuthenticationSchemesAsync()).Select(s => s.Name).ToList();
            Cards = Providers.Union(schemes, StringComparer.OrdinalIgnoreCase).Union(logins.Select(l => l.LoginProvider), StringComparer.OrdinalIgnoreCase)
                .Select(name => new ProviderCard(
                    name,
                    logins.FirstOrDefault(l => string.Equals(l.LoginProvider, name, StringComparison.OrdinalIgnoreCase)),
                    schemes.Contains(name, StringComparer.OrdinalIgnoreCase)))
                .ToList();

            string passwordHash = null;
            if (_userStore is IUserPasswordStore<ApplicationUser> userPasswordStore)
            {
                passwordHash = await userPasswordStore.GetPasswordHashAsync(user, HttpContext.RequestAborted);
            }

            ShowRemoveButton = passwordHash != null || logins.Count > 1;
            return Page();
        }

        public async Task<IActionResult> OnPostRemoveLoginAsync(string loginProvider, string providerKey)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var result = await _userManager.RemoveLoginAsync(user, loginProvider, providerKey);
            if (!result.Succeeded)
            {
                StatusMessage = "Error: " + _loc["Manage.Connections.NotRemoved", loginProvider].Value;
                return RedirectToPage();
            }

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = _loc["Manage.Connections.Removed", loginProvider].Value;
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostLinkLoginAsync(string provider)
        {
            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            if ((await _signInManager.GetExternalAuthenticationSchemesAsync()).All(s => s.Name != provider))
            {
                return RedirectToPage();
            }

            // Request a redirect to the external login provider to link a login for the current user
            var redirectUrl = Url.Page("./ExternalLogins", pageHandler: "LinkLoginCallback");
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl, _userManager.GetUserId(User));
            return new ChallengeResult(provider, properties);
        }

        public async Task<IActionResult> OnGetLinkLoginCallbackAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            // Null after a cancelled or broken round trip, which ExternalSignIn.Failed sends back here.
            var userId = await _userManager.GetUserIdAsync(user);
            var info = await _signInManager.GetExternalLoginInfoAsync(userId);
            if (info == null)
            {
                StatusMessage = "Error: " + _loc["Manage.Connections.Failed"].Value;
                return RedirectToPage();
            }

            var result = await _userManager.AddLoginAsync(user, info);
            if (!result.Succeeded)
            {
                StatusMessage = "Error: " + _loc["Manage.Connections.InUse", info.ProviderDisplayName ?? info.LoginProvider].Value;
                return RedirectToPage();
            }

            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            StatusMessage = _loc["Manage.Connections.Added", info.ProviderDisplayName ?? info.LoginProvider].Value;
            return RedirectToPage();
        }
    }
}
