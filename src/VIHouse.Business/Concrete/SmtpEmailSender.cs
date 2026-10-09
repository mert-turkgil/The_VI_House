using System.Net;
using System.Text.RegularExpressions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;

namespace VIHouse.Business.Concrete;

public partial class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    private readonly SmtpOptions opts = options.Value;

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        // Said plainly rather than left to MailKit's "host not found" on an empty string, so the
        // email log (Admin › Emails) tells whoever reads it what to fix.
        if (!opts.IsConfigured)
            throw new InvalidOperationException("Email is not configured: " + string.Join(" ", opts.Problems()));

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(opts.FromName, opts.FromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        if (!string.IsNullOrEmpty(opts.ReplyToEmail))
            message.ReplyTo.Add(MailboxAddress.Parse(opts.ReplyToEmail));
        message.Subject = subject;

        // multipart/alternative — the HTML plus a plain-text rendering of it. Filters score an
        // HTML-only message worse, some clients (and every screen reader in "text" mode) prefer
        // the text part, and the fallback costs nothing: it is derived from the HTML at send time.
        var builder = new BodyBuilder { HtmlBody = htmlBody, TextBody = ToPlainText(htmlBody) };
        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient { Timeout = Math.Max(5, opts.TimeoutSeconds) * 1000 };
        // Port decides the TLS handshake, not just the UseSsl flag: 465 is implicit TLS from the
        // first byte (SslOnConnect), while 587 negotiates plaintext-then-upgrade (StartTls). Mixing
        // these up doesn't fail loudly — it just hangs until the connection times out. UseSsl still
        // controls whether 587 upgrades at all, for a relay that genuinely wants plaintext (e.g. a
        // local dev catcher).
        var socketOptions = opts.Port == 465
            ? SecureSocketOptions.SslOnConnect
            : opts.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
        await client.ConnectAsync(opts.Host, opts.Port, socketOptions, ct);

        try
        {
            if (!string.IsNullOrEmpty(opts.Username))
                await client.AuthenticateAsync(opts.Username, opts.Password, ct);

            await client.SendAsync(message, ct);
        }
        catch (AuthenticationException ex)
        {
            throw new InvalidOperationException(
                $"The mail server ({opts.Host}) refused the sign-in for '{opts.Username}': {ex.Message} Check Smtp:Username / Smtp:Password.", ex);
        }
        catch (SmtpCommandException ex) when (ex.ErrorCode is SmtpErrorCode.SenderNotAccepted)
        {
            throw new InvalidOperationException(
                $"The mail server rejected the sender {opts.FromEmail} ({(int)ex.StatusCode} {ex.Message}). The From address must be the signed-in mailbox ({opts.Username}) or one of its aliases.", ex);
        }
        finally
        {
            if (client.IsConnected) await client.DisconnectAsync(true, CancellationToken.None);
        }
    }

    /// <summary>
    /// A readable text version of a template's HTML: hidden preheader and styles dropped, links
    /// written as "label (url)", block elements as line breaks, entities decoded, whitespace
    /// collapsed. Good enough to act on — every button's URL survives — without a second template
    /// per email to keep in step.
    /// </summary>
    public static string ToPlainText(string html)
    {
        var text = HiddenBlocks().Replace(html, "");
        text = Anchors().Replace(text, m =>
        {
            var href = m.Groups["href"].Value;
            var label = Tags().Replace(m.Groups["label"].Value, "").Trim();
            if (href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) return label;
            return string.IsNullOrEmpty(label) || label == href ? href : $"{label} ({href})";
        });
        text = LineBreaks().Replace(text, "\n");
        text = Tags().Replace(text, "");
        text = WebUtility.HtmlDecode(text);
        text = HorizontalSpace().Replace(text, " ");
        text = BlankLines().Replace(text, "\n\n");
        return string.Join('\n', text.Split('\n').Select(l => l.Trim())).Trim();
    }

    [GeneratedRegex(@"<(head|style|script)\b[^>]*>.*?</\1>|<div[^>]*display:none[^>]*>.*?</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex HiddenBlocks();

    [GeneratedRegex(@"<a\b[^>]*href=[""'](?<href>[^""']+)[""'][^>]*>(?<label>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Anchors();

    [GeneratedRegex(@"<br\s*/?>|</(p|tr|h[1-6]|li|div|table)>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreaks();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"[ \t ]+")]
    private static partial Regex HorizontalSpace();

    [GeneratedRegex(@"\n\s*\n(\s*\n)+")]
    private static partial Regex BlankLines();
}
