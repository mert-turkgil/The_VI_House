using VIHouse.Business.Abstract;
using VIHouse.Business.Concrete;

namespace VIHouse.Tests;

public class MemberPricingTests
{
    [Theory]
    [InlineData(10_000, 10, 0, 9_000)]
    [InlineData(10_000, 10, 5, 8_500)]   // member + Founder add up, they do not compound
    [InlineData(10_000, 0, 15, 8_500)]   // a Founder extra on an item with no member discount
    [InlineData(10_000, 80, 40, 0)]      // capped at 100%, never negative
    [InlineData(9_999, 10, 0, 8_999)]    // half-up on the discount, as MemberPricing.Apply always has
    public void ApplyStacked_adds_member_and_founder_discounts(long baseMinor, int member, int founder, long expected) =>
        Assert.Equal(expected, MemberPricing.ApplyStacked(baseMinor, member, founder));

    [Fact]
    public void Combined_ignores_negative_inputs() =>
        Assert.Equal(10, MemberPricing.Combined(10, -5));
}

public class FounderPerksTests
{
    private static readonly DateTimeOffset Open = new(2026, 11, 1, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Founder_with_early_access_gets_in_early() =>
        Assert.Equal(Open.AddDays(-3), new FounderPerks(true, 0, 3, false).OpensAtFor(Open));

    [Fact]
    public void Non_founder_waits_for_members_open() =>
        Assert.Equal(Open, FounderPerks.None.OpensAtFor(Open));

    [Fact]
    public void No_open_date_means_no_wait() =>
        Assert.Null(new FounderPerks(true, 0, 3, false).OpensAtFor(null));

    [Fact]
    public void Extra_discount_only_applies_to_founders()
    {
        Assert.Equal(5, new FounderPerks(true, 5, 0, false).ExtraDiscount);
        Assert.Equal(0, new FounderPerks(false, 5, 0, false).ExtraDiscount);
    }

    [Fact]
    public void Window_is_open_until_its_end()
    {
        var programme = new FounderProgramme(Open, 0, 0, false);
        Assert.True(programme.IsWindowOpen(Open.AddMinutes(-1)));
        Assert.False(programme.IsWindowOpen(Open));
        Assert.False(new FounderProgramme(null, 0, 0, false).IsWindowOpen(Open));
    }
}
