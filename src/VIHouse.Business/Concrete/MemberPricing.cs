namespace VIHouse.Business.Concrete;

/// <summary>The one place a member discount percentage turns into a price, so tickets and
/// sessions round the same way and the figure shown before checkout is the figure charged.</summary>
public static class MemberPricing
{
    public static long Apply(long baseAmountMinor, int discountPercent)
    {
        if (discountPercent <= 0) return baseAmountMinor;
        var pct = Math.Min(100, discountPercent);
        // Round half up on the discount, so £99.99 at 10% off is £89.99 and never a fraction of a penny.
        var off = (long)Math.Round(baseAmountMinor * pct / 100m, MidpointRounding.AwayFromZero);
        return Math.Max(0, baseAmountMinor - off);
    }

    /// <summary>The member discount and the Founder extra as one percentage, capped at 100. Added,
    /// not compounded: "10% member + 5% Founder" reads as 15% off, so it is 15% off.</summary>
    public static int Combined(int memberDiscountPercent, int founderExtraPercent) =>
        Math.Clamp(Math.Max(0, memberDiscountPercent) + Math.Max(0, founderExtraPercent), 0, 100);

    public static long ApplyStacked(long baseAmountMinor, int memberDiscountPercent, int founderExtraPercent) =>
        Apply(baseAmountMinor, Combined(memberDiscountPercent, founderExtraPercent));
}
