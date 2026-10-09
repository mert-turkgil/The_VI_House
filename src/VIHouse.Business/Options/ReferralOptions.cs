namespace VIHouse.Business.Options;

/// <summary>The "Referrals" configuration section. Non-secret.</summary>
public class ReferralOptions
{
    /// <summary>
    /// The smallest owed balance an influencer can ask to be paid, in minor units of whichever
    /// currency (5000 = £50 / €50 / $50). Below it a bank transfer costs more effort than it is worth.
    /// </summary>
    public long MinimumWithdrawalMinor { get; set; } = 5000;
}
