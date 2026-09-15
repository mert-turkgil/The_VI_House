using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Referrals;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class ReferralConversionConfiguration : IEntityTypeConfiguration<ReferralConversion>
{
    public void Configure(EntityTypeBuilder<ReferralConversion> builder)
    {
        builder.ToTable("ReferralConversions");
        builder.Property(c => c.Currency).HasMaxLength(3);
        builder.Property(c => c.SourceEntityType).HasMaxLength(50).IsRequired();
        builder.Property(c => c.Kind).HasConversion<string>().HasMaxLength(30);
        // One conversion per source row per step: a webhook delivered twice, or an approval
        // clicked twice, must not become two lines on the ambassador's timeline.
        builder.HasIndex(c => new { c.SourceEntityType, c.SourceEntityId, c.Kind }).IsUnique();
        builder.HasIndex(c => new { c.AmbassadorId, c.OccurredAt });
        builder.HasOne<Ambassador>().WithMany().HasForeignKey(c => c.AmbassadorId).OnDelete(DeleteBehavior.Cascade);
    }
}
