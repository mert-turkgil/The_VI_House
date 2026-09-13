namespace VIHouse.Entities.Users;

/// <summary>
/// The annual-earnings ranges a member picks from, in place of the free-text revenue figure that
/// used to be asked for. A band rather than a number on purpose: nobody wants to type their
/// turnover into a form, and a range is all the House needs to seat the right people together.
///
/// Stored as the <see cref="Band.Code"/> (a short, stable, culture-neutral string) so the label can
/// be reworded without touching a row. Ordered, so the dropdown and any reporting read low to high.
/// </summary>
public static class EarningsBand
{
    public sealed record Band(string Code, string Label);

    public static readonly IReadOnlyList<Band> All =
    [
        new("0-75k", "0 – 75k"),
        new("75-150k", "75k – 150k"),
        new("150-300k", "150k – 300k"),
        new("300-600k", "300k – 600k"),
        new("600k-1m", "600k – 1m"),
        new("1-3m", "1m – 3m"),
        new("3-6m", "3m – 6m"),
        new("6m+", "6m+"),
    ];

    public static bool IsValid(string? code) =>
        !string.IsNullOrWhiteSpace(code) && All.Any(b => b.Code == code);

    /// <summary>The display label for a stored code, or the code itself for one that is no longer
    /// in the list — an old answer is still an answer.</summary>
    public static string? LabelFor(string? code) =>
        code is null ? null : All.FirstOrDefault(b => b.Code == code)?.Label ?? code;
}
