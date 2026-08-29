using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;

namespace VIHouse.Business.Concrete;

/// <summary>
/// One form POST to whatever gateway is configured. See SmsOptions for why this isn't a provider SDK.
/// </summary>
public class HttpSmsSender(HttpClient http, IOptions<SmsOptions> options) : ISmsSender
{
    private readonly SmsOptions opts = options.Value;

    public bool IsConfigured => opts.IsConfigured;

    public async Task SendAsync(string toPhoneE164, string body, CancellationToken ct = default)
    {
        if (!opts.IsConfigured)
            throw new InvalidOperationException("No SMS gateway is configured — see SmsOptions.");

        // ExtraFields first, so the three fields that always matter can never be shadowed by a stray
        // config entry with the same name.
        var fields = new Dictionary<string, string>(opts.ExtraFields)
        {
            [opts.ToField] = toPhoneE164,
            [opts.BodyField] = body,
        };

        if (!string.IsNullOrWhiteSpace(opts.From))
            fields[opts.FromField] = opts.From;

        using var request = new HttpRequestMessage(HttpMethod.Post, opts.Endpoint)
        {
            Content = new FormUrlEncodedContent(fields),
        };

        if (string.Equals(opts.AuthScheme, "Basic", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(opts.Username))
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{opts.Username}:{opts.Password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }

        using var response = await http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            // The gateway's own words, kept verbatim into the log. "Not a mobile number",
            // "insufficient balance" and "unverified sender id" are all the same 400, and the body is
            // the only thing that tells them apart.
            var detail = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"SMS gateway returned {(int)response.StatusCode}: {Truncate(detail, 300)}");
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
