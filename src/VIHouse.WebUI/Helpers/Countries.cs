using System.Globalization;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace VIHouse.WebUI.Helpers;

/// <summary>
/// ISO 3166-1 alpha-2 countries for the forms that ask where someone is. Replaces the two-letter
/// text box that used to sit on /join and /apply: a code is what the database wants (brief §186),
/// but nobody should have to know theirs.
///
/// Names are the English short forms, then localised through <see cref="RegionInfo"/> where the
/// runtime knows the region — so a Turkish reader sees "Almanya" for DE without a 250-line
/// translation table per language. Regions the runtime does not know keep the English name.
/// </summary>
public static class Countries
{
    public static readonly IReadOnlyList<(string Code, string Name)> All =
    [
        ("AF", "Afghanistan"), ("AX", "Åland Islands"), ("AL", "Albania"), ("DZ", "Algeria"), ("AS", "American Samoa"),
        ("AD", "Andorra"), ("AO", "Angola"), ("AI", "Anguilla"), ("AQ", "Antarctica"), ("AG", "Antigua and Barbuda"),
        ("AR", "Argentina"), ("AM", "Armenia"), ("AW", "Aruba"), ("AU", "Australia"), ("AT", "Austria"),
        ("AZ", "Azerbaijan"), ("BS", "Bahamas"), ("BH", "Bahrain"), ("BD", "Bangladesh"), ("BB", "Barbados"),
        ("BY", "Belarus"), ("BE", "Belgium"), ("BZ", "Belize"), ("BJ", "Benin"), ("BM", "Bermuda"),
        ("BT", "Bhutan"), ("BO", "Bolivia"), ("BA", "Bosnia and Herzegovina"), ("BW", "Botswana"), ("BR", "Brazil"),
        ("IO", "British Indian Ocean Territory"), ("VG", "British Virgin Islands"), ("BN", "Brunei"), ("BG", "Bulgaria"), ("BF", "Burkina Faso"),
        ("BI", "Burundi"), ("KH", "Cambodia"), ("CM", "Cameroon"), ("CA", "Canada"), ("CV", "Cape Verde"),
        ("BQ", "Caribbean Netherlands"), ("KY", "Cayman Islands"), ("CF", "Central African Republic"), ("TD", "Chad"), ("CL", "Chile"),
        ("CN", "China"), ("CX", "Christmas Island"), ("CC", "Cocos (Keeling) Islands"), ("CO", "Colombia"), ("KM", "Comoros"),
        ("CG", "Congo - Brazzaville"), ("CD", "Congo - Kinshasa"), ("CK", "Cook Islands"), ("CR", "Costa Rica"), ("CI", "Côte d'Ivoire"),
        ("HR", "Croatia"), ("CU", "Cuba"), ("CW", "Curaçao"), ("CY", "Cyprus"), ("CZ", "Czechia"),
        ("DK", "Denmark"), ("DJ", "Djibouti"), ("DM", "Dominica"), ("DO", "Dominican Republic"), ("EC", "Ecuador"),
        ("EG", "Egypt"), ("SV", "El Salvador"), ("GQ", "Equatorial Guinea"), ("ER", "Eritrea"), ("EE", "Estonia"),
        ("SZ", "Eswatini"), ("ET", "Ethiopia"), ("FK", "Falkland Islands"), ("FO", "Faroe Islands"), ("FJ", "Fiji"),
        ("FI", "Finland"), ("FR", "France"), ("GF", "French Guiana"), ("PF", "French Polynesia"), ("TF", "French Southern Territories"),
        ("GA", "Gabon"), ("GM", "Gambia"), ("GE", "Georgia"), ("DE", "Germany"), ("GH", "Ghana"),
        ("GI", "Gibraltar"), ("GR", "Greece"), ("GL", "Greenland"), ("GD", "Grenada"), ("GP", "Guadeloupe"),
        ("GU", "Guam"), ("GT", "Guatemala"), ("GG", "Guernsey"), ("GN", "Guinea"), ("GW", "Guinea-Bissau"),
        ("GY", "Guyana"), ("HT", "Haiti"), ("HN", "Honduras"), ("HK", "Hong Kong"), ("HU", "Hungary"),
        ("IS", "Iceland"), ("IN", "India"), ("ID", "Indonesia"), ("IR", "Iran"), ("IQ", "Iraq"),
        ("IE", "Ireland"), ("IM", "Isle of Man"), ("IL", "Israel"), ("IT", "Italy"), ("JM", "Jamaica"),
        ("JP", "Japan"), ("JE", "Jersey"), ("JO", "Jordan"), ("KZ", "Kazakhstan"), ("KE", "Kenya"),
        ("KI", "Kiribati"), ("XK", "Kosovo"), ("KW", "Kuwait"), ("KG", "Kyrgyzstan"), ("LA", "Laos"),
        ("LV", "Latvia"), ("LB", "Lebanon"), ("LS", "Lesotho"), ("LR", "Liberia"), ("LY", "Libya"),
        ("LI", "Liechtenstein"), ("LT", "Lithuania"), ("LU", "Luxembourg"), ("MO", "Macao"), ("MG", "Madagascar"),
        ("MW", "Malawi"), ("MY", "Malaysia"), ("MV", "Maldives"), ("ML", "Mali"), ("MT", "Malta"),
        ("MH", "Marshall Islands"), ("MQ", "Martinique"), ("MR", "Mauritania"), ("MU", "Mauritius"), ("YT", "Mayotte"),
        ("MX", "Mexico"), ("FM", "Micronesia"), ("MD", "Moldova"), ("MC", "Monaco"), ("MN", "Mongolia"),
        ("ME", "Montenegro"), ("MS", "Montserrat"), ("MA", "Morocco"), ("MZ", "Mozambique"), ("MM", "Myanmar"),
        ("NA", "Namibia"), ("NR", "Nauru"), ("NP", "Nepal"), ("NL", "Netherlands"), ("NC", "New Caledonia"),
        ("NZ", "New Zealand"), ("NI", "Nicaragua"), ("NE", "Niger"), ("NG", "Nigeria"), ("NU", "Niue"),
        ("NF", "Norfolk Island"), ("KP", "North Korea"), ("MK", "North Macedonia"), ("MP", "Northern Mariana Islands"), ("NO", "Norway"),
        ("OM", "Oman"), ("PK", "Pakistan"), ("PW", "Palau"), ("PS", "Palestine"), ("PA", "Panama"),
        ("PG", "Papua New Guinea"), ("PY", "Paraguay"), ("PE", "Peru"), ("PH", "Philippines"), ("PN", "Pitcairn Islands"),
        ("PL", "Poland"), ("PT", "Portugal"), ("PR", "Puerto Rico"), ("QA", "Qatar"), ("RE", "Réunion"),
        ("RO", "Romania"), ("RU", "Russia"), ("RW", "Rwanda"), ("WS", "Samoa"), ("SM", "San Marino"),
        ("ST", "São Tomé and Príncipe"), ("SA", "Saudi Arabia"), ("SN", "Senegal"), ("RS", "Serbia"), ("SC", "Seychelles"),
        ("SL", "Sierra Leone"), ("SG", "Singapore"), ("SX", "Sint Maarten"), ("SK", "Slovakia"), ("SI", "Slovenia"),
        ("SB", "Solomon Islands"), ("SO", "Somalia"), ("ZA", "South Africa"), ("GS", "South Georgia and the South Sandwich Islands"), ("KR", "South Korea"),
        ("SS", "South Sudan"), ("ES", "Spain"), ("LK", "Sri Lanka"), ("BL", "St. Barthélemy"), ("SH", "St. Helena"),
        ("KN", "St. Kitts and Nevis"), ("LC", "St. Lucia"), ("MF", "St. Martin"), ("PM", "St. Pierre and Miquelon"), ("VC", "St. Vincent and the Grenadines"),
        ("SD", "Sudan"), ("SR", "Suriname"), ("SJ", "Svalbard and Jan Mayen"), ("SE", "Sweden"), ("CH", "Switzerland"),
        ("SY", "Syria"), ("TW", "Taiwan"), ("TJ", "Tajikistan"), ("TZ", "Tanzania"), ("TH", "Thailand"),
        ("TL", "Timor-Leste"), ("TG", "Togo"), ("TK", "Tokelau"), ("TO", "Tonga"), ("TT", "Trinidad and Tobago"),
        ("TN", "Tunisia"), ("TR", "Türkiye"), ("TM", "Turkmenistan"), ("TC", "Turks and Caicos Islands"), ("TV", "Tuvalu"),
        ("UM", "U.S. Outlying Islands"), ("VI", "U.S. Virgin Islands"), ("UG", "Uganda"), ("UA", "Ukraine"), ("AE", "United Arab Emirates"),
        ("GB", "United Kingdom"), ("US", "United States"), ("UY", "Uruguay"), ("UZ", "Uzbekistan"), ("VU", "Vanuatu"),
        ("VA", "Vatican City"), ("VE", "Venezuela"), ("VN", "Vietnam"), ("WF", "Wallis and Futuna"), ("EH", "Western Sahara"),
        ("YE", "Yemen"), ("ZM", "Zambia"), ("ZW", "Zimbabwe"),
    ];

