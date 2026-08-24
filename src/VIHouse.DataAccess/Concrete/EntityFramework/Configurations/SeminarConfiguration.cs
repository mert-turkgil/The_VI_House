using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VIHouse.Entities.Seminars;

namespace VIHouse.DataAccess.Concrete.EntityFramework.Configurations;

public class SeminarConfiguration : IEntityTypeConfiguration<Seminar>
{
    public void Configure(EntityTypeBuilder<Seminar> builder)
    {
        builder.ToTable("Seminars");

        builder.HasIndex(s => s.Slug).IsUnique();

        // Covers the public listing, which filters on Status + Visibility and orders by PublishedAt.
        builder.HasIndex(s => new { s.Status, s.Visibility, s.PublishedAt });

        builder.Property(s => s.Slug).HasMaxLength(200).IsRequired();
        builder.Property(s => s.HostName).HasMaxLength(150);
        builder.Property(s => s.HostTitle).HasMaxLength(150);
        builder.Property(s => s.Location).HasMaxLength(200);
        builder.Property(s => s.TimeZoneId).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Currency).HasMaxLength(3).IsRequired();

        // Owned collections of the Seminar aggregate — cascade is safe because this is the only FK
        // path into either table. SeminarEnrollment is deliberately NOT in this set: it references
        // a user and an amount of money, and must survive its seminar being deleted.
        builder.HasMany(s => s.Translations)
            .WithOne()
            .HasForeignKey(t => t.SeminarId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Media)
            .WithOne()
            .HasForeignKey(m => m.SeminarId)
            .OnDelete(DeleteBehavior.Cascade);

        // CoverMediaId is deliberately left as a plain nullable Guid with no FK constraint. A real
        // one would be circular (Seminar -> SeminarMedia -> Seminar) and would fight the cascade
        // delete above; SeminarService clears the pointer when the chosen image is removed, and the
        // read side treats a dangling id as "no cover" rather than an error.
    }
}
