using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;

namespace VIHouse.WebUI.Helpers;

/// <summary>
/// Enum values as people read them, in the admin's interface language: <c>Admin.Enum.{Type}.{Value}</c>
/// in SharedResource ("PaymentPending" → "Zahlung ausstehend"). A value with no entry yet reads as
/// its own name split into words ("Payment pending"), never as a raw key or a PascalCase identifier.
/// </summary>
public static partial class EnumLabels
{
    public static string Label<TEnum>(this IStringLocalizer loc, TEnum value) where TEnum : struct, Enum
    {
        var text = loc[$"Admin.Enum.{typeof(TEnum).Name}.{value}"];
        return text.ResourceNotFound ? Words(value.ToString()) : text.Value;
    }

    /// <summary>
    /// Replaces <c>Html.GetEnumSelectList</c>: the same numeric values (so binding and the select tag
    /// helper's selected state are unchanged), with translated text.
    /// </summary>
    public static IEnumerable<SelectListItem> Options<TEnum>(this IStringLocalizer loc) where TEnum : struct, Enum =>
        Enum.GetValues<TEnum>().Select(v => new SelectListItem(
            loc.Label(v), Convert.ToInt64(v, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)));

    private static string Words(string name)
    {
        var spaced = WordBoundary().Replace(name, " ");
        return spaced.Length == 0 ? spaced : spaced[0] + spaced[1..].ToLowerInvariant();
    }

    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])")]
    private static partial Regex WordBoundary();
}