    private static readonly HashSet<string> Codes = new(All.Select(c => c.Code), StringComparer.OrdinalIgnoreCase);

    public static bool IsValid(string? code) => code is not null && Codes.Contains(code);

    /// <summary>The name for a stored code in the current UI culture, or the code itself when it
    /// is not one we know — an old row is still a row.</summary>
    public static string NameFor(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "";
        var english = All.FirstOrDefault(c => c.Code.Equals(code, StringComparison.OrdinalIgnoreCase)).Name;
        return Localise(code, english ?? code);
    }

    /// <summary>
    /// Select-list items sorted by the localised name, with an empty first option so an unchosen
    /// country fails validation rather than defaulting to Afghanistan.
    /// </summary>
    public static List<SelectListItem> SelectList(string? selected, string placeholder)
    {
        var items = All
            .Select(c => new SelectListItem(Localise(c.Code, c.Name), c.Code,
                string.Equals(c.Code, selected, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(i => i.Text, StringComparer.Create(CultureInfo.CurrentUICulture, ignoreCase: true))
            .ToList();

        items.Insert(0, new SelectListItem(placeholder, "", string.IsNullOrEmpty(selected)) { Disabled = false });
        return items;
    }

    private static string Localise(string code, string fallback)
    {
        // Only worth it for a non-English reader; the English list above is already the short form.
        if (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en") return fallback;

        try
        {
            var region = new RegionInfo(code);
            return string.IsNullOrWhiteSpace(region.DisplayName) ? fallback : region.DisplayName;
        }
        catch (ArgumentException)
        {
            return fallback;
        }
    }
}
