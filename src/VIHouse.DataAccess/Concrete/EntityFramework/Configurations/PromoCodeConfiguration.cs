using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Commerce;
using VIHouse.Entities.Experiences;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class PromoCodeConfiguration : IEntityTypeConfiguration<PromoCode>
{
    public void Configure(EntityTypeBuilder<PromoCode> builder)
    {
        builder.ToTable("PromoCodes");
        builder.HasIndex(p => p.Code).IsUnique();
        builder.Property(p => p.Code).HasMaxLength(50).IsRequired();
        builder.Property(p => p.RestrictedToEmail).HasMaxLength(320);
        builder.Property(p => p.ProviderCouponId).HasMaxLength(100);

        builder.HasOne<Experience>().WithMany().HasForeignKey(p => p.ExperienceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.RestrictedToUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
