namespace VIHouse.Business.Concrete;

public static class MemberNumbers
{
    /// <summary>"VIH-3F2A9C1D" — the short number printed on the member card and in emails,
    /// derived from the membership id so it never needs storing.</summary>
    public static string For(Guid membershipId) => $"VIH-{membershipId:N}"[..12].ToUpperInvariant();
}
