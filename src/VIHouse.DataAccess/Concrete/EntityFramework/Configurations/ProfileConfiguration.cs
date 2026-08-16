using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.DataAccess.Identity;
using VIHouse.Entities.Users;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class ProfileConfiguration : IEntityTypeConfiguration<Profile>
{
    public void Configure(EntityTypeBuilder<Profile> builder)
    {
        builder.ToTable("Profiles");
        builder.HasKey(p => p.UserId);

        // No navigation property on Profile (Entities must stay Identity-agnostic), but the FK
        // constraint itself is still enforced at the database level here, since VIHouseDbContext
        // already depends on ApplicationUser.
        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<Profile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(p => p.CompanyName).HasMaxLength(200);
        builder.Property(p => p.JobTitle).HasMaxLength(200);
        builder.Property(p => p.Industry).HasMaxLength(100);
        builder.Property(p => p.LinkedInUrl).HasMaxLength(500);
        builder.Property(p => p.InstagramHandle).HasMaxLength(100);
        builder.Property(p => p.WebsiteUrl).HasMaxLength(500);
    }
}
