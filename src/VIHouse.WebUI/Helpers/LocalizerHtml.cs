using System.Globalization;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.Localization;

namespace VIHouse.WebUI.Helpers;

/// <summary>
/// A translated sentence whose values carry markup — "Ambassador since <strong>12 May</strong>" —
/// without the markup living in the resource file. The sentence is ours ("Ambassador since {0}"),
/// so it is written as-is; each argument is HTML-encoded unless it is already
/// <see cref="IHtmlContent"/> built with <see cref="Strong"/> or <see cref="Code"/>, which encode
/// their own contents. A name, email or code can therefore never inject markup.
/// </summary>
public static class LocalizerHtml
{
    public static IHtmlContent Html(this IStringLocalizer loc, string key, params object?[] args)
    {
        var values = args.Select(a => (object)(a switch
        {
            null => string.Empty,
            IHtmlContent html => Render(html),
            IFormattable f => HtmlEncoder.Default.Encode(f.ToString(null, CultureInfo.CurrentCulture)),
            _ => HtmlEncoder.Default.Encode(a.ToString() ?? string.Empty)
        })).ToArray();
        return new HtmlString(string.Format(CultureInfo.CurrentCulture, loc[key].Value, values));
    }

    public static IHtmlContent Strong(object? value) => Wrap("strong", value);

    public static IHtmlContent Code(object? value) => Wrap("code", value);

    private static IHtmlContent Wrap(string tag, object? value) =>
        new HtmlString($"<{tag}>{HtmlEncoder.Default.Encode(Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty)}</{tag}>");

    private static string Render(IHtmlContent html)
    {
        using var writer = new StringWriter();
        html.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }
}
