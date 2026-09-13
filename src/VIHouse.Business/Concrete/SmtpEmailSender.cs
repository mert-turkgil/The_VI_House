using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using VIHouse.Business.Abstract;
using VIHouse.Business.Options;

namespace VIHouse.Business.Concrete;

public class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    private readonly SmtpOptions opts = options.Value;

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(opts.FromName, opts.FromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        // Port decides the TLS handshake, not just the UseSsl flag: 465 is implicit TLS from the
        // first byte (SslOnConnect), while 587 negotiates plaintext-then-upgrade (StartTls). Mixing
        // these up doesn't fail loudly — it just hangs until the connection times out. UseSsl still
        // controls whether 587 upgrades at all, for a relay that genuinely wants plaintext (e.g. a
        // local dev catcher).
        var socketOptions = opts.Port == 465
            ? SecureSocketOptions.SslOnConnect
            : opts.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
        await client.ConnectAsync(opts.Host, opts.Port, socketOptions, ct);

        if (!string.IsNullOrEmpty(opts.Username))
            await client.AuthenticateAsync(opts.Username, opts.Password, ct);

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }
}
