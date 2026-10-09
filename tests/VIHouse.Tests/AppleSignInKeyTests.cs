using System.Security.Cryptography;
using VIHouse.Business.Options;

namespace VIHouse.Tests;

public class AppleSignInKeyTests
{
    private static readonly string Pem = ECDsa.Create(ECCurve.NamedCurves.nistP256).ExportPkcs8PrivateKeyPem();

    public static TheoryData<string> Pastes() => new()
    {
        Pem,                                                     // the .p8 file as downloaded
        Pem.Replace("\n", "\\n"),                                // one line with \n escapes (JSON / CI secret)
        string.Concat(Pem.Split('\n').Where(l => !l.StartsWith("-----"))), // the base64 body only
        "  " + Pem.Replace("\n", "\r\n") + "  ",                 // Windows line endings, stray spaces
    };

    [Theory]
    [MemberData(nameof(Pastes))]
    public void Any_paste_imports_as_the_same_key(string pasted)
    {
        var normalised = AppleSignInKey.Normalise(pasted);
        Assert.NotNull(normalised);

        using var key = ECDsa.Create();
        key.ImportFromPem(normalised);   // what the Apple handler does
        using var original = ECDsa.Create();
        original.ImportFromPem(Pem);
        Assert.Equal(original.ExportParameters(false).Q.X, key.ExportParameters(false).Q.X);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("-----BEGIN PRIVATE KEY-----\n-----END PRIVATE KEY-----")]
    public void Nothing_usable_is_null(string? pasted) => Assert.Null(AppleSignInKey.Normalise(pasted));
}
