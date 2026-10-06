using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace VIHouse.WebUI.Helpers;

/// <summary>
/// Puts each error from a failed Identity operation on the field it is about, instead of piling them
/// all into the form-level summary.
///
/// The scaffolded pages did <c>AddModelError(string.Empty, …)</c> for everything, so "add a symbol"
/// appeared at the top of the form, away from the password box it was about. Keyed by error code
/// (stable), not description (localised).
/// </summary>
public static class IdentityErrorMapping
{
    /// <param name="newPasswordKey">The ModelState key of the new-password input, e.g. "Input.Password"
    /// or "Input.NewPassword".</param>
    /// <param name="currentPasswordKey">The current-password input, where there is one (ChangePassword):
    /// that is where "the current password is wrong" belongs.</param>
    public static void AddTo(ModelStateDictionary modelState, IdentityResult result, string newPasswordKey, string? currentPasswordKey = null)
    {
        foreach (var error in result.Errors)
            modelState.AddModelError(KeyFor(error.Code, newPasswordKey, currentPasswordKey), error.Description);
    }

    /// <summary>True when the result failed because the emailed link (token) is expired, already used
    /// or not genuine. Pages show a "request a new link" state for this instead of a field error.</summary>
    public static bool IsInvalidToken(IdentityResult result) =>
        result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken));

    private static string KeyFor(string code, string newPasswordKey, string? currentPasswordKey) => code switch
    {
        nameof(IdentityErrorDescriber.PasswordMismatch) when currentPasswordKey is not null => currentPasswordKey,
        nameof(IdentityErrorDescriber.PasswordMismatch) => string.Empty,
        _ when code.StartsWith("Password", StringComparison.Ordinal) => newPasswordKey,
        _ => string.Empty,
    };
}
