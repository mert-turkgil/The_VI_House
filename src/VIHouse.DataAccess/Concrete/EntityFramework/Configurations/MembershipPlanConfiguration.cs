using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Membership;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class MembershipPlanConfiguration : IEntityTypeConfiguration<MembershipPlan>
{
    public void Configure(EntityTypeBuilder<MembershipPlan> builder)
    {
        builder.ToTable("MembershipPlans");

        builder.Property(p => p.Name).HasMaxLength(150).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(1000);
        builder.Property(p => p.Currency).HasMaxLength(3).IsRequired();
        builder.Property(p => p.Features).HasMaxLength(2000);

        builder.Property(p => p.ProviderProductId).HasMaxLength(100);
        builder.Property(p => p.ProviderPriceId).HasMaxLength(100);
        builder.Property(p => p.ProviderSyncError).HasMaxLength(1000);

        // A provider id belongs to exactly one plan; an import that finds a product already
        // mirrored here must attach to that row, never create a twin.
        builder.HasIndex(p => p.ProviderProductId).IsUnique().HasFilter("[ProviderProductId] IS NOT NULL");
    }
}
