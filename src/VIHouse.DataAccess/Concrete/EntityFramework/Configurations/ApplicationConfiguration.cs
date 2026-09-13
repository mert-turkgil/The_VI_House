using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Applications;
using VIHouse.Entities.Experiences;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class ApplicationConfiguration : IEntityTypeConfiguration<Application>
{
    public void Configure(EntityTypeBuilder<Application> builder)
    {
        builder.ToTable("Applications");

        // Restrict, not Cascade: an Experience should never be deletable in a way that silently
        // wipes out Application history — admins archive/close experiences instead of deleting
        // them once applications exist against them.
        builder.HasOne<Experience>()
            .WithMany()
            .HasForeignKey(a => a.ExperienceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.ExperienceId, a.Status });
        builder.HasIndex(a => a.Email);

        builder.Property(a => a.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(a => a.LastName).HasMaxLength(100).IsRequired();
        builder.Property(a => a.Email).HasMaxLength(320).IsRequired();
        builder.Property(a => a.Country).HasMaxLength(2).IsRequired();
        builder.Property(a => a.Phone).HasMaxLength(32);
        builder.Property(a => a.JobTitle).HasMaxLength(200);
        builder.Property(a => a.AddressLine1).HasMaxLength(200);
        builder.Property(a => a.AddressLine2).HasMaxLength(200);
        builder.Property(a => a.PostalCode).HasMaxLength(20);
        builder.Property(a => a.City).HasMaxLength(100);
        builder.Property(a => a.AboutStatement).HasMaxLength(2000);
        builder.Property(a => a.ExpectationsStatement).HasMaxLength(2000);
        builder.Property(a => a.EarningsBand).HasMaxLength(20);
        builder.Property(a => a.ReferralCode).HasMaxLength(40);

        builder.HasMany(a => a.Tags)
            .WithOne()
            .HasForeignKey(t => t.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
