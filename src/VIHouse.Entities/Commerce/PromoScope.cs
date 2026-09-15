namespace VIHouse.Entities.Commerce;

/// <summary>What a promo code can be spent on.</summary>
public enum PromoScope
{
    /// <summary>Experience tickets, the original use — optionally narrowed to one experience.</summary>
    Experiences,

    /// <summary>Membership checkout — optionally narrowed to one plan.</summary>
    Memberships,
}

/// <summary>For a membership code: how long the discount lasts on a recurring plan.</summary>
public enum PromoDuration
{
    /// <summary>The first payment only; renewals bill at full price.</summary>
    FirstPayment,

    /// <summary>Every renewal, for as long as the subscription runs.</summary>
    Forever,
}
