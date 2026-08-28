using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Experiences;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class ExperienceConfiguration : IEntityTypeConfiguration<Experience>
{
    public void Configure(EntityTypeBuilder<Experience> builder)
    {
        builder.ToTable("Experiences");
        builder.HasIndex(e => e.Slug).IsUnique();
        builder.HasIndex(e => new { e.Status, e.StartAtUtc });

        builder.Property(e => e.Slug).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
        builder.Property(e => e.City).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Country).HasMaxLength(2).IsRequired(); // ISO 3166-1 alpha-2
        builder.Property(e => e.TimeZoneId).HasMaxLength(100).IsRequired();
        builder.Property(e => e.CoverImageAlt).HasMaxLength(300);
        builder.Property(e => e.AudienceTags).HasMaxLength(300);

        // Owned collections of the Experience aggregate root — cascade delete is safe here since
        // this is the only FK path into each child table (no other entity FKs into these).
        builder.HasMany(e => e.TicketTypes)
            .WithOne()
            .HasForeignKey(t => t.ExperienceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.ProgramDays)
            .WithOne()
            .HasForeignKey(d => d.ExperienceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Inclusions)
            .WithOne()
            .HasForeignKey(i => i.ExperienceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Faqs)
            .WithOne()
            .HasForeignKey(f => f.ExperienceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Gallery)
            .WithOne()
            .HasForeignKey(g => g.ExperienceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
