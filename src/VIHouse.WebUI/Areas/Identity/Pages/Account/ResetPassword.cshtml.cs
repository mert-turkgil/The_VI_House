// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using VIHouse.DataAccess.Identity;
using VIHouse.WebUI.Helpers;

namespace VIHouse.WebUI.Areas.Identity.Pages.Account
{
    // Brute-force protection, matching Login/LoginWith2fa/ForgotPassword. This page takes a
    // token and sets a password, so leaving it unlimited was the one gap in that set.
    [EnableRateLimiting("auth")]
    public class ResetPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ResetPasswordModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
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
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            // Messages are SharedResource keys (DataAnnotations localisation, Program.cs), so the
            // browser-side check speaks the page's language too.
            [Required(ErrorMessage = "Auth.Validation.EmailRequired")]
            [EmailAddress(ErrorMessage = "Auth.Validation.EmailInvalid")]
            [Display(Name = "Auth.Email")]
            public string Email { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            // MinimumLength matches options.Password.RequiredLength in Program.cs. It was 6, so the
            // browser happily accepted a password the server was then going to refuse.
            [Required(ErrorMessage = "Auth.Validation.PasswordRequired")]
            [StringLength(100, ErrorMessage = "Auth.Validation.PasswordLength", MinimumLength = 10)]
            [DataType(DataType.Password)]
            [Display(Name = "Auth.NewPassword")]
            public string Password { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required(ErrorMessage = "Auth.Validation.ConfirmRequired")]
            [DataType(DataType.Password)]
            [Display(Name = "Auth.ConfirmPassword")]
            [Compare("Password", ErrorMessage = "Auth.Validation.PasswordsDiffer")]
            public string ConfirmPassword { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            public string Code { get; set; }

        }

        /// <summary>
        /// True when the emailed link cannot be used: missing, mangled by a mail client, expired or
        /// already used. The page then explains that and offers a new link, instead of the bare 400
        /// (or, for a mangled code, the 500) the scaffold produced.
        /// </summary>
        public bool LinkInvalid { get; private set; }

        public IActionResult OnGet(string code = null)
        {
            Input = new InputModel();

            try
            {
                Input.Code = code is null ? null : Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            }
            catch (FormatException)
            {
                Input.Code = null;
            }

            LinkInvalid = string.IsNullOrEmpty(Input.Code);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // No code means the form was not reached through a reset link at all; "the Code field
            // is required" would be the least helpful thing to say about that.
            if (string.IsNullOrEmpty(Input?.Code))
            {
                ModelState.Clear();
                LinkInvalid = true;
                return Page();
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                return RedirectToPage("./ResetPasswordConfirmation");
            }

            // Read before the reset — afterwards it is always true.
            var hadNoPassword = !await _userManager.HasPasswordAsync(user);

            var result = await _userManager.ResetPasswordAsync(user, Input.Code, Input.Password);
            if (result.Succeeded)
            {
                // An account provisioned by a payment has no password and an unconfirmed email;
                // the setup link that led here was delivered to that inbox, which is the same
                // proof ConfirmEmail relies on. Narrowed to the first password on purpose: an
                // ordinary forgot-password on an unconfirmed address earns no free confirmation.
                if (hadNoPassword && !user.EmailConfirmed)
                {
                    user.EmailConfirmed = true;
                    await _userManager.UpdateAsync(user);
                }

                return RedirectToPage("./ResetPasswordConfirmation");
            }

            // An expired or reused link is not something the reader can fix by retyping, so it gets
            // its own state with a way to request a new link, not a line in the error list.
            if (IdentityErrorMapping.IsInvalidToken(result))
            {
                LinkInvalid = true;
                return Page();
            }

            // Each message on the field it is about ("add a symbol" under the password box).
            IdentityErrorMapping.AddTo(ModelState, result, newPasswordKey: "Input.Password");
            return Page();
        }
    }
}
