using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Referrals;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class ReferralVisitConfiguration : IEntityTypeConfiguration<ReferralVisit>
{
    public void Configure(EntityTypeBuilder<ReferralVisit> builder)
    {
        builder.ToTable("ReferralVisits");

        builder.Property(v => v.UtmSource).HasMaxLength(100);
        builder.Property(v => v.UtmMedium).HasMaxLength(100);
        builder.Property(v => v.UtmCampaign).HasMaxLength(100);
        builder.Property(v => v.UtmContent).HasMaxLength(100);
        builder.Property(v => v.TargetKind).HasConversion<string>().HasMaxLength(20);
        builder.Property(v => v.LandingPath).HasMaxLength(200);
        builder.HasIndex(v => new { v.AmbassadorId, v.TargetKind, v.TargetId });

        builder.HasOne<Ambassador>().WithMany().HasForeignKey(v => v.AmbassadorId).OnDelete(DeleteBehavior.Cascade);
    }
}
