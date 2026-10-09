using VIHouse.Business.Options;

namespace VIHouse.Tests;

public class SmtpOptionsTests
{
    private static SmtpOptions Good() => new()
    {
        Host = "smtp.hostinger.com", Port = 587, FromEmail = "info@thevihouse.com",
        Username = "info@thevihouse.com", Password = "secret",
    };

    [Fact]
    public void Complete_settings_have_no_problems() => Assert.Empty(Good().Problems());

    [Fact]
    public void Missing_host_and_sender_are_reported()
    {
        var problems = new SmtpOptions().Problems();
        Assert.Contains(problems, p => p.Contains("Smtp:Host"));
        Assert.Contains(problems, p => p.Contains("Smtp:FromEmail"));
        Assert.False(new SmtpOptions().IsConfigured);
    }

    [Fact]
    public void Sender_that_is_not_the_signed_in_mailbox_is_flagged()
    {
        var options = Good();
        options.Username = "concierge@thevihouse.com";
        Assert.Contains(options.Problems(), p => p.Contains("differs from Smtp:Username"));
    }

    [Fact]
    public void Username_without_password_is_flagged()
    {
        var options = Good();
        options.Password = "";
        Assert.Contains(options.Problems(), p => p.Contains("Smtp:Password is empty"));
    }

    [Fact]
    public void Local_catcher_needs_no_login()
    {
        var options = new SmtpOptions { Host = "localhost", Port = 25, UseSsl = false, FromEmail = "dev@localhost" };
        Assert.Empty(options.Problems());
    }
}
