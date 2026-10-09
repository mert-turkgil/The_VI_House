using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Referrals;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class ReferralPayoutConfiguration : IEntityTypeConfiguration<ReferralPayout>
{
    public void Configure(EntityTypeBuilder<ReferralPayout> builder)
    {
        builder.ToTable("ReferralPayouts");
        builder.Property(p => p.Currency).HasMaxLength(3).IsRequired();
        builder.Property(p => p.Reference).HasMaxLength(100);
        builder.Property(p => p.Note).HasMaxLength(500);
        builder.HasIndex(p => new { p.AmbassadorId, p.PaidAt });
        builder.HasOne<Ambassador>().WithMany().HasForeignKey(p => p.AmbassadorId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ReferralWithdrawalRequestConfiguration : IEntityTypeConfiguration<ReferralWithdrawalRequest>
{
    public void Configure(EntityTypeBuilder<ReferralWithdrawalRequest> builder)
    {
        builder.ToTable("ReferralWithdrawalRequests");
        builder.Property(r => r.Currency).HasMaxLength(3).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Note).HasMaxLength(500);
        builder.Property(r => r.DecisionNote).HasMaxLength(500);
        builder.HasIndex(r => new { r.Status, r.RequestedAt });

        // One open request per influencer and currency — enforced here as well as in the service,
        // so a double click cannot open two.
        builder.HasIndex(r => new { r.AmbassadorId, r.Currency }).IsUnique().HasFilter("[Status] = 'Open'");

        builder.HasOne<Ambassador>().WithMany().HasForeignKey(r => r.AmbassadorId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ReferralPayout>().WithMany().HasForeignKey(r => r.PayoutId).OnDelete(DeleteBehavior.NoAction);
    }
}
