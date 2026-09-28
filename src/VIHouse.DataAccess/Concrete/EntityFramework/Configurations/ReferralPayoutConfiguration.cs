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
