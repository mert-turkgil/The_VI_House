using VIHouse.Business.Abstract;
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using VIHouse.DataAccess.Identity;
using VIHouse.WebUI.Helpers;

namespace VIHouse.WebUI.Areas.Identity.Pages.Account.Manage
{
    public class ChangePasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<ChangePasswordModel> _logger;
        private readonly ISecurityAlertService _securityAlerts;
        private readonly IStringLocalizer<SharedResource> _loc;

        public ChangePasswordModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<ChangePasswordModel> logger,
            ISecurityAlertService securityAlerts,
            IStringLocalizer<SharedResource> loc)
        {
            _loc = loc;
            _userManager = userManager;
            _signInManager = signInManager;
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
        [TempData]
        public string StatusMessage { get; set; }

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
            [Required(ErrorMessage = "Auth.Validation.CurrentRequired")]
            [DataType(DataType.Password)]
            [Display(Name = "Manage.Password.Current")]
            public string OldPassword { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required(ErrorMessage = "Auth.Validation.PasswordRequired")]
            [StringLength(100, ErrorMessage = "Auth.Validation.PasswordLength", MinimumLength = 10)] // minimum matches Program.cs
            [DataType(DataType.Password)]
            [Display(Name = "Auth.NewPassword")]
            public string NewPassword { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [DataType(DataType.Password)]
            [Required(ErrorMessage = "Auth.Validation.ConfirmRequired")]
            [Display(Name = "Auth.ConfirmPassword")]
            [Compare("NewPassword", ErrorMessage = "Auth.Validation.PasswordsDiffer")]
            public string ConfirmPassword { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var hasPassword = await _userManager.HasPasswordAsync(user);
            if (!hasPassword)
            {
                return RedirectToPage("./SetPassword");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var changePasswordResult = await _userManager.ChangePasswordAsync(user, Input.OldPassword, Input.NewPassword);
            if (!changePasswordResult.Succeeded)
            {
                // A wrong current password goes on the current-password box, rule failures on the new one.
                IdentityErrorMapping.AddTo(ModelState, changePasswordResult, "Input.NewPassword", "Input.OldPassword");
                return Page();
            }

            await _signInManager.RefreshSignInAsync(user);
            await _securityAlerts.PasswordChangedAsync(user.Id, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString());
            _logger.LogInformation("User changed their password successfully.");
            StatusMessage = _loc["Manage.Password.Changed"].Value;

            return RedirectToPage();
        }
    }
}
