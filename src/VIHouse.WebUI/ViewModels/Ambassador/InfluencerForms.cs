using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;
using VIHouse.Business.Abstract;
using VIHouse.Entities.Referrals;

namespace VIHouse.WebUI.ViewModels.Ambassador;

/// <summary>One row of the channels table. A row with no address is ignored, so the form can always
/// offer a few empty rows without JavaScript.</summary>
public class InfluencerChannelForm
{
    public SocialPlatform Platform { get; set; }

    [StringLength(300)]
    public string? Url { get; set; }

    [Range(0, int.MaxValue)]
    public int? Audience { get; set; }
}

/// <summary>
/// What the House shows of an influencer — bio, niche and channels. The same form on the admin page
/// (Marketing) and in the influencer's own area. The service validates it and answers with error
/// keys; <see cref="InfluencerFormErrors"/> puts them next to the right field.
/// </summary>
public class InfluencerProfileForm
{
    /// <summary>Rows the form always offers, filled or not.</summary>
    public const int Rows = 5;

    [StringLength(200)]
    public string? Niche { get; set; }

    [StringLength(1000)]
    public string? Bio { get; set; }

    public List<InfluencerChannelForm> Channels { get; set; } = [];

    public InfluencerProfileInput ToInput() => new()
    {
        Bio = Bio,
        Niche = Niche,
        Channels = [.. Channels
            .Where(c => !string.IsNullOrWhiteSpace(c.Url))
            .Select(c => new InfluencerChannelInput(c.Platform, c.Url!.Trim(), c.Audience))],
    };

    public static InfluencerProfileForm From(Entities.Referrals.Ambassador a) =>
        new InfluencerProfileForm
        {
            Niche = a.Niche,
            Bio = a.Bio,
            Channels = [.. a.Channels.Select(c => new InfluencerChannelForm { Platform = c.Platform, Url = c.Url, Audience = c.Audience })],
        }.Padded();

    /// <summary>Tops the table up to <see cref="Rows"/> (or one spare row past a longer list).</summary>
    public InfluencerProfileForm Padded()
    {
        var filled = Channels.Where(c => !string.IsNullOrWhiteSpace(c.Url)).ToList();
        // Eight is the most the service keeps (AmbassadorService.MaxChannels).
        var target = Math.Max(Rows, Math.Min(filled.Count + 1, 8));
        while (filled.Count < target) filled.Add(new InfluencerChannelForm());
        Channels = filled;
        return this;
    }
}

/// <summary>What the House pays against: legal name, billing address, tax id and bank. Only the
/// House enters it — on the invitation and, afterwards, by Finance on the influencer's page.</summary>
public class InfluencerPayoutForm
{
    [StringLength(100)] public string? LegalFirstName { get; set; }
    [StringLength(100)] public string? LegalLastName { get; set; }
    [StringLength(200)] public string? AddressLine1 { get; set; }
    [StringLength(200)] public string? AddressLine2 { get; set; }
    [StringLength(100)] public string? City { get; set; }
    [StringLength(20)] public string? PostalCode { get; set; }
    public string? Country { get; set; }
    [StringLength(40)] public string? TaxId { get; set; }
    [StringLength(150)] public string? AccountHolder { get; set; }
    [StringLength(50)] public string? Iban { get; set; }
    [StringLength(15)] public string? Bic { get; set; }

    public InfluencerPayoutInput ToInput() => new()
    {
        LegalFirstName = LegalFirstName ?? "",
        LegalLastName = LegalLastName ?? "",
        AddressLine1 = AddressLine1 ?? "",
        AddressLine2 = AddressLine2,
        City = City ?? "",
        PostalCode = PostalCode ?? "",
        Country = Country ?? "",
        TaxId = TaxId,
        AccountHolder = AccountHolder ?? "",
        Iban = Iban ?? "",
        Bic = Bic,
    };

    public static InfluencerPayoutForm From(Entities.Referrals.Ambassador a) => new()
    {
        LegalFirstName = a.LegalFirstName,
        LegalLastName = a.LegalLastName,
        AddressLine1 = a.BillingAddressLine1,
        AddressLine2 = a.BillingAddressLine2,
        City = a.BillingCity,
        PostalCode = a.BillingPostalCode,
        Country = a.BillingCountry,
        TaxId = a.TaxId,
        AccountHolder = a.PayoutAccountHolder,
        Iban = a.PayoutIban,
        Bic = a.PayoutBic,
    };
}

/// <summary>Service error keys ("Influencer.Error.{What}") placed next to the field they are about.</summary>
public static class InfluencerFormErrors
{
    private static readonly Dictionary<string, string> Fields = new()
    {
        ["LegalName"] = nameof(InfluencerPayoutForm.LegalFirstName),
        ["Address"] = nameof(InfluencerPayoutForm.AddressLine1),
        ["Country"] = nameof(InfluencerPayoutForm.Country),
        ["AccountHolder"] = nameof(InfluencerPayoutForm.AccountHolder),
        ["Iban"] = nameof(InfluencerPayoutForm.Iban),
        ["Bic"] = nameof(InfluencerPayoutForm.Bic),
        ["Channels"] = nameof(InfluencerProfileForm.Channels),
        ["Bio"] = nameof(InfluencerProfileForm.Bio),
    };

    public static void Apply(ModelStateDictionary modelState, string prefix, IEnumerable<string> errorKeys, IStringLocalizer loc)
    {
        foreach (var key in errorKeys)
        {
            var field = Fields.GetValueOrDefault(key[(key.LastIndexOf('.') + 1)..]);
            modelState.AddModelError(field is null ? string.Empty : $"{prefix}.{field}", loc[key].Value);
        }
    }
}
