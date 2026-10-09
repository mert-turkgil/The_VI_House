namespace VIHouse.Business.Options;

/// <summary>Configuration helper for Sign in with Apple (see Program.cs).</summary>
public static class AppleSignInKey
{
    /// <summary>
    /// Accepts the .p8 key however it was pasted into a secret store — the whole file with its
    /// BEGIN/END lines, the base64 body alone, or one line with literal "\n" escapes (what a JSON
    /// settings file or a CI secret usually ends up holding) — and returns a well-formed PKCS #8
    /// PEM, which is what the Apple handler imports. Null when nothing usable was configured.
    /// </summary>
    public static string? Normalise(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured)) return null;

        var text = configured.Replace("\\n", "\n").Replace("\r", "");
        var body = string.Concat(text.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith("-----", StringComparison.Ordinal)))
            .Replace(" ", "");
        if (body.Length == 0) return null;

        var lines = Enumerable.Range(0, (body.Length + 63) / 64)
            .Select(i => body.Substring(i * 64, Math.Min(64, body.Length - i * 64)));
        return "-----BEGIN PRIVATE KEY-----\n" + string.Join("\n", lines) + "\n-----END PRIVATE KEY-----";
    }
}
