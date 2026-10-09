using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace VIHouse.WebUI.Localization;

/// <summary>
/// Gives every validation attribute that has no message of its own a resource key, so a bare
/// <c>[Required]</c> reads "Titel ist erforderlich." on a German form instead of the framework's
/// English "The Title field is required.". An attribute that already names its message — a key or
/// plain English — is left exactly as written.
///
/// The key goes through the same DataAnnotations localizer as every other message (SharedResource),
/// on the server and in the client-side rules alike; {0} is the field's display name, also localised.
/// </summary>
public sealed class DefaultValidationMessages : IValidationMetadataProvider
{
    public void CreateValidationMetadata(ValidationMetadataProviderContext context)
    {
        foreach (var attribute in context.ValidationMetadata.ValidatorMetadata.OfType<ValidationAttribute>())
        {
            if (attribute.ErrorMessage is not null || attribute.ErrorMessageResourceName is not null) continue;

            attribute.ErrorMessage = attribute switch
            {
                RequiredAttribute => "Validation.Required",
                StringLengthAttribute { MinimumLength: > 0 } => "Validation.StringLengthRange",
                StringLengthAttribute or MaxLengthAttribute => "Validation.StringLength",
                RangeAttribute => "Validation.Range",
                EmailAddressAttribute => "Validation.EmailAddress",
                PhoneAttribute => "Validation.Phone",
                UrlAttribute => "Validation.Url",
                _ => null,
            };
        }
    }
}
