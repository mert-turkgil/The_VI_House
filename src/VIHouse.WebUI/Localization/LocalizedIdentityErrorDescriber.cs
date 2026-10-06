using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;

namespace VIHouse.WebUI.Localization;

/// <summary>
/// The messages ASP.NET Core Identity produces when a password or token is refused, in the reader's
/// language and written as instructions rather than diagnoses ("Add at least one symbol, such as
/// ! @ # or ?" instead of "Passwords must have at least one non alphanumeric character").
///
/// Only the errors a member can meet on this site are overridden: the password rules, a mismatched
/// current password, an expired or reused link, a taken email address, and the catch-alls. The
/// codes are untouched, which is what <see cref="Helpers.IdentityErrorMapping"/> uses to put each
/// message on the field it is about.
///
/// Scoped like the framework's own describer, so <see cref="IStringLocalizer"/> resolves against the
/// request's culture at the moment the error is described.
/// </summary>
public class LocalizedIdentityErrorDescriber(IStringLocalizer<SharedResource> loc) : IdentityErrorDescriber
{
    private IdentityError Error(string code, string key, params object[] args) =>
        new() { Code = code, Description = loc[key, args].Value };

    public override IdentityError DefaultError() =>
        Error(nameof(DefaultError), "Identity.Error.Default");

    public override IdentityError ConcurrencyFailure() =>
        Error(nameof(ConcurrencyFailure), "Identity.Error.Concurrency");

    public override IdentityError PasswordMismatch() =>
        Error(nameof(PasswordMismatch), "Identity.Error.PasswordMismatch");

    public override IdentityError InvalidToken() =>
        Error(nameof(InvalidToken), "Identity.Error.InvalidToken");

    public override IdentityError InvalidEmail(string? email) =>
        Error(nameof(InvalidEmail), "Identity.Error.InvalidEmail", email ?? string.Empty);

    public override IdentityError DuplicateEmail(string email) =>
        Error(nameof(DuplicateEmail), "Identity.Error.DuplicateEmail", email);

    public override IdentityError DuplicateUserName(string userName) =>
        Error(nameof(DuplicateUserName), "Identity.Error.DuplicateEmail", userName);

    public override IdentityError UserAlreadyHasPassword() =>
        Error(nameof(UserAlreadyHasPassword), "Identity.Error.AlreadyHasPassword");

    public override IdentityError PasswordTooShort(int length) =>
        Error(nameof(PasswordTooShort), "Identity.Error.PasswordTooShort", length);

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) =>
        Error(nameof(PasswordRequiresUniqueChars), "Identity.Error.PasswordUniqueChars", uniqueChars);

    public override IdentityError PasswordRequiresNonAlphanumeric() =>
        Error(nameof(PasswordRequiresNonAlphanumeric), "Identity.Error.PasswordSymbol");

    public override IdentityError PasswordRequiresDigit() =>
        Error(nameof(PasswordRequiresDigit), "Identity.Error.PasswordDigit");

    public override IdentityError PasswordRequiresLower() =>
        Error(nameof(PasswordRequiresLower), "Identity.Error.PasswordLower");

    public override IdentityError PasswordRequiresUpper() =>
        Error(nameof(PasswordRequiresUpper), "Identity.Error.PasswordUpper");
}
