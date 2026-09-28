using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VIHouse.Business.Concrete;

/// <summary>Small string helpers that several services and controllers used to each keep a copy of.</summary>
public static class Text
{
    /// <summary>Trimmed, or null when blank — how optional form fields are stored.</summary>
    public static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>At most <paramref name="max"/> characters, for columns with a length limit.</summary>
    [return: NotNullIfNotNull(nameof(value))]
    public static string? Clip(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];

    /// <summary>Lower-case hex SHA-256 of the UTF-8 bytes.</summary>
    public static string Sha256Hex(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static bool IsValidJson(string value)
    {
        try
        {
            using var _ = JsonDocument.Parse(value);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
